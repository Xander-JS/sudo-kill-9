using UnityEngine;

namespace SudoKill9.Combat
{
    /// <summary>
    /// Controlador auxiliar para el prefab 'Explosion' de Hovl Studio:
    /// - Garantiza que el efecto de explosión se reproduzca exactamente una sola vez (loop = false).
    /// - Gestiona la destrucción limpia y automática del GameObject tras su ciclo de vida (sin dejar partículas residuales).
    /// - Permite configurar la escala visual de forma dinámica sin modificar los recursos originales del paquete.
    /// </summary>
    public class ExplosionEffectController : MonoBehaviour
    {
        [Tooltip("Tiempo de vida total antes de destruir la instancia de explosión.")]
        [SerializeField] private float duracionTotal = 2.0f;

        private void Awake()
        {
            // Hovl Studio tiene partículas con loop=true en sus prefabs originales.
            // Para que funcione como explosión de impacto, desactivamos el loop en todos los ParticleSystems hijos.
            ParticleSystem[] sistemas = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < sistemas.Length; i++)
            {
                if (sistemas[i] != null)
                {
                    var main = sistemas[i].main;
                    main.loop = false;
                    main.stopAction = ParticleSystemStopAction.None;
                }
            }

            // Desactivar colliders residuales para que la explosión visual no interfiera con proyectiles
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = false;
            }
        }

        private void Start()
        {
            // Asegurar reproducción inicial
            ParticleSystem[] sistemas = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < sistemas.Length; i++)
            {
                if (sistemas[i] != null && !sistemas[i].isPlaying)
                {
                    sistemas[i].Play(true);
                }
            }

            // Destrucción limpia y segura
            Destroy(gameObject, duracionTotal);
        }

        /// <summary>
        /// Inicializa la explosión con escala personalizada y duración controlada.
        /// </summary>
        public void Inicializar(Vector3 escala, float duracion = 2.0f)
        {
            duracionTotal = Mathf.Max(0.5f, duracion);
            transform.localScale = escala;
        }
    }
}
