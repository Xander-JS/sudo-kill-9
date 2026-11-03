using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the foggy road environment inside TerrenoJuego.unity based on the
/// dark-forest-road reference image.
/// Run via:  Tools > Build Foggy Road Environment
/// </summary>
public static class BuildFoggyRoadEnvironment
{
    // ──────────────────────────── Config ────────────────────────────────────
    private const string ScenePath   = "Assets/_Project/Scenes/TerrenoJuego.unity";
    private const string EnvRootName = "Environment_FoggyRoad";

    // Palette (linear colour space)
    private static readonly Color ColAsphalt      = new Color(0.09f, 0.09f, 0.10f);
    private static readonly Color ColYellowLine   = new Color(1.00f, 0.82f, 0.00f);
    private static readonly Color ColGuardrail    = new Color(0.52f, 0.55f, 0.58f);
    private static readonly Color ColTreeCanopy   = new Color(0.04f, 0.10f, 0.06f);
    private static readonly Color ColTreeTrunk    = new Color(0.18f, 0.12f, 0.08f);
    private static readonly Color ColCreature     = new Color(0.03f, 0.03f, 0.03f);
    private static readonly Color ColHill         = new Color(0.06f, 0.09f, 0.11f);
    private static readonly Color ColGround       = new Color(0.08f, 0.10f, 0.08f);
    private static readonly Color ColFern         = new Color(0.07f, 0.14f, 0.07f);
    private static readonly Color FogColor        = new Color(0.07f, 0.15f, 0.20f);

    // Fog
    private const float FogDensity  = 0.06f;
    private const float FogStart    = 0f;
    private const float FogEnd      = 35f;

    // ────────────────────────── Entry point ─────────────────────────────────
    [MenuItem("Tools/Build Foggy Road Environment")]
    public static void Build()
    {
        // Open scene
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // Find terrain
        Renderer terrainRend = FindTerrain(scene);
        if (terrainRend == null)
        {
            Debug.LogError("[BuildFoggyRoadEnvironment] Terrain renderer not found. Aborting.");
            return;
        }

        Bounds tb = terrainRend.bounds;
        float surfaceY = tb.max.y;
        Vector3 tc     = new Vector3(tb.center.x, surfaceY, tb.center.z);
        float hw       = tb.extents.x;   // half-width  (X)
        float hd       = tb.extents.z;   // half-depth  (Z)

        Debug.Log($"[BuildFoggyRoad] Terrain centre {tc}  half-size X={hw:F2} Z={hd:F2}");

        // Remove previous build if any
        ClearOldEnvironment(scene);

        // Create materials (all URP/Lit or standard)
        Material matRoad      = MakeMaterial("FRoad_Asphalt",    ColAsphalt,    0.03f, 0.6f);
        Material matLine      = MakeMaterial("FRoad_Line",       ColYellowLine, 0.05f, 0.8f);
        Material matRail      = MakeMaterial("FRoad_Guardrail",  ColGuardrail,  0.6f,  0.4f);
        Material matCanopy    = MakeMaterial("FRoad_Canopy",     ColTreeCanopy, 0.02f, 0.9f);
        Material matTrunk     = MakeMaterial("FRoad_Trunk",      ColTreeTrunk,  0.05f, 0.7f);
        Material matCreature  = MakeMaterial("FRoad_Creature",   ColCreature,   0.01f, 0.9f);
        Material matHill      = MakeMaterial("FRoad_Hill",       ColHill,       0.02f, 0.9f);
        Material matGround    = MakeMaterial("FRoad_Ground",     ColGround,     0.01f, 0.9f);
        Material matFern      = MakeMaterial("FRoad_Fern",       ColFern,       0.02f, 0.9f);

        // Root parent for the whole environment
        GameObject envRoot = new GameObject(EnvRootName);
        Undo.RegisterCreatedObjectUndo(envRoot, "Create Env Root");

        // ── Road surface ────────────────────────────────────────────────────
        float roadW = hw * 0.45f;   // road takes ~45% of terrain half-width each side → ~45% total
        float roadH = 0.03f;
        BuildRoad(envRoot, tc, roadW, hd, roadH, surfaceY, matRoad, matLine, matFern);

        // ── Guardrails ───────────────────────────────────────────────────────
        float railX = roadW + 0.35f;
        BuildGuardrailLine(envRoot, tc, hd,  railX, surfaceY, matRail);
        BuildGuardrailLine(envRoot, tc, hd, -railX, surfaceY, matRail);

        // ── Trees ────────────────────────────────────────────────────────────
        float treeZoneStart = roadW + 1.2f;
        BuildTreeZone(envRoot, tc, hd, hw, treeZoneStart, surfaceY, matCanopy, matTrunk, true);
        BuildTreeZone(envRoot, tc, hd, hw, treeZoneStart, surfaceY, matCanopy, matTrunk, false);
        BuildPerimeterTrees(envRoot, tc, hw, hd, surfaceY, matCanopy, matTrunk);

        // ── Mystery creature at far end ──────────────────────────────────────
        BuildCreature(envRoot, new Vector3(tc.x, surfaceY, tc.z + hd * 0.85f), matCreature);

        // ── Background hills (outside terrain, swallowed by fog) ─────────────
        BuildHills(envRoot, tc, hw, hd, surfaceY, matHill);

        // ── Ground patch (fills terrain surface with dark ground tone) ────────
        BuildGroundPatch(envRoot, tc, hw, hd, surfaceY - 0.01f, matGround);

        // ── Invisible boundary walls ─────────────────────────────────────────
        BuildBoundaryWalls(envRoot, tb, surfaceY);

        // ── Add/refresh TerrainBoundaryConstraint on Player linux ─────────────
        AttachConstraint(scene, terrainRend);

        // ── Fog + lighting ───────────────────────────────────────────────────
        ConfigureAtmosphere(scene, surfaceY);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[BuildFoggyRoadEnvironment] Done. Scene {(saved ? "saved" : "NOT saved")}.");
    }

    // ─────────────────────────── Terrain ────────────────────────────────────
    private static Renderer FindTerrain(Scene scene)
    {
        string[] names = { "Terrno", "Terreno Juego", "TerrainSurface", "Ground", "Terrain" };
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (string n in names)
            {
                Transform t = FindDeep(root.transform, n);
                if (t == null) continue;
                Renderer r = t.GetComponent<Renderer>();
                if (r != null) return r;
            }
        }
        // Fallback: largest ground-level renderer
        Renderer best = null;
        float bestArea = 0f;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Bounds b = mr.bounds;
                float area = b.size.x * b.size.z;
                if (area > bestArea && Mathf.Abs(b.center.y) < 15f)
                {
                    bestArea = area;
                    best = mr;
                }
            }
        }
        return best;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // ─────────────────────────── Clear ──────────────────────────────────────
    private static void ClearOldEnvironment(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == EnvRootName)
            {
                Undo.DestroyObjectImmediate(root);
                return;
            }
        }
    }

    // ─────────────────────────── Road ───────────────────────────────────────
    private static void BuildRoad(GameObject parent, Vector3 tc, float roadW, float hd, float roadH,
                                   float surfaceY, Material matRoad, Material matLine, Material matFern)
    {
        float roadLen = hd * 2f;
        float y = surfaceY + roadH * 0.5f;

        // Road slab
        CreateBox(parent, "Road_Surface", new Vector3(tc.x, y, tc.z),
                  new Vector3(roadW * 2f, roadH, roadLen), matRoad);

        // Yellow center lines (dashed)
        float lineW    = 0.18f;
        float dashLen  = 1.0f;
        float gapLen   = 0.6f;
        float lineXL   = tc.x - 0.22f;
        float lineXR   = tc.x + 0.22f;
        float lineY    = surfaceY + roadH + 0.005f;
        float zStep    = dashLen + gapLen;
        int   dashCount = Mathf.FloorToInt(roadLen / zStep);
        float zStart   = tc.z - hd + dashLen * 0.5f;
        for (int i = 0; i < dashCount; i++)
        {
            float zz = zStart + i * zStep;
            CreateBox(parent, $"Line_L_{i}", new Vector3(lineXL, lineY, zz), new Vector3(lineW, 0.01f, dashLen), matLine);
            CreateBox(parent, $"Line_R_{i}", new Vector3(lineXR, lineY, zz), new Vector3(lineW, 0.01f, dashLen), matLine);
        }

        // Center median strip (small dark fern-covered island)
        float medW = 0.4f;
        float medH = 0.04f;
        CreateBox(parent, "Road_Median", new Vector3(tc.x, surfaceY + medH * 0.5f, tc.z),
                  new Vector3(medW, medH, roadLen * 0.98f), matFern);

        // Fern tufts on median (small cubes)
        System.Random rng = new System.Random(42);
        int fernCount = Mathf.FloorToInt(roadLen / 0.8f);
        for (int i = 0; i < fernCount; i++)
        {
            float zz = tc.z - hd + (i + 0.5f) * (roadLen / fernCount);
            float xOff = (float)(rng.NextDouble() - 0.5) * medW * 0.6f;
            float fernH = 0.15f + (float)rng.NextDouble() * 0.15f;
            float fernW = 0.08f + (float)rng.NextDouble() * 0.08f;
            CreateBox(parent, $"Fern_M_{i}",
                      new Vector3(tc.x + xOff, surfaceY + medH + fernH * 0.5f, zz),
                      new Vector3(fernW, fernH, fernW * 1.5f), matFern);
        }
    }

    // ─────────────────────────── Guardrails ─────────────────────────────────
    private static void BuildGuardrailLine(GameObject parent, Vector3 tc, float hd,
                                            float xOffset, float surfaceY, Material mat)
    {
        float railLen     = hd * 2f;
        float postH       = 0.6f;
        float postW       = 0.06f;
        float railH       = 0.08f;
        float railThick   = 0.04f;
        float postSpacing = 1.5f;
        float xPos        = tc.x + xOffset;

        // Continuous top rail
        CreateBox(parent, $"Rail_Top_{(xOffset > 0 ? "R" : "L")}",
                  new Vector3(xPos, surfaceY + postH - railH * 0.5f, tc.z),
                  new Vector3(railThick, railH, railLen), mat);

        // Posts
        int postCount = Mathf.FloorToInt(railLen / postSpacing);
        float zStart  = tc.z - hd + postSpacing * 0.5f;
        for (int i = 0; i < postCount; i++)
        {
            float zz = zStart + i * postSpacing;
            CreateBox(parent, $"RailPost_{(xOffset > 0 ? "R" : "L")}_{i}",
                      new Vector3(xPos, surfaceY + postH * 0.5f, zz),
                      new Vector3(postW, postH, postW), mat);
        }
    }

    // ─────────────────────────── Trees ──────────────────────────────────────
    private static void BuildTreeZone(GameObject parent, Vector3 tc, float hd, float hw,
                                       float xStart, float surfaceY,
                                       Material matCanopy, Material matTrunk, bool rightSide)
    {
        float xMin = rightSide ? tc.x + xStart : tc.x - hw;
        float xMax = rightSide ? tc.x + hw      : tc.x - xStart;
        float zMin = tc.z - hd;
        float zMax = tc.z + hd;

        System.Random rng = new System.Random(rightSide ? 1 : 2);
        int count = Mathf.FloorToInt((xMax - xMin) * (zMax - zMin) * 0.22f);
        count = Mathf.Clamp(count, 6, 60);

        for (int i = 0; i < count; i++)
        {
            float x = xMin + (float)rng.NextDouble() * (xMax - xMin);
            float z = zMin + (float)rng.NextDouble() * (zMax - zMin);
            float tH = 3.5f + (float)rng.NextDouble() * 4f;
            float tR = 0.12f + (float)rng.NextDouble() * 0.08f;
            SpawnTree(parent, $"Tree_{(rightSide ? "R" : "L")}_{i}",
                      new Vector3(x, surfaceY, z), tH, tR, matCanopy, matTrunk);
        }
    }

    private static void BuildPerimeterTrees(GameObject parent, Vector3 tc, float hw, float hd,
                                             float surfaceY, Material matCanopy, Material matTrunk)
    {
        // Dense row along far Z edge (behind creature)
        System.Random rng = new System.Random(99);
        int count = Mathf.FloorToInt(hw * 2f / 1.4f) + 2;
        for (int i = 0; i < count; i++)
        {
            float x  = tc.x - hw + i * (hw * 2f / count) + (float)rng.NextDouble() * 0.8f;
            float zN = tc.z - hd + 0.5f + (float)rng.NextDouble() * 1.5f;
            float zF = tc.z + hd - 0.5f - (float)rng.NextDouble() * 1.5f;
            float tH = 4f + (float)rng.NextDouble() * 3f;
            float tR = 0.12f + (float)rng.NextDouble() * 0.08f;
            SpawnTree(parent, $"PerimN_{i}", new Vector3(x, surfaceY, zN), tH, tR, matCanopy, matTrunk);
            SpawnTree(parent, $"PerimF_{i}", new Vector3(x, surfaceY, zF), tH, tR, matCanopy, matTrunk);
        }
    }

    private static void SpawnTree(GameObject parent, string name, Vector3 base3,
                                   float height, float radius,
                                   Material matCanopy, Material matTrunk)
    {
        GameObject root = new GameObject(name);
        root.transform.parent = parent.transform;

        float trunkH   = height * 0.28f;
        float trunkR   = radius * 0.35f;

        // Trunk
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.parent = root.transform;
        trunk.transform.position = base3 + Vector3.up * trunkH * 0.5f;
        trunk.transform.localScale = new Vector3(trunkR * 2f, trunkH * 0.5f, trunkR * 2f);
        ApplyMat(trunk, matTrunk);

        // Three layered cones (approximated with scaled cubes flattened to diamond)
        float[] layerFracs = { 0.55f, 0.38f, 0.22f };
        float[] yFracs     = { 0.35f, 0.58f, 0.76f };
        for (int k = 0; k < 3; k++)
        {
            GameObject layer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            layer.name = $"Canopy_{k}";
            layer.transform.parent = root.transform;
            float r = radius * (1.6f - k * 0.35f) * layerFracs[k] * 3f;
            float h = height * 0.28f;
            layer.transform.position = base3 + Vector3.up * (trunkH + height * yFracs[k] * 0.55f);
            layer.transform.localScale = new Vector3(r, h, r);
            layer.transform.localRotation = Quaternion.Euler(0f, 45f * k, 0f);
            ApplyMat(layer, matCanopy);
        }
    }

    // ─────────────────────────── Mystery creature ────────────────────────────
    private static void BuildCreature(GameObject parent, Vector3 basePos, Material mat)
    {
        GameObject root = new GameObject("Creature_Silhouette");
        root.transform.parent = parent.transform;

        // Central pillar body
        CreateBox(root, "Body",   basePos + Vector3.up * 4.5f,  new Vector3(0.55f, 9f, 0.55f), mat);
        // Tentacle arms
        float[] armAngles = { 30f, 90f, 150f, 210f, 270f, 330f };
        float[] armLen    = { 2.5f, 1.8f, 2.2f, 1.5f, 2.8f, 1.6f };
        for (int i = 0; i < armAngles.Length; i++)
        {
            float rad   = armAngles[i] * Mathf.Deg2Rad;
            float xOff  = Mathf.Sin(rad) * 1.8f;
            float zOff  = Mathf.Cos(rad) * 1.8f;
            float yBase = 7f + i * 0.35f;
            GameObject arm = CreateBox(root, $"Arm_{i}",
                new Vector3(basePos.x + xOff, basePos.y + yBase, basePos.z + zOff),
                new Vector3(0.12f, armLen[i], 0.12f), mat);
            arm.transform.Rotate(Vector3.forward, armAngles[i] * 0.25f, Space.World);
        }
        // Head mass
        CreateBox(root, "Head", basePos + Vector3.up * 10f, new Vector3(1.2f, 0.8f, 1.2f), mat);
    }

    // ─────────────────────────── Background hills ────────────────────────────
    private static void BuildHills(GameObject parent, Vector3 tc, float hw, float hd,
                                    float surfaceY, Material mat)
    {
        // Hillside behind the far tree line
        float zFar = tc.z + hd + 1f;
        float zNear = tc.z - hd - 1f;
        int count = 7;
        for (int i = 0; i < count; i++)
        {
            float x  = tc.x - hw * 1.5f + i * (hw * 3f / (count - 1));
            float hH = 5f + Mathf.Sin(i * 1.3f) * 2f;
            float hW = 5f + Mathf.Cos(i * 0.9f) * 1.5f;
            CreateBox(parent, $"Hill_F_{i}", new Vector3(x, surfaceY + hH * 0.5f - 0.5f, zFar + hW * 0.3f),
                      new Vector3(hW, hH, hW * 1.2f), mat);
            CreateBox(parent, $"Hill_N_{i}", new Vector3(x, surfaceY + hH * 0.5f - 0.5f, zNear - hW * 0.3f),
                      new Vector3(hW, hH, hW * 1.2f), mat);
        }
        // Side walls (left/right)
        for (int i = 0; i < 5; i++)
        {
            float z  = tc.z - hd * 1.2f + i * (hd * 2.4f / 4f);
            float hH = 4f + i * 0.5f;
            CreateBox(parent, $"Hill_SL_{i}", new Vector3(tc.x - hw - 3f, surfaceY + hH * 0.5f - 0.2f, z),
                      new Vector3(4f, hH, 3.5f), mat);
            CreateBox(parent, $"Hill_SR_{i}", new Vector3(tc.x + hw + 3f, surfaceY + hH * 0.5f - 0.2f, z),
                      new Vector3(4f, hH, 3.5f), mat);
        }
    }

    // ─────────────────────────── Ground patch ───────────────────────────────
    private static void BuildGroundPatch(GameObject parent, Vector3 tc, float hw, float hd,
                                          float y, Material mat)
    {
        CreateBox(parent, "GroundFill",
                  new Vector3(tc.x, y, tc.z),
                  new Vector3(hw * 2f, 0.02f, hd * 2f), mat);
    }

    // ─────────────────────────── Boundary walls ──────────────────────────────
    private static void BuildBoundaryWalls(GameObject parent, Bounds tb, float surfaceY)
    {
        float wallH     = 12f;
        float wallThick = 0.3f;
        float cx = tb.center.x;
        float cz = tb.center.z;
        float ex = tb.extents.x;
        float ez = tb.extents.z;
        float yC = surfaceY + wallH * 0.5f;

        CreateWall(parent, "Wall_N", new Vector3(cx, yC, tb.max.z + wallThick * 0.5f),
                   new Vector3(ex * 2f + wallThick * 2f, wallH, wallThick));
        CreateWall(parent, "Wall_S", new Vector3(cx, yC, tb.min.z - wallThick * 0.5f),
                   new Vector3(ex * 2f + wallThick * 2f, wallH, wallThick));
        CreateWall(parent, "Wall_E", new Vector3(tb.max.x + wallThick * 0.5f, yC, cz),
                   new Vector3(wallThick, wallH, ez * 2f + wallThick * 2f));
        CreateWall(parent, "Wall_W", new Vector3(tb.min.x - wallThick * 0.5f, yC, cz),
                   new Vector3(wallThick, wallH, ez * 2f + wallThick * 2f));
    }

    private static void CreateWall(GameObject parent, string name, Vector3 pos, Vector3 size)
    {
        GameObject go = new GameObject(name);
        go.transform.parent = parent.transform;
        go.transform.position = pos;
        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.size = size;
        // Wall is invisible - no renderer
    }

    // ─────────────────────────── Constraint ─────────────────────────────────
    private static void AttachConstraint(Scene scene, Renderer terrainRend)
    {
        LinuxSkeletonController ctrl = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            ctrl = root.GetComponentInChildren<LinuxSkeletonController>(true);
            if (ctrl != null) break;
        }
        if (ctrl == null)
        {
            Debug.LogWarning("[BuildFoggyRoad] LinuxSkeletonController not found – TerrainBoundaryConstraint not added.");
            return;
        }

        GameObject player = ctrl.gameObject;
        TerrainBoundaryConstraint existing = player.GetComponent<TerrainBoundaryConstraint>();
        if (existing == null)
            existing = player.AddComponent<TerrainBoundaryConstraint>();

        // Wire terrain renderer via serialized field
        SerializedObject so = new SerializedObject(existing);
        so.FindProperty("terrainRenderer").objectReferenceValue = terrainRend;
        so.FindProperty("edgeInset").floatValue = 0.5f;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(player);
        Debug.Log($"[BuildFoggyRoad] TerrainBoundaryConstraint added/refreshed on '{player.name}'.");
    }

    // ─────────────────────────── Atmosphere ─────────────────────────────────
    private static void ConfigureAtmosphere(Scene scene, float surfaceY)
    {
        // Fog
        RenderSettings.fog           = true;
        RenderSettings.fogMode       = FogMode.ExponentialSquared;
        RenderSettings.fogDensity    = FogDensity;
        RenderSettings.fogColor      = FogColor;
        RenderSettings.fogStartDistance = FogStart;
        RenderSettings.fogEndDistance   = FogEnd;

        // Ambient light (very dark teal)
        RenderSettings.ambientMode  = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.04f, 0.09f, 0.12f);

        // Sky
        RenderSettings.skybox = null;   // solid background colour from camera

        // Directional light
        Light dirLight = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            dirLight = root.GetComponentInChildren<Light>(true);
            if (dirLight != null && dirLight.type == LightType.Directional) break;
        }
        if (dirLight != null)
        {
            dirLight.color     = new Color(0.30f, 0.42f, 0.55f);
            dirLight.intensity = 0.35f;
            dirLight.transform.rotation = Quaternion.Euler(38f, -30f, 0f);
            EditorUtility.SetDirty(dirLight);
        }

        // Camera background color
        Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera cam in cams)
        {
            cam.clearFlags        = CameraClearFlags.SolidColor;
            cam.backgroundColor   = FogColor;
            EditorUtility.SetDirty(cam);
        }

        // RenderSettings is a static class; marking scene dirty is enough.
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ─────────────────────────── Primitives ─────────────────────────────────
    private static GameObject CreateBox(GameObject parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.parent   = parent.transform;
        go.transform.position = pos;
        go.transform.localScale = size;
        ApplyMat(go, mat);
        DestroyColliderIfTrivial(go);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    private static void DestroyColliderIfTrivial(GameObject go)
    {
        // Only remove box collider from visual-only pieces (not the terrain/walls)
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    private static void ApplyMat(GameObject go, Material mat)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
    }

    // ─────────────────────────── Material factory ────────────────────────────
    private static Material MakeMaterial(string name, Color color, float metallic, float roughness)
    {
        string path = $"Assets/_Project/Materials/Blockout/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.color = color;
            existing.SetFloat("_Metallic", metallic);
            existing.SetFloat("_Smoothness", 1f - roughness);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Material mat = new Material(GetLitShader());
        mat.name = name;
        mat.color = color;
        if (mat.HasProperty("_Metallic"))    mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness"))  mat.SetFloat("_Smoothness", 1f - roughness);
        if (mat.HasProperty("_Glossiness"))  mat.SetFloat("_Glossiness", 1f - roughness);
        mat.enableInstancing = true;

        System.IO.Directory.CreateDirectory("Assets/_Project/Materials/Blockout");
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Shader GetLitShader()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        return s;
    }
}
