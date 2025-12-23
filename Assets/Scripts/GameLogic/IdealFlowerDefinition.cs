/**
 * @file IdealFlowerDefinition.cs
 * @brief IdealFlowerDefinition script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

// File: IdealFlowerDefinition.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Flower/Ideal Flower Definition")]
    /**
     * @details
     * Intent:
     * - Scriptable authoring definition of "ideal" trimming outcomes:
     *   stem length, cut angle, and per-part rules.
     *
     * Rule types:
     * - Stem: idealStemLength + deltas define perfect vs. hard fail ranges.
     * - Cut Angle: idealCutAngleDeg + deltas define perfect vs. hard fail ranges.
     * - Weights: stemScoreWeight / cutAngleScoreWeight determine contribution to final normalized score.
     *
     * Game-over toggles:
     * - stemCanCauseGameOver / cutAngleCanCauseGameOver allow authoring "strict ideals."
     * - (Per-part rules extend this strictness to leaves/petals/etc.)
     *
     * Authoring notes:
     * - Treat deltas as "tolerance" knobs. Tightening them over time is a direct mechanical path to
     *   your metric-cruelty escalation without changing core code.
     *
     * Invariants:
     * - Perfect delta should be <= hard-fail delta for each measured dimension.
     

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
 * @ingroup tools
 */
public class IdealFlowerDefinition : ScriptableObject
{
    [Header("Stem Length Rules")]
    [Tooltip("Ideal length of the main stem after trimming, in world units or your chosen metric.")]
    public float idealStemLength = 1.0f;

    [Tooltip("If |current - ideal| > hardFailDelta and 'canCauseGameOver' is true, it's game over.")]
    public float stemHardFailDelta = 0.5f;

    [Tooltip("Inside this delta is treated as 'perfect' for scoring.")]
    public float stemPerfectDelta = 0.05f;

    [Tooltip("How much stem length contributes to the score (0-1).")]
    [Range(0f, 1f)] public float stemScoreWeight = 0.3f;

    public bool stemCanCauseGameOver = true;
    public bool stemContributesToScore = true;

    [Header("Cut Angle Rules")]
    [Tooltip("Ideal angle in degrees for the cut (e.g. 45 degrees).")]
    public float idealCutAngleDeg = 45f;

    [Tooltip("If |angle - ideal| > hardFailDelta and 'cutAngleCanCauseGameOver' is true, it's game over.")]
    public float cutAngleHardFailDelta = 20f;

    [Tooltip("Inside this delta is 'perfect' for scoring.")]
    public float cutAnglePerfectDelta = 3f;

    [Range(0f, 1f)] public float cutAngleScoreWeight = 0.2f;
    public bool cutAngleCanCauseGameOver = true;
    public bool cutAngleContributesToScore = true;

    //[Header("Per-Part Ideal Rules")]


    [Serializable]
    /**
     * @class PartRule
     * @brief PartRule component.
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
     * @ingroup tools
     */
    public class PartRule
    {
        [Tooltip("Must match the FlowerPartRuntime.partId.")]
        public string partId;

        public FlowerPartKind kind = FlowerPartKind.Leaf;
        public FlowerPartCondition idealCondition = FlowerPartCondition.Normal;

        [Tooltip("If true, harming/removing this part beyond rules can cause game over.")]
        public bool canCauseGameOver = false;

        [Tooltip("If true, this part is special (for UI / feedback).")]
        public bool isSpecial = false;

        [Tooltip("If true, differences on this part affect score.")]
        public bool contributesToScore = true;

        [Tooltip("If true, this part is allowed to be withered and still OK.")]
        public bool allowedWithered = true;

        [Tooltip("If false, missing this part counts against you.")]
        public bool allowedMissing = false;

        [Range(0f, 1f)]
        [Tooltip("Score importance of this part relative to other parts.")]
        public float scoreWeight = 1f;

        [Header("Ideal spatial reference (optional)")]
        [Tooltip("Ideal local position relative to the flower root for this part.")]
        public Vector3 idealLocalPosition;
        public Vector3 idealLocalEuler;
    }

    public List<PartRule> partRules = new List<PartRule>();
}
