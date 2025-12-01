using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Rebinds all joints (FixedJoint, HingeJoint, ConfigurableJoint, XYTetherJoint)
/// under a flower / stem to the closest stem piece after a cut.
/// Attach this to the flower root (e.g. Flower_Daisy).
/// Call RebindAllPartsToClosestStemPiece() right after the stem is cut and
/// new stem pieces with StemPieceMarker components have been created.
/// </summary>
public class FlowerJointRebinder : MonoBehaviour
{
    [Tooltip("Runtime stem this flower belongs to. If left null, will be auto-found in children.")]
    public FlowerStemRuntime stemRuntime;

    [Tooltip("Root transform for this flower. If null, uses this.transform.")]
    public Transform flowerRoot;

    /// <summary>
    /// Call this AFTER the stem has been split / cut and the new stem pieces exist
    /// and have StemPieceMarker components pointing back to this stemRuntime.
    /// </summary>
    public void RebindAllPartsToClosestStemPiece()
    {
        // --- Resolve references ---
        if (flowerRoot == null)
            flowerRoot = transform;

        if (stemRuntime == null)
            stemRuntime = flowerRoot.GetComponentInChildren<FlowerStemRuntime>();

        if (stemRuntime == null)
            return;

        // 1. Collect all stem piece rigidbodies that belong to THIS stem
        var markers = FindObjectsByType<StemPieceMarker>(FindObjectsSortMode.None);
        var stemPieces = markers
            .Where(m => m != null && m.stemRuntime == stemRuntime)
            .Select(m => m.GetComponent<Rigidbody>())
            .Where(rb => rb != null)
            .ToArray();

        // Fallback: if no markers yet, just use any rigidbodies under the stem
        if (stemPieces.Length == 0)
            stemPieces = stemRuntime.GetComponentsInChildren<Rigidbody>(true);

        if (stemPieces.Length == 0)
            return;

        // Lookup set: "is this RB one of our stem pieces?"
        var stemSet = new HashSet<Rigidbody>(stemPieces);

        // 2. Find ALL joints:
        //    - joints on flowerRoot (leaves, petals, etc.)
        //    - joints on the stem hierarchy (attachment nodes, crown, etc.)
        var fixedJoints = CollectJoints<FixedJoint>(flowerRoot, stemRuntime);
        var hingeJoints = CollectJoints<HingeJoint>(flowerRoot, stemRuntime);
        var configurableJoints = CollectJoints<ConfigurableJoint>(flowerRoot, stemRuntime);
        var xyJoints = CollectJoints<XYTetherJoint>(flowerRoot, stemRuntime);

        // 3. Rebind each joint type
        RebindFixedJoints(fixedJoints, stemPieces, stemSet);
        RebindHingeJoints(hingeJoints, stemPieces, stemSet);
        RebindConfigJoints(configurableJoints, stemPieces, stemSet);
        RebindXYTetherJoints(xyJoints, stemPieces, stemSet);
    }

    // ─────────────────── Joint collection helper ───────────────────

    private T[] CollectJoints<T>(Transform rootA, FlowerStemRuntime stem) where T : Component
    {
        var result = new List<T>();

        if (rootA != null)
            result.AddRange(rootA.GetComponentsInChildren<T>(true));

        if (stem != null)
            result.AddRange(stem.GetComponentsInChildren<T>(true));

        // Remove duplicates if any object is in both hierarchies
        return result.Distinct().ToArray();
    }

    // ─────────────────── Hierarchy helper ───────────────────

    private bool IsUnderStem(Transform t)
    {
        if (stemRuntime == null) return false;
        Transform stemRoot = stemRuntime.transform;

        while (t != null)
        {
            if (t == stemRoot)
                return true;
            t = t.parent;
        }

        return false;
    }

    // ─────────────────── Joint rebind helpers ───────────────────

    private void RebindFixedJoints(FixedJoint[] joints, Rigidbody[] stemPieces, HashSet<Rigidbody> stemSet)
    {
        foreach (var fj in joints)
        {
            if (fj == null) continue;

            var ownerRb = fj.GetComponent<Rigidbody>();
            if (ownerRb == null) continue;

            bool onStemHierarchy = IsUnderStem(fj.transform);
            bool isLeafAttachment = fj.GetComponent<LeafAttachmentMarker>() != null;

            // SPECIAL CASE: Leaf attachment sphere.
            // These are the little spheres the leaves are XY-tethered to.
            // We always rebind them to the closest stem piece, because their
            // connectedBody is often the old stem which may be gone or null.
            if (isLeafAttachment && onStemHierarchy)
            {
                Vector3 anchorWorld = fj.transform.TransformPoint(fj.anchor);
                var newBody = FindClosestStemPiece(anchorWorld, stemPieces, ownerRb);
                if (newBody != null && newBody != ownerRb)
                {
                    Debug.Log($"[Rebinder] Leaf attachment '{fj.name}' reconnected to '{newBody.name}'", fj);
                    fj.connectedBody = newBody;
                }
                else
                {
                    Debug.Log($"[Rebinder] Leaf attachment '{fj.name}' could not find a new stem piece.", fj);
                }
                continue;
            }

            // ORIGINAL RULE for everything else:
            if (fj.connectedBody == null) continue;
            if (!stemSet.Contains(fj.connectedBody))
                continue;

            Vector3 anchorWorldNormal = fj.transform.TransformPoint(fj.anchor);
            var newBodyNormal = FindClosestStemPiece(anchorWorldNormal, stemPieces, ownerRb);
            if (newBodyNormal == null || newBodyNormal == ownerRb)
                continue; // never connect a joint to itself

            fj.connectedBody = newBodyNormal;
        }
    }

    private void RebindHingeJoints(HingeJoint[] joints, Rigidbody[] stemPieces, HashSet<Rigidbody> stemSet)
    {
        foreach (var hj in joints)
        {
            if (hj == null) continue;
            if (hj.connectedBody == null) continue;

            var ownerRb = hj.GetComponent<Rigidbody>();
            if (ownerRb == null) continue;

            if (!stemSet.Contains(hj.connectedBody))
                continue;

            Vector3 anchorWorld = hj.transform.TransformPoint(hj.anchor);
            var newBody = FindClosestStemPiece(anchorWorld, stemPieces, ownerRb);
            if (newBody == null || newBody == ownerRb)
                continue;

            hj.connectedBody = newBody;
        }
    }

    private void RebindConfigJoints(ConfigurableJoint[] joints, Rigidbody[] stemPieces, HashSet<Rigidbody> stemSet)
    {
        foreach (var cj in joints)
        {
            if (cj == null) continue;
            if (cj.connectedBody == null) continue;

            var ownerRb = cj.GetComponent<Rigidbody>();
            if (ownerRb == null) continue;

            if (!stemSet.Contains(cj.connectedBody))
                continue;

            Vector3 anchorWorld = cj.transform.TransformPoint(cj.anchor);
            var newBody = FindClosestStemPiece(anchorWorld, stemPieces, ownerRb);
            if (newBody == null || newBody == ownerRb)
                continue;

            cj.connectedBody = newBody;
        }
    }

    private void RebindXYTetherJoints(XYTetherJoint[] joints, Rigidbody[] stemPieces, HashSet<Rigidbody> stemSet)
    {
        foreach (var xy in joints)
        {
            if (xy == null) continue;
            if (xy.connectedBody == null) continue;

            var ownerRb = xy.GetComponent<Rigidbody>();
            if (ownerRb == null) continue;

            // We only want XY joints that were attached DIRECTLY to stem pieces.
            // Leaves & petals attached to crown / attachment nodes should keep
            // their existing connectedBody.
            if (!stemSet.Contains(xy.connectedBody))
                continue;

            Vector3 refPos = xy.transform.position;
            var newBody = FindClosestStemPiece(refPos, stemPieces, ownerRb);
            if (newBody == null || newBody == ownerRb)
                continue;

            xy.connectedBody = newBody;
        }
    }

    /// <summary>
    /// Finds the closest stem piece to a given world-space point, using collider.ClosestPoint
    /// when available, otherwise falling back to the Rigidbody's worldCenterOfMass.
    /// Optionally excludes a specific rigidbody (e.g. the joint owner) so we
    /// don’t pick "self" as the connection target.
    /// </summary>
    private Rigidbody FindClosestStemPiece(Vector3 worldPos, Rigidbody[] pieces, Rigidbody exclude = null)
    {
        Rigidbody best = null;
        float bestDistSq = float.MaxValue;

        foreach (var rb in pieces)
        {
            if (rb == null) continue;
            if (rb == exclude) continue; // don’t allow self

            var cols = rb.GetComponentsInChildren<Collider>(true);

            if (cols != null && cols.Length > 0)
            {
                foreach (var col in cols)
                {
                    if (col == null) continue;

                    Vector3 closest = col.ClosestPoint(worldPos);
                    float d = (closest - worldPos).sqrMagnitude;

                    if (d < bestDistSq)
                    {
                        bestDistSq = d;
                        best = rb;
                    }
                }
            }
            else
            {
                float d = (rb.worldCenterOfMass - worldPos).sqrMagnitude;
                if (d < bestDistSq)
                {
                    bestDistSq = d;
                    best = rb;
                }
            }
        }

        return best;
    }
}
