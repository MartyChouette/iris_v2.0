/**
 * @file SquishMove.cs
 * @brief SquishMove script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
/**
 * @class SquishMove
 * @brief SquishMove component.
 * @details
 * Responsibilities:
 * - (Documented) See fields and methods below.
 *
 * Unity lifecycle:
 * - Awake(): cache references / validate setup.
 * - OnEnable()/OnDisable(): hook/unhook events.
 * - Update(): per-frame behavior (if any).
 *
 * Gotchas:
 * - Keep hot paths allocation-free (Update/cuts/spawns).
 * - Prefer event-driven UI updates over per-frame string building.
 *
 * @ingroup tools
 */
public class SquishMove : MonoBehaviour
{
    [Header("Jelly Settings")]
    public float Intensity = 1f;
    public float Mass = 1f;
    public float stiffness = 1f;
    public float damping = 0.75f;

    [Header("Drag Settings (XY-only)")]
    public float dragRadius = 0.5f;
    public float dragStrength = 1f;

    [Header("Object Motion (XY-only)")]
    [Range(0f, 1f)] public float moveThreshold = 0.6f;
    public float moveGain = 0.35f;
    public float velocityMoveGain = 0.02f;
    public float maxMoveSpeed = 6f;

    [Header("Rigidbody / Constraints")]
    public bool enforceXYConstraints = true;
    public bool addRigidbodyIfMissing = true;

    [Header("Physics Coupling")]
    [Tooltip("If true, mouse drag drives the Rigidbody (so joints feel the tug).")]
    public bool driveRigidbodyFromDrag = true;

    [Tooltip("How fast the Rigidbody is allowed to accelerate toward drag intent.")]
    public float dragAcceleration = 40f;

    [Tooltip("Absolute cap on Rigidbody speed to prevent 'jettison'.")]
    public float hardMaxSpeed = 15f;

    [Header("Stem Coupling (optional)")]
    [Tooltip("If set, this jelly will also tug on another jelly (e.g. stem) when dragged.")]
    public SquishMove coupledStem;

    [Tooltip("World-space point where this jelly is attached to the stem (the small sphere in the stem).")]
    public Transform stemAttachPoint;

    [Tooltip("Radius around the attach point that the stem jelly will be deformed.")]
    public float stemTugRadius = 0.35f;

    [Tooltip("How strongly we deform the stem jelly per drag step.")]
    public float stemTugStrength = 0.7f;

    // ────────────────────────── Private state ──────────────────────────

    private Mesh originalMesh, meshClone;
    private MeshRenderer meshRenderer;
    private JellyVertex[] jv;
    private Vector3[] vertexArray;

    private Camera cam;
    private bool isDragging = false;
    private Plane dragPlane;    // XY plane at planeZ
    private float planeZ;
    private Vector3 currentDragPoint;

    private Vector3 initialDragCenter;
    private Vector3 lastDragPoint;
    private Vector3 dragVelocity;

    private readonly List<int> draggedVertices = new List<int>();
    private readonly Dictionary<int, Vector3> dragOffsets = new Dictionary<int, Vector3>();

    private Rigidbody rb;

    // Track last world position so jelly vertices can follow physics motion
    private Vector3 lastWorldPos;

    // The "intent" movement per frame from mouse drag (XY only)
    private Vector3 dragMoveStep = Vector3.zero;

    // ────────────────────────── Unity lifecycle ──────────────────────────

    void Awake()
    {
        cam = Camera.main;

        if (!TryGetComponent(out rb))
        {
            if (addRigidbodyIfMissing)
                rb = gameObject.AddComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            if (enforceXYConstraints)
            {
                rb.constraints = RigidbodyConstraints.FreezePositionZ
                                 | RigidbodyConstraints.FreezeRotationX
                                 | RigidbodyConstraints.FreezeRotationY
                                 | RigidbodyConstraints.FreezeRotationZ;
            }
        }
    }

    void Start()
    {
        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            Debug.LogWarning($"[SquishMove] No MeshFilter found on '{gameObject.name}'. SquishMove requires a MeshFilter.", this);
            enabled = false;
            return;
        }
        
        originalMesh = meshFilter.sharedMesh;
        if (originalMesh == null)
        {
            Debug.LogWarning($"[SquishMove] MeshFilter on '{gameObject.name}' has no mesh assigned. SquishMove disabled.", this);
            enabled = false;
            return;
        }
        
        meshClone = Instantiate(originalMesh);
        meshFilter.sharedMesh = meshClone;

        meshRenderer = GetComponent<MeshRenderer>();

        jv = new JellyVertex[meshClone.vertices.Length];
        for (int i = 0; i < meshClone.vertices.Length; i++)
            jv[i] = new JellyVertex(i, transform.TransformPoint(meshClone.vertices[i]));

        lastWorldPos = transform.position;
    }

    void Update()
    {
        // default: no intent if we're not dragging this frame
        dragMoveStep = Vector3.zero;

        // ───── Begin drag ─────
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == gameObject)
            {
                // Lock to XY plane at hit Z
                planeZ = hit.point.z;
                dragPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));

                if (dragPlane.Raycast(ray, out float enter))
                {
                    currentDragPoint = ray.GetPoint(enter);
                    currentDragPoint.z = planeZ;

                    initialDragCenter = currentDragPoint;
                    lastDragPoint = currentDragPoint;
                    dragVelocity = Vector3.zero;

                    // Collect vertices within radius (XY distance) from hit point
                    draggedVertices.Clear();
                    dragOffsets.Clear();
                    for (int i = 0; i < jv.Length; i++)
                    {
                        float distXY = Vector2.Distance(ToXY(jv[i].Position), ToXY(hit.point));
                        if (distXY <= dragRadius)
                        {
                            Vector3 off = jv[i].Position - hit.point;
                            off.z = 0f; // XY-only offset
                            draggedVertices.Add(i);
                            dragOffsets[i] = off;
                        }
                    }

                    isDragging = true;
                }
            }
        }

        // ───── Dragging ─────
        if (Input.GetMouseButton(0) && isDragging)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (dragPlane.Raycast(ray, out float enter))
            {
                Vector3 newPoint = ray.GetPoint(enter);
                newPoint.z = planeZ;
                float dt = Mathf.Max(Time.deltaTime, 1e-5f);

                // XY velocity only
                Vector3 frameDelta = newPoint - lastDragPoint;
                frameDelta.z = 0f;
                Vector3 instVel = frameDelta / dt;
                dragVelocity = Vector3.Lerp(dragVelocity, instVel, 0.5f);

                currentDragPoint = newPoint;

                // Deform this mesh's jelly verts (XY only; keep Z)
                foreach (int i in draggedVertices)
                {
                    float distXY = Vector2.Distance(ToXY(jv[i].Position), ToXY(currentDragPoint));
                    float weight = Mathf.Clamp01(1f - distXY / dragRadius) * dragStrength;

                    Vector3 target = currentDragPoint + dragOffsets[i];
                    target.z = jv[i].Position.z;
                    jv[i].Position = Vector3.Lerp(jv[i].Position, target, weight);
                    jv[i].velocity = Vector3.zero;
                }

                // Whole-object XY translation "intent"
                float startMoveAt = dragRadius * moveThreshold;
                float outside = Mathf.Max(0f, Vector2.Distance(ToXY(currentDragPoint), ToXY(initialDragCenter)) - startMoveAt);

                Vector3 moveStep = Vector3.zero;
                if (outside > 0f)
                {
                    Vector2 dirXY = (ToXY(currentDragPoint) - ToXY(initialDragCenter)).normalized;
                    moveStep = new Vector3(dirXY.x, dirXY.y, 0f) * (outside * moveGain);
                }
                moveStep += new Vector3(dragVelocity.x, dragVelocity.y, 0f) * (velocityMoveGain * dt);

                // Clamp the *intent* step length
                float maxStep = maxMoveSpeed * dt;
                if (moveStep.sqrMagnitude > maxStep * maxStep)
                    moveStep = moveStep.normalized * maxStep;

                dragMoveStep = moveStep; // store for FixedUpdate / AddForce

                // ───── Stem coupling: tug the stem jelly around the attach point ─────
                if (coupledStem != null && stemAttachPoint != null)
                {
                    // Use the same step as a tug vector (XY only).
                    Vector3 tugVec = new Vector3(moveStep.x, moveStep.y, 0f);

                    // Center the tug at the attach point (your small joint sphere in the stem).
                    Vector3 stemCenter = stemAttachPoint.position;

                    coupledStem.ApplyExternalTug(stemCenter, tugVec, stemTugRadius, stemTugStrength);
                }

                // Keep reference center in sync with our intent
                initialDragCenter += new Vector3(moveStep.x, moveStep.y, 0f);

                lastDragPoint = newPoint;
            }
        }

        // ───── End drag ─────
        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            draggedVertices.Clear();
            dragOffsets.Clear();

            dragMoveStep = Vector3.zero; // no more drive intent
        }
    }

    void FixedUpdate()
    {
        // 1) Drive the Rigidbody with forces (not teleport / hard velocity)
        if (rb != null && !rb.isKinematic && driveRigidbodyFromDrag)
        {
            float fdt = Time.fixedDeltaTime;

            if (dragMoveStep.sqrMagnitude > 0f)
            {
                // Turn intent step into desired velocity
                Vector3 desiredVel = dragMoveStep / Mathf.Max(fdt, 1e-5f);
                desiredVel.z = 0f;

                // Clamp to a soft max speed
                if (desiredVel.magnitude > maxMoveSpeed)
                    desiredVel = desiredVel.normalized * maxMoveSpeed;

                Vector3 currentVel = rb.linearVelocity;
                Vector3 currentXY = new Vector3(currentVel.x, currentVel.y, 0f);

                Vector3 neededAccel = (desiredVel - currentXY) / Mathf.Max(fdt, 1e-5f);

                // Clamp acceleration
                if (neededAccel.magnitude > dragAcceleration)
                    neededAccel = neededAccel.normalized * dragAcceleration;

                // Apply force to drive toward desired velocity (XY only)
                Vector3 force = neededAccel * rb.mass;
                rb.AddForce(new Vector3(force.x, force.y, 0f), ForceMode.Force);

                // Final safety cap on absolute speed so we don't yeet into infinity
                Vector3 v = rb.linearVelocity;
                float speed = v.magnitude;
                if (speed > hardMaxSpeed)
                    rb.linearVelocity = v.normalized * hardMaxSpeed;
            }
        }

        // 2) Make jelly vertices follow actual body motion caused by physics/joints
        Vector3 worldDelta = transform.position - lastWorldPos;
        if (worldDelta.sqrMagnitude > 0f)
        {
            for (int i = 0; i < jv.Length; i++)
            {
                jv[i].Position += worldDelta;
            }
        }
        lastWorldPos = transform.position;

        // 3) Jelly spring step into mesh vertices
        vertexArray = originalMesh.vertices;

        for (int i = 0; i < jv.Length; i++)
        {
            Vector3 target = transform.TransformPoint(vertexArray[jv[i].ID]);
            float intensity = (1 - (meshRenderer.bounds.max.y - target.y) / meshRenderer.bounds.size.y) * Intensity;

            jv[i].Shake(target, Mass, stiffness, damping);

            Vector3 worldPos = jv[i].Position;
            Vector3 localPos = transform.InverseTransformPoint(worldPos);

            vertexArray[jv[i].ID] = Vector3.Lerp(vertexArray[jv[i].ID], localPos, intensity);
        }

        meshClone.vertices = vertexArray;
        meshClone.RecalculateNormals();
    }

    // ────────────────────────── External tug API ──────────────────────────

    /// <summary>
    /// External tug: deform this jelly around a world-space center by a tug vector.
    /// Used so a leaf can tug on the stem's jelly verts.
    /// </summary>
    public void ApplyExternalTug(Vector3 worldCenter, Vector3 tugVector, float radius, float strength)
    {
        if (jv == null || jv.Length == 0)
            return;

        if (tugVector.sqrMagnitude < 1e-8f || radius <= 0f || strength <= 0f)
            return;

        for (int i = 0; i < jv.Length; i++)
        {
            float distXY = Vector2.Distance(ToXY(jv[i].Position), ToXY(worldCenter));
            if (distXY <= radius)
            {
                float weight = Mathf.Clamp01(1f - distXY / radius) * strength;

                // Tug in XY only, keep Z as-is
                Vector3 tugXY = new Vector3(tugVector.x, tugVector.y, 0f);
                jv[i].Position += tugXY * weight;
                jv[i].velocity = Vector3.zero;
            }
        }
    }

    // ────────────────────────── Helpers / nested type ──────────────────────────

    static Vector2 ToXY(Vector3 v) => new Vector2(v.x, v.y);
    /**
     * @class JellyVertex
     * @brief JellyVertex component.
     * @details
     * Responsibilities:
     * - (Documented) See fields and methods below.
     *
     * Unity lifecycle:
     * - Awake(): cache references / validate setup.
     * - OnEnable()/OnDisable(): hook/unhook events.
     * - Update(): per-frame behavior (if any).
     *
     * Gotchas:
     * - Keep hot paths allocation-free (Update/cuts/spawns).
     * - Prefer event-driven UI updates over per-frame string building.
     *
     * @ingroup tools
     */

    public class JellyVertex
    {
        public int ID;
        public Vector3 Position;
        public Vector3 velocity, Force;

        public JellyVertex(int _id, Vector3 _pos)
        {
            ID = _id;
            Position = _pos;
        }

        public void Shake(Vector3 target, float m, float s, float d)
        {
            Force = (target - Position) * s;
            velocity = (velocity + Force / m) * d;
            Position += velocity;

            if ((velocity + Force + Force / m).magnitude < 0.001f)
                Position = target;
        }
    }
}
