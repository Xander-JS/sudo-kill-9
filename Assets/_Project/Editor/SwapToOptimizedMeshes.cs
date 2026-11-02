using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Replaces every environment model in TerrenoJuego with its _opt.fbx version,
/// preserving position, rotation, scale, name, parent and static flags exactly.
///
/// Menu: Tools > Swap to Optimized FBX (TerrenoJuego)
/// </summary>
public class SwapToOptimizedMeshes : EditorWindow
{
    // Map  baseName -> path to optimized FBX inside Assets/
    static readonly Dictionary<string, string> OptPaths = new Dictionary<string, string>
    {
        { "cpu",                      "Assets/_Project/Models/Environment/cpu/cpu_opt.fbx" },
        { "gpu",                      "Assets/_Project/Models/Environment/gpu/gpu_opt.fbx" },
        { "chip",                     "Assets/_Project/Models/Environment/chip/chip_opt.fbx" },
        { "ram",                      "Assets/_Project/Models/Environment/ram/ram_opt.fbx" },
        { "resistencia",              "Assets/_Project/Models/Environment/resistencia/resistencia_opt.fbx" },
        { "condensador",              "Assets/_Project/Models/Environment/condensador/condensador_opt.fbx" },
        { "disco_almacenamiento",     "Assets/_Project/Models/Environment/disco_almacenamiento/disco_almacenamiento_opt.fbx" },
        { "targeta_red",              "Assets/_Project/Models/Environment/targeta_red/targeta_red_opt.fbx" },
        { "puertos_conexion",         "Assets/_Project/Models/Environment/puertos_conexion/puertos_conexion_opt.fbx" },
        { "ventilador_refrigeracion", "Assets/_Project/Models/Environment/ventilador_refrigeracion/ventilador_refrigeracion_opt.fbx" },
        { "batería_CMOS",             "Assets/_Project/Models/Environment/batería_CMOS/batería_CMOS_opt.fbx" },
    };

    [MenuItem("Tools/Swap to Optimized FBX (TerrenoJuego)")]
    static void Run()
    {
        // Open TerrenoJuego if not already open
        const string scenePath = "Assets/_Project/Scenes/TerrenoJuego.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        // Pre-load all optimized prefabs
        var prefabCache = new Dictionary<string, GameObject>();
        foreach (var kv in OptPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(kv.Value);
            if (prefab == null)
                Debug.LogWarning($"[SwapOptFBX] Optimized FBX not found: {kv.Value}");
            else
                prefabCache[kv.Key] = prefab;
        }

        // Collect all root GameObjects (snapshot, since we'll modify the hierarchy)
        var roots = new List<GameObject>();
        foreach (var go in scene.GetRootGameObjects())
            CollectAll(go.transform, roots);

        int swapped = 0;
        foreach (var go in roots)
        {
            if (go == null) continue;

            string baseName = StripIndex(go.name);
            if (!prefabCache.TryGetValue(baseName, out var optPrefab)) continue;

            // Save state
            var pos         = go.transform.position;
            var rot         = go.transform.rotation;
            var scale       = go.transform.localScale;
            var parent      = go.transform.parent;
            var goName      = go.name;
            var siblingIdx  = go.transform.GetSiblingIndex();
            var staticFlags = GameObjectUtility.GetStaticEditorFlags(go);
            var layer       = go.layer;
            var tag         = go.tag;

            // Destroy original
            Object.DestroyImmediate(go);

            // Instantiate optimized
            var newGo = (GameObject)PrefabUtility.InstantiatePrefab(optPrefab, scene);
            newGo.name = goName;
            newGo.transform.position   = pos;
            newGo.transform.rotation   = rot;
            newGo.transform.localScale = scale;
            if (parent != null)
                newGo.transform.SetParent(parent, worldPositionStays: true);
            newGo.transform.SetSiblingIndex(siblingIdx);
            GameObjectUtility.SetStaticEditorFlags(newGo, staticFlags);
            newGo.layer = layer;
            newGo.tag   = tag;

            Debug.Log($"[SwapOptFBX] Swapped: {goName}");
            swapped++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[SwapOptFBX] Done. {swapped} object(s) replaced in TerrenoJuego.");
        EditorUtility.DisplayDialog(
            "Swap Complete",
            $"{swapped} object(s) swapped to optimized FBX.\nScene saved.",
            "OK");
    }

    // Recursively collect every descendant into the list
    static void CollectAll(Transform t, List<GameObject> result)
    {
        result.Add(t.gameObject);
        foreach (Transform child in t)
            CollectAll(child, result);
    }

    // "resistencia (7)" -> "resistencia"
    static string StripIndex(string name) =>
        Regex.Replace(name, @"\s*\(\d+\)\s*$", "").Trim();
}
