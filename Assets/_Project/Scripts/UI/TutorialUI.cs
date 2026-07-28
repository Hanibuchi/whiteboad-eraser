using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class TutorialUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Button advanceButton;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private TextMeshProUGUI stepCounterText;

    public event Action AdvanceRequested;
    public event Action Finished;

    private void OnEnable()
    {
        if (advanceButton != null)
        {
            advanceButton.onClick.AddListener(InvokeAdvanceRequested);
        }
    }

    private void OnDisable()
    {
        if (advanceButton != null)
        {
            advanceButton.onClick.RemoveListener(InvokeAdvanceRequested);
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

    public void ResetStory()
    {
        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        if (stepCounterText != null)
        {
            stepCounterText.text = string.Empty;
        }
    }

    public void SetDialogue(string text)
    {
        if (dialogueText != null)
        {
            dialogueText.text = text ?? string.Empty;
        }
    }

    public void SetStep(int currentStep, int totalSteps)
    {
        if (stepCounterText != null)
        {
            stepCounterText.text = $"{Mathf.Max(0, currentStep)}/{Mathf.Max(0, totalSteps)}";
        }
    }

    public void InvokeAdvanceRequested()
    {
        AdvanceRequested?.Invoke();
    }

    public void InvokeFinished()
    {
        Finished?.Invoke();
    }

    private void SetRootActive(bool visible)
    {
        GameObject targetRoot = root != null ? root : gameObject;
        targetRoot.SetActive(visible);
    }
}
