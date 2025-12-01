using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class ItemDiscoveredEvent : UnityEvent<GameItemDefinition> { }

[System.Serializable]
public class InfoLearnedEvent : UnityEvent<InfoBitDefinition> { }

[DisallowMultipleComponent]
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
