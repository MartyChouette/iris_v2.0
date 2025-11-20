// File: FlowerSessionController.cs
using UnityEngine;
using UnityEngine.Events;

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
    public class ResultEvent : UnityEvent<FlowerGameBrain.EvaluationResult, int, int> { }
    [Header("Detailed Result Event")]
    [Tooltip("Invoked with (evaluationResult, finalScore, daysAlive).")]
    public ResultEvent onResult;

    [Header("Debug Last Result")]
    public bool lastGameOver;
    public string lastGameOverReason;
    public int lastScore;
    public int lastDays;

    private void Reset()
    {
        if (brain == null)
            brain = GetComponentInChildren<FlowerGameBrain>();

        if (flowerType == null && brain != null && brain.ideal != null)
        {
            // optional: you can try to find a FlowerTypeDefinition nearby, but
            // usually you'll assign this manually in inspector or via spawner.
        }
    }

    /// <summary>
    /// Call this when the player is DONE with trimming/cutting this flower.
    /// e.g. scissors put down, confirm button pressed, etc.
    /// </summary>
    public void FinalizeAndScore()
    {
        if (brain == null)
        {
            Debug.LogError("[FlowerSessionController] No FlowerGameBrain assigned.");
            return;
        }

        var eval = brain.EvaluateFlower();

        // Apply global flowerType rules.
        bool gameOver = eval.isGameOver;
        string reason = eval.gameOverReason;

        int finalScore = 0;
        int daysAlive = 0;

        if (flowerType != null)
        {
            if (!flowerType.allowGameOver)
            {
                // Even if the brain says game over, this flower type may "never hard-fail";
                // treat it as zero score / zero days but not a hard fail, if you want.
                // Here we just clear the gameOver flag but keep reason for debug.
                gameOver = false;
            }

            if (!gameOver)
            {
                finalScore = flowerType.GetFinalScoreFromNormalized(eval.scoreNormalized);
                daysAlive = flowerType.GetDaysFromNormalized(eval.scoreNormalized);
            }
        }
        else
        {
            // Fallback mapping if no flowerType asset yet.
            if (!gameOver)
            {
                finalScore = Mathf.RoundToInt(eval.scoreNormalized * 100f);
                daysAlive = Mathf.RoundToInt(Mathf.Lerp(1, 7, eval.scoreNormalized));
            }
        }

        lastGameOver = gameOver;
        lastGameOverReason = reason;
        lastScore = finalScore;
        lastDays = daysAlive;

        // Fire events for UI / flow.
        if (gameOver)
        {
            Debug.Log($"[FlowerSessionController] GAME OVER: {reason}");
            onGameOver?.Invoke();
        }
        else
        {
            Debug.Log($"[FlowerSessionController] Success. Score={finalScore}, Days={daysAlive}");
            onSuccessfulEvaluation?.Invoke();
        }

        onResult?.Invoke(eval, finalScore, daysAlive);
    }

    /// <summary>
    /// Call this when a leaf or petal is detached / broken.
    /// Lets us do instant game-over if it's a 'perfect' or forbidden part.
    /// </summary>
    public void NotifyPartDetached(FlowerPartRuntime part)
    {
        if (part == null || flowerType == null || flowerType.ideal == null)
            return;

        // Optional "instant death" for perfect parts, depending on flowerType rules:
        var rules = flowerType.ideal.partRules;
        IdealFlowerDefinition.PartRule rule = null;

        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i].partId == part.partId)
            {
                rule = rules[i];
                break;
            }
        }

        if (rule == null)
            return;

        // If the type says "any perfect damage is game over", or the rule itself can cause game over:
        bool isPerfectIdeal = (rule.idealCondition == FlowerPartCondition.Perfect);
        bool typeInstantPerfectDeath = flowerType.globalPerfectDamageCausesGameOver && isPerfectIdeal;

        if (!typeInstantPerfectDeath && !rule.canCauseGameOver)
            return;

        // The part is now detached / damaged:
        bool removed = !part.isAttached;
        bool damagedCondition = (part.condition != rule.idealCondition);

        if (removed || damagedCondition)
        {
            // Hard fail immediately.
            lastGameOver = true;
            lastGameOverReason = $"Critical {rule.kind} '{rule.partId}' damaged/removed.";
            lastScore = 0;
            lastDays = 0;

            Debug.Log($"[FlowerSessionController] Instant GAME OVER: {lastGameOverReason}");
            onGameOver?.Invoke();

            // Optional: you could also call onResult with a "zero score" evaluation
            // if you want your UI to get a result payload.
        }
    }
}
