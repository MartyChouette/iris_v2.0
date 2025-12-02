using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DynamicMeshCutter;   // for AngleStagePlaneBehaviour / FlowerStemRuntime

public class FlowerHUD : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("Session controller on the flower root.")]
    public FlowerSessionController session;

    [Tooltip("Brain used by this flower. Will auto-fill from session if left null.")]
    public FlowerGameBrain brain;

    [Tooltip("Flower type definition (for ideal values). Will auto-fill from session if left null.")]
    public FlowerTypeDefinition flowerType;

    [Header("Result UI Text Elements (TMP)")]
    public TMP_Text statusLabel;
    public TMP_Text scoreLabel;
    public TMP_Text daysLabel;
    public TMP_Text reasonLabel;
    public TMP_Text liveStatsLabel;

    [Header("Status Colors")]
    public Color neutralColor = Color.white;
    public Color successColor = new Color(0.25f, 0.9f, 0.4f, 1f);
    public Color failColor = new Color(1f, 0.3f, 0.25f, 1f);

    [Header("Ideal Flower HUD")]
    [Tooltip("UI Image that will display the ideal (goal) flower sprite in the corner of the screen.")]
    public Image idealFlowerImage;

    [Tooltip("If true, hide the image when no ideal sprite is defined on the flower type.")]
    public bool hideIdealImageWhenMissing = true;

    [Header("Live Stats")]
    [Tooltip("Show live debug info while trimming (stem length/angle, leaf counts).")]
    public bool showLiveStats = true;

    [Tooltip("How often to update live stats, in seconds.")]
    public float liveStatsUpdateInterval = 0.1f;

    private float _liveStatsTimer;

    // ───────────────────────── Angle HUD ─────────────────────────

    [Header("Angle HUD")]
    [Tooltip("Enable the cut-angle HUD section.")]
    public bool showAngleHUD = true;

    [Tooltip("Two-stage angle plane driving the cut. " +
             "If assigned, we can optionally show angle only while its angle stage is armed.")]
    public AngleStagePlaneBehaviour anglePlane;

    [Tooltip("Optional explicit stem for angle HUD. If null, uses brain.stem.")]
    public FlowerStemRuntime angleStemOverride;

    [Tooltip("Target angle in degrees that the player is aiming for.")]
    public float targetAngleDeg = 45f;

    [Tooltip("If true, angle HUD only shows while the anglePlane is armed (IsAngleStageArmed == true). " +
             "If anglePlane is null or lacks this property, HUD will always show when enabled.")]
    public bool onlyShowAngleWhenArmed = true;

    [Tooltip("How close (in degrees) counts as 'on target' for angle coloring.")]
    public float angleOnTargetTolerance = 2f;

    [Tooltip("Calibration offset (degrees) so HUD & scoring share the same angle space.")]
    public float angleOffsetDeg = 0f;

    [Tooltip("TMP label to show current / target angle and delta.")]
    public TMP_Text angleLabel;

    [Tooltip("UI dial / plane icon RectTransform. Its Z rotation will match the current cut angle.")]
    public RectTransform angleDialUI;

    [Tooltip("Optional worldspace visual that should track the angle plane orientation.")]
    public Transform angleWorldPlaneVisual;

    [Header("Angle HUD Colors")]
    public Color angleNormalColor = Color.white;
    public Color angleOnTargetColor = new Color(0.3f, 1.0f, 0.3f, 1f);

    [Header("Debug")]
    public bool debugLogs = false;

    void Awake()
    {
        if (session != null)
        {
            if (brain == null)
                brain = session.brain;
            if (flowerType == null)
                flowerType = session.FlowerType;
        }

        // Sync angle calibration and target from the brain/ideal if available.
        if (brain != null)
        {
            angleOffsetDeg = brain.angleOffsetDeg;
            if (brain.ideal != null)
                targetAngleDeg = brain.ideal.idealCutAngleDeg;
        }

        // Safety: avoid zero or negative intervals
        if (liveStatsUpdateInterval <= 0f)
            liveStatsUpdateInterval = 0.05f;

        // Set up the ideal flower HUD image from the flower type, if present.
        ApplyIdealFlowerSprite();
    }

    void OnEnable()
    {
        if (session != null)
            session.OnResult.AddListener(OnResult);
    }

    void OnDisable()
    {
        if (session != null)
            session.OnResult.RemoveListener(OnResult);
    }

    void Update()
    {
        // ───────── Live stats ─────────
        if (showLiveStats && liveStatsLabel != null && brain != null)
        {
            _liveStatsTimer += Time.deltaTime;
            if (_liveStatsTimer >= liveStatsUpdateInterval)
            {
                _liveStatsTimer = 0f;
                UpdateLiveStats();
            }
        }

        // ───────── Angle HUD ─────────
        if (showAngleHUD)
        {
            UpdateAngleHUD();
        }
        else
        {
            SetAngleHUDVisible(false);
        }
    }

    // ───────────────────────── Ideal Flower HUD ─────────────────────────

    public void ApplyIdealFlowerSprite()
    {
        if (idealFlowerImage == null)
            return;

        Sprite sprite = null;
        if (flowerType != null)
            sprite = flowerType.idealFlowerSprite;

        if (sprite != null)
        {
            idealFlowerImage.sprite = sprite;
            idealFlowerImage.enabled = true;
        }
        else
        {
            if (hideIdealImageWhenMissing)
                idealFlowerImage.enabled = false;
        }
    }

    // ───────────────────────── Result UI ─────────────────────────

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
            scoreLabel.text = $"Score: {finalScore}";

        if (daysLabel != null)
            daysLabel.text = $"Days: {daysAlive}";

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

        // ───────── STEM READOUT (LIVE ON SCREEN) ─────────
        if (brain.stem != null && brain.ideal != null)
        {
            float stemLen = brain.stem.CurrentLength;
            float idealLen = brain.ideal.idealStemLength;
            float deltaLen = stemLen - idealLen;

            sb.AppendLine($"Stem length: {stemLen:0.###} (ideal {idealLen:0.###}, Δ {deltaLen:+0.###;-0.###;0})");

            // Use the same calibrated angle that scoring uses.
            float rawAngle = brain.stem.GetCurrentCutAngleDeg(Vector3.up);

            // Calibrated cut angle, mapped into 0..180 for display.
            float calibrated = Mathf.DeltaAngle(rawAngle, brain.angleOffsetDeg);
            float cutAngle = Mathf.Clamp(Mathf.Abs(calibrated), 0f, 180f);

            // Ideal angle, also treated as a 0..180 target.
            float idealAngle = Mathf.Clamp(Mathf.Abs(brain.ideal.idealCutAngleDeg), 0f, 180f);

            // Non-negative difference between cut and ideal.
            float deltaAngle = Mathf.Abs(cutAngle - idealAngle);

            sb.AppendLine($"Cut angle:   {cutAngle:0.#}° (ideal {idealAngle:0.#}°, Δ {deltaAngle:0.#}°)");
        }

        // ───────── PARTS READOUT ─────────
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

        // ───────── LATEST SCORE SNAPSHOT ─────────
        sb.AppendLine($"Last score: {brain.lastScoreNormalized * 100f:0.#}%");

        if (brain.lastWasGameOver && !string.IsNullOrEmpty(brain.lastGameOverReason))
            sb.AppendLine($"Fail: {brain.lastGameOverReason}");

        liveStatsLabel.text = sb.ToString();
    }

    // ───────────────────────── Angle HUD ─────────────────────────

    private void UpdateAngleHUD()
    {
        // If we have no label, no dial, and no world visual, nothing to show.
        if (angleLabel == null && angleDialUI == null && angleWorldPlaneVisual == null)
            return;

        // Optional gating: only show while the two-stage plane is armed.
        if (onlyShowAngleWhenArmed && anglePlane != null)
        {
            // NOTE: this assumes AngleStagePlaneBehaviour exposes IsAngleStageArmed().
            if (!anglePlane.enabled || !anglePlane.IsAngleStageArmed())
            {
                SetAngleHUDVisible(false);
                return;
            }
        }

        // If we're here, HUD should be visible.
        SetAngleHUDVisible(true);

        // Determine which stem to use for angle measurement.
        FlowerStemRuntime stemForAngle = angleStemOverride;

        if (stemForAngle == null && brain != null)
            stemForAngle = brain.stem;

        if (stemForAngle == null)
        {
            if (debugLogs)
                Debug.LogWarning("[FlowerHUD] Angle HUD: no stem available for angle measurement.", this);
            return;
        }

        // Measure current cut angle (relative to world up), then apply calibration so HUD matches scoring.
        float rawAngle = stemForAngle.GetCurrentCutAngleDeg(Vector3.up);
        float calibrated = Mathf.DeltaAngle(rawAngle, angleOffsetDeg);

        // Map calibrated angle into 0..180 range for player display.
        float currentAngle = Mathf.Clamp(Mathf.Abs(calibrated), 0f, 180f);

        // Target angle is likewise treated as 0..180.
        float targetAngle = Mathf.Clamp(Mathf.Abs(targetAngleDeg), 0f, 180f);

        // Non-negative difference between current and target.
        float absDelta = Mathf.Abs(currentAngle - targetAngle);
        bool onTarget = absDelta <= angleOnTargetTolerance;

        // Text label
        if (angleLabel != null)
        {
            angleLabel.text =
                $"Angle: {currentAngle:0.#}° / Target: {targetAngle:0.#}° (Δ {absDelta:0.#}°)";

            angleLabel.color = onTarget ? angleOnTargetColor : angleNormalColor;
        }

        // UI dial: rotate around Z so it visually tracks the cut angle.
        if (angleDialUI != null)
        {
            // Convention: positive angles tilt clockwise on-screen; adjust sign to taste.
            angleDialUI.localRotation = Quaternion.Euler(0f, 0f, -currentAngle);
        }

        // Worldspace plane visual: track the angle plane's orientation (if assigned).
        if (angleWorldPlaneVisual != null && anglePlane != null)
        {
            // Align world visual's forward to the plane normal, with world up as reference.
            angleWorldPlaneVisual.rotation = Quaternion.LookRotation(
                anglePlane.transform.forward,
                Vector3.up
            );
        }
    }

    private void SetAngleHUDVisible(bool visible)
    {
        if (angleLabel != null && angleLabel.gameObject.activeSelf != visible)
            angleLabel.gameObject.SetActive(visible);

        if (angleDialUI != null && angleDialUI.gameObject.activeSelf != visible)
            angleDialUI.gameObject.SetActive(visible);
    }
}
