// File: FlowerSessionController.cs
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// High-level orchestrator for a single flower trimming session.
/// Talks to FlowerGameBrain to evaluate, then maps the normalized score into
/// final score / days-lived using FlowerTypeDefinition.
/// Also supports instant game-over when a critical part is detached.
/// </summary>
[DisallowMultipleComponent]
public class FlowerSessionController : MonoBehaviour
{
    [Header("Core Refs")]
    public FlowerGameBrain brain;
    public FlowerTypeDefinition flowerType;

    [Header("Events")]
    public UnityEvent onGameOver;
    public UnityEvent onSuccessfulEvaluation;

    [System.Serializable]
    public class EvaluationEvent : UnityEvent<FlowerGameBrain.EvaluationResult, int, int> { }

    [Header("Result Event (eval, finalScore, daysAlive)")]
    public EvaluationEvent onResult;

    [Header("Debug / Last Result")]
    public bool lastGameOver;
    public string lastGameOverReason;
    public int lastScore;
    public int lastDays;
    [Range(0f, 1f)] public float lastNormalizedScore;

    /// <summary>
    /// Call this when the player is done trimming and confirms the cut.
    /// Typically wired to a UI button.
    /// </summary>
    public void EvaluateNow()
    {
        if (brain == null)
        {
            Debug.LogWarning("[FlowerSessionController] No FlowerGameBrain assigned.", this);
            return;
        }

        var eval = brain.EvaluateFlower();

        bool gameOver = eval.isGameOver;
        string reason = eval.gameOverReason;
        float normalized = Mathf.Clamp01(eval.scoreNormalized);

        int finalScore = 0;
        int daysAlive = 0;

        // Use FlowerTypeDefinition mapping if present.
        if (flowerType != null)
        {
            // Some flower types may never hard-fail (e.g., tutorial flowers).
            if (!flowerType.allowGameOver)
            {
                gameOver = false;
            }

            if (!gameOver)
            {
                finalScore = flowerType.GetFinalScoreFromNormalized(normalized);
                daysAlive = flowerType.GetDaysFromNormalized(normalized);
            }
        }
        else
        {
            // Fallback mapping if no flowerType asset yet.
            if (!gameOver)
            {
                finalScore = Mathf.RoundToInt(normalized * 100f);
                daysAlive = Mathf.RoundToInt(Mathf.Lerp(1, 7, normalized));
            }
        }

        // If brain says gameOver, score/days stay at 0.
        lastGameOver = gameOver;
        lastGameOverReason = gameOver ? reason : "";
        lastNormalizedScore = gameOver ? 0f : normalized;
        lastScore = finalScore;
        lastDays = daysAlive;

        if (gameOver)
        {
            Debug.Log($"[FlowerSessionController] GAME OVER: {lastGameOverReason}", this);
            onGameOver?.Invoke();
        }
        else
        {
            Debug.Log($"[FlowerSessionController] Success: score={finalScore}, days={daysAlive}", this);
            onSuccessfulEvaluation?.Invoke();
        }

        onResult?.Invoke(eval, finalScore, daysAlive);
    }

    /// <summary>
    /// Optional hook for instant game-over when a critical part is detached / broken.
    /// Call this from FlowerPartRuntime (e.g., from its joint-break callback).
    /// </summary>
    public void NotifyPartDetached(FlowerPartRuntime part)
    {
        if (part == null || brain == null || brain.ideal == null)
            return;

        var rules = brain.ideal.partRules;
        IdealFlowerDefinition.PartRule rule = null;

        // Find the matching rule for this runtime part
        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i].partId == part.PartId)   // ← use PartId, not a raw field
            {
                rule = rules[i];
                break;
            }
        }

        if (rule == null)
            return;

        if (!rule.canCauseGameOver)
            return;

        // Policy:
        // - If the ideal says this part is Perfect, and the runtime part is
        //   now missing OR no longer Perfect, we hard fail immediately.
        bool missing = !part.isAttached;
        bool damaged = part.condition != rule.idealCondition;

        if (rule.idealCondition == FlowerPartCondition.Perfect && (missing || damaged))
        {
            lastGameOver = true;
            lastGameOverReason = $"Critical {rule.kind} '{rule.partId}' damaged/removed.";
            lastScore = 0;
            lastDays = 0;
            lastNormalizedScore = 0f;

            Debug.Log($"[FlowerSessionController] Instant GAME OVER: {lastGameOverReason}", this);
            onGameOver?.Invoke();

            // (Optional) you could also fire onResult here with a zero-score payload.
        }
    }

}
