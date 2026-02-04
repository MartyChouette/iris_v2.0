/**
 * @file QualityDropdownUI.cs
 * @brief TMP_Dropdown binding for quality preset selection.
 *
 * @details
 * Attach to a GameObject with a TMP_Dropdown. Populates options from
 * IrisQualityManager.presets and applies the selected preset at runtime.
 *
 * @ingroup ui
 */

using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Dropdown))]
public class QualityDropdownUI : MonoBehaviour
{
    private TMP_Dropdown _dropdown;

    void Awake()
    {
        _dropdown = GetComponent<TMP_Dropdown>();
    }

    void Start()
    {
        var manager = IrisQualityManager.Instance;
        if (manager == null || manager.presets == null || manager.presets.Length == 0)
        {
            Debug.LogWarning("[QualityDropdownUI] No IrisQualityManager or presets found in scene.");
            return;
        }

        _dropdown.ClearOptions();
        var options = new List<string>();
        foreach (var preset in manager.presets)
            options.Add(preset != null ? preset.displayName : "???");
        _dropdown.AddOptions(options);

        _dropdown.SetValueWithoutNotify(manager.ActivePresetIndex);
        _dropdown.onValueChanged.AddListener(OnValueChanged);
    }

    void OnDestroy()
    {
        if (_dropdown != null)
            _dropdown.onValueChanged.RemoveListener(OnValueChanged);
    }

    private void OnValueChanged(int index)
    {
        var manager = IrisQualityManager.Instance;
        if (manager != null)
            manager.ApplyPreset(index);
    }
}
