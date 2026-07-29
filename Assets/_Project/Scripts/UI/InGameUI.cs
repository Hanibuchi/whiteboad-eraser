using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections; // コルーチン用に必要

public sealed class InGameUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI remainingTimeText;
    [SerializeField] private TextMeshProUGUI whitePercentageText;
    [SerializeField] private TextMeshProUGUI clearConditionText;
    [SerializeField] private GameObject countdownWarningRoot;

    private Coroutine countdownCoroutine; // アニメーション用のコルーチン参照

    public void Show()
    {
        SetRootActive(true);
    }

    public void Hide()
    {
        SetRootActive(false);
    }

    public void SetRemainingTime(float remainingSeconds)
    {
        if (remainingTimeText == null)
        {
            return;
        }

        // 制限時間を秒数のみの表示に変更
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        remainingTimeText.text = totalSeconds.ToString();
    }

    public void SetWhitePercentage(float whitePercentage)
    {
        if (whitePercentageText != null)
        {
            whitePercentageText.text = $"{Mathf.Clamp(whitePercentage, 0f, 100f):0.0}%";
        }
    }

    public void SetClearCondition(float percentage)
    {
        if (clearConditionText != null)
        {
            // 「97%でクリア」のように表示（小数点以下を切り捨てる場合は 0 を指定）
            clearConditionText.text = $"{percentage:0}%でクリア";
        }
    }

    public void SetCountdownWarningVisible(bool visible)
    {
        if (countdownWarningRoot != null)
        {
            countdownWarningRoot.SetActive(visible);
        }
    }

    // 毎秒数字を大きく表示するアニメーションの呼び出し
    public void PlayCountdownAnimation()
    {
        if (remainingTimeText == null) return;

        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
        }
        countdownCoroutine = StartCoroutine(CountdownAnimationRoutine());
    }

    private IEnumerator CountdownAnimationRoutine()
    {
        float duration = 0.3f;
        float elapsed = 0f;
        Vector3 startScale = Vector3.one * 1.5f; // 1.5倍のサイズからスタート
        Vector3 endScale = Vector3.one;          // 元のサイズ

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            remainingTimeText.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }
        
        remainingTimeText.transform.localScale = endScale;
    }

    private void SetRootActive(bool visible)
    {
        GameObject targetRoot = root != null ? root : gameObject;
        targetRoot.SetActive(visible);
    }
}