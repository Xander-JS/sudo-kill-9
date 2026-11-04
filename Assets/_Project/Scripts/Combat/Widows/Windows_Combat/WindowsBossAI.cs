using UnityEngine;
using SudoKill9.Combat;

/// <summary>
/// Controlador de IA para el jefe final Windows basado en el flujo de estados por distancia:
/// - Calcula frame a frame la distancia exacta hacia el jugador ('Player_LinuxSkeleton' / 'linux')
///   y la envía al parámetro 'Distancia_Pinguino' del Animator.
/// - El Animator (WindowsBoss_MasterController) gestiona todo el ciclo de combate:
///     1. 'Se prepara para atacar' (Inicio / Reanudación)
///     2. Dependiendo de la distancia elige locomoción: 'Camina', 'Trota' o 'Rum'
///     3. Al llegar al rango de ataque ejecuta: 'Patada', 'Ataque_Patada', 'Tornado' o 'Salto'
///     4. Tras el ataque regresa a 'Se prepara para atacar' para reiniciar el ciclo
/// - Realiza la rotación continua suave hacia el jugador manteniendo el eje Y bloqueado.
/// - Traslada físicamente al jefe en el escenario cuando el Animator se encuentra en 'Camina', 'Trota' o 'Rum'.
/// - Implementa IDamageable para reaccionar a impactos y muerte (Hit, Die).
/// </summary>
/// <summary>
/// Estados tácticos de alto nivel para el jefe Windows (HFSM).
/// </summary>
public enum BossCombatState
{
    Stalking,          // Manteniendo distancia y evaluando
    Approaching,       // Caminando, trotando o corriendo hacia el jugador
    MeleeEngage,       // Ejecutando combo cuerpo a cuerpo (Patada, Izquiedazo, Tornado, Salto)
    ExecutingSpecial,  // Ejecutando habilidad especial (Kamehameha, Firewall, Taskkill, SaltaLoco)
    VulnerableTired,   // Ventana de castigo en PoseCansado
    HitReaction,       // Reacción a impacto
    Dead
}

public class WindowsBossAI : MonoBehaviour, IDamageable
{
    [Header("Objetivo (Target)")]
    [Tooltip("Referencia al Transform del jugador hacia el cual se orienta y mide la distancia.")]
    [SerializeField] private Transform target;

    [Header("Componentes")]
    [Tooltip("Componente Animator con WindowsBoss_MasterController asignado.")]
    [SerializeField] private Animator animator;

    [Header("Parámetros del Animator")]
    [SerializeField] private string distanceParamName = "Distancia_Pinguino";
    [SerializeField] private string animationTimeParamName = "Tiempo_Animacion";
    [SerializeField] private string hitTriggerName = "Hit";
    [SerializeField] private string dieTriggerName = "Die";

    [Header("Velocidades de Desplazamiento")]
    [Tooltip("Activa o desactiva el movimiento por código hacia el jugador.")]
    [UnityEngine.Serialization.FormerlySerializedAs("translateTransform")]
    [SerializeField] private bool moverHaciaObjetivo = true;

    [Tooltip("Velocidad de traslación cuando la animación es 'Camina'.")]
    [SerializeField] private float walkSpeed = 2.0f;

    [Tooltip("Velocidad de traslación cuando la animación es 'Trota'.")]
    [SerializeField] private float trotSpeed = 3.5f;

    [Tooltip("Velocidad de traslación cuando la animación es 'Rum'.")]
    [SerializeField] private float runSpeed = 5.2f;

    [Tooltip("Velocidad de impulso frontal durante el 'Salto'.")]
    [SerializeField] private float leapSpeed = 3.8f;

    [Tooltip("Distancia mínima al objetivo para frenar el desplazamiento y no atravesar al pingüino.")]
    [SerializeField] private float stoppingDistance = 1.4f;

    [Header("Rotación y Orientación")]
    [Tooltip("Velocidad de rotación suave hacia el jugador.")]
    [SerializeField] private float rotationSpeed = 5.0f;

    [Tooltip("Distancia mínima para rotar y evitar giros bruscos en contacto cercano.")]
    [SerializeField] private float minDistanceToRotate = 0.1f;

    [Header("Salud y Combate")]
    [SerializeField] private float maxHealth = 500f;
    [SerializeField] private float currentHealth = 500f;

    [Tooltip("Tiempo mínimo en segundos entre reinicios de la animación Hit para evitar tartamudeo con ráfagas continuas.")]
    [SerializeField] private float intervaloMinimoReaccionGolpe = 0.32f;

    [Tooltip("Si está activo, ataques aéreos o pesados como Salto y Tornado no son interrumpidos en el aire por balas.")]
    [SerializeField] private bool hiperArmaduraEnAtaquesPesados = true;

    [Header("Fases de Combate y Ataques Especiales")]
    [Tooltip("Fase actual del jefe: 1 (100-70%), 2 (70-35%), 3 (<35% Enrage).")]
    [SerializeField] private int faseActual = 1;

    [Tooltip("Referencia opcional a WindowsCommandAttacks para coordinar ataques y estados especiales.")]
    [SerializeField] private WindowsCommandAttacks commandAttacks;

    [Header("Máquina de Estados Táctica (HFSM)")]
    [Tooltip("Estado táctico actual del jefe calculado en tiempo real.")]
    [SerializeField] private BossCombatState estadoCombate = BossCombatState.Stalking;
    public BossCombatState EstadoCombate => estadoCombate;

    [Header("Navegación Inteligente (NavMesh)")]
    [Tooltip("Componente NavMeshAgent opcional para desplazamiento inteligente sorteando obstáculos.")]
    [SerializeField] private UnityEngine.AI.NavMeshAgent navAgent;

    [Header("Diálogos y Provocaciones (Gemini / Local)")]
    [Tooltip("Módulo híbrido de diálogos y burlas satíricas.")]
    [SerializeField] private WindowsAIGeminiDialogue geminiDialogue;

    // Eventos estáticos de salud, fase y muerte del jefe para la UI
    public static event System.Action<float, float> OnBossHealthChanged;
    public static event System.Action<int> OnBossPhaseChanged;
    public static event System.Action OnBossDied;

    private float ultimoTiempoGolpe = -999f;

    [Header("Colisión y Físicas del Jefe")]
    [Tooltip("Collider principal de Windows para recibir impactos de bala sin que lo traspasen.")]
    [SerializeField] private CapsuleCollider bossCollider;

    [Tooltip("Altura deseada en espacio de mundo para el collider del jefe (m).")]
    [SerializeField] private float alturaMundo = 1.9f;

    [Tooltip("Radio deseado en espacio de mundo para el collider del jefe (m).")]
    [SerializeField] private float radioMundo = 0.42f;

    [Header("Control de Tiempo de Animación (Inspector)")]
    [Tooltip("Tiempo asignado al parámetro. Modifícalo en el Inspector durante Play Mode para pruebas.")]
    [SerializeField] private float tiempoAnimacion = 3f;

    [Tooltip("Si está activo, descuenta el tiempo automáticamente frame a frame.")]
    [SerializeField] private bool cuentaRegresivaAutomatica = true;

    [Tooltip("Si está activo, reinicia el temporizador al llegar a 0.")]
    [SerializeField] private bool repetirEnBucle = false;

    [Header("Monitoreo en Tiempo Real (Lecturas)")]
    [Tooltip("Distancia calculada en tiempo real hacia el objetivo.")]
    [SerializeField] private float currentDistanceToTarget;

    [Tooltip("Nombre de la animación actualmente activa en el Animator.")]
    [SerializeField] private string animacionActual;

    [Tooltip("Valor actual enviado a Tiempo_Animacion.")]
    [SerializeField] private float valorActualEnviadoAlAnimator;

    private int distanceHash;
    private int animationTimeHash;
    private int hitTriggerHash;
    private int dieTriggerHash;

    private bool hasDistanceParam;
    private bool hasAnimationTimeParam;
    private bool hasHitParam;
    private bool hasDieParam;

    private float ultimoTiempoInspector;
    private bool isDead = false;
    private bool golpeAplicadoEnCiclo = false;

    private void Awake()
    {
        currentHealth = maxHealth;
        GarantizarComponentesFisicos();
        ConfigurarHashesParametros();
        ultimoTiempoInspector = tiempoAnimacion;
        moverHaciaObjetivo = true;

        if (commandAttacks == null)
        {
            commandAttacks = GetComponent<WindowsCommandAttacks>();
            if (commandAttacks == null) commandAttacks = gameObject.AddComponent<WindowsCommandAttacks>();
        }
        if (geminiDialogue == null)
        {
            geminiDialogue = GetComponent<WindowsAIGeminiDialogue>();
            if (geminiDialogue == null) geminiDialogue = gameObject.AddComponent<WindowsAIGeminiDialogue>();
        }
        ActualizarFaseActual(false);

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    private void Start()
    {
        GarantizarComponentesFisicos();
        BuscarJugadorSiEsNecesario();

        if (commandAttacks == null)
        {
            commandAttacks = GetComponent<WindowsCommandAttacks>();
            if (commandAttacks == null) commandAttacks = gameObject.AddComponent<WindowsCommandAttacks>();
        }
        if (geminiDialogue == null)
        {
            geminiDialogue = GetComponent<WindowsAIGeminiDialogue>();
            if (geminiDialogue == null) geminiDialogue = gameObject.AddComponent<WindowsAIGeminiDialogue>();
        }
        ActualizarFaseActual(false);
        OnBossHealthChanged?.Invoke(currentHealth, maxHealth);
        OnBossPhaseChanged?.Invoke(faseActual);

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        DetectarParametrosEnAnimator();

        valorActualEnviadoAlAnimator = tiempoAnimacion;
        ultimoTiempoInspector = tiempoAnimacion;
        ActualizarParametroTiempoEnAnimator();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && gameObject != null)
                {
                    GarantizarComponentesFisicos();
                }
            };
        }
        else
        {
            GarantizarComponentesFisicos();
        }
#else
        GarantizarComponentesFisicos();
#endif

        if (Application.isPlaying && animator != null)
        {
            if (Mathf.Abs(tiempoAnimacion - ultimoTiempoInspector) > 0.001f)
            {
                ultimoTiempoInspector = tiempoAnimacion;
                valorActualEnviadoAlAnimator = tiempoAnimacion;
                ActualizarParametroTiempoEnAnimator();
            }
        }
    }

    /// <summary>
    /// Garantiza la presencia de un CapsuleCollider y Rigidbody cinemático calibrados
    /// para que las balas impacten físicamente y no traspasen a Windows bajo ninguna circunstancia.
    /// </summary>
    public void GarantizarComponentesFisicos()
    {
        if (bossCollider == null)
        {
            bossCollider = GetComponent<CapsuleCollider>();
            if (bossCollider == null)
            {
                bossCollider = gameObject.AddComponent<CapsuleCollider>();
            }
        }

        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        float scaleXZ = Mathf.Max(0.0001f, (Mathf.Abs(transform.lossyScale.x) + Mathf.Abs(transform.lossyScale.z)) * 0.5f);

        bossCollider.height = alturaMundo / scaleY;
        bossCollider.radius = radioMundo / scaleXZ;
        bossCollider.center = new Vector3(0f, (alturaMundo * 0.5f) / scaleY, 0f);
        bossCollider.isTrigger = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        if (navAgent == null)
        {
            navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        }
        if (navAgent != null)
        {
            navAgent.stoppingDistance = stoppingDistance;
            navAgent.updateRotation = false;
        }
    }

    private void Update()
    {
        if (isDead)
        {
            ActualizarEstadoCombate();
            return;
        }

        // Validar y asegurar referencia activa del jugador (redirige a LinuxCombatIK si target es nulo o un contenedor inactivo)
        if (target == null || !target.gameObject.activeInHierarchy || target.GetComponent<LinuxCombatIK>() == null)
        {
            BuscarJugadorSiEsNecesario();
            if (target == null) return;
        }

        if (animator != null && animator.applyRootMotion)
        {
            animator.applyRootMotion = false;
        }

        // 1. CÁLCULO Y ENVÍO DE DISTANCIA HORIZONTAL (Plano XZ)
        // Ignorar la altura Y para que desniveles, saltos o pequeñas pendientes no distorsionen los rangos de combate
        Vector3 diff = target.position - transform.position;
        diff.y = 0f;
        currentDistanceToTarget = diff.magnitude;

        if (animator != null && hasDistanceParam)
        {
            animator.SetFloat(distanceHash, currentDistanceToTarget);
        }

        // 2. TEMPORIZADOR DE ANIMACIÓN (Retrocompatibilidad e Inspector)
        ActualizarTemporizadorAnimacionInspector();

        // 3. ROTACIÓN CONTINUA HACIA EL JUGADOR CON EJE Y BLOQUEADO
        RotarHaciaJugador();

        // 4. DESPLAZAMIENTO SEGÚN LA ANIMACIÓN ACTIVA DEL ANIMATOR
        GestionarMovimientoSegunAnimacion();

        // 5. EVALUACIÓN DE ESTADO TÁCTICO HFSM
        ActualizarEstadoCombate();
    }

    private void ActualizarEstadoCombate()
    {
        if (isDead)
        {
            estadoCombate = BossCombatState.Dead;
            return;
        }

        if (commandAttacks != null && commandAttacks.EstaExhausto)
        {
            estadoCombate = BossCombatState.VulnerableTired;
            return;
        }

        if (commandAttacks != null && commandAttacks.EstaEjecutandoAtaqueEspecial)
        {
            estadoCombate = BossCombatState.ExecutingSpecial;
            return;
        }

        if (IsStateOrTransitioningTo("Hit"))
        {
            estadoCombate = BossCombatState.HitReaction;
            return;
        }

        if (IsStateOrTransitioningTo("Patada") || 
            IsStateOrTransitioningTo("Ataque_Patada") || 
            IsStateOrTransitioningTo("Tornado") || 
            IsStateOrTransitioningTo("Izquiedazo") || 
            IsStateOrTransitioningTo("Salto"))
        {
            estadoCombate = BossCombatState.MeleeEngage;
            return;
        }

        if (IsLocomotionState())
        {
            estadoCombate = BossCombatState.Approaching;
            return;
        }

        estadoCombate = BossCombatState.Stalking;
    }

    private void RotarHaciaJugador()
    {
        if (target == null) return;
        if (commandAttacks != null && commandAttacks.EstaEjecutandoAtaqueEspecial) return;

        if (currentDistanceToTarget > minDistanceToRotate)
        {
            Vector3 direction = target.position - transform.position;
            direction.y = 0f; // Bloqueo estricto del eje Y

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    private void GestionarMovimientoSegunAnimacion()
    {
        if (!moverHaciaObjetivo || animator == null) return;
        if (commandAttacks != null && commandAttacks.EstaEjecutandoAtaqueEspecial)
        {
            DetenerAgenteNavMesh();
            return;
        }

        if (IsStateOrTransitioningTo("Camina"))
        {
            golpeAplicadoEnCiclo = false;
            animacionActual = "Camina";
            MoverHaciaJugador(walkSpeed);
        }
        else if (IsStateOrTransitioningTo("Trota"))
        {
            golpeAplicadoEnCiclo = false;
            animacionActual = "Trota";
            MoverHaciaJugador(trotSpeed);
        }
        else if (IsStateOrTransitioningTo("Rum"))
        {
            golpeAplicadoEnCiclo = false;
            animacionActual = "Rum";
            MoverHaciaJugador(runSpeed);
        }
        else if (IsStateOrTransitioningTo("Salto"))
        {
            animacionActual = "Salto";
            MoverHaciaJugador(leapSpeed);
            VerificarImpactoAtaqueCuerpoACuerpo("Salto", 3.0f, 2f, 0.40f, 0.85f);
        }
        else if (IsStateOrTransitioningTo("Se prepara para atacar"))
        {
            DetenerAgenteNavMesh();
            golpeAplicadoEnCiclo = false;
            animacionActual = "Se prepara para atacar";
        }
        else if (IsStateOrTransitioningTo("Ataque_Patada"))
        {
            DetenerAgenteNavMesh();
            animacionActual = "Ataque_Patada";
            VerificarImpactoAtaqueCuerpoACuerpo("Ataque_Patada", 3.2f, 1f, 0.20f, 0.75f);
        }
        else if (IsStateOrTransitioningTo("Tornado"))
        {
            DetenerAgenteNavMesh();
            animacionActual = "Tornado";
            VerificarImpactoAtaqueCuerpoACuerpo("Tornado", 3.5f, 1f, 0.15f, 0.80f);
        }
        else if (IsStateOrTransitioningTo("Patada"))
        {
            DetenerAgenteNavMesh();
            animacionActual = "Patada";
            VerificarImpactoAtaqueCuerpoACuerpo("Patada", 2.3f, 1f, 0.20f, 0.75f);
        }
        else if (IsStateOrTransitioningTo("Izquiedazo"))
        {
            DetenerAgenteNavMesh();
            animacionActual = "Izquiedazo";
            VerificarImpactoAtaqueCuerpoACuerpo("Izquiedazo", 2.2f, 1f, 0.20f, 0.70f);
        }
        else if (IsStateOrTransitioningTo("Mueve_Cuello"))
        {
            DetenerAgenteNavMesh();
            golpeAplicadoEnCiclo = false;
            animacionActual = "Mueve_Cuello";
        }
        else if (IsStateOrTransitioningTo("Se rie"))
        {
            DetenerAgenteNavMesh();
            golpeAplicadoEnCiclo = false;
            animacionActual = "Se rie";
        }
        else if (IsStateOrTransitioningTo("PoseCansado"))
        {
            DetenerAgenteNavMesh();
            animacionActual = "PoseCansado";
        }
        else if (IsStateOrTransitioningTo("Hit"))
        {
            DetenerAgenteNavMesh();
            animacionActual = "Hit";
            // Pausar traslación mientras se reproduce el retroceso del impacto
        }
        else if (IsStateOrTransitioningTo("Die"))
        {
            DetenerAgenteNavMesh();
            animacionActual = "Die";
        }
        else
        {
            DetenerAgenteNavMesh();
            animacionActual = "Otro";
        }
    }

    /// <summary>
    /// Comprueba frame a frame si el ataque físico actual golpea a Tux dentro de la ventana activa de la animación.
    /// Deduplica para golpear máximo 1 vez por ciclo de ataque hasta que el jefe regrese a preparación o locomoción.
    /// </summary>
    private void VerificarImpactoAtaqueCuerpoACuerpo(string ataque, float rangoGolpe, float dano, float minNormalizedTime, float maxNormalizedTime)
    {
        if (golpeAplicadoEnCiclo || target == null || isDead) return;

        if (currentDistanceToTarget <= rangoGolpe)
        {
            float normTime = 0.5f;
            if (animator != null)
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                normTime = info.normalizedTime % 1.0f;
            }

            if (normTime >= minNormalizedTime && normTime <= maxNormalizedTime)
            {
                golpeAplicadoEnCiclo = true;

                IDamageable danable = target.GetComponent<IDamageable>() ??
                                      target.GetComponentInParent<IDamageable>() ??
                                      target.GetComponentInChildren<IDamageable>();

                if (danable != null && !danable.IsDead)
                {
                    danable.TakeDamage(dano);

                    if (geminiDialogue != null)
                    {
                        geminiDialogue.SolicitarDialogo(WindowsAIGeminiDialogue.DialogueContext.GolpeConectado, $"Windows conectó {ataque} contra Tux.");
                    }

#if UNITY_EDITOR
                    Debug.Log($"<color=orange>[WindowsBossAI]</color> ¡Impacto físico ({ataque}) conectó a Tux! Daño: {dano}");
#endif
                }
            }
        }
    }

    private bool IsStateOrTransitioningTo(string stateName)
    {
        if (animator == null) return false;
        int hash = Animator.StringToHash(stateName);
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.IsName(stateName) || current.shortNameHash == hash) return true;
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (next.IsName(stateName) || next.shortNameHash == hash) return true;
        }
        return false;
    }

    private bool IsLocomotionState()
    {
        return IsStateOrTransitioningTo("Camina") || 
               IsStateOrTransitioningTo("Trota") || 
               IsStateOrTransitioningTo("Rum");
    }

    private void MoverHaciaJugador(float velocidad)
    {
        if (target == null) return;

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            if (currentDistanceToTarget <= stoppingDistance)
            {
                if (!navAgent.isStopped) navAgent.isStopped = true;
            }
            else
            {
                if (navAgent.isStopped) navAgent.isStopped = false;
                navAgent.speed = velocidad;
                navAgent.SetDestination(target.position);
            }
        }
        else
        {
            // Frenar si ya está en la distancia mínima de contacto
            if (currentDistanceToTarget <= stoppingDistance) return;

            Vector3 targetPos = target.position;
            targetPos.y = transform.position.y; // Mantener la misma altura

            transform.position = Vector3.MoveTowards(transform.position, targetPos, velocidad * Time.deltaTime);
        }
    }

    private void DetenerAgenteNavMesh()
    {
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            if (!navAgent.isStopped) navAgent.isStopped = true;
        }
    }

    // ========================================================================= //
    // DAÑO Y MUERTE (IDamageable)                                               //
    // ========================================================================= //

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        if (commandAttacks == null) commandAttacks = GetComponent<WindowsCommandAttacks>();
        float danoFinal = Mathf.Abs(amount);
        if (commandAttacks != null && commandAttacks.EstaExhausto)
        {
            danoFinal *= 1.5f; // Ventana de castigo crítico durante estado exhausto
        }

        currentHealth -= danoFinal;
        ActualizarFaseActual(true);
        OnBossHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            Morir();
        }
        else
        {
            RecibirGolpe();
        }
    }

    private void ActualizarFaseActual(bool dispararEvento)
    {
        float ratio = currentHealth / Mathf.Max(1f, maxHealth);
        int nuevaFase = 1;
        if (ratio <= 0.35f) nuevaFase = 3;
        else if (ratio <= 0.70f) nuevaFase = 2;

        if (nuevaFase != faseActual)
        {
            faseActual = nuevaFase;
            if (faseActual == 3)
            {
                runSpeed = 6.0f;
                if (animator != null)
                {
                    animator.CrossFadeInFixedTime("Grito", 0.15f);
                }
            }
            if (dispararEvento)
            {
                OnBossPhaseChanged?.Invoke(faseActual);
            }
        }
    }

    /// <summary>
    /// Invocado directamente por las balas (BalaProyectil) al impactar contra el jefe.
    /// Aplica el daño y activa la animación reactiva de impacto si no es bloqueado por el firewall.
    /// </summary>
    public void RecibirDisparo(float dano, Vector3 puntoImpacto, Vector3 direccionBala)
    {
        if (commandAttacks == null) commandAttacks = GetComponent<WindowsCommandAttacks>();
        if (commandAttacks != null && commandAttacks.NotificarImpactoEnEscudo(null, puntoImpacto, direccionBala))
        {
            return; // Bloqueado frontalmente por el firewall de Windows
        }

        TakeDamage(dano);
    }

    /// <summary>
    /// Ejecuta la animación de impacto (Hit / Un_golpe_suave) interrumpiendo fluidamente cualquier acción previa.
    /// Limpia triggers colgantes para evitar dobles impactos fantasma y respeta el intervalo mínimo para evitar stutter.
    /// </summary>
    public void RecibirGolpe()
    {
        if (isDead) return;

        if (animator != null)
        {
            // Limpiar el trigger 'Hit' para que no quede encolado en el Animator y dispare una segunda animación fantasma al salir de Hit
            if (hasHitParam)
            {
                animator.ResetTrigger(hitTriggerHash);
            }

            // Hiperarmadura en ataques pesados (Salto, Tornado) o ataques especiales (Kamehameha, SaltaLoco, PoseCansado)
            if (hiperArmaduraEnAtaquesPesados && (IsStateOrTransitioningTo("Salto") || IsStateOrTransitioningTo("Tornado")))
            {
                return;
            }

            if (commandAttacks != null && commandAttacks.EstaEjecutandoAtaqueEspecial)
            {
                return;
            }

            // Si ya está en Hit y la última reacción fue muy reciente, permitimos que el movimiento del retroceso fluya sin resetearlo a frame 0
            bool yaEnHit = IsStateOrTransitioningTo("Hit");
            if (!yaEnHit || (Time.time - ultimoTiempoGolpe) >= intervaloMinimoReaccionGolpe)
            {
                ultimoTiempoGolpe = Time.time;
                animator.CrossFadeInFixedTime("Hit", 0.08f, 0, 0f);
            }
        }
    }

    /// <summary>
    /// Restablece la salud del jefe a un valor específico (útil para reinicios y pruebas de verificación sin ensuciar la escena).
    /// </summary>
    public void RestaurarVida(float nuevaVida)
    {
        currentHealth = Mathf.Max(0f, nuevaVida);
        isDead = currentHealth <= 0f;
        if (!isDead && navAgent != null && !navAgent.enabled)
        {
            navAgent.enabled = true;
        }
        ActualizarFaseActual(true);
        ActualizarEstadoCombate();
        OnBossHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void Morir()
    {
        isDead = true;
        currentHealth = 0f;
        DetenerAgenteNavMesh();
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.enabled = false;
        }
        ActualizarEstadoCombate();
        if (commandAttacks == null) commandAttacks = GetComponent<WindowsCommandAttacks>();
        if (commandAttacks != null)
        {
            commandAttacks.LimpiarAtaquesActivosPublico();
        }
        OnBossHealthChanged?.Invoke(0f, maxHealth);
        OnBossDied?.Invoke();

        if (animator != null && hasDieParam)
        {
            animator.SetTrigger(dieTriggerHash);
        }
    }

    public bool IsDead => isDead;

    // ========================================================================= //
    // BÚSQUEDA Y PARÁMETROS                                                     //
    // ========================================================================= //

    private void BuscarJugadorSiEsNecesario()
    {
        // 1. Buscar prioritariamente al jugador que tiene el componente activo LinuxCombatIK
        var combatPlayer = Object.FindFirstObjectByType<LinuxCombatIK>();
        if (combatPlayer != null)
        {
            target = combatPlayer.transform;
            Debug.Log($"<color=cyan>[WindowsBossAI]</color> Objetivo fijado exitosamente al jugador activo '{target.name}'.");
            return;
        }

        // 2. Si target ya tiene un valor pero tiene hijo con LinuxCombatIK
        if (target != null)
        {
            var childIK = target.GetComponentInChildren<LinuxCombatIK>();
            if (childIK != null)
            {
                target = childIK.transform;
                return;
            }
            if (target.gameObject.activeInHierarchy) return;
        }

        // 3. Buscar por nombres conocidos en la jerarquía
        GameObject playerObj = GameObject.Find("linux");
        if (playerObj == null) playerObj = GameObject.Find("Player_LinuxSkeleton");
        if (playerObj == null) playerObj = GameObject.Find("Player_Linux");
        if (playerObj == null) playerObj = GameObject.Find("linux-skeleton");

        // 4. Buscar por Tag
        if (playerObj == null)
        {
            try
            {
                playerObj = GameObject.FindWithTag("Player");
            }
            catch {}
        }

        // 5. Buscar por componente alternativo
        if (playerObj == null)
        {
            var skeletonCtrl = Object.FindFirstObjectByType<LinuxSkeletonController>();
            if (skeletonCtrl != null) playerObj = skeletonCtrl.gameObject;
        }

        if (playerObj != null)
        {
            var foundIK = playerObj.GetComponentInChildren<LinuxCombatIK>();
            target = (foundIK != null) ? foundIK.transform : playerObj.transform;
            Debug.Log($"<color=cyan>[WindowsBossAI]</color> Objetivo fijado exitosamente a '{target.name}'.");
        }
        else
        {
            Debug.LogWarning("[WindowsBossAI] No se encontró al jugador 'linux' ni 'Player_LinuxSkeleton' en la jerarquía.");
        }
    }

    private void ConfigurarHashesParametros()
    {
        distanceHash = Animator.StringToHash(distanceParamName);
        animationTimeHash = Animator.StringToHash(animationTimeParamName);
        hitTriggerHash = Animator.StringToHash(hitTriggerName);
        dieTriggerHash = Animator.StringToHash(dieTriggerName);
    }

    private void DetectarParametrosEnAnimator()
    {
        if (animator == null) return;

        hasDistanceParam = TieneParametro(animator, distanceParamName);
        hasHitParam = TieneParametro(animator, hitTriggerName);
        hasDieParam = TieneParametro(animator, dieTriggerName);

        if (TieneParametro(animator, animationTimeParamName))
        {
            animationTimeHash = Animator.StringToHash(animationTimeParamName);
            hasAnimationTimeParam = true;
        }
        else if (TieneParametro(animator, "Tiempo_Animacion"))
        {
            animationTimeParamName = "Tiempo_Animacion";
            animationTimeHash = Animator.StringToHash(animationTimeParamName);
            hasAnimationTimeParam = true;
        }
    }

    private bool TieneParametro(Animator anim, string paramName)
    {
        if (anim == null || string.IsNullOrEmpty(paramName)) return false;

        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
    }

    private void ActualizarTemporizadorAnimacionInspector()
    {
        if (Mathf.Abs(tiempoAnimacion - ultimoTiempoInspector) > 0.001f)
        {
            ultimoTiempoInspector = tiempoAnimacion;
            valorActualEnviadoAlAnimator = tiempoAnimacion;
        }
        else if (cuentaRegresivaAutomatica)
        {
            // Si el jefe está en 'Se prepara para atacar', reseteamos el temporizador para la próxima carga
            if (IsStateOrTransitioningTo("Se prepara para atacar"))
            {
                valorActualEnviadoAlAnimator = tiempoAnimacion;
            }
            // Mientras está en marcha/carrera acercándose, el tiempo se descuenta
            else if (IsLocomotionState())
            {
                if (valorActualEnviadoAlAnimator > 0f)
                {
                    valorActualEnviadoAlAnimator -= Time.deltaTime;
                    if (valorActualEnviadoAlAnimator < 0f) valorActualEnviadoAlAnimator = 0f;
                }
            }
        }
        else
        {
            valorActualEnviadoAlAnimator = tiempoAnimacion;
        }

        ActualizarParametroTiempoEnAnimator();
    }

    private void ActualizarParametroTiempoEnAnimator()
    {
        if (animator != null && hasAnimationTimeParam)
        {
            animator.SetFloat(animationTimeHash, valorActualEnviadoAlAnimator);
        }
    }

    // ========================================================================= //
    // PROPIEDADES PÚBLICAS                                                      //
    // ========================================================================= //

    public float CurrentDistanceToTarget => currentDistanceToTarget;
    public string AnimacionActual => animacionActual;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public int FaseActual => faseActual;
    public WindowsCommandAttacks CommandAttacks => commandAttacks;

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    public float TiempoAnimacion
    {
        get => tiempoAnimacion;
        set
        {
            tiempoAnimacion = value;
            ultimoTiempoInspector = value;
            valorActualEnviadoAlAnimator = value;
            ActualizarParametroTiempoEnAnimator();
        }
    }

    public bool CuentaRegresivaAutomatica
    {
        get => cuentaRegresivaAutomatica;
        set => cuentaRegresivaAutomatica = value;
    }

    public bool RepetirEnBucle
    {
        get => repetirEnBucle;
        set => repetirEnBucle = value;
    }
}
