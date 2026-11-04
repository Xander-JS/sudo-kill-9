using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SudoKill9.Enemies;
using SudoKill9.UI;
using Random = UnityEngine.Random;

namespace SudoKill9.Levels
{
    public enum SurvivalLevelState { Starting, Running, Clearing, Completed, Failed }

    public enum VictoryCondition
    {
        [Tooltip("Gana al llegar el temporizador a cero.")]
        SurviveTimer,
        [Tooltip("Al acabar el tiempo deja de generar y hay que eliminar a los enemigos que quedan vivos.")]
        SurviveTimerThenClear
    }

    public enum SpawnPlacement
    {
        [Tooltip("Cualquier dirección alrededor del jugador, fuera de cámara.")]
        AroundPlayer,
        [Tooltip("Por delante del jugador (fuera del cono de cámara): bloquea su avance.")]
        AheadOfPlayer,
        [Tooltip("Por detrás de la cámara: emboscada por la espalda.")]
        BehindPlayer
    }

    /// <summary>Enemigo del goteo progresivo: probabilidad relativa y momento (0-1 del nivel) a partir del que aparece.</summary>
    [Serializable]
    public class EnemySpawnEntry
    {
        public string label = "Enemy";
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
        [Tooltip("Progreso del nivel (0 = inicio, 1 = fin del tiempo) a partir del cual puede salir.")]
        [Range(0f, 1f)] public float unlockAtProgress;
        public SpawnPlacement placement = SpawnPlacement.AroundPlayer;
    }

    /// <summary>Un tipo de enemigo dentro de una horda.</summary>
    [Serializable]
    public class HordeGroup
    {
        public GameObject prefab;
        [Min(1)] public int count = 3;
        [Tooltip("Metros extra de separación respecto al centro de la horda, alejándose del jugador (p. ej. Espía cubriendo desde atrás).")]
        [Min(0f)] public float extraDistance;
    }

    /// <summary>Horda coordinada que se dispara a un tiempo concreto, contado hacia atrás desde el final del temporizador.</summary>
    [Serializable]
    public class ScriptedWave
    {
        public string label = "Horde";
        [Tooltip("Segundos ANTES del final del temporizador en que se dispara (30 = a falta de 30 s).")]
        [Min(0f)] public float secondsBeforeEnd = 30f;
        public SpawnPlacement placement = SpawnPlacement.AroundPlayer;
        [Tooltip("Número de puntos de entrada distintos; los grupos se reparten entre ellos (1 = todos juntos).")]
        [Min(1)] public int entryPoints = 1;
        [Tooltip("Retardo entre cada miembro, para que no aparezcan todos en el mismo frame.")]
        [Min(0f)] public float memberDelay = 0.2f;
        [Tooltip("Mensaje de aviso en el HUD (vacío = sin aviso).")]
        public string announcement = "HORDE INCOMING";
        public HordeGroup[] groups = Array.Empty<HordeGroup>();

        [NonSerialized] public bool fired;
    }

    /// <summary>
    /// Nivel de supervivencia por tiempo: goteo de enemigos con curva de dificultad, hordas coordinadas
    /// programadas, condición de victoria y transición a la siguiente escena con fundido.
    /// Toda la configuración vive en el inspector; SetupSurvivalLevels (Editor) genera Nivel_1 y Nivel_2.
    /// La UI la pinta SurvivalLevelHUD escuchando los eventos de este componente.
    /// </summary>
    public class SurvivalLevelController : MonoBehaviour
    {
        [Header("Nivel")]
        [SerializeField] private int levelNumber = 1;
        [Tooltip("Duración del temporizador de supervivencia en segundos.")]
        [Min(10f)] [SerializeField] private float duration = 105f;
        [Tooltip("Segundos de margen antes de que empiece a generar enemigos (cuenta atrás de inicio).")]
        [Min(0f)] [SerializeField] private float startDelay = 3f;
        [SerializeField] private VictoryCondition victoryCondition = VictoryCondition.SurviveTimer;
        [Tooltip("Segundos finales que se consideran 'tramo final' (evento OnFinalStretch y aviso en el HUD).")]
        [Min(0f)] [SerializeField] private float finalStretchSeconds = 30f;

        [Header("Referencias")]
        [Tooltip("Vacío = se busca el LinuxCombatIK de la escena, o 'Player_Linux'.")]
        [SerializeField] private Transform player;
        [Tooltip("Vacío = Camera.main.")]
        [SerializeField] private Camera viewCamera;
        [Tooltip("Puntos de aparición fijos (opcional). Si no hay ninguno válido se usa el anillo alrededor del jugador.")]
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [Tooltip("Padre de las instancias generadas (vacío = se crea 'SpawnedEnemies').")]
        [SerializeField] private Transform enemiesParent;

        [Header("Goteo progresivo")]
        [SerializeField] private EnemySpawnEntry[] enemyPool = Array.Empty<EnemySpawnEntry>();
        [Tooltip("X: progreso del nivel (0-1). Y: segundos entre tandas.")]
        [SerializeField] private AnimationCurve spawnInterval = new AnimationCurve(new Keyframe(0f, 6f), new Keyframe(0.7f, 3.5f), new Keyframe(1f, 1.2f));
        [Tooltip("X: progreso del nivel (0-1). Y: enemigos por tanda (se redondea).")]
        [SerializeField] private AnimationCurve batchSize = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 3f));
        [Tooltip("X: progreso del nivel (0-1). Y: máximo de enemigos vivos a la vez para el goteo (las hordas pueden superarlo).")]
        [SerializeField] private AnimationCurve maxAlive = new AnimationCurve(new Keyframe(0f, 4f), new Keyframe(1f, 14f));

        [Header("Hordas programadas")]
        [SerializeField] private ScriptedWave[] scriptedWaves = Array.Empty<ScriptedWave>();
        [Tooltip("Radio en el que se dispersan los miembros de una horda alrededor de su punto de entrada.")]
        [Min(0.5f)] [SerializeField] private float hordeSpread = 3.5f;

        [Header("Posición de aparición")]
        [SerializeField] private float minSpawnDistance = 18f;
        [SerializeField] private float maxSpawnDistance = 30f;
        [Tooltip("Probabilidad (0-1) de intentar primero un punto fijo de 'spawnPoints'.")]
        [Range(0f, 1f)] [SerializeField] private float spawnPointChance = 0.5f;
        [Tooltip("Margen del viewport (fracción de pantalla) que también cuenta como 'visible'.")]
        [Range(0f, 0.5f)] [SerializeField] private float viewportMargin = 0.1f;
        [SerializeField] private float navMeshSampleRadius = 4f;
        [Min(1)] [SerializeField] private int maxPlacementAttempts = 16;

        [Header("Victoria y transición")]
        [Tooltip("Mensaje del panel de nivel completado.")]
        [SerializeField] private string completedMessage = "LEVEL 1 COMPLETED!";
        [Tooltip("Escena a la que se pasa al ganar (debe estar en Build Settings).")]
        [SerializeField] private string nextSceneName = "Nivel_2";
        [Tooltip("Escena de respaldo si 'nextSceneName' aún no existe en Build Settings (p. ej. Nivel_3 en desarrollo).")]
        [SerializeField] private string fallbackSceneName = "Inicio";
        [Tooltip("Segundos mínimos que se muestra el panel antes de aceptar la tecla de continuar.")]
        [Min(0f)] [SerializeField] private float minPanelTime = 1.2f;
        [Tooltip("Segundos tras los que se avanza solo (0 = esperar a la tecla).")]
        [Min(0f)] [SerializeField] private float autoAdvanceDelay = 5f;
        [SerializeField] private Key continueKey = Key.Space;
        [SerializeField] private float fadeDuration = 0.6f;
        [Tooltip("Segundos tras la muerte del jugador antes de poder reintentar.")]
        [SerializeField] private float defeatPanelDelay = 1.5f;

        [Header("Eventos")]
        public UnityEvent onLevelStarted;
        public UnityEvent onFinalStretch;
        public UnityEvent onTimerExpired;
        public UnityEvent onLevelCompleted;
        public UnityEvent onLevelFailed;

        /// <summary>Texto de aviso de horda (para el HUD).</summary>
        public event Action<string> OnAnnouncement;

        public SurvivalLevelState State { get; private set; } = SurvivalLevelState.Starting;
        public int LevelNumber => levelNumber;
        public float Duration => duration;
        public float Elapsed { get; private set; }
        public float RemainingTime => Mathf.Max(0f, duration - Elapsed);
        public float StartCountdown { get; private set; }
        public float Progress => Mathf.Clamp01(Elapsed / duration);
        public bool InFinalStretch => RemainingTime <= finalStretchSeconds;
        public int AliveCount => alive.Count;
        public string CompletedMessage => completedMessage;
        public VictoryCondition Victory => victoryCondition;
        public Key ContinueKey => continueKey;
        /// <summary>Verdadero cuando el panel final ya acepta la tecla de continuar.</summary>
        public bool CanContinue { get; private set; }

        private readonly List<EnemyStatsBase> alive = new List<EnemyStatsBase>();
        private float nextTrickleTime;
        private bool finalStretchRaised;
        private bool leaving;

        // ------------------------------------------------------------------ Ciclo de vida

        private void Awake()
        {
            Time.timeScale = 1f; // por si se llega desde una pantalla que lo dejó pausado
            if (viewCamera == null) viewCamera = Camera.main;
            if (player == null) FindPlayer();
            if (enemiesParent == null) enemiesParent = new GameObject("SpawnedEnemies").transform;
            foreach (var wave in scriptedWaves) wave.fired = false;
        }

        private void OnEnable() => LinuxCombatIK.OnPlayerDied += HandlePlayerDied;
        private void OnDisable() => LinuxCombatIK.OnPlayerDied -= HandlePlayerDied;

        private IEnumerator Start()
        {
            if (player == null) Debug.LogError("[SurvivalLevelController] No se encontró al jugador (LinuxCombatIK / Player_Linux).", this);

            for (StartCountdown = startDelay; StartCountdown > 0f; StartCountdown -= Time.deltaTime)
                yield return null;
            StartCountdown = 0f;

            State = SurvivalLevelState.Running;
            nextTrickleTime = 0f;
            onLevelStarted?.Invoke();
        }

        private void Update()
        {
            switch (State)
            {
                case SurvivalLevelState.Running: UpdateRunning(); break;
                case SurvivalLevelState.Clearing:
                    PruneDead();
                    if (alive.Count == 0) Complete();
                    break;
                case SurvivalLevelState.Completed:
                    if (CanContinue && Keyboard.current != null && Keyboard.current[continueKey].wasPressedThisFrame) GoToNextLevel();
                    break;
                case SurvivalLevelState.Failed:
                    if (CanContinue && Keyboard.current != null && Keyboard.current[continueKey].wasPressedThisFrame)
                        SceneFader.LoadScene(SceneManager.GetActiveScene().name, fadeDuration);
                    break;
            }
        }

        private void UpdateRunning()
        {
            Elapsed += Time.deltaTime;
            PruneDead();

            if (!finalStretchRaised && InFinalStretch)
            {
                finalStretchRaised = true;
                onFinalStretch?.Invoke();
                OnAnnouncement?.Invoke($"FINAL {Mathf.RoundToInt(finalStretchSeconds)} SECONDS");
            }

            // Hordas programadas
            foreach (var wave in scriptedWaves)
            {
                if (wave.fired || RemainingTime > wave.secondsBeforeEnd) continue;
                wave.fired = true;
                StartCoroutine(SpawnHorde(wave));
            }

            // Goteo progresivo según la curva de dificultad
            if (Elapsed >= nextTrickleTime)
            {
                float p = Progress;
                nextTrickleTime = Elapsed + Mathf.Max(0.25f, spawnInterval.Evaluate(p));
                int cap = Mathf.RoundToInt(maxAlive.Evaluate(p));
                int count = Mathf.Max(1, Mathf.RoundToInt(batchSize.Evaluate(p)));
                for (int i = 0; i < count && alive.Count < cap; i++)
                {
                    var entry = PickPoolEntry(p);
                    if (entry == null) break;
                    if (TryFindSpawnCenter(entry.placement, out Vector3 pos)) SpawnEnemy(entry.prefab, pos);
                }
            }

            if (Elapsed >= duration) OnTimerFinished();
        }

        private void OnTimerFinished()
        {
            onTimerExpired?.Invoke();
            if (victoryCondition == VictoryCondition.SurviveTimerThenClear)
            {
                PruneDead();
                if (alive.Count > 0)
                {
                    State = SurvivalLevelState.Clearing;
                    OnAnnouncement?.Invoke("ELIMINATE THE REMAINING ENEMIES");
                    return;
                }
            }
            Complete();
        }

        // ------------------------------------------------------------------ Victoria / derrota

        private void Complete()
        {
            if (State == SurvivalLevelState.Completed || State == SurvivalLevelState.Failed) return;
            State = SurvivalLevelState.Completed;
            StopAllCoroutines(); // corta hordas a medio generar
            FreezeEnemies();
            onLevelCompleted?.Invoke();
            StartCoroutine(CompletionFlow());
        }

        private IEnumerator CompletionFlow()
        {
            yield return new WaitForSecondsRealtime(minPanelTime);
            CanContinue = true;
            if (autoAdvanceDelay <= 0f) yield break;
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, autoAdvanceDelay - minPanelTime));
            GoToNextLevel();
        }

        private void HandlePlayerDied()
        {
            if (State == SurvivalLevelState.Completed || State == SurvivalLevelState.Failed) return;
            State = SurvivalLevelState.Failed;
            StopAllCoroutines();
            onLevelFailed?.Invoke();
            StartCoroutine(EnableRetry());
        }

        private IEnumerator EnableRetry()
        {
            yield return new WaitForSecondsRealtime(defeatPanelDelay);
            CanContinue = true;
        }

        private void GoToNextLevel()
        {
            if (leaving) return;
            leaving = true;

            string target = nextSceneName;
            if (string.IsNullOrEmpty(target) || !Application.CanStreamedLevelBeLoaded(target))
            {
                Debug.LogWarning($"[SurvivalLevelController] La escena '{nextSceneName}' no está en Build Settings; se carga '{fallbackSceneName}'.", this);
                target = fallbackSceneName;
            }
            SceneFader.LoadScene(target, fadeDuration);
        }

        /// <summary>Congela a los enemigos vivos: sin IA, sin movimiento y con la animación detenida.</summary>
        private void FreezeEnemies()
        {
            PruneDead();
            foreach (var stats in alive)
            {
                if (stats.TryGetComponent<EnemyAIBase>(out var ai)) ai.enabled = false;
                if (stats.TryGetComponent<NavMeshAgent>(out var agent) && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                foreach (var anim in stats.GetComponentsInChildren<Animator>()) anim.speed = 0f;
                if (stats.TryGetComponent<EnemyAudioBase>(out var audio)) audio.StopMovementAudio();
            }
        }

        // ------------------------------------------------------------------ Generación

        private IEnumerator SpawnHorde(ScriptedWave wave)
        {
            if (!string.IsNullOrEmpty(wave.announcement)) OnAnnouncement?.Invoke(wave.announcement);

            int entries = Mathf.Max(1, wave.entryPoints);
            var centers = new Vector3[entries];
            for (int i = 0; i < entries; i++)
            {
                if (!TryFindSpawnCenter(wave.placement, out centers[i]))
                    centers[i] = i > 0 ? centers[0] : player.position - player.forward * minSpawnDistance;
            }

            int groupIndex = 0;
            foreach (var group in wave.groups)
            {
                if (group.prefab == null) continue;
                Vector3 center = centers[groupIndex++ % entries];
                Vector3 away = center - player.position;
                away.y = 0f;
                away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
                Vector3 groupCenter = center + away * group.extraDistance;

                for (int i = 0; i < group.count; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * hordeSpread;
                    Vector3 candidate = groupCenter + new Vector3(offset.x, 0f, offset.y);
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas) &&
                        !NavMesh.SamplePosition(center, out hit, navMeshSampleRadius, NavMesh.AllAreas))
                        continue;
                    SpawnEnemy(group.prefab, hit.position);
                    if (wave.memberDelay > 0f) yield return new WaitForSeconds(wave.memberDelay);
                }
            }
        }

        private void SpawnEnemy(GameObject prefab, Vector3 position)
        {
            if (prefab == null || player == null) return;

            Vector3 look = player.position - position;
            look.y = 0f;
            Quaternion rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;

            GameObject go = Instantiate(prefab, position, rot, enemiesParent);
            if (go.TryGetComponent<NavMeshAgent>(out var agent) && agent.enabled) agent.Warp(position);
            if (go.TryGetComponent<EnemyAIBase>(out var ai)) ai.SetTarget(player, true);

            if (go.TryGetComponent<EnemyStatsBase>(out var stats)) alive.Add(stats); // las bajas se purgan en PruneDead
        }

        private EnemySpawnEntry PickPoolEntry(float progress)
        {
            float total = 0f;
            foreach (var e in enemyPool)
                if (e.prefab != null && progress >= e.unlockAtProgress) total += e.weight;
            if (total <= 0f) return null;

            float roll = Random.value * total;
            foreach (var e in enemyPool)
            {
                if (e.prefab == null || progress < e.unlockAtProgress) continue;
                roll -= e.weight;
                if (roll <= 0f) return e;
            }
            return null;
        }

        /// <summary>Punto de NavMesh fuera de cámara: primero un spawn point fijo (si toca), si no el anillo alrededor del jugador.</summary>
        private bool TryFindSpawnCenter(SpawnPlacement placement, out Vector3 position)
        {
            position = default;
            if (player == null) return false;

            if (spawnPoints.Length > 0 && Random.value < spawnPointChance && TryPickSpawnPoint(placement, out position))
                return true;

            Vector3 forward = GetReferenceForward();
            for (int i = 0; i < maxPlacementAttempts; i++)
            {
                float angle = placement switch
                {
                    // ±(50..85)º: por delante, pero fuera del cono horizontal de la cámara
                    SpawnPlacement.AheadOfPlayer => Random.Range(50f, 85f) * (Random.value < 0.5f ? -1f : 1f),
                    SpawnPlacement.BehindPlayer => 180f + Random.Range(-60f, 60f),
                    _ => Random.Range(0f, 360f)
                };
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
                Vector3 candidate = player.position + dir * Random.Range(minSpawnDistance, maxSpawnDistance);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas)) continue;
                if (IsVisible(hit.position)) continue;
                if ((hit.position - player.position).sqrMagnitude < minSpawnDistance * minSpawnDistance * 0.6f) continue;

                position = hit.position;
                return true;
            }

            return spawnPoints.Length > 0 && TryPickSpawnPoint(placement, out position);
        }

        private bool TryPickSpawnPoint(SpawnPlacement placement, out Vector3 position)
        {
            position = default;
            Vector3 forward = GetReferenceForward();
            float maxDist = maxSpawnDistance * 2f;
            int start = Random.Range(0, spawnPoints.Length);

            for (int n = 0; n < spawnPoints.Length; n++)
            {
                Transform sp = spawnPoints[(start + n) % spawnPoints.Length];
                if (sp == null) continue;

                Vector3 delta = sp.position - player.position;
                delta.y = 0f;
                float dist = delta.magnitude;
                if (dist < minSpawnDistance || dist > maxDist || IsVisible(sp.position)) continue;

                float dot = dist > 0.01f ? Vector3.Dot(delta / dist, forward) : 0f;
                if (placement == SpawnPlacement.AheadOfPlayer && dot < 0f) continue;
                if (placement == SpawnPlacement.BehindPlayer && dot > 0f) continue;

                if (!NavMesh.SamplePosition(sp.position, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas)) continue;
                position = hit.position;
                return true;
            }
            return false;
        }

        private Vector3 GetReferenceForward()
        {
            Vector3 f = viewCamera != null ? viewCamera.transform.forward : player.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
        }

        private bool IsVisible(Vector3 worldPos)
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return false;

            Vector3 vp = viewCamera.WorldToViewportPoint(worldPos + Vector3.up);
            float m = viewportMargin;
            return vp.z > 0f && vp.x > -m && vp.x < 1f + m && vp.y > -m && vp.y < 1f + m;
        }

        private void PruneDead() => alive.RemoveAll(s => s == null || s.IsDead);

        private void FindPlayer()
        {
            var ik = FindFirstObjectByType<LinuxCombatIK>();
            if (ik != null) { player = ik.transform; return; }
            var go = GameObject.Find("Player_Linux");
            if (go != null) player = go.transform;
        }

        private void OnDrawGizmosSelected()
        {
            Transform p = player;
            if (p == null) return;
            Gizmos.color = new Color(0.2f, 0.95f, 0.45f, 0.6f);
            DrawCircle(p.position, minSpawnDistance);
            Gizmos.color = new Color(0.95f, 0.3f, 0.2f, 0.6f);
            DrawCircle(p.position, maxSpawnDistance);
        }

        private static void DrawCircle(Vector3 c, float r)
        {
            Vector3 prev = c + Vector3.right * r;
            for (int i = 1; i <= 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                Vector3 next = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
