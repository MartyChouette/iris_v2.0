/**
 * @file LeafAttachmentMarker.cs
 * @brief LeafAttachmentMarker script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

// File: LeafAttachmentMarker.cs
using UnityEngine;

/// <summary>
/// Tag component for leaf attachment nodes on the stem.
/// Add this to the small sphere objects that the leaves are jointed to.
/// </summary>
/**
 * @class LeafAttachmentMarker
 * @brief LeafAttachmentMarker component.
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
public class LeafAttachmentMarker : MonoBehaviour
{
    public FlowerPartRuntime owningLeaf;

}