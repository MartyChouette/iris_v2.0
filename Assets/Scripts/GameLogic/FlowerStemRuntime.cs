/**
 * @file FlowerStemRuntime.cs
 * @brief FlowerStemRuntime script.
 * @details
 * - Renamed stemStart -> StemAnchor (The fixed point attached to Crown)
 * - Renamed stemEnd   -> StemTip    (The moving point defined by the cut)
 * @ingroup flowers_runtime
 */

using UnityEngine;

[DisallowMultipleComponent]
public class FlowerStemRuntime : MonoBehaviour
{
    [Header("Stem Measurement")]
    [Tooltip("The fixed anchor point (e.g., connection to the Crown/Flower Head). The cut logic will 'KEEP' the piece closest to this point.")]
    public Transform StemAnchor; // Was stemStart

    [Tooltip("The dynamic end of the stem (the cut point). This MUST be moved by the cut logic to the new cut location.")]
    public Transform StemTip;    // Was stemEnd

    [Header("Spline (Optional)")]
    [Tooltip("If assigned, stem queries use the spline centerline instead of a straight line.")]
    public StemSplineGenerator splineGenerator;

    [Header("Cut angle reference")]
    [Tooltip("Object whose forward = plane normal. Used for angle measurement.")]
    public Transform cutNormalRef;

    [Tooltip("Local axis used for angle measurement (usually up).")]
    public Vector3 referenceAxisLocal = Vector3.up;

    [Header("Cut Game-Over Threshold")]
    [Tooltip("If the cut happens ABOVE this world-space Y height → instant game over.")]
    public float minAllowedCutY = -9999f;

    // ----------------------------------------------------------------------
    // Derived Properties
    // ----------------------------------------------------------------------

    public float CurrentLength
    {
        get
        {
            if (!StemAnchor || !StemTip)
                return 0f;

            return Vector3.Distance(StemAnchor.position, StemTip.position);
        }
    }

    public float GetCurrentCutAngleDeg(Vector3 worldReferenceAxis)
    {
        if (!cutNormalRef)
            return 0f;

        Vector3 axisWorld = cutNormalRef.TransformDirection(referenceAxisLocal).normalized;
        return Vector3.Angle(axisWorld, worldReferenceAxis.normalized);
    }

    // ----------------------------------------------------------------------
    // Cut Application
    // ----------------------------------------------------------------------

    public void ApplyCutFromPlane(Vector3 planePoint, Vector3 planeNormal)
    {
        if (!cutNormalRef || !StemTip)
            return;

        // 1. Update angle
        cutNormalRef.position = planePoint;
        cutNormalRef.rotation = Quaternion.LookRotation(planeNormal, Vector3.up);

        // 2. NEW: Re-position StemTip to the EXACT cut location
        StemTip.position = planePoint;

        // 3. Store last cut height for instant fail
        lastCutHeight = planePoint.y;

        // 4. Rebuild spline to match the now-shorter stem
        if (splineGenerator != null)
            splineGenerator.RegenerateAfterCut();
    }

    public Vector3 GetClosestPointOnStem(Vector3 worldPoint)
    {
        // Spline path: use curved centerline when available
        if (splineGenerator != null)
            return splineGenerator.GetNearestPointOnSpline(worldPoint, out _);

        // Fallback: straight line projection
        if (StemAnchor == null || StemTip == null)
            return transform.position;

        Vector3 a = StemAnchor.position;
        Vector3 b = StemTip.position;
        Vector3 ab = b - a;

        float abLenSq = ab.sqrMagnitude;
        if (abLenSq < 1e-6f)
            return a;

        float t = Vector3.Dot(worldPoint - a, ab) / abLenSq;
        t = Mathf.Clamp01(t);

        return a + ab * t;
    }

    [HideInInspector] public float lastCutHeight = -99999f;
}