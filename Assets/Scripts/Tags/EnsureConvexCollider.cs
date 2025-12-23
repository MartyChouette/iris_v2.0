/**
 * @file EnsureConvexCollider.cs
 * @brief EnsureConvexCollider script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnsureCompoundConvex))]
/**
 * @class EnsureConvexCollider
 * @brief EnsureConvexCollider component.
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
 * @ingroup tools
 */

public class EnsureConvexCollider : MonoBehaviour
{
    void Awake()
    {
        var mc = GetComponent<MeshCollider>();
        var rb = GetComponent<Rigidbody>();
        if (mc != null && rb != null && !rb.isKinematic && !mc.convex)
        {
            mc.convex = true;       // or rb.isKinematic = true;
        }
    }
}