using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DynamicMeshCutter
{
    /// <summary>
    /// Plane-based cutter for the ANGLE STAGE.
    /// - Stores plane (point + normal) from this transform
    /// - Lets HUD query the plane
    /// - Supports virtual stem cut (non-destructive, no joint rebinding)
    /// - Falls back to DMC destructive path if virtual cutter is unavailable
    /// - Notifies FlowerStemRuntime + FlowerSessionController
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

        [Header("Virtual Stem Cut")]
        [Tooltip("If true, stem cuts use the non-destructive virtual cut path. The original stem GameObject is preserved, eliminating joint rebinding entirely.")]
        public bool useVirtualStemCut = true;

        [Tooltip("Reference to the VirtualStemCutter component. If null, auto-found on this GameObject or parents.")]
        public VirtualStemCutter virtualStemCutter;

        // PERF: Cache references to avoid expensive FindObjects calls
        private FlowerStemRuntime _cachedStem;
        private FlowerSessionController[] _cachedSessions;
        private float _lastCacheTime = -999f;
        private const float CACHE_REFRESH_INTERVAL = 2f;

        // ───────────────────── Cached plane (for other systems / UI) ─────────────────────

        private Vector3 _lastPlanePoint;
        private Vector3 _lastPlaneNormal;

        /// <summary>World-space point on the last plane used / previewed.</summary>
        public Vector3 LastPlanePoint => _lastPlanePoint;

        /// <summary>World-space normal of the last plane used / previewed.</summary>
        public Vector3 LastPlaneNormal => _lastPlaneNormal;

        // ───────────────────── Unity lifecycle ─────────────────────

        private void Start()
        {
            if (virtualStemCutter == null)
                virtualStemCutter = GetComponent<VirtualStemCutter>();
            if (virtualStemCutter == null)
                virtualStemCutter = GetComponentInParent<VirtualStemCutter>();
        }

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
        private void RefreshCachedReferencesIfNeeded()
        {
            if (Time.time - _lastCacheTime > CACHE_REFRESH_INTERVAL)
            {
                _cachedStem = UnityEngine.Object.FindFirstObjectByType<FlowerStemRuntime>();
                _cachedSessions = UnityEngine.Object.FindObjectsByType<FlowerSessionController>(FindObjectsSortMode.None);
                _lastCacheTime = Time.time;
            }
        }

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

            Vector3 planePoint = _lastPlanePoint;
            Vector3 planeNormal = _lastPlaneNormal;

            stem.ApplyCutFromPlane(planePoint, planeNormal);

            if (debugLogs)
            {
                float angle = stem.GetCurrentCutAngleDeg(Vector3.up);
                float len = stem.CurrentLength;
                Debug.Log($"[AngleStagePlaneBehaviour] PREVIEW angle:{angle:F1}, length:{len:F3}", stem);
            }
        }

        /// <summary>
        /// Stage 2: actually perform the cut on all angleTargets.
        ///
        /// If useVirtualStemCut is enabled, stem targets are routed through
        /// VirtualStemCutter (non-destructive, no joint rebinding needed).
        /// Falls back to DMC destructive path otherwise.
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

            // ── Try virtual stem cut path first ──
            if (useVirtualStemCut && virtualStemCutter != null)
            {
                bool anyVirtualCuts = false;

                foreach (var target in angleTargets)
                {
                    if (target == null) continue;

                    var mf = target.GetComponent<MeshFilter>();
                    var smr = target.GetComponent<SkinnedMeshRenderer>();
                    bool hasMesh = (mf != null && mf.sharedMesh != null) ||
                                   (smr != null && smr.sharedMesh != null);
                    if (!hasMesh) continue;

                    var stemRuntime = target.GetComponentInParent<FlowerStemRuntime>();
                    if (stemRuntime == null) continue;
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
                            if (session == null)
                            {
                                RefreshCachedReferencesIfNeeded();
                                if (_cachedSessions != null && _cachedSessions.Length > 0)
                                    session = _cachedSessions[0];
                            }

                            if (session != null)
                            {
                                session.StartCutGraceWindow();
                                StartCoroutine(DelayedGameOverCheck(session, 0.1f));
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        if (debugLogs)
                            Debug.LogWarning($"[AngleStagePlaneBehaviour] Virtual cut failed for '{target.name}': {e.Message}\n{e.StackTrace}", target);
                    }
                }

                if (anyVirtualCuts)
                {
                    if (debugLogs)
                        Debug.Log("[AngleStagePlaneBehaviour] Virtual stem cut completed successfully.", this);
                    return; // Done - no DMC path needed
                }
            }

            // ── Fallback: DMC destructive path ──
            if (debugLogs)
                Debug.Log("[AngleStagePlaneBehaviour] Using DMC destructive path (virtual cutter unavailable or failed).", this);

            RefreshCachedReferencesIfNeeded();
            var sessions = _cachedSessions ?? System.Array.Empty<FlowerSessionController>();

            foreach (var s in sessions)
                if (s != null) s.suppressDetachEvents = true;

            XYTetherJoint.SetCutBreakSuppressed(true);

            foreach (var session in sessions)
            {
                if (session != null && session.transform != null)
                {
                    JointCutSuppressor.SuppressAllJoints(session.transform.root.gameObject);
                }
            }

            try
            {
                bool anyCut = false;

                foreach (var target in angleTargets)
                {
                    if (target == null) continue;

                    var mf = target.GetComponent<MeshFilter>();
                    var smr = target.GetComponent<SkinnedMeshRenderer>();
                    bool hasMesh = (mf != null && mf.sharedMesh != null) ||
                                   (smr != null && smr.sharedMesh != null);
                    if (!hasMesh) continue;

                    var stemRuntime = target.GetComponentInParent<FlowerStemRuntime>();
                    if (stemRuntime == null) continue;
                    if (target.GetComponent<FlowerPartRuntime>() != null) continue;

                    try
                    {
                        Cut(target, _lastPlanePoint, _lastPlaneNormal, null, OnCreatedFallback);
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
                JointCutSuppressor.RestoreAllJoints();

                foreach (var s in sessions)
                    if (s != null) s.suppressDetachEvents = false;
            }
        }

        // ───────────────────── DMC fallback callback ─────────────────────

        /// <summary>
        /// Fallback DMC callback - only used when virtual cutter is unavailable.
        /// Handles physics setup and rebinding for the destructive cut path.
        /// </summary>
        private void OnCreatedFallback(Info info, MeshCreationData cData)
        {
            if (cData == null) return;

            MeshCreation.TranslateCreatedObjects(info,
                                                 cData.CreatedObjects,
                                                 cData.CreatedTargets,
                                                 Separation);

            var stemRuntime = info.MeshTarget.GetComponentInParent<FlowerStemRuntime>();

            for (int i = 0; i < cData.CreatedTargets.Length; i++)
            {
                var createdTarget = cData.CreatedTargets[i];
                if (createdTarget == null) continue;

                GameObject physicsRoot = (i < cData.CreatedObjects.Length) ? cData.CreatedObjects[i] : null;
                if (physicsRoot == null) continue;

                if (stemRuntime != null)
                {
                    var marker = physicsRoot.AddComponent<StemPieceMarker>();
                    marker.stemRuntime = stemRuntime;

                    Rigidbody rb = physicsRoot.GetComponent<Rigidbody>();
                    if (rb == null) continue;

                    bool isKeptStemPiece = (stemRuntime != null && physicsRoot.transform.IsChildOf(stemRuntime.transform));

                    if (isKeptStemPiece)
                    {
                        rb.isKinematic = true;
                        rb.useGravity = false;
                        rb.constraints = RigidbodyConstraints.None;
                    }
                    else
                    {
                        rb.isKinematic = false;
                        rb.useGravity = true;
                        var despawner = physicsRoot.GetComponent<OffScreenDespawner>();
                        if (despawner == null)
                            physicsRoot.AddComponent<OffScreenDespawner>();
                    }
                }
            }

            // Inform stem & session
            var stem = info.MeshTarget != null
                ? info.MeshTarget.GetComponentInParent<FlowerStemRuntime>()
                : null;

            if (stem == null) stem = previewStemOverride;
            if (stem == null)
            {
                RefreshCachedReferencesIfNeeded();
                stem = _cachedStem;
            }

            if (stem != null)
            {
                Vector3 planePoint = info.Plane.WorldPosition;
                Vector3 planeNormal = info.Plane.WorldNormal;

                var session = previewSessionOverride;
                if (session == null)
                    session = stem.GetComponentInParent<FlowerSessionController>();
                if (session == null)
                {
                    RefreshCachedReferencesIfNeeded();
                    if (_cachedSessions != null && _cachedSessions.Length > 0)
                        session = _cachedSessions[0];
                }

                var sap = stem.GetComponentInParent<FlowerSapController>();
                sap?.EmitStemCut(planePoint, planeNormal, stem);

                session?.StartCutGraceWindow();

                stem.ApplyCutFromPlane(planePoint, planeNormal);

                // Fallback: use rebinder (only when virtual cut is not available)
                var rebinder = stem.GetComponentInParent<FlowerJointRebinder>();
                rebinder?.RebindAllPartsToClosestStemPiece();

                if (session != null)
                {
                    session.StartCoroutine(DelayedGameOverCheck(session, 0.1f));
                }
            }
        }

        // ───────────────────── Debug drawing ─────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            CachePlaneFromTransform();

            Vector3 p = _lastPlanePoint;
            Vector3 n = _lastPlaneNormal.normalized;

            Gizmos.color = Color.cyan;
            float halfLen = debugPlaneLength * 0.5f;

            Vector3 arbitrary = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 tangent = Vector3.Cross(n, arbitrary).normalized;

            Vector3 a = p + tangent * halfLen;
            Vector3 b = p - tangent * halfLen;
            Gizmos.DrawLine(a, b);

            float normalLen = halfLen;
            Vector3 end = p + n * normalLen;
            Gizmos.DrawLine(p, end);

            Vector3 side = Vector3.Cross(n, tangent).normalized;
            float headSize = normalLen * 0.2f;
            Vector3 headA = end - n * headSize + side * headSize * 0.5f;
            Vector3 headB = end - n * headSize - side * headSize * 0.5f;
            Gizmos.DrawLine(end, headA);
            Gizmos.DrawLine(end, headB);
        }
#endif

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
