using System;
using System.Collections;
using UnityEngine;
using unityroom.Api; // ← 追加: unityroom APIを使用するための宣言

public sealed class GameManager : MonoBehaviour
{
    public const string NormalClearPrefsKey = "GameManager.Clear.Normal";
    public const string HardClearPrefsKey = "GameManager.Clear.Hard";
    public const string SelectedDifficultyPrefsKey = "GameManager.SelectedDifficulty";
    public const string HasPlayedOncePrefsKey = "GameManager.HasPlayedOnce";

    [SerializeField] private float defaultTimeLimitSeconds = 180f;
    [SerializeField] private float clearPercentageThreshold = 99.0f;
    private const float ResultCountUpDurationSeconds = 2f;


    [SerializeField] private float cameraBlendDuration = 2f;
    private bool isTransitioningWithoutFade = false;

    private string[] tutorialLines;

    [Header("Core References")]
    [SerializeField] private FadeManager fadeManager;
    [SerializeField] private Whiteboard whiteboard;
    [SerializeField] private TitleUI titleUI;
    [SerializeField] private TutorialUI tutorialUI;
    [SerializeField] private InGameUI inGameUI;
    [SerializeField] private ResultUI resultUI;

    // --- ここから追加: unityroom Ranking Settings ---
    [Header("unityroom Ranking")]
    [SerializeField] private int normalBoardNo = 1;       // ノーマルモード用のボードNo
    [SerializeField] private int impossibleBoardNo = 2;   // インポッシブルモード用のボードNo
    // --- ここまで追加 ---

    // --- ここから追加: PenEraserの生成設定 ---
    [Header("PenEraser Settings")]
    [SerializeField] private GameObject normalPenEraserPrefab;
    [SerializeField] private GameObject hardPenEraserPrefab;
    [SerializeField] private GameObject impossiblePenEraserPrefab;
    [SerializeField] private GameObject mainCinemachineCamera;
    [SerializeField] private Transform penEraserSpawnPoint;

    private GameObject currentPenEraserInstance;
    // --- ここまで追加 ---

    [Header("Audio")]
    [SerializeField] private AudioClip titleBgm;
    [SerializeField] private AudioClip tutorialBgm;
    [SerializeField] private AudioClip inGameBgm;
    [SerializeField] private AudioClip resultBgm;
    [SerializeField] private AudioClip gameStartSe;
    [SerializeField] private AudioClip countdownSe;
    [SerializeField] private AudioClip timeUpSe;
    [SerializeField] private AudioClip resultCountSe;
    [SerializeField] private AudioClip resultFinalSe;

    public GameState CurrentState { get; private set; } = GameState.Title;
    public DifficultyMode CurrentDifficulty { get; private set; } = DifficultyMode.Normal;
    public float TimeLimitSeconds { get; private set; }
    public float RemainingTimeSeconds { get; private set; }
    public float WhitePercentage { get; private set; }
    public bool IsNormalCleared { get; private set; }
    public bool IsHardCleared { get; private set; }
    public bool IsHardUnlocked => IsNormalCleared;
    public bool IsImpossibleUnlocked => IsHardCleared;
    public bool HasPlayedOnce { get; private set; }

    public event Action<GameState> StateChanged;
    public event Action<GameState, GameState> StateTransitionRequested;
    public event Action<DifficultyMode> DifficultyChanged;
    public event Action<float> RemainingTimeChanged;
    public event Action<float> WhitePercentageChanged;
    public event Action<bool> ClearStateChanged;

    private Coroutine stateTransitionCoroutine;
    private Coroutine gameTimerCoroutine;
    private Coroutine resultCountUpCoroutine;
    private int tutorialStepIndex;
    private int lastCountdownSecondPlayed = int.MaxValue;
    private bool lastRunCleared;
    private float resultTargetPercentage;
    private bool isGamePlaying = false;

    private void Awake()
    {
        tutorialLines = new string[]
                {
            "ホワイトボード消しにペンを固定すれば、描画と消去を同時に行えるのではないか？",
            "始まりは、ちょっとした思いつきだった。",
            "固定するのに瞬間接着剤を使ったら、取れなくなった。",
            "予備のホワイトボード消しはない。",
            "そして、最悪のタイミングで急用の連絡が入った。",
            "『今すぐホワイトボードを使って説明してくれ』",
            "制限時間内にホワイトボードを消そう。",
            $"クリア条件：{clearPercentageThreshold}%以上白くする"
                };

        LoadProgress();
        ApplyInitialState();
    }

    private void OnEnable()
    {
        SubscribeUiEvents();
    }

    private void OnDisable()
    {
        StopStateRoutines();
        UnsubscribeUiEvents();
    }

    private void Update()
    {
        if (stateTransitionCoroutine != null)
        {
            return;
        }
        if (CurrentState == GameState.InGame && isGamePlaying)
        {
            RefreshWhitePercentage();
        }
    }

    public void StartTitleFlow()
    {
        TransitionToState(GameState.Title);
    }

    public void StartTutorialFlow()
    {
        TransitionToState(GameState.Tutorial);
    }

    public void StartInGameFlow(bool skipFade = false)
    {
        isTransitioningWithoutFade = skipFade;
        TransitionToState(GameState.InGame, skipFade);
    }

    public void StartResultFlow()
    {
        TransitionToState(GameState.Result);
    }

    public void RequestStartGame()
    {
        SelectDifficulty(DifficultyMode.Normal);
        StartTutorialFlow();
    }

    public void RequestAdvanceTutorial()
    {
        if (CurrentState != GameState.Tutorial)
        {
            return;
        }

        if (tutorialLines.Length == 0)
        {
            tutorialUI?.InvokeFinished();
            return;
        }

        if (tutorialStepIndex >= tutorialLines.Length - 1)
        {
            tutorialUI?.InvokeFinished();
            return;
        }

        tutorialStepIndex++;
        UpdateTutorialLine();
    }

    public void RequestFinishTutorial()
    {
        if (CurrentState != GameState.Tutorial)
        {
            return;
        }

        StartInGameFlow(true);
    }

    public void RequestRetry()
    {
        StartInGameFlow();
    }

    public void RequestReturnToTitle()
    {
        StartTitleFlow();
    }

    public void RequestStartDifficulty(DifficultyMode difficultyMode)
    {
        if (!IsDifficultyAvailable(difficultyMode))
        {
            return;
        }

        SelectDifficulty(difficultyMode);
        StartInGameFlow();
    }

    public void SelectDifficulty(DifficultyMode difficultyMode)
    {
        DifficultyMode normalizedDifficulty = NormalizeDifficultySelection(difficultyMode);
        if (CurrentDifficulty == normalizedDifficulty)
        {
            return;
        }

        CurrentDifficulty = normalizedDifficulty;
        DifficultyChanged?.Invoke(CurrentDifficulty);
        SaveProgress();
    }

    public void SetWhitePercentage(float whitePercentage)
    {
        float normalizedPercentage = Mathf.Clamp(whitePercentage, 0f, 100f);
        if (Mathf.Approximately(WhitePercentage, normalizedPercentage))
        {
            return;
        }

        WhitePercentage = normalizedPercentage;
        WhitePercentageChanged?.Invoke(WhitePercentage);

        if (inGameUI != null && CurrentState == GameState.InGame)
        {
            inGameUI.SetWhitePercentage(WhitePercentage);
        }
    }

    public void RefreshWhitePercentage()
    {
        if (whiteboard == null)
        {
            return;
        }

        SetWhitePercentage(whiteboard.GetWhitePercentage());

        if (CurrentState == GameState.InGame && WhitePercentage >= clearPercentageThreshold)
        {
            NotifyGameClear();
        }
    }

    private void NotifyGameClear()
    {
        if (CurrentState != GameState.InGame)
        {
            return;
        }
        isGamePlaying = false;

        if (gameTimerCoroutine != null)
        {
            StopCoroutine(gameTimerCoroutine);
            gameTimerCoroutine = null;
        }

        StartResultFlow();
    }

    public void NotifyTimeExpired()
    {
        if (CurrentState != GameState.InGame)
        {
            return;
        }
        isGamePlaying = false;

        if (gameTimerCoroutine != null)
        {
            StopCoroutine(gameTimerCoroutine);
            gameTimerCoroutine = null;
        }

        PlaySe(timeUpSe);
        StartResultFlow();
    }

    public void MarkClear(DifficultyMode difficultyMode)
    {
        switch (difficultyMode)
        {
            case DifficultyMode.Normal:
                IsNormalCleared = true;
                break;
            case DifficultyMode.Hard:
            case DifficultyMode.Impossible:
                IsNormalCleared = true;
                IsHardCleared = true;
                break;
        }

        ClearStateChanged?.Invoke(true);
        SaveProgress();
    }

    // --- ここから追加: クリア時間のスコア送信処理 ---
    private void SendClearTimeScore()
    {
        // クリアタイム(秒)を計算
        float clearTime = TimeLimitSeconds - RemainingTimeSeconds;

        if (CurrentDifficulty == DifficultyMode.Normal)
        {
            // ボードNo1にノーマルのクリア時間を送信 (時間が短いほど上位のため HighScoreAsc を指定)
            UnityroomApiClient.Instance.SendScore(normalBoardNo, clearTime, ScoreboardWriteMode.HighScoreAsc);
        }
        else if (CurrentDifficulty == DifficultyMode.Impossible)
        {
            // ボードNo2にインポッシブルのクリア時間を送信
            UnityroomApiClient.Instance.SendScore(impossibleBoardNo, clearTime, ScoreboardWriteMode.HighScoreAsc);
        }
    }
    // --- ここまで追加 ---

    public bool IsDifficultyAvailable(DifficultyMode difficultyMode)
    {
        return difficultyMode switch
        {
            DifficultyMode.Normal => true,
            DifficultyMode.Hard => IsHardUnlocked,
            DifficultyMode.Impossible => IsImpossibleUnlocked,
            _ => false,
        };
    }

    public void LoadProgress()
    {
        IsNormalCleared = PlayerPrefs.GetInt(NormalClearPrefsKey, 0) != 0;
        IsHardCleared = PlayerPrefs.GetInt(HardClearPrefsKey, 0) != 0;
        HasPlayedOnce = PlayerPrefs.GetInt(HasPlayedOncePrefsKey, 0) != 0;

        int savedDifficulty = PlayerPrefs.GetInt(SelectedDifficultyPrefsKey, (int)DifficultyMode.Normal);
        CurrentDifficulty = NormalizeDifficultySelection((DifficultyMode)savedDifficulty);

        ClearStateChanged?.Invoke(IsNormalCleared || IsHardCleared);
    }

    public void SaveProgress()
    {
        PlayerPrefs.SetInt(NormalClearPrefsKey, IsNormalCleared ? 1 : 0);
        PlayerPrefs.SetInt(HardClearPrefsKey, IsHardCleared ? 1 : 0);
        PlayerPrefs.SetInt(SelectedDifficultyPrefsKey, (int)CurrentDifficulty);
        PlayerPrefs.SetInt(HasPlayedOncePrefsKey, HasPlayedOnce ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyInitialState()
    {
        CurrentState = GameState.Title;
        TimeLimitSeconds = defaultTimeLimitSeconds;
        RemainingTimeSeconds = TimeLimitSeconds;
        WhitePercentage = whiteboard != null ? whiteboard.GetWhitePercentage() : 0f;
        tutorialStepIndex = 0;
        lastCountdownSecondPlayed = int.MaxValue;
        lastRunCleared = false;
        resultTargetPercentage = 0f;

        if (fadeManager != null)
        {
            fadeManager.SetImmediateVisible(false);
        }

        ShowOnlyTitleUi();
        StateChanged?.Invoke(CurrentState);
        RefreshResultUnlockButtons(false);
    }

    private void TransitionToState(GameState nextState, bool skipFade = false)
    {
        if (stateTransitionCoroutine != null)
        {
            StopCoroutine(stateTransitionCoroutine);
        }

        stateTransitionCoroutine = StartCoroutine(TransitionRoutine(nextState, skipFade));
    }

    private IEnumerator TransitionRoutine(GameState nextState, bool skipFade)
    {
        StateTransitionRequested?.Invoke(CurrentState, nextState);

        if (fadeManager != null && !skipFade)
        {
            yield return fadeManager.FadeTransition(() => EnterState(nextState), null);
        }
        else
        {
            EnterState(nextState);
        }

        stateTransitionCoroutine = null;
    }

    private IEnumerator TransitionRoutine(GameState nextState)
    {
        StateTransitionRequested?.Invoke(CurrentState, nextState);

        if (fadeManager != null)
        {
            yield return fadeManager.FadeTransition(() => EnterState(nextState), null);
        }
        else
        {
            EnterState(nextState);
        }

        stateTransitionCoroutine = null;
    }

    private void EnterState(GameState nextState)
    {
        StopStateRoutines();
        HideAllUi();

        CurrentState = nextState;
        StateChanged?.Invoke(CurrentState);

        switch (nextState)
        {
            case GameState.Title:
                EnterTitleState();
                break;
            case GameState.Tutorial:
                EnterTutorialState();
                break;
            case GameState.InGame:
                EnterInGameState();
                break;
            case GameState.Result:
                EnterResultState();
                break;
        }
    }

    private void EnterTitleState()
    {
        CleanupPenEraser(); 

        PlayBgm(titleBgm);

        mainCinemachineCamera.SetActive(true);

        if (titleUI != null)
        {
            titleUI.Show();
            titleUI.SetInteractable(true);
            titleUI.UpdateDifficultyButtons(HasPlayedOnce, IsHardUnlocked, IsImpossibleUnlocked);
        }
    }

    private void EnterTutorialState()
    {
        PlayBgm(tutorialBgm);
        tutorialStepIndex = 0;

        mainCinemachineCamera.SetActive(false);

        if (tutorialUI != null)
        {
            tutorialUI.Show();
            tutorialUI.ResetStory();

            if (tutorialLines.Length > 0)
            {
                tutorialUI.SetDialogue(tutorialLines[0]);
                tutorialUI.SetStep(1, tutorialLines.Length);
            }
        }
    }
    private void EnterInGameState()
    {
        PlayBgm(inGameBgm);

        mainCinemachineCamera.SetActive(true);

        if (!HasPlayedOnce)
        {
            HasPlayedOnce = true;
            SaveProgress();
        }

        if (whiteboard != null)
        {
            whiteboard.ClearBoard();
        }
        TimeLimitSeconds = defaultTimeLimitSeconds;

        RemainingTimeSeconds = TimeLimitSeconds;
        lastCountdownSecondPlayed = int.MaxValue;
        SetWhitePercentage(0f); 
        isGamePlaying = false; // 確実に追加

        if (inGameUI != null)
        {
            inGameUI.Show();
            inGameUI.SetCountdownWarningVisible(false);
            inGameUI.SetRemainingTime(RemainingTimeSeconds);
            inGameUI.SetWhitePercentage(WhitePercentage);
            inGameUI.SetClearCondition(clearPercentageThreshold);
        }

        float delay = isTransitioningWithoutFade ? cameraBlendDuration : 0f;
        gameTimerCoroutine = StartCoroutine(RunGameTimer(delay));
    }

    private void EnterResultState()
    {
        PlayBgm(resultBgm);
        resultTargetPercentage = WhitePercentage;
        lastRunCleared = resultTargetPercentage >= clearPercentageThreshold;

        if (resultUI != null)
        {
            resultUI.Show();
            resultUI.ResetDisplay();
            resultUI.SetTargetPercentage(resultTargetPercentage);
            resultUI.SetCurrentPercentage(0f);
            float clearTime = TimeLimitSeconds - RemainingTimeSeconds;
            resultUI.SetCleared(lastRunCleared, clearTime, clearPercentageThreshold);
            resultUI.SetHardButtonVisible(false);
            resultUI.SetImpossibleButtonVisible(false);
        }

        resultCountUpCoroutine = StartCoroutine(RunResultCountUp());
    }
    
    private void SpawnPenEraser()
    {
        CleanupPenEraser();

        GameObject targetPrefab = CurrentDifficulty switch
        {
            DifficultyMode.Normal => normalPenEraserPrefab,
            DifficultyMode.Hard => hardPenEraserPrefab,
            DifficultyMode.Impossible => impossiblePenEraserPrefab,
            _ => normalPenEraserPrefab
        };

        if (targetPrefab == null || penEraserSpawnPoint == null)
        {
            Debug.LogWarning("PenEraserのプレハブ、またはSpawnPointが設定されていません。");
            return;
        }

        currentPenEraserInstance = Instantiate(targetPrefab, penEraserSpawnPoint.position, penEraserSpawnPoint.rotation);

        PenTool[] penTools = currentPenEraserInstance.GetComponentsInChildren<PenTool>(true);
        foreach (PenTool pen in penTools)
        {
            pen.SetWhiteboard(whiteboard);
        }

        EraserTool[] eraserTools = currentPenEraserInstance.GetComponentsInChildren<EraserTool>(true);
        foreach (EraserTool eraser in eraserTools)
        {
            eraser.SetWhiteboard(whiteboard);
        }
    }

    private void CleanupPenEraser()
    {
        if (currentPenEraserInstance != null)
        {
            Destroy(currentPenEraserInstance);
            currentPenEraserInstance = null;
        }
    }

    private IEnumerator RunGameTimer(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        SpawnPenEraser();
        PlaySe(gameStartSe);

        isGamePlaying = true;

        while (CurrentState == GameState.InGame && RemainingTimeSeconds > 0f)
        {
            RemainingTimeSeconds = Mathf.Max(0f, RemainingTimeSeconds - Time.unscaledDeltaTime);

            if (inGameUI != null)
            {
                inGameUI.SetRemainingTime(RemainingTimeSeconds);
                inGameUI.SetCountdownWarningVisible(RemainingTimeSeconds <= 10f);
            }

            int currentCountdownSecond = Mathf.CeilToInt(RemainingTimeSeconds);
            if (currentCountdownSecond <= 10 && currentCountdownSecond > 0 && currentCountdownSecond != lastCountdownSecondPlayed)
            {
                lastCountdownSecondPlayed = currentCountdownSecond;
                PlaySe(countdownSe);

                if (inGameUI != null)
                {
                    inGameUI.PlayCountdownAnimation();
                }
            }

            yield return null;
        }

        gameTimerCoroutine = null;
        NotifyTimeExpired();
    }

    private IEnumerator RunResultCountUp()
    {
        float elapsedSeconds = 0f;
        int lastDisplayedValue = -1;
        float seInterval = 0.05f;
        float timeSinceLastSe = seInterval;

        while (elapsedSeconds < ResultCountUpDurationSeconds)
        {
            float deltaTime = Time.unscaledDeltaTime;
            elapsedSeconds += deltaTime;
            timeSinceLastSe += deltaTime;

            float normalizedTime = Mathf.Clamp01(elapsedSeconds / ResultCountUpDurationSeconds);
            float currentPercentage = Mathf.Lerp(0f, resultTargetPercentage, normalizedTime);

            if (resultUI != null)
            {
                resultUI.SetCurrentPercentage(currentPercentage);
            }

            int currentDisplayedValue = Mathf.FloorToInt(currentPercentage * 10f);
            if (currentDisplayedValue != lastDisplayedValue && elapsedSeconds < ResultCountUpDurationSeconds)
            {
                lastDisplayedValue = currentDisplayedValue;

                if (timeSinceLastSe >= seInterval)
                {
                    PlaySe(resultCountSe);
                    timeSinceLastSe = 0f;
                }
            }

            yield return null;
        }

        if (resultUI != null)
        {
            resultUI.SetCurrentPercentage(resultTargetPercentage);
        }

        if (lastRunCleared)
        {
            MarkClear(CurrentDifficulty);
            SendClearTimeScore(); // ← 追加: クリア時にスコアを送信
            
            if (resultUI != null)
            {
                resultUI.PlayClearParticle();
            }
        }

        PlaySe(resultFinalSe);
        RefreshResultUnlockButtons(lastRunCleared);
        resultCountUpCoroutine = null;
    }

    private void StopStateRoutines()
    {
        if (gameTimerCoroutine != null)
        {
            StopCoroutine(gameTimerCoroutine);
            gameTimerCoroutine = null;
        }

        if (resultCountUpCoroutine != null)
        {
            StopCoroutine(resultCountUpCoroutine);
            resultCountUpCoroutine = null;
        }

        lastCountdownSecondPlayed = int.MaxValue;
    }

    private void HideAllUi()
    {
        titleUI?.Hide();
        tutorialUI?.Hide();
        inGameUI?.Hide();
        resultUI?.Hide();
    }

    private void ShowOnlyTitleUi()
    {
        HideAllUi();
        titleUI?.Show();
        titleUI?.SetInteractable(true);
        titleUI.UpdateDifficultyButtons(HasPlayedOnce, IsHardUnlocked, IsImpossibleUnlocked);
    }

    private void UpdateTutorialLine()
    {
        if (tutorialUI == null || tutorialLines.Length == 0)
        {
            return;
        }

        int clampedIndex = Mathf.Clamp(tutorialStepIndex, 0, tutorialLines.Length - 1);
        tutorialUI.SetDialogue(tutorialLines[clampedIndex]);
        tutorialUI.SetStep(clampedIndex + 1, tutorialLines.Length);
    }

    private void RefreshResultUnlockButtons(bool cleared)
    {
        if (resultUI == null)
        {
            return;
        }
        
        bool showHard = cleared && CurrentDifficulty == DifficultyMode.Normal && IsHardUnlocked;
        bool showImpossible = cleared && CurrentDifficulty == DifficultyMode.Hard && IsImpossibleUnlocked;

        // ▼ 追加：上位ステージのボタンが出ない場合（失敗時や、最上位のImpossibleクリア時）にリトライボタンを表示する
        bool showRetry = !showHard && !showImpossible;

        resultUI.SetHardButtonVisible(showHard);
        resultUI.SetImpossibleButtonVisible(showImpossible);
        resultUI.SetRetryButtonVisible(showRetry);
    }

    private void PlayBgm(AudioClip clip)
    {
        SoundManager soundManager = SoundManager.Instance;
        if (soundManager == null)
        {
            return;
        }

        soundManager.PlayBgm(clip, true);
    }

    private void PlaySe(AudioClip clip)
    {
        SoundManager soundManager = SoundManager.Instance;
        if (soundManager == null || clip == null)
        {
            return;
        }

        soundManager.PlaySe(clip);
    }

    private DifficultyMode NormalizeDifficultySelection(DifficultyMode difficultyMode)
    {
        if (IsDifficultyAvailable(difficultyMode))
        {
            return difficultyMode;
        }

        if (IsHardUnlocked)
        {
            return DifficultyMode.Hard;
        }

        return DifficultyMode.Normal;
    }

    public void RequestTweet()
    {
        string tweetText = $"ホワイトボードを {WhitePercentage:0.0}% まで消しました！\n#ホワイトボードを消すゲーム\nhttps://unityroom.com/games/whiteboard-eraser";
        string tweetUrl = "https://twitter.com/intent/tweet?text=" + Uri.EscapeDataString(tweetText);
        Application.OpenURL(tweetUrl);
    }
    private void RequestStartNormal() => RequestStartDifficulty(DifficultyMode.Normal);
    private void RequestStartHard() => RequestStartDifficulty(DifficultyMode.Hard);
    private void RequestStartImpossible() => RequestStartDifficulty(DifficultyMode.Impossible);

    private void SubscribeUiEvents()
    {
        if (titleUI != null)
        {
            titleUI.StartRequested += RequestStartGame;
            titleUI.NormalRequested += RequestStartNormal;
            titleUI.HardRequested += RequestStartHard;
            titleUI.ImpossibleRequested += RequestStartImpossible;
        }

        if (tutorialUI != null)
        {
            tutorialUI.AdvanceRequested += RequestAdvanceTutorial;
            tutorialUI.Finished += RequestFinishTutorial;
        }

        if (resultUI != null)
        {
            resultUI.RetryRequested += RequestRetry;
            resultUI.TitleRequested += RequestReturnToTitle;
            resultUI.TweetRequested += RequestTweet;
            resultUI.DifficultyRequested += RequestStartDifficulty;
        }
    }

    private void UnsubscribeUiEvents()
    {
        if (titleUI != null)
        {
            titleUI.StartRequested -= RequestStartGame;
            titleUI.NormalRequested -= RequestStartNormal;
            titleUI.HardRequested -= RequestStartHard;
            titleUI.ImpossibleRequested -= RequestStartImpossible;
        }

        if (tutorialUI != null)
        {
            tutorialUI.AdvanceRequested -= RequestAdvanceTutorial;
            tutorialUI.Finished -= RequestFinishTutorial;
        }

        if (resultUI != null)
        {
            resultUI.RetryRequested -= RequestRetry;
            resultUI.TitleRequested -= RequestReturnToTitle;
            resultUI.TweetRequested -= RequestTweet;
            resultUI.DifficultyRequested -= RequestStartDifficulty;
        }
    }
}