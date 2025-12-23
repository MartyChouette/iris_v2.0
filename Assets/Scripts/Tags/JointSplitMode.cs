/**
 * @file JointSplitMode.cs
 * @brief JointSplitMode script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup flowers_runtime
 */

using Unity;
public enum JointSplitMode
{
    DestroyOnCut,          // remove joint when the object is cut
    KeepAnchorSideOnly,    // copy joint only onto the piece containing its anchor
    DuplicateOnBoth,       // give both pieces their own copy
    CustomLogic            // call user hook for special behavior
}