/**
 * @file FluidSquirter.cs
 * @brief FluidSquirter script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup thirdparty
 */

using UnityEngine;
// using Obi; // Uncomment when you have Obi imported

/// <summary>
/// Placeholder / wrapper for Obi integration.
/// Now handles positioning itself at the source of the spray.
/// </summary>
/**
 * @class FluidSquirter
 * @brief FluidSquirter component.
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
 * @ingroup thirdparty
 */
public class FluidSquirter : MonoBehaviour
{
    [Tooltip("Optional ParticleSystem used as a stand-in splatter until Obi is wired.")]
    public ParticleSystem fallbackParticles;

    [Tooltip("Base spawn amount / intensity for this emitter (1 = normal).")]
    public float baseIntensity = 1f;

    /// <summary>
    /// Legacy overload: Squirts at the current transform position.
    /// </summary>
    public void Squirt(float goreIntensity)
    {
        Squirt(goreIntensity, transform.position, transform.forward);
    }

    /// <summary>
    /// New Overload: Moves the emitter to the specific position/normal, then squirts.
    /// </summary>
    public void Squirt(float goreIntensity, Vector3 position, Vector3 normal)
    {
        float final = Mathf.Clamp01(goreIntensity) * baseIntensity;
        if (final <= 0f) return;

        // 1. Move to the exact contact point
        transform.position = position;

        // 2. Rotate to face the spray direction
        if (normal.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(normal);
        }

        // 3. Emit fallback particles (if assigned)
        if (fallbackParticles != null)
        {
            // If the particle system is a child, ensure it uses Local simulation space
            // or we just rely on moving the parent transform before emission.
            int emitCount = Mathf.CeilToInt(10 * final);
            fallbackParticles.Emit(emitCount);
        }

        // 4. Emit Obi Fluid (Integration)
        /*
        var obiEmitter = GetComponent<Obi.ObiEmitter>();
        if (obiEmitter != null) 
        {
            // Kill velocity so it doesn't streak from the old position
            obiEmitter.KillAll(); 
            
            // Speed hack for burst intensity
            float originalSpeed = obiEmitter.speed;
            obiEmitter.speed = 10f * final; 

            // Note: Obi emitters are continuous. You usually need a Coroutine 
            // to turn 'obiEmitter.speed' back to 0 after a fraction of a second,
            // similar to how FlowerSapController.Burst works.
        }
        */
    }
}