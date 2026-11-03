using UnityEngine;

/// <summary>
/// Keeps Player linux inside Terreno Juego's physical bounds at all times.
/// Attach to the Player linux root. Works with the Rigidbody-based LinuxSkeletonController.
/// Reads terrain bounds at runtime so no hardcoded coordinates are needed.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TerrainBoundaryConstraint : MonoBehaviour
{
    [Tooltip("Renderer of the terrain surface. Auto-found if left empty.")]
    [SerializeField] private Renderer terrainRenderer;

    [Tooltip("How far inside the terrain edge the player is clamped (metres).")]
    [SerializeField] private float edgeInset = 0.5f;

    private Rigidbody rb;
    private bool boundsReady;
    private float minX, maxX, minZ, maxZ;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        ResolveTerrain();
    }

    private void ResolveTerrain()
    {
        if (terrainRenderer == null)
            terrainRenderer = FindTerrainRenderer();

        if (terrainRenderer == null)
        {
            Debug.LogWarning("[TerrainBoundaryConstraint] Terrain renderer not found – boundary inactive.");
            return;
        }

        Bounds b = terrainRenderer.bounds;
        minX = b.min.x + edgeInset;
        maxX = b.max.x - edgeInset;
        minZ = b.min.z + edgeInset;
        maxZ = b.max.z - edgeInset;
        boundsReady = true;

        Debug.Log($"[TerrainBoundaryConstraint] Bounds set – X:[{minX:F2},{maxX:F2}] Z:[{minZ:F2},{maxZ:F2}]");
    }

    private static Renderer FindTerrainRenderer()
    {
        // Try exact names first.
        string[] candidateNames = { "Terrno", "Terreno Juego", "TerrainSurface", "Terrain", "Ground" };
        foreach (string n in candidateNames)
        {
            GameObject go = GameObject.Find(n);
            if (go == null) continue;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) return r;
            r = go.GetComponentInChildren<Renderer>();
            if (r != null) return r;
        }

        // Fall back to the largest MeshRenderer near Y=0.
        Renderer best = null;
        float bestArea = 0f;
        foreach (MeshRenderer mr in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (mr.GetComponentInParent<LinuxSkeletonController>() != null) continue;
            Bounds b = mr.bounds;
            float area = b.size.x * b.size.z;
            if (area > bestArea && Mathf.Abs(b.center.y) < 10f)
            {
                bestArea = area;
                best = mr;
            }
        }
        return best;
    }

    private void FixedUpdate()
    {
        if (!boundsReady) return;

        Vector3 pos = rb.position;
        float cx = Mathf.Clamp(pos.x, minX, maxX);
        float cz = Mathf.Clamp(pos.z, minZ, maxZ);

        if (cx == pos.x && cz == pos.z) return;

        Vector3 vel = rb.linearVelocity;
        if (cx != pos.x) vel.x = 0f;
        if (cz != pos.z) vel.z = 0f;

        rb.linearVelocity = vel;
        rb.position = new Vector3(cx, pos.y, cz);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!boundsReady) return;
        float cy = terrainRenderer != null ? terrainRenderer.bounds.center.y : transform.position.y;
        Vector3 center = new Vector3((minX + maxX) * 0.5f, cy, (minZ + maxZ) * 0.5f);
        Vector3 size   = new Vector3(maxX - minX, 0.5f, maxZ - minZ);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
