using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Video;
using SudoKill9.Enemies;
using SudoKill9.UI;

namespace SudoKill9.Cinematics
{
    /// <summary>
    /// Cinemática final (homenaje "pantalla en el cielo"): al morir WindowsBoss se congela el combate,
    /// se apaga la música, la cámara se inclina suavemente hacia un monitor gigante en el cielo que
    /// reproduce bill.webm y, al terminar el video (o al pulsar Espacio/Escape), se funde a negro
    /// hacia la escena de créditos con <see cref="SceneFader"/>.
    ///
    /// Montaje: Tools/SudoKill9/Configurar Cinemática Final (SetupFinalCinematic.cs).
    /// Mientras este director esté activo, VictoryDefeatScreen no muestra su panel de victoria.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class FinalCinematicDirector : MonoBehaviour
    {
        public static FinalCinematicDirector Instance { get; private set; }

        /// <summary>True si hay un director activo en la escena que se encargará de la victoria.</summary>
        public static bool ControlaLaVictoria => Instance != null && Instance.isActiveAndEnabled;

        [Header("Pantalla en el cielo")]
        [Tooltip("Raíz de la pantalla (Quad 16:9 + marco). Se oculta hasta que empieza la cinemática.")]
        [SerializeField] private Transform pantalla;
        [Tooltip("Si está activo, la pantalla se recoloca al iniciar la cinemática delante de la cámara " +
                 "(en el cielo), garantizando que siempre se vea sin importar dónde muera el boss.")]
        [SerializeField] private bool reposicionarFrenteACamara = true;
        [SerializeField] private float distanciaPantalla = 140f;
        [SerializeField] private float alturaPantalla = 85f;
        [Tooltip("Fracción de la altura de la vista que ocupa la pantalla al terminar el giro.")]
        [Range(0.3f, 1f)] [SerializeField] private float ocupacionVertical = 0.7f;

        [Header("Cámara")]
        [SerializeField] private float esperaTrasMuerte = 1.5f;
        [SerializeField] private float duracionGiro = 2.5f;
        [SerializeField] private float fovFinal = 45f;
        [Tooltip("Desactiva la niebla durante la cinemática para que el monitor lejano no se pierda.")]
        [SerializeField] private bool desactivarNiebla = true;

        [Header("Audio")]
        [SerializeField] private float duracionFadeMusica = 1.5f;

        [Header("Final")]
        [SerializeField] private string escenaCreditos = "Credits";
        [SerializeField] private float duracionFadeNegro = 1.2f;
        [Tooltip("Tiempo máximo de reproducción antes de pasar a créditos (0 = duración completa del clip).")]
        [SerializeField] private float duracionMaximaVideo = 30f;
        [SerializeField] private KeyCode[] teclasSaltar = { KeyCode.Space, KeyCode.Escape };

        [Header("Extras opcionales")]
        [Tooltip("Comportamientos adicionales a desactivar al empezar (además de los detectados automáticamente).")]
        [SerializeField] private Behaviour[] desactivarAdemas;

        private VideoPlayer videoPlayer;
        private bool iniciada;
        private bool terminada;
        private bool videoTerminado;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[FinalCinematicDirector] Ya existe otro director en la escena; se desactiva este.");
                enabled = false;
                return;
            }
            Instance = this;

            videoPlayer = GetComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.loopPointReached += _ => videoTerminado = true;

            if (pantalla != null) pantalla.gameObject.SetActive(false);
        }

        private void Start()
        {
            // Precargar el video para que arranque sin tirones cuando llegue el momento.
            if (videoPlayer.clip != null || !string.IsNullOrEmpty(videoPlayer.url)) videoPlayer.Prepare();
        }

        private void OnEnable()
        {
            WindowsBossAI.OnBossDied -= IniciarCinematica;
            WindowsBossAI.OnBossDied += IniciarCinematica;
        }

        private void OnDisable() => WindowsBossAI.OnBossDied -= IniciarCinematica;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!iniciada || terminada) return;
            foreach (var tecla in teclasSaltar)
            {
                if (Input.GetKeyDown(tecla)) { Terminar(); return; }
            }
        }

        /// <summary>Punto de entrada (evento OnBossDied). Público para poder probarla desde el inspector o un debug.</summary>
        [ContextMenu("Probar cinemática")]
        public void IniciarCinematica()
        {
            if (iniciada) return;
            iniciada = true;
            StartCoroutine(Secuencia());
        }

        private IEnumerator Secuencia()
        {
            Time.timeScale = 1f;
            CongelarJuego();
            StartCoroutine(ApagarMusica());

            // Dejar respirar la animación de muerte del boss antes de mirar al cielo.
            yield return new WaitForSecondsRealtime(esperaTrasMuerte);
            if (terminada) yield break;

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[FinalCinematicDirector] No hay Camera.main; se salta directo a créditos.");
                Terminar();
                yield break;
            }

            // Separar la cámara del rig del jugador (sus scripts ya están apagados) conservando su pose.
            cam.transform.SetParent(null, true);
            if (desactivarNiebla) RenderSettings.fog = false;
            if (cam.farClipPlane < distanciaPantalla * 2f) cam.farClipPlane = distanciaPantalla * 2f;

            if (pantalla != null)
            {
                if (reposicionarFrenteACamara) ColocarPantalla(cam);
                pantalla.gameObject.SetActive(true);
            }
            videoPlayer.Play();

            yield return GirarCamara(cam);

            // Esperar fin del video, tiempo máximo o salto manual.
            float limite = duracionMaximaVideo > 0f ? duracionMaximaVideo
                         : videoPlayer.clip != null ? (float)videoPlayer.clip.length + 1f : 60f;
            float t = 0f;
            while (!terminada && !videoTerminado && t < limite)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Terminar();
        }

        private void ColocarPantalla(Camera cam)
        {
            Vector3 adelante = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (adelante.sqrMagnitude < 0.001f) adelante = Vector3.forward;
            adelante.Normalize();

            Vector3 pos = cam.transform.position + adelante * distanciaPantalla + Vector3.up * alturaPantalla;
            pantalla.position = pos;
            // El Quad de Unity se ve desde su -Z: su forward debe apuntar en la dirección de la mirada.
            pantalla.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);

            float distancia = Vector3.Distance(cam.transform.position, pos);
            float alto = 2f * distancia * Mathf.Tan(fovFinal * 0.5f * Mathf.Deg2Rad) * ocupacionVertical;
            pantalla.localScale = new Vector3(alto * 16f / 9f, alto, 1f);
        }

        private IEnumerator GirarCamara(Camera cam)
        {
            Transform ct = cam.transform;
            Quaternion desde = ct.rotation;
            Vector3 objetivo = pantalla != null ? pantalla.position : ct.position + Vector3.up * 100f + ct.forward;
            Quaternion hacia = Quaternion.LookRotation(objetivo - ct.position, Vector3.up);
            float fovInicial = cam.fieldOfView;

            for (float t = 0f; t < 1f && !terminada;)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duracionGiro);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)); // ease in-out
                ct.rotation = Quaternion.Slerp(desde, hacia, k);
                cam.fieldOfView = Mathf.Lerp(fovInicial, fovFinal, k);
                yield return null;
            }
        }

        private void CongelarJuego()
        {
            var aDesactivar = new List<Behaviour>();

            // Jugador (Linux): combate, movimiento, apuntado y cámara.
            aDesactivar.AddRange(FindObjectsByType<LinuxCombatIK>(FindObjectsSortMode.None));
            aDesactivar.AddRange(FindObjectsByType<LinuxSkeletonController>(FindObjectsSortMode.None));
            aDesactivar.AddRange(FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None));
            aDesactivar.AddRange(FindObjectsByType<PlayerShooter>(FindObjectsSortMode.None));
            aDesactivar.AddRange(FindObjectsByType<PlayerAim>(FindObjectsSortMode.None));
            aDesactivar.AddRange(FindObjectsByType<PlayerCursorAim>(FindObjectsSortMode.None));
            aDesactivar.AddRange(FindObjectsByType<CrosshairUI>(FindObjectsSortMode.None));
            aDesactivar.AddRange(FindObjectsByType<ThirdPersonCameraGTA>(FindObjectsSortMode.None));
            aDesactivar.AddRange(FindObjectsByType<ControladorCamaraJugador>(FindObjectsSortMode.None));

            // Boss: que no lance más ataques durante la cinemática.
            aDesactivar.AddRange(FindObjectsByType<WindowsCommandAttacks>(FindObjectsSortMode.None));

            if (desactivarAdemas != null) aDesactivar.AddRange(desactivarAdemas);

            foreach (var b in aDesactivar)
                if (b != null) b.enabled = false;

            // Dejar a Linux en reposo: sin esto el Animator se queda con el último input (corriendo en el sitio).
            foreach (var skel in FindObjectsByType<LinuxSkeletonController>(FindObjectsSortMode.None))
                ResetearAnimator(skel.GetComponentInChildren<Animator>());
            foreach (var cc in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
                if (cc.TryGetComponent<LinuxSkeletonController>(out _)) cc.enabled = false;

            // Enemigos restantes: IA apagada, agente detenido y animación congelada.
            foreach (var ai in FindObjectsByType<EnemyAIBase>(FindObjectsSortMode.None))
            {
                ai.enabled = false;
                if (ai.TryGetComponent<NavMeshAgent>(out var agent) && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                var anim = ai.GetComponentInChildren<Animator>();
                if (anim != null) anim.speed = 0f;
            }

            // HUD de combate fuera: la escena es del monitor.
            foreach (var hud in FindObjectsByType<CombatHUDManager>(FindObjectsSortMode.None))
                OcultarCanvas(hud);
            foreach (var hud in FindObjectsByType<WindowsHeadHealthBar>(FindObjectsSortMode.None))
                OcultarCanvas(hud);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void ResetearAnimator(Animator anim)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return;
            foreach (var p in anim.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Float) anim.SetFloat(p.nameHash, 0f);
                else if (p.type == AnimatorControllerParameterType.Bool) anim.SetBool(p.nameHash, false);
            }
        }

        private static void OcultarCanvas(Component c)
        {
            var canvas = c.GetComponentInParent<Canvas>();
            if (canvas != null) canvas.rootCanvas.enabled = false;
            else c.gameObject.SetActive(false);
        }

        private IEnumerator ApagarMusica()
        {
            // "Música" = toda fuente en bucle que esté sonando (BackgroundMusic, Audio_Loop, etc.).
            var fuentes = new List<(AudioSource src, float vol)>();
            foreach (var src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (src.isPlaying && src.loop && src.gameObject != gameObject)
                    fuentes.Add((src, src.volume));

            for (float t = 0f; t < 1f;)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duracionFadeMusica);
                foreach (var (src, vol) in fuentes)
                    if (src != null) src.volume = Mathf.Lerp(vol, 0f, t);
                yield return null;
            }
            foreach (var (src, _) in fuentes)
                if (src != null) src.Stop();
        }

        private void Terminar()
        {
            if (terminada) return;
            terminada = true;
            Time.timeScale = 1f;
            SceneFader.LoadScene(escenaCreditos, duracionFadeNegro);
        }
    }
}
