/**
 * @file SafeBreathing.cs
 * @brief SafeBreathing script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

using UnityEngine;
/**
 * @class SafeBreathing
 * @brief SafeBreathing component.
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

public class SafeBreathing : MonoBehaviour
{
    [Header("Rhythm")]
    public float beatsPerMinute = 15f;
    [Range(0f, 1f)] public float timeOffset = 0f;

    [Header("Stretch (Scale)")]
    [Tooltip("Usually Y (0,1,0) for petals.")]
    public Vector3 stretchAxis = Vector3.up;
    public float stretchMagnitude = 0.1f;
    [Tooltip("If checked, X and Z will shrink when Y grows.")]
    public bool preserveVolume = true;

    [Header("Positional Offset")]
    public Vector3 moveDirection = Vector3.zero;
    public float moveMagnitude = 0.05f;

    [Header("Joint Safety")]
    [Tooltip("Set this to where the petal connects to the stem. -0.5 is the bottom, 0 is center, 0.5 is top.")]
    [Range(-0.5f, 0.5f)] public float pivotY = -0.5f;

    [Tooltip("Automatically increases the Tether limit so breathing doesn't snap the joint.")]
    public bool preventJointBreak = true;

    // Internal
    private Vector3 initialScale;
    private Vector3 initialLocalPos;
    private XYTetherJoint tether;
    private float tetherBaseMaxDist;
    private FlowerPartRuntime _partRuntime;

    void Start()
    {
        initialScale = transform.localScale;
        initialLocalPos = transform.localPosition;

        // Randomize rhythm if not set manually
        if (timeOffset == 0f) timeOffset = Random.Range(0f, 1f);

        // Find the joint to manage safety
        tether = GetComponent<XYTetherJoint>();
        if (tether != null)
        {
            tetherBaseMaxDist = tether.maxDistance;
        }

        _partRuntime = GetComponent<FlowerPartRuntime>();
    }

    void Update()
    {
        // Kill switch: stop breathing after detach (mirrors FlowerBreathing)
        if (_partRuntime != null && !_partRuntime.isAttached)
        {
            transform.localScale = initialScale;
            transform.localPosition = initialLocalPos;
            this.enabled = false;
            return;
        }

        // 1. Calculate Sine Wave
        float t = Time.time + timeOffset;
        float freq = beatsPerMinute / 60f * Mathf.PI * 2f;
        float sine = Mathf.Sin(t * freq);

        // 2. Calculate Scale (Stretch)
        // Map sine (-1 to 1) to a stretch factor (e.g. 1.0 to 1.1)
        float stretchFactor = 1f + (sine * stretchMagnitude);
        float inverseFactor = preserveVolume ? 1f / Mathf.Sqrt(Mathf.Max(0.01f, stretchFactor)) : 1f;

        Vector3 targetScale = initialScale;

        // Apply stretch based on axis
        if (stretchAxis == Vector3.up)
        {
            targetScale.x *= inverseFactor;
            targetScale.y *= stretchFactor;
            targetScale.z *= inverseFactor;
        }
        else if (stretchAxis == Vector3.right)
        {
            targetScale.x *= stretchFactor;
            targetScale.y *= inverseFactor;
            targetScale.z *= inverseFactor;
        }
        else
        {
            targetScale.z *= stretchFactor;
            targetScale.x *= inverseFactor;
            targetScale.y *= inverseFactor;
        }

        transform.localScale = targetScale;

        // 3. PIVOT COMPENSATION (The Fix for Breaking Joints)
        // If we stretch Y, the center moves. We must move the position 
        // opposite to the stretch to keep the "Pivot" stationary.

        float heightChange = targetScale.y - initialScale.y;

        // Calculate how much the "Center" needs to move to keep "PivotY" still.
        // If pivot is -0.5 (bottom), and we grow, we must move UP (+Y) by half the growth.
        float pivotCorrectionY = heightChange * -pivotY;

        // This assumes the object's local Y axis is the length. 
        Vector3 pivotOffset = transform.up * pivotCorrectionY;


        // 4. Calculate Directional Move (The "Offset" parameter you asked for)
        Vector3 directionalOffset = moveDirection.normalized * (sine * moveMagnitude);


        // 5. Apply Position (Initial + PivotFix + AnimationOffset)
        // We use localPosition so it rides on top of the parent logic or physics drift
        transform.localPosition = initialLocalPos + pivotOffset + directionalOffset;


        // 6. JOINT SAFETY
        // If we are physically moving the object (directionalOffset), the joint 
        // might think we are breaking it. We update the maxDistance dynamically.
        if (preventJointBreak && tether != null)
        {
            // We calculate how much extra room we need based on our animation offset
            float extraRoom = directionalOffset.magnitude + Mathf.Abs(pivotCorrectionY);

            // Update the tether's limit for this frame
            tether.maxDistance = tetherBaseMaxDist + (extraRoom * 1.5f); // 1.5x buffer
        }
    }
}