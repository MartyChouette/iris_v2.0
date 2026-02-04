/**
 * @file IrisQualityPreset.cs
 * @brief ScriptableObject defining a quality preset (Low/Medium/High).
 *
 * @details
 * Each preset stores tunable parameters for particle counts, pool sizes,
 * physics fidelity, and UI update rates. Applied at runtime by IrisQualityManager.
 *
 * @ingroup framework
 */

using UnityEngine;

[CreateAssetMenu(fileName = "NewQualityPreset", menuName = "Iris/Quality Preset")]
public class IrisQualityPreset : ScriptableObject
{
    [Header("Display")]
    [Tooltip("Name shown in the quality dropdown (e.g. Low, Medium, High).")]
    public string displayName = "Medium";

    [Header("Sap Particles")]
    [Tooltip("Multiplier for sap particle speed, size, and count. Maps to SapParticleController.sapIntensity.")]
    [Range(0f, 2f)]
    public float sapIntensity = 1f;

    [Tooltip("Max number of pooled ParticleSystems. Maps to SapParticleController.maxPoolSize.")]
    public int sapMaxPoolSize = 12;

    [Header("Sap Decals")]
    [Tooltip("Max number of decals before recycling oldest. Maps to SapDecalPool.maxPoolSize.")]
    public int decalMaxPoolSize = 100;

    [Header("Mesh Physics")]
    [Tooltip("SquishMove normal recalculation interval (in FixedUpdate frames). Higher = cheaper.")]
    [Min(1)]
    public int normalRecalcInterval = 3;

    [Tooltip("Multiplier for SquishMove jelly intensity. Maps to SquishMove.Intensity.")]
    [Range(0f, 2f)]
    public float squishIntensity = 1f;

    [Header("UI Effects")]
    [Tooltip("Seconds between TMP_FocusBlur mesh updates. Higher = cheaper.")]
    [Min(0.016f)]
    public float tmpBlurUpdateInterval = 0.05f;
}
