using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DynamicMeshCutter;

public class FlowerHUD : MonoBehaviour
{
    // -------------------------------------------------------------
    // CORE REFERENCES
    // -------------------------------------------------------------
    [Header("Core References")]
    public FlowerSessionController session;
    public FlowerGameBrain brain;
    public FlowerTypeDefinition flowerType;

    // -------------------------------------------------------------
    // UI REFERENCES
    // -------------------------------------------------------------
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

    // -------------------------------------------------------------
    // IDEAL FLOWER IMAGE
    // -------------------------------------------------------------
    [Header("Ideal Flower HUD")]
    public Image idealFlowerImage;
    public bool hideIdealImageWhenMissing = true;

    // -------------------------------------------------------------
    // LIVE STATS
    // -------------------------------------------------------------
    [Header("Live Stats")]
    public bool showLiveStats = true;
    public float liveStatsUpdateInterval = 0.1f;
    private float _liveStatsTimer;

    // -------------------------------------------------------------
    // ANGLE HUD
    // -------------------------------------------------------------
    [Header("Angle HUD")]
    public bool showAngleHUD = true;
    public AngleStagePlaneBehaviour anglePlane;
    public FlowerStemRuntime angleStemOverride;
    public float targetAngleDeg = 45f;
    public bool onlyShowAngleWhenArmed = true;
    public float angleOnTargetTolerance = 2f;
    public float angleOffsetDeg = 0f;

    public TMP_Text angleLabel;
    public RectTransform angleDialUI;
    public Transform angleWorldPlaneVisual;

    [Header("Angle Quality Text")]
    public TMP_Text angleQualityLabel;
    public string perfectAngleText = "Perfect";
    public string closeAngleText = "Close";
    public string offAngleText = "Way off";
    public float closeAngleThreshold = 5f;

    [Header("Angle HUD Colors")]
    public Color angleNormalColor = Color.white;
    public Color angleOnTargetColor = new Color(0.3f, 1.0f, 0.3f, 1f);

    // -------------------------------------------------------------
    // RESULT SPLASH
    // -------------------------------------------------------------
    [Header("Result Splash")]
    public bool showResultSplash = true;
    public TMP_Text resultSplashLabel;
    public Color splashBaseColor = Color.white;
    public Color splashStatColor = new Color(1f, 0.85f, 0.3f, 1f);

    [TextArea(4, 12)]
    public string splashTemplate =
        "you kept this flower alive for <stat>{DAYS}</stat> days.\n\n" +
        "final score: <stat>{SCORE}%</stat>\n\n" +
        "stem left: <stat>{STEM_LEFT}</stat>\n" +
        "stem delta from ideal: <stat>{STEM_DELTA}</stat>\n\n" +
        "cut angle achieved: <stat>{ANGLE_ACTUAL}°</stat>\n" +
        "angle delta from ideal: <stat>{ANGLE_DELTA}°</stat>\n\n" +
        "perfect parts: <stat>{PERFECT}</stat> out of <stat>{TOTAL}</stat>\n" +
        "withered parts: <stat>{WITHERED}</stat>\n" +
        "attached parts: <stat>{ATTACHED}</stat> / <stat>{TOTAL}</stat>";

    [Header("Debug")]
    public bool debugLogs = false;

    // =============================================================
    // INITIALIZATION
    // =============================================================
    void Awake()
    {
        if (session != null)
        {
            if (brain == null) brain = session.brain;
            if (flowerType == null) flowerType = session.FlowerType;
        }

        if (brain != null)
        {
            angleOffsetDeg = brain.angleOffsetDeg;
            if (brain.ideal != null)
                targetAngleDeg = brain.ideal.idealCutAngleDeg;
        }

        if (liveStatsUpdateInterval <= 0f)
            liveStatsUpdateInterval = 0.05f;

        ApplyIdealFlowerSprite();

        if (resultSplashLabel != null && !showResultSplash)
            resultSplashLabel.gameObject.SetActive(false);
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

    // =============================================================
    // UPDATE LOOP
    // =============================================================
    void Update()
    {
        if (showLiveStats && liveStatsLabel != null && brain != null)
        {
            _liveStatsTimer += Time.deltaTime;
            if (_liveStatsTimer >= liveStatsUpdateInterval)
            {
                _liveStatsTimer = 0f;
                UpdateLiveStats();
            }
        }

        if (showAngleHUD)
            UpdateAngleHUD();
        else
            SetAngleHUDVisible(false);
    }

    // =============================================================
    // IDEAL FLOWER IMAGE
    // =============================================================
    public void ApplyIdealFlowerSprite()
    {
        if (idealFlowerImage == null) return;

        Sprite sprite = flowerType?.idealFlowerSprite;
        if (sprite != null)
        {
            idealFlowerImage.sprite = sprite;
            idealFlowerImage.enabled = true;
        }
        else if (hideIdealImageWhenMissing)
        {
            idealFlowerImage.enabled = false;
        }
    }

    // =============================================================
    // RESULT EVENT HANDLER
    // =============================================================
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

        if (showResultSplash)
            BuildResultSplash(eval, finalScore, daysAlive);
    }

    // =============================================================
    // LIVE HUD LOGIC
    // =============================================================
    private void UpdateLiveStats()
    {
        if (brain == null || liveStatsLabel == null) return;

        var sb = new StringBuilder();

        if (brain.stem != null && brain.ideal != null)
        {
            // EQUATIONS SWITCHED: 
            // cutOff now gets the CurrentLength
            // stemLen now gets the Delta calculation
            float cutOff = brain.stem.CurrentLength;
            float idealLen = brain.ideal.idealStemLength;
            float stemLen = Mathf.Max(idealLen - cutOff, 0f);

            sb.AppendLine($"Stem left: {stemLen:0.###}  (cut off {cutOff:0.###}, ideal {idealLen:0.###})");

            float rawAngle = brain.stem.GetCurrentCutAngleDeg(Vector3.up);
            float calibrated = Mathf.DeltaAngle(rawAngle, brain.angleOffsetDeg);
            float cutAngle = Mathf.Clamp(Mathf.Abs(calibrated), 0f, 180f);
            float idealAngle = Mathf.Abs(brain.ideal.idealCutAngleDeg);
            float deltaAngle = cutAngle - idealAngle;

            sb.AppendLine($"Cut angle: {cutAngle:0.#}° (ideal {idealAngle:0.#}°, Δ {deltaAngle:0.#}°)");
        }

        int totalParts = 0, attached = 0, perfect = 0, withered = 0;
        foreach (var p in brain.parts)
        {
            if (p == null) continue;
            totalParts++;
            if (p.isAttached) attached++;
            if (p.condition == FlowerPartCondition.Perfect) perfect++;
            if (p.condition == FlowerPartCondition.Withered) withered++;
        }

        sb.AppendLine($"Parts attached: {attached}/{totalParts}");
        sb.AppendLine($"Perfect parts: {perfect}");
        sb.AppendLine($"Withered parts: {withered}");
        sb.AppendLine($"Last score: {brain.lastScoreNormalized * 100f:0.#}%");

        if (brain.lastWasGameOver && !string.IsNullOrEmpty(brain.lastGameOverReason))
            sb.AppendLine($"Fail: {brain.lastGameOverReason}");

        liveStatsLabel.text = sb.ToString();
    }

    // =============================================================
    // ANGLE HUD LOGIC
    // =============================================================
    private void UpdateAngleHUD()
    {
        if (angleLabel == null && angleDialUI == null && angleWorldPlaneVisual == null && angleQualityLabel == null)
            return;

        if (onlyShowAngleWhenArmed && anglePlane != null)
        {
            if (!anglePlane.enabled || !anglePlane.IsAngleStageArmed())
            {
                SetAngleHUDVisible(false);
                return;
            }
        }

        SetAngleHUDVisible(true);

        FlowerStemRuntime stem = angleStemOverride ?? brain?.stem;
        if (stem == null)
            return;

        float raw = stem.GetCurrentCutAngleDeg(Vector3.up);
        float calibrated = Mathf.DeltaAngle(raw, angleOffsetDeg);
        float angleNow = Mathf.Clamp(Mathf.Abs(calibrated), 0f, 180f);
        float ideal = Mathf.Abs(targetAngleDeg);

        float delta = Mathf.Abs(angleNow - ideal);
        bool onTarget = delta <= angleOnTargetTolerance;

        if (angleLabel != null)
        {
            angleLabel.text = $"Angle: {angleNow:0.#}° / Target: {ideal:0.#}° (Δ {delta:0.#}°)";
            angleLabel.color = onTarget ? angleOnTargetColor : angleNormalColor;
        }

        if (angleQualityLabel != null)
        {
            string q = onTarget ? perfectAngleText :
                      delta <= closeAngleThreshold ? closeAngleText :
                      offAngleText;

            angleQualityLabel.text = q;
            angleQualityLabel.color = onTarget ? angleOnTargetColor : angleNormalColor;
        }

        if (angleDialUI != null)
            angleDialUI.localRotation = Quaternion.Euler(0f, 0f, -angleNow);

        if (angleWorldPlaneVisual != null && anglePlane != null)
            angleWorldPlaneVisual.rotation = Quaternion.LookRotation(anglePlane.transform.forward, Vector3.up);
    }

    private void SetAngleHUDVisible(bool visible)
    {
        if (angleLabel != null) angleLabel.gameObject.SetActive(visible);
        if (angleDialUI != null) angleDialUI.gameObject.SetActive(visible);
        if (angleQualityLabel != null) angleQualityLabel.gameObject.SetActive(visible);
    }

    // =============================================================
    // SPLASH BUILDER (FINAL CLEAN VERSION)
    // =============================================================
    private void BuildResultSplash(FlowerGameBrain.EvaluationResult eval, int finalScore, int daysAlive)
    {
        if (resultSplashLabel == null) return;

        if (!resultSplashLabel.gameObject.activeSelf)
            resultSplashLabel.gameObject.SetActive(true);

        float stemLeft = 0f;
        float stemDelta = 0f;
        float angleActual = 0f;
        float angleDelta = 0f;

        int total = 0, attached = 0, perfect = 0, withered = 0;

        if (brain != null)
        {
            // ----- STEM -----
            if (brain.stem != null && brain.ideal != null)
            {
                float idealLen = brain.ideal.idealStemLength;

                // EQUATIONS SWITCHED:
                // stemDelta now holds the raw length (used to be stemLeft)
                stemDelta = brain.stem.CurrentLength;

                // stemLeft now holds the absolute difference (used to be stemDelta)
                stemLeft = Mathf.Abs(stemDelta - idealLen);

                // ----- ANGLE -----
                float raw = brain.stem.GetCurrentCutAngleDeg(Vector3.up);
                float calibrated = Mathf.DeltaAngle(raw, brain.angleOffsetDeg);
                angleActual = Mathf.Clamp(Mathf.Abs(calibrated), 0f, 180f);
                float idealAngle = Mathf.Abs(brain.ideal.idealCutAngleDeg);

                angleDelta = Mathf.Abs(angleActual - idealAngle); // absolute
            }

            // ----- PARTS -----
            foreach (var part in brain.parts)
            {
                if (part == null) continue;
                total++;
                if (part.isAttached) attached++;
                if (part.condition == FlowerPartCondition.Perfect) perfect++;
                if (part.condition == FlowerPartCondition.Withered) withered++;
            }
        }

        // ----- BUILD SPLASH -----
        string splash = splashTemplate;

        splash = splash.Replace("{DAYS}", daysAlive.ToString());
        splash = splash.Replace("{SCORE}", finalScore.ToString());

        splash = splash.Replace("{STEM_LEFT}", stemLeft.ToString("0.###"));
        splash = splash.Replace("{STEM_DELTA}", stemDelta.ToString("0.###"));

        splash = splash.Replace("{ANGLE_ACTUAL}", angleActual.ToString("0.#"));
        splash = splash.Replace("{ANGLE_DELTA}", angleDelta.ToString("0.#"));

        splash = splash.Replace("{PERFECT}", perfect.ToString());
        splash = splash.Replace("{WITHERED}", withered.ToString());
        splash = splash.Replace("{ATTACHED}", attached.ToString());
        splash = splash.Replace("{TOTAL}", total.ToString());

        // Add color tags
        string statHex = ColorUtility.ToHtmlStringRGB(splashStatColor);
        splash = splash.Replace("<stat>", $"<color=#{statHex}>");
        splash = splash.Replace("</stat>", "</color>");

        resultSplashLabel.text = splash;
    }
}