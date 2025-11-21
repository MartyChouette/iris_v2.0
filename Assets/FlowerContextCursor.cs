using UnityEngine;

/// <summary>
/// Context-sensitive cursor + interaction:
/// - Hover Leaf/Petal → pull cursor, pull tool active
/// - Hover trim zone collider → trim cursor, trim tool active
/// - Else → default cursor, both tools disabled
/// </summary>
[DisallowMultipleComponent]
public class FlowerContextCursor : MonoBehaviour
{
    public enum ContextMode { Default, Pull, Trim }

    [Header("Scene Refs")]
    public Camera cam;

    [Tooltip("Which layers to raycast against for interaction (leaves, petals, trim zone).")]
    public LayerMask interactMask = ~0;

    [Header("Tags / Components")]
    [Tooltip("Any collider with this tag will be treated as a leaf.")]
    public string leafTag = "Leaf";

    [Tooltip("Any collider with this tag will be treated as a petal.")]
    public string petalTag = "Petal";

    [Tooltip("Any collider with this tag will be treated as the stem-trim zone.")]
    public string stemTrimTag = "StemTrimZone";

    [Header("Cursor Textures")]
    public Texture2D defaultCursor;
    public Texture2D pullCursor;
    public Texture2D trimCursor;
    public Vector2 cursorHotspot = Vector2.zero;

    [Header("Tools To Toggle")]
    [Tooltip("Script that handles pulling leaves/petals (e.g. NewLeafGrabber).")]
    public MonoBehaviour pullTool;

    [Tooltip("Script that handles stem trimming (e.g. CuttingPlaneController / MouseBehaviour).")]
    public MonoBehaviour trimTool;

    [Header("Debug")]
    public ContextMode currentMode = ContextMode.Default;
    public float raycastMaxDistance = 5f;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        ApplyMode(ContextMode.Default, force: true);
    }

    void Update()
    {
        if (cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        ContextMode newMode = ContextMode.Default;

        if (Physics.Raycast(ray, out hit, raycastMaxDistance, interactMask, QueryTriggerInteraction.Collide))
        {
            GameObject go = hit.collider.gameObject;

            // Option 1: by tag
            if (go.CompareTag(leafTag) || go.CompareTag(petalTag))
            {
                newMode = ContextMode.Pull;
            }
            else if (go.CompareTag(stemTrimTag))
            {
                newMode = ContextMode.Trim;
            }
            else
            {
                // Option 2: by component (FlowerPartRuntime)
                var part = go.GetComponentInParent<FlowerPartRuntime>();
                if (part != null)
                {
                    if (part.kind == FlowerPartKind.Leaf || part.kind == FlowerPartKind.Petal)
                        newMode = ContextMode.Pull;
                }
            }
        }

        if (newMode != currentMode)
            ApplyMode(newMode, force: false);
    }

    void ApplyMode(ContextMode mode, bool force)
    {
        if (!force && mode == currentMode)
            return;

        currentMode = mode;

        // Enable / disable tools
        if (pullTool)
            pullTool.enabled = (mode == ContextMode.Pull);

        if (trimTool)
            trimTool.enabled = (mode == ContextMode.Trim);

        // Set cursor
        Texture2D tex = defaultCursor;
        if (mode == ContextMode.Pull && pullCursor) tex = pullCursor;
        else if (mode == ContextMode.Trim && trimCursor) tex = trimCursor;

        Cursor.SetCursor(tex, cursorHotspot, CursorMode.Auto);
    }
}
