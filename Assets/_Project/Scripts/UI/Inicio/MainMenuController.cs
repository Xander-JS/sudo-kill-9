using SudoKill9.Settings;
using UnityEngine;
using SudoKill9.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SudoKill9.UI.Inicio
{
    /// <summary>
    /// Controlador principal para la escena de Inicio (Menú).
    /// Maneja la navegación de escenas, modales de opciones y créditos, y salida del juego.
    /// Completamente modular e independiente de los sistemas de gameplay.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Escenas")]
        [Tooltip("Nombre de la escena principal de juego a cargar.")]
        [SerializeField] private string gameplaySceneName = "prueba";

        [Tooltip("Nombre de la escena de créditos a cargar.")]
        [SerializeField] private string creditsSceneName = "Credits";

        [Tooltip("Nombre de la escena de configuraciones a cargar.")]
        [SerializeField] private string settingsSceneName = "Configuraciones";

        [Header("Botones")]
        [Tooltip("Botón de Créditos; su onClick se suscribe por código en Awake.")]
        [SerializeField] private Button botonCreditos;

        [Header("Paneles Modales")]
        [Tooltip("Panel emergente de Opciones / Configuración.")]
        [SerializeField] private GameObject optionsPanel;

        [Tooltip("Panel emergente de Créditos.")]
        [SerializeField] private GameObject creditsPanel;

        [Header("Audio")]
        [Tooltip("AudioSource para música de fondo.")]
        [SerializeField] private AudioSource bgmAudioSource;

        [Tooltip("Slider de volumen en el panel de opciones (opcional).")]
        [SerializeField] private Slider volumeSlider;

        private void Awake()
        {
            // Suscripción por código: no depende de que el OnClick del Inspector siga enlazado.
            if (botonCreditos != null)
            {
                botonCreditos.onClick.RemoveListener(OpenCredits);
                botonCreditos.onClick.AddListener(OpenCredits);
            }
            else
            {
                Debug.LogWarning("[MainMenuController] 'botonCreditos' no asignado en el Inspector.");
            }
        }

        private void Start()
        {
            // Asegurar que los paneles secundarios inicien ocultos
            if (optionsPanel != null) optionsPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);

            // Inicializar slider con el volumen actual de AudioListener
            if (volumeSlider != null)
            {
                volumeSlider.value = GameSettings.MasterVolume;
                volumeSlider.onValueChanged.AddListener(SetMasterVolume);
            }

            // La música la reproduce MenuMusicPlayer (persistente): así sigue sonando en Configuraciones
            // sin reiniciarse. Este AudioSource de la escena sólo aporta el clip y el volumen de mezcla.
            if (bgmAudioSource != null)
            {
                bgmAudioSource.Stop(); // por si tiene Play On Awake: evita que suene duplicada
                MenuMusicPlayer.Play(bgmAudioSource.clip, bgmAudioSource.volume);
            }
        }

        // ============================================================== //
        // Acciones de Botones                                            //
        // ============================================================== //

        /// <summary>Inicia la misión cargando la escena de juego.</summary>
        public void StartGame()
        {
            if (!string.IsNullOrEmpty(gameplaySceneName))
            {
                MenuMusicPlayer.Stop(0f);
                SceneManager.LoadScene(gameplaySceneName);
            }
            else
            {
                Debug.LogWarning("[MainMenuController] Nombre de escena de juego no configurado.");
            }
        }

        /// <summary>Carga la escena Configuraciones con fundido a negro.</summary>
        public void OpenSettings()
        {
            SceneFader.LoadScene(settingsSceneName);
        }

        /// <summary>Abre el panel de opciones (modal legado; el botón ahora usa <see cref="OpenSettings"/>).</summary>
        public void OpenOptions()
        {
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(true);
        }

        /// <summary>Cierra el panel de opciones.</summary>
        public void CloseOptions()
        {
            if (optionsPanel != null) optionsPanel.SetActive(false);
        }

        /// <summary>Carga la escena Credits con fundido a negro (Credits debe estar en Build Settings).</summary>
        public void OpenCredits()
        {
            SceneFader.LoadScene(creditsSceneName);
        }

        /// <summary>Cierra el panel de créditos.</summary>
        public void CloseCredits()
        {
            if (creditsPanel != null) creditsPanel.SetActive(false);
        }

        /// <summary>Ajusta el volumen general del juego.</summary>
        public void SetMasterVolume(float volume)
        {
            GameSettings.MasterVolume = volume;
        }

        /// <summary>
        /// Cierra la aplicación. Application.Quit() se ignora dentro del editor, así que ahí
        /// se detiene el Play mode para confirmar visualmente que el botón funciona.
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("[MainMenuController] Cerrando juego...");
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
