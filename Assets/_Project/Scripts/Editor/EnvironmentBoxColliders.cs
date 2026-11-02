using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds one BoxCollider per map-model instance in the open scene, fitted to the mesh's local bounds
/// (so position, rotation and scale of the instance are respected). Every collider added gets a
/// GeneratedBoxCollider marker so "Remove" can undo exactly this pass.
/// </summary>
public static class EnvironmentBoxColliders
{
    private const string ModelsRoot = "Assets/_Project/Models/Environment/";
    private static readonly string[] ExcludedFolders = { "Inicio_modelos/", "Diseño_suelo/" };

    [MenuItem("Tools/Environment Colliders/Add Box Colliders")]
    public static void Add()
    {
        Scene scene = SceneManager.GetActiveScene();
        int added = 0, skippedExisting = 0, skippedDynamic = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                GameObject go = filter.gameObject;
                if (filter.sharedMesh == null || go.GetComponent<MeshRenderer>() == null) continue;
                if (!IsMapModel(go)) continue;

                if (go.GetComponentInParent<Rigidbody>() != null) { skippedDynamic++; continue; }
                if (go.GetComponent<Collider>() != null) { skippedExisting++; continue; }

                Bounds b = filter.sharedMesh.bounds;
                var box = Undo.AddComponent<BoxCollider>(go);
                box.center = b.center;
                box.size = b.size;
                Undo.AddComponent<GeneratedBoxCollider>(go);
                added++;
            }
        }

        Finish(scene, "[EnvironmentBoxColliders] Added " + added + " BoxColliders (skipped: " + skippedExisting
            + " already had a collider, " + skippedDynamic + " dynamic).");
    }

    [MenuItem("Tools/Environment Colliders/Remove Generated Box Colliders")]
    public static void Remove()
    {
        Scene scene = SceneManager.GetActiveScene();
        int removed = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (GeneratedBoxCollider marker in root.GetComponentsInChildren<GeneratedBoxCollider>(true))
            {
                BoxCollider box = marker.GetComponent<BoxCollider>();
                if (box != null) Undo.DestroyObjectImmediate(box);
                Undo.DestroyObjectImmediate(marker);
                removed++;
            }
        }

        Finish(scene, "[EnvironmentBoxColliders] Removed " + removed + " generated BoxColliders.");
    }

    private static bool IsMapModel(GameObject go)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
        if (source == null) return false;

        string path = AssetDatabase.GetAssetPath(source);
        if (!path.StartsWith(ModelsRoot)) return false;

        foreach (string folder in ExcludedFolders)
            if (path.Contains(folder)) return false;
        return true;
    }

    private static void Finish(Scene scene, string message)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(message);
    }
}
