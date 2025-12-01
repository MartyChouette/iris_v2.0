using UnityEngine;

[RequireComponent(typeof(HingeJoint), typeof(Rigidbody))]
public class LeafHingeBreak : MonoBehaviour
{
    [Header("Break Force")]
    [Tooltip("Baseline breakForce once armed (higher = harder to pluck).")]
    public float armedBreakForce = 40f;

    [Tooltip("Time after start before we even allow breaking (avoids startup jitters).")]
    public float armDelay = 0.1f;

    [Header("Hex Notch Assist (easier in 6 directions)")]
    [Tooltip("Enable hex-style notches where pulling breaks earlier.")]
    public bool useHexNotches = true;

    [Tooltip("Number of notches around the circle. 6 = hex.")]
    public int notchCount = 6;

    [Tooltip("How close (in degrees) you need to be to a notch direction for easier break.")]
    public float notchForgiveness = 12f;

    [Tooltip("Multiplier for breakForce when inside a notch (<1 = easier to break).")]
    [Range(0.1f, 1f)]
    public float notchForceScale = 0.5f;

    [Header("Rebound")]
    [Tooltip("Little kickback when the joint snaps, based on current velocity.")]
    public float reboundBoost = 2f;

    [Header("Audio / FX")]
    public AudioSource audioSource;
    public AudioClip breakSound;

    public ParticleSystem breakSpray;
    public Transform obiFluidSpawn; // future Obi emitter anchor

    [Header("Debug / State")]
    public bool isBroken;

    private HingeJoint joint;
    private Rigidbody rb;

    private float restAngle;
    private float armTimer;
    private bool armed;

    void Awake()
    {
        joint = GetComponent<HingeJoint>();
        rb = GetComponent<Rigidbody>();

        if (joint != null)
        {
            // Angle we consider "neutral" for hex directions.
            restAngle = joint.angle;

            // At startup: make it effectively unbreakable until we arm.
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
        }

        if (armedBreakForce <= 0f) armedBreakForce = 10f;
        if (notchCount < 1) notchCount = 1;
        if (notchForgiveness < 0f) notchForgiveness = 0f;
        if (armDelay < 0f) armDelay = 0f;
    }

    void FixedUpdate()
    {
        if (isBroken || joint == null)
            return;

        // ── Arming window ─────────────────────────────────
        if (!armed)
        {
            armTimer += Time.fixedDeltaTime;
            if (armTimer >= armDelay)
                armed = true;

            if (!armed)
                return;
        }

        // ── Decide what breakForce to use this frame ─────────────────────
        float effectiveBreakForce = armedBreakForce;

        if (useHexNotches && notchCount > 0)
        {
            // Angle relative to the rest pose
            float current = joint.angle;
            float relativeAngle = Mathf.DeltaAngle(current, restAngle); // -180..180 around rest

            float step = 360f / notchCount;         // e.g. 360/6 = 60°
            float nearestNotch = Mathf.Round(relativeAngle / step) * step;
            float distToNotch = Mathf.Abs(Mathf.DeltaAngle(relativeAngle, nearestNotch));

            bool inNotch = distToNotch <= notchForgiveness;

            if (inNotch)
            {
                effectiveBreakForce *= notchForceScale; // easier to break in those 6 wedges
            }
        }

        // Apply to the hinge each frame
        joint.breakForce = effectiveBreakForce;
        // Let torque be huge so we only care about pull force, not twist
        joint.breakTorque = Mathf.Infinity;
    }

    /// <summary>
    /// Called automatically when Unity breaks the joint due to breakForce.
    /// </summary>
    void OnJointBreak(float brokenForce)
    {
        Pop();
    }

    // ───────────────────────────── Break / Pop ─────────────────────────────
    public void Pop()
    {
        if (isBroken)
            return;

        isBroken = true;

        if (joint != null)
        {
            Destroy(joint);
            joint = null;
        }

        // Little reactive kick so it doesn’t just drop like a dead rigidbody.
        if (rb != null)
        {
            rb.AddForce(-rb.linearVelocity * reboundBoost, ForceMode.VelocityChange);
        }

        // AUDIO
        if (audioSource != null && breakSound != null)
        {
            audioSource.PlayOneShot(breakSound);
        }

        // PARTICLES
        if (breakSpray != null)
        {
            breakSpray.transform.position = transform.position;
            breakSpray.transform.rotation = transform.rotation;
            breakSpray.Play(true);
        }

        // OBI FLUID PLACEHOLDER
        if (obiFluidSpawn != null)
        {
            // Later: place an ObiEmitter here & emit a burst.
        }
    }
}
