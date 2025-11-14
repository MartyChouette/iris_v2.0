using UnityEngine;
using UnityEngine.InputSystem;
using DynamicMeshCutter;

[DisallowMultipleComponent]
public class CuttingPlaneController : MonoBehaviour
{
    public enum ControlMode
    {
        KeyboardWASD,      // W/S for height, Space for cut
        MouseOnly,         // Mouse Y for height, LMB (or whatever cut binding) for cut
        MouseAndKeyboard,  // Both axis + mouse Y active
        Gamepad,           // Left stick Y, gamepad cut button
        Touchscreen        // Touch position (same as mouse pointer) + cut button
    }

    [Header("Control Mode")]
    [Tooltip("High-level input profile so menus / settings can flip this at runtime.")]
    public ControlMode controlMode = ControlMode.MouseAndKeyboard;

    [Header("References")]
    [Tooltip("PlaneBehaviour that actually performs the cut. If left null, will try to find it on this GameObject.")]
    public PlaneBehaviour plane;

    [Header("Movement Settings")]
    [Tooltip("Units per second when using keyboard / gamepad axis (W/S, left stick).")]
    public float axisMoveSpeed = 2f;

    [Tooltip("How quickly the plane chases the mouse/touch Y-mapped height.")]
    public float mouseFollowSpeed = 20f;

    [Tooltip("Minimum world Y position for the cutting plane.")]
    public float minY = -1f;

    [Tooltip("Maximum world Y position for the cutting plane.")]
    public float maxY = 1f;

    [Tooltip("If true, pointer vertical position controls plane height between minY and maxY (for modes that use pointer).")]
    public bool useMouseHeight = true;

    [Header("Input Actions (New Input System)")]
    [Tooltip("1D axis: bind W/S and/or gamepad left stick Y, etc.")]
    public InputActionReference moveYAction;

    [Tooltip("Pointer position: bind to <Pointer>/position (Mouse, Touchscreen, etc.).")]
    public InputActionReference pointerPositionAction;

    [Tooltip("Cut button: bind Space, <Mouse>/leftButton, gamepad south button, etc.")]
    public InputActionReference cutAction;

    Transform _planeTransform;

    void Reset()
    {
        // Auto-wire PlaneBehaviour on same object if present.
        plane = GetComponent<PlaneBehaviour>();
    }

    void Awake()
    {
        if (plane == null)
            plane = GetComponent<PlaneBehaviour>();

        _planeTransform = plane != null ? plane.transform : transform;

        if (minY > maxY)
        {
            float tmp = minY;
            minY = maxY;
            maxY = tmp;
        }
    }

    void OnEnable()
    {
        EnableAction(moveYAction);
        EnableAction(pointerPositionAction);
        EnableAction(cutAction);
    }

    void OnDisable()
    {
        DisableAction(moveYAction);
        DisableAction(pointerPositionAction);
        DisableAction(cutAction);
    }

    void Update()
    {
        if (_planeTransform == null)
            return;

        // Decide which input paths we actually use based on the control mode.
        bool useAxis = false;
        bool usePointer = false;

        switch (controlMode)
        {
            case ControlMode.KeyboardWASD:
                useAxis = true;        // W/S axis
                usePointer = false;    // ignore mouse/touch height
                break;

            case ControlMode.MouseOnly:
                useAxis = false;
                usePointer = true;     // mouse Y height
                break;

            case ControlMode.MouseAndKeyboard:
                useAxis = true;
                usePointer = true;
                break;

            case ControlMode.Gamepad:
                useAxis = true;        // left stick Y should be part of moveYAction
                usePointer = false;
                break;

            case ControlMode.Touchscreen:
                useAxis = false;
                usePointer = true;     // touch acts like pointer
                break;
        }

        Vector3 pos = _planeTransform.position;

        // ───────── Keyboard / Gamepad axis (W/S, LS Y) ─────────
        if (useAxis)
        {
            float axis = ReadAxis(moveYAction);
            if (Mathf.Abs(axis) > 0.0001f)
            {
                // Positive axis moves up. Flip sign here if needed.
                pos.y += axis * axisMoveSpeed * Time.deltaTime;
            }
        }

        // ───────── Mouse / Touch vertical position → world Y ─────────
        if (usePointer && useMouseHeight &&
            pointerPositionAction != null &&
            pointerPositionAction.action != null &&
            pointerPositionAction.action.enabled)
        {
            Vector2 screenPos = pointerPositionAction.action.ReadValue<Vector2>();

            float screenHeight = Mathf.Max(1f, Screen.height);
            float t = Mathf.Clamp01(screenPos.y / screenHeight); // 0 bottom, 1 top

            float targetY = Mathf.Lerp(minY, maxY, t);
            pos.y = Mathf.Lerp(pos.y, targetY, mouseFollowSpeed * Time.deltaTime);
        }

        // Clamp to bounds:
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        _planeTransform.position = pos;

        // ───────── Cut button (Space / LMB / Gamepad South) ─────────
        // Note: which *device* actually triggers this depends on how you bound cutAction.
        if (cutAction != null && cutAction.action != null && cutAction.action.WasPerformedThisFrame())
        {
            if (plane != null)
            {
                plane.Cut();
            }
        }
    }

    // ───────────────────── Helpers ─────────────────────

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

    float ReadAxis(InputActionReference actionRef)
    {
        if (actionRef == null || actionRef.action == null || !actionRef.action.enabled)
            return 0f;

        var action = actionRef.action;

        // Ideal case: float axis
        if (action.activeValueType == typeof(float))
        {
            return action.ReadValue<float>();
        }

        // If it's accidentally a Vector2 (e.g., you wired pointer here), use Y
        if (action.activeValueType == typeof(Vector2))
        {
            Vector2 v = action.ReadValue<Vector2>();
            return v.y;
        }

        // Fallback
        try
        {
            return action.ReadValue<float>();
        }
        catch
        {
            return 0f;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 pMin = transform.position;
        Vector3 pMax = transform.position;
        pMin.y = minY;
        pMax.y = maxY;
        Gizmos.DrawLine(pMin, pMax);
    }
#endif
}
