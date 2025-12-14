// Assets/Editor/CodebaseAudit/CodebaseAuditReport.cs
// Generates:
//  - UsedInScenes.csv
//  - UsedInPrefabs.csv
//  - PotentiallyDead.csv
//  - Dupes.csv
//
// How it works:
//  - Scans ALL scenes under Assets (not just build settings) by opening them Additive, enumerating root objects,
//    collecting MonoBehaviours and their scripts, then closing.
//  - Scans ALL prefabs under Assets via PrefabUtility.LoadPrefabContents, enumerating MonoBehaviours, then unloading.
//  - Writes CSVs to: Assets/Reports/CodebaseAudit/<timestamp>/
//
// Notes:
//  - Editor-only. Safe to include in repo.
//  - Does not rely on string matching / dependencies; it inspects actual components.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodebaseAudit
{
    public static class CodebaseAuditReport
    {
        private const string REPORT_ROOT = "Assets/Reports/CodebaseAudit";

        private sealed class ScriptUsage
        {
            public string ScriptAssetPath;          // path to .cs from MonoScript
            public string FullTypeName;             // Namespace.Type
            public string AssemblyName;
            public HashSet<string> Scenes = new HashSet<string>();
            public HashSet<string> Prefabs = new HashSet<string>();
            public int SceneComponentCount;
            public int PrefabComponentCount;

            // Heuristics / flags
            public bool LooksLikeSingleton;
            public bool LooksLikeAudioBackbone;
            public bool LooksLikeManager;
        }

        private sealed class Occurrence
        {
            public string ContainerPath;     // scene path or prefab path
            public string GameObjectPath;    // hierarchy path inside container
        }

        [MenuItem("Tools/Codebase Audit/Generate Usage Report (Scenes + Prefabs)")]
        public static void Generate()
        {
            // Ensure reports folder exists
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outDir = Path.Combine(REPORT_ROOT, timestamp).Replace("\\", "/");
            Directory.CreateDirectory(outDir);

            // Collect all MonoScripts in project (MonoBehaviour only)
            var allMonoBehaviourScripts = FindAllMonoBehaviourScripts();
            var usage = new Dictionary<string, ScriptUsage>(StringComparer.Ordinal);

            foreach (var ms in allMonoBehaviourScripts)
            {
                var t = ms.GetClass();
                if (t == null) continue;
                if (!typeof(MonoBehaviour).IsAssignableFrom(t)) continue;

                string key = t.AssemblyQualifiedName;
                usage[key] = new ScriptUsage
                {
                    ScriptAssetPath = AssetDatabase.GetAssetPath(ms),
                    FullTypeName = t.FullName ?? t.Name,
                    AssemblyName = t.Assembly.GetName().Name ?? "",
                    LooksLikeSingleton = Heuristics_LooksLikeSingleton(t),
                    LooksLikeAudioBackbone = Heuristics_LooksLikeAudioBackbone(t),
                    LooksLikeManager = Heuristics_LooksLikeManager(t),
                };
            }

            // Scan scenes
            var scenePaths = AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();

            // Scan prefabs
            var prefabPaths = AssetDatabase.FindAssets("t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();

            // Actual occurrence maps for Dupes
            // key: scriptKey -> list of occurrences
            var sceneOccurrences = new Dictionary<string, List<Occurrence>>(StringComparer.Ordinal);
            var prefabOccurrences = new Dictionary<string, List<Occurrence>>(StringComparer.Ordinal);

            try
            {
                EditorUtility.DisplayProgressBar("Codebase Audit", "Scanning scenes...", 0f);
                ScanAllScenes(scenePaths, usage, sceneOccurrences);

                EditorUtility.DisplayProgressBar("Codebase Audit", "Scanning prefabs...", 0.5f);
                ScanAllPrefabs(prefabPaths, usage, prefabOccurrences);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // Write CSVs
            string usedInScenesPath = Path.Combine(outDir, "UsedInScenes.csv").Replace("\\", "/");
            string usedInPrefabsPath = Path.Combine(outDir, "UsedInPrefabs.csv").Replace("\\", "/");
            string potentiallyDeadPath = Path.Combine(outDir, "PotentiallyDead.csv").Replace("\\", "/");
            string dupesPath = Path.Combine(outDir, "Dupes.csv").Replace("\\", "/");

            WriteUsedInScenesCsv(usedInScenesPath, usage);
            WriteUsedInPrefabsCsv(usedInPrefabsPath, usage);
            WritePotentiallyDeadCsv(potentiallyDeadPath, usage);
            WriteDupesCsv(dupesPath, usage, sceneOccurrences, prefabOccurrences);

            AssetDatabase.Refresh();

            Debug.Log(
                $"[CodebaseAudit] Report complete.\n" +
                $"- {usedInScenesPath}\n" +
                $"- {usedInPrefabsPath}\n" +
                $"- {potentiallyDeadPath}\n" +
                $"- {dupesPath}\n"
            );
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Scene scanning
        // ─────────────────────────────────────────────────────────────────────────────

        private static void ScanAllScenes(
            List<string> scenePaths,
            Dictionary<string, ScriptUsage> usage,
            Dictionary<string, List<Occurrence>> occurrences)
        {
            for (int i = 0; i < scenePaths.Count; i++)
            {
                string scenePath = scenePaths[i];
                float p = scenePaths.Count == 0 ? 0f : (i / (float)scenePaths.Count) * 0.5f;
                EditorUtility.DisplayProgressBar("Codebase Audit", $"Scene {i + 1}/{scenePaths.Count}: {scenePath}", p);

                // Open additively so we can close it immediately without clobbering the user's open scene.
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        CollectMonoBehavioursFromRoot(
                            root,
                            containerPath: scenePath,
                            isScene: true,
                            usage,
                            occurrences
                        );
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Prefab scanning
        // ─────────────────────────────────────────────────────────────────────────────

        private static void ScanAllPrefabs(
            List<string> prefabPaths,
            Dictionary<string, ScriptUsage> usage,
            Dictionary<string, List<Occurrence>> occurrences)
        {
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                string prefabPath = prefabPaths[i];
                float p = 0.5f + (prefabPaths.Count == 0 ? 0f : (i / (float)prefabPaths.Count) * 0.5f);
                EditorUtility.DisplayProgressBar("Codebase Audit", $"Prefab {i + 1}/{prefabPaths.Count}: {prefabPath}", p);

                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(prefabPath);
                    if (root == null) continue;

                    CollectMonoBehavioursFromRoot(
                        root,
                        containerPath: prefabPath,
                        isScene: false,
                        usage,
                        occurrences
                    );
                }
                finally
                {
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Collectors
        // ─────────────────────────────────────────────────────────────────────────────

        private static void CollectMonoBehavioursFromRoot(
            GameObject root,
            string containerPath,
            bool isScene,
            Dictionary<string, ScriptUsage> usage,
            Dictionary<string, List<Occurrence>> occurrences)
        {
            // Include inactive so we don’t miss disabled objects
            var monos = root.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (var mb in monos)
            {
                if (mb == null) continue; // missing script
                var t = mb.GetType();
                string key = t.AssemblyQualifiedName;

                if (!usage.TryGetValue(key, out var su))
                {
                    // This happens if script is in a package/asmdef we didn't enumerate by MonoScript,
                    // or if it’s generated. We still want to track it.
                    su = new ScriptUsage
                    {
                        ScriptAssetPath = "(unknown)",
                        FullTypeName = t.FullName ?? t.Name,
                        AssemblyName = t.Assembly.GetName().Name ?? "",
                        LooksLikeSingleton = Heuristics_LooksLikeSingleton(t),
                        LooksLikeAudioBackbone = Heuristics_LooksLikeAudioBackbone(t),
                        LooksLikeManager = Heuristics_LooksLikeManager(t),
                    };
                    usage[key] = su;
                }

                if (isScene)
                {
                    su.Scenes.Add(containerPath);
                    su.SceneComponentCount++;
                }
                else
                {
                    su.Prefabs.Add(containerPath);
                    su.PrefabComponentCount++;
                }

                if (!occurrences.TryGetValue(key, out var list))
                {
                    list = new List<Occurrence>();
                    occurrences[key] = list;
                }

                list.Add(new Occurrence
                {
                    ContainerPath = containerPath,
                    GameObjectPath = GetHierarchyPath(mb.gameObject)
                });
            }
        }

        private static string GetHierarchyPath(GameObject go)
        {
            if (go == null) return "(null)";
            var stack = new Stack<string>();
            Transform t = go.transform;
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", stack);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Script inventory
        // ─────────────────────────────────────────────────────────────────────────────

        private static List<MonoScript> FindAllMonoBehaviourScripts()
        {
            // Load all MonoScripts and filter to those that have a class type deriving from MonoBehaviour.
            // This is better than searching for "*.cs" because it respects asmdefs and script compilation.
            return AssetDatabase.FindAssets("t:MonoScript")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<MonoScript>(path))
                .Where(ms => ms != null)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // CSV Writers
        // ─────────────────────────────────────────────────────────────────────────────

        private static void WriteUsedInScenesCsv(string path, Dictionary<string, ScriptUsage> usage)
        {
            using var w = new StreamWriter(path);
            w.WriteLine("FullTypeName,Assembly,ScriptAssetPath,SceneCount,SceneComponentCount,ScenePaths");

            foreach (var su in usage.Values
                         .Where(u => u.Scenes.Count > 0)
                         .OrderByDescending(u => u.SceneComponentCount)
                         .ThenBy(u => u.FullTypeName, StringComparer.Ordinal))
            {
                w.WriteLine(string.Join(",",
                    Csv(su.FullTypeName),
                    Csv(su.AssemblyName),
                    Csv(su.ScriptAssetPath),
                    su.Scenes.Count.ToString(),
                    su.SceneComponentCount.ToString(),
                    Csv(string.Join(" | ", su.Scenes.OrderBy(s => s, StringComparer.Ordinal)))
                ));
            }
        }

        private static void WriteUsedInPrefabsCsv(string path, Dictionary<string, ScriptUsage> usage)
        {
            using var w = new StreamWriter(path);
            w.WriteLine("FullTypeName,Assembly,ScriptAssetPath,PrefabCount,PrefabComponentCount,PrefabPaths");

            foreach (var su in usage.Values
                         .Where(u => u.Prefabs.Count > 0)
                         .OrderByDescending(u => u.PrefabComponentCount)
                         .ThenBy(u => u.FullTypeName, StringComparer.Ordinal))
            {
                w.WriteLine(string.Join(",",
                    Csv(su.FullTypeName),
                    Csv(su.AssemblyName),
                    Csv(su.ScriptAssetPath),
                    su.Prefabs.Count.ToString(),
                    su.PrefabComponentCount.ToString(),
                    Csv(string.Join(" | ", su.Prefabs.OrderBy(s => s, StringComparer.Ordinal)))
                ));
            }
        }

        private static void WritePotentiallyDeadCsv(string path, Dictionary<string, ScriptUsage> usage)
        {
            using var w = new StreamWriter(path);
            w.WriteLine("FullTypeName,Assembly,ScriptAssetPath,LooksLikeSingleton,LooksLikeAudioBackbone,LooksLikeManager,Notes");

            foreach (var su in usage.Values
                         .Where(u => u.Scenes.Count == 0 && u.Prefabs.Count == 0)
                         .OrderByDescending(u => u.LooksLikeSingleton || u.LooksLikeAudioBackbone || u.LooksLikeManager)
                         .ThenBy(u => u.FullTypeName, StringComparer.Ordinal))
            {
                string notes = "";
                if (su.LooksLikeAudioBackbone) notes += "Audio-backbone-heuristic; ";
                if (su.LooksLikeSingleton) notes += "Singleton-heuristic; ";
                if (su.LooksLikeManager) notes += "Manager/Controller-heuristic; ";

                w.WriteLine(string.Join(",",
                    Csv(su.FullTypeName),
                    Csv(su.AssemblyName),
                    Csv(su.ScriptAssetPath),
                    su.LooksLikeSingleton ? "TRUE" : "FALSE",
                    su.LooksLikeAudioBackbone ? "TRUE" : "FALSE",
                    su.LooksLikeManager ? "TRUE" : "FALSE",
                    Csv(notes.Trim())
                ));
            }
        }

        private static void WriteDupesCsv(
            string path,
            Dictionary<string, ScriptUsage> usage,
            Dictionary<string, List<Occurrence>> sceneOcc,
            Dictionary<string, List<Occurrence>> prefabOcc)
        {
            using var w = new StreamWriter(path);
            w.WriteLine("Category,FullTypeName,Assembly,ScriptAssetPath,SceneInstanceCount,PrefabInstanceCount,ExampleOccurrences");

            // 1) Singletons with multiple attachments
            foreach (var kvp in usage)
            {
                var su = kvp.Value;
                if (!su.LooksLikeSingleton) continue;

                int sCount = su.SceneComponentCount;
                int pCount = su.PrefabComponentCount;
                if (sCount + pCount <= 1) continue;

                var examples = BuildExampleOccurrenceString(kvp.Key, sceneOcc, prefabOcc, maxExamples: 6);
                w.WriteLine(string.Join(",",
                    Csv("SingletonMultipleInstances"),
                    Csv(su.FullTypeName),
                    Csv(su.AssemblyName),
                    Csv(su.ScriptAssetPath),
                    sCount.ToString(),
                    pCount.ToString(),
                    Csv(examples)
                ));
            }

            // 2) Audio backbones present (helps catch AudioManager vs MusicPlayer situations)
            foreach (var kvp in usage)
            {
                var su = kvp.Value;
                if (!su.LooksLikeAudioBackbone) continue;

                // Always list them if they exist anywhere; this file is meant to help you reason about duplicates.
                if (su.SceneComponentCount + su.PrefabComponentCount == 0) continue;

                var examples = BuildExampleOccurrenceString(kvp.Key, sceneOcc, prefabOcc, maxExamples: 6);
                w.WriteLine(string.Join(",",
                    Csv("AudioBackboneFound"),
                    Csv(su.FullTypeName),
                    Csv(su.AssemblyName),
                    Csv(su.ScriptAssetPath),
                    su.SceneComponentCount.ToString(),
                    su.PrefabComponentCount.ToString(),
                    Csv(examples)
                ));
            }

            // 3) “Manager-like” scripts duplicated (useful when you have multiple menu managers etc.)
            foreach (var kvp in usage)
            {
                var su = kvp.Value;
                if (!su.LooksLikeManager) continue;

                int total = su.SceneComponentCount + su.PrefabComponentCount;
                if (total <= 3) continue; // threshold to reduce noise

                var examples = BuildExampleOccurrenceString(kvp.Key, sceneOcc, prefabOcc, maxExamples: 6);
                w.WriteLine(string.Join(",",
                    Csv("ManyInstancesManagerLike"),
                    Csv(su.FullTypeName),
                    Csv(su.AssemblyName),
                    Csv(su.ScriptAssetPath),
                    su.SceneComponentCount.ToString(),
                    su.PrefabComponentCount.ToString(),
                    Csv(examples)
                ));
            }
        }

        private static string BuildExampleOccurrenceString(
            string scriptKey,
            Dictionary<string, List<Occurrence>> sceneOcc,
            Dictionary<string, List<Occurrence>> prefabOcc,
            int maxExamples)
        {
            var ex = new List<string>();

            if (sceneOcc.TryGetValue(scriptKey, out var sList))
            {
                foreach (var o in sList.Take(maxExamples))
                    ex.Add($"[SCENE] {o.ContainerPath} :: {o.GameObjectPath}");
            }

            if (ex.Count < maxExamples && prefabOcc.TryGetValue(scriptKey, out var pList))
            {
                foreach (var o in pList.Take(maxExamples - ex.Count))
                    ex.Add($"[PREFAB] {o.ContainerPath} :: {o.GameObjectPath}");
            }

            return string.Join(" | ", ex);
        }

        private static string Csv(string s)
        {
            s ??= "";
            // Quote if it contains comma, quote, or newline
            bool mustQuote = s.Contains(",") || s.Contains("\"") || s.Contains("\n") || s.Contains("\r");
            if (!mustQuote) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Heuristics
        // ─────────────────────────────────────────────────────────────────────────────

        private static bool Heuristics_LooksLikeManager(Type t)
        {
            string n = t.Name ?? "";
            return n.Contains("Manager", StringComparison.OrdinalIgnoreCase) ||
                   n.Contains("Controller", StringComparison.OrdinalIgnoreCase) ||
                   n.EndsWith("System", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Heuristics_LooksLikeSingleton(Type t)
        {
            // Detect typical patterns:
            //  - public static <Type> Instance
            //  - static field of own type
            //  - Awake assigns Instance (we won’t parse IL; field presence is good enough)
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var fields = t.GetFields(flags);

            foreach (var f in fields)
            {
                if (!f.IsStatic) continue;
                if (f.FieldType == t)
                {
                    if (string.Equals(f.Name, "Instance", StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Also accept common variants
                    if (f.Name.IndexOf("instance", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            return false;
        }

        private static bool Heuristics_LooksLikeAudioBackbone(Type t)
        {
            // Heuristic: type name contains Audio/Music and/or has AudioSource fields.
            string n = t.Name ?? "";
            bool nameMatch =
                n.Contains("Audio", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Music", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Sound", StringComparison.OrdinalIgnoreCase);

            bool hasAudioSourceField =
                t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                 .Any(f => typeof(AudioSource).IsAssignableFrom(f.FieldType));

            return nameMatch || hasAudioSourceField;
        }
    }
}
