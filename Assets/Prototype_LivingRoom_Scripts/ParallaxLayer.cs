using UnityEngine;

public class ParallaxLayer3D : MonoBehaviour
{
    [Tooltip("The factor by which this layer moves relative to the camera. Closer layers use a larger factor (e.g., 0.6).")]
    [Range(0f, 1f)]
    public float parallaxFactor = 0.5f;

    private Vector3 initialLayerPosition;
    private Transform mainCamera;
    private Vector3 cameraInitialPosition;

    void Start()
    {
        mainCamera = Camera.main.transform;

        // Store the starting positions
        initialLayerPosition = transform.position;
        cameraInitialPosition = mainCamera.position;
    }

    void LateUpdate()
    {
        // 1. Calculate Camera Movement (Delta)
        Vector3 cameraDelta = mainCamera.position - cameraInitialPosition;

        // 2. Calculate Parallax Movement
        // Moves the layer in the opposite direction of the camera movement, scaled by the factor.
        Vector3 parallaxMovement = new Vector3(
            cameraDelta.x * -parallaxFactor,
            cameraDelta.y * -parallaxFactor,
            0f // Keep the layer's Z-depth fixed relative to its initial Z
        );

        // 3. Apply New Layer Position
        transform.position = initialLayerPosition + parallaxMovement;
    }
}