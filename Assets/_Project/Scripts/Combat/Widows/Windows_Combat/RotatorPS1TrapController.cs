using System.Collections.Generic;
using UnityEngine;

namespace SudoKill9.Combat
{
    /// <summary>
    /// Controlador para el ataque de área estática 'RotatorPS1':
    /// - Permanece FIJO en el punto de impacto exacto en el suelo. NO sigue, persigue ni se desplaza con el pingüino.
    /// - Si el pingüino estaba dentro del área al momento del impacto, queda confinado dentro del círculo:
    ///   puede moverse, rotar y disparar con total libertad dentro del círculo, pero no puede atravesar el límite perimetral.
    /// - Las balas del pingüino y los ataques del jefe atraviesan libremente el límite perimetral (sin barreras sólidas que bloqueen proyectiles).
    /// - La cámara en tercera persona no colisiona con el borde del área, evitando tirones o acercamientos indeseados.
    /// - Si esquivó antes del impacto, no sufre daño ni confinamiento.
    /// - Al finalizar la duración configurable, se retira la restricción y el pingüino recupera movilidad completa.
    /// - Desactiva colliders internos del prefab para no interferir con las balas del jugador.
    /// </summary>
    public class RotatorPS1TrapController : MonoBehaviour
    {
        [Header("Configuración de Duración")]
        [Tooltip("Duración activa de la trampa estática en segundos antes de disiparse.")]
        [SerializeField] private float duracionTrampa = 2.5f;

        [Header("Configuración de Área y Confinamiento")]
        [Tooltip("Radio del área circular de confinamiento.")]
        [SerializeField] private float radioConfinamiento = 3.2f;

        [Tooltip("Velocidad de rotación orbital del efecto visual (grados/s).")]
        [SerializeField] private float velocidadRotacionY = 160.0f;

        [Tooltip("Margen de seguridad del CharacterController para el límite físico.")]
        [SerializeField] private float margenBorde = 0.35f;

        private Vector3 posicionCentro;
        private Transform targetLinux;
        private bool estaAtrapado = false;
        private bool estaActivo = false;
        private float tiempoTranscurrido = 0f;
        private ParticleSystem[] particulas;

        public float DuracionTrampa => duracionTrampa;
        public float RadioConfinamiento => radioConfinamiento;
        public bool EstaAtrapado => estaAtrapado;
        public Vector3 PosicionCentro => posicionCentro;

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
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        /// <summary>
        /// Inicializa la trampa en la posición FIJA de impacto en el suelo.
        /// </summary>
        public void Inicializar(
            Vector3 puntoImpactoSuelo,
            float duracion = 2.5f,
            float radio = 3.2f,
            Transform transformLinux = null,
            bool atraparJugador = false,
            Vector3? escalaBase = null)
        {
            DesactivarCollidersInternos();

            posicionCentro = puntoImpactoSuelo;
            transform.position = posicionCentro;

            duracionTrampa = Mathf.Max(0.5f, duracion);
            radioConfinamiento = Mathf.Max(0.8f, radio);
            targetLinux = transformLinux;
            estaAtrapado = (atraparJugador && targetLinux != null);

            if (escalaBase.HasValue)
            {
                transform.localScale = escalaBase.Value;
            }

            tiempoTranscurrido = 0f;
            estaActivo = true;
        }

        public void InicializarTrampa(Transform transformLinux, float duracion = 2.5f, bool atraparJugador = false)
        {
            Inicializar(transform.position, duracion, radioConfinamiento, transformLinux, atraparJugador);
        }

        private void LateUpdate()
        {
            if (!estaActivo) return;

            // 1. MANTENER ESTRICTAMENTE FIJO: La trampa NO se mueve ni sigue al jugador
            transform.position = posicionCentro;

            // 2. Rotación continua sobre el eje Y para mantener el vórtice visual
            transform.Rotate(Vector3.up, velocidadRotacionY * Time.deltaTime, Space.World);

            // 3. Confinamiento suave y hermético del pingüino dentro del radio si quedó atrapado en el impacto
            if (estaAtrapado && targetLinux != null)
            {
                AplicarLimiteConfinamiento(targetLinux);
            }

            // 4. Progresión temporal y finalización
            tiempoTranscurrido += Time.deltaTime;
            if (tiempoTranscurrido >= duracionTrampa)
            {
                FinalizarTrampa();
            }
        }

        /// <summary>
        /// Restringe frame a frame al pingüino dentro del radio circular sin congelarlo.
        /// Utiliza el CharacterController nativo (Move) para permitir deslizamiento suave
        /// y tangencial a lo largo del perímetro circular, preservando la física de suelo (grounding)
        /// y sin crear colliders estáticos que bloqueen las balas o la cámara en tercera persona.
        /// </summary>
        public void AplicarLimiteConfinamiento(Transform jugador)
        {
            if (jugador == null) return;

            Vector3 posJugador = jugador.position;
            Vector3 offset = posJugador - posicionCentro;
            offset.y = 0f;

            float distanciaHorizontal = offset.magnitude;
            float radioMaximoPermitido = Mathf.Max(0.2f, radioConfinamiento - margenBorde);

            if (distanciaHorizontal > radioMaximoPermitido && distanciaHorizontal > 0.001f)
            {
                Vector3 direccionRadial = offset.normalized;
                Vector3 posBorde = posicionCentro + direccionRadial * radioMaximoPermitido;

                CharacterController cc = jugador.GetComponent<CharacterController>();
                if (cc != null && cc.enabled)
                {
                    // Vector correctivo hacia adentro del círculo para deslizar suavemente por la circunferencia
                    Vector3 correccion = new Vector3(posBorde.x - posJugador.x, 0f, posBorde.z - posJugador.z);
                    cc.Move(correccion);

                    // Clamp de seguridad absoluto en caso de que fuerzas externas o alta velocidad sobrepasen el paso
                    Vector3 nuevoOffset = jugador.position - posicionCentro;
                    nuevoOffset.y = 0f;
                    if (nuevoOffset.magnitude > radioMaximoPermitido + 0.05f)
                    {
                        bool estabaHabilitado = cc.enabled;
                        cc.enabled = false;
                        jugador.position = new Vector3(posBorde.x, jugador.position.y, posBorde.z);
                        cc.enabled = estabaHabilitado;
                    }
                }
                else
                {
                    jugador.position = new Vector3(posBorde.x, posJugador.y, posBorde.z);
                }
            }
        }

        public void LiberarYDestruirInmediato()
        {
            LiberarJugador();
            Destroy(gameObject);
        }

        private void LiberarJugador()
        {
            estaAtrapado = false;
        }

        private void FinalizarTrampa()
        {
            estaActivo = false;
            LiberarJugador();

            // Detener emisión de partículas para desvanecimiento natural
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

            Destroy(gameObject, 1.1f);
        }

        private void OnDestroy()
        {
            LiberarJugador();
            estaActivo = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = estaAtrapado ? Color.red : new Color(1f, 0.5f, 0f, 0.75f);
            Vector3 centro = (posicionCentro != Vector3.zero) ? posicionCentro : transform.position;
            
            // Dibujar círculo de confinamiento en Editor
            int segmentos = 36;
            float paso = 360f / segmentos;
            Vector3 prev = centro + new Vector3(radioConfinamiento, 0.1f, 0f);
            for (int i = 1; i <= segmentos; i++)
            {
                float rad = (i * paso) * Mathf.Deg2Rad;
                Vector3 sig = centro + new Vector3(Mathf.Cos(rad) * radioConfinamiento, 0.1f, Mathf.Sin(rad) * radioConfinamiento);
                Gizmos.DrawLine(prev, sig);
                prev = sig;
            }
        }
    }
}
