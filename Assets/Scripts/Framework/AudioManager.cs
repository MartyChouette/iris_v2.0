/**
 * @file AudioManager.cs
 * @brief Central audio hub for Iris: owns persistent AudioSources for common mix lanes.
 * @details
 * This component provides stable, scene-agnostic audio channels (SFX, ambience, weather, music, environment, UI)
 * and survives scene loads via DontDestroyOnLoad.
 *
 * Design intent:
 * - "Nintendo build quality": no surprise duplication, no missing references, predictable behavior across scenes.
 * - Channel separation: keep UI click sounds from being drowned out by SFX; keep weather fades independent of ambience.
 * - Allocation discipline: hot paths (PlayOneShot) are allocation-free; coroutines are used only on explicit fade calls.
 *
 * Invariants:
 * - Exactly one instance exists at runtime (singleton).
 * - The AudioManager GameObject is always a ROOT object before calling DontDestroyOnLoad.
 * - All managed AudioSources live under the AudioManager so they survive scene changes.
 *
 * Perf notes:
 * - This script should be negligible CPU/GPU cost; the heavy cost is the audio decode/mix itself.
 * - Coroutines allocate once per call; avoid calling fades per-frame.
 *
 * @ingroup audio
 */

using UnityEngine;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    /// Singleton instance. Created by scene placement (recommended), guarded against duplicates.
    public static AudioManager Instance;

    [Header("Audio Channels (2D)")]
    /// One-shot SFX lane (cuts, clicks, impacts).
    public AudioSource sfxSource;

    /// Continuous ambience bed lane (room tone, apartment hum, etc).
    public AudioSource ambienceSource;

    /// Continuous weather lane (rain/wind), separate so it can fade independently.
    public AudioSource weatherSource;

    /// Music lane (BGM).
    public AudioSource musicSource;

    /// One-shot / occasional environmental events (distant thumps, fridge click, etc).
    public AudioSource environmentSource;

    /// UI lane (menu blips, hover/click).
    public AudioSource uiSource;

    [Header("Debug")]
    /// If enabled, logs one-shot events.
    public bool debugLogs = false;

    /// Optional: Ensure we only ever detach once (avoids surprising hierarchy changes in editor-play).
    private bool _wasDetachedToRoot = false;

    private void Awake()
    {
        // Singleton guard: keep the first, destroy any later duplicates.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // CRITICAL: DontDestroyOnLoad only works for ROOT objects.
        // If this component is on a child object, detach it before calling DDoL.
        if (transform.parent != null)
        {
            transform.SetParent(null, worldPositionStays: true);
            _wasDetachedToRoot = true;
        }

        DontDestroyOnLoad(gameObject);

        // Create / fix sources under THIS persistent object so they survive scene loads.
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

    /**
     * @brief Ensure an AudioSource reference exists and is parented to this manager.
     * @param src Reference to the AudioSource field.
     * @param childName Name of the child object to find/create.
     *
     * @details
     * If the reference is missing or destroyed, attempts to find a child by name.
     * If not found, creates a new child GameObject with an AudioSource.
     *
     * @note
     * All sources are configured as 2D (spatialBlend=0) by default and playOnAwake=false.
     */
    private void EnsureSource(ref AudioSource src, string childName)
    {
        // Unity "fake null" check handles destroyed components.
        if (src != null)
            return;

        // Try find existing child
        Transform child = transform.Find(childName);
        if (child != null)
        {
            src = child.GetComponent<AudioSource>();
        }

        // Otherwise create new child
        if (src == null)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(transform, false);

            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f; // 2D by default
        }

        // If someone assigned a source in the inspector but it's not under us,
        // we still want it to persist with the manager across scenes.
        if (src.transform.parent != transform)
        {
            src.transform.SetParent(transform, worldPositionStays: true);
        }
    }

    /// @brief Validate the AudioSource reference (covers Unity destroyed refs).
    private static bool IsValid(AudioSource src) => src != null;

    // ───────────────────────────────── One-shot channels ─────────────────────────────────

    /**
     * @brief Play a one-shot clip on the SFX channel.
     * @param clip Clip to play.
     * @param volume Volume multiplier (0..1+).
     */
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null || !IsValid(sfxSource)) return;
        sfxSource.PlayOneShot(clip, volume);
        if (debugLogs) Debug.Log("[AudioManager] SFX: " + clip.name, this);
    }

    /**
     * @brief Play a one-shot clip on the Environment channel.
     * @param clip Clip to play.
     * @param volume Volume multiplier (0..1+).
     */
    public void PlayEnvironment(AudioClip clip, float volume = 1f)
    {
        if (clip == null || !IsValid(environmentSource)) return;
        environmentSource.PlayOneShot(clip, volume);
    }

    /**
     * @brief Play a one-shot clip on the UI channel.
     * @param clip Clip to play.
     * @param volume Volume multiplier (0..1+).
     */
    public void PlayUI(AudioClip clip, float volume = 1f)
    {
        if (clip == null || !IsValid(uiSource)) return;
        uiSource.PlayOneShot(clip, volume);
    }

    // ───────────────────────────────── Ambience ─────────────────────────────────

    /**
     * @brief Start looping ambience on the ambience channel.
     * @param clip Clip to play.
     * @param volume Volume multiplier.
     * @param loop Whether to loop.
     */
    public void PlayAmbience(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (clip == null || !IsValid(ambienceSource)) return;

        ambienceSource.clip = clip;
        ambienceSource.volume = volume;
        ambienceSource.loop = loop;
        ambienceSource.Play();
    }

    /// @brief Stop ambience playback immediately.
    public void StopAmbience()
    {
        if (!IsValid(ambienceSource)) return;
        ambienceSource.Stop();
    }

    // ───────────────────────────────── Weather ─────────────────────────────────

    /**
     * @brief Start looping weather on the weather channel.
     * @param clip Clip to play.
     * @param volume Volume multiplier.
     * @param loop Whether to loop.
     */
    public void PlayWeather(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (clip == null || !IsValid(weatherSource)) return;

        weatherSource.clip = clip;
        weatherSource.volume = volume;
        weatherSource.loop = loop;
        weatherSource.Play();
    }

    /// @brief Stop weather playback immediately.
    public void StopWeather()
    {
        if (!IsValid(weatherSource)) return;
        weatherSource.Stop();
    }

    // ───────────────────────────────── Music ─────────────────────────────────

    /**
     * @brief Start music playback on the music channel.
     * @param clip Clip to play.
     * @param volume Volume multiplier.
     * @param loop Whether to loop.
     */
    public void PlayMusic(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (clip == null || !IsValid(musicSource)) return;

        musicSource.clip = clip;
        musicSource.volume = volume;
        musicSource.loop = loop;
        musicSource.Play();
    }

    /**
     * @brief Stop music, optionally fading out over time.
     * @param fadeTime Fade duration in seconds. If <= 0, stops immediately.
     *
     * @warning
     * Each fade call starts a coroutine. Avoid calling this repeatedly.
     */
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

    private IEnumerator FadeOutMusic(float time)
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

    // ───────────────────────────────── Dual SFX ─────────────────────────────────

    /**
     * @brief Play two SFX clips with a delay between them (e.g., "snip" then "thud").
     * @param first First clip (optional).
     * @param second Second clip (optional).
     * @param delay Delay in seconds between clips.
     *
     * @note
     * Uses a coroutine. Keep calls event-driven (not per-frame).
     */
    public void PlayDualSFX(AudioClip first, AudioClip second, float delay)
    {
        if (!IsValid(sfxSource)) return;
        if (first == null && second == null) return;

        StartCoroutine(PlayDualRoutine(first, second, delay));
    }

    private IEnumerator PlayDualRoutine(AudioClip first, AudioClip second, float delay)
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

    /**
     * @section viz_audio_manager_relations Visual Relationships
     * @dot
     * digraph AudioManagerRelations {
     *   rankdir=LR;
     *   node [shape=box];
     *   AudioManager -> AudioSource [label="owns channels"];
     * }
     * @enddot
     */
}
