using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Swaps the heavy prop models in the ACTIVE scene for their "_opt" versions.
///
/// - Keeps parent, sibling order, local position/rotation/scale, name, tag, layer,
///   static flags and active state.
/// - Copies extra root components (colliders, scripts...) the _opt model lacks.
/// - Old objects are NOT deleted: they are disabled and renamed with OldSuffix,
///   everything inside one Undo group (Ctrl+Z reverts the whole swap).
/// - Idempotent: objects already coming from an _opt model are skipped.
///
/// Menu: Tools > SudoKill9 > Optimization > Swap All Scene Props to _opt
///       Tools > SudoKill9 > Optimization > Delete Disabled Old Props (final cleanup)
/// </summary>
public static class SwapScenePropsToOpt
{
    const string MenuRoot  = "Tools/SudoKill9/Optimization/";
    const string ModelsDir = "Assets/_Project/Models/Environment/";
    const string OldSuffix = " [OLD_preOpt]";

    // Normalized base name (lowercase, no accents) -> optimized model path.
    static readonly Dictionary<string, string> OptPaths = new Dictionary<string, string>
    {
        { "cpu",                      ModelsDir + "cpu/cpu_opt.fbx" },
        { "chip",                     ModelsDir + "chip/chip_opt.fbx" },
        { "ventilador_refrigeracion", ModelsDir + "ventilador_refrigeracion/ventilador_refrigeracion_opt.fbx" },
        { "ram",                      ModelsDir + "ram/ram_opt.fbx" },
        { "targeta_red",              ModelsDir + "targeta_red/targeta_red_opt.fbx" },
        { "puertos_conexion",         ModelsDir + "puertos_conexion/puertos_conexion_opt.fbx" },
        { "disco_almacenamiento",     ModelsDir + "disco_almacenamiento/disco_almacenamiento_opt.fbx" },
        { "gpu",                      ModelsDir + "gpu/gpu_opt.fbx" },
        { "bateria_cmos",             ModelsDir + "batería_CMOS/batería_CMOS_opt.fbx" },
        { "condensador",              ModelsDir + "condensador/condensador_opt.fbx" },
        { "resistencia",              ModelsDir + "resistencia/resistencia_opt.fbx" },
    };

    [MenuItem(MenuRoot + "Swap All Scene Props to _opt")]
    static void SwapAll()
    {
        Scene scene = SceneManager.GetActiveScene();

        var prefabs = new Dictionary<string, GameObject>();
        foreach (var kv in OptPaths)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(kv.Value);
            if (p != null) prefabs[kv.Key] = p;
            else Debug.LogWarning($"[SwapOpt] Optimized model not found: {kv.Value}");
        }

        // Snapshot of every object (including inactive) before touching the hierarchy.
        var all = new List<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
            all.AddRange(root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject));

        var targets = all.Where(go => ResolveKey(go, prefabs) != null).ToList();
        // Containers like "Condensador" share a name with their children: only swap leaves.
        targets.RemoveAll(go => targets.Any(o => o != go && o.transform.IsChildOf(go.transform)));

        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("Swap to _opt",
                $"Nothing to swap in '{scene.name}': all props already use their _opt model.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Swap Scene Props to _opt");

        var log = new StringBuilder();
        foreach (var old in targets)
        {
            var newGo = Swap(old, prefabs[ResolveKey(old, prefabs)], scene);
            log.AppendLine($"{GetPath(old.transform)}  ->  {AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(newGo))}");
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[SwapOpt] {targets.Count} object(s) swapped in '{scene.name}':\n{log}");
        EditorUtility.DisplayDialog("Swap to _opt",
            $"{targets.Count} object(s) swapped.\n\nOld objects are disabled and renamed with '{OldSuffix}'.\n" +
            "Check the scene, then use 'Delete Disabled Old Props' or Ctrl+Z to revert.\nScene NOT saved.", "OK");
    }

    static GameObject Swap(GameObject old, GameObject optPrefab, Scene scene)
    {
        Transform ot = old.transform;
        var newGo = (GameObject)PrefabUtility.InstantiatePrefab(optPrefab, scene);
        Undo.RegisterCreatedObjectUndo(newGo, "Swap to _opt");

        Transform nt = newGo.transform;
        nt.SetParent(ot.parent, false);
        nt.SetSiblingIndex(ot.GetSiblingIndex()); // takes old slot; old moves one down
        // Same parent => copying local values preserves world pose exactly (no float drift).
        nt.localPosition = ot.localPosition;
        nt.localRotation = ot.localRotation;
        nt.localScale    = ot.localScale;

        newGo.name  = old.name;
        newGo.tag   = old.tag;
        newGo.layer = old.layer;
        GameObjectUtility.SetStaticEditorFlags(newGo, GameObjectUtility.GetStaticEditorFlags(old));

        CopyExtraComponents(old, newGo);

        bool wasActive = old.activeSelf;
        Undo.RecordObject(old, "Swap to _opt");
        old.name += OldSuffix;
        old.SetActive(false);
        newGo.SetActive(wasActive);
        return newGo;
    }

    // Copies root components that the old object has but the fresh _opt instance doesn't
    // (colliders, scripts, audio...). Mesh-related components come from the _opt model.
    static void CopyExtraComponents(GameObject from, GameObject to)
    {
        var newMesh = to.GetComponentInChildren<MeshFilter>(true)?.sharedMesh
                      ?? to.GetComponentInChildren<SkinnedMeshRenderer>(true)?.sharedMesh;

        foreach (var c in from.GetComponents<Component>())
        {
            if (c == null || c is Transform || c is MeshFilter || c is Renderer || c is Animator) continue;
            if (to.GetComponent(c.GetType()) != null) continue;

            UnityEditorInternal.ComponentUtility.CopyComponent(c);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(to);
            var pasted = to.GetComponents(c.GetType()).Last();
            Undo.RegisterCreatedObjectUndo(pasted, "Swap to _opt");

            // A MeshCollider pointing at the heavy mesh would defeat the optimization.
            if (pasted is MeshCollider mc && newMesh != null && mc.sharedMesh != null &&
                !AssetDatabase.GetAssetPath(mc.sharedMesh).Contains("_opt"))
                mc.sharedMesh = newMesh;
        }

        foreach (Transform child in from.transform)
            if (!PrefabUtility.IsPartOfPrefabInstance(child.gameObject) || PrefabUtility.IsAddedGameObjectOverride(child.gameObject))
                Debug.LogWarning($"[SwapOpt] '{GetPath(child)}' is a custom child of a swapped prop; it stays on the disabled old object. Move it manually if needed.", child);
    }

    // Returns the OptPaths key for a GameObject that should be swapped, or null.
    static string ResolveKey(GameObject go, Dictionary<string, GameObject> prefabs)
    {
        if (go.name.EndsWith(OldSuffix)) return null;

        bool isPrefab = PrefabUtility.IsPartOfPrefabInstance(go);
        if (isPrefab && !PrefabUtility.IsOutermostPrefabInstanceRoot(go)) return null;

        string srcPath = isPrefab
            ? AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(go))
            : null;
        if (!string.IsNullOrEmpty(srcPath) && srcPath.Contains("_opt")) return null; // already optimized

        // 1) Prefer the source model: ".../cpu.fbx" -> "cpu"
        if (!string.IsNullOrEmpty(srcPath))
        {
            string k = Normalize(System.IO.Path.GetFileNameWithoutExtension(srcPath));
            if (prefabs.ContainsKey(k)) return k;
        }

        // 2) Fall back to the object name ("resistencia (7)" -> "resistencia"),
        //    but only if it actually renders something (skips empty containers).
        string key = Normalize(Regex.Replace(go.name, @"\s*\(\d+\)\s*$", ""));
        if (!prefabs.ContainsKey(key)) return null;
        return go.GetComponentInChildren<Renderer>(true) != null ? key : null;
    }

    static string Normalize(string s)
    {
        var sb = new StringBuilder();
        foreach (char ch in s.Trim().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().ToLowerInvariant();
    }

    static string GetPath(Transform t) => t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;

    [MenuItem(MenuRoot + "Delete Disabled Old Props")]
    static void DeleteOld()
    {
        Scene scene = SceneManager.GetActiveScene();
        var olds = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject)
            .Where(g => g.name.EndsWith(OldSuffix) && !g.activeSelf)
            .ToList();

        if (olds.Count == 0) { EditorUtility.DisplayDialog("Cleanup", "No old props to delete.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Cleanup", $"Delete {olds.Count} disabled old prop(s)? (Undo available)", "Delete", "Cancel")) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        foreach (var g in olds) if (g != null) Undo.DestroyObjectImmediate(g);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[SwapOpt] Deleted {olds.Count} old prop(s) from '{scene.name}'.");
    }
}
