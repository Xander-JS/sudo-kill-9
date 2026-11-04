using UnityEngine;
using UnityEngine.Video;

namespace SudoKill9.Environment
{
    /// <summary>
    /// Controlador para la pantalla gigante del mapa que reproduce el video de Bill Gates (Bill.mp4).
    /// Configura automáticamente el VideoPlayer, RenderTexture, material emissivo/unlit y AudioSource
    /// en la arena de combate para asegurar reproducción continua en bucle y audio sincronizado.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(VideoPlayer))]
    [RequireComponent(typeof(AudioSource))]
    public class ArenaVideoDisplay : MonoBehaviour
    {
        [Header("1. Configuración de Video")]
        [Tooltip("Clip de video a reproducir (Bill.mp4).")]
        [SerializeField] private VideoClip videoClip;

        [Tooltip("RenderTexture de destino donde se dibujan los fotogramas del video.")]
        [SerializeField] private RenderTexture renderTexture;

        [Tooltip("Renderer de la pantalla frontal.")]
        [SerializeField] private Renderer screenRenderer;

        [Tooltip("Renderer de la pantalla posterior (opcional, para visualización bilateral).")]
        [SerializeField] private Renderer backRenderer;

        [Header("2. Controles de Reproducción")]
        [Tooltip("Inicia la reproducción automáticamente al cargar la escena.")]
        [SerializeField] private bool autoPlay = true;

        [Tooltip("Reproduce el video en bucle continuo.")]
        [SerializeField] private bool loop = true;

        [Tooltip("Volumen general del audio del video (0.0 a 1.0).")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        [Header("3. Componentes Internos")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private AudioSource audioSource;

        public VideoPlayer VideoPlayer => videoPlayer != null ? videoPlayer : (videoPlayer = GetComponent<VideoPlayer>());
        public AudioSource AudioSource => audioSource != null ? audioSource : (audioSource = GetComponent<AudioSource>());

        private void Awake()
        {
            SetupComponents();
        }

        private void Start()
        {
            SetupComponents();

            if (Application.isPlaying && autoPlay)
            {
                PlayVideo();
            }
        }

        private void OnEnable()
        {
            SetupComponents();
            if (Application.isPlaying && autoPlay && videoPlayer != null && !videoPlayer.isPlaying)
            {
                PlayVideo();
            }
        }

        private void OnValidate()
        {
            SetupComponents();
        }

        /// <summary>
        /// Configura y vincula todos los componentes requeridos para el video y audio.
        /// </summary>
        public void SetupComponents()
        {
            if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            if (videoPlayer != null)
            {
                videoPlayer.playOnAwake = autoPlay;
                videoPlayer.isLooping = loop;
                videoPlayer.skipOnDrop = true;
                videoPlayer.waitForFirstFrame = true;

                if (videoClip != null)
                {
                    videoPlayer.source = VideoSource.VideoClip;
                    videoPlayer.clip = videoClip;
                }

                if (renderTexture != null)
                {
                    videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                    videoPlayer.targetTexture = renderTexture;
                    videoPlayer.aspectRatio = VideoAspectRatio.FitHorizontally;

                    AsignarTexturaEnRenderer(screenRenderer);
                    AsignarTexturaEnRenderer(backRenderer);
                }

                // Configuración de audio enrutado a AudioSource 3D
                if (audioSource != null)
                {
                    audioSource.playOnAwake = false;
                    audioSource.loop = loop;
                    audioSource.volume = volume;

                    videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                    videoPlayer.controlledAudioTrackCount = 1;
                    videoPlayer.EnableAudioTrack(0, true);
                    videoPlayer.SetTargetAudioSource(0, audioSource);
                }
            }
        }

        private void AsignarTexturaEnRenderer(Renderer r)
        {
            if (r == null || renderTexture == null) return;

            Material mat = Application.isPlaying ? r.material : r.sharedMaterial;
            if (mat != null)
            {
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", renderTexture);
                }
                else if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", renderTexture);
                }
            }
        }

        [ContextMenu("Reproducir Video")]
        public void PlayVideo()
        {
            if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
            if (videoPlayer != null)
            {
                videoPlayer.Play();
            }
        }

        [ContextMenu("Pausar Video")]
        public void PauseVideo()
        {
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
            }
        }

        [ContextMenu("Detener Video")]
        public void StopVideo()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
            }
            if (audioSource != null)
            {
                audioSource.Stop();
            }
        }

        [ContextMenu("Reiniciar Video")]
        public void RestartVideo()
        {
            StopVideo();
            PlayVideo();
        }
    }
}
