/**
 * @file PlaneAngleTiltController.cs
 * @brief Modifies the Angle of Incidence (Tilt) of a cutting plane based on user input.
 *
 * @details
 * ## AI FAST CONTEXT (READ THIS FIRST)
 * **Project:** Iris
 * **This script’s job:** Maps linear Mouse X / Scroll input to a rotational value on a specific local axis.
 * **Non-goals:** * - Does NOT control the "Scissor" action (opening/closing).
 * - Does NOT decide *when* tilting is allowed (controlled by `ScissorStation`).
 * - Does NOT move the scissors up/down (Y-axis translation is `CuttingPlaneController`).
 * **Authority / source of truth:** This script holds the authoritative `_currentAngleDeg`.
 *
 * ## DESIGN INTENT (PLAYER-FACING)
 * - **Variable Vectors:** The tool allows the user to change the **Plane of Motion** (Incidence).
 * - **Persistence:** The angle shouldn't "pop" or reset when the user lets go or re-equips the tool.
 * - **Responsiveness:** Scroll wheel offers "detented" feel, Mouse X offers "analog" feel.
 *
 * ## SYSTEM CONTRACTS (HARD RULES)
 * - `planeTransform` MUST be the parent Pivot of **both** the Visuals and the Logic (`PlaneBehaviour`).
 * - Angles are always clamped to `minAngleDeg` and `maxAngleDeg`.
 * - Must sync from current transform on `OnEnable` to prevent jumps.
 *
 * ## HIERARCHY + TRANSFORM TRUTH (CRITICAL)
 * **Transform of Truth:** `planeTransform` (The Pivot/Shoulder). This drives the physics/logic.
 * **Common Bug:** If visuals tilt but the cut is straight, you are likely rotating the Mesh, not the Logic.
 *
 * **Required Hierarchy:**
 * <ScissorsPivot> (Assigned to `planeTransform`)
 * ├─ <PlaneBehaviour> (The Logic/Cutter)
 * └─ <ScissorsMesh> (The Visuals)
 *
 * **Wiring Rules:**
 * - Do NOT put this script on the mesh if the mesh is a child of the logic. Put it on the Parent/Pivot.
 *
 * ## OWNERSHIP / STATE MODEL
 * **States:** * - `Disabled` (Fixed angle, no input processing).
 * - `Enabled` (Input drives rotation).
 * **Who transitions state:** `ScissorStation` (via `SetEnabled`).
 * **What “enabled” means:** Mouse movements affect rotation.
 *
 * ## SCENE / PREFAB AUTHORING REQUIREMENTS
 * - **Pivot Placement:** The Pivot must be at the center of rotation (the "Shoulder").
 * - **Axis Selection:** Ensure `tiltAxis` matches the mechanical hinge of your specific tool model.
 *
 * ## EVENT / HOOK POINTS
 * - None currently. Future: `OnTiltChanged(float degrees)` for HUD updates.
 * - **Audio:** Planned FMOD migration to trigger "Ratchet" sound on ScrollWheel steps.
 *
 * ## PERFORMANCE + SAFETY NOTES
 * - **Hot paths:** `Update` (Input polling).
 * - **Allocation rules:** Zero alloc in Update.
 * - **Physics:** `Mathf.Clamp` protects against gimbal lock or impossible angles.
 *
 * ## DEBUGGING PLAYBOOK (WHAT TO CHECK FIRST)
 * 1) **Symptom:** "Tool snaps to 0 when I click."
 * - **Check:** `SyncAngleFromTransform` logic; is axis correct?
 * 2) **Symptom:** "Visuals rotate, but Cut is flat."
 * - **Check:** Is `planeTransform` assigned to the Mesh instead of the Pivot?
 * - **Check:** Is `PlaneBehaviour` a sibling of the mesh (correct), or a parent of the pivot (incorrect)?
 *
 * @ingroup tools
 *
 * @section viz_planeangletiltcontroller Visual Relationships
 * @dot
 * digraph PlaneAngleTiltController {
 * rankdir=LR;
 * node [shape=box];
 * "Input (Mouse)" -> "PlaneAngleTiltController" [label="Delta"];
 * "PlaneAngleTiltController" -> "planeTransform (Pivot)" [label="Rotates Local Axis"];
 * "planeTransform (Pivot)" -> "PlaneBehaviour (Logic)" [label="Child 1 (Calculates Cut)"];
 * "planeTransform (Pivot)" -> "ScissorsMesh (Visuals)" [label="Child 2 (Shows Tilt)"];
 * }
 * @enddot
 */

using UnityEngine;

namespace DynamicMeshCutter
{
    [DisallowMultipleComponent]
    public class PlaneAngleTiltController : MonoBehaviour
    {
        public enum TiltAxis { X, Y, Z }

        [Header("Mode")]
        [Tooltip("Kept for compatibility with older staging setups; this component itself always tilts when enabled.")]
        public bool alwaysTiltActive = false;

        [Header("References")]
        [Tooltip("TARGET THE PARENT PIVOT. This transform will be rotated. Both Visuals and Logic should be children of this.")]
        public Transform planeTransform;

        [Tooltip("Optional separate visual transform to mirror planeTransform pose. (Unnecessary if you use the Parent Pivot hierarchy).")]
        public Transform planeVisual;

        [Tooltip("Optional: If your pivot point isn't perfectly centered, offset it here.")]
        public Vector3 pivotOffset = Vector3.zero;

        [Header("Tilt Settings")]
        [Tooltip("Which local axis of planeTransform is rotated by this controller.")]
        public TiltAxis tiltAxis = TiltAxis.Z;

        [Header("Scroll Wheel Tilt (ONLY)")]
        [Tooltip("If true, Mouse ScrollWheel tilts the plane.")]
        public bool useScrollWheel = true;

        [Tooltip("Degrees per scroll wheel notch.")]
        public float stepDegScroll = 20f;

        [Header("Angle Limits")]
        [Tooltip("Minimum allowed angle in degrees (normalized -180..180 space).")]
        public float minAngleDeg = -80f;

        [Tooltip("Maximum allowed angle in degrees (normalized -180..180 space).")]
        public float maxAngleDeg = 80f;

        [Header("Feel")]
        [Tooltip("Higher = faster snap to target angle (helps remove jitter).")]
        public float snapSpeed = 40f;

        [Tooltip("If true, apply rotation in LateUpdate so we win after other scripts that might touch rotation in Update.")]
        public bool applyInLateUpdate = true;

        [Header("Debug")]
        public bool debugLogs = false;

        private float _currentAngleDeg;
        private float _targetAngleDeg;

        private Quaternion _baseLocalRotation;
        private Vector3 _authoredLocalPos;
        private bool _hasCapturedBase;

        /// <summary>
        /// External helper for tool logic: enable/disable this component.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            this.enabled = enabled;

            if (enabled)
                SyncAngleFromTransform();
        }

        private void Reset()
        {
            planeTransform = transform;
        }

        private void Awake()
        {
            if (planeTransform == null)
            {
                planeTransform = transform;
                if (debugLogs) Debug.LogWarning("[PlaneAngleTiltController] planeTransform not assigned. Defaulting to self. Ensure this is the Pivot!", this);
            }

            if (minAngleDeg > maxAngleDeg)
            {
                float tmp = minAngleDeg;
                minAngleDeg = maxAngleDeg;
                maxAngleDeg = tmp;
            }

            CaptureAuthoredBasePoseOnce();

            // Apply Pivot Offset (one-time, non-cumulative)
            planeTransform.localPosition = _authoredLocalPos + pivotOffset;

            SyncAngleFromTransform();
        }

        private void OnEnable()
        {
            if (planeTransform == null)
                planeTransform = transform;

            if (!_hasCapturedBase)
                CaptureAuthoredBasePoseOnce();

            SyncAngleFromTransform();
        }

        private void Update()
        {
            if (planeTransform == null)
                return;

            HandleScrollOnly();

            if (!applyInLateUpdate)
                ApplyTilt();

            SyncVisual();
        }

        private void LateUpdate()
        {
            if (!applyInLateUpdate)
                return;

            if (planeTransform == null)
                return;

            ApplyTilt();
        }

        private void HandleScrollOnly()
        {
            if (!useScrollWheel) return;

            float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
            if (scroll > 0.0001f) _targetAngleDeg += stepDegScroll;
            if (scroll < -0.0001f) _targetAngleDeg -= stepDegScroll;

            _targetAngleDeg = NormalizeAngle(_targetAngleDeg);
            _targetAngleDeg = Mathf.Clamp(_targetAngleDeg, minAngleDeg, maxAngleDeg);
        }

        private void ApplyTilt()
        {
            float t = 1f - Mathf.Exp(-snapSpeed * Time.deltaTime);
            _currentAngleDeg = Mathf.Lerp(_currentAngleDeg, _targetAngleDeg, t);

            Vector3 axis =
                tiltAxis == TiltAxis.X ? Vector3.right :
                tiltAxis == TiltAxis.Y ? Vector3.up :
                Vector3.forward;

            planeTransform.localRotation = _baseLocalRotation * Quaternion.AngleAxis(_currentAngleDeg, axis);
        }

        private void CaptureAuthoredBasePoseOnce()
        {
            _baseLocalRotation = planeTransform.localRotation;
            _authoredLocalPos = planeTransform.localPosition;
            _hasCapturedBase = true;

            if (debugLogs)
                Debug.Log("[PlaneAngleTiltController] Captured authored base pose (rotation + position).", this);
        }

        private void SyncAngleFromTransform()
        {
            if (planeTransform == null)
                return;

            Quaternion rel = Quaternion.Inverse(_baseLocalRotation) * planeTransform.localRotation;
            Vector3 relEuler = rel.eulerAngles;

            _currentAngleDeg = NormalizeAngle(GetAxisAngleFromEuler(relEuler));
            _targetAngleDeg = Mathf.Clamp(_currentAngleDeg, minAngleDeg, maxAngleDeg);

            if (debugLogs)
                Debug.Log($"[PlaneAngleTiltController] Sync angle = {_currentAngleDeg:0.0}° (axis={tiltAxis})", this);
        }

        private void SyncVisual()
        {
            if (planeVisual == null || planeVisual == planeTransform) return;
            planeVisual.position = planeTransform.position;
            planeVisual.rotation = planeTransform.rotation;
        }

        private float NormalizeAngle(float angle)
        {
            return Mathf.Repeat(angle + 180f, 360f) - 180f;
        }

        private float GetAxisAngleFromEuler(Vector3 euler)
        {
            switch (tiltAxis)
            {
                case TiltAxis.X: return euler.x;
                case TiltAxis.Y: return euler.y;
                case TiltAxis.Z: return euler.z;
                default: return euler.z;
            }
        }
    }
}
