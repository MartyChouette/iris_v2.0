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
    public float cutSenseRadius = 0.04f; // Increased slightly to ensure stem detection
    public float cutSenseLength = 1.0f;
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

    Transform _planeTransform;

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

        if (usePointer && useMouseHeight && !tiltLockActive && pointerPositionAction != null && pointerPositionAction.action != null && pointerPositionAction.action.enabled)
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

            if (plane != null && plane.enabled)
            {
                if (scissorsVisuals != null && scissorsVisuals.AttemptSnip() == false)
                    return;

                HandleCutEffects();
                plane.Cut();
            }
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
    // IMPROVED CUT DETECTION
    // ─────────────────────────────────────────────────────────────
    void HandleCutEffects()
    {
        if (_planeTransform == null) return;

        bool hasAnySfx = stemCutPrimary || stemCutSecondary || leafCutPrimary || leafCutSecondary || petalCutPrimary || petalCutSecondary;
        if (!hasAnySfx && goreIntensity <= 0f) return;

        Vector3 center = _planeTransform.position;
        Vector3 halfExtents = new Vector3(cutSenseLength * 0.5f, cutSenseRadius, cutSenseRadius);
        Quaternion rotation = _planeTransform.rotation;

        Collider[] hits = Physics.OverlapBox(center, halfExtents, rotation, cutDetectionMask, QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            TriggerFluid(genericFluidPlane, null);
            return;
        }

        Collider leafCol = null, petalCol = null, stemCol = null;

        foreach (var col in hits)
        {
            if (col == null) continue;

            // --- SMART COMPONENT CHECK ---
            // A Stem has a StemRuntime in its parent hierarchy, but NOT a PartRuntime.
            // A Leaf/Petal has a PartRuntime.
            var part = col.GetComponentInParent<FlowerPartRuntime>();
            var stem = col.GetComponentInParent<FlowerStemRuntime>();

            if (part != null)
            {
                if (part.kind == FlowerPartKind.Leaf) leafCol = col;
                else if (part.kind == FlowerPartKind.Petal) petalCol = col;
            }
            else if (stem != null)
            {
                // It has a stem parent but is not a part => It is the Stem!
                if (stemCol == null) stemCol = col;
            }
            // Fallback to tags if scripts are missing
            else if (col.CompareTag("Stem")) stemCol = col;
            else if (col.CompareTag("Leaf")) leafCol = col;
            else if (col.CompareTag("Petal")) petalCol = col;
        }

        CutHitKind kind = CutHitKind.None;
        Collider chosen = null;

        // PRIORITY: Stem > Leaf > Petal
        // If we hit a stem, we assume that was the intended target, even if a leaf was also grazed.
        if (stemCol != null) { kind = CutHitKind.Stem; chosen = stemCol; }
        else if (leafCol != null) { kind = CutHitKind.Leaf; chosen = leafCol; }
        else if (petalCol != null) { kind = CutHitKind.Petal; chosen = petalCol; }

        switch (kind)
        {
            case CutHitKind.Leaf:
                PlayCutDual(leafCutPrimary, leafCutSecondary, leafSecondaryDelay);
                TriggerFluid(leafFluidPlane, chosen);
                break;
            case CutHitKind.Petal:
                PlayCutDual(petalCutPrimary, petalCutSecondary, petalSecondaryDelay);
                TriggerFluid(petalFluidPlane, chosen);
                break;
            case CutHitKind.Stem:
                PlayCutDual(stemCutPrimary, stemCutSecondary, stemSecondaryDelay);
                TriggerFluid(stemFluidPlane, chosen);
                break;
            default:
                PlayCutDual(stemCutPrimary, stemCutSecondary, stemSecondaryDelay);
                TriggerFluid(genericFluidPlane, chosen);
                break;
        }
    }

    void PlayCutDual(AudioClip first, AudioClip second, float delay)
    {
        if (AudioManager.Instance == null || (first == null && second == null)) return;
        if (second != null || delay > 0f) AudioManager.Instance.PlayDualSFX(first, second, delay);
        else AudioManager.Instance.PlaySFX(first);
    }

    void TriggerFluid(FluidSquirter planeSquirter, Collider exampleCol)
    {
        float intensity = Mathf.Clamp01(goreIntensity);
        if (intensity <= 0f) return;

        // Option A: Move the generic "Cut Plane" squirter to the cut location and fire
        if (planeSquirter != null)
        {
            planeSquirter.Squirt(intensity, _planeTransform.position, _planeTransform.forward);
        }

        // Option B: If the object itself has specific squirters attached (rare for stems, common for bosses)
        if (exampleCol != null)
        {
            var squirters = exampleCol.GetComponentsInParent<FluidSquirter>();
            if (squirters != null && squirters.Length > 0)
            {
                Vector3 hitPoint = exampleCol.ClosestPoint(_planeTransform.position);
                Vector3 hitNormal = exampleCol.transform.up;

                foreach (var fs in squirters)
                {
                    // Don't double fire if it's the same one we just fired
                    if (fs != planeSquirter)
                        fs.Squirt(intensity, hitPoint, hitNormal);
                }
            }
        }
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