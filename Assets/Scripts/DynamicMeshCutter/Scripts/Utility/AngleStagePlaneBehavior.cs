using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DynamicMeshCutter
{
    /// <summary>
    /// Plane-based cutter for the ANGLE STAGE.
    /// - Stores plane (point + normal) from this transform
    /// - Lets HUD query the plane
    /// - Performs cuts using the real DMC API with OnCreated
    /// - Applies guard rails (XYTether suppression, session suppressDetachEvents)
    /// - Sets up rigidbodies so bottom stem piece is anchored, top pieces fall
    /// - Notifies FlowerStemRuntime + FlowerSessionController + FlowerJointRebinder
    /// - Optionally triggers FlowerSapController on stem cuts
    /// </summary>
    [DisallowMultipleComponent]
    public class AngleStagePlaneBehaviour : CutterBehaviour
    {
        [Header("Debug")]
        [Tooltip("Length of the debug line drawn in the Scene view.")]
        public float debugPlaneLength = 2f;
        public bool debugLogs = true;

        [Header("Angle Preview")]
        [Tooltip("If true, we keep updating the cached plane from the transform so HUD/UI can show the angle before cutting.")]
        public bool previewBeforeCut = true;

        [Tooltip("Optional explicit stem to preview against. If null, the first FlowerStemRuntime in the scene is used.")]
        public FlowerStemRuntime previewStemOverride;

        [Tooltip("Optional explicit session to use for instant fail checks. If null, taken from stem's parent.")]
        public FlowerSessionController previewSessionOverride;

        [Header("Two-Stage Angle Mode")]
        [Tooltip("If enabled, controller will first lock height, then angle, then call PerformCut().")]
        public bool useTwoStageAngleMode = true;

        [Tooltip("If true, we reset stage state when this component is disabled.")]
        public bool autoCancelOnDisable = true;

        [Header("Angle Snapping")]
        [Tooltip("Snap step (in degrees) when the controller asks us to snap the angle.")]
        public float angleSnapStepDeg = 5f;

        [Header("HUD State")]
        [SerializeField]
        [Tooltip("Used by FlowerHUD to know if the angle stage is currently armed.")]
        private bool isAngleStageArmed = false;

        [Header("Targets")]
        [Tooltip("MeshTargets that this angle plane will cut. Drag your stem MeshTarget(s) here.")]
        public MeshTarget[] angleTargets;

        // ───────────────────── Cached plane (for other systems / UI) ─────────────────────

        private Vector3 _lastPlanePoint;
        private Vector3 _lastPlaneNormal;

        /// <summary>World-space point on the last plane used / previewed.</summary>
        public Vector3 LastPlanePoint => _lastPlanePoint;

        /// <summary>World-space normal of the last plane used / previewed.</summary>
        public Vector3 LastPlaneNormal => _lastPlaneNormal;

        // ───────────────────── Unity lifecycle ─────────────────────

        private void OnEnable()
        {
            CachePlaneFromTransform();
        }

        private void OnDisable()
        {
            if (autoCancelOnDisable)
            {
                SetAngleStageArmed(false);
            }
        }

        private void Update()
        {
            if (previewBeforeCut)
            {
                CachePlaneFromTransform();
                PreviewAgainstFlower();
            }

            // Let CutterBehaviour process async work
            base.Update();
        }

        // ───────────────────── Public API ─────────────────────

        /// <summary>
        /// Rebuild the cached plane from the current transform.
        /// Plane normal is transform.up (aligned with your gizmo).
        /// </summary>
        public void CachePlaneFromTransform()
        {
            _lastPlanePoint = transform.position;
            _lastPlaneNormal = transform.up;
        }

        /// <summary>
        /// Called by controllers when the plane pose changes.
        /// </summary>
        public void NotifyPlanePoseChanged()
        {
            CachePlaneFromTransform();
            if (debugLogs)
            {
                Debug.Log($"[AngleStagePlaneBehaviour] Plane updated. Point={_lastPlanePoint}, Normal={_lastPlaneNormal}");
            }
        }

        /// <summary>
        /// Mark the angle stage as armed (used by FlowerHUD).
        /// </summary>
        public void SetAngleStageArmed(bool armed)
        {
            isAngleStageArmed = armed;
        }

        /// <summary>
        /// Used by FlowerHUD to decide what icon/state to show.
        /// </summary>
        public bool IsAngleStageArmed()
        {
            return isAngleStageArmed;
        }

        /// <summary>
        /// Stage 1: preview – same idea as PlaneBehaviour.PreviewAgainstFlower.
        /// </summary>
        public void PreviewAgainstFlower()
        {
            FlowerStemRuntime stem = previewStemOverride;
            if (stem == null)
                stem = UnityEngine.Object.FindFirstObjectByType<FlowerStemRuntime>();

            if (stem == null)
                return;

            Vector3 planePoint = _lastPlanePoint;
            Vector3 planeNormal = _lastPlaneNormal;

            stem.ApplyCutFromPlane(planePoint, planeNormal);

            if (debugLogs)
            {
                float angle = stem.GetCurrentCutAngleDeg(Vector3.up);
                float len = stem.CurrentLength;
                Debug.Log($"[AngleStagePlaneBehaviour] PREVIEW angle:{angle:F1}°, length:{len:F3}", stem);
            }
        }

        /// <summary>
        /// Stage 2: actually perform the cut on all angleTargets.
        /// Uses the real DMC API with OnCreated callback, plus XYTether suppression.
        /// </summary>
        public void PerformCut()
        {
            CachePlaneFromTransform();

            if (angleTargets == null || angleTargets.Length == 0)
            {
                Debug.LogWarning("[AngleStagePlaneBehaviour] PerformCut: No MeshTargets assigned in angleTargets.");
                return;
            }

            if (debugLogs)
                Debug.Log($"[AngleStagePlaneBehaviour] Cutting with plane point:{_lastPlanePoint}, normal:{_lastPlaneNormal}", this);

            // suppress detach events on all sessions while slicing
            var sessions = UnityEngine.Object.FindObjectsByType<FlowerSessionController>(
                FindObjectsSortMode.None
            );

            foreach (var s in sessions)
                if (s != null) s.suppressDetachEvents = true;

            // suppress XYTether force-breaks for the entire cut
            XYTetherJoint.SetCutBreakSuppressed(true);

            try
            {
                bool anyCut = false;

                foreach (var target in angleTargets)
                {
                    if (target == null)
                        continue;

                    // Must actually have a mesh
                    var mf = target.GetComponent<MeshFilter>();
                    var smr = target.GetComponent<SkinnedMeshRenderer>();
                    bool hasMesh =
                        (mf != null && mf.sharedMesh != null) ||
                        (smr != null && smr.sharedMesh != null);

                    if (!hasMesh)
                        continue;

                    // Must belong to a stem hierarchy
                    var stemRuntime = target.GetComponentInParent<FlowerStemRuntime>();
                    if (stemRuntime == null)
                        continue;

                    // Must NOT be leaves / petals / crown (FlowerPartRuntime)
                    if (target.GetComponent<FlowerPartRuntime>() != null)
                        continue;

                    try
                    {
                        Cut(target, _lastPlanePoint, _lastPlaneNormal, null, OnCreated);
                        anyCut = true;
                    }
                    catch (System.Exception e)
                    {
                        if (debugLogs)
                            Debug.LogWarning($"[AngleStagePlaneBehaviour] Skipped cutting '{target.name}' due to error: {e.Message}", target);
                    }
                }

                if (!anyCut && debugLogs)
                {
                    Debug.LogWarning("[AngleStagePlaneBehaviour] PerformCut: angleTargets contained no valid stem MeshTargets.");
                }
            }
            finally
            {
                XYTetherJoint.SetCutBreakSuppressed(false);

                foreach (var s in sessions)
                    if (s != null) s.suppressDetachEvents = false;
            }
        }

        // ───────────────────── DMC callback ─────────────────────
        // This is essentially the same logic as PlaneBehaviour.OnCreated,
        // with a small hook for sap.

        private void OnCreated(Info info, MeshCreationData cData)
        {
            if (cData == null)
                return;

            // Let DMC move/offset the created objects first
            MeshCreation.TranslateCreatedObjects(info,
                                                 cData.CreatedObjects,
                                                 cData.CreatedTargets,
                                                 Separation);

            var stemRuntime = info.MeshTarget.GetComponentInParent<FlowerStemRuntime>();

            // create rigidbodies + marker for each piece
            var pieceBodies = new List<Rigidbody>();

            for (int i = 0; i < cData.CreatedTargets.Length; i++)
            {
                var createdTarget = cData.CreatedTargets[i];
                if (createdTarget == null)
                    continue;

                GameObject piece = createdTarget.gameObject;

                if (stemRuntime != null)
                {
                    var marker = piece.AddComponent<StemPieceMarker>();
                    marker.stemRuntime = stemRuntime;

                    var rb = piece.GetComponent<Rigidbody>() ?? piece.AddComponent<Rigidbody>();
                    rb.interpolation = RigidbodyInterpolation.Interpolate;

                    pieceBodies.Add(rb);

                    // Check if this piece has already been "claimed" by the MeshCreation smart logic
                    // (MeshCreation.AnchorTopStemPiece parents the "Kept" piece to the StemRuntime transform)
                    bool isKeptStemPiece = (stemRuntime != null && piece.transform.parent == stemRuntime.transform);

                    if (isKeptStemPiece)
                    {
                        // This is the FLOWER HEAD (top piece with crown and leaves).
                        // It must stay in hand (Kinematic) and NOT fall.
                        rb.isKinematic = true;
                        rb.useGravity = false;
                        rb.constraints = RigidbodyConstraints.None;
                    }
                    else
                    {
                        // This is STEM WASTE (bottom/falling piece).
                        // It must fall (Gravity) and be dynamic.
                        rb.isKinematic = false;
                        rb.useGravity = true;
                        rb.constraints = RigidbodyConstraints.None;
                    }
                }
            }

            // ─────────────────────────────────────────────
            // Inform the flower stem & session AFTER the cut
            // ─────────────────────────────────────────────

            var stem = info.MeshTarget != null
                ? info.MeshTarget.GetComponentInParent<FlowerStemRuntime>()
                : null;

            if (stem == null)
                stem = previewStemOverride;
            if (stem == null)
                stem = UnityEngine.Object.FindFirstObjectByType<FlowerStemRuntime>();

            if (stem != null)
            {
                Vector3 planePoint = info.Plane.WorldPosition;
                Vector3 planeNormal = info.Plane.WorldNormal;

                var session = previewSessionOverride;
                if (session == null)
                    session = stem.GetComponentInParent<FlowerSessionController>();
                if (session == null)
                    session = UnityEngine.Object.FindFirstObjectByType<FlowerSessionController>();

                // 🔥 SAP HOOK: spray from both ends on angle cuts too
                var sap = stem.GetComponentInParent<FlowerSapController>();
                if (sap != null)
                {
                    sap.EmitStemCut(planePoint, planeNormal, stem);
                }

                // suppress detach events during cut + rebind
                if (session != null) session.suppressDetachEvents = true;
                try
                {
                    stem.ApplyCutFromPlane(planePoint, planeNormal);

                    float angle = stem.GetCurrentCutAngleDeg(Vector3.up);
                    float len = stem.CurrentLength;
                    if (debugLogs)
                        Debug.Log($"[AngleStagePlaneBehaviour] Stem cut angle:{angle:F1}°, length:{len:F3}", stem);

                    session?.CheckStemCutImmediate();

                    // rebind leaves/petals to nearest stem chunk
                    var rebinder = stem.GetComponentInParent<FlowerJointRebinder>();
                    rebinder?.RebindAllPartsToClosestStemPiece();
                }
                finally
                {
                    if (session != null)
                        session.suppressDetachEvents = false;
                }
            }
        }

        // ───────────────────── Debug drawing ─────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Keep plane info in sync even when not playing.
            CachePlaneFromTransform();

            Vector3 p = _lastPlanePoint;
            Vector3 n = _lastPlaneNormal.normalized;

            Gizmos.color = Color.cyan;
            float halfLen = debugPlaneLength * 0.5f;

            // Tangent lying in the plane.
            Vector3 arbitrary = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 tangent = Vector3.Cross(n, arbitrary).normalized;

            Vector3 a = p + tangent * halfLen;
            Vector3 b = p - tangent * halfLen;
            Gizmos.DrawLine(a, b);

            // Normal arrow.
            float normalLen = halfLen;
            Vector3 end = p + n * normalLen;
            Gizmos.DrawLine(p, end);

            // Arrow head.
            Vector3 side = Vector3.Cross(n, tangent).normalized;
            float headSize = normalLen * 0.2f;
            Vector3 headA = end - n * headSize + side * headSize * 0.5f;
            Vector3 headB = end - n * headSize - side * headSize * 0.5f;
            Gizmos.DrawLine(end, headA);
            Gizmos.DrawLine(end, headB);
        }
#endif
    }
}
