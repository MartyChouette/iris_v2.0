using UnityEngine;

/**
 * @class InteractiveObject3D
 * @brief Prototype 3D interactable that changes color and scale on hover and can provide a target scene name.
 *
 * @details
 * Used in the siloed living-room prototype for 3D props (e.g., scissors, objects on a table)
 * that the player can hover. This class exposes a @ref targetScene, which RaycastManager may
 * load on click if the hovered target is recognized as a 3D interactable.
 *
 * ------------------------------------------------------------
 * Responsibilities
 * ------------------------------------------------------------
 * - Cache the Renderer material and initial scale.
 * - Apply hover feedback (color + slight scale increase).
 * - Restore default color and scale on hover exit.
 * - Provide a target scene name for optional scene transitions.
 *
 * ------------------------------------------------------------
 * Non-Responsibilities
 * ------------------------------------------------------------
 * - Does not perform raycasting or input detection.
 * - Does not validate that the scene exists in Build Settings.
 *
 * ------------------------------------------------------------
 * Key Data & Invariants
 * ------------------------------------------------------------
 * - @ref initialScale is the restoration anchor.
 * - @ref defaultMaterial is taken from @c meshRenderer.material (instance material).
 *
 * ------------------------------------------------------------
 * Unity Lifecycle Notes
 * ------------------------------------------------------------
 * - Start(): caches renderer/material and initial scale.
 *
 * ------------------------------------------------------------
 * Performance & Allocation Notes
 * ------------------------------------------------------------
 * - Accessing @c meshRenderer.material creates/uses an instanced material in Unity; this is fine for prototype,
 *   but in production you may want a different approach (shared material, MaterialPropertyBlock) depending on use.
 *
 * ------------------------------------------------------------
 * Visual Maps
 * ------------------------------------------------------------
 * @section viz_relationships_obj3d Visual Relationships
 * @dot
 * digraph InteractiveObject3D_Relations {
 *   rankdir=LR;
 *   node [shape=box];
 *
 *   "RaycastManager" -> "InteractiveObject3D" [label="calls hover enter/exit"];
 *   "InteractiveObject3D" -> "Renderer" [label="material color"];
 *   "InteractiveObject3D" -> "Transform" [label="scale feedback"];
 * }
 * @enddot
 */
public class InteractiveObject3D : MonoBehaviour
{
    [Header("Visual Feedback")]
    public Color defaultColor = Color.white;
    public Color hoverColor = Color.yellow;

    [Header("Scene Destination")]
    public string targetScene = "Flower Scene";

    private Renderer meshRenderer;
    private Material defaultMaterial;
    private Vector3 initialScale;

    void Start()
    {
        meshRenderer = GetComponent<Renderer>();
        if (meshRenderer != null)
        {
            defaultMaterial = meshRenderer.material;
            defaultMaterial.color = defaultColor;
        }
        initialScale = transform.localScale;
    }

    public void OnHoverEnter()
    {
        if (defaultMaterial != null)
        {
            defaultMaterial.color = hoverColor;
            transform.localScale = initialScale * 1.05f;
        }
    }

    public void OnHoverExit()
    {
        if (defaultMaterial != null)
        {
            defaultMaterial.color = defaultColor;
            transform.localScale = initialScale;
        }
    }

    public string GetTargetSceneName()
    {
        return targetScene;
    }
}
