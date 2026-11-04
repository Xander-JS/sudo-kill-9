using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SudoKill9.UI
{
    /// <summary>
    /// Barra de salud 3D World Space para Windows Boss:
    /// - Se posiciona siempre por encima de la cabeza de Windows siguiendo su movimiento con separación visual adecuada (offset configurable).
    /// - Se orienta automáticamente hacia la cámara (Billboard) para visualización nítida y sin distorsión.
    /// - Desacoplada de la jerarquía de escala del modelo 3D para evitar encogimientos o cizallamientos (permanece a escala 1.9m x 0.32m en mundo).
    /// - Sistema de Doble Vida:
    ///     * Vida 1: Se reduce con el daño recibido (Color Ámbar Dorado #F4B41A). Indicador [VIDA 1/2].
    ///     * Transición a Vida 2: Al llegar a cero la primera vida, se activa la segunda vida con animación de recarga suave
    ///       a color Rojo Carmesí (#E02424) e indicador claro [VIDA 2/2 - FASE FINAL].
    ///     * Daño en transición: Si Windows recibe disparos consecutivos durante la recarga de Vida 2, el daño no se sobrescribe ni se pierde.
    ///     * Muerte: Windows solo muere cuando ambas vidas se han agotado completamente.
    /// - Animación fluida de daño:
    ///     * Barra principal con respuesta inmediata al impacto.
    ///     * Barra secundaria de vaciado progresivo (decay / ghost damage) con retraso configurable.
    ///     * Soporta ráfagas consecutivas sin saltos bruscos ni desajustes de escala.
    /// - Indicadores visuales gemelos de vidas (pips) construidos en la UI superior.
    /// </summary>
    public class WindowsHeadHealthBar : MonoBehaviour
    {
        [Header("1. Seguimiento de Cabeza (World Space)")]
        [Tooltip("Transform objetivo de la cabeza de Windows. Si es null, se detecta automáticamente.")]
        [SerializeField] private Transform targetCabeza;

        [Tooltip("Desplazamiento vertical y espacial relativo a la cabeza para conservar separación visual adecuada.")]
        [SerializeField] private Vector3 offsetSobreCabeza = new Vector3(0f, 0.45f, 0f);

        [Tooltip("Suavizado de seguimiento de posición (0 = instantáneo sin desfase, mayor = amortiguado).")]
        [SerializeField] private float suavizadoSeguimiento = 0f;

        [Header("2. Colores del Sistema de Dos Vidas")]
        [Tooltip("Color de la primera vida de Windows (Ámbar Dorado).")]
        [SerializeField] private Color colorVida1 = new Color(0.96f, 0.71f, 0.10f); // #F4B41A

        [Tooltip("Color de la segunda vida de Windows (Rojo Carmesí / Crítico).")]
        [SerializeField] private Color colorVida2 = new Color(0.90f, 0.18f, 0.15f); // #E02424

        [Tooltip("Color de la barra de retraso / daño residual (decay).")]
        [SerializeField] private Color colorDecay = new Color(0.85f, 0.38f, 0.10f, 0.90f);

        [Header("3. Animación de Daño y Transición")]
        [Tooltip("Velocidad de vaciado suave de la barra secundaria (decay).")]
        [SerializeField] private float velocidadDecay = 0.50f;

        [Tooltip("Tiempo de espera en segundos antes de que la barra secundaria empiece a descender.")]
        [SerializeField] private float retardoDecay = 0.35f;

        [Tooltip("Duración en segundos de la animación de recarga al pasar a la segunda vida.")]
        [SerializeField] private float duracionRecargaSegundaVida = 0.65f;

        [Header("4. Componentes UI")]
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fillHealthImage;
        [SerializeField] private Image decayHealthImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text lifeBadgeText;
        [SerializeField] private TMP_Text hpNumberText;
        [SerializeField] private Image life1Dot;
        [SerializeField] private Image life2Dot;

        [Header("5. Burbuja de Diálogo IA (Flotante Arriba de la Barra)")]
        [SerializeField] private GameObject speechBubbleContainer;
        [SerializeField] private CanvasGroup speechBubbleCanvasGroup;
        [SerializeField] private TMP_Text speechBubbleSenderText;
        [SerializeField] private TMP_Text speechBubbleMessageText;
        [SerializeField] private Image speechBubbleBg;
        [SerializeField] private float duracionDialogoDefecto = 4.5f;

        // Variables internas de estado
        private WindowsBossAI bossAI;
        private Camera camaraPrincipal;
        private float targetFill = 1f;
        private float currentFill = 1f;
        private float decayFill = 1f;
        private float temporizadorDecay = 0f;
        private int vidaActual = 1;
        private int vidasTotales = 2;
        private bool enTransicionVida = false;
        private Coroutine corrutinaTransicion;
        private Coroutine corrutinaBurbujaDialogo;

        public Vector3 OffsetSobreCabeza
        {
            get => offsetSobreCabeza;
            set => offsetSobreCabeza = value;
        }

        public Color ColorVida1 => colorVida1;
        public Color ColorVida2 => colorVida2;
        public int VidaActual => vidaActual;
        public int VidasTotales => vidasTotales;
        public Image Life1Dot => life1Dot;
        public Image Life2Dot => life2Dot;
        public GameObject SpeechBubbleContainer => speechBubbleContainer;

        public void ConfigurarParaBoss(WindowsBossAI boss, Transform cabeza = null)
        {
            bossAI = boss;
            if (cabeza != null) targetCabeza = cabeza;
            LocalizarReferencias();
            DesacoplarDeEscalaPadre();
            AsegurarUIConstruida();
            if (bossAI != null)
            {
                ManejarCambioVidaBoss(bossAI.CurrentHealth, bossAI.MaxHealth, bossAI.VidaActual, bossAI.VidasTotales);
            }
        }

        private void Awake()
        {
            // Protección crítica: WindowsHeadHealthBar es un Canvas World Space.
            // NUNCA debe residir en el mismo GameObject que el modelo del jefe ni que WindowsBossAI.
            if (GetComponent<WindowsBossAI>() != null)
            {
                var boss = GetComponent<WindowsBossAI>();
                GameObject dedicated = new GameObject("WindowsHeadHealthBar");
                var bar = dedicated.AddComponent<WindowsHeadHealthBar>();
                bar.ConfigurarParaBoss(boss);
                DestroyImmediate(this);
                return;
            }

            LocalizarReferencias();
            DesacoplarDeEscalaPadre();
            AsegurarUIConstruida();
        }

        /// <summary>
        /// Desacopla la barra de la jerarquía directa del jefe para evitar que herede escalas
        /// reducidas (como el scale 0.2 del FBX de Windows) o cizallamientos por animación de huesos.
        /// </summary>
        private void DesacoplarDeEscalaPadre()
        {
            if (transform.parent != null)
            {
                transform.SetParent(null, true);
            }
            transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
        }

        private void OnEnable()
        {
            WindowsBossAI.OnBossLifeChanged -= ManejarCambioVidaBoss;
            WindowsBossAI.OnBossLifeChanged += ManejarCambioVidaBoss;

            WindowsBossAI.OnBossHealthChanged -= ManejarCambioSaludBoss;
            WindowsBossAI.OnBossHealthChanged += ManejarCambioSaludBoss;

            WindowsBossAI.OnBossDied -= ManejarMuerteBoss;
            WindowsBossAI.OnBossDied += ManejarMuerteBoss;

            WindowsAIGeminiDialogue.OnDialogueSpoken -= MostrarMensajeIA;
            WindowsAIGeminiDialogue.OnDialogueSpoken += MostrarMensajeIA;
        }

        private void OnDisable()
        {
            WindowsBossAI.OnBossLifeChanged -= ManejarCambioVidaBoss;
            WindowsBossAI.OnBossHealthChanged -= ManejarCambioSaludBoss;
            WindowsBossAI.OnBossDied -= ManejarMuerteBoss;

            WindowsAIGeminiDialogue.OnDialogueSpoken -= MostrarMensajeIA;
        }

        private void Start()
        {
            LocalizarReferencias();
            DesacoplarDeEscalaPadre();
            AsegurarUIConstruida();

            if (bossAI != null)
            {
                ManejarCambioVidaBoss(bossAI.CurrentHealth, bossAI.MaxHealth, bossAI.VidaActual, bossAI.VidasTotales);
            }
        }

        private void LocalizarReferencias()
        {
            if (bossAI == null)
            {
                bossAI = GetComponentInParent<WindowsBossAI>() ?? Object.FindFirstObjectByType<WindowsBossAI>();
            }

            if (targetCabeza == null && bossAI != null)
            {
                Animator anim = bossAI.GetComponentInChildren<Animator>();
                if (anim != null && anim.isHuman)
                {
                    targetCabeza = anim.GetBoneTransform(HumanBodyBones.Head);
                }

                if (targetCabeza == null)
                {
                    Transform[] transforms = bossAI.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        string n = transforms[i].name.ToLower();
                        if (n.Contains("head") || n.Contains("cabeza"))
                        {
                            targetCabeza = transforms[i];
                            break;
                        }
                    }
                }
            }

            if (camaraPrincipal == null)
            {
                camaraPrincipal = Camera.main;
            }
        }

        private void LateUpdate()
        {
            ActualizarPosicionYBillboard();
            ActualizarDecaySuave();
        }

        /// <summary>
        /// Mantiene la barra posicionada exactamente encima de la cabeza y orientada hacia la cámara.
        /// </summary>
        private void ActualizarPosicionYBillboard()
        {
            if (bossAI == null || bossAI.gameObject == null)
            {
                Destroy(gameObject);
                return;
            }

            if (camaraPrincipal == null)
            {
                camaraPrincipal = Camera.main;
                if (camaraPrincipal == null) return;
            }

            Vector3 puntoBase;
            if (targetCabeza != null)
            {
                puntoBase = targetCabeza.position;
            }
            else if (bossAI != null)
            {
                puntoBase = bossAI.transform.position + Vector3.up * 1.95f;
            }
            else
            {
                puntoBase = transform.position;
            }

            Vector3 posicionDeseada = puntoBase + offsetSobreCabeza;

            if (suavizadoSeguimiento > 0.001f)
            {
                transform.position = Vector3.Lerp(transform.position, posicionDeseada, Time.deltaTime / suavizadoSeguimiento);
            }
            else
            {
                transform.position = posicionDeseada;
            }

            // Billboard perfecto hacia la cámara
            transform.rotation = camaraPrincipal.transform.rotation;
        }

        private void ActualizarDecaySuave()
        {
            if (enTransicionVida || decayHealthImage == null) return;

            if (temporizadorDecay > 0f)
            {
                temporizadorDecay -= Time.deltaTime;
            }
            else if (decayFill > currentFill)
            {
                decayFill = Mathf.MoveTowards(decayFill, currentFill, velocidadDecay * Time.deltaTime);
                decayHealthImage.fillAmount = decayFill;
            }
            else
            {
                decayFill = currentFill;
                decayHealthImage.fillAmount = decayFill;
            }
        }

        /// <summary>
        /// Actualiza los valores de salud y detecta transiciones entre la primera y segunda vida.
        /// </summary>
        public void ManejarCambioVidaBoss(float hpActual, float hpMax, int vidaNum, int vidaTotales)
        {
            vidasTotales = Mathf.Max(1, vidaTotales);
            float maxSegura = Mathf.Max(1f, hpMax);
            float nuevoTargetFill = Mathf.Clamp01(hpActual / maxSegura);

            // Transición a la segunda vida
            if (vidaNum != vidaActual && vidaNum == 2)
            {
                vidaActual = vidaNum;
                targetFill = nuevoTargetFill;
                IniciarTransicionSegundaVida(hpActual, hpMax);
                return;
            }

            vidaActual = vidaNum;
            targetFill = nuevoTargetFill;
            ActualizarTextosEIndicadores(hpActual, hpMax);

            if (enTransicionVida)
            {
                // Si recibe disparos durante la animación de recarga, targetFill ya se actualizó
                // y la corrutina rellenará únicamente hasta este nuevo valor
                return;
            }

            // Daño recibido normal: la barra principal baja instantáneamente
            if (targetFill < currentFill)
            {
                currentFill = targetFill;
                if (fillHealthImage != null)
                {
                    fillHealthImage.fillAmount = currentFill;
                }
                temporizadorDecay = retardoDecay;
            }
            else
            {
                currentFill = targetFill;
                decayFill = targetFill;
                if (fillHealthImage != null) fillHealthImage.fillAmount = currentFill;
                if (decayHealthImage != null) decayHealthImage.fillAmount = decayFill;
            }
        }

        private void ManejarCambioSaludBoss(float hpActual, float hpMax)
        {
            if (!enTransicionVida)
            {
                ActualizarTextosEIndicadores(hpActual, hpMax);
            }
        }

        private void IniciarTransicionSegundaVida(float hpActual, float hpMax)
        {
            if (corrutinaTransicion != null) StopCoroutine(corrutinaTransicion);
            corrutinaTransicion = StartCoroutine(RutinaAnimacionSegundaVida(hpActual, hpMax));
        }

        private IEnumerator RutinaAnimacionSegundaVida(float hpActual, float hpMax)
        {
            enTransicionVida = true;

            // 1. Vaciar completamente la barra previa
            currentFill = 0f;
            decayFill = 0f;
            if (fillHealthImage != null) fillHealthImage.fillAmount = 0f;
            if (decayHealthImage != null) decayHealthImage.fillAmount = 0f;

            // 2. Cambiar color y textos a la segunda vida
            if (fillHealthImage != null) fillHealthImage.color = colorVida2;
            ActualizarTextosEIndicadores(hpActual, hpMax);

            // 3. Animación de llenado suave de 0 al targetFill real (EaseOutCubic)
            float transcurrido = 0f;
            while (transcurrido < duracionRecargaSegundaVida)
            {
                if (bossAI != null && bossAI.IsDead) break;

                transcurrido += Time.deltaTime;
                float t = Mathf.Clamp01(transcurrido / duracionRecargaSegundaVida);
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                // Rellenar dinámicamente respetando cualquier daño entrante en paralelo
                float techo = Mathf.Clamp01(targetFill);
                float progreso = Mathf.Lerp(0f, techo, ease);

                currentFill = progreso;
                decayFill = progreso;
                if (fillHealthImage != null) fillHealthImage.fillAmount = progreso;
                if (decayHealthImage != null) decayHealthImage.fillAmount = progreso;

                yield return null;
            }

            // Fijar de forma limpia al targetFill actual
            float fillFinal = Mathf.Clamp01(targetFill);
            currentFill = fillFinal;
            decayFill = fillFinal;
            if (fillHealthImage != null) fillHealthImage.fillAmount = fillFinal;
            if (decayHealthImage != null) decayHealthImage.fillAmount = fillFinal;

            enTransicionVida = false;
            corrutinaTransicion = null;
        }

        private void ActualizarTextosEIndicadores(float hpActual, float hpMax)
        {
            if (fillHealthImage != null)
            {
                fillHealthImage.color = (vidaActual == 1) ? colorVida1 : colorVida2;
            }

            if (hpNumberText != null)
            {
                hpNumberText.text = $"{Mathf.Max(0, Mathf.CeilToInt(hpActual))} / {Mathf.CeilToInt(hpMax)} HP";
            }

            if (lifeBadgeText != null)
            {
                if (vidaActual == 1)
                {
                    lifeBadgeText.text = "<color=#F4B41A>[VIDA 1/2]</color>";
                }
                else
                {
                    lifeBadgeText.text = "<color=#FF3333>[VIDA 2/2 - FASE FINAL]</color>";
                }
            }

            // Indicadores visuales gemelos de puntos de vida
            if (life1Dot != null)
            {
                life1Dot.color = (vidaActual == 1) ? colorVida1 : new Color(0.25f, 0.25f, 0.28f, 0.85f);
            }
            if (life2Dot != null)
            {
                life2Dot.color = (vidaActual == 2) ? colorVida2 : new Color(0.40f, 0.40f, 0.45f, 0.60f);
            }
        }

        private void ManejarMuerteBoss()
        {
            if (canvasGroup != null)
            {
                StartCoroutine(RutinaFadeOutMuerte());
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private IEnumerator RutinaFadeOutMuerte()
        {
            float t = 1f;
            while (t > 0f)
            {
                t -= Time.deltaTime * 1.5f;
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Clamp01(t);
                yield return null;
            }
            Destroy(gameObject);
        }

        // ===================================================================== //
        // BURBUJA DE DIÁLOGO FLOTANTE (ARRIBA DEL JEFE Y DE LA BARRA DE VIDA)   //
        // ===================================================================== //

        /// <summary>
        /// Muestra una frase dicha por la IA de Windows en una burbuja 3D flotante
        /// posicionada directamente por encima del jefe y por encima de su barra de salud.
        /// </summary>
        public void MostrarMensajeIA(string mensaje)
        {
            MostrarMensajeIA(mensaje, duracionDialogoDefecto);
        }

        public void MostrarMensajeIA(string mensaje, float duracion)
        {
            if (string.IsNullOrEmpty(mensaje)) return;

            AsegurarUIConstruida();

            if (speechBubbleContainer == null || speechBubbleCanvasGroup == null || speechBubbleMessageText == null)
            {
                return;
            }

            if (corrutinaBurbujaDialogo != null)
            {
                StopCoroutine(corrutinaBurbujaDialogo);
            }
            corrutinaBurbujaDialogo = StartCoroutine(RutinaAnimacionBurbuja(mensaje, duracion));
        }

        private IEnumerator RutinaAnimacionBurbuja(string mensaje, float duracion)
        {
            speechBubbleContainer.SetActive(true);
            speechBubbleMessageText.text = $"\"{mensaje}\"";

            // Animación suave de entrada con escala sutil
            float tIn = 0f;
            float durIn = 0.22f;
            RectTransform rtBurbuja = speechBubbleContainer.GetComponent<RectTransform>();
            Vector3 escalaBase = Vector3.one;

            while (tIn < durIn)
            {
                tIn += Time.deltaTime;
                float norm = Mathf.Clamp01(tIn / durIn);
                speechBubbleCanvasGroup.alpha = norm;
                if (rtBurbuja != null)
                {
                    rtBurbuja.localScale = Vector3.Lerp(new Vector3(0.85f, 0.85f, 1f), escalaBase, norm);
                }
                yield return null;
            }

            speechBubbleCanvasGroup.alpha = 1f;
            if (rtBurbuja != null) rtBurbuja.localScale = escalaBase;

            // Mantener en pantalla para lectura fluida
            float visibleTimer = 0f;
            while (visibleTimer < duracion)
            {
                if (bossAI != null && bossAI.IsDead) break;
                visibleTimer += Time.deltaTime;
                yield return null;
            }

            // Animación suave de salida (fade out)
            float tOut = 0f;
            float durOut = 0.40f;
            while (tOut < durOut)
            {
                tOut += Time.deltaTime;
                float norm = 1f - Mathf.Clamp01(tOut / durOut);
                speechBubbleCanvasGroup.alpha = norm;
                yield return null;
            }

            speechBubbleCanvasGroup.alpha = 0f;
            speechBubbleContainer.SetActive(false);
            corrutinaBurbujaDialogo = null;
        }

        // ===================================================================== //
        // CONSTRUCCIÓN AUTOMÁTICA EN TIEMPO DE EJECUCIÓN (WORLD SPACE)          //
        // ===================================================================== //

        public void AsegurarUIConstruida()
        {
            // Configurar Canvas World Space
            worldCanvas = GetComponent<Canvas>();
            if (worldCanvas == null) worldCanvas = gameObject.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.sortingOrder = 50;

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            RectTransform rtCanvas = GetComponent<RectTransform>();
            if (rtCanvas == null) rtCanvas = gameObject.AddComponent<RectTransform>();
            rtCanvas.sizeDelta = new Vector2(380f, 65f);
            transform.localScale = new Vector3(0.005f, 0.005f, 0.005f); // 1.9m ancho x 0.325m alto en mundo

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/LiberationSans SDF");
            if (font == null)
            {
                TMP_FontAsset[] fuentes = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                for (int i = 0; i < fuentes.Length; i++)
                {
                    if (fuentes[i].name.Contains("LiberationSans") || fuentes[i].name.Contains("PixelTactical") || fuentes[i].name.Contains("Point"))
                    {
                        font = fuentes[i];
                        break;
                    }
                }
                if (font == null && fuentes.Length > 0) font = fuentes[0];
            }

            // ------------------------------------------------------------- //
            // BURBUJA DE DIÁLOGO FLOTANTE (DIRECTAMENTE ARRIBA DE LA BARRA) //
            // ------------------------------------------------------------- //
            if (speechBubbleContainer == null)
            {
                speechBubbleContainer = ObtenerOCrearHijo(transform, "SpeechBubbleContainer");
                RectTransform rtBubble = speechBubbleContainer.GetComponent<RectTransform>();
                rtBubble.anchorMin = new Vector2(0.5f, 1f);
                rtBubble.anchorMax = new Vector2(0.5f, 1f);
                rtBubble.pivot = new Vector2(0.5f, 0f);
                rtBubble.anchoredPosition = new Vector2(0f, 14f); // 14px por encima del borde superior de la barra
                rtBubble.sizeDelta = new Vector2(440f, 68f);

                speechBubbleCanvasGroup = ObtenerOAgregarComponente<CanvasGroup>(speechBubbleContainer);
                speechBubbleCanvasGroup.alpha = 0f;

                // Fondo panel de diálogo
                GameObject bubbleBgGO = ObtenerOCrearHijo(speechBubbleContainer.transform, "BubbleBackground");
                RectTransform rtBg = bubbleBgGO.GetComponent<RectTransform>();
                rtBg.anchorMin = Vector2.zero;
                rtBg.anchorMax = Vector2.one;
                rtBg.sizeDelta = Vector2.zero;
                speechBubbleBg = ObtenerOAgregarComponente<Image>(bubbleBgGO);
                speechBubbleBg.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);

                // Marco estético / borde cyan
                GameObject bubbleBorderGO = ObtenerOCrearHijo(bubbleBgGO.transform, "BubbleBorder");
                RectTransform rtBorder = bubbleBorderGO.GetComponent<RectTransform>();
                rtBorder.anchorMin = Vector2.zero;
                rtBorder.anchorMax = Vector2.one;
                rtBorder.offsetMin = new Vector2(2f, 2f);
                rtBorder.offsetMax = new Vector2(-2f, -2f);
                Image borderImg = ObtenerOAgregarComponente<Image>(bubbleBorderGO);
                borderImg.color = new Color(0.00f, 0.85f, 1.00f, 0.40f);

                // Flecha / indicador hacia abajo apuntando a la barra
                GameObject arrowGO = ObtenerOCrearHijo(speechBubbleContainer.transform, "BubblePointer");
                RectTransform rtArrow = arrowGO.GetComponent<RectTransform>();
                rtArrow.anchorMin = new Vector2(0.5f, 0f);
                rtArrow.anchorMax = new Vector2(0.5f, 0f);
                rtArrow.pivot = new Vector2(0.5f, 1f);
                rtArrow.anchoredPosition = new Vector2(0f, -1f);
                rtArrow.sizeDelta = new Vector2(16f, 8f);
                Image arrowImg = ObtenerOAgregarComponente<Image>(arrowGO);
                arrowImg.color = new Color(0.00f, 0.85f, 1.00f, 0.95f);

                // Texto remitente: "💬 WINDOWS 11 [IA]:"
                speechBubbleSenderText = ObtenerOCrearTexto(speechBubbleContainer.transform, "SenderText", font, 11, FontStyles.Bold, TextAlignmentOptions.Left);
                RectTransform rtSender = speechBubbleSenderText.rectTransform;
                rtSender.anchorMin = new Vector2(0f, 1f);
                rtSender.anchorMax = new Vector2(1f, 1f);
                rtSender.pivot = new Vector2(0f, 1f);
                rtSender.anchoredPosition = new Vector2(12f, -5f);
                rtSender.sizeDelta = new Vector2(-24f, 15f);
                speechBubbleSenderText.text = "<color=#00E5FF>💬 WINDOWS 11 [IA]:</color>";

                // Texto del mensaje hablado
                speechBubbleMessageText = ObtenerOCrearTexto(speechBubbleContainer.transform, "MessageText", font, 12, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                RectTransform rtMsg = speechBubbleMessageText.rectTransform;
                rtMsg.anchorMin = new Vector2(0f, 0f);
                rtMsg.anchorMax = new Vector2(1f, 1f);
                rtMsg.pivot = new Vector2(0.5f, 0.5f);
                rtMsg.offsetMin = new Vector2(12f, 6f);
                speechBubbleMessageText.textWrappingMode = TextWrappingModes.Normal;
                speechBubbleMessageText.overflowMode = TextOverflowModes.Ellipsis;
                speechBubbleMessageText.color = Color.white;
                speechBubbleMessageText.text = "";

                speechBubbleContainer.SetActive(false);
            }

            if (fillHealthImage != null && decayHealthImage != null && life1Dot != null && life2Dot != null) return;

            // Fondo del panel
            GameObject panelGO = ObtenerOCrearHijo(transform, "PanelFondo");
            RectTransform rtPanel = panelGO.GetComponent<RectTransform>();
            rtPanel.anchorMin = Vector2.zero;
            rtPanel.anchorMax = Vector2.one;
            rtPanel.sizeDelta = Vector2.zero;
            Image panelImg = ObtenerOAgregarComponente<Image>(panelGO);
            panelImg.color = new Color(0.06f, 0.08f, 0.10f, 0.92f);

            // Título
            titleText = ObtenerOCrearTexto(transform, "TitleText", font, 14, FontStyles.Bold, TextAlignmentOptions.Left);
            RectTransform rtTitle = titleText.rectTransform;
            rtTitle.anchorMin = new Vector2(0f, 1f);
            rtTitle.anchorMax = new Vector2(0.55f, 1f);
            rtTitle.pivot = new Vector2(0f, 1f);
            rtTitle.anchoredPosition = new Vector2(12f, -6f);
            rtTitle.sizeDelta = new Vector2(0f, 20f);
            titleText.text = "WINDOWS 11 DEFENDER";
            titleText.color = new Color(1f, 0.92f, 0.75f);

            // Contenedor de Pips de Vida (Indicadores visuales gemelos)
            GameObject pipsContainer = ObtenerOCrearHijo(transform, "LifePipsContainer");
            RectTransform rtPips = pipsContainer.GetComponent<RectTransform>();
            rtPips.anchorMin = new Vector2(0.55f, 1f);
            rtPips.anchorMax = new Vector2(0.72f, 1f);
            rtPips.pivot = new Vector2(0.5f, 1f);
            rtPips.anchoredPosition = new Vector2(0f, -10f);
            rtPips.sizeDelta = new Vector2(48f, 14f);

            // Dot Vida 1
            GameObject dot1GO = ObtenerOCrearHijo(pipsContainer.transform, "Life1Dot");
            RectTransform rtDot1 = dot1GO.GetComponent<RectTransform>();
            rtDot1.anchorMin = new Vector2(0f, 0.5f);
            rtDot1.anchorMax = new Vector2(0f, 0.5f);
            rtDot1.pivot = new Vector2(0f, 0.5f);
            rtDot1.anchoredPosition = new Vector2(0f, 0f);
            rtDot1.sizeDelta = new Vector2(20f, 10f);
            life1Dot = ObtenerOAgregarComponente<Image>(dot1GO);
            life1Dot.color = colorVida1;

            // Dot Vida 2
            GameObject dot2GO = ObtenerOCrearHijo(pipsContainer.transform, "Life2Dot");
            RectTransform rtDot2 = dot2GO.GetComponent<RectTransform>();
            rtDot2.anchorMin = new Vector2(1f, 0.5f);
            rtDot2.anchorMax = new Vector2(1f, 0.5f);
            rtDot2.pivot = new Vector2(1f, 0.5f);
            rtDot2.anchoredPosition = new Vector2(0f, 0f);
            rtDot2.sizeDelta = new Vector2(20f, 10f);
            life2Dot = ObtenerOAgregarComponente<Image>(dot2GO);
            life2Dot.color = new Color(0.40f, 0.40f, 0.45f, 0.60f);

            // Life Badge Text
            lifeBadgeText = ObtenerOCrearTexto(transform, "LifeBadgeText", font, 12, FontStyles.Bold, TextAlignmentOptions.Right);
            RectTransform rtBadge = lifeBadgeText.rectTransform;
            rtBadge.anchorMin = new Vector2(0.72f, 1f);
            rtBadge.anchorMax = new Vector2(1f, 1f);
            rtBadge.pivot = new Vector2(1f, 1f);
            rtBadge.anchoredPosition = new Vector2(-12f, -6f);
            rtBadge.sizeDelta = new Vector2(0f, 20f);
            lifeBadgeText.text = "<color=#F4B41A>[VIDA 1/2]</color>";

            // Contenedor de barra
            GameObject barArea = ObtenerOCrearHijo(transform, "BarArea");
            RectTransform rtBar = barArea.GetComponent<RectTransform>();
            rtBar.anchorMin = new Vector2(0f, 0f);
            rtBar.anchorMax = new Vector2(1f, 1f);
            rtBar.offsetMin = new Vector2(12f, 16f);
            rtBar.offsetMax = new Vector2(-12f, -28f);

            Image barBg = ObtenerOAgregarComponente<Image>(barArea);
            barBg.color = new Color(0.02f, 0.03f, 0.04f, 0.98f);

            // Barra Decay (Delayed / Trailing)
            GameObject decayGO = ObtenerOCrearHijo(barArea.transform, "DecayBar");
            RectTransform rtDecay = decayGO.GetComponent<RectTransform>();
            rtDecay.anchorMin = Vector2.zero;
            rtDecay.anchorMax = Vector2.one;
            rtDecay.sizeDelta = Vector2.zero;
            decayHealthImage = ObtenerOAgregarComponente<Image>(decayGO);
            decayHealthImage.type = Image.Type.Filled;
            decayHealthImage.fillMethod = Image.FillMethod.Horizontal;
            decayHealthImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            decayHealthImage.fillAmount = 1f;
            decayHealthImage.color = colorDecay;

            // Barra Fill Principal
            GameObject fillGO = ObtenerOCrearHijo(barArea.transform, "FillBar");
            RectTransform rtFill = fillGO.GetComponent<RectTransform>();
            rtFill.anchorMin = Vector2.zero;
            rtFill.anchorMax = Vector2.one;
            rtFill.sizeDelta = Vector2.zero;
            fillHealthImage = ObtenerOAgregarComponente<Image>(fillGO);
            fillHealthImage.type = Image.Type.Filled;
            fillHealthImage.fillMethod = Image.FillMethod.Horizontal;
            fillHealthImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillHealthImage.fillAmount = 1f;
            fillHealthImage.color = colorVida1;

            // Número HP
            hpNumberText = ObtenerOCrearTexto(transform, "HPText", font, 11, FontStyles.Normal, TextAlignmentOptions.Right);
            RectTransform rtHp = hpNumberText.rectTransform;
            rtHp.anchorMin = new Vector2(0.5f, 0f);
            rtHp.anchorMax = new Vector2(1f, 0f);
            rtHp.pivot = new Vector2(1f, 0f);
            rtHp.anchoredPosition = new Vector2(-12f, 2f);
            rtHp.sizeDelta = new Vector2(0f, 16f);
            hpNumberText.text = "500 / 500 HP";
            hpNumberText.color = new Color(0.9f, 0.95f, 1f, 0.85f);
        }

        private GameObject ObtenerOCrearHijo(Transform parent, string nombre)
        {
            Transform t = parent.Find(nombre);
            if (t != null) return t.gameObject;
            GameObject go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private TMP_Text ObtenerOCrearTexto(Transform parent, string nombre, TMP_FontAsset font, float tamano, FontStyles estilo, TextAlignmentOptions alineacion)
        {
            GameObject go = ObtenerOCrearHijo(parent, nombre);
            TextMeshProUGUI tmp = ObtenerOAgregarComponente<TextMeshProUGUI>(go);
            if (font != null) tmp.font = font;
            tmp.fontSize = tamano;
            tmp.fontStyle = estilo;
            tmp.alignment = alineacion;
            return tmp;
        }

        private T ObtenerOAgregarComponente<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }
    }
}
