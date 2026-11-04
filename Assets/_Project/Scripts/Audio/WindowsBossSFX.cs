using System.Collections;
using UnityEngine;
using SudoKill9.Combat;

namespace SudoKill9.Audio
{
    /// <summary>
    /// Controlador integral de efectos de sonido (SFX) 3D para el jefe windows_opt.
    /// Administra:
    /// - Pisadas rítmicas de carrera y trote con 'Running Snow 1' y 'Running Snow 2'.
    /// - Poderes especiales (Magic Fire 0, Magic Fire 1, SaltaLoco, Firewall, Taskkill).
    /// - Combate físico cuerpo a cuerpo (Patada, Ataque_Patada, Izquiedazo, Tornado, Salto).
    /// - Reacciones a impactos, respiración en PoseCansado, pantalla azul (BSOD) y muerte.
    /// - Arquitectura de 3 canales AudioSource 3D independientes sin interferir con la voz TTS.
    /// </summary>
    [SelectionBase]
    public class WindowsBossSFX : MonoBehaviour
    {
        // ========================================================================= //
        // 1. CANALES DE AUDIOSOURCE 3D DEDICADOS                                    //
        // ========================================================================= //

        [Header("1. Canales de Audio 3D")]
        [Tooltip("AudioSource para pasos y locomoción.")]
        [SerializeField] private AudioSource audioSourceFootsteps;

        [Tooltip("AudioSource para habilidades, golpes y disparos de un solo impacto.")]
        [SerializeField] private AudioSource audioSourcePowers;

        [Tooltip("AudioSource para sonidos continuos en bucle (trampa de fuego, zumbidos).")]
        [SerializeField] private AudioSource audioSourceLoop;

        // ========================================================================= //
        // 2. PISADAS Y LOCOMOCIÓN (RUNNING SNOW 1 & 2)                              //
        // ========================================================================= //

        [Header("2. Pisadas y Locomoción (Running Snow)")]
        [Tooltip("Sonido principal de pisada para carrera y trote.")]
        [SerializeField] private AudioClip clipRunningSnow1;

        [Tooltip("Sonido secundario alternado para evitar monotonía auditiva.")]
        [SerializeField] private AudioClip clipRunningSnow2;

        [Range(0f, 1f)]
        [SerializeField] private float volumenPisadas = 0.85f;

        [Tooltip("Intervalo entre pasos durante 'Camina' (segundos).")]
        [SerializeField] private float intervaloCamina = 0.52f;

        [Tooltip("Intervalo entre pasos durante 'Trota' (segundos).")]
        [SerializeField] private float intervaloTrota = 0.38f;

        [Tooltip("Intervalo entre pasos durante 'Rum' (segundos).")]
        [SerializeField] private float intervaloRum = 0.28f;

        [Tooltip("Variación aleatoria de tono para enriquecer el impacto acústico.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float variacionPitch = 0.05f;

        // ========================================================================= //
        // 3. PODERES MÁGICOS Y HABILIDADES ESPECIALES                               //
        // ========================================================================= //

        [Header("3. Ataque 1: Magic Fire 0 & RotatorPS1")]
        [SerializeField] private AudioClip clipMagicFire0Launch;
        [SerializeField] private AudioClip clipMagicFire0Impact;
        [SerializeField] private AudioClip clipRotatorPS1Loop;

        [Header("4. Ataque 2: Magic Fire 1 (Homing & Exhausted)")]
        [SerializeField] private AudioClip clipMagicFire1Charge;
        [SerializeField] private AudioClip clipMagicFire1Fly;
        [SerializeField] private AudioClip clipMagicFire1Explosion;
        [SerializeField] private AudioClip clipExhaustedBreathing;
        [SerializeField] private AudioClip clipExhaustedSparks;

        [Header("5. Habilidades Tácticas de Consola")]
        [SerializeField] private AudioClip clipSaltaLoco;
        [SerializeField] private AudioClip clipTaskkill;
        [SerializeField] private AudioClip clipFirewallDeploy;
        [SerializeField] private AudioClip clipFirewallBlock;

        [Header("6. Combate Físico Melee")]
        [SerializeField] private AudioClip clipMeleePatada;
        [SerializeField] private AudioClip clipMeleePesado;
        [SerializeField] private AudioClip clipMeleeTornado;
        [SerializeField] private AudioClip clipMeleeSaltoImpacto;

        [Header("7. Reacciones, BSOD y Muerte")]
        [SerializeField] private AudioClip clipHit;
        [SerializeField] private AudioClip clipDie;
        [SerializeField] private AudioClip clipBSOD;

        // ========================================================================= //
        // VARIABLES INTERNAS DE CONTROL                                             //
        // ========================================================================= //

        private WindowsBossAI bossAI;
        private WindowsCommandAttacks commandAttacks;
        private float temporizadorPasos = 0f;
        private bool usarSegundoClipPaso = false;
        private bool estaEnPoseCansado = false;

        private void Awake()
        {
            bossAI = GetComponent<WindowsBossAI>();
            commandAttacks = GetComponent<WindowsCommandAttacks>();

            ConfigurarCanalesAudio3D();
            GarantizarClipsDeAudio();
        }

        private void OnEnable()
        {
            SuscribirEventos();
        }

        private void OnDisable()
        {
            DesuscribirEventos();
            DetenerTodosLosSonidos();
        }

        private void SuscribirEventos()
        {
            WindowsCommandAttacks.OnSpecialAttackStarted += ManejarAtaqueEspecialIniciado;
            WindowsCommandAttacks.OnSpecialAttackFinished += ManejarAtaqueEspecialTerminado;
            WindowsCommandAttacks.OnMeleeAttackStarted += ManejarAtaqueMeleeIniciado;
            WindowsCommandAttacks.OnBSODTriggered += ManejarBSOD;

            WindowsBossAI.OnBossStateChanged += ManejarCambioEstadoBoss;
            WindowsBossAI.OnBossDied += ManejarMuerteBoss;
        }

        private void DesuscribirEventos()
        {
            WindowsCommandAttacks.OnSpecialAttackStarted -= ManejarAtaqueEspecialIniciado;
            WindowsCommandAttacks.OnSpecialAttackFinished -= ManejarAtaqueEspecialTerminado;
            WindowsCommandAttacks.OnMeleeAttackStarted -= ManejarAtaqueMeleeIniciado;
            WindowsCommandAttacks.OnBSODTriggered -= ManejarBSOD;

            WindowsBossAI.OnBossStateChanged -= ManejarCambioEstadoBoss;
            WindowsBossAI.OnBossDied -= ManejarMuerteBoss;
        }

        private void Update()
        {
            if (bossAI == null || bossAI.IsDead) return;

            ActualizarSonidoPisadas();
        }

        // ========================================================================= //
        // LÓGICA DE PISADAS DINÁMICAS (RUNNING SNOW 1 & 2)                          //
        // ========================================================================= //

        private void ActualizarSonidoPisadas()
        {
            string anim = bossAI.AnimacionActual;
            bool estaMoviendose = anim == "Camina" || anim == "Trota" || anim == "Rum";

            // Si está atacando, en preparación, quieto o exhausto, reiniciar el temporizador de pisadas
            if (!estaMoviendose || (commandAttacks != null && (commandAttacks.EstaEjecutandoAtaqueEspecial || commandAttacks.EstaEjecutandoAtaqueMelee)))
            {
                temporizadorPasos = 0.08f;
                return;
            }

            float intervaloActual = intervaloTrota;
            float pitchBase = 1.0f;

            if (anim == "Camina")
            {
                intervaloActual = intervaloCamina;
                pitchBase = 0.94f;
            }
            else if (anim == "Rum")
            {
                intervaloActual = intervaloRum;
                pitchBase = 1.08f;
            }

            temporizadorPasos -= Time.deltaTime;
            if (temporizadorPasos <= 0f)
            {
                temporizadorPasos = intervaloActual;
                ReproducirPaso(pitchBase);
            }
        }

        /// <summary>
        /// Reproduce un paso alternando entre Running Snow 1 y Running Snow 2 con modulación de pitch.
        /// </summary>
        public void ReproducirPaso(float pitchBase = 1.0f)
        {
            if (audioSourceFootsteps == null) return;

            AudioClip clipElegido = clipRunningSnow1;
            if (clipRunningSnow2 != null)
            {
                clipElegido = usarSegundoClipPaso ? clipRunningSnow2 : clipRunningSnow1;
                usarSegundoClipPaso = !usarSegundoClipPaso;
            }

            if (clipElegido == null) clipElegido = clipRunningSnow1;
            if (clipElegido == null) return;

            audioSourceFootsteps.pitch = pitchBase + Random.Range(-variacionPitch, variacionPitch);
            audioSourceFootsteps.PlayOneShot(clipElegido, volumenPisadas);
        }

        // ========================================================================= //
        // MANEJADORES DE EVENTOS DE PODERES Y COMBATE                               //
        // ========================================================================= //

        private void ManejarAtaqueEspecialIniciado(string nombreAtaque)
        {
            switch (nombreAtaque)
            {
                case "MagicFire0":
                    ReproducirOneShot(clipMagicFire0Launch, 1.0f);
                    break;

                case "MagicFire1":
                    // Sonido telegrafiado de carga eléctrica y plasma
                    if (audioSourceLoop != null && clipMagicFire1Charge != null)
                    {
                        audioSourceLoop.clip = clipMagicFire1Charge;
                        audioSourceLoop.loop = true;
                        audioSourceLoop.volume = 0.95f;
                        audioSourceLoop.pitch = 1.0f;
                        audioSourceLoop.Play();
                    }
                    StartCoroutine(RutinaDisparoMagicFire1());
                    break;

                case "SaltaLoco":
                    ReproducirOneShot(clipSaltaLoco, 0.9f);
                    break;

                case "Taskkill":
                    ReproducirOneShot(clipTaskkill, 1.0f);
                    break;

                case "Firewall":
                    ReproducirOneShot(clipFirewallDeploy, 0.95f);
                    break;
            }
        }

        private IEnumerator RutinaDisparoMagicFire1()
        {
            // Espera el tiempo de telegrafiado antes del vuelo homing
            yield return new WaitForSeconds(1.5f);

            if (audioSourceLoop != null && audioSourceLoop.isPlaying)
            {
                audioSourceLoop.Stop();
            }

            ReproducirOneShot(clipMagicFire1Fly, 1.0f);
        }

        private void ManejarAtaqueEspecialTerminado(string nombreAtaque, bool exito)
        {
            if (nombreAtaque == "MagicFire1")
            {
                if (audioSourceLoop != null && audioSourceLoop.isPlaying)
                {
                    audioSourceLoop.Stop();
                }

                ReproducirOneShot(clipMagicFire1Explosion, 1.0f);
            }
            else if (nombreAtaque == "MagicFire0")
            {
                ReproducirOneShot(clipMagicFire0Impact, 1.0f);

                // Activar bucle de fuego de la trampa RotatorPS1
                if (audioSourceLoop != null && clipRotatorPS1Loop != null)
                {
                    audioSourceLoop.clip = clipRotatorPS1Loop;
                    audioSourceLoop.loop = true;
                    audioSourceLoop.volume = 0.70f;
                    audioSourceLoop.Play();
                    StartCoroutine(RutinaDetenerBucleFuego(2.6f));
                }
            }
        }

        private IEnumerator RutinaDetenerBucleFuego(float duracion)
        {
            yield return new WaitForSeconds(duracion);
            if (audioSourceLoop != null && audioSourceLoop.clip == clipRotatorPS1Loop)
            {
                audioSourceLoop.Stop();
            }
        }

        private void ManejarAtaqueMeleeIniciado(string nombreAtaque)
        {
            switch (nombreAtaque)
            {
                case "Patada":
                case "Izquiedazo":
                    ReproducirOneShot(clipMeleePatada, 0.9f);
                    break;

                case "Ataque_Patada":
                    ReproducirOneShot(clipMeleePesado, 1.0f);
                    break;

                case "Tornado":
                    ReproducirOneShot(clipMeleeTornado, 0.95f);
                    break;

                case "Salto":
                    ReproducirOneShot(clipMeleeSaltoImpacto, 1.0f);
                    break;
            }
        }

        private void ManejarCambioEstadoBoss(BossCombatState nuevoEstado)
        {
            if (nuevoEstado == BossCombatState.Exhausted)
            {
                estaEnPoseCansado = true;
                if (audioSourceLoop != null && clipExhaustedBreathing != null)
                {
                    audioSourceLoop.clip = clipExhaustedBreathing;
                    audioSourceLoop.loop = true;
                    audioSourceLoop.volume = 0.85f;
                    audioSourceLoop.pitch = 0.95f;
                    audioSourceLoop.Play();
                }
                ReproducirOneShot(clipExhaustedSparks, 0.85f);
            }
            else if (estaEnPoseCansado)
            {
                estaEnPoseCansado = false;
                if (audioSourceLoop != null && (audioSourceLoop.clip == clipExhaustedBreathing || audioSourceLoop.clip == clipExhaustedSparks))
                {
                    audioSourceLoop.Stop();
                }
            }
        }

        private void ManejarBSOD()
        {
            ReproducirOneShot(clipBSOD, 1.0f);
        }

        private void ManejarMuerteBoss()
        {
            DetenerTodosLosSonidos();
            ReproducirOneShot(clipDie, 1.0f);
        }

        public void ReproducirImpactoHit()
        {
            ReproducirOneShot(clipHit, 0.75f);
        }

        public void ReproducirBloqueoFirewall()
        {
            ReproducirOneShot(clipFirewallBlock, 0.85f);
        }

        private void ReproducirOneShot(AudioClip clip, float volumen = 1.0f)
        {
            if (audioSourcePowers == null || clip == null) return;
            audioSourcePowers.pitch = 1.0f + Random.Range(-0.03f, 0.03f);
            audioSourcePowers.PlayOneShot(clip, volumen);
        }

        public void DetenerTodosLosSonidos()
        {
            if (audioSourceFootsteps != null && audioSourceFootsteps.isPlaying) audioSourceFootsteps.Stop();
            if (audioSourcePowers != null && audioSourcePowers.isPlaying) audioSourcePowers.Stop();
            if (audioSourceLoop != null && audioSourceLoop.isPlaying) audioSourceLoop.Stop();
        }

        // ========================================================================= //
        // CONFIGURACIÓN AUTOMÁTICA Y CARGA DE ASSETS                                //
        // ========================================================================= //

        public void ConfigurarCanalesAudio3D()
        {
            if (audioSourceFootsteps == null)
            {
                Transform tFoot = transform.Find("Audio_Footsteps");
                if (tFoot == null)
                {
                    GameObject goFoot = new GameObject("Audio_Footsteps");
                    goFoot.transform.SetParent(transform, false);
                    audioSourceFootsteps = goFoot.AddComponent<AudioSource>();
                }
                else
                {
                    audioSourceFootsteps = tFoot.GetComponent<AudioSource>() ?? tFoot.gameObject.AddComponent<AudioSource>();
                }
            }

            if (audioSourcePowers == null)
            {
                Transform tPow = transform.Find("Audio_Powers");
                if (tPow == null)
                {
                    GameObject goPow = new GameObject("Audio_Powers");
                    goPow.transform.SetParent(transform, false);
                    audioSourcePowers = goPow.AddComponent<AudioSource>();
                }
                else
                {
                    audioSourcePowers = tPow.GetComponent<AudioSource>() ?? tPow.gameObject.AddComponent<AudioSource>();
                }
            }

            if (audioSourceLoop == null)
            {
                Transform tLoop = transform.Find("Audio_Loop");
                if (tLoop == null)
                {
                    GameObject goLoop = new GameObject("Audio_Loop");
                    goLoop.transform.SetParent(transform, false);
                    audioSourceLoop = goLoop.AddComponent<AudioSource>();
                }
                else
                {
                    audioSourceLoop = tLoop.GetComponent<AudioSource>() ?? tLoop.gameObject.AddComponent<AudioSource>();
                }
            }

            CalibrarAudioSource3D(audioSourceFootsteps);
            CalibrarAudioSource3D(audioSourcePowers);
            CalibrarAudioSource3D(audioSourceLoop);
        }

        private void CalibrarAudioSource3D(AudioSource src)
        {
            if (src == null) return;
            src.playOnAwake = false;
            src.spatialBlend = 1.0f; // 100% Espacialización 3D
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 2.0f;
            src.maxDistance = 35.0f;
            src.dopplerLevel = 0.4f;
        }

        /// <summary>
        /// Carga automáticamente todos los clips solicitados desde Assets/Multiple Solutions/Basic RPG Sounds/Audio/.
        /// </summary>
        public void GarantizarClipsDeAudio()
        {
#if UNITY_EDITOR
            const string baseDir = "Assets/Multiple Solutions/Basic RPG Sounds/Audio/";

            if (clipRunningSnow1 == null)
                clipRunningSnow1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Running Snow 1.wav");

            if (clipRunningSnow2 == null)
                clipRunningSnow2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Running Snow 2.wav");

            if (clipMagicFire0Launch == null)
                clipMagicFire0Launch = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Missile Fire 2.wav");

            if (clipMagicFire0Impact == null)
                clipMagicFire0Impact = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Explosion 2.wav");

            if (clipRotatorPS1Loop == null)
                clipRotatorPS1Loop = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Outdoor Fire(Loop).wav");

            if (clipMagicFire1Charge == null)
                clipMagicFire1Charge = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Gattling Laser 4.wav");

            if (clipMagicFire1Fly == null)
                clipMagicFire1Fly = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Flying 1.wav");

            if (clipMagicFire1Explosion == null)
                clipMagicFire1Explosion = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Explosion 6 Nuclear.wav");

            if (clipExhaustedBreathing == null)
                clipExhaustedBreathing = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Running Snow 3(Breathing).wav");

            if (clipExhaustedSparks == null)
                clipExhaustedSparks = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Electricity 4.wav");

            if (clipSaltaLoco == null)
                clipSaltaLoco = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Flying 2.wav");

            if (clipTaskkill == null)
                clipTaskkill = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Safe Cracking 10(Single Click).wav");

            if (clipFirewallDeploy == null)
                clipFirewallDeploy = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Pressure Containment Unlock 1.wav");

            if (clipFirewallBlock == null)
                clipFirewallBlock = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Bullet Impact.wav");

            if (clipMeleePatada == null)
                clipMeleePatada = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Bumper LEFT 2.wav");

            if (clipMeleePesado == null)
                clipMeleePesado = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Bumper RIGHT.wav");

            if (clipMeleeTornado == null)
                clipMeleeTornado = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Flying 1.wav");

            if (clipMeleeSaltoImpacto == null)
                clipMeleeSaltoImpacto = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Explosion 0.wav");

            if (clipHit == null)
                clipHit = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Bullet Impact.wav");

            if (clipDie == null)
                clipDie = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Glass Breaking Electricity 1.wav");

            if (clipBSOD == null)
                clipBSOD = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(baseDir + "Glass Breaking Electricity 2.wav");
#endif
        }

        // ========================================================================= //
        // PROPIEDADES PÚBLICAS                                                      //
        // ========================================================================= //

        public AudioSource FootstepsSource => audioSourceFootsteps;
        public AudioSource PowersSource => audioSourcePowers;
        public AudioSource LoopSource => audioSourceLoop;
        public AudioSource SourceFootsteps => audioSourceFootsteps;
        public AudioSource SourcePowers => audioSourcePowers;
        public AudioSource SourceLoop => audioSourceLoop;
        public AudioClip ClipRunningSnow1 => clipRunningSnow1;
        public AudioClip ClipRunningSnow2 => clipRunningSnow2;
        public float VolumenPisadas { get => volumenPisadas; set => volumenPisadas = value; }
    }
}
