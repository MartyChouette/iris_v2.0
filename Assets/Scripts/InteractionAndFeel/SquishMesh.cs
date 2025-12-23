/**
 * @file SquishMesh.cs
 * @brief SquishMesh script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/**
 * @class SquishMesh
 * @brief SquishMesh component.
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
public class SquishMesh : MonoBehaviour
{
    [Header("Jelly Settings")]
    public float Intensity = 1f;
    public float Mass = 1f;
    public float stiffness = 1f;
    public float damping = 0.75f;

    [Header("Drag Settings")]
    public float dragRadius = 0.5f; // how much area gets pulled
    public float dragStrength = 1f; // weight of the drag

    private Mesh OriginalMesh, MeshClone;
    private MeshRenderer renderer;
    private JellyVertex[] jv;
    private Vector3[] vertexArray;

    private Camera cam;
    private bool isDragging = false;
    private Plane dragPlane;
    private Vector3 currentDragPoint;

    private List<int> draggedVertices = new List<int>();
    private Dictionary<int, Vector3> dragOffsets = new Dictionary<int, Vector3>();

    void Start()
    {
        cam = Camera.main;

        OriginalMesh = GetComponent<MeshFilter>().sharedMesh;
        MeshClone = Instantiate(OriginalMesh);
        GetComponent<MeshFilter>().sharedMesh = MeshClone;
        renderer = GetComponent<MeshRenderer>();

        jv = new JellyVertex[MeshClone.vertices.Length];
        for (int i = 0; i < MeshClone.vertices.Length; i++)
            jv[i] = new JellyVertex(i, transform.TransformPoint(MeshClone.vertices[i]));
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == gameObject)
            {
                dragPlane = new Plane(Vector3.up, hit.point);
                if (dragPlane.Raycast(ray, out float enter))
                {
                    currentDragPoint = ray.GetPoint(enter);

                    // Find all vertices within dragRadius
                    draggedVertices.Clear();
                    dragOffsets.Clear();
                    for (int i = 0; i < jv.Length; i++)
                    {
                        float dist = Vector3.Distance(jv[i].Position, hit.point);
                        if (dist <= dragRadius)
                        {
                            draggedVertices.Add(i);
                            dragOffsets[i] = jv[i].Position - hit.point;
                        }
                    }

                    isDragging = true;
                }
            }
        }

        if (Input.GetMouseButton(0) && isDragging)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (dragPlane.Raycast(ray, out float enter))
            {
                currentDragPoint = ray.GetPoint(enter);

                foreach (int i in draggedVertices)
                {
                    float dist = Vector3.Distance(jv[i].Position, currentDragPoint);
                    float weight = Mathf.Clamp01(1f - dist / dragRadius) * dragStrength;
                    jv[i].Position = Vector3.Lerp(jv[i].Position, currentDragPoint + dragOffsets[i], weight);
                    jv[i].velocity = Vector3.zero;
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            draggedVertices.Clear();
            dragOffsets.Clear();
        }
    }

    void FixedUpdate()
    {
        vertexArray = OriginalMesh.vertices;
        for (int i = 0; i < jv.Length; i++)
        {
            Vector3 target = transform.TransformPoint(vertexArray[jv[i].ID]);
            float intensity = (1 - (renderer.bounds.max.y - target.y) / renderer.bounds.size.y) * Intensity;
            jv[i].Shake(target, Mass, stiffness, damping);
            target = transform.InverseTransformPoint(jv[i].Position);
            vertexArray[jv[i].ID] = Vector3.Lerp(vertexArray[jv[i].ID], target, intensity);
        }
        MeshClone.vertices = vertexArray;
        MeshClone.RecalculateNormals();
    }
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
