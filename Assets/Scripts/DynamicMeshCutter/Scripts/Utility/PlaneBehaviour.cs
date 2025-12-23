using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DynamicMeshCutter
{
    public class PlaneBehaviour : CutterBehaviour
    {
        [Header("Debug")]
        public float DebugPlaneLength = 2f;
        public bool debugLogs = true;

        [Header("Plane Pose Source")]
        [Tooltip("If set, this transform defines the cut plane point+normal (matches the visible plane). If null, uses this GameObject.")]
        public Transform planePoseOverride;

        [Tooltip("Which local axis of the planePoseOverride is the plane normal.")]
        public NormalAxis normalAxis = NormalAxis.Up;

        public enum NormalAxis { Up, Forward, Right }

        [Header("Angle Preview")]
        [Tooltip("If true, the current plane (position + normal) will be pushed to the flower stem every frame, so the HUD shows cut angle BEFORE you cut.")]
        public bool previewBeforeCut = false;

        [Tooltip("Optional explicit stem to preview against. If null, the first FlowerStemRuntime in the scene is used (cached).")]
        public FlowerStemRuntime previewStemOverride;

        [Tooltip("Optional explicit session to use for instant fail checks. If null, taken from stem's parent (cached).")]
        public FlowerSessionController previewSessionOverride;

        [Header("Preview Logging (Safe)")]
        public bool previewLogs = false;
        public float previewLogInterval = 0.35f;
        public float previewAngleDeltaToLog = 0.5f;
        public float previewLengthDeltaToLog = 0.01f;

        [Header("Cut Targeting")]
        public bool cutAllStemsInScene = false;

        [Header("Cut Filters")]
        public bool useCutLayerMask = true;
        public LayerMask cutLayerMask = ~0; // set to "Stem" in inspector

        [Header("Intersection Precheck")]
        [Tooltip("If true, skip calling DMC Cut() when the plane clearly does not intersect the target bounds.")]
        public bool precheckPlaneIntersectsBounds = true;

        [Tooltip("Extra slack added to bounds radius for the intersection precheck.")]
        public float boundsSlack = 0.01f;

        [Header("Triangle-side Precheck (Recommended)")]
        [Tooltip("If true, checks mesh vertices to confirm some are on both sides of the plane before slicing.")]
        public bool precheckVerticesCrossPlane = true;

        [Tooltip("Small push along normal to avoid coplanar/degenerate cases.")]
        public float planeNudge = 0.0005f;

        // Cache these so we can debug plane pose
        private Vector3 _lastPlanePoint;
        private Vector3 _lastPlaneNormal;

        // Cached preview targets
        private FlowerStemRuntime _cachedStem;
        private FlowerSessionController _cachedSession;

        // Preview log throttling
        private float _nextPreviewLogTime = 0f;
        private float _lastLoggedAngle = float.NaN;
        private float _lastLoggedLen = float.NaN;

        // Lock release coroutine handle
        private Coroutine _releaseRoutine;

        // Per-cut gating
        private readonly HashSet<FlowerStemRuntime> _rebinderRanFor = new HashSet<FlowerStemRuntime>();

        // Per-cut diagnostics
        private bool _anyOnCreatedThisCut = false;
        private int _lastAttemptedCuts = 0;

        private void OnEnable()
        {
            RefreshPreviewCache(force: true);
        }

        private void Update()
        {
            if (previewBeforeCut)
            {
                GetPlanePose(out _lastPlanePoint, out _lastPlaneNormal);
                PreviewAgainstFlower();
            }

            base.Update();
        }

        private void OnDrawGizmosSelected()
        {
            Transform p = planePoseOverride != null ? planePoseOverride : transform;

            Vector3 center = p.position;
            Vector3 n = GetAxisNormal(p).normalized;

            // draw a line in the plane
            Vector3 right = Vector3.Cross(n, Vector3.up);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.Cross(n, Vector3.right);
            right.Normalize();

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(center - right * DebugPlaneLength, center + right * DebugPlaneLength);
            Gizmos.DrawRay(center, n * 0.5f);
        }

        // ───────────────────── Public API ─────────────────────

        public void Cut()
        {
            _rebinderRanFor.Clear();
            _anyOnCreatedThisCut = false;
            _lastAttemptedCuts = 0;

            GetPlanePose(out _lastPlanePoint, out _lastPlaneNormal);

            if (debugLogs)
                Debug.Log($"[PlaneBehaviour] Cutting with plane point:{_lastPlanePoint}, normal:{_lastPlaneNormal}", this);

            List<FlowerStemRuntime> stemsToCut = ResolveStemsToCut();
            if (stemsToCut.Count == 0)
            {
                if (debugLogs)
                    Debug.LogWarning("[PlaneBehaviour] Cut() called but no FlowerStemRuntime could be resolved.", this);
                return;
            }

            List<FlowerSessionController> sessionsToLock = ResolveSessionsToLock(stemsToCut);

            XYTetherJoint.SetCutBreakSuppressed(true);
            for (int i = 0; i < sessionsToLock.Count; i++)
            {
                if (sessionsToLock[i] != null)
                    sessionsToLock[i].suppressDetachEvents = true;
            }

            int scannedTargets = 0;
            int skippedNoMesh = 0;
            int skippedNotReadable = 0;
            int skippedPartRuntime = 0;
            int skippedLayerMask = 0;
            int skippedNoBounds = 0;
            int skippedNoIntersect = 0;
            int skippedNoCross = 0;
            int attemptedCuts = 0;

            Plane plane = new Plane(_lastPlaneNormal, _lastPlanePoint);

            try
            {
                for (int s = 0; s < stemsToCut.Count; s++)
                {
                    FlowerStemRuntime stem = stemsToCut[s];
                    if (stem == null) continue;

                    var targets = stem.GetComponentsInChildren<MeshTarget>(true);
                    for (int t = 0; t < targets.Length; t++)
                    {
                        var target = targets[t];
                        if (target == null) continue;

                        scannedTargets++;

                        if (useCutLayerMask)
                        {
                            int layerBit = 1 << target.gameObject.layer;
                            if ((cutLayerMask.value & layerBit) == 0)
                            {
                                skippedLayerMask++;
                                continue;
                            }
                        }

                        // Must actually have a mesh
                        Mesh mesh = null;
                        Transform meshXform = target.transform;

                        var mf = target.GetComponent<MeshFilter>();
                        var smr = target.GetComponent<SkinnedMeshRenderer>();

                        if (mf != null && mf.sharedMesh != null)
                            mesh = mf.sharedMesh;
                        else if (smr != null && smr.sharedMesh != null)
                            mesh = smr.sharedMesh;

                        if (mesh == null)
                        {
                            skippedNoMesh++;
                            continue;
                        }

                        if (!mesh.isReadable)
                        {
                            skippedNotReadable++;
                            continue;
                        }

                        // Skip leaf/petal/crown MeshTargets only if they are part objects inside the stem subtree.
                        var partInParents = target.GetComponentInParent<FlowerPartRuntime>();
                        if (partInParents != null)
                        {
                            bool partIsInsideStemSubtree =
                                partInParents.transform == stem.transform ||
                                partInParents.transform.IsChildOf(stem.transform);

                            if (partIsInsideStemSubtree && partInParents.transform != stem.transform)
                            {
                                skippedPartRuntime++;
                                continue;
                            }
                        }

                        // Bounds precheck (correct plane-vs-AABB)
                        Renderer r = target.GetComponent<Renderer>();
                        if (precheckPlaneIntersectsBounds)
                        {
                            if (r == null)
                            {
                                skippedNoBounds++;
                                continue;
                            }

                            Bounds b = r.bounds;
                            float dist = plane.GetDistanceToPoint(b.center);
                            float radius = AabbRadiusAlongNormal(b.extents, _lastPlaneNormal) + Mathf.Max(0f, boundsSlack);

                            if (debugLogs)
                            {
                                Debug.Log($"[PlaneBehaviour] DIAG target='{target.name}' mesh='{mesh.name}' readable=True verts={mesh.vertexCount} " +
                                          $"boundsCenter={b.center} distToPlane={dist:F3} radius={radius:F3} layer='{LayerMask.LayerToName(target.gameObject.layer)}'",
                                          target);
                            }

                            if (Mathf.Abs(dist) > radius)
                            {
                                skippedNoIntersect++;
                                continue;
                            }
                        }

                        // Vertex-side precheck (definitive)
                        if (precheckVerticesCrossPlane)
                        {
                            if (!MeshVerticesCrossPlane(mesh, meshXform, plane, out float minD, out float maxD))
                            {
                                skippedNoCross++;
                                if (debugLogs)
                                    Debug.Log($"[PlaneBehaviour] Skipped (no cross): '{target.name}' minD={minD:F4} maxD={maxD:F4}", target);
                                continue;
                            }
                            else if (debugLogs)
                            {
                                Debug.Log($"[PlaneBehaviour] Cross OK: '{target.name}' minD={minD:F4} maxD={maxD:F4}", target);
                            }
                        }

                        attemptedCuts++;
                        _lastAttemptedCuts = attemptedCuts;

                        if (debugLogs)
                            Debug.Log($"[PlaneBehaviour] AttemptCut target='{target.name}' stem='{stem.name}' layer='{LayerMask.LayerToName(target.gameObject.layer)}'", target);

                        try
                        {
                            Vector3 nudgedPoint = _lastPlanePoint + _lastPlaneNormal * planeNudge;
                            Cut(target, nudgedPoint, _lastPlaneNormal, null, OnCreated);
                        }
                        catch (System.Exception e)
                        {
                            if (debugLogs)
                                Debug.LogWarning($"[PlaneBehaviour] Skipped cutting '{target.name}' due to error: {e.Message}", target);
                        }
                    }
                }
            }
            catch
            {
                XYTetherJoint.SetCutBreakSuppressed(false);
                for (int i = 0; i < sessionsToLock.Count; i++)
                    if (sessionsToLock[i] != null) sessionsToLock[i].suppressDetachEvents = false;
                throw;
            }

            if (debugLogs)
            {
                Debug.Log(
                    $"[PlaneBehaviour] Cut summary: stems={stemsToCut.Count}, scannedTargets={scannedTargets}, attemptedCuts={attemptedCuts}, " +
                    $"skippedLayerMask={skippedLayerMask}, skippedNoMesh={skippedNoMesh}, skippedNotReadable={skippedNotReadable}, " +
                    $"skippedPartRuntime={skippedPartRuntime}, skippedNoBounds={skippedNoBounds}, skippedNoIntersect={skippedNoIntersect}, skippedNoCross={skippedNoCross}",
                    this
                );

                if (attemptedCuts == 0)
                    Debug.LogWarning("[PlaneBehaviour] No cuts attempted. Filters excluded everything (see summary above).", this);
            }

            if (_releaseRoutine != null)
                StopCoroutine(_releaseRoutine);

            _releaseRoutine = StartCoroutine(ReleaseLocksAfterDelay(0.30f, sessionsToLock));
        }

        private IEnumerator ReleaseLocksAfterDelay(float delay, List<FlowerSessionController> sessions)
        {
            yield return new WaitForSeconds(delay);

            XYTetherJoint.SetCutBreakSuppressed(false);

            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                if (s == null) continue;

                s.suppressDetachEvents = false;
                s.StartCutGraceWindow();
            }

            if (debugLogs && _lastAttemptedCuts > 0 && !_anyOnCreatedThisCut)
            {
                Debug.LogWarning("[PlaneBehaviour] AttemptedCuts > 0, but OnCreated never fired. " +
                                 "That means DMC found no triangle intersections. If Cross OK logs never appear, your plane pose is wrong (wrong transform).",
                                 this);
            }

            _releaseRoutine = null;
        }

        public void PreviewAgainstFlower()
        {
            RefreshPreviewCache(force: false);

            FlowerStemRuntime stem = previewStemOverride != null ? previewStemOverride : _cachedStem;
            if (stem == null) return;

            GetPlanePose(out Vector3 planePoint, out Vector3 planeNormal);
            stem.ApplyCutFromPlane(planePoint, planeNormal);

            if (previewLogs && debugLogs)
            {
                float now = Time.unscaledTime;
                if (now >= _nextPreviewLogTime)
                {
                    float angle = stem.GetCurrentCutAngleDeg(Vector3.up);
                    float len = stem.CurrentLength;

                    bool angleChanged = float.IsNaN(_lastLoggedAngle) || Mathf.Abs(angle - _lastLoggedAngle) >= previewAngleDeltaToLog;
                    bool lenChanged = float.IsNaN(_lastLoggedLen) || Mathf.Abs(len - _lastLoggedLen) >= previewLengthDeltaToLog;

                    if (angleChanged || lenChanged)
                    {
                        Debug.Log($"[PlaneBehaviour] PREVIEW angle:{angle:F1}°, length:{len:F3}", stem);
                        _lastLoggedAngle = angle;
                        _lastLoggedLen = len;
                    }

                    _nextPreviewLogTime = now + Mathf.Max(0.05f, previewLogInterval);
                }
            }
        }

        // ───────────────────── DMC callback ─────────────────────

        void OnCreated(Info info, MeshCreationData cData)
        {
            _anyOnCreatedThisCut = true;

            if (debugLogs)
                Debug.Log($"[PlaneBehaviour] OnCreated fired for MeshTarget='{info.MeshTarget?.name}', createdTargets={cData?.CreatedTargets?.Length}", this);

            if (cData == null) return;

            MeshCreation.TranslateCreatedObjects(info,
                cData.CreatedObjects,
                cData.CreatedTargets,
                Separation);

            var stemRuntime = info.MeshTarget != null ? info.MeshTarget.GetComponentInParent<FlowerStemRuntime>() : null;

            // Non-flower fallback stays unchanged.
            for (int i = 0; i < cData.CreatedTargets.Length; i++)
            {
                var createdTarget = cData.CreatedTargets[i];
                if (createdTarget == null) continue;

                GameObject pieceRoot =
                    (createdTarget.GameobjectRoot != null)
                        ? createdTarget.GameobjectRoot
                        : createdTarget.gameObject;

                if (pieceRoot == null) continue;

                if (stemRuntime != null)
                    continue;

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

            var stem = stemRuntime;
            if (stem == null) stem = previewStemOverride;
            if (stem == null) stem = _cachedStem;
            if (stem == null) return;

            if (_rebinderRanFor.Contains(stem)) return;
            _rebinderRanFor.Add(stem);

            Vector3 planePoint = info.Plane.WorldPosition;
            Vector3 planeNormal = info.Plane.WorldNormal;

            var session = previewSessionOverride != null ? previewSessionOverride : _cachedSession;
            if (session == null)
                session = stem.GetComponentInParent<FlowerSessionController>();

            var sap = stem.GetComponentInParent<FlowerSapController>();
            if (sap != null)
                sap.EmitStemCut(planePoint, planeNormal, stem);

            stem.ApplyCutFromPlane(planePoint, planeNormal);

            float angle = stem.GetCurrentCutAngleDeg(Vector3.up);
            float len = stem.CurrentLength;
            if (debugLogs)
                Debug.Log($"[PlaneBehaviour] Stem cut angle:{angle:F1}°, length:{len:F3}", stem);

            session?.CheckStemCutImmediate();

            var rebinder = stem.GetComponentInParent<FlowerJointRebinder>();
            rebinder?.RebindAllPartsToClosestStemPiece();
        }

        // ───────────────────── Helpers ─────────────────────

        private void GetPlanePose(out Vector3 point, out Vector3 normal)
        {
            Transform p = planePoseOverride != null ? planePoseOverride : transform;
            point = p.position;
            normal = GetAxisNormal(p).normalized;
        }

        private Vector3 GetAxisNormal(Transform p)
        {
            switch (normalAxis)
            {
                case NormalAxis.Forward: return p.forward;
                case NormalAxis.Right: return p.right;
                default: return p.up;
            }
        }

        private static float AabbRadiusAlongNormal(Vector3 extents, Vector3 n)
        {
            n = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            return n.x * extents.x + n.y * extents.y + n.z * extents.z;
        }

        private static bool MeshVerticesCrossPlane(Mesh mesh, Transform meshTransform, Plane plane, out float minD, out float maxD)
        {
            minD = float.PositiveInfinity;
            maxD = float.NegativeInfinity;

            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 w = meshTransform.TransformPoint(verts[i]);
                float d = plane.GetDistanceToPoint(w);
                if (d < minD) minD = d;
                if (d > maxD) maxD = d;
            }

            // crosses if we have points on both sides (or close enough)
            return minD <= 0f && maxD >= 0f;
        }

        private void RefreshPreviewCache(bool force)
        {
            if (!force)
            {
                if (previewStemOverride != null)
                    _cachedStem = previewStemOverride;

                if (_cachedStem != null && _cachedSession == null)
                    _cachedSession = previewSessionOverride != null
                        ? previewSessionOverride
                        : _cachedStem.GetComponentInParent<FlowerSessionController>();

                return;
            }

            _cachedStem = previewStemOverride != null
                ? previewStemOverride
                : Object.FindFirstObjectByType<FlowerStemRuntime>();

            _cachedSession = previewSessionOverride != null
                ? previewSessionOverride
                : (_cachedStem != null ? _cachedStem.GetComponentInParent<FlowerSessionController>() : null);
        }

        private List<FlowerStemRuntime> ResolveStemsToCut()
        {
            var stems = new List<FlowerStemRuntime>(4);

            if (cutAllStemsInScene)
            {
                var all = Object.FindObjectsByType<FlowerStemRuntime>(FindObjectsSortMode.None);
                if (all != null && all.Length > 0)
                    stems.AddRange(all);
                return stems;
            }

            RefreshPreviewCache(force: false);

            var stem = previewStemOverride != null ? previewStemOverride : _cachedStem;
            if (stem != null)
                stems.Add(stem);

            return stems;
        }

        private List<FlowerSessionController> ResolveSessionsToLock(List<FlowerStemRuntime> stems)
        {
            var sessions = new List<FlowerSessionController>(4);
            var seen = new HashSet<FlowerSessionController>();

            if (previewSessionOverride != null)
            {
                sessions.Add(previewSessionOverride);
                return sessions;
            }

            for (int i = 0; i < stems.Count; i++)
            {
                var stem = stems[i];
                if (stem == null) continue;

                var s = stem.GetComponentInParent<FlowerSessionController>();
                if (s == null) continue;

                if (seen.Add(s))
                    sessions.Add(s);
            }

            if (_cachedSession == null && sessions.Count > 0)
                _cachedSession = sessions[0];

            return sessions;
        }
    }
}
