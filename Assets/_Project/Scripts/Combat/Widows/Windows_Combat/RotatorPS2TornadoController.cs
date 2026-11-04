using UnityEngine;

namespace SudoKill9.Combat
{
    /// <summary>
    /// Controlador auxiliar para el prefab 'RotatorPS2' (Tornado mágico que rodea a Linux):
    /// - Sigue la posición del jugador Linux en tiempo real de manera suave y estable.
    /// - Se va cerrando progresivamente, reduciendo su escala/radio de manera coherente.
    /// - Rota alrededor del personaje para enfatizar el efecto de vórtice energético.
    /// - Desactiva colliders propios para no bloquear balas del jugador ni colisiones físicas.
    /// - Finaliza y se destruye automáticamente tras cumplirse su duración.
    /// </summary>
    public class RotatorPS2TornadoController : MonoBehaviour
    {
        [Header("Configuración de Seguimiento")]
        [Tooltip("Transform del personaje Linux al que rodea y sigue.")]
        [SerializeField] private Transform targetLinux;

        [Tooltip("Desplazamiento relativo respecto a la posición del objetivo.")]
        [SerializeField] private Vector3 offsetSeguimiento = new Vector3(0f, 0.1f, 0f);

        [Header("Configuración de Duración y Contracción")]
        [Tooltip("Duración activa del tornado en segundos antes de disiparse.")]
        [SerializeField] private float duracionTornado = 5.0f;

        [Tooltip("Escala inicial al aparecer.")]
        [SerializeField] private Vector3 escalaInicial = new Vector3(1.2f, 1.2f, 1.2f);

        [Tooltip("Escala final al contraerse (al cerrarse completamente).")]
        [SerializeField] private Vector3 escalaFinal = new Vector3(0.08f, 0.08f, 0.08f);

        [Tooltip("Velocidad de rotación orbital adicional (grados/s) sobre el eje Y.")]
        [SerializeField] private float velocidadRotacionY = 180.0f;

        private float tiempoTranscurrido = 0f;
        private bool estaActivo = false;
        private ParticleSystem[] particulas;

        private void Awake()
        {
            DesactivarCollidersInternos();
            particulas = GetComponentsInChildren<ParticleSystem>(true);
        }

        private void DesactivarCollidersInternos()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = false;
            }
        }

        /// <summary>
        /// Inicializa el seguimiento y la secuencia de contracción del tornado.
        /// </summary>
        public void Inicializar(Transform objetivo, float duracion = 5.0f, Vector3? escalaBase = null)
        {
            DesactivarCollidersInternos();
            targetLinux = objetivo;
            duracionTornado = Mathf.Max(0.5f, duracion);
            if (escalaBase.HasValue)
            {
                escalaInicial = escalaBase.Value;
            }

            transform.localScale = escalaInicial;
            if (targetLinux != null)
            {
                transform.position = targetLinux.position + offsetSeguimiento;
            }

            tiempoTranscurrido = 0f;
            estaActivo = true;
        }

        private void LateUpdate()
        {
            if (!estaActivo) return;

            // 1. Verificar si el objetivo sigue existiendo
            if (targetLinux == null)
            {
                FinalizarTornado();
                return;
            }

            // 2. Seguimiento suave y estable centrado en Linux
            transform.position = targetLinux.position + offsetSeguimiento;

            // 3. Rotación orbital continua alrededor del eje Y
            transform.Rotate(Vector3.up, velocidadRotacionY * Time.deltaTime, Space.World);

            // 4. Progresión de contracción temporal
            tiempoTranscurrido += Time.deltaTime;
            float t = Mathf.Clamp01(tiempoTranscurrido / duracionTornado);

            // Curva suave tipo Ease-In para la contracción del tornado
            float factorContraccion = t * t;
            transform.localScale = Vector3.Lerp(escalaInicial, escalaFinal, factorContraccion);

            // 5. Finalización controlada
            if (tiempoTranscurrido >= duracionTornado)
            {
                FinalizarTornado();
            }
        }

        public void CancelarInmediatamente()
        {
            estaActivo = false;
            Destroy(gameObject);
        }

        private void FinalizarTornado()
        {
            estaActivo = false;

            // Detener emisión de partículas para desvanecimiento suave
            if (particulas != null)
            {
                for (int i = 0; i < particulas.Length; i++)
                {
                    if (particulas[i] != null)
                    {
                        particulas[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }

            // Destruir tras permitir el fade-out completo de las partículas vivas (vida original de 1.0s)
            Destroy(gameObject, 1.1f);
        }

        private void OnDestroy()
        {
            estaActivo = false;
        }
    }
}
