using UnityEngine;

/**
 * @class InteractiveSprite
 * @brief Prototype 2D interactable that swaps sprites and offsets position on hover.
 *
 * @details
 * Used in the siloed living-room prototype to provide simple hover feedback for 2D elements
 * (UI-like props, VN-style objects, flat set dressing). Scene navigation is optional and is
 * exposed via @ref targetScene, but in the current prototype flow, scene loading is restricted
 * by RaycastManager to 3D targets only.
 *
 * ------------------------------------------------------------
 * Responsibilities
 * ------------------------------------------------------------
 * - Cache the initial local position and SpriteRenderer.
 * - Swap sprites on hover and apply a local-space hover offset.
 * - Restore default sprite and position on hover exit.
 *
 * ------------------------------------------------------------
 * Non-Responsibilities
 * ------------------------------------------------------------
 * - Does not perform raycasting; it only responds to hover events.
 * - Does not validate that sprites are assigned.
 *
 * ------------------------------------------------------------
 * Key Data & Invariants
 * ------------------------------------------------------------
 * - @ref initialLocalPosition is the restoration anchor.
 * - Hover effects are local-space (transform.localPosition).
 *
 * ------------------------------------------------------------
 * Unity Lifecycle Notes
 * ------------------------------------------------------------
 * - Start(): caches SpriteRenderer and initialLocalPosition, applies default sprite if available.
 *
 * ------------------------------------------------------------
 * Performance & Allocation Notes
 * ------------------------------------------------------------
 * - No allocations expected during hover, beyond any internal SpriteRenderer/material behavior.
 *
 * ------------------------------------------------------------
 * Visual Maps
 * ------------------------------------------------------------
 * @section viz_relationships_sprite Visual Relationships
 * @dot
 * digraph InteractiveSprite_Relations {
 *   rankdir=LR;
 *   node [shape=box];
 *
 *   "RaycastManager" -> "InteractiveSprite" [label="calls hover enter/exit"];
 *   "InteractiveSprite" -> "SpriteRenderer" [label="swap sprite"];
 *   "InteractiveSprite" -> "Transform" [label="local offset"];
 * }
 * @enddot
 */
public class InteractiveSprite : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite defaultSprite;
    public Sprite hoverSprite;

    [Header("Position Offset")]
    public Vector2 hoverOffset = new Vector2(0.2f, 0.2f);

    [Header("Scene Destination")]
    public string targetScene = ""; // Optional scene for 2D objects

    private SpriteRenderer spriteRenderer;
    private Vector3 initialLocalPosition;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialLocalPosition = transform.localPosition;
        if (spriteRenderer != null && defaultSprite != null)
        {
            spriteRenderer.sprite = defaultSprite;
        }
    }

    public void OnHoverEnter()
    {
        if (spriteRenderer != null && hoverSprite != null)
        {
            spriteRenderer.sprite = hoverSprite;
            Vector3 targetPos = initialLocalPosition + new Vector3(hoverOffset.x, hoverOffset.y, 0);
            transform.localPosition = targetPos;
        }
    }

    public void OnHoverExit()
    {
        if (spriteRenderer != null && defaultSprite != null)
        {
            spriteRenderer.sprite = defaultSprite;
            transform.localPosition = initialLocalPosition;
        }
    }

    public string GetTargetSceneName()
    {
        return targetScene;
    }
}
