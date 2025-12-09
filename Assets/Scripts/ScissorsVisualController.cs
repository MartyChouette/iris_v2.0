using System.Collections;
using UnityEngine;

public class ScissorsVisualController : MonoBehaviour
{
    [Header("Visual Models")]
    public GameObject openModel;
    public GameObject closedModel;

    [Header("Settings")]
    [Tooltip("How long the scissors stay closed (animation time).")]
    public float closeDuration = 0.15f;

    [Tooltip("Minimum time between cuts. Prevents spamming.")]
    public float cutCooldown = 0.5f;

    private float nextCutTime = 0f; // Tracks when we can cut again
    private Coroutine cutCoroutine;

    private void Start()
    {
        SetState(true); // Start open
    }

    // NOTE: Update() and Input checks were removed. 
    // This script now only runs when the Main Controller tells it to.

    /// <summary>
    /// Tries to cut. Returns TRUE if successful, FALSE if on cooldown.
    /// </summary>
    public bool AttemptSnip()
    {
        // 1. COOLDOWN CHECK
        if (Time.time < nextCutTime)
        {
            return false; // Too early! Deny the cut.
        }

        // 2. Set the next allowed time
        nextCutTime = Time.time + cutCooldown;

        // 3. Play Animation
        if (cutCoroutine != null) StopCoroutine(cutCoroutine);
        cutCoroutine = StartCoroutine(DoSnipAnimation());

        return true; // Success! We tell the main script "Yes, go ahead."
    }

    private IEnumerator DoSnipAnimation()
    {
        SetState(false); // Close
        yield return new WaitForSeconds(closeDuration);
        SetState(true);  // Open
    }

    private void SetState(bool isOpen)
    {
        if (openModel != null) openModel.SetActive(isOpen);
        if (closedModel != null) closedModel.SetActive(!isOpen);
    }
}