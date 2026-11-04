using UnityEngine;

namespace SudoKill9.Settings
{
    /// <summary>
    /// Escala el volumen de este AudioSource por el canal elegido (Música o SFX/Voz) de <see cref="GameSettings"/>.
    /// El volumen configurado en el AudioSource se toma como "volumen de mezcla" base.
    /// El volumen general (Master) ya lo aplica AudioListener.volume a todo el juego.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioVolumeChannel : MonoBehaviour
    {
        [SerializeField] private AudioChannel channel = AudioChannel.Sfx;

        private AudioSource source;
        private float baseVolume;

        public AudioChannel Channel
        {
            get => channel;
            set { channel = value; Apply(); }
        }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            baseVolume = source.volume;
        }

        private void OnEnable()
        {
            GameSettings.VolumeChanged += Apply;
            Apply();
        }

        private void OnDisable() => GameSettings.VolumeChanged -= Apply;

        private void Apply()
        {
            if (source != null) source.volume = baseVolume * GameSettings.GetVolume(channel);
        }
    }
}
