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

    public void ForceGameOver(string reason)
    {
        lastGameOver = true;
        lastGameOverReason = reason;

        // Also update the brain so HUD live stats can show the cause.
        if (brain != null)
        {
            brain.lastWasGameOver = true;
            brain.lastGameOverReason = reason;
            brain.lastScoreNormalized = 0f;
        }

        Debug.Log($"[FlowerSessionController] GAME OVER: {reason}", this);

        OnGameOver?.Invoke();
    }


    public void EvaluateCurrentFlower()
    {
        if (brain == null)
            return;

        var result = brain.EvaluateFlower();

        if (result.isGameOver)
        {
            lastGameOver = true;
            lastGameOverReason = result.gameOverReason;
            OnGameOver?.Invoke();
        }
        else
        {
            int score = Mathf.RoundToInt(result.scoreNormalized * 100f);
            int days = Mathf.RoundToInt(result.scoreNormalized * 7f);

            lastScore = score;
            lastDays = days;
            lastNormalizedScore = result.scoreNormalized;

            OnSuccessfulEvaluation?.Invoke();
            OnResult?.Invoke(result, score, days);
        }
    }
}