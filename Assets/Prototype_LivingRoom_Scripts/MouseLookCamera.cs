using UnityEngine;

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
        // Store the camera's original position. This is the center point.
        initialCameraPosition = transform.position;
    }

    void Update()
    {
        // 1. Get Mouse Position Normalized (0,0 is center)
        float x = (Input.mousePosition.x / Screen.width) - 0.5f;
        float y = (Input.mousePosition.y / Screen.height) - 0.5f;

        Vector3 mouseNormalized = new Vector3(x, y, 0);

        // 2. Calculate Camera Target Position
        Vector3 mouseOffset = mouseNormalized * maxOffset;

        // Maintain the camera's Z position
        mouseOffset.z = 0f;

        targetPosition = initialCameraPosition + mouseOffset;

        // 3. Smoothly Move the Camera
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
    }
}