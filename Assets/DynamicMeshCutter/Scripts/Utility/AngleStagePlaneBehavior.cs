using System.Reflection;
using UnityEngine;

namespace DynamicMeshCutter
{
    /// <summary>
    /// Plane-based cutter that stores the current plane (point + normal) and
    /// optionally previews angle. This sits on your AnglePlane object.
    /// </summary>
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

        // At the top of the class:
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
            }
        }

        // ───────────────────── Public API ─────────────────────

        /// <summary>
        /// Rebuild the cached plane from the current transform.
        /// *** This is where we fix the 90° issue: the plane normal is transform.forward. ***
        /// </summary>
        public void CachePlaneFromTransform()
        {
            _lastPlanePoint = transform.position;
            _lastPlaneNormal = transform.up;   // ← plane normal
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
        /// Signature kept to satisfy existing code.
        /// </summary>
        public bool IsAngleStageArmed()
        {
            return isAngleStageArmed;
        }

        /// <summary>
        /// Perform the actual mesh cut, using the plane defined by this component.
        /// This finds the first MeshTarget on the base CutterBehaviour via reflection
        /// and calls CutterBehaviour.Cut(target, point, normal, null).
        /// </summary>
        // Replace old PerformCut() with this:
        public void PerformCut()
        {
            CachePlaneFromTransform();

            if (angleTargets == null || angleTargets.Length == 0)
            {
                Debug.LogWarning("[AngleStagePlaneBehaviour] PerformCut: No MeshTargets assigned in angleTargets.");
                return;
            }

            bool anyCut = false;

            foreach (var mt in angleTargets)
            {
                if (mt == null) continue;

                // This is the real DynamicMeshCutter API.
                base.Cut(mt, _lastPlanePoint, _lastPlaneNormal, null);
                anyCut = true;
            }

            if (!anyCut)
            {
                Debug.LogWarning("[AngleStagePlaneBehaviour] PerformCut: All entries in angleTargets are null.");
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
