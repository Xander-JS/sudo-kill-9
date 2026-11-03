using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SudoKill9.UI
{
    /// <summary>
    /// Efecto cinemático de Pantalla Azul de la Muerte (BSOD) clásica de Windows:
    /// - Se activa automáticamente al recibir el evento OnBSODTriggered (o vía TriggerBSOD()).
    /// - Muestra la pantalla azul con el clásico emoticono ':(', porcentaje y códigos de detención.
    /// - Aplica distorsión, temblor (screen shake) y parpadeo durante 1.5 segundos.
    /// - Transición de desvanecimiento (Fade-out suave) al completarse.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class BSODGlitchEffect : MonoBehaviour
    {
        public static BSODGlitchEffect Instance { get; private set; }

        [Header("Configuración Visual")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fondoAzul;
        [SerializeField] private TMP_Text textoCaraTriste;
        [SerializeField] private TMP_Text textoMensajePrincipal;
        [SerializeField] private TMP_Text textoCodigoDetencion;

        [Header("Tiempos del Efecto")]
        [Tooltip("Duración activa de la pantalla azul antes del fade-out.")]
        [SerializeField] private float duracionGlitch = 1.5f;

        [Tooltip("Duración del desvanecimiento suave de salida.")]
        [SerializeField] private float duracionFadeOut = 0.35f;

        [Tooltip("Intensidad del temblor/sacudida en píxeles.")]
        [SerializeField] private float intensidadShake = 16f;

        [Header("Audio Opcional")]
        [SerializeField] private AudioClip sonidoGlitch;
        [SerializeField] private AudioSource audioSource;

        private RectTransform rectTransform;
        private Vector2 posicionOriginal;
        private Coroutine corrutinaActiva;
        private Color colorAzulOriginal = new Color(0f, 0.47f, 0.84f, 1f); // #0078D7 Windows Blue

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

            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null) posicionOriginal = rectTransform.anchoredPosition;

            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (fondoAzul != null) colorAzulOriginal = fondoAzul.color;

            ConfigurarTextosPorDefecto();
        }

        private void OnEnable()
        {
            WindowsCommandAttacks.OnBSODTriggered -= HandleBSODTriggered;
            WindowsCommandAttacks.OnBSODTriggered += HandleBSODTriggered;
        }

        private void OnDisable()
        {
            WindowsCommandAttacks.OnBSODTriggered -= HandleBSODTriggered;
        }

        private void HandleBSODTriggered()
        {
            ActivarBSOD();
        }

        public static void Trigger()
        {
            if (Instance != null)
            {
                Instance.ActivarBSOD();
            }
        }

        public void ActivarBSOD()
        {
            if (corrutinaActiva != null) StopCoroutine(corrutinaActiva);
            corrutinaActiva = StartCoroutine(RutinaBSOD());
        }

        private IEnumerator RutinaBSOD()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = false;
            }

            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(100f, 100f);
            }

            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource != null && sonidoGlitch != null)
            {
                audioSource.PlayOneShot(sonidoGlitch);
            }

            float tiempoRestante = duracionGlitch;

            while (tiempoRestante > 0f)
            {
                tiempoRestante -= Time.unscaledDeltaTime;

                // 1. Efecto Shake / Glitch de posición
                if (rectTransform != null)
                {
                    float offsetX = Random.Range(-intensidadShake, intensidadShake);
                    float offsetY = Random.Range(-intensidadShake * 0.5f, intensidadShake * 0.5f);
                    rectTransform.anchoredPosition = posicionOriginal + new Vector2(offsetX, offsetY);
                }

                // 2. Parpadeo sutil de color azul
                if (fondoAzul != null)
                {
                    float tint = Random.Range(0.85f, 1.15f);
                    fondoAzul.color = new Color(
                        colorAzulOriginal.r * tint,
                        colorAzulOriginal.g * tint,
                        colorAzulOriginal.b * tint,
                        1f
                    );
                }

                yield return null;
            }

            // Restaurar posición, tamaño y color
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = posicionOriginal;
                rectTransform.sizeDelta = Vector2.zero;
            }
            if (fondoAzul != null) fondoAzul.color = colorAzulOriginal;

            // 3. Fade out suave
            float transcurridoFade = 0f;
            while (transcurridoFade < duracionFadeOut)
            {
                transcurridoFade += Time.unscaledDeltaTime;
                float t = transcurridoFade / duracionFadeOut;
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
                }
                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
            }

            corrutinaActiva = null;
        }

        private void ConfigurarTextosPorDefecto()
        {
            if (textoCaraTriste != null && string.IsNullOrEmpty(textoCaraTriste.text))
            {
                textoCaraTriste.text = ":(";
            }

            if (textoMensajePrincipal != null && string.IsNullOrEmpty(textoMensajePrincipal.text))
            {
                textoMensajePrincipal.text =
                    "Tu PC tuvo un problema y necesita reiniciarse.\n" +
                    "Estamos recopilando información sobre el error y luego se reiniciará automáticamente.\n\n" +
                    "100% completado";
            }

            if (textoCodigoDetencion != null && string.IsNullOrEmpty(textoCodigoDetencion.text))
            {
                textoCodigoDetencion.text =
                    "Código de detención: CRITICAL_PROCESS_DIED\n" +
                    "Lo que falló: win32kfull.sys (Windows Defender Rootkit)";
            }
        }

        internal void ConfigurarReferencias(CanvasGroup cg, Image fondo, TMP_Text cara, TMP_Text msg, TMP_Text codigo)
        {
            canvasGroup = cg;
            fondoAzul = fondo;
            textoCaraTriste = cara;
            textoMensajePrincipal = msg;
            textoCodigoDetencion = codigo;
            if (fondoAzul != null) colorAzulOriginal = fondoAzul.color;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }
    }
}
