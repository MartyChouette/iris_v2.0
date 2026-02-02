/**
 * @file ParametricBreathing.cs
 * @brief ParametricBreathing script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

using UnityEngine;
/**
 * @class ParametricBreathing
 * @brief ParametricBreathing component.
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

public class ParametricBreathing : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Reference to the script that handles clicking/pulling.")]
    public GrabPull grabPull;

    [Header("Timing")]
    [Tooltip("How fast the breathing cycles.")]
    public float beatsPerMinute = 20f;

    [Tooltip("Offset the starting time (0.0 to 1.0) so petals don't breathe in sync.")]
    [Range(0f, 1f)] public float timeOffset = 0f;

    [Header("Mesh Stretch (Scale)")]
    [Tooltip("Which local axis does the object stretch along? (Usually Y for length).")]
    public Vector3 stretchAxis = Vector3.up;

    [Tooltip("How much to stretch? 0 = none, 0.1 = 10% stretch.")]
    public float stretchMagnitude = 0.1f;

    [Tooltip("If true, shrinking Y will expand X/Z to keep mass constant (Rubber feel).")]
    public bool preserveVolume = true;

    [Header("Positional Offset")]
    [Tooltip("Direction the object moves while breathing.")]
    public Vector3 moveDirection = Vector3.zero;

    [Tooltip("How far it moves.")]
    public float moveMagnitude = 0.05f;

    [Header("Debug Status")]
    [Tooltip("True when the object is being held and breathing has completely paused.")]
    public bool isBreathingStopped;

    // Internal state
    private Vector3 initialScale;
    private Vector3 initialPos;
    private FlowerPartRuntime _partRuntime;

    // Smooth dampener to handle the click transition
    private float currentIntensity = 1f;

    void Start()
    {
        initialScale = transform.localScale;
        initialPos = transform.localPosition;

        // Auto-find the GrabPull script if it's on the same object
        if (grabPull == null) grabPull = GetComponent<GrabPull>();

        _partRuntime = GetComponent<FlowerPartRuntime>();
    }

    void Update()
    {
        // Kill switch: stop breathing after detach (mirrors FlowerBreathing)
        if (_partRuntime != null && !_partRuntime.isAttached)
        {
            transform.localScale = initialScale;
            transform.localPosition = initialPos;
            this.enabled = false;
            return;
        }

        // 1. CHECK INPUT (Click/Grab Detection)
        bool isClicked = false;

        // Check the public 'grabbing' variable in GrabPull.cs
        if (grabPull != null && grabPull.grabbing)
        {
            isClicked = true;
        }

        // 2. SMOOTH TRANSITION
        // Fade intensity to 0 when clicked, Fade to 1 when released
        float targetIntensity = isClicked ? 0f : 1f;
        currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, Time.deltaTime * 5f);

        // 3. OPTIMIZATION / HARD STOP
        // If intensity is near 0 (fully held), reset to exact rest pose, set flag, and skip math.
        if (currentIntensity <= 0.001f)
        {
            isBreathingStopped = true;
            transform.localScale = initialScale;
            transform.localPosition = initialPos;
            return;
        }

        // If we reached here, we are still breathing (even if just a little)
        isBreathingStopped = false;

        // 4. APPLY INTENSITY TO MAGNITUDES
        float activeStretchMag = stretchMagnitude * currentIntensity;
        float activeMoveMag = moveMagnitude * currentIntensity;

        // 5. Calculate the Sine Wave
        float t = Time.time + timeOffset;
        float freq = beatsPerMinute / 60f * Mathf.PI * 2f;
        float sine = Mathf.Sin(t * freq);

        // ────── Stretch Logic ──────
        float stretchFactor = 1f + (sine * activeStretchMag);

        float inverseFactor = preserveVolume ? 1f / Mathf.Sqrt(Mathf.Max(0.01f, stretchFactor)) : 1f;

        Vector3 targetScale = initialScale;

        if (stretchAxis == Vector3.up) // Y Axis Stretch
        {
            targetScale.x *= inverseFactor;
            targetScale.y *= stretchFactor;
            targetScale.z *= inverseFactor;
        }
        else if (stretchAxis == Vector3.right) // X Axis Stretch
        {
            targetScale.x *= stretchFactor;
            targetScale.y *= inverseFactor;
            targetScale.z *= inverseFactor;
        }
        else // Z Axis
        {
            targetScale.x *= inverseFactor;
            targetScale.y *= inverseFactor;
            targetScale.z *= stretchFactor;
        }

        transform.localScale = targetScale;

        // ────── Position Logic ──────
        Vector3 posOffset = moveDirection.normalized * (sine * activeMoveMag);
        transform.localPosition = initialPos + posOffset;
    }
}