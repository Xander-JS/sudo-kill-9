using UnityEngine;

/// <summary>
/// Cámara de tercera persona orientada por la mira (PlayerAim).
/// El ratón mueve la mira; la cámara orbita alrededor de Linux siguiendo ese ángulo.
/// Adjunta al Main Camera.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("Pivote del jugador. Se busca automáticamente si está vacío.")]
    [SerializeField] private Transform target;

    [Header("Encuadre")]
    [Tooltip("Distancia desde el pivote hasta la cámara.")]
    [SerializeField] private float distance = 4f;
    [Tooltip("Altura sobre el pivote del jugador hacia donde mira la cámara.")]
    [SerializeField] private float heightOffset = 1.2f;

    [Header("Suavizado")]
    [SerializeField] private float smoothTime = 0.1f;

    private PlayerAim aim;
    private Vector3 velocity;

    private void Start()
    {
        if (target == null)
        {
            LinuxSkeletonController player = FindFirstObjectByType<LinuxSkeletonController>();
            if (player != null) target = player.transform;
        }

        aim = FindFirstObjectByType<PlayerAim>();

        if (target != null && aim != null)
            transform.position = DesiredPosition();

        ApplyRotation();
    }

    private void LateUpdate()
    {
        if (target == null || aim == null) return;

        transform.position = Vector3.SmoothDamp(
            transform.position, DesiredPosition(), ref velocity, smoothTime);
        ApplyRotation();
    }

    // Posición deseada: retrocede `distance` unidades desde el pivote en la dirección de la mira.
    private Vector3 DesiredPosition()
    {
        Vector3 pivot = target.position + Vector3.up * heightOffset;
        Quaternion rot = Quaternion.Euler(aim.Pitch, aim.Yaw, 0f);
        return pivot + rot * new Vector3(0f, 0f, -distance);
    }

    private void ApplyRotation()
    {
        if (aim == null) return;
        transform.rotation = Quaternion.Euler(aim.Pitch, aim.Yaw, 0f);
    }
}
