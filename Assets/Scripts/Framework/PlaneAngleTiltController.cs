using UnityEngine;

namespace DynamicMeshCutter
{
    /// <summary>
    /// Add-on controller that only handles a "second stage" angle tilt
    /// for an existing cutting plane. It does NOT call Cut() or change
    /// any targets; it only rotates the plane transform.
    /// 
    /// Usage:
    /// - Attach this to the same GameObject as the cutter plane (or a parent).
    /// - planeTransform should point at the PlaneBehaviour's transform.
    /// - Press toggleKey to enter/exit tilt mode.
    /// - While in tilt mode, Mouse X tilts the plane around the chosen axis.
    /// 
    /// New: You can choose which local axis (X/Y/Z) to tilt around,
    ///      and optionally lock world-space Y in tilt mode so stage 2
    ///      is purely about angle, not height.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlaneAngleTiltController : MonoBehaviour
    {
        public enum TiltAxis
        {
            X,
            Y,
            Z
        }

        [Header("References")]
        [Tooltip("Transform of the cutting plane (same object that has PlaneBehaviour). If null, defaults to this.transform.")]
        public Transform planeTransform;

        [Tooltip("Optional separate visual plane to match rotation; leave null to ignore.")]
        public Transform planeVisual;

        [Header("Tilt Settings")]
        [Tooltip("Key to toggle tilt mode on/off.")]
        public KeyCode toggleKey = KeyCode.Space;

        [Tooltip("Which local axis of planeTransform to rotate around.")]
        public TiltAxis tiltAxis = TiltAxis.X;

        [Tooltip("Degrees per Mouse X unit per second.")]
        public float rotateSpeedDegPerUnit = 120f;

        [Tooltip("Minimum angle in degrees on the chosen axis (in -180..180 range).")]
        public float minAngleDeg = -80f;

        [Tooltip("Maximum angle in degrees on the chosen axis (in -180..180 range).")]
        public float maxAngleDeg = 80f;

        [Header("Height Lock")]
        [Tooltip("If true, when tilt mode (stage 2) is active, the plane's world-space Y is locked " +
                 "to the height it had when tilt mode was entered. This makes stage 2 purely about angle.")]
        public bool lockYInTiltMode = true;

        [Tooltip("Debug: stored world-space Y captured when tilt mode was entered.")]
        public float lockedWorldY;

        [Header("Debug")]
        public bool debugLogs = false;

        // Internal state
        [SerializeField]
        private bool _tiltModeActive = false;
        private float _currentAngleDeg;

        /// <summary>
        /// Public read-only access so other scripts (like CuttingPlaneController)
        /// can know when tilt mode is active and stop moving Y.
        /// </summary>
        public bool TiltModeActive => _tiltModeActive;

        private void Awake()
        {
            if (planeTransform == null)
                planeTransform = transform;

            // Initialize from current localEulerAngles on chosen axis
            Vector3 euler = planeTransform.localEulerAngles;
            _currentAngleDeg = GetAxisAngleFromEuler(euler);
            _currentAngleDeg = NormalizeAngle(_currentAngleDeg);

            // Initial locked Y is just whatever height we're currently at.
            lockedWorldY = planeTransform.position.y;

            if (debugLogs)
            {
                Debug.Log(
                    $"[PlaneAngleTiltController] Awake; initial axis {tiltAxis}, angle = {_currentAngleDeg}, lockedY = {lockedWorldY}",
                    this
                );
            }
        }

        private void Update()
        {
            HandleToggleInput();

            if (_tiltModeActive)
            {
                // Stage 2: lock height, only change angle.
                if (lockYInTiltMode)
                    LockHeightY();

                HandleTilt();
            }

            SyncVisual();
        }

        // ───────────────────────────────── Toggle ─────────────────────────────────

        private void HandleToggleInput()
        {
            if (!Input.GetKeyDown(toggleKey))
                return;

            _tiltModeActive = !_tiltModeActive;

            if (_tiltModeActive)
            {
                // Re-sync current angle from transform when entering tilt mode
                Vector3 euler = planeTransform.localEulerAngles;
                _currentAngleDeg = GetAxisAngleFromEuler(euler);
                _currentAngleDeg = NormalizeAngle(_currentAngleDeg);

                // Capture the current world-space Y so height is fixed for stage 2
                lockedWorldY = planeTransform.position.y;

                if (debugLogs)
                {
                    Debug.Log(
                        $"[PlaneAngleTiltController] Tilt mode ON. Axis {tiltAxis}, angle = {_currentAngleDeg}, lockedY = {lockedWorldY}",
                        this
                    );
                }
            }
            else
            {
                if (debugLogs)
                {
                    Debug.Log("[PlaneAngleTiltController] Tilt mode OFF.", this);
                }
            }
        }

        // ───────────────────────────────── Tilt ─────────────────────────────────

        private void HandleTilt()
        {
            float mouseX = Input.GetAxisRaw("Mouse X");
            if (Mathf.Abs(mouseX) < 0.0001f)
                return;

            _currentAngleDeg += mouseX * rotateSpeedDegPerUnit * Time.deltaTime;

            // Normalize & clamp to -180..180 then within min/max
            _currentAngleDeg = NormalizeAngle(_currentAngleDeg);
            _currentAngleDeg = Mathf.Clamp(_currentAngleDeg, minAngleDeg, maxAngleDeg);

            Vector3 euler = planeTransform.localEulerAngles;
            euler = SetAxisAngleOnEuler(euler, _currentAngleDeg);
            planeTransform.localEulerAngles = euler;
        }

        // ───────────────────────────────── Height Lock ─────────────────────────────────

        /// <summary>
        /// Keeps the plane's world-space Y locked to the value captured when
        /// tilt mode was entered. This is what makes stage 2 purely about angle.
        /// </summary>
        private void LockHeightY()
        {
            Vector3 pos = planeTransform.position;
            pos.y = lockedWorldY;
            planeTransform.position = pos;
        }

        // ───────────────────────────────── Visual Sync ─────────────────────────────────

        private void SyncVisual()
        {
            if (planeVisual == null || planeVisual == planeTransform)
                return;

            planeVisual.position = planeTransform.position;
            planeVisual.rotation = planeTransform.rotation;
        }

        // ───────────────────────────────── Helpers ─────────────────────────────────

        /// <summary>
        /// Normalize angle from any 0..360 value into -180..180.
        /// </summary>
        private float NormalizeAngle(float angle)
        {
            angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
            return angle;
        }

        /// <summary>
        /// Read the plane's chosen axis angle from a localEulerAngles vector.
        /// </summary>
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

        /// <summary>
        /// Write the plane's chosen axis angle to a localEulerAngles vector.
        /// </summary>
        private Vector3 SetAxisAngleOnEuler(Vector3 euler, float angleDeg)
        {
            switch (tiltAxis)
            {
                case TiltAxis.X:
                    euler.x = angleDeg;
                    break;
                case TiltAxis.Y:
                    euler.y = angleDeg;
                    break;
                case TiltAxis.Z:
                    euler.z = angleDeg;
                    break;
            }

            return euler;
        }
    }
}
