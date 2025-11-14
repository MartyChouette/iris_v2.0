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

    private Mesh originalMesh, meshClone;
    private MeshRenderer meshRenderer;  // ← renamed (no collision with Component.renderer)
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

    void Awake()
    {
        cam = Camera.main;

        if (!TryGetComponent(out rb))
        {
            if (addRigidbodyIfMissing) rb = gameObject.AddComponent<Rigidbody>();
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

        meshRenderer = GetComponent<MeshRenderer>(); // ← fixed name

        jv = new JellyVertex[meshClone.vertices.Length];
        for (int i = 0; i < meshClone.vertices.Length; i++)
            jv[i] = new JellyVertex(i, transform.TransformPoint(meshClone.vertices[i]));
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

                    // collect vertices within radius (XY distance)
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

                // Whole-object XY translation
                float startMoveAt = dragRadius * moveThreshold;
                float outside = Mathf.Max(0f, Vector2.Distance(ToXY(currentDragPoint), ToXY(initialDragCenter)) - startMoveAt);

                Vector3 moveStep = Vector3.zero;
                if (outside > 0f)
                {
                    Vector2 dirXY = (ToXY(currentDragPoint) - ToXY(initialDragCenter)).normalized;
                    moveStep = new Vector3(dirXY.x, dirXY.y, 0f) * (outside * moveGain);
                }
                moveStep += new Vector3(dragVelocity.x, dragVelocity.y, 0f) * (velocityMoveGain * dt);

                float maxStep = maxMoveSpeed * dt;
                if (moveStep.sqrMagnitude > maxStep * maxStep)
                    moveStep = moveStep.normalized * maxStep;

                if (moveStep.sqrMagnitude > 0f)
                {
                    if (rb != null && !rb.isKinematic)
                        rb.MovePosition(rb.position + new Vector3(moveStep.x, moveStep.y, 0f));
                    else
                        transform.position += new Vector3(moveStep.x, moveStep.y, 0f);

                    // shift world-space jelly anchors by same XY step
                    for (int i = 0; i < jv.Length; i++)
                        jv[i].Position += new Vector3(moveStep.x, moveStep.y, 0f);

                    // keep reference center in sync
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
        }
    }

    void FixedUpdate()
    {
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
