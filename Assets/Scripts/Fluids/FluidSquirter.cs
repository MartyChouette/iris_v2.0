/**
 * @file FluidSquirter.cs
 * @brief FluidSquirter script.
 * @details
 * Handles positioning and emitting fallback particle effects at a spray source point.
 *
 * @ingroup fluids
 */

using UnityEngine;

public class FluidSquirter : MonoBehaviour
{
    [Tooltip("Optional ParticleSystem used for splatter effects.")]
    public ParticleSystem fallbackParticles;

    [Tooltip("Base spawn amount / intensity for this emitter (1 = normal).")]
    public float baseIntensity = 1f;

    /// <summary>
    /// Squirts at the current transform position.
    /// </summary>
    public void Squirt(float goreIntensity)
    {
        Squirt(goreIntensity, transform.position, transform.forward);
    }

    /// <summary>
    /// Moves the emitter to the specific position/normal, then squirts.
    /// </summary>
    public void Squirt(float goreIntensity, Vector3 position, Vector3 normal)
    {
        float final_ = Mathf.Clamp01(goreIntensity) * baseIntensity;
        if (final_ <= 0f) return;

        // 1. Move to the exact contact point
        transform.position = position;

        // 2. Rotate to face the spray direction
        if (normal.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(normal);
        }

        // 3. Emit particles
        if (fallbackParticles != null)
        {
            int emitCount = Mathf.CeilToInt(10 * final_);
            fallbackParticles.Emit(emitCount);
        }
    }
}
