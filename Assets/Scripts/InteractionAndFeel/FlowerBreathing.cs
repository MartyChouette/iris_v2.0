using UnityEngine;

public class FlowerBreathing : MonoBehaviour
{
    [Header("Rhythm")]
    public float beatsPerMinute = 20f;
    [Range(0f, 1f)] public float timeOffset = 0f;

    [Header("Motion Settings")]
    public Vector3 stretchAxis = Vector3.up;
    public float stretchMagnitude = 0.1f;
    public Vector3 moveDirection = Vector3.zero;
    public float moveMagnitude = 0.05f;
    public bool preserveVolume = true;

    [Header("Safety & Pivot")]
    [Range(-0.5f, 0.5f)] public float pivotY = -0.5f;
    public bool stopWhenGrabbed = true;
    public bool preventJointBreak = true;

    // --- Internal State ---
    private Vector3 _initialScale;
    private Vector3 _initialLocalPos;
    private float _currentIntensity = 1f;

    // --- Cached Components ---
    private GrabPull _grabPull;
    private XYTetherJoint _tether;
    private FlowerPartRuntime _partRuntime;
    private float _tetherBaseMaxDist;

    void Start()
    {
        _initialScale = transform.localScale;
        _initialLocalPos = transform.localPosition;
        if (timeOffset == 0f) timeOffset = Random.Range(0f, 1f);

        _grabPull = GetComponent<GrabPull>();
        _tether = GetComponent<XYTetherJoint>();
        _partRuntime = GetComponent<FlowerPartRuntime>();

        if (_tether != null)
        {
            _tetherBaseMaxDist = _tether.maxDistance;
        }
    }

    void Update()
    {
        // 1. KILL SWITCH (Disconnect logic)
        if (_partRuntime != null && !_partRuntime.isAttached)
        {
            transform.localScale = _initialScale;
            this.enabled = false;
            return;
        }

        // 2. INPUT CHECK
        bool isHeld = (_grabPull != null && _grabPull.grabbing);

        // --- FIX 1: SMOOTH TRANSITION ---
        // Instead of snapping to 0, we Lerp very quickly (speed 10). 
        // This removes the visual "pop" in scale.
        float targetIntensity = (stopWhenGrabbed && isHeld) ? 0f : 1f;
        _currentIntensity = Mathf.MoveTowards(_currentIntensity, targetIntensity, Time.deltaTime * 10f);

        // OPTIMIZATION: If effectively stopped, reset to rest pose and skip math
        if (_currentIntensity <= 0.001f)
        {
            transform.localScale = _initialScale;

            // Only force position reset if we are NOT holding it
            if (!isHeld) transform.localPosition = _initialLocalPos;

            // --- FIX 2: SAFE TETHER RESET ---
            // We only reset the tether limit when the intensity is fully zero.
            // This prevents the "Physics Pop" on the very first frame of the grab.
            if (_tether != null) _tether.maxDistance = _tetherBaseMaxDist;
            return;
        }

        // 3. CALCULATE SINE WAVE
        float t = Time.time + timeOffset;
        float freq = beatsPerMinute / 60f * Mathf.PI * 2f;
        float sine = Mathf.Sin(t * freq);

        // 4. APPLY STRETCH
        float activeStretch = stretchMagnitude * _currentIntensity;
        float stretchFactor = 1f + (sine * activeStretch);
        float inverseFactor = preserveVolume ? 1f / Mathf.Sqrt(Mathf.Max(0.01f, stretchFactor)) : 1f;

        Vector3 targetScale = _initialScale;
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
            targetScale.x *= inverseFactor;
            targetScale.y *= inverseFactor;
            targetScale.z *= stretchFactor;
        }
        transform.localScale = targetScale;

        // 5. APPLY POSITION (Only if not held)
        if (!isHeld)
        {
            float heightChange = targetScale.y - _initialScale.y;
            float pivotCorrectionY = heightChange * -pivotY;
            Vector3 pivotOffset = transform.up * pivotCorrectionY;

            float activeMove = moveMagnitude * _currentIntensity;
            Vector3 swayOffset = moveDirection.normalized * (sine * activeMove);

            transform.localPosition = _initialLocalPos + pivotOffset + swayOffset;

            if (preventJointBreak && _tether != null)
            {
                float extraRoom = swayOffset.magnitude + Mathf.Abs(pivotCorrectionY);
                _tether.maxDistance = _tetherBaseMaxDist + (extraRoom * 1.2f);
            }
        }
        // --- FIX 3: REMOVED ELSE BLOCK ---
        // We no longer force-reset the tether maxDistance while held inside the main loop.
        // We let the "Safety Buffer" stay active for the split second it takes 
        // for the intensity to fade out. This prevents the physics engine 
        // from violently snapping the leaf back during the grab frame.
    }
}