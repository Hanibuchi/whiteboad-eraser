using System;
using System.Collections; // コルーチンを使用するために追加
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class TutorialUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Button advanceButton;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private TextMeshProUGUI stepCounterText;

    // --- 追加: タイピングエフェクト用の設定 ---
    [Header("Typing Settings")]
    [SerializeField] private float typingSpeed = 0.05f;
    [SerializeField] private AudioClip typingSe;
    private Coroutine typingCoroutine;
    // ----------------------------------------

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
        // --- 追加: 表示のリセット時にコルーチンを停止 ---
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        // ----------------------------------------

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        if (stepCounterText != null)
        {
            stepCounterText.text = string.Empty;
        }
    }

    // --- 変更: 文字を一文字ずつ表示するように変更 ---
    public void SetDialogue(string text)
    {
        if (dialogueText != null)
        {
            // 既に文字表示中の場合は停止してリセットする
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }
            typingCoroutine = StartCoroutine(TypeDialogueRoutine(text ?? string.Empty));
        }
    }

    private IEnumerator TypeDialogueRoutine(string text)
    {
        dialogueText.text = string.Empty;

        foreach (char c in text)
        {
            dialogueText.text += c;

            // 一文字表示するごとに SoundManager 経由で SE を鳴らす
            if (typingSe != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySe(typingSe);
            }

            // 次の文字を表示するまで待機
            yield return new WaitForSeconds(typingSpeed);
        }

        typingCoroutine = null;
    }
    // ----------------------------------------

    public void SetStep(int currentStep, int totalSteps)
    {
        if (stepCounterText != null)
        {
            stepCounterText.text = $"{Mathf.Max(0, currentStep)}/{Mathf.Max(0, totalSteps)}";
        }
    }

    public void InvokeAdvanceRequested()
    {
        // 演出中に進むボタンが押された際、即時全表示する仕様にしたい場合は
        // ここにコルーチン停止と全文字列代入の処理を追加できます。
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