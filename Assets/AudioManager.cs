using UnityEngine;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Channels")]
    public AudioSource sfxSource;
    public AudioSource ambienceSource;
    public AudioSource weatherSource;
    public AudioSource musicSource;
    public AudioSource environmentSource;
    public AudioSource uiSource;

    [Header("Debug")]
    public bool debugLogs = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // create / fix sources on THIS object so they survive scene loads
        EnsureSource(ref sfxSource, "Audio_SFX");
        EnsureSource(ref ambienceSource, "Audio_Ambience");
        EnsureSource(ref weatherSource, "Audio_Weather");
        EnsureSource(ref musicSource, "Audio_Music");
        EnsureSource(ref environmentSource, "Audio_Environment");
        EnsureSource(ref uiSource, "Audio_UI");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ───────────────────────────────── Helpers ─────────────────────────────────

    private void EnsureSource(ref AudioSource src, string childName)
    {
        // if it's a destroyed ref, Unity's == will treat it as null
        if (src != null)
        {
            if (src == null)
            {
                src = null;
            }
            else
            {
                return; // already valid
            }
        }

        // try find an existing child
        Transform child = transform.Find(childName);
        if (child != null)
        {
            src = child.GetComponent<AudioSource>();
        }

        // otherwise create a new child
        if (src == null)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f; // 2D by default
        }
    }

    private bool IsValid(AudioSource src)
    {
        return src != null; // also covers destroyed refs
    }

    // ───────────────────────────────── One-shot channels ─────────────────────────────────

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null || !IsValid(sfxSource)) return;
        sfxSource.PlayOneShot(clip, volume);
        if (debugLogs) Debug.Log("[AudioManager] SFX: " + clip.name, this);
    }

    public void PlayEnvironment(AudioClip clip, float volume = 1f)
    {
        if (clip == null || !IsValid(environmentSource)) return;
        environmentSource.PlayOneShot(clip, volume);
    }

    public void PlayUI(AudioClip clip, float volume = 1f)
    {
        if (clip == null || !IsValid(uiSource)) return;
        uiSource.PlayOneShot(clip, volume);
    }

    // ───────────────────────────────── Ambience ─────────────────────────────────

    public void PlayAmbience(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (clip == null || !IsValid(ambienceSource)) return;

        ambienceSource.clip = clip;
        ambienceSource.volume = volume;
        ambienceSource.loop = loop;
        ambienceSource.Play();
    }

    public void StopAmbience()
    {
        if (!IsValid(ambienceSource)) return;
        ambienceSource.Stop();
    }

    // ───────────────────────────────── Weather ─────────────────────────────────
    // e.g. rain, wind. Separate from ambience so you can fade/weather independently.

    public void PlayWeather(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (clip == null || !IsValid(weatherSource)) return;

        weatherSource.clip = clip;
        weatherSource.volume = volume;
        weatherSource.loop = loop;
        weatherSource.Play();
    }

    public void StopWeather()
    {
        if (!IsValid(weatherSource)) return;
        weatherSource.Stop();
    }

    // ───────────────────────────────── Music ─────────────────────────────────

    public void PlayMusic(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (clip == null || !IsValid(musicSource)) return;

        musicSource.clip = clip;
        musicSource.volume = volume;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void StopMusic(float fadeTime = 0f)
    {
        if (!IsValid(musicSource)) return;

        if (fadeTime <= 0f)
        {
            musicSource.Stop();
        }
        else
        {
            StartCoroutine(FadeOutMusic(fadeTime));
        }
    }

    IEnumerator FadeOutMusic(float time)
    {
        if (!IsValid(musicSource))
            yield break;

        float start = musicSource.volume;
        float t = 0f;
        while (t < time && IsValid(musicSource))
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(start, 0f, t / time);
            yield return null;
        }

        if (IsValid(musicSource))
        {
            musicSource.Stop();
            musicSource.volume = start;
        }
    }

    // ───────────────────────────────── Dual SFX (for your 2-stage sounds) ─────────────────────────────────

    public void PlayDualSFX(AudioClip first, AudioClip second, float delay)
    {
        if (!IsValid(sfxSource)) return;
        if (first == null && second == null) return;

        StartCoroutine(PlayDualRoutine(first, second, delay));
    }

    IEnumerator PlayDualRoutine(AudioClip first, AudioClip second, float delay)
    {
        if (!IsValid(sfxSource))
            yield break;

        if (first != null)
            sfxSource.PlayOneShot(first);

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!IsValid(sfxSource))
            yield break;

        if (second != null)
            sfxSource.PlayOneShot(second);
    }
}
