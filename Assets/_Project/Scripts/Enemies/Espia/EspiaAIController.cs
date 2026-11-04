using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using SudoKill9.Combat;

namespace SudoKill9.Enemies.Espia
{
    /// <summary>
    /// IA de espia_opt: tirador a distancia que dispara un láser por el ojo.
    /// Reutiliza la máquina de estados de EnemyAIBase y solo cambia la parte de combate:
    ///   - Entra en Ataque a attackRange (10 m) si tiene línea de visión; si un obstáculo tapa al jugador, sigue acercándose.
    ///   - Mantiene la distancia: si el jugador se mete a menos de retreatDistance (4 m), se retira hasta stoppingDistance (7.5 m).
    ///   - Alterna 2 disparos (AttackIndex 0 = rápido, 1 = cargado). El daño llega por raycast desde el ojo tras fireDelay,
    ///     así que una pared entre ambos bloquea el disparo.
    /// </summary>
    [RequireComponent(typeof(EspiaStats), typeof(EspiaAudio))]
    public class EspiaAIController : EnemyAIBase
    {
        public const int AttackVariants = 2; // Attack_EyeLaser, Attack_EyeLaserCharged en EspiaController.controller

        [Header("Combate a Distancia")]
        [Tooltip("Distancia preferida de combate: NavMeshAgent.stoppingDistance y destino de la retirada.")]
        [SerializeField] private float stoppingDistance = 7.5f;
        [Tooltip("Si el jugador se acerca más que esto, el espía retrocede en lugar de disparar.")]
        [SerializeField] private float retreatDistance = 4f;
        [Tooltip("Segundos mínimos entre dos retiradas (evita que huya sin parar si lo arrinconan).")]
        [SerializeField] private float retreatCooldown = 2f;
        [SerializeField] private float maxRetreatTime = 2.5f;

        [Header("Láser")]
        [Tooltip("Punto de salida del haz (hijo del hueso Head). Si está vacío se usa el hueso Head o una altura fija.")]
        [SerializeField] private Transform eyeMuzzle;
        [SerializeField] private float fallbackEyeHeight = 1.6f;
        [Tooltip("Altura sobre el pivote del jugador a la que apunta el haz.")]
        [SerializeField] private float targetAimHeight = 1.1f;
        [Tooltip("Alcance real del raycast. Debe ser ≥ attackRange × 1.25 (distancia a la que deja de atacar).")]
        [SerializeField] private float laserMaxDistance = 14f;
        [Tooltip("Retardo entre el inicio de la animación de disparo y el impacto (sincroniza haz, audio y daño).")]
        [SerializeField] private float fireDelay = 0.2f;
        [SerializeField] private LayerMask laserMask = ~0;
        [SerializeField] private Color laserColor = new Color(1f, 0.1f, 0.15f, 1f);
        [SerializeField] private float laserWidth = 0.05f;
        [SerializeField] private float laserVisibleTime = 0.12f;

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        private int attackHashLocal, attackIndexHashLocal;
        private int nextVariant;
        private bool retreating;
        private float retreatStartTime = -999f;
        private Coroutine shotRoutine;
        private LineRenderer laserLine;

        public float RunSpeed => runSpeed;
        public float AttackRange => attackRange;
        public float StoppingDistance => stoppingDistance;

        /// <summary>Rangos de tirador. Vida/daño son los del Virus (EspiaStats); aquí solo distancias y ritmo.</summary>
        public void ApplyBalanceDefaults()
        {
            alertRange = 16f;           // detecta antes que el Virus (14): es un vigía
            attackRange = 10f;          // Virus 2.2 → dispara desde 10 m
            loseTargetMultiplier = 1.6f;
            runSpeed = 4.5f;            // no necesita correr tanto como el Virus (5.5)
            crawlSpeed = 2.2f;
            rotationSpeed = 8f;
            alertDuration = 0.8f;       // "fija el blanco" en vez del rugido de 1.4 s
            hitStaggerDuration = 0.45f; // igual que el Virus
            stoppingDistance = 7.5f;
            retreatDistance = 4f;
            laserMaxDistance = attackRange * 1.4f;
        }

        private void Reset() => ApplyBalanceDefaults();

        private void OnValidate()
        {
            retreatDistance = Mathf.Clamp(retreatDistance, 0f, stoppingDistance - 0.5f);
            stoppingDistance = Mathf.Clamp(stoppingDistance, retreatDistance + 0.5f, attackRange);
            laserMaxDistance = Mathf.Max(laserMaxDistance, attackRange * 1.25f);
        }

        protected override void Awake()
        {
            base.Awake();
            attackHashLocal = Animator.StringToHash(attackTriggerParam);
            attackIndexHashLocal = Animator.StringToHash(attackIndexParam);

            if (eyeMuzzle == null && animator != null && animator.isHuman)
            {
                eyeMuzzle = animator.GetBoneTransform(HumanBodyBones.Head);
            }
            CreateLaserLine();
        }

        protected override void Start()
        {
            base.Start();
            // EnemyAIBase pone attackRange × 0.85; el tirador se para en su distancia preferida.
            agent.stoppingDistance = stoppingDistance;
        }

        // ---------------------------------------------------------------- Estados

        protected override void UpdateChase(float distance)
        {
            // En rango pero con la visión tapada: seguir acercándose (rodeando el obstáculo) en lugar de disparar a la pared.
            if (distance <= attackRange && distance <= alertRange * loseTargetMultiplier && !HasLineOfSight())
            {
                agent.speed = IsCrawling ? crawlSpeed : runSpeed;
                agent.stoppingDistance = retreatDistance;
                if (agent.isOnNavMesh) agent.SetDestination(target.position);
                return;
            }

            agent.stoppingDistance = stoppingDistance;
            base.UpdateChase(distance);
        }

        protected override void UpdateAttack(float distance)
        {
            if (distance > attackRange * 1.25f)
            {
                ChangeState(EnemyState.Chase);
                return;
            }

            if (retreating)
            {
                UpdateRetreat(distance);
                return;
            }

            bool shooting = shotRoutine != null;
            if (!shooting && distance < retreatDistance && Time.time >= retreatStartTime + retreatCooldown && TryStartRetreat())
            {
                return;
            }

            RotateTowards(target.position);

            if (!shooting && !HasLineOfSight())
            {
                ChangeState(EnemyState.Chase); // UpdateChase lo acercará hasta recuperar la visión
                return;
            }

            if (!shooting && Time.time >= lastAttackTime + stats.AttackCooldown)
            {
                lastAttackTime = Time.time;
                PerformAttack(ChooseAttackVariant());
            }
        }

        protected override void OnStateEntered(EnemyState state)
        {
            if (state == EnemyState.Attack) return;

            // Cualquier interrupción (daño, muerte, pérdida del blanco) cancela disparo y retirada.
            CancelShot();
            retreating = false;
            if (agent.enabled) agent.stoppingDistance = stoppingDistance;
        }

        // Alterna estrictamente 0, 1, 0, 1… para que los dos disparos (anim + audio) nunca se repitan seguidos.
        protected override int ChooseAttackVariant()
        {
            int variant = nextVariant;
            nextVariant = (nextVariant + 1) % AttackVariants;
            return variant;
        }

        protected override void PerformAttack(int variant)
        {
            // No se llama a base.PerformAttack: allí el daño es instantáneo y sin comprobar obstáculos.
            if (animator != null)
            {
                animator.SetInteger(attackIndexHashLocal, variant);
                animator.SetTrigger(attackHashLocal);
            }
            shotRoutine = StartCoroutine(FireAfterDelay(variant));
        }

        // ---------------------------------------------------------------- Retirada

        private bool TryStartRetreat()
        {
            if (!agent.isOnNavMesh) return false;

            Vector3 away = transform.position - target.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();

            // Directamente hacia atrás; si hay pared, probar en diagonal a ambos lados.
            float[] angles = { 0f, 45f, -45f, 90f, -90f };
            foreach (float angle in angles)
            {
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * away;
                Vector3 candidate = target.position + dir * stoppingDistance;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;
                if (Vector3.Distance(hit.position, target.position) < retreatDistance + 1f) continue;

                retreating = true;
                retreatStartTime = Time.time;
                agent.speed = IsCrawling ? crawlSpeed : runSpeed;
                agent.stoppingDistance = 0.3f;
                ResumeAgent();
                agent.SetDestination(hit.position);
                return true;
            }
            return false; // Arrinconado: se queda y dispara.
        }

        private void UpdateRetreat(float distance)
        {
            bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f;
            bool farEnough = distance >= stoppingDistance * 0.9f;
            bool timedOut = Time.time >= retreatStartTime + maxRetreatTime;
            if (!arrived && !farEnough && !timedOut) return;

            retreating = false;
            StopAgent();
            agent.stoppingDistance = stoppingDistance;
        }

        // ---------------------------------------------------------------- Láser

        private IEnumerator FireAfterDelay(int variant)
        {
            if (fireDelay > 0f) yield return new WaitForSeconds(fireDelay);
            shotRoutine = null;
            if (currentState != EnemyState.Attack || target == null) yield break;

            if (enemyAudio != null) enemyAudio.PlayAttack(variant);

            Vector3 origin = EyePosition;
            Vector3 aim = AimPoint;
            Vector3 dir = (aim - origin).normalized;
            float distToAim = Vector3.Distance(origin, aim);

            Vector3 end = origin + dir * laserMaxDistance;
            bool blocked = TryGetFirstHit(origin, dir, laserMaxDistance, out RaycastHit hit);
            bool hitsTarget = blocked ? IsTargetCollider(hit.collider) : distToAim <= laserMaxDistance;

            if (blocked) end = hit.point;
            else if (hitsTarget) end = aim; // jugador sin collider: el haz termina en él

            if (hitsTarget && target.TryGetComponent<IDamageable>(out var damageable) && !damageable.IsDead)
            {
                damageable.TakeDamage(stats.AttackDamage);
                NotifyAttackConnected(end, -dir); // impacto del haz: chispas de vuelta hacia el espía
            }

            if (laserLine != null) StartCoroutine(FlashLaser(origin, end));
        }

        private IEnumerator FlashLaser(Vector3 from, Vector3 to)
        {
            laserLine.SetPosition(0, from);
            laserLine.SetPosition(1, to);
            laserLine.enabled = true;
            yield return new WaitForSeconds(laserVisibleTime);
            laserLine.enabled = false;
        }

        private void CancelShot()
        {
            if (shotRoutine == null) return;
            StopCoroutine(shotRoutine);
            shotRoutine = null;
        }

        private Vector3 EyePosition => eyeMuzzle != null ? eyeMuzzle.position : transform.position + Vector3.up * fallbackEyeHeight;
        private Vector3 AimPoint => target.position + Vector3.up * targetAimHeight;

        private bool HasLineOfSight()
        {
            if (target == null) return false;
            Vector3 origin = EyePosition;
            Vector3 toAim = AimPoint - origin;
            float dist = toAim.magnitude;
            if (dist < 0.01f) return true;

            // Visible si nada se interpone o si lo primero que toca el rayo es el propio jugador.
            if (!TryGetFirstHit(origin, toAim / dist, dist, out RaycastHit hit)) return true;
            return IsTargetCollider(hit.collider);
        }

        /// <summary>Primer impacto del rayo ignorando los colliders del propio espía.</summary>
        private bool TryGetFirstHit(Vector3 origin, Vector3 dir, float maxDist, out RaycastHit closest)
        {
            closest = default;
            int count = Physics.RaycastNonAlloc(origin, dir, HitBuffer, maxDist, laserMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = HitBuffer[i];
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    closest = h;
                    found = true;
                }
            }
            return found;
        }

        private bool IsTargetCollider(Collider col) => target != null && col.transform.IsChildOf(target);

        private void CreateLaserLine()
        {
            // Mismo enfoque que el trazador de PlayerShooter: material creado en runtime, sin assets extra.
            var go = new GameObject("EyeLaser");
            go.transform.SetParent(transform, false);
            laserLine = go.AddComponent<LineRenderer>();
            laserLine.useWorldSpace = true;
            laserLine.positionCount = 2;
            laserLine.startWidth = laserWidth;
            laserLine.endWidth = laserWidth * 0.6f;
            laserLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laserLine.receiveShadows = false;

            Shader unlit = Shader.Find("Sprites/Default");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");
            if (unlit != null) laserLine.material = new Material(unlit);
            laserLine.startColor = laserColor;
            laserLine.endColor = laserColor;
            laserLine.enabled = false;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = new Color(0.2f, 0.8f, 1f);
            Gizmos.DrawWireSphere(transform.position, stoppingDistance);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, retreatDistance);
        }
    }
}
