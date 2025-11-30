using UnityEngine;

namespace DynamicMeshCutter
{
    public enum AnglePlaneControlMode
    {
        MouseAndKeyboard,
        Locked
    }

    /// <summary>
    /// Two–stage angle cutter controller:
    /// Stage 1: mouse Y moves plane up/down in world Y (height).
    /// Stage 2: mouse X rotates plane around its local Z axis.
    /// </summary>
    public class AngleCuttingPlaneController : MonoBehaviour
    {
        [Header("Control Mode")]
        public AnglePlaneControlMode controlMode = AnglePlaneControlMode.MouseAndKeyboard;

        [Header("References")]
        [Tooltip("The cutting plane behaviour sitting on the AnglePlane object.")]
        public AngleStagePlaneBehaviour anglePlane;

        [Tooltip("Visual representation (e.g. a cube) that should match the cutter plane.")]
        public Transform planeVisual;

        [Header("Stage 1 – Height (world Y)")]
        [Tooltip("Units of world Y per mouse Y unit per second.")]
        public float heightMoveSpeed = 1.0f;

        [Tooltip("Optional clamp for min/max world Y height.")]
        public float minHeightY = -1.0f;
        public float maxHeightY = 1.0f;

        [Header("Stage 2 – Angle (local Z)")]
        [Tooltip("Degrees per mouse X unit per second.")]
        public float rotateSpeedDegPerUnit = 90f;

        [Tooltip("Clamp for rotation around local Z axis, in degrees.")]
        public float minAngleZ = -80f;
        public float maxAngleZ = 80f;

        [Header("Input")]
        [Tooltip("Key that advances stage: 1st press locks height, 2nd press locks angle + cuts.")]
        public KeyCode confirmKey = KeyCode.Space;

        [Tooltip("Key to cancel back to Stage 1 without cutting.")]
        public KeyCode cancelKey = KeyCode.Escape;

        // ───────────────────── Internal state ─────────────────────

        private enum Stage
        {
            AdjustHeight = 0,
            AdjustAngle = 1,
            Locked = 2
        }

        private Stage _stage = Stage.AdjustHeight;

        private float _currentY;
        private float _currentAngleZ;

        // ───────────────────── Unity lifecycle ─────────────────────

        private void Awake()
        {
            if (anglePlane != null)
            {
                Transform t = anglePlane.transform;
                _currentY = t.position.y;
                _currentAngleZ = t.localEulerAngles.z;
            }
            else
            {
                Transform t = transform;
                _currentY = t.position.y;
                _currentAngleZ = t.localEulerAngles.z;
            }
        }

        private void OnEnable()
        {
            _stage = Stage.AdjustHeight;
            SetAngleStageArmed(false);
        }

        private void Update()
        {
            if (controlMode == AnglePlaneControlMode.Locked || anglePlane == null)
            {
                SyncVisualToPlane();
                return;
            }

            HandleStageInput();

            switch (_stage)
            {
                case Stage.AdjustHeight:
                    HandleStage1_MoveHeight();
                    break;

                case Stage.AdjustAngle:
                    HandleStage2_RotateAngleZ();
                    break;

                case Stage.Locked:
                    break;
            }

            SyncVisualToPlane();
        }

        // ───────────────────── Stage switching ─────────────────────

        private void HandleStageInput()
        {
            if (Input.GetKeyDown(confirmKey))
            {
                if (_stage == Stage.AdjustHeight)
                {
                    // Lock height, go to angle stage.
                    _stage = Stage.AdjustAngle;
                    SetAngleStageArmed(true);

                    // Cache current Z angle so we rotate relative to it.
                    _currentAngleZ = anglePlane.transform.localEulerAngles.z;
                }
                else if (_stage == Stage.AdjustAngle)
                {
                    // Lock angle and cut.
                    DoCut();
                    _stage = Stage.Locked;
                    SetAngleStageArmed(false);
                }
            }

            if (Input.GetKeyDown(cancelKey))
            {
                if (_stage != Stage.Locked)
                {
                    _stage = Stage.AdjustHeight;
                    SetAngleStageArmed(false);
                }
            }
        }

        private void SetAngleStageArmed(bool armed)
        {
            if (anglePlane != null)
            {
                anglePlane.SetAngleStageArmed(armed);
            }
        }

        // ───────────────────── Stage 1: HEIGHT ONLY ─────────────────────

        private void HandleStage1_MoveHeight()
        {
            if (anglePlane == null)
                return;

            Transform t = anglePlane.transform;
            Vector3 pos = t.position;

            // Mouse Y → world Y (height).  Flip the sign if this feels backwards.
            float mouseY = Input.GetAxisRaw("Mouse Y");
            _currentY += mouseY * heightMoveSpeed * Time.deltaTime;

            _currentY = Mathf.Clamp(_currentY, minHeightY, maxHeightY);

            pos.y = _currentY; // X/Z untouched.
            t.position = pos;

            anglePlane.NotifyPlanePoseChanged();
        }

        // ───────────────────── Stage 2: ANGLE ONLY (local Z) ─────────────────────

        private void HandleStage2_RotateAngleZ()
        {
            if (anglePlane == null)
                return;

            Transform t = anglePlane.transform;

            // Mouse X → rotation around local Z.
            float mouseX = Input.GetAxisRaw("Mouse X");
            _currentAngleZ += mouseX * rotateSpeedDegPerUnit * Time.deltaTime;

            // Normalize around 0 before clamping.
            _currentAngleZ = Mathf.Repeat(_currentAngleZ + 180f, 360f) - 180f;
            _currentAngleZ = Mathf.Clamp(_currentAngleZ, minAngleZ, maxAngleZ);

            Vector3 euler = t.localEulerAngles;
            euler.z = _currentAngleZ;
            t.localEulerAngles = euler;

            anglePlane.NotifyPlanePoseChanged();
        }

        // ───────────────────── Cut + visual sync ─────────────────────

        private void DoCut()
        {
            if (anglePlane == null) return;

            anglePlane.NotifyPlanePoseChanged();
            anglePlane.PerformCut();
        }

        private void SyncVisualToPlane()
        {
            if (planeVisual == null || anglePlane == null) return;

            Transform src = anglePlane.transform;
            planeVisual.position = src.position;
            planeVisual.rotation = src.rotation;
        }
    }
}
