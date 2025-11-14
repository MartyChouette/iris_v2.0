using UnityEngine;
using UnityEngine.Events;


[RequireComponent(typeof(Rigidbody))]
public class XYTetherJoint : MonoBehaviour
{
    public enum TestSpace { XYOnly, XYZ }
    public enum VelocityMode { Rigidbody, Integrated }

    [System.Flags]
    public enum BreakCriteria
    {
        None = 0,
        Force = 1 << 0,
        Distance = 1 << 1,   // stretch-from-rest
        RelativeSpeed = 1 << 2,
        OwnSpeed = 1 << 3,
        AbsoluteTravel = 1 << 4,
        RelativeTravel = 1 << 5
    }

    [Header("Connection")]
    public Rigidbody connectedBody;

    [Header("Behavior")]
    [Tooltip("Break if STRETCH beyond rest exceeds this.")]
    public float maxDistance = 0.75f;
    public float spring = 1200f;
    public float damper = 60f;

    [Header("Break Conditions")]
    public BreakCriteria criteria = BreakCriteria.Force | BreakCriteria.Distance;
    public TestSpace testSpace = TestSpace.XYOnly;
    public float armDelay = 0.05f;
    public float breakForce = Mathf.Infinity;
    public float relativeSpeedThreshold = 6f;
    public float ownSpeedThreshold = 8f;
    public float absoluteTravelThreshold = 5f;
    public float relativeTravelThreshold = 5f;

    [Header("Velocity Sampling")]
    public VelocityMode velocityMode = VelocityMode.Integrated;
    public float velocitySmoothing = 0.1f;

    [Header("Drive Cap & Projection")]
    public float driveMaxForce = 500f;
    public bool useJointProjection = true;
    public float projectionDistance = 0.02f;

    [Header("Constraints")]
    public bool enforceXYConstraints = true;

    [Header("Events")]
    public UnityEvent onBroke;

    [Header("Debug / Viz")]
    public bool debugLogs = true;
    public bool drawGizmos = true;
    public bool logLiveDistance = false;
    public Color lineColor = new Color(0f, 1f, 1f, 0.9f);
    public Color limitColor = new Color(1f, 0.3f, 0f, 0.6f);

    private Rigidbody rb;
    private ConfigurableJoint joint;
    private float armedAt = -999f;
    private float logTimer;

    private Vector3 prevA, prevB;
    private float absoluteTravel, relativeTravel;
    private Vector3 restAB;
    private Vector3 vA_int, vB_int;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (enforceXYConstraints)
        {
            rb.constraints = RigidbodyConstraints.FreezePositionZ
                           | RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationY
                           | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    void Start() => TryCreateJoint();
    void OnEnable() { if (!joint && connectedBody) TryCreateJoint(); }
    void OnDisable() => DestroyJoint();

    void FixedUpdate()
    {
        if (!joint || !connectedBody) return;

        Vector3 a = transform.TransformPoint(joint.anchor);
        Vector3 b = connectedBody.transform.TransformPoint(joint.connectedAnchor);

        float dt = Mathf.Max(Time.fixedDeltaTime, 1e-5f);
        Vector3 vA_frame = (a - prevA) / dt;
        Vector3 vB_frame = (b - prevB) / dt;
        float alpha = Mathf.Clamp01(dt / Mathf.Max(velocitySmoothing, dt));
        vA_int = Vector3.Lerp(vA_int, vA_frame, alpha);
        vB_int = Vector3.Lerp(vB_int, vB_frame, alpha);

        if (Time.time >= armedAt)
        {
            absoluteTravel += Dist(ApplySpace(a - prevA));
            relativeTravel += Dist(ApplySpace((a - b) - (prevA - prevB)));
        }

        if (logLiveDistance)
        {
            logTimer += dt;
            if (logTimer >= 0.2f)
            {
                float stretch = Mathf.Max(0f, Dist(ApplySpace(a - b)) - Dist(restAB));
                if (debugLogs) Debug.Log($"[XYTetherJoint] stretch={stretch:F3}  | absTravel={absoluteTravel:F2}  relTravel={relativeTravel:F2}", this);
                logTimer = 0f;
            }
        }

        prevA = a;
        prevB = b;

        if (Time.time < armedAt) return;

        // (1) Stretch-from-rest
        if ((criteria & BreakCriteria.Distance) != 0)
        {
            float stretch = Mathf.Max(0f, Dist(ApplySpace(a - b)) - Dist(restAB));
            if (stretch > Mathf.Max(0.0001f, maxDistance))
            {
                ForceBreak($"Stretch {stretch:F3} > {maxDistance:F3}");
                return;
            }
        }

        // choose velocity sources
        Vector3 vA = velocityMode == VelocityMode.Rigidbody ? rb.linearVelocity : vA_int;
        Vector3 vB = velocityMode == VelocityMode.Rigidbody ? connectedBody.linearVelocity : vB_int;

        // (2) Relative speed
        if ((criteria & BreakCriteria.RelativeSpeed) != 0)
        {
            float relSpeed = Dist(ApplySpace(vA - vB));
            if (relSpeed > relativeSpeedThreshold)
            {
                ForceBreak($"RelativeSpeed {relSpeed:F2} > {relativeSpeedThreshold:F2}");
                return;
            }
        }

        // (3) Own speed
        if ((criteria & BreakCriteria.OwnSpeed) != 0)
        {
            float ownSpeed = Dist(ApplySpace(vA));
            if (ownSpeed > ownSpeedThreshold)
            {
                ForceBreak($"OwnSpeed {ownSpeed:F2} > {ownSpeedThreshold:F2}");
                return;
            }
        }

        // (4) Absolute travel
        if ((criteria & BreakCriteria.AbsoluteTravel) != 0)
        {
            if (absoluteTravel >= absoluteTravelThreshold)
            {
                ForceBreak($"AbsoluteTravel {absoluteTravel:F2} ≥ {absoluteTravelThreshold:F2}");
                return;
            }
        }

        // (5) Relative travel
        if ((criteria & BreakCriteria.RelativeTravel) != 0)
        {
            if (relativeTravel >= relativeTravelThreshold)
            {
                ForceBreak($"RelativeTravel {relativeTravel:F2} ≥ {relativeTravelThreshold:F2}");
                return;
            }
        }
    }

    void OnJointBreak(float force)
    {
        if ((criteria & BreakCriteria.Force) != 0 && debugLogs)
            Debug.Log($"[XYTetherJoint] Joint broke by physics force = {force:F1}.", this);
        joint = null;
        onBroke?.Invoke();
    }

    public void SetConnectedBody(Rigidbody body) { connectedBody = body; TryCreateJoint(); }

    public void Retune(float newMaxDist, float newSpring, float newDamper, float newDriveMax = -1f)
    {
        maxDistance = newMaxDist;
        spring = newSpring;
        damper = newDamper;
        if (newDriveMax > 0f) driveMaxForce = newDriveMax;
        TryCreateJoint();
    }

    public void MakeEasierToBreak(
        float newMaxDistance = 0.35f,
        float newBreakForce = 100f,
        float newDriveMax = 300f,
        float newSpring = 800f,
        float newDamper = 40f)
    {
        maxDistance = newMaxDistance;
        breakForce = newBreakForce;
        spring = newSpring;
        damper = newDamper;
        driveMaxForce = newDriveMax;
        TryCreateJoint();
    }

    public void ForceBreak(string reason = "Forced")
    {
        if (debugLogs) Debug.Log($"[XYTetherJoint] Break → {reason}", this);
        DestroyJoint();
        onBroke?.Invoke();
    }

    void TryCreateJoint()
    {
        DestroyJoint();

        if (!connectedBody)
        {
            if (debugLogs) Debug.LogWarning("[XYTetherJoint] No connectedBody assigned.", this);
            return;
        }

        maxDistance = Mathf.Max(0.0001f, maxDistance);
        spring = Mathf.Max(0f, spring);
        damper = Mathf.Max(0f, damper);
        driveMaxForce = Mathf.Max(0f, driveMaxForce);

        joint = gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = connectedBody;
        joint.autoConfigureConnectedAnchor = false;

        joint.anchor = Vector3.zero;
        joint.connectedAnchor = connectedBody.transform.InverseTransformPoint(transform.position);

        joint.xMotion = ConfigurableJointMotion.Free;
        joint.yMotion = ConfigurableJointMotion.Free;
        joint.zMotion = ConfigurableJointMotion.Locked;

        joint.angularXMotion = ConfigurableJointMotion.Locked;
        joint.angularYMotion = ConfigurableJointMotion.Locked;
        joint.angularZMotion = ConfigurableJointMotion.Locked;

        JointDrive drive = new JointDrive
        {
            positionSpring = spring,
            positionDamper = damper,
            maximumForce = driveMaxForce
        };
        joint.xDrive = drive;
        joint.yDrive = drive;
        joint.zDrive = new JointDrive();

        joint.targetPosition = Vector3.zero;

        if (useJointProjection)
        {
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = projectionDistance;
        }
        else
        {
            joint.projectionMode = JointProjectionMode.None;
        }

        joint.breakForce = ((criteria & BreakCriteria.Force) != 0) ? breakForce : Mathf.Infinity;
        joint.breakTorque = Mathf.Infinity;

        Vector3 a = transform.TransformPoint(joint.anchor);
        Vector3 b = connectedBody.transform.TransformPoint(joint.connectedAnchor);
        restAB = ApplySpace(a - b);
        prevA = a; prevB = b;
        absoluteTravel = 0f; relativeTravel = 0f;
        vA_int = vB_int = Vector3.zero;

        armedAt = Time.time + Mathf.Max(0f, armDelay);

        if (debugLogs)
        {
            string bf = float.IsInfinity(joint.breakForce) ? "∞" : joint.breakForce.ToString("F0");
            Debug.Log($"[XYTetherJoint] Created → Spring={spring}, Damper={damper}, StretchMax={maxDistance}, DriveMax={driveMaxForce}, BreakForce={bf}, Criteria={criteria}, VelMode={velocityMode}, Projection={(useJointProjection ? "On" : "Off")}", this);
        }
    }

    void DestroyJoint()
    {
        if (joint)
        {
            if (debugLogs) Debug.Log("[XYTetherJoint] Destroying joint.", this);
            Destroy(joint);
            joint = null;
        }
    }

    Vector3 ApplySpace(Vector3 v) => (testSpace == TestSpace.XYOnly) ? new Vector3(v.x, v.y, 0f) : v;
    static float Dist(Vector3 v) => v.magnitude;

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Vector3 a = transform.position;
        Vector3 b;

        if (joint && connectedBody)
            b = connectedBody.transform.TransformPoint(joint.connectedAnchor);
        else if (connectedBody)
            b = connectedBody.transform.position;
        else
            return;

        Gizmos.color = lineColor; Gizmos.DrawLine(a, b);
        Gizmos.color = limitColor; Gizmos.DrawWireSphere(b, Mathf.Max(0.0001f, maxDistance));
    }
}
