using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
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