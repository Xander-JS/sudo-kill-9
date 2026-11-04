using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SudoKill9.Levels
{
    /// <summary>
    /// HUD del nivel de supervivencia: temporizador, contador de enemigos, avisos de horda y el panel
    /// modal de nivel completado / derrota. Construye su propio Canvas en Awake (no requiere prefab)
    /// con el estilo del proyecto: TextMeshPro en verde primario sobre fondo oscuro.
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

        [Header("Textos")]
        [SerializeField] private string surviveLabel = "SURVIVE";
        [SerializeField] private string clearLabel = "ELIMINATE REMAINING: {0}";
        [SerializeField] private string continueHint = "Press [{0}] to continue";
        [SerializeField] private string defeatTitle = "KERNEL PANIC";
        [SerializeField] private string defeatHint = "Press [{0}] to retry";
        [SerializeField] private float announcementDuration = 2.2f;

        private SurvivalLevelController level;
        private TMP_Text timerText, subtitleText, enemiesText, announceText;
        private TMP_Text panelTitle, panelHint;
        private CanvasGroup panelGroup;
        private Coroutine announceRoutine;

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
        }

        private void OnDisable()
        {
            level.OnAnnouncement -= Announce;
            level.onLevelCompleted.RemoveListener(ShowCompleted);
            level.onLevelFailed.RemoveListener(ShowFailed);
        }

        private void Update()
        {
            switch (level.State)
            {
                case SurvivalLevelState.Starting:
                    timerText.text = Mathf.CeilToInt(level.StartCountdown).ToString();
                    timerText.color = primaryGreen;
                    subtitleText.text = $"LEVEL {level.LevelNumber} · {surviveLabel} {Mathf.RoundToInt(level.Duration)}s";
                    break;

                case SurvivalLevelState.Running:
                    float t = level.RemainingTime;
                    timerText.text = $"{(int)t / 60:00}:{(int)t % 60:00}";
                    timerText.color = t <= 10f ? dangerColor : level.InFinalStretch ? warningColor : primaryGreen;
                    subtitleText.text = surviveLabel;
                    break;

                case SurvivalLevelState.Clearing:
                    timerText.text = "00:00";
                    timerText.color = warningColor;
                    subtitleText.text = string.Format(clearLabel, level.AliveCount);
                    break;
            }

            enemiesText.text = $"HOSTILES: {level.AliveCount}";

            if (panelHint != null && panelGroup.alpha > 0f)
            {
                bool failed = level.State == SurvivalLevelState.Failed;
                panelHint.text = level.CanContinue ? string.Format(failed ? defeatHint : continueHint, level.ContinueKey.ToString().ToUpper()) : "";
                // parpadeo suave del aviso de tecla
                panelHint.alpha = 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 1.6f, 1f);
            }
        }

        // ------------------------------------------------------------------ Eventos

        private void Announce(string message)
        {
            if (announceRoutine != null) StopCoroutine(announceRoutine);
            announceRoutine = StartCoroutine(AnnounceRoutine(message));
        }

        private IEnumerator AnnounceRoutine(string message)
        {
            announceText.text = message;
            for (float t = 0f; t < announcementDuration; t += Time.unscaledDeltaTime)
            {
                // entra rápido, parpadea, se desvanece al final
                float fade = Mathf.Clamp01(Mathf.Min(t / 0.15f, (announcementDuration - t) / 0.4f));
                float blink = 0.7f + 0.3f * Mathf.Sin(t * 14f);
                announceText.alpha = fade * blink;
                yield return null;
            }
            announceText.alpha = 0f;
        }

        private void ShowCompleted() => ShowPanel(level.CompletedMessage, primaryGreen);
        private void ShowFailed() => ShowPanel(defeatTitle, dangerColor);

        private void ShowPanel(string title, Color color)
        {
            announceText.alpha = 0f;
            panelTitle.text = title;
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
