using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mouse-driven aim pivot — el movimiento del ratón dicta hacia dónde apunta el jugador
/// y, por extensión, dónde orbita la cámara.
/// Adjunta este script al GameObject Player_LinuxSkeleton.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerAim : MonoBehaviour
{
    [Header("Sensibilidad")]
    [SerializeField] private float mouseSensitivity = 0.15f;

    [Header("Límites verticales")]
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 40f;

    [Header("Giro del cuerpo")]
    [Tooltip("Velocidad con la que el cuerpo sigue el yaw de la mira.")]
    [SerializeField] private float bodyTurnSpeed = 18f;

    /// <summary>Ángulo horizontal de la mira (eje Y).</summary>
    public float Yaw   { get; private set; }
    /// <summary>Ángulo vertical de la mira (eje X). Negativo = mirando arriba.</summary>
    public float Pitch { get; private set; }

    private Rigidbody rb;

    private void Awake()
    {
        rb  = GetComponent<Rigidbody>();
        Yaw = transform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue();
        Yaw   += delta.x * mouseSensitivity;
        Pitch  = Mathf.Clamp(Pitch - delta.y * mouseSensitivity, minPitch, maxPitch);
    }

    private void FixedUpdate()
    {
        // El cuerpo gira suavemente hacia donde apunta la mira (solo eje Y).
        Quaternion targetRot = Quaternion.Euler(0f, Yaw, 0f);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, bodyTurnSpeed * Time.fixedDeltaTime));
    }
}
