using System;
using System.Collections;
using UnityEngine;

public sealed class FadeManager : MonoBehaviour
{
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField, Min(0.01f)] private float defaultFadeDuration = 0.5f;

    public bool IsFading { get; private set; }

    private void Awake()
    {
        if (fadeCanvasGroup == null)
        {
            fadeCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (fadeCanvasGroup == null)
        {
            fadeCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        SetImmediateVisible(false);
    }

    public void SetImmediateVisible(bool visible)
    {
        if (fadeCanvasGroup == null)
        {
            return;
        }

        fadeCanvasGroup.alpha = visible ? 1f : 0f;
        fadeCanvasGroup.interactable = visible;
        fadeCanvasGroup.blocksRaycasts = visible;
    }

    public IEnumerator FadeOut(float durationSeconds = -1f, Action onCompleted = null)
    {
        if (fadeCanvasGroup == null)
        {
            onCompleted?.Invoke();
            yield break;
        }

        IsFading = true;
        float fadeDuration = durationSeconds > 0f ? durationSeconds : defaultFadeDuration;
        float elapsedSeconds = 0f;

        fadeCanvasGroup.gameObject.SetActive(true);
        fadeCanvasGroup.interactable = false;
        fadeCanvasGroup.blocksRaycasts = true;

        while (elapsedSeconds < fadeDuration)
        {
            elapsedSeconds += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(elapsedSeconds / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 1f;
        onCompleted?.Invoke();
        IsFading = false;
    }

    public IEnumerator FadeIn(float durationSeconds = -1f, Action onCompleted = null)
    {
        if (fadeCanvasGroup == null)
        {
            onCompleted?.Invoke();
            yield break;
        }

        IsFading = true;
        float fadeDuration = durationSeconds > 0f ? durationSeconds : defaultFadeDuration;
        float elapsedSeconds = 0f;

        fadeCanvasGroup.gameObject.SetActive(true);
        fadeCanvasGroup.interactable = false;
        fadeCanvasGroup.blocksRaycasts = true;

        while (elapsedSeconds < fadeDuration)
        {
            elapsedSeconds += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsedSeconds / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.interactable = false;
        onCompleted?.Invoke();
        IsFading = false;
    }

    public IEnumerator FadeOutIn(Action onFadeOutCompleted = null, Action onFadeInCompleted = null)
    {
        yield return FadeOut(onCompleted: onFadeOutCompleted);
        yield return FadeIn(onCompleted: onFadeInCompleted);
    }

    public IEnumerator FadeTransition(Action onBlackScreenReached = null, Action onCompleted = null)
    {
        yield return FadeOut(onCompleted: onBlackScreenReached);
        yield return FadeIn(onCompleted: onCompleted);
    }
}
