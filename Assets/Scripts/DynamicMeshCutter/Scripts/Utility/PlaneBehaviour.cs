using UnityEngine;
using UnityEngine.SceneManagement;

namespace DynamicMeshCutter
{
    public class PlaneBehaviour : CutterBehaviour
    {
        [Header("Debug")]
        public float DebugPlaneLength = 2f;
        public bool debugLogs = true;

        [Header("Angle Preview")]
        [Tooltip("If true, the current plane (position + forward) will be pushed to the flower stem every frame, so the HUD shows cut angle BEFORE you cut.")]
        public bool previewBeforeCut = false;

        [Tooltip("Optional explicit stem to preview against. If null, the first FlowerStemRuntime in the scene is used.")]
        public FlowerStemRuntime previewStemOverride;

        [Tooltip("Optional explicit session to use for instant fail checks. If null, taken from stem's parent.")]
        public FlowerSessionController previewSessionOverride;

        // Cache these so we can debug plane pose
        private Vector3 _lastPlanePoint;
        private Vector3 _lastPlaneNormal;

        // ───────────────────── Unity ─────────────────────

        private void Update()
        {
            // Stage 1: Live preview while you position/rotate the plane.
            if (previewBeforeCut)
            {
                _lastPlanePoint = transform.position;
                _lastPlaneNormal = transform.forward;

                PreviewAgainstFlower();
            }

            // Let CutterBehaviour process async work
            base.Update();
        }

        private void OnDrawGizmosSelected()
        {
            // Simple visual for the plane
            Gizmos.color = Color.cyan;
            Vector3 center = transform.position;
            Vector3 dir = transform.forward;
            Vector3 right = transform.right * DebugPlaneLength;
            Gizmos.DrawLine(center - right, center + right);
            Gizmos.DrawRay(center, dir * 0.5f);
        }

        // ───────────────────── Public API ─────────────────────

        /// <summary>
        /// Stage 2: Call this from UI / input to actually perform the cut
        /// with the current plane pose.
        /// </summary>
        public void Cut()
        {
            _lastPlanePoint = transform.position;
            _lastPlaneNormal = transform.forward;

            if (debugLogs)
                Debug.Log($"[PlaneBehaviour] Cutting with plane point:{_lastPlanePoint}, normal:{_lastPlaneNormal}", this);

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();

            // suppress detach events on all sessions while slicing
            var sessions = UnityEngine.Object.FindObjectsByType<FlowerSessionController>(
                FindObjectsSortMode.None
            );

            foreach (var s in sessions)
                if (s != null) s.suppressDetachEvents = true;

            // ***** ADDED: suppress XYTether force-breaks for the entire cut
            XYTetherJoint.SetCutBreakSuppressed(true);

            try
            {
                foreach (var root in roots)
                {
                    if (!root.activeInHierarchy)
                        continue;

                    var targets = root.GetComponentsInChildren<MeshTarget>();
                    foreach (var target in targets)
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

                        // 1) Must belong to the stem hierarchy
                        FlowerStemRuntime stemRuntime = target.GetComponentInParent<FlowerStemRuntime>();
                        if (stemRuntime == null)
                            continue;

                        // 2) Must NOT be leaves / petals / crown
                        if (target.GetComponent<FlowerPartRuntime>() != null)
                            continue;

                        try
                        {
                            Cut(target, _lastPlanePoint, _lastPlaneNormal, null, OnCreated);
                        }
                        catch (System.Exception e)
                        {
                            if (debugLogs)
                                Debug.LogWarning($"[PlaneBehaviour] Skipped cutting '{target.name}' due to error: {e.Message}", target);
                        }

                    }
                }
            }
            finally
            {
                // ***** ADDED: always restore suppression + detach flags
                XYTetherJoint.SetCutBreakSuppressed(false);

                foreach (var s in sessions)
                    if (s != null) s.suppressDetachEvents = false;
            }
        }

        /// <summary>
        /// Stage 1: preview – call this manually or just enable previewBeforeCut
        /// so Update() does it every frame.
        /// </summary>
        public void PreviewAgainstFlower()
        {
            FlowerStemRuntime stem = previewStemOverride;
            if (stem == null)
                stem = UnityEngine.Object.FindFirstObjectByType<FlowerStemRuntime>();

            if (stem == null)
                return;

            Vector3 planePoint = transform.position;
            Vector3 planeNormal = transform.forward;

            stem.ApplyCutFromPlane(planePoint, planeNormal);

            if (debugLogs)
            {
                float angle = stem.GetCurrentCutAngleDeg(Vector3.up);
                float len = stem.CurrentLength;
                Debug.Log($"[PlaneBehaviour] PREVIEW angle:{angle:F1}°, length:{len:F3}", stem);
            }
        }

        // ───────────────────── DMC callback ─────────────────────
        // This is called once for each MeshTarget that was cut
        void OnCreated(Info info, MeshCreationData cData)
        {
            if (cData == null)
                return;

            // ***** REMOVED: we no longer toggle suppression here
            // XYTetherJoint.SetCutBreakSuppressed(true);

            // Let DMC move/offset the created objects first
            MeshCreation.TranslateCreatedObjects(info,
                                                 cData.CreatedObjects,
                                                 cData.CreatedTargets,
                                                 Separation);

            // Source object (the thing that was cut)
            GameObject sourceGO = info.MeshTarget != null
                ? info.MeshTarget.gameObject
                : null;

            // Stem that owns this mesh

            var stemRuntime = info.MeshTarget.GetComponentInParent<FlowerStemRuntime>();

            for (int i = 0; i < cData.CreatedTargets.Length; i++)
            {
                var createdTarget = cData.CreatedTargets[i];
                if (createdTarget == null)
                    continue;

                GameObject piece = createdTarget.gameObject;

                // CopyComponentsFromSource(...);

                if (stemRuntime != null)
                {
                    var marker = piece.AddComponent<StemPieceMarker>();
                    marker.stemRuntime = stemRuntime;

                    bool isBottom = info.BT != null &&
                                    i < info.BT.Length &&
                                    info.BT[i] == 0;

                    var rb = piece.GetComponent<Rigidbody>() ?? piece.AddComponent<Rigidbody>();

                    rb.interpolation = RigidbodyInterpolation.Interpolate;

                    if (isBottom)
                    {
                        rb.isKinematic = true;
                        rb.useGravity = false;
                        rb.constraints = RigidbodyConstraints.FreezeAll;
                    }
                    else
                    {
                        rb.isKinematic = false;
                        rb.useGravity = true;  // or false if you don't want them to fall
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

                // suppress detach events during cut + rebind
                if (session != null) session.suppressDetachEvents = true;
                try
                {
                    stem.ApplyCutFromPlane(planePoint, planeNormal);

                    float angle = stem.GetCurrentCutAngleDeg(Vector3.up);
                    float len = stem.CurrentLength;
                    if (debugLogs)
                        Debug.Log($"[PlaneBehaviour] Stem cut angle:{angle:F1}°, length:{len:F3}", stem);

                    session?.CheckStemCutImmediate();

                    // rebind leaves/petals to nearest stem chunk
                    var rebinder = stem.GetComponentInParent<FlowerJointRebinder>();
                    rebinder?.RebindAllPartsToClosestStemPiece();
                }
                finally
                {
                    if (session != null)
                    {
                        // ***** REMOVED: suppression is handled at the top-level Cut() now
                        // XYTetherJoint.SetCutBreakSuppressed(false);
                        session.suppressDetachEvents = false;
                    }
                }

            }
        }

        void CopyComponentsFromSource(GameObject source, GameObject piece)
        {
            foreach (var comp in source.GetComponents<Component>())
            {
                // Skip core components already handled by DMC
                if (comp is Transform || comp is MeshFilter || comp is MeshRenderer || comp is MeshTarget)
                    continue;

                // skip gameplay / interaction components on cut pieces.
                if (comp is GrabPull
                    || comp is FlowerPartRuntime
                    || comp is FlowerStemRuntime
                    || comp is XYTetherJoint
                    || comp is InteractionEngagement
                    || comp is FlowerJointRebinder
                    || comp is FlowerGameBrain)
                {
                    continue;
                }

                // SPECIAL CASE: joints
                if (comp is Joint joint)
                {
                    TryHandleJointOnCut(joint, source, piece);
                    continue;
                }

                // Generic component copy – add as needed
                var type = comp.GetType();
                piece.AddComponent(type);
            }
        }

        void TryHandleJointOnCut(Joint original, GameObject source, GameObject piece)
        {
            // only joints that OPT-IN via JointCutPolicy are affected by cutting
            var policy = original.GetComponent<JointCutPolicy>();
            if (policy == null)
                return;  // no policy => leave this joint exactly as-is

            var mode = policy.mode;

            if (mode == JointSplitMode.DestroyOnCut)
            {
                if (Application.isPlaying)
                    Destroy(original);
                else
                    DestroyImmediate(original);
                return;
            }

            var pieceCollider = piece.GetComponent<Collider>();
            if (pieceCollider == null)
                return;

            // Convert anchor to world space
            Vector3 anchorWorld = original.transform.TransformPoint(original.anchor);

            bool thisPieceHasAnchor = pieceCollider.bounds.Contains(anchorWorld);

            // keep only anchor side
            if (mode == JointSplitMode.KeepAnchorSideOnly && !thisPieceHasAnchor)
                return;

            // Custom hook
            if (mode == JointSplitMode.CustomLogic)
            {
                policy?.OnCutCustom?.Invoke(original, source, piece);
                return;
            }

            // Clone the joint onto this piece
            var cloned = piece.AddComponent(original.GetType()) as Joint;
            if (!cloned)
                return;

            cloned.anchor = original.anchor;
            cloned.autoConfigureConnectedAnchor = original.autoConfigureConnectedAnchor;
            cloned.connectedAnchor = original.connectedAnchor;
            cloned.breakForce = original.breakForce;
            cloned.breakTorque = original.breakTorque;
            cloned.enableCollision = original.enableCollision;

            var srcRb = source.GetComponent<Rigidbody>();

            if (original.connectedBody != null && original.connectedBody != srcRb)
                cloned.connectedBody = original.connectedBody;
            else
                cloned.connectedBody = null;

            if (original is HingeJoint oH && cloned is HingeJoint cH)
            {
                cH.axis = oH.axis;
                cH.useLimits = oH.useLimits;
                cH.limits = oH.limits;
                cH.useSpring = oH.useSpring;
                cH.spring = oH.spring;
            }
        }
    }
}
