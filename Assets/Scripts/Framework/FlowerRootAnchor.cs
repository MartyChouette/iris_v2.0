using UnityEngine;

public class FlowerRootAnchor : MonoBehaviour
{
    [Header("Bottom piece (stem base)")]
    public bool lockBottomPiece = true;
    public RigidbodyConstraints bottomConstraints = RigidbodyConstraints.FreezeAll;
    public bool bottomUseGravity = false;
    public bool bottomIsKinematic = true;

    [Header("Cut-off chunks (top pieces)")]
    public bool makeTopDynamic = true;
    public bool topUseGravity = true;
    public RigidbodyConstraints topConstraints = RigidbodyConstraints.None;
}
