using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LevelConfig : MonoBehaviour
{
    [Header("Core")]
    public FlowerTypeDefinition flowerType;

    [Header("Always Available in This Level")]
    [Tooltip("Items that the player always has access to in this level (e.g. ideal photo).")]
    public List<GameItemDefinition> alwaysAvailableItems = new List<GameItemDefinition>();

    [Header("Potential Level Items")]
    [Tooltip("Items that exist in this level's environment and can be discovered/picked up.")]
    public List<GameItemDefinition> levelItems = new List<GameItemDefinition>();

    /// <summary>
    /// Optional helper to initialize a runtime inventory from this config.
    /// </summary>
    public void ApplyToInventory(ItemsRuntimeInventory inventory)
    {
        if (inventory == null) return;
        inventory.InitializeFromLevel(this);
    }
}