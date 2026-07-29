using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class TitleUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Button startButton;
    [SerializeField] private Button normalButton;     // 追加
    [SerializeField] private Button hardButton;       // 追加
    [SerializeField] private Button impossibleButton; // 追加
    [SerializeField] private Button settingsButton;

    public event Action StartRequested;
    public event Action NormalRequested;     // 追加
    public event Action HardRequested;       // 追加
    public event Action ImpossibleRequested; // 追加

    private void OnEnable()
    {
        if (startButton != null) startButton.onClick.AddListener(InvokeStartRequested);
        if (normalButton != null) normalButton.onClick.AddListener(InvokeNormalRequested);
        if (hardButton != null) hardButton.onClick.AddListener(InvokeHardRequested);
        if (impossibleButton != null) impossibleButton.onClick.AddListener(InvokeImpossibleRequested);
    }

    private void OnDisable()
    {
        if (startButton != null) startButton.onClick.RemoveListener(InvokeStartRequested);
        if (normalButton != null) normalButton.onClick.RemoveListener(InvokeNormalRequested);
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

    public void SetInteractable(bool interactable)
    {
        if (startButton != null) startButton.interactable = interactable;
        if (settingsButton != null) settingsButton.interactable = interactable;
    }

    // 各ボタンの解放状態を更新するメソッドを追加
    public void UpdateDifficultyButtons(bool hasPlayedOnce, bool isHardUnlocked, bool isImpossibleUnlocked)
    {
        if (normalButton != null) normalButton.interactable = hasPlayedOnce;
        if (hardButton != null) hardButton.interactable = isHardUnlocked;
        if (impossibleButton != null) impossibleButton.interactable = isImpossibleUnlocked;
    }

    public void InvokeStartRequested() => StartRequested?.Invoke();
    private void InvokeNormalRequested() => NormalRequested?.Invoke();         // 追加
    private void InvokeHardRequested() => HardRequested?.Invoke();             // 追加
    private void InvokeImpossibleRequested() => ImpossibleRequested?.Invoke(); // 追加

    private void SetRootActive(bool visible)
    {
        GameObject targetRoot = root != null ? root : gameObject;
        targetRoot.SetActive(visible);
    }
}