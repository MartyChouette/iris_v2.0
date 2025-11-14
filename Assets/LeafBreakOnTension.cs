using UnityEngine;

[RequireComponent(typeof(ConfigurableJoint))]
public class LeafBreakOnTension : MonoBehaviour
{
    public float breakDistance = 0.12f;    // world units beyond rest
    public float dwellTime = 0.08f;       // how long we must exceed it
    public Transform attachPointOnStem;   // where it *should* rest (world-space ref)

    float _overTimer;
    ConfigurableJoint _joint;
    Rigidbody _rb;

    void Awake()
    {
        _joint = GetComponent<ConfigurableJoint>();
        _rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (_joint == null) return;

        Vector3 leafPos = _rb.worldCenterOfMass;
        Vector3 stemPos = attachPointOnStem.position;

        float dist = Vector3.Distance(leafPos, stemPos);

        // NOTE: you can store 'restDistance' at Start() if needed
        float restDistance = 0f;
        float stretch = dist - restDistance;

        if (stretch > breakDistance)
        {
            _overTimer += Time.fixedDeltaTime;
            if (_overTimer >= dwellTime)
            {
                BreakLeaf();
            }
        }
        else
        {
            _overTimer = 0f;
        }
    }

    void BreakLeaf()
    {
        Destroy(_joint);        // frees the leaf
        // TODO: trigger sap, sound, jelly burst, etc.
        // e.g. Leaf3D_PullBreak pop event
    }
}