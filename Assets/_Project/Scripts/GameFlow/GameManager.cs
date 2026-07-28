using System;
using System.Collections;
using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    public const string NormalClearPrefsKey = "GameManager.Clear.Normal";
    public const string HardClearPrefsKey = "GameManager.Clear.Hard";
    public const string SelectedDifficultyPrefsKey = "GameManager.SelectedDifficulty";
    public const string HasPlayedOncePrefsKey = "GameManager.HasPlayedOnce";

    [SerializeField] private float defaultTimeLimitSeconds = 180f;
    [SerializeField] private float clearPercentageThreshold = 99.0f;
    private const float ResultCountUpDurationSeconds = 2f;

    private static readonly string[] TutorialLines =
    {
        "ある日、ホワイトボード消しにペンをつけるとどうなるか興味がわいた。",
        "瞬間接着剤でくっつけたら取れなくなった。",
        "連絡が来てすぐホワイトボードを使う必要が出た。",
        "制限時間内に落書きを全部消す。",
    };

    [Header("Core References")]
    [SerializeField] private FadeManager fadeManager;
    [SerializeField] private Whiteboard whiteboard;
    [SerializeField] private TitleUI titleUI;
    [SerializeField] private TutorialUI tutorialUI;
    [SerializeField] private InGameUI inGameUI;
    [SerializeField] private ResultUI resultUI;

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

    private void Awake()
    {
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
        if (CurrentState == GameState.InGame)
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

    public void StartInGameFlow()
    {
        TransitionToState(GameState.InGame);
    }

    public void StartResultFlow()
    {
        TransitionToState(GameState.Result);
    }

    public void RequestStartGame()
    {
        StartTutorialFlow();
    }

    public void RequestAdvanceTutorial()
    {
        if (CurrentState != GameState.Tutorial)
        {
            return;
        }

        if (TutorialLines.Length == 0)
        {
            tutorialUI?.InvokeFinished();
            return;
        }

        if (tutorialStepIndex >= TutorialLines.Length - 1)
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

        StartInGameFlow();
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

        // 追加: 99%を超えたらその時点でクリアとしてリザルトへ遷移する
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

        // タイマーを止める
        if (gameTimerCoroutine != null)
        {
            StopCoroutine(gameTimerCoroutine);
            gameTimerCoroutine = null;
        }

        // タイムアップSEの代わりに、クリア用のSEを鳴らす場合はここに処理を追加できます
        // PlaySe(gameClearSe);

        StartResultFlow();
    }

    public void NotifyTimeExpired()
    {
        if (CurrentState != GameState.InGame)
        {
            return;
        }

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
        HasPlayedOnce = PlayerPrefs.GetInt(HasPlayedOncePrefsKey, 0) != 0; // 追加

        int savedDifficulty = PlayerPrefs.GetInt(SelectedDifficultyPrefsKey, (int)DifficultyMode.Normal);
        CurrentDifficulty = NormalizeDifficultySelection((DifficultyMode)savedDifficulty);

        ClearStateChanged?.Invoke(IsNormalCleared || IsHardCleared);
    }

    public void SaveProgress()
    {
        PlayerPrefs.SetInt(NormalClearPrefsKey, IsNormalCleared ? 1 : 0);
        PlayerPrefs.SetInt(HardClearPrefsKey, IsHardCleared ? 1 : 0);
        PlayerPrefs.SetInt(SelectedDifficultyPrefsKey, (int)CurrentDifficulty);
        PlayerPrefs.SetInt(HasPlayedOncePrefsKey, HasPlayedOnce ? 1 : 0); // 追加
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

    private void TransitionToState(GameState nextState)
    {
        if (stateTransitionCoroutine != null)
        {
            StopCoroutine(stateTransitionCoroutine);
        }

        stateTransitionCoroutine = StartCoroutine(TransitionRoutine(nextState));
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
        PlayBgm(titleBgm);

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

        if (tutorialUI != null)
        {
            tutorialUI.Show();
            tutorialUI.ResetStory();

            if (TutorialLines.Length > 0)
            {
                tutorialUI.SetDialogue(TutorialLines[0]);
                tutorialUI.SetStep(1, TutorialLines.Length);
            }
        }
    }

    private void EnterInGameState()
    {
        PlayBgm(inGameBgm);
        PlaySe(gameStartSe);

        if (!HasPlayedOnce)
        {
            HasPlayedOnce = true;
            SaveProgress();
        }

        RemainingTimeSeconds = TimeLimitSeconds;
        lastCountdownSecondPlayed = int.MaxValue;
        SetWhitePercentage(whiteboard != null ? whiteboard.GetWhitePercentage() : 0f);

        if (inGameUI != null)
        {
            inGameUI.Show();
            inGameUI.SetCountdownWarningVisible(false);
            inGameUI.SetRemainingTime(RemainingTimeSeconds);
            inGameUI.SetWhitePercentage(WhitePercentage);
        }

        gameTimerCoroutine = StartCoroutine(RunGameTimer());
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
            resultUI.SetCleared(lastRunCleared);
            resultUI.SetHardButtonVisible(false);
            resultUI.SetImpossibleButtonVisible(false);
        }

        resultCountUpCoroutine = StartCoroutine(RunResultCountUp());
    }
    private IEnumerator RunGameTimer()
    {
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

                // --- ここを追加: 毎秒切り替わるタイミングでUIのアニメーションを再生 ---
                if (inGameUI != null)
                {
                    inGameUI.PlayCountdownAnimation();
                }
                // -----------------------------------------------------------
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

        // --- SEを間引くための変数 ---
        float seInterval = 0.05f; // 0.05秒（50ミリ秒）に1回だけ鳴らす
        float timeSinceLastSe = seInterval; // 最初はすぐに鳴るように初期値を設定

        while (elapsedSeconds < ResultCountUpDurationSeconds)
        {
            float deltaTime = Time.unscaledDeltaTime;
            elapsedSeconds += deltaTime;
            timeSinceLastSe += deltaTime; // 経過時間を加算

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

                // 前回SEを鳴らしてから指定した時間(seInterval)以上経過しているかチェック
                if (timeSinceLastSe >= seInterval)
                {
                    PlaySe(resultCountSe);
                    timeSinceLastSe = 0f; // タイマーをリセット
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
        if (tutorialUI == null || TutorialLines.Length == 0)
        {
            return;
        }

        int clampedIndex = Mathf.Clamp(tutorialStepIndex, 0, TutorialLines.Length - 1);
        tutorialUI.SetDialogue(TutorialLines[clampedIndex]);
        tutorialUI.SetStep(clampedIndex + 1, TutorialLines.Length);
    }

    private void RefreshResultUnlockButtons(bool cleared)
    {
        if (resultUI == null)
        {
            return;
        }

        resultUI.SetHardButtonVisible(cleared && CurrentDifficulty == DifficultyMode.Normal && IsHardUnlocked);
        resultUI.SetImpossibleButtonVisible(cleared && CurrentDifficulty == DifficultyMode.Hard && IsImpossibleUnlocked);
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
        string tweetText = $"ホワイトボード消し: {WhitePercentage:0.0}% まで消しました！";
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
            titleUI.NormalRequested += RequestStartNormal;         // 追加
            titleUI.HardRequested += RequestStartHard;             // 追加
            titleUI.ImpossibleRequested += RequestStartImpossible; // 追加
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
            titleUI.NormalRequested -= RequestStartNormal;         // 追加
            titleUI.HardRequested -= RequestStartHard;             // 追加
            titleUI.ImpossibleRequested -= RequestStartImpossible; // 追加
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
