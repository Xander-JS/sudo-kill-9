using UnityEngine;
using UnityEngine.UI;

namespace SudoKill9.Enemies
{
    /// <summary>
    /// Barra de vida flotante (Canvas World Space) para cualquier enemigo que herede de EnemyStatsBase.
    /// - Rectángulo mínimo: fondo oscuro + Image Filled/Horizontal.
    /// - Solo se actualiza con EnemyStatsBase.OnHealthChanged (daño o curación); nada de sondeo por frame.
    /// - Billboard: copia la rotación de la cámara en LateUpdate (se ve plana y nítida con cualquier giro).
    /// - Se oculta en OnDeath; al destruir al enemigo se destruye con él (es hija suya).
    /// Se crea con Tools/Setup Enemy Health Bars o desde los Setup*Enemy (EnemyHealthBar.CreateOrUpdate).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public class EnemyHealthBar : MonoBehaviour
    {
        public const string BarObjectName = "HealthBar";
        private const float UnitsPerMeter = 100f; // 1 unidad de Canvas = 1 cm

        [Header("Referencias")]
        [SerializeField] private EnemyStatsBase stats;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image fillImage;

        [Header("Aspecto")]
        [Tooltip("Tamaño de la barra en metros (ancho, alto), independiente de la escala del modelo.")]
        [SerializeField] private Vector2 worldSize = new Vector2(1f, 0.12f);
        [Tooltip("Margen del fondo alrededor del relleno, en cm.")]
        [SerializeField] private float padding = 1.5f;
        [SerializeField] private Color backgroundColor = new Color(0.04f, 0.05f, 0.07f, 0.85f);
        [SerializeField] private Color fillColor = new Color(0.88f, 0.14f, 0.14f, 1f); // #E02424, rojo de enemigo del HUD

        [Header("Posición")]
        [Tooltip("Metros por encima del punto más alto del mesh (informativo; lo aplica CreateOrUpdate).")]
        [SerializeField] private float heightAboveHead = 0.35f;

        private static Sprite whiteSprite;
        private Camera cam;

        public float HeightAboveHead => heightAboveHead;
        public Image FillImage => fillImage;

        private void Awake()
        {
            if (stats == null) stats = GetComponentInParent<EnemyStatsBase>();
            if (canvas == null) canvas = GetComponent<Canvas>();
            ApplyWorldScale();

            // Image.Filled necesita sprite para recortar; se crea en runtime para no depender de assets.
            if (fillImage != null && fillImage.sprite == null) fillImage.sprite = WhiteSprite;
            if (backgroundImage != null && backgroundImage.sprite == null) backgroundImage.sprite = WhiteSprite;
        }

        private void OnEnable()
        {
            if (stats == null) return;
            stats.OnHealthChanged += HandleHealthChanged;
            stats.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (stats == null) return;
            stats.OnHealthChanged -= HandleHealthChanged;
            stats.OnDeath -= HandleDeath;
        }

        private void Start()
        {
            if (stats == null)
            {
                Debug.LogWarning($"[EnemyHealthBar] '{name}' no tiene EnemyStatsBase en sus padres; se oculta.", this);
                canvas.enabled = false;
                return;
            }
            // Valor inicial (Start corre después de EnemyStatsBase.Awake, que fija currentHealth).
            HandleHealthChanged(stats.CurrentHealth, stats.MaxHealth);
            canvas.enabled = !stats.IsDead;
        }

        private void LateUpdate()
        {
            if (!canvas.enabled) return;
            if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (fillImage != null) fillImage.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        private void HandleDeath() => canvas.enabled = false;

        /// <summary>Mantiene la barra en worldSize metros aunque el modelo tenga escala (p. ej. FBX a 0.01).</summary>
        private void ApplyWorldScale()
        {
            Vector3 ps = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float k = 1f / UnitsPerMeter;
            transform.localScale = new Vector3(k / SafeAbs(ps.x), k / SafeAbs(ps.y), k / SafeAbs(ps.z));
        }

        private static float SafeAbs(float v) => Mathf.Max(Mathf.Abs(v), 0.0001f);

        private static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    Texture2D tex = Texture2D.whiteTexture;
                    whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    whiteSprite.name = "EnemyHealthBar_White";
                }
                return whiteSprite;
            }
        }

        // ------------------------------------------------------------------ Construcción

        /// <summary>
        /// Crea (o reutiliza) el hijo 'HealthBar' del enemigo y lo coloca heightAboveHead metros
        /// por encima del punto más alto de sus renderers. Sirve en Editor y en runtime.
        /// </summary>
        public static EnemyHealthBar CreateOrUpdate(EnemyStatsBase enemy, float heightAboveHead, out bool created)
        {
            Transform root = enemy.transform;
            Transform existing = root.Find(BarObjectName);
            created = existing == null;

            GameObject go = created
                ? new GameObject(BarObjectName, typeof(RectTransform), typeof(Canvas), typeof(EnemyHealthBar))
                : existing.gameObject;
            if (created) go.transform.SetParent(root, false);

            EnemyHealthBar bar = go.GetComponent<EnemyHealthBar>();
            if (bar == null) bar = go.AddComponent<EnemyHealthBar>();
            bar.stats = enemy;
            bar.heightAboveHead = heightAboveHead;
            bar.Build();
            bar.PlaceAboveHead(root);
            return bar;
        }

        private void Build()
        {
            canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;

            var rt = (RectTransform)transform;
            rt.pivot = new Vector2(0.5f, 0f); // la base de la barra queda a heightAboveHead
            rt.sizeDelta = worldSize * UnitsPerMeter;
            ApplyWorldScale();

            backgroundImage = GetOrCreateImage(transform, "Background", 0f);
            backgroundImage.color = backgroundColor;

            fillImage = GetOrCreateImage(transform, "Fill", padding);
            fillImage.color = fillColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;
        }

        private void PlaceAboveHead(Transform root)
        {
            bool found = false;
            float top = root.position.y;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                top = found ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y;
                found = true;
            }
            if (!found) top = root.position.y + 2f;

            // Centrado en el pivote (no en los bounds: los brazos en T-pose desplazarían la barra).
            transform.position = new Vector3(root.position.x, top + heightAboveHead, root.position.z);
            transform.rotation = root.rotation;
        }

        private static Image GetOrCreateImage(Transform parent, string childName, float inset)
        {
            Transform t = parent.Find(childName);
            GameObject go = t != null ? t.gameObject : new GameObject(childName, typeof(RectTransform), typeof(Image));
            if (t == null) go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);

            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }
    }
}
