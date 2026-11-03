using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SudoKill9.UI.Inicio;

namespace SudoKill9.UI
{
    /// <summary>
    /// Controlador central del HUD táctico Pixel Art para la escena de combate:
    /// - Barra de salud superior de Windows Boss (estilo RPG/Souls):
    ///     * Nombre 'WINDOWS_11_DEFENDER.EXE'
    ///     * Subtítulo dinámico con estado según la fase
    ///     * Barra dual con vaciado suave (decay / chip damage)
    /// - Placa táctica militar de Tux (esquina superior izquierda):
    ///     * 'TUX_OS [KERNEL OK]'
    ///     * Paleta verde militar (#23372E) y cian brillante (#14E0F5)
    /// - Terminal flotante CMD (esquina superior derecha):
    ///     * Escribe en tiempo real los comandos ejecutados carácter a carácter con cursor parpadeante.
    /// </summary>
    public class CombatHUDManager : MonoBehaviour
    {
        public static CombatHUDManager Instance { get; private set; }

        // ===================================================================== //
        // 1. BARRA DE SALUD DE WINDOWS BOSS (SUPERIOR CENTRAL)                  //
        // ===================================================================== //

        [Header("1. Barra de Windows Boss (Top Center)")]
        [SerializeField] private GameObject bossBarContainer;
        [SerializeField] private Image bossHealthFillImage;
        [SerializeField] private Image bossHealthDecayImage; // Barra de daño residual/fantasma
        [SerializeField] private TMP_Text bossTitleText;
        [SerializeField] private TMP_Text bossSubtitleText;
        [SerializeField] private TMP_Text bossHealthNumberText;

        [Header("Colores Boss")]
        [SerializeField] private Color bossHpColorPhase1 = new Color(0.96f, 0.71f, 0.10f); // Ámbar dorado #F4B41A
        [SerializeField] private Color bossHpColorPhase2 = new Color(0.12f, 0.88f, 0.96f); // Cian #14E0F5
        [SerializeField] private Color bossHpColorPhase3 = new Color(0.92f, 0.22f, 0.15f); // Rojo crítico #E02424
        [SerializeField] private Color bossDecayColor = new Color(0.75f, 0.35f, 0.12f, 0.85f);

        [Tooltip("Velocidad a la que la barra de vaciado suave (decay) persigue a la barra principal.")]
        [SerializeField] private float velocidadDecay = 0.45f;
        [SerializeField] private float retardoDecay = 0.35f;

        // ===================================================================== //
        // 2. BARRA DE SALUD DE TUX (ESQUINA SUPERIOR IZQUIERDA)                 //
        // ===================================================================== //

        [Header("2. Barra de Tux (Top Left)")]
        [SerializeField] private GameObject tuxBarContainer;
        [SerializeField] private Image tuxHealthFillImage;
        [SerializeField] private TMP_Text tuxStatusTitleText;
        [SerializeField] private TMP_Text tuxHealthNumberText;

        [Header("Colores Tux")]
        [SerializeField] private Color tuxHpNormalColor = new Color(0.08f, 0.88f, 0.96f); // Cian brillante #14E0F5
        [SerializeField] private Color tuxHpLowColor = new Color(0.95f, 0.25f, 0.20f);    // Rojo alerta

        // ===================================================================== //
        // 3. CONSOLA TERMINAL CMD (ESQUINA SUPERIOR DERECHA)                    //
        // ===================================================================== //

        [Header("3. Consola Terminal CMD (Top Right)")]
        [SerializeField] private GameObject terminalContainer;
        [SerializeField] private TMP_Text terminalTitleText;
        [SerializeField] private TMP_Text terminalContentText;
        [SerializeField] private int maxLineasTerminal = 6;
        [SerializeField] private float velocidadTipeoChar = 0.018f; // segundos por carácter

        // ===================================================================== //
        // REFERENCIAS DE ACTORES                                                //
        // ===================================================================== //

        [Header("Referencias Opcionales")]
        [SerializeField] private WindowsBossAI bossAI;
        [SerializeField] private LinuxCombatIK playerCombatIK;

        // Variables internas de estado
        private float bossTargetFill = 1f;
        private float bossCurrentFill = 1f;
        private float bossDecayFill = 1f;
        private float temporizadorDecay = 0f;

        private float tuxTargetFill = 1f;
        private float tuxCurrentFill = 1f;

        private readonly List<string> lineasCompletas = new List<string>();
        private readonly Queue<string> colaMensajesPendientes = new Queue<string>();
        private bool estaTipeando = false;
        private bool parpadeoCursor = false;
        private float tiempoUltimoCursor = 0f;
        private string lineaEnTipeoActual = "";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            LocalizarReferencias();
        }

        private void OnEnable()
        {
            SuscribirEventos();
        }

        private void OnDisable()
        {
            DesuscribirEventos();
        }

        private void Start()
        {
            LocalizarReferencias();
            InicializarUI();
            SuscribirEventos();

            // Mensajes iniciales de arranque en el terminal
            LogTerminal("> Microsoft Windows [Versión 10.0.22631.4169]");
            LogTerminal("> (c) Microsoft Corporation. Todos los derechos reservados.");
            LogTerminal("> C:\\Windows\\System32> DefenderSecurityService.exe --realtime-scan=ON");
            LogTerminal("> [KERNEL DETECTED] Hostil 'linux.elf' detectado en ring0. Iniciando contramedidas.");
        }

        private void Update()
        {
            ActualizarBarrasSuaves();
            ActualizarCursorTerminal();
        }

        // ===================================================================== //
        // GESTIÓN DE SALUD: WINDOWS BOSS                                        //
        // ===================================================================== //

        public void ActualizarSaludBoss(float actual, float max)
        {
            float maxSegura = Mathf.Max(1f, max);
            bossTargetFill = Mathf.Clamp01(actual / maxSegura);

            if (bossHealthNumberText != null)
            {
                bossHealthNumberText.text = $"{Mathf.Max(0, Mathf.CeilToInt(actual))} / {Mathf.CeilToInt(max)} HP";
            }

            // Si la vida bajó, la barra principal baja rápido y programamos el retraso del decay
            if (bossTargetFill < bossCurrentFill)
            {
                bossCurrentFill = bossTargetFill;
                if (bossHealthFillImage != null)
                {
                    bossHealthFillImage.fillAmount = bossCurrentFill;
                }
                temporizadorDecay = retardoDecay;
            }
            else
            {
                bossCurrentFill = bossTargetFill;
                bossDecayFill = bossTargetFill;
                if (bossHealthFillImage != null) bossHealthFillImage.fillAmount = bossCurrentFill;
                if (bossHealthDecayImage != null) bossHealthDecayImage.fillAmount = bossDecayFill;
            }
        }

        public void ActualizarFaseBoss(int fase)
        {
            if (bossSubtitleText == null) return;

            switch (fase)
            {
                case 1:
                    bossSubtitleText.text = "ESTADO: ESCANEANDO ARCHIVOS DE SISTEMA... [FASE 1]";
                    if (bossHealthFillImage != null) bossHealthFillImage.color = bossHpColorPhase1;
                    break;
                case 2:
                    bossSubtitleText.text = "ESTADO: MODO ALERTA - FIREWALL ON & EVASIÓN [FASE 2]";
                    if (bossHealthFillImage != null) bossHealthFillImage.color = bossHpColorPhase2;
                    LogTerminal("> [SYS_ALERT] CPU Core Voltage Overclocked -> Fase 2 Activada.");
                    break;
                case 3:
                    bossSubtitleText.text = "ESTADO: ERROR CRÍTICO - KERNEL COLLAPSE & ENRAGE [FASE 3]";
                    if (bossHealthFillImage != null) bossHealthFillImage.color = bossHpColorPhase3;
                    LogTerminal("> [FATAL RING0 COMPROMISE] Activando protocolo de emergencia Kamehameha.ps1!");
                    break;
            }
        }

        private void ActualizarBarrasSuaves()
        {
            // Barra de decaimiento del boss
            if (bossHealthDecayImage != null)
            {
                if (temporizadorDecay > 0f)
                {
                    temporizadorDecay -= Time.deltaTime;
                }
                else if (bossDecayFill > bossCurrentFill)
                {
                    bossDecayFill = Mathf.MoveTowards(bossDecayFill, bossCurrentFill, velocidadDecay * Time.deltaTime);
                    bossHealthDecayImage.fillAmount = bossDecayFill;
                }
                else
                {
                    bossDecayFill = bossCurrentFill;
                    bossHealthDecayImage.fillAmount = bossDecayFill;
                }
            }

            // Barra suave de Tux
            if (tuxHealthFillImage != null)
            {
                tuxCurrentFill = Mathf.MoveTowards(tuxCurrentFill, tuxTargetFill, 3.5f * Time.deltaTime);
                tuxHealthFillImage.fillAmount = tuxCurrentFill;
            }
        }

        // ===================================================================== //
        // GESTIÓN DE SALUD: TUX                                                 //
        // ===================================================================== //

        public void ActualizarSaludTux(float actual, float max)
        {
            float maxSegura = Mathf.Max(1f, max);
            tuxTargetFill = Mathf.Clamp01(actual / maxSegura);

            if (tuxHealthNumberText != null)
            {
                tuxHealthNumberText.text = $"{Mathf.Max(0, Mathf.CeilToInt(actual))} / {Mathf.CeilToInt(max)} HP";
            }

            if (tuxStatusTitleText != null)
            {
                if (tuxTargetFill > 0.35f)
                {
                    tuxStatusTitleText.text = "TUX_OS [KERNEL OK]";
                    tuxStatusTitleText.color = tuxHpNormalColor;
                    if (tuxHealthFillImage != null) tuxHealthFillImage.color = tuxHpNormalColor;
                }
                else if (tuxTargetFill > 0f)
                {
                    tuxStatusTitleText.text = "TUX_OS [LOW MEMORY - WARNING]";
                    tuxStatusTitleText.color = tuxHpLowColor;
                    if (tuxHealthFillImage != null) tuxHealthFillImage.color = tuxHpLowColor;
                }
                else
                {
                    tuxStatusTitleText.text = "TUX_OS [KERNEL PANIC]";
                    tuxStatusTitleText.color = tuxHpLowColor;
                }
            }
        }

        // ===================================================================== //
        // GESTIÓN DE TERMINAL FLOTANTE                                          //
        // ===================================================================== //

        /// <summary>
        /// Agrega un mensaje a la cola del terminal CMD que se tipea en tiempo real.
        /// </summary>
        public static void LogTerminal(string mensaje)
        {
            if (Instance != null)
            {
                Instance.EncolarMensajeTerminal(mensaje);
            }
            else
            {
                Debug.Log($"<color=#14E0F5>[TerminalCMD]</color> {mensaje}");
            }
        }

        private void EncolarMensajeTerminal(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return;

            colaMensajesPendientes.Enqueue(texto);
            if (!estaTipeando)
            {
                StartCoroutine(RutinaTipeoTerminal());
            }
        }

        private IEnumerator RutinaTipeoTerminal()
        {
            estaTipeando = true;

            while (colaMensajesPendientes.Count > 0)
            {
                string mensajeCompleto = colaMensajesPendientes.Dequeue();
                lineaEnTipeoActual = "";

                for (int i = 0; i < mensajeCompleto.Length; i++)
                {
                    lineaEnTipeoActual += mensajeCompleto[i];
                    RefrescarTextoTerminal();
                    yield return new WaitForSeconds(velocidadTipeoChar);
                }

                lineasCompletas.Add(mensajeCompleto);
                while (lineasCompletas.Count > maxLineasTerminal)
                {
                    lineasCompletas.RemoveAt(0);
                }
                lineaEnTipeoActual = "";
                RefrescarTextoTerminal();
                yield return new WaitForSeconds(0.06f);
            }

            estaTipeando = false;
        }

        private void RefrescarTextoTerminal()
        {
            if (terminalContentText == null) return;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < lineasCompletas.Count; i++)
            {
                sb.AppendLine(lineasCompletas[i]);
            }

            if (!string.IsNullOrEmpty(lineaEnTipeoActual))
            {
                sb.Append(lineaEnTipeoActual);
                if (parpadeoCursor) sb.Append("_");
                sb.AppendLine();
            }
            else if (parpadeoCursor)
            {
                sb.AppendLine("_");
            }

            terminalContentText.text = sb.ToString();
        }

        private void ActualizarCursorTerminal()
        {
            if (Time.time - tiempoUltimoCursor >= 0.4f)
            {
                tiempoUltimoCursor = Time.time;
                parpadeoCursor = !parpadeoCursor;
                RefrescarTextoTerminal();
            }
        }

        // ===================================================================== //
        // CONFIGURACIÓN, EVENTOS Y REFERENCIAS                                  //
        // ===================================================================== //

        private void LocalizarReferencias()
        {
            if (bossAI == null) bossAI = Object.FindFirstObjectByType<WindowsBossAI>();
            if (playerCombatIK == null) playerCombatIK = Object.FindFirstObjectByType<LinuxCombatIK>();
        }

        private void InicializarUI()
        {
            if (bossTitleText != null) bossTitleText.text = "WINDOWS_11_DEFENDER.EXE";
            if (bossSubtitleText != null) bossSubtitleText.text = "ESTADO: ESCANEANDO ARCHIVOS DE SISTEMA... [FASE 1]";
            if (terminalTitleText != null) terminalTitleText.text = "[CMD] C:\\Windows\\System32\\cmd.exe";

            if (bossAI != null)
            {
                ActualizarSaludBoss(bossAI.CurrentHealth, bossAI.MaxHealth);
                ActualizarFaseBoss(bossAI.FaseActual);
            }
            else
            {
                ActualizarSaludBoss(500f, 500f);
            }

            if (playerCombatIK != null)
            {
                ActualizarSaludTux(playerCombatIK.CurrentHealth, playerCombatIK.MaxHealth);
            }
            else
            {
                ActualizarSaludTux(100f, 100f);
            }
        }

        private void SuscribirEventos()
        {
            WindowsBossAI.OnBossHealthChanged -= ActualizarSaludBoss;
            WindowsBossAI.OnBossHealthChanged += ActualizarSaludBoss;

            WindowsBossAI.OnBossPhaseChanged -= ActualizarFaseBoss;
            WindowsBossAI.OnBossPhaseChanged += ActualizarFaseBoss;

            LinuxCombatIK.OnPlayerHealthChanged -= ActualizarSaludTux;
            LinuxCombatIK.OnPlayerHealthChanged += ActualizarSaludTux;

            WindowsCommandAttacks.OnCommandExecuted -= HandleCommandExecuted;
            WindowsCommandAttacks.OnCommandExecuted += HandleCommandExecuted;
        }

        private void DesuscribirEventos()
        {
            WindowsBossAI.OnBossHealthChanged -= ActualizarSaludBoss;
            WindowsBossAI.OnBossPhaseChanged -= ActualizarFaseBoss;
            LinuxCombatIK.OnPlayerHealthChanged -= ActualizarSaludTux;
            WindowsCommandAttacks.OnCommandExecuted -= HandleCommandExecuted;
        }

        private void HandleCommandExecuted(string comando)
        {
            EncolarMensajeTerminal(comando);
        }

        internal void ConfigurarReferencias(
            GameObject bossBar, Image bossFill, Image bossDecay, TMP_Text bossTitle, TMP_Text bossSub, TMP_Text bossHpNum,
            GameObject tuxBar, Image tuxFill, TMP_Text tuxTitle, TMP_Text tuxHpNum,
            GameObject termBar, TMP_Text termTitle, TMP_Text termContent)
        {
            bossBarContainer = bossBar;
            bossHealthFillImage = bossFill;
            bossHealthDecayImage = bossDecay;
            bossTitleText = bossTitle;
            bossSubtitleText = bossSub;
            bossHealthNumberText = bossHpNum;
            tuxBarContainer = tuxBar;
            tuxHealthFillImage = tuxFill;
            tuxStatusTitleText = tuxTitle;
            tuxHealthNumberText = tuxHpNum;
            terminalContainer = termBar;
            terminalTitleText = termTitle;
            terminalContentText = termContent;

            LocalizarReferencias();
            InicializarUI();
            SuscribirEventos();
        }

        // ===================================================================== //
        // GENERACIÓN Y AUTO-INICIALIZACIÓN PROCEDURAL EN TIEMPO DE EJECUCIÓN    //
        // ===================================================================== //

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AsegurarCanvasCombateEnRuntime()
        {
            // Solo auto-inicializar si la escena activa contiene al Jefe o al Jugador Linux
            var bossAI = Object.FindFirstObjectByType<WindowsBossAI>();
            var combatIK = Object.FindFirstObjectByType<LinuxCombatIK>();
            if (bossAI == null && combatIK == null) return;

            // Si ya existe una instancia o Canvas en la escena, no duplicar
            if (Instance != null || Object.FindFirstObjectByType<CombatHUDManager>() != null) return;

            ConstruirCanvasRuntime(bossAI, combatIK);
        }

        public static GameObject ConstruirCanvasRuntime(WindowsBossAI bossRef = null, LinuxCombatIK playerRef = null)
        {
            // 1. Asegurar EventSystem
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            // 2. Buscar TMP_FontAsset
            TMP_FontAsset font = null;
            TMP_FontAsset[] fuentes = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < fuentes.Length; i++)
            {
                if (fuentes[i] != null && fuentes[i].name.Contains("PixelTactical"))
                {
                    font = fuentes[i];
                    break;
                }
            }
            if (font == null && fuentes.Length > 0) font = fuentes[0];
            if (font == null) font = TMP_Settings.defaultFontAsset;

            // 3. Buscar Sprites tácticos
            Sprite buttonSprite = null;
            Sprite panelSprite = null;
            Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                if (sprites[i].name.Contains("PixelArt_Tactical_Button")) buttonSprite = sprites[i];
                else if (sprites[i].name.Contains("PixelArt_Tactical_Panel")) panelSprite = sprites[i];
            }

            if (panelSprite == null)
            {
                panelSprite = CrearSpritePixelBorde(32, 32, new Color(0.12f, 0.16f, 0.14f, 0.95f), new Color(0.24f, 0.38f, 0.32f, 1f), 2);
            }
            if (buttonSprite == null)
            {
                buttonSprite = CrearSpritePixelBorde(32, 32, new Color(0.10f, 0.16f, 0.22f, 0.95f), new Color(0.14f, 0.88f, 0.96f, 1f), 2);
            }

            // 4. Crear Canvas Principal
            GameObject canvasGO = GameObject.Find("CombatUI_Canvas");
            if (canvasGO == null) canvasGO = new GameObject("CombatUI_Canvas");

            Canvas canvas = canvasGO.GetComponent<Canvas>() ?? canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>() ?? canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasGO.GetComponent<GraphicRaycaster>() == null) canvasGO.AddComponent<GraphicRaycaster>();

            CombatHUDManager hud = canvasGO.GetComponent<CombatHUDManager>() ?? canvasGO.AddComponent<CombatHUDManager>();

            // 5. Construir Elementos del HUD
            ConstruirBarraBossRuntime(canvasGO.transform, font, panelSprite, hud);
            ConstruirBarraTuxRuntime(canvasGO.transform, font, panelSprite, hud);
            ConstruirTerminalCMDRuntime(canvasGO.transform, font, panelSprite, hud);

            // 6. Construir BSOD y Pantalla de Fin de Juego
            ConstruirBSODRuntime(canvasGO.transform, font);
            ConstruirVictoriaDerrotaRuntime(canvasGO.transform, font, buttonSprite, panelSprite);

            return canvasGO;
        }

        private static void ConstruirBarraBossRuntime(Transform parent, TMP_FontAsset font, Sprite panelSprite, CombatHUDManager hud)
        {
            Transform tBoss = parent.Find("BossHealthBar");
            GameObject bossGO = tBoss != null ? tBoss.gameObject : new GameObject("BossHealthBar");
            bossGO.transform.SetParent(parent, false);

            RectTransform rt = bossGO.GetComponent<RectTransform>() ?? bossGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -25f);
            rt.sizeDelta = new Vector2(620f, 75f);

            Image panelImg = bossGO.GetComponent<Image>() ?? bossGO.AddComponent<Image>();
            if (panelSprite != null) { panelImg.sprite = panelSprite; panelImg.type = Image.Type.Sliced; }
            panelImg.color = new Color(0.12f, 0.08f, 0.08f, 0.92f);

            TMP_Text title = ObtenerOCrearTextoRuntime(bossGO.transform, "BossTitle", font, 18, FontStyles.Bold, TextAlignmentOptions.Center);
            RectTransform rtTitle = title.rectTransform;
            rtTitle.anchorMin = new Vector2(0f, 1f);
            rtTitle.anchorMax = new Vector2(1f, 1f);
            rtTitle.pivot = new Vector2(0.5f, 1f);
            rtTitle.anchoredPosition = new Vector2(0f, -6f);
            rtTitle.sizeDelta = new Vector2(-20f, 24f);
            title.text = "WINDOWS_11_DEFENDER.EXE";
            title.color = new Color(1f, 0.88f, 0.70f);

            GameObject barArea = ObtenerOCrearHijoRuntime(bossGO.transform, "BarArea");
            RectTransform rtBarArea = barArea.GetComponent<RectTransform>() ?? barArea.AddComponent<RectTransform>();
            rtBarArea.anchorMin = new Vector2(0f, 0f);
            rtBarArea.anchorMax = new Vector2(1f, 1f);
            rtBarArea.offsetMin = new Vector2(18f, 22f);
            rtBarArea.offsetMax = new Vector2(-18f, -30f);

            Image barBg = barArea.GetComponent<Image>() ?? barArea.AddComponent<Image>();
            barBg.color = new Color(0.04f, 0.04f, 0.06f, 0.95f);

            GameObject decayGO = ObtenerOCrearHijoRuntime(barArea.transform, "DecayBar");
            RectTransform rtDecay = decayGO.GetComponent<RectTransform>() ?? decayGO.AddComponent<RectTransform>();
            rtDecay.anchorMin = Vector2.zero;
            rtDecay.anchorMax = Vector2.one;
            rtDecay.sizeDelta = Vector2.zero;
            Image decayImg = decayGO.GetComponent<Image>() ?? decayGO.AddComponent<Image>();
            decayImg.type = Image.Type.Filled;
            decayImg.fillMethod = Image.FillMethod.Horizontal;
            decayImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            decayImg.fillAmount = 1f;
            decayImg.color = new Color(0.85f, 0.35f, 0.10f, 0.90f);

            GameObject fillGO = ObtenerOCrearHijoRuntime(barArea.transform, "FillBar");
            RectTransform rtFill = fillGO.GetComponent<RectTransform>() ?? fillGO.AddComponent<RectTransform>();
            rtFill.anchorMin = Vector2.zero;
            rtFill.anchorMax = Vector2.one;
            rtFill.sizeDelta = Vector2.zero;
            Image fillImg = fillGO.GetComponent<Image>() ?? fillGO.AddComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
            fillImg.color = new Color(0.96f, 0.71f, 0.10f);

            TMP_Text subtitle = ObtenerOCrearTextoRuntime(bossGO.transform, "BossSubtitle", font, 12, FontStyles.Normal, TextAlignmentOptions.Left);
            RectTransform rtSub = subtitle.rectTransform;
            rtSub.anchorMin = new Vector2(0f, 0f);
            rtSub.anchorMax = new Vector2(0.75f, 0f);
            rtSub.pivot = new Vector2(0f, 0f);
            rtSub.anchoredPosition = new Vector2(18f, 5f);
            rtSub.sizeDelta = new Vector2(0f, 18f);
            subtitle.text = "ESTADO: ESCANEANDO ARCHIVOS DE SISTEMA... [FASE 1]";
            subtitle.color = new Color(0.75f, 0.85f, 0.90f);

            TMP_Text hpNumber = ObtenerOCrearTextoRuntime(bossGO.transform, "BossHPNumber", font, 12, FontStyles.Bold, TextAlignmentOptions.Right);
            RectTransform rtHp = hpNumber.rectTransform;
            rtHp.anchorMin = new Vector2(0.75f, 0f);
            rtHp.anchorMax = new Vector2(1f, 0f);
            rtHp.pivot = new Vector2(1f, 0f);
            rtHp.anchoredPosition = new Vector2(-18f, 5f);
            rtHp.sizeDelta = new Vector2(0f, 18f);
            hpNumber.text = "500 / 500 HP";
            hpNumber.color = new Color(1f, 0.95f, 0.75f);

            hud.bossBarContainer = bossGO;
            hud.bossHealthFillImage = fillImg;
            hud.bossHealthDecayImage = decayImg;
            hud.bossTitleText = title;
            hud.bossSubtitleText = subtitle;
            hud.bossHealthNumberText = hpNumber;
        }

        private static void ConstruirBarraTuxRuntime(Transform parent, TMP_FontAsset font, Sprite panelSprite, CombatHUDManager hud)
        {
            Transform tTux = parent.Find("TuxHealthPlate");
            GameObject tuxGO = tTux != null ? tTux.gameObject : new GameObject("TuxHealthPlate");
            tuxGO.transform.SetParent(parent, false);

            RectTransform rt = tuxGO.GetComponent<RectTransform>() ?? tuxGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(30f, -25f);
            rt.sizeDelta = new Vector2(320f, 65f);

            Image panelImg = tuxGO.GetComponent<Image>() ?? tuxGO.AddComponent<Image>();
            if (panelSprite != null) { panelImg.sprite = panelSprite; panelImg.type = Image.Type.Sliced; }
            panelImg.color = new Color(0.14f, 0.22f, 0.18f, 0.94f);

            TMP_Text title = ObtenerOCrearTextoRuntime(tuxGO.transform, "TuxStatusTitle", font, 15, FontStyles.Bold, TextAlignmentOptions.Left);
            RectTransform rtTitle = title.rectTransform;
            rtTitle.anchorMin = new Vector2(0f, 1f);
            rtTitle.anchorMax = new Vector2(1f, 1f);
            rtTitle.pivot = new Vector2(0f, 1f);
            rtTitle.anchoredPosition = new Vector2(16f, -6f);
            rtTitle.sizeDelta = new Vector2(-32f, 22f);
            title.text = "TUX_OS [KERNEL OK]";
            title.color = new Color(0.08f, 0.88f, 0.96f);

            GameObject barArea = ObtenerOCrearHijoRuntime(tuxGO.transform, "BarArea");
            RectTransform rtBarArea = barArea.GetComponent<RectTransform>() ?? barArea.AddComponent<RectTransform>();
            rtBarArea.anchorMin = new Vector2(0f, 0f);
            rtBarArea.anchorMax = new Vector2(1f, 1f);
            rtBarArea.offsetMin = new Vector2(16f, 12f);
            rtBarArea.offsetMax = new Vector2(-16f, -28f);

            Image barBg = barArea.GetComponent<Image>() ?? barArea.AddComponent<Image>();
            barBg.color = new Color(0.05f, 0.08f, 0.06f, 0.95f);

            GameObject fillGO = ObtenerOCrearHijoRuntime(barArea.transform, "FillBar");
            RectTransform rtFill = fillGO.GetComponent<RectTransform>() ?? fillGO.AddComponent<RectTransform>();
            rtFill.anchorMin = Vector2.zero;
            rtFill.anchorMax = Vector2.one;
            rtFill.sizeDelta = Vector2.zero;
            Image fillImg = fillGO.GetComponent<Image>() ?? fillGO.AddComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
            fillImg.color = new Color(0.08f, 0.88f, 0.96f);

            TMP_Text hpNumber = ObtenerOCrearTextoRuntime(tuxGO.transform, "TuxHPNumber", font, 11, FontStyles.Bold, TextAlignmentOptions.Right);
            RectTransform rtHp = hpNumber.rectTransform;
            rtHp.anchorMin = new Vector2(0.5f, 1f);
            rtHp.anchorMax = new Vector2(1f, 1f);
            rtHp.pivot = new Vector2(1f, 1f);
            rtHp.anchoredPosition = new Vector2(-16f, -8f);
            rtHp.sizeDelta = new Vector2(0f, 18f);
            hpNumber.text = "5 / 5 HP";
            hpNumber.color = new Color(0.85f, 1f, 0.95f);

            hud.tuxBarContainer = tuxGO;
            hud.tuxHealthFillImage = fillImg;
            hud.tuxStatusTitleText = title;
            hud.tuxHealthNumberText = hpNumber;
        }

        private static void ConstruirTerminalCMDRuntime(Transform parent, TMP_FontAsset font, Sprite panelSprite, CombatHUDManager hud)
        {
            Transform tTerminal = parent.Find("CMDTerminalHUD");
            GameObject termGO = tTerminal != null ? tTerminal.gameObject : new GameObject("CMDTerminalHUD");
            termGO.transform.SetParent(parent, false);

            RectTransform rt = termGO.GetComponent<RectTransform>() ?? termGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-25f, -25f);
            rt.sizeDelta = new Vector2(460f, 155f);

            Image panelImg = termGO.GetComponent<Image>() ?? termGO.AddComponent<Image>();
            if (panelSprite != null) { panelImg.sprite = panelSprite; panelImg.type = Image.Type.Sliced; }
            panelImg.color = new Color(0.04f, 0.06f, 0.05f, 0.88f);

            TMP_Text title = ObtenerOCrearTextoRuntime(termGO.transform, "TerminalTitle", font, 12, FontStyles.Bold, TextAlignmentOptions.Left);
            RectTransform rtTitle = title.rectTransform;
            rtTitle.anchorMin = new Vector2(0f, 1f);
            rtTitle.anchorMax = new Vector2(1f, 1f);
            rtTitle.pivot = new Vector2(0f, 1f);
            rtTitle.anchoredPosition = new Vector2(14f, -6f);
            rtTitle.sizeDelta = new Vector2(-28f, 18f);
            title.text = "[CMD] C:\\Windows\\System32\\cmd.exe";
            title.color = new Color(0.08f, 0.88f, 0.96f);

            TMP_Text content = ObtenerOCrearTextoRuntime(termGO.transform, "TerminalContent", font, 10, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            RectTransform rtContent = content.rectTransform;
            rtContent.anchorMin = Vector2.zero;
            rtContent.anchorMax = Vector2.one;
            rtContent.offsetMin = new Vector2(14f, 10f);
            rtContent.offsetMax = new Vector2(-14f, -24f);
            content.lineSpacing = -10f;
            content.text = "> Inicializando terminal...";
            content.color = new Color(0.85f, 0.95f, 0.90f);

            hud.terminalContainer = termGO;
            hud.terminalTitleText = title;
            hud.terminalContentText = content;
        }

        private static void ConstruirBSODRuntime(Transform parent, TMP_FontAsset font)
        {
            Transform tBSOD = parent.Find("BSOD_GlitchOverlay");
            GameObject bsodGO = tBSOD != null ? tBSOD.gameObject : new GameObject("BSOD_GlitchOverlay");
            bsodGO.transform.SetParent(parent, false);

            RectTransform rt = bsodGO.GetComponent<RectTransform>() ?? bsodGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            CanvasGroup cg = bsodGO.GetComponent<CanvasGroup>() ?? bsodGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            Image fondo = bsodGO.GetComponent<Image>() ?? bsodGO.AddComponent<Image>();
            fondo.color = new Color(0f, 0.47f, 0.84f, 1f);

            TMP_Text face = ObtenerOCrearTextoRuntime(bsodGO.transform, "CaraTriste", font, 90, FontStyles.Normal, TextAlignmentOptions.Left);
            RectTransform rtFace = face.rectTransform;
            rtFace.anchorMin = new Vector2(0.12f, 0.65f);
            rtFace.anchorMax = new Vector2(0.35f, 0.85f);
            rtFace.anchoredPosition = Vector2.zero;
            face.text = ":(";
            face.color = Color.white;

            TMP_Text msg = ObtenerOCrearTextoRuntime(bsodGO.transform, "MensajePrincipal", font, 24, FontStyles.Normal, TextAlignmentOptions.Left);
            RectTransform rtMsg = msg.rectTransform;
            rtMsg.anchorMin = new Vector2(0.12f, 0.35f);
            rtMsg.anchorMax = new Vector2(0.88f, 0.65f);
            rtMsg.anchoredPosition = Vector2.zero;
            msg.text = "Tu PC tuvo un problema y necesita reiniciarse.\nEstamos recopilando información sobre el error y luego se reiniciará automáticamente.\n\n100% completado";
            msg.color = Color.white;

            TMP_Text stopCode = ObtenerOCrearTextoRuntime(bsodGO.transform, "CodigoDetencion", font, 16, FontStyles.Normal, TextAlignmentOptions.Left);
            RectTransform rtStop = stopCode.rectTransform;
            rtStop.anchorMin = new Vector2(0.12f, 0.15f);
            rtStop.anchorMax = new Vector2(0.88f, 0.35f);
            rtStop.anchoredPosition = Vector2.zero;
            stopCode.text = "Código de detención: CRITICAL_PROCESS_DIED\nLo que falló: win32kfull.sys (Windows Defender Rootkit)";
            stopCode.color = Color.white;

            BSODGlitchEffect effect = bsodGO.GetComponent<BSODGlitchEffect>() ?? bsodGO.AddComponent<BSODGlitchEffect>();
            effect.ConfigurarReferencias(cg, fondo, face, msg, stopCode);
        }

        private static void ConstruirVictoriaDerrotaRuntime(Transform parent, TMP_FontAsset font, Sprite buttonSprite, Sprite panelSprite)
        {
            Transform tScreen = parent.Find("VictoryDefeatScreen");
            GameObject screenGO = tScreen != null ? tScreen.gameObject : new GameObject("VictoryDefeatScreen");
            screenGO.transform.SetParent(parent, false);

            RectTransform rt = screenGO.GetComponent<RectTransform>() ?? screenGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            CanvasGroup cg = screenGO.GetComponent<CanvasGroup>() ?? screenGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // Panel de Victoria
            GameObject vicGO = ObtenerOCrearHijoRuntime(screenGO.transform, "VictoryPanel");
            RectTransform rtVic = vicGO.GetComponent<RectTransform>() ?? vicGO.AddComponent<RectTransform>();
            rtVic.anchorMin = new Vector2(0.5f, 0.5f);
            rtVic.anchorMax = new Vector2(0.5f, 0.5f);
            rtVic.sizeDelta = new Vector2(680f, 380f);
            Image vicImg = vicGO.GetComponent<Image>() ?? vicGO.AddComponent<Image>();
            if (panelSprite != null) { vicImg.sprite = panelSprite; vicImg.type = Image.Type.Sliced; }
            vicImg.color = new Color(0.08f, 0.16f, 0.14f, 0.96f);

            TMP_Text vicTitle = ObtenerOCrearTextoRuntime(vicGO.transform, "VicTitle", font, 28, FontStyles.Bold, TextAlignmentOptions.Center);
            vicTitle.rectTransform.anchoredPosition = new Vector2(0f, 130f);
            vicTitle.rectTransform.sizeDelta = new Vector2(640f, 40f);
            vicTitle.text = "[SUDO KILL -9 EXITOSO]";
            vicTitle.color = new Color(0.08f, 0.95f, 1f);

            TMP_Text vicSub = ObtenerOCrearTextoRuntime(vicGO.transform, "VicSub", font, 14, FontStyles.Normal, TextAlignmentOptions.Center);
            vicSub.rectTransform.anchoredPosition = new Vector2(0f, 85f);
            vicSub.rectTransform.sizeDelta = new Vector2(640f, 30f);
            vicSub.text = "PROCESO WINDOWS_11_DEFENDER.EXE TERMINADO FORZOSAMENTE";
            vicSub.color = new Color(0.85f, 0.95f, 0.90f);

            TMP_Text vicStats = ObtenerOCrearTextoRuntime(vicGO.transform, "VicStats", font, 13, FontStyles.Normal, TextAlignmentOptions.Center);
            vicStats.rectTransform.anchoredPosition = new Vector2(0f, 15f);
            vicStats.rectTransform.sizeDelta = new Vector2(600f, 80f);
            vicStats.text = "[ESTADO]: PRIVILEGIOS DE ROOT ASEGURADOS\n[SALIDA]: Exit code 0";
            vicStats.color = new Color(0.75f, 0.85f, 0.80f);

            Button vicBtnRestart = CrearBotonTacticoRuntime(vicGO.transform, "Btn_ReiniciarMision", "REINICIAR MISIÓN", new Vector2(-150f, -120f), font, buttonSprite);
            Button vicBtnMenu = CrearBotonTacticoRuntime(vicGO.transform, "Btn_MenuPrincipal", "MENÚ PRINCIPAL", new Vector2(150f, -120f), font, buttonSprite);

            // Panel de Derrota
            GameObject defGO = ObtenerOCrearHijoRuntime(screenGO.transform, "DefeatPanel");
            RectTransform rtDef = defGO.GetComponent<RectTransform>() ?? defGO.AddComponent<RectTransform>();
            rtDef.anchorMin = new Vector2(0.5f, 0.5f);
            rtDef.anchorMax = new Vector2(0.5f, 0.5f);
            rtDef.sizeDelta = new Vector2(680f, 380f);
            Image defImg = defGO.GetComponent<Image>() ?? defGO.AddComponent<Image>();
            if (panelSprite != null) { defImg.sprite = panelSprite; defImg.type = Image.Type.Sliced; }
            defImg.color = new Color(0.18f, 0.06f, 0.06f, 0.96f);

            TMP_Text defTitle = ObtenerOCrearTextoRuntime(defGO.transform, "DefTitle", font, 28, FontStyles.Bold, TextAlignmentOptions.Center);
            defTitle.rectTransform.anchoredPosition = new Vector2(0f, 130f);
            defTitle.rectTransform.sizeDelta = new Vector2(640f, 40f);
            defTitle.text = "[KERNEL PANIC: SYSTEM CRASH]";
            defTitle.color = new Color(0.95f, 0.22f, 0.18f);

            TMP_Text defSub = ObtenerOCrearTextoRuntime(defGO.transform, "DefSub", font, 14, FontStyles.Normal, TextAlignmentOptions.Center);
            defSub.rectTransform.anchoredPosition = new Vector2(0f, 85f);
            defSub.rectTransform.sizeDelta = new Vector2(640f, 30f);
            defSub.text = "FATAL EXCEPTION: MEMORY_MANAGEMENT_VIOLATION";
            defSub.color = new Color(1f, 0.80f, 0.80f);

            TMP_Text defCause = ObtenerOCrearTextoRuntime(defGO.transform, "DefCause", font, 13, FontStyles.Normal, TextAlignmentOptions.Center);
            defCause.rectTransform.anchoredPosition = new Vector2(0f, 15f);
            defCause.rectTransform.sizeDelta = new Vector2(600f, 80f);
            defCause.text = "[CAUSA]: El pingüino Linux fue neutralizado por Windows Defender.\n[ACCIÓN]: Reintente la ejecución con permisos elevados.";
            defCause.color = new Color(0.90f, 0.75f, 0.75f);

            Button defBtnRetry = CrearBotonTacticoRuntime(defGO.transform, "Btn_Reintentar", "REINTENTAR", new Vector2(-150f, -120f), font, buttonSprite);
            Button defBtnMenu = CrearBotonTacticoRuntime(defGO.transform, "Btn_MenuPrincipal", "MENÚ PRINCIPAL", new Vector2(150f, -120f), font, buttonSprite);

            VictoryDefeatScreen screenComp = screenGO.GetComponent<VictoryDefeatScreen>() ?? screenGO.AddComponent<VictoryDefeatScreen>();
            screenComp.ConfigurarReferencias(
                vicGO, defGO, cg,
                vicTitle, vicSub, vicStats, vicBtnRestart, vicBtnMenu,
                defTitle, defSub, defCause, defBtnRetry, defBtnMenu);
        }

        private static Button CrearBotonTacticoRuntime(Transform parent, string nombre, string texto, Vector2 pos, TMP_FontAsset font, Sprite btnSprite)
        {
            GameObject btnGO = ObtenerOCrearHijoRuntime(parent, nombre);
            RectTransform rt = btnGO.GetComponent<RectTransform>() ?? btnGO.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(250f, 50f);

            Image img = btnGO.GetComponent<Image>() ?? btnGO.AddComponent<Image>();
            if (btnSprite != null) { img.sprite = btnSprite; img.type = Image.Type.Sliced; }
            img.color = new Color(0.10f, 0.16f, 0.22f, 0.95f);

            Button btn = btnGO.GetComponent<Button>() ?? btnGO.AddComponent<Button>();

            if (btnGO.GetComponent<MenuButtonEffects>() == null)
            {
                btnGO.AddComponent<MenuButtonEffects>();
            }

            TMP_Text label = ObtenerOCrearTextoRuntime(btnGO.transform, "Text", font, 14, FontStyles.Bold, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
            label.text = texto;
            label.color = new Color(0.85f, 0.95f, 1f);

            return btn;
        }

        private static GameObject ObtenerOCrearHijoRuntime(Transform parent, string nombre)
        {
            Transform t = parent.Find(nombre);
            if (t != null) return t.gameObject;
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text ObtenerOCrearTextoRuntime(Transform parent, string nombre, TMP_FontAsset font, float tamano, FontStyles estilo, TextAlignmentOptions alineacion)
        {
            GameObject go = ObtenerOCrearHijoRuntime(parent, nombre);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = tamano;
            tmp.fontStyle = estilo;
            tmp.alignment = alineacion;
            return tmp;
        }

        private static Sprite CrearSpritePixelBorde(int width, int height, Color fill, Color border, int borderWidth)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = x < borderWidth || x >= width - borderWidth || y < borderWidth || y >= height - borderWidth;
                    pixels[y * width + x] = isBorder ? border : fill;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(borderWidth, borderWidth, borderWidth, borderWidth));
        }
    }
}

