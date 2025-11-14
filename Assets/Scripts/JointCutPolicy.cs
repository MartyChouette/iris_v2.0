using UnityEngine;

[DisallowMultipleComponent]
public class JointCutPolicy : MonoBehaviour
{
    public JointSplitMode mode = JointSplitMode.KeepAnchorSideOnly;

    // Optional callback for advanced logic
    public System.Action<Joint, GameObject, GameObject> OnCutCustom;
}