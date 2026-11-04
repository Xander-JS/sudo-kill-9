using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SudoKill9.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace SudoKill9.UI.Configuraciones
{
    /// <summary>
    /// Pantalla de Configuraciones: idioma, audio y video. Cada control se aplica en tiempo real
    /// a través de <see cref="GameSettings"/>; al volver a Inicio se escribe todo a disco.
    /// Los listeners se enlazan en código, no hace falta cablear eventos en el Inspector.
    /// </summary>
    public class ConfiguracionesController : MonoBehaviour
    {
        [Header("Idioma")]
        [SerializeField] private TMP_Dropdown languageDropdown;

        [Header("Audio")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [Tooltip("Opcionales: muestran el porcentaje junto a cada slider.")]
        [SerializeField] private TMP_Text masterValue;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private TMP_Text sfxValue;

        [Header("Pantalla")]
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Dropdown resolutionDropdown;

        [Header("Navegación")]
        [SerializeField] private Button backButton;
        [SerializeField] private string menuScene = "Inicio";

        private List<Locale> locales = new List<Locale>();
        private List<Vector2Int> resolutions = new List<Vector2Int>();

        private IEnumerator Start()
        {
            backButton.onClick.AddListener(GoBack);

            BindSlider(masterSlider, masterValue, GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
            BindSlider(musicSlider, musicValue, GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);
            BindSlider(sfxSlider, sfxValue, GameSettings.SfxVolume, v => GameSettings.SfxVolume = v);

            SetupDisplay();

            // Los locales se cargan asíncronos: el dropdown de idioma se rellena cuando están.
            yield return LocalizationSettings.InitializationOperation;
            SetupLanguage();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) GoBack();
        }

        // ============================================================== //
        // Idioma                                                         //
        // ============================================================== //

        private void SetupLanguage()
        {
            locales = LocalizationSettings.AvailableLocales.Locales;
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(locales.Select(DisplayName).ToList());
            languageDropdown.SetValueWithoutNotify(Mathf.Max(0, locales.IndexOf(LocalizationSettings.SelectedLocale)));
            languageDropdown.onValueChanged.AddListener(i => GameSettings.LocaleCode = locales[i].Identifier.Code);
        }

        // Los nombres de idioma se muestran siempre en su propio idioma (convención de UX).
        private static string DisplayName(Locale locale)
        {
            switch (locale.Identifier.Code)
            {
                case "es": return "Español";
                case "en": return "English";
                default: return locale.LocaleName;
            }
        }

        // ============================================================== //
        // Audio                                                          //
        // ============================================================== //

        private static void BindSlider(Slider slider, TMP_Text label, float initial, System.Action<float> apply)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(initial);
            SetPercent(label, initial);
            slider.onValueChanged.AddListener(v =>
            {
                apply(v);
                SetPercent(label, v);
            });
        }

        private static void SetPercent(TMP_Text label, float v)
        {
            if (label != null) label.text = Mathf.RoundToInt(v * 100f) + "%";
        }

        // ============================================================== //
        // Pantalla                                                       //
        // ============================================================== //

        private void SetupDisplay()
        {
            // Screen.resolutions repite cada tamaño por frecuencia de refresco: quedarse con tamaños únicos.
            resolutions = Screen.resolutions
                .Select(r => new Vector2Int(r.width, r.height))
                .Distinct()
                .OrderBy(r => r.x).ThenBy(r => r.y)
                .ToList();

            Vector2Int current = GameSettings.Resolution;
            if (!resolutions.Contains(current)) resolutions.Add(current);

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(resolutions.Select(r => $"{r.x} x {r.y}").ToList());
            resolutionDropdown.SetValueWithoutNotify(resolutions.IndexOf(current));
            resolutionDropdown.onValueChanged.AddListener(_ => ApplyDisplay());

            fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
            fullscreenToggle.onValueChanged.AddListener(_ => ApplyDisplay());
        }

        private void ApplyDisplay()
        {
            Vector2Int res = resolutions[resolutionDropdown.value];
            GameSettings.SetDisplay(res.x, res.y, fullscreenToggle.isOn);
        }

        // ============================================================== //
        // Navegación                                                     //
        // ============================================================== //

        public void GoBack()
        {
            GameSettings.Save();
            SceneFader.LoadScene(menuScene);
        }

        // Cerrar el juego desde esta pantalla tampoco debe perder lo elegido.
        private void OnApplicationQuit() => GameSettings.Save();
    }
}
