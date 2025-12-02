using UnityEngine;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Channels")]
    public AudioSource sfxSource;
    public AudioSource ambienceSource;
    public AudioSource musicSource;
    public AudioSource environmentSource;
    public AudioSource uiSource;

    [Header("Debug")]
    public bool debugLogs = false;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ───────────────────────────────────────────────
    // BASIC ONE-SHOTS
    // ───────────────────────────────────────────────

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volume);
        if (debugLogs) Debug.Log("[AudioManager] SFX: " + clip.name);
    }

    public void PlayEnvironment(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        environmentSource.PlayOneShot(clip, volume);
    }

    public void PlayUI(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        uiSource.PlayOneShot(clip, volume);
    }

    // ───────────────────────────────────────────────
    // AMBIENCE
    // ───────────────────────────────────────────────

    public void PlayAmbience(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (clip == null) return;
        ambienceSource.clip = clip;
        ambienceSource.volume = volume;
        ambienceSource.loop = loop;
        ambienceSource.Play();
    }

    public void StopAmbience()
    {
        ambienceSource.Stop();
    }

    // ───────────────────────────────────────────────
    // MUSIC
    // ───────────────────────────────────────────────

    public void PlayMusic(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (clip == null) return;

        musicSource.clip = clip;
        musicSource.volume = volume;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void StopMusic(float fadeTime = 0f)
    {
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
        float start = musicSource.volume;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(start, 0f, t / time);
            yield return null;
        }
        musicSource.Stop();
        musicSource.volume = start;
    }

    // ───────────────────────────────────────────────
    // TWO-STAGE PAIRED SOUNDS
    // ───────────────────────────────────────────────
    public void PlayDualSFX(AudioClip first, AudioClip second, float delay)
    {
        StartCoroutine(PlayDualRoutine(first, second, delay));
    }

    IEnumerator PlayDualRoutine(AudioClip first, AudioClip second, float delay)
    {
        if (first != null)
            sfxSource.PlayOneShot(first);

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (second != null)
            sfxSource.PlayOneShot(second);
    }
}
