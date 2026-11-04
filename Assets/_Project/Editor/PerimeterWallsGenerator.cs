using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > SudoKill9 > Generate Perimeter Walls: gives the MapColiders boundary colliders in Mapa_completo
/// the military wall material from the Inicio "Cube" wall. Colliders are never touched.
/// </summary>
public static class PerimeterWallsGenerator
{
    // Already a standalone asset: the material on Inicio > Scene > ... > Cube.
    const string WallMatPath = "Assets/_Project/Materials/Mat_Warzone_BunkerWall.mat";
    const string MapScenePath = "Assets/_Project/Scenes/GamePlay/Mapa_completo.unity";
    const string CollidersRoot = "MapColiders";
    const string WallName = "PerimeterWall_Mesh";

    [MenuItem("Tools/SudoKill9/Generate Perimeter Walls")]
    static void GenerateWalls()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(WallMatPath);
        if (mat == null) { Debug.LogError($"[PerimeterWalls] Material not found: {WallMatPath}"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.OpenScene(MapScenePath);
        var root = GameObject.Find(CollidersRoot);
        if (root == null) { Debug.LogError($"[PerimeterWalls] '{CollidersRoot}' not found in {MapScenePath}"); return; }

        int painted = 0, created = 0;
        foreach (var col in root.GetComponentsInChildren<BoxCollider>(true))
        {
            var existing = col.GetComponent<MeshRenderer>();
            if (existing != null && col.GetComponent<MeshFilter>() != null)
            {
                existing.sharedMaterial = mat;
                painted++;
                continue;
            }

            var old = col.transform.Find(WallName);
            if (old != null) Object.DestroyImmediate(old.gameObject); // re-run safe

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cube.GetComponent<BoxCollider>()); // the original collider stays the only physics
            cube.name = WallName;
            // Child at the collider's center/size = exactly the collider's world box.
            cube.transform.SetParent(col.transform, false);
            cube.transform.localPosition = col.center;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = col.size;
            cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
            created++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[PerimeterWalls] Done: {created} wall meshes created, {painted} existing renderers repainted.");
    }
}
