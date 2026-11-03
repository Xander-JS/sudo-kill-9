using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Cámara Over-The-Shoulder estilo GTA / Tomb Raider.
/// Arquitectura de 3 niveles:
///   PlayerRoot (este GameObject + CharacterController)
///   └── CameraPivot  — solo controla Pitch (Mouse Y)
///       └── MainCamera — offset fijo sobre el hombro, nunca rota sola
///
/// Auto-configura CameraPivot y MainCamera si no existen.
/// Añade este componente al PlayerRoot y ajusta los sliders en el Inspector.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonCameraGTA : MonoBehaviour
{
    // ── Posición y forma de la cámara ──────────────────────────────────
    [Header("Cámara — Posición")]
    [Tooltip("Altura del pivote sobre los pies del personaje.")]
    [Range(0.5f, 3.0f)] public float alturaPivote = 1.35f;

    [Tooltip("Distancia de la cámara detrás del personaje.")]
    [Range(0.5f, 8.0f)] public float distanciaCamara = 2.4f;

    [Tooltip("Desplazamiento lateral (+ derecha, - izquierda).")]
    [Range(-2.0f, 2.0f)] public float offsetHombro = 0.55f;

    [Tooltip("Desplazamiento vertical extra de la cámara.")]
    [Range(-2.0f, 2.0f)] public float offsetVertical = 0f;

    [Tooltip("Campo de visión (FOV).")]
    [Range(30f, 110f)] public float fov = 60f;

    // ── Sensibilidad ───────────────────────────────────────────────────
    [Header("Sensibilidad del Ratón")]
    [Range(0.5f, 10f)] public float sensibilidadH = 2f;
    [Range(0.5f, 10f)] public float sensibilidadV = 2f;
    public bool invertirVertical = false;

    // ── Límites de pitch ───────────────────────────────────────────────
    [Header("Límites de Inclinación Vertical")]
    [Range(-85f, 0f)] public float pitchMin = -40f;
    [Range(0f, 85f)] public float pitchMax = 60f;

    // ── Movimiento ─────────────────────────────────────────────────────
    [Header("Movimiento del Personaje")]
    public float velocidad = 6f;
    public float gravedad = -9.81f;

    // ── Anti-colisión ──────────────────────────────────────────────────
    [Header("Anti-Colisión (SphereCast)")]
    public bool antiColision = true;
    [Range(0.05f, 0.5f)] public float radioSphere = 0.2f;
    [Tooltip("Distancia mínima de la cámara al pivote cuando hay obstáculo.")]
    [Range(0.1f, 2f)] public float distanciaMinima = 0.3f;
    [Tooltip("Velocidad de recuperación al alejarse del obstáculo.")]
    [Range(1f, 30f)] public float velocidadRecuperacion = 15f;
    public LayerMask capasColision = ~0;

    // ── Referencias ────────────────────────────────────────────────────
    private CharacterController cc;
    private Transform pivotCamara;
    private Transform mainCamTransform;
    private Camera cam;

    private float yaw;
    private float pitch;
    private float factorDistancia = 1f;
    private Vector3 velocidadCaida;

    // ══════════════════════════════════════════════════════════════════ //
    //  Inicialización                                                   //
    // ══════════════════════════════════════════════════════════════════ //

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        ConfigurarJerarquia();
        yaw = transform.eulerAngles.y;
        BloquearCursor(true);
    }

    /// <summary>
    /// Detecta o crea CameraPivot y MainCamera en la jerarquía.
    /// No borra nada; si ya existen los reutiliza.
    /// </summary>
    private void ConfigurarJerarquia()
    {
        // ── CameraPivot ──────────────────────────────────────────────
        pivotCamara = transform.Find("CameraPivot");
        if (pivotCamara == null)
        {
            pivotCamara = new GameObject("CameraPivot").transform;
            pivotCamara.SetParent(transform, false);
        }
        pivotCamara.localPosition = new Vector3(0f, alturaPivote, 0f);
        pivotCamara.localRotation = Quaternion.identity;
        pivotCamara.localScale    = Vector3.one;

        // ── MainCamera ───────────────────────────────────────────────
        Camera camExistente = pivotCamara.GetComponentInChildren<Camera>(true);
        if (camExistente != null)
        {
            mainCamTransform = camExistente.transform;
        }
        else
        {
            // Reparentar Camera.main si es ajena al personaje
            Camera cmain = Camera.main;
            if (cmain != null && !cmain.transform.IsChildOf(transform))
            {
                AudioListener alViejo = cmain.GetComponent<AudioListener>();
                if (alViejo != null) alViejo.enabled = false;
                cmain.transform.SetParent(pivotCamara, false);
                mainCamTransform = cmain.transform;
                cmain.gameObject.AddComponent<AudioListener>();
            }
            else
            {
                // Crear MainCamera nueva
                GameObject go = new GameObject("MainCamera");
                go.tag = "MainCamera";
                go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
                mainCamTransform = go.transform;
                mainCamTransform.SetParent(pivotCamara, false);
            }
        }

        // Desactivar el resto de cámaras en escena para evitar conflictos
        foreach (Camera c in Camera.allCameras)
        {
            if (c == null || mainCamTransform == null) continue;
            if (c.transform == mainCamTransform) continue;
            AudioListener al = c.GetComponent<AudioListener>();
            if (al != null) al.enabled = false;
            c.enabled = false;
        }

        cam = mainCamTransform.GetComponent<Camera>();
        if (cam != null) cam.fieldOfView = fov;

        AplicarOffsetCamara(1f);
    }

    // ══════════════════════════════════════════════════════════════════ //
    //  Update — Input, rotación y movimiento                           //
    // ══════════════════════════════════════════════════════════════════ //

    private void Update()
    {
        GestionarCursor();

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            LeerRaton(out float mouseX, out float mouseY);

            // Mouse X → Yaw del cuerpo (eje Y mundial de PlayerRoot)
            yaw += mouseX * sensibilidadH;

            // Mouse Y → Pitch del pivote (eje X local de CameraPivot)
            float sentido = invertirVertical ? 1f : -1f;
            pitch += mouseY * sensibilidadV * sentido;
            pitch  = Mathf.Clamp(pitch, pitchMin, pitchMax);
        }

        // Aplicar rotaciones — separación estricta de ejes
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (pivotCamara != null)
            pivotCamara.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        ActualizarMovimiento();
    }

    // ══════════════════════════════════════════════════════════════════ //
    //  LateUpdate — Posicionamiento de cámara y anti-colisión          //
    // ══════════════════════════════════════════════════════════════════ //

    private void LateUpdate()
    {
        if (pivotCamara == null || mainCamTransform == null) return;

        // Fijar posición del pivote (puede haberse movido por física de padres)
        pivotCamara.localPosition = new Vector3(0f, alturaPivote, 0f);

        float factorObjetivo = 1f;

        if (antiColision)
        {
            Vector3 origen  = pivotCamara.position;
            Vector3 destino = pivotCamara.TransformPoint(
                new Vector3(offsetHombro, offsetVertical, -distanciaCamara));
            Vector3 dir  = destino - origen;
            float   dist = dir.magnitude;

            if (dist > 0.001f)
            {
                if (Physics.SphereCast(origen, radioSphere, dir / dist, out RaycastHit hit,
                        dist, capasColision, QueryTriggerInteraction.Ignore)
                    && !hit.collider.transform.IsChildOf(transform))
                {
                    float segura = Mathf.Clamp(hit.distance - 0.05f, distanciaMinima, dist);
                    factorObjetivo = Mathf.Clamp01(segura / dist);
                }
            }
        }

        // Acercar instantáneo al chocar, alejar suave al despejarse
        if (factorObjetivo < factorDistancia)
            factorDistancia = factorObjetivo;
        else
            factorDistancia = Mathf.Lerp(
                factorDistancia, factorObjetivo,
                1f - Mathf.Exp(-velocidadRecuperacion * Time.deltaTime));

        AplicarOffsetCamara(factorDistancia);
    }

    // ══════════════════════════════════════════════════════════════════ //
    //  Helpers                                                          //
    // ══════════════════════════════════════════════════════════════════ //

    private void AplicarOffsetCamara(float factor)
    {
        if (mainCamTransform == null) return;
        mainCamTransform.localPosition =
            new Vector3(offsetHombro * factor, offsetVertical, -distanciaCamara * factor);
        mainCamTransform.localRotation = Quaternion.identity; // nunca rota sola
    }

    private void ActualizarMovimiento()
    {
        float h = 0f, v = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            h = (Keyboard.current.dKey.isPressed     ? 1f : 0f)
              - (Keyboard.current.aKey.isPressed     ? 1f : 0f);
            v = (Keyboard.current.wKey.isPressed     ? 1f : 0f)
              - (Keyboard.current.sKey.isPressed     ? 1f : 0f);
        }
        // Fallback al Input Manager clásico
        if (Mathf.Abs(h) < 0.001f && Mathf.Abs(v) < 0.001f)
        {
            h = Input.GetAxis("Horizontal");
            v = Input.GetAxis("Vertical");
        }
#else
        h = Input.GetAxis("Horizontal");
        v = Input.GetAxis("Vertical");
#endif

        // Proyectar forward y right sobre el plano XZ para que mirar arriba/abajo
        // no haga flotar ni hundir al personaje.
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 rgt = Vector3.ProjectOnPlane(transform.right,   Vector3.up).normalized;
        Vector3 dir = fwd * v + rgt * h;
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        // Gravedad acumulativa
        if (cc.isGrounded && velocidadCaida.y < 0f)
            velocidadCaida.y = -2f;
        else
            velocidadCaida.y += gravedad * Time.deltaTime;

        cc.Move((dir * velocidad + velocidadCaida) * Time.deltaTime);
    }

    private void LeerRaton(out float mouseX, out float mouseY)
    {
        mouseX = 0f;
        mouseY = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue() * 0.05f;
            mouseX = delta.x;
            mouseY = delta.y;
        }
        if (Mathf.Abs(mouseX) < 0.0001f && Mathf.Abs(mouseY) < 0.0001f)
        {
            mouseX = Input.GetAxis("Mouse X");
            mouseY = Input.GetAxis("Mouse Y");
        }
#else
        mouseX = Input.GetAxis("Mouse X");
        mouseY = Input.GetAxis("Mouse Y");
#endif
    }

    private void GestionarCursor()
    {
        bool escape = Input.GetKeyDown(KeyCode.Escape);
        bool clic   = Input.GetMouseButtonDown(0);

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) escape = true;
        if (Mouse.current  != null && Mouse.current.leftButton.wasPressedThisFrame)    clic   = true;
#endif

        if (escape)
            BloquearCursor(false);
        else if (Cursor.lockState != CursorLockMode.Locked && clic)
            BloquearCursor(true);
    }

    private void BloquearCursor(bool bloquear)
    {
        Cursor.lockState = bloquear ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !bloquear;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (pivotCamara      != null) pivotCamara.localPosition = new Vector3(0f, alturaPivote, 0f);
        if (cam              != null) cam.fieldOfView = fov;
        AplicarOffsetCamara(factorDistancia);
    }
#endif
}
