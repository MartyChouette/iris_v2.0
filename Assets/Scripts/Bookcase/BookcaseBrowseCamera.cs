using UnityEngine;
using UnityEngine.InputSystem;

public class BookcaseBrowseCamera : MonoBehaviour
{
    [Header("Look Settings")]
    [Tooltip("Maximum yaw angle (degrees) from center.")]
    [SerializeField] private float maxYaw = 60f;

    [Tooltip("Minimum pitch angle (degrees, looking down).")]
    [SerializeField] private float minPitch = -30f;

    [Tooltip("Maximum pitch angle (degrees, looking up).")]
    [SerializeField] private float maxPitch = 40f;

    [Tooltip("How quickly camera rotation follows the cursor.")]
    [SerializeField] private float smoothSpeed = 10f;

    [Header("Height Settings")]
    [Tooltip("Standing eye height in meters.")]
    [SerializeField] private float standingHeight = 1.5f;

    [Tooltip("Tippy-toes eye height in meters.")]
    [SerializeField] private float tiptoeHeight = 1.85f;

    [Tooltip("Crouching eye height in meters.")]
    [SerializeField] private float crouchHeight = 0.9f;

    [Tooltip("How quickly camera height changes.")]
    [SerializeField] private float heightSpeed = 8f;

    private InputAction _mousePositionAction;
    private InputAction _tiptoeAction;
    private InputAction _crouchAction;

    private float _targetYaw;
    private float _targetPitch;
    private float _currentYaw;
    private float _currentPitch;
    private float _targetHeight;
    private bool _lookEnabled = true;

    private void Awake()
    {
        _mousePositionAction = new InputAction("MousePosition", InputActionType.Value,
            "<Mouse>/position");

        _tiptoeAction = new InputAction("Tiptoe", InputActionType.Button,
            "<Keyboard>/space");

        _crouchAction = new InputAction("Crouch", InputActionType.Button,
            "<Keyboard>/leftCtrl");

        _targetHeight = standingHeight;
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    }

    private void OnEnable()
    {
        _mousePositionAction.Enable();
        _tiptoeAction.Enable();
        _crouchAction.Enable();
    }

    private void OnDisable()
    {
        _mousePositionAction.Disable();
        _tiptoeAction.Disable();
        _crouchAction.Disable();
    }

    public void SetLookEnabled(bool enabled)
    {
        _lookEnabled = enabled;
    }

    private void Update()
    {
        UpdateLook();
        UpdateHeight();
    }

    private void UpdateLook()
    {
        if (!_lookEnabled) return;

        Vector2 mousePos = _mousePositionAction.ReadValue<Vector2>();

        // Normalize cursor position to [-1, +1]
        float normalizedX = (mousePos.x / Screen.width) * 2f - 1f;
        float normalizedY = (mousePos.y / Screen.height) * 2f - 1f;

        _targetYaw = normalizedX * maxYaw;
        _targetPitch = Mathf.Lerp(minPitch, maxPitch, (normalizedY + 1f) * 0.5f);

        // Smooth toward target
        float dt = Time.deltaTime * smoothSpeed;
        _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, dt);
        _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, dt);

        transform.rotation = Quaternion.Euler(-_currentPitch, _currentYaw, 0f);
    }

    private void UpdateHeight()
    {
        if (_tiptoeAction.IsPressed())
            _targetHeight = tiptoeHeight;
        else if (_crouchAction.IsPressed())
            _targetHeight = crouchHeight;
        else
            _targetHeight = standingHeight;

        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, _targetHeight, Time.deltaTime * heightSpeed);
        transform.position = pos;
    }
}
