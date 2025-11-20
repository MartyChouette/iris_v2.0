// File: FlowerPartRuntime.cs
using UnityEngine;

[DisallowMultipleComponent]
public class FlowerPartRuntime : MonoBehaviour
{
    public enum FlowerPartSide
    {
        Center,
        Left,
        Right
        // Add UpperLeft/UpperRight etc later if you want, the ID generation will still work.
    }

    public enum FlowerAttachmentTarget
    {
        None,   // for stem / crown
        Stem,   // leaves attach to stem
        Crown   // petals attach to crown
    }

    [Header("Identity / Matching")]
    [Tooltip("What kind of part this is (stem, crown, leaf, petal).")]
    public FlowerPartKind kind = FlowerPartKind.Leaf;

    [Tooltip("Which core piece this attaches to (None for stem/crown themselves).")]
    public FlowerAttachmentTarget attachmentTarget = FlowerAttachmentTarget.Stem;

    [Tooltip("Broad side classification to avoid hand-typing IDs.")]
    public FlowerPartSide side = FlowerPartSide.Center;

    [Tooltip("Index among parts of the same kind+attachment+side (0,1,2...).")]
    public int indexInGroup = 0;

    [SerializeField, HideInInspector]
    [Tooltip("Auto-generated ID from kind/attachment/side/index. Used by the brain; do not edit.")]
    private string partId = string.Empty;

    public string PartId => partId;

    [Header("Runtime Condition")]
    public FlowerPartCondition condition = FlowerPartCondition.Normal;

    [Tooltip("If false, this part is considered 'missing' (plucked / broken).")]
    public bool isAttached = true;

    [Tooltip("The custom XY tether joint that represents this being connected. " +
             "When it breaks, this part will auto-mark as detached.")]
    public XYTetherJoint attachJoint;

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

    // ───────────────────── Lifecycle ─────────────────────

    void OnValidate()
    {
        // Keep index non-negative
        if (indexInGroup < 0) indexInGroup = 0;

        // Auto-generate a stable ID from the dropdowns
        partId = GeneratePartId(kind, attachmentTarget, side, indexInGroup);
    }

    void Awake()
    {
        if (attachJoint == null)
            attachJoint = GetComponent<XYTetherJoint>();

        // Ensure ID is generated at runtime too
        if (string.IsNullOrEmpty(partId))
            partId = GeneratePartId(kind, attachmentTarget, side, indexInGroup);
    }

    void OnEnable()
    {
        if (attachJoint != null)
            attachJoint.onBroke.AddListener(OnJointBroke);
    }

    void OnDisable()
    {
        if (attachJoint != null)
            attachJoint.onBroke.RemoveListener(OnJointBroke);
    }

    // ───────────────────── Connection logic ─────────────────────

    public void MarkDetached()
    {
        isAttached = false;
    }

    private void OnJointBroke()
    {
        MarkDetached();
    }

    private void Update()
    {
        // Safety: if the joint component is gone, treat as detached.
        if (attachJoint == null && isAttached)
            isAttached = false;
    }

    // ───────────────────── ID helper ─────────────────────

    private static string GeneratePartId(
        FlowerPartKind kind,
        FlowerAttachmentTarget attach,
        FlowerPartSide side,
        int index)
    {
        // Examples:
        //  Stem_Center_0
        //  Crown_Center_0
        //  Leaf_Stem_Left_0
        //  Petal_Crown_Right_2
        return $"{kind}_{attach}_{side}_{Mathf.Max(0, index)}";
    }
}
