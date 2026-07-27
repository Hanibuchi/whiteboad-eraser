using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MyButton : Button
{
    [SerializeField] private AudioClip hoverSeClip;
    [SerializeField] private AudioClip selectSeClip;
    [SerializeField] private AudioClip clickSeClip;

    private bool suppressNextSelectSe;

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        PlaySe(hoverSeClip);
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);

        if (suppressNextSelectSe)
        {
            suppressNextSelectSe = false;
            return;
        }

        PlaySe(selectSeClip);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            suppressNextSelectSe = true;
        }

        base.OnPointerDown(eventData);
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        base.OnPointerClick(eventData);

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            PlaySe(clickSeClip);
        }
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        base.OnSubmit(eventData);
        PlaySe(clickSeClip);
    }

    protected override void OnDisable()
    {
        suppressNextSelectSe = false;
        base.OnDisable();
    }

    private void PlaySe(AudioClip clip)
    {
        SoundManager soundManager = SoundManager.Instance;
        if (soundManager == null)
        {
            return;
        }

        if (clip == null)
        {
            soundManager.PlaySettingsChangeSe();
            return;
        }

        soundManager.PlaySe(clip);
    }
}