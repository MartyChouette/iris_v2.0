using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using DynamicMeshCutter;

[DisallowMultipleComponent]
public class AngleCuttingPlaneController : MonoBehaviour
{
    public enum ControlMode
    {
        MouseOnly,
        KeyboardOnly,
        MouseAndKeyboard,
        Gamepad,
        Touch
    }

    [Header("Control Mode")]
    public ControlMode controlMode = ControlMode.MouseAndKeyboard;

    [Header("References")]
    [Tooltip("AngleStagePlaneBehaviour (subclass of PlaneBehaviour) that actually performs the cut.")]
    public AngleStagePlaneBehaviour anglePlane;    // inherits PlaneBehaviour

    [Header("Visual Plane")]
    [Tooltip("Optional separate visual plane object (mesh, gizmo, etc.). " +
             "If left null, we just use the anglePlane's transform as the visual.")]
    public Transform planeVisual;

    [Header("Movement Settings (Stage 1 height)")]
    public float axisMoveSpeed = 2f;
    public float mouseFollowSpeed = 20f;
    public float minY = -1f;
    public float maxY = 1f;
    public bool useMouseHeight = false;   // only if you want mouse Y during stage 1

    [Header("Tilt Settings (Stage 2)")]
    [Tooltip("Degrees per second to tilt the plane around its local Z in stage 2.")]
    public float tiltSpeedDegPerSec = 90f;
    public bool invertTilt = false;

    [Header("Stem Lock / Pivot")]
    [Tooltip("If set, this stem is used as the pivot reference. If null, " +
             "we fall back to anglePlane.previewStemOverride, then the first FlowerStemRuntime in the scene.")]
    public FlowerStemRuntime stemForPivotOverride;

    [Header("Input Actions (New Input System)")]
    [Tooltip("Vector2: W/S = Y, A/D = X. (Digital Normalized 2D Vector).")]
    public InputActionReference angleMoveAction;   // "AngleMove" (WASD or stick)

    [Tooltip("Primary cut button: first press arms, second press cuts (e.g. Space).")]
    public InputActionReference cutAction;

    [Tooltip("Cancel input to exit angle stage without cutting (e.g. Right Click).")]
    public InputActionReference cancelAngleStageAction;

    [Tooltip("Pointer position for optional mouse height in stage 1.")]
    public InputActionReference pointerPositionAction;

    [Header("Debug")]
    public bool debugLogs = false;

    // ───────────────────── internal state ─────────────────────

    Transform _planeTransform;          // logical cutter plane (anglePlane.transform)
    bool _isArmed = false;
    float _lockedY = 0f;                // height when we arm (for reference)

    // for tilt
    Quaternion _armedBaseRotation;
    Vector3 _armedTiltAxis;             // world-space axis we tilt around
    float _armedRawTiltDeg = 0f;        // continuous degrees accumulated
    float _armedSnappedTiltDeg = 0f;

    // pivot / base position
    FlowerStemRuntime _pivotStem;
    Vector3 _armedBasePosition;         // plane position when we first arm
    Vector3 _armedPivotWorld;          // world pivot point on the stem

    void Reset()
    {
        anglePlane = GetComponent<AngleStagePlaneBehaviour>();
    }

    void Awake()
    {
        if (anglePlane == null)
            anglePlane = GetComponent<AngleStagePlaneBehaviour>();

        _planeTransform = anglePlane != null ? anglePlane.transform : transform;

        if (planeVisual == null && _planeTransform != null)
            planeVisual = _planeTransform;

        if (minY > maxY)
        {
            float tmp = minY;
            minY = maxY;
            maxY = tmp;
        }
    }

    void OnEnable()
    {
        EnableAction(angleMoveAction);
        EnableAction(cutAction);
        EnableAction(cancelAngleStageAction);
        EnableAction(pointerPositionAction);
    }

    void OnDisable()
    {
        DisableAction(angleMoveAction);
        DisableAction(cutAction);
        DisableAction(cancelAngleStageAction);
        DisableAction(pointerPositionAction);
    }

    // ───────────────────── Unity Update ─────────────────────

    void Update()
    {
        if (_planeTransform == null || anglePlane == null || !anglePlane.enabled)
            return;

        // read WASD / stick once
        Vector2 move = ReadMove(angleMoveAction);

        if (!_isArmed)
        {
            Stage1_MoveHeight(move);
        }
        else
        {
            Stage2_TiltAroundStem(move);
        }

        // ───────── Cut / Confirm button ─────────
        if (cutAction != null &&
            cutAction.action != null &&
            cutAction.action.WasPerformedThisFrame())
        {
            HandleCutPress();
        }

        // ───────── Cancel angle stage ─────────
        if (_isArmed &&
            cancelAngleStageAction != null &&
            cancelAngleStageAction.action != null &&
            cancelAngleStageAction.action.WasPerformedThisFrame())
        {
            CancelAngleStage();
        }

        // Optional: during angle stage, keep preview HUD updated
        if (_isArmed)
        {
            anglePlane.PreviewAgainstFlower();
        }
    }

    // ───────────────────── Stage 1 / 2 ─────────────────────

    void Stage1_MoveHeight(Vector2 move)
    {
        Vector3 pos = _planeTransform.position;

        // Axis-based movement (W/S)
        float axisY = move.y;   // W = +1, S = -1
        if (Mathf.Abs(axisY) > 0.0001f)
        {
            pos.y += axisY * axisMoveSpeed * Time.deltaTime;
        }

        // Optional: mouse Y for height
        if (useMouseHeight &&
            pointerPositionAction != null &&
            pointerPositionAction.action != null &&
            pointerPositionAction.action.enabled)
        {
            Vector2 screenPos = pointerPositionAction.action.ReadValue<Vector2>();
            float screenHeight = Mathf.Max(1f, Screen.height);
            float t = Mathf.Clamp01(screenPos.y / screenHeight);
            float targetY = Mathf.Lerp(minY, maxY, t);
            pos.y = Mathf.Lerp(pos.y, targetY, mouseFollowSpeed * Time.deltaTime);
        }

        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        _planeTransform.position = pos;

        if (planeVisual != null && planeVisual != _planeTransform)
        {
            planeVisual.position = pos;
            planeVisual.rotation = _planeTransform.rotation;
        }
    }

    void Stage2_TiltAroundStem(Vector2 move)
    {
        // horizontal axis (A/D or stick X) controls tilt
        float axisX = move.x;   // A = -1, D = +1

        if (Mathf.Abs(axisX) > 0.0001f)
        {
            float dir = invertTilt ? -1f : 1f;
            _armedRawTiltDeg += dir * axisX * tiltSpeedDegPerSec * Time.deltaTime;
        }

        // snapping using AngleStagePlaneBehaviour.AngleSnapStepDeg
        float snapStep = (anglePlane != null) ? anglePlane.AngleSnapStepDeg : 0f;
        if (snapStep > 0.0001f)
            _armedSnappedTiltDeg = Mathf.Round(_armedRawTiltDeg / snapStep) * snapStep;
        else
            _armedSnappedTiltDeg = _armedRawTiltDeg;

        // rotation around pivot:
        //   rotation = delta * baseRot
        //   position = pivot + delta * (basePos - pivot)
        Quaternion tiltRot = Quaternion.AngleAxis(_armedSnappedTiltDeg, _armedTiltAxis);

        _planeTransform.rotation = tiltRot * _armedBaseRotation;
        _planeTransform.position = _armedPivotWorld +
                                   tiltRot * (_armedBasePosition - _armedPivotWorld);

        if (planeVisual != null && planeVisual != _planeTransform)
        {
            planeVisual.position = _planeTransform.position;
            planeVisual.rotation = _planeTransform.rotation;
        }
    }

    // ───────────────────── Cut / Cancel ─────────────────────

    void HandleCutPress()
    {
        // If pointer is over UI, STILL CUT (per your request: UI should NOT protect)
        // If you ever want to block, uncomment the check below.
        /*
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            if (debugLogs) Debug.Log("[AngleCuttingPlaneController] Cut ignored (UI).");
            return;
        }
        */

        if (!_isArmed)
        {
            ArmAngleStage();
        }
        else
        {
            // Second press → actually cut using the SAME logic as the working plane
            if (debugLogs)
                Debug.Log("[AngleCuttingPlaneController] Second press → plane.Cut()", this);

            anglePlane.Cut();   // <-- this is PlaneBehaviour.Cut()

            // After a real cut, leave angle mode
            _isArmed = false;
        }
    }

    void ArmAngleStage()
    {
        _isArmed = true;

        // Lock current Y just for reference (stage 1 → stage 2)
        _lockedY = _planeTransform.position.y;

        // Decide which stem we pivot around
        _pivotStem = stemForPivotOverride;
        if (_pivotStem == null && anglePlane != null)
            _pivotStem = anglePlane.previewStemOverride;
        if (_pivotStem == null)
            _pivotStem = UnityEngine.Object.FindFirstObjectByType<FlowerStemRuntime>();

        _armedBasePosition = _planeTransform.position;

        // Find pivot on stem (or fallback to plane position)
        if (_pivotStem != null)
            _armedPivotWorld = _pivotStem.GetClosestPointOnStem(_armedBasePosition);
        else
            _armedPivotWorld = _armedBasePosition;

        // Cache base rotation and tilt axis.
        _armedBaseRotation = _planeTransform.rotation;

        // ALWAYS tilt around LOCAL Z axis in world space
        _armedTiltAxis = _planeTransform.forward;   // world-space direction

        _armedRawTiltDeg = 0f;
        _armedSnappedTiltDeg = 0f;

        if (debugLogs)
        {
            Debug.Log(
                $"[AngleCuttingPlaneController] Angle stage ARMED " +
                $"(Y={_lockedY:0.###}, pivot={_armedPivotWorld})",
                this
            );
        }
    }

    void CancelAngleStage()
    {
        if (!_isArmed) return;

        _isArmed = false;

        if (debugLogs)
            Debug.Log("[AngleCuttingPlaneController] Angle stage CANCELLED.", this);
    }

    // ───────────────────── Helpers ─────────────────────

    Vector2 ReadMove(InputActionReference actionRef)
    {
        if (actionRef == null || actionRef.action == null || !actionRef.action.enabled)
            return Vector2.zero;

        var action = actionRef.action;

        // MUST be Vector2 ("2D Vector" composite: W/S/A/D)
        if (action.activeValueType == typeof(Vector2))
            return action.ReadValue<Vector2>();

        // if someone later makes it float, treat as vertical
        if (action.activeValueType == typeof(float))
        {
            float v = action.ReadValue<float>();
            return new Vector2(0f, v);
        }

        try
        {
            return action.ReadValue<Vector2>();
        }
        catch
        {
            return Vector2.zero;
        }
    }

    void EnableAction(InputActionReference actionRef)
    {
        if (actionRef == null) return;
        var action = actionRef.action;
        if (action != null && !action.enabled)
            action.Enable();
    }

    void DisableAction(InputActionReference actionRef)
    {
        if (actionRef == null) return;
        var action = actionRef.action;
        if (action != null && action.enabled)
            action.Disable();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (_planeTransform == null)
            _planeTransform = transform;

        Gizmos.color = Color.cyan;
        Vector3 pMin = _planeTransform.position;
        Vector3 pMax = _planeTransform.position;
        pMin.y = minY;
        pMax.y = maxY;
        Gizmos.DrawLine(pMin, pMax);

        // also draw pivot when armed (editor-time visual sanity check)
        if (_isArmed)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_armedPivotWorld, 0.01f);
        }
    }
#endif
}
