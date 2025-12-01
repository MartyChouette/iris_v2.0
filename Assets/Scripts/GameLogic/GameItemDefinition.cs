using UnityEngine;

public enum GameItemKind
{
    InfoOnly,   // photo, brochure, lore objects
    Equipment   // worn/held on the character
}

[CreateAssetMenu(menuName = "Game/Item Definition")]
public class GameItemDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique ID, used for save/knowledge tracking. e.g. 'ideal_photo_flower01'")]
    public string itemId;

    [Tooltip("Display name shown in UI.")]
    public string displayName;

    [TextArea]
    [Tooltip("Short description shown in the Items menu.")]
    public string description;

    [Header("Kind")]
    [Tooltip("Info-only items cannot be equipped (photo, brochure, lore objects).")]
    public GameItemKind kind = GameItemKind.InfoOnly;

    [Tooltip("If true, this item is always present for this level (e.g. ideal photo).")]
    public bool isAlwaysAvailable = false;

    [Tooltip("If true, this item only exists in this level's context (not global).")]
    public bool isLevelBound = true;

    [Header("Visuals")]
    [Tooltip("Icon used in the wheel UI.")]
    public Sprite icon;

    [Tooltip("3D model used in the spinning preview / inspect view.")]
    public GameObject modelPrefab;

    [Header("Equip")]
    [Tooltip("If true and kind = Equipment, this item can be equipped by the player.")]
    public bool equippable = false;

    [Tooltip("Logical slot this item occupies when equipped (e.g. 'Hands', 'Accessory').")]
    public string equipmentSlotId;
}