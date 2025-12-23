/**
 * @file JointCutPolicy.cs
 * @brief JointCutPolicy script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup flowers_runtime
 */

using UnityEngine;

[DisallowMultipleComponent]
/**
 * @class JointCutPolicy
 * @brief JointCutPolicy component.
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
 * @ingroup flowers_runtime
 */
public class JointCutPolicy : MonoBehaviour
{
    public JointSplitMode mode = JointSplitMode.KeepAnchorSideOnly;

    // Optional callback for advanced logic
    public System.Action<Joint, GameObject, GameObject> OnCutCustom;
}