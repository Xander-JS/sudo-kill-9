using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Sistema Profesional de Cámara en Tercera Persona Over-The-Shoulder (Inspirado en GTA / Tomb Raider / Free Fire).
/// 
/// ARQUITECTURA DE TRES NIVELES OBLIGATORIA:
/// PlayerRoot (Cuerpo del jugador)
///  └── CameraPivot (Pivote de inclinación vertical - altura de hombros/cabeza)
///       └── MainCamera (Cámara real desplazada sobre el hombro)
/// 
/// SEPARACIÓN MATEMÁTICA DE ROTACIONES:
/// - Mouse X -> Rota exclusivamente el cuerpo del jugador sobre el eje Y mundial (Yaw).
/// - Mouse Y -> Inclina exclusivamente el pivote de la cámara sobre su eje X local (Pitch con Clamp estricto).
/// - MainCamera -> Hereda ambas orientaciones de forma estable y mantiene su desplazamiento lateral (Over-The-Shoulder).
/// </summary>
[AddComponentMenu("Cámara/Cámara Tercera Persona (Estilo GTA)")]
public class ThirdPersonCameraGTA : MonoBehaviour
{
    // ========================================================================= //
    // 1. REFERENCIAS DE JERARQUÍA                                               //
    // ========================================================================= //

    [Header("1. Jerarquía de la Cámara")]
    [Tooltip("Transform del personaje principal que rotará horizontalmente con el ratón (por defecto este mismo objeto).")]
    public Transform targetPlayer;

    [Tooltip("Transform del pivote a la altura de los hombros/cabeza. Controla únicamente la inclinación vertical.")]
    public Transform pivotCamara;

    [Tooltip("Transform de la cámara principal. Mantiene el desplazamiento Over-The-Shoulder.")]
    public Transform camaraTransform;

    // ========================================================================= //
    // 2. CONFIGURACIÓN OVER-THE-SHOULDER                                        //
    // ========================================================================= //

    [Header("2. Posición y Composición Over-The-Shoulder")]
    [Tooltip("Altura del pivote de cámara respecto a los pies del personaje.")]
    [Range(-1.0f, 3.5f)]
    public float alturaPivote = 1.35f;

    [Tooltip("Ajuste fino vertical de la cámara (subir o bajar la vista respecto al hombro).")]
    [Range(-2.0f, 2.0f)]
    public float offsetVerticalCamara = 0.0f;

    [Tooltip("Distancia de la cámara detrás del personaje (menor = más cerca, mayor = más lejos).")]
    [Range(0.5f, 8.0f)]
    public float distanciaCamara = 2.4f;

    [Tooltip("Desplazamiento lateral sobre el hombro (+ derecha tipo GTA, 0 centrado, - izquierda).")]
    [Range(-2.5f, 2.5f)]
    public float offsetLateralHombro = 0.55f;

    [Tooltip("Inclinación vertical adicional en grados para la mirada.")]
    [Range(-45f, 45f)]
    public float inclinacionVerticalOffset = 0f;

    [Tooltip("Campo de visión (Zoom / FOV) de la cámara (60 = estándar GTA, menor = más zoom).")]
    [Range(30f, 110f)]
    public float campoDeVisionFOV = 60f;

    // ========================================================================= //
    // 3. SENSIBILIDAD Y LÍMITES DEL RATÓN                                       //
    // ========================================================================= //

    [Header("3. Control del Ratón (Sensibilidad y Límites)")]
    [Tooltip("Velocidad de giro horizontal del personaje (Mouse X).")]
    [Range(0.1f, 10f)]
    public float sensibilidadHorizontal = 2.0f;

    [Tooltip("Velocidad de giro vertical de la cámara (Mouse Y).")]
    [Range(0.1f, 10f)]
    public float sensibilidadVertical = 2.0f;

    [Tooltip("Límite inferior para mirar hacia arriba en grados (por ejemplo -40°).")]
    [Range(-85f, 0f)]
    public float anguloMinimo = -40f;

    [Tooltip("Límite superior para mirar hacia abajo en grados (por ejemplo 60°).")]
    [Range(0f, 85f)]
    public float anguloMaximo = 60f;

    [Tooltip("Tiempo de suavizado angular (0 = respuesta inmediata y nítida).")]
    [Range(0f, 0.3f)]
    public float suavizadoCamara = 0f;

    [Tooltip("Si está activo, invierte el eje vertical arriba/abajo del ratón.")]
    public bool invertirEjeVertical = false;

    // ========================================================================= //
    // 4. SISTEMA ANTI-COLISIÓN (SPHERECAST)                                     //
    // ========================================================================= //

    [Header("4. Protección de Colisiones contra Paredes")]
    [Tooltip("Acerca automáticamente la cámara si hay una pared o geometría detrás del personaje.")]
    public bool evitarColisiones = true;

    [Tooltip("Radio de la esfera de detección para colisiones de cámara.")]
    [Range(0.05f, 0.6f)]
    public float radioColision = 0.2f;

    [Tooltip("Distancia mínima entre el pivote y la cámara cuando hay una pared pegada.")]
    [Range(0.1f, 1.5f)]
    public float distanciaMinima = 0.35f;

    [Tooltip("Velocidad con la que la cámara recupera su distancia normal tras despejarse un obstáculo.")]
    [Range(1f, 30f)]
    public float velocidadRecuperacion = 15f;

    [Tooltip("Capas físicas con las que colisiona la cámara (debe excluir al jugador).")]
    public LayerMask capasColision = ~0;

    // ========================================================================= //
    // 5. CONTROL DEL CURSOR                                                     //
    // ========================================================================= //

    [Header("5. Estado del Cursor")]
    [Tooltip("Bloquea y oculta el puntero del ratón automáticamente al iniciar el juego.")]
    public bool bloquearCursorAlIniciar = true;

    [Tooltip("Tecla para liberar el cursor del ratón (por defecto Escape).")]
    public KeyCode teclaDesbloquearCursor = KeyCode.Escape;

    // Variables internas de control angular
    private float yawObjetivo;
    private float pitchObjetivo;
    private float yawActual;
    private float pitchActual;
    private float velocidadYaw;
    private float velocidadPitch;
    private float factorDistanciaActual = 1f;
    private readonly RaycastHit[] bufferColisiones = new RaycastHit[16];
    private Collider[] collidersJugador;

    private void Awake()
    {
        if (targetPlayer == null) targetPlayer = transform;
        GarantizarJerarquiaCamara();
    }

    private void Start()
    {
        InicializarAngulosYReferencias();

        if (bloquearCursorAlIniciar)
        {
            BloquearCursor(true);
        }
    }

    /// <summary>
    /// Crea o localiza automáticamente CameraPivot y MainCamera si no estaban asignados.
    /// </summary>
    [ContextMenu("Auto-Configurar Jerarquía de Cámara")]
    public void GarantizarJerarquiaCamara()
    {
        if (targetPlayer == null) targetPlayer = transform;

        // 1. Localizar o crear CameraPivot
        if (pivotCamara == null)
        {
            Transform encontrado = targetPlayer.Find("CameraPivot");
            if (encontrado != null)
            {
                pivotCamara = encontrado;
            }
            else
            {
                GameObject nuevoPivote = new GameObject("CameraPivot");
                nuevoPivote.transform.SetParent(targetPlayer, false);
                nuevoPivote.transform.localPosition = new Vector3(0f, alturaPivote, 0f);
                pivotCamara = nuevoPivote.transform;
            }
        }

        // 2. Localizar o crear MainCamera
        if (camaraTransform == null && pivotCamara != null)
        {
            Camera camEnPivote = pivotCamara.GetComponentInChildren<Camera>();
            if (camEnPivote != null)
            {
                camaraTransform = camEnPivote.transform;
            }
            else
            {
                Camera camGlobal = Camera.main;
                if (camGlobal != null)
                {
                    camGlobal.transform.SetParent(pivotCamara, false);
                    camaraTransform = camGlobal.transform;
                }
                else
                {
                    GameObject nuevaCam = new GameObject("MainCamera");
                    nuevaCam.tag = "MainCamera";
                    nuevaCam.transform.SetParent(pivotCamara, false);
                    Camera camComp = nuevaCam.AddComponent<Camera>();
                    camComp.fieldOfView = campoDeVisionFOV;
                    nuevaCam.AddComponent<AudioListener>();
                    camaraTransform = nuevaCam.transform;
                }
            }
        }

        // Cachear colliders del personaje para que la cámara no colisione consigo mismo
        collidersJugador = targetPlayer.GetComponentsInChildren<Collider>(true);
    }

    private void InicializarAngulosYReferencias()
    {
        GarantizarJerarquiaCamara();

        yawObjetivo = NormalizarAngulo(targetPlayer.eulerAngles.y);
        yawActual = yawObjetivo;

        if (pivotCamara != null)
        {
            pitchObjetivo = NormalizarAngulo(pivotCamara.localEulerAngles.x);
            pitchActual = pitchObjetivo;
        }

        AplicarTransformacionesInmediatas();
    }

    private void Update()
    {
        GestionarCursor();
        ActualizarRotacionConMouse();
    }

    private void LateUpdate()
    {
        ActualizarPosicionYSeguimientoCamara();
    }

    /// <summary>
    /// Gestiona la captura y liberación del cursor con tecla Escape o clic dentro de pantalla.
    /// </summary>
    private void GestionarCursor()
    {
        bool escape = Input.GetKeyDown(teclaDesbloquearCursor);
        bool clic = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) escape = true;
        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) clic = true;
#endif

        if (escape)
        {
            BloquearCursor(false);
        }
        else if (Cursor.lockState != CursorLockMode.Locked && clic)
        {
            BloquearCursor(true);
        }
    }

    public void BloquearCursor(bool bloquear)
    {
        Cursor.lockState = bloquear ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !bloquear;
    }

    /// <summary>
    /// Lee la entrada del ratón y aplica estrictamente la separación:
    /// - Mouse X -> Eje Y de PlayerRoot (Yaw horizontal).
    /// - Mouse Y -> Eje X local de CameraPivot (Pitch vertical limitado con Clamp).
    /// </summary>
    private void ActualizarRotacionConMouse()
    {
        if (pivotCamara == null || targetPlayer == null) return;

        float mouseX = 0f;
        float mouseY = 0f;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            mouseX = Input.GetAxis("Mouse X");
            mouseY = Input.GetAxis("Mouse Y");

#if ENABLE_INPUT_SYSTEM
            if (Mathf.Abs(mouseX) < 0.0001f && Mathf.Abs(mouseY) < 0.0001f && Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                mouseX = delta.x * 0.05f;
                mouseY = delta.y * 0.05f;
            }
#endif
        }

        // 1. Acumulación horizontal en PlayerRoot
        yawObjetivo = NormalizarAngulo(yawObjetivo + mouseX * sensibilidadHorizontal);

        // 2. Acumulación vertical en CameraPivot
        float factorInversion = invertirEjeVertical ? 1f : -1f;
        pitchObjetivo += mouseY * sensibilidadVertical * factorInversion;
        pitchObjetivo = Mathf.Clamp(pitchObjetivo, anguloMinimo, anguloMaximo);

        // 3. Suavizado angular opcional
        if (suavizadoCamara > 0.0001f)
        {
            yawActual = NormalizarAngulo(Mathf.SmoothDampAngle(yawActual, yawObjetivo, ref velocidadYaw, suavizadoCamara));
            pitchActual = Mathf.SmoothDampAngle(pitchActual, pitchObjetivo, ref velocidadPitch, suavizadoCamara);
            pitchActual = Mathf.Clamp(pitchActual, anguloMinimo, anguloMaximo);
        }
        else
        {
            yawActual = yawObjetivo;
            pitchActual = pitchObjetivo;
        }

        // Aplicar rotaciones
        targetPlayer.rotation = Quaternion.Euler(0f, yawActual, 0f);
        float pitchFinal = Mathf.Clamp(pitchActual + inclinacionVerticalOffset, anguloMinimo, anguloMaximo);
        pivotCamara.localRotation = Quaternion.Euler(pitchFinal, 0f, 0f);
    }

    /// <summary>
    /// Posiciona la cámara con desplazamiento Over-The-Shoulder y resuelve colisiones contra paredes.
    /// Se ejecuta en LateUpdate para garantizar cero vibración o temblor con el movimiento del personaje.
    /// </summary>
    private void ActualizarPosicionYSeguimientoCamara()
    {
        if (pivotCamara == null || camaraTransform == null) return;

        // Mantener el pivote en la altura exacta
        pivotCamara.localPosition = new Vector3(0f, alturaPivote, 0f);

        Vector3 offsetDeseado = new Vector3(offsetLateralHombro, offsetVerticalCamara, -Mathf.Abs(distanciaCamara));
        float factorObjetivo = 1f;

        // Detección de colisiones mediante SphereCast
        if (evitarColisiones)
        {
            Vector3 origenPivote = pivotCamara.position;
            Vector3 destinoMundo = pivotCamara.TransformPoint(offsetDeseado);
            Vector3 vectorHaciaCamara = destinoMundo - origenPivote;
            float distanciaTotal = vectorHaciaCamara.magnitude;

            if (distanciaTotal > 0.001f)
            {
                Vector3 dir = vectorHaciaCamara / distanciaTotal;
                int impactos = Physics.SphereCastNonAlloc(
                    origenPivote,
                    radioColision,
                    dir,
                    bufferColisiones,
                    distanciaTotal,
                    capasColision,
                    QueryTriggerInteraction.Ignore
                );

                float menorDistancia = distanciaTotal;
                bool hayObstaculo = false;

                for (int i = 0; i < impactos; i++)
                {
                    Collider col = bufferColisiones[i].collider;
                    if (col == null) continue;

                    // Ignorar el propio personaje y sus hijos
                    if (EsColliderDelJugador(col)) continue;

                    // Ignorar el suelo horizontal bajo los pies si la cámara está baja
                    if (bufferColisiones[i].normal.y > 0.75f && bufferColisiones[i].point.y <= targetPlayer.position.y + 0.15f)
                        continue;

                    if (bufferColisiones[i].distance < menorDistancia)
                    {
                        menorDistancia = bufferColisiones[i].distance;
                        hayObstaculo = true;
                    }
                }

                if (hayObstaculo)
                {
                    float limiteMinimo = Mathf.Min(distanciaMinima, distanciaTotal);
                    float distanciaSegura = Mathf.Clamp(menorDistancia - 0.05f, limiteMinimo, distanciaTotal);
                    factorObjetivo = Mathf.Clamp01(distanciaSegura / distanciaTotal);
                }
            }
        }

        // Transición suave de colisión
        if (factorObjetivo < factorDistanciaActual)
        {
            factorDistanciaActual = factorObjetivo; // Acercar de inmediato si hay colisión
        }
        else
        {
            factorDistanciaActual = Mathf.Lerp(
                factorDistanciaActual,
                factorObjetivo,
                1f - Mathf.Exp(-velocidadRecuperacion * Time.deltaTime)
            );
        }

        // Posicionar cámara respetando siempre el hombro y la altura
        camaraTransform.localPosition = new Vector3(
            offsetLateralHombro * factorDistanciaActual,
            offsetVerticalCamara,
            -Mathf.Abs(distanciaCamara) * factorDistanciaActual
        );
        camaraTransform.localRotation = Quaternion.identity;

        // Actualizar FOV
        Camera camComp = camaraTransform.GetComponent<Camera>();
        if (camComp != null && !Mathf.Approximately(camComp.fieldOfView, campoDeVisionFOV))
        {
            camComp.fieldOfView = campoDeVisionFOV;
        }
    }

    private bool EsColliderDelJugador(Collider col)
    {
        if (col == null || targetPlayer == null) return false;
        if (col.transform == targetPlayer || col.transform.IsChildOf(targetPlayer) || col.transform.root == targetPlayer.root)
            return true;
        return false;
    }

    private void AplicarTransformacionesInmediatas()
    {
        if (targetPlayer != null)
        {
            targetPlayer.rotation = Quaternion.Euler(0f, yawActual, 0f);
        }
        if (pivotCamara != null)
        {
            pivotCamara.localPosition = new Vector3(0f, alturaPivote, 0f);
            float pitchFinal = Mathf.Clamp(pitchActual + inclinacionVerticalOffset, anguloMinimo, anguloMaximo);
            pivotCamara.localRotation = Quaternion.Euler(pitchFinal, 0f, 0f);
        }
        if (camaraTransform != null)
        {
            camaraTransform.localPosition = new Vector3(offsetLateralHombro, offsetVerticalCamara, -Mathf.Abs(distanciaCamara));
            camaraTransform.localRotation = Quaternion.identity;
        }
    }

    private static float NormalizarAngulo(float angulo)
    {
        angulo %= 360f;
        if (angulo > 180f) angulo -= 360f;
        if (angulo < -180f) angulo += 360f;
        return angulo;
    }

    private void OnValidate()
    {
        if (anguloMinimo > anguloMaximo) anguloMinimo = anguloMaximo;
        if (Application.isPlaying)
        {
            AplicarTransformacionesInmediatas();
        }
    }
}
