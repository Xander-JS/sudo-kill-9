using UnityEngine;
using UnityEngine.EventSystems;
using SudoKill9.Combat;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Sistema integral de control en tercera persona (Over-The-Shoulder) y combate con IK de dos manos para Linux.
/// Arquitectura obligatoria:
///   PlayerRoot (este GameObject) -> rotación horizontal (Yaw) y movimiento en el plano XZ.
///   └── CameraPivot (pivotCamara) -> inclinación vertical (Pitch) con límites y sin rotación en Z.
///       └── MainCamera (camaraPrincipal) -> offset Over-The-Shoulder y protección de colisiones.
/// Preserva íntegramente el CharacterController, la gravedad y el sistema IK de dos manos (WeaponPoseProfile).
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
public class LinuxCombatIK : MonoBehaviour
{
    // ========================================================================= //
    // CONFIGURACIÓN DE CÁMARA EN TERCERA PERSONA (OVER-THE-SHOULDER)            //
    // ========================================================================= //

    [Header("1. Barras de Posición y Altura de Cámara (Play y Editor)")]
    [Tooltip("Altura principal del pivote de cámara (mueve la cámara hacia ARRIBA o hacia ABAJO respecto a los pies del jugador).")]
    [Range(-1.0f, 3.5f)]
    public float alturaPivote = 1.25f;

    [Tooltip("Ajuste fino vertical de la cámara (valores negativos bajan más la cámara, valores positivos la suben más).")]
    [Range(-2.5f, 2.5f)]
    public float offsetVerticalCamara = -0.58f;

    [Tooltip("Distancia de la cámara detrás del jugador (menor = más cerca, mayor = más lejos).")]
    [Range(0.5f, 8.0f)]
    public float distanciaCamara = 2.6f;

    [Tooltip("Desplazamiento lateral sobre el hombro (+ derecha, 0 centrado, - izquierda).")]
    [Range(-2.5f, 2.5f)]
    public float offsetLateralHombro = 0.45f;

    [Tooltip("Inclinación vertical extra de la vista en grados (negativo = mirar más arriba, positivo = mirar más abajo).")]
    [Range(-45f, 45f)]
    public float inclinacionVerticalOffset = 0f;

    [Tooltip("Campo de visión (Zoom / FOV) de la cámara de Play (60 = normal, menor = más zoom, mayor = gran angular).")]
    [Range(30f, 110f)]
    public float campoDeVisionFOV = 60f;

    [Header("2. Sensibilidad y Límites del Ratón")]
    [Tooltip("Sensibilidad para el giro horizontal del personaje (Mouse X).")]
    [Range(0.1f, 10f)]
    public float sensibilidadHorizontal = 2.0f;

    [Tooltip("Sensibilidad para la inclinación vertical del pivote de cámara (Mouse Y).")]
    [Range(0.1f, 10f)]
    public float sensibilidadVertical = 2.0f;

    [Tooltip("Ángulo mínimo de inclinación vertical en grados (mirar hacia arriba).")]
    [Range(-85f, 0f)]
    public float anguloMinimo = -40f;

    [Tooltip("Ángulo máximo de inclinación vertical en grados (mirar hacia abajo).")]
    [Range(0f, 85f)]
    public float anguloMaximo = 60f;

    [Tooltip("Tiempo de suavizado angular de la cámara (0 = respuesta inmediata sin retardo).")]
    [Range(0f, 0.3f)]
    public float suavizadoCamara = 0f;

    [Tooltip("Si está activo, invierte la dirección vertical al mover el ratón.")]
    public bool invertirEjeVertical = false;

    [Header("3. Referencias y Colisiones de Cámara")]
    [Tooltip("Transform del pivote de cámara (hijo de PlayerRoot a la altura de los hombros/cabeza). Controla únicamente el Pitch vertical.")]
    public Transform pivotCamara;

    [Tooltip("Transform de la cámara principal (hija de CameraPivot). Mantiene el desplazamiento Over-The-Shoulder.")]
    public Transform camaraPrincipal;

    [Tooltip("Acerca temporalmente la cámara con SphereCast si hay una pared u obstáculo detrás.")]
    public bool evitarColisionesCamara = true;

    [Tooltip("Radio del SphereCast para detección de colisiones de cámara.")]
    [Range(0.05f, 1.0f)]
    public float radioColisionCamara = 0.2f;

    [Tooltip("Distancia mínima entre el pivote y la cámara cuando hay un obstáculo pegado al jugador.")]
    [Range(0.1f, 2.0f)]
    public float distanciaMinimaColision = 0.35f;

    [Tooltip("Velocidad de recuperación de la distancia de cámara al despejarse un obstáculo.")]
    [Range(1f, 30f)]
    public float velocidadRecuperacionColision = 15f;

    [Tooltip("Capas físicas evaluadas por el sistema de colisión de cámara.")]
    public LayerMask capasColisionCamara = ~0;

    // ========================================================================= //
    // MOVIMIENTO Y FÍSICA DEL PERSONAJE                                         //
    // ========================================================================= //

    [Header("Movimiento y Gravedad")]
    [Tooltip("Velocidad de traslación horizontal del personaje en m/s (caminata moderada y controlable).")]
    public float velocidad = 2.7f;

    [Tooltip("Aceleración de gravedad aplicada al personaje.")]
    public float gravedad = -9.81f;

    // ========================================================================= //
    // SISTEMA PREEXISTENTE: CALIBRACIÓN DE POSES E IK DE DOS MANOS              //
    // ========================================================================= //

    [Header("Calibración de Poses de Arma")]
    [Tooltip("Saved poses, one per animation. Edited with Tools > Weapon & IK Setup Wizard.")]
    [SerializeField] private WeaponPoseProfile profile;
    [Tooltip("Editor only: which pose the Scene view shows while the game is not running.")]
    [SerializeField] private WeaponPoseKey previewPose = WeaponPoseKey.Idle;
    [Tooltip("Play mode only: hold a single pose regardless of animation, to calibrate it.")]
    [SerializeField] private bool forcePose;
    [SerializeField] private WeaponPoseKey forcedPose = WeaponPoseKey.Idle;

    [Header("Left Hand IK Target")]
    [Tooltip("Child of the weapon named LeftHand_IK_Target. Move/rotate it to align the fingers with the barrel.")]
    [SerializeField] private Transform leftHandTarget;
    [Tooltip("If on, the fields below drive the target's local transform. Turn off to move the target by hand.")]
    [SerializeField] private bool driveTargetFromFields = true;
    [SerializeField] private Vector3 gripLocalPosition = new Vector3(0.1542209f, 0.004864674f, -0.00009275189f);
    [SerializeField] private Vector3 gripLocalEuler;

    [Header("Weapon Socket")]
    [Tooltip("The socketed weapon (child of the right-hand bone).")]
    [SerializeField] private Transform weapon;
    [SerializeField] private bool driveWeaponTransform = true;

    [Header("Blending")]
    [Tooltip("How fast the weapon moves between poses. Higher = snappier.")]
    [SerializeField] private float transitionSpeed = 10f;
    [Tooltip("Fallback when the Animator has no InputX/InputZ: horizontal speed (m/s) above which RunForward is used.")]
    [SerializeField] private float movingSpeedThreshold = 0.5f;

    [Header("Animator Parameters / States")]
    [SerializeField] private string inputXParam = "InputX";
    [SerializeField] private string inputZParam = "InputZ";
    [SerializeField] private string isCrouchingParam = "IsCrouching";
    [SerializeField] private string jumpStateName = "Jump";
    [SerializeField] private string armaActivaParam = "ArmaActiva";

    [Header("Right Hand IK")]
    [Range(0f, 1f)] [SerializeField] private float rightHandWeight = 1f;

    [Header("Left Hand Weights")]
    [Range(0f, 1f)] [SerializeField] private float positionWeight = 1f;
    [Range(0f, 1f)] [SerializeField] private float rotationWeight = 1f;

    // ========================================================================= //
    // CONFIGURACIÓN DE COMBATE, APUNTADO (MIRA) Y DISPARO DE BALAS              //
    // ========================================================================= //

    [Header("Activación de Arma y Animación Camina_arma")]
    [Tooltip("Indica si el arma está actualmente activa (desenfundada). Se activa con clic derecho o clic izquierdo.")]
    public bool armaActiva = false;

    [Tooltip("Tiempo en segundos que el arma permanece activa antes de guardarse si no se vuelve a disparar o apuntar (0 = permanente).")]
    public float tiempoParaGuardarArma = 6.0f;

    [Header("Sistema de Proyectiles (Balas)")]
    [Tooltip("Transform en la punta del cañón (MuzzlePoint). Si no se asigna, se busca o crea automáticamente en el arma.")]
    public Transform muzzlePoint;

    [Header("Calibración Manual de la Salida de Bala (Muzzle)")]
    [Tooltip("Permite ajustar manualmente con barras o en Escena la posición exacta por donde salen las balas.")]
    public bool ajusteManualMuzzle = true;

    [Tooltip("Offset local respecto al fusil SCAR-H: X = Frente/Atrás, Y = Arriba/Abajo, Z = Izquierda/Derecha.")]
    public Vector3 offsetMuzzleManual = new Vector3(0.42f, 0.008f, 0f);

    [Tooltip("Rotación local del cañón (por defecto 0, 90, 0 para orientar hacia el frente del fusil).")]
    public Vector3 rotacionMuzzleManual = new Vector3(0f, 90f, 0f);

    [Tooltip("Prefab 3D opcional de la bala. Si está vacío, se genera automáticamente una bala 3D con estela y física.")]
    public GameObject prefabBala;

    [Tooltip("Velocidad de desplazamiento de la bala en metros por segundo.")]
    public float velocidadBala = 65f;

    [Tooltip("Tiempo de vida máximo del proyectil antes de autodestruirse.")]
    public float tiempoVidaBala = 3.5f;

    [Tooltip("Daño infligido al impactar sobre objetivos con IDamageable.")]
    public float danoBala = 25f;

    [Tooltip("Cadencia de disparo (máximo de balas por segundo).")]
    public float cadenciaDisparo = 5f;

    [Tooltip("Capas contra las que impacta la bala.")]
    public LayerMask capasDisparo = ~0;

    [Tooltip("Distancia máxima del rayo de puntería hacia donde mira la cámara.")]
    public float distanciaMaximaMira = 100f;

    [Header("Efectos y Audio Opcionales")]
    [Tooltip("Luz puntual para el fogonazo del disparo.")]
    public Light muzzleLight;

    [Tooltip("Efecto de partículas o chispas al impactar contra paredes o enemigos.")]
    public GameObject efectoImpactoPrefab;

    [Tooltip("Efecto de sonido al disparar.")]
    public AudioClip sonidoDisparo;

    [Tooltip("Referencia a CrosshairUI (se busca automáticamente en MainCamera).")]
    public CrosshairUI crosshairUI;

    // Variables privadas de combate y disparo
    private int armaActivaHash;
    private bool hasArmaActivaParam;
    private bool estaApuntando = false;
    private float tiempoProximoDisparo = 0f;
    private float temporizadorGuardarArma = 0f;
    private Coroutine corrutinaMuzzleLight;

    // Variables privadas de cámara y rotación acumulada
    private float yawObjetivo;
    private float yawActual;
    private float velocidadYaw;

    private float pitchObjetivo;
    private float pitchActual;
    private float velocidadPitch;

    private float rotacionBaseY = 0f;
    private float rotacionBaseZ = 0f;
    private float factorDistanciaActual = 1f;
    private bool advertenciaPivoteMostrada = false;
    private readonly RaycastHit[] bufferColisionesCamara = new RaycastHit[16];

    // Variables privadas de movimiento y física
    private CharacterController controlador;
    private Vector3 velocidadCaida;

    // Variables privadas de animación e IK
    private Animator animator;
    private Rigidbody body;
    private int inputXHash, inputZHash, crouchHash;
    private bool hasInputParams, hasCrouchParam;

    private Vector3 currentPosition;
    private Quaternion currentRotation = Quaternion.identity;
    private Vector3 currentHandOffset;

    public Transform LeftHandTarget => leftHandTarget;
    public Transform Weapon => weapon;
    public WeaponPoseProfile Profile => profile;
    public bool ArmaActiva => armaActiva;
    public bool EstaApuntando => estaApuntando;

    // Dibuja el punto de agarre y la boca del cañón en la vista de escena
    private void OnDrawGizmos()
    {
        // 1. Boca del cañón (MuzzlePoint) visible en la vista de escena
        if (muzzlePoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(muzzlePoint.position, 0.035f);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            Gizmos.DrawRay(muzzlePoint.position, muzzlePoint.forward * 0.45f);
        }

        // 2. Agarre de mano izquierda
        if (leftHandTarget == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(leftHandTarget.position, 0.03f);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(leftHandTarget.position, leftHandTarget.position + leftHandTarget.right * 0.08f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(leftHandTarget.position, leftHandTarget.position + leftHandTarget.up * 0.08f);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(leftHandTarget.position, leftHandTarget.position + leftHandTarget.forward * 0.08f);
    }

    private void OnDrawGizmosSelected()
    {
        if (muzzlePoint != null)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.75f);
            Gizmos.DrawSphere(muzzlePoint.position, 0.02f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(muzzlePoint.position, muzzlePoint.forward * 1.5f);
        }
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        body = GetComponentInParent<Rigidbody>();

        // 1. Configurar CharacterController y resolver conflictos con contenedores padres (Player_LinuxSkeleton)
        ConfigurarFisicaYJerarquia();

        // 2. Configurar y validar jerarquía PlayerRoot -> CameraPivot -> MainCamera
        ConfigurarSistemaCamara();

        // 3. Verificar y reparar referencias de arma e IK de mano izquierda (LeftHand_IK_Target)
        ValidarYRepararReferenciasArmaIK();

        // 4. Inicializar hashes del Animator
        inputXHash = Animator.StringToHash(inputXParam);
        inputZHash = Animator.StringToHash(inputZParam);
        crouchHash = Animator.StringToHash(isCrouchingParam);
        armaActivaHash = Animator.StringToHash(armaActivaParam);
        bool hasX = false, hasZ = false;
        if (animator != null)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == inputXHash && p.type == AnimatorControllerParameterType.Float) hasX = true;
                if (p.nameHash == inputZHash && p.type == AnimatorControllerParameterType.Float) hasZ = true;
                if (p.nameHash == crouchHash && p.type == AnimatorControllerParameterType.Bool) hasCrouchParam = true;
                if (p.nameHash == armaActivaHash && p.type == AnimatorControllerParameterType.Bool) hasArmaActivaParam = true;
            }
        }
        hasInputParams = hasX && hasZ;

        // 5. Configurar MuzzlePoint y CrosshairUI
        GarantizarMuzzlePoint();
        LocalizarCrosshairUI();
        if (crosshairUI != null) crosshairUI.EstaApuntando = false;
        if (animator != null && hasArmaActivaParam) animator.SetBool(armaActivaHash, armaActiva);

        // 6. Inicializar pose inicial del arma
        if (profile != null)
        {
            WeaponPoseProfile.Entry idle = profile.Get(WeaponPoseKey.Idle);
            currentPosition = idle.weaponPosition;
            currentRotation = Quaternion.Euler(idle.weaponEuler);
            currentHandOffset = idle.handOffset;
        }
        ApplyGrip();
        if (driveWeaponTransform && weapon != null)
        {
            weapon.localPosition = currentPosition;
            weapon.localRotation = currentRotation;
        }
    }

    private void OnDisable()
    {
        BloquearCursor(false);
        if (crosshairUI != null) crosshairUI.EstaApuntando = false;
    }

    private void OnDestroy()
    {
        BloquearCursor(false);
        if (crosshairUI != null) crosshairUI.EstaApuntando = false;
    }

    private void Start()
    {
        // Bloquear y ocultar el cursor al iniciar el control normal
        BloquearCursor(true);

        // Asegurar asignación y calibración del CharacterController
        if (controlador == null)
        {
            controlador = GetComponent<CharacterController>();
        }
        if (controlador != null)
        {
            controlador.height = 1.8f;
            controlador.center = new Vector3(0f, 0.9f, 0f);
            controlador.radius = 0.35f;
            controlador.skinWidth = 0.05f;
            controlador.minMoveDistance = 0f;
            controlador.stepOffset = 0.3f;
            controlador.slopeLimit = 45f;
        }

        // Verificar nuevamente el sistema de cámara e inicializar ángulos acumulados
        ConfigurarSistemaCamara();
        ValidarYRepararReferenciasArmaIK();
        GarantizarMuzzlePoint();
        LocalizarCrosshairUI();
    }

    /// <summary>
    /// Asegura que 'weapon' apunte al arma emparentada en la mano derecha y que 'leftHandTarget'
    /// apunte específicamente al hijo 'LeftHand_IK_Target' (nunca al mismo Transform raíz del arma,
    /// lo cual sobrescribiría la pose del arma con gripLocalPosition).
    /// </summary>
    public void ValidarYRepararReferenciasArmaIK()
    {
        if (animator == null) animator = GetComponent<Animator>();

        if (weapon == null && animator != null && animator.isHuman)
        {
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand != null)
            {
                foreach (Transform child in rightHand)
                {
                    if (child.name != "LeftHand_IK_Target")
                    {
                        weapon = child;
                        break;
                    }
                }
            }
        }

        if (weapon != null && (leftHandTarget == null || leftHandTarget == weapon))
        {
            Transform targetHijo = weapon.Find("LeftHand_IK_Target");
            if (targetHijo != null)
            {
                leftHandTarget = targetHijo;
                if (gripLocalPosition.sqrMagnitude < 0.00001f && targetHijo.localPosition.sqrMagnitude > 0.00001f)
                {
                    gripLocalPosition = targetHijo.localPosition;
                    gripLocalEuler = targetHijo.localEulerAngles;
                }
            }
        }
    }

    /// <summary>
    /// Calibra el CharacterController (centro y altura) para que los pies toquen exactamente el suelo
    /// y resuelve cualquier conflicto de colisiones, Rigidbodies o scripts antiguos en objetos padres.
    /// </summary>
    private void ConfigurarFisicaYJerarquia()
    {
        controlador = GetComponent<CharacterController>();
        if (controlador != null)
        {
            controlador.height = 1.8f;
            controlador.center = new Vector3(0f, 0.9f, 0f);
            controlador.radius = 0.35f;
            controlador.skinWidth = 0.05f;
            controlador.minMoveDistance = 0f;
            controlador.stepOffset = 0.3f;
            controlador.slopeLimit = 45f;
        }

        // En caso de que ReturnToMenu esté presente en un GameManager de prueba, desactivarlo
        // para que la tecla Escape libere el cursor en lugar de cerrar la escena de combate.
        var returnToMenu = Object.FindFirstObjectByType<ReturnToMenu>();
        if (returnToMenu != null)
        {
            returnToMenu.enabled = false;
        }

        if (transform.parent != null)
        {
            Transform parent = transform.parent;

            Rigidbody parentRb = parent.GetComponent<Rigidbody>();
            if (parentRb != null)
            {
                parentRb.isKinematic = true;
                parentRb.useGravity = false;
                parentRb.linearVelocity = Vector3.zero;
            }

            Collider[] parentColliders = parent.GetComponents<Collider>();
            foreach (Collider c in parentColliders)
            {
                if (c != null) c.enabled = false;
            }

            var oldCtrl = parent.GetComponent<LinuxSkeletonController>();
            if (oldCtrl != null) oldCtrl.enabled = false;

            var oldAim = parent.GetComponent<PlayerAim>();
            if (oldAim != null) oldAim.enabled = false;

            // Desvincular limpiamente para actuar como PlayerRoot independiente
            transform.SetParent(null, true);
            parent.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Verifica y garantiza la arquitectura PlayerRoot -> CameraPivot -> MainCamera.
    /// Inicializa los ángulos acumulados de Yaw y Pitch sin provocar saltos de orientación al iniciar.
    /// </summary>
    [ContextMenu("Configurar Jerarquía CameraPivot")]
    public void ConfigurarSistemaCamara()
    {
        if (anguloMinimo > anguloMaximo)
        {
            float temp = anguloMinimo;
            anguloMinimo = anguloMaximo;
            anguloMaximo = temp;
        }

        // 1. Verificar o localizar CameraPivot
        if (pivotCamara == null)
        {
            Transform encontrado = transform.Find("CameraPivot");
            if (encontrado != null)
            {
                pivotCamara = encontrado;
            }
            else
            {
                if (!advertenciaPivoteMostrada)
                {
                    Debug.LogWarning("[LinuxCombatIK] 'pivotCamara' no estaba asignado en el Inspector. Se ha creado y configurado automáticamente el objeto 'CameraPivot' como hijo del personaje.");
                    advertenciaPivoteMostrada = true;
                }

                GameObject nuevoPivote = new GameObject("CameraPivot");
                pivotCamara = nuevoPivote.transform;
                pivotCamara.SetParent(transform, false);
                pivotCamara.localPosition = new Vector3(0f, alturaPivote, 0f);
                pivotCamara.localRotation = Quaternion.identity;
                pivotCamara.localScale = Vector3.one;
            }
        }

        // Asegurar que el pivote sea hijo de PlayerRoot y esté a la altura configurada
        if (pivotCamara.parent != transform)
        {
            pivotCamara.SetParent(transform, true);
        }
        pivotCamara.localPosition = new Vector3(0f, alturaPivote, 0f);
        pivotCamara.localScale = Vector3.one;

        // 2. Verificar o localizar MainCamera como hija de CameraPivot
        if (camaraPrincipal == null && pivotCamara != null)
        {
            Camera camEnPivote = pivotCamara.GetComponentInChildren<Camera>(true);
            if (camEnPivote != null)
            {
                camaraPrincipal = camEnPivote.transform;
            }
        }

        if (camaraPrincipal == null)
        {
            Transform hijoMainCam = pivotCamara != null ? pivotCamara.Find("MainCamera") : null;
            if (hijoMainCam != null)
            {
                camaraPrincipal = hijoMainCam;
            }
            else
            {
                // Buscar si existe otra cámara en el personaje o en la escena para copiar sus ajustes
                Camera camExistente = GetComponentInChildren<Camera>(true);
                if (camExistente == null) camExistente = Camera.main;
                if (camExistente == null) camExistente = Object.FindFirstObjectByType<Camera>();

                // Si la cámara encontrada es externa al modelo FBX y estamos en ejecución, se puede emparentar;
                // si pertenece al FBX importado (como 'Camera' dentro de linux.fbx), creamos 'MainCamera' limpia bajo CameraPivot
                bool esCamaraInternaFBX = camExistente != null && camExistente.transform.IsChildOf(transform) && camExistente.transform.parent != pivotCamara;

                if (camExistente != null && !esCamaraInternaFBX && Application.isPlaying)
                {
                    camExistente.transform.SetParent(pivotCamara, false);
                    camaraPrincipal = camExistente.transform;
                }
                else
                {
                    GameObject nuevaCamGO = new GameObject("MainCamera");
                    camaraPrincipal = nuevaCamGO.transform;
                    camaraPrincipal.SetParent(pivotCamara, false);
                    nuevaCamGO.tag = "MainCamera";

                    Camera nuevaCam = nuevaCamGO.AddComponent<Camera>();
                    if (camExistente != null)
                    {
                        nuevaCam.CopyFrom(camExistente);
                    }
                    nuevaCam.nearClipPlane = 0.15f;

                    if (nuevaCamGO.GetComponent<AudioListener>() == null)
                    {
                        nuevaCamGO.AddComponent<AudioListener>();
                    }
                    if (nuevaCamGO.GetComponent<CrosshairUI>() == null)
                    {
                        nuevaCamGO.AddComponent<CrosshairUI>();
                    }

                    if (camExistente != null)
                    {
                        AudioListener oldListener = camExistente.GetComponent<AudioListener>();
                        if (oldListener != null) oldListener.enabled = false;
                        camExistente.enabled = false;
                    }
                }
            }
        }

        // Desactivar cualquier otra cámara residual hija del personaje (ej. cámaras exportadas en el FBX)
        // y desactivar controladores de cámara externos que puedan competir por la rotación
        Camera[] todasLasCamarasHijas = GetComponentsInChildren<Camera>(true);
        foreach (Camera c in todasLasCamarasHijas)
        {
            if (c != null && camaraPrincipal != null && c.transform != camaraPrincipal)
            {
                AudioListener al = c.GetComponent<AudioListener>();
                if (al != null) al.enabled = false;
                c.enabled = false;
            }
        }

        if (camaraPrincipal != null)
        {
            var oldTpCam = camaraPrincipal.GetComponent<ThirdPersonCameraController>();
            if (oldTpCam != null) oldTpCam.enabled = false;

            var oldCamCtrl = camaraPrincipal.GetComponent<CameraController>();
            if (oldCamCtrl != null) oldCamCtrl.enabled = false;

            camaraPrincipal.localScale = Vector3.one;
            camaraPrincipal.localPosition = new Vector3(offsetLateralHombro, offsetVerticalCamara, -Mathf.Abs(distanciaCamara));
            camaraPrincipal.localRotation = Quaternion.identity;

            Camera camComp = camaraPrincipal.GetComponent<Camera>();
            if (camComp != null && campoDeVisionFOV >= 20f)
            {
                camComp.fieldOfView = campoDeVisionFOV;
            }
        }

        // 3. Inicializar ángulos acumulados a partir de la orientación actual normalizada
        yawObjetivo = NormalizarAngulo(transform.eulerAngles.y);
        yawActual = yawObjetivo;

        float pitchInicial = pivotCamara != null ? NormalizarAngulo(pivotCamara.localEulerAngles.x) - inclinacionVerticalOffset : 8f;
        if (Mathf.Abs(pitchInicial) < 0.01f) pitchInicial = 8f;
        pitchObjetivo = Mathf.Clamp(pitchInicial, anguloMinimo, anguloMaximo);
        pitchActual = pitchObjetivo;

        // Mantener Y=0 y Z=0 en el pivote para impedir inclinaciones laterales (roll)
        rotacionBaseY = 0f;
        rotacionBaseZ = 0f;

        transform.rotation = Quaternion.Euler(0f, yawActual, 0f);
        if (pivotCamara != null)
        {
            float pitchConOffset = Mathf.Clamp(pitchActual + inclinacionVerticalOffset, anguloMinimo, anguloMaximo);
            pivotCamara.localRotation = Quaternion.Euler(pitchConOffset, rotacionBaseY, rotacionBaseZ);
        }

        // Bloquear y ocultar cursor al iniciar para responder inmediatamente al mouse y al teclado sin requerir clics previos
        BloquearCursor(true);
    }

    /// <summary>
    /// Aplica inmediatamente en Edit Mode y en Play Mode cualquier cambio hecho en las barras (sliders)
    /// de posición, altura, distancia, hombro, inclinación o FOV de la cámara.
    /// </summary>
    public void AplicarAjustesCamaraEnTiempoReal()
    {
        if (anguloMinimo > anguloMaximo)
        {
            anguloMinimo = anguloMaximo;
        }

        if (pivotCamara == null)
        {
            Transform encontrado = transform.Find("CameraPivot");
            if (encontrado != null) pivotCamara = encontrado;
        }

        if (camaraPrincipal == null && pivotCamara != null)
        {
            Camera camEnPivote = pivotCamara.GetComponentInChildren<Camera>(true);
            if (camEnPivote != null) camaraPrincipal = camEnPivote.transform;
        }

        if (pivotCamara != null)
        {
            pivotCamara.localPosition = new Vector3(0f, alturaPivote, 0f);
            float basePitch = Application.isPlaying ? pitchActual : 8f;
            if (!Application.isPlaying)
            {
                float currentEulerX = NormalizarAngulo(pivotCamara.localEulerAngles.x) - inclinacionVerticalOffset;
                if (Mathf.Abs(currentEulerX) > 0.01f) basePitch = currentEulerX;
            }
            float pitchConOffset = Mathf.Clamp(basePitch + inclinacionVerticalOffset, anguloMinimo, anguloMaximo);
            pivotCamara.localRotation = Quaternion.Euler(pitchConOffset, rotacionBaseY, rotacionBaseZ);
        }

        if (camaraPrincipal != null && (pivotCamara == null || camaraPrincipal.parent == pivotCamara))
        {
            float factor = Application.isPlaying ? factorDistanciaActual : 1f;
            camaraPrincipal.localPosition = new Vector3(
                offsetLateralHombro * factor,
                offsetVerticalCamara,
                -Mathf.Abs(distanciaCamara) * factor
            );
            camaraPrincipal.localRotation = Quaternion.identity;

            Camera camComp = camaraPrincipal.GetComponent<Camera>();
            if (camComp != null && campoDeVisionFOV >= 20f)
            {
                camComp.fieldOfView = campoDeVisionFOV;
            }
        }
    }

    private void OnValidate()
    {
        if (anguloMinimo > anguloMaximo)
        {
            anguloMinimo = anguloMaximo;
        }

        ValidarYRepararReferenciasArmaIK();
        AplicarAjustesCamaraEnTiempoReal();
        ApplyOffsets();

        // Sincronizar calibración manual del cañón (MuzzlePoint) en tiempo real
        if (weapon != null && ajusteManualMuzzle)
        {
            if (muzzlePoint == null)
            {
                GarantizarMuzzlePoint();
            }
            if (muzzlePoint != null && muzzlePoint.parent == weapon)
            {
                muzzlePoint.localPosition = offsetMuzzleManual;
                muzzlePoint.localRotation = Quaternion.Euler(rotacionMuzzleManual);
            }
        }
    }

    /// <summary>Modo edición: ajusta el arma y el agarre según la pose elegida para preview.</summary>
    public void ApplyOffsets()
    {
        ValidarYRepararReferenciasArmaIK();

        if (!Application.isPlaying && driveWeaponTransform && weapon != null && profile != null)
        {
            WeaponPoseProfile.Entry e = profile.Get(previewPose);
            Vector3 position = e.weaponPosition;

            if (weapon.parent != null)
                position += weapon.parent.InverseTransformVector(OffsetToWorld(e.handOffset));

            weapon.localPosition = position;
            weapon.localRotation = Quaternion.Euler(e.weaponEuler);
        }
        ApplyGrip();
    }

    private void ApplyGrip()
    {
        if (!driveTargetFromFields || leftHandTarget == null || leftHandTarget == weapon) return;
        leftHandTarget.localPosition = gripLocalPosition;
        leftHandTarget.localRotation = Quaternion.Euler(gripLocalEuler);
    }

    private void Update()
    {
        // ========================================================================= //
        // 1. GESTIÓN DE CURSOR (BLOQUEO / LIBERACIÓN CON ESCAPE Y CLIC)             //
        // ========================================================================= //
        GestionarCursor();

        // ========================================================================= //
        // 2. CONTROL DE CÁMARA Y ORIENTACIÓN CON MOUSE (YAW EN CUERPO, PITCH PIVOTE)//
        // ========================================================================= //
        ActualizarRotacionConMouse();

        // ========================================================================= //
        // 3. MOVIMIENTO HORIZONTAL CON CHARACTERCONTROLLER Y GRAVEDAD               //
        // ========================================================================= //
        ActualizarMovimiento();

        // ========================================================================= //
        // 4. LÓGICA DE APUNTADO (MIRA), DISPARO Y ACTIVACIÓN DE ARMA (Camina_arma) //
        // ========================================================================= //
        ActualizarCombateYDisparo();

        // ========================================================================= //
        // 5. LÓGICA DE COMBATE E IK DE ARMA                                         //
        // ========================================================================= //
        ActualizarPoseArmaIK();
    }

    private void LateUpdate()
    {
        // ========================================================================= //
        // 5. SEGUIMIENTO DE PIVOTE, COMPOSICIÓN OVER-THE-SHOULDER Y COLISIONES      //
        // ========================================================================= //
        ActualizarSeguimientoYColisionCamara();

        // Actualizar línea de guía láser 3D proyectada desde el cañón
        ActualizarGuiaLaser();
    }

    /// <summary>
    /// Controla el estado del cursor:
    /// - Bloqueado y oculto durante el juego normal.
    /// - Escape libera el cursor.
    /// - Clic izquierdo dentro de la ventana (cuando no se pulsa sobre UI) recupera el bloqueo.
    /// </summary>
    private void GestionarCursor()
    {
        bool escapePresionado = Input.GetKeyDown(KeyCode.Escape);
        bool clicIzquierdo = Input.GetMouseButtonDown(0);
        bool clicDerecho = Input.GetMouseButtonDown(1);

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            escapePresionado = true;
        }
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame) clicIzquierdo = true;
            if (Mouse.current.rightButton.wasPressedThisFrame) clicDerecho = true;
        }
#endif

        if (escapePresionado)
        {
            BloquearCursor(false);
        }
        else if (Cursor.lockState != CursorLockMode.Locked && (clicIzquierdo || clicDerecho))
        {
#if UNITY_EDITOR
            // En el Editor de Unity (incluyendo Unity 6 con PlayModeView), no bloquear el cursor si el foco está en SceneView, Inspector o Hierarchy.
            string nombreVentana = UnityEditor.EditorWindow.focusedWindow != null ? UnityEditor.EditorWindow.focusedWindow.GetType().Name : "";
            if (!string.IsNullOrEmpty(nombreVentana) &&
                nombreVentana != "GameView" &&
                nombreVentana != "PlayModeView" &&
                !nombreVentana.Contains("Game") &&
                !nombreVentana.Contains("Play"))
            {
                return;
            }
#endif
            bool clicSobreUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!clicSobreUI)
            {
                BloquearCursor(true);
            }
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            BloquearCursor(false);
        }
    }

    private void BloquearCursor(bool bloquear)
    {
        if (bloquear)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>
    /// Separa estrictamente la rotación horizontal (Mouse X -> eje Y de PlayerRoot)
    /// y la inclinación vertical (Mouse Y -> eje X local de CameraPivot).
    /// No multiplica Input.GetAxis("Mouse X/Y") por Time.deltaTime porque ya es delta relativo del ratón.
    /// </summary>
    private void ActualizarRotacionConMouse()
    {
        if (pivotCamara == null)
        {
            if (!advertenciaPivoteMostrada)
            {
                Debug.LogWarning("[LinuxCombatIK] 'pivotCamara' no está asignado. Asígnalo en el Inspector para habilitar la rotación vertical.");
                advertenciaPivoteMostrada = true;
            }
            return;
        }

        float mouseX = 0f;
        float mouseY = 0f;

        // Solo leer entrada de cámara cuando el cursor está bloqueado para el control del juego
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            mouseX = Input.GetAxis("Mouse X");
            mouseY = Input.GetAxis("Mouse Y");

#if ENABLE_INPUT_SYSTEM
            if (Mathf.Abs(mouseX) < 0.0001f && Mathf.Abs(mouseY) < 0.0001f && Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue() * 0.05f;
                mouseX = delta.x;
                mouseY = delta.y;
            }
#endif
        }

        AplicarEntradaRotacion(mouseX, mouseY);
    }

    /// <summary>
    /// Aplica una entrada de rotación (Mouse X, Mouse Y) separando el giro horizontal en PlayerRoot
    /// y la inclinación vertical limitada en CameraPivot.
    /// </summary>
    public void AplicarEntradaRotacion(float mouseX, float mouseY)
    {
        if (pivotCamara == null) return;

        // Acumulación horizontal estable sobre Y
        yawObjetivo = NormalizarAngulo(yawObjetivo + mouseX * sensibilidadHorizontal);

        // Acumulación vertical sobre X con soporte de inversión de eje y límites estrictos
        float factorInversion = invertirEjeVertical ? 1f : -1f;
        pitchObjetivo += mouseY * sensibilidadVertical * factorInversion;
        pitchObjetivo = Mathf.Clamp(pitchObjetivo, anguloMinimo, anguloMaximo);

        // Aplicar suavizado opcional sin oscilación al detener el mouse
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
            velocidadYaw = 0f;
            velocidadPitch = 0f;
        }

        // 1. PlayerRoot gira exclusivamente sobre el eje Y mundial (sin inclinarse en X ni Z)
        transform.rotation = Quaternion.Euler(0f, yawActual, 0f);

        // 2. CameraPivot gira exclusivamente sobre su eje X local (sin roll lateral en Z)
        pivotCamara.localRotation = Quaternion.Euler(pitchActual, rotacionBaseY, rotacionBaseZ);
    }

    /// <summary>
    /// Calcula el vector de desplazamiento horizontal (Y = 0) a partir de la orientación de la cámara
    /// proyectada sobre el plano del suelo (o de PlayerRoot como respaldo), asegurando que 'W' avance
    /// siempre hacia el frente de la pantalla, 'S' hacia el jugador, 'A' a la izquierda y 'D' a la derecha.
    /// </summary>
    public Vector3 CalcularDireccionMovimientoHorizontal(float inputHorizontal, float inputVertical)
    {
        Transform cam = camaraPrincipal != null ? camaraPrincipal : (Camera.main != null ? Camera.main.transform : null);

        Vector3 forwardHorizontal;
        Vector3 rightHorizontal;

        if (cam != null)
        {
            forwardHorizontal = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            rightHorizontal = Vector3.ProjectOnPlane(cam.right, Vector3.up);
        }
        else
        {
            forwardHorizontal = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            rightHorizontal = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        }

        if (forwardHorizontal.sqrMagnitude > 0.0001f) forwardHorizontal.Normalize();
        else forwardHorizontal = Vector3.forward;

        if (rightHorizontal.sqrMagnitude > 0.0001f) rightHorizontal.Normalize();
        else rightHorizontal = Vector3.right;

        Vector3 direccionMovimiento = forwardHorizontal * inputVertical + rightHorizontal * inputHorizontal;
        direccionMovimiento.y = 0f;

        if (direccionMovimiento.sqrMagnitude > 1f)
        {
            direccionMovimiento.Normalize();
        }

        return direccionMovimiento;
    }

    /// <summary>
    /// Desplaza al personaje en el plano horizontal (XZ) en función de la orientación de la cámara
    /// proyectada en el plano del suelo, nunca de la inclinación vertical ni de ejes fijos mundiales.
    /// </summary>
    private void ActualizarMovimiento()
    {
        float inputHorizontal = Input.GetAxis("Horizontal");
        float inputVertical = Input.GetAxis("Vertical");

#if ENABLE_INPUT_SYSTEM
        if (Mathf.Abs(inputHorizontal) < 0.0001f && Mathf.Abs(inputVertical) < 0.0001f && Keyboard.current != null)
        {
            Keyboard kb = Keyboard.current;
            inputHorizontal = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) -
                              (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            inputVertical = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) -
                            (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
        }
#endif

        Vector3 direccionMovimiento = CalcularDireccionMovimientoHorizontal(inputHorizontal, inputVertical);

        if (controlador != null)
        {
            if (controlador.isGrounded)
            {
                if (velocidadCaida.y < 0f)
                {
                    velocidadCaida.y = -2f;
                }
            }
            else
            {
                velocidadCaida.y += gravedad * Time.deltaTime;
            }

            Vector3 finalMove = (direccionMovimiento * velocidad) + velocidadCaida;
            controlador.Move(finalMove * Time.deltaTime);
        }

        if (animator != null && hasInputParams)
        {
            // Proyectar el vector de movimiento al espacio local del personaje para que el Blend Tree
            // reciba los valores de desplazamiento lateral (X) y avance/retroceso (Z) relativos al cuerpo
            Vector3 dirLocal = transform.InverseTransformDirection(direccionMovimiento);
            animator.SetFloat(inputXHash, dirLocal.x);
            animator.SetFloat(inputZHash, dirLocal.z);
        }
    }

    /// <summary>
    /// Mantiene estable el CameraPivot y posiciona la MainCamera con encuadre Over-The-Shoulder,
    /// acercándola temporalmente mediante SphereCast si una pared u obstáculo se interpone.
    /// </summary>
    private void ActualizarSeguimientoYColisionCamara()
    {
        if (pivotCamara == null) return;

        // Fijar posición y rotación local limpias del pivote
        pivotCamara.localPosition = new Vector3(0f, alturaPivote, 0f);
        float pitchConOffset = Mathf.Clamp(pitchActual + inclinacionVerticalOffset, anguloMinimo, anguloMaximo);
        pivotCamara.localRotation = Quaternion.Euler(pitchConOffset, rotacionBaseY, rotacionBaseZ);

        if (camaraPrincipal == null) return;

        Vector3 offsetLocalDeseado = new Vector3(offsetLateralHombro, offsetVerticalCamara, -Mathf.Abs(distanciaCamara));
        float factorObjetivo = 1f;

        if (evitarColisionesCamara)
        {
            Vector3 origenPivote = pivotCamara.position;
            Vector3 destinoMundoDeseado = pivotCamara.TransformPoint(offsetLocalDeseado);
            Vector3 vectorHaciaCamara = destinoMundoDeseado - origenPivote;
            float distanciaTotal = vectorHaciaCamara.magnitude;

            if (distanciaTotal > 0.001f)
            {
                Vector3 direccion = vectorHaciaCamara / distanciaTotal;
                int numImpactos = Physics.SphereCastNonAlloc(
                    origenPivote,
                    radioColisionCamara,
                    direccion,
                    bufferColisionesCamara,
                    distanciaTotal,
                    capasColisionCamara,
                    QueryTriggerInteraction.Ignore
                );

                float distanciaMasCercana = distanciaTotal;
                bool obstaculoDetectado = false;

                for (int i = 0; i < numImpactos; i++)
                {
                    RaycastHit hit = bufferColisionesCamara[i];
                    if (hit.collider == null) continue;

                    // Ignorar el propio CharacterController y cualquier collider hijo del jugador (armas, huesos, etc.)
                    if (hit.collider == controlador || hit.collider.transform.IsChildOf(transform))
                        continue;

                    // Si el usuario baja la cámara cerca del piso, no tratar el suelo horizontal bajo los pies como una pared trasera
                    if (hit.normal.y > 0.75f && hit.point.y <= transform.position.y + 0.15f)
                        continue;

                    if (hit.distance < distanciaMasCercana)
                    {
                        distanciaMasCercana = hit.distance;
                        obstaculoDetectado = true;
                    }
                }

                if (obstaculoDetectado)
                {
                    float limiteMinimo = Mathf.Min(distanciaMinimaColision, distanciaTotal);
                    float distanciaSegura = Mathf.Clamp(distanciaMasCercana - 0.05f, limiteMinimo, distanciaTotal);
                    factorObjetivo = Mathf.Clamp01(distanciaSegura / distanciaTotal);
                }
            }
        }

        // Si hay colisión, acercar de inmediato para no atravesar geometría; al despejarse, recuperar suavemente
        if (factorObjetivo < factorDistanciaActual)
        {
            factorDistanciaActual = factorObjetivo;
        }
        else
        {
            factorDistanciaActual = Mathf.Lerp(
                factorDistanciaActual,
                factorObjetivo,
                1f - Mathf.Exp(-velocidadRecuperacionColision * Time.deltaTime)
            );
        }

        // Mantener offsetVerticalCamara exacto para que subir/bajar la cámara siempre responda al 100%
        camaraPrincipal.localPosition = new Vector3(
            offsetLateralHombro * factorDistanciaActual,
            offsetVerticalCamara,
            -Mathf.Abs(distanciaCamara) * factorDistanciaActual
        );
        camaraPrincipal.localRotation = Quaternion.identity;

        Camera camComp = camaraPrincipal.GetComponent<Camera>();
        if (camComp != null && campoDeVisionFOV >= 20f && !Mathf.Approximately(camComp.fieldOfView, campoDeVisionFOV))
        {
            camComp.fieldOfView = campoDeVisionFOV;
        }
    }

    private static float NormalizarAngulo(float angulo)
    {
        angulo %= 360f;
        if (angulo > 180f) angulo -= 360f;
        if (angulo < -180f) angulo += 360f;
        return angulo;
    }

    // ========================================================================= //
    // LÓGICA DE APUNTADO (MIRA), ACTIVACIÓN DE ARMA Y DISPARO DE BALAS          //
    // ========================================================================= //

    /// <summary>
    /// Maneja la activación del arma, el apuntado (clic derecho) que enciende la mira,
    /// y el disparo de proyectiles (clic izquierdo) desde la punta del arma hacia el punto de mira.
    /// Ambos clics activan el arma dando paso a la animación 'Camina_arma'.
    /// </summary>
    private void ActualizarCombateYDisparo()
    {
        bool clicDerecho = Input.GetMouseButton(1);
        bool clicIzquierdoPresionado = Input.GetMouseButtonDown(0);
        bool clicIzquierdoMantenido = Input.GetMouseButton(0);

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            if (Mouse.current.rightButton.isPressed) clicDerecho = true;
            if (Mouse.current.leftButton.wasPressedThisFrame) clicIzquierdoPresionado = true;
            if (Mouse.current.leftButton.isPressed) clicIzquierdoMantenido = true;
        }
#endif

        // 1. Apuntado con Clic Derecho (activa la mira en forma de cruz exclusivamente mientras se apunta)
        estaApuntando = clicDerecho;

        if (crosshairUI == null) LocalizarCrosshairUI();
        if (crosshairUI != null)
        {
            crosshairUI.SoloAlApuntar = true;
            crosshairUI.EstaApuntando = estaApuntando;
        }

        // Si se presiona clic derecho (apuntar) o clic izquierdo (disparar): activar el arma y la animación Camina_arma
        if (clicDerecho || clicIzquierdoPresionado || clicIzquierdoMantenido)
        {
            if (!armaActiva)
            {
                ActivarArma(true);
            }
            temporizadorGuardarArma = tiempoParaGuardarArma;
        }

        // 2. Disparo con Clic Izquierdo (lanza bala física desde la punta del arma hacia el centro de la mira)
        if (clicIzquierdoPresionado || (clicIzquierdoMantenido && Time.time >= tiempoProximoDisparo))
        {
            if (Time.time >= tiempoProximoDisparo)
            {
                DispararBala();
                tiempoProximoDisparo = Time.time + (1f / Mathf.Max(0.1f, cadenciaDisparo));
                temporizadorGuardarArma = tiempoParaGuardarArma;
            }
        }

        // 3. Temporizador opcional para enfundar el arma automáticamente tras inactividad
        if (tiempoParaGuardarArma > 0f && armaActiva && !estaApuntando && !clicIzquierdoMantenido)
        {
            temporizadorGuardarArma -= Time.deltaTime;
            if (temporizadorGuardarArma <= 0f)
            {
                ActivarArma(false);
            }
        }

        // 4. Zoom sutil al apuntar para dar sensación cinemática
        if (camaraPrincipal != null)
        {
            Camera camComp = camaraPrincipal.GetComponent<Camera>();
            if (camComp != null && campoDeVisionFOV >= 20f)
            {
                float fovObjetivo = estaApuntando ? Mathf.Max(25f, campoDeVisionFOV - 10f) : campoDeVisionFOV;
                camComp.fieldOfView = Mathf.Lerp(camComp.fieldOfView, fovObjetivo, 14f * Time.deltaTime);
            }
        }
    }

    /// <summary>
    /// Activa o desactiva el estado del arma, sincronizando el parámetro 'ArmaActiva'
    /// en el Animator para transicionar hacia o desde la animación 'Camina_arma'.
    /// </summary>
    public void ActivarArma(bool activar)
    {
        armaActiva = activar;

        if (animator != null && hasArmaActivaParam)
        {
            animator.SetBool(armaActivaHash, activar);
        }

        if (weapon != null)
        {
            weapon.gameObject.SetActive(true);
        }

        if (activar)
        {
            temporizadorGuardarArma = tiempoParaGuardarArma;
        }
        else
        {
            estaApuntando = false;
            if (crosshairUI != null)
            {
                crosshairUI.EstaApuntando = false;
            }
        }
    }

    /// <summary>
    /// Instancia y dispara una bala desde la punta del cañón (MuzzlePoint) hacia el punto
    /// exacto enfocado por la mira en el centro de la pantalla.
    /// Preserva colisiones con todo el entorno (incluida la Capa 0 / Default) y excluye únicamente al propio personaje.
    /// </summary>
    /// <summary>
    /// Calcula el punto exacto enfocado por la mira en el mundo 3D (puntoObjetivo).
    /// Garantiza que el raycast ignore cualquier superficie u obstáculo que quede DETRÁS del cañón del arma
    /// (como el suelo o paredes traseras bajo la cámara), evitando disparos hacia atrás o desviaciones de paralaje.
    /// </summary>
    public Vector3 CalcularPuntoObjetivoMira(out Ray rayoCamara)
    {
        Camera cam = camaraPrincipal != null ? camaraPrincipal.GetComponent<Camera>() : Camera.main;
        if (crosshairUI == null) LocalizarCrosshairUI();

        Vector2 vp = crosshairUI != null ? crosshairUI.ObtenerPosicionViewport() : new Vector2(0.5f, 0.5f);
        if (cam != null)
        {
            rayoCamara = cam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
        }
        else
        {
            rayoCamara = new Ray(transform.position + Vector3.up * 1.4f, transform.forward);
        }

        GarantizarMuzzlePoint();
        Vector3 muzzlePos = muzzlePoint != null ? muzzlePoint.position : (transform.position + Vector3.up * 1.2f + transform.forward * 0.4f);
        Vector3 forwardXZ = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forwardXZ.sqrMagnitude < 0.001f) forwardXZ = transform.forward;

        // Distancia mínima requerida a lo largo del rayo para estar delante del cañón del jugador
        float distMinimaAlFrente = Vector3.Dot(muzzlePos - rayoCamara.origin, rayoCamara.direction);

        RaycastHit[] hits = Physics.RaycastAll(rayoCamara, distanciaMaximaMira, capasDisparo.value, QueryTriggerInteraction.Collide);
        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            Transform miRaiz = transform.root;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null) continue;
                if (h.collider.transform.root == miRaiz || h.collider.transform == transform) continue;

                // El impacto debe estar estrictamente DELANTE del jugador/cañón (no detrás del jugador)
                Vector3 toHit = h.point - muzzlePos;
                bool estaAlFrente = (Vector3.Dot(toHit, forwardXZ) > -0.1f || Vector3.Dot(h.point - transform.position, forwardXZ) > 0.05f) 
                                 && h.distance > (distMinimaAlFrente - 0.25f);
                if (estaAlFrente)
                {
                    return h.point;
                }
            }
        }

        // Si no hay impacto al frente, proyectar hacia el infinito a lo largo de la línea de la mira
        return rayoCamara.origin + rayoCamara.direction * distanciaMaximaMira;
    }

    /// <summary>
    /// Instancia y dispara una bala desde la punta del cañón (MuzzlePoint) hacia el punto
    /// exacto enfocado por la mira en la pantalla.
    /// Preserva colisiones con todo el entorno (incluida la Capa 0 / Default) y excluye únicamente al propio personaje.
    /// </summary>
    public void DispararBala()
    {
        GarantizarMuzzlePoint();
        if (muzzlePoint == null) return;

        // 1. Obtener el punto objetivo exacto de la mira (filtrando cualquier obstáculo detrás del jugador)
        Ray rayoCamara;
        Vector3 puntoObjetivo = CalcularPuntoObjetivoMira(out rayoCamara);

        // 2. Dirección de la bala desde MuzzlePoint hacia puntoObjetivo
        Vector3 direccionDisparo = (puntoObjetivo - muzzlePoint.position);
        Vector3 forwardXZ = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forwardXZ.sqrMagnitude < 0.001f) forwardXZ = transform.forward;

        if (direccionDisparo.sqrMagnitude < 0.01f || Vector3.Dot(direccionDisparo.normalized, forwardXZ) < 0.05f)
        {
            direccionDisparo = rayoCamara.direction;
        }
        else
        {
            direccionDisparo.Normalize();
        }

        // 3. Crear y lanzar el proyectil (bala) excluyendo al propio tirador
        BalaProyectil.Disparar(
            prefabBala,
            muzzlePoint.position,
            direccionDisparo,
            velocidadBala,
            danoBala,
            tiempoVidaBala,
            capasDisparo,
            efectoImpactoPrefab,
            transform.root
        );

        // Notificar a la mira para activar la animación de retroceso dinámico
        if (crosshairUI != null)
        {
            crosshairUI.NotificarDisparo();
        }

        // 4. Trazador de disparo instantáneo que conecta el cañón con el objetivo
        MostrarTrazadorDisparo(muzzlePoint.position, puntoObjetivo);

        // 5. Efecto de fogonazo en la boca del cañón (Muzzle Flash)
        DispararFogonazo();

        // 6. Sonido de disparo si está asignado
        if (sonidoDisparo != null)
        {
            AudioSource.PlayClipAtPoint(sonidoDisparo, muzzlePoint.position, 1f);
        }
    }

    private LineRenderer trazadorDisparo;

    private void MostrarTrazadorDisparo(Vector3 origen, Vector3 destino)
    {
        if (trazadorDisparo == null)
        {
            trazadorDisparo = gameObject.AddComponent<LineRenderer>();
            trazadorDisparo.positionCount = 2;
            trazadorDisparo.startWidth = 0.035f;
            trazadorDisparo.endWidth = 0.01f;
            trazadorDisparo.useWorldSpace = true;
            trazadorDisparo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trazadorDisparo.receiveShadows = false;
            trazadorDisparo.generateLightingData = false;

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (unlit != null)
            {
                Material mat = new Material(unlit);
                trazadorDisparo.material = mat;
            }
            trazadorDisparo.startColor = new Color(1f, 0.95f, 0.4f, 1f);
            trazadorDisparo.endColor = new Color(1f, 0.5f, 0.1f, 0f);
            trazadorDisparo.enabled = false;
        }

        trazadorDisparo.SetPosition(0, origen);
        trazadorDisparo.SetPosition(1, destino);
        trazadorDisparo.enabled = true;
        CancelInvoke(nameof(OcultarTrazador));
        Invoke(nameof(OcultarTrazador), 0.06f);
    }

    private void OcultarTrazador()
    {
        if (trazadorDisparo != null) trazadorDisparo.enabled = false;
    }

    private LineRenderer guiaLaserLine;

    /// <summary>
    /// Dibuja una línea láser tridimensional desde la boca del cañón hacia el punto de mira exacto
    /// cuando el jugador está apuntando y la opción está habilitada en CrosshairUI.
    /// </summary>
    private void ActualizarGuiaLaser()
    {
        bool debeMostrar = estaApuntando && crosshairUI != null && crosshairUI.activarGuiaLaser && muzzlePoint != null;
        if (!debeMostrar)
        {
            if (guiaLaserLine != null && guiaLaserLine.enabled)
            {
                guiaLaserLine.enabled = false;
            }
            return;
        }

        if (guiaLaserLine == null)
        {
            guiaLaserLine = gameObject.AddComponent<LineRenderer>();
            guiaLaserLine.positionCount = 2;
            guiaLaserLine.startWidth = 0.012f;
            guiaLaserLine.endWidth = 0.006f;
            guiaLaserLine.useWorldSpace = true;
            guiaLaserLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            guiaLaserLine.receiveShadows = false;
            guiaLaserLine.generateLightingData = false;

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (unlit != null)
            {
                Material mat = new Material(unlit);
                mat.color = new Color(1f, 0.2f, 0.2f, 0.85f);
                guiaLaserLine.material = mat;
            }
            guiaLaserLine.startColor = new Color(1f, 0.25f, 0.25f, 0.9f);
            guiaLaserLine.endColor = new Color(1f, 0.1f, 0.1f, 0.35f);
        }

        guiaLaserLine.enabled = true;
        Ray rayoCamara;
        Vector3 destino = CalcularPuntoObjetivoMira(out rayoCamara);

        guiaLaserLine.SetPosition(0, muzzlePoint.position);
        guiaLaserLine.SetPosition(1, destino);
    }

    private void DispararFogonazo()
    {
        GarantizarLuzFogonazo();
        if (muzzleLight == null) return;
        if (corrutinaMuzzleLight != null) StopCoroutine(corrutinaMuzzleLight);
        corrutinaMuzzleLight = StartCoroutine(EfectoMuzzleFlash());
    }

    private void GarantizarLuzFogonazo()
    {
        if (muzzlePoint == null) return;
        if (muzzleLight == null)
        {
            Light l = muzzlePoint.GetComponentInChildren<Light>();
            if (l == null)
            {
                GameObject luzObj = new GameObject("MuzzleFlashLight");
                luzObj.transform.SetParent(muzzlePoint, false);
                l = luzObj.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.9f, 0.4f);
                l.range = 3.5f;
                l.intensity = 5f;
                l.enabled = false;
            }
            muzzleLight = l;
        }
    }

    private System.Collections.IEnumerator EfectoMuzzleFlash()
    {
        if (muzzleLight == null) yield break;
        muzzleLight.enabled = true;
        yield return new WaitForSeconds(0.04f);
        if (muzzleLight != null) muzzleLight.enabled = false;
    }

    /// <summary>
    /// Busca o crea el objeto MuzzlePoint exactamente en la punta frontal del cañón del arma (hijo directo de weapon).
    /// Si 'ajusteManualMuzzle' está activo, respeta escrupulosamente los valores manuales configurados por el usuario.
    /// </summary>
    public void GarantizarMuzzlePoint()
    {
        if (weapon != null)
        {
            Vector3 posDeseada = ajusteManualMuzzle ? offsetMuzzleManual : new Vector3(0.42f, 0.008f, 0f);
            Quaternion rotDeseada = Quaternion.Euler(rotacionMuzzleManual);

            if (!ajusteManualMuzzle && leftHandTarget != null && leftHandTarget.parent == weapon)
            {
                Vector3 dirGrip = leftHandTarget.localPosition;
                if (dirGrip.sqrMagnitude > 0.001f)
                {
                    posDeseada = dirGrip + dirGrip.normalized * 0.26f;
                }
            }

            Transform encontrado = weapon.Find("MuzzlePoint");
            if (encontrado == null)
            {
                // Buscar si existía en otro hijo
                foreach (Transform t in weapon.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && t.name == "MuzzlePoint" && t != weapon)
                    {
                        encontrado = t;
                        break;
                    }
                }
            }

            if (encontrado != null)
            {
                muzzlePoint = encontrado;
                if (muzzlePoint.parent != weapon)
                {
                    muzzlePoint.SetParent(weapon, false);
                }

                if (ajusteManualMuzzle)
                {
                    muzzlePoint.localPosition = offsetMuzzleManual;
                    muzzlePoint.localRotation = rotDeseada;
                }
                else
                {
                    muzzlePoint.localPosition = posDeseada;
                    muzzlePoint.localRotation = rotDeseada;
                    offsetMuzzleManual = posDeseada;
                }

                GarantizarLuzFogonazo();
                return;
            }

            GameObject nuevoMuzzle = new GameObject("MuzzlePoint");
            nuevoMuzzle.transform.SetParent(weapon, false);
            nuevoMuzzle.transform.localPosition = posDeseada;
            nuevoMuzzle.transform.localRotation = rotDeseada;
            muzzlePoint = nuevoMuzzle.transform;
            GarantizarLuzFogonazo();
            return;
        }

        if (muzzlePoint != null) return;

        // Fallback en mano derecha si weapon no está asignado
        if (animator != null && animator.isHuman)
        {
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand != null)
            {
                Transform encontrado = rightHand.Find("MuzzlePoint");
                if (encontrado != null)
                {
                    muzzlePoint = encontrado;
                    return;
                }
                GameObject nuevoMuzzle = new GameObject("MuzzlePoint");
                nuevoMuzzle.transform.SetParent(rightHand, false);
                nuevoMuzzle.transform.localPosition = new Vector3(0f, 0.15f, 0.55f);
                nuevoMuzzle.transform.localRotation = Quaternion.identity;
                muzzlePoint = nuevoMuzzle.transform;
                GarantizarLuzFogonazo();
                return;
            }
        }

        Transform encontradoEnCuerpo = transform.Find("MuzzlePoint");
        if (encontradoEnCuerpo != null)
        {
            muzzlePoint = encontradoEnCuerpo;
        }
        else
        {
            GameObject nuevoMuzzle = new GameObject("MuzzlePoint");
            nuevoMuzzle.transform.SetParent(transform, false);
            nuevoMuzzle.transform.localPosition = new Vector3(0.25f, 1.2f, 0.5f);
            muzzlePoint = nuevoMuzzle.transform;
            GarantizarLuzFogonazo();
        }
    }

    /// <summary>
    /// Localiza la referencia a CrosshairUI en la cámara principal o en la escena y garantiza
    /// que solo se active durante el apuntado con clic derecho.
    /// </summary>
    public void LocalizarCrosshairUI()
    {
        if (crosshairUI == null && camaraPrincipal != null) crosshairUI = camaraPrincipal.GetComponent<CrosshairUI>();
        if (crosshairUI == null) crosshairUI = GetComponentInChildren<CrosshairUI>(true);
        if (crosshairUI == null) crosshairUI = Object.FindFirstObjectByType<CrosshairUI>();

        if (crosshairUI != null)
        {
            crosshairUI.SoloAlApuntar = true;
        }
    }

    // ========================================================================= //
    // SISTEMA PREEXISTENTE DE POSES E IK DE DOS MANOS (INTACTO)                 //
    // ========================================================================= //

    private void ActualizarPoseArmaIK()
    {
        if (!driveWeaponTransform || weapon == null || profile == null) return;

        Vector3 targetPosition, targetHand;
        Quaternion targetRotation;
        ComputeTargetPose(out targetPosition, out targetRotation, out targetHand);

        // Interpolación independiente de la tasa de cuadros
        float t = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);
        currentPosition = Vector3.Lerp(currentPosition, targetPosition, t);
        currentRotation = Quaternion.Slerp(currentRotation, targetRotation, t);
        currentHandOffset = Vector3.Lerp(currentHandOffset, targetHand, t);

        weapon.localPosition = currentPosition;
        weapon.localRotation = currentRotation;
    }

    // Calcula y mezcla la pose del arma según la acción actual del personaje
    private void ComputeTargetPose(out Vector3 position, out Quaternion rotation, out Vector3 hand)
    {
        float[] w = new float[7];

        if (forcePose)
        {
            w[(int)forcedPose] = 1f;
        }
        else if (hasCrouchParam && animator.GetBool(crouchHash))
        {
            w[(int)WeaponPoseKey.Crouch] = 1f;
        }
        else if (IsInJumpState())
        {
            w[(int)WeaponPoseKey.Jump] = 1f;
        }
        else if (hasInputParams)
        {
            float x = animator.GetFloat(inputXHash);
            float z = animator.GetFloat(inputZHash);
            float magnitude = Mathf.Clamp01(new Vector2(x, z).magnitude);

            float forward = Mathf.Max(0f, z), backward = Mathf.Max(0f, -z);
            float right = Mathf.Max(0f, x), left = Mathf.Max(0f, -x);
            float sum = forward + backward + right + left;

            if (sum < 1e-4f)
            {
                w[(int)WeaponPoseKey.Idle] = 1f;
            }
            else
            {
                float scale = magnitude / sum;
                w[(int)WeaponPoseKey.RunForward] = forward * scale;
                w[(int)WeaponPoseKey.RunBackward] = backward * scale;
                w[(int)WeaponPoseKey.RunRight] = right * scale;
                w[(int)WeaponPoseKey.RunLeft] = left * scale;
                w[(int)WeaponPoseKey.Idle] = 1f - magnitude;
            }
        }
        else
        {
            bool moving = false;
            if (controlador != null)
            {
                moving = new Vector2(controlador.velocity.x, controlador.velocity.z).magnitude > movingSpeedThreshold;
            }
            else if (body != null)
            {
                moving = new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude > movingSpeedThreshold;
            }
            w[(int)(moving ? WeaponPoseKey.RunForward : WeaponPoseKey.Idle)] = 1f;
        }

        position = Vector3.zero;
        hand = Vector3.zero;
        Quaternion first = Quaternion.identity;
        Vector4 q = Vector4.zero;
        bool haveFirst = false;
        float total = 0f;

        for (int i = 0; i < w.Length; i++)
        {
            if (w[i] <= 1e-5f) continue;
            WeaponPoseProfile.Entry e = profile.Get((WeaponPoseKey)i);
            Quaternion r = Quaternion.Euler(e.weaponEuler);
            if (!haveFirst) { first = r; haveFirst = true; }
            if (Quaternion.Dot(r, first) < 0f) r = new Quaternion(-r.x, -r.y, -r.z, -r.w);

            position += e.weaponPosition * w[i];
            hand += e.handOffset * w[i];
            q += new Vector4(r.x, r.y, r.z, r.w) * w[i];
            total += w[i];
        }

        if (total < 1e-5f)
        {
            WeaponPoseProfile.Entry idle = profile.Get(WeaponPoseKey.Idle);
            position = idle.weaponPosition;
            hand = idle.handOffset;
            rotation = Quaternion.Euler(idle.weaponEuler);
            return;
        }

        position /= total;
        hand /= total;
        float len = q.magnitude;
        rotation = len > 1e-5f ? new Quaternion(q.x / len, q.y / len, q.z / len, q.w / len) : first;
    }

    private bool IsInJumpState()
    {
        if (string.IsNullOrEmpty(jumpStateName)) return false;
        return animator.GetCurrentAnimatorStateInfo(0).IsName(jumpStateName) ||
               (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(jumpStateName));
    }

    // Convierte el offset local a un vector en espacio mundial
    private Vector3 OffsetToWorld(Vector3 offset)
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        return right * offset.x + Vector3.up * offset.y + forward * offset.z;
    }

    // Callback de IK del Animator (única definición)
    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) animator = GetComponent<Animator>();

        // 1. Apuntado de torso superior, hombros y cabeza hacia la mira configurada
        if (animator != null && animator.isHuman)
        {
            float pesoLook = estaApuntando ? 0.75f : (armaActiva ? 0.45f : 0f);
            if (pesoLook > 0.01f && camaraPrincipal != null)
            {
                Ray rayoCamara;
                Vector3 lookTarget = CalcularPuntoObjetivoMira(out rayoCamara);
                animator.SetLookAtWeight(pesoLook, 0.35f, 0.75f, 0.3f, 0.5f);
                animator.SetLookAtPosition(lookTarget);
            }
            else
            {
                animator.SetLookAtWeight(0f);
            }
        }

        Vector3 reach = OffsetToWorld(currentHandOffset);

        // Mano derecha: desplaza el hueso hacia adelante manteniendo orientación
        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (rightHand != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, rightHandWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightHand.position + reach);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightHand.rotation);
        }

        // Mano izquierda: fija la mano en el agarre del cañón (LeftHand_IK_Target)
        if (leftHandTarget == null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            return;
        }

        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, positionWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, rotationWeight);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget.position + reach);
        animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandTarget.rotation);
    }

    public float YawActual => yawActual;
    public float PitchActual => pitchActual;

    /// <summary>
    /// Ejecuta las 6 pruebas obligatorias de calidad sobre la jerarquía real de cámara y restaura el estado inicial.
    /// </summary>
    [ContextMenu("Ejecutar Pruebas de Calidad (1-6)")]
    public bool EjecutarPruebasCalidadCamara()
    {
        ConfigurarSistemaCamara();
        ValidarYRepararReferenciasArmaIK();

        if (pivotCamara == null || camaraPrincipal == null)
        {
            Debug.LogError("[LinuxCombatIK - Pruebas] Fallo: pivotCamara o camaraPrincipal es nulo.");
            return false;
        }

        // Guardar estado original para restaurarlo al finalizar
        Quaternion rotOriginalRoot = transform.rotation;
        Quaternion rotOriginalPivot = pivotCamara.localRotation;
        float yawObjOrig = yawObjetivo, yawActOrig = yawActual;
        float pitchObjOrig = pitchObjetivo, pitchActOrig = pitchActual;
        float suavizadoOrig = suavizadoCamara;
        suavizadoCamara = 0f;

        bool exito = true;

        // Prueba 1: Movimiento vertical brusco (+500 y -500 en Mouse Y)
        AplicarEntradaRotacion(0f, 500f);
        bool p1Up = Mathf.Approximately(pitchActual, anguloMinimo) &&
                    Mathf.Abs(NormalizarAngulo(transform.eulerAngles.x)) < 0.001f &&
                    Mathf.Abs(NormalizarAngulo(transform.eulerAngles.z)) < 0.001f;
        AplicarEntradaRotacion(0f, -1000f);
        bool p1Down = Mathf.Approximately(pitchActual, anguloMaximo) &&
                      Mathf.Abs(NormalizarAngulo(transform.eulerAngles.x)) < 0.001f &&
                      Mathf.Abs(NormalizarAngulo(transform.eulerAngles.z)) < 0.001f;
        exito &= (p1Up && p1Down);

        // Prueba 2: Movimiento horizontal (giros rápidos izquierda/derecha)
        float yawAntes = yawActual;
        AplicarEntradaRotacion(45f, 0f);
        bool p2 = !Mathf.Approximately(yawActual, yawAntes) &&
                  Mathf.Abs(NormalizarAngulo(pivotCamara.localEulerAngles.z)) < 0.001f &&
                  Mathf.Abs(NormalizarAngulo(transform.eulerAngles.x)) < 0.001f;
        exito &= p2;

        // Prueba 3: Movimiento combinado (Mouse X/Y + WASD con cámara inclinada al máximo)
        AplicarEntradaRotacion(30f, -200f);
        Vector3 dirCombinada = CalcularDireccionMovimientoHorizontal(1f, 1f);
        bool p3 = Mathf.Abs(dirCombinada.y) < 0.0001f && Mathf.Approximately(dirCombinada.magnitude, 1f);
        exito &= p3;

        // Prueba 4: Límites de cámara (intentos repetidos de superar anguloMinimo y anguloMaximo)
        for (int i = 0; i < 20; i++) AplicarEntradaRotacion(0f, 100f);
        bool p4Min = pitchActual >= anguloMinimo - 0.001f && pitchActual <= anguloMaximo + 0.001f;
        for (int i = 0; i < 20; i++) AplicarEntradaRotacion(0f, -100f);
        bool p4Max = pitchActual >= anguloMinimo - 0.001f && pitchActual <= anguloMaximo + 0.001f;
        exito &= (p4Min && p4Max);

        // Prueba 5: Estabilidad (sin entrada de mouse la orientación permanece invariable)
        float yawEstable = yawActual;
        float pitchEstable = pitchActual;
        AplicarEntradaRotacion(0f, 0f);
        bool p5 = Mathf.Approximately(yawActual, yawEstable) &&
                  Mathf.Approximately(pitchActual, pitchEstable) &&
                  camaraPrincipal.localRotation == Quaternion.identity;
        exito &= p5;

        // Prueba 6: Sistemas preexistentes (IK de dos manos con LeftHand_IK_Target separado de weapon, Profile y CharacterController)
        bool p6 = GetComponent<Animator>() != null &&
                  GetComponent<CharacterController>() != null &&
                  profile != null &&
                  weapon != null &&
                  leftHandTarget != null &&
                  leftHandTarget != weapon;
        exito &= p6;

        // Restaurar estado original
        suavizadoCamara = suavizadoOrig;
        yawObjetivo = yawObjOrig;
        yawActual = yawActOrig;
        pitchObjetivo = pitchObjOrig;
        pitchActual = pitchActOrig;
        transform.rotation = rotOriginalRoot;
        pivotCamara.localRotation = rotOriginalPivot;

        Debug.Log($"<color={(exito ? "green" : "red")}>[LinuxCombatIK - Verificación Cámara 1-6]</color> " +
                  $"P1(Vertical Brusco)={p1Up && p1Down}, P2(Horizontal)={p2}, P3(Combinado XZ)={p3}, " +
                  $"P4(Límites [{anguloMinimo},{anguloMaximo}])={p4Min && p4Max}, P5(Estabilidad)={p5}, P6(IK/Física)={p6}");
        return exito;
    }

    /// <summary>
    /// Pruebas automatizadas de calidad para el sistema de combate, activación de arma (Camina_arma),
    /// disparo, físicas de proyectil, y entrega de daño en Capa 0 (Default).
    /// </summary>
    [ContextMenu("Ejecutar Pruebas de Combate y Disparo (7-12)")]
    public bool EjecutarPruebasCalidadCombate()
    {
        bool exito = true;

        // 7. Prueba MuzzlePoint: garantizar que la punta del cañón existe y está orientada hacia adelante
        GarantizarMuzzlePoint();
        bool p7 = muzzlePoint != null && (weapon == null || muzzlePoint.parent == weapon);
        exito &= p7;

        // 8. Prueba Activación de Arma (Camina_arma): toggle de armaActiva y parámetro de Animator
        bool estadoPrevio = armaActiva;
        ActivarArma(true);
        bool p8Activar = armaActiva && (!hasArmaActivaParam || animator.GetBool(armaActivaHash));
        ActivarArma(false);
        bool p8Desactivar = !armaActiva && (!hasArmaActivaParam || !animator.GetBool(armaActivaHash));
        ActivarArma(estadoPrevio);
        bool p8 = p8Activar && p8Desactivar;
        exito &= p8;

        // 9. Prueba CrosshairUI: mira oculta por defecto y activa solo al apuntar
        LocalizarCrosshairUI();
        bool p9 = crosshairUI != null && crosshairUI.SoloAlApuntar;
        exito &= p9;

        // 10. Prueba Spawning y Física de Bala (BalaProyectil con Rigidbody y SphereCollider)
        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up;
        BalaProyectil bala = BalaProyectil.Disparar(
            prefabBala,
            spawnPos,
            transform.forward,
            velocidadBala,
            danoBala,
            tiempoVidaBala,
            capasDisparo,
            null,
            transform.root
        );
        bool p10 = bala != null &&
                   bala.GetComponent<BalaProyectil>() != null &&
                   bala.GetComponent<Rigidbody>() != null &&
                   bala.GetComponent<Collider>() != null;
        if (bala != null)
        {
            if (Application.isPlaying) Destroy(bala.gameObject);
            else DestroyImmediate(bala.gameObject);
        }
        exito &= p10;

        // 11. Prueba Navegación de Scene View: verificar que el input no secuestre la Scene View
#if ENABLE_INPUT_SYSTEM
        bool p11 = UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode ==
                   UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;
        exito &= p11;
#else
        bool p11 = true;
#endif

        // 12. Prueba Impacto y Daño en Capa 0 (Default): verificar que la bala colisiona y entrega daño
        GameObject objetivoPrueba = new GameObject("DummyTarget_TestL0");
        objetivoPrueba.layer = 0; // Capa 0 (Default)
        BoxCollider boxCol = objetivoPrueba.AddComponent<BoxCollider>();
        PruebaDamageableTarget mockDanable = objetivoPrueba.AddComponent<PruebaDamageableTarget>();

        Vector3 posObjetivo = spawnPos + transform.forward * 2f;
        objetivoPrueba.transform.position = posObjetivo;

        BalaProyectil balaImpacto = BalaProyectil.Disparar(
            prefabBala,
            spawnPos,
            transform.forward,
            velocidadBala,
            25f,
            2f,
            capasDisparo,
            null,
            transform.root
        );

        balaImpacto.ProcesarImpacto(boxCol, posObjetivo, -transform.forward);
        bool p12 = mockDanable.danoRecibido >= 25f;

        if (Application.isPlaying) Destroy(objetivoPrueba);
        else DestroyImmediate(objetivoPrueba);

        exito &= p12;

        // 13. Prueba Colisión con WindowsBossAI: asegurar collider calibrado, barrido continuo, solapamiento a quemarropa y recepción de daño
        GameObject windowsDummy = new GameObject("Windows_TestDummy");
        windowsDummy.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        WindowsBossAI bossAI = windowsDummy.AddComponent<WindowsBossAI>();
        bossAI.GarantizarComponentesFisicos();

        CapsuleCollider colBoss = windowsDummy.GetComponent<CapsuleCollider>();
        bool p13Col = colBoss != null && !colBoss.isTrigger && colBoss.height > 8f;

        float vidaInicial = bossAI.CurrentHealth;
        Vector3 posBoss = spawnPos + transform.forward * 2f;
        windowsDummy.transform.position = posBoss;

        // A) Probar barrido volumétrico continuo (SphereCast)
        bool sweepOk = Physics.SphereCast(spawnPos, 0.05f, transform.forward, out RaycastHit hitBoss, 4f, capasDisparo.value, QueryTriggerInteraction.Collide);
        bool sweepDetectaBoss = sweepOk && hitBoss.collider != null && (hitBoss.collider == colBoss || hitBoss.collider.transform.root == windowsDummy.transform);

        // B) Probar solapamiento a quemarropa (OverlapSphere)
        Collider[] overlaps = Physics.OverlapSphere(posBoss + Vector3.up * 0.5f, 0.1f, capasDisparo.value, QueryTriggerInteraction.Collide);
        bool overlapDetectaBoss = overlaps != null && System.Array.Exists(overlaps, c => c == colBoss || c.transform.root == windowsDummy.transform);

        // C) Probar procesamiento de impacto directo y aplicación de daño
        BalaProyectil balaBoss = BalaProyectil.Disparar(
            prefabBala,
            spawnPos,
            transform.forward,
            velocidadBala,
            35f,
            2f,
            capasDisparo,
            null,
            transform.root
        );

        balaBoss.ProcesarImpacto(colBoss, posBoss, -transform.forward);
        bool p13Dano = bossAI.CurrentHealth < vidaInicial;
        bool p13 = p13Col && sweepDetectaBoss && overlapDetectaBoss && p13Dano;
        exito &= p13;

        // 14. Prueba Velocidad Reducida de Caminata de Linux: verificar que la velocidad es lenta y controlable (<= 3.0 m/s)
        bool p14 = velocidad <= 3.0f && velocidad >= 2.0f;
        exito &= p14;

        if (Application.isPlaying) Destroy(windowsDummy);
        else DestroyImmediate(windowsDummy);

        Debug.Log($"<color={(exito ? "green" : "red")}>[LinuxCombatIK - Verificación Combate 7-14]</color> " +
                  $"P7(MuzzlePoint)={p7}, P8(ArmaActiva/Camina_arma)={p8}, P9(Crosshair Solo Apuntar)={p9}, " +
                  $"P10(Física y Spawn Bala)={p10}, P11(Scene View Input Seguro)={p11}, P12(Impacto/Daño Capa 0)={p12}, " +
                  $"P13(Colisión Windows / Sin Traspaso)={p13}, P14(Velocidad Linux Reducida)={p14}");

        return exito;
    }
}

/// <summary>
/// Componente de soporte interno para verificar la recepción de daño de IDamageable en pruebas.
/// </summary>
public class PruebaDamageableTarget : MonoBehaviour, IDamageable
{
    public float danoRecibido = 0f;
    public bool IsDead => danoRecibido >= 100f;

    public void TakeDamage(float amount)
    {
        danoRecibido += amount;
    }
}
