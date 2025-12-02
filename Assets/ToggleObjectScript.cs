using UnityEngine;
using UnityEngine.InputSystem; // Required for the new input system

public class ToggleObjectScript : MonoBehaviour
{
    // Reference the GameObject you want to activate/deactivate in the Inspector
    public GameObject objectToToggle;
    public GameObject objectToToggle2;

    // This function can be called by the PlayerInput component via Unity Events
    public void ToggleObjectActiveState(InputAction.CallbackContext context)
    {
        // Check if the key / action was performed
        if (context.performed)
        {
            if (objectToToggle != null && objectToToggle2 != null)
            {
                bool currentState = objectToToggle.activeSelf;
                bool currentState2 = objectToToggle2.activeSelf;

                objectToToggle.SetActive(!currentState);
                objectToToggle2.SetActive(!currentState2);

                Debug.Log("M pressed. Object 1 active state toggled to: " + !currentState);
                Debug.Log("M pressed. Object 2 active state toggled to: " + !currentState2);
            }
            else
            {
                Debug.LogError("Object(s) to Toggle are not assigned in the Inspector!");
            }
        }
    }
}