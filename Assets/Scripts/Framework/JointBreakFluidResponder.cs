/**
 * @file JointBreakFluidResponder.cs
 * @brief JointBreakFluidResponder script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup flowers_runtime
 */

using UnityEngine;
/**
 * @class JointBreakFluidResponder
 * @brief JointBreakFluidResponder component.
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

public class JointBreakFluidResponder : MonoBehaviour
{
    public enum PartType { Leaf, Petal, Stem }

    [Header("Configuration")]
    [Tooltip("Is this a leaf, petal, or stem?")]
    public PartType partType = PartType.Leaf;

    [Tooltip("The specific point where fluid shoots from. If empty, uses this object's center.")]
    public Transform bleedPoint;

    [Tooltip("Optional: If the controller is not the singleton, assign it manually.")]
    public FlowerSapController sapController;

    private void Start()
    {
        // 1. Auto-assign BleedPoint if missing
        if (bleedPoint == null)
            bleedPoint = this.transform;

        // 2. Auto-find Controller if missing
        // Note: This relies on FlowerSapController having the 'public static FlowerSapController Instance' line.
        if (sapController == null)
            sapController = FlowerSapController.Instance;
    }

    /// <summary>
    /// Call this from your joint-break script (e.g., right next to your audio call).
    /// </summary>
    public void OnJointBroken()
    {
        // Safety Check 1: Try to find controller if we missed it in Start
        if (sapController == null)
        {
            sapController = FlowerSapController.Instance;

            // If still null, we can't do anything
            if (sapController == null)
            {
                Debug.LogWarning($"[FluidResponder] No FlowerSapController found for {name}!");
                return;
            }
        }

        // Safety Check 2: Ensure BleedPoint is valid
        if (bleedPoint == null) bleedPoint = this.transform;

        // Get Position & Direction
        Vector3 pos = bleedPoint.position;
        Vector3 dir = bleedPoint.forward; // Ensure the Z-axis (Blue Arrow) of BleedPoint points OUT

        // Fire the correct fluid type
        switch (partType)
        {
            case PartType.Leaf:
                sapController.EmitLeafTear(pos, dir);
                break;

            case PartType.Petal:
                sapController.EmitPetalTear(pos, dir);
                break;

            case PartType.Stem:
                // FIX: Find the stem runtime so the fluid controller knows where to project the burst
                var stem = GetComponentInParent<FlowerStemRuntime>();

                // Pass the 'stem' as the new 3rd argument
                sapController.EmitStemCut(pos, dir, stem);
                break;
        }
    }

    // Debug helper to see the "shoot direction" in Scene View
    private void OnDrawGizmosSelected()
    {
        if (bleedPoint != null)
        {
            Gizmos.color = Color.cyan;
            // Draws a line showing which way the fluid will fly
            Gizmos.DrawRay(bleedPoint.position, bleedPoint.forward * 0.1f);
            Gizmos.DrawSphere(bleedPoint.position, 0.005f);
        }
    }
}