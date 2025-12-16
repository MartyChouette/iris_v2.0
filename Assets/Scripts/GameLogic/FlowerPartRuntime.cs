// File: FlowerPartRuntime.cs
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Runtime component on any physical flower piece (leaf, petal, crown, etc).
/// Tracks whether the part is attached and can trigger game over on critical breaks.
/// </summary>
[DisallowMultipleComponent]
public class FlowerPartRuntime : MonoBehaviour
{
    // ADDED: canonical detach reasons so other systems can make correct decisions.
    public enum DetachReason
    {
        None,
        PlayerRipped,
        PlayerCut,
        StemSwap,
        PhysicsBreak,
        UnityJointBreak,
        Debug
    }

    [Header("Identity / Matching")]
    [Tooltip("Unique ID for this part so the brain can match it to IdealFlowerDefinition.partRules.")]
    public string PartId;

    public FlowerPartKind kind = FlowerPartKind.Leaf;

    [Header("Runtime Condition")]
    public FlowerPartCondition condition = FlowerPartCondition.Normal;

    [Tooltip("True while the part is still attached to the flower.")]
    public bool isAttached = true;

    // ADDED: if true, this part must never be rebound again.
    [Header("Detach Authority")]
    [Tooltip("If true, this part is permanently detached and MUST never be rebound.")]
    public bool permanentlyDetached = false;

    [Tooltip("Last known reason for detachment. Useful for debugging + rebinder rules.")]
    public DetachReason lastDetachReason = DetachReason.None;

    [Header("Authoring / Rule Hints (some of this is duplicated in IdealFlowerDefinition)")]
    [Tooltip("If true, removing this part may immediately cause game over (in addition to any Ideal rules).")]
    public bool canCauseGameOver = false;

    [Tooltip("If true, this part is special (for UI / feedback).")]
    public bool isSpecial = false;

    [Tooltip("If true, differences on this part affect score.")]
    public bool contributesToScore = true;

    [Tooltip("If true, this part is allowed to be withered and still OK.")]
    public bool allowedWithered = true;

    [Tooltip("If false, missing this part counts against you.")]
    public bool allowedMissing = false;

    [Range(0f, 1f)]
    [Tooltip("Score importance of this part relative to other parts.")]
    public float scoreWeight = 1f;

    [Header("Debug / Ideal Pose (optional)")]
    public Vector3 idealLocalPosition;
    public Vector3 idealLocalEuler;

    [Header("Runtime refs")]
    public FlowerSessionController session;
    public FlowerGameBrain brain;

    [Header("Physics")]
    [Tooltip("Optional custom tether joint used instead of generic Unity joints.")]
    public XYTetherJoint xyJoint;

    [Tooltip("Fallback Unity joints (HingeJoint, SpringJoint, etc.) used for detachment events.")]
    public Joint[] unityJoints;

    [Header("Crown Fall Failsafe")]
    [Tooltip("If true and this part is a Crown, then if it falls below crownFailY after detaching, the session will be forced to game over.")]
    public bool enableCrownYFailsafe = true;

    [Tooltip("World-space Y threshold for crown fall failsafe. If the crown's position.y drops below this after detaching, it will trigger a forced game over.")]
    public float crownFailY = -1f;

    // Internal guard so we only trigger the fall failsafe once.
    private bool _crownFallFailTriggered = false;

    private void Awake()
    {
        // Auto-wire session / brain if not set in inspector.
        if (session == null)
            session = GetComponentInParent<FlowerSessionController>();
        if (brain == null)
            brain = GetComponentInParent<FlowerGameBrain>();

        // Auto-wire XY joint if not set.
        if (xyJoint == null)
            xyJoint = GetComponent<XYTetherJoint>();

        // Cache any joints if array empty.
        if (unityJoints == null || unityJoints.Length == 0)
            unityJoints = GetComponents<Joint>();

        // Guard rail: some clones / destroyed parts may have a missing or half-constructed XY joint.
        if (xyJoint != null)
        {
            if (xyJoint.onBroke == null)
                xyJoint.onBroke = new UnityEvent();

            xyJoint.onBroke.AddListener(OnXYJointBroke);
        }
    }

    private void OnDestroy()
    {
        // Guard against race conditions when parts are destroyed by the cutter.
        if (xyJoint != null && xyJoint.onBroke != null)
            xyJoint.onBroke.RemoveListener(OnXYJointBroke);
    }

    private void Update()
    {
        if (enableCrownYFailsafe &&
            kind == FlowerPartKind.Crown &&
            !_crownFallFailTriggered &&
            session != null)
        {
            if (transform.position.y < crownFailY)
            {
                _crownFallFailTriggered = true;
                session.ForceGameOver("Crown fell too low.");
            }
        }
    }

    /// <summary>
    /// Central check: should we ignore detach events right now?
    /// </summary>
    public bool ShouldSuppressDetachEvents()
    {
        return session != null && session.suppressDetachEvents;
    }

    // Unity built-in: any 3D Joint on THIS object breaking will call this.
    private void OnJointBreak(float breakForce)
    {
        // CHANGED: use new overload with reason + permanence
        MarkDetached("Unity joint broke", DetachReason.UnityJointBreak, permanent: true);
    }

    // Called by XYTetherJoint via its UnityEvent.
    private void OnXYJointBroke()
    {
        // CHANGED: default reason (XYTetherJoint will ideally call the richer overload below)
        MarkDetached("XY tether broke", DetachReason.PhysicsBreak, permanent: true);
    }

    /// <summary>
    /// Backwards-compatible API (existing calls keep working).
    /// Defaults to permanent detachment (safe for rebinder rules).
    /// </summary>
    public void MarkDetached(string reason = "Detached")
    {
        MarkDetached(reason, DetachReason.Debug, permanent: true);
    }

    /// <summary>
    /// NEW authoritative detach API. Use this whenever possible.
    /// permanent=true means: DO NOT EVER REBIND THIS PART.
    /// </summary>
    public void MarkDetached(string reason, DetachReason detachReason, bool permanent)
    {
        // Ignore detach events while the session is in a cut/rebind grace window.
        if (ShouldSuppressDetachEvents())
        {
            Debug.Log($"[FlowerPartRuntime] Detach '{PartId}' skipped during cut grace: {reason}", this);
            return;
        }

        // If we're already detached, don't double-fire.
        if (!isAttached)
            return;

        isAttached = false;

        // ADDED: record canonical detach state
        lastDetachReason = detachReason;
        if (permanent)
            permanentlyDetached = true;

        Debug.Log($"[FlowerPartRuntime] '{PartId}' detached: {reason} (Reason={detachReason}, Permanent={permanent})", this);

        bool triggerInstantFail = false;
        string failReason = "";

        // The only true instant game over we want is when the crown is lost.
        int crownLayer = LayerMask.NameToLayer("CrownCore");
        bool isCrownByLayer = (crownLayer >= 0 && gameObject.layer == crownLayer);
        bool isCrownByKind = (kind == FlowerPartKind.Crown);

        if (isCrownByLayer || isCrownByKind)
        {
            triggerInstantFail = true;
            failReason = "Crown detached.";
        }

        if (triggerInstantFail && session != null)
        {
            session.ForceGameOver(failReason);
        }
        // else: no other parts cause immediate failure here.
    }
}
