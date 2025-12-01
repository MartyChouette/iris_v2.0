using UnityEngine;

[CreateAssetMenu(menuName = "Flower/Flower Type")]
public class FlowerTypeDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable ID used in save files, analytics, etc. Can be short like 'rose_01'.")]
    public string flowerId;

    [Tooltip("Display name shown in UI.")]
    public string displayName = "New Flower";

    [TextArea]
    [Tooltip("Optional description shown in UI / debug.")]
    public string description;

    [Header("Visuals")]
    [Tooltip("Reference image shown in the HUD as the goal / ideal flower.")]
    public Sprite idealFlowerSprite;

    [Header("Game Over Policy")]
    [Tooltip("If false, even hard failures are treated as very bad scores instead of true game-overs.")]
    public bool allowGameOver = true;

    [Header("Score Mapping (0–1 → final score)")]
    [Tooltip("Minimum possible final score for this flower type.")]
    public int minScore = 0;

    [Tooltip("Maximum possible final score for this flower type.")]
    public int maxScore = 100;

    [Tooltip("If true, use a custom curve to map normalized score (0–1) to final score.")]
    public bool useCustomScoreCurve = false;

    [Tooltip("X axis = normalized score (0–1). Y axis = interpolation factor between minScore and maxScore.")]
    public AnimationCurve scoreCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Days Mapping (0–1 → days alive)")]
    [Tooltip("Minimum possible days alive.")]
    public int minDays = 0;

    [Tooltip("Maximum possible days alive.")]
    public int maxDays = 7;

    [Tooltip("If true, use scoreToDaysCurve instead of simple linear mapping.")]
    public bool useCustomDaysCurve = false;

    [Tooltip("X axis = normalized score (0–1). Y axis = interpolation factor between minDays and maxDays.")]
    public AnimationCurve scoreToDaysCurve = AnimationCurve.Linear(0, 0, 1, 1);

    /// <summary>
    /// Maps a normalized [0,1] score from FlowerGameBrain into a final integer score.
    /// </summary>
    public int GetFinalScoreFromNormalized(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);

        float t;
        if (useCustomScoreCurve && scoreCurve != null)
        {
            t = Mathf.Clamp01(scoreCurve.Evaluate(normalized));
        }
        else
        {
            // Simple linear mapping: 0 -> minScore, 1 -> maxScore
            t = normalized;
        }

        float scoreFloat = Mathf.Lerp(minScore, maxScore, t);
        return Mathf.RoundToInt(scoreFloat);
    }

    /// <summary>
    /// Maps a normalized [0,1] score into a number of days (for lore / outcome flavor).
    /// </summary>
    public int GetDaysFromNormalized(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);

        float t;
        if (useCustomDaysCurve && scoreToDaysCurve != null)
        {
            t = Mathf.Clamp01(scoreToDaysCurve.Evaluate(normalized));
        }
        else
        {
            // Simple linear mapping: 0 -> minDays, 1 -> maxDays
            t = normalized;
        }

        float daysFloat = Mathf.Lerp(minDays, maxDays, t);
        return Mathf.Max(0, Mathf.RoundToInt(daysFloat));
    }
}
