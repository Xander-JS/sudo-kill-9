using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controlador de Movimiento en Tercera Persona Relativo a la Cámara (Estilo GTA / Tomb Raider / Fortnite).
/// 
/// RESUELVE EL PROBLEMA:
/// "Las teclas WASD no corresponden a la pantalla o la dirección queda fija".
/// 
/// ¿POR QUÉ OCURRE ESE PROBLEMA?
/// Si el movimiento se calcula en coordenadas mundiales (Vector3.forward * v) o locales fijas del personaje
/// sin proyectar los vectores de la CÁMARA en el plano horizontal (XZ), presionar 'W' moverá al personaje
/// hacia el norte del mundo o hacia donde apuntaba su modelo, ignorando hacia dónde mira el jugador en pantalla.
/// 
/// SOLUCIÓN ARQUITECTÓNICA DE ESTE SCRIPT:
/// 1. Proyecta los vectores forward y right de la cámara activa sobre el plano del suelo (Y = 0):
///    camFwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
///    camRight = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
/// 2. Construye el vector de movimiento:
///    moveDir = camFwd * vertical + camRight * horizontal;
///    - 'W' SIEMPRE avanza hacia el fondo de la pantalla (alejándose de la cámara).
///    - 'S' SIEMPRE retrocede hacia la pantalla (acercándose a la cámara).
///    - 'A' SIEMPRE se desplaza hacia la izquierda de la pantalla.
///    - 'D' SIEMPRE se desplaza hacia la derecha de la pantalla.
/// 
/// DOS MODOS DE JUEGO CONFIGURABLES:
/// - Libre / Exploración (GTA libre): El personaje gira suavemente hacia la dirección de avance.
/// - Strafe / Apuntado (GTA con arma / Shooter): El cuerpo se alinea con la cámara y hace paso lateral.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[AddComponentMenu("Movimiento/Movimiento Tercera Persona (Estilo GTA)")]
public class ThirdPersonMovementGTA : MonoBehaviour
{
    public enum ModoMovimiento
    {
        [Tooltip("El personaje rota suavemente hacia la dirección en la que camina/corre (Modo libre GTA / RPG).")]
        LibreExploracion,

        [Tooltip("El personaje siempre mira hacia el frente de la cámara y camina de lado (Strafe / TPS Shooter).")]
        StrafeApuntado
    }

    // ========================================================================= //
    // 1. REFERENCIAS PRINCIPALES                                                //
    // ========================================================================= //

    [Header("1. Referencias")]
    [Tooltip("Cámara que define la orientación del movimiento (si está vacío, busca Camera.main automáticamente).")]
    public Transform camara;

    [Tooltip("CharacterController para el movimiento físico (se detecta automáticamente).")]
    public CharacterController controlador;

    [Tooltip("Animator para reproducir animaciones de caminata/carrera (opcional).")]
    public Animator animator;

    // ========================================================================= //
    // 2. MODALIDAD Y VELOCIDADES DE MOVIMIENTO                                  //
    // ========================================================================= //

    [Header("2. Modalidad y Velocidades")]
    [Tooltip("Modo de movimiento base: Libre (rota al moverse) o Strafe (mira siempre hacia la cámara).")]
    public ModoMovimiento modo = ModoMovimiento.LibreExploracion;

    [Tooltip("Si está activo, pasar a Strafe/Apuntado automáticamente al mantener presionado el botón derecho del ratón.")]
    public bool strafeAlApuntar = true;

    [Tooltip("Velocidad normal de caminata en metros por segundo.")]
    [Range(1f, 15f)]
    public float velocidadCaminata = 4.5f;

    [Tooltip("Velocidad de carrera rápida (Sprint al presionar Shift).")]
    [Range(2f, 25f)]
    public float velocidadCarrera = 8.0f;

    [Tooltip("Tiempo de suavizado para la rotación del cuerpo hacia la dirección de movimiento (Modo Libre).")]
    [Range(0.01f, 0.4f)]
    public float tiempoSuavizadoGiro = 0.1f;

    // ========================================================================= //
    // 3. FÍSICA, GRAVEDAD Y SALTO                                               //
    // ========================================================================= //

    [Header("3. Física y Gravedad")]
    [Tooltip("Fuerza de gravedad aplicada hacia abajo (por ejemplo -18 m/s²).")]
    public float gravedad = -19.62f;

    [Tooltip("Altura máxima del salto en metros.")]
    [Range(0.5f, 5f)]
    public float alturaSalto = 1.3f;

    [Tooltip("Permite saltar con la barra espaciadora.")]
    public bool permitirSalto = true;

    // ========================================================================= //
    // 4. PARÁMETROS DEL ANIMATOR (BLEND TREE / VELOCIDAD)                       //
    // ========================================================================= //

    [Header("4. Nombres de Parámetros del Animator")]
    [Tooltip("Nombre del parámetro flotante para la velocidad total (magnitud 0 a 1).")]
    public string paramVelocidad = "Speed";

    [Tooltip("Nombre del parámetro para desplazamiento lateral en Blend Tree 2D (-1 izquierda, +1 derecha).")]
    public string paramInputX = "InputX";

    [Tooltip("Nombre del parámetro para avance/retroceso en Blend Tree 2D (-1 atrás, +1 adelante).")]
    public string paramInputZ = "InputZ";

    [Tooltip("Nombre del parámetro booleano para indicar si está tocando el suelo.")]
    public string paramEnSuelo = "IsGrounded";

    [Tooltip("Nombre del disparador para la animación de salto.")]
    public string paramTriggerSalto = "Jump";

    // Variables internas
    private Vector3 velocidadVertical;
    private float velocidadGiroActual;
    private int hashVelocidad;
    private int hashInputX;
    private int hashInputZ;
    private int hashEnSuelo;
    private int hashTriggerSalto;
    private bool tieneParamVelocidad;
    private bool tieneParamInputX;
    private bool tieneParamInputZ;
    private bool tieneParamEnSuelo;
    private bool tieneParamSalto;

    private void Awake()
    {
        if (controlador == null) controlador = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        LocalizarCamara();
        CachearParametrosAnimator();
    }

    private void Start()
    {
        LocalizarCamara();
    }

    private void Update()
    {
        if (camara == null)
        {
            LocalizarCamara();
            if (camara == null) return;
        }

        ProcesarMovimiento();
    }

    /// <summary>
    /// Localiza automáticamente la cámara principal si no está asignada en el Inspector.
    /// </summary>
    public void LocalizarCamara()
    {
        if (camara != null) return;

        Camera camMain = Camera.main;
        if (camMain != null)
        {
            camara = camMain.transform;
            return;
        }

        Camera anyCam = Object.FindFirstObjectByType<Camera>();
        if (anyCam != null)
        {
            camara = anyCam.transform;
        }
    }

    /// <summary>
    /// Cachea los hashes de los parámetros del Animator para evitar consultas por texto cada fotograma.
    /// </summary>
    private void CachearParametrosAnimator()
    {
        if (animator == null) return;

        hashVelocidad = Animator.StringToHash(paramVelocidad);
        hashInputX = Animator.StringToHash(paramInputX);
        hashInputZ = Animator.StringToHash(paramInputZ);
        hashEnSuelo = Animator.StringToHash(paramEnSuelo);
        hashTriggerSalto = Animator.StringToHash(paramTriggerSalto);

        foreach (var p in animator.parameters)
        {
            if (p.name == paramVelocidad) tieneParamVelocidad = true;
            if (p.name == paramInputX) tieneParamInputX = true;
            if (p.name == paramInputZ) tieneParamInputZ = true;
            if (p.name == paramEnSuelo) tieneParamEnSuelo = true;
            if (p.name == paramTriggerSalto) tieneParamSalto = true;
        }
    }

    /// <summary>
    /// Lee las entradas de teclado/mouse, calcula la dirección relativa a la cámara
    /// y aplica la traslación, rotación y gravedad correspondientes.
    /// </summary>
    private void ProcesarMovimiento()
    {
        // 1. Leer entradas horizontales y verticales (compatible con Input Manager clásico y New Input System)
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool jump = Input.GetKeyDown(KeyCode.Space);
        bool apuntando = Input.GetMouseButton(1);

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            Keyboard kb = Keyboard.current;
            if (Mathf.Abs(horizontal) < 0.0001f && Mathf.Abs(vertical) < 0.0001f)
            {
                horizontal = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) -
                             (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                vertical = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) -
                           (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            }
            if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) sprint = true;
            if (kb.spaceKey.wasPressedThisFrame) jump = true;
        }

        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            apuntando = true;
        }
#endif

        // 2. Determinar si estamos en modo Strafe (por configuración o por apuntar con clic derecho)
        bool modoStrafeActivo = (modo == ModoMovimiento.StrafeApuntado) || (strafeAlApuntar && apuntando);

        // 3. CÁLCULO CLAVE DE LA INDUSTRIA: Proyección de los vectores de la CÁMARA sobre el suelo (Y = 0)
        Vector3 camFwd = Vector3.ProjectOnPlane(camara.forward, Vector3.up);
        Vector3 camRight = Vector3.ProjectOnPlane(camara.right, Vector3.up);

        if (camFwd.sqrMagnitude > 0.0001f) camFwd.Normalize();
        else camFwd = Vector3.forward;

        if (camRight.sqrMagnitude > 0.0001f) camRight.Normalize();
        else camRight = Vector3.right;

        // Dirección mundial de movimiento deseada:
        // 'W' = camFwd (+ fondo de la pantalla)
        // 'S' = -camFwd (+ frente de la pantalla)
        // 'A' = -camRight (+ izquierda de la pantalla)
        // 'D' = camRight (+ derecha de la pantalla)
        Vector3 direccionDeseada = (camFwd * vertical + camRight * horizontal);
        float magnitudEntrada = Mathf.Clamp01(new Vector2(horizontal, vertical).magnitude);

        if (direccionDeseada.sqrMagnitude > 1f)
        {
            direccionDeseada.Normalize();
        }

        // 4. ROTACIÓN DEL PERSONAJE
        if (modoStrafeActivo)
        {
            // MODO STRAFE / SHOOTER: El cuerpo siempre se orienta horizontalmente con la cámara
            float targetYaw = camara.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
        }
        else
        {
            // MODO LIBRE GTA: El cuerpo rota suavemente hacia la dirección en la que se está moviendo
            if (magnitudEntrada > 0.05f)
            {
                float targetAngle = Mathf.Atan2(direccionDeseada.x, direccionDeseada.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref velocidadGiroActual, tiempoSuavizadoGiro);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        // 5. GESTIÓN DE GRAVEDAD Y SALTO
        bool enSuelo = controlador != null && controlador.isGrounded;

        if (enSuelo)
        {
            // Pequeña fuerza descendente para mantener contacto firme con el suelo y desniveles
            if (velocidadVertical.y < 0f)
            {
                velocidadVertical.y = -2f;
            }

            if (permitirSalto && jump)
            {
                velocidadVertical.y = Mathf.Sqrt(alturaSalto * -2f * gravedad);
                if (animator != null && tieneParamSalto)
                {
                    animator.SetTrigger(hashTriggerSalto);
                }
            }
        }
        else
        {
            // Caída acelerada por gravedad
            velocidadVertical.y += gravedad * Time.deltaTime;
        }

        // 6. APLICAR MOVIMIENTO CON CHARACTERCONTROLLER
        float velocidadActual = sprint ? velocidadCarrera : velocidadCaminata;
        Vector3 movimientoHorizontal = direccionDeseada * (velocidadActual * magnitudEntrada);
        Vector3 movimientoFinal = movimientoHorizontal + velocidadVertical;

        if (controlador != null && controlador.enabled)
        {
            controlador.Move(movimientoFinal * Time.deltaTime);
        }

        // 7. ACTUALIZAR ANIMATOR
        ActualizarAnimaciones(direccionDeseada, magnitudEntrada, sprint, enSuelo, modoStrafeActivo);
    }

    /// <summary>
    /// Sincroniza los parámetros de avance, desplazamiento lateral y velocidad con el Animator.
    /// </summary>
    private void ActualizarAnimaciones(Vector3 direccionDeseada, float magnitudEntrada, bool sprint, bool enSuelo, bool modoStrafe)
    {
        if (animator == null) return;

        if (tieneParamEnSuelo)
        {
            animator.SetBool(hashEnSuelo, enSuelo);
        }

        // Parámetro de velocidad simple (0 a 1 normal, hasta 2 en sprint)
        if (tieneParamVelocidad)
        {
            float valorVel = magnitudEntrada * (sprint ? 1.5f : 1.0f);
            animator.SetFloat(hashVelocidad, valorVel, 0.1f, Time.deltaTime);
        }

        // Blend Tree 2D (InputX / InputZ) proyectado en el espacio local del personaje
        if (tieneParamInputX || tieneParamInputZ)
        {
            Vector3 direccionLocal = transform.InverseTransformDirection(direccionDeseada);
            float x = direccionLocal.x * (sprint ? 1.5f : 1.0f);
            float z = direccionLocal.z * (sprint ? 1.5f : 1.0f);

            if (tieneParamInputX) animator.SetFloat(hashInputX, x, 0.08f, Time.deltaTime);
            if (tieneParamInputZ) animator.SetFloat(hashInputZ, z, 0.08f, Time.deltaTime);
        }
    }
}
