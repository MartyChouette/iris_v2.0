/**
 * @file FlowerEnums.cs
 * @brief FlowerEnums script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup flowers_runtime
 */

// File: FlowerEnums.cs
using UnityEngine;

public enum FlowerPartKind
{
    Leaf,
    Petal,
    StemExtra,
    Stem,
    Crown// e.g. buds, side stems, etc. (optional)
}

public enum FlowerPartCondition
{
    Normal,
    Withered,
    Perfect
}