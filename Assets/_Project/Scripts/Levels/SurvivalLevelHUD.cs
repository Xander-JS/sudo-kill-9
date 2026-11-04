using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace SudoKill9.Levels
{
    /// <summary>
    /// HUD del nivel de supervivencia: temporizador con barra de progreso, vida de Linux (celdas pixel),
    /// contador de enemigos, avisos de horda y el panel modal de nivel completado / derrota. Construye su propio Canvas en Awake (no requiere prefab)
    /// con el estilo del proyecto: TextMeshPro en verde primario sobre fondo oscuro.
    /// Todos los textos salen de la String Table "Gameplay" (ver <see cref="GameplayKeys"/>) y se
    /// recargan al cambiar <see cref="LocalizationSettings.SelectedLocale"/>, sin reiniciar la escena.
    /// </summary>
    [RequireComponent(typeof(SurvivalLevelController))]
    public class SurvivalLevelHUD : MonoBehaviour
    {
        [Header("Estilo")]
        [Tooltip("Vacío = fuente por defecto de TMP.")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Color primaryGreen = new Color(0.2f, 0.95f, 0.45f, 1f);
        [SerializeField] private Color warningColor = new Color(0.96f, 0.71f, 0.10f, 1f); // ámbar #F4B41A
        [SerializeField] private Color dangerColor = new Color(0.88f, 0.14f, 0.14f, 1f);  // rojo #E02424
        [SerializeField] private Color panelBackground = new Color(0.04f, 0.07f, 0.06f, 0.94f);
        [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.6f);

        [Header("Avisos")]
        [SerializeField] private float announcementDuration = 2.2f;

        [Header("Vida de Linux")]
        [SerializeField] private Color emptyCellColor = new Color(0.12f, 0.16f, 0.14f, 1f);
        [Tooltip("Segundos que el panel de vida destella en rojo al recibir un golpe.")]
        [SerializeField] private float hitFlashDuration = 0.35f;

        private SurvivalLevelController level;
        private TMP_Text timerText, subtitleText, enemiesText, announceText;
        private TMP_Text panelTitle, panelHint;
        private CanvasGroup panelGroup;
        private Coroutine announceRoutine;

        // Progreso del nivel
        private RectTransform progressFill;
        private Image progressFillImage;
        private TMP_Text progressText;

        // Vida de Linux
        private RectTransform healthCellsRoot;
        private Image healthBox;
        private TMP_Text healthText;
        private readonly List<Image> healthCells = new List<Image>();
        private float lastHealth = -1f, healthFlashTimer;

        // Localización: tabla del locale activo + textos "vivos" que hay que re-traducir al cambiar de idioma.
        private StringTable table;
        private int tableRequest;
        private readonly HashSet<string> reportedMissing = new HashSet<string>();
        private string announceKey, panelKey;
        private object announceArg, panelArg;

        private void Awake()
        {
            level = GetComponent<SurvivalLevelController>();
            BuildUI();
        }

        private void OnEnable()
        {
            level.OnAnnouncement += Announce;
            level.onLevelCompleted.AddListener(ShowCompleted);
            level.onLevelFailed.AddListener(ShowFailed);
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            LinuxCombatIK.OnPlayerHealthChanged += OnPlayerHealthChanged;
            LoadTable();
        }

        private void Start()
        {
            // Valor inicial: el evento de LinuxCombatIK puede haberse disparado antes de suscribirnos.
            var ik = FindFirstObjectByType<LinuxCombatIK>();
            if (ik != null) OnPlayerHealthChanged(ik.CurrentHealth, ik.MaxHealth);
        }

        private void OnDisable()
        {
            level.OnAnnouncement -= Announce;
            level.onLevelCompleted.RemoveListener(ShowCompleted);
            level.onLevelFailed.RemoveListener(ShowFailed);
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            LinuxCombatIK.OnPlayerHealthChanged -= OnPlayerHealthChanged;
        }

        // ------------------------------------------------------------------ Localización

        private void OnLocaleChanged(Locale _) => LoadTable();

        private void LoadTable()
        {
            int request = ++tableRequest; // si el idioma cambia dos veces seguidas, gana la última petición
            var op = LocalizationSettings.StringDatabase.GetTableAsync(GameplayKeys.Table);
            op.Completed += h =>
            {
                if (this == null || request != tableRequest) return;
                table = h.Result;
                if (table == null) Debug.LogError($"[SurvivalLevelHUD] No se pudo cargar la String Table '{GameplayKeys.Table}'. Ejecuta Tools/Survival Levels/Sync Gameplay Localization (es-en).", this);
                RefreshStaticTexts();
            };
        }

        /// <summary>Texto traducido de <paramref name="key"/>; con argumentos aplica string.Format.</summary>
        private string L(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (table == null) return string.Empty; // la tabla llega en el primer frame; no mostrar claves crudas

            string value = table.GetEntry(key)?.LocalizedValue;
            if (string.IsNullOrEmpty(value))
            {
                if (reportedMissing.Add(key))
                    Debug.LogWarning($"[SurvivalLevelHUD] Falta la clave '{key}' en '{GameplayKeys.Table}' ({table.LocaleIdentifier.Code}).", this);
                return key;
            }
            return args == null || args.Length == 0 ? value : string.Format(value, args);
        }

        /// <summary>Nombre localizado de la tecla (gameplay_key_space → ESPACIO); si no hay clave, el nombre del enum.</summary>
        private string KeyName(UnityEngine.InputSystem.Key key)
        {
            string id = GameplayKeys.KeyPrefix + key.ToString().ToLowerInvariant();
            string value = table?.GetEntry(id)?.LocalizedValue;
            return string.IsNullOrEmpty(value) ? key.ToString().ToUpperInvariant() : value;
        }

        /// <summary>Re-traduce los textos que no se recalculan cada frame (aviso activo y título del panel).</summary>
        private void RefreshStaticTexts()
        {
            if (announceKey != null) announceText.text = L(announceKey, Args(announceArg));
            if (panelKey != null) panelTitle.text = L(panelKey, Args(panelArg));
        }

        private static object[] Args(object arg) => arg == null ? null : new[] { arg };

        private void Update()
        {
            switch (level.State)
            {
                case SurvivalLevelState.Starting:
                    timerText.text = Mathf.CeilToInt(level.StartCountdown).ToString();
                    timerText.color = primaryGreen;
                    subtitleText.text = L(GameplayKeys.LevelIntro, level.LevelNumber, Mathf.RoundToInt(level.Duration));
                    break;

                case SurvivalLevelState.Running:
                    float t = level.RemainingTime;
                    timerText.text = $"{(int)t / 60:00}:{(int)t % 60:00}";
                    timerText.color = t <= 10f ? dangerColor : level.InFinalStretch ? warningColor : primaryGreen;
                    subtitleText.text = L(GameplayKeys.Survive);
                    break;

                case SurvivalLevelState.Clearing:
                    timerText.text = "00:00";
                    timerText.color = warningColor;
                    subtitleText.text = L(GameplayKeys.ClearCounter, level.AliveCount);
                    break;
            }

            enemiesText.text = L(GameplayKeys.Hostiles, level.AliveCount);

            UpdateProgress();

            if (healthFlashTimer > 0f)
            {
                healthFlashTimer -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(healthFlashTimer / hitFlashDuration);
                healthBox.color = Color.Lerp(panelBackground, new Color(dangerColor.r, dangerColor.g, dangerColor.b, panelBackground.a), k);
            }

            if (panelHint != null && panelGroup.alpha > 0f)
            {
                bool failed = level.State == SurvivalLevelState.Failed;
                panelHint.text = level.CanContinue ? L(failed ? GameplayKeys.RetryHint : GameplayKeys.ContinueHint, KeyName(level.ContinueKey)) : "";
                // parpadeo suave del aviso de tecla
                panelHint.alpha = 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 1.6f, 1f);
            }
        }

        // ------------------------------------------------------------------ Progreso y vida

        private void UpdateProgress()
        {
            float p = level.State switch
            {
                SurvivalLevelState.Starting => 0f,
                SurvivalLevelState.Running => level.Progress,
                _ => 1f // Clearing / Completed (y Failed muestra hasta dónde llegó, congelado abajo)
            };
            if (level.State == SurvivalLevelState.Failed) p = level.Progress;

            progressFill.anchorMax = new Vector2(p, 1f);
            progressFillImage.color = timerText.color;
            progressText.text = $"{Mathf.FloorToInt(p * 100f)}%";
        }

        /// <summary>Recibe (vida actual, vida máxima) de LinuxCombatIK, que reenvía PlayerHealth.OnHealthChanged.</summary>
        private void OnPlayerHealthChanged(float current, float max)
        {
            int cells = Mathf.Clamp(Mathf.RoundToInt(max), 1, 20);
            if (cells != healthCells.Count) BuildHealthCells(cells);

            float ratio = max > 0f ? current / max : 0f;
            Color full = ratio <= 0.3f ? dangerColor : ratio <= 0.6f ? warningColor : primaryGreen;
            // Si la vida no es en corazones (fallback de 100 HP) cada celda representa una fracción.
            float perCell = max / cells;
            for (int i = 0; i < healthCells.Count; i++)
                healthCells[i].color = current >= (i + 1) * perCell - 0.001f ? full : emptyCellColor;

            healthText.text = $"{Mathf.CeilToInt(current)}/{Mathf.RoundToInt(max)}";
            healthText.color = full;

            if (lastHealth >= 0f && current < lastHealth) healthFlashTimer = hitFlashDuration;
            lastHealth = current;
        }

        private void BuildHealthCells(int count)
        {
            foreach (var c in healthCells) if (c != null) Destroy(c.gameObject);
            healthCells.Clear();

            // Paso fijo de 32 px hasta 15 celdas; con más se encogen para que la fila no pase de MaxCellsRowWidth
            // (a 1920x1080 de referencia el recuadro queda lejos del temporizador centrado, también en 4:3).
            const float MaxCellsRowWidth = 480f;
            float step = Mathf.Min(32f, MaxCellsRowWidth / count);
            float size = Mathf.Round(step * 0.8f);
            for (int i = 0; i < count; i++)
            {
                var cell = CreateImage(healthCellsRoot, $"Cell_{i}", emptyCellColor);
                SetAnchors(cell, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * step, 0f), new Vector2(size, size), new Vector2(0f, 0.5f));
                healthCells.Add(cell.GetComponent<Image>());
            }
            // El recuadro crece con la fila (40 px = márgenes izquierdo y derecho).
            var box = (RectTransform)healthBox.transform;
            box.sizeDelta = new Vector2(Mathf.Max(260f, 40f + (count - 1) * step + size), box.sizeDelta.y);
        }

        // ------------------------------------------------------------------ Eventos

        private void Announce(string key, object arg)
        {
            if (announceRoutine != null) StopCoroutine(announceRoutine);
            announceKey = key;
            announceArg = arg;
            announceRoutine = StartCoroutine(AnnounceRoutine());
        }

        private IEnumerator AnnounceRoutine()
        {
            announceText.text = L(announceKey, Args(announceArg));
            for (float t = 0f; t < announcementDuration; t += Time.unscaledDeltaTime)
            {
                // entra rápido, parpadea, se desvanece al final
                float fade = Mathf.Clamp01(Mathf.Min(t / 0.15f, (announcementDuration - t) / 0.4f));
                float blink = 0.7f + 0.3f * Mathf.Sin(t * 14f);
                announceText.alpha = fade * blink;
                yield return null;
            }
            announceText.alpha = 0f;
            announceKey = null;
        }

        private void ShowCompleted() => ShowPanel(level.CompletedMessageKey, level.LevelNumber, primaryGreen);
        private void ShowFailed() => ShowPanel(GameplayKeys.DefeatTitle, null, dangerColor);

        private void ShowPanel(string key, object arg, Color color)
        {
            if (announceRoutine != null) StopCoroutine(announceRoutine);
            announceText.alpha = 0f;
            announceKey = null;
            panelKey = key;
            panelArg = arg;
            panelTitle.text = L(key, Args(arg));
            panelTitle.color = color;
            StartCoroutine(FadePanelIn());
        }

        private IEnumerator FadePanelIn()
        {
            panelGroup.blocksRaycasts = true;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                panelGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }
            panelGroup.alpha = 1f;
        }

        // ------------------------------------------------------------------ Construcción de la UI

        private void BuildUI()
        {
            if (font == null) font = TMP_Settings.defaultFontAsset;

            var canvasGO = new GameObject("SurvivalHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGO.transform;

            // Temporizador superior
            var timerBox = CreateImage(root, "TimerBox", panelBackground);
            SetAnchors(timerBox, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(340f, 120f), new Vector2(0.5f, 1f));
            AddOutline(timerBox.gameObject);
            timerText = CreateText(timerBox, "Timer", 64f, primaryGreen);
            SetAnchors(timerText.rectTransform, new Vector2(0f, 0.35f), Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            subtitleText = CreateText(timerBox, "Subtitle", 22f, primaryGreen);
            SetAnchors(subtitleText.rectTransform, Vector2.zero, new Vector2(1f, 0.38f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));

            // Barra de progreso del nivel (bajo el temporizador)
            var progressBox = CreateImage(root, "ProgressBar", panelBackground);
            SetAnchors(progressBox, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(340f, 26f), new Vector2(0.5f, 1f));
            AddOutline(progressBox.gameObject);
            progressFill = CreateImage(progressBox, "Fill", primaryGreen);
            progressFillImage = progressFill.GetComponent<Image>();
            SetAnchors(progressFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
            progressFill.offsetMin = new Vector2(3f, 3f);
            progressFill.offsetMax = new Vector2(-3f, -3f);
            progressText = CreateText(progressBox, "Percent", 18f, Color.white);
            SetAnchors(progressText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            progressText.outlineWidth = 0.25f;
            progressText.outlineColor = new Color32(0, 0, 0, 255);

            // Vida de Linux (arriba a la izquierda)
            var healthRT = CreateImage(root, "PlayerHealth", panelBackground);
            healthBox = healthRT.GetComponent<Image>();
            SetAnchors(healthRT, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -32f), new Vector2(260f, 84f), new Vector2(0f, 1f));
            AddOutline(healthRT.gameObject);
            var healthLabel = CreateText(healthRT, "Label", 20f, primaryGreen);
            healthLabel.text = "LINUX";
            healthLabel.alignment = TextAlignmentOptions.Left;
            SetAnchors(healthLabel.rectTransform, new Vector2(0f, 0.55f), Vector2.one, new Vector2(20f, 0f), new Vector2(-40f, 0f), new Vector2(0f, 0.5f));
            healthText = CreateText(healthRT, "Value", 20f, primaryGreen);
            healthText.alignment = TextAlignmentOptions.Right;
            SetAnchors(healthText.rectTransform, new Vector2(0f, 0.55f), Vector2.one, new Vector2(-20f, 0f), new Vector2(-40f, 0f), new Vector2(1f, 0.5f));
            healthCellsRoot = new GameObject("Cells", typeof(RectTransform)).GetComponent<RectTransform>();
            healthCellsRoot.SetParent(healthRT, false);
            SetAnchors(healthCellsRoot, new Vector2(0f, 0f), new Vector2(1f, 0.55f), new Vector2(20f, 4f), new Vector2(-40f, -8f), new Vector2(0f, 0.5f));

            // Contador de enemigos (arriba a la derecha)
            enemiesText = CreateText(root, "Hostiles", 26f, primaryGreen);
            enemiesText.alignment = TextAlignmentOptions.Right;
            SetAnchors(enemiesText.rectTransform, Vector2.one, Vector2.one, new Vector2(-32f, -32f), new Vector2(420f, 40f), Vector2.one);

            // Aviso de horda
            announceText = CreateText(root, "Announcement", 48f, warningColor);
            SetAnchors(announceText.rectTransform, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), Vector2.zero, new Vector2(1400f, 80f), new Vector2(0.5f, 0.5f));
            announceText.alpha = 0f;

            // Panel modal (completado / derrota)
            var overlay = CreateImage(root, "ResultOverlay", overlayColor);
            SetAnchors(overlay, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            panelGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = false;

            var panel = CreateImage(overlay, "ResultPanel", panelBackground);
            SetAnchors(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 320f), new Vector2(0.5f, 0.5f));
            AddOutline(panel.gameObject);

            panelTitle = CreateText(panel, "Title", 76f, primaryGreen);
            SetAnchors(panelTitle.rectTransform, new Vector2(0f, 0.4f), Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            panelHint = CreateText(panel, "Hint", 28f, primaryGreen);
            SetAnchors(panelHint.rectTransform, Vector2.zero, new Vector2(1f, 0.4f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        }

        private void AddOutline(GameObject go)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(primaryGreen.r, primaryGreen.g, primaryGreen.b, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private static RectTransform CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        private TMP_Text CreateText(Transform parent, string name, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var txt = go.GetComponent<TextMeshProUGUI>();
            if (font != null) txt.font = font;
            txt.fontSize = size;
            txt.color = color;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            return txt;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
