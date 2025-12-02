using UnityEngine;

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