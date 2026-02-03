using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Editor utility that programmatically builds the bookcase_browsing scene with
/// a room, bookcase full of books, first-person browse camera, and UI overlay.
/// Menu: Window > Iris > Build Bookcase Browsing Scene
/// </summary>
public static class BookcaseSceneBuilder
{
    private const string BooksLayerName = "Books";

    // Bookcase dimensions
    private const float CaseWidth = 2.4f;
    private const float CaseHeight = 2.2f;
    private const float CaseDepth = 0.35f;
    private const float CaseCenterZ = 1.8f;
    private const int ShelfCount = 5;       // 5 shelves = 4 usable rows
    private const float ShelfThickness = 0.02f;
    private const float SidePanelThickness = 0.03f;

    // Book generation
    private const int MinBooksPerRow = 8;
    private const int MaxBooksPerRow = 14;
    private const float MinBookThickness = 0.02f;
    private const float MaxBookThickness = 0.06f;
    private const float MinGap = 0.001f;
    private const float MaxGap = 0.005f;

    // Spine colors — 10 muted library tones
    private static readonly Color[] SpineColors =
    {
        new Color(0.55f, 0.15f, 0.15f),    // dark red
        new Color(0.12f, 0.15f, 0.40f),    // navy
        new Color(0.12f, 0.35f, 0.15f),    // forest green
        new Color(0.70f, 0.62f, 0.48f),    // tan
        new Color(0.45f, 0.20f, 0.40f),    // plum
        new Color(0.25f, 0.25f, 0.25f),    // charcoal
        new Color(0.85f, 0.82f, 0.75f),    // cream
        new Color(0.50f, 0.12f, 0.18f),    // burgundy
        new Color(0.10f, 0.10f, 0.25f),    // midnight
        new Color(0.35f, 0.38f, 0.18f),    // olive
    };

    // Sample book data
    private static readonly string[] BookTitles =
    {
        "Roots in Darkness",
        "The Quiet Garden",
        "Letters to Soil",
        "Pressing Petals",
        "On Wilting",
        "Stem Theory",
        "A Gentle Thorn",
        "Bloom & Fade",
        "The Potting Shed",
        "Chlorophyll Dreams",
        "Under the Canopy",
        "Seed Catalog No. 7",
        "Still Life with Vase",
        "Water & Light",
        "The Pruner's Almanac",
    };

    private static readonly string[] BookAuthors =
    {
        "Eleanor Moss",
        "H. Fernwood",
        "Clara Rootley",
        "Jasper Thorn",
        "Wren Dewdrop",
        "P.L. Greenshaw",
        "Ivy Ashcroft",
        "Basil Harrow",
        "Rose Underhill",
        "Silas Compost",
        "Hazel Canopy",
        "Dahlia Greer",
        "Olive Stillman",
        "Linden Clearwell",
        "Sage Draper",
    };

    private static readonly string[][] BookPages =
    {
        new[] {
            "The roots do not ask permission. They push through clay and stone, searching for what they need in total darkness.",
            "I have often wondered if the flower knows it is beautiful, or if beauty is simply a side effect of reaching toward light.",
            "When the last petal falls, the stem stands bare — not empty, but unburdened."
        },
        new[] {
            "A garden is never quiet. Listen closely: the earthworms turning, the slow exhale of opening buds, the patient drip.",
            "She planted marigolds along the fence not for their color, but because they reminded her of someone she'd rather not forget.",
            "By September the garden had its own ideas. She learned to stop arguing with it."
        },
        new[] {
            "Dear Soil, I am writing to apologize. I have taken so much from you and returned so little.",
            "The compost heap is a love letter written in eggshells and coffee grounds. Decomposition as devotion.",
            "Perhaps we are all just soil, waiting patiently for something to take root."
        },
        new[] {
            "To press a flower is to stop time — or at least, to press pause. The color fades, but the shape remembers.",
            "Page 47 of her journal: a flattened daisy, brown at the edges, still holding its circular argument.",
            "Some flowers are better preserved in memory than in books. But we press them anyway."
        },
        new[] {
            "Wilting is not failure. It is the flower's way of saying: I have given everything I had to give.",
            "The drooping head of a sunflower in October carries more dignity than any spring bloom.",
            "We fear wilting because we see ourselves in it. But the plant does not fear. It simply returns."
        },
        new[] {
            "Chapter 1: The stem is not merely a support structure. It is a highway, a messenger, a spine.",
            "Consider the hollow stem of the dandelion — empty inside, yet strong enough to hold a wish.",
            "All architecture aspires to the condition of the stem: vertical, purposeful, alive."
        },
        new[] {
            "The thorn exists not out of cruelty but out of memory. Every wound the rose has received is recorded there.",
            "To hold a thorned stem gently requires practice. Most people never bother to learn.",
            "Gentleness, she decided, was not the absence of thorns but the willingness to reach past them."
        },
        new[] {
            "The bloom arrives without announcement. One morning the bud is closed; the next, it has made its decision.",
            "Fading is the bloom's longest act. We celebrate the opening, but the slow close deserves its own ovation.",
            "Between bloom and fade there is a single perfect day. The flower does not know which one it is."
        },
        new[] {
            "The potting shed smelled of damp earth and turpentine. Tools hung on nails, each one a different kind of patience.",
            "He repotted the fern for the third time that year. It kept outgrowing its container. He respected that.",
            "In the corner of the shed, a spider had built a web between two terra cotta pots. He left it there."
        },
        new[] {
            "Chlorophyll is the green dream of sunlight made solid. Every leaf is a sleeping solar panel.",
            "The plant does not choose to photosynthesize. It simply faces the light and lets chemistry do the rest.",
            "At night, the chloroplasts rest. Even the most dedicated workers need darkness to make sense of the day."
        },
        new[] {
            "The canopy is a ceiling made of arguments — each branch competing for its share of sky.",
            "Under the canopy, the light comes in fragments. The forest floor learns to make do with scraps.",
            "She sat under the oak for an hour. When she stood, she understood something she could not put into words."
        },
        new[] {
            "Item 7A: Moonflower seeds. Plant in evening. Do not expect results before midnight.",
            "Item 12C: Forget-me-nots. Self-explanatory. Plant near things you wish to remember.",
            "Item 23F: Ghost orchid. Availability uncertain. May not exist. Order anyway."
        },
        new[] {
            "The vase held three stems: one straight, one curved, one broken. Together they made a sentence.",
            "Still life painters know: the flower in the painting will outlast the flower on the table.",
            "She arranged the flowers not for beauty but for conversation. The lily had something to say to the rose."
        },
        new[] {
            "Water finds the root the way memory finds the dreamer — without effort, by gravity, in the dark.",
            "Too much light bleaches. Too little starves. The plant's whole life is a negotiation between extremes.",
            "The best gardeners understand that water and light are not gifts. They are partnerships."
        },
        new[] {
            "January: Sharpen the shears. A clean cut heals faster than a ragged one. This applies to most things.",
            "June: The roses will try to take over. Let them think they're winning, then prune on the solstice.",
            "October: Let the dead heads stand. The goldfinches need the seeds more than you need tidiness."
        },
    };

    [MenuItem("Window/Iris/Build Bookcase Browsing Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int booksLayer = EnsureLayer(BooksLayerName);

        // ── 1. Directional light ───────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.0f;
        light.color = new Color(1f, 0.95f, 0.85f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 2. Main Camera + BookcaseBrowseCamera ──────────────────────
        var camGO = BuildCamera();

        // ── 3. Room geometry ───────────────────────────────────────────
        BuildRoom();

        // ── 4. Bookcase frame ──────────────────────────────────────────
        BuildBookcaseFrame();

        // ── 5. Books ───────────────────────────────────────────────────
        BuildBooks(booksLayer);

        // ── 6. BookInteractionManager + UI ─────────────────────────────
        BuildInteractionManager(camGO, booksLayer);

        // ── 7. Save scene ──────────────────────────────────────────────
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/bookcase_browsing.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[BookcaseSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // Camera
    // ════════════════════════════════════════════════════════════════════

    private static GameObject BuildCamera()
    {
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<UnityEngine.Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.06f, 0.05f);
        cam.fieldOfView = 60f;
        camGO.transform.position = new Vector3(0f, 1.5f, 0f);
        camGO.transform.rotation = Quaternion.identity;

        camGO.AddComponent<BookcaseBrowseCamera>();

        // Reading anchor — child of camera, positioned in front
        var anchorGO = new GameObject("ReadingAnchor");
        anchorGO.transform.SetParent(camGO.transform);
        anchorGO.transform.localPosition = new Vector3(0f, -0.1f, 0.5f);
        anchorGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        return camGO;
    }

    // ════════════════════════════════════════════════════════════════════
    // Room
    // ════════════════════════════════════════════════════════════════════

    private static void BuildRoom()
    {
        var parent = new GameObject("Room");

        // Floor — 6m x 4m
        CreateBox("Floor", parent.transform,
            new Vector3(0f, -0.025f, 1f), new Vector3(6f, 0.05f, 4f),
            new Color(0.30f, 0.20f, 0.12f)); // dark wood

        // Back wall — at Z = 2 (behind bookcase)
        CreateBox("BackWall", parent.transform,
            new Vector3(0f, 1.5f, 2.2f), new Vector3(6f, 3f, 0.1f),
            new Color(0.85f, 0.78f, 0.68f)); // warm plaster

        // Side walls
        CreateBox("SideWall_L", parent.transform,
            new Vector3(-3f, 1.5f, 1f), new Vector3(0.1f, 3f, 4f),
            new Color(0.82f, 0.75f, 0.65f));

        CreateBox("SideWall_R", parent.transform,
            new Vector3(3f, 1.5f, 1f), new Vector3(0.1f, 3f, 4f),
            new Color(0.82f, 0.75f, 0.65f));
    }

    // ════════════════════════════════════════════════════════════════════
    // Bookcase Frame
    // ════════════════════════════════════════════════════════════════════

    private static void BuildBookcaseFrame()
    {
        var parent = new GameObject("Bookcase");
        var frame = new GameObject("BookcaseFrame");
        frame.transform.SetParent(parent.transform);

        Color darkBrown = new Color(0.25f, 0.15f, 0.08f);
        float caseX = 0f;
        float caseBottomY = 0f;

        // Side panels
        CreateBox("SidePanel_L", frame.transform,
            new Vector3(caseX - CaseWidth / 2f, caseBottomY + CaseHeight / 2f, CaseCenterZ),
            new Vector3(SidePanelThickness, CaseHeight, CaseDepth),
            darkBrown);

        CreateBox("SidePanel_R", frame.transform,
            new Vector3(caseX + CaseWidth / 2f, caseBottomY + CaseHeight / 2f, CaseCenterZ),
            new Vector3(SidePanelThickness, CaseHeight, CaseDepth),
            darkBrown);

        // Back panel
        CreateBox("BackPanel", frame.transform,
            new Vector3(caseX, caseBottomY + CaseHeight / 2f, CaseCenterZ + CaseDepth / 2f - 0.01f),
            new Vector3(CaseWidth, CaseHeight, 0.015f),
            new Color(0.20f, 0.12f, 0.06f));

        // Shelves — 5 horizontal planks (including top and bottom)
        float innerWidth = CaseWidth - SidePanelThickness * 2f;
        float rowHeight = CaseHeight / ShelfCount;

        for (int i = 0; i <= ShelfCount - 1; i++)
        {
            float shelfY = caseBottomY + i * rowHeight;
            CreateBox($"Shelf_{i}", frame.transform,
                new Vector3(caseX, shelfY, CaseCenterZ),
                new Vector3(innerWidth, ShelfThickness, CaseDepth),
                darkBrown);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Books
    // ════════════════════════════════════════════════════════════════════

    private static void BuildBooks(int booksLayer)
    {
        // Create BookDefinition assets
        string defDir = "Assets/ScriptableObjects/Bookcase";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(defDir))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Bookcase");

        var booksParent = new GameObject("Books");
        // Find or create the Bookcase parent
        var bookcase = GameObject.Find("Bookcase");
        if (bookcase != null)
            booksParent.transform.SetParent(bookcase.transform);

        float innerWidth = CaseWidth - SidePanelThickness * 2f;
        float rowHeight = CaseHeight / ShelfCount;
        float caseLeftX = -innerWidth / 2f;

        int bookIndex = 0;

        // 4 usable rows (between shelves)
        for (int row = 0; row < ShelfCount - 1; row++)
        {
            float shelfTopY = row * rowHeight + ShelfThickness / 2f;
            float availableHeight = rowHeight - ShelfThickness;

            // Random number of books per row
            int bookCount = Random.Range(MinBooksPerRow, MaxBooksPerRow + 1);
            float xCursor = caseLeftX + 0.01f; // small left margin

            for (int b = 0; b < bookCount; b++)
            {
                float thickness = Random.Range(MinBookThickness, MaxBookThickness);
                float heightFrac = Random.Range(0.70f, 0.95f);
                float bookHeight = availableHeight * heightFrac;
                float depthFrac = Random.Range(0.70f, 0.95f);
                float bookDepth = CaseDepth * depthFrac;
                Color color = SpineColors[Random.Range(0, SpineColors.Length)];

                // Check if book fits in row
                if (xCursor + thickness > innerWidth / 2f - 0.01f)
                    break;

                // Title/author — cycle through sample data
                int dataIndex = bookIndex % BookTitles.Length;
                string title = BookTitles[dataIndex];
                string author = BookAuthors[dataIndex];
                string[] pages = BookPages[dataIndex];

                // Create BookDefinition asset
                var def = ScriptableObject.CreateInstance<BookDefinition>();
                def.title = title;
                def.author = author;
                def.pageTexts = pages;
                def.spineColor = color;
                def.heightScale = heightFrac;
                def.thicknessScale = thickness;

                string defPath = $"{defDir}/Book_{bookIndex:D3}_{title.Replace(" ", "_")}.asset";
                AssetDatabase.CreateAsset(def, defPath);

                // Create book GameObject
                float bookX = xCursor + thickness / 2f;
                float bookY = shelfTopY + bookHeight / 2f;
                float bookZ = CaseCenterZ - (CaseDepth - bookDepth) / 2f * 0.5f;

                var bookGO = CreateBox($"Book_{bookIndex}", booksParent.transform,
                    new Vector3(bookX, bookY, bookZ),
                    new Vector3(thickness, bookHeight, bookDepth),
                    color);
                bookGO.isStatic = false;
                bookGO.layer = booksLayer;

                // Add BookVolume component
                var volume = bookGO.AddComponent<BookVolume>();

                // Build pages child hierarchy (inactive by default)
                var pagesRoot = BuildBookPages(bookGO.transform, thickness, bookHeight, bookDepth);

                // Wire serialized fields
                var so = new SerializedObject(volume);
                so.FindProperty("definition").objectReferenceValue = def;
                so.FindProperty("pagesRoot").objectReferenceValue = pagesRoot;

                // Find page labels
                var pageLabels = pagesRoot.GetComponentsInChildren<TMP_Text>(true);
                var labelsProperty = so.FindProperty("pageLabels");
                labelsProperty.arraySize = Mathf.Min(pageLabels.Length, 3);
                for (int p = 0; p < labelsProperty.arraySize; p++)
                    labelsProperty.GetArrayElementAtIndex(p).objectReferenceValue = pageLabels[p];

                so.ApplyModifiedPropertiesWithoutUndo();

                // Advance cursor
                xCursor += thickness + Random.Range(MinGap, MaxGap);
                bookIndex++;
            }
        }

        Debug.Log($"[BookcaseSceneBuilder] Created {bookIndex} books across {ShelfCount - 1} rows.");
    }

    private static GameObject BuildBookPages(Transform bookTransform, float thickness, float height, float depth)
    {
        var pagesRoot = new GameObject("Pages");
        pagesRoot.transform.SetParent(bookTransform);
        pagesRoot.transform.localPosition = Vector3.zero;
        pagesRoot.transform.localRotation = Quaternion.identity;

        // World-space canvas on the book face
        var canvasGO = new GameObject("PageCanvas");
        canvasGO.transform.SetParent(pagesRoot.transform);
        canvasGO.transform.localPosition = new Vector3(0f, 0f, -depth / 2f - 0.001f);
        canvasGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(900f, 400f);
        canvasRT.localScale = new Vector3(0.0005f, 0.0005f, 0.0005f);

        // Three pages: left, center, right
        string[] pageNames = { "PageLeft", "PageCenter", "PageRight" };
        float pageWidth = 280f;
        float[] xPositions = { -300f, 0f, 300f };

        for (int i = 0; i < 3; i++)
        {
            var pageGO = new GameObject(pageNames[i]);
            pageGO.transform.SetParent(canvasGO.transform);

            var rt = pageGO.AddComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(xPositions[i], 0f);
            rt.sizeDelta = new Vector2(pageWidth, 380f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            // Page background
            var bg = pageGO.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.95f, 0.93f, 0.88f, 0.95f);

            // Text child
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(pageGO.transform);

            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(8f, 8f);
            textRT.offsetMax = new Vector2(-8f, -8f);
            textRT.localScale = Vector3.one;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "";
            tmp.fontSize = 18f;
            tmp.fontStyle = FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = new Color(0.12f, 0.10f, 0.08f);
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
        }

        pagesRoot.SetActive(false);
        return pagesRoot;
    }

    // ════════════════════════════════════════════════════════════════════
    // Interaction Manager + UI
    // ════════════════════════════════════════════════════════════════════

    private static void BuildInteractionManager(GameObject camGO, int booksLayer)
    {
        var managerGO = new GameObject("BookInteractionManager");
        var manager = managerGO.AddComponent<BookInteractionManager>();

        // Screen-space overlay canvas for title hint
        var uiCanvasGO = new GameObject("UI_Canvas");
        var uiCanvas = uiCanvasGO.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 10;
        uiCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        uiCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Title hint panel — bottom center of screen
        var hintPanel = new GameObject("TitleHintPanel");
        hintPanel.transform.SetParent(uiCanvasGO.transform);

        var panelRT = hintPanel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.sizeDelta = new Vector2(400f, 50f);
        panelRT.anchoredPosition = new Vector2(0f, 30f);
        panelRT.localScale = Vector3.one;

        var bg = hintPanel.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        // Title text child
        var textGO = new GameObject("TitleText");
        textGO.transform.SetParent(hintPanel.transform);

        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 5f);
        textRT.offsetMax = new Vector2(-10f, -5f);
        textRT.localScale = Vector3.one;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Book Title";
        tmp.fontSize = 24f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Italic;

        hintPanel.SetActive(false);

        // Wire serialized fields on BookInteractionManager
        var readingAnchor = camGO.transform.Find("ReadingAnchor");
        var cam = camGO.GetComponent<UnityEngine.Camera>();
        var browseCamera = camGO.GetComponent<BookcaseBrowseCamera>();

        var so = new SerializedObject(manager);
        so.FindProperty("readingAnchor").objectReferenceValue = readingAnchor;
        so.FindProperty("mainCamera").objectReferenceValue = cam;
        so.FindProperty("browseCamera").objectReferenceValue = browseCamera;
        so.FindProperty("titleHintPanel").objectReferenceValue = hintPanel;
        so.FindProperty("titleHintText").objectReferenceValue = tmp;
        so.FindProperty("booksLayerMask").intValue = 1 << booksLayer;
        so.FindProperty("maxRayDistance").floatValue = 10f;
        so.ApplyModifiedPropertiesWithoutUndo();
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

    private static int EnsureLayer(string layerName)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));

        var layersProp = tagManager.FindProperty("layers");

        for (int i = 0; i < layersProp.arraySize; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (element.stringValue == layerName)
                return i;
        }

        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(element.stringValue))
            {
                element.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[BookcaseSceneBuilder] Added '{layerName}' as layer {i}.");
                return i;
            }
        }

        Debug.LogError($"[BookcaseSceneBuilder] No empty layer slots for '{layerName}'.");
        return 0;
    }
}
