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

    [System.Serializable]
    public class FloatEvent : UnityEvent<float> { }

    // ───────────────────────── Connection ─────────────────────────

    [Header("Connection")]
    public Rigidbody connectedBody;

    [Header("Behavior")]
    [Tooltip("Break if STRETCH beyond rest exceeds this.")]
    public float maxDistance = 0.75f;
    public float spring = 1200f;
    public float damper = 60f;

    // ───────────────────────── Break Conditions ─────────────────────────

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

    // ───────────────────────── Feel / Nintendo-ish Stuff ─────────────────────────

    [Header("Soft Zone / Adaptive Tension")]
    [Tooltip("If true, spring/damper are scaled based on stretch/maxDistance using tensionCurve.")]
    public bool useAdaptiveDrive = false;

    [Tooltip("Portion of maxDistance that counts as 'soft zone'. 0.6 = first 60% is gentle.")]
    [Range(0f, 1f)] public float softZoneFraction = 0.6f;

    [Tooltip("X: normalized stretch (0..1), Y: tension (0..1).")]
    public AnimationCurve tensionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Spring multiplier at tension=0 (completely slack).")]
    public float minSpringMultiplier = 0.25f;

    [Tooltip("Spring multiplier at tension=1 (max stretch).")]
    public float maxSpringMultiplier = 1.5f;

    [Tooltip("Damper multiplier at tension=0 (completely slack).")]
    public float minDamperMultiplier = 0.25f;

    [Tooltip("Damper multiplier at tension=1 (max stretch).")]
    public float maxDamperMultiplier = 1.5f;

    [Header("Pluck / Pop Feel")]
    [Tooltip("If true, holding past a stretch fraction for dwell time will auto-break (pluck).")]
    public bool usePluckDwell = false;

    [Tooltip("Stretch fraction (0..1 of maxDistance) at which pluck dwell starts counting.")]
    [Range(0f, 1f)] public float pluckThresholdFraction = 0.8f;

    [Tooltip("Time we must stay above pluckThresholdFraction before auto-break.")]
    public float pluckDwellSeconds = 0.08f;

    [Tooltip("If true, break only when tension FALLS back below a threshold after being pulled high (pop on release).")]
    public bool breakOnReleaseFromHighStretch = false;

    [Tooltip("Stretch fraction (0..1) we must drop BELOW after having exceeded pluckThresholdFraction to pop.")]
    [Range(0f, 1f)] public float releasePopThresholdFraction = 0.4f;

    [Header("Feel Events")]
    [Tooltip("Fired with normalized tension (0..1) each FixedUpdate. Use for audio, haptics, etc.")]
    public FloatEvent onTensionChanged;

    [Header("Engagement Scaling")]
    [Tooltip("If true, scale all forces/break checks by an engagement factor.")]
    public bool useEngagementScaling = true;

    [Tooltip("Override: how strong this joint is when directly engaged (if 0, use 1).")]
    [Range(0f, 2f)] public float engagedMultiplier = 1f;

    [Tooltip("Override for passive intensity (if 0, use InteractionEngagement.passiveIntensity).")]
    [Range(0f, 1f)] public float passiveMultiplierOverride = 0f;

    [Tooltip("If true, joint will only be allowed to break while engaged.")]
    public bool onlyBreakWhenEngaged = true;

    private InteractionEngagement _engagement;

    // ───────────────────────── Static cut suppression ─────────────────────────
    // During stem cuts / juice moments we suppress physics-based breaks
    // so leaves don't pop off.

    public static bool cutBreakSuppressed = false;

    /// <summary>Read-only so external systems (JuiceMomentController) can check state.</summary>
    public static bool IsCutBreakSuppressed => cutBreakSuppressed;

    /// <summary>
    /// Globally toggle suppression. When ON:
    /// - All joints have breakForce/torque set to Infinity.
    /// - Their travel / pluck timers are reset so they don't immediately pop
    ///   on the first frame after suppression ends.
    /// </summary>
    public static void SetCutBreakSuppressed(bool on)
    {
        cutBreakSuppressed = on;

        var all = FindObjectsByType<XYTetherJoint>(FindObjectsSortMode.None);
        foreach (var t in all)
        {
            if (t == null) continue;

            if (on)
            {
                // Clear accumulated break conditions so we don't insta-snap
                // when suppression is lifted.
                t.ResetBreakAccumulators();
            }

            t.ApplyBreakForceToJoint();
        }
    }

    // ───────────────────────── Events / Debug ─────────────────────────

    [Header("Events")]
    public UnityEvent onBroke;

    [Header("Debug / Viz")]
    public bool debugLogs = true;
    public bool drawGizmos = true;
    public bool logLiveDistance = false;
    public Color lineColor = new Color(0f, 1f, 1f, 0.9f);
    public Color limitColor = new Color(1f, 0.3f, 0f, 0.6f);

    // ───────────────────────── Internals ─────────────────────────

    private Rigidbody rb;
    private ConfigurableJoint joint;
    private float armedAt = -999f;
    private float logTimer;

    private Vector3 prevA, prevB;
    private float absoluteTravel, relativeTravel;
    private Vector3 restAB;
    private Vector3 vA_int, vB_int;

    // for adaptive drive
    private float baseSpring;
    private float baseDamper;

    // for pluck / pop
    private float pluckTimer;
    private bool wasAbovePluckThreshold;

    private float lastTension; // for event sanity

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (enforceXYConstraints)
        {
            // don't overwrite constraints, only add freezes
            rb.constraints |= RigidbodyConstraints.FreezePositionZ
                            | RigidbodyConstraints.FreezeRotationX
                            | RigidbodyConstraints.FreezeRotationY
                            | RigidbodyConstraints.FreezeRotationZ;
        }

        _engagement = GetComponent<InteractionEngagement>();
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

        // travel accumulation once armed
        if (Time.time >= armedAt)
        {
            absoluteTravel += Dist(ApplySpace(a - prevA));
            relativeTravel += Dist(ApplySpace((a - b) - (prevA - prevB)));
        }

        prevA = a;
        prevB = b;

        // Compute stretch and normalized tension for feel logic.
        float restDistance = Dist(restAB);
        float currentDistance = Dist(ApplySpace(a - b));
        float stretch = Mathf.Max(0f, currentDistance - restDistance);
        float stretchNorm = 0f;
        if (maxDistance > 0.0001f)
            stretchNorm = Mathf.Clamp01(stretch / maxDistance);

        // ───── Nintendo-ish feel: adaptive tension & pluck / pop ─────

        if (useAdaptiveDrive && joint != null)
        {
            float tension = tensionCurve != null ? tensionCurve.Evaluate(stretchNorm) : stretchNorm;
            tension = Mathf.Clamp01(tension);

            float springMult = Mathf.Lerp(minSpringMultiplier, maxSpringMultiplier, tension);
            float damperMult = Mathf.Lerp(minDamperMultiplier, maxDamperMultiplier, tension);

            // scale by engagement factor (1.0 when engaged, ~0.4 when passive, etc.)
            float engageFactor = GetEngagementFactor();

            var drive = joint.xDrive;
            drive.positionSpring = baseSpring * springMult * engageFactor;
            drive.positionDamper = baseDamper * damperMult * engageFactor;
            joint.xDrive = drive;
            joint.yDrive = drive;

            if (onTensionChanged != null)
            {
                onTensionChanged.Invoke(tension);
                lastTension = tension;
            }
        }

        if (Time.time >= armedAt)
        {
            if (usePluckDwell)
            {
                if (stretchNorm >= pluckThresholdFraction)
                {
                    pluckTimer += dt;
                    if (pluckTimer >= pluckDwellSeconds)
                    {
                        ForceBreak($"Pluck dwell (stretchNorm={stretchNorm:F2})");
                        return;
                    }
                }
                else
                {
                    pluckTimer = 0f;
                }
            }

            if (breakOnReleaseFromHighStretch)
            {
                if (stretchNorm >= pluckThresholdFraction)
                {
                    wasAbovePluckThreshold = true;
                }
                else if (wasAbovePluckThreshold && stretchNorm <= releasePopThresholdFraction)
                {
                    ForceBreak($"Release pop (stretchNorm={stretchNorm:F2})");
                    return;
                }
            }
        }

        // Optional live distance logs
        if (logLiveDistance)
        {
            logTimer += dt;
            if (logTimer >= 0.2f)
            {
                if (debugLogs)
                    Debug.Log($"[XYTetherJoint] stretch={stretch:F3}  | absTravel={absoluteTravel:F2}  relTravel={relativeTravel:F2}", this);
                logTimer = 0f;
            }
        }

        if (Time.time < armedAt) return;

        // choose velocity sources
        Vector3 vA = velocityMode == VelocityMode.Rigidbody ? rb.linearVelocity : vA_int;
        Vector3 vB = velocityMode == VelocityMode.Rigidbody ? connectedBody.linearVelocity : vB_int;

        // (1) Stretch-from-rest
        if ((criteria & BreakCriteria.Distance) != 0)
        {
            if (stretch > Mathf.Max(0.0001f, maxDistance))
            {
                ForceBreak($"Stretch {stretch:F3} > {maxDistance:F3}");
                return;
            }
        }

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

    float GetEngagementFactor()
    {
        if (!useEngagementScaling)
            return 1f;

        float engaged = 1f;
        float passive = 0.25f;

        if (_engagement != null)
        {
            engaged = 1f;
            passive = _engagement.passiveIntensity;
        }

        if (engagedMultiplier > 0f) engaged = engagedMultiplier;
        if (passiveMultiplierOverride > 0f) passive = passiveMultiplierOverride;

        bool isEngaged = _engagement != null && _engagement.isEngaged;
        return isEngaged ? engaged : passive;
    }

    // ───────────────────────── Break callbacks ─────────────────────────

    void OnJointBreak(float force)
    {
        // During a cut, completely ignore physics auto-breaks.
        if (cutBreakSuppressed)
        {
            if (debugLogs)
                Debug.Log($"[XYTetherJoint] OnJointBreak suppressed (force={force:F1}) due to cutBreakSuppressed.", this);
            return;
        }

        if ((criteria & BreakCriteria.Force) != 0 && debugLogs)
            Debug.Log($"[XYTetherJoint] Joint broke by physics force = {force:F1}.", this);

        // Play audio if present
        TriggerBreakAudio();

        joint = null;
        onBroke?.Invoke();
    }

    /// <summary>
    /// Called by scripts to intentionally break the joint.
    /// </summary>
    public void ForceBreak(string reason = "Forced")
    {
        // Also suppress scripted breaks during a cut, so nothing detaches mid-slice.
        if (cutBreakSuppressed)
        {
            if (debugLogs)
                Debug.Log($"[XYTetherJoint] ForceBreak \"{reason}\" suppressed due to cutBreakSuppressed.", this);
            return;
        }

        // optionally suppress breaks if this part is not engaged
        if (onlyBreakWhenEngaged)
        {
            bool isEngagedNow = (_engagement != null && _engagement.isEngaged);
            if (!isEngagedNow)
            {
                if (debugLogs)
                    Debug.Log($"[XYTetherJoint] Suppressed break \"{reason}\" because not engaged.", this);
                return;
            }
        }

        if (debugLogs) Debug.Log($"[XYTetherJoint] Break → {reason}", this);

        DestroyJoint();

        // Play audio if present
        TriggerBreakAudio();

        onBroke?.Invoke();
    }

    /// <summary>
    /// Finds a JointBreakAudioResponder on this GameObject and fires its audio.
    /// This is used both for physics breaks (OnJointBreak) and scripted ForceBreak.
    /// </summary>
    private void TriggerBreakAudio()
    {
        var audio = GetComponent<JointBreakAudioResponder>();
        if (audio != null)
        {
            audio.OnJointBroken();
        }
    }

    // ───────────────────────── Public API ─────────────────────────

    public void SetConnectedBody(Rigidbody body)
    {
        connectedBody = body;
        TryCreateJoint();
    }

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

    // ───────────────────────── Joint Setup ─────────────────────────

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

        baseSpring = spring;
        baseDamper = damper;

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
            positionSpring = baseSpring,
            positionDamper = baseDamper,
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

        // apply correct breakForce / breakTorque based on cut suppression
        ApplyBreakForceToJoint();

        Vector3 a = transform.TransformPoint(joint.anchor);
        Vector3 b = connectedBody.transform.TransformPoint(joint.connectedAnchor);
        restAB = ApplySpace(a - b);
        prevA = a; prevB = b;
        absoluteTravel = 0f; relativeTravel = 0f;
        vA_int = vB_int = Vector3.zero;

        pluckTimer = 0f;
        wasAbovePluckThreshold = false;

        armedAt = Time.time + Mathf.Max(0f, armDelay);

        if (debugLogs)
        {
            string bf = float.IsInfinity(joint.breakForce) ? "∞" : joint.breakForce.ToString("F0");
            Debug.Log($"[XYTetherJoint] Created → Spring={spring}, Damper={damper}, StretchMax={maxDistance}, DriveMax={driveMaxForce}, BreakForce={bf}, Criteria={criteria}, VelMode={velocityMode}, Projection={(useJointProjection ? "On" : "Off")}", this);
        }
    }

    /// <summary>
    /// Sync joint.breakForce / breakTorque with the current suppression state.
    /// </summary>
    void ApplyBreakForceToJoint()
    {
        if (!joint) return;

        if (cutBreakSuppressed)
        {
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
        }
        else
        {
            joint.breakForce = ((criteria & BreakCriteria.Force) != 0) ? breakForce : Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
        }
    }

    /// <summary>
    /// Used when turning suppression ON: clear accumulated travel/pluck state
    /// and re-arm the joint so it doesn't instantly pop when suppression ends.
    /// </summary>
    void ResetBreakAccumulators()
    {
        absoluteTravel = 0f;
        relativeTravel = 0f;
        pluckTimer = 0f;
        wasAbovePluckThreshold = false;
        armedAt = Time.time + Mathf.Max(0f, armDelay);
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
