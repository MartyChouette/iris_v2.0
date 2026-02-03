/**
 * @file SapOnXYTether.cs
 * @brief SapOnXYTether script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup flowers_runtime
 */

using UnityEngine;

[RequireComponent(typeof(XYTetherJoint))]
/**
 * @class SapOnXYTether
 * @brief SapOnXYTether component.
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
public class SapOnXYTether : MonoBehaviour
{
    public enum PartKind { Leaf, Petal }
    public PartKind partKind = PartKind.Leaf;

    public Vector3 localOffset = Vector3.zero;
    public Vector3 localNormal = Vector3.up;

    private XYTetherJoint _joint;
    private FlowerSapController _sap;
    private FlowerSessionController _session;
    private FlowerPartRuntime _part;

    private bool _hasFired = false;

    private void Awake()
    {
        _joint = GetComponent<XYTetherJoint>();
        _sap = GetComponentInParent<FlowerSapController>();
        _session = GetComponentInParent<FlowerSessionController>();
        _part = GetComponent<FlowerPartRuntime>();

        if (_sap == null)
            Debug.LogError($"[SapOnXYTether] No FlowerSapController found in parent hierarchy of '{name}'. " +
                "Sap emission will be skipped on joint break.", this);
        if (_session == null)
            Debug.LogWarning($"[SapOnXYTether] No FlowerSessionController found in parent hierarchy of '{name}'. " +
                "Suppression check will fall back to scene-wide find.", this);
    }

    private void OnEnable()
    {
        if (_joint != null) _joint.onBroke.AddListener(OnJointBroke);
    }

    private void OnDisable()
    {
        if (_joint != null) _joint.onBroke.RemoveListener(OnJointBroke);
    }

    private void OnJointBroke()
    {
        if (_hasFired) return;
        if (_sap == null) return;

        // --- FIX 1: SUPPRESSION CHECK ---
        // If the session is currently suppressing events (due to a stem cut),
        // DO NOT fire fluid. This fixes the "wrong moment" firing.
        // PERF: _session is cached in Awake(); no scene search fallback needed.
        if (_session != null && _session.suppressDetachEvents) return;

        // --- FIX 2: ALREADY DETACHED CHECK ---
        // Only fire if the part WAS attached and just broke. 
        // Fixes duplicate firing if logic runs multiple times.
        if (_part != null && !_part.isAttached) return;

        _hasFired = true;

        Vector3 worldPos = transform.TransformPoint(localOffset);
        Vector3 worldNormal = transform.TransformDirection(localNormal).normalized;

        if (partKind == PartKind.Leaf)
            _sap.EmitLeafTear(worldPos, worldNormal);
        else
            _sap.EmitPetalTear(worldPos, worldNormal);
    }
}