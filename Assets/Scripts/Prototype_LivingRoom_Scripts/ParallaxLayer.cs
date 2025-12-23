using UnityEngine;

/**
 * @class ParallaxLayer3D
 * @brief Prototype parallax component that offsets a 3D layer based on camera movement.
 *
 * @details
 * This script supports the siloed 2D/3D living-room prototype by giving background/foreground
 * layers a subtle parallax drift when the camera moves. It assumes a fixed Z-depth for the layer
 * and only offsets X/Y.
 *
 * ------------------------------------------------------------
 * Responsibilities
 * ------------------------------------------------------------
 * - Cache the initial layer and camera positions at startup.
 * - Compute camera movement delta and apply inverse-scaled movement to the layer.
 *
 * ------------------------------------------------------------
 * Non-Responsibilities
 * ------------------------------------------------------------
 * - Does not support looping / tiling backgrounds.
 * - Does not handle camera swapping or runtime camera changes.
 *
 * ------------------------------------------------------------
 * Key Data & Invariants
 * ------------------------------------------------------------
 * - @ref initialLayerPosition is treated as the anchor point.
 * - @ref cameraInitialPosition is treated as the reference camera position (delta origin).
 * - Z movement is ignored to preserve layer depth.
 *
 * ------------------------------------------------------------
 * Unity Lifecycle Notes
 * ------------------------------------------------------------
 * - Start(): caches @c Camera.main and initial positions.
 * - LateUpdate(): applies parallax after camera motion is finalized for the frame.
 *
 * ------------------------------------------------------------
 * Performance & Allocation Notes
 * ------------------------------------------------------------
 * - Lightweight math only; no allocations expected per frame.
 *
 * ------------------------------------------------------------
 * Visual Maps
 * ------------------------------------------------------------
 * @section viz_relationships_parallax Visual Relationships
 * @dot
 * digraph ParallaxLayer3D_Relations {
 *   rankdir=LR;
 *   node [shape=box];
 *
 *   "ParallaxLayer3D" -> "Camera" [label="reads position delta"];
 *   "ParallaxLayer3D" -> "Transform" [label="writes position"];
 * }
 * @enddot
 */
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
        Vector3 cameraDelta = mainCamera.position - cameraInitialPosition;

        Vector3 parallaxMovement = new Vector3(
            cameraDelta.x * -parallaxFactor,
            cameraDelta.y * -parallaxFactor,
            0f
        );

        transform.position = initialLayerPosition + parallaxMovement;
    }
}
