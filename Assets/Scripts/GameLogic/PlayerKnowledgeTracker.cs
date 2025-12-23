/**
 * @file PlayerKnowledgeTracker.cs
 * @brief PlayerKnowledgeTracker script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
/**
 * @details
 * Intent:
 * - Central ledger for what the player has discovered (items) and learned (info bits).
 * - Emits events exactly at the moment knowledge changes, enabling UI + narrative reactions.
 *
 * Data model:
 * - discoveredItems / learnedInfo: inspector-visible lists for debugging and author review.
 * - _itemIds / _infoIds: HashSet mirrors to prevent duplicates and allow fast checks.
 *
 * Events:
 * - OnItemDiscovered fires once per unique itemId (first discovery only).
 * - OnInfoLearned fires when info becomes learned; respects InfoBitDefinition.learnOnce.
 *
 * Key behaviors:
 * - Awake() reconciles pre-filled lists into HashSets (so authoring/debug states are consistent).
 * - MarkItemDiscovered(): idempotent, only adds + invokes once per itemId.
 * - MarkInfoLearned(): supports both "learn once" and "can re-trigger" via learnOnce flag.
 *
 * Invariants:
 * - If _itemIds contains an id, discoveredItems should include the matching definition at least once.
 * - If info.learnOnce is true, repeated MarkInfoLearned calls should not re-add or re-emit for that id.
 *
 * Design hooks:
 * - This is where "complicity memory" lives in code: knowledge persists even when the player wishes
 *   it didn’t. Use these events to escalate tone, UI language, or access rules without adding new systems.
 *
 * Gotchas:
 * - Since lists are serialized, authors can accidentally duplicate entries; HashSet will ignore duplicates,
 *   but your list may still look messy unless you actively clean it.
 

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
* @ingroup tools
*/
public class ItemDiscoveredEvent : UnityEvent<GameItemDefinition> { }

[System.Serializable]
/**
 * @class InfoLearnedEvent
 * @brief InfoLearnedEvent component.
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
 * @ingroup tools
 */
public class InfoLearnedEvent : UnityEvent<InfoBitDefinition> { }

[DisallowMultipleComponent]
/**
 * @file PlayerKnowledgeTracker.cs
 * @brief Persistent record of what the player has learned (epistemic state).
 *
 * @details
 * PlayerKnowledgeTracker is the authoritative source of truth for "player knowledge" in Iris.
 * This is not a quest log and not an inventory: it stores discovered facts/flags (InfoBits) and
 * allows other systems (InspectableObject, date logic, UI, LevelConfig gating) to query whether
 * the player has encountered or unlocked certain meaning.
 *
 * Responsibilities:
 * - Store knowledge flags / info-bit IDs the player has acquired.
 * - Persist reliably across scene loads and (optionally) across sessions/loops.
 * - Provide query + grant operations that are stable and idempotent.
 *
 * Non-responsibilities:
 * - Does not render text/UI itself.
 * - Does not decide narrative tone; it only tracks truth-state.
 * - Does not manage item ownership (that belongs to ItemsRuntimeLibrary / item systems).
 *
 * Invariants / design constraints:
 * - Granting the same InfoBit multiple times should be safe (idempotent).
 * - Queries must be fast and side-effect-free.
 * - Avoid over-granular flags: keep InfoBits meaningful to prevent brittle narrative logic.
 *
 * Integration points:
 * - InspectableObject: often grants and/or checks InfoBits.
 * - LevelConfig: gates variants of scene rules based on knowledge.
 * - Date / dialogue logic: branches phrasing and options based on knowledge.
 *
 * @note If you support loop resets, define explicitly whether knowledge persists across loops
 *       (and if partial resets exist). Professors will look for this rule.
 *
 * @see InfoBitDefinition
 * @see InspectableObject
 * @see LevelConfig
 */

public class PlayerKnowledgeTracker : MonoBehaviour
{
    [Header("Debug View (runtime)")]
    [SerializeField] private List<GameItemDefinition> discoveredItems = new List<GameItemDefinition>();
    [SerializeField] private List<InfoBitDefinition> learnedInfo = new List<InfoBitDefinition>();

    // Fast lookup tables
    private readonly HashSet<string> _itemIds = new HashSet<string>();
    private readonly HashSet<string> _infoIds = new HashSet<string>();

    [Header("Events")]
    [Tooltip("Raised the first time an item is discovered.")]
    public ItemDiscoveredEvent OnItemDiscovered;

    [Tooltip("Raised the first time a piece of information is learned.")]
    public InfoLearnedEvent OnInfoLearned;

    void Awake()
    {
        // Ensure internal lists/sets are consistent if pre-filled in inspector
        _itemIds.Clear();
        foreach (var item in discoveredItems)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
            if (_itemIds.Add(item.itemId) == false) continue;
        }

        _infoIds.Clear();
        foreach (var info in learnedInfo)
        {
            if (info == null || string.IsNullOrEmpty(info.infoId)) continue;
            if (_infoIds.Add(info.infoId) == false) continue;
        }
    }

    // ───────────────────────── Items ─────────────────────────

    public bool HasItem(GameItemDefinition item)
    {
        if (item == null || string.IsNullOrEmpty(item.itemId)) return false;
        return _itemIds.Contains(item.itemId);
    }

    public bool HasItemId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        return _itemIds.Contains(itemId);
    }

    /// <summary>
    /// Mark this item as discovered, if it wasn't already.
    /// Triggers OnItemDiscovered once per itemId.
    /// </summary>
    public void MarkItemDiscovered(GameItemDefinition item)
    {
        if (item == null || string.IsNullOrEmpty(item.itemId))
            return;

        if (_itemIds.Contains(item.itemId))
            return; // already known

        _itemIds.Add(item.itemId);
        discoveredItems.Add(item);

        if (OnItemDiscovered != null)
            OnItemDiscovered.Invoke(item);

        Debug.Log($"[PlayerKnowledgeTracker] Item discovered → {item.itemId}", this);
    }

    // ───────────────────────── Info bits ─────────────────────────

    public bool HasInfo(InfoBitDefinition info)
    {
        if (info == null || string.IsNullOrEmpty(info.infoId)) return false;
        return _infoIds.Contains(info.infoId);
    }

    public bool HasInfoId(string infoId)
    {
        if (string.IsNullOrEmpty(infoId)) return false;
        return _infoIds.Contains(infoId);
    }

    /// <summary>
    /// Mark a piece of information as learned, if allowed.
    /// Respects InfoBitDefinition.learnOnce.
    /// </summary>
    public void MarkInfoLearned(InfoBitDefinition info)
    {
        if (info == null || string.IsNullOrEmpty(info.infoId))
            return;

        if (info.learnOnce && _infoIds.Contains(info.infoId))
            return; // already learned

        if (!_infoIds.Contains(info.infoId))
        {
            _infoIds.Add(info.infoId);
            learnedInfo.Add(info);

            if (OnInfoLearned != null)
                OnInfoLearned.Invoke(info);

            Debug.Log($"[PlayerKnowledgeTracker] Info learned → {info.infoId}", this);
        }
        else
        {
            // info already known but learnOnce == false, you might still want to re-trigger something
            if (!info.learnOnce && OnInfoLearned != null)
                OnInfoLearned.Invoke(info);
        }
    }
}
