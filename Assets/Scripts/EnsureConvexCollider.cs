using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnsureCompoundConvex))]

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