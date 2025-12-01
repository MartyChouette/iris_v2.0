using Unity;
public enum JointSplitMode
{
    DestroyOnCut,          // remove joint when the object is cut
    KeepAnchorSideOnly,    // copy joint only onto the piece containing its anchor
    DuplicateOnBoth,       // give both pieces their own copy
    CustomLogic            // call user hook for special behavior
}