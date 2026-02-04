/**
 * @file InputRebindUI.cs
 * @brief UI panel for interactive input rebinding.
 *
 * @details
 * Shows rebindable actions from the InputActionAsset and lets the player
 * perform interactive rebinding via Unity's PerformInteractiveRebinding() API.
 *
 * Scope:
 * - Only InputActionReference-based actions (CuttingPlaneController,
 *   PauseMenuController, ItemsMenuUI).
 * - Excludes inline InputActions (SimpleTestCharacter, MarkerController).
 *
 * Cancel:
 * - Pressing Escape cancels an active rebind operation.
 *
 * @ingroup ui
 */

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InputRebindUI : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("The project's InputActionAsset (same one wired to InputOverrideLoader).")]
    public InputActionAsset inputActionAsset;

    [Header("Rebindable Actions")]
    [Tooltip("Action names to show for rebinding (must match names in the InputActionAsset).")]
    public string[] rebindableActionNames = { "Cut", "MoveY", "Pause" };

    [Header("UI Template")]
    [Tooltip("Parent transform for spawned rebind rows.")]
    public Transform rowContainer;

    [Tooltip("Prefab for a single rebind row. Must have a TMP_Text child named 'ActionLabel' and a Button child named 'BindButton' with a TMP_Text child named 'BindingLabel'.")]
    public GameObject rowPrefab;

    [Header("Reset Button")]
    [Tooltip("Button that resets all bindings to defaults.")]
    public Button resetButton;

    [Header("Listening Overlay")]
    [Tooltip("Optional panel shown while waiting for input during rebind.")]
    public GameObject listeningOverlay;

    [Header("Debug")]
    public bool debugLogs = true;

    private readonly List<RebindRow> _rows = new List<RebindRow>();
    private InputActionRebindingExtensions.RebindingOperation _activeRebind;

    private class RebindRow
    {
        public InputAction action;
        public TMP_Text bindingLabel;
        public Button button;
    }

    void Start()
    {
        if (inputActionAsset == null)
        {
            Debug.LogWarning("[InputRebindUI] No InputActionAsset assigned.", this);
            return;
        }

        InputRebindManager.Initialize(inputActionAsset);

        if (rowPrefab != null && rowContainer != null)
            BuildRows();

        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);

        if (listeningOverlay != null)
            listeningOverlay.SetActive(false);
    }

    void OnDestroy()
    {
        CleanupActiveRebind();

        if (resetButton != null)
            resetButton.onClick.RemoveListener(OnResetClicked);
    }

    private void BuildRows()
    {
        foreach (string actionName in rebindableActionNames)
        {
            InputAction action = inputActionAsset.FindAction(actionName);
            if (action == null)
            {
                if (debugLogs)
                    Debug.LogWarning($"[InputRebindUI] Action '{actionName}' not found in asset.", this);
                continue;
            }

            GameObject row = Instantiate(rowPrefab, rowContainer);

            var actionLabel = row.transform.Find("ActionLabel")?.GetComponent<TMP_Text>();
            if (actionLabel != null)
                actionLabel.SetText(actionName);

            var bindButton = row.transform.Find("BindButton")?.GetComponent<Button>();
            var bindingLabel = row.transform.Find("BindButton/BindingLabel")?.GetComponent<TMP_Text>();

            if (bindButton == null || bindingLabel == null)
            {
                if (debugLogs)
                    Debug.LogWarning($"[InputRebindUI] Row prefab missing BindButton or BindingLabel for '{actionName}'.", this);
                continue;
            }

            var rebindRow = new RebindRow
            {
                action = action,
                bindingLabel = bindingLabel,
                button = bindButton
            };

            UpdateBindingLabel(rebindRow);

            // Capture for closure
            var capturedRow = rebindRow;
            bindButton.onClick.AddListener(() => StartRebind(capturedRow));

            _rows.Add(rebindRow);
        }
    }

    private void UpdateBindingLabel(RebindRow row)
    {
        if (row.action.bindings.Count > 0)
        {
            string display = InputControlPath.ToHumanReadableString(
                row.action.bindings[0].effectivePath,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
            row.bindingLabel.SetText(display);
        }
        else
        {
            row.bindingLabel.SetText("---");
        }
    }

    private void StartRebind(RebindRow row)
    {
        CleanupActiveRebind();

        row.action.Disable();
        row.bindingLabel.SetText("...");

        if (listeningOverlay != null)
            listeningOverlay.SetActive(true);

        _activeRebind = row.action.PerformInteractiveRebinding(0)
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(op =>
            {
                OnRebindComplete(row);
                op.Dispose();
                _activeRebind = null;
            })
            .OnCancel(op =>
            {
                OnRebindCancelled(row);
                op.Dispose();
                _activeRebind = null;
            })
            .Start();

        if (debugLogs)
            Debug.Log($"[InputRebindUI] Listening for rebind on '{row.action.name}'...");
    }

    private void OnRebindComplete(RebindRow row)
    {
        row.action.Enable();
        UpdateBindingLabel(row);
        InputRebindManager.SaveOverrides();

        if (listeningOverlay != null)
            listeningOverlay.SetActive(false);

        if (debugLogs)
            Debug.Log($"[InputRebindUI] Rebind complete for '{row.action.name}'.");
    }

    private void OnRebindCancelled(RebindRow row)
    {
        row.action.Enable();
        UpdateBindingLabel(row);

        if (listeningOverlay != null)
            listeningOverlay.SetActive(false);

        if (debugLogs)
            Debug.Log($"[InputRebindUI] Rebind cancelled for '{row.action.name}'.");
    }

    private void OnResetClicked()
    {
        InputRebindManager.ResetAllBindings();

        foreach (var row in _rows)
            UpdateBindingLabel(row);

        if (debugLogs)
            Debug.Log("[InputRebindUI] All bindings reset to defaults.");
    }

    private void CleanupActiveRebind()
    {
        if (_activeRebind != null)
        {
            _activeRebind.Cancel();
            _activeRebind.Dispose();
            _activeRebind = null;
        }
    }
}
