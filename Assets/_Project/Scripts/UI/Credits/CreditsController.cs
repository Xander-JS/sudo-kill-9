using System.Collections;
using SudoKill9.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SudoKill9.UI.Credits
{
    /// <summary>
    /// Scroll cinemático de créditos: el texto entra desde abajo, sube durante <see cref="scrollDuration"/>
    /// segundos y se detiene cuando la última línea llega al centro. Espacio/Escape vuelve a Inicio
    /// con fundido a negro que sobrevive a la carga y se desvanece ya en Inicio.
    /// Vive en el canvas del fundido (raíz) para poder persistir con DontDestroyOnLoad.
    /// </summary>
    public class CreditsController : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private TMP_Text lastLine;
        [SerializeField] private CanvasGroup fader;
        [SerializeField] private string menuScene = "Inicio";
        [SerializeField] private float scrollDuration = 15f;
        [SerializeField] private float fadeDuration = 0.6f;

        [Header("Música")]
        [Tooltip("Pista de fondo de los créditos (distinta de la del menú). Arrastra aquí el AudioClip.")]
        [SerializeField] private AudioClip creditsMusic;
        [Tooltip("Opcional: si se deja vacío se crea uno en este objeto al iniciar.")]
        [SerializeField] private AudioSource musicSource;
        [Tooltip("Volumen de mezcla base; se multiplica por el slider Música de Configuraciones.")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.65f;

        private bool leaving;
        private Coroutine fadeRoutine;
        private AudioVolumeChannel musicChannel;

        private IEnumerator Start()
        {
            fader.alpha = 1f;
            fadeRoutine = StartCoroutine(FadeTo(0f));
            PlayMusic();

            float startY = -((RectTransform)content.parent).rect.height * 0.5f; // borde superior justo bajo la pantalla
            content.anchoredPosition = new Vector2(0f, startY);

            // Los textos llegan asíncronos desde la String Table: medir el layout solo cuando ya están.
            // shortcut: si la tabla nunca carga, no hay scroll (Espacio/Esc siguen funcionando); añadir timeout si ocurre.
            yield return new WaitUntil(() => !string.IsNullOrEmpty(lastLine.text));
            yield return null;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            // Pivote del contenido arriba-centro: la posición local Y de la última línea es su distancia al borde superior.
            float endY = -lastLine.rectTransform.localPosition.y;

            for (float t = 0f; t < scrollDuration && !leaving; t += Time.unscaledDeltaTime)
            {
                content.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, t / scrollDuration));
                yield return null;
            }
            if (!leaving) content.anchoredPosition = new Vector2(0f, endY);
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (leaving || kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
                StartCoroutine(ExitToMenu());
        }

        private IEnumerator ExitToMenu()
        {
            leaving = true;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            DontDestroyOnLoad(gameObject);

            // La música se apaga junto con el fundido a negro: no debe colarse en Inicio
            // (este objeto sobrevive a la carga por DontDestroyOnLoad).
            StartCoroutine(FadeOutMusic());
            yield return FadeTo(1f);
            if (musicSource != null) musicSource.Stop();
            yield return SceneManager.LoadSceneAsync(menuScene);
            yield return FadeTo(0f);
            Destroy(gameObject);
        }

        private void PlayMusic()
        {
            if (creditsMusic == null) return; // slot aún vacío: créditos sin música

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.spatialBlend = 0f;
                musicSource.volume = musicVolume; // antes de añadir el canal: AudioVolumeChannel lo toma como base
            }

            musicSource.clip = creditsMusic;
            musicSource.loop = true;
            musicSource.playOnAwake = false;

            // Respeta el slider "Música" de Configuraciones.
            musicChannel = musicSource.GetComponent<AudioVolumeChannel>();
            if (musicChannel == null) musicChannel = musicSource.gameObject.AddComponent<AudioVolumeChannel>();
            musicChannel.Channel = AudioChannel.Music;

            musicSource.Play();
        }

        private IEnumerator FadeOutMusic()
        {
            if (musicSource == null || !musicSource.isPlaying) yield break;
            if (musicChannel != null) musicChannel.enabled = false; // que el canal no pise el fundido

            float from = musicSource.volume;
            for (float t = 0f; t < 1f && musicSource != null; t += Time.unscaledDeltaTime / fadeDuration)
            {
                musicSource.volume = Mathf.Lerp(from, 0f, t);
                yield return null;
            }
        }

        private IEnumerator FadeTo(float target)
        {
            float from = fader.alpha;
            for (float t = 0f; t < 1f;)
            {
                // Limitar el delta evita que el tirón de carga de escena se coma el fundido.
                t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f) / fadeDuration;
                fader.alpha = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }
    }
}
