/**
 * @file InputRebindManager.cs
 * @brief Static utility for saving/loading InputAction binding overrides.
 *
 * @details
 * Wraps Unity's InputActionAsset JSON override API with PlayerPrefs persistence.
 * Call Initialize() with the project's InputActionAsset before Load/Save.
 *
 * Pattern:
 * - Static utility like TimeScaleManager — no MonoBehaviour, no singleton.
 * - Persistence key: "Iris_InputOverrides"
 *
 * @ingroup framework
 */

using UnityEngine;
using UnityEngine.InputSystem;

public static class InputRebindManager
{
    private const string PREFS_KEY = "Iris_InputOverrides";

    private static InputActionAsset s_asset;

    public static InputActionAsset Asset => s_asset;

    public static void Initialize(InputActionAsset asset)
    {
        s_asset = asset;
    }

    public static void LoadOverrides()
    {
        if (s_asset == null)
        {
            Debug.LogWarning("[InputRebindManager] No InputActionAsset initialized.");
            return;
        }

        string json = PlayerPrefs.GetString(PREFS_KEY, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            s_asset.LoadBindingOverridesFromJson(json);
            Debug.Log("[InputRebindManager] Loaded binding overrides from PlayerPrefs.");
        }
    }

    public static void SaveOverrides()
    {
        if (s_asset == null)
        {
            Debug.LogWarning("[InputRebindManager] No InputActionAsset initialized.");
            return;
        }

        string json = s_asset.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(PREFS_KEY, json);
        PlayerPrefs.Save();
        Debug.Log("[InputRebindManager] Saved binding overrides to PlayerPrefs.");
    }

    public static void ResetAllBindings()
    {
        if (s_asset == null)
        {
            Debug.LogWarning("[InputRebindManager] No InputActionAsset initialized.");
            return;
        }

        s_asset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(PREFS_KEY);
        PlayerPrefs.Save();
        Debug.Log("[InputRebindManager] Reset all bindings to defaults.");
    }
}
