using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SettingsWindowController : MonoBehaviour
{
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backgroundButton;
    [SerializeField] private bool startClosed = true;

    private CanvasGroup windowCanvasGroup;
    private BackgroundCloseGuard backgroundCloseGuard;
    private bool wasBackgroundButtonEnabled;

    private void Awake()
    {
        if (windowRoot == null)
        {
            windowRoot = gameObject;
        }

        windowCanvasGroup = windowRoot.GetComponent<CanvasGroup>();
        if (windowCanvasGroup == null)
        {
            windowCanvasGroup = windowRoot.AddComponent<CanvasGroup>();
        }

        SetWindowVisible(!startClosed);
    }

    private void OnEnable()
    {
        RegisterListeners();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    public void Open()
    {
        SetWindowVisible(true);
    }

    public void Close()
    {
        SetWindowVisible(false);
    }

    public void Toggle()
    {
        if (windowRoot == null)
        {
            return;
        }

        SetWindowVisible(!windowRoot.activeSelf);
    }

    private void RegisterListeners()
    {
        if (openButton != null)
        {
            openButton.onClick.AddListener(Open);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (backgroundButton != null)
        {
            wasBackgroundButtonEnabled = backgroundButton.enabled;
            backgroundButton.enabled = false;

            if (!backgroundButton.TryGetComponent(out backgroundCloseGuard))
            {
                backgroundCloseGuard = backgroundButton.gameObject.AddComponent<BackgroundCloseGuard>();
            }

            backgroundCloseGuard.Initialize(Close);
        }
    }

    private void UnregisterListeners()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(Open);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (backgroundButton != null)
        {
            backgroundButton.enabled = wasBackgroundButtonEnabled;

            if (backgroundCloseGuard != null)
            {
                backgroundCloseGuard.ResetHandler();
            }
        }
    }

    private void SetWindowVisible(bool visible)
    {
        if (openButton != null)
        {
            openButton.gameObject.SetActive(!visible);
        }

        if (windowCanvasGroup != null)
        {
            windowCanvasGroup.alpha = visible ? 1f : 0f;
            windowCanvasGroup.interactable = visible;
            windowCanvasGroup.blocksRaycasts = visible;
            return;
        }

        if (windowRoot != null)
        {
            windowRoot.SetActive(visible);
        }
    }

    private sealed class BackgroundCloseGuard : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        private System.Action onDirectBackgroundClick;
        private bool pressedOnSelf;

        public void Initialize(System.Action onClick)
        {
            onDirectBackgroundClick = onClick;
        }

        public void ResetHandler()
        {
            onDirectBackgroundClick = null;
            pressedOnSelf = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressedOnSelf = IsDirectTarget(eventData.pointerPressRaycast.gameObject)
                || IsDirectTarget(eventData.pointerCurrentRaycast.gameObject);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            bool releasedOnSelf = IsDirectTarget(eventData.pointerCurrentRaycast.gameObject);
            if (!pressedOnSelf || !releasedOnSelf)
            {
                return;
            }

            onDirectBackgroundClick?.Invoke();
        }

        private bool IsDirectTarget(GameObject target)
        {
            return target != null && target == gameObject;
        }
    }
}