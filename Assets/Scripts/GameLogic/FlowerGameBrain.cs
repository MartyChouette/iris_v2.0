// File: FlowerGameBrain.cs
using System.Collections.Generic;
using UnityEngine;

public class FlowerGameBrain : MonoBehaviour
{
    [Header("Design Data")]
    public IdealFlowerDefinition ideal;
    public FlowerStemRuntime stem;

    [Header("Runtime Parts (auto-populated if empty)")]
    public List<FlowerPartRuntime> parts = new List<FlowerPartRuntime>();

    [Header("Debug Output")]
    public bool lastWasGameOver;
    public string lastGameOverReason;
    [Range(0f, 1f)] public float lastScoreNormalized;

    private Dictionary<string, IdealFlowerDefinition.PartRule> ruleLookup =
        new Dictionary<string, IdealFlowerDefinition.PartRule>();

    private Dictionary<string, FlowerPartRuntime> partLookup =
        new Dictionary<string, FlowerPartRuntime>();

    private void Awake()
    {
        BuildLookups();
    }

    private void OnValidate()
    {
        if (ideal != null)
            BuildRuleLookupOnly();
    }

    private void BuildLookups()
    {
        BuildRuleLookupOnly();

        partLookup.Clear();
        if (parts.Count == 0)
        {
            GetComponentsInChildren(true, parts);
        }

        foreach (var p in parts)
        {
            if (p == null || string.IsNullOrEmpty(p.partId))
                continue;

            if (!partLookup.ContainsKey(p.partId))
                partLookup.Add(p.partId, p);
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

    // ────────────────── Evaluation API ──────────────────

    public struct EvaluationResult
    {
        public bool isGameOver;
        public string gameOverReason;
        public float scoreNormalized; // 0..1
    }

    /// <summary>
    /// Call this when the player "finishes" the trimming step.
    /// </summary>
    public EvaluationResult EvaluateFlower()
    {
        if (ideal == null)
        {
            Debug.LogWarning("FlowerGameBrain has no IdealFlowerDefinition assigned.");
            return default;
        }

        BuildLookups();

        bool gameOver = false;
        string reason = "";
        float totalScoreWeight = 0f;
        float accumulatedScore = 0f;

        // 1) Stem length
        if (stem != null && ideal.stemContributesToScore)
        {
            float currentLen = stem.CurrentLength;
            float delta = Mathf.Abs(currentLen - ideal.idealStemLength);

            // Hard fail?
            if (ideal.stemCanCauseGameOver && delta > ideal.stemHardFailDelta)
            {
                gameOver = true;
                reason = $"Stem length off by {delta:F2} (hard fail).";
            }

            // Score contribution (if not game over yet or even if – up to you)
            float stemScore = Mathf.Clamp01(1f - (delta / ideal.stemHardFailDelta));
            totalScoreWeight += ideal.stemScoreWeight;
            accumulatedScore += stemScore * ideal.stemScoreWeight;
        }

        // 2) Cut angle
        if (!gameOver && stem != null && ideal.cutAngleContributesToScore)
        {
            float angle = stem.GetCurrentCutAngleDeg(Vector3.up);
            float delta = Mathf.Abs(angle - ideal.idealCutAngleDeg);

            if (ideal.cutAngleCanCauseGameOver && delta > ideal.cutAngleHardFailDelta)
            {
                gameOver = true;
                reason = $"Cut angle off by {delta:F1}° (hard fail).";
            }

            float angleScore = Mathf.Clamp01(1f - (delta / ideal.cutAngleHardFailDelta));
            totalScoreWeight += ideal.cutAngleScoreWeight;
            accumulatedScore += angleScore * ideal.cutAngleScoreWeight;
        }

        // 3) Leaves / petals
        if (!gameOver)
        {
            foreach (var kvp in ruleLookup)
            {
                var rule = kvp.Value;
                FlowerPartRuntime runtime = null;
                partLookup.TryGetValue(rule.partId, out runtime);

                bool exists = runtime != null;
                bool attached = exists && runtime.isAttached;
                FlowerPartCondition cond = exists ? runtime.condition : FlowerPartCondition.Withered;

                // ------- Hard fail logic per-part -------
                if (rule.canCauseGameOver)
                {
                    // Example 1: perfect part must not be pulled or damaged
                    if (rule.idealCondition == FlowerPartCondition.Perfect)
                    {
                        if (!attached)
                        {
                            gameOver = true;
                            reason = $"Perfect {rule.kind} '{rule.partId}' was removed.";
                            break;
                        }

                        if (cond != FlowerPartCondition.Perfect)
                        {
                            gameOver = true;
                            reason = $"Perfect {rule.kind} '{rule.partId}' was damaged.";
                            break;
                        }
                    }

                    // Example 2: part must exist at all
                    if (!rule.allowedMissing && !attached)
                    {
                        gameOver = true;
                        reason = $"{rule.kind} '{rule.partId}' was removed and is not allowed to be missing.";
                        break;
                    }
                }

                // ------- Scoring logic per-part -------
                if (!rule.contributesToScore)
                    continue;

                float partScore = 0f;

                if (!exists || !attached)
                {
                    // Missing part
                    partScore = rule.allowedMissing ? 1f : 0f;
                }
                else
                {
                    if (cond == rule.idealCondition)
                    {
                        partScore = 1f;  // perfect match
                    }
                    else if (cond == FlowerPartCondition.Withered && rule.allowedWithered)
                    {
                        // Slight penality for allowed withering
                        partScore = 0.5f;
                    }
                    else
                    {
                        partScore = 0.2f; // wrong condition
                    }
                }

                totalScoreWeight += rule.scoreWeight;
                accumulatedScore += partScore * rule.scoreWeight;
            }
        }

        float normalized = 0f;
        if (totalScoreWeight > 0.0001f)
        {
            normalized = Mathf.Clamp01(accumulatedScore / totalScoreWeight);
        }

        var result = new EvaluationResult
        {
            isGameOver = gameOver,
            gameOverReason = gameOver ? reason : "",
            scoreNormalized = gameOver ? 0f : normalized
        };

        lastWasGameOver = result.isGameOver;
        lastGameOverReason = result.gameOverReason;
        lastScoreNormalized = result.scoreNormalized;

        return result;
    }
}
