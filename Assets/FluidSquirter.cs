using UnityEngine;
/// <summary>
/// Placeholder / wrapper for later Obi integration.
/// Right now it just exposes a Squirt(intensity) API and optional ParticleSystem,
/// so you can test gore intensity without needing Obi in the project yet.
/// </summary>
public class FluidSquirter : MonoBehaviour
{
    [Tooltip("Optional ParticleSystem used as a stand-in splatter until Obi is wired.")]
    public ParticleSystem fallbackParticles;

    [Tooltip("Base spawn amount / intensity for this emitter (1 = normal).")]
    public float baseIntensity = 1f;

    public void Squirt(float goreIntensity)
    {
        float final = Mathf.Clamp01(goreIntensity) * baseIntensity;
        if (final <= 0f) return;

        if (fallbackParticles != null)
        {
            int emitCount = Mathf.CeilToInt(10 * final);
            fallbackParticles.Emit(emitCount);
        }

        // Later: replace this with Obi emitter calls.
    }
}
