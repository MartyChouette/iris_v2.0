using UnityEngine;
using UnityEngine.SceneManagement;

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