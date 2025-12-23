/**
 * @file JointBreakAudioResponder.cs
 * @brief JointBreakAudioResponder script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup flowers_runtime
 */

using UnityEngine;
/**
 * @class JointBreakAudioResponder
 * @brief JointBreakAudioResponder component.
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

public class JointBreakAudioResponder : MonoBehaviour
{
    [Header("Audio Clips")]
    public AudioClip primaryBreakSound;
    public AudioClip secondaryBreakSound;

    [Tooltip("Delay (seconds) before playing the secondary sound.")]
    public float secondaryDelay = 0.15f;

    [Header("Debug")]
    public bool debugLogs = false;

    // Called by your leaf/petal/stem joint break logic
    public void OnJointBroken()
    {
        if (AudioManager.Instance == null)
            return;

        if (secondaryBreakSound != null)
            AudioManager.Instance.PlayDualSFX(primaryBreakSound, secondaryBreakSound, secondaryDelay);
        else
            AudioManager.Instance.PlaySFX(primaryBreakSound);

        if (debugLogs)
            Debug.Log($"[JointBreakAudioResponder] Played break sounds on {gameObject.name}");
    }
}