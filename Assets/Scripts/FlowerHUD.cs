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
                flowerType = session.FlowerType;
        }
    }

    void OnEnable()
    {
        if (session != null)
        {
            // NOTE: event name is OnResult (capital O)
            session.OnResult.AddListener(OnResult);
        }
    }

    void OnDisable()
    {
        if (session != null)
        {
            // NOTE: event name is OnResult (capital O)
            session.OnResult.RemoveListener(OnResult);
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
    /// Signature must match UnityEvent<EvaluationResult,int,int>.
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
                statusLabel.text = "OK";
                statusLabel.color = successColor;
            }
        }

        if (scoreLabel != null)
        {
            scoreLabel.text = $"Score: {finalScore}";
        }

        if (daysLabel != null)
        {
            daysLabel.text = $"Days: {daysAlive}";
        }

        if (reasonLabel != null)
        {
            if (eval.isGameOver && !string.IsNullOrEmpty(eval.gameOverReason))
            {
                reasonLabel.text = eval.gameOverReason;
                reasonLabel.gameObject.SetActive(true);
            }
            else
            {
                reasonLabel.text = "";
                reasonLabel.gameObject.SetActive(false);
            }
        }
    }

    // ───────────────────────── Live Debug ─────────────────────────

    private void UpdateLiveStats()
    {
        if (brain == null || liveStatsLabel == null)
            return;

        var sb = new StringBuilder();

        // Stem
        if (brain.stem != null && brain.ideal != null)
        {
            float stemLen = brain.stem.CurrentLength;
            float idealLen = brain.ideal.idealStemLength;
            float deltaLen = stemLen - idealLen;

            sb.AppendLine($"Stem length: {stemLen:0.###}");
            sb.AppendLine($"(ideal {idealLen:0.###}, Δ {deltaLen:+0.###;-0.###;0.000})");

            float cutAngle = brain.stem.GetCurrentCutAngleDeg(Vector3.up);
            float idealAngle = brain.ideal.idealCutAngleDeg;
            float deltaAngle = cutAngle - idealAngle;

            sb.AppendLine($"Cut angle:   {cutAngle:0.#}° (ideal {idealAngle:0.#}°, Δ {deltaAngle:+0.#;-0.#;0})");
        }

        // Parts
        int totalParts = 0;
        int attachedParts = 0;
        int perfectParts = 0;
        int witheredParts = 0;

        foreach (var part in brain.parts)
        {
            if (part == null) continue;
            totalParts++;

            if (part.isAttached) attachedParts++;
            if (part.condition == FlowerPartCondition.Perfect) perfectParts++;
            if (part.condition == FlowerPartCondition.Withered) witheredParts++;
        }

        sb.AppendLine($"Parts attached: {attachedParts}/{totalParts}");
        sb.AppendLine($"Perfect parts:  {perfectParts}");
        sb.AppendLine($"Withered parts: {witheredParts}");

        // Last evaluation snapshot:
        sb.AppendLine($"Last score: {brain.lastScoreNormalized * 100f:0.#}%");
        if (brain.lastWasGameOver && !string.IsNullOrEmpty(brain.lastGameOverReason))
        {
            sb.AppendLine($"Last fail: {brain.lastGameOverReason}");
        }

        liveStatsLabel.text = sb.ToString();
    }
}
