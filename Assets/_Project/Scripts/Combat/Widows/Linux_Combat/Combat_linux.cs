using UnityEngine;
using UnityEngine.EventSystems;
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
    [Tooltip("Velocidad de traslación horizontal del personaje en m/s.")]
    public float velocidad = 6f;

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

    [Header("Right Hand IK")]
    [Range(0f, 1f)] [SerializeField] private float rightHandWeight = 1f;

    [Header("Left Hand Weights")]
    [Range(0f, 1f)] [SerializeField] private float positionWeight = 1f;
    [Range(0f, 1f)] [SerializeField] private float rotationWeight = 1f;

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

    // Dibuja el punto de agarre en la vista de escena
    private void OnDrawGizmos()
    {
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
        bool hasX = false, hasZ = false;
        if (animator != null)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == inputXHash && p.type == AnimatorControllerParameterType.Float) hasX = true;
                if (p.nameHash == inputZHash && p.type == AnimatorControllerParameterType.Float) hasZ = true;
                if (p.nameHash == crouchHash && p.type == AnimatorControllerParameterType.Bool) hasCrouchParam = true;
            }
        }
        hasInputParams = hasX && hasZ;

        // 5. Inicializar pose inicial del arma
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
        // 4. LÓGICA DE COMBATE E IK DE ARMA                                         //
        // ========================================================================= //
        ActualizarPoseArmaIK();
    }

    private void LateUpdate()
    {
        // ========================================================================= //
        // 5. SEGUIMIENTO DE PIVOTE, COMPOSICIÓN OVER-THE-SHOULDER Y COLISIONES      //
        // ========================================================================= //
        ActualizarSeguimientoYColisionCamara();
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

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            escapePresionado = true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            clicIzquierdo = true;
        }
#endif

        if (escapePresionado)
        {
            BloquearCursor(false);
        }
        else if (Cursor.lockState != CursorLockMode.Locked && clicIzquierdo)
        {
            bool clicSobreUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!clicSobreUI)
            {
                BloquearCursor(true);
            }
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
    /// Calcula el vector de desplazamiento horizontal (Y = 0) a partir de la orientación de PlayerRoot.
    /// </summary>
    public Vector3 CalcularDireccionMovimientoHorizontal(float inputHorizontal, float inputVertical)
    {
        Vector3 forwardHorizontal = transform.forward;
        forwardHorizontal.y = 0f;
        if (forwardHorizontal.sqrMagnitude > 0.0001f) forwardHorizontal.Normalize();

        Vector3 rightHorizontal = transform.right;
        rightHorizontal.y = 0f;
        if (rightHorizontal.sqrMagnitude > 0.0001f) rightHorizontal.Normalize();

        Vector3 direccionMovimiento = forwardHorizontal * inputVertical + rightHorizontal * inputHorizontal;
        direccionMovimiento.y = 0f;

        if (direccionMovimiento.sqrMagnitude > 1f)
        {
            direccionMovimiento.Normalize();
        }

        return direccionMovimiento;
    }

    /// <summary>
    /// Desplaza al personaje en el plano horizontal (XZ) en función de la orientación horizontal
    /// de PlayerRoot (transform.forward y transform.right), nunca de la inclinación vertical de la cámara.
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
            Vector2 animVec = new Vector2(inputHorizontal, inputVertical);
            if (animVec.sqrMagnitude > 1f) animVec.Normalize();
            animator.SetFloat(inputXHash, animVec.x);
            animator.SetFloat(inputZHash, animVec.y);
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
}
