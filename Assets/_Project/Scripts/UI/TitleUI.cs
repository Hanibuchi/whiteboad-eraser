using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class TitleUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;

    public event Action StartRequested;

    private void OnEnable()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(InvokeStartRequested);
        }
    }

    private void OnDisable()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(InvokeStartRequested);
        }
    }

    public void Show()
    {
        SetRootActive(true);
    }

    public void Hide()
    {
        SetRootActive(false);
    }

    public void SetInteractable(bool interactable)
    {
        if (startButton != null)
        {
            startButton.interactable = interactable;
        }

        if (settingsButton != null)
        {
            settingsButton.interactable = interactable;
        }
    }

    public void InvokeStartRequested()
    {
        StartRequested?.Invoke();
    }

    private void SetRootActive(bool visible)
    {
        GameObject targetRoot = root != null ? root : gameObject;
        targetRoot.SetActive(visible);
    }
}
