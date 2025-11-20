// File: FlowerPartRuntime.cs
using UnityEngine;

[DisallowMultipleComponent]
public class FlowerPartRuntime : MonoBehaviour
{
    [Header("Identity / Matching")]
    [Tooltip("Unique ID for this part so the brain can match it to an ideal spec.")]
    public string partId;

    public FlowerPartKind kind = FlowerPartKind.Leaf;

    [Header("Runtime Condition")]
    public FlowerPartCondition condition = FlowerPartCondition.Normal;

    [Tooltip("If false, this part is considered 'missing' (plucked / broken).")]
    public bool isAttached = true;

    [Tooltip("Optional: the joint that represents this being connected. " +
             "If destroyed/disabled you can let this auto-mark as detached.")]
    public Joint attachJoint;

    [Header("Gameplay Flags (check boxes)")]
    [Tooltip("If true, bad treatment of this part can cause instant game over.")]
    public bool canCauseGameOver = false;

    [Tooltip("If true, this part is 'special' (rare petal, etc).")]
    public bool isSpecial = false;

    [Tooltip("If true, this part participates in scoring.")]
    public bool contributesToScore = true;

    [Tooltip("If true, this part is allowed to exist in a withered state in the ideal design.")]
    public bool allowedWithered = true;

    [Tooltip("If false, removing this part is considered a bad thing for score.")]
    public bool allowedMissing = false;

    [Range(0f, 1f)]
    [Tooltip("How much this part matters to score relative to other parts.")]
    public float scoreWeight = 1f;

    [Header("Debug / Ideal Ref (optional)")]
    [Tooltip("Where this part would sit in the 'ideal' flower (for visual authoring).")]
    public Vector3 idealLocalPosition;
    public Vector3 idealLocalEuler;

    // Called by your joint-break / pluck logic when the joint snaps.
    public void MarkDetached()
    {
        isAttached = false;
    }

    private void Update()
    {
        // Optional auto-detection if you destroy the joint on break:
        if (attachJoint == null && isAttached)
        {
            // It used to be attached, but the joint is gone.
            isAttached = false;
        }
    }
}
