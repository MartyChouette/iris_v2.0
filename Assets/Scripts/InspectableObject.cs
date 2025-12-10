using UnityEngine;

public class InspectableObject : MonoBehaviour
{
    [TextArea]
    public string description;

    [Tooltip("The 3D model that should be used for the inspect view. " +
             "If left null, this object itself will be duplicated in the viewer.")]
    public GameObject modelOverride;
}