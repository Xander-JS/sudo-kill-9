using System;
using UnityEngine;

namespace SudoKill9.Combat
{
    /// <summary>
    /// Controlador del Ataque 2: Magic Fire 1 (Proyectil Mágico de Persecución Homing):
    /// - Se instancia en el punto de disparo de Windows orientado hacia Linux.
    /// - Persigue activamente la posición dinámica de Linux durante un máximo de 3 segundos.
    /// - Utiliza rotación suave (turn rate limitado) y velocidad configurable para trayectorias fluidas.
    /// - No se teletransporta ni se vincula rígidamente al transform de Linux.
    /// - Detecta colisiones físicas con obstáculos o con Linux.
    /// - Al impactar a Linux (o al cumplirse los 3 segundos) desata el efecto 'Explosion'
    ///   y aplica el daño correspondiente una sola vez.
    /// </summary>
    public class MagicFire1HomingController : MonoBehaviour
    {
        [Header("Cinemática de Persecución")]
        [Tooltip("Velocidad lineal de avance del proyectil (m/s).")]
        [SerializeField] private float velocidadPersecucion = 15.0f;

        [Tooltip("Velocidad angular máxima de giro (grados/s) para arquear suavemente.")]
        [SerializeField] private float velocidadGiro = 160.0f;

        [Tooltip("Duración máxima de persecución antes de autodestruirse (segundos).")]
        [SerializeField] private float duracionMaxima = 3.0f;

        [Header("Impacto y Daño")]
        [Tooltip("Radio de proximidad para considerar impacto directo con Linux.")]
        [SerializeField] private float radioContacto = 1.1f;

        [Tooltip("Radio de onda expansiva al detonar por fin de tiempo.")]
        [SerializeField] private float radioExplosion = 2.8f;

        [Tooltip("Daño infligido a Linux al impactar.")]
        [SerializeField] private float dano = 1.0f;

        [Tooltip("Escala del prefab Explosion.")]
        [SerializeField] private Vector3 escalaExplosion = new Vector3(1.5f, 1.5f, 1.5f);

        [Tooltip("Capas de colisión que el proyectil evalúa como obstáculos sólidos del entorno.")]
        [SerializeField] private LayerMask capaObstaculos = ~0;

        private Transform targetLinux;
        private Transform lanzadorBoss;
        private GameObject prefabExplosion;
        private Action onFinalizado;

        private float tiempoVuelo = 0f;
        private bool homingActivo = false;
        private bool impactoProcesado = false;

        public float VelocidadPersecucion => velocidadPersecucion;
        public float DuracionMaxima => duracionMaxima;
        public float RadioExplosion => radioExplosion;
        public float Dano => dano;

        /// <summary>
        /// Inicializa el proyectil de persecución telegrafiada hacia Linux.
        /// </summary>
        public void Inicializar(
            Transform objetivoLinux,
            Transform origenBoss,
            GameObject explosionPrefab,
            float velocidad = 15f,
            float giro = 160f,
            float duracion = 3.0f,
            float contacto = 1.1f,
            float radioExp = 2.8f,
            float danoAtaque = 1.0f,
            Vector3? escalaExp = null,
            Action callbackFin = null)
        {
            targetLinux = objetivoLinux;
            lanzadorBoss = origenBoss;
            prefabExplosion = explosionPrefab;
            onFinalizado = callbackFin;

            velocidadPersecucion = Mathf.Max(1f, velocidad);
            velocidadGiro = Mathf.Max(10f, giro);
            duracionMaxima = Mathf.Max(0.5f, duracion);
            radioContacto = Mathf.Max(0.2f, contacto);
            radioExplosion = Mathf.Max(0.5f, radioExp);
            dano = danoAtaque;

            if (escalaExp.HasValue) escalaExplosion = escalaExp.Value;

            // Desactivar colliders residuales en el propio proyectil
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = false;
            }

            tiempoVuelo = 0f;
            homingActivo = true;
            impactoProcesado = false;
        }

        private void Update()
        {
            if (!homingActivo || impactoProcesado) return;

            tiempoVuelo += Time.deltaTime;

            // 1. CONDICIÓN DE TIEMPO AGOTADO (Máximo 3 segundos)
            if (tiempoVuelo >= duracionMaxima)
            {
                ProcesarFinPorTiempo();
                return;
            }

            // 2. Calcular punto de mira sobre Linux (centrado en el torso)
            Vector3 objetivoPos = transform.position + transform.forward * 10f;
            if (targetLinux != null)
            {
                objetivoPos = targetLinux.position + Vector3.up * 0.8f;
            }

            Vector3 direccionDeseada = (objetivoPos - transform.position).normalized;

            // 3. Rotación angular suave hacia el objetivo
            if (direccionDeseada.sqrMagnitude > 0.001f)
            {
                Quaternion rotacionObjetivo = Quaternion.LookRotation(direccionDeseada);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacionObjetivo, velocidadGiro * Time.deltaTime);
            }

            // 4. Avance del proyectil
            float distanciaPaso = velocidadPersecucion * Time.deltaTime;
            Vector3 siguientePosicion = transform.position + transform.forward * distanciaPaso;

            // 5. Detección de colisión con obstáculos en trayectoria (SphereCast)
            RaycastHit hit;
            if (Physics.SphereCast(transform.position, 0.4f, transform.forward, out hit, distanciaPaso, capaObstaculos, QueryTriggerInteraction.Ignore))
            {
                // Ignorar al jefe lanzador y al propio proyectil
                if (lanzadorBoss == null || (!hit.collider.transform.IsChildOf(lanzadorBoss) && hit.collider.transform != lanzadorBoss))
                {
                    bool esLinux = targetLinux != null && (hit.collider.transform == targetLinux || hit.collider.transform.IsChildOf(targetLinux));
                    ProcesarImpacto(hit.point, esLinux);
                    return;
                }
            }

            transform.position = siguientePosicion;

            // 6. Detección de impacto directo por proximidad con Linux
            if (targetLinux != null)
            {
                Vector3 centroLinux = targetLinux.position + Vector3.up * 0.8f;
                float distanciaLinux = Vector3.Distance(transform.position, centroLinux);

                if (distanciaLinux <= radioContacto)
                {
                    ProcesarImpacto(transform.position, esImpactoDirectoLinux: true);
                }
            }
        }

        private void ProcesarImpacto(Vector3 puntoImpacto, bool esImpactoDirectoLinux)
        {
            if (impactoProcesado) return;
            impactoProcesado = true;
            homingActivo = false;

            // 1. Instanciar Explosion una sola vez en el punto de contacto
            if (prefabExplosion != null)
            {
                GameObject goExp = Instantiate(prefabExplosion, puntoImpacto, Quaternion.identity);
                ExplosionEffectController expCtrl = goExp.GetComponent<ExplosionEffectController>();
                if (expCtrl == null) expCtrl = goExp.AddComponent<ExplosionEffectController>();
                expCtrl.Inicializar(escalaExplosion, 2.0f);
            }

            // 2. Aplicar daño a Linux exactamente una vez:
            // Por impacto directo, o si la explosión de colisión con obstáculo alcanza a Linux
            if (targetLinux != null)
            {
                if (esImpactoDirectoLinux)
                {
                    AplicarDanoALinux();
                }
                else
                {
                    float dist = Vector3.Distance(puntoImpacto, targetLinux.position);
                    if (dist <= radioExplosion)
                    {
                        AplicarDanoALinux();
                    }
                }
            }

            FinalizarYDestruir();
        }

        private void ProcesarFinPorTiempo()
        {
            if (impactoProcesado) return;
            impactoProcesado = true;
            homingActivo = false;

            Vector3 puntoFinal = transform.position;

            // 1. Instanciar Explosion en la última posición válida
            if (prefabExplosion != null)
            {
                GameObject goExp = Instantiate(prefabExplosion, puntoFinal, Quaternion.identity);
                ExplosionEffectController expCtrl = goExp.GetComponent<ExplosionEffectController>();
                if (expCtrl == null) expCtrl = goExp.AddComponent<ExplosionEffectController>();
                expCtrl.Inicializar(escalaExplosion, 2.0f);
            }

            // 2. Si Linux se encuentra dentro del radio de explosión, recibe daño
            if (targetLinux != null)
            {
                float dist = Vector3.Distance(puntoFinal, targetLinux.position);
                if (dist <= radioExplosion)
                {
                    AplicarDanoALinux();
                }
            }

            FinalizarYDestruir();
        }

        private void AplicarDanoALinux()
        {
            if (targetLinux == null) return;

            IDamageable damageable = targetLinux.GetComponent<IDamageable>() ??
                                    targetLinux.GetComponentInParent<IDamageable>() ??
                                    targetLinux.GetComponentInChildren<IDamageable>();

            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(dano);
            }
        }

        private void FinalizarYDestruir()
        {
            // Detener emisión de partículas para salida visual limpia
            ParticleSystem[] sistemas = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < sistemas.Length; i++)
            {
                if (sistemas[i] != null)
                {
                    sistemas[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            // Ocultar mallas o luces activas del proyectil para que solo queden las estelas desvaneciéndose
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = false;
            }
            Light[] lights = GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null) lights[i].enabled = false;
            }

            onFinalizado?.Invoke();
            // Tiempo suficiente (1.2s) para permitir el fade-out suave de las estelas sin corte abrupto
            Destroy(gameObject, 1.2f);
        }

        private void OnDestroy()
        {
            homingActivo = false;
        }
    }
}
