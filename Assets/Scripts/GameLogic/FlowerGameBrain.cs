// File: FlowerGameBrain.cs
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
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
        // inject brain reference into all parts
        foreach (var p in parts)
            if (p != null)
                p.brain = this;
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
        if (stem != null && ideal.stemContributesToScore)
        {
            float currentLen = stem.CurrentLength;
            float delta = Mathf.Abs(currentLen - ideal.idealStemLength);
            float signedDelta = currentLen - ideal.idealStemLength;

            if (ideal.stemCanCauseGameOver &&
                delta > ideal.stemHardFailDelta)
            {
                gameOver = true;
                reason = signedDelta < 0f ?
                    "Crown decapitated (cut too high)" :
                    $"Stem length off by {delta:F2} (hard fail)";
            }

            float score = Mathf.Clamp01(1f - delta / ideal.stemHardFailDelta);
            totalScoreWeight += ideal.stemScoreWeight;
            accumulatedScore += score * ideal.stemScoreWeight;
        }

        // --- 2) Cut angle ---
        if (!gameOver && stem != null && ideal.cutAngleContributesToScore)
        {
            float angle = stem.GetCurrentCutAngleDeg(Vector3.up);
            float delta = Mathf.Abs(angle - ideal.idealCutAngleDeg);

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

        // --- 3) Leaves & Petals ---
        if (!gameOver)
        {
            foreach (var kvp in ruleLookup)
            {
                var rule = kvp.Value;
                partLookup.TryGetValue(rule.partId, out var runtime);

                bool exists = runtime != null;
                bool attached = exists && runtime.isAttached;
                FlowerPartCondition cond =
                    exists ? runtime.condition : FlowerPartCondition.Withered;

                // Critical part removed
                if (rule.canCauseGameOver && !attached)
                {
                    gameOver = true;
                    reason = $"{rule.kind} '{rule.partId}' is missing";
                    break;
                }

                if (!rule.contributesToScore)
                    continue;

                float partScore = 0f;




                //HOW MUCH TO GIVE OR TAKE AWAY IF LEAF STILL THERE OR GONE
                if (!attached)
                {
                    // Optional parts still lose some score when removed.
                    partScore = rule.allowedMissing ? 0.5f : 0f;
                }
                else if (cond == rule.idealCondition)
                    partScore = 1f;
                else if (cond == FlowerPartCondition.Withered && rule.allowedWithered)
                    partScore = 0.5f;
                else
                    partScore = 0.2f;


                totalScoreWeight += rule.scoreWeight;
                accumulatedScore += partScore * rule.scoreWeight;
            }
        }


        // --- 4) Global: All parts detached = instant fail ---
        if (!gameOver)
        {
            int totalParts = partLookup.Count;
            int attachedParts = 0;

            foreach (var p in partLookup.Values)
                if (p != null && p.isAttached)
                    attachedParts++;

            if (totalParts > 0 && attachedParts == 0)
            {
                gameOver = true;
                reason = "All parts removed – flower is dead.";
            }
        }

        float normalizedScore =
            totalScoreWeight > 0.001f ?
            Mathf.Clamp01(accumulatedScore / totalScoreWeight) :
            0f;

        var result = new EvaluationResult
        {
            isGameOver = gameOver,
            gameOverReason = gameOver ? reason : "",
            scoreNormalized = gameOver ? 0f : normalizedScore
        };

        lastWasGameOver = result.isGameOver;
        lastGameOverReason = result.gameOverReason;
        lastScoreNormalized = result.scoreNormalized;

        return result;
    }
}
