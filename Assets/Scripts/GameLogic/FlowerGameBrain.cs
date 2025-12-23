/**
 * @file FlowerGameBrain.cs
 * @brief FlowerGameBrain script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup flowers_runtime
 */

// File: FlowerGameBrain.cs
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
    /**
     * @details
     * Intent:
     * - Evaluates the current flower state against @ref IdealFlowerDefinition rules and produces a result.
     * - Maintains lookups:
     *   ruleLookup: partId -> ideal rule
     *   partLookup: partId -> runtime part instance
     *
     * Runtime wiring:
     * - Awake() calls BuildLookups() and injects this brain into all @ref FlowerPartRuntime children.
     *   This makes parts "judge-aware" without manual wiring.
     *
     * Calibration:
     * - angleOffsetDeg exists so scoring space and HUD angle space can be aligned consistently.
     *
     * Invariants:
     * - Each @ref FlowerPartRuntime intended for scoring must have a stable PartId.
     * - PartId values must match IdealFlowerDefinition.PartRule.partId authoring exactly.
     *
     * Gotchas:
     * - Duplicate PartIds will be ignored in partLookup (first add wins). Treat PartId uniqueness as a hard rule.
     * - If 'parts' is empty, children are auto-collected; be careful with inactive children and debug variants.
     

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
 *
 * @section viz_flowergamebrain Visual Relationships
 * @dot
 * digraph FlowerGameBrain {
 *   rankdir=LR;
 *   node [shape=box];
 *   FlowerGameBrain -> FlowerTypeDefinition;
 *   FlowerGameBrain -> IdealFlowerDefinition;
 *   FlowerGameBrain -> FlowerStemRuntime;
 *   FlowerGameBrain -> FlowerPartRuntime;
 * }
 * @enddot
 */
public class FlowerGameBrain : MonoBehaviour
{
    [Header("Design Data")]
    public IdealFlowerDefinition ideal;
    public FlowerStemRuntime stem;

    [Header("Runtime Parts")]
    public List<FlowerPartRuntime> parts = new List<FlowerPartRuntime>();

    [Header("Debug Output")]
    public bool lastWasGameOver;
    public string lastGameOverReason;
    [Range(0f, 1f)] public float lastScoreNormalized;

    [Header("Angle Calibration")]
    [Tooltip("Calibration offset (degrees) so angle scoring & HUD share the same angle space.")]
    public float angleOffsetDeg = 0f;

    private Dictionary<string, IdealFlowerDefinition.PartRule> ruleLookup =
        new Dictionary<string, IdealFlowerDefinition.PartRule>();

    private Dictionary<string, FlowerPartRuntime> partLookup =
        new Dictionary<string, FlowerPartRuntime>();

    public bool IsPartMarkedPerfect(string partId)
    {
        if (ruleLookup.TryGetValue(partId, out var rule))
            return rule.idealCondition == FlowerPartCondition.Perfect;
        return false;
    }

    private void Awake()
    {
        BuildLookups();

        // Inject brain reference into all parts
        if (parts.Count == 0)
            GetComponentsInChildren(true, parts);

        foreach (var p in parts)
        {
            if (p == null) continue;
            p.brain = this;
        }
    }

    private void OnValidate()
    {
        BuildRuleLookupOnly();
    }

    private void BuildLookups()
    {
        BuildRuleLookupOnly();

        partLookup.Clear();
        if (parts.Count == 0)
            GetComponentsInChildren(true, parts);

        foreach (var p in parts)
        {
            if (p == null || string.IsNullOrEmpty(p.PartId))
                continue;

            if (!partLookup.ContainsKey(p.PartId))
                partLookup.Add(p.PartId, p);
        }
    }

    private void BuildRuleLookupOnly()
    {
        ruleLookup.Clear();
        if (ideal == null) return;

        foreach (var rule in ideal.partRules)
        {
            if (rule == null || string.IsNullOrEmpty(rule.partId))
                continue;
            if (!ruleLookup.ContainsKey(rule.partId))
                ruleLookup.Add(rule.partId, rule);
        }
    }

    public struct EvaluationResult
    {
        public bool isGameOver;
        public string gameOverReason;
        public float scoreNormalized;
    }

    public EvaluationResult EvaluateFlower()
    {
        BuildLookups();

        bool gameOver = false;
        string reason = "";

        float totalScoreWeight = 0f;
        float accumulatedScore = 0f;

        // --- 1) Stem length ---
        if (stem != null && ideal != null && ideal.stemContributesToScore)
        {
            float currentLen = stem.CurrentLength;
            float delta = Mathf.Abs(currentLen - ideal.idealStemLength);
            float signedDelta = currentLen - ideal.idealStemLength;

            if (ideal.stemCanCauseGameOver &&
                delta > ideal.stemHardFailDelta)
            {
                gameOver = true;
                reason = signedDelta < 0f
                    ? "Crown decapitated (cut too high)"
                    : "Stem cut too low";
            }

            float score = Mathf.Clamp01(1f - delta / ideal.stemHardFailDelta);
            totalScoreWeight += ideal.stemScoreWeight;
            accumulatedScore += score * ideal.stemScoreWeight;
        }

        // --- 2) Cut angle ---
        if (ideal != null && stem != null && ideal.cutAngleContributesToScore)
        {
            // Raw physical angle from the stem / plane relationship.
            float rawAngle = stem.GetCurrentCutAngleDeg(Vector3.up);

            // Calibrated angle so that scoring and HUD share the same space.
            float cutAngle = Mathf.DeltaAngle(rawAngle, angleOffsetDeg);

            float idealAngle = ideal.idealCutAngleDeg;
            float delta = Mathf.Abs(Mathf.DeltaAngle(cutAngle, idealAngle));

            if (ideal.cutAngleCanCauseGameOver &&
                delta > ideal.cutAngleHardFailDelta)
            {
                gameOver = true;
                reason = $"Cut angle off by {delta:F1}° (hard fail)";
            }

            float score = Mathf.Clamp01(1f - delta / ideal.cutAngleHardFailDelta);
            totalScoreWeight += ideal.cutAngleScoreWeight;
            accumulatedScore += score * ideal.cutAngleScoreWeight;
        }

        // --- 3) Leaves & Petals / Parts ---
        bool specialPartRemoved = false;

        foreach (var kvp in ruleLookup)
        {
            var rule = kvp.Value;
            partLookup.TryGetValue(rule.partId, out var runtime);

            bool exists = runtime != null;
            bool attached = exists && runtime.isAttached;
            FlowerPartCondition cond =
                exists ? runtime.condition : FlowerPartCondition.Withered;

            // Immediate crown failure at grading time.
            if (rule.canCauseGameOver &&
                rule.kind == FlowerPartKind.Crown &&
                !attached)
            {
                gameOver = true;
                reason = "Crown was completely removed";
                // We still continue scoring so we can show a meaningful percentage.
            }

            if (rule.isSpecial && !attached)
            {
                specialPartRemoved = true;
            }

            if (!rule.contributesToScore)
                continue;

            float partScore = 0f;

            if (!attached)
            {
                partScore = rule.allowedMissing ? 0.5f : 0f;
            }
            else if (cond == rule.idealCondition)
            {
                partScore = 1f;
            }
            else if (cond == FlowerPartCondition.Withered && rule.allowedWithered)
            {
                partScore = 0.5f;
            }
            else
            {
                partScore = 0.2f;
            }

            totalScoreWeight += rule.scoreWeight;
            accumulatedScore += partScore * rule.scoreWeight;
        }

        float normalizedScore = 0f;
        if (totalScoreWeight > 0f)
        {
            normalizedScore = Mathf.Clamp01(accumulatedScore / totalScoreWeight);
        }

        // Grading-time game over for removed special parts.
        if (!gameOver && specialPartRemoved)
        {
            gameOver = true;
            reason = "You removed a special leaf.";
        }

        var result = new EvaluationResult
        {
            isGameOver = gameOver,
            gameOverReason = gameOver ? reason : "",
            // IMPORTANT: keep the score even on game over.
            scoreNormalized = normalizedScore
        };

        lastWasGameOver = result.isGameOver;
        lastGameOverReason = result.gameOverReason;
        lastScoreNormalized = result.scoreNormalized;

        return result;
    }
}
