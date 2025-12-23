/**
 * @file FlowerTypeAuthoring.cs
 * @brief FlowerTypeAuthoring script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup flowers_runtime
 */

using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor-side helper to "bake" the current runtime flower into:
/// - an IdealFlowerDefinition (what the brain compares against)
/// - a FlowerTypeDefinition (ID + display name + high-level tuning).
/// Attach this to the root of a flower prefab in the scene.
/// </summary>
[DisallowMultipleComponent]
/**
 * @class FlowerTypeAuthoring
 * @brief FlowerTypeAuthoring component.
 * @details
 * Responsibilities:
 * - (Documented) See fields and methods below.
 *
 * Unity lifecycle:
 * - Awake(): cache references / validate setup.
 * - OnEnable()/OnDisable(): hook/unhook events.
 * - Update(): per-frame behavior (if any).
 *
 * Gotchas:
 * - Keep hot paths allocation-free (Update/cuts/spawns).
 * - Prefer event-driven UI updates over per-frame string building.
 *
 * @ingroup flowers_runtime
 */
public class FlowerTypeAuthoring : MonoBehaviour
{
    [Header("Meta")]
    [Tooltip("Pretty name for this flower (used for UI / default displayName).")]
    public string flowerName = "New Flower Type";

    [Tooltip("Short ID for this flower type (if empty, we'll generate from flowerName when baking).")]
    public string flowerIdOverride;

    [Header("Runtime Refs")]
    [Tooltip("If empty, we'll auto-gather from children on bake.")]
    public FlowerStemRuntime stem;

    [Tooltip("Optional explicit list of parts; if empty, we auto-gather from children on bake.")]
    public List<FlowerPartRuntime> parts = new List<FlowerPartRuntime>();

    [Header("Assets To Bake Into")]
    [Tooltip("Where the ideal stem/part rules get stored.")]
    public IdealFlowerDefinition idealAsset;

    [Tooltip("High-level type definition asset (ID, display name, score mapping etc).")]
    public FlowerTypeDefinition flowerTypeAsset;

    [Header("Default Stem Settings (for Ideal asset)")]
    public float defaultStemHardFailDelta = 0.5f;
    public float defaultStemPerfectDelta = 0.05f;
    [Range(0f, 1f)] public float defaultStemScoreWeight = 0.4f;

    [Header("Default Cut Angle Settings (for Ideal asset)")]
    public float defaultCutAngleHardFailDelta = 15f;
    public float defaultCutAnglePerfectDelta = 3f;
    [Range(0f, 1f)] public float defaultCutAngleScoreWeight = 0.4f;

#if UNITY_EDITOR

    // ───────────────────── Editor Buttons ─────────────────────

    [ContextMenu("Flower/Bake Ideal From Current Pose")]
    public void BakeIdealFromCurrentPose_Context()
    {
        BakeIdealFromCurrentPose();
        Debug.Log($"[FlowerTypeAuthoring] Baked IdealFlowerDefinition from '{name}'.", this);
    }

    [ContextMenu("Flower/Bake Type Asset From Settings")]
    public void BakeToTypeAsset_Context()
    {
        BakeToTypeAsset();
        Debug.Log($"[FlowerTypeAuthoring] Baked FlowerTypeDefinition from '{name}'.", this);
    }

#endif

    // ───────────────────── Core Baking Logic ─────────────────────

    /// <summary>
    /// Reads the current scene instance (stem + parts) and writes rules into idealAsset.
    /// </summary>
    public void BakeIdealFromCurrentPose()
    {
#if UNITY_EDITOR
        if (idealAsset == null)
        {
            Debug.LogWarning("[FlowerTypeAuthoring] No IdealFlowerDefinition asset assigned.", this);
            return;
        }

        // Auto-gather if not wired.
        if (stem == null)
            stem = GetComponentInChildren<FlowerStemRuntime>();

        if (parts == null)
            parts = new List<FlowerPartRuntime>();
        if (parts.Count == 0)
            GetComponentsInChildren(true, parts);

        // ── Stem rules ──
        if (stem != null)
        {
            idealAsset.idealStemLength = stem.CurrentLength;
        }

        idealAsset.stemHardFailDelta = defaultStemHardFailDelta;
        idealAsset.stemPerfectDelta = defaultStemPerfectDelta;
        idealAsset.stemScoreWeight = defaultStemScoreWeight;

        // Cut-angle rules
        idealAsset.cutAngleHardFailDelta = defaultCutAngleHardFailDelta;
        idealAsset.cutAnglePerfectDelta = defaultCutAnglePerfectDelta;
        idealAsset.cutAngleScoreWeight = defaultCutAngleScoreWeight;

        // Reasonable defaults; you can tweak in the asset later.
        idealAsset.stemCanCauseGameOver = true;
        idealAsset.stemContributesToScore = true;
        idealAsset.cutAngleCanCauseGameOver = true;
        idealAsset.cutAngleContributesToScore = true;

        // ── Per-part rules ──
        idealAsset.partRules.Clear();

        foreach (var p in parts)
        {
            if (p == null || string.IsNullOrEmpty(p.PartId))
            {
                Debug.LogWarning(
                    $"[{nameof(FlowerTypeAuthoring)}] Skipping part without PartId on '{p?.name}'.",
                    this);
                continue;
            }

            var rule = new IdealFlowerDefinition.PartRule
            {
                partId = p.PartId,
                kind = p.kind,

                // The CURRENT runtime condition becomes the IDEAL condition.
                idealCondition = p.condition,

                // ---------- NEW: Default rule settings ----------
                canCauseGameOver = false,
                isSpecial = false,
                contributesToScore = true,
                allowedWithered = false,
                allowedMissing = false,
                scoreWeight = 0.1f,

                // ---------- Pose ----------
                idealLocalPosition = p.transform.localPosition,
                idealLocalEuler = p.transform.localEulerAngles
            };

            idealAsset.partRules.Add(rule);
        }

        EditorUtility.SetDirty(idealAsset);

#endif
    }

    /// <summary>
    /// Writes identity / high-level settings into the FlowerTypeDefinition asset.
    /// We keep this conservative and only touch fields we know exist (flowerId, displayName, description).
    /// </summary>
    public void BakeToTypeAsset()
    {
#if UNITY_EDITOR
        if (flowerTypeAsset == null)
        {
            Debug.LogWarning("[FlowerTypeAuthoring] No FlowerTypeDefinition asset assigned to bake into.", this);
            return;
        }

        // Identity
        string id = !string.IsNullOrEmpty(flowerIdOverride)
            ? SanitizeId(flowerIdOverride)
            : SanitizeId(flowerName);

        flowerTypeAsset.flowerId = id;
        flowerTypeAsset.displayName = string.IsNullOrEmpty(flowerName) ? "Flower" : flowerName;

        // Only touch description if it's empty so you can hand-write later.
        if (string.IsNullOrEmpty(flowerTypeAsset.description))
        {
            flowerTypeAsset.description = $"A carefully trimmed {flowerTypeAsset.displayName}.";
        }

        // allowGameOver / curves / days mapping are tuned directly on the asset.

        EditorUtility.SetDirty(flowerTypeAsset);
#endif
    }

#if UNITY_EDITOR
    private static string SanitizeId(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "flower_type";

        // Replace spaces and invalid chars with underscores.
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '_');
        }

        input = input.Replace(' ', '_');
        return input.ToLowerInvariant();
    }
#endif
}
