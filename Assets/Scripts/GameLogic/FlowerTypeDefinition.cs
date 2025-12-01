using UnityEngine;

[CreateAssetMenu(menuName = "Flower/Flower Type")]
public class FlowerTypeDefinition : ScriptableObject
{
    // ──────────────────────────────────────────────────────────────
    // Identity
    // ──────────────────────────────────────────────────────────────
    [Header("Identity")]
    [Tooltip("Stable ID used in save files, analytics, etc.")]
    public string flowerId;

    [Tooltip("Display name shown in UI.")]
    public string displayName = "New Flower";

    [TextArea]
    [Tooltip("Optional description shown in UI / debug.")]
    public string description;

    // ──────────────────────────────────────────────────────────────
    // Visuals
    // ──────────────────────────────────────────────────────────────
    [Header("Visuals")]
    [Tooltip("Image of the ideal flower shown in HUD + item menus.")]
    public Sprite idealFlowerSprite;

    [Tooltip("Icon used in menus (optional).")]
    public Sprite icon;

    [Tooltip("Prefab that represents the base flower setup for this type.")]
    public GameObject flowerPrefab;

    // ──────────────────────────────────────────────────────────────
    // Difficulty
    // ──────────────────────────────────────────────────────────────
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

    // ──────────────────────────────────────────────────────────────
    // Ideal configuration
    // ──────────────────────────────────────────────────────────────
    [Header("Ideal Setup")]
    [Tooltip("Ideal configuration describing what 'perfect' looks like.")]
    public IdealFlowerDefinition ideal;

    [Tooltip("If true, ANY perfect part damaged/removed is treated as game over.")]
    public bool globalPerfectDamageCausesGameOver = false;

    [Tooltip("If true, ANY fatal violation found by FlowerGameBrain causes a hard fail.")]
    public bool allowGameOver = true;

    // ──────────────────────────────────────────────────────────────
    // Days Mapping
    // ──────────────────────────────────────────────────────────────
    [Header("Life / Days Mapping")]
    [Tooltip("Minimum days this flower can live (score = 0).")]
    public int minDays = 1;

    [Tooltip("Maximum days this flower can live (score = 1).")]
    public int maxDays = 10;

    [Tooltip("If true, use scoreToDaysCurve instead of simple min/max linear mapping.")]
    public bool useCustomCurve = false;

    [Tooltip("X = normalized score (0..1), Y = normalized life (0..1).")]
    public AnimationCurve scoreToDaysCurve = AnimationCurve.Linear(0, 0, 1, 1);

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────
    public int GetFinalScoreFromNormalized(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);
        float raw = normalized * basePerfectScore * scoreMultiplier;
        return Mathf.RoundToInt(raw);
    }

    public int GetDaysFromNormalized(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);

        float t = useCustomCurve && scoreToDaysCurve != null
            ? Mathf.Clamp01(scoreToDaysCurve.Evaluate(normalized))
            : normalized;

        float daysFloat = Mathf.Lerp(minDays, maxDays, t);
        return Mathf.Max(0, Mathf.RoundToInt(daysFloat));
    }
}
