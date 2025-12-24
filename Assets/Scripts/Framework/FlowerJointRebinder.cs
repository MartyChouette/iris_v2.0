/// <summary>
/// Rebinds joints under a flower after a stem cut.
/// Key rules:
/// 1) Pick HELD stem piece using Crown joints when possible (most reliable).
/// 2) NEVER destroy joints on Crown/head parts. If they point to the wrong chunk, rebind to HELD.
/// 3) LeafAttachmentMarker joints should ALWAYS bind to HELD (unless their owning leaf is permanently detached).
/// 4) Optionally, sever stem-internal joints that still connect held<->falling chunks (rare, but fixes "won't drop").
/// </summary>
/**
 * @class FlowerJointRebinder
 * @brief FlowerJointRebinder component.
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
 *
 * @section viz_flowerjointrebinder Visual Relationships
 * @dot
 * digraph FlowerJointRebinder {
 *   rankdir=LR;
 *   node [shape=box];
 *   FlowerJointRebinder -> FlowerStemRuntime;
 *   FlowerJointRebinder -> StemPieceMarker;
 *   FlowerJointRebinder -> LeafAttachmentMarker;
 *   FlowerJointRebinder -> XYTetherJoint;
 * }
 * @enddot
 */

using System.Linq;
using UnityEngine;
using System.Collections.Generic;

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

    [Header("Anchor Hold (Optional)")]
    [Tooltip("If true, uses SoftStemAnchor to give the held chunk a gentle sway instead of a rigid lock.")]
    public bool useSoftStemAnchor = false;

    [Tooltip("Optional reference to the SoftStemAnchor. Auto-resolves on this flower root if left null.")]
    public SoftStemAnchor softAnchor;

    [Tooltip("If true, creates/uses a kinematic anchor Rigidbody at anchorPoint and tethers the HELD chunk to it.")]
    public bool enableAnchorHold = true;

    [Tooltip("Anchor point (usually Crown). If null, we resolve CrownRoot; else fallback to flowerRoot.")]
    public Transform anchorPoint;

    [Tooltip("Kinematic Rigidbody at the anchor point. If null, we create one under this object.")]
    public Rigidbody anchorBody;

    [Tooltip("Seconds to disable gravity on HELD right after a cut to avoid 'instant drop' on the cut frame.")]
    public float cutHoldSeconds = 0.12f;

    [Header("Anchor Joint Settings")]
    [Tooltip("Locks linear motion of HELD to anchor (recommended).")]
    public bool anchorLockLinear = true;

    [Tooltip("Use Limited angular motion (recommended) vs Locked angular motion (stiffer).")]
    public bool anchorAngularLimited = true;

    [Tooltip("Angular limit (degrees) when anchorAngularLimited is true.")]
    public float anchorAngularLimitDegrees = 15f;

    [Tooltip("Enable joint projection to reduce separation/explosions.")]
    public bool anchorUseProjection = true;

    [Tooltip("Projection distance for the anchor joint.")]
    public float anchorProjectionDistance = 0.02f;

    [Tooltip("Projection angle for the anchor joint.")]
    public float anchorProjectionAngle = 5f;

    [Header("Anchor Stability (Extra)")]
    [Tooltip("If true, anchorBody is kept snapped to anchorPoint every FixedUpdate (recommended if anything else might move it).")]
    public bool keepAnchorBodySnapped = true;

    [Tooltip("Increase solver iterations on HELD to reduce post-cut sag. (0 = no change)")]
    public int minHeldSolverIterations = 20;

    [Tooltip("Increase solver velocity iterations on HELD to reduce post-cut sag. (0 = no change)")]
    public int minHeldSolverVelocityIterations = 20;

    [Tooltip("Joint mass scaling: makes the kinematic anchor feel 'infinitely heavy'.")]
    public float anchorConnectedMassScale = 100f;

    [Header("Debug")]
    public bool debugLogs = true;
    private Rigidbody _lastHeld;

    // We keep track of the joint we own so we never hijack unrelated joints.
    private ConfigurableJoint _anchorHoldJoint;
    private Rigidbody _anchorHeldBody;

    // ─────────────────────────────────────────────────────────────
    // YELLOW LOG HELPERS
    // ─────────────────────────────────────────────────────────────
    private const string LOG_COLOR = "yellow";

    private void LogYellow(string msg, Object ctx = null)
    {
        if (!debugLogs) return;
        if (ctx != null) Debug.Log($"<color={LOG_COLOR}>{msg}</color>", ctx);
        else Debug.Log($"<color={LOG_COLOR}>{msg}</color>", this);
    }

    private void LogYellowWarning(string msg, Object ctx = null)
    {
        if (!debugLogs) return;
        if (ctx != null) Debug.LogWarning($"<color={LOG_COLOR}>{msg}</color>", ctx);
        else Debug.LogWarning($"<color={LOG_COLOR}>{msg}</color>", this);
    }

    private void Awake()
    {
        if (flowerRoot == null) flowerRoot = transform;
        if (stemRuntime == null) stemRuntime = flowerRoot.GetComponentInChildren<FlowerStemRuntime>();

        if (enableAnchorHold)
            EnsureAnchorBody();
    }

    private void FixedUpdate()
    {
        if (!enableAnchorHold) return;
        if (!keepAnchorBodySnapped) return;
        if (anchorBody == null) return;

        // CRITICAL: Use StemTip as anchor point if available (it tracks the cut location)
        Transform targetAnchor = anchorPoint;
        if (stemRuntime != null && stemRuntime.StemTip != null)
        {
            targetAnchor = stemRuntime.StemTip;
        }
        
        if (targetAnchor == null) return;

        // Keep the anchor truly stable in world-space at the NEW cut location.
        anchorBody.isKinematic = true;
        anchorBody.useGravity = false;
        anchorBody.transform.position = targetAnchor.position;
        anchorBody.transform.rotation = targetAnchor.rotation;
    }

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
        // CRITICAL: Only get NEW cut pieces, NOT the original stem
        var markers = FindObjectsByType<StemPieceMarker>(FindObjectsSortMode.None);
        var stemPieces = markers
            .Where(m => m != null && m.stemRuntime == stemRuntime)
            .Select(m => m.GetComponent<Rigidbody>())
            .Where(rb => rb != null)
            .Distinct()
            .ToArray();

        // CRITICAL FIX: Don't fallback to GetComponentsInChildren - that includes the ORIGINAL stem!
        // Only use pieces that have StemPieceMarker (these are the NEW cut pieces)
        if (stemPieces.Length == 0)
        {
            LogYellowWarning("[Rebinder] No stem pieces found with StemPieceMarker! Cut pieces may not have been marked correctly.");
            // Don't fallback - return early instead of including original stem
            return;
        }
        
        // CRITICAL: Exclude the original stem's Rigidbody if it somehow got in the list
        var originalStemRb = stemRuntime.GetComponent<Rigidbody>();
        if (originalStemRb != null)
        {
            stemPieces = stemPieces.Where(rb => rb != originalStemRb).ToArray();
            LogYellow($"[Rebinder] Excluded original stem Rigidbody '{originalStemRb.name}' from cut pieces list");
        }
        
        // DEBUG: Log what pieces we found
        LogYellow($"[Rebinder] Found {stemPieces.Length} cut stem pieces: [{string.Join(", ", stemPieces.Select(r => r.name))}]");

        if (stemPieces == null || stemPieces.Length == 0) return;

        var stemSet = new HashSet<Rigidbody>(stemPieces);

        // 2) Decide HELD piece robustly
        // CRITICAL: First check for parented pieces (set by AnchorTopStemPiece)
        // These are the pieces that should be kept - they're already parented to stemRuntime
        Rigidbody held = null;
        
        // Priority 1: Find parented pieces (these are already marked as kept by AnchorTopStemPiece)
        // CRITICAL: Make sure we're not selecting the original stem (it might also be parented)
        foreach (var rb in stemPieces)
        {
            if (rb == null) continue;
            
            // Skip if this is the original stem (check if it has StemPieceMarker - cut pieces have this)
            var marker = rb.GetComponent<StemPieceMarker>();
            if (marker == null)
            {
                // This doesn't have a marker - it's probably the original stem, skip it
                LogYellow($"[Rebinder] Skipping '{rb.name}' - no StemPieceMarker (likely original stem)", rb);
                continue;
            }
            
            if (stemRuntime != null && rb.transform.IsChildOf(stemRuntime.transform))
            {
                held = rb;
                LogYellow($"[Rebinder] Found parented CUT piece as HELD: '{held.name}'", held);
                break;
            }
        }

        // Priority 2: If no parented piece found, use anchor point logic
        if (held == null && enableAnchorHold)
        {
            EnsureAnchorBody(); // ensures anchorPoint fallback is resolved too
            if (anchorPoint != null)
                held = ChooseHeldStemPieceByAnchorPoint(stemPieces, anchorPoint.position);
        }

        // Priority 3: Fallback to crown joints
        if (held == null)
            held = ChooseHeldStemPieceByCrownJoints(stemPieces, stemSet);

        // Priority 4: Final fallback
        if (held == null)
            held = ChooseHeldStemPieceByHighestYThenProximity(stemPieces);

        if (held == null) return;

        var falling = stemPieces.Where(rb => rb != null && rb != held).ToArray();

        LogYellow($"[Rebinder] HELD='{held.name}', FALLING=[{string.Join(", ", falling.Select(r => r.name))}]");

        // 2.5) Ensure HELD piece doesn't fall
        // Priority: Use SoftStemAnchor if available (best solution), otherwise use kinematic parenting
        bool isParentedToStem = (stemRuntime != null && held.transform.IsChildOf(stemRuntime.transform));
        
        // CRITICAL: Get the NEW cut location from StemTip (this is where the anchor should be)
        Vector3 newCutLocation = stemRuntime != null && stemRuntime.StemTip != null 
            ? stemRuntime.StemTip.position 
            : (anchorPoint != null ? anchorPoint.position : held.worldCenterOfMass);
        
        // Try SoftStemAnchor first (if enabled)
        if (useSoftStemAnchor)
        {
            if (softAnchor == null)
                softAnchor = flowerRoot != null 
                    ? flowerRoot.GetComponent<SoftStemAnchor>()
                    : GetComponentInParent<SoftStemAnchor>();
            
            if (softAnchor != null)
            {
                // SoftStemAnchor expects DYNAMIC body with gravity OFF + joint
                // This allows natural sway while preventing fall
                held.isKinematic = false; // MUST be dynamic for joints to work
                held.useGravity = false; // Gravity off, joint holds it
                
                // CRITICAL: Use the NEW cut location (StemTip) as the anchor point
                // This ensures the anchor moves to where the cut happened
                softAnchor.AnchorHeldStem(held, newCutLocation);
                LogYellow($"[Rebinder] HELD '{held.name}' using SoftStemAnchor at NEW cut location {newCutLocation} (DYNAMIC, gravity OFF, joint holds it)", held);
            }
            else
            {
                LogYellowWarning("[Rebinder] Soft anchor enabled but no SoftStemAnchor found; falling back to kinematic parenting.");
                useSoftStemAnchor = false; // Fall through to kinematic approach
            }
        }
        
        // If not using SoftStemAnchor, use kinematic parenting approach
        if (!useSoftStemAnchor || softAnchor == null)
        {
            // ALWAYS set kinematic and disable gravity for held piece
            held.isKinematic = true;
            held.useGravity = false;
            
            // If parented, it's already set up correctly by AnchorTopStemPiece
            if (isParentedToStem)
            {
                LogYellow($"[Rebinder] HELD '{held.name}' is parented to stem - KINEMATIC (gravity OFF) - already configured", held);
            }
            else if (enableAnchorHold)
            {
                // Not parented - use joint-based anchor hold
                EnsureAnchorBody();
                // CRITICAL: Update anchor point to new cut location
                if (stemRuntime != null && stemRuntime.StemTip != null)
                {
                    anchorPoint = stemRuntime.StemTip;
                    anchorBody.transform.position = newCutLocation;
                }
                ApplyAnchorHoldToHeld(held);
                LogYellow($"[Rebinder] HELD '{held.name}' using anchor hold at NEW cut location {newCutLocation} with KINEMATIC (gravity OFF)", held);
            }
            else
            {
                // Not parented and no anchor hold - parent it to stemRuntime as fallback
                held.transform.SetParent(stemRuntime.transform, true);
                LogYellow($"[Rebinder] HELD '{held.name}' not parented - parenting to stemRuntime and setting KINEMATIC (gravity OFF)", held);
            }
        }
        
        // Store for later use (e.g., Back anchor protection)
        _lastHeld = held;

        // 3) Targeted fixes:
        if (forceLeafAttachmentsToHeld)
            ForceLeafAttachmentJointsToHeld(held, stemSet);

        if (forceCrownJointsToHeld)
            ForceCrownHeadJointsToHeld(held, stemSet);

        // 4) Optional: sever leftover internal cross-chunk joints inside stemRuntime only.
        if (severStemInternalCrossChunkJoints)
            SeverStemInternalCrossChunkJoints(held, stemSet);

        // 5) Optional: make sure falling chunks actually fall.
        if (forceFallingChunksDynamic && falling.Length > 0)
            ForceChunksDynamicAndAwake(falling);

        // 6) Normal rebinding passes.
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
    // ANCHOR HOLD (WORLD ANCHOR + CLOSEST POINT ON HELD)
    // ─────────────────────────────────────────────────────────────

    private void EnsureAnchorBody()
    {
        if (anchorPoint == null)
        {
            // Prefer crown if possible.
            var crown = ResolveCrownRoot();
            anchorPoint = crown != null ? crown : (flowerRoot != null ? flowerRoot : transform);
        }

        if (anchorBody == null)
        {
            // IMPORTANT: WORLD-SPACE. Do NOT parent under the flower.
            var go = new GameObject("StemAnchorBody");
            go.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            go.transform.position = anchorPoint != null ? anchorPoint.position : transform.position;
            go.transform.rotation = anchorPoint != null ? anchorPoint.rotation : transform.rotation;

            anchorBody = go.AddComponent<Rigidbody>();
        }

        // If someone assigned an anchorBody that lives under the flower hierarchy, detach it.
        if (flowerRoot != null && anchorBody != null && anchorBody.transform.IsChildOf(flowerRoot))
        {
            LogYellowWarning($"[Rebinder] AnchorBody '{anchorBody.name}' was under FlowerRoot. Detaching to world-space.", anchorBody);
            anchorBody.transform.SetParent(null, true);
        }

        anchorBody.isKinematic = true;
        anchorBody.useGravity = false;

        if (anchorPoint != null)
        {
            anchorBody.transform.position = anchorPoint.position;
            anchorBody.transform.rotation = anchorPoint.rotation;
        }
    }

    private void ApplyAnchorHoldToHeld(Rigidbody held)
    {
        if (held == null || anchorBody == null) return;

        // Improve solver stability immediately.
        if (minHeldSolverIterations > 0)
            held.solverIterations = Mathf.Max(held.solverIterations, minHeldSolverIterations);
        if (minHeldSolverVelocityIterations > 0)
            held.solverVelocityIterations = Mathf.Max(held.solverVelocityIterations, minHeldSolverVelocityIterations);

        // If HELD changed, destroy the old anchor joint we owned to avoid leaving junk.
        if (_anchorHeldBody != null && _anchorHeldBody != held && _anchorHoldJoint != null)
        {
            try
            {
                if (_anchorHoldJoint != null)
                    Destroy(_anchorHoldJoint);
            }
            catch (System.Exception ex)
            {
                LogYellowWarning($"[Rebinder] Failed to destroy old anchor joint: {ex.Message}");
            }
            _anchorHoldJoint = null;
        }

        _anchorHeldBody = held;

        // Create or reuse OUR joint on held.
        _anchorHoldJoint = FindOrCreateOwnedAnchorJoint(held);

        // CRITICAL: Resolve current world target - prefer StemTip (new cut location) over anchorPoint
        Vector3 worldTarget;
        if (stemRuntime != null && stemRuntime.StemTip != null)
        {
            worldTarget = stemRuntime.StemTip.position;
            anchorBody.transform.rotation = stemRuntime.StemTip.rotation;
        }
        else if (anchorPoint != null)
        {
            worldTarget = anchorPoint.position;
            anchorBody.transform.rotation = anchorPoint.rotation;
        }
        else
        {
            worldTarget = held.worldCenterOfMass;
            anchorBody.transform.rotation = Quaternion.identity;
        }

        // Snap anchorBody to world target (the new cut location).
        anchorBody.transform.position = worldTarget;

        _anchorHoldJoint.connectedBody = anchorBody;
        _anchorHoldJoint.autoConfigureConnectedAnchor = false;

        // CRITICAL: pin closest point on held to the anchor target.
        Vector3 heldAttachWorld = ClosestPointOnBody(held, worldTarget);

        _anchorHoldJoint.anchor = held.transform.InverseTransformPoint(heldAttachWorld);
        _anchorHoldJoint.connectedAnchor = Vector3.zero;

        if (anchorLockLinear)
        {
            _anchorHoldJoint.xMotion = ConfigurableJointMotion.Locked;
            _anchorHoldJoint.yMotion = ConfigurableJointMotion.Locked;
            _anchorHoldJoint.zMotion = ConfigurableJointMotion.Locked;
        }
        else
        {
            _anchorHoldJoint.xMotion = ConfigurableJointMotion.Free;
            _anchorHoldJoint.yMotion = ConfigurableJointMotion.Free;
            _anchorHoldJoint.zMotion = ConfigurableJointMotion.Free;
        }

        if (anchorAngularLimited)
        {
            _anchorHoldJoint.angularXMotion = ConfigurableJointMotion.Limited;
            _anchorHoldJoint.angularYMotion = ConfigurableJointMotion.Limited;
            _anchorHoldJoint.angularZMotion = ConfigurableJointMotion.Limited;

            var limit = new SoftJointLimit { limit = Mathf.Max(0f, anchorAngularLimitDegrees) };
            _anchorHoldJoint.lowAngularXLimit = new SoftJointLimit { limit = -limit.limit };
            _anchorHoldJoint.highAngularXLimit = new SoftJointLimit { limit = limit.limit };
            _anchorHoldJoint.angularYLimit = limit;
            _anchorHoldJoint.angularZLimit = limit;
        }
        else
        {
            _anchorHoldJoint.angularXMotion = ConfigurableJointMotion.Locked;
            _anchorHoldJoint.angularYMotion = ConfigurableJointMotion.Locked;
            _anchorHoldJoint.angularZMotion = ConfigurableJointMotion.Locked;
        }

        if (anchorUseProjection)
        {
            _anchorHoldJoint.projectionMode = JointProjectionMode.PositionAndRotation;
            _anchorHoldJoint.projectionDistance = anchorProjectionDistance;
            _anchorHoldJoint.projectionAngle = anchorProjectionAngle;
        }
        else
        {
            _anchorHoldJoint.projectionMode = JointProjectionMode.None;
        }

        // Make the anchor feel "infinitely heavy".
        _anchorHoldJoint.massScale = 1f;
        _anchorHoldJoint.connectedMassScale = Mathf.Max(1f, anchorConnectedMassScale);

        _anchorHoldJoint.enableCollision = false;
        _anchorHoldJoint.enablePreprocessing = true;

        LogYellow($"[Rebinder] AnchorHold applied: HELD '{held.name}' attach@{heldAttachWorld} -> target@{worldTarget} via AnchorBody '{anchorBody.name}'");
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

    private ConfigurableJoint FindOrCreateOwnedAnchorJoint(Rigidbody held)
    {
        // Use a marker component to reliably find the joint WE own.
        var marker = held.GetComponent<FlowerAnchorHoldMarker>();
        if (marker == null)
            marker = held.gameObject.AddComponent<FlowerAnchorHoldMarker>();

        if (marker.joint == null)
            marker.joint = held.gameObject.AddComponent<ConfigurableJoint>();

        return marker.joint;
    }

    private System.Collections.IEnumerator HoldRoutine(Rigidbody rb, float seconds)
    {
        if (rb == null || seconds <= 0f) yield break;

        bool oldGrav = rb.useGravity;
        bool wasKinematic = rb.isKinematic;
        rb.useGravity = false;
        rb.WakeUp();

        yield return new WaitForSeconds(seconds);

        // CRITICAL FIX: Only restore gravity if the piece is NOT kinematic
        // Kinematic pieces should never have gravity (they're parented/moved by joints)
        if (rb != null && !rb.isKinematic)
        {
            rb.useGravity = oldGrav;
        }
        // If kinematic, ensure gravity stays off (it should already be off from AnchorTopStemPiece)
        else if (rb != null && rb.isKinematic)
        {
            rb.useGravity = false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // HELD SELECTION
    // ─────────────────────────────────────────────────────────────

    private Rigidbody ChooseHeldStemPieceByAnchorPoint(Rigidbody[] stemPieces, Vector3 anchorWorldPos)
    {
        // Choose the piece whose CENTER OF MASS is closest to the anchor point.
        // IMPORTANT: We use worldCenterOfMass, NOT ClosestPoint on colliders!
        // ClosestPoint was causing bugs where a longer falling piece had a surface point
        // closer to the anchor than the keeper piece's center, selecting the wrong piece.
        Rigidbody best = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < stemPieces.Length; i++)
        {
            var rb = stemPieces[i];
            if (rb == null) continue;

            // Use worldCenterOfMass for reliable piece selection
            float d = (rb.worldCenterOfMass - anchorWorldPos).sqrMagnitude;
            LogYellow($"[Rebinder] Piece '{rb.name}' centerOfMass={rb.worldCenterOfMass}, distance to anchor: {Mathf.Sqrt(d):F3}");
            if (d < bestDistSq)
            {
                bestDistSq = d;
                best = rb;
            }
        }

        if (best != null)
            LogYellow($"[Rebinder] HELD picked by AnchorPoint proximity (using center of mass): '{best.name}'");

        return best;
    }

    private Rigidbody ChooseHeldStemPieceByCrownJoints(Rigidbody[] stemPieces, HashSet<Rigidbody> stemSet)
    {
        Transform crown = ResolveCrownRoot();
        if (crown == null) return null;

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

        LogYellow($"[Rebinder] HELD picked by Crown joint votes: '{held.name}'");

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

            bool better = (y > bestY + 1e-5f) || (Mathf.Abs(y - bestY) <= 1e-5f && d < bestDistSq);
            if (better)
            {
                best = rb;
                bestY = y;
                bestDistSq = d;
            }
        }

        if (best != null)
            LogYellow($"[Rebinder] HELD fallback by HighestY/Proximity: '{best.name}'");

        return best;
    }

    private Transform ResolveCrownRoot()
    {
        if (crownRoot != null) return crownRoot;

        // Prefer Tag "Crown" if you have it.
        Transform tagged = null;
        try
        {
            tagged = GameObject.FindGameObjectsWithTag("Crown")
                .Select(go => go.transform)
                .FirstOrDefault(t => t != null && flowerRoot != null && t.IsChildOf(flowerRoot));
        }
        catch
        {
            // Tag might not exist; ignore.
        }

        if (tagged != null) return tagged;

        // Fallback: search for something on layer "CrownCore"
        int crownLayer = LayerMask.NameToLayer("CrownCore");
        if (crownLayer >= 0 && flowerRoot != null)
        {
            var all = flowerRoot.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t != null && t.gameObject.layer == crownLayer && t.name == "Crown")
                    return t;
            }
        }

        // Final fallback: name contains Crown
        if (flowerRoot != null)
        {
            var byName = flowerRoot.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t != null && t.name.ToLower().Contains("crown"));
            return byName;
        }

        return null;
    }

    // ─────────────────────────────────────────────────────────────
    // TARGETED FIXES (DO NOT DESTROY CROWN JOINTS)
    // ─────────────────────────────────────────────────────────────

    private void ForceLeafAttachmentJointsToHeld(Rigidbody held, HashSet<Rigidbody> stemSet)
    {
        var fixedJoints = flowerRoot.GetComponentsInChildren<FixedJoint>(true);

        foreach (var fj in fixedJoints)
        {
            if (fj == null) continue;

            var marker = fj.GetComponent<LeafAttachmentMarker>();
            if (marker == null) continue;

            if (marker.owningLeaf != null && marker.owningLeaf.permanentlyDetached)
                continue;

            if (fj.connectedBody != held)
            {
                if (fj.connectedBody != null && stemSet.Contains(fj.connectedBody))
                    LogYellow($"[Rebinder] LeafAttachment '{fj.name}' redirected {fj.connectedBody.name} -> {held.name}", fj);

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

            if (stemSet.Contains(j.connectedBody) && j.connectedBody != held)
            {
                LogYellow($"[Rebinder] Crown joint '{j.name}' redirected {j.connectedBody.name} -> {held.name}", j);
                j.connectedBody = held;
            }
        }
    }

    private void SeverStemInternalCrossChunkJoints(Rigidbody held, HashSet<Rigidbody> stemSet)
    {
        if (stemRuntime == null || held == null) return;

        var joints = stemRuntime.GetComponentsInChildren<Joint>(true);

        int killed = 0;
        foreach (var j in joints)
        {
            if (j == null) continue;
            if (j.connectedBody == null) continue;

            // Owner must be a stem chunk RB too, otherwise we're severing random non-stem joints.
            var ownerRb = j.GetComponent<Rigidbody>();
            if (ownerRb == null) continue;

            // Both sides must be stem chunks.
            if (!stemSet.Contains(ownerRb)) continue;
            if (!stemSet.Contains(j.connectedBody)) continue;

            bool ownerIsHeld = ownerRb == held;
            bool connectedIsHeld = j.connectedBody == held;

            // Only sever if it actually bridges HELD <-> FALLING.
            // (If both are held-side or both are falling-side, it's not a cross-chunk bridge.)
            if (ownerIsHeld == connectedIsHeld)
                continue;

            // NEVER sever protected anchors/attachments (Back, Crown, LeafAttachment).
            if (JointTouchesProtectedAnchor(j))
                continue;

            LogYellow($"[Rebinder] Severing TRUE cross-chunk joint '{j.GetType().Name}' on '{j.name}' " +
                      $"(owner='{ownerRb.name}' -> connected='{j.connectedBody.name}', HELD='{held.name}').", j);

            // CRITICAL: Disconnect joint first, then destroy with delay to prevent memory corruption
            if (j != null)
            {
                try
                {
                    // Disconnect the joint first to prevent physics system from accessing it
                    j.connectedBody = null;
                    j.enableCollision = false;
                    
                    // Destroy with delay to avoid corruption during physics updates
                    Destroy(j, 0.1f);
                    killed++;
                }
                catch (System.Exception ex)
                {
                    LogYellowWarning($"[Rebinder] Failed to destroy joint '{j.name}': {ex.Message}", j);
                }
            }
        }

        if (killed > 0)
            LogYellow($"[Rebinder] Severed {killed} stem-internal cross-chunk joints.");
    }

    private bool JointTouchesProtectedAnchor(Joint j)
    {
        if (j == null) return false;

        // Protect leaf attachment points.
        if (j.GetComponent<LeafAttachmentMarker>() != null) return true;
        if (j.connectedBody != null && j.connectedBody.GetComponentInParent<LeafAttachmentMarker>() != null) return true;

        // Protect crown/head area joints.
        var crown = ResolveCrownRoot();
        if (crown != null)
        {
            if (j.transform.IsChildOf(crown)) return true;
            if (j.connectedBody != null && j.connectedBody.transform.IsChildOf(crown)) return true;
        }

        // Protect Back/root anchor explicitly (your log shows 'Back' being severed).
        if (j.name == "Back") return true;
        if (j.transform.name == "Back") return true;
        if (j.connectedBody != null && j.connectedBody.transform.name == "Back") return true;

        // (Optional extra safety) Protect anything that looks like leaf attachment points by name.
        if (j.name.Contains("Leaf_AttachmentPoint")) return true;
        if (j.transform.name.Contains("Leaf_AttachmentPoint")) return true;
        if (j.connectedBody != null && j.connectedBody.transform.name.Contains("Leaf_AttachmentPoint")) return true;

        return false;
    }


    private void ForceChunksDynamicAndAwake(Rigidbody[] chunks)
    {
        // Make falling chunks dynamic with gravity ON so they actually fall
        foreach (var rb in chunks)
        {
            if (rb == null) continue;
            
            // Check if this chunk is actually parented (shouldn't be in falling list)
            bool isParentedToStem = (stemRuntime != null && rb.transform.IsChildOf(stemRuntime.transform));
            if (!isParentedToStem)
            {
                // This is a true falling chunk - make it dynamic
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.WakeUp();
                LogYellow($"[Rebinder] Falling chunk '{rb.name}' set to DYNAMIC (gravity ON)", rb);
            }
            else
            {
                // This chunk is parented but was in falling list - keep it kinematic
                rb.isKinematic = true;
                rb.useGravity = false;
                LogYellowWarning($"[Rebinder] Kept piece '{rb.name}' was in falling chunks list - corrected to KINEMATIC", rb);
            }
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
    private bool IsBackAnchor(Transform t)
    {
        if (t == null) return false;

        // Exact match based on your logs ("Back")
        if (t.name == "Back") return true;

        // If Back is nested under something, you can broaden it:
        // return t.name.Contains("Back");
        return false;
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

            // IMPORTANT: Skip the anchor-hold joint we own.
            var marker = cj.GetComponent<FlowerAnchorHoldMarker>();
            if (marker != null && marker.joint == cj)
                continue;

            // NEW: Protect Back/root anchor joints.
            // If this joint lives on the "Back" object, it should ALWAYS bind to HELD.
            if (IsBackAnchor(cj.transform))
            {
                if (_lastHeld != null && cj.connectedBody != _lastHeld)
                {
                    LogYellow($"[Rebinder] Back/root joint '{cj.name}' forced -> HELD '{_lastHeld.name}'", cj);
                    cj.connectedBody = _lastHeld;
                }
                continue; // do NOT run normal closest-piece logic on Back
            }

            if (cj.connectedBody == null) continue;

            var ownerRb = cj.GetComponent<Rigidbody>();
            if (ownerRb == null) continue;

            if (!stemSet.Contains(cj.connectedBody)) continue;

            Vector3 anchorWorld = cj.transform.TransformPoint(cj.anchor);
            var newBody = FindClosestStemPiece(anchorWorld, stemPieces, ownerRb);
            if (newBody == null || newBody == ownerRb) continue;

            cj.connectedBody = newBody;
        }
        LogYellow($"[Rebinder] After RebindConfigJoints, Back joints connectedBody = " +
                  $"{string.Join(", ", flowerRoot.GetComponentsInChildren<ConfigurableJoint>(true).Where(j => IsBackAnchor(j.transform)).Select(j => j.connectedBody ? j.connectedBody.name : "NULL"))}");

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


/// <summary>
/// Marker component used by FlowerJointRebinder to track the anchor-hold joint it owns.
/// </summary>
public sealed class FlowerAnchorHoldMarker : MonoBehaviour
{
    [HideInInspector] public ConfigurableJoint joint;
}

