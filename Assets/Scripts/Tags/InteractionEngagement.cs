/**
 * @file InteractionEngagement.cs
 * @brief InteractionEngagement script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

// File: InteractionEngagement.cs
using UnityEngine;

[DisallowMultipleComponent]
/**
 * @class InteractionEngagement
 * @brief InteractionEngagement component.
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
public class InteractionEngagement : MonoBehaviour
{
    [Tooltip("True while the player is actively grabbing / interacting with this object.")]
    public bool isEngaged = false;

    [Range(0f, 1f)]
    [Tooltip("How much intensity this object should feel when NOT directly engaged.")]
    public float passiveIntensity = 0.25f;

    public float GetIntensity()
    {
        return isEngaged ? 1f : passiveIntensity;
    }
}