using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool that generates a graybox motherboard map layout using Unity primitives.
/// Run via: Tools > The Last Root > Generate Motherboard Layout
/// Or right-click the component in the Inspector > Generate Motherboard Layout.
/// </summary>
public class MotherboardGenerator : MonoBehaviour
{
    // ------------------------------------------------------------------ //
    //  Color palette (no external material assets required)               //
    // ------------------------------------------------------------------ //
    private static readonly Color ColorFloor      = new Color(0.30f, 0.30f, 0.30f); // medium gray
    private static readonly Color ColorChip       = new Color(0.20f, 0.20f, 0.20f); // dark gray
    private static readonly Color ColorTransistor = new Color(0.15f, 0.40f, 0.80f); // blue
    private static readonly Color ColorBadSector  = new Color(0.70f, 0.00f, 0.00f); // red

    // Scatter area half-extent: objects are placed randomly within [-Spread, +Spread] on X and Z
    private const float ScatterSpread = 40f;

    // Perlin noise scale: higher = larger clusters, lower = smaller, denser clusters
    private const float NoiseScale = 8f;

    // Noise threshold: positions with noise > this value are considered "dense" and get objects
    private const float NoiseDensityThreshold = 0.4f;

    // Fixed seed for reproducible layouts; change to get a different arrangement
    private const int RandomSeed = 42;

    // Collision detection: tracks bounds of all placed objects to prevent overlap
    private static System.Collections.Generic.List<Bounds> placedBounds = new System.Collections.Generic.List<Bounds>();

    // ================================================================== //
    //  ENTRY POINTS                                                       //
    // ================================================================== //

    [MenuItem("Tools/The Last Root/Generate Motherboard Layout")]
    public static void GenerateFromMenu()
    {
        Generate();
    }

    [ContextMenu("Generate Motherboard Layout")]
    public void GenerateFromContext()
    {
        Generate();
    }

    // ================================================================== //
    //  CORE GENERATOR                                                     //
    // ================================================================== //

    private static void Generate()
    {
        // Clear collision tracking from previous generation
        placedBounds.Clear();

        // Destroy previous blockout if it exists
        GameObject existing = GameObject.Find("Level_Blockout");
        if (existing != null)
        {
            DestroyImmediate(existing);
        }

        // Root container
        GameObject root = new GameObject("Level_Blockout");

        // Folder containers
        GameObject floorFolder     = CreateFolder("Floor",            root.transform);
        GameObject chipFolder      = CreateFolder("ChipArenas",       root.transform);
        GameObject coverFolder     = CreateFolder("TransistorCovers", root.transform);
        GameObject badSectorFolder = CreateFolder("BadSectors",       root.transform);

        // ---------------------------------------------------------------- //
        // INFINITE FLOOR — single large plane sitting at y = 0             //
        // ---------------------------------------------------------------- //
        SpawnFloor("Floor_Infinite", floorFolder.transform);

        // Seed RNG for reproducible random placement
        Random.InitState(RandomSeed);

        // ---------------------------------------------------------------- //
        // CHIP ARENAS — large wide cubes (combat platforms)                //
        // Sit on top of the floor: y offset = half their height = 0.1      //
        // ---------------------------------------------------------------- //
        for (int i = 0; i < 15; i++)
        {
            Vector3 chipScale = new Vector3(6f, 0.2f, 6f);
            SpawnChip($"ChipArena_{i:D2}", RandomXZ(0.1f, chipScale), chipScale, chipFolder.transform, "CHIP_3D");
        }

        // ---------------------------------------------------------------- //
        // TRANSISTOR / CAPACITOR COVERS — cubes and cylinders (cover)      //
        // Sit on top of the floor: y offset = half their height            //
        // ---------------------------------------------------------------- //
        for (int i = 0; i < 25; i++)
        {
            PrimitiveType type = (i % 2 == 0) ? PrimitiveType.Cube : PrimitiveType.Cylinder;
            Vector3 scale = (i % 2 == 0) ? new Vector3(0.8f, 0.8f, 0.8f) : new Vector3(0.4f, 1.0f, 0.4f);
            float yOff = (i % 2 == 0) ? 0.4f : 0.5f;
            SpawnCover($"TransistorCover_{i:D2}", RandomXZ(yOff, scale), type, scale, coverFolder.transform, "TRANSISTOR_3D");
        }

        // ---------------------------------------------------------------- //
        // BAD SECTORS — hazard zones (red, thin flattened cubes)           //
        // ---------------------------------------------------------------- //
        for (int i = 0; i < 12; i++)
        {
            Vector3 badScale = new Vector3(2f, 0.15f, 2f);
            SpawnBadSector($"BadSector_{i:D2}", RandomXZ(0.08f, badScale), badScale, badSectorFolder.transform);
        }

        // Register undo so the editor can undo the whole generation
        Undo.RegisterCreatedObjectUndo(root, "Generate Motherboard Layout");

        Selection.activeGameObject = root;
        Debug.Log("[MotherboardGenerator] Level_Blockout generated successfully.");
    }

    // ================================================================== //
    //  SPAWN HELPERS                                                      //
    // ================================================================== //

    // Returns a clustered random position on the floor using Perlin noise.
    // Samples positions until one falls in a "dense" cluster area AND has no collision with existing objects.
    private static Vector3 RandomXZ(float yOffset, Vector3 scale)
    {
        Vector3 pos = Vector3.zero;
        int attempts = 0;
        const int maxAttempts = 100; // More attempts to find collision-free spots

        while (attempts < maxAttempts)
        {
            float x = Random.Range(-ScatterSpread, ScatterSpread);
            float z = Random.Range(-ScatterSpread, ScatterSpread);

            // Sample Perlin noise at this position (scaled to create reasonable cluster sizes)
            float noiseValue = Mathf.PerlinNoise(x / NoiseScale, z / NoiseScale);

            // If noise is above threshold, check if position is collision-free
            if (noiseValue > NoiseDensityThreshold)
            {
                pos = new Vector3(x, yOffset, z);
                Bounds newBounds = new Bounds(pos, scale);

                // Check collision with all existing objects
                bool collision = false;
                foreach (Bounds b in placedBounds)
                {
                    if (newBounds.Intersects(b))
                    {
                        collision = true;
                        break;
                    }
                }

                // If no collision, we found a valid spot
                if (!collision)
                {
                    placedBounds.Add(newBounds);
                    break;
                }
            }

            attempts++;
        }

        // If no collision-free position found after max attempts, expand search and place anyway
        // (this preserves layout but may have overlaps in very dense scenes)
        if (attempts == maxAttempts)
        {
            float x = Random.Range(-ScatterSpread, ScatterSpread);
            float z = Random.Range(-ScatterSpread, ScatterSpread);
            pos = new Vector3(x, yOffset, z);
            placedBounds.Add(new Bounds(pos, scale));
        }

        return pos;
    }

    private static void SpawnFloor(string objName, Transform parent)
    {
        // A Plane primitive is 10×10 units; scale 100 on X and Z → 1000×1000 world units
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = objName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = new Vector3(100f, 1f, 100f);
        ApplyColor(go, ColorFloor);
        AddPlaceholder(go, EnvironmentPlaceholder.ComponentType.Floor, "FLOOR_INFINITE_3D");
    }

    private static void SpawnChip(string objName, Vector3 position, Vector3 scale, Transform parent, string modelID)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = objName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        ApplyColor(go, ColorChip);
        AddPlaceholder(go, EnvironmentPlaceholder.ComponentType.ChipArena, modelID);
    }

    private static void SpawnCover(string objName, Vector3 position, PrimitiveType shape, Vector3 scale, Transform parent, string modelID)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = objName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        ApplyColor(go, ColorTransistor);
        AddPlaceholder(go, EnvironmentPlaceholder.ComponentType.TransistorCover, modelID);
    }

    private static void SpawnBadSector(string objName, Vector3 position, Vector3 scale, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = objName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        ApplyColor(go, ColorBadSector);
        AddPlaceholder(go, EnvironmentPlaceholder.ComponentType.BadSector, objName + "_3D");
    }

    // ================================================================== //
    //  UTILITY                                                            //
    // ================================================================== //

    private static GameObject CreateFolder(string folderName, Transform parent)
    {
        GameObject folder = new GameObject(folderName);
        folder.transform.SetParent(parent, false);
        folder.transform.localPosition = Vector3.zero;
        return folder;
    }

    private static void ApplyColor(GameObject go, Color color)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        // Create a unique material instance so each type has its own color
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader.name == "Hidden/InternalErrorShader")
        {
            // Fallback to standard if URP is not available
            mat = new Material(Shader.Find("Standard"));
        }
        mat.color = color;
        renderer.sharedMaterial = mat;
    }

    private static void AddPlaceholder(GameObject go, EnvironmentPlaceholder.ComponentType type, string modelID)
    {
        EnvironmentPlaceholder ph = go.AddComponent<EnvironmentPlaceholder>();
        ph.componentType = type;
        ph.TargetModelID = modelID;
    }
}
