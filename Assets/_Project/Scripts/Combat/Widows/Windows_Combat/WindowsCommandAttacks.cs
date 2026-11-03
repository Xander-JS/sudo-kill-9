using System.Collections;
using UnityEngine;
using SudoKill9.Combat;

/// <summary>
/// Gestiona los ataques de comandos especiales del jefe final Windows:
/// 1. Esquive Dinámico con 'SaltaLoco':
///    Detecta proyectiles entrantes y realiza una finta lateral errática.
/// 2. Comando 'taskkill':
///    Aplica un debuff de ralentización al jugador Tux (30% velocidad por 2.5s).
/// 3. Comando 'netsh advfirewall':
///    Levanta un escudo holográfico frontal que bloquea/desvía proyectiles durante 4s.
/// 4. Gran Kamehameha de PowerShell:
///    Carga telegrafiada de 1.8s -> Rayo continuo de energía por 2.5s ->
///    Ventana de vulnerabilidad en 'PoseCansado' durante 3.0s (1.5x daño crítico).
/// 5. Evento BSOD (Pantalla Azul de la Muerte):
///    Al bajar del 15% de vida, se ríe y desata la pantalla azul crítica por 1.5s.
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

    // ========================================================================= //
    // CONFIGURACIÓN DE ESQUIVE (SaltaLoco)                                      //
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
    // CONFIGURACIÓN DE TASKKILL                                                 //
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
    // CONFIGURACIÓN DE NETSH FIREWALL                                            //
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
    // CONFIGURACIÓN DE POWERSHELL KAMEHAMEHA                                    //
    // ========================================================================= //

    [Header("4. Rayo Kamehameha de PowerShell")]
    [Tooltip("Habilita el gran rayo continuo de energía en Fase 3.")]
    [SerializeField] private bool kamehamehaHabilitado = true;

    [Tooltip("Intervalo en segundos entre disparos del Kamehameha en Fase 3.")]
    [SerializeField] private float intervaloKamehameha = 14.0f;

    [Tooltip("Tiempo de carga telegrafiado antes de disparar el rayo.")]
    [SerializeField] private float tiempoCarga = 1.8f;

    [Tooltip("Duración activa del rayo destructivo.")]
    [SerializeField] private float duracionDisparo = 2.5f;

    [Tooltip("Tiempo en PoseCansado donde Windows recibe 1.5x de daño crítico.")]
    [SerializeField] private float tiempoExhausto = 3.0f;

    [Tooltip("Daño por pulso telegrafiado que causa el rayo directo.")]
    [SerializeField] private float danoPorPulso = 1.0f;

    [Tooltip("Alcance máximo del haz de energía.")]
    [SerializeField] private float alcanceRayo = 28.0f;

    [Tooltip("Radio del cilindro de colisión del rayo.")]
    [SerializeField] private float radioRayo = 1.0f;

    [Tooltip("Guía láser telegrafiada durante la carga.")]
    [SerializeField] private LineRenderer guiaLaserCarga;

    [Tooltip("Renderizado visual del rayo de energía Kamehameha.")]
    [SerializeField] private LineRenderer hazKamehameha;

    [Header("Efectos Visuales Hovl Studio (Procedural Fire / Plasma)")]
    [Tooltip("Prefab del vórtice de carga telegrafiado (ej. RotatorPS1 o Magic fire pro blue).")]
    [SerializeField] private GameObject prefabEfectoCarga;

    [Tooltip("Prefab del chorro/haz destructivo de plasma (ej. Magic fire pro blue).")]
    [SerializeField] private GameObject prefabEfectoRayo;

    [Tooltip("Prefab del efecto de impacto cuando el rayo colisiona (ej. Explosion).")]
    [SerializeField] private GameObject prefabImpactoRayo;

    private GameObject instanciaEfectoCarga;
    private GameObject instanciaEfectoRayo;
    private GameObject instanciaImpactoRayo;

    private float temporizadorKamehameha = 5.0f;
    private float intervaloDanioRayo = 0.5f;
    private float temporizadorPulsoRayo = 0f;
    private bool estaCargandoRayo = false;
    private bool estaDisparandoRayo = false;
    private bool estaExhausto = false;
    private Coroutine corrutinaKamehameha;

    // ========================================================================= //
    // CONFIGURACIÓN DE EVENTO BSOD                                              //
    // ========================================================================= //

    [Header("5. Pantalla Azul de la Muerte (BSOD)")]
    [Tooltip("Porcentaje de vida (0 a 1) para activar el crash de pantalla azul.")]
    [SerializeField] private float umbralVidaBSOD = 0.15f;

    private bool bsodDisparado = false;

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
    public bool FirewallActivo => firewallActivo;
    public bool EstaEjecutandoAtaqueEspecial => estaCargandoRayo || estaDisparandoRayo || estaExhausto || estaEsquivando;

    private void Awake()
    {
        bossAI = GetComponent<WindowsBossAI>();
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

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

        // 1. Verificación de BSOD al bajar del 15% de vida
        VerificarBSOD();

        // 2. Si está ejecutando el ciclo de Kamehameha o esquivando, no iniciar nuevos comandos
        if (EstaEjecutandoAtaqueEspecial)
        {
            if (estaDisparandoRayo)
            {
                ActualizarHazKamehameha();
            }
            return;
        }

        int fase = bossAI.FaseActual;

        // FASE 2 y FASE 3: Esquive con SaltaLoco, taskkill y netsh firewall
        if (fase >= 2)
        {
            // A. Detección y esquive reactivo de balas
            if (esquiveHabilitado && (Time.time - ultimoTiempoEsquive) >= cooldownEsquive)
            {
                DetectarBalasYEsquivar();
            }

            // B. Temporizador y ejecución de taskkill
            if (taskkillHabilitado)
            {
                temporizadorTaskkill -= Time.deltaTime;
                if (temporizadorTaskkill <= 0f)
                {
                    temporizadorTaskkill = intervaloTaskkill;
                    EjecutarTaskkill();
                }
            }

            // C. Temporizador y ejecución de netsh firewall
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

        // FASE 3: Rayo Kamehameha de PowerShell
        if (fase >= 3 && kamehamehaHabilitado)
        {
            temporizadorKamehameha -= Time.deltaTime;
            if (temporizadorKamehameha <= 0f)
            {
                temporizadorKamehameha = intervaloKamehameha;
                IniciarKamehameha();
            }
        }
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

                // Si la bala apunta hacia Windows (> 0.45)
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
        Vector3 vectorAmenaza = (transform.position - origenAmenaza).normalized;
        vectorAmenaza.y = 0f;

        // Decidir si esquivar a izquierda o derecha
        Vector3 lateral = transform.right;
        if (Random.value > 0.5f) lateral = -transform.right;

        StartCoroutine(RutinaEsquive(lateral));
    }

    private IEnumerator RutinaEsquive(Vector3 direccionLateral)
    {
        estaEsquivando = true;

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
            float curva = Mathf.Sin(t * Mathf.PI * 0.5f); // Ease-out suave
            transform.position = Vector3.Lerp(posInicial, posDestino, curva);
            yield return null;
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

    /// <summary>
    /// Llamado por el escudo frontal cuando un proyectil intenta impactarlo.
    /// Retorna true si el escudo desvió la bala.
    /// </summary>
    public bool NotificarImpactoEnEscudo(Collider colBala, Vector3 puntoImpacto, Vector3 dirBala)
    {
        if (!firewallActivo) return false;

        // Comprobar que el proyectil provenga del frente de Windows
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
    // 4. GRAN KAMEHAMEHA DE POWERSHELL                                          //
    // ========================================================================= //

    public void IniciarKamehameha()
    {
        if (corrutinaKamehameha != null) StopCoroutine(corrutinaKamehameha);
        corrutinaKamehameha = StartCoroutine(RutinaKamehameha());
    }

    private IEnumerator RutinaKamehameha()
    {
        // --------------------------------------------------------------------- //
        // PASO 1: CARGA Y TELEGRAFIADO (1.8s)                                   //
        // --------------------------------------------------------------------- //
        estaCargandoRayo = true;
        estaDisparandoRayo = false;
        estaExhausto = false;

        EmitirComando("> powershell.exe -ExecutionPolicy Bypass -Command \"Invoke-KamehamehaBeam\"");
        EmitirComando("> [ADVERTENCIA] Condensando flujo de datos... ¡APÁRTATE DE LA LÍNEA DE VISTA!");

        if (animator != null)
        {
            animator.CrossFadeInFixedTime("Se prepara para atacar", 0.15f);
        }

        AsegurarInstanciasEfectos();
        Vector3 origenGuia = transform.position + transform.forward * 0.8f + Vector3.up * 1.2f;

        if (instanciaEfectoCarga != null)
        {
            if (!instanciaEfectoCarga.activeSelf) instanciaEfectoCarga.SetActive(true);
            instanciaEfectoCarga.transform.position = origenGuia;
            instanciaEfectoCarga.transform.rotation = transform.rotation;
        }

        if (guiaLaserCarga != null)
        {
            guiaLaserCarga.enabled = true;
        }

        float transcurrido = 0f;
        while (transcurrido < tiempoCarga)
        {
            if (bossAI != null && bossAI.IsDead) yield break;

            transcurrido += Time.deltaTime;

            // Orientar suavemente hacia el jugador mientras carga el rayo
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

            origenGuia = transform.position + transform.forward * 0.8f + Vector3.up * 1.2f;

            if (instanciaEfectoCarga != null)
            {
                instanciaEfectoCarga.transform.position = origenGuia;
                instanciaEfectoCarga.transform.rotation = transform.rotation;
            }

            // Actualizar guía de puntería
            if (guiaLaserCarga != null)
            {
                guiaLaserCarga.SetPosition(0, origenGuia);
                guiaLaserCarga.SetPosition(1, origenGuia + transform.forward * alcanceRayo);
            }

            yield return null;
        }

        if (bossAI != null && bossAI.IsDead) yield break;

        if (instanciaEfectoCarga != null && instanciaEfectoCarga.activeSelf)
        {
            instanciaEfectoCarga.SetActive(false);
        }

        if (guiaLaserCarga != null) guiaLaserCarga.enabled = false;
        estaCargandoRayo = false;

        // --------------------------------------------------------------------- //
        // PASO 2: DISPARO CONTINUO DEL RAYO (2.5s)                              //
        // --------------------------------------------------------------------- //
        estaDisparandoRayo = true;
        temporizadorPulsoRayo = 0f; // Primer pulso inmediato

        EmitirComando("> [POWERSHELL RAYO DESATADO] Buffer Overflow frontal en ejecución!");

        if (animator != null)
        {
            animator.CrossFadeInFixedTime("Grito", 0.1f);
        }

        if (hazKamehameha != null)
        {
            hazKamehameha.enabled = true;
        }

        if (instanciaEfectoRayo != null)
        {
            if (!instanciaEfectoRayo.activeSelf) instanciaEfectoRayo.SetActive(true);
            Vector3 origenRayo = transform.position + transform.forward * 0.8f + Vector3.up * 1.2f;
            instanciaEfectoRayo.transform.position = origenRayo;
            instanciaEfectoRayo.transform.rotation = Quaternion.LookRotation(transform.forward);
        }

        float tiempoRayo = 0f;
        while (tiempoRayo < duracionDisparo)
        {
            if (bossAI != null && bossAI.IsDead) yield break;

            tiempoRayo += Time.deltaTime;
            ActualizarHazKamehameha();
            yield return null;
        }

        if (bossAI != null && bossAI.IsDead) yield break;

        if (hazKamehameha != null) hazKamehameha.enabled = false;
        if (instanciaEfectoRayo != null && instanciaEfectoRayo.activeSelf) instanciaEfectoRayo.SetActive(false);
        if (instanciaImpactoRayo != null && instanciaImpactoRayo.activeSelf) instanciaImpactoRayo.SetActive(false);
        estaDisparandoRayo = false;

        // --------------------------------------------------------------------- //
        // PASO 3: ESTADO EXHAUSTO / VULNERABLE (3.0s - PoseCansado, 1.5x Daño)  //
        // --------------------------------------------------------------------- //
        estaExhausto = true;

        EmitirComando("> [CPU OVERHEAT 100%] Windows en 'PoseCansado'... ¡RECIBE 1.5x DAÑO CRÍTICO!");

        if (animator != null)
        {
            animator.CrossFadeInFixedTime("PoseCansado", 0.15f);
        }

        float transcurridoExhausto = 0f;
        while (transcurridoExhausto < tiempoExhausto)
        {
            if (bossAI != null && bossAI.IsDead) yield break;
            transcurridoExhausto += Time.deltaTime;
            yield return null;
        }

        if (bossAI != null && bossAI.IsDead) yield break;

        estaExhausto = false;
        EmitirComando("> [SISTEMA REINICIADO] Windows Defender reanuda ciclo de combate.");

        if (animator != null)
        {
            animator.CrossFadeInFixedTime("Se prepara para atacar", 0.2f);
        }

        corrutinaKamehameha = null;
    }

    private void ActualizarHazKamehameha()
    {
        Vector3 origen = transform.position + transform.forward * 0.8f + Vector3.up * 1.2f;
        Vector3 direccion = transform.forward;
        Vector3 finRayo = origen + direccion * alcanceRayo;

        if (instanciaEfectoRayo != null)
        {
            instanciaEfectoRayo.transform.position = origen;
            instanciaEfectoRayo.transform.rotation = Quaternion.LookRotation(direccion);
        }

        temporizadorPulsoRayo -= Time.deltaTime;
        bool aplicarDano = temporizadorPulsoRayo <= 0f;
        if (aplicarDano)
        {
            temporizadorPulsoRayo = intervaloDanioRayo;
        }

        // Trazar Raycast para detectar punto de impacto en obstáculos o jugador
        RaycastHit hitImpacto;
        Vector3 puntoImpacto = finRayo;
        bool huboImpacto = false;
        if (Physics.Raycast(origen, direccion, out hitImpacto, alcanceRayo, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hitImpacto.collider.transform.IsChildOf(transform) && hitImpacto.collider.transform != transform)
            {
                puntoImpacto = hitImpacto.point;
                huboImpacto = true;
            }
        }

        if (hazKamehameha != null)
        {
            hazKamehameha.SetPosition(0, origen);
            hazKamehameha.SetPosition(1, puntoImpacto);
        }

        if (instanciaImpactoRayo != null)
        {
            if (huboImpacto)
            {
                if (!instanciaImpactoRayo.activeSelf) instanciaImpactoRayo.SetActive(true);
                instanciaImpactoRayo.transform.position = puntoImpacto;
                instanciaImpactoRayo.transform.rotation = Quaternion.LookRotation(hitImpacto.normal);
            }
            else
            {
                if (instanciaImpactoRayo.activeSelf) instanciaImpactoRayo.SetActive(false);
            }
        }

        // Trazar SphereCast continuo en la trayectoria del rayo para aplicar daño
        float distanciaEfectiva = Vector3.Distance(origen, puntoImpacto);
        RaycastHit[] hits = Physics.SphereCastAll(origen, radioRayo, direccion, distanciaEfectiva, ~0, QueryTriggerInteraction.Ignore);

        if (hits != null && hits.Length > 0)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null) continue;
                if (h.collider.transform.IsChildOf(transform) || h.collider.transform == transform) continue;

                // Dañar al jugador Tux en pulsos telegrafiados (1 daño por pulso)
                if (aplicarDano)
                {
                    IDamageable danable = h.collider.GetComponentInParent<IDamageable>() ??
                                          h.collider.GetComponentInChildren<IDamageable>();

                    if (danable != null && !danable.IsDead)
                    {
                        danable.TakeDamage(danoPorPulso);
                    }
                }

                // Empuje continuo sobre CharacterController
                CharacterController cc = h.collider.GetComponentInParent<CharacterController>();
                if (cc != null)
                {
                    cc.Move(direccion * 4.0f * Time.deltaTime);
                }
            }
        }
    }

    // ========================================================================= //
    // 5. EVENTO BSOD (Pantalla Azul de la Muerte)                               //
    // ========================================================================= //

    private void VerificarBSOD()
    {
        if (bsodDisparado || bossAI == null) return;

        float ratioVida = bossAI.CurrentHealth / Mathf.Max(1f, bossAI.MaxHealth);
        if (ratioVida <= umbralVidaBSOD && ratioVida > 0f)
        {
            bsodDisparado = true;
            DispararBSOD();
        }
    }

    public void DispararBSOD()
    {
        EmitirComando("> [FATAL EXCEPTION: 0x000000EF] CRITICAL_PROCESS_DIED");
        EmitirComando("> C:\\Windows\\System32> echo Inyectando BSOD en buffer de pantalla...");

        if (animator != null)
        {
            animator.CrossFadeInFixedTime("Se rie", 0.1f);
        }

        OnBSODTriggered?.Invoke();
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

    private void EmitirComando(string textoComando)
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

    private void LimpiarAtaquesActivos()
    {
        StopAllCoroutines();
        corrutinaKamehameha = null;
        corrutinaFirewall = null;
        if (guiaLaserCarga != null) guiaLaserCarga.enabled = false;
        if (hazKamehameha != null) hazKamehameha.enabled = false;
        if (escudoVisual != null) escudoVisual.SetActive(false);
        if (instanciaEfectoCarga != null && instanciaEfectoCarga.activeSelf) instanciaEfectoCarga.SetActive(false);
        if (instanciaEfectoRayo != null && instanciaEfectoRayo.activeSelf) instanciaEfectoRayo.SetActive(false);
        if (instanciaImpactoRayo != null && instanciaImpactoRayo.activeSelf) instanciaImpactoRayo.SetActive(false);
        estaCargandoRayo = false;
        estaDisparandoRayo = false;
        estaExhausto = false;
        firewallActivo = false;
        estaEsquivando = false;
    }

    private void OnDestroy()
    {
        if (instanciaImpactoRayo != null)
        {
            if (Application.isPlaying) Destroy(instanciaImpactoRayo);
            else DestroyImmediate(instanciaImpactoRayo);
        }
    }

    private void AsegurarInstanciasEfectos()
    {
        GarantizarComponentesVisuales();

        if (instanciaEfectoCarga == null && prefabEfectoCarga != null)
        {
            instanciaEfectoCarga = Instantiate(prefabEfectoCarga, transform);
            instanciaEfectoCarga.name = "InstanciaEfectoCarga_Hovl";
            instanciaEfectoCarga.transform.localScale = Vector3.one * 0.75f;
            instanciaEfectoCarga.SetActive(false);
        }

        if (instanciaEfectoRayo == null && prefabEfectoRayo != null)
        {
            instanciaEfectoRayo = Instantiate(prefabEfectoRayo, transform);
            instanciaEfectoRayo.name = "InstanciaEfectoRayo_Hovl";
            instanciaEfectoRayo.transform.localScale = new Vector3(1.3f, 1.3f, 2.5f);
            instanciaEfectoRayo.SetActive(false);
        }

        if (instanciaImpactoRayo == null && prefabImpactoRayo != null)
        {
            instanciaImpactoRayo = Instantiate(prefabImpactoRayo);
            instanciaImpactoRayo.name = "InstanciaImpactoRayo_Hovl";
            instanciaImpactoRayo.transform.localScale = Vector3.one * 1.3f;
            instanciaImpactoRayo.SetActive(false);
        }
    }

    /// <summary>
    /// Garantiza la existencia de los LineRenderers y el escudo visual proceduralmente
    /// si no fueron configurados previamente en el Inspector.
    /// </summary>
    public void GarantizarComponentesVisuales()
    {
#if UNITY_EDITOR
        // Descubrimiento y enlace automático de los prefabs de Hovl Studio
        if (prefabEfectoCarga == null)
        {
            prefabEfectoCarga = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/RotatorPS1.prefab");
            if (prefabEfectoCarga == null)
                prefabEfectoCarga = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/Magic fire pro blue.prefab");
        }
        if (prefabEfectoRayo == null)
        {
            prefabEfectoRayo = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/Magic fire pro blue.prefab");
        }
        if (prefabImpactoRayo == null)
        {
            prefabImpactoRayo = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/Explosion.prefab");
        }
#endif

        // 1. Guía de Carga (Láser telegrafiado)
        if (guiaLaserCarga == null)
        {
            Transform tGuia = transform.Find("GuiaLaserCarga");
            if (tGuia == null)
            {
                GameObject goGuia = new GameObject("GuiaLaserCarga");
                goGuia.transform.SetParent(transform, false);
                guiaLaserCarga = goGuia.AddComponent<LineRenderer>();
            }
            else
            {
                guiaLaserCarga = tGuia.GetComponent<LineRenderer>();
            }

            if (guiaLaserCarga != null)
            {
                guiaLaserCarga.positionCount = 2;
                guiaLaserCarga.startWidth = 0.08f;
                guiaLaserCarga.endWidth = 0.08f;
                guiaLaserCarga.useWorldSpace = true;
                guiaLaserCarga.enabled = false;

                Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                             ?? Shader.Find("Universal Render Pipeline/Unlit") 
                             ?? Shader.Find("Sprites/Default");
                if (unlit != null)
                {
                    Material mat = new Material(unlit);
                    mat.SetFloat("_Surface", 1);
                    mat.SetFloat("_Blend", 1);
                    mat.color = new Color(0.1f, 0.9f, 1f, 0.85f);
                    guiaLaserCarga.material = mat;
                }
            }
        }

        // 2. Haz Kamehameha (Rayo continuo potente)
        if (hazKamehameha == null)
        {
            Transform tHaz = transform.Find("HazKamehameha");
            if (tHaz == null)
            {
                GameObject goHaz = new GameObject("HazKamehameha");
                goHaz.transform.SetParent(transform, false);
                hazKamehameha = goHaz.AddComponent<LineRenderer>();
            }
            else
            {
                hazKamehameha = tHaz.GetComponent<LineRenderer>();
            }

            if (hazKamehameha != null)
            {
                hazKamehameha.positionCount = 2;
                hazKamehameha.startWidth = 1.6f;
                hazKamehameha.endWidth = 1.6f;
                hazKamehameha.useWorldSpace = true;
                hazKamehameha.enabled = false;

                Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                             ?? Shader.Find("Universal Render Pipeline/Unlit") 
                             ?? Shader.Find("Sprites/Default");
                if (unlit != null)
                {
                    Material mat = new Material(unlit);
                    mat.SetFloat("_Surface", 1);
                    mat.SetFloat("_Blend", 1);
                    mat.color = new Color(0.15f, 0.95f, 1f, 0.95f); // Cian brillante eléctrico
                    hazKamehameha.material = mat;
                }
            }
        }

        // 3. Escudo Frontal Holográfico de Firewall
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

                // Configurar BoxCollider como trigger para evitar el error de MeshCollider no convexo en Unity
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
