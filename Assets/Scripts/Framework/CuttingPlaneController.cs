using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;   // for IsPointerOverGameObject
using DynamicMeshCutter;

/// <summary>
/// Handles moving the cutting plane up/down and triggering cuts via PlaneBehaviour.
/// Now also:
/// - Detects what we hit (Stem / Leaf / Petal) near the plane when we cut
/// - Plays a two-stage SFX for each type
/// - Triggers FluidSquirter on both the plane and hit objects
/// - Has a goreIntensity slider to scale fluid intensity
/// </summary>
[DisallowMultipleComponent]
public class CuttingPlaneController : MonoBehaviour
{
    public enum ControlMode
    {
        KeyboardWASD,
        MouseOnly,
        MouseAndKeyboard,
        Gamepad,
        Touchscreen
    }

    private enum CutHitKind
    {
        None,
        Stem,
        Leaf,
        Petal
    }

    [Header("Control Mode")]
    public ControlMode controlMode = ControlMode.MouseAndKeyboard;

    [Header("References")]
    [Tooltip("PlaneBehaviour that actually performs the cut. If left null, will try to find it on this GameObject.")]
    public PlaneBehaviour plane;

    [Header("Movement Settings")]
    public float axisMoveSpeed = 2f;
    public float mouseFollowSpeed = 20f;
    public float minY = -1f;
    public float maxY = 1f;
    public bool useMouseHeight = true;

    [Header("Input Actions (New Input System)")]
    public InputActionReference moveYAction;
    public InputActionReference pointerPositionAction;
    public InputActionReference cutAction;

    [Header("Angle Tilt Integration")]
    [Tooltip("Optional: if assigned, this controller will NOT move Y while angle tilt mode is active, so stage 2 is purely about angle.")]
    public PlaneAngleTiltController angleTiltController;

    [Tooltip("If true, disables all Y movement (axis + mouse-height) when angle tilt mode is active.")]
    public bool disableYMovementWhenAngleTiltActive = true;

    // ─────────────────────────────────────────────────────────────
    // Cut Detection / Effects
    // ─────────────────────────────────────────────────────────────

    [Header("Cut Detection Volume")]
    [Tooltip("Half-thickness of the overlap box perpendicular to the plane (in world units).")]
    public float cutSenseRadius = 0.02f;

    [Tooltip("Length of the overlap box along the plane (in world units).")]
    public float cutSenseLength = 1.0f;

    [Tooltip("Layers considered when detecting what we just cut.")]
    public LayerMask cutDetectionMask = ~0;

    [Header("Cut SFX – Stem")]
    public AudioClip stemCutPrimary;
    public AudioClip stemCutSecondary;
    public float stemSecondaryDelay = 0.08f;

    [Header("Cut SFX – Leaf")]
    public AudioClip leafCutPrimary;
    public AudioClip leafCutSecondary;
    public float leafSecondaryDelay = 0.08f;

    [Header("Cut SFX – Petal")]
    public AudioClip petalCutPrimary;
    public AudioClip petalCutSecondary;
    public float petalSecondaryDelay = 0.08f;

    [Header("Cut Fluids (Plane-Level Emitters)")]
    [Tooltip("Generic squirter on the plane, used if we can't classify the hit.")]
    public FluidSquirter genericFluidPlane;

    [Tooltip("Plane-level fluid squirter used when we primarily hit stem.")]
    public FluidSquirter stemFluidPlane;

    [Tooltip("Plane-level fluid squirter used when we primarily hit leaf.")]
    public FluidSquirter leafFluidPlane;

    [Tooltip("Plane-level fluid squirter used when we primarily hit petal.")]
    public FluidSquirter petalFluidPlane;

    [Header("Gore Control")]
    [Tooltip("0 = no fluid. 1 = full intensity. This is multiplied by per-squirter settings.")]
    [Range(0f, 1f)] public float goreIntensity = 1f;

    [Header("Debug")]
    public bool debugLogs = false;
    public bool drawDetectionGizmo = true;
    public Color detectionGizmoColor = new Color(1f, 0f, 0f, 0.25f);

    Transform _planeTransform;

    void Reset()
    {
        plane = GetComponent<PlaneBehaviour>();
    }

    void Awake()
    {
        if (plane == null)
            plane = GetComponent<PlaneBehaviour>();

        _planeTransform = plane != null ? plane.transform : transform;

        if (minY > maxY)
        {
            float tmp = minY;
            minY = maxY;
            maxY = tmp;
        }

        // Try auto-wire the tilt controller if it's on the same object.
        if (angleTiltController == null)
            angleTiltController = GetComponent<PlaneAngleTiltController>();
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
        if (_planeTransform == null)
            return;

        bool useAxis = false;
        bool usePointer = false;

        switch (controlMode)
        {
            case ControlMode.KeyboardWASD:
                useAxis = true;
                break;
            case ControlMode.MouseOnly:
                usePointer = true;
                break;
            case ControlMode.MouseAndKeyboard:
                useAxis = true;
                usePointer = true;
                break;
            case ControlMode.Gamepad:
                useAxis = true;
                break;
            case ControlMode.Touchscreen:
                usePointer = true;
                break;
        }

        // Check if tilt mode is active (second stage).
        bool tiltLockActive =
            disableYMovementWhenAngleTiltActive &&
            angleTiltController != null &&
            angleTiltController.TiltModeActive;

        Vector3 pos = _planeTransform.position;

        // ───────── Axis movement ─────────
        if (useAxis && !tiltLockActive)
        {
            float axis = ReadAxis(moveYAction);
            if (Mathf.Abs(axis) > 0.0001f)
                pos.y += axis * axisMoveSpeed * Time.deltaTime;
        }

        // ───────── Pointer-based height ─────────
        if (usePointer && useMouseHeight && !tiltLockActive &&
            pointerPositionAction != null &&
            pointerPositionAction.action != null &&
            pointerPositionAction.action.enabled)
        {
            Vector2 screenPos = pointerPositionAction.action.ReadValue<Vector2>();
            float screenHeight = Mathf.Max(1f, Screen.height);
            float t = Mathf.Clamp01(screenPos.y / screenHeight);
            float targetY = Mathf.Lerp(minY, maxY, t);
            pos.y = Mathf.Lerp(pos.y, targetY, mouseFollowSpeed * Time.deltaTime);
        }

        // Clamp Y
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        _planeTransform.position = pos;

        // ───────── Cut button ─────────
        if (cutAction != null &&
            cutAction.action != null &&
            cutAction.action.WasPerformedThisFrame())
        {
            // 1) If the pointer is over UI, ignore this cut.
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
#if UNITY_EDITOR
                if (debugLogs)
                    Debug.Log("[CuttingPlaneController] Cut input ignored (pointer over UI).", this);
#endif
                return;
            }

            // 2) Only cut if we actually have a PlaneBehaviour AND it is enabled
            if (plane != null && plane.enabled)
            {
                // Pre-cut: detect what we're likely to hit and play effects.
                HandleCutEffects();

                // Perform the actual cut (DynamicMeshCutter)
                plane.Cut();
            }
        }
    }

    // ───────────────────── Helpers ─────────────────────

    void EnableAction(InputActionReference actionRef)
    {
        if (actionRef == null) return;
        var action = actionRef.action;
        if (action != null && !action.enabled)
            action.Enable();
    }

    void DisableAction(InputActionReference actionRef)
    {
        if (actionRef == null) return;
        var action = actionRef.action;
        if (action != null && action.enabled)
            action.Disable();
    }

    float ReadAxis(InputActionReference actionRef)
    {
        if (actionRef == null || actionRef.action == null || !actionRef.action.enabled)
            return 0f;

        var action = actionRef.action;

        if (action.activeValueType == typeof(float))
            return action.ReadValue<float>();

        if (action.activeValueType == typeof(Vector2))
        {
            Vector2 v = action.ReadValue<Vector2>();
            return v.y;
        }

        try
        {
            return action.ReadValue<float>();
        }
        catch
        {
            return 0f;
        }
    }

    // ───────────────────── Cut Effects (SFX + Fluid) ─────────────────────

    void HandleCutEffects()
    {
        if (_planeTransform == null)
            return;

        // If gore is zero and there are no SFX at all, skip quickly
        bool hasAnySfx =
            stemCutPrimary || stemCutSecondary ||
            leafCutPrimary || leafCutSecondary ||
            petalCutPrimary || petalCutSecondary;

        if (!hasAnySfx && goreIntensity <= 0f)
            return;

        // Build an oriented overlap box that roughly hugs the plane.
        Vector3 center = _planeTransform.position;
        Vector3 halfExtents = new Vector3(cutSenseLength * 0.5f, cutSenseRadius, cutSenseRadius);
        Quaternion rotation = _planeTransform.rotation;

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            rotation,
            cutDetectionMask,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0)
        {
            // generic splat if desired
            if (debugLogs)
                Debug.Log("[CuttingPlaneController] Cut: no specific hit found, using generic fluid if any.", this);

            // Generic fluid only (no SFX, or SFX could still play as generic stem if you want)
            TriggerFluid(genericFluidPlane, null);
            return;
        }

        // Choose what we "primarily" hit by tag priority: Leaf > Petal > Stem
        Collider leafCol = null;
        Collider petalCol = null;
        Collider stemCol = null;

        foreach (var col in hits)
        {
            if (col == null) continue;

            if (col.CompareTag("Leaf") )
            {
                leafCol = col;
                break;
            }

            if ((col.CompareTag("Petal") ) && petalCol == null)
            {
                petalCol = col;
            }

            if ((col.CompareTag("Stem") ) && stemCol == null)
            {
                stemCol = col;
            }
        }

        CutHitKind kind = CutHitKind.None;
        Collider chosen = null;

        if (leafCol != null)
        {
            kind = CutHitKind.Leaf;
            chosen = leafCol;
        }
        else if (petalCol != null)
        {
            kind = CutHitKind.Petal;
            chosen = petalCol;
        }
        else if (stemCol != null)
        {
            kind = CutHitKind.Stem;
            chosen = stemCol;
        }

        if (debugLogs)
        {
            Debug.Log($"[CuttingPlaneController] Cut overlap hits={hits.Length}, chosen={kind}, collider={chosen?.name}", this);
        }

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
                // None matched – maybe just a generic cut through nothing important
                PlayCutDual(stemCutPrimary, stemCutSecondary, stemSecondaryDelay);
                TriggerFluid(genericFluidPlane, chosen);
                break;
        }
    }

    void PlayCutDual(AudioClip first, AudioClip second, float delay)
    {
        if (AudioManager.Instance == null)
            return;

        if (first == null && second == null)
            return;

        if (second != null || delay > 0f)
            AudioManager.Instance.PlayDualSFX(first, second, delay);
        else
            AudioManager.Instance.PlaySFX(first);
    }

    /// <summary>
    /// Triggers fluid on both the plane-level squirter and any squirters
    /// found on/above the collider we hit.
    /// </summary>
    void TriggerFluid(FluidSquirter planeSquirter, Collider exampleCol)
    {
        float intensity = Mathf.Clamp01(goreIntensity);
        if (intensity <= 0f)
            return;

        // Plane-level squirter (global splatter)
        if (planeSquirter != null)
            planeSquirter.Squirt(intensity);

        // Hit-object squirters (local squirts on stem/leaf/petal pieces)
        if (exampleCol != null)
        {
            var squirters = exampleCol.GetComponentsInParent<FluidSquirter>();
            foreach (var fs in squirters)
            {
                fs.Squirt(intensity);
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Height gizmo
        Gizmos.color = Color.cyan;
        Vector3 pMin = transform.position;
        Vector3 pMax = transform.position;
        pMin.y = minY;
        pMax.y = maxY;
        Gizmos.DrawLine(pMin, pMax);

        // Detection gizmo
        if (!drawDetectionGizmo) return;

        Transform t = Application.isPlaying && plane != null ? plane.transform : transform;
        Vector3 center = t.position;
        Vector3 halfExtents = new Vector3(cutSenseLength * 0.5f, cutSenseRadius, cutSenseRadius);
        Quaternion rotation = t.rotation;

        Gizmos.color = detectionGizmoColor;
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, halfExtents * 2f);
        Gizmos.matrix = prev;
    }
#endif
}
