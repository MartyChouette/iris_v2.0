/**
 * @file InspectableObject.cs
 * @brief InspectableObject script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

using UnityEngine;
/**
 * @file InspectableObject.cs
 * @brief Interaction surface for apartment objects that can be inspected/observed.
 *
 * @details
 * InspectableObject mediates player interaction with environmental props and narrative cues.
 * It typically:
 * - Responds to player "inspect" intent (via RaycastManager or other input layer).
 * - Checks PlayerKnowledgeTracker to determine first-time vs repeat behavior.
 * - Optionally grants one or more InfoBits when inspected.
 * - Triggers feedback (text/audio/silence) via whatever UI/audio systems are in use.
 *
 * Responsibilities:
 * - Define what happens when the player inspects this object.
 * - Maintain consistent rules for repeat inspections (no accidental re-grants unless intended).
 * - Keep logic local: the object owns its inspect behavior; knowledge system owns memory.
 *
 * Non-responsibilities:
 * - Should not own global progression rules (that belongs in higher-level controllers/config).
 * - Should not implement raycasting or hover selection (RaycastManager).
 *
 * Design constraints:
 * - Inspections should not always "pay off" with text; ambiguity is allowed and intentional.
 * - Avoid turning inspection into collectible scavenging; focus on meaning and recontextualization.
 *
 * Integration points:
 * - RaycastManager: hover/activate routing.
 * - PlayerKnowledgeTracker: query + grant.
 * - InfoBitDefinition: meanings this object may grant/check.
 * - LevelConfig: can gate availability or variant behavior per level/date/loop.
 *
 * @see RaycastManager
 * @see PlayerKnowledgeTracker
 * @see InfoBitDefinition
 * @see LevelConfig
 */


public class InspectableObject : MonoBehaviour
{
    [TextArea]
    public string description;

    [Tooltip("The 3D model that should be used for the inspect view. " +
             "If left null, this object itself will be duplicated in the viewer.")]
    public GameObject modelOverride;
}