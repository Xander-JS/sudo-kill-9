using UnityEngine;

namespace SudoKill9.Enemies.Espia
{
    /// <summary>
    /// SFX de espia_opt. Detección, daño, muerte y locomoción usan la reproducción 3D de EnemyAudioBase (lineal 4 → 40 m).
    /// Los disparos del ojo van por un AudioSource propio ("laserSource") con mayor alcance y pitch aleatorio:
    ///   - attackClips tiene exactamente 2 clips: [0] disparo rápido, [1] disparo cargado (AttackIndex 0 / 1).
    ///   - Pitch aleatorio 0.95–1.05 por disparo, sin afectar al pitch del resto de efectos.
    ///   - Atenuación: volumen completo hasta 8 m, curva personalizada con cola larga hasta 60 m,
    ///     para que un disparo desde el borde del alcance (10 m) se oiga claro y "viaje" por el escenario.
    /// </summary>
    public class EspiaAudio : EnemyAudioBase
    {
        public const int LaserClipCount = 2;

        [Header("Láser (AudioSource de disparo)")]
        [Tooltip("AudioSource exclusivo para los disparos. Se crea solo si está vacío.")]
        [SerializeField] private AudioSource laserSource;
        [Range(0f, 1f)] [SerializeField] private float laserVolume = 1f;
        [SerializeField] private float laserMinDistance = 8f;
        [SerializeField] private float laserMaxDistance = 60f;
        [SerializeField] private Vector2 laserPitchRange = new Vector2(0.95f, 1.05f);
        [Tooltip("Extensión estéreo en grados: un haz suena más 'ancho' que un golpe puntual.")]
        [Range(0f, 360f)] [SerializeField] private float laserSpread = 40f;

        private void Reset()
        {
            effectsVolume = 1f;
            movementVolume = 0.8f;
            minDistance = 4f;
            maxDistance = 40f;
        }

        protected override void Awake()
        {
            base.Awake();
            if (laserSource == null) laserSource = Create3DSource(false);
            ConfigureLaserSource(laserSource);
        }

        /// <summary>Ajustes 3D del disparo. Público para que el script de Editor configure el AudioSource serializado.</summary>
        public void ConfigureLaserSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 1f;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.spread = laserSpread;
            source.minDistance = laserMinDistance;
            source.maxDistance = laserMaxDistance;
            source.rolloffMode = AudioRolloffMode.Custom;
            // Eje X normalizado sobre maxDistance (60 m): 0.133 ≈ 8 m (volumen pleno), 0.5 = 30 m, 1 = 60 m (silencio).
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(laserMinDistance / laserMaxDistance, 1f),
                new Keyframe(0.5f, 0.35f),
                new Keyframe(1f, 0f)));
        }

        public override void PlayAttack(int attackIndex)
        {
            if (attackClips.Length == 0 || laserSource == null) return;
            AudioClip clip = attackClips[Mathf.Abs(attackIndex) % Mathf.Min(attackClips.Length, LaserClipCount)];
            if (clip == null) return;

            laserSource.pitch = Random.Range(laserPitchRange.x, laserPitchRange.y);
            laserSource.PlayOneShot(clip, effectsVolume * laserVolume);
        }

        public void SetLaserSource(AudioSource source) => laserSource = source;
    }
}
