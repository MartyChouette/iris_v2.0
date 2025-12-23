/**
 * @file ItemsMenuUI.cs
 * @brief ItemsMenuUI script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup items
 */

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
/**
 * @class ItemsMenuUI
 * @brief ItemsMenuUI component.
 * @details
 * Responsibilities:
 * - (Documented) See fields and methods below.
 *
 * Unity lifecycle:
 * - Awake(): cache references / validate setup.
 * - OnEnable()/OnDisable(): hook/unhook events.
 * - Update(): per-frame behavior (if any).
 *
 * Gotchas:
 * - Keep hot paths allocation-free (Update/cuts/spawns).
 * - Prefer event-driven UI updates over per-frame string building.
 *
 * @ingroup items
 */
public class ItemsMenuUI : MonoBehaviour
{
    [Header("Data Sources")]
    [Tooltip("Runtime inventory providing the items for this menu.")]
    public ItemsRuntimeInventory inventory;

    [Tooltip("Optional: controller that actually handles equipping items.")]
    public EquipmentController equipmentController;

    [Header("Wheel UI")]
    [Tooltip("Parent transform for the wheel icon slots (not strictly required but handy).")]
    public RectTransform wheelContainer;

    [Tooltip("Icon images arranged in a row / arc. We'll fill them from inventory items.")]
    public Image[] wheelIcons;

    [Tooltip("Color used for the currently selected item icon.")]
    public Color selectedColor = Color.white;

    [Tooltip("Color used for non-selected item icons.")]
    public Color unselectedColor = new Color(1f, 1f, 1f, 0.4f);

    [Tooltip("Color used when no item exists for that icon slot.")]
    public Color emptyColor = new Color(1f, 1f, 1f, 0.05f);

    [Header("Texts")]
    public TMP_Text titleLabel;
    public TMP_Text descriptionLabel;

    [Header("3D Preview")]
    [Tooltip("Where to spawn the item's 3D model for spinning preview.")]
    public Transform previewRoot;

    [Tooltip("Idle spin speed (degrees per second).")]
    public float previewSpinSpeed = 40f;

    [Tooltip("Rotation speed multiplier when using manual rotation input.")]
    public float manualRotateSpeed = 120f;

    [Header("Inspect View")]
    [Tooltip("Root object for the wheel view (normal item list).")]
    public GameObject wheelViewRoot;

    [Tooltip("Root object for the close-up inspect view (black background).")]
    public GameObject inspectViewRoot;

    [Tooltip("Image used in inspect view for 2D cards (e.g. photos) if no 3D model exists.")]
    public Image inspectImage2D;

    [Tooltip("If true, items with no 3D model will be shown as a 2D card using their icon.")]
    public bool fallbackTo2DInspectForModelLessItems = true;

    [Header("Button Prompts")]
    [Tooltip("UI root for the Inspect prompt (e.g. 'X Inspect'). Always shown when an item exists.")]
    public GameObject inspectPromptRoot;

    [Tooltip("UI root for the Equip prompt (e.g. 'Y Equip'). Shown only for equippable items.")]
    public GameObject equipPromptRoot;

    [Tooltip("Optional text label for the equip prompt (we can change wording depending on state).")]
    public TMP_Text equipPromptLabel;

    [Header("Input (New Input System)")]
    [Tooltip("Move to previous item (left on carousel).")]
    public InputActionReference previousItemAction;

    [Tooltip("Move to next item (right on carousel).")]
    public InputActionReference nextItemAction;

    [Tooltip("Open/close inspect mode for current item.")]
    public InputActionReference inspectAction;

    [Tooltip("Equip current item (if equippable).")]
    public InputActionReference equipAction;

    [Tooltip("2D rotation input (Vector2) for spinning the preview manually.")]
    public InputActionReference rotateAction;

    [Header("Debug")]
    public bool debugLogs = false;

    readonly List<GameItemDefinition> _items = new List<GameItemDefinition>();
    int _currentIndex = 0;
    GameObject _currentPreviewInstance;
    bool _inInspectView = false;

    void OnEnable()
    {
        BuildItemListFromInventory();
        RebuildWheelIcons();
        SetIndex(0);
        ShowWheelView();

        // Subscribe + enable input actions
        if (previousItemAction != null && previousItemAction.action != null)
        {
            previousItemAction.action.performed += OnPreviousItemPerformed;
            previousItemAction.action.Enable();
        }

        if (nextItemAction != null && nextItemAction.action != null)
        {
            nextItemAction.action.performed += OnNextItemPerformed;
            nextItemAction.action.Enable();
        }

        if (inspectAction != null && inspectAction.action != null)
        {
            inspectAction.action.performed += OnInspectPerformed;
            inspectAction.action.Enable();
        }

        if (equipAction != null && equipAction.action != null)
        {
            equipAction.action.performed += OnEquipPerformed;
            equipAction.action.Enable();
        }

        if (rotateAction != null && rotateAction.action != null)
        {
            rotateAction.action.Enable();
        }
    }

    void OnDisable()
    {
        ClearPreviewInstance();

        if (previousItemAction != null && previousItemAction.action != null)
        {
            previousItemAction.action.performed -= OnPreviousItemPerformed;
            previousItemAction.action.Disable();
        }

        if (nextItemAction != null && nextItemAction.action != null)
        {
            nextItemAction.action.performed -= OnNextItemPerformed;
            nextItemAction.action.Disable();
        }

        if (inspectAction != null && inspectAction.action != null)
        {
            inspectAction.action.performed -= OnInspectPerformed;
            inspectAction.action.Disable();
        }

        if (equipAction != null && equipAction.action != null)
        {
            equipAction.action.performed -= OnEquipPerformed;
            equipAction.action.Disable();
        }

        if (rotateAction != null && rotateAction.action != null)
        {
            rotateAction.action.Disable();
        }
    }

    void Update()
    {
        UpdatePreviewRotation();
    }

    // ─────────────────── Input callbacks ───────────────────

    void OnPreviousItemPerformed(InputAction.CallbackContext ctx)
    {
        if (_items.Count == 0) return;
        SetIndex((_currentIndex - 1 + _items.Count) % _items.Count);
    }

    void OnNextItemPerformed(InputAction.CallbackContext ctx)
    {
        if (_items.Count == 0) return;
        SetIndex((_currentIndex + 1) % _items.Count);
    }

    void OnInspectPerformed(InputAction.CallbackContext ctx)
    {
        if (_items.Count == 0) return;
        if (!_inInspectView) EnterInspectView();
        else ExitInspectView();
    }

    void OnEquipPerformed(InputAction.CallbackContext ctx)
    {
        TryEquipCurrentItem();
    }

    // ─────────────────── Preview rotation ───────────────────

    void UpdatePreviewRotation()
    {
        if (_currentPreviewInstance == null)
            return;

        // Idle spin
        _currentPreviewInstance.transform.Rotate(
            Vector3.up,
            previewSpinSpeed * Time.unscaledDeltaTime,
            Space.World);

        if (rotateAction == null || rotateAction.action == null)
            return;

        Vector2 rotInput = rotateAction.action.ReadValue<Vector2>();
        if (rotInput.sqrMagnitude < 0.001f)
            return;

        // Horizontal → yaw, Vertical → pitch
        float yaw = rotInput.x * manualRotateSpeed * Time.unscaledDeltaTime;
        float pitch = rotInput.y * manualRotateSpeed * Time.unscaledDeltaTime;

        _currentPreviewInstance.transform.Rotate(Vector3.up, yaw, Space.World);
        _currentPreviewInstance.transform.Rotate(Vector3.right, -pitch, Space.World);
    }

    // ─────────────────── Data setup ───────────────────

    void BuildItemListFromInventory()
    {
        _items.Clear();

        if (inventory == null)
        {
            Debug.LogWarning("[ItemsMenuUI] No ItemsRuntimeInventory assigned.", this);
            return;
        }

        var unlocked = inventory.UnlockedItems;
        for (int i = 0; i < unlocked.Count; i++)
        {
            var item = unlocked[i];
            if (item == null) continue;
            _items.Add(item);
        }

        if (debugLogs)
            Debug.Log($"[ItemsMenuUI] Loaded {_items.Count} items from inventory.", this);
    }

    void RebuildWheelIcons()
    {
        if (wheelIcons == null || wheelIcons.Length == 0)
            return;

        int maxIcons = wheelIcons.Length;

        for (int i = 0; i < maxIcons; i++)
        {
            var icon = wheelIcons[i];
            if (icon == null) continue;

            if (i < _items.Count)
            {
                var item = _items[i];
                icon.gameObject.SetActive(true);
                icon.sprite = item.icon;
                icon.color = (i == _currentIndex) ? selectedColor : unselectedColor;
            }
            else
            {
                icon.gameObject.SetActive(true);
                icon.sprite = null;
                icon.color = emptyColor;
            }
        }
    }

    void RefreshWheelHighlight()
    {
        if (wheelIcons == null || wheelIcons.Length == 0)
            return;

        int maxIcons = wheelIcons.Length;
        for (int i = 0; i < maxIcons; i++)
        {
            var icon = wheelIcons[i];
            if (icon == null) continue;

            if (i < _items.Count)
            {
                icon.color = (i == _currentIndex) ? selectedColor : unselectedColor;
            }
            else
            {
                icon.color = emptyColor;
            }
        }
    }

    void SetIndex(int newIndex)
    {
        if (_items.Count == 0)
            return;

        _currentIndex = Mathf.Clamp(newIndex, 0, _items.Count - 1);
        RefreshWheelHighlight();
        RefreshCurrentItemUI();
    }

    // ─────────────────── UI refresh ───────────────────

    void RefreshCurrentItemUI()
    {
        if (_items.Count == 0)
        {
            ClearPreviewInstance();
            if (titleLabel != null) titleLabel.text = "";
            if (descriptionLabel != null) descriptionLabel.text = "";
            UpdatePrompts(null);
            return;
        }

        var item = _items[_currentIndex];

        if (titleLabel != null)
            titleLabel.text = item.displayName;

        if (descriptionLabel != null)
            descriptionLabel.text = item.description;

        SpawnPreviewForItem(item);
        UpdatePrompts(item);
    }

    void SpawnPreviewForItem(GameItemDefinition item)
    {
        ClearPreviewInstance();

        if (previewRoot == null)
            return;

        if (item.modelPrefab != null)
        {
            _currentPreviewInstance = Instantiate(item.modelPrefab, previewRoot);
            _currentPreviewInstance.transform.localPosition = Vector3.zero;
            _currentPreviewInstance.transform.localRotation = Quaternion.identity;
            _currentPreviewInstance.transform.localScale = Vector3.one;
        }
        else
        {
            _currentPreviewInstance = null;
        }
    }

    void ClearPreviewInstance()
    {
        if (_currentPreviewInstance != null)
        {
            Destroy(_currentPreviewInstance);
            _currentPreviewInstance = null;
        }
    }

    void UpdatePrompts(GameItemDefinition item)
    {
        if (inspectPromptRoot != null)
            inspectPromptRoot.SetActive(item != null);

        bool canEquip = false;

        if (item != null &&
            item.kind == GameItemKind.Equipment &&
            item.equippable)
        {
            canEquip = true;
        }

        if (equipPromptRoot != null)
            equipPromptRoot.SetActive(canEquip);

        if (equipPromptLabel != null)
            equipPromptLabel.text = canEquip ? "Equip" : "";
    }

    // ─────────────────── Inspect view ───────────────────

    void ShowWheelView()
    {
        _inInspectView = false;

        if (wheelViewRoot != null)
            wheelViewRoot.SetActive(true);

        if (inspectViewRoot != null)
            inspectViewRoot.SetActive(false);

        if (inspectImage2D != null)
            inspectImage2D.gameObject.SetActive(false);
    }

    void EnterInspectView()
    {
        if (_items.Count == 0)
            return;

        var item = _items[_currentIndex];
        _inInspectView = true;

        if (wheelViewRoot != null)
            wheelViewRoot.SetActive(false);

        if (inspectViewRoot != null)
            inspectViewRoot.SetActive(true);

        if (item.modelPrefab == null &&
            fallbackTo2DInspectForModelLessItems &&
            inspectImage2D != null)
        {
            inspectImage2D.gameObject.SetActive(true);
            inspectImage2D.sprite = item.icon;
        }
        else if (inspectImage2D != null)
        {
            inspectImage2D.gameObject.SetActive(false);
        }
    }

    void ExitInspectView()
    {
        _inInspectView = false;

        if (inspectViewRoot != null)
            inspectViewRoot.SetActive(false);

        if (wheelViewRoot != null)
            wheelViewRoot.SetActive(true);

        if (inspectImage2D != null)
            inspectImage2D.gameObject.SetActive(false);
    }

    // ─────────────────── Equip ───────────────────

    void TryEquipCurrentItem()
    {
        if (_items.Count == 0)
            return;

        var item = _items[_currentIndex];

        if (item == null ||
            item.kind != GameItemKind.Equipment ||
            !item.equippable)
        {
            if (debugLogs)
                Debug.Log("[ItemsMenuUI] TryEquipCurrentItem on non-equippable item.", this);
            return;
        }

        if (equipmentController == null)
        {
            if (debugLogs)
                Debug.LogWarning("[ItemsMenuUI] No EquipmentController assigned; cannot equip.", this);
            return;
        }

        equipmentController.Equip(item);
    }

    // Optional UI button hooks if you also want clickable arrows
    public void UI_NextItem() => SetIndex((_currentIndex + 1) % Mathf.Max(1, _items.Count));
    public void UI_PreviousItem() => SetIndex((_currentIndex - 1 + Mathf.Max(1, _items.Count)) % Mathf.Max(1, _items.Count));
    public void UI_InspectToggle()
    {
        if (_items.Count == 0) return;
        if (!_inInspectView) EnterInspectView();
        else ExitInspectView();
    }
    public void UI_EquipCurrent() => TryEquipCurrentItem();
}
