/**
 * @file MouseParallax.cs
 * @brief MouseParallax script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup ui
 */

using UnityEngine;
using System.Collections.Generic;
/**
 * @class MouseParallax
 * @brief MouseParallax component.
 * @details
 * Responsibilities:
 * - (Documented) See fields and methods below.
 *
 * Unity lifecycle:
 * - Awake(): cache references / validate setup.
 * - OnEnable()/OnDisable(): hook/unhook events.
 * - Update(): per-frame behavior (if any).
 *
 * Gotchas:
 * - Keep hot paths allocation-free (Update/cuts/spawns).
 * - Prefer event-driven UI updates over per-frame string building.
 *
 * @ingroup ui
 */

public class MouseParallax : MonoBehaviour
{
    [System.Serializable]
    /**
     * @class ParallaxLayer
     * @brief ParallaxLayer component.
     * @details
     * Responsibilities:
     * - (Documented) See fields and methods below.
     *
     * Unity lifecycle:
     * - Awake(): cache references / validate setup.
     * - OnEnable()/OnDisable(): hook/unhook events.
     * - Update(): per-frame behavior (if any).
     *
     * Gotchas:
     * - Keep hot paths allocation-free (Update/cuts/spawns).
     * - Prefer event-driven UI updates over per-frame string building.
     *
     * @ingroup ui
     */
    public class ParallaxLayer
    {
        public Transform layerObject; // The UI Image or Sprite
        [Tooltip("Higher numbers = more movement. Use negative numbers to move in opposite direction.")]
        public float moveSpeed = 10f;
    }

    [Header("Settings")]
    public List<ParallaxLayer> layers = new List<ParallaxLayer>();
    public float smoothing = 5f; // How fast the layers drift to the target position

    [Header("Constraints")]
    [Tooltip("Prevents the layers from moving too far off screen")]
    public float maxOffset = 50f;

    private List<Vector3> startPositions = new List<Vector3>();

    void Start()
    {
        // Store the original positions of all layers so we know where to return
        foreach (var layer in layers)
        {
            if (layer.layerObject != null)
            {
                startPositions.Add(layer.layerObject.localPosition);
            }
        }
    }

    void Update()
    {
        // 1. Get Mouse Position centered (0,0 is center of screen)
        // Range becomes approx -0.5 to 0.5
        float mouseX = (Input.mousePosition.x / Screen.width) - 0.5f;
        float mouseY = (Input.mousePosition.y / Screen.height) - 0.5f;

        // 2. Loop through layers and apply movement
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].layerObject == null) continue;

            // Calculate target position based on mouse position * speed
            float targetX = mouseX * layers[i].moveSpeed;
            float targetY = mouseY * layers[i].moveSpeed;

            // Clamp the movement so it doesn't go too far
            targetX = Mathf.Clamp(targetX, -maxOffset, maxOffset);
            targetY = Mathf.Clamp(targetY, -maxOffset, maxOffset);

            Vector3 targetPos = startPositions[i] + new Vector3(targetX, targetY, 0);

            // 3. Smoothly move (Lerp) towards the target
            layers[i].layerObject.localPosition = Vector3.Lerp(
                layers[i].layerObject.localPosition,
                targetPos,
                Time.deltaTime * smoothing
            );
        }
    }
}