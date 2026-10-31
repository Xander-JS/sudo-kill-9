using UnityEngine;
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

        private void Start()
        {
            // Asegurar que los paneles secundarios inicien ocultos
            if (optionsPanel != null) optionsPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);

            // Inicializar slider con el volumen actual de AudioListener
            if (volumeSlider != null)
            {
                volumeSlider.value = AudioListener.volume;
                volumeSlider.onValueChanged.AddListener(SetMasterVolume);
            }

            // Reproducir música si está asignada
            if (bgmAudioSource != null && !bgmAudioSource.isPlaying)
            {
                bgmAudioSource.loop = true;
                bgmAudioSource.Play();
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
                SceneManager.LoadScene(gameplaySceneName);
            }
            else
            {
                Debug.LogWarning("[MainMenuController] Nombre de escena de juego no configurado.");
            }
        }

        /// <summary>Abre el panel de opciones.</summary>
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

        /// <summary>Abre el panel de créditos.</summary>
        public void OpenCredits()
        {
            if (optionsPanel != null) optionsPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(true);
        }

        /// <summary>Cierra el panel de créditos.</summary>
        public void CloseCredits()
        {
            if (creditsPanel != null) creditsPanel.SetActive(false);
        }

        /// <summary>Ajusta el volumen general del juego.</summary>
        public void SetMasterVolume(float volume)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
        }

        /// <summary>Cierra la aplicación o detiene el Play mode en editor.</summary>
        public void QuitGame()
        {
            Debug.Log("[MainMenuController] Cerrando juego...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
