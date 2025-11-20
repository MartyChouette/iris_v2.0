using System.Text;
using TMPro;
using UnityEngine;

public class FlowerHUD : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("Session controller on the flower root.")]
    public FlowerSessionController session;

    [Tooltip("Brain used by this flower. Will auto-fill from session if left null.")]
    public FlowerGameBrain brain;

    [Tooltip("Flower type definition (for ideal values). Will auto-fill from session if left null.")]
    public FlowerTypeDefinition flowerType;

    [Header("UI Text Elements (TMP)")]
    public TMP_Text statusLabel;   // e.g. "PERFECT", "OK", "GAME OVER"
    public TMP_Text scoreLabel;    // e.g. "Score: 83"
    public TMP_Text daysLabel;     // e.g. "Days: 5"
    public TMP_Text reasonLabel;   // e.g. "Stem length off by 0.63 (hard fail)."
    public TMP_Text liveStatsLabel; // debug info while playing

    [Header("Status Colors")]
    public Color neutralColor = Color.white;
    public Color successColor = new Color(0.25f, 0.9f, 0.4f, 1f);
    public Color failColor = new Color(1f, 0.3f, 0.25f, 1f);

    [Header("Live Stats")]
    [Tooltip("Show live debug info while trimming (stem length/angle, leaf counts).")]
    public bool showLiveStats = true;

    [Tooltip("How often to update live stats, in seconds.")]
    public float liveStatsUpdateInterval = 0.2f;

    private float _liveStatsTimer;

    void Awake()
    {
        if (session != null)
        {
            if (brain == null)
                brain = session.brain;
            if (flowerType == null)
                flowerType = session.flowerType;
        }
    }

    void OnEnable()
    {
        if (session != null)
        {
            session.onResult.AddListener(OnResult);
        }
    }

    void OnDisable()
    {
        if (session != null)
        {
            session.onResult.RemoveListener(OnResult);
        }
    }

    void Update()
    {
        if (!showLiveStats || liveStatsLabel == null || brain == null)
            return;

        _liveStatsTimer += Time.deltaTime;
        if (_liveStatsTimer >= liveStatsUpdateInterval)
        {
            _liveStatsTimer = 0f;
            UpdateLiveStats();
        }
    }

    // ───────────────────────── Result UI ─────────────────────────

    /// <summary>
    /// Called by FlowerSessionController when the player finishes a flower.
    /// </summary>
    public void OnResult(FlowerGameBrain.EvaluationResult eval, int finalScore, int daysAlive)
    {
        if (statusLabel != null)
        {
            if (eval.isGameOver)
            {
                statusLabel.text = "GAME OVER";
                statusLabel.color = failColor;
            }
            else
            {
                // You can get fancier if you want (e.g., "PERFECT" if eval.scoreNormalized > 0.95)
                if (eval.scoreNormalized >= 0.95f)
                {
                    statusLabel.text = "PERFECT";
                }
                else if (eval.scoreNormalized >= 0.7f)
                {
                    statusLabel.text = "GOOD";
                }
                else
                {
                    statusLabel.text = "OK";
                }
                statusLabel.color = successColor;
            }
        }

        if (scoreLabel != null)
        {
            if (eval.isGameOver)
                scoreLabel.text = "Score: 0";
            else
                scoreLabel.text = $"Score: {finalScore}";
        }

        if (daysLabel != null)
        {
            if (eval.isGameOver)
                daysLabel.text = "Days: 0";
            else
                daysLabel.text = $"Days: {daysAlive}";
        }

        if (reasonLabel != null)
        {
            if (eval.isGameOver && !string.IsNullOrEmpty(eval.gameOverReason))
                reasonLabel.text = eval.gameOverReason;
            else if (!eval.isGameOver)
                reasonLabel.text = ""; // or something like "Nice trim."
        }
    }

    // ───────────────────────── Live Stats UI ─────────────────────────

    private void UpdateLiveStats()
    {
        if (brain == null || liveStatsLabel == null)
            return;

        var sb = new StringBuilder();

        // Stem stats if available
        if (brain.stem != null)
        {
            float currentLen = brain.stem.CurrentLength;
            float idealLen = (flowerType != null && flowerType.ideal != null)
                ? flowerType.ideal.idealStemLength
                : 0f;

            float lenDelta = idealLen > 0f ? currentLen - idealLen : 0f;

            float currentAngle = brain.stem.GetCurrentCutAngleDeg(Vector3.up);
            float idealAngle = (flowerType != null && flowerType.ideal != null)
                ? flowerType.ideal.idealCutAngleDeg
                : 0f;
            float angleDelta = currentAngle - idealAngle;

            sb.AppendLine($"Stem length: {currentLen:F3} (ideal {idealLen:F3}, Δ {lenDelta:+0.000;-0.000;0.000})");
            sb.AppendLine($"Cut angle:   {currentAngle:F1}° (ideal {idealAngle:F1}°, Δ {angleDelta:+0.0;-0.0;0.0})");
        }

        // Leaf / petal stats
        int totalParts = 0;
        int attachedParts = 0;
        int witheredParts = 0;
        int perfectParts = 0;

        if (brain.parts != null)
        {
            foreach (var p in brain.parts)
            {
                if (p == null) continue;
                totalParts++;

                if (p.isAttached) attachedParts++;
                if (p.condition == FlowerPartCondition.Withered) witheredParts++;
                if (p.condition == FlowerPartCondition.Perfect) perfectParts++;
            }
        }

        sb.AppendLine($"Parts attached: {attachedParts}/{totalParts}");
        sb.AppendLine($"Perfect parts:  {perfectParts}");
        sb.AppendLine($"Withered parts: {witheredParts}");

        // If you want, show the last evaluation snapshot:
        sb.AppendLine($"Last score: {brain.lastScoreNormalized * 100f:0.#}%");
        if (brain.lastWasGameOver && !string.IsNullOrEmpty(brain.lastGameOverReason))
        {
            sb.AppendLine($"Last fail: {brain.lastGameOverReason}");
        }

        liveStatsLabel.text = sb.ToString();
    }
}
