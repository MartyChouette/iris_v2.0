// File: FlowerPartRuntime.cs
using UnityEngine;

[DisallowMultipleComponent]
public class FlowerPartRuntime : MonoBehaviour
{
    [Header("Identity / Matching")]
    [Tooltip("Unique ID for this part so the brain can match it to an ideal spec.")]
    public string PartId;

    public FlowerPartKind kind = FlowerPartKind.Leaf;

    [Header("Runtime Condition")]
    public FlowerPartCondition condition = FlowerPartCondition.Normal;

    [Tooltip("True while the part is still attached to the flower.")]
    public bool isAttached = true;

    [Header("Authoring / Rule Hints (baked into IdealFlowerDefinition)")]
    public bool canCauseGameOver = false;
    public bool isSpecial = false;
    public bool contributesToScore = true;
    public bool allowedWithered = false;
    public bool allowedMissing = false;
    [Range(0f, 1f)] public float scoreWeight = 0.1f;

    [Header("Debug / Ideal Pose")]
    public Vector3 idealLocalPosition;
    public Vector3 idealLocalEuler;

    [Header("Optional Joint Refs")]
    [Tooltip("Custom XY tether joint, if this part uses one.")]
    public XYTetherJoint xyJoint;

    [Tooltip("Any Unity joints (FixedJoint, HingeJoint, etc) on this same GameObject.")]
    public Joint[] unityJoints;

    [Header("Auto-wired at runtime")]
    [HideInInspector] public FlowerSessionController session;
    [HideInInspector] public FlowerGameBrain brain;

    private void Awake()
    {
        // Auto-wire session / brain if not set in inspector
        if (session == null)
            session = GetComponentInParent<FlowerSessionController>();
        if (brain == null)
            brain = GetComponentInParent<FlowerGameBrain>();

        // Auto-wire XY joint if not set
        if (xyJoint == null)
            xyJoint = GetComponent<XYTetherJoint>();

        // Cache any joints if array empty
        if (unityJoints == null || unityJoints.Length == 0)
            unityJoints = GetComponents<Joint>();

        // Listen for XY joint break
        if (xyJoint != null)
            xyJoint.onBroke.AddListener(OnXYJointBroke);
    }

    private void OnDestroy()
    {
        if (xyJoint != null)
            xyJoint.onBroke.RemoveListener(OnXYJointBroke);
    }

    // Called automatically by Unity when ANY 3D Joint on THIS object breaks
    private void OnJointBreak(float breakForce)
    {
        MarkDetached("Unity joint broke");
    }

    // Called by XYTetherJoint via its UnityEvent
    private void OnXYJointBroke()
    {
        MarkDetached("XY tether broke");
    }

    /// <summary>
    /// Public so cutters or other scripts can force a detach.
    /// </summary>
    public void MarkDetached(string reason = "Detached")
    {
        if (!isAttached)
            return;

        isAttached = false;
        Debug.Log($"[FlowerPartRuntime] '{PartId}' detached: {reason}", this);

        bool triggerInstantFail = false;
        string failReason = "";

        // Crown detection by layer
        int crownLayer = LayerMask.NameToLayer("CrownCore");
        if (crownLayer >= 0 && gameObject.layer == crownLayer)
        {
            triggerInstantFail = true;
            failReason = "Crown detached.";
        }
        else if (brain != null)
        {
            // Option 2: any part whose idealCondition is Perfect
            if (brain.IsPartMarkedPerfect(PartId))
            {
                triggerInstantFail = true;
                failReason = $"Perfect part '{PartId}' was removed.";
            }

            // Global: all parts removed -> dead flower
            if (!triggerInstantFail)
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
