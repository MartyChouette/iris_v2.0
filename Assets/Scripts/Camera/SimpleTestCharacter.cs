using UnityEngine;
using UnityEngine.InputSystem;

namespace Iris.Camera
{
    /// <summary>
    /// Minimal capsule character for camera test scene.
    /// World-space WASD/gamepad movement with gravity — no camera-relative steering.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SimpleTestCharacter : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float gravity = -15f;

        private CharacterController _cc;
        private InputAction _moveAction;
        private float _verticalVelocity;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();

            // Inline composite binding — no InputActionAsset needed
            _moveAction = new InputAction("Move", InputActionType.Value);
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/w")
                .With("Down",  "<Keyboard>/s")
                .With("Left",  "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.AddBinding("<Gamepad>/leftStick");
        }

        private void OnEnable()  => _moveAction.Enable();
        private void OnDisable() => _moveAction.Disable();

        private void Update()
        {
            Vector2 input = _moveAction.ReadValue<Vector2>();
            Vector3 move  = new Vector3(input.x, 0f, input.y);

            // Gravity
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f; // small stick force
            _verticalVelocity += gravity * Time.deltaTime;
            move.y = _verticalVelocity;

            _cc.Move(move * (moveSpeed * Time.deltaTime));

            // Face movement direction (horizontal only)
            Vector3 horizontal = new Vector3(input.x, 0f, input.y);
            if (horizontal.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(horizontal.normalized);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, target, rotationSpeed * Time.deltaTime);
            }
        }
    }
}
