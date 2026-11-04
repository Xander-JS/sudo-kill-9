using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace SudoKill9.Enemies.Volador
{
    /// <summary>
    /// IA del Volador: máquina voladora que patrulla y persigue sobre el NavMesh a flightHeight (NavMeshAgent.baseOffset).
    /// Al atacar desciende hasta attackHeight (animación Stylish Flip), vuelve a subir y, al morir en el aire, cae al suelo.
    /// Toda la máquina de estados vive en EnemyAIBase; aquí solo va lo específico del vuelo.
    /// </summary>
    [RequireComponent(typeof(VoladorStats), typeof(VoladorAudio))]
    public class VoladorAIController : EnemyAIBase
    {
        [Header("Vuelo")]
        [Tooltip("Altura de vuelo sobre el suelo/NavMesh en metros (se aplica como NavMeshAgent.baseOffset).")]
        [FormerlySerializedAs("hoverHeight")]
        [SerializeField] private float flightHeight = 1.2f;
        [Tooltip("Amplitud del balanceo vertical en metros. 0 = sin balanceo.")]
        [SerializeField] private float bobAmplitude = 0.12f;
        [SerializeField] private float bobFrequency = 1.6f;

        [Header("Descenso de Ataque")]
        [Tooltip("Altura sobre el suelo durante el ataque (0 = aterriza).")]
        [SerializeField] private float attackHeight = 0f;
        [Tooltip("Velocidad de descenso y de ascenso en m/s.")]
        [SerializeField] private float heightChangeSpeed = 5f;
        [Tooltip("Segundos que permanece abajo tras iniciar el ataque antes de volver a subir.")]
        [SerializeField] private float attackGroundTime = 1.2f;

        [Header("Muerte")]
        [Tooltip("Segundos que tarda en caer al suelo al morir.")]
        [SerializeField] private float deathFallDuration = 0.6f;

        private VoladorAudio voladorAudio;
        private float currentHeight;
        private float descendUntil = -999f;
        private float bobPhase;

        /// <summary>Altura de vuelo actual configurada (metros sobre el suelo).</summary>
        public float FlightHeight => flightHeight;

        // Una máquina voladora no pasa a gateo con vida crítica.
        protected override bool IsCrawling => false;

        private void Reset()
        {
            alertRange = 18f;
            attackRange = 3.2f;     // distancia horizontal: no depende de la altura de vuelo
            loseTargetMultiplier = 1.6f;
            runSpeed = 4.2f;        // más pesado y lento que el Virus (5.5)
            crawlSpeed = 4.2f;
            rotationSpeed = 6f;
            alertDuration = 1.0f;
            hitStaggerDuration = 0.3f; // se aturde menos al recibir daño
        }

        protected override void Awake()
        {
            base.Awake();
            voladorAudio = enemyAudio as VoladorAudio;
            currentHeight = flightHeight;
            bobPhase = Random.value * Mathf.PI * 2f; // desincroniza varios voladores
        }

        protected override void Update()
        {
            base.Update();
            if (!agent.enabled) return;

            bool descending = Time.time < descendUntil;
            float goal = descending ? attackHeight : flightHeight;
            currentHeight = Mathf.MoveTowards(currentHeight, goal, heightChangeSpeed * Time.deltaTime);

            // El balanceo solo se aplica en vuelo, no pegado al suelo.
            float bob = descending ? 0f : Mathf.Sin(Time.time * bobFrequency + bobPhase) * bobAmplitude;
            agent.baseOffset = currentHeight + bob;
        }

        // Horizontal: la altura de vuelo no debe impedir entrar en rango de ataque.
        protected override float DistanceToTarget()
        {
            Vector3 delta = target.position - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        protected override void PerformAttack(int variant)
        {
            // Inicio del descenso/ataque hacia el jugador.
            descendUntil = Time.time + attackGroundTime;
            if (voladorAudio != null) voladorAudio.PlayLandingAttackSound();

            base.PerformAttack(variant); // trigger Attack (Stylish Flip), SFX de impacto y daño
        }

        protected override void OnStateEntered(EnemyState state)
        {
            if (state == EnemyState.Dead) StartCoroutine(FallToGround());
        }

        private System.Collections.IEnumerator FallToGround()
        {
            Vector3 start = transform.position;
            float height = agent.baseOffset;
            float groundY = start.y - height;
            if (NavMesh.SamplePosition(start, out NavMeshHit hit, height + 2f, NavMesh.AllAreas)) groundY = hit.position.y;
            Vector3 end = new Vector3(start.x, groundY, start.z);

            for (float t = 0f; t < deathFallDuration; t += Time.deltaTime)
            {
                float k = t / deathFallDuration;
                transform.position = Vector3.Lerp(start, end, k * k); // acelera como en caída libre
                yield return null;
            }
            transform.position = end;
        }

#if UNITY_EDITOR
        // Previsualiza la altura de vuelo en la escena al cambiarla en el inspector.
        private void OnValidate()
        {
            flightHeight = Mathf.Max(0f, flightHeight);
            attackHeight = Mathf.Clamp(attackHeight, 0f, flightHeight);
            if (!Application.isPlaying && TryGetComponent<NavMeshAgent>(out var navAgent)) navAgent.baseOffset = flightHeight;
        }
#endif
    }
}
