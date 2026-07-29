using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class ResultUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI resultPercentageText;
    [SerializeField] private TextMeshProUGUI resultMessageText; // 追加: メッセージ用TMP
    [SerializeField] private Animator resultAnimator; // 追加: アニメーション用Animator

    [SerializeField] private ParticleSystem clearParticle;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button titleButton;
    [SerializeField] private Button tweetButton;
    [SerializeField] private Button hardButton;
    [SerializeField] private Button impossibleButton;

    public event Action RetryRequested;
    public event Action TitleRequested;
    public event Action TweetRequested;
    public event Action<DifficultyMode> DifficultyRequested;

    private void OnEnable()
    {
        if (retryButton != null) retryButton.onClick.AddListener(InvokeRetryRequested);
        if (titleButton != null) titleButton.onClick.AddListener(InvokeTitleRequested);
        if (tweetButton != null) tweetButton.onClick.AddListener(InvokeTweetRequested);
        if (hardButton != null) hardButton.onClick.AddListener(InvokeHardRequested);
        if (impossibleButton != null) impossibleButton.onClick.AddListener(InvokeImpossibleRequested);
    }

    private void OnDisable()
    {
        if (retryButton != null) retryButton.onClick.RemoveListener(InvokeRetryRequested);
        if (titleButton != null) titleButton.onClick.RemoveListener(InvokeTitleRequested);
        if (tweetButton != null) tweetButton.onClick.RemoveListener(InvokeTweetRequested);
        if (hardButton != null) hardButton.onClick.RemoveListener(InvokeHardRequested);
        if (impossibleButton != null) impossibleButton.onClick.RemoveListener(InvokeImpossibleRequested);
    }

    public void Show()
    {
        SetRootActive(true);
    }

    public void Hide()
    {
        SetRootActive(false);
    }
    public void ResetDisplay()
    {
        SetCurrentPercentage(0f);
        SetTargetPercentage(0f);
        SetHardButtonVisible(false);
        SetImpossibleButtonVisible(false);
        SetRetryButtonVisible(true);
        SetCleared(false);

        if (clearParticle != null)
        {
            clearParticle.Stop();
            clearParticle.Clear();
        }
    }

    public void SetTargetPercentage(float whitePercentage)
    {
        if (resultPercentageText != null)
        {
            resultPercentageText.text = $"{Mathf.Clamp(whitePercentage, 0f, 100f):0.0}%";
        }
    }

    public void SetCurrentPercentage(float whitePercentage)
    {
        if (resultPercentageText != null)
        {
            resultPercentageText.text = $"{Mathf.Clamp(whitePercentage, 0f, 100f):0.0}%";
        }
    }

    public void SetRetryButtonVisible(bool visible)
    {
        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(visible);
        }
    }

    public void SetHardButtonVisible(bool visible)
    {
        if (hardButton != null)
        {
            hardButton.gameObject.SetActive(visible);
        }
    }

    public void SetImpossibleButtonVisible(bool visible)
    {
        if (impossibleButton != null)
        {
            impossibleButton.gameObject.SetActive(visible);
        }
    }

    public void SetCleared(bool cleared, float clearTime = 0f, float clearConditionPercentage = 0f)
    {
        // クリア状況に応じてメッセージを出し分ける
        if (resultMessageText != null)
        {
            if (cleared)
            {
                // タイムの表示形式。小数点第1位まで表示なら F1、第2位なら F2 に変更してください。
                // 整数のみ（例：10秒）にしたい場合は {clearTime:0} とします。
                resultMessageText.text = $"クリア！ タイム: {clearTime:F2}秒";
            }
            else
            {
                // 「97%以上」のように表示
                resultMessageText.text = $"残念...! クリア条件: {clearConditionPercentage:0}%以上";
            }
        }

        if (resultAnimator != null)
        {
            resultAnimator.SetTrigger("Result");
        }
    }

    public void PlayClearParticle()
    {
        if (clearParticle != null)
        {
            clearParticle.Play();
        }
    }

    public void InvokeRetryRequested() => RetryRequested?.Invoke();
    public void InvokeTitleRequested() => TitleRequested?.Invoke();
    public void InvokeTweetRequested() => TweetRequested?.Invoke();
    public void InvokeDifficultyRequested(DifficultyMode difficultyMode) => DifficultyRequested?.Invoke(difficultyMode);

    private void InvokeHardRequested() => InvokeDifficultyRequested(DifficultyMode.Hard);
    private void InvokeImpossibleRequested() => InvokeDifficultyRequested(DifficultyMode.Impossible);

    private void SetRootActive(bool visible)
    {
        GameObject targetRoot = root != null ? root : gameObject;
        targetRoot.SetActive(visible);
    }
}