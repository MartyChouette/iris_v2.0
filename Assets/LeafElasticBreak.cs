using UnityEngine;

[RequireComponent(typeof(SpringJoint))]
public class LeafElasticBreak : MonoBehaviour
{
    public Transform attachPoint;        // stem reference point
    public float maxStretch = 0.10f;     // how far before break
    public float dwellTime = 0.06f;      // how long overstretched
    public float springReboundBoost = 2f;

    SpringJoint joint;
    Rigidbody leafRb;
    float timer;

    void Awake()
    {
        joint = GetComponent<SpringJoint>();
        leafRb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (joint == null) return;

        float dist = Vector3.Distance(leafRb.worldCenterOfMass, attachPoint.position);

        if (dist > maxStretch)
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
        // Remove joint
        Destroy(joint);

        // Rebound effect
        leafRb.AddForce(-leafRb.linearVelocity * springReboundBoost, ForceMode.VelocityChange);

        // TODO: spawn sap FX, sound, DynamicMeshCutter cut, wobble stem, etc.
    }
}