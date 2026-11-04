using UnityEngine;

namespace SudoKill9.Enemies.Volador
{
    /// <summary>
    /// SFX del Volador. Hereda toda la reproducción 3D (volumen 1, rolloff lineal 4–40 m);
    /// los clips base se asignan con Tools/Setup Volador Enemy.
    /// attackClips[0] = impacto del ataque. landingAttackClip = descenso en picado (se asigna en el inspector).
    /// El bucle de movimiento es el zumbido de los rotores; no tiene clip de arrastre.
    /// </summary>
    public class VoladorAudio : EnemyAudioBase
    {
        [Header("SFX de Descenso / Aterrizaje")]
        [Tooltip("Suena cuando el Volador empieza a descender hacia el jugador para atacar.")]
        [SerializeField] private AudioClip landingAttackClip;

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
            if (landingAttackClip != null && landingAttackClip.loadState == AudioDataLoadState.Unloaded) landingAttackClip.LoadAudioData();
        }

        /// <summary>One-shot por el AudioSource de efectos al iniciar el descenso de ataque.</summary>
        public void PlayLandingAttackSound() => PlayOneShot(landingAttackClip);
    }
}
