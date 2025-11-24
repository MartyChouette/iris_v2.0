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
    [Header("Identity / Matching")]
    [Tooltip("Unique ID for this part so the brain can match it to IdealFlowerDefinition.partRules.")]
    public string PartId;

    public FlowerPartKind kind = FlowerPartKind.Leaf;

    [Header("Runtime Condition")]
    public FlowerPartCondition condition = FlowerPartCondition.Normal;

    [Tooltip("True while the part is still attached to the flower.")]
    public bool isAttached = true;

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

    // Unity built-in: any 3D Joint on THIS object breaking will call this.
    private void OnJointBreak(float breakForce)
    {
        MarkDetached("Unity joint broke");
    }

    // Called by XYTetherJoint via its UnityEvent.
    private void OnXYJointBroke()
    {
        MarkDetached("XY tether broke");
    }

    /// <summary>
    /// Public so cutters or other scripts can force a detach.
    /// </summary>
    /// <summary>
    /// Public so cutters or other scripts can force a detach.
    /// </summary>
    public void MarkDetached(string reason = "Detached")
    {
        // NEW: ignore detach events while the session is in a cut/rebind grace window
        if (session != null && session.suppressDetachEvents)
        {
            Debug.Log($"[FlowerPartRuntime] Detach '{PartId}' skipped during cut grace: {reason}", this);
            return;
        }

        if (!isAttached)
            return;

        isAttached = false;
        Debug.Log($"[FlowerPartRuntime] '{PartId}' detached: {reason}", this);

        bool triggerInstantFail = false;
        string failReason = "";


        // ───────── Option 1: crown via layer ─────────
        int crownLayer = LayerMask.NameToLayer("CrownCore");
        if (crownLayer >= 0 && gameObject.layer == crownLayer)
        {
            triggerInstantFail = true;
            failReason = "Crown detached.";
        }

        // ───────── Option 2: Ideal per-part rules ─────────
        IdealFlowerDefinition.PartRule rule = null;
        if (!triggerInstantFail && brain != null && brain.ideal != null && !string.IsNullOrEmpty(PartId))
        {
            foreach (var r in brain.ideal.partRules)
            {
                if (r == null || string.IsNullOrEmpty(r.partId))
                    continue;
                if (r.partId == PartId)
                {
                    rule = r;
                    break;
                }
            }

            if (rule != null)
            {
                // Perfect parts: always fatal if removed.
                if (rule.idealCondition == FlowerPartCondition.Perfect)
                {
                    triggerInstantFail = true;
                    failReason = $"Perfect part '{PartId}' was removed.";
                }

                // Authoring flag that this part can cause game over.
                if (!triggerInstantFail && rule.canCauseGameOver)
                {
                    triggerInstantFail = true;
                    failReason = $"Critical part '{PartId}' was removed.";
                }

                // Part not allowed to be missing at all.
                if (!triggerInstantFail && !rule.allowedMissing)
                {
                    triggerInstantFail = true;
                    failReason = $"Part '{PartId}' is not allowed to be missing.";
                }
            }
        }

        // ───────── Option 3: runtime flag on this component ─────────
        if (!triggerInstantFail && canCauseGameOver)
        {
            triggerInstantFail = true;
            failReason = $"Critical part '{PartId}' was removed.";
        }

        // ───────── Option 4: if all parts are gone, treat as dead ─────────
        if (!triggerInstantFail && brain != null)
        {
            int total = 0;
            int attached = 0;
            foreach (var p in brain.parts)
            {
                if (p == null) continue;
                total++;
                if (p.isAttached) attached++;
            }

            if (total > 0 && attached == 0)
            {
                triggerInstantFail = true;
                failReason = "All parts removed – flower is dead.";
            }
        }

        if (triggerInstantFail && session != null)
        {
            session.ForceGameOver(failReason);
        }
        else
        {
            // No hard fail here – we *don’t* run full scoring yet.
            // HUD live stats will still see isAttached changes.
        }
    }
}
