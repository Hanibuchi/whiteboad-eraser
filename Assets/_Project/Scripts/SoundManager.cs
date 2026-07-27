using UnityEngine;

public sealed class SoundManager : MonoBehaviour
{
    public const string BgmVolumePrefsKey = "SoundManager.BgmVolume";
    public const string SeVolumePrefsKey = "SoundManager.SeVolume";

    private const float DefaultVolume = 1f;
    private const float MinVolume = 0f;
    private const float MaxVolume = 1f;

    private static SoundManager instance;

    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private AudioSource seAudioSource;
    [SerializeField] private AudioClip defaultBgmClip;
    [SerializeField] private AudioClip settingsChangeSeClip;
    [SerializeField] private bool useGeneratedSettingsChangeSe = true;
    [SerializeField, Range(1, 20)] private int discreteVolumeSteps = 10;
    [SerializeField] private bool playDefaultBgmOnStart;

    private AudioClip generatedSettingsChangeSeClip;

    public static SoundManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SoundManager>();
            }

            return instance;
        }
    }

    public float BgmVolume
    {
        get => bgmAudioSource != null ? bgmAudioSource.volume : DefaultVolume;
        set => SetBgmVolume(value);
    }

    public float SeVolume
    {
        get => seAudioSource != null ? seAudioSource.volume : DefaultVolume;
        set => SetSeVolume(value);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioSources();
        LoadVolumes();
        ApplyVolumes();

        if (playDefaultBgmOnStart && defaultBgmClip != null && !bgmAudioSource.isPlaying)
        {
            PlayBgm(defaultBgmClip, true);
        }
    }

    private void OnValidate()
    {
        discreteVolumeSteps = Mathf.Max(1, discreteVolumeSteps);
    }

    public void PlayBgm(AudioClip clip, bool restartIfSameClip = false)
    {
        if (clip == null)
        {
            StopBgm();
            return;
        }

        EnsureAudioSources();

        if (!restartIfSameClip && bgmAudioSource.clip == clip && bgmAudioSource.isPlaying)
        {
            return;
        }

        bgmAudioSource.clip = clip;
        bgmAudioSource.loop = true;
        bgmAudioSource.Play();
    }

    public void StopBgm()
    {
        if (bgmAudioSource == null)
        {
            return;
        }

        bgmAudioSource.Stop();
        bgmAudioSource.clip = null;
    }

    public void PauseBgm()
    {
        if (bgmAudioSource != null && bgmAudioSource.isPlaying)
        {
            bgmAudioSource.Pause();
        }
    }

    public void ResumeBgm()
    {
        if (bgmAudioSource != null)
        {
            bgmAudioSource.UnPause();
        }
    }

    public void PlaySe(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null)
        {
            return;
        }

        EnsureAudioSources();
        seAudioSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void PlaySettingsChangeSe()
    {
        AudioClip clip = ResolveSettingsChangeSeClip();
        if (clip == null)
        {
            return;
        }

        PlaySe(clip);
    }

    public void SetBgmVolume(float volume, bool save = true)
    {
        EnsureAudioSources();
        bgmAudioSource.volume = NormalizeVolume(volume);

        if (save)
        {
            SaveVolumes();
        }
    }

    public void SetSeVolume(float volume, bool save = true)
    {
        EnsureAudioSources();
        seAudioSource.volume = NormalizeVolume(volume);

        if (save)
        {
            SaveVolumes();
        }
    }

    public float GetBgmVolumeStep()
    {
        return Mathf.Round(NormalizeVolume(BgmVolume) * discreteVolumeSteps) / discreteVolumeSteps;
    }

    public float GetSeVolumeStep()
    {
        return Mathf.Round(NormalizeVolume(SeVolume) * discreteVolumeSteps) / discreteVolumeSteps;
    }

    public void SaveVolumes()
    {
        PlayerPrefs.SetFloat(BgmVolumePrefsKey, NormalizeVolume(BgmVolume));
        PlayerPrefs.SetFloat(SeVolumePrefsKey, NormalizeVolume(SeVolume));
        PlayerPrefs.Save();
    }

    public void LoadVolumes()
    {
        SetBgmVolume(PlayerPrefs.GetFloat(BgmVolumePrefsKey, DefaultVolume), false);
        SetSeVolume(PlayerPrefs.GetFloat(SeVolumePrefsKey, DefaultVolume), false);
    }

    public void ApplyVolumes()
    {
        if (bgmAudioSource != null)
        {
            bgmAudioSource.volume = NormalizeVolume(BgmVolume);
        }

        if (seAudioSource != null)
        {
            seAudioSource.volume = NormalizeVolume(SeVolume);
        }
    }

    private void EnsureAudioSources()
    {
        if (bgmAudioSource == null)
        {
            bgmAudioSource = FindOrCreateChildAudioSource("BGM Audio Source");
            bgmAudioSource.loop = true;
        }

        if (seAudioSource == null)
        {
            seAudioSource = FindOrCreateChildAudioSource("SE Audio Source");
            seAudioSource.loop = false;
        }
    }

    private AudioClip ResolveSettingsChangeSeClip()
    {
        if (settingsChangeSeClip != null)
        {
            return settingsChangeSeClip;
        }

        if (!useGeneratedSettingsChangeSe)
        {
            return null;
        }

        if (generatedSettingsChangeSeClip == null)
        {
            generatedSettingsChangeSeClip = CreateGeneratedSettingsChangeSeClip();
        }

        return generatedSettingsChangeSeClip;
    }

    private AudioClip CreateGeneratedSettingsChangeSeClip()
    {
        const float durationSeconds = 0.08f;
        const int sampleRate = 44100;
        const float frequency = 880f;
        const float amplitude = 0.2f;

        int sampleCount = Mathf.CeilToInt(durationSeconds * sampleRate);
        float[] samples = new float[sampleCount];

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float normalizedTime = (float)sampleIndex / sampleRate;
            float envelope = Mathf.Exp(-normalizedTime * 25f);
            samples[sampleIndex] = Mathf.Sin(2f * Mathf.PI * frequency * normalizedTime) * amplitude * envelope;
        }

        AudioClip clip = AudioClip.Create("GeneratedSettingsChangeSe", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioSource FindOrCreateChildAudioSource(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(transform, false);
            child = childObject.transform;
        }

        AudioSource audioSource = child.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = child.gameObject.AddComponent<AudioSource>();
        }

        return audioSource;
    }

    private float NormalizeVolume(float volume)
    {
        return Mathf.Clamp(volume, MinVolume, MaxVolume);
    }
}
