/**
 * @file IrisQualityManager.cs
 * @brief Scene-scoped singleton that applies IrisQualityPreset values at runtime.
 *
 * @details
 * Finds active instances of SapParticleController, SapDecalPool, SquishMove,
 * and TMP_FocusBlur in the scene and pushes preset values to their public fields.
 *
 * Persistence:
 * - Saves selected preset index to PlayerPrefs key "Iris_QualityPreset".
 * - Auto-applies saved preset on Awake.
 *
 * Pattern:
 * - Scene-scoped singleton (no DontDestroyOnLoad), same as HorrorCameraManager.
 *
 * @ingroup framework
 */

using UnityEngine;

[DefaultExecutionOrder(-100)]
public class IrisQualityManager : MonoBehaviour
{
    public static IrisQualityManager Instance { get; private set; }

    private const string PREFS_KEY = "Iris_QualityPreset";

    [Header("Presets (ordered Low → High)")]
    [Tooltip("Quality presets in ascending order. Index 0 = lowest quality.")]
    public IrisQualityPreset[] presets;

    [Header("Debug")]
    public bool debugLogs = true;

    private int _activePresetIndex = -1;

    public int ActivePresetIndex => _activePresetIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (debugLogs)
                Debug.LogWarning("[IrisQualityManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        int saved = PlayerPrefs.GetInt(PREFS_KEY, -1);
        if (presets != null && presets.Length > 0)
        {
            if (saved >= 0 && saved < presets.Length)
                ApplyPreset(saved);
            else
                ApplyPreset(presets.Length / 2); // default to middle preset
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ApplyPreset(int index)
    {
        if (presets == null || index < 0 || index >= presets.Length)
        {
            Debug.LogWarning($"[IrisQualityManager] Invalid preset index {index}.");
            return;
        }

        IrisQualityPreset preset = presets[index];
        _activePresetIndex = index;

        PlayerPrefs.SetInt(PREFS_KEY, index);
        PlayerPrefs.Save();

        // ── SapParticleController ──
        var sap = SapParticleController.Instance;
        if (sap != null)
        {
            sap.sapIntensity = preset.sapIntensity;
            sap.maxPoolSize = preset.sapMaxPoolSize;
        }

        // ── SapDecalPool ──
        var decals = SapDecalPool.Instance;
        if (decals != null)
        {
            decals.maxPoolSize = preset.decalMaxPoolSize;
        }

        // ── SquishMove (all instances in scene) ──
        var squishes = FindObjectsByType<SquishMove>(FindObjectsSortMode.None);
        foreach (var squish in squishes)
        {
            squish.Intensity = preset.squishIntensity;
            squish.normalRecalcInterval = preset.normalRecalcInterval;
        }

        // ── TMP_FocusBlur (all instances in scene) ──
        var blurs = FindObjectsByType<TMP_FocusBlur>(FindObjectsSortMode.None);
        foreach (var blur in blurs)
        {
            blur.updateInterval = preset.tmpBlurUpdateInterval;
        }

        if (debugLogs)
            Debug.Log($"[IrisQualityManager] Applied preset '{preset.displayName}' (index {index}).");
    }
}
