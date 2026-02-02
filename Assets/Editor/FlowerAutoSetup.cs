using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor wizard that auto-wires a flower model with all runtime components
/// needed by the Iris flower system. Accelerates new flower level creation.
///
/// Usage: Select a flower root GameObject in the hierarchy, then
/// Window > Iris > Flower Auto Setup.
///
/// The wizard detects stems, leaves, petals, and crown by name conventions,
/// then adds/wires FlowerSessionController, FlowerGameBrain, FlowerStemRuntime,
/// FlowerPartRuntime, XYTetherJoint, SapOnXYTether, and creates an
/// IdealFlowerDefinition ScriptableObject.
/// </summary>
public class FlowerAutoSetup : EditorWindow
{
    // ─────────────── Wizard state ───────────────

    private GameObject flowerRoot;
    private Vector2 scrollPos;

    // Detected parts
    private Transform detectedStem;
    private Transform detectedCrown;
    private readonly List<Transform> detectedLeaves = new();
    private readonly List<Transform> detectedPetals = new();

    // Settings
    private bool addXYTetherJoints = true;
    private bool addSapOnXYTether = true;
    private bool addMeshTargetToStem = true;
    private bool createIdealDefinition = true;
    private float defaultStemLength = 0.5f;
    private float defaultCutAngle = 45f;
    private float defaultBreakForce = 800f;
    private float defaultSpring = 1200f;
    private float defaultDamper = 60f;
    private float defaultMaxDistance = 0.3f;

    [MenuItem("Window/Iris/Flower Auto Setup")]
    public static void ShowWindow()
    {
        GetWindow<FlowerAutoSetup>("Flower Auto Setup");
    }

    private void OnEnable()
    {
        if (Selection.activeGameObject != null)
        {
            flowerRoot = Selection.activeGameObject;
            DetectParts();
        }
    }

    private void OnSelectionChange()
    {
        if (Selection.activeGameObject != null)
        {
            flowerRoot = Selection.activeGameObject;
            DetectParts();
        }
        Repaint();
    }

    // ─────────────── Detection ───────────────

    private void DetectParts()
    {
        detectedStem = null;
        detectedCrown = null;
        detectedLeaves.Clear();
        detectedPetals.Clear();

        if (flowerRoot == null) return;

        var allTransforms = flowerRoot.GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms)
        {
            if (t == flowerRoot.transform) continue;
            string lower = t.name.ToLowerInvariant();

            if (detectedStem == null && (lower.Contains("stem") && !lower.Contains("extra")))
                detectedStem = t;
            else if (detectedCrown == null && lower.Contains("crown"))
                detectedCrown = t;
            else if (lower.Contains("leaf"))
                detectedLeaves.Add(t);
            else if (lower.Contains("petal"))
                detectedPetals.Add(t);
        }
    }

    // ─────────────── GUI ───────────────

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUILayout.LabelField("Iris Flower Auto Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Select a flower root GameObject, verify the detected parts below, " +
            "then click Setup to add all required runtime components.",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        flowerRoot = (GameObject)EditorGUILayout.ObjectField("Flower Root", flowerRoot, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && flowerRoot != null)
            DetectParts();

        if (flowerRoot == null)
        {
            EditorGUILayout.HelpBox("Assign or select a flower root GameObject.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Detected Parts", EditorStyles.boldLabel);

        detectedStem = DrawTransformField("Stem", detectedStem);
        detectedCrown = DrawTransformField("Crown", detectedCrown);

        EditorGUILayout.LabelField($"Leaves ({detectedLeaves.Count})", EditorStyles.miniLabel);
        for (int i = 0; i < detectedLeaves.Count; i++)
            detectedLeaves[i] = DrawTransformField($"  Leaf {i}", detectedLeaves[i]);

        if (GUILayout.Button("+ Add Leaf Slot", GUILayout.Width(120)))
            detectedLeaves.Add(null);

        EditorGUILayout.LabelField($"Petals ({detectedPetals.Count})", EditorStyles.miniLabel);
        for (int i = 0; i < detectedPetals.Count; i++)
            detectedPetals[i] = DrawTransformField($"  Petal {i}", detectedPetals[i]);

        if (GUILayout.Button("+ Add Petal Slot", GUILayout.Width(120)))
            detectedPetals.Add(null);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Component Options", EditorStyles.boldLabel);
        addXYTetherJoints = EditorGUILayout.Toggle("Add XYTetherJoint to parts", addXYTetherJoints);
        addSapOnXYTether = EditorGUILayout.Toggle("Add SapOnXYTether to parts", addSapOnXYTether);
        addMeshTargetToStem = EditorGUILayout.Toggle("Add MeshTarget to stem", addMeshTargetToStem);
        createIdealDefinition = EditorGUILayout.Toggle("Create IdealFlowerDefinition", createIdealDefinition);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Default Values", EditorStyles.boldLabel);
        defaultStemLength = EditorGUILayout.FloatField("Ideal Stem Length", defaultStemLength);
        defaultCutAngle = EditorGUILayout.FloatField("Ideal Cut Angle (deg)", defaultCutAngle);

        if (addXYTetherJoints)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("XYTetherJoint Defaults", EditorStyles.boldLabel);
            defaultBreakForce = EditorGUILayout.FloatField("Break Force", defaultBreakForce);
            defaultSpring = EditorGUILayout.FloatField("Spring", defaultSpring);
            defaultDamper = EditorGUILayout.FloatField("Damper", defaultDamper);
            defaultMaxDistance = EditorGUILayout.FloatField("Max Distance", defaultMaxDistance);
        }

        EditorGUILayout.Space();
        bool canSetup = detectedStem != null;
        if (!canSetup)
            EditorGUILayout.HelpBox("At minimum, a Stem transform is required.", MessageType.Warning);

        EditorGUI.BeginDisabledGroup(!canSetup);
        if (GUILayout.Button("Setup Flower", GUILayout.Height(30)))
            PerformSetup();
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        if (GUILayout.Button("Validate Existing Setup"))
            ValidateSetup();

        EditorGUILayout.EndScrollView();
    }

    private Transform DrawTransformField(string label, Transform current)
    {
        return (Transform)EditorGUILayout.ObjectField(label, current, typeof(Transform), true);
    }

    // ─────────────── Setup logic ───────────────

    private void PerformSetup()
    {
        Undo.SetCurrentGroupName("Flower Auto Setup");
        int undoGroup = Undo.GetCurrentGroup();

        // 1. Session controller on root
        var session = EnsureComponent<FlowerSessionController>(flowerRoot);

        // 2. Game brain
        var brain = flowerRoot.GetComponentInChildren<FlowerGameBrain>(true);
        if (brain == null)
        {
            brain = EnsureComponent<FlowerGameBrain>(flowerRoot);
        }

        // 3. Stem runtime
        var stemRuntime = EnsureComponent<FlowerStemRuntime>(detectedStem.gameObject);

        // Wire stem transforms
        if (stemRuntime.StemAnchor == null)
        {
            var anchor = FindOrCreateChild(detectedStem, "StemAnchor");
            // Position anchor at top of stem
            anchor.localPosition = Vector3.up * GetApproxBoundsHeight(detectedStem.gameObject) * 0.5f;
            stemRuntime.StemAnchor = anchor;
        }

        if (stemRuntime.StemTip == null)
        {
            var tip = FindOrCreateChild(detectedStem, "StemTip");
            // Position tip at bottom of stem
            tip.localPosition = Vector3.up * GetApproxBoundsHeight(detectedStem.gameObject) * -0.5f;
            stemRuntime.StemTip = tip;
        }

        if (stemRuntime.cutNormalRef == null)
        {
            var normalRef = FindOrCreateChild(detectedStem, "CutNormalRef");
            normalRef.localRotation = Quaternion.identity;
            stemRuntime.cutNormalRef = normalRef;
        }

        // MeshTarget on stem
        if (addMeshTargetToStem)
        {
            EnsureComponent<DynamicMeshCutter.MeshTarget>(detectedStem.gameObject);
        }

        // 4. Wire brain
        var brainSO = new SerializedObject(brain);
        SetIfNull(brainSO, "stem", stemRuntime);
        brainSO.ApplyModifiedProperties();

        // 5. Rigidbody on stem
        var stemRb = EnsureComponent<Rigidbody>(detectedStem.gameObject);
        stemRb.isKinematic = false;
        stemRb.interpolation = RigidbodyInterpolation.Interpolate;

        // 6. Crown setup
        if (detectedCrown != null)
        {
            SetupPart(detectedCrown, "Crown", FlowerPartKind.Crown, stemRb);
        }

        // 7. Leaves setup
        for (int i = 0; i < detectedLeaves.Count; i++)
        {
            if (detectedLeaves[i] == null) continue;
            string partId = $"Leaf_{detectedLeaves[i].name}";
            SetupPart(detectedLeaves[i], partId, FlowerPartKind.Leaf, stemRb);
        }

        // 8. Petals setup
        for (int i = 0; i < detectedPetals.Count; i++)
        {
            if (detectedPetals[i] == null) continue;
            string partId = $"Petal_{detectedPetals[i].name}";
            SetupPart(detectedPetals[i], partId, FlowerPartKind.Petal, stemRb);
        }

        // 9. Re-collect parts into brain
        var allParts = flowerRoot.GetComponentsInChildren<FlowerPartRuntime>(true);
        var partsSO = new SerializedObject(brain);
        var partsList = partsSO.FindProperty("parts");
        partsList.ClearArray();
        for (int i = 0; i < allParts.Length; i++)
        {
            partsList.InsertArrayElementAtIndex(i);
            partsList.GetArrayElementAtIndex(i).objectReferenceValue = allParts[i];
        }
        partsSO.ApplyModifiedProperties();

        // 10. IdealFlowerDefinition
        if (createIdealDefinition)
        {
            CreateIdealDefinition(brain, allParts);
        }

        // 11. Wire session -> brain
        var sessionSO = new SerializedObject(session);
        SetIfNull(sessionSO, "brain", brain);
        sessionSO.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[FlowerAutoSetup] Setup complete on '{flowerRoot.name}': " +
                  $"{(detectedCrown != null ? 1 : 0)} crown, " +
                  $"{detectedLeaves.Count} leaves, {detectedPetals.Count} petals.");
    }

    private void SetupPart(Transform partTransform, string partId, FlowerPartKind kind, Rigidbody stemRb)
    {
        // Rigidbody
        var rb = EnsureComponent<Rigidbody>(partTransform.gameObject);
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // FlowerPartRuntime
        var part = EnsureComponent<FlowerPartRuntime>(partTransform.gameObject);
        var partSO = new SerializedObject(part);
        if (string.IsNullOrEmpty(partSO.FindProperty("PartId")?.stringValue))
        {
            var pidProp = partSO.FindProperty("PartId");
            if (pidProp != null)
            {
                pidProp.stringValue = partId;
            }
        }
        var kindProp = partSO.FindProperty("kind");
        if (kindProp != null)
            kindProp.enumValueIndex = (int)kind;
        partSO.ApplyModifiedProperties();

        // XYTetherJoint (optional)
        if (addXYTetherJoints && kind != FlowerPartKind.Crown)
        {
            var tether = EnsureComponent<XYTetherJoint>(partTransform.gameObject);
            var tetherSO = new SerializedObject(tether);
            SetIfNull(tetherSO, "connectedBody", stemRb);
            var bfProp = tetherSO.FindProperty("breakForce");
            if (bfProp != null && bfProp.floatValue == 0f)
                bfProp.floatValue = defaultBreakForce;
            var springProp = tetherSO.FindProperty("spring");
            if (springProp != null && springProp.floatValue == 0f)
                springProp.floatValue = defaultSpring;
            var damperProp = tetherSO.FindProperty("damper");
            if (damperProp != null && damperProp.floatValue == 0f)
                damperProp.floatValue = defaultDamper;
            var maxDistProp = tetherSO.FindProperty("maxDistance");
            if (maxDistProp != null && maxDistProp.floatValue == 0f)
                maxDistProp.floatValue = defaultMaxDistance;
            tetherSO.ApplyModifiedProperties();

            // SapOnXYTether
            if (addSapOnXYTether)
            {
                var sap = EnsureComponent<SapOnXYTether>(partTransform.gameObject);
                var sapSO = new SerializedObject(sap);
                var pkProp = sapSO.FindProperty("partKind");
                if (pkProp != null)
                    pkProp.enumValueIndex = kind == FlowerPartKind.Petal ? 1 : 0;
                sapSO.ApplyModifiedProperties();
            }
        }
    }

    private void CreateIdealDefinition(FlowerGameBrain brain, FlowerPartRuntime[] allParts)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Ideal Flower Definition",
            $"Ideal_{flowerRoot.name}",
            "asset",
            "Choose where to save the IdealFlowerDefinition");

        if (string.IsNullOrEmpty(path)) return;

        var ideal = ScriptableObject.CreateInstance<IdealFlowerDefinition>();

        // Set stem defaults via SerializedObject
        var idealSO = new SerializedObject(ideal);
        SetFloat(idealSO, "idealStemLength", defaultStemLength);
        SetFloat(idealSO, "idealCutAngleDeg", defaultCutAngle);
        SetFloat(idealSO, "stemScoreWeight", 0.3f);
        SetFloat(idealSO, "cutAngleScoreWeight", 0.3f);
        SetFloat(idealSO, "stemPerfectDelta", 0.05f);
        SetFloat(idealSO, "stemHardFailDelta", 0.3f);
        SetFloat(idealSO, "cutAnglePerfectDelta", 5f);
        SetFloat(idealSO, "cutAngleHardFailDelta", 45f);
        idealSO.ApplyModifiedProperties();

        // Add part rules
        var rulesList = idealSO.FindProperty("partRules");
        if (rulesList != null)
        {
            rulesList.ClearArray();
            for (int i = 0; i < allParts.Length; i++)
            {
                var p = allParts[i];
                rulesList.InsertArrayElementAtIndex(i);
                var elem = rulesList.GetArrayElementAtIndex(i);

                SetString(elem, "partId", GetPartId(p));
                SetEnum(elem, "kind", (int)GetPartKind(p));
                SetEnum(elem, "idealCondition", (int)FlowerPartCondition.Normal);
                SetBool(elem, "contributesToScore", true);
                SetBool(elem, "canCauseGameOver", GetPartKind(p) == FlowerPartKind.Crown);
                SetFloat(elem, "scoreWeight", 1f);
            }
        }
        idealSO.ApplyModifiedProperties();

        AssetDatabase.CreateAsset(ideal, path);
        AssetDatabase.SaveAssets();

        // Wire to brain
        var brainSO = new SerializedObject(brain);
        var idealProp = brainSO.FindProperty("ideal");
        if (idealProp != null)
        {
            idealProp.objectReferenceValue = ideal;
            brainSO.ApplyModifiedProperties();
        }

        Debug.Log($"[FlowerAutoSetup] Created IdealFlowerDefinition at '{path}' with {allParts.Length} part rules.");
    }

    // ─────────────── Validation ───────────────

    private void ValidateSetup()
    {
        if (flowerRoot == null)
        {
            Debug.LogError("[FlowerAutoSetup] No flower root selected.");
            return;
        }

        int issues = 0;

        var session = flowerRoot.GetComponentInChildren<FlowerSessionController>(true);
        if (session == null) { Debug.LogError("[Validate] Missing FlowerSessionController", flowerRoot); issues++; }

        var brain = flowerRoot.GetComponentInChildren<FlowerGameBrain>(true);
        if (brain == null) { Debug.LogError("[Validate] Missing FlowerGameBrain", flowerRoot); issues++; }
        else
        {
            var brainSO = new SerializedObject(brain);
            if (brainSO.FindProperty("ideal")?.objectReferenceValue == null)
            { Debug.LogWarning("[Validate] FlowerGameBrain.ideal is null", brain); issues++; }
            if (brainSO.FindProperty("stem")?.objectReferenceValue == null)
            { Debug.LogError("[Validate] FlowerGameBrain.stem is null", brain); issues++; }

            var partsList = brainSO.FindProperty("parts");
            if (partsList == null || partsList.arraySize == 0)
            { Debug.LogWarning("[Validate] FlowerGameBrain.parts is empty", brain); issues++; }
        }

        var stemRuntime = flowerRoot.GetComponentInChildren<FlowerStemRuntime>(true);
        if (stemRuntime == null) { Debug.LogError("[Validate] Missing FlowerStemRuntime", flowerRoot); issues++; }
        else
        {
            if (stemRuntime.StemAnchor == null) { Debug.LogError("[Validate] FlowerStemRuntime.StemAnchor is null", stemRuntime); issues++; }
            if (stemRuntime.StemTip == null) { Debug.LogError("[Validate] FlowerStemRuntime.StemTip is null", stemRuntime); issues++; }
        }

        var parts = flowerRoot.GetComponentsInChildren<FlowerPartRuntime>(true);
        foreach (var p in parts)
        {
            var pSO = new SerializedObject(p);
            string pid = pSO.FindProperty("PartId")?.stringValue;
            if (string.IsNullOrEmpty(pid))
            { Debug.LogWarning($"[Validate] FlowerPartRuntime on '{p.name}' has empty PartId", p); issues++; }

            if (p.GetComponent<Rigidbody>() == null)
            { Debug.LogError($"[Validate] FlowerPartRuntime '{p.name}' has no Rigidbody", p); issues++; }
        }

        if (issues == 0)
            Debug.Log($"[FlowerAutoSetup] Validation passed - {parts.Length} parts, no issues found.");
        else
            Debug.LogWarning($"[FlowerAutoSetup] Validation found {issues} issue(s). Check console for details.");
    }

    // ─────────────── Helpers ───────────────

    private T EnsureComponent<T>(GameObject go) where T : Component
    {
        if (!go.TryGetComponent<T>(out var existing))
        {
            existing = Undo.AddComponent<T>(go);
        }
        return existing;
    }

    private Transform FindOrCreateChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        return go.transform;
    }

    private float GetApproxBoundsHeight(GameObject go)
    {
        var renderer = go.GetComponentInChildren<Renderer>();
        if (renderer != null) return renderer.bounds.size.y;
        return 1f;
    }

    private void SetIfNull(SerializedObject so, string propName, Object value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null && prop.objectReferenceValue == null)
            prop.objectReferenceValue = value;
    }

    private void SetFloat(SerializedObject so, string propName, float value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.floatValue = value;
    }

    private void SetString(SerializedProperty parent, string propName, string value)
    {
        var prop = parent.FindPropertyRelative(propName);
        if (prop != null) prop.stringValue = value;
    }

    private void SetEnum(SerializedProperty parent, string propName, int value)
    {
        var prop = parent.FindPropertyRelative(propName);
        if (prop != null) prop.enumValueIndex = value;
    }

    private void SetBool(SerializedProperty parent, string propName, bool value)
    {
        var prop = parent.FindPropertyRelative(propName);
        if (prop != null) prop.boolValue = value;
    }

    private void SetFloat(SerializedProperty parent, string propName, float value)
    {
        var prop = parent.FindPropertyRelative(propName);
        if (prop != null) prop.floatValue = value;
    }

    private string GetPartId(FlowerPartRuntime p)
    {
        var so = new SerializedObject(p);
        return so.FindProperty("PartId")?.stringValue ?? p.name;
    }

    private FlowerPartKind GetPartKind(FlowerPartRuntime p)
    {
        var so = new SerializedObject(p);
        var prop = so.FindProperty("kind");
        return prop != null ? (FlowerPartKind)prop.enumValueIndex : FlowerPartKind.Leaf;
    }
}
