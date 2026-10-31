using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace SudoKill9.UI.Inicio
{
    /// <summary>
    /// Controla la tarjeta (Card) de Términos de Servicio / Protocolo de Acceso Root
    /// que cubre toda la pantalla al iniciar la escena Inicio.
    /// El jugador puede aceptar los términos para revelar el menú principal con una transición suave.
    /// </summary>
    public class TermsOfServiceCard : MonoBehaviour
    {
        [Header("Referencias UI")]
        [Tooltip("CanvasGroup del overlay completo para animar el fade-out.")]
        [SerializeField] private CanvasGroup overlayCanvasGroup;

        [Tooltip("Toggle opcional de confirmación de lectura.")]
        [SerializeField] private Toggle confirmToggle;

        [Tooltip("Botón para aceptar los términos y entrar al menú de inicio.")]
        [SerializeField] private Button acceptButton;

        [Tooltip("Botón opcional para rechazar y salir del sistema.")]
        [SerializeField] private Button declineButton;

        [Header("Transición")]
        [SerializeField] private float fadeDuration = 0.35f;

        private bool isClosing;

        private void Awake()
        {
            if (overlayCanvasGroup == null)
            {
                overlayCanvasGroup = GetComponent<CanvasGroup>();
            }

            // Asegurar que inicie visible cubriendo toda la pantalla
            if (overlayCanvasGroup != null)
            {
                overlayCanvasGroup.alpha = 1f;
                overlayCanvasGroup.interactable = true;
                overlayCanvasGroup.blocksRaycasts = true;
            }

            if (acceptButton != null)
            {
                acceptButton.onClick.RemoveListener(AcceptTerms);
                acceptButton.onClick.AddListener(AcceptTerms);
            }

            if (declineButton != null)
            {
                declineButton.onClick.RemoveListener(DeclineTerms);
                declineButton.onClick.AddListener(DeclineTerms);
            }

            if (confirmToggle != null && acceptButton != null)
            {
                acceptButton.interactable = confirmToggle.isOn;
                confirmToggle.onValueChanged.RemoveListener(OnToggleChanged);
                confirmToggle.onValueChanged.AddListener(OnToggleChanged);
            }
        }

        private void OnToggleChanged(bool isChecked)
        {
            if (acceptButton != null)
            {
                acceptButton.interactable = isChecked;
            }
        }

        /// <summary>
        /// Acepta los términos de servicio y oculta la pantalla para mostrar el menú de inicio.
        /// </summary>
        public void AcceptTerms()
        {
            if (isClosing) return;
            isClosing = true;

            if (gameObject.activeInHierarchy && overlayCanvasGroup != null)
            {
                StartCoroutine(FadeOutAndDisable());
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Rechaza los términos y cierra la aplicación o detiene Play Mode.
        /// </summary>
        public void DeclineTerms()
        {
            Debug.Log("[TermsOfServiceCard] Términos rechazados. Saliendo del sistema...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator FadeOutAndDisable()
        {
            overlayCanvasGroup.interactable = false;
            overlayCanvasGroup.blocksRaycasts = false;

            float elapsed = 0f;
            float startAlpha = overlayCanvasGroup.alpha;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                overlayCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
                yield return null;
            }

            overlayCanvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }
    }
}
