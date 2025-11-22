// File: FlowerSessionController.cs
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class FlowerSessionController : MonoBehaviour
{
    [Header("Core Refs")]
    public FlowerGameBrain brain;
    public FlowerTypeDefinition FlowerType;

    [Header("Events")]
    public UnityEvent OnGameOver;
    public UnityEvent OnSuccessfulEvaluation;
    public UnityEvent<FlowerGameBrain.EvaluationResult, int, int> OnResult;

    [Header("Debug / Last Result")]
    public bool lastGameOver;
    public string lastGameOverReason;
    public int lastScore;
    public int lastDays;
    public float lastNormalizedScore;

    // ─────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Hard-fails the session immediately (e.g., crown ripped off, stem cut way too high).
    /// This now ALSO pushes a result through the scoring pipeline so HUD can show it.
    /// </summary>
    public void ForceGameOver(string reason)
    {
        if (brain == null)
        {
            // Fallback old behavior if brain missing
            lastGameOver = true;
            lastGameOverReason = reason;

            Debug.Log($"[FlowerSessionController] GAME OVER (no brain): {reason}", this);
            OnGameOver?.Invoke();
            return;
        }

        // Build a "forced" evaluation result.
        var result = new FlowerGameBrain.EvaluationResult
        {
            isGameOver = true,
            gameOverReason = reason,
            // Use whatever the brain last had (likely 0 if we never evaluated)
            scoreNormalized = brain.lastScoreNormalized
        };

        ApplyResult(result);
    }

    /// <summary>
    /// Call this to evaluate the current flower state (e.g. when the player confirms they're done).
    /// </summary>
    public void EvaluateCurrentFlower()
    {
        if (brain == null)
            return;

        var result = brain.EvaluateFlower();
        ApplyResult(result);
    }

    /// <summary>
    /// Call this right after a stem cut to see if we've cut "too high / too short"
    /// and should instantly game over.
    /// </summary>
    public void CheckStemCutImmediate()
    {
        if (brain == null || brain.ideal == null || brain.stem == null)
            return;

        float currentLen = brain.stem.CurrentLength;
        float signedDelta = currentLen - brain.ideal.idealStemLength;
        float absDelta = Mathf.Abs(signedDelta);

        // Only treat "too short" as instant fail: cut up into the crown area.
        if (brain.ideal.stemCanCauseGameOver &&
            absDelta > brain.ideal.stemHardFailDelta &&
            signedDelta < 0f)
        {
            ForceGameOver("Stem cut too short (cut too high towards the crown).");
        }
    }

    // ─────────────────────────────────────────────
    // INTERNAL: unified result handling
    // ─────────────────────────────────────────────

    private void ApplyResult(FlowerGameBrain.EvaluationResult result)
    {
        if (brain != null)
        {
            brain.lastWasGameOver = result.isGameOver;
            brain.lastGameOverReason = result.gameOverReason;
            brain.lastScoreNormalized = result.scoreNormalized;
        }

        // Allow a FlowerType to soften "fatal" results if allowGameOver=false
        bool finalIsGameOver = result.isGameOver;
        string finalReason = result.gameOverReason;

        if (FlowerType != null && !FlowerType.allowGameOver && result.isGameOver)
        {
            finalIsGameOver = false;
            // keep reason only as debug; HUD will show non-fail status
        }

        lastGameOver = finalIsGameOver;
        lastGameOverReason = finalReason;
        lastNormalizedScore = result.scoreNormalized;

        int score = 0;
        int days = 0;

        if (!finalIsGameOver)
        {
            if (FlowerType != null)
            {
                score = FlowerType.GetFinalScoreFromNormalized(result.scoreNormalized);
                days = FlowerType.GetDaysFromNormalized(result.scoreNormalized);
            }
            else
            {
                // Simple fallback: 0–100 score, 0–7 days
                score = Mathf.RoundToInt(result.scoreNormalized * 100f);
                days = Mathf.RoundToInt(result.scoreNormalized * 7f);
            }

            lastScore = score;
            lastDays = days;

            Debug.Log($"[FlowerSessionController] EVALUATE OK → score={score}, days={days}, norm={result.scoreNormalized:0.###}", this);

            OnSuccessfulEvaluation?.Invoke();
        }
        else
        {
            // Game over: we still want a consistent snapshot, but score/days are 0.
            lastScore = 0;
            lastDays = 0;

            Debug.Log($"[FlowerSessionController] GAME OVER → {finalReason}", this);
            OnGameOver?.Invoke();
        }

        // Always send result + whatever score/days we ended up with.
        OnResult?.Invoke(result, lastScore, lastDays);
    }
}
