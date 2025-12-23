/**
 * @file FlowerStemRuntime.cs
 * @brief FlowerStemRuntime script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup flowers_runtime
 */

// File: FlowerStemRuntime.cs
using UnityEngine;

[DisallowMultipleComponent]
/**
 * @file FlowerStemRuntime.cs
 * @brief Runtime stem measurement + cut metadata for the currently held stem segment.
 *
 * @details
 * FlowerStemRuntime is the *measurement and cut-metadata* component for a stem segment that the
 * player is actively manipulating/evaluating. It does **not** slice meshes and does **not**
 * decide score. Instead, it provides reliable geometric facts that other systems depend on:
 *
 * - Current stem length (between @ref stemStart and @ref stemEnd)
 * - Cut angle (derived from a plane normal reference transform)
 * - Last cut height (for session-level instant-fail checks)
 * - Nearest point on the stem segment (for alignment/tethering/FX placement)
 *
 * This component exists to keep “where did the cut happen?” logic centralized and consistent,
 * so that:
 * - the cutter can update stem endpoints deterministically,
 * - UI/scoring systems can query length/angle without duplicating math, and
 * - session rules (e.g., "cut too high") have a stable value to inspect.
 *
 * ------------------------------------------------------------
 * Responsibilities
 * ------------------------------------------------------------
 * - Store references used to measure the held stem segment:
 *   - @ref stemStart (base)
 *   - @ref stemEnd (tip; must be updated by cut logic)
 * - Maintain a cut-normal reference transform (@ref cutNormalRef) whose forward approximates
 *   the plane normal used for the last cut.
 * - Provide derived measurements:
 *   - @ref CurrentLength
 *   - @ref GetCurrentCutAngleDeg
 *   - @ref GetClosestPointOnStem
 * - Record the last cut height in world space (@ref lastCutHeight) for rule checks.
 * - Provide a single “apply cut metadata” method (@ref ApplyCutFromPlane) that cutter code can call
 *   for both preview and final cuts (without doing mesh slicing here).
 *
 * ------------------------------------------------------------
 * Non-Responsibilities
 * ------------------------------------------------------------
 * - Does not perform mesh slicing / geometry editing.
 * - Does not trigger game over or scoring decisions directly.
 *   - It only records data (e.g., @ref lastCutHeight) that session/brain systems interpret.
 * - Does not manage physics/tethers directly (but may be queried by those systems).
 *
 * ------------------------------------------------------------
 * Key Data & Invariants
 * ------------------------------------------------------------
 * - @ref stemStart and @ref stemEnd are assumed to represent the endpoints of the *held* stem.
 *   If either is missing, measurements fall back safely (0 length or transform position).
 *
 * - @ref stemEnd MUST be moved by the cut logic to represent the *actual cut point*.
 *   This is the single most important authoring/runtime invariant.
 *
 * - Cut angle is defined by:
 *   - Local axis @ref referenceAxisLocal transformed by @ref cutNormalRef
 *   - Compared against a provided world reference axis (often world up).
 *
 * - @ref lastCutHeight stores the world-space Y coordinate of the last cut plane point passed to
 *   @ref ApplyCutFromPlane. This value is consumed elsewhere (e.g., session "cut too high" rules).
 *
 * ------------------------------------------------------------
 * Integration Points (as currently documented in this file)
 * ------------------------------------------------------------
 * Called by:
 * - PlaneBehaviour (preview + final cut)
 * - MouseBehaviour (final cut)
 *
 * Consumed by:
 * - FlowerSessionController (checks @ref lastCutHeight vs @ref minAllowedCutY or similar rule)
 * - FlowerGameBrain / evaluation UI (queries @ref CurrentLength / angle)
 * - Potential FX helpers (queries @ref GetClosestPointOnStem for positioning)
 *
 * ------------------------------------------------------------
 * Performance Notes
 * ------------------------------------------------------------
 * - @ref CurrentLength uses Vector3.Distance; avoid calling it redundantly in hot loops.
 *   Prefer caching per evaluation tick if needed.
 * - @ref GetClosestPointOnStem is allocation-free and safe for frequent calls.
 *
 * ------------------------------------------------------------
 * Common Failure Modes / Gotchas
 * ------------------------------------------------------------
 * - “Length always wrong”: stemEnd was not updated by cut logic (violates invariant).
 * - “Angle always 0”: cutNormalRef not assigned or not updated on cut.
 * - “Cut too high rule unreliable”: ApplyCutFromPlane not called on final cut, so lastCutHeight stale.
 * - “Angle meaning mismatch”: confirm whether you want angle vs world-up or vs stem axis; this method
 *   measures axisWorld vs provided world reference axis.
 *
 * ------------------------------------------------------------
 * Visual Maps
 * ------------------------------------------------------------
 * @section viz_flowerstemruntime Visual Relationships
 * @dot
 * digraph FlowerStemRuntime {
 *   rankdir=LR;
 *   node [shape=box];
 *   "PlaneBehaviour" -> "FlowerStemRuntime" [label="ApplyCutFromPlane (preview/final)"];
 *   "MouseBehaviour" -> "FlowerStemRuntime" [label="ApplyCutFromPlane (final)"];
 *   "FlowerStemRuntime" -> "FlowerSessionController" [label="lastCutHeight for rules"];
 *   "FlowerStemRuntime" -> "FlowerGameBrain / UI" [label="length/angle queries"];
 * }
 * @enddot
 *
 * @ingroup flowers_runtime
 */

public class FlowerStemRuntime : MonoBehaviour
{
    [Header("Stem measurement")]
    [Tooltip("Where the stem begins (bottom of the held piece after cut).")]
    public Transform stemStart;

    [Tooltip("Where the stem ends (tip of the held stem after cut). This MUST be moved by the cut logic.")]
    public Transform stemEnd;

    [Header("Cut angle reference")]
    [Tooltip("Object whose forward = plane normal. Used for angle measurement.")]
    public Transform cutNormalRef;

    [Tooltip("Local axis used for angle measurement (usually up).")]
    public Vector3 referenceAxisLocal = Vector3.up;

    [Header("Cut Game-Over Threshold")]
    [Tooltip("If the cut happens ABOVE this world-space Y height → instant game over.")]
    public float minAllowedCutY = -9999f;

    /**
     * @brief Returns the current length of the held stem segment.
     *
     * @details
     * Measures world-space distance between @ref stemStart and @ref stemEnd.
     * If either reference is missing, returns 0 to avoid null exceptions.
     *
     * @note This is a derived measurement; it does not modify state.
     * @warning Avoid calling this redundantly in per-frame UI string building; cache per evaluation tick if needed.
     */

    public float CurrentLength
    {
        get
        {
            if (!stemStart || !stemEnd)
                return 0f;

            return Vector3.Distance(stemStart.position, stemEnd.position);
        }
    }

    /**
     * @brief Computes the current cut angle in degrees relative to a provided world reference axis.
     *
     * @param worldReferenceAxis The world-space axis to compare against (commonly Vector3.up).
     * @return Angle in degrees in [0..180]. Returns 0 if @ref cutNormalRef is not assigned.
     *
     * @details
     * Uses @ref cutNormalRef to produce a world-space axis based on @ref referenceAxisLocal, then
     * computes the angle between that axis and @p worldReferenceAxis using Vector3.Angle.
     *
     * Intended usage:
     * - After @ref ApplyCutFromPlane updates @ref cutNormalRef rotation to align with the cut plane normal,
     *   this method produces a stable “cut angle” measurement for evaluation.
     *
     * @note This method does not validate that worldReferenceAxis is non-zero; callers should pass a normalized axis.
     */

    public float GetCurrentCutAngleDeg(Vector3 worldReferenceAxis)
    {
        if (!cutNormalRef)
            return 0f;

        Vector3 axisWorld = cutNormalRef.TransformDirection(referenceAxisLocal).normalized;
        return Vector3.Angle(axisWorld, worldReferenceAxis.normalized);
    }

    /// ======================================================================
    /// APPLY THE CUT (preview OR real)
    /// ======================================================================
    ///
    /// Called by:
    ///     PlaneBehaviour (preview + final cut)
    ///     MouseBehaviour  (final cut)
    ///
    /// This does NOT slice any mesh. It ONLY updates:
    ///     - angle reference
    ///     - stemEnd position
    ///     - cut height for instant fail logic
    ///
    /// ======================================================================
    /**
    * @brief Applies cut metadata from a plane intersection point and plane normal(preview or final cut).
        *
        * @param planePoint World-space point where the cutting plane intersects the stem.
    * @param planeNormal World-space plane normal(expected to be normalized or near-normalized).
    *
    * @details
        * This method does NOT slice meshes.It records the geometric results of a cut so that other
        * systems can evaluate and respond consistently.
    *
    * Updates:
    * - @ref cutNormalRef position and rotation:
    *     - position = @p planePoint
    *     - rotation = LookRotation(planeNormal, Vector3.up)
        * - @ref stemEnd position = @p planePoint(authoritative: stem end becomes the cut point)
        * - @ref lastCutHeight = planePoint.y(for session rule checks)
    *
    *Early - outs if required references are missing(@ref cutNormalRef or @ref stemEnd).
        *
        * @note Game-over checks and scoring are handled externally(e.g., FlowerSessionController / brain).
        *       This method is safe to call during previews and repeatedly during plane movement.
    *
    * @warning If this is not called on the final cut, @ref lastCutHeight may be stale and rules like
        *          “cut too high” may misfire.
    */

    public void ApplyCutFromPlane(Vector3 planePoint, Vector3 planeNormal)
    {
        if (!cutNormalRef || !stemEnd)
            return;

        // 1. Update angle
        cutNormalRef.position = planePoint;
        cutNormalRef.rotation = Quaternion.LookRotation(planeNormal, Vector3.up);

        // 2. NEW: Re-position stemEnd to the EXACT cut location
        stemEnd.position = planePoint;

        // 3. Store last cut height for instant fail
        lastCutHeight = planePoint.y;

        // (No scoring or game-over here; session handles that)
    }

    // In FlowerStemRuntime.cs
    /**
 * @brief Returns the closest point on the stem segment to an arbitrary world-space point.
 *
 * @param worldPoint World-space point to project onto the stem segment.
 * @return The closest point on the segment from @ref stemStart to @ref stemEnd.
 *
 * @details
 * Computes the closest point on the line segment AB:
 * - A = stemStart.position
 * - B = stemEnd.position
 * Uses dot-product projection and clamps to [0..1] to remain within the segment.
 *
 * Null safety:
 * - If @ref stemStart or @ref stemEnd is null, returns this transform position.
 * Degenerate safety:
 * - If the segment is nearly zero length, returns A.
 *
 * Intended usage:
 * - FX placement (sap, particles) near the stem
 * - Aligning tools or preview markers to the stem
 * - Finding nearest tether anchor location
 *
 * @note Allocation-free and safe for frequent use.
 */

    public Vector3 GetClosestPointOnStem(Vector3 worldPoint)
    {
        if (stemStart == null || stemEnd == null)
            return transform.position;

        Vector3 a = stemStart.position;
        Vector3 b = stemEnd.position;
        Vector3 ab = b - a;

        float abLenSq = ab.sqrMagnitude;
        if (abLenSq < 1e-6f)
            return a;

        float t = Vector3.Dot(worldPoint - a, ab) / abLenSq;
        t = Mathf.Clamp01(t);

        return a + ab * t;
    }

    /**
 * @brief World-space Y coordinate of the most recent cut intersection.
 *
 * @details
 * Written by @ref ApplyCutFromPlane and consumed by higher-level systems (typically the session)
 * to evaluate rules such as “cut too high” thresholds.
 *
 * @note Hidden in inspector because it is runtime-derived state, not authoring data.
 */

    [HideInInspector] public float lastCutHeight = -99999f;
}
