using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ItemsRuntimeInventory : MonoBehaviour
{
    [Tooltip("Optional knowledge tracker. If assigned, it will be notified whenever an item is discovered.")]
    public PlayerKnowledgeTracker knowledgeTracker;

    [Header("Debug / Runtime Items")]
    [SerializeField] private List<GameItemDefinition> unlockedItems = new List<GameItemDefinition>();

    private readonly HashSet<string> _unlockedItemIds = new HashSet<string>();

    void Awake()
    {
        // Allow pre-filled inventory for testing
        _unlockedItemIds.Clear();
        foreach (var item in unlockedItems)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
            if (_unlockedItemIds.Add(item.itemId) == false) continue;
        }
    }

    public IReadOnlyList<GameItemDefinition> UnlockedItems => unlockedItems;

    public bool HasItem(GameItemDefinition item)
    {
        if (item == null || string.IsNullOrEmpty(item.itemId)) return false;
        return _unlockedItemIds.Contains(item.itemId);
    }

    public bool HasItemId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        return _unlockedItemIds.Contains(itemId);
    }

    /// <summary>
    /// Add item to the inventory (if not already present) and notify the knowledge tracker.
    /// </summary>
    public void AddItem(GameItemDefinition item)
    {
        if (item == null || string.IsNullOrEmpty(item.itemId)) return;

        if (_unlockedItemIds.Contains(item.itemId))
            return;

        _unlockedItemIds.Add(item.itemId);
        unlockedItems.Add(item);

        if (knowledgeTracker != null)
            knowledgeTracker.MarkItemDiscovered(item);

        Debug.Log($"[ItemsRuntimeInventory] Added item → {item.itemId}", this);
    }

    /// <summary>
    /// Initialize inventory with always-available items (like the ideal photo) for this level.
    /// </summary>
    public void InitializeFromLevel(LevelConfig level)
    {
        unlockedItems.Clear();
        _unlockedItemIds.Clear();

        if (level == null)
            return;

        // Always-add items
        foreach (var item in level.alwaysAvailableItems)
        {
            AddItem(item);
        }
    }
}
