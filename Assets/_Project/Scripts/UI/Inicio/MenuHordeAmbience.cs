using UnityEngine;

namespace SudoKill9.UI.Inicio
{
    /// <summary>
    /// Añade vida y dinamismo sutil a la horda enemiga en el menú de inicio:
    /// - Leve flotación / balanceo en las unidades voladoras.
    /// - Ligera respiración / pulso en el jefe y unidades de tierra.
    /// </summary>
    public class MenuHordeAmbience : MonoBehaviour
    {
        [Tooltip("Si es true, flota hacia arriba y abajo (ideal para drones y voladores).")]
        [SerializeField] private bool isFlyer = false;
        [SerializeField] private float bobSpeed = 1.5f;
        [SerializeField] private float bobHeight = 0.12f;

        [Tooltip("Si es true, tiene un pulso de escala muy sutil (respiración).")]
        [SerializeField] private bool hasBreathing = true;
        [SerializeField] private float breathSpeed = 1.0f;
        [SerializeField] private float breathScaleAmount = 0.02f;

        private Vector3 initialPosition;
        private Vector3 initialScale;
        private float randomOffset;

        private void Start()
        {
            initialPosition = transform.position;
            initialScale = transform.localScale;
            randomOffset = Random.Range(0f, 6.28f); // Desfase aleatorio para no sincronizar a todos
        }

        private void Update()
        {
            float time = Time.time + randomOffset;

            // Flotación para voladores
            if (isFlyer)
            {
                float newY = initialPosition.y + Mathf.Sin(time * bobSpeed) * bobHeight;
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            }

            // Respiración sutil
            if (hasBreathing)
            {
                float scaleOffset = Mathf.Sin(time * breathSpeed) * breathScaleAmount;
                transform.localScale = initialScale * (1f + scaleOffset);
            }
        }
    }
}
