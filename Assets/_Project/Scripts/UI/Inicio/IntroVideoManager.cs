using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEngine.Localization.Settings;
using SudoKill9.Settings;

namespace SudoKill9.UI.Inicio
{
    /// <summary>
    /// Reproduce el video de introducción desde el menú existente de la escena de Inicio.
    ///
    /// No muestra ningún selector de idioma en la pantalla principal: el idioma se define en
    /// la pantalla de Configuración (dropdown "Idioma" → <see cref="GameSettings.LocaleCode"/>,
    /// persistido en PlayerPrefs). Al pulsar "[ > INICIAR MISIÓN ]" se oculta por completo
    /// <c>Menu_Canvas</c>, se activa el overlay de video a pantalla completa y se reproduce el
    /// clip del idioma configurado. Al terminar el video (<see cref="VideoPlayer.loopPointReached"/>)
    /// o al saltarlo (Espacio / Escape / botón Saltar) se carga la escena de gameplay objetivo.
    /// </summary>
    public class IntroVideoManager : MonoBehaviour
    {
        [Header("Menú existente")]
        [Tooltip("Menu_Canvas: se desactiva por completo durante la reproducción.")]
        [SerializeField] private GameObject menuCanvas;

        [Header("Overlay de video (inicia desactivado)")]
        [SerializeField] private GameObject videoOverlay;
        [SerializeField] private RawImage videoImage;
        [SerializeField] private AspectRatioFitter aspectFitter;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private Button skipButton;

        [Header("Clips de video")]
        [Tooltip("Clip en inglés (video_final_en).")]
        [SerializeField] private VideoClip englishClip;
        [Tooltip("Clip en español (video_final_es / video_final_es1).")]
        [SerializeField] private VideoClip spanishClip;

        [Header("Idioma por defecto")]
        [Tooltip("Código usado si Configuración aún no definió idioma: \"es\" o \"en\".")]
        [SerializeField] private string defaultLocaleCode = "es";

        [Header("Transición")]
        [Tooltip("Nombre de la escena de gameplay que se carga al finalizar el video.")]
        [SerializeField] private string targetSceneName = "Mapa_completo";

        private RenderTexture _renderTexture;
        private bool _started;
        private bool _transitioning;

        // ── Ciclo de vida ────────────────────────────────────────────────────

        private void Awake()
        {
            // El botón "Iniciar Misión" se enlaza de forma persistente desde el editor.
            // Aquí sólo se enlaza el botón Saltar propio del overlay.
            if (skipButton != null)
            {
                skipButton.onClick.RemoveListener(Skip);
                skipButton.onClick.AddListener(Skip);
            }
        }

        private void Start()
        {
            if (videoOverlay != null) videoOverlay.SetActive(false);
            if (menuCanvas != null && !menuCanvas.activeSelf) menuCanvas.SetActive(true);
        }

        private void Update()
        {
            if (_started && !_transitioning &&
                (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space)))
            {
                Skip();
            }
        }

        private void OnDestroy()
        {
            if (skipButton != null) skipButton.onClick.RemoveListener(Skip);
            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached -= OnVideoFinished;
                videoPlayer.prepareCompleted -= OnVideoPrepared;
            }
            ReleaseRenderTexture();
        }

        // ── Idioma (leído de Configuración) ──────────────────────────────────

        /// <summary>
        /// Devuelve true si el idioma configurado es inglés. Lee <see cref="GameSettings.LocaleCode"/>
        /// (dropdown de Configuración); si nunca se eligió, usa el locale activo de Localization y,
        /// como último recurso, <see cref="defaultLocaleCode"/>.
        /// </summary>
        private bool IsEnglishSelected()
        {
            string code = GameSettings.LocaleCode;

            if (string.IsNullOrEmpty(code))
            {
                try
                {
                    var selected = LocalizationSettings.SelectedLocale;
                    if (selected != null) code = selected.Identifier.Code;
                }
                catch { /* Localization aún no inicializado: se usa el valor por defecto. */ }
            }

            if (string.IsNullOrEmpty(code)) code = defaultLocaleCode;
            return code.Trim().ToLowerInvariant().StartsWith("en");
        }

        // ── Inicio de la misión ──────────────────────────────────────────────

        /// <summary>Oculta el menú, activa el overlay y reproduce el clip del idioma configurado.</summary>
        public void StartMission()
        {
            if (_started) return;

            bool english = IsEnglishSelected();
            VideoClip clip = english
                ? (englishClip != null ? englishClip : spanishClip)
                : (spanishClip != null ? spanishClip : englishClip);

            if (clip == null || videoPlayer == null || videoImage == null || videoOverlay == null)
            {
                Debug.LogError("[IntroVideoManager] Falta un clip o el overlay de video. " +
                               "Cargando la escena objetivo directamente.");
                LoadTargetScene();
                return;
            }

            _started = true;
            MenuMusicPlayer.Stop(0.5f); // sólo debe oírse el audio del video; no se reanuda al cargar el gameplay
            Debug.Log($"[IntroVideoManager] Idioma={(english ? "EN" : "ES")} → clip '{clip.name}'.");

            if (menuCanvas != null) menuCanvas.SetActive(false); // evita solapamiento / doble input
            videoOverlay.SetActive(true);

            EnsureRenderTexture();

            videoPlayer.playOnAwake       = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.isLooping         = false;
            videoPlayer.renderMode        = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture     = _renderTexture;
            videoPlayer.source            = VideoSource.VideoClip;
            videoPlayer.clip              = clip;

            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.loopPointReached += OnVideoFinished;

            videoPlayer.Prepare();
        }

        private void OnVideoPrepared(VideoPlayer vp)
        {
            if (aspectFitter != null && vp.width > 0 && vp.height > 0)
            {
                aspectFitter.aspectMode  = AspectRatioFitter.AspectMode.FitInParent;
                aspectFitter.aspectRatio = (float)vp.width / vp.height;
            }
            vp.Play();
        }

        private void OnVideoFinished(VideoPlayer vp) => LoadTargetScene();

        /// <summary>Salta el video y pasa directamente a la escena objetivo.</summary>
        public void Skip()
        {
            if (!_started) return;
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
            LoadTargetScene();
        }

        // ── Transición ───────────────────────────────────────────────────────

        private void LoadTargetScene()
        {
            if (_transitioning) return;
            _transitioning = true;

            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached -= OnVideoFinished;
                videoPlayer.prepareCompleted -= OnVideoPrepared;
            }

            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogError("[IntroVideoManager] 'targetSceneName' vacío.");
                return;
            }

            if (Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                SceneManager.LoadScene(targetSceneName);
            }
            else
            {
                Debug.LogError($"[IntroVideoManager] La escena '{targetSceneName}' no está en Build Settings. " +
                               "Ejecuta 'Tools/Setup Intro Video Flow'.");
            }
        }

        // ── RenderTexture ────────────────────────────────────────────────────

        private void EnsureRenderTexture()
        {
            int w = Mathf.Max(640, Screen.width);
            int h = Mathf.Max(360, Screen.height);

            if (_renderTexture != null && (_renderTexture.width != w || _renderTexture.height != h))
                ReleaseRenderTexture();

            if (_renderTexture == null)
            {
                _renderTexture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "IntroVideo_RT" };
                _renderTexture.Create();
            }

            if (videoImage != null) videoImage.texture = _renderTexture;
        }

        private void ReleaseRenderTexture()
        {
            if (_renderTexture == null) return;
            if (videoPlayer != null && videoPlayer.targetTexture == _renderTexture) videoPlayer.targetTexture = null;
            if (videoImage != null && videoImage.texture == _renderTexture) videoImage.texture = null;
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
    }
}
