using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Suppresses Unity's built-in joint break forces during cuts to prevent joints from breaking due to physics jolt.
/// Works with FixedJoint, ConfigurableJoint, HingeJoint, SpringJoint, etc.
/// </summary>
public static class JointCutSuppressor
{
    private static Dictionary<Joint, float> _savedBreakForces = new Dictionary<Joint, float>();
    private static Dictionary<Joint, float> _savedBreakTorques = new Dictionary<Joint, float>();
    private static bool _isSuppressed = false;

    /// <summary>
    /// Suppress all joint breaks by setting breakForce/breakTorque to infinity.
    /// Call this BEFORE cutting.
    /// </summary>
    public static void SuppressAllJoints(GameObject rootObject)
    {
        if (_isSuppressed) return; // Already suppressed
        
        _savedBreakForces.Clear();
        _savedBreakTorques.Clear();
        
        // Find all joints under the root
        var joints = rootObject.GetComponentsInChildren<Joint>(true);
        
        foreach (var joint in joints)
        {
            if (joint == null) continue;
            
            // Save original values
            _savedBreakForces[joint] = joint.breakForce;
            _savedBreakTorques[joint] = joint.breakTorque;
            
            // Set to infinity to prevent breaking
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
        }
        
        _isSuppressed = true;
        
        Debug.Log($"[JointCutSuppressor] Suppressed {joints.Length} joints under '{rootObject.name}'");
    }
    
    /// <summary>
    /// Suppress joints globally (all joints in scene).
    /// Use this if you don't have a specific root object.
    /// </summary>
    public static void SuppressAllJointsGlobal()
    {
        if (_isSuppressed) return;
        
        _savedBreakForces.Clear();
        _savedBreakTorques.Clear();
        
        // Find ALL joints in the scene
        var joints = Object.FindObjectsByType<Joint>(FindObjectsSortMode.None);
        
        foreach (var joint in joints)
        {
            if (joint == null) continue;
            
            // Save original values
            _savedBreakForces[joint] = joint.breakForce;
            _savedBreakTorques[joint] = joint.breakTorque;
            
            // Set to infinity to prevent breaking
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
        }
        
        _isSuppressed = true;
        
        Debug.Log($"[JointCutSuppressor] Suppressed {joints.Length} joints globally");
    }
    
    /// <summary>
    /// Restore all joint break forces to their original values.
    /// Call this AFTER cutting is complete.
    /// </summary>
    public static void RestoreAllJoints()
    {
        if (!_isSuppressed) return;
        
        int restored = 0;
        
        foreach (var kvp in _savedBreakForces)
        {
            var joint = kvp.Key;
            if (joint == null) continue; // Joint was destroyed
            
            try
            {
                joint.breakForce = kvp.Value;
                restored++;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[JointCutSuppressor] Failed to restore breakForce on '{joint.name}': {ex.Message}");
            }
        }
        
        foreach (var kvp in _savedBreakTorques)
        {
            var joint = kvp.Key;
            if (joint == null) continue;
            
            try
            {
                joint.breakTorque = kvp.Value;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[JointCutSuppressor] Failed to restore breakTorque on '{joint.name}': {ex.Message}");
            }
        }
        
        _savedBreakForces.Clear();
        _savedBreakTorques.Clear();
        _isSuppressed = false;
        
        Debug.Log($"[JointCutSuppressor] Restored {restored} joints");
    }
}
