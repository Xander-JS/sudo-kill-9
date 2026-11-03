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

    private void Awake()
    {
        currentHealth = maxHealth;
        GarantizarComponentesFisicos();
        ConfigurarHashesParametros();
        ultimoTiempoInspector = tiempoAnimacion;
        moverHaciaObjetivo = true;

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
        GarantizarComponentesFisicos();

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
    }

    private void Update()
    {
        if (isDead) return;

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
    }

    private void RotarHaciaJugador()
    {
        if (target == null) return;

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

        if (IsStateOrTransitioningTo("Camina"))
        {
            animacionActual = "Camina";
            MoverHaciaJugador(walkSpeed);
        }
        else if (IsStateOrTransitioningTo("Trota"))
        {
            animacionActual = "Trota";
            MoverHaciaJugador(trotSpeed);
        }
        else if (IsStateOrTransitioningTo("Rum"))
        {
            animacionActual = "Rum";
            MoverHaciaJugador(runSpeed);
        }
        else if (IsStateOrTransitioningTo("Salto"))
        {
            animacionActual = "Salto";
            MoverHaciaJugador(leapSpeed);
        }
        else if (IsStateOrTransitioningTo("Se prepara para atacar"))
        {
            animacionActual = "Se prepara para atacar";
        }
        else if (IsStateOrTransitioningTo("Ataque_Patada"))
        {
            animacionActual = "Ataque_Patada";
        }
        else if (IsStateOrTransitioningTo("Tornado"))
        {
            animacionActual = "Tornado";
        }
        else if (IsStateOrTransitioningTo("Patada"))
        {
            animacionActual = "Patada";
        }
        else if (IsStateOrTransitioningTo("Hit"))
        {
            animacionActual = "Hit";
            // Pausar traslación mientras se reproduce el retroceso del impacto
        }
        else if (IsStateOrTransitioningTo("Die"))
        {
            animacionActual = "Die";
        }
        else
        {
            animacionActual = "Otro";
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

        // Frenar si ya está en la distancia mínima de contacto
        if (currentDistanceToTarget <= stoppingDistance) return;

        Vector3 targetPos = target.position;
        targetPos.y = transform.position.y; // Mantener la misma altura

        transform.position = Vector3.MoveTowards(transform.position, targetPos, velocidad * Time.deltaTime);
    }

    // ========================================================================= //
    // DAÑO Y MUERTE (IDamageable)                                               //
    // ========================================================================= //

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= Mathf.Abs(amount);

        if (currentHealth <= 0f)
        {
            Morir();
        }
        else
        {
            RecibirGolpe();
        }
    }

    /// <summary>
    /// Invocado directamente por las balas (BalaProyectil) al impactar contra el jefe.
    /// Aplica el daño y activa la animación reactiva de impacto.
    /// </summary>
    public void RecibirDisparo(float dano, Vector3 puntoImpacto, Vector3 direccionBala)
    {
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

            // Hiperarmadura en ataques pesados (Salto, Tornado): el jefe absorbe el daño sin cortar bruscamente el ataque
            if (hiperArmaduraEnAtaquesPesados && (IsStateOrTransitioningTo("Salto") || IsStateOrTransitioningTo("Tornado")))
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
    }

    public void Morir()
    {
        isDead = true;
        currentHealth = 0f;

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
