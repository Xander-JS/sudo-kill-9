using System.Collections;
using UnityEngine;
using SudoKill9.Combat;

/// <summary>
/// Gestiona los ataques de comandos especiales del jefe final Windows:
/// 1. Esquive Dinámico con 'SaltaLoco':
///    Detecta proyectiles entrantes y realiza una finta lateral errática.
/// 2. Comando 'taskkill':
///    Aplica un debuff de ralentización al jugador Linux Tux (30% velocidad por 2.5s).
/// 3. Comando 'netsh advfirewall':
///    Levanta un escudo holográfico frontal que bloquea/desvía proyectiles durante 4s.
/// 4. ATAQUE 1: Magic Fire 0 + Explosion + RotatorPS2:
///    Ataque aéreo telegrafiado que cae sobre la posición inicial de Linux (permitiendo esquivar).
///    Si impacta, detona 'Explosion', activa BSOD durante 5 segundos y desata el tornado 'RotatorPS2'.
/// 5. ATAQUE 2: Magic Fire 1 (Proyectil Homing de Persecución):
///    Dispara 'Magic Fire 1' persiguiendo fluidamente a Linux durante máx 3 segundos.
///    Al impactar o expirar el tiempo, detona 'Explosion', dejando a Windows vulnerable en 'PoseCansado' (1.5x daño).
/// 6. Evento BSOD (Pantalla Azul de la Muerte):
///    Efecto visual cinemático del sistema de pantalla azul (duración exacta 5s).
/// </summary>
public class WindowsCommandAttacks : MonoBehaviour
{
    // ========================================================================= //
    // EVENTOS ESTÁTICOS                                                         //
    // ========================================================================= //

    /// <summary>Evento emitido al ejecutar cualquier comando para mostrarlo en el terminal CMD.</summary>
    public static event System.Action<string> OnCommandExecuted;

    /// <summary>Evento emitido al dispararse la Pantalla Azul de la Muerte.</summary>
    public static event System.Action OnBSODTriggered;

    /// <summary>Evento emitido al dispararse la Pantalla Azul con duración personalizada.</summary>
    public static event System.Action<float> OnBSODTriggeredWithDuration;

    /// <summary>Emite el evento de BSOD con la duración deseada sin disparos redundantes.</summary>
    public static void NotificarBSOD(float duracion = 5.0f)
    {
        if (OnBSODTriggeredWithDuration != null)
        {
            OnBSODTriggeredWithDuration.Invoke(duracion);
        }
        else
        {
            OnBSODTriggered?.Invoke();
        }
    }

    // ========================================================================= //
    // 1. CONFIGURACIÓN DE ESQUIVE (SaltaLoco)                                   //
    // ========================================================================= //

    [Header("1. Esquive Dinámico (SaltaLoco)")]
    [Tooltip("Habilita o deshabilita la detección y esquive reactivo de balas.")]
    [SerializeField] private bool esquiveHabilitado = true;

    [Tooltip("Radio de detección volumétrica de proyectiles entrantes (m).")]
    [SerializeField] private float radioDeteccionBalas = 7.0f;

    [Tooltip("Tiempo de enfriamiento entre esquives consecutivos (segundos).")]
    [SerializeField] private float cooldownEsquive = 4.0f;

    [Tooltip("Distancia de desplazamiento lateral al esquivar.")]
    [SerializeField] private float distanciaEsquive = 2.4f;

    [Tooltip("Capas evaluadas para detectar balas entrantes.")]
    [SerializeField] private LayerMask capaBalas = ~0;

    private float ultimoTiempoEsquive = -999f;
    private bool estaEsquivando = false;

    // ========================================================================= //
    // 2. CONFIGURACIÓN DE TASKKILL                                              //
    // ========================================================================= //

    [Header("2. Comando taskkill (Ralentización)")]
    [Tooltip("Habilita el debuff periódico de velocidad sobre Tux.")]
    [SerializeField] private bool taskkillHabilitado = true;

    [Tooltip("Intervalo en segundos entre usos de taskkill.")]
    [SerializeField] private float intervaloTaskkill = 13.0f;

    [Tooltip("Duración de la ralentización sobre Tux.")]
    [SerializeField] private float duracionRalentizacion = 2.5f;

    [Tooltip("Factor de velocidad resultante para Tux (0.3 = 30% de velocidad).")]
    [SerializeField] private float factorRalentizacion = 0.3f;

    private float temporizadorTaskkill = 8.0f;

    // ========================================================================= //
    // 3. CONFIGURACIÓN DE NETSH FIREWALL                                        //
    // ========================================================================= //

    [Header("3. Comando netsh firewall (Escudo Frontal)")]
    [Tooltip("Habilita el escudo holográfico que bloquea balas frontales.")]
    [SerializeField] private bool firewallHabilitado = true;

    [Tooltip("Intervalo en segundos entre activaciones de firewall.")]
    [SerializeField] private float intervaloFirewall = 17.0f;

    [Tooltip("Duración activa del escudo en segundos.")]
    [SerializeField] private float duracionFirewall = 4.0f;

    [Tooltip("Objeto o prefab visual del escudo holográfico.")]
    [SerializeField] private GameObject escudoVisual;

    private float temporizadorFirewall = 12.0f;
    private bool firewallActivo = false;
    private Coroutine corrutinaFirewall;

    // ========================================================================= //
    // 4. ATAQUE 1: MAGIC FIRE 0 + EXPLOSION + ROTATORPS2                        //
    // ========================================================================= //

    [Header("4. Ataque 1: Magic Fire 0 (Aéreo + Esquive + BSOD + Tornado)")]
    [Tooltip("Habilita el ataque aéreo orbital telegrafiado.")]
    [SerializeField] private bool magicFire0Habilitado = true;

    [Tooltip("Intervalo en segundos entre lanzamientos de Magic Fire 0.")]
    [SerializeField] private float intervaloMagicFire0 = 11.0f;

    [Tooltip("Fase mínima de combate de Windows para activar Magic Fire 0.")]
    [SerializeField] private int faseMinimaMagicFire0 = 1;

    [Tooltip("Altura sobre el punto capturado desde donde cae Magic Fire 0 (m).")]
    [SerializeField] private float alturaInicialMagicFire0 = 14.0f;

    [Tooltip("Velocidad de descenso de Magic Fire 0 (m/s).")]
    [SerializeField] private float velocidadCaidaMagicFire0 = 16.0f;

    [Tooltip("Radio de la zona de impacto para verificar si Linux esquivó o recibió el impacto.")]
    [SerializeField] private float radioImpactoMagicFire0 = 3.2f;

    [Tooltip("Daño infligido por Magic Fire 0 al acertar.")]
    [SerializeField] private float danoMagicFire0 = 1.0f;

    [Tooltip("Duración de la Pantalla Azul (BSOD) en segundos activada por el impacto.")]
    [SerializeField] private float duracionBSODMagicFire0 = 5.0f;

    [Tooltip("Duración del tornado RotatorPS2 alrededor de Linux.")]
    [SerializeField] private float duracionRotatorPS2 = 5.0f;

    [Tooltip("Escala inicial del tornado RotatorPS2.")]
    [SerializeField] private Vector3 escalaRotatorPS2 = new Vector3(1.2f, 1.2f, 1.2f);

    private float temporizadorMagicFire0 = 4.0f;
    private bool estaEjecutandoMagicFire0 = false;
    private GameObject instanciaMagicFire0Activa;

    // ========================================================================= //
    // 5. ATAQUE 2: MAGIC FIRE 1 (Proyectil Homing de Persecución)               //
    // ========================================================================= //

    [Header("5. Ataque 2: Magic Fire 1 (Proyectil Homing + Exhausto)")]
    [Tooltip("Habilita el proyectil telegrafiado de persecución homing.")]
    [SerializeField] private bool magicFire1Habilitado = true;

    [Tooltip("Compatibilidad con inspector previo de Kamehameha.")]
    [SerializeField] private bool kamehamehaHabilitado = true;

    [Tooltip("Intervalo en segundos entre disparos de Magic Fire 1.")]
    [SerializeField] private float intervaloMagicFire1 = 14.0f;
    [SerializeField] private float intervaloKamehameha = 14.0f;

    [Tooltip("Fase mínima de combate para activar Magic Fire 1.")]
    [SerializeField] private int faseMinimaMagicFire1 = 2;

    [Tooltip("Tiempo de carga telegrafiado antes de disparar el proyectil (segundos).")]
    [SerializeField] private float tiempoCarga = 1.6f;

    [Tooltip("Velocidad de avance lineal de Magic Fire 1 (m/s).")]
    [SerializeField] private float velocidadPersecucionMagicFire1 = 15.0f;

    [Tooltip("Velocidad angular de giro para arquear suavemente (grados/s).")]
    [SerializeField] private float velocidadGiroMagicFire1 = 160.0f;

    [Tooltip("Duración máxima de persecución activa de Magic Fire 1 (segundos).")]
    [SerializeField] private float duracionHomingMagicFire1 = 3.0f;

    [Tooltip("Radio de proximidad para registrar impacto directo sobre Linux.")]
    [SerializeField] private float radioContactoMagicFire1 = 1.1f;

    [Tooltip("Radio de la explosión al agotarse los 3 segundos.")]
    [SerializeField] private float radioExplosionMagicFire1 = 2.8f;

    [Tooltip("Daño infligido por el impacto o explosión de Magic Fire 1.")]
    [SerializeField] private float danoMagicFire1 = 1.0f;

    [Tooltip("Tiempo en PoseCansado donde Windows queda vulnerable a 1.5x daño crítico (segundos).")]
    [SerializeField] private float tiempoExhausto = 3.0f;

    [Tooltip("Escala del prefab Explosion para los ataques.")]
    [SerializeField] private Vector3 escalaExplosion = new Vector3(1.5f, 1.5f, 1.5f);

    [Tooltip("Desplazamiento local del punto de emisión respecto al boss (X, Y, Z).")]
    [SerializeField] private Vector3 offsetDisparo = new Vector3(0f, 1.3f, 2.4f);

    [Header("5.1 Animaciones de Combate")]
    [Tooltip("Animación durante la carga del ataque.")]
    [SerializeField] private string animacionCarga = "Se prepara para atacar";
    [SerializeField] private float transicionCarga = 0.15f;

    [Tooltip("Animación durante el lanzamiento del proyectil.")]
    [SerializeField] private string animacionDisparo = "Grito";
    [SerializeField] private float transicionDisparo = 0.10f;

    [Tooltip("Animación de agotamiento/vulnerabilidad tras disparar.")]
    [SerializeField] private string animacionExhausto = "PoseCansado";
    [SerializeField] private float transicionExhausto = 0.15f;

    [Tooltip("Animación al recuperarse de la vulnerabilidad.")]
    [SerializeField] private string animacionRecuperacion = "Se prepara para atacar";
    [SerializeField] private float transicionRecuperacion = 0.20f;

    // Campos de compatibilidad con reflexión y tests existentes
    private bool estaCargandoRayo = false;
    private bool estaDisparandoRayo = false;
    private bool estaExhausto = false;
    private bool estaEjecutandoMagicFire1 = false;
    private GameObject instanciaMagicFire1Activa;
    private Coroutine corrutinaMagicFire1;

    // ========================================================================= //
    // 6. PREFABS DE EFECTOS HOVL STUDIO                                         //
    // ========================================================================= //

    [Header("6. Prefabs Reales de Hovl Studio")]
    [Tooltip("Prefab del Ataque 1 (Caída aérea): Assets/Hovl Studio/Procedural fire/Prefabs/Magic fire 0.prefab")]
    [SerializeField] private GameObject prefabMagicFire0;

    [Tooltip("Prefab del Ataque 2 (Homing 3s): Assets/Hovl Studio/Procedural fire/Prefabs/Magic fire 1.prefab")]
    [SerializeField] private GameObject prefabMagicFire1;

    [Tooltip("Prefab del Tornado que rodea a Linux: Assets/Hovl Studio/Procedural fire/Prefabs/RotatorPS2.prefab")]
    [SerializeField] private GameObject prefabRotatorPS2;

    [Tooltip("Prefab de Impacto/Detonación: Assets/Hovl Studio/Procedural fire/Prefabs/Explosion.prefab")]
    [SerializeField] private GameObject prefabExplosion;

    // Referencias legadas de LineRenderers (desactivadas para evitar líneas rígidas estáticas)
    [SerializeField] private LineRenderer guiaLaserCarga;
    [SerializeField] private LineRenderer hazKamehameha;
    [SerializeField] private bool mostrarRayoLineRenderer = false;
    [SerializeField] private bool mostrarGuiaLaserCarga = false;

    // ========================================================================= //
    // 7. EVENTO BSOD (Pantalla Azul de la Muerte)                               //
    // ========================================================================= //

    [Header("7. Pantalla Azul de la Muerte (BSOD)")]
    [Tooltip("Porcentaje de vida (0 a 1) para activar el crash de pantalla azul al 15% de vida.")]
    [SerializeField] private float umbralVidaBSOD = 0.15f;

    private bool bsodDisparadoPorVida = false;

    // ========================================================================= //
    // REFERENCIAS INTERNAS                                                      //
    // ========================================================================= //

    private WindowsBossAI bossAI;
    private Animator animator;
    private Transform playerTarget;
    private LinuxCombatIK playerCombatIK;

    // ========================================================================= //
    // PROPIEDADES PÚBLICAS                                                      //
    // ========================================================================= //

    public bool EstaExhausto => estaExhausto;
    public bool EstaCargandoRayo => estaCargandoRayo;
    public bool EstaDisparandoRayo => estaDisparandoRayo;
    public bool EstaEjecutandoMagicFire0 => estaEjecutandoMagicFire0;
    public bool EstaEjecutandoMagicFire1 => estaEjecutandoMagicFire1;
    public bool FirewallActivo => firewallActivo;
    public bool EstaEjecutandoAtaqueEspecial => 
        estaCargandoRayo || 
        estaDisparandoRayo || 
        estaExhausto || 
        estaEsquivando || 
        estaEjecutandoMagicFire0 || 
        estaEjecutandoMagicFire1;

    private void Awake()
    {
        bossAI = GetComponent<WindowsBossAI>();
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (intervaloMagicFire1 <= 0f && intervaloKamehameha > 0f) intervaloMagicFire1 = intervaloKamehameha;

        GarantizarComponentesVisuales();
    }

    private void Start()
    {
        BuscarJugador();
        GarantizarComponentesVisuales();
    }

    private void Update()
    {
        if (bossAI == null || bossAI.IsDead)
        {
            LimpiarAtaquesActivos();
            return;
        }

        if (playerTarget == null || playerCombatIK == null)
        {
            BuscarJugador();
        }

        // 1. Verificación pasiva de BSOD al descender del 15% de vida
        VerificarBSOD();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Atajos de prueba rápida en Play Mode: 1 = Magic Fire 0, 2 = Magic Fire 1, 3 = BSOD 5s
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            EjecutarAtaqueMagicFire0();
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            EjecutarAtaqueMagicFire1();
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            DispararBSOD();
        }
#endif

        // 2. Si está ejecutando un ataque especial activo, no iniciar nuevas rutinas
        if (EstaEjecutandoAtaqueEspecial)
        {
            return;
        }

        int fase = bossAI.FaseActual;

        // ATAQUE 1: Magic Fire 0 (Disponible desde Fase 1 en adelante)
        if (fase >= faseMinimaMagicFire0 && magicFire0Habilitado)
        {
            temporizadorMagicFire0 -= Time.deltaTime;
            if (temporizadorMagicFire0 <= 0f)
            {
                temporizadorMagicFire0 = intervaloMagicFire0;
                EjecutarAtaqueMagicFire0();
                return;
            }
        }

        // FASE 2 y FASE 3: Esquive SaltaLoco, taskkill y netsh firewall
        if (fase >= 2)
        {
            if (esquiveHabilitado && (Time.time - ultimoTiempoEsquive) >= cooldownEsquive)
            {
                DetectarBalasYEsquivar();
            }

            if (taskkillHabilitado)
            {
                temporizadorTaskkill -= Time.deltaTime;
                if (temporizadorTaskkill <= 0f)
                {
                    temporizadorTaskkill = intervaloTaskkill;
                    EjecutarTaskkill();
                }
            }

            if (firewallHabilitado && !firewallActivo)
            {
                temporizadorFirewall -= Time.deltaTime;
                if (temporizadorFirewall <= 0f)
                {
                    temporizadorFirewall = intervaloFirewall;
                    EjecutarFirewall();
                }
            }
        }

        // ATAQUE 2: Magic Fire 1 (Homing persecución 3s + Explosión)
        bool habilitadoF2 = (magicFire1Habilitado || kamehamehaHabilitado);
        if (fase >= faseMinimaMagicFire1 && habilitadoF2)
        {
            temporizadorMagicFire1Internal -= Time.deltaTime;
            if (temporizadorMagicFire1Internal <= 0f)
            {
                temporizadorMagicFire1Internal = intervaloMagicFire1;
                EjecutarAtaqueMagicFire1();
            }
        }
    }

    private float temporizadorMagicFire1Internal = 6.0f;

    // ========================================================================= //
    // ATAQUE 1: MAGIC FIRE 0 + EXPLOSION + ROTATORPS2                           //
    // ========================================================================= //

    [ContextMenu("Ejecutar Ataque 1: Magic Fire 0")]
    public void EjecutarAtaqueMagicFire0()
    {
        if (bossAI != null && bossAI.IsDead) return;
        if (estaEjecutandoMagicFire0 || estaEjecutandoMagicFire1 || estaEsquivando) return;

        StartCoroutine(RutinaMagicFire0());
    }

    private IEnumerator RutinaMagicFire0()
    {
        BuscarJugador();
        if (playerTarget == null) yield break;

        estaEjecutandoMagicFire0 = true;

        // 1. REGLA FUNDAMENTAL: Capturar la posición actual de Linux al inicio exacto del ataque
        Vector3 posicionCapturadaLinux = playerTarget.position;

        EmitirComando("> C:\\Windows\\System32> powershell.exe -Command \"Invoke-OrbitalStrike -Target Linux\"");
        EmitirComando($"> [MAGIC FIRE 0] Ataque aéreo descendiendo en ({posicionCapturadaLinux.x:0.1}, {posicionCapturadaLinux.z:0.1})... ¡ESQUIVA!");

        // Animación de telegrafiado en Windows
        if (animator != null && !string.IsNullOrEmpty(animacionCarga))
        {
            animator.CrossFadeInFixedTime(animacionCarga, 0.12f);
        }

        // Breve telegrafiado visual antes de soltar el proyectil (0.35s)
        yield return new WaitForSeconds(0.35f);

        if (bossAI != null && bossAI.IsDead)
        {
            estaEjecutandoMagicFire0 = false;
            yield break;
        }

        // 2. Instanciar prefab Magic Fire 0 en la posición elevada sobre el punto registrado
        GarantizarComponentesVisuales();
        if (prefabMagicFire0 != null)
        {
            Vector3 posAparicion = posicionCapturadaLinux + Vector3.up * alturaInicialMagicFire0;
            instanciaMagicFire0Activa = Instantiate(prefabMagicFire0, posAparicion, Quaternion.Euler(90f, 0f, 0f));
            instanciaMagicFire0Activa.name = "Instancia_MagicFire0_Aereo";

            MagicFire0AereoController aereoCtrl = instanciaMagicFire0Activa.GetComponent<MagicFire0AereoController>();
            if (aereoCtrl == null) aereoCtrl = instanciaMagicFire0Activa.AddComponent<MagicFire0AereoController>();

            bool ataqueTerminado = false;

            aereoCtrl.Inicializar(
                posicionCapturadaLinux,
                playerTarget,
                prefabExplosion,
                prefabRotatorPS2,
                alturaInicialMagicFire0,
                velocidadCaidaMagicFire0,
                radioImpactoMagicFire0,
                danoMagicFire0,
                duracionBSODMagicFire0,
                duracionRotatorPS2,
                escalaRotatorPS2,
                escalaExplosion,
                callbackFin: () => { ataqueTerminado = true; }
            );

            // Esperar mientras cae y procesa impacto
            while (!ataqueTerminado && instanciaMagicFire0Activa != null)
            {
                if (bossAI != null && bossAI.IsDead)
                {
                    if (instanciaMagicFire0Activa != null) Destroy(instanciaMagicFire0Activa);
                    break;
                }
                yield return null;
            }
        }

        estaEjecutandoMagicFire0 = false;
        instanciaMagicFire0Activa = null;
    }

    // ========================================================================= //
    // ATAQUE 2: MAGIC FIRE 1 (Proyectil Homing 3s + Explosion + PoseCansado)    //
    // ========================================================================= //

    [ContextMenu("Ejecutar Ataque 2: Magic Fire 1")]
    public void EjecutarAtaqueMagicFire1()
    {
        if (bossAI != null && bossAI.IsDead) return;
        if (estaEjecutandoMagicFire1 || estaEjecutandoMagicFire0 || estaExhausto) return;

        if (corrutinaMagicFire1 != null) StopCoroutine(corrutinaMagicFire1);
        corrutinaMagicFire1 = StartCoroutine(RutinaMagicFire1());
    }

    /// <summary>
    /// Reemplazo directo del anterior rayo Kamehameha estático.
    /// Ejecuta el nuevo ataque visual con prefab Magic Fire 1 (homing) y Explosión.
    /// </summary>
    public void IniciarKamehameha()
    {
        EjecutarAtaqueMagicFire1();
    }

    private IEnumerator RutinaMagicFire1()
    {
        BuscarJugador();
        if (playerTarget == null) yield break;

        // --------------------------------------------------------------------- //
        // FASE 1: TEGRAFIADO Y CARGA                                            //
        // --------------------------------------------------------------------- //
        estaCargandoRayo = true;
        estaDisparandoRayo = false;
        estaExhausto = false;
        estaEjecutandoMagicFire1 = true;

        EmitirComando("> powershell.exe -ExecutionPolicy Bypass -Command \"Invoke-MagicFireHomingStrike\"");
        EmitirComando("> [MAGIC FIRE 1] Concentrando plasma dirigido... ¡PROYECTIL HOMING EN CAMINO!");

        if (animator != null && !string.IsNullOrEmpty(animacionCarga))
        {
            animator.CrossFadeInFixedTime(animacionCarga, transicionCarga);
        }

        float transcurridoCarga = 0f;
        while (transcurridoCarga < tiempoCarga)
        {
            if (bossAI != null && bossAI.IsDead)
            {
                LimpiarAtaquesActivos();
                yield break;
            }

            transcurridoCarga += Time.deltaTime;

            // Orientar gradualmente hacia Linux durante la carga
            if (playerTarget != null)
            {
                Vector3 dir = playerTarget.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    Quaternion rot = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rot, 4.0f * Time.deltaTime);
                }
            }

            yield return null;
        }

        estaCargandoRayo = false;

        // --------------------------------------------------------------------- //
        // FASE 2: DISPARO Y PERSECUCIÓN HOMING DE MAGIC FIRE 1 (3 SEGUNDOS MÁX) //
        // --------------------------------------------------------------------- //
        estaDisparandoRayo = true;

        if (animator != null && !string.IsNullOrEmpty(animacionDisparo))
        {
            animator.CrossFadeInFixedTime(animacionDisparo, transicionDisparo);
        }

        GarantizarComponentesVisuales();
        Vector3 puntoOrigen = ObtenerPuntoDisparo();
        Vector3 dirInicial = (playerTarget.position + Vector3.up * 0.8f - puntoOrigen).normalized;
        if (dirInicial.sqrMagnitude < 0.001f) dirInicial = transform.forward;

        bool proyectilFinalizado = false;

        if (prefabMagicFire1 != null)
        {
            instanciaMagicFire1Activa = Instantiate(prefabMagicFire1, puntoOrigen, Quaternion.LookRotation(dirInicial));
            instanciaMagicFire1Activa.name = "Instancia_MagicFire1_Homing";

            MagicFire1HomingController homingCtrl = instanciaMagicFire1Activa.GetComponent<MagicFire1HomingController>();
            if (homingCtrl == null) homingCtrl = instanciaMagicFire1Activa.AddComponent<MagicFire1HomingController>();

            homingCtrl.Inicializar(
                playerTarget,
                transform,
                prefabExplosion,
                velocidadPersecucionMagicFire1,
                velocidadGiroMagicFire1,
                duracionHomingMagicFire1,
                radioContactoMagicFire1,
                radioExplosionMagicFire1,
                danoMagicFire1,
                escalaExplosion,
                callbackFin: () => { proyectilFinalizado = true; }
            );

            // Esperar mientras persigue a Linux (máximo duracionHomingMagicFire1 segundos con margen de seguridad)
            float maxEspera = duracionHomingMagicFire1 + 1.2f;
            float tiempoEsperado = 0f;
            while (!proyectilFinalizado && instanciaMagicFire1Activa != null && tiempoEsperado < maxEspera)
            {
                if (bossAI != null && bossAI.IsDead)
                {
                    LimpiarAtaquesActivos();
                    yield break;
                }
                tiempoEsperado += Time.deltaTime;
                yield return null;
            }
        }

        estaDisparandoRayo = false;
        estaEjecutandoMagicFire1 = false;
        instanciaMagicFire1Activa = null;

        // --------------------------------------------------------------------- //
        // FASE 3: ESTADO EXHAUSTO / VULNERABLE (PoseCansado, 1.5x Daño Crítico) //
        // --------------------------------------------------------------------- //
        estaExhausto = true;
        EmitirComando("> [CPU OVERHEAT 100%] Windows en 'PoseCansado'... ¡RECIBE 1.5x DAÑO CRÍTICO!");

        if (animator != null && !string.IsNullOrEmpty(animacionExhausto))
        {
            animator.CrossFadeInFixedTime(animacionExhausto, transicionExhausto);
        }

        float transcurridoExhausto = 0f;
        while (transcurridoExhausto < tiempoExhausto)
        {
            if (bossAI != null && bossAI.IsDead)
            {
                LimpiarAtaquesActivos();
                yield break;
            }
            transcurridoExhausto += Time.deltaTime;
            yield return null;
        }

        estaExhausto = false;
        EmitirComando("> [SISTEMA REINICIADO] Windows Defender reanuda ciclo de combate.");

        if (animator != null && !string.IsNullOrEmpty(animacionRecuperacion))
        {
            animator.CrossFadeInFixedTime(animacionRecuperacion, transicionRecuperacion);
        }

        corrutinaMagicFire1 = null;
    }

    private Vector3 ObtenerPuntoDisparo()
    {
        return transform.position 
             + transform.forward * offsetDisparo.z 
             + transform.up * offsetDisparo.y 
             + transform.right * offsetDisparo.x;
    }

    // ========================================================================= //
    // 1. MECÁNICA DE ESQUIVE (SaltaLoco)                                        //
    // ========================================================================= //

    private void DetectarBalasYEsquivar()
    {
        Collider[] proyectiles = Physics.OverlapSphere(transform.position, radioDeteccionBalas, capaBalas, QueryTriggerInteraction.Collide);
        if (proyectiles == null || proyectiles.Length == 0) return;

        for (int i = 0; i < proyectiles.Length; i++)
        {
            Collider col = proyectiles[i];
            if (col == null) continue;

            BalaProyectil bala = col.GetComponent<BalaProyectil>();
            if (bala == null) bala = col.GetComponentInParent<BalaProyectil>();

            if (bala != null)
            {
                Vector3 dirHaciaBoss = (transform.position - bala.transform.position).normalized;
                float dot = Vector3.Dot(bala.transform.forward, dirHaciaBoss);

                if (dot > 0.45f)
                {
                    EjecutarEsquiveLateral(bala.transform.position);
                    break;
                }
            }
        }
    }

    private void EjecutarEsquiveLateral(Vector3 origenAmenaza)
    {
        ultimoTiempoEsquive = Time.time;
        Vector3 lateral = transform.right;
        if (Random.value > 0.5f) lateral = -transform.right;

        StartCoroutine(RutinaEsquive(lateral));
    }

    private IEnumerator RutinaEsquive(Vector3 direccionLateral)
    {
        estaEsquivando = true;

        var navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
        }

        if (animator != null)
        {
            animator.CrossFadeInFixedTime("SaltaLoco", 0.08f);
        }

        EmitirComando("> [DEFENSE_PROTOCOL] SaltaLoco.exe: Realizando evasión balística...");

        Vector3 posInicial = transform.position;
        Vector3 posDestino = posInicial + direccionLateral * distanciaEsquive;
        posDestino.y = posInicial.y;

        float duracion = 0.42f;
        float transcurrido = 0f;

        while (transcurrido < duracion)
        {
            transcurrido += Time.deltaTime;
            float t = transcurrido / duracion;
            float curva = Mathf.Sin(t * Mathf.PI * 0.5f);
            transform.position = Vector3.Lerp(posInicial, posDestino, curva);
            yield return null;
        }

        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.Warp(transform.position);
            navAgent.isStopped = false;
        }

        estaEsquivando = false;
    }

    // ========================================================================= //
    // 2. COMANDO TASKKILL                                                       //
    // ========================================================================= //

    public void EjecutarTaskkill()
    {
        BuscarJugador();
        EmitirComando("> C:\\Windows\\System32> taskkill /f /im tux_movement.exe");

        if (playerCombatIK != null)
        {
            playerCombatIK.AplicarRalentizacion(duracionRalentizacion, factorRalentizacion);
            EmitirComando($"> [KERNEL ALERT] tux_movement.exe suspendido por {duracionRalentizacion:0.0}s (30% Vel)");
        }
    }

    // ========================================================================= //
    // 3. COMANDO NETSH FIREWALL                                                 //
    // ========================================================================= //

    public void EjecutarFirewall()
    {
        if (corrutinaFirewall != null) StopCoroutine(corrutinaFirewall);
        corrutinaFirewall = StartCoroutine(RutinaFirewall());
    }

    private IEnumerator RutinaFirewall()
    {
        firewallActivo = true;
        if (escudoVisual != null) escudoVisual.SetActive(true);

        EmitirComando("> C:\\Windows\\System32> netsh advfirewall set allprofiles state on");
        EmitirComando("> [FIREWALL ACTIVO] Bloqueando tráfico y proyectiles frontales...");

        yield return new WaitForSeconds(duracionFirewall);

        firewallActivo = false;
        if (escudoVisual != null) escudoVisual.SetActive(false);

        EmitirComando("> [FIREWALL REPOSO] Escudo perimetral desactivado temporalmente.");
        corrutinaFirewall = null;
    }

    public bool NotificarImpactoEnEscudo(Collider colBala, Vector3 puntoImpacto, Vector3 dirBala)
    {
        if (!firewallActivo) return false;

        Vector3 forwardXZ = transform.forward;
        forwardXZ.y = 0f;
        forwardXZ.Normalize();

        if (Vector3.Dot(-dirBala, forwardXZ) > 0.2f)
        {
            EmitirComando("> [NETSH PACKET DROP] Bala de Linux bloqueada por Firewall.");
            return true;
        }

        return false;
    }

    // ========================================================================= //
    // 7. EVENTO BSOD (Pantalla Azul de la Muerte)                               //
    // ========================================================================= //

    private void VerificarBSOD()
    {
        if (bsodDisparadoPorVida || bossAI == null) return;

        float ratioVida = bossAI.CurrentHealth / Mathf.Max(1f, bossAI.MaxHealth);
        if (ratioVida <= umbralVidaBSOD && ratioVida > 0f)
        {
            bsodDisparadoPorVida = true;
            DispararBSOD();
        }
    }

    [ContextMenu("Disparar Pantalla Azul (5s)")]
    public void DispararBSOD()
    {
        EmitirComando("> [FATAL EXCEPTION: 0x000000EF] CRITICAL_PROCESS_DIED");
        EmitirComando("> C:\\Windows\\System32> echo Inyectando BSOD en buffer de pantalla (5.0s)...");

        if (animator != null)
        {
            animator.CrossFadeInFixedTime("Se rie", 0.1f);
        }

        SudoKill9.UI.BSODGlitchEffect.DispararBSOD(5.0f);
        NotificarBSOD(5.0f);
    }

    // ========================================================================= //
    // UTILIDADES Y SOPORTE VISUAL                                               //
    // ========================================================================= //

    private void BuscarJugador()
    {
        if (playerTarget != null && playerCombatIK != null) return;

        playerCombatIK = Object.FindFirstObjectByType<LinuxCombatIK>();
        if (playerCombatIK != null)
        {
            playerTarget = playerCombatIK.transform;
            return;
        }

        GameObject playerObj = GameObject.Find("linux");
        if (playerObj == null) playerObj = GameObject.Find("Player_LinuxSkeleton");
        if (playerObj != null)
        {
            playerTarget = playerObj.transform;
            playerCombatIK = playerObj.GetComponentInChildren<LinuxCombatIK>();
        }
    }

    public static void EmitirComando(string textoComando)
    {
        OnCommandExecuted?.Invoke(textoComando);
#if UNITY_EDITOR
        Debug.Log($"<color=#14E0F5>[WindowsCommand]</color> {textoComando}");
#endif
    }

    public void LimpiarAtaquesActivosPublico()
    {
        LimpiarAtaquesActivos();
    }

    public void LimpiarAtaquesActivos()
    {
        StopAllCoroutines();
        corrutinaMagicFire1 = null;
        corrutinaFirewall = null;

        if (instanciaMagicFire0Activa != null)
        {
            Destroy(instanciaMagicFire0Activa);
            instanciaMagicFire0Activa = null;
        }

        if (instanciaMagicFire1Activa != null)
        {
            Destroy(instanciaMagicFire1Activa);
            instanciaMagicFire1Activa = null;
        }

        // Cancelar y destruir de inmediato cualquier tornado activo en la escena
        RotatorPS2TornadoController[] tornados = Object.FindObjectsByType<RotatorPS2TornadoController>(FindObjectsSortMode.None);
        for (int i = 0; i < tornados.Length; i++)
        {
            if (tornados[i] != null)
            {
                tornados[i].CancelarInmediatamente();
            }
        }

        if (guiaLaserCarga != null) guiaLaserCarga.enabled = false;
        if (hazKamehameha != null) hazKamehameha.enabled = false;
        if (escudoVisual != null) escudoVisual.SetActive(false);

        estaCargandoRayo = false;
        estaDisparandoRayo = false;
        estaExhausto = false;
        firewallActivo = false;
        estaEsquivando = false;
        estaEjecutandoMagicFire0 = false;
        estaEjecutandoMagicFire1 = false;
    }

    private void OnDestroy()
    {
        LimpiarAtaquesActivos();
    }

    /// <summary>
    /// Garantiza la existencia de las referencias a los 4 prefabs de Hovl Studio
    /// y deshabilita componentes de rayos antiguos obsoletos.
    /// </summary>
    public void GarantizarComponentesVisuales()
    {
#if UNITY_EDITOR
        if (prefabMagicFire0 == null)
        {
            prefabMagicFire0 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/Magic fire 0.prefab");
        }
        if (prefabMagicFire1 == null)
        {
            prefabMagicFire1 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/Magic fire 1.prefab");
        }
        if (prefabRotatorPS2 == null)
        {
            prefabRotatorPS2 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/RotatorPS2.prefab");
        }
        if (prefabExplosion == null)
        {
            prefabExplosion = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/Explosion.prefab");
        }
#endif

        // Sustitución de rayos antiguos: Desactivar LineRenderers antiguos rígidos
        if (guiaLaserCarga != null) guiaLaserCarga.enabled = mostrarGuiaLaserCarga;
        if (hazKamehameha != null) hazKamehameha.enabled = mostrarRayoLineRenderer;

        // Escudo Frontal Holográfico de Firewall
        if (escudoVisual == null)
        {
            Transform tEscudo = transform.Find("FirewallShield_Holo");
            if (tEscudo != null)
            {
                escudoVisual = tEscudo.gameObject;
            }
            else
            {
                escudoVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                escudoVisual.name = "FirewallShield_Holo";
                escudoVisual.transform.SetParent(transform, false);
                escudoVisual.transform.localPosition = new Vector3(0f, 1.1f, 1.2f);
                escudoVisual.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                escudoVisual.transform.localScale = new Vector3(2.5f, 2.0f, 1f);

                Collider col = escudoVisual.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
                BoxCollider boxCol = escudoVisual.AddComponent<BoxCollider>();
                boxCol.size = new Vector3(1f, 1f, 0.1f);
                boxCol.isTrigger = true;

                WindowsFirewallShield shieldComp = escudoVisual.AddComponent<WindowsFirewallShield>();
                shieldComp.attackManager = this;

                MeshRenderer mr = escudoVisual.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    Shader transparentShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                    if (transparentShader != null)
                    {
                        Material matShield = new Material(transparentShader);
                        matShield.color = new Color(0.08f, 0.65f, 1f, 0.45f);
                        mr.material = matShield;
                    }
                }

                escudoVisual.SetActive(false);
            }
        }
    }
}

/// <summary>
/// Componente acoplado al quad del escudo para desviar balas en tiempo de ejecución.
/// </summary>
public class WindowsFirewallShield : MonoBehaviour
{
    public WindowsCommandAttacks attackManager;

    private void OnTriggerEnter(Collider other)
    {
        if (attackManager == null || !attackManager.FirewallActivo) return;

        BalaProyectil bala = other.GetComponent<BalaProyectil>();
        if (bala == null) bala = other.GetComponentInParent<BalaProyectil>();

        if (bala != null)
        {
            bool deflectada = attackManager.NotificarImpactoEnEscudo(other, other.transform.position, bala.transform.forward);
            if (deflectada)
            {
                bala.ProcesarImpacto(null, other.transform.position, -bala.transform.forward);
            }
        }
    }
}
