using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace SudoKill9.UI
{
    /// <summary>
    /// Gestiona las pantallas tácticas de Victoria y Derrota con estética Pixel Art:
    /// - Victoria: '[SUDO KILL -9 EXITOSO]' -> Muestra estadísticas y opciones de reiniciar o volver al menú.
    /// - Derrota: '[KERNEL PANIC: SYSTEM CRASH]' -> Opciones de reintentar de inmediato o menú principal.
    /// - Transición en cámara lenta cinemática al activarse.
    /// - Libera automáticamente el cursor del mouse para permitir la interacción inmediata con los botones.
    /// </summary>
    public class VictoryDefeatScreen : MonoBehaviour
    {
        public static VictoryDefeatScreen Instance { get; private set; }

        [Header("Contenedores de Paneles")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;
        [SerializeField] private CanvasGroup screenCanvasGroup;

        [Header("Elementos de Victoria")]
        [SerializeField] private TMP_Text victoryTitleText;
        [SerializeField] private TMP_Text victorySubtitleText;
        [SerializeField] private TMP_Text victoryStatsText;
        [SerializeField] private Button victoryRestartButton;
        [SerializeField] private Button victoryMenuButton;

        [Header("Elementos de Derrota")]
        [SerializeField] private TMP_Text defeatTitleText;
        [SerializeField] private TMP_Text defeatSubtitleText;
        [SerializeField] private TMP_Text defeatCauseText;
        [SerializeField] private Button defeatRetryButton;
        [SerializeField] private Button defeatMenuButton;

        [Header("Configuración de Cinemática")]
        [Tooltip("Escala de tiempo lenta al terminar el combate.")]
        [SerializeField] private float slowMotionTimeScale = 0.25f;

        [Tooltip("Tiempo real antes de mostrar el panel tras la muerte.")]
        [SerializeField] private float delayAntesDeMostrar = 1.2f;

        [Header("Audio (Opcional)")]
        [SerializeField] private AudioClip victorySound;
        [SerializeField] private AudioClip defeatSound;
        [SerializeField] private AudioSource audioSource;

        private float tiempoInicioCombate;
        private bool pantallaMostrada = false;

        public bool EstaMostrada => pantallaMostrada;

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

            if (screenCanvasGroup == null) screenCanvasGroup = GetComponent<CanvasGroup>();

            OcultarPaneles();
            ConfigurarBotones();
        }

        private void Start()
        {
            tiempoInicioCombate = Time.time;
            OcultarPaneles();
        }

        private void Update()
        {
            if (pantallaMostrada)
            {
                if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
        }

        private void OnEnable()
        {
            WindowsBossAI.OnBossDied -= HandleBossDied;
            WindowsBossAI.OnBossDied += HandleBossDied;

            LinuxCombatIK.OnPlayerDied -= HandlePlayerDied;
            LinuxCombatIK.OnPlayerDied += HandlePlayerDied;
        }

        private void OnDisable()
        {
            WindowsBossAI.OnBossDied -= HandleBossDied;
            LinuxCombatIK.OnPlayerDied -= HandlePlayerDied;
        }

        private void OnDestroy()
        {
            // Garantizar que Time.timeScale siempre regrese a 1 si se destruye la pantalla
            Time.timeScale = 1.0f;
        }

        private void ConfigurarBotones()
        {
            if (victoryRestartButton != null)
            {
                victoryRestartButton.onClick.RemoveAllListeners();
                victoryRestartButton.onClick.AddListener(ReiniciarMision);
            }

            if (victoryMenuButton != null)
            {
                victoryMenuButton.onClick.RemoveAllListeners();
                victoryMenuButton.onClick.AddListener(CargarMenuPrincipal);
            }

            if (defeatRetryButton != null)
            {
                defeatRetryButton.onClick.RemoveAllListeners();
                defeatRetryButton.onClick.AddListener(ReiniciarMision);
            }

            if (defeatMenuButton != null)
            {
                defeatMenuButton.onClick.RemoveAllListeners();
                defeatMenuButton.onClick.AddListener(CargarMenuPrincipal);
            }
        }

        private void OcultarPaneles()
        {
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);

            if (screenCanvasGroup != null)
            {
                screenCanvasGroup.alpha = 0f;
                screenCanvasGroup.blocksRaycasts = false;
                screenCanvasGroup.interactable = false;
            }
        }

        private void HandleBossDied()
        {
            if (pantallaMostrada) return;
            // La cinemática final (monitor en el cielo -> créditos) sustituye al panel de victoria.
            if (SudoKill9.Cinematics.FinalCinematicDirector.ControlaLaVictoria)
            {
                pantallaMostrada = true; // bloquea también el panel de derrota durante la cinemática
                return;
            }
            pantallaMostrada = true;
            StartCoroutine(RutinaMostrarVictoria());
        }

        private void HandlePlayerDied()
        {
            if (pantallaMostrada) return;
            pantallaMostrada = true;
            StartCoroutine(RutinaMostrarDerrota());
        }

        private IEnumerator RutinaMostrarVictoria()
        {
            Time.timeScale = slowMotionTimeScale;
            yield return new WaitForSecondsRealtime(delayAntesDeMostrar);

            LiberarCursor();

            if (audioSource != null && victorySound != null)
            {
                audioSource.PlayOneShot(victorySound);
            }

            float duracionCombate = Time.time - tiempoInicioCombate;
            int minutos = Mathf.FloorToInt(duracionCombate / 60f);
            int segundos = Mathf.FloorToInt(duracionCombate % 60f);

            if (victoryStatsText != null)
            {
                victoryStatsText.text =
                    $"[TIEMPO DE COMBATE]: {minutos:00}:{segundos:00}\n" +
                    "[PROCESO TERMINADO]: WINDOWS_11_DEFENDER.EXE (PID 4096)\n" +
                    "[ESTADO DEL SISTEMA]: ROOT PRIVILEGES SECURED (exit code 0)";
            }

            if (victoryPanel != null) victoryPanel.SetActive(true);
            if (defeatPanel != null) defeatPanel.SetActive(false);

            yield return RutinaFadeIn();
        }

        private IEnumerator RutinaMostrarDerrota()
        {
            Time.timeScale = slowMotionTimeScale;
            yield return new WaitForSecondsRealtime(delayAntesDeMostrar);

            LiberarCursor();

            if (audioSource != null && defeatSound != null)
            {
                audioSource.PlayOneShot(defeatSound);
            }

            if (defeatCauseText != null)
            {
                defeatCauseText.text =
                    "[ERROR]: KERNEL_SECURITY_CHECK_FAILURE\n" +
                    "[CAUSA]: Proceso Tux interceptado y terminado por Windows Defender.\n" +
                    "[ACCIÓN]: Reinicie el kernel de inmediato para reintentar la misión.";
            }

            if (defeatPanel != null) defeatPanel.SetActive(true);
            if (victoryPanel != null) victoryPanel.SetActive(false);

            yield return RutinaFadeIn();
        }

        private IEnumerator RutinaFadeIn()
        {
            if (screenCanvasGroup == null) yield break;

            screenCanvasGroup.blocksRaycasts = true;
            screenCanvasGroup.interactable = true;

            float transcurrido = 0f;
            float duracion = 0.5f;

            while (transcurrido < duracion)
            {
                transcurrido += Time.unscaledDeltaTime;
                screenCanvasGroup.alpha = Mathf.Clamp01(transcurrido / duracion);
                yield return null;
            }

            screenCanvasGroup.alpha = 1f;
        }

        private void LiberarCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ReiniciarMision()
        {
            Time.timeScale = 1.0f;
            string escenaActual = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(escenaActual))
            {
                escenaActual = "prueba_Expe_K";
            }
            SceneManager.LoadScene(escenaActual);
        }

        public void CargarMenuPrincipal()
        {
            Time.timeScale = 1.0f;
            SceneManager.LoadScene("Inicio");
        }

        internal void ConfigurarReferencias(
            GameObject vicPanel, GameObject defPanel, CanvasGroup cg,
            TMP_Text vicTitle, TMP_Text vicSub, TMP_Text vicStats, Button vicRestart, Button vicMenu,
            TMP_Text defTitle, TMP_Text defSub, TMP_Text defCause, Button defRetry, Button defMenu)
        {
            victoryPanel = vicPanel;
            defeatPanel = defPanel;
            screenCanvasGroup = cg;
            victoryTitleText = vicTitle;
            victorySubtitleText = vicSub;
            victoryStatsText = vicStats;
            victoryRestartButton = vicRestart;
            victoryMenuButton = vicMenu;
            defeatTitleText = defTitle;
            defeatSubtitleText = defSub;
            defeatCauseText = defCause;
            defeatRetryButton = defRetry;
            defeatMenuButton = defMenu;
            OcultarPaneles();
            ConfigurarBotones();
        }
    }
}
