/**
 * @file FlowerHUD_DebugTelemetry.cs
 * @brief Developer telemetry HUD (debug-only) for Iris flower sessions.
 *
 * @details
 * This is intentionally NOT “player UI”.
 * It exists to answer: “what does the system think is happening right now?”
 *
 * Outputs:
 * - stem length vs ideal, delta
 * - current cut angle vs ideal, delta
 * - part counts (attached/perfect/withered)
 * - last score %
 *
 * Runtime behavior:
 * - Updates on a timer (allocation-conscious).
 * - Can be toggled on/off (default key: F3).
 *
 * @ingroup flowers_ui
 */

using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class FlowerHUD_DebugTelemetry : MonoBehaviour
{
    [Header("Core")]
    public FlowerSessionController session;
    public FlowerGameBrain brain;

    [Header("UI")]
    public CanvasGroup root;
    public TMP_Text telemetryLabel;

    [Header("Behavior")]
    public bool startVisible = false;
    public bool allowToggle = true;
    public KeyCode toggleKey = KeyCode.F3;

    [Tooltip("Seconds between updates.")]
    public float updateInterval = 0.10f;

    [Header("Debug")]
    public bool debugLogs = false;

    float _t;
    readonly StringBuilder _sb = new StringBuilder(512);

    void Awake()
    {
        if (root == null) root = GetComponentInChildren<CanvasGroup>(true);

        AutoWire();

        SetVisible(startVisible);
    }

    void OnEnable()
    {
        AutoWire();
    }

    void Update()
    {
        if (allowToggle && Input.GetKeyDown(toggleKey))
            SetVisible(!IsVisible());

        if (!IsVisible())
            return;

        if (telemetryLabel == null || brain == null)
            return;

        _t += Time.deltaTime;
        if (_t < updateInterval) return;
        _t = 0f;

        BuildTelemetry();
    }

    void AutoWire()
    {
        if (session == null) session = FindFirstObjectByType<FlowerSessionController>();
        if (brain == null && session != null) brain = session.brain;
        if (brain == null) brain = FindFirstObjectByType<FlowerGameBrain>();

        if (session == null)
            Debug.LogError("[FlowerHUD_DebugTelemetry] No FlowerSessionController found. " +
                "Wire the 'session' field in the Inspector.", this);
        if (brain == null)
            Debug.LogError("[FlowerHUD_DebugTelemetry] No FlowerGameBrain found. " +
                "Wire the 'brain' field in the Inspector or ensure session.brain is set.", this);

        if (debugLogs)
            Debug.Log($"[FlowerHUD_DebugTelemetry] Wired session={(session ? session.name : "null")} brain={(brain ? brain.name : "null")}", this);
    }

    void BuildTelemetry()
    {
        _sb.Clear();

        if (brain.stem != null && brain.ideal != null)
        {
            float stemLeft = brain.stem.CurrentLength;
            float idealLen = brain.ideal.idealStemLength;
            float stemDelta = stemLeft - idealLen;

            _sb.AppendLine($"Stem left: {stemLeft:0.###} (ideal {idealLen:0.###}, Δ {stemDelta:0.###})");

            float raw = brain.stem.GetCurrentCutAngleDeg(Vector3.up);
            float calibrated = Mathf.DeltaAngle(raw, brain.angleOffsetDeg);
            float angleNow = Mathf.Clamp(Mathf.Abs(calibrated), 0f, 180f);

            float idealAngle = Mathf.Abs(brain.ideal.idealCutAngleDeg);
            float angleDelta = angleNow - idealAngle;

            _sb.AppendLine($"Cut angle: {angleNow:0.#}° (ideal {idealAngle:0.#}°, Δ {angleDelta:0.#}°)");
        }

        int total = 0, attached = 0, perfect = 0, withered = 0;
        foreach (var p in brain.parts)
        {
            if (p == null) continue;
            total++;
            if (p.isAttached) attached++;
            if (p.condition == FlowerPartCondition.Perfect) perfect++;
            if (p.condition == FlowerPartCondition.Withered) withered++;
        }

        _sb.AppendLine($"Parts attached: {attached}/{total}");
        _sb.AppendLine($"Perfect parts: {perfect}");
        _sb.AppendLine($"Withered parts: {withered}");
        _sb.AppendLine($"Last score: {brain.lastScoreNormalized * 100f:0.#}%");

        telemetryLabel.text = _sb.ToString();
    }

    public void SetVisible(bool visible)
    {
        if (root != null)
        {
            root.alpha = visible ? 1f : 0f;
            root.interactable = visible;
            root.blocksRaycasts = visible;
        }
        else if (telemetryLabel != null)
        {
            telemetryLabel.gameObject.SetActive(visible);
        }
    }

    public bool IsVisible()
    {
        if (root != null) return root.alpha > 0.5f;
        if (telemetryLabel != null) return telemetryLabel.gameObject.activeSelf;
        return false;
    }
}
