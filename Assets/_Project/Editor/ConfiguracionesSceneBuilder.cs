using System;
using System.Linq;
using SudoKill9.UI.Configuraciones;
using SudoKill9.UI.Inicio;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Genera (o regenera) la escena Configuraciones:
    /// 1. String Table Collection "Settings" (es/en) con todas las etiquetas.
    /// 2. Escena Configuraciones.unity con secciones Idioma / Audio / Pantalla, botón Volver,
    ///    ConfiguracionesController con todas sus referencias asignadas, y alta en Build Settings.
    /// Idempotente: re-ejecutarlo actualiza textos y reconstruye la escena.
    /// </summary>
    public static class ConfiguracionesSceneBuilder
    {
        private const string TablesDir = "Assets/_Project/Localization/Tables";
        private const string TableName = "Settings";
        private const string ScenePath = "Assets/_Project/Scenes/Configuraciones.unity";
        private const string FontPath = "Assets/_Project/Fonts/PixelTactical_Point.asset";

        // Paleta de Inicio (InicioPixelArtUISetup / CreditsSceneBuilder).
        private static readonly Color Background = new Color(0.035f, 0.045f, 0.055f);
        private static readonly Color PanelBg = new Color(0.08f, 0.12f, 0.18f, 0.85f);
        private static readonly Color ControlBg = new Color(0.15f, 0.17f, 0.18f, 1f);
        private static readonly Color TitleWhite = new Color(0.95f, 0.98f, 1f);
        private static readonly Color TextNormal = new Color(0.86f, 0.90f, 0.88f);
        private static readonly Color TextDim = new Color(0.86f, 0.90f, 0.88f, 0.55f);
        // Verde primario: mismo tono (~150°) que el verde militar de Inicio (0.14, 0.22, 0.18), aclarado para líneas/acentos.
        private static readonly Color AccentGreen = new Color(0.56f, 0.88f, 0.72f);
        private static readonly Color HoverGreen = new Color(0.45f, 0.78f, 0.60f);
        private static readonly Color TextHoverGreen = new Color(0.72f, 1f, 0.86f);
        private static readonly Color AccentRust = new Color(1.00f, 0.62f, 0.28f);

        private static readonly (string key, string es, string en)[] Strings =
        {
            ("settings_title", "CONFIGURACIÓN", "SETTINGS"),
            ("settings_subtitle", "// parámetros del sistema", "// system parameters"),
            ("settings_language_header", "IDIOMA", "LANGUAGE"),
            ("settings_language", "Idioma", "Language"),
            ("settings_audio_header", "AUDIO", "AUDIO"),
            ("settings_master", "Volumen general", "Master volume"),
            ("settings_music", "Música", "Music"),
            ("settings_sfx", "Efectos y voces", "Effects & voice"),
            ("settings_video_header", "PANTALLA", "DISPLAY"),
            ("settings_fullscreen", "Pantalla completa", "Fullscreen"),
            ("settings_resolution", "Resolución", "Resolution"),
            ("settings_back", "[ < VOLVER ]", "[ < BACK ]"),
            ("settings_hint", "[ESC] Volver al menú", "[ESC] Back to menu"),
        };

        private static TMP_FontAsset font;

        [MenuItem("Tools/The Last Root/Build Configuraciones Scene (Localized)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            CreditsSceneBuilder.EnsureLocalization();
            FillStringTable();
            BuildScene();

            Debug.Log($"[ConfiguracionesSceneBuilder] Escena '{ScenePath}' y tabla '{TableName}' (es/en) generadas.");
        }

        private static void FillStringTable()
        {
            CreditsSceneBuilder.EnsureFolder(TablesDir);
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(TableName)
                ?? LocalizationEditorSettings.CreateStringTableCollection(TableName, TablesDir);

            foreach (string code in new[] { "es", "en" })
            {
                var id = new UnityEngine.Localization.LocaleIdentifier(code);
                var table = (StringTable)collection.GetTable(id) ?? (StringTable)collection.AddNewTable(id);
                foreach (var s in Strings) table.AddEntry(s.key, code == "es" ? s.es : s.en);
                EditorUtility.SetDirty(table);
            }

            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
        }

        // ============================================================== //
        // Escena                                                         //
        // ============================================================== //

        private static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // --- Canvas raíz ---
            var canvasGo = new GameObject("Configuraciones_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGo.transform;

            Stretch(NewUI("Background", root, typeof(Image))).GetComponent<Image>().color = Background;

            // --- Encabezado (mismo lenguaje visual que Inicio: título + subtítulo + línea de acento) ---
            var header = NewUI("Header", root);
            Anchor(header, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(80f, -70f), new Vector2(900f, 130f));
            var title = LocalizedText(header, "settings_title", 72, TitleWhite, FontStyles.Bold, 8f, TextAlignmentOptions.TopLeft);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 80f));
            var sub = LocalizedText(header, "settings_subtitle", 24, AccentGreen, FontStyles.Normal, 2f, TextAlignmentOptions.TopLeft);
            Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -82f), new Vector2(0f, 32f));
            var line = NewUI("Accent_Line", header, typeof(Image));
            line.GetComponent<Image>().color = new Color(AccentGreen.r, AccentGreen.g, AccentGreen.b, 0.75f);
            Anchor(line, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -122f), new Vector2(420f, 2f));

            // --- Panel central con las tres secciones ---
            var panel = NewUI("Settings_Panel", root, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1000f, 0f));
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.GetComponent<Image>().color = PanelBg;
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(56, 56, 40, 44);
            vlg.spacing = 14f;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Idioma
            SectionHeader(panel, "Section_Idioma", "settings_language_header");
            var languageDropdown = CreateDropdown(Row(panel, "Row_Idioma", "settings_language"), "Dropdown_Idioma");

            // Audio
            SectionHeader(panel, "Section_Audio", "settings_audio_header");
            var (master, masterVal) = SliderRow(panel, "Master", "settings_master");
            var (music, musicVal) = SliderRow(panel, "Musica", "settings_music");
            var (sfx, sfxVal) = SliderRow(panel, "SFX_Voz", "settings_sfx");

            // Pantalla
            SectionHeader(panel, "Section_Pantalla", "settings_video_header");
            var fullscreen = CreateToggle(Row(panel, "Row_PantallaCompleta", "settings_fullscreen"));
            var resolution = CreateDropdown(Row(panel, "Row_Resolucion", "settings_resolution"), "Dropdown_Resolucion");

            // --- Volver (abajo-izquierda, como el menú de Inicio) + pista de teclado ---
            var back = BackButton(root);
            var hint = LocalizedText(root, "settings_hint", 20, TextDim, FontStyles.Normal, 2f, TextAlignmentOptions.BottomRight);
            Anchor(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-80f, 60f), new Vector2(500f, 30f));
            hint.rectTransform.pivot = new Vector2(1f, 0f);

            // --- Controlador ---
            var controller = canvasGo.AddComponent<ConfiguracionesController>();
            var so = new SerializedObject(controller);
            so.FindProperty("languageDropdown").objectReferenceValue = languageDropdown;
            so.FindProperty("masterSlider").objectReferenceValue = master;
            so.FindProperty("musicSlider").objectReferenceValue = music;
            so.FindProperty("sfxSlider").objectReferenceValue = sfx;
            so.FindProperty("masterValue").objectReferenceValue = masterVal;
            so.FindProperty("musicValue").objectReferenceValue = musicVal;
            so.FindProperty("sfxValue").objectReferenceValue = sfxVal;
            so.FindProperty("fullscreenToggle").objectReferenceValue = fullscreen;
            so.FindProperty("resolutionDropdown").objectReferenceValue = resolution;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);

            var buildScenes = EditorBuildSettings.scenes.ToList();
            if (!buildScenes.Any(s => s.path == ScenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }
        }

        // ============================================================== //
        // Bloques de UI                                                  //
        // ============================================================== //

        private static void SectionHeader(RectTransform panel, string name, string key)
        {
            var section = NewUI(name, panel, typeof(LayoutElement));
            section.GetComponent<LayoutElement>().preferredHeight = 58f;
            var label = LocalizedText(section, key, 30, AccentGreen, FontStyles.Bold, 6f, TextAlignmentOptions.BottomLeft);
            Anchor(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.rectTransform.offsetMin = new Vector2(0f, 8f);
            var rule = NewUI("Rule", section, typeof(Image));
            rule.GetComponent<Image>().color = new Color(AccentGreen.r, AccentGreen.g, AccentGreen.b, 0.35f);
            Anchor(rule, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f));
            rule.pivot = new Vector2(0.5f, 0f);
        }

        /// <summary>Fila: etiqueta localizada a la izquierda (ancho fijo) + contenedor del control a la derecha.</summary>
        private static RectTransform Row(RectTransform panel, string name, string labelKey)
        {
            var row = NewUI(name, panel, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.GetComponent<LayoutElement>().preferredHeight = 52f;
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 24f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var label = LocalizedText(row, labelKey, 26, TextNormal, FontStyles.Normal, 1f, TextAlignmentOptions.MidlineLeft);
            label.gameObject.AddComponent<LayoutElement>().preferredWidth = 320f;

            var slot = NewUI("Control", row, typeof(LayoutElement));
            slot.GetComponent<LayoutElement>().flexibleWidth = 1f;
            return slot;
        }

        private static (Slider, TMP_Text) SliderRow(RectTransform panel, string id, string labelKey)
        {
            RectTransform slot = Row(panel, "Row_" + id, labelKey);

            var sliderGo = DefaultControls.CreateSlider(UiResources());
            sliderGo.name = "Slider_" + id;
            var srt = (RectTransform)sliderGo.transform;
            srt.SetParent(slot, false);
            Anchor(srt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-55f, 0f), new Vector2(-110f, 24f));

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            sliderGo.transform.Find("Background").GetComponent<Image>().color = ControlBg;
            sliderGo.transform.Find("Fill Area/Fill").GetComponent<Image>().color = AccentGreen;
            var handle = sliderGo.transform.Find("Handle Slide Area/Handle").GetComponent<Image>();
            handle.color = TitleWhite;
            var colors = slider.colors;
            colors.highlightedColor = colors.selectedColor = TextHoverGreen;
            slider.colors = colors;

            var value = PlainText(slot, "Value", "100%", 24, AccentGreen, TextAlignmentOptions.MidlineRight);
            Anchor(value.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(90f, 0f));
            value.rectTransform.pivot = new Vector2(1f, 0.5f);

            return (slider, value);
        }

        private static Toggle CreateToggle(RectTransform slot)
        {
            var go = NewUI("Toggle_PantallaCompleta", slot, typeof(Image), typeof(Toggle));
            Anchor(go, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(40f, 40f));
            var bg = go.GetComponent<Image>();
            bg.color = ControlBg;

            var check = NewUI("Checkmark", go, typeof(Image));
            Anchor(check, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            check.offsetMin = new Vector2(8f, 8f);
            check.offsetMax = new Vector2(-8f, -8f);
            check.GetComponent<Image>().color = AccentGreen;

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = check.GetComponent<Image>();
            var colors = toggle.colors;
            colors.highlightedColor = colors.selectedColor = TextHoverGreen;
            toggle.colors = colors;
            return toggle;
        }

        private static TMP_Dropdown CreateDropdown(RectTransform slot, string name)
        {
            var go = TMP_DefaultControls.CreateDropdown(TmpResources());
            go.name = name;
            var rt = (RectTransform)go.transform;
            rt.SetParent(slot, false);
            Anchor(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var dropdown = go.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions(); // las opciones las rellena ConfiguracionesController en runtime

            // Tipografía y colores del proyecto en todos los textos (caption + plantilla de ítem).
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
            {
                if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
                t.fontSize = 24;
                t.color = TextNormal;
            }

            go.GetComponent<Image>().color = ControlBg;
            go.transform.Find("Arrow").GetComponent<Image>().color = AccentGreen;
            var template = (RectTransform)go.transform.Find("Template");
            template.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.10f, 0.98f);
            template.sizeDelta = new Vector2(0f, 260f);
            var item = (RectTransform)template.Find("Viewport/Content/Item");
            item.sizeDelta = new Vector2(item.sizeDelta.x, 40f);
            ((RectTransform)template.Find("Viewport/Content")).sizeDelta = new Vector2(0f, 44f);
            item.Find("Item Background").GetComponent<Image>().color = ControlBg;
            item.Find("Item Checkmark").GetComponent<Image>().color = AccentGreen;
            var itemToggle = item.GetComponent<Toggle>();
            var ic = itemToggle.colors;
            ic.highlightedColor = ic.selectedColor = HoverGreen;
            itemToggle.colors = ic;

            var colors = dropdown.colors;
            colors.highlightedColor = colors.selectedColor = TextHoverGreen;
            dropdown.colors = colors;
            return dropdown;
        }

        private static Button BackButton(Transform root)
        {
            var go = NewUI("Btn_Volver", root, typeof(Image), typeof(Button));
            Anchor(go, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(80f, 60f), new Vector2(320f, 56f));
            go.pivot = new Vector2(0f, 0f);
            go.GetComponent<Image>().color = ControlBg;

            var label = LocalizedText(go, "settings_back", 20, TextNormal, FontStyles.Bold, 0f, TextAlignmentOptions.MidlineLeft);
            Anchor(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.rectTransform.offsetMin = new Vector2(24f, 0f);
            label.rectTransform.offsetMax = new Vector2(-15f, 0f);

            // Mismo hover que los botones del menú de Inicio.
            var fx = new SerializedObject(go.gameObject.AddComponent<MenuButtonEffects>());
            fx.FindProperty("normalColor").colorValue = ControlBg;
            fx.FindProperty("hoverColor").colorValue = new Color(0.60f, 0.25f, 0.08f, 1f);
            fx.FindProperty("normalTextColor").colorValue = TextNormal;
            fx.FindProperty("hoverTextColor").colorValue = AccentRust;
            fx.ApplyModifiedPropertiesWithoutUndo();

            return go.GetComponent<Button>();
        }

        // ============================================================== //
        // Utilidades                                                     //
        // ============================================================== //

        /// <summary>Texto TMP cuyo contenido lo inyecta LocalizeStringEvent desde la tabla Settings.</summary>
        private static TMP_Text LocalizedText(Transform parent, string key, float size, Color color, FontStyles style,
            float spacing, TextAlignmentOptions align)
        {
            var tmp = PlainText(parent, key, string.Empty, size, color, align);
            tmp.fontStyle = style;
            tmp.characterSpacing = spacing;

            var localize = tmp.gameObject.AddComponent<LocalizeStringEvent>();
            localize.StringReference.SetReference(TableName, key);
            var setText = (UnityAction<string>)Delegate.CreateDelegate(
                typeof(UnityAction<string>), tmp, typeof(TMP_Text).GetProperty(nameof(TMP_Text.text)).GetSetMethod());
            UnityEventTools.AddPersistentListener(localize.OnUpdateString, setText);
            return tmp;
        }

        private static TMP_Text PlainText(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align)
        {
            var go = NewUI(name, parent);
            var tmp = go.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) { tmp.font = font; tmp.fontSharedMaterial = font.material; }
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static RectTransform NewUI(string name, Transform parent, params Type[] components)
        {
            var go = new GameObject(name, new[] { typeof(RectTransform) }.Concat(components).ToArray());
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform Stretch(RectTransform rt)
        {
            Anchor(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return rt;
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        private static DefaultControls.Resources UiResources() => new DefaultControls.Resources
        {
            standard = Builtin("UI/Skin/UISprite.psd"),
            background = Builtin("UI/Skin/Background.psd"),
            knob = Builtin("UI/Skin/Knob.psd"),
            checkmark = Builtin("UI/Skin/Checkmark.psd"),
            dropdown = Builtin("UI/Skin/DropdownArrow.psd"),
            mask = Builtin("UI/Skin/UIMask.psd"),
        };

        private static TMP_DefaultControls.Resources TmpResources() => new TMP_DefaultControls.Resources
        {
            standard = Builtin("UI/Skin/UISprite.psd"),
            background = Builtin("UI/Skin/Background.psd"),
            inputField = Builtin("UI/Skin/InputFieldBackground.psd"),
            knob = Builtin("UI/Skin/Knob.psd"),
            checkmark = Builtin("UI/Skin/Checkmark.psd"),
            dropdown = Builtin("UI/Skin/DropdownArrow.psd"),
            mask = Builtin("UI/Skin/UIMask.psd"),
        };
    }
}
