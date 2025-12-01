using UnityEngine;

namespace DynamicMeshCutter
{
    /// <summary>
    /// Add-on controller that only handles a "second stage" angle tilt
    /// for an existing plane cutter. It does NOT call Cut() or change
    /// any targets; it only rotates the plane transform.
    /// 
    /// Usage:
    /// - Press toggleKey to enter/exit tilt mode.
    /// - While in tilt mode, mouse X tilts the plane around its local X axis.
    /// </summary>
    public class PlaneAngleTiltController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Transform of the cutting plane (same object with PlaneBehaviour).")]
        public Transform planeTransform;

        [Tooltip("Optional visual plane to match rotation.")]
        public Transform planeVisual;

        [Header("Tilt Settings")]
        [Tooltip("Key to toggle tilt mode on/off.")]
        public KeyCode toggleKey = KeyCode.Space;

        [Tooltip("Degrees per mouse X unit per second.")]
        public float rotateSpeedDegPerUnit = 120f;

        [Tooltip("Clamp for rotation around local X axis.")]
        public float minAngleX = -80f;
        public float maxAngleX = 80f;

        private bool _tiltModeActive = false;
        private float _currentAngleX;

        private void Awake()
        {
            if (planeTransform == null)
                planeTransform = transform;

            _currentAngleX = planeTransform.localEulerAngles.x;
        }

        private void Update()
        {
            HandleToggleInput();

            if (_tiltModeActive)
            {
                HandleTilt();
            }

            SyncVisual();
        }

        private void HandleToggleInput()
        {
            if (!Input.GetKeyDown(toggleKey))
                return;

            _tiltModeActive = !_tiltModeActive;

            if (_tiltModeActive)
            {
                _currentAngleX = planeTransform.localEulerAngles.x;
            }
        }

        private void HandleTilt()
        {
            float mouseX = Input.GetAxisRaw("Mouse X");

            if (Mathf.Abs(mouseX) < 0.0001f)
                return;

            _currentAngleX += mouseX * rotateSpeedDegPerUnit * Time.deltaTime;

            // Normalize & clamp
            _currentAngleX = Mathf.Repeat(_currentAngleX + 180f, 360f) - 180f;
            _currentAngleX = Mathf.Clamp(_currentAngleX, minAngleX, maxAngleX);

            Vector3 euler = planeTransform.localEulerAngles;
            euler.x = _currentAngleX;
            planeTransform.localEulerAngles = euler;
        }

        private void SyncVisual()
        {
            if (planeVisual == null || planeVisual == planeTransform)
                return;

            planeVisual.position = planeTransform.position;
            planeVisual.rotation = planeTransform.rotation;
        }
    }
}
