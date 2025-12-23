using UnityEngine;

/**
 * @class MouseLookCamera3D
 * @brief Prototype camera drift that offsets the camera based on mouse position on screen.
 *
 * @details
 * This supports the siloed 2D/3D living-room prototype by creating a subtle "look around"
 * effect without full character movement. The mouse position is normalized relative to the
 * screen center, scaled by @ref maxOffset, and smoothed with a lerp.
 *
 * ------------------------------------------------------------
 * Responsibilities
 * ------------------------------------------------------------
 * - Compute a screen-centered normalized mouse vector each frame.
 * - Convert that vector into a world-space offset around the camera's initial position.
 * - Smoothly move the camera toward that target position.
 *
 * ------------------------------------------------------------
 * Non-Responsibilities
 * ------------------------------------------------------------
 * - Does not clamp to world bounds.
 * - Does not support gamepad / touch input.
 * - Does not handle camera handoff or multiple cameras.
 *
 * ------------------------------------------------------------
 * Key Data & Invariants
 * ------------------------------------------------------------
 * - @ref initialCameraPosition is the anchor point.
 * - Z offset is forced to 0 to preserve depth.
 *
 * ------------------------------------------------------------
 * Unity Lifecycle Notes
 * ------------------------------------------------------------
 * - Start(): caches the initial camera position.
 * - Update(): computes target and lerps toward it.
 *
 * ------------------------------------------------------------
 * Performance & Allocation Notes
 * ------------------------------------------------------------
 * - Uses simple math and @c Vector3.Lerp; no allocations expected per frame.
 *
 * ------------------------------------------------------------
 * Visual Maps
 * ------------------------------------------------------------
 * @section viz_relationships_mousecam Visual Relationships
 * @dot
 * digraph MouseLookCamera3D_Relations {
 *   rankdir=LR;
 *   node [shape=box];
 *
 *   "MouseLookCamera3D" -> "Input" [label="mousePosition"];
 *   "MouseLookCamera3D" -> "Transform" [label="writes position"];
 * }
 * @enddot
 */
public class MouseLookCamera3D : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("The maximum distance the camera can move from its origin (world units).")]
    public float maxOffset = 1.0f;

    [Tooltip("The speed for smooth camera movement.")]
    public float smoothSpeed = 5.0f;

    private Vector3 targetPosition;
    private Vector3 initialCameraPosition;

    void Start()
    {
        initialCameraPosition = transform.position;
    }

    void Update()
    {
        float x = (Input.mousePosition.x / Screen.width) - 0.5f;
        float y = (Input.mousePosition.y / Screen.height) - 0.5f;

        Vector3 mouseNormalized = new Vector3(x, y, 0);
        Vector3 mouseOffset = mouseNormalized * maxOffset;
        mouseOffset.z = 0f;

        targetPosition = initialCameraPosition + mouseOffset;

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
    }
}
