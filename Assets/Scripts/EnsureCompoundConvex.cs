using UnityEngine;

[DefaultExecutionOrder(10000)]
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