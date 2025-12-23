/**
 * @file EnsureCompoundConvex.cs
 * @brief EnsureCompoundConvex script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

using UnityEngine;

[DefaultExecutionOrder(10000)]
/**
 * @class EnsureCompoundConvex
 * @brief EnsureCompoundConvex component.
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
public class EnsureCompoundConvex : MonoBehaviour
{
    void Awake() { FixAll(); }
    void OnEnable() { FixAll(); }

#if UNITY_EDITOR
    void OnValidate() { if (!Application.isPlaying) FixAll(); }
#endif

    void FixAll()
    {
        // Look at every Rigidbody under this root
        var bodies = GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in bodies)
        {
            if (rb == null || rb.isKinematic)
                continue;

            // All MeshColliders that belong to this RB's compound collider
            var colliders = rb.GetComponentsInChildren<MeshCollider>(true);
            foreach (var mc in colliders)
            {
                if (mc != null && !mc.convex)
                {
                    mc.convex = true;  // or: rb.isKinematic = true;
#if UNITY_EDITOR
                    Debug.Log($"[EnsureCompoundConvex] Set convex on {GetPath(mc.transform)}", mc);
#endif
                }
            }
        }
    }

    static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}