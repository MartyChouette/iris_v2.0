using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using DynamicMeshCutter;

[DisallowMultipleComponent]
public class CuttingPlaneController : MonoBehaviour
{
    public enum ControlMode { KeyboardWASD, MouseOnly, MouseAndKeyboard, Gamepad, Touchscreen }
    private enum CutHitKind { None, Stem, Leaf, Petal }

    [Header("Control Mode")]
    public ControlMode controlMode = ControlMode.MouseAndKeyboard;

    [Header("References")]
    public PlaneBehaviour plane;
    public ScissorsVisualController scissorsVisuals;

    [Header("Movement Settings")]
    public float axisMoveSpeed = 2f;
    public float mouseFollowSpeed = 20f;
    public float minY = -1f;
    public float maxY = 1f;
    public bool useMouseHeight = true;

    [Header("Input Actions")]
    public InputActionReference moveYAction;
    public InputActionReference pointerPositionAction;
    public InputActionReference cutAction;

    [Header("Angle Tilt Integration")]
    public PlaneAngleTiltController angleTiltController;
    public bool disableYMovementWhenAngleTiltActive = true;

    // ─────────────────────────────────────────────────────────────
    // Cut Detection / Effects
    // ─────────────────────────────────────────────────────────────

    [Header("Cut Detection Volume")]
    [Tooltip("Radius/thickness of the overlap volume. Keep small.")]
    public float cutSenseRadius = 0.04f;

    [Tooltip("Length of the overlap volume along the plane's local X axis.")]
    public float cutSenseLength = 1.0f;

    [Tooltip("IMPORTANT: set this to ONLY the layers containing Stem/Leaf/Petal colliders. Exclude CrownCore/References/UI/etc.")]
    public LayerMask cutDetectionMask = ~0;

    [Header("Cut SFX")]
    public AudioClip stemCutPrimary;
    public AudioClip stemCutSecondary;
    public float stemSecondaryDelay = 0.08f;

    public AudioClip leafCutPrimary;
    public AudioClip leafCutSecondary;
    public float leafSecondaryDelay = 0.08f;

    public AudioClip petalCutPrimary;
    public AudioClip petalCutSecondary;
    public float petalSecondaryDelay = 0.08f;

    [Header("Cut Fluids")]
    public FluidSquirter genericFluidPlane;
    public FluidSquirter stemFluidPlane;
    public FluidSquirter leafFluidPlane;
    public FluidSquirter petalFluidPlane;

    [Header("Gore Control")]
    [Range(0f, 1f)] public float goreIntensity = 1f;

    [Header("Debug")]
    public bool debugLogs = false;
    public bool drawDetectionGizmo = true;
    public Color detectionGizmoColor = new Color(1f, 0f, 0f, 0.25f);

    private Transform _planeTransform;

    // NonAlloc buffer (avoids per-cut allocations)
    private const int HIT_BUFFER_SIZE = 64;
    private readonly Collider[] _hitBuffer = new Collider[HIT_BUFFER_SIZE];

    void Reset() => plane = GetComponent<PlaneBehaviour>();

    void Awake()
    {
        if (plane == null) plane = GetComponentInChildren<PlaneBehaviour>();
        _planeTransform = plane != null ? plane.transform : transform;
        if (minY > maxY) { float tmp = minY; minY = maxY; maxY = tmp; }
        if (angleTiltController == null) angleTiltController = GetComponent<PlaneAngleTiltController>();
    }

    void OnEnable()
    {
        EnableAction(moveYAction);
        EnableAction(pointerPositionAction);
        EnableAction(cutAction);
    }

    void OnDisable()
    {
        DisableAction(moveYAction);
        DisableAction(pointerPositionAction);
        DisableAction(cutAction);
    }

    void Update()
    {
        if (_planeTransform == null) return;

        // --- MOVEMENT LOGIC ---
        bool useAxis = false;
        bool usePointer = false;

        switch (controlMode)
        {
            case ControlMode.KeyboardWASD: useAxis = true; break;
            case ControlMode.MouseOnly: usePointer = true; break;
            case ControlMode.MouseAndKeyboard: useAxis = true; usePointer = true; break;
            case ControlMode.Gamepad: useAxis = true; break;
            case ControlMode.Touchscreen: usePointer = true; break;
        }

        bool tiltLockActive = disableYMovementWhenAngleTiltActive && angleTiltController != null && angleTiltController.TiltModeActive;
        Vector3 pos = _planeTransform.position;

        if (useAxis && !tiltLockActive)
        {
            float axis = ReadAxis(moveYAction);
            if (Mathf.Abs(axis) > 0.0001f)
                pos.y += axis * axisMoveSpeed * Time.deltaTime;
        }

        if (usePointer && useMouseHeight && !tiltLockActive &&
            pointerPositionAction != null && pointerPositionAction.action != null && pointerPositionAction.action.enabled)
        {
            Vector2 screenPos = pointerPositionAction.action.ReadValue<Vector2>();
            float screenHeight = Mathf.Max(1f, Screen.height);
            float t = Mathf.Clamp01(screenPos.y / screenHeight);
            float targetY = Mathf.Lerp(minY, maxY, t);
            pos.y = Mathf.Lerp(pos.y, targetY, mouseFollowSpeed * Time.deltaTime);
        }

        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        _planeTransform.position = pos;

        // --- CUT LOGIC ---
        if (cutAction != null && cutAction.action != null && cutAction.action.WasPerformedThisFrame())
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (plane == null || !plane.enabled) return;

            if (scissorsVisuals != null && scissorsVisuals.AttemptSnip() == false)
                return;

            // 1) Perform the actual cut first (so effects match reality)
            plane.Cut();

            // 2) Now resolve what we actually cut and fire feedback
            HandleCutEffects_AfterCut();
        }
    }

    void EnableAction(InputActionReference actionRef) { if (actionRef?.action != null && !actionRef.action.enabled) actionRef.action.Enable(); }
    void DisableAction(InputActionReference actionRef) { if (actionRef?.action != null && actionRef.action.enabled) actionRef.action.Disable(); }

    float ReadAxis(InputActionReference actionRef)
    {
        if (actionRef?.action == null || !actionRef.action.enabled) return 0f;
        var action = actionRef.action;
        if (action.activeValueType == typeof(float)) return action.ReadValue<float>();
        if (action.activeValueType == typeof(Vector2)) return action.ReadValue<Vector2>().y;
        return 0f;
    }

    // ─────────────────────────────────────────────────────────────
    // EFFECTS (AFTER the cut)
    // ─────────────────────────────────────────────────────────────

    void HandleCutEffects_AfterCut()
    {
        if (_planeTransform == null) return;

        bool hasAnySfx = stemCutPrimary || stemCutSecondary || leafCutPrimary || leafCutSecondary || petalCutPrimary || petalCutSecondary;
        if (!hasAnySfx && goreIntensity <= 0f) return;

        Vector3 planePos = _planeTransform.position;
        Vector3 planeNormal = _planeTransform.forward; // your "cut direction"

        // Overlap volume centered on plane
        Vector3 halfExtents = new Vector3(cutSenseLength * 0.5f, cutSenseRadius, cutSenseRadius);
        Quaternion rotation = _planeTransform.rotation;

        int hitCount = Physics.OverlapBoxNonAlloc(
            planePos,
            halfExtents,
            _hitBuffer,
            rotation,
            cutDetectionMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            // Nothing detected -> generic feedback only
            PlayCutDual(stemCutPrimary, stemCutSecondary, stemSecondaryDelay);
            TriggerPlaneFluid(genericFluidPlane, planePos, planeNormal);
            return;
        }

        // Choose the closest valid target to the plane position, with priority rules.
        CutHitKind bestKind = CutHitKind.None;
        Collider bestCol = null;
        float bestDistSq = float.MaxValue;

        // We track best candidate per kind, then choose by priority with distance sanity
        Collider bestStem = null, bestLeaf = null, bestPetal = null;
        float bestStemD = float.MaxValue, bestLeafD = float.MaxValue, bestPetalD = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            var col = _hitBuffer[i];
            if (col == null) continue;

            // Compute distance to plane center using closest point
            Vector3 cp = col.ClosestPoint(planePos);
            float d = (cp - planePos).sqrMagnitude;

            // Classify
            var part = col.GetComponentInParent<FlowerPartRuntime>();
            if (part != null)
            {
                if (part.kind == FlowerPartKind.Leaf)
                {
                    if (d < bestLeafD) { bestLeafD = d; bestLeaf = col; }
                }
                else if (part.kind == FlowerPartKind.Petal)
                {
                    if (d < bestPetalD) { bestPetalD = d; bestPetal = col; }
                }
                continue;
            }

            // Stem: prefer real stem runtime markers (or stem runtime in parents)
            var stem = col.GetComponentInParent<FlowerStemRuntime>();
            if (stem != null)
            {
                if (d < bestStemD) { bestStemD = d; bestStem = col; }
                continue;
            }

            // fallback tags (only if your project still uses them)
            if (col.CompareTag("Stem"))
            {
                if (d < bestStemD) { bestStemD = d; bestStem = col; }
            }
            else if (col.CompareTag("Leaf"))
            {
                if (d < bestLeafD) { bestLeafD = d; bestLeaf = col; }
            }
            else if (col.CompareTag("Petal"))
            {
                if (d < bestPetalD) { bestPetalD = d; bestPetal = col; }
            }
        }

        // Final selection:
        // - If we have a stem candidate, take it UNLESS it's much farther than leaf/petal (prevents grazing stem from overriding a clear leaf cut).
        const float STEM_DISTANCE_OVERRIDE_FACTOR = 4f; // tweak: higher = stem wins more often

        if (bestStem != null)
        {
            float minNonStem = Mathf.Min(bestLeafD, bestPetalD);
            bool stemClearlyTooFar = (minNonStem < float.MaxValue) && (bestStemD > minNonStem * STEM_DISTANCE_OVERRIDE_FACTOR);

            if (!stemClearlyTooFar)
            {
                bestKind = CutHitKind.Stem;
                bestCol = bestStem;
                bestDistSq = bestStemD;
            }
        }

        if (bestKind == CutHitKind.None && bestLeaf != null)
        {
            bestKind = CutHitKind.Leaf;
            bestCol = bestLeaf;
            bestDistSq = bestLeafD;
        }

        if (bestKind == CutHitKind.None && bestPetal != null)
        {
            bestKind = CutHitKind.Petal;
            bestCol = bestPetal;
            bestDistSq = bestPetalD;
        }

        // Compute final hit point from chosen collider
        Vector3 hitPoint = (bestCol != null) ? bestCol.ClosestPoint(planePos) : planePos;

        if (debugLogs)
            Debug.Log($"[CutEffects] kind={bestKind} col={(bestCol ? bestCol.name : "null")} distSq={bestDistSq:F6} hitPoint={hitPoint}", bestCol);

        // Fire SFX + Plane fluid at hit point
        switch (bestKind)
        {
            case CutHitKind.Stem:
                PlayCutDual(stemCutPrimary, stemCutSecondary, stemSecondaryDelay);
                TriggerPlaneFluid(stemFluidPlane, hitPoint, planeNormal);
                break;

            case CutHitKind.Leaf:
                PlayCutDual(leafCutPrimary, leafCutSecondary, leafSecondaryDelay);
                TriggerPlaneFluid(leafFluidPlane, hitPoint, planeNormal);
                break;

            case CutHitKind.Petal:
                PlayCutDual(petalCutPrimary, petalCutSecondary, petalSecondaryDelay);
                TriggerPlaneFluid(petalFluidPlane, hitPoint, planeNormal);
                break;

            default:
                PlayCutDual(stemCutPrimary, stemCutSecondary, stemSecondaryDelay);
                TriggerPlaneFluid(genericFluidPlane, hitPoint, planeNormal);
                break;
        }

        // Clear buffer refs (not required, but helps debugging / avoids holding dead refs)
        for (int i = 0; i < hitCount; i++) _hitBuffer[i] = null;
    }

    void PlayCutDual(AudioClip first, AudioClip second, float delay)
    {
        if (AudioManager.Instance == null || (first == null && second == null)) return;

        if (second != null || delay > 0f)
            AudioManager.Instance.PlayDualSFX(first, second, delay);
        else
            AudioManager.Instance.PlaySFX(first);
    }

    void TriggerPlaneFluid(FluidSquirter planeSquirter, Vector3 pos, Vector3 normal)
    {
        float intensity = Mathf.Clamp01(goreIntensity);
        if (intensity <= 0f) return;

        if (planeSquirter != null)
            planeSquirter.Squirt(intensity, pos, normal.normalized);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 pMin = transform.position; Vector3 pMax = transform.position;
        pMin.y = minY; pMax.y = maxY;
        Gizmos.DrawLine(pMin, pMax);

        if (!drawDetectionGizmo) return;

        Transform t = Application.isPlaying && plane != null ? plane.transform : transform;
        Gizmos.color = detectionGizmoColor;
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, new Vector3(cutSenseLength, cutSenseRadius * 2f, cutSenseRadius * 2f));
        Gizmos.matrix = prev;
    }
#endif
}
