using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;   // ⬅ added
using DynamicMeshCutter;

[DisallowMultipleComponent]
public class CuttingPlaneController : MonoBehaviour
{
    public enum ControlMode
    {
        KeyboardWASD,
        MouseOnly,
        MouseAndKeyboard,
        Gamepad,
        Touchscreen
    }

    [Header("Control Mode")]
    public ControlMode controlMode = ControlMode.MouseAndKeyboard;

    [Header("References")]
    [Tooltip("PlaneBehaviour that actually performs the cut. If left null, will try to find it on this GameObject.")]
    public PlaneBehaviour plane;

    [Header("Movement Settings")]
    public float axisMoveSpeed = 2f;
    public float mouseFollowSpeed = 20f;
    public float minY = -1f;
    public float maxY = 1f;
    public bool useMouseHeight = true;

    [Header("Input Actions (New Input System)")]
    public InputActionReference moveYAction;
    public InputActionReference pointerPositionAction;
    public InputActionReference cutAction;

    Transform _planeTransform;

    void Reset()
    {
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

        bool useAxis = false;
        bool usePointer = false;

        switch (controlMode)
        {
            case ControlMode.KeyboardWASD:
                useAxis = true;
                break;
            case ControlMode.MouseOnly:
                usePointer = true;
                break;
            case ControlMode.MouseAndKeyboard:
                useAxis = true;
                usePointer = true;
                break;
            case ControlMode.Gamepad:
                useAxis = true;
                break;
            case ControlMode.Touchscreen:
                usePointer = true;
                break;
        }

        Vector3 pos = _planeTransform.position;

        // ───────── Axis movement ─────────
        if (useAxis)
        {
            float axis = ReadAxis(moveYAction);
            if (Mathf.Abs(axis) > 0.0001f)
                pos.y += axis * axisMoveSpeed * Time.deltaTime;
        }

        // ───────── Pointer-based height ─────────
        if (usePointer && useMouseHeight &&
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

        // ───────── Cut button ─────────
        if (cutAction != null &&
            cutAction.action != null &&
            cutAction.action.WasPerformedThisFrame())
        {
            // 1) If the pointer is over UI, ignore this cut.
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
#if UNITY_EDITOR
                Debug.Log("[CuttingPlaneController] Cut input ignored (pointer over UI).");
#endif
                return;
            }

            // 2) Only cut if we actually have a PlaneBehaviour AND it is enabled
            if (plane != null && plane.enabled)
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

        if (action.activeValueType == typeof(float))
            return action.ReadValue<float>();

        if (action.activeValueType == typeof(Vector2))
        {
            Vector2 v = action.ReadValue<Vector2>();
            return v.y;
        }

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
