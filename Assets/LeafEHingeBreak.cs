using UnityEngine;

[RequireComponent(typeof(HingeJoint))]
public class LeafHingeBreak : MonoBehaviour
{
    public float breakAngle = 18f;   // degrees away from 0 before we consider breaking
    public float dwellTime = 0.06f;  // how long we must exceed that angle

    public float reboundBoost = 2f;  // little kickback when it pops

    HingeJoint joint;
    Rigidbody rb;
    float timer;

    void Awake()
    {
        joint = GetComponent<HingeJoint>();
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (joint == null) return;

        float a = Mathf.Abs(joint.angle); // hinge angle in degrees

        if (a > breakAngle)
        {
            timer += Time.fixedDeltaTime;
            if (timer >= dwellTime)
                Pop();
        }
        else
        {
            timer = 0f;
        }
    }

    void Pop()
    {
        // remove the hinge so the leaf detaches
        Destroy(joint);

        if (rb != null)
        {
            // quick little rebound so it feels like a snap, not a dead drop
            rb.AddForce(-rb.linearVelocity * reboundBoost, ForceMode.VelocityChange);
        }

        // TODO: sap FX / sound / jelly kick, etc.
    }
}