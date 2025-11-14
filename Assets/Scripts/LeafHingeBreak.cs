using UnityEngine;

[RequireComponent(typeof(HingeJoint), typeof(Rigidbody))]
public class LeafHingeBreak : MonoBehaviour
{
    [Header("Rebound")]
    [Tooltip("Little kickback when the joint breaks, based on current velocity.")]
    public float reboundBoost = 2f;

    [Header("Audio / FX")]
    public AudioSource audioSource;     // optional; if null, nothing plays
    public AudioClip breakSound;        // snap / tear sound

    public ParticleSystem breakSpray;   // sap/pollen spray burst
    public Transform obiFluidSpawn;     // EMPTY null – later: Obi emitter position

    [Header("State (read-only)")]
    public bool isBroken;               // true after the joint breaks

    private HingeJoint joint;
    private Rigidbody rb;

    void Awake()
    {
        joint = GetComponent<HingeJoint>();
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Called automatically by Unity when this hinge breaks due to breakForce/breakTorque.
    /// Make sure the HingeJoint has breakForce/breakTorque set to something > 0.
    /// </summary>
    void OnJointBreak(float breakForce)
    {
        Pop();
    }

    /// <summary>
    /// You can also call this manually from other scripts if you ever want to force a break.
    /// </summary>
    public void Pop()
    {
        if (isBroken) return;
        isBroken = true;

        // Remove the joint if it's still there (Unity usually destroys it already on break).
        if (joint != null)
        {
            Destroy(joint);
            joint = null;
        }

        // Rebound kick so it feels like a snap, not a dead drop.
        if (rb != null)
        {
            rb.AddForce(-rb.linearVelocity * reboundBoost, ForceMode.VelocityChange);
        }

        // ---------- AUDIO ----------
        if (audioSource != null && breakSound != null)
        {
            audioSource.PlayOneShot(breakSound);
        }

        // ---------- PARTICLE FX ----------
        if (breakSpray != null)
        {
            // Optional: spawn from the leaf position.
            breakSpray.transform.position = transform.position;
            breakSpray.transform.rotation = transform.rotation;
            breakSpray.Play(true);
        }

        // ---------- OBI FLUID PLACEHOLDER ----------
        if (obiFluidSpawn != null)
        {
            // Later:
            // - position an ObiEmitter here
            // - align its ObiEmitterShape
            // - emit a burst
            //
            // Example (pseudo):
            // obiEmitter.transform.SetPositionAndRotation(obiFluidSpawn.position, obiFluidSpawn.rotation);
            // obiEmitter.EmitBursts(1);
        }

        // After this, the leaf is just a free Rigidbody and will fall / be dropped
        // according to your grab script / physics.
    }
}
