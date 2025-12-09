using UnityEngine;

/// <summary>
/// Listens to XYTetherJoint.onBroke and tells FlowerSapController
/// to emit a leaf or petal tear burst.
/// Attach this wherever your XYTetherJoint lives (leaf or petal anchor).
/// </summary>
[RequireComponent(typeof(XYTetherJoint))]
public class SapOnXYTetherBreak : MonoBehaviour
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

    private void Awake()
    {
        _joint = GetComponent<XYTetherJoint>();
        _sap = GetComponentInParent<FlowerSapController>();
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
        if (_sap == null)
            return;

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
    }
}
