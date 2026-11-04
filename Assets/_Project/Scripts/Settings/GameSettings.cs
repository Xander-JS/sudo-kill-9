using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace SudoKill9.Settings
{
    public enum AudioChannel { Music, Sfx }

    /// <summary>
    /// Fuente única de verdad de las preferencias del jugador (PlayerPrefs).
    /// Se aplica sola al arrancar el juego, así el idioma/volumen/pantalla guardados rigen
    /// aunque el jugador nunca abra la escena Configuraciones.
    /// </summary>
    public static class GameSettings
    {
        private const string KeyMaster = "settings.volume.master";
        private const string KeyMusic = "settings.volume.music";
        private const string KeySfx = "settings.volume.sfx";
        private const string KeyLocale = "settings.locale";
        private const string KeyFullscreen = "settings.video.fullscreen";
        private const string KeyResWidth = "settings.video.width";
        private const string KeyResHeight = "settings.video.height";

        /// <summary>Se dispara al cambiar Música o SFX (Master va directo a AudioListener).</summary>
        public static event Action VolumeChanged;

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(KeyMaster, 1f);
            set
            {
                value = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(KeyMaster, value);
                AudioListener.volume = value;
            }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(KeyMusic, 0.8f);
            set { PlayerPrefs.SetFloat(KeyMusic, Mathf.Clamp01(value)); VolumeChanged?.Invoke(); }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(KeySfx, 1f);
            set { PlayerPrefs.SetFloat(KeySfx, Mathf.Clamp01(value)); VolumeChanged?.Invoke(); }
        }

        public static float GetVolume(AudioChannel channel) =>
            channel == AudioChannel.Music ? MusicVolume : SfxVolume;

        /// <summary>Código de locale guardado ("es", "en") o vacío si nunca se eligió.</summary>
        public static string LocaleCode
        {
            get => PlayerPrefs.GetString(KeyLocale, string.Empty);
            set
            {
                PlayerPrefs.SetString(KeyLocale, value);
                ApplyLocale(value);
            }
        }

        public static bool Fullscreen => PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;

        /// <summary>Resolución guardada, o la actual de la ventana si nunca se guardó.</summary>
        public static Vector2Int Resolution => new Vector2Int(
            PlayerPrefs.GetInt(KeyResWidth, Screen.width),
            PlayerPrefs.GetInt(KeyResHeight, Screen.height));

        public static void SetDisplay(int width, int height, bool fullscreen)
        {
            PlayerPrefs.SetInt(KeyResWidth, width);
            PlayerPrefs.SetInt(KeyResHeight, height);
            PlayerPrefs.SetInt(KeyFullscreen, fullscreen ? 1 : 0);
            Screen.SetResolution(width, height,
                fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        /// <summary>Escribe a disco. Los setters solo tocan memoria; llamar al salir de la pantalla.</summary>
        public static void Save() => PlayerPrefs.Save();

        // ============================================================== //
        // Arranque                                                       //
        // ============================================================== //

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedOnBoot()
        {
            AudioListener.volume = MasterVolume;

#if !UNITY_EDITOR
            // En el editor la ventana Game no admite cambiar resolución; solo en build.
            if (PlayerPrefs.HasKey(KeyResWidth))
            {
                Vector2Int res = Resolution;
                Screen.SetResolution(res.x, res.y,
                    Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            }
#endif

            string code = LocaleCode;
            if (!string.IsNullOrEmpty(code))
                LocalizationSettings.InitializationOperation.Completed += _ => ApplyLocale(code);
        }

        private static void ApplyLocale(string code)
        {
            var locale = LocalizationSettings.AvailableLocales.Locales
                .FirstOrDefault(l => l.Identifier.Code == code);
            if (locale != null && LocalizationSettings.SelectedLocale != locale)
                LocalizationSettings.SelectedLocale = locale;
        }
    }
}
