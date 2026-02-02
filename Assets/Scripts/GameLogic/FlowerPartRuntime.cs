/**
 * @file FlowerPartRuntime.cs
 * @brief FlowerPartRuntime script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup flowers_runtime
 */

// File: FlowerPartRuntime.cs
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Runtime component on any physical flower piece (leaf, petal, crown, etc).
/// Tracks whether the part is attached and can trigger game over on critical breaks.
/// </summary>
[DisallowMultipleComponent]
/**
 * @file FlowerPartRuntime.cs
 * @brief Runtime component attached to any physical flower piece (leaf, petal, crown, etc).
 *
 * @details
 * FlowerPartRuntime is the *per-part* runtime state + detachment authority for the flower system.
 * It sits on individual mesh pieces that can be cut, ripped, swapped, or physically broken.
 *
 * In the Iris flower loop, parts are not just decorative geometry: they are scored, tracked, and
 * can end the session when critical pieces are lost (most importantly the Crown).
 *
 * This component’s primary job is to answer two questions reliably:
 *  1) "Is this part still attached?" (and if not, why / is it permanent?)
 *  2) "Does this detachment require an immediate fail right now?"
 *
 * ------------------------------------------------------------
 * Responsibilities
 * ------------------------------------------------------------
 * - Identity: Provide a stable @ref PartId that other systems (e.g., scoring / rules) can match.
 * - Runtime state: Track attachment state (@ref isAttached) and condition (@ref condition).
 * - Detachment authority: Centralize detachment transitions via @ref MarkDetached, including:
 *      - Deduping (no double-fire)
 *      - Recording canonical detach reason (@ref lastDetachReason)
 *      - Enforcing "never rebind" semantics (@ref permanentlyDetached)
 * - Physics event intake:
 *      - React to Unity joint breaks via OnJointBreak().
 *      - React to custom XY tether breaks via OnXYJointBroke().
 * - Failure rules (runtime-level):
 *      - Enforce the one *hard* instant-fail rule: Crown detachment triggers game over.
 *      - Optionally enforce a crown fall failsafe (Y-threshold) after detachment.
 *
 * ------------------------------------------------------------
 * Non-Responsibilities
 * ------------------------------------------------------------
 * - Does not compute score. (This script only exposes metadata/hints like @ref scoreWeight.)
 * - Does not decide "ideal" authoring rules. (Those belong in data definitions such as Ideal rules.)
 * - Does not render UI. (It may log; UI should subscribe to session/brain events.)
 * - Does not perform cutting. (Cutting systems call @ref MarkDetached when a part is separated.)
 * - Does not manage reattachment/rebinding. (A rebinder can read @ref permanentlyDetached
 *   and @ref lastDetachReason, but it should not live here.)
 *
 * ------------------------------------------------------------
 * Key Data Model
 * ------------------------------------------------------------
 * Identity / classification:
 * - @ref PartId is the stable lookup key used to match authoring rules (e.g., "Leaf_A_03").
 * - @ref kind distinguishes structural roles (Leaf/Petal/Crown/etc).
 *
 * Runtime condition:
 * - @ref condition describes the current health/quality of the part (Normal/Withered/etc).
 *   This is *not* the same as attachment; a part can be withered but still attached.
 *
 * Attachment state (the authoritative trio):
 * - @ref isAttached: true while part is physically considered attached.
 * - @ref permanentlyDetached: once true, this part MUST never be rebound (hard rule).
 * - @ref lastDetachReason: canonical classification of how the detachment happened.
 *
 * Authoring / rule hints:
 * - Fields like @ref contributesToScore, @ref allowedMissing, @ref allowedWithered, and
 *   @ref scoreWeight provide lightweight hints for scoring/rules systems.
 * - @warning These are "hints" and may duplicate rule data elsewhere. If you keep both,
 *   define precedence clearly (data-definition should usually win).
 *
 * ------------------------------------------------------------
 * Detachment Semantics (Very Important)
 * ------------------------------------------------------------
 * Detachment is a one-way transition unless an *external* rebinder explicitly allows it.
 *
 * The intended contract for production:
 * - All detachment sources call @ref MarkDetached with an explicit @ref DetachReason.
 * - permanent=true means: "this part is irrevocably gone" (rebinder must refuse).
 * - If detachment is suppressed (@ref ShouldSuppressDetachEvents), the call is ignored.
 *
 * Suppression window:
 * - @ref ShouldSuppressDetachEvents reads @c session.suppressDetachEvents.
 * - This exists to prevent accidental joint-break events during cut/rebind grace windows
 *   from falsely triggering detachment and/or game over.
 *
 * ------------------------------------------------------------
 * Failure Rules (Session-End Triggers)
 * ------------------------------------------------------------
 * This script intentionally keeps instantaneous fail rules minimal:
 * - Crown detachment forces game over. This can be detected by:
 *     - @ref kind == FlowerPartKind.Crown, OR
 *     - GameObject layer == "CrownCore" (if present in the project).
 *
 * Optional crown fall failsafe:
 * - If @ref enableCrownYFailsafe is true, a Crown part that falls below @ref crownFailY
 *   triggers @c session.ForceGameOver("Crown fell too low.").
 * - @note As currently implemented, the Y-failsafe does not explicitly check @ref isAttached.
 *   If the intended behavior is "only after detaching", add an @c !isAttached gate.
 *
 * ------------------------------------------------------------
 * Unity Lifecycle Notes
 * ------------------------------------------------------------
 * - Awake():
 *   - Auto-wires @ref session and @ref brain from parents if not set.
 *   - Auto-wires @ref xyJoint and caches Unity @ref unityJoints if not provided.
 *   - Subscribes to @c xyJoint.onBroke (UnityEvent) to detect tether breaks.
 * - OnDestroy():
 *   - Unsubscribes from @c xyJoint.onBroke to prevent leaked listeners during cutter destroys.
 * - Update():
 *   - Runs the crown Y-failsafe check (if enabled).
 *
 * ------------------------------------------------------------
 * Performance & Allocation Notes
 * ------------------------------------------------------------
 * Hot paths to keep allocation-free:
 * - Joint break callbacks (OnJointBreak / OnXYJointBroke)
 * - Detachment processing (@ref MarkDetached)
 *
 * Current per-frame work:
 * - Update() performs a small conditional check; acceptable for a small number of parts.
 *   If many parts exist simultaneously, consider moving the crown fall failsafe to:
 *   - a coroutine that runs only after detachment, or
 *   - a session-level monitor that watches the crown instance only.
 *
 * Logging:
 * - Debug.Log in @ref MarkDetached is useful for development but can spam.
 *   Consider gating logs behind a session/debug flag for production builds.
 *
 * ------------------------------------------------------------
 * Integration Points
 * ------------------------------------------------------------
 * - FlowerSessionController:
 *   - Provides suppression window (@c suppressDetachEvents).
 *   - Receives hard fail calls via @c ForceGameOver(string).
 * - FlowerGameBrain:
 *   - Typically evaluates results and scoring; this script only supplies state + identity.
 * - Cutting / swap systems:
 *   - Should call @ref MarkDetached with explicit reasons (PlayerCut/StemSwap/etc).
 * - Rebinding systems (if present):
 *   - Must respect @ref permanentlyDetached and reason-based rules.
 *
 * ------------------------------------------------------------
 * Common Failure Modes / Gotchas
 * ------------------------------------------------------------
 * - Double detachment:
 *   - Without the @ref isAttached guard, multiple physics events could cascade into duplicate
 *     failures/logs. This script prevents that by early-returning if already detached.
 *
 * - Missing layer:
 *   - If "CrownCore" layer does not exist, crown detection falls back to @ref kind.
 *   - Ensure @ref kind is correctly authored for crown pieces.
 *
 * - Cutter destroy race conditions:
 *   - Parts destroyed mid-event can leave listeners dangling; OnDestroy handles this.
 *
 * - Rebind loopholes:
 *   - If a rebinder ignores @ref permanentlyDetached, you will get inconsistent scoring and
 *     narrative states. Treat permanence as a hard authority rule.
 *
 * ------------------------------------------------------------
 * Visual Maps
 * ------------------------------------------------------------
 * @section viz_relationships_flowerpart Visual Relationships
 * @dot
 * digraph FlowerPartRuntime_Relations {
 *   rankdir=LR;
 *   node [shape=box];
 *
 *   "Cutting / Swap Systems" -> "FlowerPartRuntime" [label="MarkDetached(reason)"];
 *   "XYTetherJoint" -> "FlowerPartRuntime"          [label="onBroke -> OnXYJointBroke"];
 *   "Unity Joint" -> "FlowerPartRuntime"            [label="OnJointBreak"];
 *   "FlowerPartRuntime" -> "FlowerSessionController"[label="ForceGameOver()"];
 *   "FlowerPartRuntime" -> "FlowerGameBrain"        [label="state + identity (read)"];
 * }
 * @enddot
 *
 * @ingroup flowers_runtime
 */

public class FlowerPartRuntime : MonoBehaviour
{
    // ADDED: canonical detach reasons so other systems can make correct decisions.
    public enum DetachReason
    {
        None,
        PlayerRipped,
        PlayerCut,
        StemSwap,
        PhysicsBreak,
        UnityJointBreak,
        Debug
    }

    [Header("Identity / Matching")]
    [Tooltip("Unique ID for this part so the brain can match it to IdealFlowerDefinition.partRules.")]
    public string PartId;

    public FlowerPartKind kind = FlowerPartKind.Leaf;

    [Header("Runtime Condition")]
    public FlowerPartCondition condition = FlowerPartCondition.Normal;

    [Tooltip("True while the part is still attached to the flower.")]
    public bool isAttached = true;

    // ADDED: if true, this part must never be rebound again.
    [Header("Detach Authority")]
    [Tooltip("If true, this part is permanently detached and MUST never be rebound.")]
    public bool permanentlyDetached = false;

    [Tooltip("Last known reason for detachment. Useful for debugging + rebinder rules.")]
    public DetachReason lastDetachReason = DetachReason.None;

    [Header("Authoring / Rule Hints (some of this is duplicated in IdealFlowerDefinition)")]
    [Tooltip("If true, removing this part may immediately cause game over (in addition to any Ideal rules).")]
    public bool canCauseGameOver = false;

    [Tooltip("If true, this part is special (for UI / feedback).")]
    public bool isSpecial = false;

    [Tooltip("If true, differences on this part affect score.")]
    public bool contributesToScore = true;

    [Tooltip("If true, this part is allowed to be withered and still OK.")]
    public bool allowedWithered = true;

    [Tooltip("If false, missing this part counts against you.")]
    public bool allowedMissing = false;

    [Range(0f, 1f)]
    [Tooltip("Score importance of this part relative to other parts.")]
    public float scoreWeight = 1f;

    [Header("Debug / Ideal Pose (optional)")]
    public Vector3 idealLocalPosition;
    public Vector3 idealLocalEuler;

    [Header("Runtime refs")]
    public FlowerSessionController session;
    public FlowerGameBrain brain;

    [Header("Physics")]
    [Tooltip("Optional custom tether joint used instead of generic Unity joints.")]
    public XYTetherJoint xyJoint;

    [Tooltip("Fallback Unity joints (HingeJoint, SpringJoint, etc.) used for detachment events.")]
    public Joint[] unityJoints;

    [Header("Crown Fall Failsafe")]
    [Tooltip("If true and this part is a Crown, then if it falls below crownFailY after detaching, the session will be forced to game over.")]
    public bool enableCrownYFailsafe = true;

    [Tooltip("World-space Y threshold for crown fall failsafe. If the crown's position.y drops below this after detaching, it will trigger a forced game over.")]
    public float crownFailY = -1f;

    // Internal guard so we only trigger the fall failsafe once.
    private bool _crownFallFailTriggered = false;

    // PERF: cached layer index (avoids string lookup on every detach)
    private static int s_crownCoreLayer = -2; // -2 = not yet resolved

    /**
 * @brief Unity lifecycle hook for initial wiring and safety setup.
 *
 * @details
 * Awake() performs defensive auto-wiring and event hookup so this part behaves correctly
 * even when instantiated via cloning, cutting, or runtime spawning.
 *
 * Specifically:
 * - Auto-resolves @ref session and @ref brain from parent hierarchy if not assigned.
 * - Auto-resolves @ref xyJoint if present on this GameObject.
 * - Caches all Unity @ref Joint components if @ref unityJoints was left empty.
 * - Subscribes to @c xyJoint.onBroke (UnityEvent), ensuring the event exists.
 *
 * @note This method must be allocation-safe and side-effect free beyond wiring.
 *       It must not trigger detachment or scoring logic.
 *
 * @warning If this component is added at runtime, Awake() is the only guarantee that
 *          physics break events are correctly hooked before simulation begins.
 */

    private void Awake()
    {
        // PERF: resolve CrownCore layer once (static, shared across all instances)
        if (s_crownCoreLayer == -2)
            s_crownCoreLayer = LayerMask.NameToLayer("CrownCore");

        // Auto-wire session / brain if not set in inspector.
        if (session == null)
            session = GetComponentInParent<FlowerSessionController>();
        if (brain == null)
            brain = GetComponentInParent<FlowerGameBrain>();

        // Auto-wire XY joint if not set.
        if (xyJoint == null)
            xyJoint = GetComponent<XYTetherJoint>();

        // Cache any joints if array empty.
        if (unityJoints == null || unityJoints.Length == 0)
            unityJoints = GetComponents<Joint>();

        // Guard rail: some clones / destroyed parts may have a missing or half-constructed XY joint.
        if (xyJoint != null)
        {
            if (xyJoint.onBroke == null)
                xyJoint.onBroke = new UnityEvent();

            xyJoint.onBroke.AddListener(OnXYJointBroke);
        }
    }

    /**
 * @brief Unity lifecycle hook for cleanup during destruction.
 *
 * @details
 * Ensures that any event subscriptions created in Awake() are removed.
 * This prevents leaked listeners and invalid callbacks when parts are destroyed
 * by cutters, scene unloads, or pooled cleanup.
 *
 * @note This method defensively checks for nulls to avoid race conditions where
 *       the XY joint or its UnityEvent is already partially destroyed.
 */

    private void OnDestroy()
    {
        // Guard against race conditions when parts are destroyed by the cutter.
        if (xyJoint != null && xyJoint.onBroke != null)
            xyJoint.onBroke.RemoveListener(OnXYJointBroke);
    }

    /**
 * @brief Per-frame safety check for crown fall failure conditions.
 *
 * @details
 * Update() only performs logic when all of the following are true:
 * - Crown Y-failsafe is enabled (@ref enableCrownYFailsafe).
 * - This part represents a Crown (@ref kind == FlowerPartKind.Crown).
 * - The fail has not already been triggered.
 * - A valid @ref session exists.
 *
 * If the Crown's world-space Y position falls below @ref crownFailY,
 * the session is forced into game over.
 *
 * @note This check is intentionally conservative and runs every frame.
 *       If performance becomes a concern, this can be moved to:
 *       - a coroutine started on detachment, or
 *       - a session-level watcher tracking only the crown instance.
 *
 * @warning As implemented, this check does not explicitly require
 *          @ref isAttached == false. If the intended rule is
 *          "only after detachment," an additional guard should be added.
 */

    private void Update()
    {
        if (enableCrownYFailsafe &&
            kind == FlowerPartKind.Crown &&
            !_crownFallFailTriggered &&
            session != null)
        {
            if (transform.position.y < crownFailY)
            {
                _crownFallFailTriggered = true;
                session.ForceGameOver("Crown fell too low.");
            }
        }
    }

    /**
     * @brief Determines whether detachment events should be ignored right now.
     *
     * @details
     * This method centralizes the check for session-level detachment suppression.
     * It is typically used during:
     * - Cutting grace windows
     * - Rebinding operations
     * - Controlled swaps where joints may temporarily break
     *
     * When this returns true, calls to @ref MarkDetached will be ignored.
     *
     * @return True if the session is suppressing detach events; otherwise false.
     *
     * @note This method must remain side-effect free.
     *       It should be safe to call multiple times per frame.
     */

    public bool ShouldSuppressDetachEvents()
    {
        return session != null && session.suppressDetachEvents;
    }


    /**
 * @brief Unity physics callback invoked when a built-in Joint on this object breaks.
 *
 * @param breakForce The force that caused the joint to break (provided by Unity).
 *
 * @details
 * This callback treats Unity joint breaks as authoritative, irreversible detachment
 * events. It forwards the event into the unified detachment pipeline by calling
 * @ref MarkDetached with:
 * - @ref DetachReason.UnityJointBreak
 * - permanent = true
 *
 * @note The breakForce value is currently unused but intentionally kept for
 *       debugging or future heuristics.
 *
 * @warning This callback may fire during physics-heavy operations; therefore,
 *          it must not allocate or perform expensive logic.
 */

    // Unity built-in: any 3D Joint on THIS object breaking will call this.
    private void OnJointBreak(float breakForce)
    {
        // CHANGED: use new overload with reason + permanence
        MarkDetached("Unity joint broke", DetachReason.UnityJointBreak, permanent: true);
    }

    // Called by XYTetherJoint via its UnityEvent.
    /**
 * @brief Callback invoked when the custom XYTetherJoint reports a break.
 *
 * @details
 * This method is registered as a listener on @c xyJoint.onBroke.
 * It forwards the event into the unified detachment pipeline by calling
 * @ref MarkDetached with:
 * - @ref DetachReason.PhysicsBreak
 * - permanent = true
 *
 * @note The XYTetherJoint is expected to evolve toward calling the richer
 *       @ref MarkDetached overload directly with explicit reasons.
 *
 * @warning This callback may fire during physics resolution; it must remain
 *          allocation-free and deterministic.
 */

    private void OnXYJointBroke()
    {
        // CHANGED: default reason (XYTetherJoint will ideally call the richer overload below)
        MarkDetached("XY tether broke", DetachReason.PhysicsBreak, permanent: true);
    }

    /**
     * @brief Backwards-compatible detachment API.
     *
     * @param reason Human-readable reason for detachment (debug/logging only).
     *
     * @details
     * This overload exists to preserve compatibility with older call sites.
     * It forwards to the authoritative overload using:
     * - @ref DetachReason.Debug
     * - permanent = true
     *
     * @note New systems should prefer the full overload:
     *       @ref MarkDetached(string, DetachReason, bool)
     *
     * @warning This method always treats the detachment as permanent.
     */

    public void MarkDetached(string reason = "Detached")
    {
        MarkDetached(reason, DetachReason.Debug, permanent: true);
    }

    /**
 * @brief Authoritative entry point for detaching a flower part.
 *
 * @param reason Human-readable explanation for logging/debugging.
 * @param detachReason Canonical classification of why the detachment occurred.
 * @param permanent If true, this part must never be rebound again.
 *
 * @details
 * This method defines the *single source of truth* for detachment semantics.
 * All systems (cutting, physics, swaps, debug tools) should call this method
 * rather than manipulating state directly.
 *
 * Detachment pipeline:
 * 1. If detachment events are currently suppressed
 *    (@ref ShouldSuppressDetachEvents), exit immediately.
 * 2. If the part is already detached (@ref isAttached == false), exit.
 * 3. Mark the part as detached and record canonical state:
 *      - @ref isAttached = false
 *      - @ref lastDetachReason = detachReason
 *      - @ref permanentlyDetached = true (if permanent)
 * 4. Evaluate hard failure rules:
 *      - If this part is a Crown (by layer or @ref kind), force game over.
*/
    public void MarkDetached(string reason, DetachReason detachReason, bool permanent)
    {
        // Ignore detach events while the session is in a cut/rebind grace window.
        if (ShouldSuppressDetachEvents())
        {
#if UNITY_EDITOR
            Debug.Log($"[FlowerPartRuntime] Detach '{PartId}' skipped during cut grace: {reason}", this);
#endif
            return;
        }

        // If we're already detached, don't double-fire.
        if (!isAttached)
            return;

        isAttached = false;

        // ADDED: record canonical detach state
        lastDetachReason = detachReason;
        if (permanent)
            permanentlyDetached = true;

#if UNITY_EDITOR
        Debug.Log($"[FlowerPartRuntime] '{PartId}' detached: {reason} (Reason={detachReason}, Permanent={permanent})", this);
#endif

        // The only true instant game over we want is when the crown is lost.
        // PERF: use cached layer index instead of string lookup every detach
        bool isCrownByLayer = (s_crownCoreLayer >= 0 && gameObject.layer == s_crownCoreLayer);
        bool isCrownByKind = (kind == FlowerPartKind.Crown);

        if ((isCrownByLayer || isCrownByKind) && session != null)
        {
            session.ForceGameOver("Crown detached.");
        }
    }
}
