using System;
using System.Collections;
using SudoKill9.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SudoKill9.UI
{
    /// <summary>
    /// Música de fondo del menú, persistente entre las escenas de menú (Inicio ↔ Configuraciones)
    /// sin reiniciar la pista. Se crea por código (DontDestroyOnLoad); no hace falta ponerlo en ninguna escena.
    /// Respeta el slider "Música" mediante <see cref="AudioVolumeChannel"/>.
    /// Al cargar cualquier escena que no sea de menú (Credits, gameplay...) se apaga con un fundido corto.
    /// Uso: <c>MenuMusicPlayer.Play(clip, volumen)</c> / <c>MenuMusicPlayer.Stop()</c>.
    /// </summary>
    public class MenuMusicPlayer : MonoBehaviour
    {
        /// <summary>Escenas donde la música del menú sigue sonando.</summary>
        private static readonly string[] MenuScenes = { "Inicio", "Configuraciones" };

        private static MenuMusicPlayer instance;

        private AudioSource source;
        private AudioVolumeChannel channel;
        private bool stopping;

        /// <summary>
        /// Reproduce <paramref name="clip"/> en bucle. Si ya está sonando ese mismo clip, no lo reinicia
        /// (así volver de Configuraciones a Inicio no corta la música).
        /// </summary>
        public static void Play(AudioClip clip, float volume)
        {
            if (clip == null) return;

            if (instance != null && !instance.stopping && instance.source.clip == clip && instance.source.isPlaying)
                return;

            if (instance != null) Destroy(instance.gameObject);

            var go = new GameObject("MenuMusic");
            DontDestroyOnLoad(go);

            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = volume; // volumen de mezcla base: AudioVolumeChannel lo toma en su Awake

            instance = go.AddComponent<MenuMusicPlayer>();
            instance.source = src;
            instance.channel = go.AddComponent<AudioVolumeChannel>();
            instance.channel.Channel = AudioChannel.Music;

            src.Play();
        }

        /// <summary>Detiene la música del menú con un fundido de <paramref name="fadeDuration"/> segundos.</summary>
        public static void Stop(float fadeDuration = 0.5f)
        {
            if (instance == null || instance.stopping) return;
            instance.StartCoroutine(instance.FadeOutAndDestroy(fadeDuration));
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single && Array.IndexOf(MenuScenes, scene.name) < 0)
                Stop(0.3f);
        }

        private IEnumerator FadeOutAndDestroy(float duration)
        {
            stopping = true;
            channel.enabled = false; // que el canal de volumen no pise el fundido

            float from = source.volume;
            for (float t = 0f; t < 1f && duration > 0f; t += Time.unscaledDeltaTime / duration)
            {
                source.volume = Mathf.Lerp(from, 0f, t);
                yield return null;
            }

            source.Stop();
            Destroy(gameObject);
        }

        // Con "Enter Play Mode Options" (sin recarga de dominio) la referencia estática no debe quedar colgada.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;
    }
}
