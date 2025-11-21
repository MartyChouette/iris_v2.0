// File: FlowerPartRuntime.cs
using UnityEngine;

[DisallowMultipleComponent]
public class FlowerPartRuntime : MonoBehaviour
{
    public string PartId;
    public FlowerPartKind kind = FlowerPartKind.Leaf;

    [Header("Runtime State")]
    public bool isAttached = true;
    public FlowerPartCondition condition = FlowerPartCondition.Normal;

    // Assigned by scene, used to trigger game over and re-evaluation
    [HideInInspector] public FlowerSessionController session;
    [HideInInspector] public FlowerGameBrain brain;

    // Crown detection
    private static int CrownLayer = 9; // "CrownCore"

    private void Awake()
    {
        // Find session and brain from parents
        session = GetComponentInParent<FlowerSessionController>();
        brain = GetComponentInParent<FlowerGameBrain>();
    }

    // Called when ANY joint on this object breaks:
    //  - FixedJoint
    //  - ConfigurableJoint
    //  - etc.
    private void OnJointBreak(float breakForce)
    {
        HandleDetached("Unity Joint Break");
    }

    /// <summary>
    /// Called by XYTetherJoint or by stem-joint detector to notify detachment.
    /// </summary>
    public void HandleDetached(string reason)
    {
        if (!isAttached)
            return; // already detached

        isAttached = false;

        // --- Instant-fail logic ---
        bool isCrown = (gameObject.layer == CrownLayer);
        bool isPerfect = brain != null &&
                         brain.IsPartMarkedPerfect(PartId);

        if (isCrown)
        {
            session?.ForceGameOver("Crown detached");
            return;
        }

        if (isPerfect)
        {
            session?.ForceGameOver($"Perfect part '{PartId}' detached");
            return;
        }

        // For non-instant parts, trigger re-evaluation
        session?.EvaluateCurrentFlower();
    }
}