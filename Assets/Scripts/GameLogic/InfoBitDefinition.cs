using UnityEngine;

[CreateAssetMenu(menuName = "Game/Info Bit")]
public class InfoBitDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique ID, used for save/knowledge tracking. e.g. 'tutorial_cut_45deg'")]
    public string infoId;

    [Tooltip("Short title shown in menus or logs.")]
    public string title;

    [TextArea]
    [Tooltip("Description / text of the info the player has learned.")]
    public string description;

    [Header("Visuals")]
    [Tooltip("Optional icon for UI lists.")]
    public Sprite icon;

    [Header("Meta")]
    [Tooltip("If true, this info bit is part of the tutorial/learning track.")]
    public bool isTutorial = false;

    [Tooltip("If true, this info can only be learned once per profile.")]
    public bool learnOnce = true;
}