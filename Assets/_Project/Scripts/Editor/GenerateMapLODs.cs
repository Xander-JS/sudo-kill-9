using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

public static class GenerateMapLODs
{
    private const string ModelsRoot = "Assets/_Project/Models/Environment/";
    private const string MeshOut = "Assets/_Project/Models/Environment_LOD/";
    private const string PrefabOut = "Assets/_Project/Prefabs/Environment_LOD/";

    private static readonly float[] SmallHeights = { 0.06f, 0.025f, 0.008f };
    private static readonly float[] LargeHeights = { 0.20f, 0.08f, 0.02f };

    private struct Entry
    {
        public string Name;
        public float Lod0, Lod1, Lod2;
        public bool Small;

        public Entry(string name, float l0, float l1, float l2, bool small)
        {
            Name = name; Lod0 = l0; Lod1 = l1; Lod2 = l2; Small = small;
        }
    }

    private static readonly Entry[] Entries =
    {
        new Entry("resistencia",                0.08f, 0.02f, 0.005f, true),
        new Entry("condensador",                0.08f, 0.02f, 0.005f, true),
        new Entry("batería_CMOS",               0.15f, 0.05f, 0.015f, false),
        new Entry("disco_almacenamiento",       0.20f, 0.07f, 0.02f,  false),
        new Entry("chip",                       0.20f, 0.06f, 0.02f,  false),
        new Entry("targeta_red",                0.25f, 0.08f, 0.025f, false),
        new Entry("puertos_conexion",           0.30f, 0.10f, 0.03f,  false),
        new Entry("ram",                        0.30f, 0.10f, 0.03f,  false),
        new Entry("cpu",                        0.35f, 0.12f, 0.04f,  false),
        new Entry("gpu",                        0.35f, 0.12f, 0.04f,  false),
        new Entry("ventilador_refrigeracion",   0.35f, 0.12f, 0.04f,  false),
    };

    private static int _currentIndex;
    private static bool _running;

    [MenuItem("Tools/Map LODs/Generate ALL LOD Prefabs")]
    public static void GenerateAll()
    {
        if (_running) { Debug.LogWarning("[MapLODs] Already running."); return; }
        Directory.CreateDirectory(MeshOut);
        Directory.CreateDirectory(PrefabOut);
        _currentIndex = 0;
        _running = true;
        Debug.Log("[MapLODs] Starting generation for " + Entries.Length + " models...");
        EditorApplication.delayCall += ProcessNext;
    }

    [MenuItem("Tools/Map LODs/Cancel")]
    public static void Cancel()
    {
        _running = false;
        EditorUtility.ClearProgressBar();
        Debug.LogWarning("[MapLODs] Cancelled.");
    }

    private static void ProcessNext()
    {
        if (!_running || _currentIndex >= Entries.Length)
        {
            _running = false;
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (_currentIndex >= Entries.Length)
                Debug.Log("[MapLODs] Done! All " + Entries.Length + " models processed.");
            return;
        }

        var entry = Entries[_currentIndex];
        float progress = _currentIndex / (float)Entries.Length;

        if (EditorUtility.DisplayCancelableProgressBar(
                "Map LODs (" + (_currentIndex + 1) + "/" + Entries.Length + ")",
                entry.Name, progress))
        {
            Cancel();
            return;
        }

        try
        {
            Generate(entry);
        }
        catch (Exception e)
        {
            Debug.LogError("[MapLODs] " + entry.Name + " failed: " + e);
        }

        _currentIndex++;
        EditorApplication.delayCall += ProcessNext;
    }

    private static void Generate(Entry e)
    {
        string fbxPath = ModelsRoot + e.Name + "/" + e.Name + ".fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
        if (importer == null) { Debug.LogError("[MapLODs] FBX not found: " + fbxPath); return; }

        bool wasReadable = importer.isReadable;
        if (!wasReadable) { importer.isReadable = true; importer.SaveAndReimport(); }

        try
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            MeshFilter srcFilter = fbx.GetComponentInChildren<MeshFilter>();
            if (srcFilter == null) { Debug.LogError("[MapLODs] No MeshFilter in " + fbxPath); return; }
            Mesh original = srcFilter.sharedMesh;
            int originalTris = original.triangles.Length / 3;
            Debug.Log("[MapLODs] " + e.Name + ": " + originalTris + " tris, simplifying...");

            Mesh lod0 = Simplify(original, e.Lod0, e.Name + " LOD0");
            Mesh lod1 = Simplify(lod0, e.Lod1 / e.Lod0, e.Name + " LOD1");
            Mesh lod2 = Simplify(lod1, e.Lod2 / e.Lod1, e.Name + " LOD2");

            string dir = MeshOut + e.Name + "/";
            Directory.CreateDirectory(dir);
            Mesh[] meshes = { lod0, lod1, lod2 };
            for (int i = 0; i < meshes.Length; i++)
            {
                meshes[i].name = e.Name + "_LOD" + i;
                string assetPath = dir + meshes[i].name + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.CreateAsset(meshes[i], assetPath);
            }

            BuildPrefab(e, fbx, meshes);

            Debug.Log("[MapLODs] " + e.Name + " done: " + originalTris + " -> LOD0 " +
                (lod0.triangles.Length / 3) + ", LOD1 " + (lod1.triangles.Length / 3) +
                ", LOD2 " + (lod2.triangles.Length / 3));
        }
        finally
        {
            if (!wasReadable) { importer.isReadable = false; importer.SaveAndReimport(); }
        }
    }

    private static Mesh Simplify(Mesh source, float quality, string label)
    {
        Debug.Log("[MapLODs]   " + label + " (quality " + quality.ToString("F3") +
            ", " + (source.triangles.Length / 3) + " input tris)...");

        var simplifier = new MeshSimplifier();
        SimplificationOptions options = SimplificationOptions.Default;
        options.PreserveBorderEdges = false;
        options.PreserveUVSeamEdges = false;
        options.PreserveUVFoldoverEdges = false;
        options.EnableSmartLink = true;
        options.VertexLinkDistance = 1e-3;
        options.MaxIterationCount = 200;
        simplifier.SimplificationOptions = options;

        simplifier.Initialize(source);
        simplifier.SimplifyMesh(quality);

        Mesh result = simplifier.ToMesh();
        result.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        result.RecalculateBounds();
        return result;
    }

    private static void BuildPrefab(Entry e, GameObject fbx, Mesh[] meshes)
    {
        GameObject root = UnityEngine.Object.Instantiate(fbx);
        root.name = e.Name + "_LOD";

        MeshFilter filter0 = root.GetComponentInChildren<MeshFilter>();
        GameObject node0 = filter0.gameObject;
        Material[] materials = node0.GetComponent<MeshRenderer>().sharedMaterials;

        var renderers = new Renderer[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject node = i == 0 ? node0 : UnityEngine.Object.Instantiate(node0, node0.transform.parent);
            node.name = e.Name + "_LOD" + i;
            node.transform.localPosition = node0.transform.localPosition;
            node.transform.localRotation = node0.transform.localRotation;
            node.transform.localScale = node0.transform.localScale;
            node.GetComponent<MeshFilter>().sharedMesh = meshes[i];
            var mr = node.GetComponent<MeshRenderer>();
            mr.sharedMaterials = materials;
            renderers[i] = mr;
        }

        float[] heights = e.Small ? SmallHeights : LargeHeights;
        var lods = new LOD[3];
        for (int i = 0; i < 3; i++)
            lods[i] = new LOD(heights[i], new[] { renderers[i] });

        LODGroup group = root.AddComponent<LODGroup>();
        group.SetLODs(lods);
        group.RecalculateBounds();

        string prefabPath = PrefabOut + e.Name + "_LOD.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }
}
