using UnityEngine;

/// <summary>
/// Cámara con proyección oblicua que sigue al personaje Linux y orbita
/// sincronizada con la mira (PlayerAim.Yaw).
/// Adjunta a "Camera 3".
///
/// Cuando el jugador apunta hacia atrás la cámara gira automáticamente
/// para mostrar lo que hay en esa dirección.
/// </summary>
public class IsometricCameraController : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("Transform del jugador. Se busca automáticamente si está vacío.")]
    [SerializeField] private Transform target;

    [Header("Sincronización con la mira")]
    [Tooltip("Sincroniza el Yaw de la cámara con PlayerAim. Si está desactivado usa el Yaw fijo de abajo.")]
    [SerializeField] private bool syncWithAim = true;
    [Tooltip("Suavizado del Yaw al seguir la mira (segundos). 0 = instantáneo.")]
    [SerializeField] private float yawSmoothTime = 0.15f;
    [Tooltip("Desfase angular respecto a la mira (grados). 0 = cámara detrás del jugador.")]
    [SerializeField] private float yawOffset = 0f;

    [Header("Posición de la cámara")]
    [Tooltip("Yaw fijo usado solo cuando syncWithAim = false.")]
    [SerializeField] private float yaw = 45f;
    [Tooltip("Pitch de la cámara en grados. 90° = cenital puro; 60-75° = oblique típico.")]
    [SerializeField] private float pitch = 75f;
    [Tooltip("Distancia desde el jugador hasta la cámara.")]
    [SerializeField] private float distance = 16f;
    [Tooltip("Altura extra sobre el pivote del jugador.")]
    [SerializeField] private float heightOffset = 1f;

    [Header("Proyección oblicua")]
    [Tooltip("Activa el shear oblicuo en la matriz de proyección.")]
    [SerializeField] private bool obliqueProjection = true;
    [Tooltip("Intensidad del shear vertical (negativo = hacia delante). Rango típico: -0.3 a -0.7.")]
    [Range(-1f, 0f)]
    [SerializeField] private float obliqueY = -0.5f;
    [Tooltip("Intensidad del shear horizontal. 0 = sin desplazamiento lateral.")]
    [Range(-0.5f, 0.5f)]
    [SerializeField] private float obliqueX = 0f;

    [Header("Proyección base")]
    [Tooltip("True = orthographic; False = perspective.")]
    [SerializeField] private bool useOrthographic = true;
    [Tooltip("Tamaño ortográfico. Solo si useOrthographic = true.")]
    [SerializeField] private float orthographicSize = 8f;

    [Header("Suavizado de posición")]
    [SerializeField] private float smoothTime = 0.12f;

    private Camera cam;
    private Vector3 posVelocity;
    private float currentYaw;
    private float yawVelocity;
    private PlayerAim aim;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (target == null)
        {
            LinuxSkeletonController player = FindFirstObjectByType<LinuxSkeletonController>();
            if (player != null)
                target = player.transform;
            else
                Debug.LogWarning("[IsometricCamera] No se encontró LinuxSkeletonController. Asigna 'target' manualmente.");
        }

        aim = FindFirstObjectByType<PlayerAim>();
        if (syncWithAim && aim == null)
            Debug.LogWarning("[IsometricCamera] syncWithAim = true pero no se encontró PlayerAim en la escena.");

        // Semilla del yaw para evitar snap en el primer frame.
        currentYaw = syncWithAim && aim != null ? aim.Yaw + yawOffset : yaw;

        ApplyProjection();

        if (target != null)
            transform.position = DesiredPosition();

        ApplyRotation();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Actualizar yaw: seguir la mira o usar el valor fijo.
        float targetYaw = syncWithAim && aim != null ? aim.Yaw + yawOffset : yaw;
        currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, yawSmoothTime);

        transform.position = Vector3.SmoothDamp(
            transform.position, DesiredPosition(), ref posVelocity, smoothTime);

        ApplyRotation();
        ApplyObliqueMatrix();
    }

    private Vector3 DesiredPosition()
    {
        Vector3 pivot = target.position + Vector3.up * heightOffset;
        Quaternion rot = Quaternion.Euler(pitch, currentYaw, 0f);
        return pivot + rot * new Vector3(0f, 0f, -distance);
    }

    private void ApplyRotation()
    {
        transform.rotation = Quaternion.Euler(pitch, currentYaw, 0f);
    }

    private void ApplyProjection()
    {
        if (cam == null) return;
        cam.orthographic = useOrthographic;
        if (useOrthographic)
            cam.orthographicSize = orthographicSize;
        cam.ResetProjectionMatrix();
    }

    // Se llama cada frame porque Unity puede resetear la matriz en ciertos eventos.
    private void ApplyObliqueMatrix()
    {
        if (cam == null || !obliqueProjection) return;

        Matrix4x4 mat = cam.projectionMatrix;
        mat[0, 2] = obliqueX;
        mat[1, 2] = obliqueY;
        cam.projectionMatrix = mat;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (cam == null) cam = GetComponent<Camera>();
        ApplyProjection();
        if (target != null)
        {
            currentYaw = yaw;
            transform.position = DesiredPosition();
            ApplyRotation();
        }
        if (obliqueProjection && cam != null)
        {
            Matrix4x4 mat = cam.projectionMatrix;
            mat[0, 2] = obliqueX;
            mat[1, 2] = obliqueY;
            cam.projectionMatrix = mat;
        }
    }
#endif
}
