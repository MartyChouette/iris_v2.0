using System.Collections; // Required for IEnumerator
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

        [Header("Virtual Stem Cut")]
        [Tooltip("If true, stem cuts use the non-destructive virtual cut path. The original stem GameObject is preserved, eliminating joint rebinding entirely.")]
        public bool useVirtualStemCut = true;

        [Tooltip("Reference to the VirtualStemCutter component. If null, auto-found on this GameObject or parents.")]
        public VirtualStemCutter virtualStemCutter;

        // Cache these so we can debug plane pose
        private Vector3 _lastPlanePoint;
        private Vector3 _lastPlaneNormal;

        // PERF: Cache references to avoid expensive FindObjects calls every cut
        private FlowerSessionController[] _cachedSessions;
        private FlowerStemRuntime _cachedStem;
        private float _lastCacheTime = -999f;
        private const float CACHE_REFRESH_INTERVAL = 2f;

        private void Start()
        {
            // Auto-resolve virtual stem cutter if not assigned
            if (virtualStemCutter == null)
                virtualStemCutter = GetComponent<VirtualStemCutter>();
            if (virtualStemCutter == null)
                virtualStemCutter = GetComponentInParent<VirtualStemCutter>();
        }

        private void RefreshCachedReferencesIfNeeded()
        {
            if (Time.time - _lastCacheTime > CACHE_REFRESH_INTERVAL)
            {
                _cachedSessions = UnityEngine.Object.FindObjectsByType<FlowerSessionController>(FindObjectsSortMode.None);
                _cachedStem = UnityEngine.Object.FindFirstObjectByType<FlowerStemRuntime>();
                _lastCacheTime = Time.time;
            }
        }

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
        ///
        /// If useVirtualStemCut is enabled, stem targets are routed through
        /// VirtualStemCutter (non-destructive, no joint rebinding needed).
        /// Non-stem targets still use the standard DMC destructive path.
        /// </summary>
        public void Cut()
        {
            _lastPlanePoint = transform.position;
            _lastPlaneNormal = transform.forward;

            if (debugLogs)
                Debug.Log($"[PlaneBehaviour] Cutting with plane point:{_lastPlanePoint}, normal:{_lastPlaneNormal}", this);

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();

            RefreshCachedReferencesIfNeeded();
            var sessions = _cachedSessions ?? System.Array.Empty<FlowerSessionController>();

            bool anyVirtualCuts = false;
            bool anyDMCCuts = false;

            // ── PHASE 1: Virtual stem cuts (no suppression needed) ──
            if (useVirtualStemCut && virtualStemCutter != null)
            {
                foreach (var root in roots)
                {
                    if (!root.activeInHierarchy) continue;

                    var targets = root.GetComponentsInChildren<MeshTarget>();
                    foreach (var target in targets)
                    {
                        if (target == null) continue;

                        // Must have a mesh
                        var mf = target.GetComponent<MeshFilter>();
                        var smr = target.GetComponent<SkinnedMeshRenderer>();
                        bool hasMesh = (mf != null && mf.sharedMesh != null) ||
                                       (smr != null && smr.sharedMesh != null);
                        if (!hasMesh) continue;

                        // Must belong to stem hierarchy
                        FlowerStemRuntime stemRuntime = target.GetComponentInParent<FlowerStemRuntime>();
                        if (stemRuntime == null) continue;

                        // Must NOT be leaves / petals / crown
                        if (target.GetComponent<FlowerPartRuntime>() != null) continue;

                        try
                        {
                            bool success = virtualStemCutter.PerformVirtualCut(
                                target, stemRuntime,
                                _lastPlanePoint, _lastPlaneNormal,
                                DefaultMaterial);

                            if (success)
                            {
                                anyVirtualCuts = true;

                                // Fire sap effects
                                var sap = stemRuntime.GetComponentInParent<FlowerSapController>();
                                sap?.EmitStemCut(_lastPlanePoint, _lastPlaneNormal, stemRuntime);

                                // Check for game over (stem too short)
                                var session = previewSessionOverride;
                                if (session == null)
                                    session = stemRuntime.GetComponentInParent<FlowerSessionController>();
                                if (session == null && sessions.Length > 0)
                                    session = sessions[0];

                                if (session != null)
                                {
                                    // Brief grace window for any minor physics flutter
                                    session.StartCutGraceWindow();
                                    StartCoroutine(DelayedGameOverCheck(session, 0.1f));
                                }
                            }
                        }
                        catch (System.Exception e)
                        {
                            if (debugLogs)
                                Debug.LogWarning($"[PlaneBehaviour] Virtual cut failed for '{target.name}': {e.Message}\n{e.StackTrace}", target);
                        }
                    }
                }
            }

            // ── PHASE 2: DMC destructive cuts for non-stem targets ──
            // (Also used as fallback if virtual cutter is disabled or missing)
            bool needDMCPath = !useVirtualStemCut || virtualStemCutter == null;

            // Collect non-stem targets (or all targets if virtual cut is off)
            foreach (var root in roots)
            {
                if (!root.activeInHierarchy) continue;

                var targets = root.GetComponentsInChildren<MeshTarget>();
                foreach (var target in targets)
                {
                    if (target == null) continue;

                    var mf = target.GetComponent<MeshFilter>();
                    var smr = target.GetComponent<SkinnedMeshRenderer>();
                    bool hasMesh = (mf != null && mf.sharedMesh != null) ||
                                   (smr != null && smr.sharedMesh != null);
                    if (!hasMesh) continue;

                    FlowerStemRuntime stemRuntime = target.GetComponentInParent<FlowerStemRuntime>();

                    // Skip stem targets if virtual cutter already handled them
                    if (useVirtualStemCut && virtualStemCutter != null && stemRuntime != null)
                    {
                        if (target.GetComponent<FlowerPartRuntime>() == null)
                            continue; // Already handled by virtual cutter above
                    }

                    // Non-stem target: use DMC destructive path
                    if (!anyDMCCuts)
                    {
                        // First DMC cut: set up suppression (only needed for destructive path)
                        XYTetherJoint.SetCutBreakSuppressed(true);
                        JointCutSuppressor.BeginSuppressionCycle();
                        foreach (var r in roots)
                        {
                            if (r.activeInHierarchy)
                                JointCutSuppressor.SuppressAllJoints(r);
                        }
                        JointCutSuppressor.EndSuppressionCycle();
                        foreach (var s in sessions)
                            if (s != null) s.suppressDetachEvents = true;
                    }

                    anyDMCCuts = true;

                    try
                    {
                        Cut(target, _lastPlanePoint, _lastPlaneNormal, null, OnCreatedNonStem);
                    }
                    catch (System.Exception e)
                    {
                        if (debugLogs)
                            Debug.LogWarning($"[PlaneBehaviour] Skipped cutting '{target.name}' due to error: {e.Message}", target);
                    }
                }
            }

            // Release DMC locks if we used the destructive path
            if (anyDMCCuts)
            {
                StartCoroutine(ReleaseLocksAfterDelay(0.3f, sessions));
            }
        }

        // ───────────────────── Lock release (DMC path only) ─────────────────────

        private IEnumerator ReleaseLocksAfterDelay(float delay, FlowerSessionController[] sessions)
        {
            // Use real-time so slow-mo / pause can't prevent the unlock from firing.
            yield return new WaitForSecondsRealtime(delay);

            XYTetherJoint.SetCutBreakSuppressed(false);
            JointCutSuppressor.RestoreAllJoints();

            foreach (var s in sessions)
            {
                if (s != null) s.StartCutGraceWindow();
            }
        }

        private void OnDisable()
        {
            // Safety: if this component is disabled mid-cut (e.g. scissors unequipped),
            // release suppression so joints don't stay permanently unbreakable.
            if (XYTetherJoint.IsCutBreakSuppressed)
            {
                XYTetherJoint.SetCutBreakSuppressed(false);
                JointCutSuppressor.RestoreAllJoints();
            }
        }

        // ───────────────────── Preview ─────────────────────

        /// <summary>
        /// Stage 1: preview – call this manually or just enable previewBeforeCut
        /// so Update() does it every frame.
        /// </summary>
        public void PreviewAgainstFlower()
        {
            FlowerStemRuntime stem = previewStemOverride;
            if (stem == null)
            {
                RefreshCachedReferencesIfNeeded();
                stem = _cachedStem;
            }

            if (stem == null)
                return;

            Vector3 planePoint = transform.position;
            Vector3 planeNormal = transform.forward;

            stem.ApplyCutFromPlane(planePoint, planeNormal);

            if (debugLogs)
            {
                float angle = stem.GetCurrentCutAngleDeg(Vector3.up);
                float len = stem.CurrentLength;
                Debug.Log($"[PlaneBehaviour] PREVIEW angle:{angle:F1}, length:{len:F3}", stem);
            }
        }

        // ───────────────────── DMC callback (non-stem targets only) ─────────────────────

        /// <summary>
        /// Callback for non-stem DMC cuts. Handles physics setup for cut pieces.
        /// Stem-specific logic (rebinding, stem runtime updates) is removed since
        /// stems now use VirtualStemCutter.
        /// </summary>
        void OnCreatedNonStem(Info info, MeshCreationData cData)
        {
            if (cData == null)
                return;

            MeshCreation.TranslateCreatedObjects(info,
                                                 cData.CreatedObjects,
                                                 cData.CreatedTargets,
                                                 Separation);

            for (int i = 0; i < cData.CreatedTargets.Length; i++)
            {
                var createdTarget = cData.CreatedTargets[i];
                if (createdTarget == null) continue;

                GameObject pieceRoot = (createdTarget.GameobjectRoot != null)
                    ? createdTarget.GameobjectRoot
                    : createdTarget.gameObject;
                if (pieceRoot == null) continue;

                var rb = pieceRoot.GetComponent<Rigidbody>() ?? pieceRoot.AddComponent<Rigidbody>();
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                bool isBottom = info.BT != null && i < info.BT.Length && info.BT[i] == 0;
                if (isBottom)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.constraints = RigidbodyConstraints.FreezeAll;
                }
                else
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.constraints = RigidbodyConstraints.None;
                }
            }
        }

        // ───────────────────── Utility ─────────────────────

        /// <summary>
        /// Delays game over check to allow physics to settle after a cut.
        /// </summary>
        private System.Collections.IEnumerator DelayedGameOverCheck(FlowerSessionController session, float delay)
        {
            yield return new WaitForSeconds(delay);
            session?.CheckStemCutImmediate();
        }
    }
}
