using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using Iris.Camera;

/// <summary>
/// Editor utility that programmatically builds the camera_test scene with
/// a test room, fixed horror cameras, trigger zones, and a player capsule.
/// Menu: Window > Iris > Build Camera Test Scene
/// </summary>
public static class CameraTestSceneBuilder
{
    [MenuItem("Window/Iris/Build Camera Test Scene")]
    public static void Build()
    {
        // ── 0. New empty scene ─────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        EnsurePlayerTag();

        // ── 1. Directional light ───────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.color = new Color(1f, 0.95f, 0.85f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 2. Main Camera with CinemachineBrain ───────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<UnityEngine.Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
        camGO.AddComponent<CinemachineBrain>();
        // URP camera data is auto-added by the pipeline

        // ── 3. Room geometry ───────────────────────────────────────────
        BuildRoom();

        // ── 4. Player capsule ──────────────────────────────────────────
        var player = BuildPlayer();

        // ── 5. HorrorCameraManager ─────────────────────────────────────
        var managerGO = new GameObject("HorrorCameraManager");
        managerGO.AddComponent<HorrorCameraManager>();

        // ── 6. Cameras & Trigger Zones ─────────────────────────────────
        BuildCamerasAndZones();

        // ── 7. Save scene ──────────────────────────────────────────────
        string dir = "Assets/Scenes";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string path = $"{dir}/camera_test.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[CameraTestSceneBuilder] Scene saved to {path}");
    }

    // ════════════════════════════════════════════════════════════════════
    // Room geometry
    // ════════════════════════════════════════════════════════════════════

    private static void BuildRoom()
    {
        // Room layout (top-down, Z = north):
        //
        //   +----------+-------+
        //   |          | East  |
        //   | Hallway  | Room  |
        //   |          |       |
        //   +--wall-A--+       |
        //   | Entry /  |       |
        //   | Trans    +-------+
        //   |          |
        //   | SouthCor |
        //   +----------+
        //
        // Full extents: X [-5, 5], Z [-10, 10]  →  10 wide, 20 deep
        // Divider A (east-west wall) at Z = 0, from X = -5 to X = 0
        // Divider B (north-south wall) at X = 0, from Z = 0 to Z = 10

        var parent = new GameObject("Room");

        // Floor
        CreateBox("Floor", parent.transform,
            new Vector3(0f, -0.05f, 0f), new Vector3(10f, 0.1f, 20f),
            new Color(0.25f, 0.25f, 0.25f));

        // Walls (0.3 thick, 4 tall)
        float wallH = 4f;
        float wallT = 0.3f;

        // South wall  Z = -10
        CreateBox("Wall_South", parent.transform,
            new Vector3(0f, wallH / 2f, -10f), new Vector3(10f + wallT, wallH, wallT),
            new Color(0.35f, 0.32f, 0.30f));

        // North wall  Z = 10
        CreateBox("Wall_North", parent.transform,
            new Vector3(0f, wallH / 2f, 10f), new Vector3(10f + wallT, wallH, wallT),
            new Color(0.35f, 0.32f, 0.30f));

        // West wall   X = -5
        CreateBox("Wall_West", parent.transform,
            new Vector3(-5f, wallH / 2f, 0f), new Vector3(wallT, wallH, 20f + wallT),
            new Color(0.35f, 0.32f, 0.30f));

        // East wall   X = 5
        CreateBox("Wall_East", parent.transform,
            new Vector3(5f, wallH / 2f, 0f), new Vector3(wallT, wallH, 20f + wallT),
            new Color(0.35f, 0.32f, 0.30f));

        // Divider A — east-west wall at Z=0, X in [-5, 0]
        CreateBox("Divider_A", parent.transform,
            new Vector3(-2.5f, wallH / 2f, 0f), new Vector3(5f, wallH, wallT),
            new Color(0.40f, 0.35f, 0.32f));

        // Divider B — north-south wall at X=0, Z in [0, 10]
        CreateBox("Divider_B", parent.transform,
            new Vector3(0f, wallH / 2f, 5f), new Vector3(wallT, wallH, 10f),
            new Color(0.40f, 0.35f, 0.32f));
    }

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
            // Create a simple material (URP Lit if available, else Standard)
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = color;
            rend.sharedMaterial = mat;
        }
        return go;
    }

    // ════════════════════════════════════════════════════════════════════
    // Player
    // ════════════════════════════════════════════════════════════════════

    private static GameObject BuildPlayer()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Player";
        go.tag = "Player";
        go.transform.position = new Vector3(0f, 1f, 0f);

        // Remove default CapsuleCollider — CharacterController handles collision
        Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());

        var cc = go.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = Vector3.zero;

        go.AddComponent<SimpleTestCharacter>();

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                   ?? Shader.Find("Standard"));
            mat.color = new Color(0.2f, 0.6f, 0.9f);
            rend.sharedMaterial = mat;
        }

        return go;
    }

    // ════════════════════════════════════════════════════════════════════
    // Cameras & Trigger Zones
    // ════════════════════════════════════════════════════════════════════

    private static void BuildCamerasAndZones()
    {
        var parent = new GameObject("Cameras");
        var zonesParent = new GameObject("TriggerZones");

        // ── Camera definitions ─────────────────────────────────────────
        // Each: name, position, lookTarget, FOV, isInitial
        // Zone:  center, size, hardCut, blendDuration

        // 1. Entry — high corner, 35° down, 45° yaw (initial camera)
        var camEntry = CreateCinemachineCamera("Cam_Entry", parent.transform,
            new Vector3(-4f, 5f, -6f),
            Quaternion.Euler(35f, 45f, 0f),
            60f, priority: 20); // starts active

        CreateTriggerZone("Zone_Entry", zonesParent.transform,
            new Vector3(-2.5f, 1.5f, -3f), new Vector3(5f, 3f, 6f),
            camEntry, hardCut: false, blendDuration: 0.5f, zoneName: "Entry");

        // 2. South Corridor — low angle (0.8m height) — hard cut
        var camSouth = CreateCinemachineCamera("Cam_SouthCorridor", parent.transform,
            new Vector3(-3f, 0.8f, -9f),
            Quaternion.Euler(5f, 30f, 0f),
            60f);

        CreateTriggerZone("Zone_SouthCorridor", zonesParent.transform,
            new Vector3(-2.5f, 1.5f, -8f), new Vector3(5f, 3f, 4f),
            camSouth, hardCut: true, blendDuration: 0f, zoneName: "South Corridor");

        // 3. East Room — overhead surveillance (60° down) — blend 0.4s
        var camEast = CreateCinemachineCamera("Cam_EastRoom", parent.transform,
            new Vector3(2.5f, 7f, 5f),
            Quaternion.Euler(60f, 180f, 0f),
            60f);

        CreateTriggerZone("Zone_EastRoom", zonesParent.transform,
            new Vector3(2.5f, 1.5f, 5f), new Vector3(5f, 3f, 10f),
            camEast, hardCut: false, blendDuration: 0.4f, zoneName: "East Room");

        // 4. North Hallway — end-of-hall looking back (tight 45° FOV) — hard cut
        var camNorth = CreateCinemachineCamera("Cam_NorthHallway", parent.transform,
            new Vector3(-2.5f, 3f, 9f),
            Quaternion.Euler(20f, 180f, 0f),
            45f);

        CreateTriggerZone("Zone_NorthHallway", zonesParent.transform,
            new Vector3(-2.5f, 1.5f, 7f), new Vector3(5f, 3f, 6f),
            camNorth, hardCut: true, blendDuration: 0f, zoneName: "North Hallway");

        // 5. Transition — mid-room diagonal — blend 0.5s
        var camTrans = CreateCinemachineCamera("Cam_Transition", parent.transform,
            new Vector3(-4.5f, 4f, 1f),
            Quaternion.Euler(30f, 60f, 0f),
            55f);

        CreateTriggerZone("Zone_Transition", zonesParent.transform,
            new Vector3(-2.5f, 1.5f, 1f), new Vector3(5f, 3f, 4f),
            camTrans, hardCut: false, blendDuration: 0.5f, zoneName: "Transition");
    }

    private static CinemachineCamera CreateCinemachineCamera(
        string name, Transform parent,
        Vector3 position, Quaternion rotation, float fov,
        int priority = 0)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.rotation = rotation;

        var vcam = go.AddComponent<CinemachineCamera>();
        vcam.Lens = new LensSettings
        {
            FieldOfView = fov,
            NearClipPlane = 0.1f,
            FarClipPlane = 500f,
        };
        vcam.Priority = priority;

        return vcam;
    }

    private static void CreateTriggerZone(
        string name, Transform parent,
        Vector3 center, Vector3 size,
        CinemachineCamera targetCamera,
        bool hardCut, float blendDuration, string zoneName)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = center;

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        box.center = Vector3.zero;

        var trigger = go.AddComponent<CameraZoneTrigger>();
        trigger.targetCamera = targetCamera;
        trigger.hardCut = hardCut;
        trigger.blendDuration = blendDuration;

        // Set the private/serialized zoneName via SerializedObject
        var so = new SerializedObject(trigger);
        var prop = so.FindProperty("zoneName");
        if (prop != null)
        {
            prop.stringValue = zoneName;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // Tag helper
    // ════════════════════════════════════════════════════════════════════

    private static void EnsurePlayerTag()
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));

        var tagsProp = tagManager.FindProperty("tags");

        // "Player" is a built-in tag in Unity, but verify it exists
        bool found = false;
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == "Player")
            {
                found = true;
                break;
            }
        }

        // Also check if it works as a built-in tag
        try
        {
            // Unity's built-in tags include "Player" — setting tag will throw
            // if it doesn't exist at all. This is a safety net.
            var temp = new GameObject("_tagTest");
            temp.tag = "Player";
            Object.DestroyImmediate(temp);
            found = true;
        }
        catch
        {
            // Tag doesn't exist as built-in, add it
        }

        if (!found)
        {
            int idx = tagsProp.arraySize;
            tagsProp.InsertArrayElementAtIndex(idx);
            tagsProp.GetArrayElementAtIndex(idx).stringValue = "Player";
            tagManager.ApplyModifiedProperties();
            Debug.Log("[CameraTestSceneBuilder] Added 'Player' tag to TagManager.");
        }
    }
}
