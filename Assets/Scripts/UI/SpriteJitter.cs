/**
 * @file SpriteJitter.cs
 * @brief SpriteJitter script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup ui
 */

using UnityEngine;
/**
 * @class SpriteJitter
 * @brief SpriteJitter component.
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
 * @ingroup ui
 */

public class SpriteJitter : MonoBehaviour
{
    [Header("Jitter Intensity")]
    [Tooltip("How much to shake the position.")]
    public float posStrength = 0.02f;

    [Tooltip("How much to shake the rotation (in degrees).")]
    public float rotStrength = 2.0f;

    [Tooltip("How much to distort the scale (e.g. 0.05 = +/- 5%).")]
    public float scaleStrength = 0.05f;

    [Header("Frame Rate")]
    [Tooltip("Frames per second for the jitter (12-15 looks hand-drawn).")]
    public float fps = 12f;

    [Header("Rhythm / Pausing")]
    [Tooltip("Minimum time to spend jittering.")]
    public float minJitterDuration = 0.5f;
    [Tooltip("Maximum time to spend jittering.")]
    public float maxJitterDuration = 1.5f;

    [Space(5)]
    [Tooltip("Minimum time to stay still (Pause).")]
    public float minPauseDuration = 1.0f;
    [Tooltip("Maximum time to stay still (Pause).")]
    public float maxPauseDuration = 3.0f;

    // Internal
    private Vector3 initialPos;
    private Quaternion initialRot;
    private Vector3 initialScale;

    private float fpsTimer;     // Controls the strobe effect (12fps)
    private float stateTimer;   // Controls the Switch between Jittering and Pausing
    private bool isJittering = true;

    void Start()
    {
        // Cache the resting transforms
        initialPos = transform.localPosition;
        initialRot = transform.localRotation;
        initialScale = transform.localScale;

        // Initialize state
        isJittering = true;
        stateTimer = Random.Range(minJitterDuration, maxJitterDuration);
    }

    void Update()
    {
        // 1. Manage the State (Jittering vs Pausing)
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            // Toggle State
            isJittering = !isJittering;

            if (isJittering)
            {
                // Start Jittering
                stateTimer = Random.Range(minJitterDuration, maxJitterDuration);
            }
            else
            {
                // Start Pausing (Reset to rest pose immediately)
                stateTimer = Random.Range(minPauseDuration, maxPauseDuration);
                ResetToRest();
            }
        }

        // 2. If Jittering, run the animation logic
        if (isJittering)
        {
            fpsTimer += Time.deltaTime;

            // Run at the desired FPS interval (e.g., every 0.08 seconds)
            if (fpsTimer >= 1f / fps)
            {
                ApplyJitter();
                fpsTimer = 0f;
            }
        }
    }

    void ApplyJitter()
    {
        // Position Noise
        Vector3 posOffset = (Vector3)Random.insideUnitCircle * posStrength;
        transform.localPosition = initialPos + posOffset;

        // Rotation Noise
        float rotOffset = Random.Range(-rotStrength, rotStrength);
        transform.localRotation = initialRot * Quaternion.Euler(0, 0, rotOffset);

        // Scale Noise
        float scaleX = 1f + Random.Range(-scaleStrength, scaleStrength);
        float scaleY = 1f + Random.Range(-scaleStrength, scaleStrength);

        transform.localScale = new Vector3(
            initialScale.x * scaleX,
            initialScale.y * scaleY,
            initialScale.z
        );
    }

    void ResetToRest()
    {
        transform.localPosition = initialPos;
        transform.localRotation = initialRot;
        transform.localScale = initialScale;
    }
}