// File: FlowerStemRuntime.cs
using UnityEngine;

[DisallowMultipleComponent]
public class FlowerStemRuntime : MonoBehaviour
{
    [Header("Stem measurement")]
    [Tooltip("Start of stem (e.g. where it meets the crown).")]
    public Transform stemStart;

    [Tooltip("End of stem after cut (tip).")]
    public Transform stemEnd;

    [Header("Cut angle measurement")]
    [Tooltip("Transform whose 'up' or 'forward' represents the cut plane normal.")]
    public Transform cutNormalRef;

    [Tooltip("Axis in local space used for angle measurement (usually Vector3.up).")]
    public Vector3 referenceAxisLocal = Vector3.up;

    public float CurrentLength
    {
        get
        {
            if (stemStart == null || stemEnd == null) return 0f;
            return Vector3.Distance(stemStart.position, stemEnd.position);
        }
    }

    /// <summary>
    /// Returns current cut angle in degrees relative to the given world-space axis (usually world up).
    /// </summary>
    public float GetCurrentCutAngleDeg(Vector3 worldReferenceAxis)
    {
        if (cutNormalRef == null)
            return 0f;

        Vector3 localAxis = referenceAxisLocal;
        Vector3 stemAxisWorld = cutNormalRef.TransformDirection(localAxis);
        stemAxisWorld.Normalize();
        worldReferenceAxis.Normalize();

        float angle = Vector3.Angle(stemAxisWorld, worldReferenceAxis);
        return angle;
    }
}