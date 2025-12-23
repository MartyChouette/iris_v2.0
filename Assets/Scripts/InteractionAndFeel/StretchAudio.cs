/**
 * @file StretchAudio.cs
 * @brief StretchAudio script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup audio
 */

using UnityEngine;
/**
 * @class StretchAudio
 * @brief StretchAudio component.
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
 * @ingroup audio
 */

public class StretchAudio : MonoBehaviour
{
    public AudioSource audioSource;

    // Drag this function into the "On Tension Changed" slot on your XYTetherJoint
    public void UpdateStretchSound(float tension)
    {
        if (tension > 0.05f)
        {
            if (!audioSource.isPlaying) audioSource.Play();

            // Map tension (0 to 1) to Pitch (0.8 to 2.0)
            audioSource.pitch = 0.8f + (tension * 1.2f);

            // Map tension to Volume
            audioSource.volume = tension;
        }
        else
        {
            // Stop sound if loose
            if (audioSource.isPlaying) audioSource.Stop();
        }
    }
}