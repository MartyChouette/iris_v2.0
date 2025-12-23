/**
 * @file ScissorsVisualController.cs
 * @brief ScissorsVisualController script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

using System.Collections;
using UnityEngine;
/**
 * @class ScissorsVisualController
 * @brief ScissorsVisualController component.
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
 * @ingroup tools
 */

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
    [Header("Offsets (local to grip)")]
    public Vector3 localPosOffset;
    public Vector3 localEulerOffset;

    [Header("Smoothing")]
    public float poseSmoothing = 25f;

    Transform _grip; // the rig socket / hand socket transform
    Transform _scissors; // active scissors transform
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
    public void SetGrip(Transform gripSocket, Transform scissorsTransform)
    {
        _grip = gripSocket;
        _scissors = scissorsTransform;
    }

    void LateUpdate()
    {
        if (_grip == null || _scissors == null) return;

        var targetPos = _grip.TransformPoint(localPosOffset);
        var targetRot = _grip.rotation * Quaternion.Euler(localEulerOffset);

        _scissors.position = Vector3.Lerp(_scissors.position, targetPos, 1f - Mathf.Exp(-poseSmoothing * Time.deltaTime));
        _scissors.rotation = Quaternion.Slerp(_scissors.rotation, targetRot, 1f - Mathf.Exp(-poseSmoothing * Time.deltaTime));
    }

    private void SetState(bool isOpen)
    {
        if (openModel != null) openModel.SetActive(isOpen);
        if (closedModel != null) closedModel.SetActive(!isOpen);
    }

    public void ResetCooldown()
    {
        nextCutTime = Time.time;
        if (cutCoroutine != null) StopCoroutine(cutCoroutine);
        
    }

}



