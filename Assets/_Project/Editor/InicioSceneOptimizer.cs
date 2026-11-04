using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace SudoKill9.Editor
{
    /// <summary>
    /// Inicio scene tools:
    /// 1. Replace heavy models with their "[Name]_opt" FBX (scale forced to 1,1,1).
    /// 2. Reorganize root hierarchy into [ENVIRONMENT] / [PROPS] / [LIGHTING] / [UI] / [CAMERAS].
    /// Everything goes through Undo (Ctrl+Z) and only marks the scene dirty; nothing is auto-saved.
    /// </summary>
    public static class InicioSceneOptimizer
    {
        private const string ScenePath = "Assets/_Project/Scenes/GamePlay/Inicio.unity";
        private const string ModelsRoot = "Assets/_Project/Models";
        private const string Tag = "[InicioOptimizer]";

        // Base names (without _opt) that belong in [PROPS]; any other mesh root goes to [ENVIRONMENT].
        private static readonly HashSet<string> PropNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            { "PC", "Mesa", "Armas", "Casillero", "Mapa" };
        private const string OldPropsContainer = "Objetos";

        // ───────────────────────────── 1. Replace models ─────────────────────────────

        [MenuItem("Tools/SudoKill9/Optimization/Replace Models in Inicio Scene (Scale 1-1-1)")]
        private static void ReplaceModels()
        {
            if (!TryGetInicio(out Scene scene)) return;

            // baseName -> optimized model asset, e.g. "Mesa" -> Mesa_opt.fbx
            var optModels = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("_opt t:Model", new[] { ModelsRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                if (!file.EndsWith("_opt", System.StringComparison.OrdinalIgnoreCase)) continue;
                optModels[file.Substring(0, file.Length - 4)] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            if (optModels.Count == 0) { Debug.LogWarning($"{Tag} No *_opt models found under {ModelsRoot}."); return; }

            // Collect top-most active objects whose model has an _opt version (snapshot before mutating).
            var targets = new List<(GameObject go, GameObject opt)>();
            foreach (GameObject root in scene.GetRootGameObjects())
                Collect(root.transform, optModels, targets);

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Replace Models", "No heavy models with an _opt version were found (already replaced?).", "OK");
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex("Replace Models",
                $"{targets.Count} object(s) will be replaced:\n\n" + string.Join("\n", targets.Select(t => $"• {t.go.name} → {t.opt.name}")) +
                "\n\nWhat should happen to the originals?",
                "Deactivate (safe)", "Cancel", "Delete");
            if (choice == 1) return;
            bool delete = choice == 2;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace Inicio models with _opt");

            foreach (var (go, optAsset) in targets)
            {
                Transform src = go.transform;
                var opt = (GameObject)PrefabUtility.InstantiatePrefab(optAsset, scene);
                Undo.RegisterCreatedObjectUndo(opt, "Create _opt model");

                opt.name = go.name + "_opt";
                opt.layer = go.layer;
                opt.tag = go.tag;
                GameObjectUtility.SetStaticEditorFlags(opt, GameObjectUtility.GetStaticEditorFlags(go));

                Transform t = opt.transform;
                t.SetParent(src.parent, false);
                t.SetPositionAndRotation(src.position, src.rotation);
                t.localScale = Vector3.one; // strict rule: always 1,1,1
                t.SetSiblingIndex(src.GetSiblingIndex() + 1);

                if (src.localScale != Vector3.one)
                    Debug.LogWarning($"{Tag} '{go.name}' had localScale {src.localScale.ToString("F4")} → forced to (1,1,1) on '{opt.name}'. Check it visually.", opt);

                if (delete)
                    Undo.DestroyObjectImmediate(go);
                else
                {
                    Undo.RecordObject(go, "Deactivate heavy model");
                    go.SetActive(false);
                }
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{Tag} Replaced {targets.Count} model(s) ({(delete ? "originals deleted" : "originals deactivated")}). Save the scene to keep changes, Ctrl+Z to revert.");
        }

        private static void Collect(Transform t, Dictionary<string, GameObject> optModels, List<(GameObject, GameObject)> result)
        {
            GameObject go = t.gameObject;
            // Inactive = already replaced on a previous run (or intentionally hidden): skip whole branch.
            if (!go.activeSelf) return;

            string key = ModelKey(go);
            if (key != null && optModels.TryGetValue(key, out GameObject opt))
            {
                result.Add((go, opt));
                return; // children belong to this model, don't match them separately
            }
            foreach (Transform child in t)
                Collect(child, optModels, result);
        }

        // Base model name of an object, or null if it is already optimized.
        private static string ModelKey(GameObject go)
        {
            string name;
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(go))
                name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go)));
            else if (go.GetComponentInChildren<MeshFilter>(true) || go.GetComponentInChildren<SkinnedMeshRenderer>(true))
                name = Regex.Replace(go.name, @"\s*\(\d+\)$", ""); // "Mesa (2)" -> "Mesa"
            else
                return null;

            return name.EndsWith("_opt", System.StringComparison.OrdinalIgnoreCase) ? null : name;
        }

        // ───────────────────────────── 2. Reorganize hierarchy ─────────────────────────────

        [MenuItem("Tools/SudoKill9/Optimization/Reorganize Inicio Hierarchy")]
        private static void ReorganizeHierarchy()
        {
            if (!TryGetInicio(out Scene scene)) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Reorganize Inicio hierarchy");

            var containers = new Dictionary<string, Transform>();
            Transform Container(string name)
            {
                if (containers.TryGetValue(name, out Transform c)) return c;
                GameObject go = scene.GetRootGameObjects().FirstOrDefault(r => r.name == name);
                if (go == null)
                {
                    go = new GameObject(name);
                    SceneManager.MoveGameObjectToScene(go, scene);
                    Undo.RegisterCreatedObjectUndo(go, "Create container");
                }
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.transform.localScale = Vector3.one;
                return containers[name] = go.transform;
            }

            // Fixed order on top of the hierarchy.
            string[] order = { "[ENVIRONMENT]", "[PROPS]", "[LIGHTING]", "[UI]", "[CAMERAS]" };
            foreach (string n in order) Container(n);

            int moved = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (order.Contains(root.name)) continue;

                if (root.name == OldPropsContainer)
                {
                    // Move the contents, keep the (now empty) container out of the way for manual deletion.
                    foreach (Transform child in root.transform.Cast<Transform>().ToList())
                        moved += Move(child, Container("[PROPS]"));
                    Debug.Log($"{Tag} '{OldPropsContainer}' emptied into [PROPS]; delete it manually once verified.", root);
                    continue;
                }

                string target = Classify(root);
                if (target == null)
                {
                    Debug.LogWarning($"{Tag} Left '{root.name}' at root: could not classify it.", root);
                    continue;
                }
                moved += Move(root.transform, Container(target));
            }

            for (int i = 0; i < order.Length; i++)
                containers[order[i]].SetSiblingIndex(i);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{Tag} Hierarchy reorganized: {moved} object(s) moved. Save the scene to keep changes, Ctrl+Z to revert.");
        }

        private static string Classify(GameObject root)
        {
            if (root.GetComponentInChildren<Canvas>(true) || root.GetComponent<EventSystem>()) return "[UI]";
            if (root.GetComponentInChildren<Camera>(true)) return "[CAMERAS]";

            bool hasRenderer = root.GetComponentInChildren<Renderer>(true);
            bool hasLight = root.GetComponentInChildren<Light>(true) || root.GetComponentInChildren<ReflectionProbe>(true)
                            || root.GetComponentInChildren<LightProbeGroup>(true);
            if (hasLight && !hasRenderer) return "[LIGHTING]";
            if (!hasRenderer) return null;

            string key = PrefabUtility.IsOutermostPrefabInstanceRoot(root)
                ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(root)))
                : root.name;
            key = Regex.Replace(key, @"(_opt)?(\s*\(\d+\))?$", "", RegexOptions.IgnoreCase);
            return PropNames.Contains(key) ? "[PROPS]" : "[ENVIRONMENT]";
        }

        private static int Move(Transform t, Transform parent)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(t) && !PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
            {
                Debug.LogWarning($"{Tag} Can't move '{t.name}': it's inside a prefab instance. Unpack it or move it in the prefab.", t);
                return 0;
            }
            Undo.SetTransformParent(t, parent, true, "Move to container"); // worldPositionStays: true
            return 1;
        }

        // ───────────────────────────── shared ─────────────────────────────

        private static bool TryGetInicio(out Scene scene)
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.isLoaded) return true;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return scene.isLoaded;
        }
    }
}
