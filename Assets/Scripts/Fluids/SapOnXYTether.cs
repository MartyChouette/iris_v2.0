using UnityEngine;

/// <summary>
/// Listens to XYTetherJoint.onBroke and tells FlowerSapController
/// to emit a leaf or petal tear burst.
/// Attach this wherever your XYTetherJoint lives (leaf or petal anchor).
/// </summary>
[RequireComponent(typeof(XYTetherJoint))]
public class SapOnXYTether : MonoBehaviour
{
    public enum PartKind
    {
        Leaf,
        Petal
    }

    [Header("Which part is this?")]
    public PartKind partKind = PartKind.Leaf;

    [Header("Where to spawn sap relative to this transform")]
    [Tooltip("Local-space offset for the sap spawn position (e.g. tip of the leaf).")]
    public Vector3 localOffset = Vector3.zero;

    [Tooltip("Local-space direction the sap should spray along (will be normalized).")]
    public Vector3 localNormal = Vector3.up;

    private XYTetherJoint _joint;
    private FlowerSapController _sap;
    private FlowerSessionController _session; // Reference to check for grace window

    private bool _hasFired = false; // One-shot flag

    private void Awake()
    {
        _joint = GetComponent<XYTetherJoint>();
        _sap = GetComponentInParent<FlowerSapController>();
        _session = GetComponentInParent<FlowerSessionController>(); // Attempt to find session in parent
    }

    private void OnEnable()
    {
        if (_joint != null)
            _joint.onBroke.AddListener(OnJointBroke);
    }

    private void OnDisable()
    {
        if (_joint != null)
            _joint.onBroke.RemoveListener(OnJointBroke);
    }

    private void OnJointBroke()
    {
        // 1. One-Shot Check: If we already fired, stop.
        if (_hasFired) return;

        // 2. References Check
        if (_sap == null) return;

        // 3. Grace Window Check: 
        // If the session says we are suppressing detaches (e.g. right after a stem cut),
        // we should NOT fire the fluid. This prevents leaves from squirting due to cut shock.
        if (_session == null)
        {
            // Try to find it one last time (e.g. if we were reparented)
            _session = GetComponentInParent<FlowerSessionController>();
            if (_session == null)
                _session = Object.FindFirstObjectByType<FlowerSessionController>();
        }

        if (_session != null && _session.suppressDetachEvents)
        {
            return; // Silently fail -> we assume this break was caused by the stem cut shock
        }

        // --- FIRE ---
        _hasFired = true;

        // Convert local offset/normal to world space:
        Vector3 worldPos = transform.TransformPoint(localOffset);
        Vector3 worldNormal = transform.TransformDirection(localNormal).normalized;

        if (worldNormal.sqrMagnitude < 0.0001f)
            worldNormal = transform.up;

        switch (partKind)
        {
            case PartKind.Leaf:
                _sap.EmitLeafTear(worldPos, worldNormal);
                break;
            case PartKind.Petal:
                _sap.EmitPetalTear(worldPos, worldNormal);
                break;
        }

        // Optimization: We can safely remove the listener now since we won't fire again
        if (_joint != null)
            _joint.onBroke.RemoveListener(OnJointBroke);
    }
}