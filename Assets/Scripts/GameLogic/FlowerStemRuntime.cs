// File: FlowerStemRuntime.cs
using UnityEngine;

[DisallowMultipleComponent]
public class FlowerStemRuntime : MonoBehaviour
{
    [Header("Stem measurement")]
    [Tooltip("Where the stem begins (bottom of the held piece after cut).")]
    public Transform stemStart;

    [Tooltip("Where the stem ends (tip of the held stem after cut). This MUST be moved by the cut logic.")]
    public Transform stemEnd;

    [Header("Cut angle reference")]
    [Tooltip("Object whose forward = plane normal. Used for angle measurement.")]
    public Transform cutNormalRef;

    [Tooltip("Local axis used for angle measurement (usually up).")]
    public Vector3 referenceAxisLocal = Vector3.up;

    [Header("Cut Game-Over Threshold")]
    [Tooltip("If the cut happens ABOVE this world-space Y height → instant game over.")]
    public float minAllowedCutY = -9999f;

    /// <summary>
    /// Current length of the *held* stem.
    /// </summary>
    public float CurrentLength
    {
        get
        {
            if (!stemStart || !stemEnd)
                return 0f;

            return Vector3.Distance(stemStart.position, stemEnd.position);
        }
    }

    /// <summary>
    /// Computes the current cut angle relative to world-up.
    /// </summary>
    public float GetCurrentCutAngleDeg(Vector3 worldReferenceAxis)
    {
        if (!cutNormalRef)
            return 0f;

        Vector3 axisWorld = cutNormalRef.TransformDirection(referenceAxisLocal).normalized;
        return Vector3.Angle(axisWorld, worldReferenceAxis.normalized);
    }

    /// ======================================================================
    /// APPLY THE CUT (preview OR real)
    /// ======================================================================
    ///
    /// Called by:
    ///     PlaneBehaviour (preview + final cut)
    ///     MouseBehaviour  (final cut)
    ///
    /// This does NOT slice any mesh. It ONLY updates:
    ///     - angle reference
    ///     - stemEnd position
    ///     - cut height for instant fail logic
    ///
    /// ======================================================================
    public void ApplyCutFromPlane(Vector3 planePoint, Vector3 planeNormal)
    {
        if (!cutNormalRef || !stemEnd)
            return;

        // 1. Update angle
        cutNormalRef.position = planePoint;
        cutNormalRef.rotation = Quaternion.LookRotation(planeNormal, Vector3.up);

        // 2. NEW: Re-position stemEnd to the EXACT cut location
        stemEnd.position = planePoint;

        // 3. Store last cut height for instant fail
        lastCutHeight = planePoint.y;

        // (No scoring or game-over here; session handles that)
    }

    /// <summary>
    /// Where the last plane intersected world space.
    /// Used by FlowerSessionController to check "cut too high".
    /// </summary>
    [HideInInspector] public float lastCutHeight = -99999f;
}
