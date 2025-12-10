using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class ExplosiveBreathing : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("How fast the breathing cycles.")]
    public float beatsPerMinute = 15f;

    [Tooltip("Offset the starting time (0.0 to 1.0) to desync petals.")]
    [Range(0f, 1f)] public float timeOffset = 0f;

    [Header("Mesh Stretch (Scale)")]
    [Tooltip("Which local axis does the object stretch along? (Usually Y for length).")]
    public Vector3 stretchAxis = Vector3.up;

    [Tooltip("How much to stretch? 0.1 = 10% change.")]
    public float stretchMagnitude = 0.1f;

    [Tooltip("If true, shrinking Y will expand X/Z to keep mass constant.")]
    public bool preserveVolume = true;

    [Header("Positional Offset")]
    [Tooltip("Direction the vertices move while breathing.")]
    public Vector3 moveDirection = Vector3.zero;

    [Tooltip("How far the vertices move.")]
    public float moveMagnitude = 0.05f;

    // Internal
    private MeshFilter mf;
    private Mesh mesh;
    private Vector3[] workingVertices;

    void Start()
    {
        mf = GetComponent<MeshFilter>();
        // Randomize offset if left at 0 for organic variation
        if (timeOffset == 0f) timeOffset = Random.Range(0f, 1f);
    }

    // We use LateUpdate to ensure we run AFTER SquishMove has done its physics calculations
    void LateUpdate()
    {
        if (mf == null) return;
        
        // 1. Get the mesh that SquishMove just updated
        // Note: accessing mesh gives us the instance specific to this object
        mesh = mf.mesh; 
        
        if (mesh == null) return;

        // 2. Get the vertices (currently in "Jelly" state)
        workingVertices = mesh.vertices;

        // 3. Calculate Breathing Factors
        float t = Time.time + timeOffset;
        float freq = beatsPerMinute / 60f * Mathf.PI * 2f;
        float sine = Mathf.Sin(t * freq);

        // Calculate Scale Factors
        float mainScale = 1f + (sine * stretchMagnitude);
        float inverseScale = preserveVolume ? 1f / Mathf.Sqrt(Mathf.Max(0.01f, mainScale)) : 1f;

        Vector3 scaleVector;
        
        // Determine which axis gets the main stretch vs the volume squish
        // We use a dot product approach to support rough arbitrary axes, 
        // but simple IFs cover the 99% use case of Cardinal directions.
        if (stretchAxis == Vector3.up)
        {
            scaleVector = new Vector3(inverseScale, mainScale, inverseScale);
        }
        else if (stretchAxis == Vector3.right)
        {
            scaleVector = new Vector3(mainScale, inverseScale, inverseScale);
        }
        else if (stretchAxis == Vector3.forward)
        {
            scaleVector = new Vector3(inverseScale, inverseScale, mainScale);
        }
        else 
        {
            // Fallback for uniform
            scaleVector = new Vector3(mainScale, mainScale, mainScale);
        }

        // Calculate Position Offset
        Vector3 posOffset = moveDirection.normalized * (sine * moveMagnitude);

        // 4. Apply to vertices
        for (int i = 0; i < workingVertices.Length; i++)
        {
            Vector3 v = workingVertices[i];

            // Apply Scale (Relative to object center 0,0,0)
            v.x *= scaleVector.x;
            v.y *= scaleVector.y;
            v.z *= scaleVector.z;

            // Apply Position
            v += posOffset;

            workingVertices[i] = v;
        }

        // 5. Commit changes back to the mesh
        mesh.vertices = workingVertices;
        
        // Optional: Recalculate bounds if the stretch is extreme, 
        // but usually skipping this is cheaper and fine for small breaths.
        // mesh.RecalculateBounds(); 
    }
}