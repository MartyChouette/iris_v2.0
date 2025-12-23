using UnityEngine;
using UnityEngine.SceneManagement;

/**
 * @file RaycastManager.cs
 * @class RaycastManager
 * @brief Unified prototype manager that raycasts from the mouse cursor to drive hover + click interactions on tagged 2D/3D objects.
 *
 * @details
 * RaycastManager performs selection/hover detection and routes hover enter/exit events to interactive targets through a shared
 * abstraction (e.g., an IInteractive-style interface). It enforces a single "current" focus target to ensure consistent enter/exit
 * behavior and prevent duplicated input logic.
 *
 * This script currently exists to support a siloed prototype: a 2D/3D "living room" scene where the player hovers over
 * interactables and receives visual feedback (2D sprite swap / 3D color+scale). On click, it can trigger a scene load, but
 * intentionally restricts scene loading to 3D objects only.
 *
 * @note This is not part of Iris's core runtime systems. It is kept separate while the team explores whether this interaction
 *       model belongs in the final game and what form it should take.
 *
 * ------------------------------------------------------------
 * Responsibilities
 * ------------------------------------------------------------
 * - Cast a 3D Physics ray from the active camera through the current mouse position.
 * - Detect objects tagged with @ref interactiveTag and route hover enter/exit transitions.
 * - Determine whether the hovered object is a 2D Sprite interactable or a 3D Object interactable.
 * - On left click, optionally load a target scene (3D objects only).
 * - Surface target metadata needed for scene transitions or contextual actions (as implemented).
 *
 * ------------------------------------------------------------
 * Non-Responsibilities
 * ------------------------------------------------------------
 * - Does not handle 2D physics raycasts (Physics2D).
 * - Does not manage UI focus / EventSystem blocking.
 * - Does not validate that target scenes are in Build Settings.
 * - Does not integrate with Iris knowledge, progression, or item systems (prototype-only).
 * - Does not decide what "inspect" means for a target (target scripts own meaning).
 *
 * ------------------------------------------------------------
 * Key Data & Invariants
 * ------------------------------------------------------------
 * - Hover state is represented by @ref currentInteractiveTarget and @ref currentHoverType.
 * - Only GameObjects with @ref interactiveTag are considered candidates.
 * - Scene loading is only allowed when the hovered target type is @c Object3D.
 * - Hover transitions must be robust under rapid camera motion and object enable/disable.
 *
 * ------------------------------------------------------------
 * Unity Lifecycle Notes
 * ------------------------------------------------------------
 * - Update(): Performs raycast, resolves hovered target, handles enter/exit transitions, and checks click-to-load.
 *
 * ------------------------------------------------------------
 * Performance & Allocation Notes
 * ------------------------------------------------------------
 * - This prototype allocates per-hover due to wrapper creation (@c new InteractiveObject3DWrapper / @c new InteractiveSpriteWrapper).
 *   For production, prefer caching the actual component reference and comparing instance IDs.
 * - Uses @c Camera.main each frame; for production, cache the camera reference.
 * - Avoid allocations every frame (wrappers/boxing can be a hidden perf footgun).
 *
 * ------------------------------------------------------------
 * Visual Maps
 * ------------------------------------------------------------
 * @section viz_relationships_raycast Visual Relationships
 * @dot
 * digraph RaycastManager_Relations {
 *   rankdir=LR;
 *   node [shape=box];
 *
 *   "RaycastManager" -> "InteractiveSprite"   [label="wraps + forwards hover"];
 *   "RaycastManager" -> "InteractiveObject3D" [label="wraps + forwards hover"];
 *   "RaycastManager" -> "SceneManager"        [label="LoadScene() on click (3D only)"];
 * }
 * @enddot
 *
 * ------------------------------------------------------------
 * Integration Points
 * ------------------------------------------------------------
 * - Interactive targets (e.g., sprite/object scripts) receive hover enter/exit and (optionally) provide scene metadata.
 * - Scene transition systems (if used) may read target scene name/type from the current target.
 *
 * @see InspectableObject
 */

public class RaycastManager : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("The Tag used by all interactive objects (3D or 2D) in the scene.")]
    public string interactiveTag = "InteractiveObject";

    [Tooltip("The maximum distance the raycast will check.")]
    public float maxDistance = 100f;

    // Abstract interface to manage the active target
    private IInteractive currentInteractiveTarget = null;

    // Enum to identify the type of object being hovered over
    private enum InteractiveType { None, Sprite2D, Object3D }
    private InteractiveType currentHoverType = InteractiveType.None;

    // Interface to allow both 2D and 3D scripts to be managed equally
    private interface IInteractive
    {
        void OnHoverEnter();
        void OnHoverExit();
        string GetTargetSceneName();
        InteractiveType GetTargetType(); // New method
    }

    // Wrappers to adapt the 2D/3D classes to the interface
    private class InteractiveSpriteWrapper : IInteractive
    {
        private InteractiveSprite script;
        public InteractiveSpriteWrapper(InteractiveSprite s) { script = s; }
        public void OnHoverEnter() { script.OnHoverEnter(); }
        public void OnHoverExit() { script.OnHoverExit(); }
        public string GetTargetSceneName() { return script.GetTargetSceneName(); }
        public InteractiveType GetTargetType() { return InteractiveType.Sprite2D; } // Identify as 2D
    }

    private class InteractiveObject3DWrapper : IInteractive
    {
        private InteractiveObject3D script;
        public InteractiveObject3DWrapper(InteractiveObject3D s) { script = s; }
        public void OnHoverEnter() { script.OnHoverEnter(); }
        public void OnHoverExit() { script.OnHoverExit(); }
        public string GetTargetSceneName() { return script.GetTargetSceneName(); }
        public InteractiveType GetTargetType() { return InteractiveType.Object3D; } // Identify as 3D
    }

    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxDistance))
        {
            GameObject hitObject = hit.collider.gameObject;
            IInteractive hitTarget = null;

            if (hitObject.CompareTag(interactiveTag))
            {
                // Try to get the 3D script first (Scissors)
                InteractiveObject3D hit3D = hitObject.GetComponent<InteractiveObject3D>();

                // Try to get the 2D script
                InteractiveSprite hit2D = hitObject.GetComponent<InteractiveSprite>();

                if (hit3D != null)
                {
                    hitTarget = new InteractiveObject3DWrapper(hit3D);
                }
                else if (hit2D != null)
                {
                    hitTarget = new InteractiveSpriteWrapper(hit2D);
                }

                if (hitTarget != null)
                {
                    // Update hover state if new target
                    if (currentInteractiveTarget == null || currentInteractiveTarget.GetHashCode() != hitTarget.GetHashCode())
                    {
                        if (currentInteractiveTarget != null)
                            currentInteractiveTarget.OnHoverExit();

                        currentInteractiveTarget = hitTarget;
                        currentHoverType = hitTarget.GetTargetType();
                        currentInteractiveTarget.OnHoverEnter();
                    }

                    // --- SCENE CHANGE LOGIC (Only runs if a click occurs) ---
                    if (Input.GetMouseButtonDown(0))
                    {
                        // ONLY load scene if the currently hovered object is a 3D object
                        if (currentHoverType == InteractiveType.Object3D)
                        {
                            LoadTargetScene(currentInteractiveTarget.GetTargetSceneName());
                        }
                    }

                    return;
                }
            }
        }

        // Handle Mouse Exit
        HandleMouseExit();
    }

    private void HandleMouseExit()
    {
        if (currentInteractiveTarget != null)
        {
            currentInteractiveTarget.OnHoverExit();
            currentInteractiveTarget = null;
            currentHoverType = InteractiveType.None;
        }
    }

    private void LoadTargetScene(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError("The 3D interactive object has an empty Target Scene Name!");
        }
    }
}
