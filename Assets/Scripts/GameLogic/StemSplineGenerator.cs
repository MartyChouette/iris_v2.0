/**
 * @file StemSplineGenerator.cs
 * @brief Generates a Unity Spline that follows the centerline of a flower stem mesh.
 *
 * @details
 * Reads the stem's MeshFilter vertices, projects them onto the stem axis
 * (StemAnchor → StemTip), divides into N cross-sectional bands, averages
 * each band's vertices to find centerline points, and builds a smooth
 * BezierKnot spline with AutoSmooth tangents.
 *
 * Scissors slide along this spline instead of a fixed Y-rail so they
 * naturally follow curved stems.
 *
 * **Fallback:** If fewer than 2 valid centroids are found, a straight
 * 2-knot spline between StemAnchor and StemTip is used (identical to
 * the original line-projection behavior).
 *
 * @ingroup flowers_runtime
 */

using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[DisallowMultipleComponent]
[RequireComponent(typeof(SplineContainer))]
public class StemSplineGenerator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The stem mesh to extract the centerline from.")]
    public MeshFilter stemMeshFilter;

    [Tooltip("Fixed anchor point (connection to Crown). Maps to spline t=0.")]
    public Transform StemAnchor;

    [Tooltip("Dynamic tip (cut end of stem). Maps to spline t=1.")]
    public Transform StemTip;

    [Header("Slicing Settings")]
    [Tooltip("Number of cross-sectional bands used to compute the centerline.")]
    [Range(4, 64)]
    public int bandCount = 12;

    private SplineContainer _container;
    private Spline _spline;

    // Cache to avoid repeated GetComponent
    private void Awake()
    {
        _container = GetComponent<SplineContainer>();
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>
    /// Full rebuild of the spline from the current mesh state.
    /// Call once at startup or whenever the mesh reference changes.
    /// </summary>
    public void GenerateFromMesh()
    {
        EnsureContainer();
        BuildSpline();
    }

    /// <summary>
    /// Rebuild spline after the stem has been cut (mesh/tip changed).
    /// Identical to GenerateFromMesh — kept as a separate entry point
    /// for readability at call sites.
    /// </summary>
    public void RegenerateAfterCut()
    {
        EnsureContainer();
        BuildSpline();
    }

    /// <summary>
    /// Returns the closest point on the spline to a world-space query point,
    /// plus the normalized t at that location.
    /// </summary>
    public Vector3 GetNearestPointOnSpline(Vector3 worldPoint, out float t)
    {
        EnsureContainer();
        if (_spline == null || _spline.Count < 2)
        {
            t = 0f;
            return StemAnchor != null ? StemAnchor.position : transform.position;
        }

        // SplineUtility works in the container's local space.
        float3 localPoint = _container.transform.InverseTransformPoint(worldPoint);

        SplineUtility.GetNearestPoint(
            _spline,
            localPoint,
            out float3 nearestLocal,
            out float nearestT,
            resolution: 8,
            iterations: 3
        );

        t = nearestT;
        return _container.transform.TransformPoint(nearestLocal);
    }

    /// <summary>
    /// Evaluate the spline at normalized t and return the world-space position.
    /// </summary>
    public Vector3 EvaluateWorld(float t)
    {
        EnsureContainer();
        if (_spline == null || _spline.Count < 2)
            return StemAnchor != null ? StemAnchor.position : transform.position;

        t = Mathf.Clamp01(t);
        float3 localPos = SplineUtility.EvaluatePosition(_spline, t);
        return _container.transform.TransformPoint(localPos);
    }

    /// <summary>
    /// Returns the world-space tangent direction at normalized t.
    /// </summary>
    public Vector3 GetTangentAtT(float t)
    {
        EnsureContainer();
        if (_spline == null || _spline.Count < 2)
        {
            if (StemAnchor != null && StemTip != null)
                return (StemTip.position - StemAnchor.position).normalized;
            return Vector3.up;
        }

        t = Mathf.Clamp01(t);
        float3 localTangent = SplineUtility.EvaluateTangent(_spline, t);
        Vector3 worldTangent = _container.transform.TransformDirection(localTangent);
        return worldTangent.normalized;
    }

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------

    private void EnsureContainer()
    {
        if (_container == null)
            _container = GetComponent<SplineContainer>();
    }

    private void BuildSpline()
    {
        // Validate inputs
        if (stemMeshFilter == null || stemMeshFilter.sharedMesh == null ||
            StemAnchor == null || StemTip == null)
        {
            BuildFallbackStraightSpline();
            return;
        }

        Mesh mesh = stemMeshFilter.sharedMesh;
        Vector3[] verts = mesh.vertices;
        Transform meshTf = stemMeshFilter.transform;

        Vector3 anchorWorld = StemAnchor.position;
        Vector3 tipWorld = StemTip.position;
        Vector3 axis = tipWorld - anchorWorld;
        float axisLen = axis.magnitude;

        if (axisLen < 1e-4f || verts.Length < 3)
        {
            BuildFallbackStraightSpline();
            return;
        }

        Vector3 axisDir = axis / axisLen;

        // --- Accumulate per-band sums ---
        Vector3[] bandSum = new Vector3[bandCount];
        int[] bandCount_ = new int[bandCount];

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 wPos = meshTf.TransformPoint(verts[i]);
            float proj = Vector3.Dot(wPos - anchorWorld, axisDir);

            // Clamp vertices outside the anchor→tip range into the end bands
            float normalized = Mathf.Clamp01(proj / axisLen);
            int band = Mathf.Clamp(Mathf.FloorToInt(normalized * bandCount), 0, bandCount - 1);

            bandSum[band] += wPos;
            bandCount_[band]++;
        }

        // --- Compute centroids, skip empty bands ---
        // Use temp array sized to bandCount; actual count may be smaller.
        Vector3[] centroids = new Vector3[bandCount];
        int centroidCount = 0;

        for (int b = 0; b < bandCount; b++)
        {
            if (bandCount_[b] == 0) continue;
            centroids[centroidCount++] = bandSum[b] / bandCount_[b];
        }

        if (centroidCount < 2)
        {
            BuildFallbackStraightSpline();
            return;
        }

        // --- Build spline from centroids ---
        // Convert centroids to SplineContainer local space
        _spline = _container.Spline;
        _spline.Clear();

        for (int i = 0; i < centroidCount; i++)
        {
            float3 localPos = _container.transform.InverseTransformPoint(centroids[i]);
            _spline.Add(new BezierKnot(localPos), TangentMode.AutoSmooth);
        }

        _spline.Closed = false;
    }

    private void BuildFallbackStraightSpline()
    {
        Vector3 a = StemAnchor != null ? StemAnchor.position : transform.position;
        Vector3 b = StemTip != null ? StemTip.position : a + Vector3.up;

        _spline = _container.Spline;
        _spline.Clear();

        float3 localA = _container.transform.InverseTransformPoint(a);
        float3 localB = _container.transform.InverseTransformPoint(b);

        _spline.Add(new BezierKnot(localA), TangentMode.AutoSmooth);
        _spline.Add(new BezierKnot(localB), TangentMode.AutoSmooth);
        _spline.Closed = false;
    }
}
