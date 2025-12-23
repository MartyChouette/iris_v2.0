/**
 * @file InfoBitDefinition.cs
 * @brief InfoBitDefinition script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup items
 */

using UnityEngine;

[CreateAssetMenu(menuName = "Game/Info Bit")]
/**
 * @file InfoBitDefinition.cs
 * @brief Data definition for a unit of knowledge (an "InfoBit") in Iris.
 *
 * @details
 * InfoBitDefinition is a data container representing a single discoverable meaning-unit.
 * It should not contain presentation logic; it exists so that meaning can be referenced
 * consistently across systems without duplicating text or branching logic.
 *
 * Typical contents (example, not required):
 * - Unique ID (string / GUID / enum)
 * - Optional category/tags (for grouping)
 * - Optional internal description (for authoring/debugging)
 *
 * Responsibilities:
 * - Provide stable identifiers for PlayerKnowledgeTracker and other systems.
 * - Allow authoring meaningful "facts" that can be granted/queried.
 *
 * Non-responsibilities:
 * - Does not decide how/when a player learns it (that’s InspectableObject / narrative logic).
 * - Does not display UI text.
 *
 * Design notes:
 * - Keep InfoBits semantically meaningful, not purely mechanical.
 * - Prefer reusing InfoBits over making near-duplicates to avoid branch explosion.
 *
 * @see PlayerKnowledgeTracker
 * @see InspectableObject
 */

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