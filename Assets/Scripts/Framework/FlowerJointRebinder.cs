using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Rebinds joints under a flower after a stem cut.
/// Key rules:
/// 1) Pick HELD stem piece using Crown joints when possible (most reliable).
/// 2) NEVER destroy joints on Crown/head parts. If they point to the wrong chunk, rebind to HELD.
/// 3) LeafAttachmentMarker joints should ALWAYS bind to HELD (unless their owning leaf is permanently detached).
/// 4) Optionally, sever stem-internal joints that still connect held<->falling chunks (rare, but fixes "won't drop").
/// </summary>
public class FlowerJointRebinder : MonoBehaviour
{
    [Tooltip("Runtime stem this flower belongs to. If left null, will be auto-found in children.")]
    public FlowerStemRuntime stemRuntime;

    [Tooltip("Root transform for this flower. If null, uses this.transform.")]
    public Transform flowerRoot;

    [Header("Held Selection")]
    [Tooltip("Optional explicit reference to crown/root of head. If null, we auto-find by Tag/Layer.")]
    public Transform crownRoot;

    [Header("Safety Gate")]
    public bool requireExplicitStemSwapGate = true;

    [Header("Post-Cut Behavior")]
    [Tooltip("If true, leaf attachment points (LeafAttachmentMarker) are forced onto HELD chunk.")]
    public bool forceLeafAttachmentsToHeld = true;

    [Tooltip("If true, crown/head joints that accidentally point to falling chunk are forced onto HELD chunk.")]
    public bool forceCrownJointsToHeld = true;

    [Tooltip("If true, any joints UNDER the stemRuntime that connect to the opposite chunk are destroyed (helps ensure separation).")]
    public bool severStemInternalCrossChunkJoints = true;

    [Tooltip("If true, makes falling stem chunks dynamic & awake.")]
    public bool forceFallingChunksDynamic = true;

    [Header("Debug")]
    public bool debugLogs = true;

    /// <summary>
    /// Call this AFTER the stem has been split / cut and the new stem pieces exist
    /// and have StemPieceMarker components pointing back to this stemRuntime.
    /// </summary>
    public void RebindAllPartsToClosestStemPiece(bool isStemSwapOperation = true)
    {
        if (requireExplicitStemSwapGate && !isStemSwapOperation)
            return;

        if (flowerRoot == null) flowerRoot = transform;
        if (stemRuntime == null) stemRuntime = flowerRoot.GetComponentInChildren<FlowerStemRuntime>();
        if (stemRuntime == null) return;

        // 1) Collect stem piece RBs
        var markers = FindObjectsByType<StemPieceMarker>(FindObjectsSortMode.None);
        var stemPieces = markers
            .Where(m => m != null && m.stemRuntime == stemRuntime)
            .Select(m => m.GetComponent<Rigidbody>())
            .Where(rb => rb != null)
            .Distinct()
            .ToArray();

        if (stemPieces.Length == 0)
            stemPieces = stemRuntime.GetComponentsInChildren<Rigidbody>(true);

        if (stemPieces == null || stemPieces.Length == 0) return;

        var stemSet = new HashSet<Rigidbody>(stemPieces);

        // 2) Decide HELD piece robustly (use crown joints if possible)
        Rigidbody held = ChooseHeldStemPieceByCrownJoints(stemPieces, stemSet);
        if (held == null)
            held = ChooseHeldStemPieceByHighestYThenProximity(stemPieces);

        if (held == null) return;

        var falling = stemPieces.Where(rb => rb != null && rb != held).ToArray();

        if (debugLogs)
        {
            Debug.Log($"[Rebinder] HELD='{held.name}', FALLING=[{string.Join(", ", falling.Select(r => r.name))}]", this);
        }

        // 3) Fix the exact problem you’re seeing:
        //    - Leaf attachment points accidentally binding to FALLING chunk
        //    - Crown joints accidentally binding to FALLING chunk
        //    We REBIND those joints to HELD, we do NOT destroy them.
        if (forceLeafAttachmentsToHeld)
            ForceLeafAttachmentJointsToHeld(held, stemSet);

        if (forceCrownJointsToHeld)
            ForceCrownHeadJointsToHeld(held, stemSet);

        // 4) Optional: if the stem pieces are still connected by some leftover joint inside stemRuntime,
        //    sever only those (safe; doesn’t touch crown).
        if (severStemInternalCrossChunkJoints)
            SeverStemInternalCrossChunkJoints(held, stemSet);

        // 5) Optional: make sure falling chunks actually fall.
        if (forceFallingChunksDynamic && falling.Length > 0)
            ForceChunksDynamicAndAwake(falling);

        // 6) Now do your normal rebinding passes (safe now that anchors are corrected).
        var fixedJoints = CollectJoints<FixedJoint>(flowerRoot, stemRuntime);
        var hingeJoints = CollectJoints<HingeJoint>(flowerRoot, stemRuntime);
        var configurableJoints = CollectJoints<ConfigurableJoint>(flowerRoot, stemRuntime);
        var xyJoints = CollectJoints<XYTetherJoint>(flowerRoot, stemRuntime);

        RebindFixedJoints(fixedJoints, stemPieces, stemSet);
        RebindHingeJoints(hingeJoints, stemPieces, stemSet);
        RebindConfigJoints(configurableJoints, stemPieces, stemSet);
        RebindXYTetherJoints(xyJoints, stemPieces, stemSet);
    }

    // ─────────────────────────────────────────────────────────────
    // HELD SELECTION
    // ─────────────────────────────────────────────────────────────

    private Rigidbody ChooseHeldStemPieceByCrownJoints(Rigidbody[] stemPieces, HashSet<Rigidbody> stemSet)
    {
        Transform crown = ResolveCrownRoot();
        if (crown == null) return null;

        // Look for any Joint under Crown that connects to a stem piece.
        // If Front/Back both connect, we choose the most common connectedBody.
        var joints = crown.GetComponentsInChildren<Joint>(true);

        var votes = new Dictionary<Rigidbody, int>();
        foreach (var j in joints)
        {
            if (j == null) continue;
            var cb = j.connectedBody;
            if (cb == null) continue;
            if (!stemSet.Contains(cb)) continue;

            if (!votes.ContainsKey(cb)) votes[cb] = 0;
            votes[cb]++;
        }

        if (votes.Count == 0) return null;

        var held = votes.OrderByDescending(kv => kv.Value).First().Key;

        if (debugLogs)
            Debug.Log($"[Rebinder] HELD picked by Crown joint votes: '{held.name}'", this);

        return held;
    }

    private Rigidbody ChooseHeldStemPieceByHighestYThenProximity(Rigidbody[] stemPieces)
    {
        Vector3 refPos = (flowerRoot != null) ? flowerRoot.position : transform.position;

        Rigidbody best = null;
        float bestY = float.NegativeInfinity;
        float bestDistSq = float.MaxValue;

        foreach (var rb in stemPieces)
        {
            if (rb == null) continue;
            float y = rb.worldCenterOfMass.y;
            float d = (rb.worldCenterOfMass - refPos).sqrMagnitude;

            // primary: higher Y, secondary: closer to flowerRoot
            bool better = (y > bestY + 1e-5f) || (Mathf.Abs(y - bestY) <= 1e-5f && d < bestDistSq);
            if (better)
            {
                best = rb;
                bestY = y;
                bestDistSq = d;
            }
        }

        if (debugLogs && best != null)
            Debug.Log($"[Rebinder] HELD fallback by HighestY/Proximity: '{best.name}'", this);

        return best;
    }

    private Transform ResolveCrownRoot()
    {
        if (crownRoot != null) return crownRoot;

        // Prefer Tag "Crown" if you have it (your screenshot shows Tag Crown on Crown object).
        var tagged = GameObject.FindGameObjectsWithTag("Crown")
            .Select(go => go.transform)
            .FirstOrDefault(t => t != null && t.IsChildOf(flowerRoot));

        if (tagged != null) return tagged;

        // Fallback: search for something on layer "CrownCore"
        int crownLayer = LayerMask.NameToLayer("CrownCore");
        if (crownLayer >= 0)
        {
            var all = flowerRoot.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t != null && t.gameObject.layer == crownLayer && t.name == "Crown")
                    return t;
            }
        }

        // Final fallback: name contains Crown
        var byName = flowerRoot.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t != null && t.name.ToLower().Contains("crown"));
        return byName;
    }

    // ─────────────────────────────────────────────────────────────
    // TARGETED FIXES (DO NOT DESTROY CROWN JOINTS)
    // ─────────────────────────────────────────────────────────────

    private void ForceLeafAttachmentJointsToHeld(Rigidbody held, HashSet<Rigidbody> stemSet)
    {
        // Leaf attachment points might live outside stem hierarchy, but under flowerRoot.
        var fixedJoints = flowerRoot.GetComponentsInChildren<FixedJoint>(true);

        foreach (var fj in fixedJoints)
        {
            if (fj == null) continue;

            var marker = fj.GetComponent<LeafAttachmentMarker>();
            if (marker == null) continue;

            // Respect permanent detach
            if (marker.owningLeaf != null && marker.owningLeaf.permanentlyDetached)
                continue;

            if (fj.connectedBody != held)
            {
                if (fj.connectedBody != null && stemSet.Contains(fj.connectedBody) && debugLogs)
                    Debug.Log($"[Rebinder] LeafAttachment '{fj.name}' redirected {fj.connectedBody.name} -> {held.name}", fj);

                fj.connectedBody = held;
            }
        }
    }

    private void ForceCrownHeadJointsToHeld(Rigidbody held, HashSet<Rigidbody> stemSet)
    {
        Transform crown = ResolveCrownRoot();
        if (crown == null) return;

        var joints = crown.GetComponentsInChildren<Joint>(true);
        foreach (var j in joints)
        {
            if (j == null) continue;
            if (j.connectedBody == null) continue;

            // Only redirect if the joint is currently connected to a stem piece (wrong one)
            if (stemSet.Contains(j.connectedBody) && j.connectedBody != held)
            {
                if (debugLogs)
                    Debug.Log($"[Rebinder] Crown joint '{j.name}' redirected {j.connectedBody.name} -> {held.name}", j);

                j.connectedBody = held;
            }
        }
    }

    private void SeverStemInternalCrossChunkJoints(Rigidbody held, HashSet<Rigidbody> stemSet)
    {
        // Only joints UNDER the stemRuntime. This avoids touching Crown/Front/Back.
        var joints = stemRuntime.GetComponentsInChildren<Joint>(true);

        int killed = 0;
        foreach (var j in joints)
        {
            if (j == null) continue;
            if (j.connectedBody == null) continue;
            if (!stemSet.Contains(j.connectedBody)) continue;

            // If a joint under stemRuntime is connected to HELD, it might be keeping the falling chunk attached.
            // Kill it so physics separation is guaranteed.
            if (j.connectedBody == held)
            {
                if (debugLogs)
                    Debug.Log($"[Rebinder] Severing stem-internal joint '{j.GetType().Name}' on '{j.name}' (was connected to HELD '{held.name}').", j);

                Destroy(j);
                killed++;
            }
        }

        if (debugLogs && killed > 0)
            Debug.Log($"[Rebinder] Severed {killed} stem-internal cross-chunk joints.", this);
    }

    private void ForceChunksDynamicAndAwake(Rigidbody[] chunks)
    {
        foreach (var rb in chunks)
        {
            if (rb == null) continue;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // NORMAL REBIND PASSES
    // ─────────────────────────────────────────────────────────────

    private T[] CollectJoints<T>(Transform rootA, FlowerStemRuntime stem) where T : Component
    {
        var result = new List<T>();
        if (rootA != null) result.AddRange(rootA.GetComponentsInChildren<T>(true));
        if (stem != null) result.AddRange(stem.GetComponentsInChildren<T>(true));
        return result.Distinct().ToArray();
    }

    private bool IsUnderStem(Transform t)
    {
        if (stemRuntime == null) return false;
        Transform stemRoot = stemRuntime.transform;
        while (t != null)
        {
            if (t == stemRoot) return true;
            t = t.parent;
        }
        return false;
    }

    private void RebindFixedJoints(FixedJoint[] joints, Rigidbody[] stemPieces, HashSet<Rigidbody> stemSet)
    {
        foreach (var fj in joints)
        {
            if (fj == null) continue;

            var ownerRb = fj.GetComponent<Rigidbody>();
            if (ownerRb == null) continue;

            bool onStemHierarchy = IsUnderStem(fj.transform);
            var leafAttachMarker = fj.GetComponent<LeafAttachmentMarker>();
            bool isLeafAttachment = leafAttachMarker != null;

            if (isLeafAttachment && onStemHierarchy)
            {
                if (leafAttachMarker.owningLeaf != null && leafAttachMarker.owningLeaf.permanentlyDetached)
                    continue;

                Vector3 anchorWorld = fj.transform.TransformPoint(fj.anchor);
                var newBody = FindClosestStemPiece(anchorWorld, stemPieces, ownerRb);
                if (newBody != null && newBody != ownerRb)
                    fj.connectedBody = newBody;

                continue;
            }

            if (fj.connectedBody == null) continue;
            if (!stemSet.Contains(fj.connectedBody)) continue;

            Vector3 anchorWorldNormal = fj.transform.TransformPoint(fj.anchor);
            var newBodyNormal = FindClosestStemPiece(anchorWorldNormal, stemPieces, ownerRb);
            if (newBodyNormal == null || newBodyNormal == ownerRb) continue;

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

            if (!stemSet.Contains(hj.connectedBody)) continue;

            Vector3 anchorWorld = hj.transform.TransformPoint(hj.anchor);
            var newBody = FindClosestStemPiece(anchorWorld, stemPieces, ownerRb);
            if (newBody == null || newBody == ownerRb) continue;

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

            if (!stemSet.Contains(cj.connectedBody)) continue;

            Vector3 anchorWorld = cj.transform.TransformPoint(cj.anchor);
            var newBody = FindClosestStemPiece(anchorWorld, stemPieces, ownerRb);
            if (newBody == null || newBody == ownerRb) continue;

            cj.connectedBody = newBody;
        }
    }

    private void RebindXYTetherJoints(XYTetherJoint[] joints, Rigidbody[] stemPieces, HashSet<Rigidbody> stemSet)
    {
        foreach (var xy in joints)
        {
            if (xy == null) continue;

            if (!xy.HasActiveJoint())
                continue;

            var part = xy.GetComponent<FlowerPartRuntime>();
            if (part != null)
            {
                if (part.permanentlyDetached) continue;
                if (!part.isAttached) continue;
            }

            var ownerRb = xy.GetComponent<Rigidbody>();
            if (ownerRb == null) continue;

            Vector3 refPos = xy.transform.position;
            var newBody = FindClosestStemPiece(refPos, stemPieces, ownerRb);

            if (newBody != null && newBody != ownerRb && xy.connectedBody != newBody)
                xy.SetConnectedBody(newBody);
        }
    }

    private Rigidbody FindClosestStemPiece(Vector3 worldPos, Rigidbody[] pieces, Rigidbody exclude = null)
    {
        Rigidbody best = null;
        float bestDistSq = float.MaxValue;

        foreach (var rb in pieces)
        {
            if (rb == null) continue;
            if (rb == exclude) continue;

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
