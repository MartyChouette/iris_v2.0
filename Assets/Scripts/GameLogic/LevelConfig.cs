/**
 * @file LevelConfig.cs
 * @brief LevelConfig script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
 /**
* @details
* Intent:
* - Per-level authoring container that declares:
*   (1) which @ref FlowerTypeDefinition governs scoring + ideals
*   (2) which items are always available vs. potentially discoverable in the space.
*
* Key behaviors:
* - ApplyToInventory() is a convenience wrapper: LevelConfig is the source; inventory is the runtime sink.
* - alwaysAvailableItems is the "entitlement baseline" for this level (ex: ideal photo).
*
* Invariants:
* - flowerType should be assigned for any playable flower-trim level.
* - alwaysAvailableItems should be safe to reset/re-add any time a session starts.
*
* Design hooks:
* - Use alwaysAvailableItems to express ideology: what the system insists you always have.
* - Use levelItems to express temptation: what exists, but must be earned/seen/taken.
  

* @file LevelConfig.cs
* @brief Authoring-time configuration describing the ruleset/variant for a scene/level/day.
*
* @details
* LevelConfig stores the structural parameters that define what kind of "day" or "date loop"
* is currently active, without hardcoding those rules into scene objects or scattered scripts.
*
* Responsibilities:
* - Provide a stable, centralized set of parameters for a level variant.
* - Gate interactables, narrative constraints, evaluation rules, and/or pacing knobs.
* - Enable recontextualizing the same space across loops/dates without duplicating scenes.
*
* Non-responsibilities:
* - Does not execute gameplay flow by itself (unless your architecture explicitly does so).
* - Does not own player knowledge; it may reference knowledge as a gating condition.
*
* Design constraints:
* - Should be readable and auditable by non-programmers (professors included).
* - Should avoid hidden side effects; configs describe, controllers apply.
*
* Integration points:
* - PlayerKnowledgeTracker: used for gating or variant selection.
* - InspectableObject / other interactables: may check config flags to enable/disable behavior.
* - Flower / ritual systems: may read scoring/evaluation parameters from config.
*
* @see PlayerKnowledgeTracker
* @see InspectableObject



* Unity lifecycle:
* - Awake(): cache references / validate setup.
* - OnEnable()/OnDisable(): hook/unhook events.
* - Update(): per-frame behavior (if any).
* * Gotchas:
* - Keep hot paths allocation-free (Update/cuts/spawns).
* - Prefer event-driven UI updates over per-frame string building.
*
* @ingroup tools
*/
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