using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class FlowerTypeAuthoring : MonoBehaviour
{
    [Header("Meta")]
    public string flowerName = "New Flower Type";

    [Tooltip("If empty, we'll auto-gather from children.")]
    public FlowerStemRuntime stem;

    [Header("Assets")]
    [Tooltip("Ideal asset to create or overwrite. Leave null to auto-create in the same folder as prefab.")]
    public IdealFlowerDefinition idealAsset;

    [Tooltip("High-level type asset that points to the ideal + prefab and holds meta data.")]
    public FlowerTypeDefinition flowerTypeAsset;

    [Tooltip("If true, baking will auto-create a FlowerTypeDefinition if missing.")]
    public bool autoCreateTypeAsset = true;

    [Header("Default Tolerances When Baking Ideal")]
    public float defaultStemHardFailDelta = 0.5f;
    public float defaultStemPerfectDelta = 0.05f;
    public float defaultStemScoreWeight = 0.3f;

    public float defaultCutAngleHardFailDelta = 20f;
    public float defaultCutAnglePerfectDelta = 3f;
    public float defaultCutAngleScoreWeight = 0.2f;

#if UNITY_EDITOR

    [ContextMenu("Flower / Bake Ideal Definition From Current Pose")]
    public void BakeIdealFromCurrentPose()
    {
        if (string.IsNullOrEmpty(flowerName))
        {
            flowerName = gameObject.name;
        }

        // Ensure we have a stem ref
        if (stem == null)
        {
            stem = GetComponentInChildren<FlowerStemRuntime>();
            if (stem == null)
            {
                Debug.LogError($"[{nameof(FlowerTypeAuthoring)}] No FlowerStemRuntime found on '{name}' or children.");
                return;
            }
        }

        // Find all parts
        var parts = new List<FlowerPartRuntime>();
        GetComponentsInChildren(true, parts);

        if (parts.Count == 0)
        {
            Debug.LogWarning($"[{nameof(FlowerTypeAuthoring)}] No FlowerPartRuntime components found under '{name}'.");
        }

        // Create or reuse ideal asset
        if (idealAsset == null)
        {
            idealAsset = CreateIdealAssetOnDisk();
        }

        if (idealAsset == null)
        {
            Debug.LogError($"[{nameof(FlowerTypeAuthoring)}] Failed to create IdealFlowerDefinition asset.");
            return;
        }

        Undo.RecordObject(idealAsset, "Bake Ideal Flower Definition");

        // --- Stem values ---
        idealAsset.idealStemLength = stem.CurrentLength;
        idealAsset.stemHardFailDelta = defaultStemHardFailDelta;
        idealAsset.stemPerfectDelta = defaultStemPerfectDelta;
        idealAsset.stemScoreWeight = defaultStemScoreWeight;
        idealAsset.stemCanCauseGameOver = true;
        idealAsset.stemContributesToScore = true;

        float currentCutAngle = stem.GetCurrentCutAngleDeg(Vector3.up);
        idealAsset.idealCutAngleDeg = currentCutAngle;
        idealAsset.cutAngleHardFailDelta = defaultCutAngleHardFailDelta;
        idealAsset.cutAnglePerfectDelta = defaultCutAnglePerfectDelta;
        idealAsset.cutAngleScoreWeight = defaultCutAngleScoreWeight;
        idealAsset.cutAngleCanCauseGameOver = true;
        idealAsset.cutAngleContributesToScore = true;

        // --- Per-part rules ---
        idealAsset.partRules.Clear();

        foreach (var p in parts)
        {
            if (p == null || string.IsNullOrEmpty(p.partId))
            {
                Debug.LogWarning($"[{nameof(FlowerTypeAuthoring)}] Skipping part without partId on '{p?.name}'.");
                continue;
            }

            var rule = new IdealFlowerDefinition.PartRule
            {
                partId = p.partId,
                kind = p.kind,
                idealCondition = p.condition, // current condition becomes 'ideal'

                canCauseGameOver = p.canCauseGameOver,
                isSpecial = p.isSpecial,
                contributesToScore = p.contributesToScore,
                allowedWithered = p.allowedWithered,
                allowedMissing = p.allowedMissing,
                scoreWeight = p.scoreWeight,

                idealLocalPosition = p.transform.localPosition,
                idealLocalEuler = p.transform.localEulerAngles
            };

            idealAsset.partRules.Add(rule);
        }

        EditorUtility.SetDirty(idealAsset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[{nameof(FlowerTypeAuthoring)}] Baked IdealFlowerDefinition for '{flowerName}' with {idealAsset.partRules.Count} parts. Asset: {AssetDatabase.GetAssetPath(idealAsset)}");

        // Optionally create / update the FlowerTypeDefinition wrapper
        if (autoCreateTypeAsset)
        {
            BakeOrUpdateFlowerTypeAsset();
        }
    }

    [ContextMenu("Flower / Bake / Update Flower Type Asset Only")]
    public void BakeOrUpdateFlowerTypeAsset()
    {
        if (string.IsNullOrEmpty(flowerName))
        {
            flowerName = gameObject.name;
        }

        if (flowerTypeAsset == null)
        {
            flowerTypeAsset = CreateFlowerTypeAssetOnDisk();
        }

        if (flowerTypeAsset == null)
        {
            Debug.LogError($"[{nameof(FlowerTypeAuthoring)}] Failed to create FlowerTypeDefinition asset.");
            return;
        }

        Undo.RecordObject(flowerTypeAsset, "Bake Flower Type Definition");

        flowerTypeAsset.displayName = flowerName;
        if (string.IsNullOrEmpty(flowerTypeAsset.flowerId))
        {
            flowerTypeAsset.flowerId = MakeSafeIdentifier(flowerName);
        }

        // Hook ideal + prefab
        if (idealAsset == null)
        {
            idealAsset = CreateIdealAssetOnDisk();
        }

        flowerTypeAsset.ideal = idealAsset;

        var prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
        if (prefabRoot != null)
        {
            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabRoot);
            var prefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            flowerTypeAsset.flowerPrefab = prefabObj;
        }

        // You can tweak difficulty / base score per type in the inspector later.
        EditorUtility.SetDirty(flowerTypeAsset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[{nameof(FlowerTypeAuthoring)}] Updated FlowerTypeDefinition for '{flowerName}'. Asset: {AssetDatabase.GetAssetPath(flowerTypeAsset)}");
    }

    private IdealFlowerDefinition CreateIdealAssetOnDisk()
    {
        // Try to place it next to the prefab/file this component belongs to.
        var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
        string folder = "Assets";

        if (!string.IsNullOrEmpty(prefabPath))
        {
            folder = System.IO.Path.GetDirectoryName(prefabPath).Replace("\\", "/");
        }

        string safeName = MakeSafeFilename(flowerName);
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}_Ideal.asset");

        var asset = ScriptableObject.CreateInstance<IdealFlowerDefinition>();
        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return asset;
    }

    private FlowerTypeDefinition CreateFlowerTypeAssetOnDisk()
    {
        var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
        string folder = "Assets";

        if (!string.IsNullOrEmpty(prefabPath))
        {
            folder = System.IO.Path.GetDirectoryName(prefabPath).Replace("\\", "/");
        }

        string safeName = MakeSafeFilename(flowerName);
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}_Type.asset");

        var asset = ScriptableObject.CreateInstance<FlowerTypeDefinition>();
        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return asset;
    }

    private string MakeSafeFilename(string input)
    {
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '_');
        }
        return input;
    }

    private string MakeSafeIdentifier(string input)
    {
        input = input.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(input))
            return "flower_type";

        // Replace spaces and invalid chars with underscores.
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '_');
        }
        input = input.Replace(' ', '_');
        return input;
    }

#endif
}
