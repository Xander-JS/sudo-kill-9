using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SudoKill9.UI
{
    /// <summary>
    /// Transición limpia entre escenas: fundido a negro, carga asíncrona y fundido de vuelta.
    /// Se crea a sí mismo bajo demanda y sobrevive a la carga (DontDestroyOnLoad); no necesita
    /// estar en ninguna escena. Uso: <c>SceneFader.LoadScene("Configuraciones");</c>
    /// </summary>
    public class SceneFader : MonoBehaviour
    {
        private static bool busy;

        private CanvasGroup group;

        public static void LoadScene(string sceneName, float fadeDuration = 0.45f)
        {
            if (busy) return; // ignora dobles clics durante la transición
            busy = true;

            var go = new GameObject("SceneFader", typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var img = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(go.transform, false);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            img.GetComponent<Image>().color = Color.black;

            var fader = go.AddComponent<SceneFader>();
            fader.group = go.GetComponent<CanvasGroup>();
            fader.group.alpha = 0f;
            fader.group.blocksRaycasts = true; // bloquea la UI de debajo mientras dura el fundido
            fader.StartCoroutine(fader.Run(sceneName, fadeDuration));
        }

        private IEnumerator Run(string sceneName, float duration)
        {
            yield return FadeTo(1f, duration);
            yield return SceneManager.LoadSceneAsync(sceneName);
            yield return FadeTo(0f, duration);
            busy = false;
            Destroy(gameObject);
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            float from = group.alpha;
            for (float t = 0f; t < 1f;)
            {
                // Delta limitado: el tirón de la carga no debe comerse el fundido. Unscaled: funciona en pausa.
                t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f) / duration;
                group.alpha = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }

        // Si se sale del Play mode a mitad de un fundido, el flag estático no debe quedar atascado.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => busy = false;
    }
}
