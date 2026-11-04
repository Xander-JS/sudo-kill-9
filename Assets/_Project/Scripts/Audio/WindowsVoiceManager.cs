using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using SudoKill9.Combat;

namespace SudoKill9.Audio
{
    /// <summary>
    /// Administrador de voz en tiempo real para el villano Windows en Sudo-Kill-9.
    /// Conecta con el microservicio local de FastAPI/edge-tts mediante peticiones POST asíncronas
    /// con un timeout estricto de 2.0 segundos reales, garantizando CERO bloqueo en el combate,
    /// movimiento y animaciones del jefe.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class WindowsVoiceManager : MonoBehaviour
    {
        [Header("Conexión con Microservicio TTS")]
        [Tooltip("URL del endpoint local FastAPI para la síntesis de audio.")]
        [SerializeField] private string serverUrl = "http://127.0.0.1:8000/generate_audio";

        [Tooltip("Habilita o deshabilita la síntesis de voz en tiempo real.")]
        [SerializeField] private bool habilitarVoz = true;

        [Tooltip("Tiempo límite estricto en segundos de reloj real para abortar si el microservicio tarda demasiado.")]
        [SerializeField] private float timeoutSegundos = 2.0f;

        [Header("Parámetros de Voz")]
        [Tooltip("Identificador de voz de edge-tts. Dejar vacío o con 'es-MX-JorgeNeural' para voz oficial.")]
        [SerializeField] private string voiceId = "es-MX-JorgeNeural";

        [Tooltip("Ajuste de velocidad (+5%, +10%, 0%).")]
        [SerializeField] private string speechRate = "+5%";

        [Tooltip("Ajuste de tono (-5Hz, 0Hz, etc.) para darle un toque más robótico y amenazante.")]
        [SerializeField] private string speechPitch = "-5Hz";

        [Header("Audio y Espacialización 3D")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Si es true, un nuevo diálogo interrumpe la reproducción del anterior.")]
        [SerializeField] private bool interrumpirFraseAnterior = true;

        [Tooltip("Muestra trazas detalladas de la síntesis en la consola de Unity.")]
        [SerializeField] private bool depurarEnConsola = false;

        private Coroutine corrutinaPeticion;
        private UnityWebRequest peticionActiva;

        public AudioSource Source => audioSource;
        public bool HabilitarVoz { get => habilitarVoz; set => habilitarVoz = value; }

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            ConfigurarAudioSource3D();
            TTSProcessLauncher.EnsureServerRunning();
        }

        private void OnEnable()
        {
            // Suscripción automática a las frases habladas por el diálogo de Gemini
            WindowsAIGeminiDialogue.OnDialogueSpoken += OnDialogueSpokenHandler;
        }

        private void OnDisable()
        {
            WindowsAIGeminiDialogue.OnDialogueSpoken -= OnDialogueSpokenHandler;
            CancelarPeticionActiva();

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        private void OnDestroy()
        {
            CancelarPeticionActiva();
        }

        /// <summary>
        /// Configura el AudioSource para simulación acústica 3D posicional en el mundo.
        /// </summary>
        public void ConfigurarAudioSource3D()
        {
            if (audioSource == null) return;

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f; // 100% 3D
            audioSource.dopplerLevel = 0.0f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 4.0f;
            audioSource.maxDistance = 40.0f;
            audioSource.spread = 45f;
            audioSource.volume = 1.0f;
        }

        private void OnDialogueSpokenHandler(string frase)
        {
            Speak(frase);
        }

        /// <summary>
        /// Solicita al microservicio sintetizar y reproducir la frase de forma totalmente asíncrona.
        /// Si el microservicio no está disponible o tarda más de 2 segundos, falla silenciosamente.
        /// </summary>
        public void Speak(string texto)
        {
            if (!habilitarVoz || string.IsNullOrWhiteSpace(texto))
            {
                return;
            }

            if (interrumpirFraseAnterior)
            {
                CancelarPeticionActiva();
                if (audioSource != null && audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
            }

            corrutinaPeticion = StartCoroutine(PeticionAudioAsync(texto));
        }

        private IEnumerator PeticionAudioAsync(string texto)
        {
            string payloadJson = ConstruirPayloadJson(texto);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(payloadJson);

            using (UnityWebRequest webRequest = new UnityWebRequest(serverUrl, "POST"))
            {
                peticionActiva = webRequest;
                webRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
                webRequest.downloadHandler = new DownloadHandlerAudioClip(serverUrl, AudioType.MPEG);
                webRequest.SetRequestHeader("Content-Type", "application/json");

                UnityWebRequestAsyncOperation asyncOp = webRequest.SendWebRequest();

                // Control estricto de timeout en tiempo real absoluto (Time.realtimeSinceStartup)
                // para garantizar que nunca se congele el combate incluso con Time.timeScale variado
                float tiempoInicio = Time.realtimeSinceStartup;
                while (!asyncOp.isDone)
                {
                    if (Time.realtimeSinceStartup - tiempoInicio > timeoutSegundos)
                    {
                        if (depurarEnConsola)
                        {
                            Debug.LogWarning($"[WindowsVoiceManager] Timeout estricto de {timeoutSegundos}s excedido. Abortando petición de voz para mantener fluidez del juego.");
                        }
                        webRequest.Abort();
                        peticionActiva = null;
                        corrutinaPeticion = null;
                        yield break;
                    }
                    yield return null;
                }

                peticionActiva = null;

                // Verificación de ciclo de vida tras el await
                if (this == null || gameObject == null || !isActiveAndEnabled)
                {
                    corrutinaPeticion = null;
                    yield break;
                }

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(webRequest);
                    if (clip != null && audioSource != null)
                    {
                        audioSource.clip = clip;
                        audioSource.Play();
                        if (depurarEnConsola)
                        {
                            Debug.Log($"<color=green>[WindowsVoiceManager]</color> Reproduciendo voz sintética: \"{texto}\" ({clip.length:F2}s)");
                        }
                    }
                }
                else
                {
                    if (depurarEnConsola && webRequest.result != UnityWebRequest.Result.ConnectionError)
                    {
                        Debug.LogWarning($"[WindowsVoiceManager] No se pudo reproducir audio ({webRequest.error}). Fallback silencioso.");
                    }
                }
            }

            corrutinaPeticion = null;
        }

        private void CancelarPeticionActiva()
        {
            if (corrutinaPeticion != null)
            {
                StopCoroutine(corrutinaPeticion);
                corrutinaPeticion = null;
            }

            if (peticionActiva != null)
            {
                try
                {
                    peticionActiva.Abort();
                    peticionActiva.Dispose();
                }
                catch (Exception)
                {
                    // Ignorado en desconexión
                }
                peticionActiva = null;
            }
        }

        private string ConstruirPayloadJson(string texto)
        {
            string escapeado = EscapeJson(texto);
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"text\":\"{escapeado}\"");

            if (!string.IsNullOrEmpty(voiceId))
            {
                sb.Append($",\"voice\":\"{EscapeJson(voiceId)}\"");
            }
            if (!string.IsNullOrEmpty(speechRate))
            {
                sb.Append($",\"rate\":\"{EscapeJson(speechRate)}\"");
            }
            if (!string.IsNullOrEmpty(speechPitch))
            {
                sb.Append($",\"pitch\":\"{EscapeJson(speechPitch)}\"");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\n", " ")
                      .Replace("\r", " ")
                      .Replace("\t", " ");
        }
    }
}
