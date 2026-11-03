using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta de un clic que:
///   1. Reemplaza el MeshCollider costoso del terreno con un BoxCollider plano.
///   2. Añade un plano de suelo "infinito" visual (sin colisionador) para el efecto de horizonte.
///   3. Añade FogDistanceCuller al jugador para culling basado en niebla.
///   4. Extiende árboles decorativos fuera de los límites para profundidad de niebla.
///
/// Menú: Tools > Optimize Terrain Collider & Fog Culling
/// </summary>
public static class OptimizeTerrainColliderAndFog
{
    private const string ScenePath = "Assets/_Project/Scenes/TerrenoJuego.unity";

    [MenuItem("Tools/Optimize Terrain Collider & Fog Culling")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        int steps = 0;

        steps += OptimizeTerrainCollider(scene);
        steps += AddInfiniteGround(scene);
        steps += AddOuterAtmosphericTrees(scene);
        steps += AttachFogCuller(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[OptimizeTerrainColliderAndFog] Done ({steps} cambios). Scene {(saved ? "guardada" : "NO guardada")}.");
    }

    // ── 1. MeshCollider → BoxCollider ────────────────────────────────────────
    private static int OptimizeTerrainCollider(Scene scene)
    {
        GameObject terrno = GameObject.Find("Terrno");
        if (terrno == null)
        {
            Debug.LogWarning("[Optimize] No se encontró 'Terrno'.");
            return 0;
        }

        MeshCollider mc = terrno.GetComponent<MeshCollider>();
        if (mc == null)
        {
            Debug.Log("[Optimize] 'Terrno' ya no tiene MeshCollider.");
            return 0;
        }

        Renderer rend = terrno.GetComponent<Renderer>();
        Bounds bounds = rend != null ? rend.bounds : new Bounds(terrno.transform.position, Vector3.one);

        // Quitar MeshCollider
        Undo.DestroyObjectImmediate(mc);

        // Añadir BoxCollider plano (colisión con el suelo, casi gratis).
        // Debe cubrir el mesh real: el Plane de Unity mide 10x10 unidades, no 1x1.
        MeshFilter mf = terrno.GetComponent<MeshFilter>();
        Bounds mb = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
        float thick = 0.02f / Mathf.Abs(terrno.transform.lossyScale.y);
        BoxCollider box = Undo.AddComponent<BoxCollider>(terrno);
        box.size   = new Vector3(mb.size.x, thick, mb.size.z);
        box.center = new Vector3(mb.center.x, mb.max.y - thick * 0.5f, mb.center.z); // cara superior = superficie

        EditorUtility.SetDirty(terrno);
        Debug.Log($"[Optimize] MeshCollider reemplazado por BoxCollider en '{terrno.name}'. Bounds: {bounds}");
        return 1;
    }

    // ── 2. Plano de suelo visual "infinito" ──────────────────────────────────
    private static int AddInfiniteGround(Scene scene)
    {
        const string GndName = "InfiniteGround_Visual";

        // Eliminar si existe de una pasada anterior
        GameObject old = GameObject.Find(GndName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        // Obtener referencia al terreno para posicionarse en su superficie
        GameObject terrno = GameObject.Find("Terrno");
        float surfaceY = 3.53f;
        float cx = 120.16f, cz = -8.05f;

        if (terrno != null)
        {
            Renderer r = terrno.GetComponent<Renderer>();
            if (r != null)
            {
                surfaceY = r.bounds.max.y + 0.005f;
                cx = r.bounds.center.x;
                cz = r.bounds.center.z;
            }
        }

        // Plano GRANDE — la niebla esconde los bordes, da sensación de infinito.
        // Sin BoxCollider: el jugador no puede llegar aquí gracias a las paredes invisibles.
        GameObject gnd = GameObject.CreatePrimitive(PrimitiveType.Plane);
        gnd.name = GndName;
        Undo.RegisterCreatedObjectUndo(gnd, "Create InfiniteGround");

        // Eliminar el MeshCollider que Unity añade al Plane primitivo
        MeshCollider gndMC = gnd.GetComponent<MeshCollider>();
        if (gndMC != null) Undo.DestroyObjectImmediate(gndMC);

        gnd.transform.position   = new Vector3(cx, surfaceY - 0.01f, cz);
        gnd.transform.localScale = new Vector3(20f, 1f, 20f);  // 200x200 unidades

        // Material oscuro tipo suelo
        Material matGnd = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/_Project/Materials/Blockout/FRoad_Ground.mat");
        if (matGnd == null)
        {
            matGnd = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            matGnd.color = new Color(0.06f, 0.08f, 0.06f);
            matGnd.enableInstancing = true;
            System.IO.Directory.CreateDirectory("Assets/_Project/Materials/Blockout");
            AssetDatabase.CreateAsset(matGnd, "Assets/_Project/Materials/Blockout/FRoad_Ground.mat");
        }

        Renderer gndRend = gnd.GetComponent<Renderer>();
        if (gndRend != null) gndRend.sharedMaterial = matGnd;

        // Mover al mismo padre de Environment_FoggyRoad si existe
        GameObject envRoot = GameObject.Find("Environment_FoggyRoad");
        if (envRoot != null) gnd.transform.SetParent(envRoot.transform, true);

        EditorUtility.SetDirty(gnd);
        Debug.Log("[Optimize] Plano InfiniteGround_Visual 200×200 creado (sin colisionador).");
        return 1;
    }

    // ── 3. Árboles atmosféricos exteriores (profundidad visual en la niebla) ─
    private static int AddOuterAtmosphericTrees(Scene scene)
    {
        const string RingName = "AtmosphericTreeRing";
        GameObject old = GameObject.Find(RingName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        GameObject terrno = GameObject.Find("Terrno");
        float surfaceY = 3.53f;
        float cx = 120.16f, cz = -8.05f, hw = 13.76f, hd = 7.68f;
        if (terrno != null)
        {
            Renderer r = terrno.GetComponent<Renderer>();
            if (r != null)
            {
                surfaceY = r.bounds.max.y;
                cx = r.bounds.center.x;
                cz = r.bounds.center.z;
                hw = r.bounds.extents.x;
                hd = r.bounds.extents.z;
            }
        }

        Material matCanopy = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/_Project/Materials/Blockout/FRoad_Canopy.mat");
        Material matTrunk = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/_Project/Materials/Blockout/FRoad_Trunk.mat");

        if (matCanopy == null || matTrunk == null)
        {
            Debug.LogWarning("[Optimize] Materiales de árbol no encontrados, omitiendo anillo.");
            return 0;
        }

        GameObject ring = new GameObject(RingName);
        Undo.RegisterCreatedObjectUndo(ring, "Create AtmosphericRing");

        GameObject envRoot = GameObject.Find("Environment_FoggyRoad");
        if (envRoot != null) ring.transform.SetParent(envRoot.transform, false);

        System.Random rng = new System.Random(77);
        float outerRings = 3f;          // cuántos "círculos" de árboles hacia afuera
        float ringStep   = 3.5f;        // separación entre anillos

        for (int ring_ = 1; ring_ <= (int)outerRings; ring_++)
        {
            float xMin = cx - hw - ring_ * ringStep;
            float xMax = cx + hw + ring_ * ringStep;
            float zMin = cz - hd - ring_ * ringStep;
            float zMax = cz + hd + ring_ * ringStep;

            int count = 20 + ring_ * 8;
            for (int i = 0; i < count; i++)
            {
                // Colocar en el perímetro de este anillo rectangular
                float px, pz;
                int side = i % 4;
                if (side == 0)      { px = xMin + (float)rng.NextDouble() * (xMax - xMin); pz = zMin; }
                else if (side == 1) { px = xMin + (float)rng.NextDouble() * (xMax - xMin); pz = zMax; }
                else if (side == 2) { px = xMin; pz = zMin + (float)rng.NextDouble() * (zMax - zMin); }
                else                { px = xMax; pz = zMin + (float)rng.NextDouble() * (zMax - zMin); }

                // Más lejos → más alto (perspectiva de niebla densa)
                float h  = (4f + (float)rng.NextDouble() * 5f) * (1f + ring_ * 0.2f);
                float rad = 0.1f + (float)rng.NextDouble() * 0.12f;

                SpawnSimpleTree(ring, $"OutTree_{ring_}_{i}",
                    new Vector3(px, surfaceY, pz), h, rad, matCanopy, matTrunk);
            }
        }

        EditorUtility.SetDirty(ring);
        Debug.Log("[Optimize] Anillo de árboles atmosféricos creado (3 capas exteriores).");
        return 1;
    }

    private static void SpawnSimpleTree(GameObject parent, string name, Vector3 base3,
                                        float height, float radius, Material matC, Material matT)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent.transform, false);

        float trunkH = height * 0.25f;
        float trunkR = radius * 0.3f;

        // Tronco (cilindro)
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "T";
        trunk.transform.SetParent(root.transform, false);
        trunk.transform.position   = base3 + Vector3.up * trunkH * 0.5f;
        trunk.transform.localScale = new Vector3(trunkR * 2f, trunkH * 0.5f, trunkR * 2f);
        SetMatNoCollider(trunk, matT);

        // Copa (cubo escalado)
        GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        canopy.name = "C";
        canopy.transform.SetParent(root.transform, false);
        canopy.transform.position   = base3 + Vector3.up * (trunkH + height * 0.38f);
        canopy.transform.localScale = new Vector3(radius * 4f, height * 0.72f, radius * 4f);
        SetMatNoCollider(canopy, matC);
    }

    private static void SetMatNoCollider(GameObject go, Material mat)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
        Collider c = go.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
    }

    // ── 4. FogDistanceCuller en el jugador ───────────────────────────────────
    private static int AttachFogCuller(Scene scene)
    {
        LinuxSkeletonController ctrl = Object.FindFirstObjectByType<LinuxSkeletonController>();
        if (ctrl == null)
        {
            Debug.LogWarning("[Optimize] LinuxSkeletonController no encontrado — FogDistanceCuller no añadido.");
            return 0;
        }

        GameObject player = ctrl.gameObject;
        FogDistanceCuller existing = player.GetComponent<FogDistanceCuller>();
        if (existing == null)
            existing = Undo.AddComponent<FogDistanceCuller>(player);

        SerializedObject so = new SerializedObject(existing);
        so.FindProperty("visibilityMultiplier").floatValue = 0.85f;
        so.FindProperty("hysteresis").floatValue           = 1.5f;
        so.FindProperty("updateInterval").intValue         = 4;

        // Asignar environmentRoot
        GameObject envRoot = GameObject.Find("Environment_FoggyRoad");
        if (envRoot != null)
            so.FindProperty("environmentRoot").objectReferenceValue = envRoot.transform;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(player);

        Debug.Log($"[Optimize] FogDistanceCuller añadido a '{player.name}'.");
        return 1;
    }
}
