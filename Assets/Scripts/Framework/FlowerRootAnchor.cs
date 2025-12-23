/**
 * @file FlowerRootAnchor.cs
 * @brief FlowerRootAnchor script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup flowers_runtime
 */

using UnityEngine;
/**
 * @class FlowerRootAnchor
 * @brief FlowerRootAnchor component.
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

public class FlowerRootAnchor : MonoBehaviour
{
    [Header("Bottom piece (stem base)")]
    public bool lockBottomPiece = true;
    public RigidbodyConstraints bottomConstraints = RigidbodyConstraints.FreezeAll;
    public bool bottomUseGravity = false;
    public bool bottomIsKinematic = true;

    [Header("Cut-off chunks (top pieces)")]
    public bool makeTopDynamic = true;
    public bool topUseGravity = true;
    public RigidbodyConstraints topConstraints = RigidbodyConstraints.None;
}