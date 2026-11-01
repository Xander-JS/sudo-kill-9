using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Technical Artist & Senior Tool: Generates the "base_mapa" 2.5D Motherboard Graybox Scene.
/// Accessible via: Tools > SudoKill9 > Generate "base_mapa" Scene
/// </summary>
[InitializeOnLoad]
public static class BaseMapGenerator
{
    private const string ScenePath = "Assets/_Project/Scenes/base_mapa.unity";

    // Auto-generate scene on first domain reload if scene doesn't exist yet
    static BaseMapGenerator()
    {
        EditorApplication.delayCall += AutoGenerateIfMissing;
    }

    private static void AutoGenerateIfMissing()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.Log("[BaseMapGenerator] 'base_mapa.unity' not found. Auto-generating fresh base map...");
            GenerateBaseMapScene();
        }
    }

    [MenuItem("Tools/SudoKill9/Generate 'base_mapa' Scene", priority = 10)]
    public static void GenerateFromMenu()
    {
        if (EditorUtility.DisplayDialog(
            "Generar Escena base_mapa",
            "¿Deseas generar la escena limpia 'base_mapa' en Assets/_Project/Scenes/?\n" +
            "Se creará la jerarquía completa del blockout de placa base con perspectiva 2.5D.",
            "Generar", "Cancelar"))
        {
            GenerateBaseMapScene();
        }
    }

    [MenuItem("Tools/SudoKill9/Populate Current Scene with Motherboard Blockout", priority = 11)]
    public static void PopulateCurrentScene()
    {
        Undo.IncrementCurrentGroup();
        string groupName = "Populate Motherboard Blockout";
        Undo.SetCurrentGroupName(groupName);
        int undoGroup = Undo.GetCurrentGroup();

        BuildHierarchy();

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("[BaseMapGenerator] Motherboard blockout hierarchy built in the active scene.");
    }

    public static void GenerateBaseMapScene()
    {
        // Ensure folder exists
        string dir = Path.GetDirectoryName(ScenePath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        // Create fresh empty scene
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Build complete hierarchy
        BuildHierarchy();

        // Save scene asset
        bool saved = EditorSceneManager.SaveScene(newScene, ScenePath);
        if (saved)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=#2ecc71><b>[BaseMapGenerator]</b> Escena 'base_mapa' creada y guardada con éxito en: {ScenePath}</color>");
        }
        else
        {
            Debug.LogError($"[BaseMapGenerator] Error al guardar la escena en {ScenePath}");
        }
    }

    private static void BuildHierarchy()
    {
        // Shared materials cache to keep drawcalls and assets clean
        Material matFloor = CreateURPMaterial("Mat_Blockout_PCB_Floor", new Color(0.08f, 0.12f, 0.10f), 0.1f, 0.3f);
        Material matCPU = CreateURPMaterial("Mat_Blockout_CPU", new Color(0.70f, 0.72f, 0.75f), 0.8f, 0.6f);
        Material matChipset = CreateURPMaterial("Mat_Blockout_Chipset", new Color(0.28f, 0.38f, 0.48f), 0.4f, 0.5f);
        Material matRAM = CreateURPMaterial("Mat_Blockout_RAM", new Color(0.12f, 0.28f, 0.60f), 0.2f, 0.5f);
        Material matGPU = CreateURPMaterial("Mat_Blockout_GPU", new Color(0.15f, 0.18f, 0.22f), 0.3f, 0.4f);
        Material matStorage = CreateURPMaterial("Mat_Blockout_Storage", new Color(0.35f, 0.40f, 0.45f), 0.6f, 0.5f);
        Material matCapacitor = CreateURPMaterial("Mat_Blockout_Capacitor", new Color(0.04f, 0.52f, 0.89f), 0.5f, 0.6f);
        Material matChip = CreateURPMaterial("Mat_Blockout_Chip", new Color(0.12f, 0.14f, 0.16f), 0.1f, 0.2f);
        Material matResistor = CreateURPMaterial("Mat_Blockout_Resistor", new Color(0.85f, 0.48f, 0.25f), 0.1f, 0.3f);
        Material matCMOS = CreateURPMaterial("Mat_Blockout_CMOS", new Color(0.88f, 0.90f, 0.92f), 0.9f, 0.8f);
        Material matCooler = CreateURPMaterial("Mat_Blockout_Cooler", new Color(0.18f, 0.20f, 0.24f), 0.2f, 0.3f);
        Material matPort = CreateURPMaterial("Mat_Blockout_IOPort", new Color(0.65f, 0.70f, 0.75f), 0.8f, 0.7f);

        // -------------------------------------------------------------
        // 1. [SYSTEM_SETUP]
        // -------------------------------------------------------------
        GameObject systemSetup = CreateEmptyFolder("[SYSTEM_SETUP]", null);

        // Main Camera (2.5D Orthographic inclined)
        GameObject camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(systemSetup.transform, false);
        camGO.transform.position = new Vector3(0f, 25f, -25f);
        camGO.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

        Camera cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 18f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 500f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.08f, 0.11f, 1f);

        camGO.AddComponent<AudioListener>();

        // Attach CameraController if present
        var cameraController = camGO.AddComponent<CameraController>();
        if (cameraController != null)
        {
            // Default offset and rotation for 2.5D top-down
            SerializedObject so = new SerializedObject(cameraController);
            var offsetProp = so.FindProperty("offset");
            if (offsetProp != null) offsetProp.vector3Value = new Vector3(0f, 25f, -25f);
            var rotProp = so.FindProperty("fixedRotation");
            if (rotProp != null) rotProp.vector3Value = new Vector3(45f, 0f, 0f);
            so.ApplyModifiedProperties();
        }

        // Configure URP Camera Data if available
        var additionalCamData = camGO.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (additionalCamData == null)
            additionalCamData = camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (additionalCamData != null)
        {
            additionalCamData.renderPostProcessing = true;
        }

        // Directional Light (Angular for sharp shadows)
        GameObject lightGO = new GameObject("Directional Light");
        lightGO.transform.SetParent(systemSetup.transform, false);
        lightGO.transform.position = new Vector3(15f, 30f, -15f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        Light dirLight = lightGO.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(1f, 0.96f, 0.88f);
        dirLight.intensity = 1.25f;
        dirLight.shadows = LightShadows.Soft;

        var additionalLightData = lightGO.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
        if (additionalLightData == null)
            additionalLightData = lightGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();

        // -------------------------------------------------------------
        // 2. [ENVIRONMENT_BLOCKOUT]
        // -------------------------------------------------------------
        GameObject envBlockout = CreateEmptyFolder("[ENVIRONMENT_BLOCKOUT]", null);

        // --- Floor ---
        GameObject floorFolder = CreateEmptyFolder("Floor", envBlockout.transform);
        // Plane native size: 10x10. Scale 5x1x5 = 50x50 area
        GameObject groundObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
        groundObj.name = "Ground_Motherboard";
        groundObj.transform.SetParent(floorFolder.transform, false);
        groundObj.transform.localPosition = Vector3.zero;
        groundObj.transform.localScale = new Vector3(5f, 1f, 5f);
        ApplyMaterial(groundObj, matFloor);
        AddPlaceholder(groundObj, EnvironmentPlaceholder.ComponentType.Floor, "BOARD_PCB_50X50",
            "Suelo principal de placa base (50x50 unidades). Origen Y=0.");

        // --- Components_Main ---
        GameObject compMain = CreateEmptyFolder("Components_Main", envBlockout.transform);

        // CPUs
        GameObject cpusFolder = CreateEmptyFolder("CPUs", compMain.transform);
        // CPU_01: Main Processor (Scale: X=4, Y=0.3, Z=4) -> Center Y = 0.15
        SpawnCube("Placeholder_CPU_01", new Vector3(0f, 0.15f, 6f), new Vector3(4f, 0.3f, 4f),
            cpusFolder.transform, matCPU, EnvironmentPlaceholder.ComponentType.CPU, "CPU_SOCKET_AM5_01",
            "Socket CPU Principal AM5 con disipador IHS. Base en Y=0.");

        // CPU_02: Secondary Processor / Chipset Hub (Scale: X=3, Y=0.25, Z=3) -> Center Y = 0.125
        SpawnCube("Placeholder_CPU_02", new Vector3(0f, 0.125f, -5f), new Vector3(3f, 0.25f, 3f),
            cpusFolder.transform, matChipset, EnvironmentPlaceholder.ComponentType.CPU, "CHIPSET_PCH_02",
            "Chipset PCH secundario / Disipador intermedio. Base en Y=0.");

        // GPUs
        GameObject gpusFolder = CreateEmptyFolder("GPUs", compMain.transform);
        // GPU_01: PCIe Graphics Card (Scale: X=1.2, Y=2.5, Z=8) -> Center Y = 1.25
        SpawnCube("Placeholder_GPU_01", new Vector3(-8f, 1.25f, -4f), new Vector3(1.2f, 2.5f, 8f),
            gpusFolder.transform, matGPU, EnvironmentPlaceholder.ComponentType.GPU, "GPU_PCIE_DUALFAN_01",
            "Bloque divisorio GPU PCIe de gran cobertura visual. Base en Y=0.");

        // Storage
        GameObject storageFolder = CreateEmptyFolder("Storage", compMain.transform);
        // StorageDisk_01: NVMe/SSD Disk (Scale: X=3.5, Y=1, Z=5.5) -> Center Y = 0.5
        SpawnCube("Placeholder_StorageDisk_01", new Vector3(10f, 0.5f, -9f), new Vector3(3.5f, 1f, 5.5f),
            storageFolder.transform, matStorage, EnvironmentPlaceholder.ComponentType.StorageDisk, "SSD_NVME_ARMOR_01",
            "Unidad de almacenamiento M.2 NVMe con armadura de disipación. Base en Y=0.");

        // --- Components_Modular ---
        GameObject compModular = CreateEmptyFolder("Components_Modular", envBlockout.transform);

        // RAM_Banks
        GameObject ramFolder = CreateEmptyFolder("RAM_Banks", compModular.transform);
        // RAM sticks (Scale: X=0.2, Y=1.8, Z=5) -> Center Y = 0.9
        Vector3 ramScale = new Vector3(0.2f, 1.8f, 5f);
        SpawnCube("Placeholder_RAM_01", new Vector3(6.5f, 0.9f, 6f), ramScale, ramFolder.transform,
            matRAM, EnvironmentPlaceholder.ComponentType.RAM, "RAM_DDR5_STICK_01", "Módulo RAM slot 1.");
        SpawnCube("Placeholder_RAM_02", new Vector3(8.0f, 0.9f, 6f), ramScale, ramFolder.transform,
            matRAM, EnvironmentPlaceholder.ComponentType.RAM, "RAM_DDR5_STICK_02", "Módulo RAM slot 2.");
        SpawnCube("Placeholder_RAM_03", new Vector3(9.5f, 0.9f, 6f), ramScale, ramFolder.transform,
            matRAM, EnvironmentPlaceholder.ComponentType.RAM, "RAM_DDR5_STICK_03", "Módulo RAM slot 3.");

        // IO_Ports (Aligned along West perimeter edge at X = -23.5)
        GameObject ioFolder = CreateEmptyFolder("IO_Ports", compModular.transform);
        SpawnCube("Placeholder_Port_01", new Vector3(-23.5f, 0.4f, 14f), new Vector3(1.2f, 0.8f, 1.6f),
            ioFolder.transform, matPort, EnvironmentPlaceholder.ComponentType.IOPort, "PORT_USB_STACK_01", "Torre de puertos USB/Tipo C.");
        SpawnCube("Placeholder_Port_02", new Vector3(-23.5f, 0.5f, 10f), new Vector3(1.4f, 1.0f, 1.8f),
            ioFolder.transform, matPort, EnvironmentPlaceholder.ComponentType.IOPort, "PORT_ETHERNET_RJ45_02", "Puerto Ethernet LAN RJ45.");
        SpawnCube("Placeholder_Port_03", new Vector3(-23.5f, 0.4f, 6f), new Vector3(1.2f, 0.8f, 2.0f),
            ioFolder.transform, matPort, EnvironmentPlaceholder.ComponentType.IOPort, "PORT_AUDIO_DISPLAY_03", "Conectores de audio y DisplayPort.");

        // --- Components_Small ---
        GameObject compSmall = CreateEmptyFolder("Components_Small", envBlockout.transform);

        // Capacitors
        GameObject capFolder = CreateEmptyFolder("Capacitors", compSmall.transform);
        // Capacitor Cylinder (Scale: X=0.8, Y=1.2, Z=0.8). In Unity, Cylinder mesh height=2.
        // Height = 2 * 1.2 = 2.4 -> Center Y = 1.2f for bottom cap to touch Y=0.
        Vector3 capScale = new Vector3(0.8f, 1.2f, 0.8f);
        SpawnCylinder("Placeholder_Capacitor_01", new Vector3(-3.5f, 1.2f, 9.0f), capScale, Quaternion.identity,
            capFolder.transform, matCapacitor, EnvironmentPlaceholder.ComponentType.Capacitor, "CAP_SOLID_VRM_01", "Condensador VRM Fase Norte.");
        SpawnCylinder("Placeholder_Capacitor_02", new Vector3(-3.5f, 1.2f, 7.5f), capScale, Quaternion.identity,
            capFolder.transform, matCapacitor, EnvironmentPlaceholder.ComponentType.Capacitor, "CAP_SOLID_VRM_02", "Condensador VRM Fase Sur.");
        // Visual cluster reinforcements for authentic motherboard feel
        SpawnCylinder("Placeholder_Capacitor_03", new Vector3(-3.5f, 1.2f, 4.5f), capScale, Quaternion.identity,
            capFolder.transform, matCapacitor, EnvironmentPlaceholder.ComponentType.Capacitor, "CAP_SOLID_VRM_03", "Condensador VRM secundario.");
        SpawnCylinder("Placeholder_Capacitor_04", new Vector3(-3.5f, 1.2f, 3.0f), capScale, Quaternion.identity,
            capFolder.transform, matCapacitor, EnvironmentPlaceholder.ComponentType.Capacitor, "CAP_SOLID_VRM_04", "Condensador VRM secundario.");

        // Chips
        GameObject chipsFolder = CreateEmptyFolder("Chips", compSmall.transform);
        // Chip Cube (Scale: X=1.5, Y=0.15, Z=1.5) -> Center Y = 0.075
        Vector3 chipScale = new Vector3(1.5f, 0.15f, 1.5f);
        SpawnCube("Placeholder_Chip_01", new Vector3(-17f, 0.075f, 2f), chipScale,
            chipsFolder.transform, matChip, EnvironmentPlaceholder.ComponentType.Chip, "CHIP_AUDIO_DAC_01", "Chip códec de Audio / Realtek.");
        SpawnCube("Placeholder_Chip_02", new Vector3(4f, 0.075f, -12f), chipScale,
            chipsFolder.transform, matChip, EnvironmentPlaceholder.ComponentType.Chip, "CHIP_SUPER_IO_02", "Chip Super I/O / BIOS ROM.");

        // Resistors
        GameObject resistorsFolder = CreateEmptyFolder("Resistors", compSmall.transform);
        // Resistor Cylinder (Scale: X=0.3, Y=1.2, Z=0.3) rotated Z=90.
        // Rotated 90° on Z -> diameter on Y is scale.x (0.3), radius = 0.15 -> Center Y = 0.15
        Vector3 resScale = new Vector3(0.3f, 1.2f, 0.3f);
        Quaternion resRot = Quaternion.Euler(0f, 0f, 90f);
        SpawnCylinder("Placeholder_Resistor_01", new Vector3(2.5f, 0.15f, 1.5f), resScale, resRot,
            resistorsFolder.transform, matResistor, EnvironmentPlaceholder.ComponentType.Resistor, "RESISTOR_AXIAL_01", "Resistencia axial de paso.");
        SpawnCylinder("Placeholder_Resistor_02", new Vector3(2.5f, 0.15f, 0.0f), resScale, resRot,
            resistorsFolder.transform, matResistor, EnvironmentPlaceholder.ComponentType.Resistor, "RESISTOR_AXIAL_02", "Resistencia axial de paso.");

        // CMOS_Battery
        GameObject cmosFolder = CreateEmptyFolder("CMOS_Battery", compSmall.transform);
        // CMOS Cylinder (Scale: X=2, Y=0.1, Z=2). Height = 2 * 0.1 = 0.2 -> Center Y = 0.1
        SpawnCylinder("Placeholder_CMOS_01", new Vector3(1.5f, 0.1f, -1.0f), new Vector3(2f, 0.1f, 2f), Quaternion.identity,
            cmosFolder.transform, matCMOS, EnvironmentPlaceholder.ComponentType.CMOSBattery, "CMOS_BATTERY_CR2032_01", "Pila de botón CMOS CR2032.");

        // Coolers
        GameObject coolersFolder = CreateEmptyFolder("Coolers", compSmall.transform);
        // CoolerFan: Flat outer cylinder housing with inner core
        GameObject coolerFanRoot = new GameObject("Placeholder_CoolerFan_01");
        coolerFanRoot.transform.SetParent(coolersFolder.transform, false);
        coolerFanRoot.transform.localPosition = new Vector3(-14f, 0f, 12f);

        // Outer chassis shroud: Cylinder (Scale: X=4.5, Y=0.2, Z=4.5) -> height 0.4, center Y = 0.2
        GameObject outerShroud = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        outerShroud.name = "Fan_OuterShroud";
        outerShroud.transform.SetParent(coolerFanRoot.transform, false);
        outerShroud.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        outerShroud.transform.localScale = new Vector3(4.5f, 0.2f, 4.5f);
        ApplyMaterial(outerShroud, matCooler);

        // Inner rotor core: Cylinder (Scale: X=1.6, Y=0.25, Z=1.6) -> height 0.5, center Y = 0.25
        GameObject innerCore = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        innerCore.name = "Fan_InnerHub";
        innerCore.transform.SetParent(coolerFanRoot.transform, false);
        innerCore.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        innerCore.transform.localScale = new Vector3(1.6f, 0.25f, 1.6f);
        ApplyMaterial(innerCore, matChipset);

        AddPlaceholder(coolerFanRoot, EnvironmentPlaceholder.ComponentType.CoolerFan, "COOLER_FAN_120MM_01", "Ventilador extractor 120mm.");

        // --- Boundaries & Obstacles ---
        GameObject boundFolder = CreateEmptyFolder("Boundaries & Obstacles", envBlockout.transform);
        GameObject mapBorders = CreateEmptyFolder("Map_Borders", boundFolder.transform);
        AddPlaceholder(mapBorders, EnvironmentPlaceholder.ComponentType.Boundary, "ARENA_WALLS_50X50", "Muros colisionadores invisibles del perímetro.");

        // 4 Invisible border colliders for 50x50 arena (X from -25 to +25, Z from -25 to +25)
        CreateInvisibleBoundary("Border_North", mapBorders.transform, new Vector3(0f, 2.5f, 25.5f), new Vector3(52f, 5f, 1f));
        CreateInvisibleBoundary("Border_South", mapBorders.transform, new Vector3(0f, 2.5f, -25.5f), new Vector3(52f, 5f, 1f));
        CreateInvisibleBoundary("Border_East",  mapBorders.transform, new Vector3(25.5f, 2.5f, 0f), new Vector3(1f, 5f, 52f));
        CreateInvisibleBoundary("Border_West",  mapBorders.transform, new Vector3(-25.5f, 2.5f, 0f), new Vector3(1f, 5f, 52f));

        // -------------------------------------------------------------
        // 3. [PLAYER_SPAWN]
        // -------------------------------------------------------------
        GameObject playerSpawnFolder = CreateEmptyFolder("[PLAYER_SPAWN]", null);

        GameObject spawnPoint = new GameObject("SpawnPoint_Start");
        spawnPoint.transform.SetParent(playerSpawnFolder.transform, false);
        spawnPoint.transform.localPosition = new Vector3(0f, 0f, -14f);
        spawnPoint.transform.localRotation = Quaternion.identity; // Facing North towards CPU
        spawnPoint.AddComponent<SpawnPointMarker>();
    }

    // ================================================================== //
    //  CREATION HELPERS                                                   //
    // ================================================================== //

    private static GameObject CreateEmptyFolder(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go;
    }

    private static GameObject SpawnCube(string name, Vector3 pos, Vector3 scale, Transform parent,
        Material mat, EnvironmentPlaceholder.ComponentType compType, string modelId, string notes)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        ApplyMaterial(go, mat);
        AddPlaceholder(go, compType, modelId, notes);
        return go;
    }

    private static GameObject SpawnCylinder(string name, Vector3 pos, Vector3 scale, Quaternion rot, Transform parent,
        Material mat, EnvironmentPlaceholder.ComponentType compType, string modelId, string notes)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        ApplyMaterial(go, mat);
        AddPlaceholder(go, compType, modelId, notes);
        return go;
    }

    private static void CreateInvisibleBoundary(string name, Transform parent, Vector3 center, Vector3 size)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = center;
        BoxCollider box = wall.AddComponent<BoxCollider>();
        box.size = size;
    }

    private static void ApplyMaterial(GameObject go, Material mat)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r != null && mat != null)
        {
            r.sharedMaterial = mat;
        }
    }

    private static void AddPlaceholder(GameObject go, EnvironmentPlaceholder.ComponentType compType, string modelId, string notes)
    {
        EnvironmentPlaceholder ph = go.GetComponent<EnvironmentPlaceholder>();
        if (ph == null) ph = go.AddComponent<EnvironmentPlaceholder>();
        ph.componentType = compType;
        ph.targetModelID = modelId;
        ph.replacementNotes = notes;
    }

    private static Material CreateURPMaterial(string matName, Color albedo, float metallic, float smoothness)
    {
        string folder = "Assets/_Project/Materials/Blockout";
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string assetPath = $"{folder}/{matName}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null || shader.name == "Hidden/InternalErrorShader")
        {
            shader = Shader.Find("Standard");
        }

        Material mat = new Material(shader);
        mat.name = matName;
        mat.color = albedo;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", albedo);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

        AssetDatabase.CreateAsset(mat, assetPath);
        return mat;
    }
}
