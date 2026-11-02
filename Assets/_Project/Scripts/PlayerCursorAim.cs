using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mira que sigue al ratón y mantiene a Linux mirando hacia ella.
/// No modifica ninguna cámara: solo lanza un rayo desde la cámara de la pantalla principal
/// hasta el plano del suelo a la altura del jugador y gira el cuerpo (eje Y) hacia ese punto.
/// Adjunta al jugador (requiere Rigidbody).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerCursorAim : MonoBehaviour
{
    [Header("Cámara")]
    [Tooltip("Cámara usada para proyectar la mira. Si está vacía se busca la cámara del display indicado.")]
    [SerializeField] private Camera aimCamera;
    [Tooltip("Display cuya cámara se usa cuando aimCamera está vacío (0 = Display 1, 2 = Display 3).")]
    [SerializeField] private int displayIndex = 2;

    [Header("Rotación")]
    [SerializeField] private float bodyTurnSpeed = 18f;
    [Tooltip("Distancia mínima (m) entre Linux y la mira para girar; evita temblores al estar encima.")]
    [SerializeField] private float minAimDistance = 0.3f;

    [Header("Mira")]
    [SerializeField] private Color color = Color.white;
    [SerializeField] private int armLength = 12;
    [SerializeField] private int gap = 4;
    [SerializeField] private int thickness = 2;
    [SerializeField] private bool showDot = true;

    private Rigidbody rb;
    private Texture2D pixel;
    private float targetYaw;

    /// <summary>Punto del mundo (a la altura de Linux) hacia el que apunta la mira.</summary>
    public Vector3 AimPoint { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        targetYaw = transform.eulerAngles.y;
        AimPoint = transform.position + transform.forward;

        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Camera cam = ResolveCamera();
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        var ground = new Plane(Vector3.up, transform.position);
        if (!ground.Raycast(ray, out float hit)) return;

        AimPoint = ray.GetPoint(hit);

        Vector3 flat = AimPoint - transform.position;
        flat.y = 0f;
        if (flat.magnitude < minAimDistance) return;

        targetYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
    }

    private void FixedUpdate()
    {
        Quaternion targetRot = Quaternion.Euler(0f, targetYaw, 0f);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, bodyTurnSpeed * Time.fixedDeltaTime));
    }

    private Camera ResolveCamera()
    {
        if (aimCamera != null && aimCamera.isActiveAndEnabled) return aimCamera;

        foreach (Camera c in Camera.allCameras)
        {
            if (c.targetDisplay == displayIndex && c.isActiveAndEnabled)
            {
                aimCamera = c;
                return c;
            }
        }
        return Camera.main;
    }

    private void OnGUI()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 p = mouse.position.ReadValue();
        float cx = p.x;
        float cy = Screen.height - p.y;

        GUI.color = color;
        GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - gap - armLength, thickness, armLength), pixel);
        GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy + gap, thickness, armLength), pixel);
        GUI.DrawTexture(new Rect(cx - gap - armLength, cy - thickness * 0.5f, armLength, thickness), pixel);
        GUI.DrawTexture(new Rect(cx + gap, cy - thickness * 0.5f, armLength, thickness), pixel);
        if (showDot) GUI.DrawTexture(new Rect(cx - 1, cy - 1, 3, 3), pixel);
        GUI.color = Color.white;
    }

    private void OnDestroy()
    {
        if (pixel != null) Destroy(pixel);
    }
}
