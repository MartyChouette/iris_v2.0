# Iris v2.0 - Codebase Quality Assessment

## Executive Summary

**Project:** Iris v3.0 (thesis project for contemplative flower trimming gameplay)
**Platform:** Unity 6.3+ with URP
**Codebase Size:** ~14,800 lines of C# across 98 scripts
**Assessment Date:** January 2026
**Overall Quality Score:** 8/10 - Production-ready for thesis/prototype

---

## 1. Architecture Overview

### 1.1 Design Patterns

The codebase follows a **layered, event-driven architecture**:

```
INPUT LAYER (CuttingPlaneController, PlaneAngleTiltController)
       │
       ▼
CONTROL LAYER (ScissorStation, ScissorsVisualController)
       │
       ▼
LOGIC LAYER (FlowerGameBrain, FlowerSessionController)
       │
       ▼
PRESENTATION LAYER (FlowerGradingUI, FlowerHUD_GameplayFeedback)
```

**Patterns Identified:**
- **Event-Driven Communication:** UnityEvents for loose coupling (OnGameOver, OnResult, OnEquipScissors)
- **Data-Driven Configuration:** ScriptableObjects for flower definitions (no code changes needed for new flowers)
- **Singleton:** AudioManager for persistent audio across scenes
- **Component Composition:** Clear MonoBehaviour separation with single responsibilities
- **Object Pooling:** SapDecalPool, SapParticleController for performance

### 1.2 Strengths

| Aspect | Evidence |
|--------|----------|
| **Documentation** | Comprehensive Doxygen headers with intent, invariants, and ASCII relationship diagrams |
| **Defensive Programming** | Multiple safety checks: duplicate session detection, end-request latching, stack trace capture |
| **Performance Awareness** | Pre-allocated buffers (`_overlapBuffer`), allocation-free hot paths, object pooling |
| **Error Handling** | Debug logs distinguish warnings, errors, and expected cases |
| **Input Safety** | Anti-accidental-cut logic, UI overlap checks, release latches |
| **Physics Safety** | Graceful freeze on game over, velocity zeroing, optional collider disabling |
| **Separation of Concerns** | Clear responsibility boundaries between systems |

---

## 2. Critical Issues & Recommendations

### 2.1 HIGH PRIORITY - Time Scale Management

**Issue:** `FlowerSessionController` directly modifies `Time.timeScale` for slow-motion effects. If the object is destroyed/disabled mid-flow, this can "poison" subsequent scenes.

**Current Mitigation:** `OnDisable()` restores time scale - but this is fragile.

**Location:** `FlowerSessionController.cs:280-301`

**Recommendation:** Create a dedicated `TimeManager` singleton:

```csharp
// Proposed: Assets/Scripts/Framework/TimeManager.cs
public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    private float _originalTimeScale = 1f;
    private int _pauseRequestCount = 0;

    public void RequestSlowMo(float scale, float duration, System.Action onComplete = null)
    {
        // Centralized time control with automatic restoration
    }

    public void RequestPause() { _pauseRequestCount++; UpdateTimeScale(); }
    public void ReleasePause() { _pauseRequestCount = Mathf.Max(0, _pauseRequestCount - 1); UpdateTimeScale(); }
}
```

### 2.2 HIGH PRIORITY - Scene Auto-Find Brittleness

**Issue:** Several components use `FindFirstObjectByType<>()` which fails silently if multiple instances exist or none are found.

**Locations:**
- `FlowerSessionController.cs:142-143` - Auto-finds brain
- `FlowerGradingUI.cs:109-118` - Auto-finds session controller

**Recommendation:** Add explicit validation and warnings:

```csharp
// In Awake() or Start()
if (brain == null)
{
    brain = GetComponentInChildren<FlowerGameBrain>(true);
    if (brain == null)
    {
        var allBrains = FindObjectsByType<FlowerGameBrain>(FindObjectsSortMode.None);
        if (allBrains.Length > 1)
            Debug.LogError($"[{GetType().Name}] Multiple FlowerGameBrain instances found! Assign explicitly.", this);
        else if (allBrains.Length == 0)
            Debug.LogError($"[{GetType().Name}] No FlowerGameBrain found in scene!", this);
    }
}
```

### 2.3 MEDIUM PRIORITY - Memory Management

**Historical Issue:** Commit messages mention "memory leak!!!" (c9fc420).

**Observed Patterns:**
- `FlowerPartRuntime.cs:119` mentions "Unsubscribes from xyJoint.onBroke to prevent leaked listeners"
- `FlowerJointRebinder.cs:819` has "CRITICAL: Disconnect joint first, then destroy with delay to prevent memory corruption"

**Recommendation:** Add cleanup validation:

```csharp
// Add to scripts that subscribe to events
private void OnDestroy()
{
    // Explicitly unsubscribe from all events
    if (session != null && session.OnResult != null)
        session.OnResult.RemoveListener(OnResultReceived);

    #if UNITY_EDITOR
    // Debug: Warn if we're being destroyed with active subscriptions
    if (_isSubscribed)
        Debug.LogWarning($"[{GetType().Name}] Destroyed while still subscribed to events!", this);
    #endif
}
```

### 2.4 MEDIUM PRIORITY - Legacy Code Cleanup

**Issue:** Obi fluid system code remains commented out throughout despite particle system replacement.

**Affected Files:**
- `FluidSquirter.cs:77-93` - Commented Obi code
- `FlowerSapController.cs` - ObiFluid mode still present
- Obi-related prefabs and meta files in project

**Recommendation:**
1. Remove all commented Obi code
2. Remove `FluidMode.ObiFluid` enum option
3. Delete unused Obi prefab references
4. Archive Obi package or remove completely

### 2.5 MEDIUM PRIORITY - Input Gating Depth

**Issue:** `CuttingPlaneController` only checks `EventSystem.IsPointerOverGameObject()` at cut-press time. Movement still occurs when over UI.

**Location:** `CuttingPlaneController.cs:417-419`

**Recommendation:** Gate movement input as well:

```csharp
private void Update()
{
    if (!_toolEnabled) return;

    // Gate ALL input when over UI, not just cuts
    bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    // MOVEMENT (only when not over UI)
    if (!overUI)
    {
        // ... existing movement code
    }

    // CUT LOGIC (already gated, but move check earlier)
    if (overUI) return;
    // ... existing cut code
}
```

---

## 3. Code Quality Observations

### 3.1 Positive Patterns

1. **Consistent Naming Conventions:**
   - Clear prefixes: `_privateField`, `s_staticField`
   - Descriptive suffixes: `_prefab`, `_source`, `_mask`
   - No cryptic abbreviations

2. **Excellent Header Documentation:**
   ```csharp
   /**
    * @file FlowerSessionController.cs
    * @brief FlowerSessionController script.
    * @details
    * Intent:
    * - Orchestrates a single flower session...
    *
    * Events:
    * - OnSuccessfulEvaluation: fired when final result is not game over.
    * ...
    */
   ```

3. **Inspector Organization:**
   - Consistent use of `[Header]` and `[Tooltip]` attributes
   - Logical grouping of related fields
   - Range constraints where appropriate

4. **Debug Infrastructure:**
   - Gated debug logs (`if (debugLogs)`)
   - Toggle flags per-component
   - Clear log prefixes for filtering

### 3.2 Areas for Improvement

1. **Magic Numbers:**
   Some hardcoded values should be constants:
   ```csharp
   // FluidSquirter.cs:73
   int emitCount = Mathf.CeilToInt(10 * final);  // Why 10?

   // Recommendation:
   private const int BASE_PARTICLE_COUNT = 10;
   int emitCount = Mathf.CeilToInt(BASE_PARTICLE_COUNT * final);
   ```

2. **Method Length:**
   `FlowerGameBrain.EvaluateFlower()` is 135 lines. Consider extracting:
   - `EvaluateStemLength()`
   - `EvaluateCutAngle()`
   - `EvaluatePartConditions()`

3. **Null Checks Repetition:**
   Many methods repeat null checks that could be validated once in Awake:
   ```csharp
   // Current pattern (repeated in multiple methods):
   if (brain == null || brain.ideal == null || brain.stem == null) return;

   // Better: Validate once, fail fast
   private void Awake()
   {
       Debug.Assert(brain != null, "Brain required", this);
       Debug.Assert(brain.ideal != null, "IdealFlowerDefinition required", this);
   }
   ```

---

## 4. Performance Recommendations

### 4.1 Current Optimizations (Good)

- Pre-allocated `Collider[]` buffer for `Physics.OverlapBoxNonAlloc`
- Object pooling for decals and particles
- Grace window timers use simple float decrements
- Comments warn about allocation-free hot paths

### 4.2 Suggested Improvements

1. **Reduce GetComponent Calls:**
   ```csharp
   // Current (CuttingPlaneController.cs:530-532):
   var part = col.GetComponentInParent<FlowerPartRuntime>();
   var stem = col.GetComponentInParent<FlowerStemRuntime>();

   // Better: Cache or use TryGetComponent
   if (col.TryGetComponent<FlowerPartRuntime>(out var part)) { ... }
   ```

2. **String Formatting in Logs:**
   Even gated logs allocate strings:
   ```csharp
   // Current:
   if (debugLogs)
       Debug.Log($"[XYTetherJoint] stretch={stretch:F3}...");

   // Better for release builds:
   [System.Diagnostics.Conditional("UNITY_EDITOR")]
   private void LogDebug(string msg) => Debug.Log(msg, this);
   ```

3. **Profile Target Platform:**
   - Test on minimum-spec hardware
   - Profile memory during particle bursts
   - Watch for frame spikes on stem cuts

---

## 5. Robustness Checklist for Video Game Release

### 5.1 Must-Have Before Release

- [ ] **Time scale safety:** Implement centralized TimeManager
- [ ] **Scene validation:** Add explicit checks for required references on scene load
- [ ] **Memory profiling:** Run extended play sessions watching for leaks
- [ ] **Error recovery:** Ensure game can recover from physics anomalies
- [ ] **Save/Load system:** Currently sessions are ephemeral (OK for thesis, not for full game)

### 5.2 Should-Have

- [ ] **Analytics hooks:** Add telemetry for player behavior research
- [ ] **Accessibility:** Colorblind modes for grading UI
- [ ] **Input rebinding:** Allow custom key/button mapping
- [ ] **Performance settings:** Quality presets for particle counts, physics

### 5.3 Nice-to-Have

- [ ] **Unit tests:** Especially for `FlowerGameBrain.EvaluateFlower()` scoring logic
- [ ] **Integration tests:** Automated scene validation
- [ ] **Localization:** Prepare text systems for multi-language
- [ ] **Crash reporting:** Remote error tracking

---

## 6. Specific File Recommendations

### FlowerSessionController.cs
- **Line 123-129:** Consider using `Interlocked` for thread-safe session counting
- **Line 280-301:** Extract coroutine to TimeManager
- **Line 419-445:** `FreezeAllRigidbodies()` allocates arrays - consider caching body references

### FlowerGameBrain.cs
- **Line 155-290:** Break into smaller evaluation methods
- **Line 83-87:** Consider using `Dictionary<string, T>.TryGetValue` pattern consistently

### CuttingPlaneController.cs
- **Line 500-617:** `HandleCutEffects()` does classification + audio + fluid. Consider splitting into:
  - `ClassifyCutHit()` - returns CutHitKind
  - `PlayCutAudio()` - handles SFX
  - `TriggerCutFluid()` - handles particles

---

## 7. Testing Strategy

### 7.1 Manual Test Cases

1. **Session Lifecycle:**
   - Start session → cut stem → verify score displays
   - Force game over (crown removal) → verify slow-mo → verify UI
   - Multiple rapid cuts → verify no double-end errors

2. **Physics Edge Cases:**
   - Extreme cut angles → verify angle scoring
   - Very short stem → verify hard-fail triggers
   - Rapid joint breaks → verify grace window works

3. **Memory Stability:**
   - Play 20+ consecutive sessions without scene reload
   - Monitor memory in profiler
   - Check for orphaned GameObjects

### 7.2 Automated Validation

Consider adding editor scripts:
```csharp
[MenuItem("Iris/Validate Scene Setup")]
static void ValidateScene()
{
    var sessions = FindObjectsByType<FlowerSessionController>(FindObjectsSortMode.None);
    if (sessions.Length != 1)
        Debug.LogError($"Expected 1 FlowerSessionController, found {sessions.Length}");

    var brain = FindFirstObjectByType<FlowerGameBrain>();
    if (brain == null)
        Debug.LogError("No FlowerGameBrain in scene");
    else if (brain.ideal == null)
        Debug.LogError("FlowerGameBrain has no IdealFlowerDefinition assigned");
}
```

---

## 8. Conclusion

Iris v2.0 is a **well-architected thesis project** with:
- Clear separation of concerns
- Thoughtful defensive programming
- Excellent documentation culture
- Solid performance foundations

**Primary areas for hardening:**
1. Time scale management (prevents scene pollution)
2. Memory leak auditing (historical concern)
3. Scene reference validation (prevents silent failures)
4. Legacy code cleanup (reduces maintenance burden)

The codebase is **ready for thesis demonstration** with minor polish. For a commercial release, the robustness checklist items should be addressed, particularly save/load, analytics, and automated testing.

---

*Assessment generated for Iris v2.0 - Flower Trimming Game*
