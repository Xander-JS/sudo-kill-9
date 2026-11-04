using System;
using UnityEngine;

namespace SudoKill9.Combat
{
    /// <summary>
    /// Controlador del Ataque 1: Magic Fire 0 (Ataque Aéreo de Impacto y Esquive):
    /// 1. Captura la posición de Linux al momento exacto del lanzamiento y la guarda fijamente.
    /// 2. Cae desde una altura configurable hacia dicha posición sin corregir trayectoria (permitiendo esquivar).
    /// 3. Al impactar el suelo:
    ///    - Si Linux esquivó (está fuera de radioImpacto): Falla limpiamente sin daño, sin explosión y sin tornado.
    ///    - Si Linux fue impactado (dentro de radioImpacto):
    ///        * Instancia prefab 'Explosion' en el punto de contacto.
    ///        * Aplica daño exactamente una vez a Linux.
    ///        * Instancia prefab 'RotatorPS2' centrado en Linux con contracción progresiva.
    /// </summary>
    public class MagicFire0AereoController : MonoBehaviour
    {
        [Header("Parámetros de Caída")]
        [Tooltip("Altura sobre el punto de impacto donde aparece el proyectil aéreo.")]
        [SerializeField] private float alturaInicial = 14.0f;

        [Tooltip("Velocidad de descenso hacia la posición guardada (m/s).")]
        [SerializeField] private float velocidadCaida = 16.0f;

        [Tooltip("Radio de la zona de impacto para verificar si Linux esquivó o recibió el golpe.")]
        [SerializeField] private float radioImpacto = 3.2f;

        [Header("Parámetros de Daño e Impacto")]
        [Tooltip("Daño infligido a Linux si permanece en el radio de impacto.")]
        [SerializeField] private float dano = 1.0f;

        [Tooltip("Escala del prefab Explosion.")]
        [SerializeField] private Vector3 escalaExplosion = new Vector3(1.5f, 1.5f, 1.5f);

        [Header("Efectos Secundarios en Acierto")]
        [Tooltip("Duración activa de la trampa estática RotatorPS1 en el suelo.")]
        [SerializeField] private float duracionRotatorPS1 = 2.5f;

        [Tooltip("Duración activa del tornado RotatorPS2 alrededor de Linux (compatibilidad).")]
        [SerializeField] private float duracionRotatorPS2 = 2.5f;

        [Tooltip("Escala inicial del tornado RotatorPS2.")]
        [SerializeField] private Vector3 escalaRotatorPS2 = new Vector3(1.2f, 1.2f, 1.2f);

        // Variables fijas del ataque
        private Vector3 posicionObjetivoRegistrada;
        private Transform targetLinux;
        private GameObject prefabExplosion;
        private GameObject prefabRotatorPS2;
        private Action onFinalizado;

        private bool inicializado = false;
        private bool haLlegadoAlDestino = false;
        private bool impactoProcesado = false;

        public float AlturaInicial => alturaInicial;
        public float VelocidadCaida => velocidadCaida;
        public float RadioImpacto => radioImpacto;
        public float Dano => dano;
        public float DuracionRotatorPS1 => duracionRotatorPS1;

        /// <summary>
        /// Configura el ataque con la posición congelada de Linux en el momento del lanzamiento.
        /// </summary>
        public void Inicializar(
            Vector3 posicionLinuxEnLanzamiento,
            Transform transformLinux,
            GameObject explosionPrefab,
            GameObject rotatorPrefab,
            float altura = 14f,
            float velocidad = 16f,
            float radio = 3.2f,
            float danoAtaque = 1f,
            float duracionTornado = 2.5f,
            Vector3? escalaTornado = null,
            Vector3? escalaExp = null,
            Action callbackFin = null)
        {
            // REGLA FUNDAMENTAL: Guardar la posición fija capturada al inicio del ataque
            posicionObjetivoRegistrada = posicionLinuxEnLanzamiento;
            targetLinux = transformLinux;
            prefabExplosion = explosionPrefab;
            prefabRotatorPS2 = rotatorPrefab;
            onFinalizado = callbackFin;

            alturaInicial = Mathf.Max(3f, altura);
            velocidadCaida = Mathf.Max(2f, velocidad);
            radioImpacto = Mathf.Max(0.5f, radio);
            dano = danoAtaque;
            duracionRotatorPS1 = Mathf.Max(0.5f, duracionTornado);
            duracionRotatorPS2 = duracionRotatorPS1;

            if (escalaTornado.HasValue) escalaRotatorPS2 = escalaTornado.Value;
            if (escalaExp.HasValue) escalaExplosion = escalaExp.Value;

            // Situar en la posición inicial elevada sobre el punto registrado
            Vector3 posAparicion = posicionObjetivoRegistrada + Vector3.up * alturaInicial;
            transform.position = posAparicion;

            // Orientar hacia el suelo en dirección al punto de impacto
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            haLlegadoAlDestino = false;
            impactoProcesado = false;
            inicializado = true;
        }

        private void Update()
        {
            if (!inicializado || haLlegadoAlDestino) return;

            // Desplazar hacia la posición guardada a velocidad constante
            float paso = velocidadCaida * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, posicionObjetivoRegistrada, paso);

            // Comprobar si ha alcanzado la altura del suelo / posición capturada
            if (Vector3.Distance(transform.position, posicionObjetivoRegistrada) <= 0.25f || transform.position.y <= posicionObjetivoRegistrada.y + 0.15f)
            {
                haLlegadoAlDestino = true;
                ProcesarLlegadaAlSuelo();
            }
        }

        private void ProcesarLlegadaAlSuelo()
        {
            if (impactoProcesado) return;
            impactoProcesado = true;

            // Comprobar si Linux sigue dentro del radio de impacto
            bool linuxEnArea = VerificarPresenciaDeLinux();

            if (linuxEnArea)
            {
                // ============================================================= //
                // HIT: Impacto confirmado sobre Linux                           //
                // ============================================================= //
                Vector3 puntoContacto = targetLinux != null ? targetLinux.position : posicionObjetivoRegistrada;

                // 1. Instanciar Explosion en el punto real del impacto
                if (prefabExplosion != null)
                {
                    GameObject goExp = Instantiate(prefabExplosion, puntoContacto, Quaternion.identity);
                    ExplosionEffectController expCtrl = goExp.GetComponent<ExplosionEffectController>();
                    if (expCtrl == null) expCtrl = goExp.AddComponent<ExplosionEffectController>();
                    expCtrl.Inicializar(escalaExplosion, 2.0f);
                }

                // 2. Aplicar daño exactamente una vez a Linux
                if (targetLinux != null)
                {
                    IDamageable damageable = targetLinux.GetComponent<IDamageable>() ??
                                            targetLinux.GetComponentInParent<IDamageable>() ??
                                            targetLinux.GetComponentInChildren<IDamageable>();
                    if (damageable != null && !damageable.IsDead)
                    {
                        damageable.TakeDamage(dano);
                    }
                }

                // 3. Instanciar RotatorPS1 FIJO en el lugar de impacto como trampa de área con confinamiento
                InstanciarRotatorEstatico(linuxEnArea);
            }
            else
            {
                // ============================================================= //
                // MISS: Linux esquivó con éxito                                 //
                // ============================================================= //
                // Esquive exitoso: no recibe daño. El efecto visual permanece en el suelo sin atrapar
                InstanciarRotatorEstatico(false);
            }

            FinalizarYDestruir();
        }

        private bool VerificarPresenciaDeLinux()
        {
            if (targetLinux == null) return false;

            // 1. Comprobación volumétrica de colisión real con el CharacterController/Hitbox de Linux
            Vector3 centroImpacto = posicionObjetivoRegistrada + Vector3.up * 0.9f;
            Collider[] colliders = Physics.OverlapSphere(centroImpacto, radioImpacto, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;
                if (colliders[i].transform == targetLinux || 
                    colliders[i].transform.IsChildOf(targetLinux) || 
                    colliders[i].transform.root == targetLinux.root)
                {
                    return true;
                }
            }

            // 2. Comprobación de distancia horizontal con cota vertical estricta (no impacta si Linux saltó a gran altura o está en otra plataforma)
            float diffY = Mathf.Abs(targetLinux.position.y - posicionObjetivoRegistrada.y);
            if (diffY <= 2.5f)
            {
                Vector3 posLinuxXZ = new Vector3(targetLinux.position.x, 0f, targetLinux.position.z);
                Vector3 posImpactoXZ = new Vector3(posicionObjetivoRegistrada.x, 0f, posicionObjetivoRegistrada.z);
                if (Vector3.Distance(posLinuxXZ, posImpactoXZ) <= radioImpacto)
                {
                    return true;
                }
            }

            return false;
        }

        private void InstanciarRotatorEstatico(bool atraparJugador)
        {
            if (prefabRotatorPS2 == null) return;

            // 1. Limpiar trampas o tornados previos para no apilar efectos
            RotatorPS1TrapController[] trampasPrevias = UnityEngine.Object.FindObjectsByType<RotatorPS1TrapController>(FindObjectsSortMode.None);
            for (int i = 0; i < trampasPrevias.Length; i++)
            {
                if (trampasPrevias[i] != null) trampasPrevias[i].LiberarYDestruirInmediato();
            }
            RotatorPS2TornadoController[] tornadosPrevios = UnityEngine.Object.FindObjectsByType<RotatorPS2TornadoController>(FindObjectsSortMode.None);
            for (int i = 0; i < tornadosPrevios.Length; i++)
            {
                if (tornadosPrevios[i] != null) tornadosPrevios[i].CancelarInmediatamente();
            }

            // 2. Determinar posición fija de suelo exacta
            Vector3 puntoSuelo = posicionObjetivoRegistrada;
            if (Physics.Raycast(posicionObjetivoRegistrada + Vector3.up * 2f, Vector3.down, out RaycastHit hitSuelo, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                puntoSuelo = hitSuelo.point;
            }

            // 3. Instanciar Rotator en punto de impacto fijo (NO sigue a Linux)
            GameObject goRotator = Instantiate(prefabRotatorPS2, puntoSuelo, Quaternion.identity);

            // Desactivar o remover componente antiguo de seguimiento si venía en el prefab
            RotatorPS2TornadoController antiguoTornado = goRotator.GetComponent<RotatorPS2TornadoController>();
            if (antiguoTornado != null) Destroy(antiguoTornado);

            RotatorPS1TrapController trapCtrl = goRotator.GetComponent<RotatorPS1TrapController>();
            if (trapCtrl == null) trapCtrl = goRotator.AddComponent<RotatorPS1TrapController>();

            trapCtrl.Inicializar(
                puntoImpactoSuelo: puntoSuelo,
                duracion: duracionRotatorPS1,
                radio: radioImpacto,
                transformLinux: targetLinux,
                atraparJugador: atraparJugador,
                escalaBase: escalaRotatorPS2
            );
        }

        private void FinalizarYDestruir()
        {
            // Detener emisión de partículas de Magic Fire 0 para salida visual limpia
            ParticleSystem[] sistemas = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < sistemas.Length; i++)
            {
                if (sistemas[i] != null)
                {
                    sistemas[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            onFinalizado?.Invoke();
            // Duración suficiente (1.2s) para permitir el fade-out natural de partículas vivas sin corte abrupto
            Destroy(gameObject, 1.2f);
        }

        private void OnDestroy()
        {
            inicializado = false;
        }
    }
}
