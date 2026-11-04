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
        /// <summary>Clave de PlayerPrefs que marca que el jugador ya aceptó los términos (1 = aceptado).</summary>
        public const string TermsAcceptedKey = "TermsAccepted";

        /// <summary>True si los términos ya se aceptaron en esta u otra sesión.</summary>
        public static bool HasAcceptedTerms => PlayerPrefs.GetInt(TermsAcceptedKey, 0) == 1;

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
            // Ya aceptados (esta sesión o una anterior): no volver a mostrar al recargar Inicio.
            if (HasAcceptedTerms)
            {
                gameObject.SetActive(false);
                return;
            }

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

            // Persistir de inmediato, antes del fade, para que un cambio de escena no la pierda.
            PlayerPrefs.SetInt(TermsAcceptedKey, 1);
            PlayerPrefs.Save();

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
