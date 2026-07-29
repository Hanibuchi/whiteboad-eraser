using UnityEngine;
using UnityEngine.UI;

public sealed class SoundSettingsUI : MonoBehaviour
{
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider seVolumeSlider;
    [SerializeField, Range(1, 20)] private int discreteVolumeSteps = 10;
    [SerializeField] private bool playChangeSeOnVolumeChange = true;

    private SoundManager soundManager;
    private bool isInitialized;

    private void Awake()
    {
        SetupSlider(bgmVolumeSlider);
        SetupSlider(seVolumeSlider);
    }

    private void OnEnable()
    {
        CacheSoundManager();
        SyncSlidersFromSoundManager();
        RegisterListeners();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    private void OnValidate()
    {
        discreteVolumeSteps = Mathf.Max(1, discreteVolumeSteps);
        SetupSlider(bgmVolumeSlider);
        SetupSlider(seVolumeSlider);
    }

    public void RefreshFromSoundManager()
    {
        CacheSoundManager();
        SyncSlidersFromSoundManager();
    }

    private void CacheSoundManager()
    {
        if (soundManager == null)
        {
            soundManager = SoundManager.Instance;
        }
    }

    private void SyncSlidersFromSoundManager()
    {
        if (soundManager == null)
        {
            return;
        }

        isInitialized = false;

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.SetValueWithoutNotify(VolumeToSliderValue(soundManager.BgmVolume));
        }

        if (seVolumeSlider != null)
        {
            seVolumeSlider.SetValueWithoutNotify(VolumeToSliderValue(soundManager.SeVolume));
        }

        isInitialized = true;
    }

    private void RegisterListeners()
    {
        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.onValueChanged.AddListener(OnBgmSliderValueChanged);
        }

        if (seVolumeSlider != null)
        {
            seVolumeSlider.onValueChanged.AddListener(OnSeSliderValueChanged);
        }
    }

    private void UnregisterListeners()
    {
        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmSliderValueChanged);
        }

        if (seVolumeSlider != null)
        {
            seVolumeSlider.onValueChanged.RemoveListener(OnSeSliderValueChanged);
        }
    }

    private void OnBgmSliderValueChanged(float sliderValue)
    {
        if (!isInitialized)
        {
            return;
        }

        CacheSoundManager();
        if (soundManager == null)
        {
            return;
        }

        soundManager.SetBgmVolume(SliderValueToVolume(sliderValue));
        PlayChangeSeIfNeeded();
    }

    private void OnSeSliderValueChanged(float sliderValue)
    {
        if (!isInitialized)
        {
            return;
        }

        CacheSoundManager();
        if (soundManager == null)
        {
            return;
        }

        soundManager.SetSeVolume(SliderValueToVolume(sliderValue));
        PlayChangeSeIfNeeded();
    }

    private void PlayChangeSeIfNeeded()
    {
        if (!playChangeSeOnVolumeChange)
        {
            return;
        }

        if (soundManager == null)
        {
            return;
        }

        soundManager.PlaySettingsChangeSe();
    }

    private void SetupSlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.wholeNumbers = true;
        slider.minValue = 0f;
        slider.maxValue = discreteVolumeSteps;
        slider.value = Mathf.Clamp(slider.value, slider.minValue, slider.maxValue);
    }

    private float SliderValueToVolume(float sliderValue)
    {
        if (discreteVolumeSteps <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01(sliderValue / discreteVolumeSteps);
    }

    private float VolumeToSliderValue(float volume)
    {
        return Mathf.Round(Mathf.Clamp01(volume) * discreteVolumeSteps);
    }
}
