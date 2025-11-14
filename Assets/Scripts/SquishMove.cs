using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
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

    [Header("Rigidbody/Constraints")]
    public bool enforceXYConstraints = true;
    public bool addRigidbodyIfMissing = true;

    [Header("Physics Coupling")]
    [Tooltip("If true, mouse drag drives the Rigidbody (so joints feel the tug).")]
    public bool driveRigidbodyFromDrag = true;

    [Tooltip("How fast the Rigidbody can accelerate toward the drag velocity (units/s^2).")]
    public float dragAcceleration = 40f;

    [Tooltip("Absolute safety cap on speed to prevent 'jettison'.")]
    public float hardMaxSpeed = 20f;

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

    // NEW: desired velocity from dragging, applied in FixedUpdate (XY only)
    private Vector3 desiredDragVelocityXY = Vector3.zero;

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
        originalMesh = GetComponent<MeshFilter>().sharedMesh;
        meshClone = Instantiate(originalMesh);
        GetComponent<MeshFilter>().sharedMesh = meshClone;

        meshRenderer = GetComponent<MeshRenderer>();

        jv = new JellyVertex[meshClone.vertices.Length];
        for (int i = 0; i < meshClone.vertices.Length; i++)
            jv[i] = new JellyVertex(i, transform.TransformPoint(meshClone.vertices[i]));

        lastWorldPos = transform.position;
    }

    void Update()
    {
        // Begin drag
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

                    // Collect vertices within radius (XY distance)
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

        // Dragging
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

                // Deform vertices (XY only; keep Z)
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

                if (moveStep.sqrMagnitude > 0f)
                {
                    if (rb != null && !rb.isKinematic && driveRigidbodyFromDrag)
                    {
                        // NEW: store a desired velocity (XY only), let physics ease toward it in FixedUpdate
                        Vector3 desiredVel = moveStep / Mathf.Max(Time.fixedDeltaTime, 1e-5f);
                        desiredVel.z = 0f;

                        // Clamp desired velocity to maxMoveSpeed
                        if (desiredVel.magnitude > maxMoveSpeed)
                            desiredVel = desiredVel.normalized * maxMoveSpeed;

                        desiredDragVelocityXY = desiredVel;
                    }
                    else
                    {
                        // Fallback: directly move transform (no physics coupling)
                        transform.position += new Vector3(moveStep.x, moveStep.y, 0f);
                    }

                    // Keep reference center in sync with our intent
                    initialDragCenter += new Vector3(moveStep.x, moveStep.y, 0f);
                }

                lastDragPoint = newPoint;
            }
        }

        // End drag
        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            draggedVertices.Clear();
            dragOffsets.Clear();

            // Let body slow down naturally when we stop dragging
            desiredDragVelocityXY = Vector3.zero;
        }
    }

    void FixedUpdate()
    {
        // 1) Drive the Rigidbody toward the desired drag velocity (XY only)
        if (rb != null && !rb.isKinematic && driveRigidbodyFromDrag)
        {
            float fdt = Time.fixedDeltaTime;

            Vector3 currentVel = rb.linearVelocity;
            Vector3 currentXY = new Vector3(currentVel.x, currentVel.y, 0f);

            // Smoothly move XY velocity toward desiredDragVelocityXY
            Vector3 targetXY = desiredDragVelocityXY;
            Vector3 newXY = Vector3.MoveTowards(currentXY, targetXY, dragAcceleration * fdt);

            // Hard cap absolute speed to avoid jettison
            float speed = newXY.magnitude;
            if (speed > hardMaxSpeed)
                newXY = newXY.normalized * hardMaxSpeed;

            rb.linearVelocity = new Vector3(newXY.x, newXY.y, currentVel.z);
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

        // 3) Jelly spring step
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

    static Vector2 ToXY(Vector3 v) => new Vector2(v.x, v.y);

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
