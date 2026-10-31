using UnityEngine;
using UnityEngine.InputSystem;

namespace SudoKill9.UI.Inicio
{
    /// <summary>
    /// Efecto sutil de paralaje cinemático para la cámara de la pantalla de inicio.
    /// Reacciona suavemente a la posición del cursor del ratón en la pantalla sin desestabilizar la toma.
    /// Compatible con el nuevo Input System.
    /// </summary>
    public class MenuParallaxCamera : MonoBehaviour
    {
        [Header("Intensidad")]
        [Tooltip("Desplazamiento horizontal máximo de la cámara.")]
        [SerializeField] private float maxOffsetX = 0.25f;

        [Tooltip("Desplazamiento vertical máximo de la cámara.")]
        [SerializeField] private float maxOffsetY = 0.15f;

        [Header("Rotación Sutil")]
        [Tooltip("Inclinación máxima en Euler Y.")]
        [SerializeField] private float maxAngleY = 1.5f;

        [Tooltip("Inclinación máxima en Euler X.")]
        [SerializeField] private float maxAngleX = 1.0f;

        [Header("Suavizado")]
        [SerializeField] private float smoothTime = 3.0f;

        private Vector3 initialPosition;
        private Quaternion initialRotation;

        private void Start()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        private void Update()
        {
            Vector2 mouseNorm = GetNormalizedMousePosition();

            // Calcular posición objetivo
            Vector3 targetPos = initialPosition + new Vector3(mouseNorm.x * maxOffsetX, mouseNorm.y * maxOffsetY, 0f);
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.unscaledDeltaTime * smoothTime);

            // Calcular rotación objetivo
            Quaternion targetRot = initialRotation * Quaternion.Euler(-mouseNorm.y * maxAngleX, mouseNorm.x * maxAngleY, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.unscaledDeltaTime * smoothTime);
        }

        private Vector2 GetNormalizedMousePosition()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return Vector2.zero;

            Vector2 mousePos = mouse.position.ReadValue();
            float normX = (mousePos.x / Screen.width) * 2f - 1f;  // [-1, 1]
            float normY = (mousePos.y / Screen.height) * 2f - 1f; // [-1, 1]

            return new Vector2(Mathf.Clamp(normX, -1f, 1f), Mathf.Clamp(normY, -1f, 1f));
        }
    }
}
