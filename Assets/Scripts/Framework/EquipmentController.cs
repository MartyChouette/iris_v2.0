/**
 * @file EquipmentController.cs
 * @brief EquipmentController script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup items
 */

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
/**
 * @class EquipmentController
 * @brief EquipmentController component.
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
public class EquipmentController : MonoBehaviour
{
    [Header("Debug / Runtime Equipment")]
    [SerializeField] private List<GameItemDefinition> equippedItems = new List<GameItemDefinition>();

    public IReadOnlyList<GameItemDefinition> EquippedItems => equippedItems;

    public void Equip(GameItemDefinition item)
    {
        if (item == null || !item.equippable || item.kind != GameItemKind.Equipment)
            return;

        // Simple: one item per slot. Remove any existing in this slot.
        equippedItems.RemoveAll(e => e != null && e.equipmentSlotId == item.equipmentSlotId);
        equippedItems.Add(item);

        Debug.Log($"[EquipmentController] Equipped {item.displayName} in slot '{item.equipmentSlotId}'.", this);

        // Later: actually attach item.modelPrefab to the character's bone for that slot.
    }
}