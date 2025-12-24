using UnityEngine;

/// <summary>
/// DEBUG: Continuously enforces kinematic state on a Rigidbody.
/// This helps us identify if something is resetting the kinematic state.
/// </summary>
public class KinematicStateEnforcer : MonoBehaviour
{
    public Rigidbody targetRb;
    
    private void Awake()
    {
        if (targetRb == null)
            targetRb = GetComponent<Rigidbody>();
    }
    
    private void FixedUpdate()
    {
        if (targetRb == null) return;
        
        // Continuously enforce kinematic state
        if (!targetRb.isKinematic)
        {
            Debug.LogWarning($"[KinematicStateEnforcer] Rigidbody '{targetRb.name}' was reset to non-kinematic! Forcing back to kinematic.", targetRb);
            targetRb.isKinematic = true;
        }
        
        if (targetRb.useGravity)
        {
            Debug.LogWarning($"[KinematicStateEnforcer] Rigidbody '{targetRb.name}' had gravity enabled! Disabling.", targetRb);
            targetRb.useGravity = false;
        }
    }
    
    private void Update()
    {
        if (targetRb == null) return;
        
        // Also check in Update (in case something resets it between FixedUpdate calls)
        if (!targetRb.isKinematic)
        {
            Debug.LogWarning($"[KinematicStateEnforcer] Rigidbody '{targetRb.name}' was reset to non-kinematic in Update! Forcing back.", targetRb);
            targetRb.isKinematic = true;
        }
        
        if (targetRb.useGravity)
        {
            Debug.LogWarning($"[KinematicStateEnforcer] Rigidbody '{targetRb.name}' had gravity enabled in Update! Disabling.", targetRb);
            targetRb.useGravity = false;
        }
    }
}
