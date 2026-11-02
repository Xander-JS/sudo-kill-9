using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// One-click, non-destructive optimization pass for the "Terreno Juego" scene: static flags, GPU instancing,
/// Terrain rendering settings, probe usage on small props and main camera far clip. Nothing is deleted or moved.
/// </summary>
public static class OptimizeTerrenoJuego
{
    private const string ScenePath = "Assets/_Project/Scenes/TerrenoJuego.unity";
    private const int HeightmapPixelError = 10;
    private const float BasemapDistance = 250f;
    private const float SmallPropMaxExtent = 2f;

    // BatchingStatic is deliberately left out: the environment models are ~1M-triangle meshes and static
    // batching would duplicate their vertex data per instance in memory. URP's SRP Batcher already batches them.
    private const StaticEditorFlags EnvironmentFlags =
        StaticEditorFlags.OccludeeStatic |
        StaticEditorFlags.OccluderStatic |
        StaticEditorFlags.NavigationStatic;

    [MenuItem("Tools/Optimize Terreno Juego Scene")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        int staticCount = MarkEnvironmentStatic(scene, out int skippedDynamic);
        int instancedCount = EnableMaterialInstancing(scene);
        int terrainCount = TuneTerrains(scene);
        int probesOffCount = DisableProbesOnSmallProps(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[OptimizeTerrenoJuego] Scene '" + scene.name + "' " + (saved ? "saved" : "NOT saved") + ".\n" +
            "- GameObjects marked Static: " + staticCount + " (dynamic objects left untouched: " + skippedDynamic + ")\n" +
            "- Materials with GPU Instancing enabled: " + instancedCount + "\n" +
            "- Terrains tuned: " + terrainCount + (terrainCount == 0 ? " (no Terrain component in scene)" : "") + "\n" +
            "- Small props with Light/Reflection probes off: " + probesOffCount);
    }

    private static int MarkEnvironmentStatic(Scene scene, out int skippedDynamic)
    {
        int count = 0;
        skippedDynamic = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform tf in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = tf.gameObject;
                bool hasGeometry = go.GetComponent<Renderer>() != null || go.GetComponent<Collider>() != null
                    || go.GetComponent<Terrain>() != null;
                if (!hasGeometry) continue;

                if (IsDynamic(tf))
                {
                    skippedDynamic++;
                    continue;
                }

                StaticEditorFlags current = GameObjectUtility.GetStaticEditorFlags(go);
                if ((current & EnvironmentFlags) == EnvironmentFlags) continue;

                GameObjectUtility.SetStaticEditorFlags(go, current | EnvironmentFlags);
                EditorUtility.SetDirty(go);
                count++;
            }
        }
        return count;
    }

    // True for anything that moves, animates, is simulated or belongs to a dynamic entity's hierarchy.
    private static bool IsDynamic(Transform tf)
    {
        for (Transform t = tf; t != null; t = t.parent)
        {
            GameObject go = t.gameObject;
            if (go.name.StartsWith("Player") || go.name.Contains("Enemy") || go.name.Contains("Spawn")) return true;
            if (go.GetComponent<Camera>() != null || go.GetComponent<Light>() != null) return true;
            if (go.GetComponent<Animator>() != null || go.GetComponent<Animation>() != null) return true;
            if (go.GetComponent<Rigidbody>() != null || go.GetComponent<CharacterController>() != null) return true;
            if (go.GetComponent<SkinnedMeshRenderer>() != null || go.GetComponent<ParticleSystem>() != null) return true;
            if (go.GetComponent<LinuxSkeletonController>() != null || go.GetComponent<WeaponIKHandler>() != null) return true;
            if (go.GetComponent<SpawnPointMarker>() != null || go.GetComponent<DirectionalSprite>() != null) return true;
        }
        return false;
    }

    private static int EnableMaterialInstancing(Scene scene)
    {
        var seen = new HashSet<Material>();
        int count = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat == null || !seen.Add(mat)) continue;
                    if (!AssetDatabase.GetAssetPath(mat).StartsWith("Assets/")) continue; // skip built-in / package materials
                    if (mat.enableInstancing) continue;

                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);
                    count++;
                }
            }
        }
        return count;
    }

    private static int TuneTerrains(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                terrain.heightmapPixelError = HeightmapPixelError;
                terrain.basemapDistance = BasemapDistance;
                terrain.drawInstanced = true;
                EditorUtility.SetDirty(terrain);
                count++;
            }
        }
        return count;
    }

    private static int DisableProbesOnSmallProps(Scene scene)
    {
        bool probesInUse = LightmapSettings.lightProbes != null && LightmapSettings.lightProbes.count > 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<LightProbeGroup>(true) != null
                || root.GetComponentInChildren<ReflectionProbe>(true) != null)
            {
                probesInUse = true;
                break;
            }
        }
        if (probesInUse) return 0;

        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (IsDynamic(renderer.transform)) continue;
                Vector3 size = renderer.bounds.size;
                if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) > SmallPropMaxExtent) continue;

                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                EditorUtility.SetDirty(renderer);
                count++;
            }
        }
        return count;
    }

}
