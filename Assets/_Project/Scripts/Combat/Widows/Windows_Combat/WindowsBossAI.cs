using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using SudoKill9.Combat;

/// <summary>
/// Estados tácticos de la Máquina de Estados Finita Jerárquica (HFSM) para Windows Boss.
/// </summary>
public enum BossCombatState
{
    Stalking,          // Acecho y evaluación táctica a media distancia, reposicionamiento y provocación
    Approaching,       // Persecución inteligente hacia Tux mediante NavMeshAgent y velocidades adaptativas
    MeleeEngage,       // Combate cuerpo a cuerpo dinámico (Patada, Ataque_Patada, Izquiedazo, Tornado)
    ExecutingSpecial,  // Ejecución de habilidades especiales subordinadas a WindowsCommandAttacks con hiperarmadura
    Exhausted,         // Ventana de vulnerabilidad post-Kamehameha (PoseCansado, 3.0s, 1.5x daño crítico)
    Dead               // Estado prioritario de muerte y cese total de actividad táctica
}

/// <summary>
/// Controlador principal de IA para el jefe final Windows en Sudo-Kill-9.
/// Arquitectura Híbrida:
/// - Núcleo de combate local en tiempo real (60 FPS, 0 ms latencia, HFSM táctica, NavMeshAgent inteligente).
/// - Ejecución física de ataques especiales delegada a WindowsCommandAttacks.
/// - Sistema asíncrono de personalidad y burlas satíricas conectado a Gemini API (WindowsAIGeminiDialogue) con fallback local.
/// </summary>
[SelectionBase]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
public class WindowsBossAI : MonoBehaviour, IDamageable
{
    // ========================================================================= //
    // 1. CONFIGURACIÓN DE OBJETIVO Y NAVEGACIÓN                                 //
    // ========================================================================= //

    [Header("1. Objetivo y Percepción")]
    [Tooltip("Referencia al Transform de Tux hacia el cual se orienta y navega.")]
    [SerializeField] private Transform target;

    [Tooltip("Distancia calculada en tiempo real hacia el objetivo en el plano horizontal (XZ).")]
    [SerializeField] private float currentDistanceToTarget;

    [Header("2. Navegación y Locomoción (NavMeshAgent)")]
    [Tooltip("Componente NavMeshAgent para desplazamiento inteligente sorteando obstáculos.")]
    [SerializeField] private NavMeshAgent navAgent;

    [Tooltip("Activa o desactiva el desplazamiento por código.")]
    [SerializeField] private bool moverHaciaObjetivo = true;

    [Tooltip("Velocidad de traslación en 'Camina' (< 3.5m).")]
    [SerializeField] private float walkSpeed = 2.0f;

    [Tooltip("Velocidad de traslación en 'Trota' (3.5m - 7.0m).")]
    [SerializeField] private float trotSpeed = 3.5f;

    [Tooltip("Velocidad de traslación en 'Rum' (> 7.0m o enrage).")]
    [SerializeField] private float runSpeed = 5.2f;

    [Tooltip("Velocidad de impulso durante el ataque 'Salto'.")]
    [SerializeField] private float leapSpeed = 3.8f;

    [Tooltip("Distancia mínima al objetivo para detener el avance físico y evitar superposiciones.")]
    [SerializeField] private float stoppingDistance = 1.4f;

    [Tooltip("Intervalo mínimo entre recalculos de ruta en el NavMesh para optimizar rendimiento.")]
    [SerializeField] private float intervaloRecalculoDestino = 0.20f;

    [Header("3. Rotación y Orientación")]
    [Tooltip("Velocidad de rotación suave hacia Tux.")]
    [SerializeField] private float rotationSpeed = 6.0f;

    [Tooltip("Distancia mínima para rotar y evitar giros erráticos en contacto íntimo.")]
    [SerializeField] private float minDistanceToRotate = 0.15f;

    // ========================================================================= //
    // 2. MÁQUINA DE ESTADOS TÁCTICA (HFSM)                                      //
    // ========================================================================= //

    [Header("4. Máquina de Estados Finita (HFSM)")]
    [Tooltip("Estado táctico actual del jefe.")]
    [SerializeField] private BossCombatState estadoCombate = BossCombatState.Stalking;

    [Tooltip("Distancia táctica deseada durante el estado Stalking.")]
    [SerializeField] private float distanciaAcecho = 5.5f;

    [Tooltip("Distancia máxima para iniciar combate cuerpo a cuerpo.")]
    [SerializeField] private float distanciaRangoMelee = 2.1f;

    [Tooltip("Distancia con histéresis para abandonar el combate cuerpo a cuerpo.")]
    [SerializeField] private float distanciaSalidaMelee = 2.7f;

    [Tooltip("Tiempo de enfriamiento entre ataques cuerpo a cuerpo.")]
    [SerializeField] private float cooldownAtaquesMelee = 1.25f;

    [Tooltip("Intervalo en segundos entre provocaciones con 'Mueve_Cuello' en Stalking.")]
    [SerializeField] private float intervaloProvocacionStalking = 14.0f;

    [Tooltip("Permite a la IA evaluar y disparar habilidades especiales tácticas en Stalking y Approaching.")]
    [SerializeField] private bool coordinarAtaquesEspeciales = true;

    [Tooltip("Tiempo de enfriamiento entre evaluaciones de ataques especiales tácticos (segundos).")]
    [SerializeField] private float cooldownEspecialesTacticos = 9.0f;

    // ========================================================================= //
    // 3. COMBATE, SALUD Y VULNERABILIDAD                                        //
    // ========================================================================= //

    [Header("5. Salud, Fases y Dos Vidas")]
    [Tooltip("Cantidad total de barras de vida que posee el jefe Windows.")]
    [SerializeField] private int vidasTotales = 2;

    [Tooltip("Vida actual activa (1 o 2).")]
    [SerializeField] private int vidaActual = 1;

    [Tooltip("Puntos de salud para la primera barra de vida.")]
    [SerializeField] private float saludVida1 = 500f;

    [Tooltip("Puntos de salud para la segunda barra de vida.")]
    [SerializeField] private float saludVida2 = 500f;

    [SerializeField] private float maxHealth = 500f;
    [SerializeField] private float currentHealth = 500f;

    [Tooltip("Fase actual del jefe: 1 (100-70%), 2 (70-35%), 3 (<35% Enrage).")]
    [SerializeField] private int faseActual = 1;

    [Tooltip("Multiplicador de daño crítico durante el estado Exhausted (PoseCansado).")]
    [SerializeField] private float multiplicadorDanoExhausto = 1.5f;

    [Tooltip("Duración exacta en segundos del estado vulnerable tras el ataque especial.")]
    [SerializeField] private float duracionEstadoExhausto = 3.0f;

    [Tooltip("Intervalo mínimo entre reacciones a impactos para evitar tartamudeo.")]
    [SerializeField] private float intervaloMinimoReaccionGolpe = 0.32f;

    [Tooltip("Si está activo, ataques pesados como Salto y Tornado no se interrumpen por disparos ordinarios.")]
    [SerializeField] private bool hiperArmaduraEnAtaquesPesados = true;

    // ========================================================================= //
    // 4. COMPONENTES Y CONTROLADORES RELACIONADOS                               //
    // ========================================================================= //

    [Header("6. Componentes Integrados")]
    [SerializeField] private Animator animator;
    [SerializeField] private WindowsCommandAttacks commandAttacks;
    [SerializeField] private WindowsAIGeminiDialogue geminiDialogue;
    [SerializeField] private SudoKill9.Audio.WindowsVoiceManager voiceManager;
    [SerializeField] private SudoKill9.Audio.WindowsBossSFX bossSFX;
    [SerializeField] private SudoKill9.UI.WindowsHeadHealthBar headHealthBar;
    [SerializeField] private CapsuleCollider bossCollider;

    [Header("7. Calibración del Collider")]
    [SerializeField] private float alturaMundo = 1.9f;
    [SerializeField] private float radioMundo = 0.42f;

    [Header("8. Parámetros del Animator")]
    [SerializeField] private string distanceParamName = "Distancia_Pinguino";
    [SerializeField] private string animationTimeParamName = "Tiempo_Animacion";
    [SerializeField] private string hitTriggerName = "Hit";
    [SerializeField] private string dieTriggerName = "Die";

    [Header("9. Control de Tiempo de Animación (Inspector)")]
    [SerializeField] private float tiempoAnimacion = 3f;
    [SerializeField] private bool cuentaRegresivaAutomatica = true;
    [SerializeField] private bool repetirEnBucle = false;

    // ========================================================================= //
    // EVENTOS ESTÁTICOS Y PÚBLICOS                                              //
    // ========================================================================= //

    public static event System.Action<float, float, int, int> OnBossLifeChanged;
    public static event System.Action<float, float> OnBossHealthChanged;
    public static event System.Action<int> OnBossPhaseChanged;
    public static event System.Action<BossCombatState> OnBossStateChanged;
    public static event System.Action OnBossDied;

    // ========================================================================= //
    // VARIABLES PRIVADAS DE CONTROL                                             //
    // ========================================================================= //

    private int distanceHash;
    private int animationTimeHash;
    private int hitTriggerHash;
    private int dieTriggerHash;

    private bool hasDistanceParam;
    private bool hasAnimationTimeParam;
    private bool hasHitParam;
    private bool hasDieParam;

    private float ultimoTiempoInspector;
    private float ultimoTiempoGolpe = -999f;
    private float ultimoTiempoRecalculoRuta = -999f;
    private float ultimoTiempoAtaqueMelee = -999f;
    private float ultimoTiempoProvocacion = -999f;
    private float ultimoTiempoEspecialTactico = -999f;
    private float temporizadorExhaustoInterno = 0f;

    private bool isDead = false;
    private bool golpeAplicadoEnCiclo = false;
    private bool movimientoExternoActivo = false;
    private string animacionActual = "Se prepara para atacar";
    private float valorActualEnviadoAlAnimator;

    // ========================================================================= //
    // INICIALIZACIÓN Y CICLO DE VIDA                                            //
    // ========================================================================= //

    private void Awake()
    {
        if (saludVida1 <= 0f) saludVida1 = maxHealth > 0f ? maxHealth : 500f;
        if (saludVida2 <= 0f) saludVida2 = saludVida1;
        vidaActual = 1;
        maxHealth = saludVida1;
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

        if (voiceManager == null)
        {
            voiceManager = GetComponent<SudoKill9.Audio.WindowsVoiceManager>();
            if (voiceManager == null) voiceManager = gameObject.AddComponent<SudoKill9.Audio.WindowsVoiceManager>();
        }

        if (bossSFX == null)
        {
            bossSFX = GetComponent<SudoKill9.Audio.WindowsBossSFX>();
            if (bossSFX == null) bossSFX = gameObject.AddComponent<SudoKill9.Audio.WindowsBossSFX>();
        }

        SudoKill9.Audio.TTSProcessLauncher.EnsureServerRunning();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        // Obligatorio: Mixamo animations tienen desactivado Bake Into Pose; root motion debe ser falso
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        ActualizarFaseActual(false);
    }

    private void OnEnable()
    {
        if (commandAttacks == null)
        {
            commandAttacks = GetComponent<WindowsCommandAttacks>();
        }

        WindowsCommandAttacks.OnSpecialAttackStarted += HandleSpecialAttackStarted;
        WindowsCommandAttacks.OnSpecialAttackFinished += HandleSpecialAttackFinished;
        WindowsCommandAttacks.OnMeleeAttackStarted += HandleMeleeAttackStarted;
        WindowsCommandAttacks.OnMeleeAttackFinished += HandleMeleeAttackFinished;
    }

    private void OnDisable()
    {
        WindowsCommandAttacks.OnSpecialAttackStarted -= HandleSpecialAttackStarted;
        WindowsCommandAttacks.OnSpecialAttackFinished -= HandleSpecialAttackFinished;
        WindowsCommandAttacks.OnMeleeAttackStarted -= HandleMeleeAttackStarted;
        WindowsCommandAttacks.OnMeleeAttackFinished -= HandleMeleeAttackFinished;
    }

    private void HandleSpecialAttackStarted(string ataque)
    {
        if (isDead) return;
        ChangeState(BossCombatState.ExecutingSpecial);
    }

    private void HandleSpecialAttackFinished(string ataque, bool exito)
    {
        if (isDead) return;
        if (commandAttacks != null && commandAttacks.EstaExhausto)
        {
            ChangeState(BossCombatState.Exhausted);
        }
        else
        {
            ChangeState(currentDistanceToTarget <= distanciaRangoMelee ? BossCombatState.MeleeEngage : BossCombatState.Approaching);
        }
    }

    private void HandleMeleeAttackStarted(string ataque)
    {
        if (isDead) return;
        DetenerAgenteNavMesh();
    }

    private void HandleMeleeAttackFinished(string ataque, bool exito)
    {
        if (isDead) return;
        if (currentDistanceToTarget > distanciaSalidaMelee)
        {
            ChangeState(BossCombatState.Approaching);
        }
    }

    private void Start()
    {
        GarantizarComponentesFisicos();
        BuscarJugadorSiEsNecesario();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
            DetectarParametrosEnAnimator();
        }

        valorActualEnviadoAlAnimator = tiempoAnimacion;
        ultimoTiempoInspector = tiempoAnimacion;
        ActualizarParametroTiempoEnAnimator();

        // Asegurar que la barra sobre la cabeza y la burbuja de diálogo 3D existan
        AsegurarBarraSobreCabeza();

        OnBossLifeChanged?.Invoke(currentHealth, maxHealth, vidaActual, vidasTotales);
        OnBossHealthChanged?.Invoke(currentHealth, maxHealth);
        OnBossPhaseChanged?.Invoke(faseActual);
        ChangeState(BossCombatState.Stalking);
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

    // ========================================================================= //
    // BUCLE PRINCIPAL DE ACTUALIZACIÓN (60 FPS - 0 ms LATENCIA)                 //
    // ========================================================================= //

    private void Update()
    {
        if (isDead)
        {
            if (estadoCombate != BossCombatState.Dead) ChangeState(BossCombatState.Dead);
            return;
        }

        // 1. Validar y asegurar referencia activa del jugador
        if (target == null || !target.gameObject.activeInHierarchy || target.GetComponent<LinuxCombatIK>() == null)
        {
            BuscarJugadorSiEsNecesario();
            if (target == null) return;
        }

        if (animator != null && animator.applyRootMotion)
        {
            animator.applyRootMotion = false;
        }

        // 2. Cálculo de distancia horizontal (Plano XZ sin distorsión de altura)
        Vector3 diff = target.position - transform.position;
        diff.y = 0f;
        currentDistanceToTarget = diff.magnitude;

        if (animator != null && hasDistanceParam)
        {
            animator.SetFloat(distanceHash, currentDistanceToTarget);
        }

        // 3. Temporizador de animación (retrocompatibilidad del Animator Controller)
        ActualizarTemporizadorAnimacionInspector();

        // 4. Orientación suave hacia Tux (bloqueando el eje Y)
        if (!movimientoExternoActivo)
        {
            RotarHaciaJugador();
        }

        // 5. Evaluación y ejecución de la Máquina de Estados Jerárquica (HFSM)
        EjecutarHFSM();

        // 6. Sincronización de locomoción y detección de impacto
        GestionarMovimientoSegunAnimacion();
    }

    // ========================================================================= //
    // MÁQUINA DE ESTADOS FINITA JERÁRQUICA (HFSM)                               //
    // ========================================================================= //

    private void EjecutarHFSM()
    {
        // Prioridad 1: Si WindowsCommandAttacks está en estado exhausto
        if (commandAttacks != null && commandAttacks.EstaExhausto)
        {
            if (estadoCombate != BossCombatState.Exhausted)
            {
                ChangeState(BossCombatState.Exhausted);
            }
            ProcesarEstadoExhausted();
            return;
        }

        // Prioridad 2: Si WindowsCommandAttacks está ejecutando una habilidad especial
        if (commandAttacks != null && commandAttacks.EstaEjecutandoAtaqueEspecial)
        {
            if (estadoCombate != BossCombatState.ExecutingSpecial)
            {
                ChangeState(BossCombatState.ExecutingSpecial);
            }
            DetenerAgenteNavMesh();
            return;
        }

        // Si terminó el ataque especial o exhausto, regresar a control táctico
        if (estadoCombate == BossCombatState.ExecutingSpecial || estadoCombate == BossCombatState.Exhausted)
        {
            ChangeState(currentDistanceToTarget <= distanciaRangoMelee ? BossCombatState.MeleeEngage : BossCombatState.Approaching);
        }

        // Evaluación de estados según la situación táctica
        switch (estadoCombate)
        {
            case BossCombatState.Stalking:
                ProcesarEstadoStalking();
                break;

            case BossCombatState.Approaching:
                ProcesarEstadoApproaching();
                break;

            case BossCombatState.MeleeEngage:
                ProcesarEstadoMeleeEngage();
                break;

            case BossCombatState.Dead:
                DetenerAgenteNavMesh();
                break;
        }
    }

    public void ChangeState(BossCombatState nuevoEstado)
    {
        if (estadoCombate == nuevoEstado || (isDead && nuevoEstado != BossCombatState.Dead)) return;

        BossCombatState estadoAnterior = estadoCombate;
        estadoCombate = nuevoEstado;

        // Salida del estado anterior
        if (estadoAnterior == BossCombatState.Exhausted)
        {
            temporizadorExhaustoInterno = 0f;
        }

        // Entrada al nuevo estado
        switch (estadoCombate)
        {
            case BossCombatState.Stalking:
                DetenerAgenteNavMesh();
                ultimoTiempoProvocacion = Time.time;
                break;

            case BossCombatState.Approaching:
                golpeAplicadoEnCiclo = false;
                ActualizarDestinoNavMeshInmediato();
                break;

            case BossCombatState.MeleeEngage:
                DetenerAgenteNavMesh();
                golpeAplicadoEnCiclo = false;
                ultimoTiempoAtaqueMelee = Time.time - (cooldownAtaquesMelee * 0.7f); // Permitir golpe rápido al entrar
                break;

            case BossCombatState.ExecutingSpecial:
                DetenerAgenteNavMesh();
                break;

            case BossCombatState.Exhausted:
                DetenerAgenteNavMesh();
                temporizadorExhaustoInterno = duracionEstadoExhausto;
                if (animator != null)
                {
                    animator.CrossFadeInFixedTime("PoseCansado", 0.15f);
                }
                break;

            case BossCombatState.Dead:
                DetenerAgenteNavMesh();
                if (navAgent != null) navAgent.enabled = false;
                break;
        }

        OnBossStateChanged?.Invoke(estadoCombate);
    }

    // ========================================================================= //
    // LÓGICA DE CADA ESTADO TÁCTICO                                             //
    // ========================================================================= //

    private void ProcesarEstadoStalking()
    {
        // Si Tux entra en rango cuerpo a cuerpo, cambiar a MeleeEngage
        if (currentDistanceToTarget <= distanciaRangoMelee)
        {
            ChangeState(BossCombatState.MeleeEngage);
            return;
        }

        // Si Tux se aleja más allá de la distancia táctica de acecho (+ histéresis), pasar a persecución
        if (currentDistanceToTarget > distanciaAcecho + 1.2f)
        {
            ChangeState(BossCombatState.Approaching);
            return;
        }

        DetenerAgenteNavMesh();

        // Evaluar habilidades especiales tácticas coordinadas con WindowsCommandAttacks
        EvaluarAtaquesEspecialesTacticos();

        // Ocasionalmente reproducir provocación 'Mueve_Cuello' si se mantiene en acecho
        if ((Time.time - ultimoTiempoProvocacion) >= intervaloProvocacionStalking)
        {
            ultimoTiempoProvocacion = Time.time;
            if (animator != null && !IsStateOrTransitioningTo("Mueve_Cuello"))
            {
                animator.CrossFadeInFixedTime("Mueve_Cuello", 0.18f);
                if (geminiDialogue != null)
                {
                    geminiDialogue.SolicitarDialogo(WindowsAIGeminiDialogue.DialogueContext.ProvocacionStalking, "Windows realiza una provocación en acecho.");
                }
            }
        }
    }

    private void ProcesarEstadoApproaching()
    {
        // Transición a combate cercano al entrar al rango melee
        if (currentDistanceToTarget <= distanciaRangoMelee)
        {
            ChangeState(BossCombatState.MeleeEngage);
            return;
        }

        // Evaluar habilidades especiales tácticas a distancia
        EvaluarAtaquesEspecialesTacticos();

        // Actualización optimizada del destino NavMesh
        if ((Time.time - ultimoTiempoRecalculoRuta) >= intervaloRecalculoDestino)
        {
            ultimoTiempoRecalculoRuta = Time.time;
            ActualizarDestinoNavMeshInmediato();
        }
    }

    private void ProcesarEstadoMeleeEngage()
    {
        // Histéresis: si Tux escapa más allá de distanciaSalidaMelee, volver a persecución
        if (currentDistanceToTarget > distanciaSalidaMelee)
        {
            ChangeState(BossCombatState.Approaching);
            return;
        }

        DetenerAgenteNavMesh();

        // Si WindowsCommandAttacks ya está ejecutando un golpe melee, esperar su ciclo físico
        if (commandAttacks != null && commandAttacks.EstaEjecutandoAtaqueMelee)
        {
            return;
        }

        // Si ya expiró el cooldown de ataque melee, seleccionar dinámicamente un ataque
        if ((Time.time - ultimoTiempoAtaqueMelee) >= cooldownAtaquesMelee)
        {
            ultimoTiempoAtaqueMelee = Time.time;
            EjecutarAtaqueMeleeDinamico();
        }
    }

    private void ProcesarEstadoExhausted()
    {
        DetenerAgenteNavMesh();

        if (temporizadorExhaustoInterno > 0f)
        {
            temporizadorExhaustoInterno -= Time.deltaTime;
            if (temporizadorExhaustoInterno <= 0f)
            {
                temporizadorExhaustoInterno = 0f;
                // Si commandAttacks terminó el estado exhausto, volver a acecho
                if (commandAttacks != null && !commandAttacks.EstaExhausto)
                {
                    ChangeState(BossCombatState.Stalking);
                }
            }
        }
    }

    // ========================================================================= //
    // SELECCIÓN Y EJECUCIÓN DINÁMICA DE ATAQUES MELEE                           //
    // ========================================================================= //

    private void EjecutarAtaqueMeleeDinamico()
    {
        if (isDead) return;

        golpeAplicadoEnCiclo = false;

        // Selección según distancia y probabilidad:
        // - Distancia muy cercana (<= 1.8m): Patada o Izquiedazo
        // - Distancia media (1.8m - 2.5m): Ataque_Patada o Tornado
        string ataqueElegido;

        if (currentDistanceToTarget <= 1.8f)
        {
            ataqueElegido = (Random.value > 0.5f) ? "Patada" : "Izquiedazo";
        }
        else
        {
            ataqueElegido = (Random.value > 0.5f) ? "Ataque_Patada" : "Tornado";
        }

        if (commandAttacks != null)
        {
            commandAttacks.EjecutarAtaqueMelee(ataqueElegido);
        }
        else if (animator != null)
        {
            animator.CrossFadeInFixedTime(ataqueElegido, 0.12f);
        }
    }

    /// <summary>
    /// Evalúa tácticamente cuándo solicitar habilidades especiales de WindowsCommandAttacks
    /// según la fase actual del jefe, distancia relativa hacia Tux y disponibilidad de recarga.
    /// </summary>
    public void EvaluarAtaquesEspecialesTacticos()
    {
        if (!coordinarAtaquesEspeciales || commandAttacks == null || isDead) return;
        if (commandAttacks.EstaEjecutandoAtaqueEspecial || commandAttacks.EstaEjecutandoAtaqueMelee || commandAttacks.EstaExhausto) return;
        if ((Time.time - ultimoTiempoEspecialTactico) < cooldownEspecialesTacticos) return;

        // Fase 3 (o vida 2): 65% probabilidad de Magic Fire 1 (Homing destructor) o Magic Fire 0
        if (faseActual >= 3 && Random.value < 0.65f)
        {
            ultimoTiempoEspecialTactico = Time.time;
            commandAttacks.EjecutarAtaqueMagicFire1();
            return;
        }

        // Fase >= 2: Taskkill para ralentizar a Tux si está huyendo/a distancia
        if (faseActual >= 2 && currentDistanceToTarget >= 5.0f && Random.value < 0.45f)
        {
            ultimoTiempoEspecialTactico = Time.time;
            commandAttacks.EjecutarTaskkill();
            return;
        }

        // Fase >= 1: Magic Fire 0 aéreo si está a distancia media o larga (>= 3.5m)
        if (currentDistanceToTarget >= 3.5f)
        {
            ultimoTiempoEspecialTactico = Time.time;
            commandAttacks.EjecutarAtaqueMagicFire0();
            return;
        }
    }

    // ========================================================================= //
    // GESTIÓN DE LOCOMOCIÓN Y ANIMACIONES                                       //
    // ========================================================================= //

    private void GestionarMovimientoSegunAnimacion()
    {
        if (!moverHaciaObjetivo || animator == null) return;
        if (movimientoExternoActivo || (commandAttacks != null && (commandAttacks.EstaEjecutandoAtaqueEspecial || commandAttacks.EstaEjecutandoAtaqueMelee)))
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
    /// Mantenido con firma exacta para compatibilidad con la suite de verificación automatizada.
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

    // ========================================================================= //
    // INTEGRACIÓN Y PROTECCIÓN DE MOVIMIENTO EXTERNO (NAVMESH)                  //
    // ========================================================================= //

    /// <summary>
    /// Pausa temporalmente el NavMeshAgent para permitir fintas físicas o desplazamientos balísticos (ej. SaltaLoco).
    /// </summary>
    public void BeginExternalMovement()
    {
        movimientoExternoActivo = true;
        DetenerAgenteNavMesh();
    }

    /// <summary>
    /// Reanuda el NavMeshAgent al finalizar el movimiento externo, sincronizando la posición sobre la malla navegable.
    /// </summary>
    public void EndExternalMovement()
    {
        if (navAgent != null && navAgent.enabled)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.5f, NavMesh.AllAreas))
            {
                navAgent.Warp(hit.position);
            }
            else
            {
                navAgent.Warp(transform.position);
            }

            if (navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
            }
        }
        movimientoExternoActivo = false;
    }

    private void ActualizarDestinoNavMeshInmediato()
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
                navAgent.SetDestination(target.position);
            }
        }
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
            // Fallback a traslación horizontal directa si no hay NavMesh horneado
            if (currentDistanceToTarget <= stoppingDistance) return;

            Vector3 targetPos = target.position;
            targetPos.y = transform.position.y;
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

    // ========================================================================= //
    // SISTEMA DE DAÑO Y MUERTE (IDamageable)                                    //
    // ========================================================================= //

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        float danoFinal = Mathf.Abs(amount);

        // Ventana de castigo crítico en PoseCansado / Exhausted (1.5x daño)
        bool estaEnVulnerabilidad = (estadoCombate == BossCombatState.Exhausted) || 
                                   (commandAttacks != null && commandAttacks.EstaExhausto);
        if (estaEnVulnerabilidad)
        {
            danoFinal *= multiplicadorDanoExhausto;
        }

        currentHealth -= danoFinal;

        if (currentHealth <= 0f)
        {
            if (vidaActual < vidasTotales)
            {
                // ========================================================= //
                // TRANSICIÓN AUTOMÁTICA A LA SEGUNDA VIDA                   //
                // ========================================================= //
                vidaActual++;
                maxHealth = saludVida2;
                currentHealth = saludVida2;

                ActualizarFaseActual(true);
                OnBossLifeChanged?.Invoke(currentHealth, maxHealth, vidaActual, vidasTotales);
                OnBossHealthChanged?.Invoke(currentHealth, maxHealth);

                WindowsCommandAttacks.EmitirComando("> [KERNEL REBOOT] ¡PRIMERA VIDA AGOTADA! Núcleo de respaldo activado. Iniciando SEGUNDA VIDA (VIDA 2/2)...");

                if (animator != null)
                {
                    animator.CrossFadeInFixedTime("Grito", 0.15f);
                }

                if (geminiDialogue != null)
                {
                    geminiDialogue.SolicitarDialogo(WindowsAIGeminiDialogue.DialogueContext.EntrarFase3, "Windows entra en su segunda vida con furia y overclocking.", forzarInmediato: true);
                }
            }
            else
            {
                // Muerte definitiva: ÚNICAMENTE cuando ambas vidas se han agotado
                currentHealth = 0f;
                OnBossLifeChanged?.Invoke(0f, maxHealth, vidaActual, vidasTotales);
                OnBossHealthChanged?.Invoke(0f, maxHealth);
                Morir();
            }
        }
        else
        {
            ActualizarFaseActual(true);
            OnBossLifeChanged?.Invoke(currentHealth, maxHealth, vidaActual, vidasTotales);
            OnBossHealthChanged?.Invoke(currentHealth, maxHealth);
            RecibirGolpe();
        }
    }

    private void ActualizarFaseActual(bool dispararEvento)
    {
        int nuevaFase;
        if (vidaActual == 1)
        {
            float ratio = currentHealth / Mathf.Max(1f, maxHealth);
            if (ratio > 0.70f) nuevaFase = 1;
            else if (ratio > 0.35f) nuevaFase = 2;
            else nuevaFase = 3;
        }
        else
        {
            // Segunda vida: modo Enrage permanente y ataques desbloqueados
            nuevaFase = 3;
        }

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

    public void RecibirDisparo(float dano, Vector3 puntoImpacto, Vector3 direccionBala)
    {
        if (commandAttacks != null && commandAttacks.NotificarImpactoEnEscudo(null, puntoImpacto, direccionBala))
        {
            if (bossSFX != null) bossSFX.ReproducirBloqueoFirewall();
            return; // Bloqueado frontalmente por el firewall de Windows
        }

        if (bossSFX != null) bossSFX.ReproducirImpactoHit();
        TakeDamage(dano);
    }

    public void RecibirGolpe()
    {
        if (isDead) return;

        if (bossSFX != null) bossSFX.ReproducirImpactoHit();

        if (animator != null)
        {
            if (hasHitParam)
            {
                animator.ResetTrigger(hitTriggerHash);
            }

            // Hiperarmadura en ataques pesados y habilidades especiales
            if (hiperArmaduraEnAtaquesPesados && (IsStateOrTransitioningTo("Salto") || IsStateOrTransitioningTo("Tornado")))
            {
                return;
            }

            if (commandAttacks != null && commandAttacks.EstaEjecutandoAtaqueEspecial)
            {
                return;
            }

            bool yaEnHit = IsStateOrTransitioningTo("Hit");
            if (!yaEnHit || (Time.time - ultimoTiempoGolpe) >= intervaloMinimoReaccionGolpe)
            {
                ultimoTiempoGolpe = Time.time;
                animator.CrossFadeInFixedTime("Hit", 0.08f, 0, 0f);
            }
        }
    }

    public void RestaurarVida(float nuevaVida)
    {
        currentHealth = Mathf.Max(0f, nuevaVida);
        isDead = currentHealth <= 0f;
        if (!isDead && navAgent != null && !navAgent.enabled)
        {
            navAgent.enabled = true;
        }
        ActualizarFaseActual(true);
        if (!isDead) ChangeState(BossCombatState.Stalking);
        OnBossLifeChanged?.Invoke(currentHealth, maxHealth, vidaActual, vidasTotales);
        OnBossHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Restablece completamente el sistema de dos vidas para reinicios limpios de combate o testing.
    /// </summary>
    public void RestaurarDobleVida(float vida1 = 500f, float vida2 = 500f, int vidaIndex = 1)
    {
        saludVida1 = Mathf.Max(1f, vida1);
        saludVida2 = Mathf.Max(1f, vida2);
        vidaActual = Mathf.Clamp(vidaIndex, 1, vidasTotales);
        maxHealth = (vidaActual == 1) ? saludVida1 : saludVida2;
        currentHealth = maxHealth;
        isDead = false;

        if (navAgent != null && !navAgent.enabled)
        {
            navAgent.enabled = true;
        }

        ActualizarFaseActual(true);
        if (!isDead) ChangeState(BossCombatState.Stalking);
        OnBossLifeChanged?.Invoke(currentHealth, maxHealth, vidaActual, vidasTotales);
        OnBossHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void Morir()
    {
        isDead = true;
        currentHealth = 0f;
        ChangeState(BossCombatState.Dead);
        DetenerAgenteNavMesh();
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.enabled = false;
        }

        if (commandAttacks != null)
        {
            commandAttacks.LimpiarAtaquesActivosPublico();
        }

        OnBossLifeChanged?.Invoke(0f, maxHealth, vidaActual, vidasTotales);
        OnBossHealthChanged?.Invoke(0f, maxHealth);
        OnBossDied?.Invoke();

        if (animator != null && hasDieParam)
        {
            animator.SetTrigger(dieTriggerHash);
        }
    }

    public bool IsDead => isDead;

    // ========================================================================= //
    // FÍSICAS Y CALIBRACIÓN DE COMPONENTES                                      //
    // ========================================================================= //

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
            navAgent = GetComponent<NavMeshAgent>();
        }
        if (navAgent != null)
        {
            navAgent.stoppingDistance = stoppingDistance;
            navAgent.updateRotation = false;
        }
    }

    private void BuscarJugadorSiEsNecesario()
    {
        var combatPlayer = Object.FindFirstObjectByType<LinuxCombatIK>();
        if (combatPlayer != null)
        {
            target = combatPlayer.transform;
            return;
        }

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

        GameObject playerObj = GameObject.Find("linux");
        if (playerObj == null) playerObj = GameObject.Find("Player_LinuxSkeleton");
        if (playerObj == null) playerObj = GameObject.Find("Player_Linux");
        if (playerObj == null) playerObj = GameObject.Find("linux-skeleton");

        if (playerObj == null)
        {
            try
            {
                playerObj = GameObject.FindWithTag("Player");
            }
            catch {}
        }

        if (playerObj != null)
        {
            var foundIK = playerObj.GetComponentInChildren<LinuxCombatIK>();
            target = (foundIK != null) ? foundIK.transform : playerObj.transform;
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
            if (IsStateOrTransitioningTo("Se prepara para atacar"))
            {
                valorActualEnviadoAlAnimator = tiempoAnimacion;
            }
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

    public BossCombatState EstadoCombate => estadoCombate;
    public NavMeshAgent NavAgent => navAgent;
    public WindowsAIGeminiDialogue GeminiDialogue => geminiDialogue;
    public SudoKill9.Audio.WindowsVoiceManager VoiceManager => voiceManager;
    public SudoKill9.Audio.WindowsBossSFX BossSFX => bossSFX;
    public float CurrentDistanceToTarget => currentDistanceToTarget;
    public string AnimacionActual => animacionActual;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public int FaseActual => faseActual;
    public int VidasTotales => vidasTotales;
    public int VidaActual => vidaActual;
    public float SaludVida1 => saludVida1;
    public float SaludVida2 => saludVida2;
    public WindowsCommandAttacks CommandAttacks => commandAttacks;
    public float MultiplicadorDanoExhausto => multiplicadorDanoExhausto;
    public float DuracionEstadoExhausto => duracionEstadoExhausto;

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

    public SudoKill9.UI.WindowsHeadHealthBar HeadHealthBar => headHealthBar;

    /// <summary>
    /// Garantiza que exista la barra de salud 3D y burbuja de diálogo flotante sobre la cabeza de Windows.
    /// Si no existe en la escena, crea automáticamente el GameObject dedicado en World Space.
    /// </summary>
    public void AsegurarBarraSobreCabeza()
    {
        if (headHealthBar == null)
        {
            headHealthBar = GetComponentInChildren<SudoKill9.UI.WindowsHeadHealthBar>();
            if (headHealthBar == null)
            {
                headHealthBar = Object.FindFirstObjectByType<SudoKill9.UI.WindowsHeadHealthBar>();
            }
        }

        if (headHealthBar == null)
        {
            GameObject headBarGO = new GameObject("WindowsHeadHealthBar");
            headHealthBar = headBarGO.AddComponent<SudoKill9.UI.WindowsHeadHealthBar>();
        }

        if (headHealthBar != null)
        {
            headHealthBar.ConfigurarParaBoss(this);
        }
    }
}
