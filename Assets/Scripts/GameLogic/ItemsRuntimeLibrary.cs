/**
 * @file ItemsRuntimeLibrary.cs
 * @brief ItemsRuntimeLibrary script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup items
 */

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
/**
 
 * @details
 * Intent:
 * - Runtime inventory of unlocked/available @ref GameItemDefinition objects.
 * - Designed for "entitlement" style progression: items are granted once, persist in-session,
 *   and can optionally feed discovery into @ref PlayerKnowledgeTracker.
 *
 * Data model:
 * - unlockedItems: inspector-visible runtime list (easy debugging).
 * - _unlockedItemIds: HashSet mirror for fast membership tests.
 *
 * Key behaviors:
 * - Awake() seeds the HashSet from any pre-filled unlockedItems (debug-friendly).
 * - AddItem() is idempotent by itemId (safe to call from multiple discovery points).
 * - InitializeFromLevel(LevelConfig) clears runtime state then always-adds LevelConfig.alwaysAvailableItems.
 *
 * Invariants:
 * - If an itemId is in _unlockedItemIds, the corresponding item should exist in unlockedItems.
 * - unlockedItems is the display/debug list; the HashSet is the source of truth for "has item?" checks.
 *
 * Side effects / hooks:
 * - If knowledgeTracker is assigned, AddItem() calls knowledgeTracker.MarkItemDiscovered(item).
 *   This is the bridge from "owned" to "known." (Important for narrative gating and UI.)
 *
 * Failure modes / gotchas:
 * - Null items or empty itemIds are ignored (safe but can hide authoring mistakes).
 * - InitializeFromLevel() hard-resets inventory; do not call mid-session unless you want a wipe.
 *
 * Performance:
 * - All membership queries are O(1) via HashSet.
 * - Avoid per-frame polling in UI; prefer event-driven refresh on AddItem() / InitializeFromLevel().
 

 * @file ItemsRuntimeLibrary.cs
 * @brief Runtime registry for meaningful item instances (lookup, identity, consistency).
 *
 * @details
 * ItemsRuntimeLibrary maintains a runtime-accessible registry of item instances that matter
 * to gameplay/narrative systems. This is not necessarily a player inventory. In Iris, items
 * can be evaluated, inspected, and recontextualized; identity consistency matters.
 *
 * Responsibilities:
 * - Track active item instances and provide lookup by ID/type/role (as implemented).
 * - Support runtime spawn/replace flows while preserving identity rules where needed.
 * - Offer a single place to query "where is X?" / "what instance is X right now?"
 *
 * Non-responsibilities:
 * - Should not decide narrative meaning (knowledge system).
 * - Should not implement UI presentation for items.
 *
 * Design constraints:
 * - Registry operations should be deterministic and robust to scene reloads (if applicable).
 * - Avoid leaking references across destroyed scenes: clear/unregister responsibly.
 *
 * Integration points:
 * - Flower systems: may register stems/leaves/petals/tools as runtime items.
 * - InspectableObject: may reference items or retrieve current instance to inspect.
 * - LevelConfig: may constrain which items exist in a given variant.
 *
 * @see LevelConfig
 * @see InspectableObject
 

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
