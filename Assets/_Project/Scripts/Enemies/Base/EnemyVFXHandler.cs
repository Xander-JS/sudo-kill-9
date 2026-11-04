using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace SudoKill9.Enemies
{
    public enum MovementVfxMode
    {
        /// <summary>Polvo / micro-chispas a ras de suelo (enemigos que caminan).</summary>
        GroundDust,
        /// <summary>Estela de propulsión hacia abajo bajo el cuerpo (enemigos que flotan).</summary>
        Thruster,
    }

    /// <summary>
    /// VFX de combate comunes a todos los enemigos (Virus, escudo_opt, volador_opt, espia_opt). No toca el código de cada villano:
    ///   - Muerte: EnemyStatsBase.OnDeath → partículas de fragmentación digital SIN padre (StopAction = Destroy) en el centro del cuerpo,
    ///     y el enemigo se encoge justo antes del Destroy que programa EnemyStatsBase (despawnDelay).
    ///   - Movimiento: polvo o propulsión que solo emite mientras NavMeshAgent.velocity supera moveSpeedThreshold.
    ///   - Impacto: EnemyAIBase.OnAttackConnected → ráfaga corta (6–9 chispas, ≤ 0.2 s) en el punto de contacto.
    ///   - Daño recibido: EnemyStatsBase.OnTakeDamage (balas de Linux) → la misma ráfaga en el centro del cuerpo.
    /// Los slots de prefab son opcionales: vacíos = sistemas procedurales construidos en Awake (sin assets).
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyVFXHandler : MonoBehaviour
    {
        [Header("Referencias (se resuelven solas si están vacías)")]
        [SerializeField] private EnemyStatsBase stats;
        [SerializeField] private EnemyAIBase ai;
        [SerializeField] private NavMeshAgent agent;

        [Header("Prefabs opcionales (vacío = procedural)")]
        [Tooltip("One-shot. Se instancia sin padre en el centro del cuerpo.")]
        [SerializeField] private ParticleSystem deathVfxPrefab;
        [Tooltip("En bucle. Se instancia como hijo; solo su emisión se enciende/apaga.")]
        [SerializeField] private ParticleSystem walkDustVfx;
        [Tooltip("One-shot. Se instancia sin padre en el punto de contacto, con +Z hacia el atacante.")]
        [SerializeField] private ParticleSystem attackHitVfx;

        [Header("Estilo")]
        [Tooltip("Color de cada villano: chispas de muerte e impacto, micro-chispas del polvo y propulsión.")]
        [SerializeField] private Color accentColor = new Color(0.25f, 0.85f, 1f);
        [SerializeField] private Color dustColor = new Color(0.55f, 0.6f, 0.62f, 0.3f);

        [Header("Movimiento")]
        [SerializeField] private MovementVfxMode movementMode = MovementVfxMode.GroundDust;
        [Tooltip("Velocidad del NavMeshAgent (m/s) a partir de la cual cuenta como 'en movimiento'.")]
        [SerializeField] private float moveSpeedThreshold = 0.35f;
        [Tooltip("Partículas/s mientras se mueve.")]
        [SerializeField] private float movingEmissionRate = 10f;
        [Tooltip("Partículas/s parado (p. ej. propulsión al flotar). 0 = nada parado.")]
        [SerializeField] private float idleEmissionRate = 0f;

        [Header("Muerte")]
        [Tooltip("Encoge al enemigo en los últimos segundos antes del Destroy (desaparición limpia, sin 'pop').")]
        [SerializeField] private bool shrinkBeforeDespawn = true;
        [SerializeField] private float shrinkDuration = 0.3f;

        private static Texture2D softTexture;
        private static Material softMaterial, pixelMaterial;

        private ParticleSystem moveSystem;
        private ParticleSystem deathTemplate, hitTemplate;
        private Transform templateRoot;
        private float currentRate = -1f;

        public MovementVfxMode MovementMode => movementMode;

        /// <summary>Valores por villano (los aplica Tools/Setup Enemy VFX).</summary>
        public void Configure(Color accent, MovementVfxMode mode, float idleRate)
        {
            accentColor = accent;
            movementMode = mode;
            idleEmissionRate = idleRate;
            movingEmissionRate = mode == MovementVfxMode.Thruster ? 18f : 10f;
        }

        private void Awake()
        {
            if (stats == null) stats = GetComponent<EnemyStatsBase>();
            if (ai == null) ai = GetComponent<EnemyAIBase>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();

            // Plantillas inactivas: cada efecto one-shot es un Instantiate barato de algo ya configurado.
            templateRoot = new GameObject("VFX_Templates").transform;
            templateRoot.SetParent(transform, false);
            templateRoot.gameObject.SetActive(false);

            deathTemplate = deathVfxPrefab != null ? deathVfxPrefab : BuildDeathVfx(templateRoot, accentColor);
            hitTemplate = attackHitVfx != null ? attackHitVfx : BuildHitVfx(templateRoot, accentColor);

            moveSystem = walkDustVfx != null
                ? Instantiate(walkDustVfx, transform)
                : movementMode == MovementVfxMode.Thruster ? BuildThrusterVfx(transform, accentColor) : BuildDustVfx(transform, dustColor, accentColor);
            moveSystem.transform.localPosition = MovementEmitterLocalPosition();
            SetMoveRate(0f);
            moveSystem.Play();
        }

        private void OnEnable()
        {
            if (stats != null) { stats.OnDeath += HandleDeath; stats.OnTakeDamage += HandleDamaged; }
            if (ai != null) ai.OnAttackConnected += HandleAttackConnected;
        }

        private void OnDisable()
        {
            if (stats != null) { stats.OnDeath -= HandleDeath; stats.OnTakeDamage -= HandleDamaged; }
            if (ai != null) ai.OnAttackConnected -= HandleAttackConnected;
        }

        private void LateUpdate()
        {
            if (moveSystem == null || stats == null || stats.IsDead) return;
            bool moving = agent != null && agent.enabled && agent.velocity.sqrMagnitude > moveSpeedThreshold * moveSpeedThreshold;
            SetMoveRate(moving ? movingEmissionRate : idleEmissionRate);
        }

        // ---------------------------------------------------------------- Disparadores

        private void HandleDeath()
        {
            if (moveSystem != null) moveSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting); // lo emitido se desvanece solo
            SpawnOneShot(deathTemplate, BodyCenter(), Quaternion.identity);

            if (shrinkBeforeDespawn && stats.DespawnDelay >= 0f) StartCoroutine(ShrinkBeforeDespawn(stats.DespawnDelay));
        }

        private void HandleDamaged(float amount)
        {
            if (!stats.IsDead) SpawnOneShot(hitTemplate, BodyCenter(), Quaternion.identity); // la muerte ya tiene su VFX
        }

        private void HandleAttackConnected(Vector3 point, Vector3 normal)
        {
            Quaternion rot = normal.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(normal) : Quaternion.identity;
            SpawnOneShot(hitTemplate, point, rot);
        }

        /// <summary>Instancia SIN padre: sobrevive al Destroy del enemigo y se autodestruye al terminar.</summary>
        private static void SpawnOneShot(ParticleSystem template, Vector3 position, Quaternion rotation)
        {
            if (template == null) return;
            ParticleSystem fx = Instantiate(template, position, rotation, null);
            fx.gameObject.SetActive(true);

            var main = fx.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            fx.Play(true);
            // Red de seguridad por si un prefab asignado es en bucle o tiene hijos más largos.
            Destroy(fx.gameObject, main.duration + main.startLifetime.constantMax + 2f);
        }

        private IEnumerator ShrinkBeforeDespawn(float despawnDelay)
        {
            float wait = Mathf.Max(0f, despawnDelay - shrinkDuration);
            yield return new WaitForSeconds(wait);

            Vector3 start = transform.localScale;
            for (float t = 0f; t < shrinkDuration; t += Time.deltaTime)
            {
                float k = t / shrinkDuration;
                transform.localScale = start * (1f - k * k);
                yield return null;
            }
            transform.localScale = Vector3.zero;
        }

        private void SetMoveRate(float rate)
        {
            if (Mathf.Approximately(rate, currentRate)) return; // solo toca el módulo al cambiar
            currentRate = rate;
            var emission = moveSystem.emission;
            emission.rateOverTime = rate;
        }

        private Vector3 BodyCenter()
        {
            return TryGetRendererBounds(out Bounds b) ? b.center : transform.position + Vector3.up;
        }

        private Vector3 MovementEmitterLocalPosition()
        {
            if (movementMode == MovementVfxMode.GroundDust) return Vector3.up * 0.05f / Mathf.Max(transform.lossyScale.y, 0.0001f);
            // Propulsión: bajo el punto más bajo del cuerpo (la raíz flota con el baseOffset del agente).
            return TryGetRendererBounds(out Bounds b)
                ? new Vector3(0f, transform.InverseTransformPoint(new Vector3(transform.position.x, b.min.y, transform.position.z)).y, 0f)
                : Vector3.zero;
        }

        private bool TryGetRendererBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }

        // ---------------------------------------------------------------- Sistemas procedurales

        /// <summary>Fragmentación digital: píxeles que saltan + destello + disolución ascendente (~2 s en total).</summary>
        private static ParticleSystem BuildDeathVfx(Transform parent, Color accent)
        {
            ParticleSystem root = NewSystem("VFX_Death", parent, PixelMaterial, 1.2f, false, 60);
            var main = root.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(accent, Color.white);
            main.gravityModifier = 0.6f;
            Burst(root, 28, 36);
            Shape(root, ParticleSystemShapeType.Sphere, 0.35f);
            ShrinkAndFade(root);

            ParticleSystem flash = NewSystem("Flash", root.transform, SoftMaterial, 0.15f, false, 1);
            var fMain = flash.main;
            fMain.startLifetime = 0.12f;
            fMain.startSpeed = 0f;
            fMain.startSize = 0.9f;
            fMain.startColor = WithAlpha(accent, 0.55f);
            Burst(flash, 1, 1);
            DisableShape(flash);
            ShrinkAndFade(flash);

            ParticleSystem dissolve = NewSystem("Dissolve", root.transform, PixelMaterial, 1.0f, false, 24);
            var dMain = dissolve.main;
            dMain.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            dMain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            dMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            dMain.startColor = WithAlpha(accent, 0.8f);
            dMain.gravityModifier = -0.15f; // sube: "se disuelve" hacia arriba
            var dEmission = dissolve.emission;
            dEmission.rateOverTime = 18f;
            Shape(dissolve, ParticleSystemShapeType.Cone, 0.3f, 10f, new Vector3(-90f, 0f, 0f));
            ShrinkAndFade(dissolve);
            return root;
        }

        /// <summary>Impacto: 6–9 chispas en cono hacia el atacante + destello mínimo; todo dura ≤ 0.2 s.</summary>
        private static ParticleSystem BuildHitVfx(Transform parent, Color accent)
        {
            ParticleSystem root = NewSystem("VFX_Hit", parent, PixelMaterial, 0.2f, false, 10);
            var main = root.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, accent);
            Burst(root, 6, 9);
            Shape(root, ParticleSystemShapeType.Cone, 0.02f, 35f, Vector3.zero);
            ShrinkAndFade(root);

            ParticleSystem flash = NewSystem("Flash", root.transform, SoftMaterial, 0.1f, false, 1);
            var fMain = flash.main;
            fMain.startLifetime = 0.07f;
            fMain.startSpeed = 0f;
            fMain.startSize = 0.35f;
            fMain.startColor = WithAlpha(Color.Lerp(accent, Color.white, 0.5f), 0.6f);
            Burst(flash, 1, 1);
            DisableShape(flash);
            ShrinkAndFade(flash);
            return root;
        }

        /// <summary>Nubecitas de polvo de placa base con alguna micro-chispa del color del villano. Bucle de baja emisión.</summary>
        private static ParticleSystem BuildDustVfx(Transform parent, Color dust, Color accent)
        {
            ParticleSystem ps = NewSystem("VFX_MoveDust", parent, SoftMaterial, 1f, true, 24);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(dust, WithAlpha(accent, 0.45f));
            main.gravityModifier = -0.02f;
            Shape(ps, ParticleSystemShapeType.Cone, 0.25f, 60f, new Vector3(-90f, 0f, 0f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = AlphaGradient(new[] { (0f, 0f), (0.2f, 1f), (1f, 0f) });
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.2f));
            return ps;
        }

        /// <summary>Estela de propulsión hacia abajo para el volador. Bucle; espacio mundo para que deje rastro al avanzar.</summary>
        private static ParticleSystem BuildThrusterVfx(Transform parent, Color accent)
        {
            ParticleSystem ps = NewSystem("VFX_Thruster", parent, SoftMaterial, 1f, true, 30);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            main.startColor = WithAlpha(accent, 0.55f);
            Shape(ps, ParticleSystemShapeType.Cone, 0.08f, 10f, new Vector3(90f, 0f, 0f)); // +Z del cono → -Y
            ShrinkAndFade(ps);
            return ps;
        }

        // ---------------------------------------------------------------- Utilidades

        private static ParticleSystem NewSystem(string name, Transform parent, Material material, float duration, bool loop, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // duration solo se puede cambiar parado

            var main = ps.main;
            main.duration = duration;
            main.loop = loop;
            main.playOnAwake = true;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local; // ignora la escala del FBX del enemigo
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static void Burst(ParticleSystem ps, short min, short max)
        {
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, min, max) });
        }

        private static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float radius, float angle = 25f, Vector3 rotation = default)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = type;
            shape.radius = radius;
            shape.angle = angle;
            shape.rotation = rotation;
        }

        private static void DisableShape(ParticleSystem ps)
        {
            var shape = ps.shape;
            shape.enabled = false;
        }

        private static void ShrinkAndFade(ParticleSystem ps)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = AlphaGradient(new[] { (0f, 1f), (0.6f, 1f), (1f, 0f) });
        }

        private static ParticleSystem.MinMaxGradient AlphaGradient((float time, float alpha)[] keys)
        {
            var alphaKeys = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) alphaKeys[i] = new GradientAlphaKey(keys[i].alpha, keys[i].time);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alphaKeys);
            return new ParticleSystem.MinMaxGradient(g);
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // Sprites/Default está en "Always Included Shaders" del proyecto: funciona en URP y en builds (igual que el láser del espía).
        private static Material SoftMaterial => softMaterial != null ? softMaterial : (softMaterial = NewMaterial(SoftTexture, "EnemyVFX_Soft"));
        private static Material PixelMaterial => pixelMaterial != null ? pixelMaterial : (pixelMaterial = NewMaterial(Texture2D.whiteTexture, "EnemyVFX_Pixel"));

        private static Material NewMaterial(Texture tex, string name)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            return new Material(shader) { name = name, mainTexture = tex, hideFlags = HideFlags.DontSave };
        }

        /// <summary>Círculo difuso 32×32 generado en memoria (polvo, destellos, propulsión).</summary>
        private static Texture2D SoftTexture
        {
            get
            {
                if (softTexture != null) return softTexture;
                const int n = 32;
                softTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "EnemyVFX_SoftDot", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
                softTexture.SetPixels32(px);
                softTexture.Apply(false, true);
                return softTexture;
            }
        }
    }
}
