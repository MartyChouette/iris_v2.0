using UnityEngine;

public class ParametricBreathing : MonoBehaviour
{
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

    // Internal state
    private Vector3 initialScale;
    private Vector3 initialPos; // Used for localPosition offset

    void Start()
    {
        initialScale = transform.localScale;
        // We track local position if this is a child, 
        // but note: modifying position on a Rigidbody via Transform can be jittery.
        // We will use a visual offset approach if needed, but simple localPos is usually fine for breathing.
        initialPos = transform.localPosition;
    }

    void Update()
    {
        // 1. Calculate the Sine Wave (0..1..0..-1..0)
        float t = Time.time + timeOffset;
        float freq = beatsPerMinute / 60f * Mathf.PI * 2f;
        float sine = Mathf.Sin(t * freq);

        // ────── Stretch Logic ──────
        // Normalize the sine to 0..1 for easier stretch math, or keep -1..1 for in/out
        // Let's use -1..1 so it shrinks and grows.
        
        float stretchFactor = 1f + (sine * stretchMagnitude);
        
        // Calculate volume preservation: Scale_Perpendicular = 1 / sqrt(Scale_Parallel)
        float inverseFactor = preserveVolume ? 1f / Mathf.Sqrt(Mathf.Max(0.01f, stretchFactor)) : 1f;

        // Apply to axes based on the chosen "Stretch Axis"
        // If Axis is Y (0,1,0), we apply stretchFactor to Y, and inverse to X and Z.
        
        Vector3 targetScale = initialScale;
        
        // This is a simplified volume preservation for cardinal axes. 
        // If you need arbitrary diagonal axes, the math gets complex (Matrix4x4). 
        // Assuming Petals are oriented with Y as "Length":
        
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
        else // Z Axis or arbitrary, default to uniform for simplicity or Z stretch
        {
            targetScale.x *= inverseFactor;
            targetScale.y *= inverseFactor;
            targetScale.z *= stretchFactor;
        }

        transform.localScale = targetScale;


        // ────── Position Logic ──────
        // We move slightly in the direction specified
        Vector3 posOffset = moveDirection.normalized * (sine * moveMagnitude);
        
        // Important: If this object has a Rigidbody, directly setting transform can fight physics.
        // However, for small breathing "drifts", modifying localPosition is often acceptable 
        // if the Rigidbody is kinematic or connected via joints that allow play.
        // If the petal is loose, this might cause 'ghost' forces. 
        
        // Safer approach: Add this offset to the Transform, 
        // effectively shifting the "Center" of the jelly simulation.
        transform.localPosition = initialPos + posOffset;
    }
}