using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("Cấu hình Nhạc nền (BGM)")]
    public AudioSource musicSource; 
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float volume = 0.5f;

    [Header("Cấu hình Hiệu ứng âm thanh (SFX)")]
    public AudioSource sfxSource; 
    public AudioClip defaultClickSFX; 

    [Header("Hiệu ứng Random Pitch")]
    [Range(0f, 0.5f)] public float pitchVariation = 0.15f;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (musicSource != null && backgroundMusic != null)
        {
            musicSource.clip = backgroundMusic;
            musicSource.loop = true;
            musicSource.volume = volume;
            musicSource.Play();
        }
    }

    public void PlaySFX(AudioClip clip, bool useRandomPitch = true)
    {
        if (sfxSource != null && clip != null)
        {
            if (useRandomPitch)
            {
                sfxSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            }
            else
            {
                sfxSource.pitch = 1f; 
            }

            sfxSource.PlayOneShot(clip); 
        }
    }

    public void PlayClickSound()
    {
        if (defaultClickSFX != null)
        {
            PlaySFX(defaultClickSFX, true);
        }
    }
}