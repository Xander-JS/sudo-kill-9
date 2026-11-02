using UnityEngine;

namespace SudoKill9.Enemies
{
    /// <summary>
    /// Reproducción 3D de SFX común a todos los enemigos: un AudioSource para efectos puntuales
    /// y otro en bucle para la locomoción. Cada enemigo solo asigna sus clips.
    /// </summary>
    public abstract class EnemyAudioBase : MonoBehaviour
    {
        [Header("SFX de Combate y Estados")]
        [SerializeField] protected AudioClip detectionClip;
        [Tooltip("Variantes de ataque; se elige por índice (índice % cantidad).")]
        [SerializeField] protected AudioClip[] attackClips = new AudioClip[0];
        [SerializeField] protected AudioClip hitDamageClip;
        [SerializeField] protected AudioClip deathClip;

        [Header("SFX de Movimiento")]
        [SerializeField] protected AudioClip normalMoveClip;
        [SerializeField] protected AudioClip crawlMoveClip;

        [Header("Volúmenes")]
        [Range(0f, 1f)] [SerializeField] protected float effectsVolume = 1f;
        [Range(0f, 1f)] [SerializeField] protected float movementVolume = 0.8f;

        [Header("Atenuación 3D")]
        [Tooltip("Hasta esta distancia del AudioListener el sonido se oye a volumen completo.")]
        [SerializeField] protected float minDistance = 4f;
        [Tooltip("A partir de esta distancia el sonido deja de oírse (rolloff lineal).")]
        [SerializeField] protected float maxDistance = 40f;

        [Header("AudioSources 3D (opcional; se crean solos si están vacíos)")]
        [SerializeField] protected AudioSource effectsSource;
        [SerializeField] protected AudioSource movementSource;

        protected virtual void Awake()
        {
            if (effectsSource == null) effectsSource = Create3DSource(false);
            if (movementSource == null) movementSource = Create3DSource(true);

            // Los clips importan con Preload Audio Data desactivado: cargarlos ya evita que el primer one-shot (detección) se pierda.
            PreloadClip(detectionClip);
            PreloadClip(hitDamageClip);
            PreloadClip(deathClip);
            PreloadClip(normalMoveClip);
            PreloadClip(crawlMoveClip);
            foreach (AudioClip clip in attackClips) PreloadClip(clip);
        }

        protected AudioSource Create3DSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            return source;
        }

        private static void PreloadClip(AudioClip clip)
        {
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }

        public virtual void PlayDetection() => PlayOneShot(detectionClip);

        public virtual void PlayAttack(int attackIndex)
        {
            if (attackClips.Length == 0) return;
            PlayOneShot(attackClips[Mathf.Abs(attackIndex) % attackClips.Length]);
        }

        public virtual void PlayHit() => PlayOneShot(hitDamageClip);

        public virtual void PlayDeath()
        {
            StopMovementAudio();
            PlayOneShot(deathClip);
        }

        /// <summary>Bucle de locomoción: pasos normales o arrastre. Seguro de llamar cada frame (solo cambia si hace falta).</summary>
        public virtual void UpdateMovementAudio(bool isMoving, bool isCrawling)
        {
            AudioClip targetClip = isMoving ? (isCrawling ? crawlMoveClip : normalMoveClip) : null;
            if (targetClip == null)
            {
                StopMovementAudio();
                return;
            }

            if (movementSource.clip != targetClip || !movementSource.isPlaying)
            {
                movementSource.clip = targetClip;
                movementSource.volume = movementVolume;
                movementSource.Play();
            }
        }

        public void StopMovementAudio()
        {
            if (movementSource != null && movementSource.isPlaying) movementSource.Stop();
        }

        protected void PlayOneShot(AudioClip clip)
        {
            if (clip != null && effectsSource != null) effectsSource.PlayOneShot(clip, effectsVolume);
        }

        /// <summary>Asigna los clips (usado por los scripts de configuración del Editor).</summary>
        public void SetClips(AudioClip detection, AudioClip normalMove, AudioClip crawlMove,
                             AudioClip[] attacks, AudioClip hitDamage, AudioClip death)
        {
            detectionClip = detection;
            normalMoveClip = normalMove;
            crawlMoveClip = crawlMove;
            attackClips = attacks;
            hitDamageClip = hitDamage;
            deathClip = death;
        }
    }
}
