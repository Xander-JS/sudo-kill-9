using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace SudoKill9.UI
{
    /// <summary>
    /// Mantiene los 4 botones del menú principal (Inicio) traducidos entre Español e Inglés.
    ///
    /// Fuente de verdad: <c>PlayerPrefs["SelectedLanguage"]</c> ("es" / "en"). Para seguir siendo
    /// coherente con el sistema existente del proyecto (la pantalla de Configuración usa un dropdown
    /// que escribe <c>settings.locale</c> y cambia el locale de Unity Localization), este componente:
    ///   • Al iniciar, resuelve el idioma efectivo (prioriza <c>settings.locale</c> si existe, luego
    ///     <c>SelectedLanguage</c>, por defecto "es") y aplica las etiquetas.
    ///   • Se suscribe a <see cref="LocalizationSettings.SelectedLocaleChanged"/> para actualizarse en
    ///     tiempo real si el idioma cambia mientras el menú está vivo.
    ///   • Al cambiar idioma, guarda ambas claves y aplica el locale de Unity, así todo el juego queda
    ///     sincronizado.
    ///
    /// Soporta tanto <see cref="TMP_Text"/> (TextMeshProUGUI) como <see cref="Text"/> en los botones o
    /// sus hijos, de modo que el reemplazo de texto nunca falla.
    /// </summary>
    public class MainMenuLocalizationSync : MonoBehaviour
    {
        public const string PrefKey       = "SelectedLanguage"; // clave requerida por la especificación
        private const string LegacyLocaleKey = "settings.locale"; // clave del sistema de Configuración existente

        [System.Serializable]
        public class LocalizedButtonLabel
        {
            [Tooltip("Botón del menú cuyo texto se traduce (se busca TMP/Text en él o en sus hijos).")]
            public Button button;
            [TextArea] public string spanish;
            [TextArea] public string english;
        }

        [Header("Botones del menú principal (ES / EN)")]
        [SerializeField] private LocalizedButtonLabel[] buttons;

        [Header("Idioma por defecto si no hay nada guardado")]
        [SerializeField] private string defaultLanguage = "es";

        private string _current = "es";

        /// <summary>Código de idioma activo ("es" / "en").</summary>
        public string CurrentLanguage => _current;

        // ── Ciclo de vida ────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Actualización en tiempo real cuando cambia el locale (p. ej. desde Configuración).
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void Start()
        {
            _current = ResolveEffectiveCode();
            PlayerPrefs.SetString(PrefKey, _current);
            PlayerPrefs.Save();
            ApplyLabels();
        }

        // ── API pública ──────────────────────────────────────────────────────

        public void SetLanguageEnglish() => SetLanguage("en");
        public void SetLanguageSpanish() => SetLanguage("es");
        public void ToggleLanguage()     => SetLanguage(_current == "en" ? "es" : "en");

        /// <summary>Fija el idioma, lo persiste, actualiza los 4 botones y aplica el locale de Unity.</summary>
        public void SetLanguage(string code)
        {
            _current = Normalize(code);

            PlayerPrefs.SetString(PrefKey, _current);
            PlayerPrefs.SetString(LegacyLocaleKey, _current); // mantiene coherencia con Configuración
            PlayerPrefs.Save();

            ApplyLabels();
            TryApplyUnityLocale(_current);
        }

        // ── Resolución / eventos ─────────────────────────────────────────────

        private string ResolveEffectiveCode()
        {
            // El dropdown de Configuración es la fuente canónica del proyecto: si ya eligió algo,
            // el menú debe reflejarlo. Si no, se usa SelectedLanguage y, por último, el valor por defecto.
            string cfg = PlayerPrefs.GetString(LegacyLocaleKey, string.Empty);
            if (!string.IsNullOrEmpty(cfg)) return Normalize(cfg);

            string sel = PlayerPrefs.GetString(PrefKey, string.Empty);
            if (!string.IsNullOrEmpty(sel)) return Normalize(sel);

            return Normalize(defaultLanguage);
        }

        private void OnLocaleChanged(Locale locale)
        {
            if (locale == null) return;
            _current = Normalize(locale.Identifier.Code);
            PlayerPrefs.SetString(PrefKey, _current);
            PlayerPrefs.SetString(LegacyLocaleKey, _current);
            PlayerPrefs.Save();
            ApplyLabels();
        }

        private static string Normalize(string code) =>
            !string.IsNullOrEmpty(code) && code.Trim().ToLowerInvariant().StartsWith("en") ? "en" : "es";

        // ── Aplicación de etiquetas ──────────────────────────────────────────

        public void ApplyLabels()
        {
            if (buttons == null) return;
            bool en = _current == "en";

            foreach (var entry in buttons)
            {
                if (entry == null || entry.button == null) continue;
                SetText(entry.button.gameObject, en ? entry.english : entry.spanish);
            }
        }

        /// <summary>Asigna texto soportando TMP_Text y UnityEngine.UI.Text (incluye hijos inactivos).</summary>
        private static void SetText(GameObject go, string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            var tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) { tmp.text = value; return; }

            var uiText = go.GetComponentInChildren<Text>(true);
            if (uiText != null) uiText.text = value;
        }

        private static void TryApplyUnityLocale(string code)
        {
            try
            {
                var locales = LocalizationSettings.AvailableLocales?.Locales;
                if (locales == null) return;
                var loc = locales.FirstOrDefault(l => l.Identifier.Code == code);
                if (loc != null && LocalizationSettings.SelectedLocale != loc)
                    LocalizationSettings.SelectedLocale = loc;
            }
            catch { /* Localization aún no inicializado: las etiquetas ya se aplicaron igualmente. */ }
        }
    }
}
