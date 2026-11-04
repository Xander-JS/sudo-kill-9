using System.Collections;
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

        private bool leaving;
        private Coroutine fadeRoutine;

        private IEnumerator Start()
        {
            fader.alpha = 1f;
            fadeRoutine = StartCoroutine(FadeTo(0f));

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

            yield return FadeTo(1f);
            yield return SceneManager.LoadSceneAsync(menuScene);
            yield return FadeTo(0f);
            Destroy(gameObject);
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
