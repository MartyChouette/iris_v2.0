/**
 * @file FlowerHUD_GameplayFeedback.cs
 * @brief Player-facing, minimal feedback during trimming (NOT debug telemetry).
 *
 * @details
 * Shows:
 * - an angle dial (rotate UI) so players can “read” the cut visually
 * - a single quality word: Perfect / Close / Way off
 *
 * It should NOT be a wall of numbers.
 *
 * Ownership hook:
 * - If planeController is assigned, this HUD can auto-hide when scissors are not equipped.
 *
 * @ingroup flowers_ui
 */

using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class FlowerHUD_GameplayFeedback : MonoBehaviour
{
    [Header("Core")]
    public FlowerSessionController session;
    public FlowerGameBrain brain;

    [Header("Optional Ownership Gate")]
    [Tooltip("If set, the gameplay feedback hides while the tool is not enabled.")]
    public CuttingPlaneController planeController;

    [Header("UI")]
    public CanvasGroup root;

    [Tooltip("Rotated like a dial/protractor needle.")]
    public RectTransform angleDialUI;

    [Tooltip("Optional numeric readout (keep it small, or leave null).")]
    public TMP_Text angleLabel;

    [Tooltip("Perfect / Close / Way off")]
    public TMP_Text qualityLabel;

    [Header("Tuning")]
    public float targetAngleDeg = 45f;
    public float perfectToleranceDeg = 2f;
    public float closeThresholdDeg = 7f;

    [Header("Text")]
    public string perfectText = "Perfect";
    public string closeText = "Close";
    public string offText = "Way off";

    [Header("Update")]
    public float updateInterval = 0.05f;

    float _t;

    void Awake()
    {
        if (root == null) root = GetComponentInChildren<CanvasGroup>(true);
        AutoWire();
    }

    void OnEnable() => AutoWire();

    void Update()
    {
        bool shouldShow = true;

        if (planeController != null)
            shouldShow = planeController.IsToolEnabled;

        SetVisible(shouldShow);
        if (!shouldShow) return;

        if (brain == null || brain.stem == null) return;

        _t += Time.deltaTime;
        if (_t < updateInterval) return;
        _t = 0f;

        // Ideal angle source: brain ideal if present, else inspector targetAngleDeg.
        float ideal = targetAngleDeg;
        if (brain.ideal != null)
            ideal = Mathf.Abs(brain.ideal.idealCutAngleDeg);

        float raw = brain.stem.GetCurrentCutAngleDeg(Vector3.up);
        float calibrated = Mathf.DeltaAngle(raw, brain.angleOffsetDeg);
        float angleNow = Mathf.Clamp(Mathf.Abs(calibrated), 0f, 180f);

        float delta = Mathf.Abs(angleNow - ideal);

        // Dial: 0..180 mapped to rotation. (You can swap sign to match your art.)
        if (angleDialUI != null)
            angleDialUI.localRotation = Quaternion.Euler(0f, 0f, -angleNow);

        if (angleLabel != null)
            angleLabel.text = $"{angleNow:0.#}°";

        if (qualityLabel != null)
        {
            if (delta <= perfectToleranceDeg) qualityLabel.text = perfectText;
            else if (delta <= closeThresholdDeg) qualityLabel.text = closeText;
            else qualityLabel.text = offText;
        }
    }

    void AutoWire()
    {
        if (session == null) session = FindFirstObjectByType<FlowerSessionController>();
        if (brain == null && session != null) brain = session.brain;
        if (brain == null) brain = FindFirstObjectByType<FlowerGameBrain>();

        if (session == null)
            Debug.LogError("[FlowerHUD_GameplayFeedback] No FlowerSessionController found. " +
                "Wire the 'session' field in the Inspector.", this);
        if (brain == null)
            Debug.LogError("[FlowerHUD_GameplayFeedback] No FlowerGameBrain found. " +
                "Wire the 'brain' field in the Inspector or ensure session.brain is set.", this);
    }

    void SetVisible(bool visible)
    {
        if (root == null) return;
        root.alpha = visible ? 1f : 0f;
        root.interactable = visible;
        root.blocksRaycasts = false; // never block gameplay clicks
    }
}
