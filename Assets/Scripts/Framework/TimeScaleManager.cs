/**
 * @file TimeScaleManager.cs
 * @brief Centralized time-scale authority for the entire game.
 *
 * @details
 * Intent:
 * - Eliminate fragile direct Time.timeScale writes scattered across systems.
 * - All systems that modify Time.timeScale should go through this manager
 *   using priority-keyed requests.
 *
 * Priority model:
 * - Lower priority number = higher importance.
 * - PRIORITY_PAUSE (0) always beats PRIORITY_GAME_OVER (10) which always beats PRIORITY_JUICE (20).
 * - When multiple requests are active, the lowest-numbered priority wins.
 * - When all requests are cleared, timeScale resets to 1.
 *
 * Scene transitions:
 * - All requests are automatically cleared on single-mode scene loads,
 *   preventing stale slow-mo/pause from poisoning the next scene.
 *
 * Usage:
 * @code
 *   TimeScaleManager.Set(TimeScaleManager.PRIORITY_PAUSE, 0f);   // pause
 *   TimeScaleManager.Clear(TimeScaleManager.PRIORITY_PAUSE);      // unpause
 *   TimeScaleManager.ClearAll();                                   // before scene reload
 * @endcode
 *
 * @ingroup framework
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TimeScaleManager
{
    /// <summary>Pause menu: nothing moves.</summary>
    public const int PRIORITY_PAUSE = 0;

    /// <summary>Game-over slow-mo / full pause.</summary>
    public const int PRIORITY_GAME_OVER = 10;

    /// <summary>Juice moment effects (freeze frame, slow-mo).</summary>
    public const int PRIORITY_JUICE = 20;

    // SortedList: key = priority (ascending), value = requested scale.
    // Values[0] is always the highest-importance (lowest-number) active request.
    private static readonly SortedList<int, float> s_active = new SortedList<int, float>();
    private static float s_baseFixedDelta = 0.02f;

    /// <summary>
    /// Domain-reload safety: clears static state when entering play mode
    /// (handles both fresh starts and Enter Play Mode Options with domain reload disabled).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReset()
    {
        s_active.Clear();
        s_baseFixedDelta = 0.02f;
    }

    /// <summary>
    /// Captures the engine's initial fixedDeltaTime and subscribes to scene-load cleanup.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        s_baseFixedDelta = Time.fixedDeltaTime;
        SceneManager.sceneLoaded -= OnSceneLoaded; // prevent double-sub across domain reloads
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            s_active.Clear();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = s_baseFixedDelta;
        }
    }

    /// <summary>
    /// Register or update a time-scale request at the given priority.
    /// Lower priority numbers are more important (PRIORITY_PAUSE always wins).
    /// </summary>
    public static void Set(int priority, float scale)
    {
        s_active[priority] = scale;
        Apply();
    }

    /// <summary>
    /// Remove the request at the given priority.
    /// If no requests remain, timeScale resets to 1.
    /// </summary>
    public static void Clear(int priority)
    {
        s_active.Remove(priority);
        Apply();
    }

    /// <summary>
    /// Remove all requests and restore timeScale to 1.
    /// Call before scene transitions to guarantee clean state.
    /// </summary>
    public static void ClearAll()
    {
        s_active.Clear();
        Apply();
    }

    /// <summary>The currently applied time scale (winning request, or 1 if no requests).</summary>
    public static float ActiveTimeScale => s_active.Count > 0 ? s_active.Values[0] : 1f;

    private static void Apply()
    {
        float scale = s_active.Count > 0 ? s_active.Values[0] : 1f;
        Time.timeScale = scale;

        // Only adjust fixedDeltaTime for non-zero scales.
        // At timeScale 0 FixedUpdate doesn't run, so fixedDeltaTime is irrelevant.
        if (scale > 0f)
            Time.fixedDeltaTime = s_baseFixedDelta * scale;
    }
}
