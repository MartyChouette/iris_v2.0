/**
 * @file SoftStemAnchor.cs
 * @brief SoftStemAnchor script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup flowers_runtime
 */

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
/**
 * @class SoftStemAnchor
 * @brief SoftStemAnchor component.
 * @details
 * Responsibilities:
 * - (Documented) See fields and methods below.
 *
 * Unity lifecycle:
 * - Awake(): cache references / validate setup.
 * - OnEnable()/OnDisable(): hook/unhook events.
 * - Update(): per-frame behavior (if any).
 *
 * Gotchas:
 * - Keep hot paths allocation-free (Update/cuts/spawns).
 * - Prefer event-driven UI updates over per-frame string building.
 *
 * @ingroup flowers_runtime
 */
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

    [Header("Angular Damping")]
    [Tooltip("If true, applies an angular drive to resist spinning (prevents wild rotation).")]
    public bool useAngularDamping = true;

    [Tooltip("Angular spring strength (resists rotation away from initial orientation).")]
    public float angularSpring = 500f;

    [Tooltip("Angular damper (reduces rotational velocity).")]
    public float angularDamper = 80f;

    [Tooltip("Max torque the angular drive can apply.")]
    public float angularMaxForce = 5000f;

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

            // IMPORTANT: world-space. Do NOT parent it.
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
    public void AnchorHeldStem(Rigidbody heldStem, Vector3 anchorWorldPoint)
    {
        EnsureAnchor();
        if (_anchorRb == null || heldStem == null) return;

        // IMPORTANT: Move the anchor RB to the target world point.
        // This makes connectedAnchor stable (0,0,0) and avoids sag/jitter.
        worldAnchorTransform.position = anchorWorldPoint;
        worldAnchorTransform.rotation = Quaternion.identity;

        // Remove any previous anchor joints on this held stem
        RemoveAnchorJoints(heldStem);

        // Create the soft joint
        var cj = heldStem.gameObject.AddComponent<ConfigurableJoint>();
        cj.connectedBody = _anchorRb;
        cj.autoConfigureConnectedAnchor = false;

        // CRITICAL FIX:
        // Pin the closest point on the held chunk to the anchor, not a point the chunk may not contain post-cut.
        Vector3 heldAttachWorld = ClosestPointOnBody(heldStem, anchorWorldPoint);

        cj.anchor = heldStem.transform.InverseTransformPoint(heldAttachWorld);
        cj.connectedAnchor = Vector3.zero;

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

        // Angular damping drive to resist wild spinning
        if (useAngularDamping)
        {
            var angDrive = new JointDrive
            {
                positionSpring = Mathf.Max(0f, angularSpring),
                positionDamper = Mathf.Max(0f, angularDamper),
                maximumForce = Mathf.Max(0f, angularMaxForce)
            };
            cj.angularXDrive = angDrive;
            cj.angularYZDrive = angDrive;
            cj.rotationDriveMode = RotationDriveMode.XYAndZ;
            // Target rotation = identity (relative to connected body) keeps it upright
            cj.targetRotation = Quaternion.identity;
        }

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
            Debug.Log($"[SoftStemAnchor] Anchored held stem '{heldStem.name}' attach@{heldAttachWorld} -> target@{anchorWorldPoint}", heldStem);
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

    private Vector3 ClosestPointOnBody(Rigidbody rb, Vector3 worldPos)
    {
        if (rb == null) return worldPos;

        var cols = rb.GetComponentsInChildren<Collider>(true);
        if (cols != null && cols.Length > 0)
        {
            Vector3 best = rb.worldCenterOfMass;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null) continue;

                Vector3 p = c.ClosestPoint(worldPos);
                float d = (p - worldPos).sqrMagnitude;
                if (d < bestDistSq)
                {
                    bestDistSq = d;
                    best = p;
                }
            }
            return best;
        }

        return rb.worldCenterOfMass;
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
