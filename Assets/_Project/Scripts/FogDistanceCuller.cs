using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Activa/desactiva los objetos del entorno según la distancia de visibilidad
/// calculada desde la densidad de la niebla. Solo renderiza lo que el jugador
/// puede ver — nada más allá de la niebla se carga visualmente.
///
/// Adjuntar a cualquier GameObject persistente en la escena (p.ej. el jugador
/// o un Manager vacío). Funciona con ExponentialSquared y Linear fog modes.
/// </summary>
public class FogDistanceCuller : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("Transform del jugador. Se busca automáticamente si está vacío.")]
    [SerializeField] private Transform playerTransform;

    [Header("Culling")]
    [Tooltip("Multiplicador sobre la distancia calculada de niebla para el radio de activación.")]
    [SerializeField] private float visibilityMultiplier = 0.85f;

    [Tooltip("Margen extra antes de desactivar (evita parpadeo en el borde de niebla).")]
    [SerializeField] private float hysteresis = 1.5f;

    [Tooltip("Frames entre cada pasada de culling. 0 = cada frame (más caro).")]
    [SerializeField] private int updateInterval = 4;

    [Header("Objetos culleados")]
    [Tooltip("Raíz del entorno construido. Se detecta automáticamente si está vacío.")]
    [SerializeField] private Transform environmentRoot;

    // ── internals ──────────────────────────────────────────────────────────
    private readonly List<CulledEntry> entries = new List<CulledEntry>(256);
    private float enableRadius;
    private float disableRadius;
    private int frameOffset;

    private struct CulledEntry
    {
        public Transform tf;
        public Renderer[] renderers;
        public bool active;
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            LinuxSkeletonController ctrl = FindFirstObjectByType<LinuxSkeletonController>();
            if (ctrl != null) playerTransform = ctrl.transform;
        }

        if (environmentRoot == null)
        {
            GameObject envGo = GameObject.Find("Environment_FoggyRoad");
            if (envGo != null) environmentRoot = envGo.transform;
        }

        RecalculateFogRadius();
        CollectEntries();
        frameOffset = Random.Range(0, updateInterval + 1);
    }

    // Recalcula cuando cambian los settings de niebla en runtime.
    private void RecalculateFogRadius()
    {
        float fogDist = ComputeFogVisibilityDistance();
        enableRadius  = fogDist * visibilityMultiplier;
        disableRadius = enableRadius + hysteresis;
    }

    /// <summary>
    /// Distancia en la que la niebla cubre ~95 % del color del objeto.
    /// </summary>
    private static float ComputeFogVisibilityDistance()
    {
        if (!RenderSettings.fog) return 9999f;

        switch (RenderSettings.fogMode)
        {
            case FogMode.Linear:
                return RenderSettings.fogEndDistance;

            case FogMode.Exponential:
                // fog = 1 - e^(-d * density)  →  0.95 → d = ln(20) / density
                float d1 = RenderSettings.fogDensity;
                return d1 > 0f ? Mathf.Log(20f) / d1 : 9999f;

            default: // ExponentialSquared
                // fog = 1 - e^(-(d*density)^2) → 0.95 → d = sqrt(ln(20)) / density
                float d2 = RenderSettings.fogDensity;
                return d2 > 0f ? Mathf.Sqrt(Mathf.Log(20f)) / d2 : 9999f;
        }
    }

    private void CollectEntries()
    {
        entries.Clear();
        if (environmentRoot == null) return;

        foreach (Transform child in environmentRoot)
        {
            // Las paredes de límite no tienen Renderer — se dejan siempre activas.
            Renderer[] rends = child.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) continue;

            entries.Add(new CulledEntry
            {
                tf        = child,
                renderers = rends,
                active    = true
            });
        }
    }

    private void Update()
    {
        if (playerTransform == null || entries.Count == 0) return;

        // Distribuir trabajo en frames para no pagar todo de golpe.
        int interval = Mathf.Max(1, updateInterval);
        if ((Time.frameCount + frameOffset) % interval != 0) return;

        Vector3 playerPos = playerTransform.position;

        for (int i = 0; i < entries.Count; i++)
        {
            CulledEntry e = entries[i];
            if (e.tf == null) continue;

            float dist = Vector3.Distance(playerPos, e.tf.position);
            bool shouldBeActive = e.active ? dist <= disableRadius : dist <= enableRadius;

            if (shouldBeActive == e.active) continue;

            // Activar o desactivar solo los renderers, no el GameObject completo
            // (preserva colliders / scripts si los hubiera).
            foreach (Renderer r in e.renderers)
                r.enabled = shouldBeActive;

            e.active = shouldBeActive;
            entries[i] = e;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (playerTransform == null) return;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(playerTransform.position, enableRadius);
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.15f);
        Gizmos.DrawWireSphere(playerTransform.position, disableRadius);
    }
#endif
}
