/**
 * @file InputOverrideLoader.cs
 * @brief Awake-only MonoBehaviour that initializes InputRebindManager and loads saved overrides.
 *
 * @details
 * Place in the scene with the InputActionAsset wired in the Inspector.
 * On Awake, calls InputRebindManager.Initialize() then LoadOverrides().
 *
 * @ingroup framework
 */

using UnityEngine;
using UnityEngine.InputSystem;

public class InputOverrideLoader : MonoBehaviour
{
    [Tooltip("The project's InputActionAsset (e.g. InputSystem_Actions).")]
    public InputActionAsset inputActionAsset;

    void Awake()
    {
        if (inputActionAsset == null)
        {
            Debug.LogWarning("[InputOverrideLoader] No InputActionAsset assigned.", this);
            return;
        }

        InputRebindManager.Initialize(inputActionAsset);
        InputRebindManager.LoadOverrides();
    }
}
