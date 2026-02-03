/**
 * @file StemPieceMarker.cs
 * @brief StemPieceMarker script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup flowers_runtime
 */

using UnityEngine;
/**
 * @class StemPieceMarker
 * @brief StemPieceMarker component.
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

public class StemPieceMarker : MonoBehaviour
{
    public FlowerStemRuntime stemRuntime;

    /// <summary>
    /// True if this is the kept piece (connected to crown), false if it's a falling piece.
    /// </summary>
    public bool isKeptPiece;

    // PERF: Static registry avoids expensive FindObjectsByType calls in FlowerJointRebinder
    private static readonly System.Collections.Generic.List<StemPieceMarker> s_all = new(16);
    public static System.Collections.Generic.IReadOnlyList<StemPieceMarker> All => s_all;

    private void OnEnable() => s_all.Add(this);
    private void OnDisable() => s_all.Remove(this);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => s_all.Clear();
}