using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Music Settings")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip musicClip;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.5f;
    [SerializeField] private bool playOnStart = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Another instance already exists; keep the original and discard this one
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
        }

        ConfigureSource();
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    private void ConfigureSource()
    {
        if (musicSource == null)
        {
            return;
        }

        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = volume;

        if (musicClip != null && musicSource.clip != musicClip)
        {
            musicSource.clip = musicClip;
        }
    }

    public void Play(AudioClip clip = null, float? newVolume = null)
    {
        if (clip != null && musicSource.clip != clip)
        {
            musicSource.clip = clip;
        }

        if (newVolume.HasValue)
        {
            musicSource.volume = Mathf.Clamp01(newVolume.Value);
        }

        if (musicSource.clip != null && !musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    public void Stop()
    {
        if (musicSource.isPlaying)
        {
            musicSource.Stop();
        }
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        if (musicSource != null)
        {
            musicSource.volume = volume;
        }
    }
}


