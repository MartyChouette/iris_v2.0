using UnityEngine;

/// <summary>
/// Soft-anchors a "held" stem rigidbody to a kinematic world anchor using a ConfigurableJoint,
/// so the flower can sway but won't fully fall after a cut.
///
/// Usage:
/// 1) Add this to the flower root.
/// 2) Assign (optional) worldAnchorTransform OR let it auto-create one.
/// 3) After a cut, call AnchorHeldStem(heldStemRb, anchorWorldPoint).
/// 4) Call ReleaseStem(fallingStemRb) for the falling piece if you ever anchored it by mistake.
///
/// Notes:
/// - The world anchor Rigidbody is kinematic + no gravity.
/// - The held stem Rigidbody still uses gravity; the joint prevents free-fall.
/// </summary>
[DisallowMultipleComponent]
public class SoftStemAnchor : MonoBehaviour
{
    [Header("World Anchor")]
    [Tooltip("Optional: assign an existing anchor transform (must have/contain a Rigidbody). If null, one is auto-created.")]
    public Transform worldAnchorTransform;

    [Tooltip("If true and no anchor is assigned, creates a hidden anchor GameObject at Start.")]
    public bool autoCreateAnchor = true;

    [Tooltip("Where the anchor lives by default if auto-created (world-space). If null, uses this transform.")]
    public Transform defaultAnchorPoint;

    [Header("Soft Anchor Tuning")]
    [Tooltip("How far (meters) the held stem is allowed to drift from the anchor.")]
    [Range(0.001f, 0.10f)]
    public float linearLimit = 0.03f;

    [Tooltip("Spring strength pulling the held stem back toward the anchor.")]
    public float spring = 3000f;

    [Tooltip("Damping to reduce oscillation.")]
    public float damper = 150f;

    [Tooltip("Max force the drive can apply.")]
    public float maxForce = 10000f;

    [Header("Projection (stability)")]
    public bool useProjection = true;
    public float projectionDistance = 0.02f;

    [Header("Debug")]
    public bool debugLogs = false;

    private Rigidbody _anchorRb;

    private void Awake()
    {
        EnsureAnchor();
    }

    private void Start()
    {
        EnsureAnchor();
    }

    /// <summary>
    /// Ensures a kinematic world anchor rigidbody exists.
    /// </summary>
    public void EnsureAnchor()
    {
        if (_anchorRb != null) return;

        if (worldAnchorTransform != null)
        {
            _anchorRb = worldAnchorTransform.GetComponent<Rigidbody>();
            if (_anchorRb == null)
                _anchorRb = worldAnchorTransform.GetComponentInChildren<Rigidbody>();
        }

        if (_anchorRb == null && autoCreateAnchor)
        {
            var go = new GameObject($"{name}_WorldAnchor");
            go.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor; // keeps scene cleaner
            go.transform.position = (defaultAnchorPoint != null) ? defaultAnchorPoint.position : transform.position;
            go.transform.rotation = Quaternion.identity;

            _anchorRb = go.AddComponent<Rigidbody>();
            _anchorRb.isKinematic = true;
            _anchorRb.useGravity = false;

            worldAnchorTransform = go.transform;

            if (debugLogs)
                Debug.Log($"[SoftStemAnchor] Auto-created world anchor: {go.name}", this);
        }

        if (_anchorRb != null)
        {
            _anchorRb.isKinematic = true;
            _anchorRb.useGravity = false;
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[SoftStemAnchor] No anchor rigidbody found/created.", this);
        }
    }

    /// <summary>
    /// Soft-anchors the held stem rigidbody so it can sway but not fall.
    /// Call this AFTER a cut once you have identified which stem piece is the held/top piece.
    /// </summary>
    /// <param name="heldStem">The stem piece rigidbody you want to remain attached.</param>
    /// <param name="anchorWorldPoint">World-space point to anchor around (often base of flower/vase point).</param>
    public void AnchorHeldStem(Rigidbody heldStem, Vector3 anchorWorldPoint)
    {
        EnsureAnchor();
        if (_anchorRb == null || heldStem == null) return;

        // Remove any previous anchor joints on this held stem
        RemoveAnchorJoints(heldStem);

        // Create the soft joint
        var cj = heldStem.gameObject.AddComponent<ConfigurableJoint>();
        cj.connectedBody = _anchorRb;
        cj.autoConfigureConnectedAnchor = false;

        // Anchor at a specific world point (stable, predictable)
        cj.anchor = heldStem.transform.InverseTransformPoint(anchorWorldPoint);
        cj.connectedAnchor = _anchorRb.transform.InverseTransformPoint(anchorWorldPoint);

        // Limited linear sway
        cj.xMotion = ConfigurableJointMotion.Limited;
        cj.yMotion = ConfigurableJointMotion.Limited;
        cj.zMotion = ConfigurableJointMotion.Limited;

        var lim = new SoftJointLimit { limit = Mathf.Max(0.0001f, linearLimit) };
        cj.linearLimit = lim;

        // Allow free rotation so it can naturally rotate
        cj.angularXMotion = ConfigurableJointMotion.Free;
        cj.angularYMotion = ConfigurableJointMotion.Free;
        cj.angularZMotion = ConfigurableJointMotion.Free;

        // Spring back toward anchor
        var drive = new JointDrive
        {
            positionSpring = Mathf.Max(0f, spring),
            positionDamper = Mathf.Max(0f, damper),
            maximumForce = Mathf.Max(0f, maxForce)
        };
        cj.xDrive = drive;
        cj.yDrive = drive;
        cj.zDrive = drive;

        // Projection helps prevent drift/explosions if forces get high
        if (useProjection)
        {
            cj.projectionMode = JointProjectionMode.PositionAndRotation;
            cj.projectionDistance = Mathf.Max(0.0001f, projectionDistance);
        }
        else
        {
            cj.projectionMode = JointProjectionMode.None;
        }

        if (debugLogs)
            Debug.Log($"[SoftStemAnchor] Anchored held stem '{heldStem.name}' at {anchorWorldPoint}", heldStem);
    }

    /// <summary>
    /// Removes any anchor joints that connect the given rigidbody to our world anchor.
    /// Useful if you accidentally anchored the wrong piece or want to force it to fall.
    /// </summary>
    public void ReleaseStem(Rigidbody rb)
    {
        if (rb == null) return;
        RemoveAnchorJoints(rb);

        if (debugLogs)
            Debug.Log($"[SoftStemAnchor] Released stem '{rb.name}'", rb);
    }

    private void RemoveAnchorJoints(Rigidbody rb)
    {
        if (rb == null) return;

        var joints = rb.GetComponents<Joint>();
        for (int i = 0; i < joints.Length; i++)
        {
            var j = joints[i];
            if (j == null) continue;

            // Only remove joints connected to OUR anchor
            if (_anchorRb != null && j.connectedBody == _anchorRb)
                Destroy(j);
        }
    }
}
