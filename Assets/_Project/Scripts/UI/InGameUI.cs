using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class InGameUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TextMeshProUGUI remainingTimeText;
    [SerializeField] private TextMeshProUGUI whitePercentageText;
    [SerializeField] private GameObject countdownWarningRoot;

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

        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        remainingTimeText.text = $"{minutes:00}:{seconds:00}";
    }

    public void SetWhitePercentage(float whitePercentage)
    {
        if (whitePercentageText != null)
        {
            whitePercentageText.text = $"{Mathf.Clamp(whitePercentage, 0f, 100f):0.0}%";
        }
    }

    public void SetCountdownWarningVisible(bool visible)
    {
        if (countdownWarningRoot != null)
        {
            countdownWarningRoot.SetActive(visible);
        }
    }

    private void SetRootActive(bool visible)
    {
        GameObject targetRoot = root != null ? root : gameObject;
        targetRoot.SetActive(visible);
    }
}
