/**
 * @file AccessibilityDropdownUI.cs
 * @brief TMP_Dropdown binding for colorblind mode selection.
 *
 * @details
 * Attach to a GameObject with a TMP_Dropdown. Populates options from
 * AccessibilitySettings.ColorblindMode enum and persists the selection.
 *
 * @ingroup ui
 */

using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Dropdown))]
public class AccessibilityDropdownUI : MonoBehaviour
{
    private TMP_Dropdown _dropdown;

    void Awake()
    {
        _dropdown = GetComponent<TMP_Dropdown>();
    }

    void Start()
    {
        _dropdown.ClearOptions();
        _dropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "Normal",
            "Deuteranopia (Red-Green)",
            "Protanopia (Red-Green)",
            "Tritanopia (Blue-Yellow)"
        });

        _dropdown.SetValueWithoutNotify((int)AccessibilitySettings.Mode);
        _dropdown.onValueChanged.AddListener(OnValueChanged);
    }

    void OnDestroy()
    {
        if (_dropdown != null)
            _dropdown.onValueChanged.RemoveListener(OnValueChanged);
    }

    private void OnValueChanged(int index)
    {
        AccessibilitySettings.SetMode((AccessibilitySettings.ColorblindMode)index);
    }
}
