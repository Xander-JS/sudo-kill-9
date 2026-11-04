using System;
using UnityEngine;
using UnityEngine.AI;
using SudoKill9.Combat;

namespace SudoKill9.Enemies
{
    public enum EnemyState
    {
        Idle,
        Alert,
        Chase,
        Attack,
        TakeDamage,
        Dead
    }

    /// <summary>
    /// Máquina de estados NavMesh común a los enemigos (Idle → Alerta → Persecución → Ataque, Daño, Muerte).
    /// Los derivados ajustan rangos/velocidades en el inspector y sobrescriben los métodos virtuales
    /// (ChooseAttackVariant, PerformAttack, IsCrawling, OnStateEntered...) para comportamientos propios.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class EnemyAIBase : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Objetivo a atacar. Si está vacío, se busca 'Player_Linux', el tag 'Player' o un LinuxSkeletonController.")]
        [SerializeField] protected Transform target;
        [SerializeField] protected Animator animator;
        [SerializeField] protected NavMeshAgent agent;
        [SerializeField] protected EnemyStatsBase stats;
        [SerializeField] protected EnemyAudioBase enemyAudio;

        [Header("Rangos y Velocidades")]
        [SerializeField] protected float alertRange = 14f;
        [SerializeField] protected float attackRange = 2.2f;
        [Tooltip("Multiplicador de alertRange a partir del cual se pierde al objetivo.")]
        [SerializeField] protected float loseTargetMultiplier = 1.6f;
        [SerializeField] protected float runSpeed = 5.5f;
        [Tooltip("Velocidad cuando IsCrawling es verdadero (por defecto: vida crítica).")]
        [SerializeField] protected float crawlSpeed = 2.2f;
        [SerializeField] protected float rotationSpeed = 10f;

        [Header("Tiempos de Estados")]
        [Tooltip("Duración de la alerta inicial (rugido). 0 = sin fase de alerta.")]
        [SerializeField] protected float alertDuration = 1.4f;
        [SerializeField] protected float hitStaggerDuration = 0.45f;

        [Header("Parámetros del Animator")]
        [SerializeField] protected string speedParam = "Speed";
        [SerializeField] protected string isCrawlingParam = "IsCrawling";
        [SerializeField] protected string roarTriggerParam = "Roar";
        [SerializeField] protected string attackTriggerParam = "Attack";
        [SerializeField] protected string attackIndexParam = "AttackIndex";
        [SerializeField] protected string hitTriggerParam = "Hit";
        [SerializeField] protected string dieTriggerParam = "Die";

        private const float TargetSearchInterval = 1f;

        protected EnemyState currentState = EnemyState.Idle;
        protected float stateTimer;
        protected float lastAttackTime = -999f;
        protected bool hasAlerted;
        private float nextTargetSearchTime;

        private int speedHash, isCrawlingHash, roarHash, attackHash, attackIndexHash, hitHash, dieHash;

        public EnemyState CurrentState => currentState;
        public Transform Target => target;

        /// <summary>Modo de locomoción lenta (arrastre). Sobrescribir para enemigos que no se arrastran.</summary>
        protected virtual bool IsCrawling => stats.IsCriticalHealth;

        protected virtual void Awake()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (stats == null) stats = GetComponent<EnemyStatsBase>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (enemyAudio == null) enemyAudio = GetComponent<EnemyAudioBase>();

            speedHash = Animator.StringToHash(speedParam);
            isCrawlingHash = Animator.StringToHash(isCrawlingParam);
            roarHash = Animator.StringToHash(roarTriggerParam);
            attackHash = Animator.StringToHash(attackTriggerParam);
            attackIndexHash = Animator.StringToHash(attackIndexParam);
            hitHash = Animator.StringToHash(hitTriggerParam);
            dieHash = Animator.StringToHash(dieTriggerParam);

            if (stats == null) Debug.LogError($"[{GetType().Name}] Falta un componente EnemyStatsBase en '{name}'.", this);
        }

        protected virtual void OnEnable()
        {
            if (stats == null) return;
            stats.OnTakeDamage += HandleTakeDamage;
            stats.OnDeath += HandleDeath;
        }

        protected virtual void OnDisable()
        {
            if (stats == null) return;
            stats.OnTakeDamage -= HandleTakeDamage;
            stats.OnDeath -= HandleDeath;
        }

        protected virtual void Start()
        {
            if (target == null) FindTarget();
            agent.speed = runSpeed;
            agent.stoppingDistance = attackRange * 0.85f;
        }

        protected virtual void Update()
        {
            if (currentState == EnemyState.Dead || stats == null) return;

            if (target == null)
            {
                // Búsqueda espaciada: GameObject.Find/FindObjectOfType son caros para hacerlos cada frame.
                if (Time.time < nextTargetSearchTime) return;
                nextTargetSearchTime = Time.time + TargetSearchInterval;
                FindTarget();
                if (target == null) return;
            }

            float distance = DistanceToTarget();

            switch (currentState)
            {
                case EnemyState.Idle: UpdateIdle(distance); break;
                case EnemyState.Alert: UpdateAlert(); break;
                case EnemyState.Chase: UpdateChase(distance); break;
                case EnemyState.Attack: UpdateAttack(distance); break;
                case EnemyState.TakeDamage: UpdateTakeDamage(); break;
            }

            UpdateAnimatorAndAudio();
        }

        protected virtual void UpdateIdle(float distance)
        {
            if (distance > alertRange) return;
            ChangeState(hasAlerted || alertDuration <= 0f ? EnemyState.Chase : EnemyState.Alert);
        }

        protected virtual void UpdateAlert()
        {
            RotateTowards(target.position);
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                hasAlerted = true;
                ChangeState(EnemyState.Chase);
            }
        }

        protected virtual void UpdateChase(float distance)
        {
            if (distance > alertRange * loseTargetMultiplier)
            {
                ChangeState(EnemyState.Idle);
                return;
            }

            if (distance <= attackRange)
            {
                ChangeState(EnemyState.Attack);
                return;
            }

            agent.speed = IsCrawling ? crawlSpeed : runSpeed;
            if (agent.isOnNavMesh) agent.SetDestination(target.position);
        }

        protected virtual void UpdateAttack(float distance)
        {
            RotateTowards(target.position);

            if (distance > attackRange * 1.25f)
            {
                ChangeState(EnemyState.Chase);
                return;
            }

            if (Time.time >= lastAttackTime + stats.AttackCooldown)
            {
                lastAttackTime = Time.time;
                PerformAttack(ChooseAttackVariant());
            }
        }

        protected virtual void UpdateTakeDamage()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f) ChangeState(EnemyState.Chase);
        }

        /// <summary>Distancia usada para alerta/persecución/ataque. Sobrescribir p. ej. para enemigos voladores (distancia horizontal).</summary>
        protected virtual float DistanceToTarget() => Vector3.Distance(transform.position, target.position);

        /// <summary>Índice de la variante de ataque (parámetro AttackIndex del Animator y clip de audio).</summary>
        protected virtual int ChooseAttackVariant() => 0;

        protected virtual void PerformAttack(int variant)
        {
            if (animator != null)
            {
                animator.SetInteger(attackIndexHash, variant);
                animator.SetTrigger(attackHash);
            }

            if (enemyAudio != null) enemyAudio.PlayAttack(variant);

            if (target.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(stats.AttackDamage);
                Vector3 point = GetMeleeContactPoint(out Vector3 normal);
                NotifyAttackConnected(point, normal);
            }
        }

        /// <summary>
        /// (punto de contacto, normal hacia el atacante) cada vez que un ataque llega a dañar al objetivo.
        /// Lo usa EnemyVFXHandler para el impacto; los derivados con daño propio (p. ej. láser) llaman a NotifyAttackConnected.
        /// </summary>
        public event Action<Vector3, Vector3> OnAttackConnected;

        protected void NotifyAttackConnected(Vector3 point, Vector3 normal) => OnAttackConnected?.Invoke(point, normal);

        /// <summary>Punto de la superficie del objetivo más cercano al pecho del atacante (bounds del collider, o altura fija).</summary>
        protected Vector3 GetMeleeContactPoint(out Vector3 normal)
        {
            Vector3 from = transform.position + Vector3.up * 1f;
            Vector3 point = target.position + Vector3.up * 1f;
            if (target.TryGetComponent<Collider>(out var col) && col.enabled)
            {
                Vector3 closest = col.bounds.ClosestPoint(from);
                point = (closest - from).sqrMagnitude > 0.0001f ? closest : col.bounds.center;
            }

            normal = from - point;
            normal.y = 0f;
            normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : -target.forward;
            return point;
        }

        protected void ChangeState(EnemyState newState)
        {
            currentState = newState;

            switch (newState)
            {
                case EnemyState.Idle:
                    StopAgent();
                    if (enemyAudio != null) enemyAudio.StopMovementAudio();
                    break;

                case EnemyState.Alert:
                    stateTimer = alertDuration;
                    StopAgent();
                    if (animator != null) animator.SetTrigger(roarHash);
                    if (enemyAudio != null) enemyAudio.PlayDetection();
                    break;

                case EnemyState.Chase:
                    ResumeAgent();
                    break;

                case EnemyState.Attack:
                    StopAgent();
                    if (enemyAudio != null) enemyAudio.StopMovementAudio();
                    break;

                case EnemyState.TakeDamage:
                    stateTimer = hitStaggerDuration;
                    StopAgent();
                    if (animator != null) animator.SetTrigger(hitHash);
                    if (enemyAudio != null) enemyAudio.PlayHit();
                    break;

                case EnemyState.Dead:
                    StopAgent();
                    agent.enabled = false;
                    if (TryGetComponent<Collider>(out var col)) col.enabled = false;
                    if (animator != null) animator.SetTrigger(dieHash);
                    if (enemyAudio != null) enemyAudio.PlayDeath();
                    break;
            }

            OnStateEntered(newState);
        }

        /// <summary>Gancho para que cada enemigo añada efectos propios al entrar en un estado.</summary>
        protected virtual void OnStateEntered(EnemyState state) { }

        private void UpdateAnimatorAndAudio()
        {
            bool agentMoving = agent.enabled && !agent.isStopped;
            bool crawling = IsCrawling;

            if (animator != null)
            {
                animator.SetFloat(speedHash, agentMoving ? agent.velocity.magnitude : 0f);
                animator.SetBool(isCrawlingHash, crawling);
            }

            if (enemyAudio != null)
            {
                enemyAudio.UpdateMovementAudio(agentMoving && agent.velocity.sqrMagnitude > 0.05f, crawling);
            }
        }

        protected void RotateTowards(Vector3 targetPos)
        {
            Vector3 direction = targetPos - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), rotationSpeed * Time.deltaTime);
            }
        }

        protected void StopAgent()
        {
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        }

        protected void ResumeAgent()
        {
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
        }

        private void HandleTakeDamage(float damage)
        {
            if (currentState != EnemyState.Dead) ChangeState(EnemyState.TakeDamage);
        }

        private void HandleDeath() => ChangeState(EnemyState.Dead);

        protected virtual void FindTarget()
        {
            GameObject playerGO = GameObject.Find("Player_Linux");
            if (playerGO == null)
            {
                try { playerGO = GameObject.FindWithTag("Player"); }
                catch (UnityException) { } // El tag 'Player' no existe en el proyecto.
            }

            if (playerGO != null)
            {
                target = playerGO.transform;
                return;
            }

            var controller = FindFirstObjectByType<LinuxSkeletonController>();
            if (controller != null) target = controller.transform;
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, alertRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
