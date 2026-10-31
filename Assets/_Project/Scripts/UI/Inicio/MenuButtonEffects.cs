using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace SudoKill9.UI.Inicio
{
    /// <summary>
    /// Efecto interactivo táctico para botones de la interfaz:
    /// - Escala suave al pasar el ratón (Hover)
    /// - Cambio de color / brillo
    /// - Desplazamiento del texto interno estilo terminal (sin romper VerticalLayoutGroup)
    /// - Sonido opcional de hover y click
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MenuButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Efecto de Escala")]
        [SerializeField] private float hoverScale = 1.04f;
        [SerializeField] private float transitionSpeed = 14f;

        [Header("Desplazamiento del Texto en Hover")]
        [Tooltip("Píxeles que se desplaza el texto hacia la derecha en hover.")]
        [SerializeField] private float textHoverOffsetX = 14f;

        [Header("Colores de Fondo")]
        [SerializeField] private Color normalColor = new Color(0.10f, 0.15f, 0.22f, 0.88f);
        [SerializeField] private Color hoverColor = new Color(0.15f, 0.35f, 0.55f, 0.98f);

        [Header("Colores del Texto")]
        [SerializeField] private Color normalTextColor = new Color(0.9f, 0.95f, 1f);
        [SerializeField] private Color hoverTextColor = new Color(0.2f, 1f, 1f); // Cian brillante

        [Header("Audio (Opcional)")]
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private AudioClip clickSound;
        [SerializeField] private AudioSource audioSource;

        private RectTransform rectTransform;
        private Image buttonImage;
        private TMP_Text buttonText;
        private RectTransform textRectTransform;

        private Vector3 defaultScale;
        private Vector2 defaultTextAnchoredPos;
        private Vector3 targetScale;
        private Vector2 targetTextAnchoredPos;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            buttonImage = GetComponent<Image>();
            buttonText = GetComponentInChildren<TMP_Text>();

            defaultScale = Vector3.one;
            targetScale = defaultScale;

            if (buttonText != null)
            {
                textRectTransform = buttonText.rectTransform;
                defaultTextAnchoredPos = textRectTransform.anchoredPosition;
                targetTextAnchoredPos = defaultTextAnchoredPos;
                buttonText.color = normalTextColor;
            }

            if (buttonImage != null)
            {
                buttonImage.color = normalColor;
            }

            if (audioSource == null)
            {
                audioSource = GetComponentInParent<AudioSource>();
            }
        }

        private void Update()
        {
            // Escala suave del botón
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);

            // Desplazamiento suave del texto interno (100% compatible con Layout Groups)
            if (textRectTransform != null)
            {
                textRectTransform.anchoredPosition = Vector2.Lerp(textRectTransform.anchoredPosition, targetTextAnchoredPos, Time.unscaledDeltaTime * transitionSpeed);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            targetScale = defaultScale * hoverScale;
            if (textRectTransform != null)
            {
                targetTextAnchoredPos = defaultTextAnchoredPos + new Vector2(textHoverOffsetX, 0f);
            }

            if (buttonImage != null) buttonImage.color = hoverColor;
            if (buttonText != null) buttonText.color = hoverTextColor;

            PlaySound(hoverSound);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = defaultScale;
            if (textRectTransform != null)
            {
                targetTextAnchoredPos = defaultTextAnchoredPos;
            }

            if (buttonImage != null) buttonImage.color = normalColor;
            if (buttonText != null) buttonText.color = normalTextColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            PlaySound(clickSound);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip, 0.7f);
            }
        }

        private void OnDisable()
        {
            if (rectTransform != null) rectTransform.localScale = defaultScale;
            if (textRectTransform != null) textRectTransform.anchoredPosition = defaultTextAnchoredPos;
        }
    }
}
