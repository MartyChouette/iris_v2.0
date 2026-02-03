using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Editor utility that programmatically builds the newspaper_dating scene with
/// a desk, newspaper with personal ads, marker controller, and UI overlays.
/// Menu: Window > Iris > Build Newspaper Dating Scene
/// </summary>
public static class NewspaperDatingSceneBuilder
{
    // Newspaper layer name — created if it doesn't exist
    private const string NewspaperLayerName = "Newspaper";

    [MenuItem("Window/Iris/Build Newspaper Dating Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int newspaperLayer = EnsureLayer(NewspaperLayerName);

        // ── 1. Directional light ───────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.96f, 0.88f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 2. Main Camera (static, looking straight down) ─────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<UnityEngine.Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.15f, 0.12f, 0.10f);
        cam.fieldOfView = 60f;
        camGO.transform.position = new Vector3(0f, 1.2f, 0f);
        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // ── 3. Desk geometry ───────────────────────────────────────────
        BuildDesk();

        // ── 4. Newspaper ───────────────────────────────────────────────
        BuildNewspaper(newspaperLayer);

        // ── 5. Marker system ───────────────────────────────────────────
        BuildMarkerSystem(newspaperLayer);

        // ── 6. NewspaperManager + screen-space UI ──────────────────────
        BuildNewspaperManager();

        // ── 7. Save scene ──────────────────────────────────────────────
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/newspaper_dating.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[NewspaperDatingSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // Desk
    // ════════════════════════════════════════════════════════════════════

    private static void BuildDesk()
    {
        var parent = new GameObject("Desk");

        // Desktop — flat brown surface
        CreateBox("DeskTop", parent.transform,
            new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.05f, 0.8f),
            new Color(0.45f, 0.30f, 0.18f));

        // Four legs
        float legH = 0.4f;
        float legT = 0.05f;
        float halfW = 0.55f;
        float halfD = 0.35f;
        float legY = legH / 2f;

        CreateBox("Leg_FL", parent.transform,
            new Vector3(-halfW, legY, -halfD), new Vector3(legT, legH, legT),
            new Color(0.35f, 0.22f, 0.12f));
        CreateBox("Leg_FR", parent.transform,
            new Vector3(halfW, legY, -halfD), new Vector3(legT, legH, legT),
            new Color(0.35f, 0.22f, 0.12f));
        CreateBox("Leg_BL", parent.transform,
            new Vector3(-halfW, legY, halfD), new Vector3(legT, legH, legT),
            new Color(0.35f, 0.22f, 0.12f));
        CreateBox("Leg_BR", parent.transform,
            new Vector3(halfW, legY, halfD), new Vector3(legT, legH, legT),
            new Color(0.35f, 0.22f, 0.12f));
    }

    // ════════════════════════════════════════════════════════════════════
    // Newspaper
    // ════════════════════════════════════════════════════════════════════

    private static void BuildNewspaper(int newspaperLayer)
    {
        var parent = new GameObject("Newspaper");

        // Newspaper surface — a quad lying on the desk
        var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        surface.name = "NewspaperSurface";
        surface.transform.SetParent(parent.transform);
        surface.transform.position = new Vector3(0f, 0.426f, 0f);
        surface.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        surface.transform.localScale = new Vector3(0.5f, 0.7f, 1f);
        surface.layer = newspaperLayer;
        surface.isStatic = true;

        // Replace the MeshCollider with a BoxCollider for raycast
        Object.DestroyImmediate(surface.GetComponent<MeshCollider>());
        var boxCol = surface.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(1f, 1f, 0.01f);
        boxCol.center = Vector3.zero;

        // Off-white newspaper material
        var rend = surface.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = new Color(0.92f, 0.90f, 0.85f);
            rend.sharedMaterial = mat;
        }

        // World-space canvas on the newspaper
        var canvasGO = new GameObject("NewspaperCanvas");
        canvasGO.transform.SetParent(parent.transform);
        canvasGO.transform.position = new Vector3(0f, 0.427f, 0f);
        canvasGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(500f, 700f);
        canvasRT.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        // Header
        CreateTMPText("Header_Text", canvasGO.transform,
            new Vector2(0f, 300f), new Vector2(400f, 60f),
            "PERSONALS", 36f, FontStyles.Bold, TextAlignmentOptions.Center);

        // 4 sample listings
        string[] names = { "Rose", "Thorn", "Lily", "Moss" };
        string[] ads =
        {
            "Romantic soul seeks someone who won't wilt under pressure. Enjoys candlelit dinners and light rain.",
            "Sharp wit, sharper edges. Looking for someone who can handle a little prick. Gardeners welcome.",
            "Gentle spirit with a pure heart. Allergic to drama, loves ponds and moonlight.",
            "Low-maintenance, earthy, always there. Seeking someone who appreciates the ground floor."
        };
        float[] arrivalTimes = { 30f, 45f, 20f, 60f };

        float startY = 200f;
        float listingHeight = 120f;

        for (int i = 0; i < 4; i++)
        {
            float yPos = startY - i * listingHeight;
            CreateListing(canvasGO.transform, i, names[i], ads[i], arrivalTimes[i],
                new Vector2(0f, yPos), new Vector2(440f, 100f), newspaperLayer);
        }
    }

    private static void CreateListing(Transform parent, int index,
        string charName, string adText, float arrivalTime,
        Vector2 anchoredPos, Vector2 size, int layer)
    {
        var go = new GameObject($"Listing_{index}");
        go.transform.SetParent(parent);
        go.layer = layer;

        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        // BoxCollider for click detection
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(size.x * 0.001f, size.y * 0.001f, 0.005f);
        col.center = Vector3.zero;

        // PersonalListing component
        var listing = go.AddComponent<PersonalListing>();

        // Name label
        var nameGO = CreateTMPText($"Name_{index}", go.transform,
            new Vector2(0f, 30f), new Vector2(size.x, 30f),
            charName, 18f, FontStyles.Bold, TextAlignmentOptions.Left);

        // Ad label
        var adGO = CreateTMPText($"Ad_{index}", go.transform,
            new Vector2(0f, -10f), new Vector2(size.x, 60f),
            adText, 12f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        // Wire up serialized fields via SerializedObject
        var so = new SerializedObject(listing);

        // Create a DatePersonalDefinition asset for this listing
        var def = ScriptableObject.CreateInstance<DatePersonalDefinition>();
        def.characterName = charName;
        def.adText = adText;
        def.arrivalTimeSec = arrivalTime;

        string defDir = "Assets/ScriptableObjects/Dating";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(defDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Dating");

        string defPath = $"{defDir}/Date_{charName}.asset";
        AssetDatabase.CreateAsset(def, defPath);

        so.FindProperty("definition").objectReferenceValue = def;
        so.FindProperty("nameLabel").objectReferenceValue = nameGO.GetComponent<TMP_Text>();
        so.FindProperty("adLabel").objectReferenceValue = adGO.GetComponent<TMP_Text>();
        so.FindProperty("circleAnchor").objectReferenceValue = go.transform;
        so.FindProperty("circleRadius").floatValue = size.x * 0.001f * 0.55f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateTMPText(string name, Transform parent,
        Vector2 anchoredPos, Vector2 size,
        string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);

        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = new Color(0.1f, 0.1f, 0.1f);
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        return go;
    }

    // ════════════════════════════════════════════════════════════════════
    // Marker System
    // ════════════════════════════════════════════════════════════════════

    private static void BuildMarkerSystem(int newspaperLayer)
    {
        var parent = new GameObject("MarkerSystem");

        // MarkerController component
        var controllerGO = new GameObject("MarkerController");
        controllerGO.transform.SetParent(parent.transform);
        var controller = controllerGO.AddComponent<MarkerController>();

        // Marker visual — tiny dark cube as Sharpie tip
        var visual = CreateBox("MarkerVisual", parent.transform,
            Vector3.zero, new Vector3(0.008f, 0.008f, 0.008f),
            new Color(0.05f, 0.05f, 0.05f));
        visual.isStatic = false;

        // CircleLine — LineRenderer
        var lineGO = new GameObject("CircleLine");
        lineGO.transform.SetParent(parent.transform);
        var lr = lineGO.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 0;
        lr.startWidth = 0.003f;
        lr.endWidth = 0.003f;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;

        // Dark marker material
        var lineMat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
        lineMat.color = new Color(0.1f, 0.05f, 0.05f);
        lr.sharedMaterial = lineMat;

        // Wire MarkerController serialized fields
        var so = new SerializedObject(controller);
        so.FindProperty("markerVisual").objectReferenceValue = visual.transform;
        so.FindProperty("circleLine").objectReferenceValue = lr;
        so.FindProperty("newspaperLayer").intValue = 1 << newspaperLayer;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ════════════════════════════════════════════════════════════════════
    // NewspaperManager + Screen-Space UI
    // ════════════════════════════════════════════════════════════════════

    private static void BuildNewspaperManager()
    {
        var managerGO = new GameObject("NewspaperManager");
        var manager = managerGO.AddComponent<NewspaperManager>();

        // Screen-space overlay canvas
        var uiCanvasGO = new GameObject("UI_Canvas");
        uiCanvasGO.transform.SetParent(managerGO.transform);
        var uiCanvas = uiCanvasGO.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 10;
        uiCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        uiCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Calling UI panel
        var callingPanel = CreateUIPanel("CallingUI", uiCanvasGO.transform,
            "Calling...", 32f, new Color(0f, 0f, 0f, 0.7f));

        // Timer UI panel
        var timerPanel = CreateUIPanel("TimerUI", uiCanvasGO.transform,
            "0:30", 48f, new Color(0f, 0f, 0f, 0.5f));

        // Arrived UI panel
        var arrivedPanel = CreateUIPanel("ArrivedUI", uiCanvasGO.transform,
            "Your date has arrived!", 36f, new Color(0f, 0f, 0f, 0.7f));

        // Wire NewspaperManager fields
        var so = new SerializedObject(manager);

        // Find the MarkerController in the scene
        var markerCtrl = Object.FindAnyObjectByType<MarkerController>();
        so.FindProperty("markerController").objectReferenceValue = markerCtrl;

        so.FindProperty("callingUI").objectReferenceValue = callingPanel;
        so.FindProperty("callingText").objectReferenceValue =
            callingPanel.GetComponentInChildren<TMP_Text>();

        so.FindProperty("timerUI").objectReferenceValue = timerPanel;
        so.FindProperty("timerText").objectReferenceValue =
            timerPanel.GetComponentInChildren<TMP_Text>();

        so.FindProperty("arrivedUI").objectReferenceValue = arrivedPanel;
        so.FindProperty("arrivedText").objectReferenceValue =
            arrivedPanel.GetComponentInChildren<TMP_Text>();

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateUIPanel(string name, Transform parent,
        string defaultText, float fontSize, Color bgColor)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent);

        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(500f, 100f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.localScale = Vector3.one;

        // Semi-transparent background
        var bg = panel.AddComponent<UnityEngine.UI.Image>();
        bg.color = bgColor;

        // Text child
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(panel.transform);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 10f);
        textRT.offsetMax = new Vector2(-10f, -10f);
        textRT.localScale = Vector3.one;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        // Start hidden
        panel.SetActive(false);

        return panel;
    }

    // ════════════════════════════════════════════════════════════════════
    // Shared helpers
    // ════════════════════════════════════════════════════════════════════

    private static GameObject CreateBox(string name, Transform parent,
        Vector3 position, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.isStatic = true;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = color;
            rend.sharedMaterial = mat;
        }
        return go;
    }

    /// <summary>
    /// Ensure a layer exists by name. Returns the layer index.
    /// </summary>
    private static int EnsureLayer(string layerName)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));

        var layersProp = tagManager.FindProperty("layers");

        // Check if the layer already exists
        for (int i = 0; i < layersProp.arraySize; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (element.stringValue == layerName)
                return i;
        }

        // Find first empty user layer (8+) and assign
        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(element.stringValue))
            {
                element.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[NewspaperDatingSceneBuilder] Added '{layerName}' as layer {i}.");
                return i;
            }
        }

        Debug.LogError($"[NewspaperDatingSceneBuilder] No empty layer slots for '{layerName}'.");
        return 0;
    }
}
