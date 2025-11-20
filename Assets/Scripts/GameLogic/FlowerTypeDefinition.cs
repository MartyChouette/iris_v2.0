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

    [Tooltip("Icon for UI (picker, menus, etc.).")]
    public Sprite icon;

    [Tooltip("Prefab that represents the base flower setup for this type.")]
    public GameObject flowerPrefab;

    public enum Difficulty
    {
        VeryEasy,
        Easy,
        Normal,
        Hard,
        VeryHard
    }

    [Header("Difficulty / Scoring")]
    public Difficulty difficulty = Difficulty.Normal;

    [Tooltip("Base score for a perfect trim of this flower type.")]
    public float basePerfectScore = 100f;

    [Tooltip("Multiplier applied to the normalized score (0-1) from FlowerGameBrain.")]
    public float scoreMultiplier = 1f;

    [Header("Game Logic")]
    [Tooltip("Ideal configuration describing what 'perfect' looks like.")]
    public IdealFlowerDefinition ideal;

    [Tooltip("If true, ANY perfect part damaged/removed is treated as game over " +
             "even if the individual part rule does not explicitly set canCauseGameOver.")]
    public bool globalPerfectDamageCausesGameOver = false;

    [Tooltip("If true, ANY fatal violation found by FlowerGameBrain will be treated as a hard fail " +
             "for this flower type (can be used to make 'gentle' flowers that rarely hard-fail).")]
    public bool allowGameOver = true;

    // ───────────────────────── Life / Days Mapping ─────────────────────────

    [Header("Life / Days Mapping")]
    [Tooltip("Minimum days this flower can live (even if the score is terrible but not a total fail).")]
    public int minDays = 1;

    [Tooltip("Maximum days this flower can live at a perfect score.")]
    public int maxDays = 10;

    [Tooltip("If true, use the scoreToDaysCurve instead of simple min/max linear mapping.")]
    public bool useCustomCurve = false;

    [Tooltip("X = normalized score (0..1), Y = normalized life (0..1).")]
    public AnimationCurve scoreToDaysCurve = AnimationCurve.Linear(0, 0, 1, 1);

    // ───────────────────────── Helpers ─────────────────────────

    // Convert 0..1 brain score into a points score.
    public int GetFinalScoreFromNormalized(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);
        float raw = normalized * basePerfectScore * scoreMultiplier;
        return Mathf.RoundToInt(raw);
    }

    // Convert 0..1 brain score into days of life (integer).
    public int GetDaysFromNormalized(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);

        float t;
        if (useCustomCurve && scoreToDaysCurve != null)
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
