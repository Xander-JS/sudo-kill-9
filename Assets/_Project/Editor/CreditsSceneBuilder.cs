using System;
using System.Linq;
using SudoKill9.UI.Credits;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Genera (o regenera) todo lo necesario para la escena de Créditos:
    /// 1. Locales es/en + LocalizationSettings.
    /// 2. String Table Collection "Credits" con cada clave en español e inglés.
    /// 3. Escena Credits.unity con cada línea TMP enlazada por LocalizeStringEvent, y alta en Build Settings.
    /// Es idempotente: re-ejecutarlo actualiza los textos y reconstruye la escena.
    /// </summary>
    public static class CreditsSceneBuilder
    {
        private const string LocalizationDir = "Assets/_Project/Localization";
        private const string LocalesDir = LocalizationDir + "/Locales";
        private const string TablesDir = LocalizationDir + "/Tables";
        private const string TableName = "Credits";
        private const string ScenePath = "Assets/_Project/Scenes/GamePlay/Credits.unity";
        private const string FontPath = "Assets/_Project/Fonts/PixelTactical_Point.asset";

        // Paleta de la escena Inicio (InicioSceneBuilder / InicioPixelArtUISetup).
        private static readonly Color TitleWhite = new Color(0.95f, 0.98f, 1f);
        private static readonly Color TextNormal = new Color(0.86f, 0.90f, 0.88f);
        private static readonly Color TextDim = new Color(0.86f, 0.90f, 0.88f, 0.55f);
        // Verde primario: mismo tono (~150°) que el verde militar de Inicio (0.14, 0.22, 0.18), aclarado para líneas/acentos.
        private static readonly Color AccentGreen = new Color(0.56f, 0.88f, 0.72f);
        private static readonly Color AccentRust = new Color(1.00f, 0.62f, 0.28f);

        private enum Style { Title, Header, Name, Role, Note, Label, Value, Body, End, Prompt, Separator, GapSmall, GapLarge }

        // Orden visual de la secuencia. Las entradas sin clave son decoración (separadores/espacios), no texto.
        private static readonly (string key, string es, string en, Style style)[] Lines =
        {
            ("credits_title", "SUDO-KILL-9", "SUDO-KILL-9", Style.Title),
            (null, null, null, Style.Separator),
            (null, null, null, Style.GapLarge),

            ("credits_developed_by", "DESARROLLADO POR", "DEVELOPED BY", Style.Header),
            (null, null, null, Style.GapSmall),
            ("credits_dev_marlon", "Marlon Alexander Imbajoa", "Marlon Alexander Imbajoa", Style.Name),
            ("credits_dev_kevin", "Kevin Stiven Buesaquillo", "Kevin Stiven Buesaquillo", Style.Name),
            ("credits_dev_juan", "Juan Felipe Zambrano", "Juan Felipe Zambrano", Style.Name),
            (null, null, null, Style.GapLarge),

            (null, null, null, Style.Separator),
            ("credits_team_header", "EQUIPO DE DESARROLLO INTEGRAL", "COMPREHENSIVE DEVELOPMENT TEAM", Style.Header),
            (null, null, null, Style.Separator),
            (null, null, null, Style.GapSmall),
            ("credits_role_direction", "Dirección del Proyecto y Diseño de Juego", "Project Direction & Game Design", Style.Role),
            ("credits_role_programming", "Programación y Lógica de Juego", "Programming & Game Logic", Style.Role),
            ("credits_role_modeling", "Modelado 3D y Arte Conceptual", "3D Modeling & Concept Art", Style.Role),
            ("credits_role_narrative", "Diseño Narrativo y Diálogos", "Narrative Design & Dialogues", Style.Role),
            ("credits_role_sound", "Diseño de Sonido y Animación", "Sound Design & Animation", Style.Role),
            (null, null, null, Style.GapSmall),
            ("credits_team_note",
                "(Todos los integrantes participaron en\ncada una de las fases de producción)",
                "(All team members participated across\neach of the production phases)", Style.Note),
            (null, null, null, Style.GapLarge),

            (null, null, null, Style.Separator),
            ("credits_tools_header", "TECNOLOGÍAS Y HERRAMIENTAS", "TECHNOLOGIES & TOOLS", Style.Header),
            (null, null, null, Style.Separator),
            (null, null, null, Style.GapSmall),
            ("credits_tool_engine_label", "Motor de Juego", "Game Engine", Style.Label),
            ("credits_tool_engine", "Unity", "Unity", Style.Value),
            (null, null, null, Style.GapSmall),
            ("credits_tool_audio_label", "Diseño y Audio", "Design and Audio", Style.Label),
            ("credits_tool_audio", "FMOD", "FMOD", Style.Value),
            (null, null, null, Style.GapSmall),
            ("credits_tool_audio_editing_label", "Edición y Masterización de Audio", "Audio Editing and Mastering", Style.Label),
            ("credits_tool_audio_editing", "Audacity", "Audacity", Style.Value),
            (null, null, null, Style.GapSmall),
            ("credits_tool_mesh_label", "Generación de Mallas 3D", "3D Mesh Generation", Style.Label),
            ("credits_tool_mesh", "Tripo 3D", "Tripo 3D", Style.Value),
            (null, null, null, Style.GapSmall),
            ("credits_tool_optimization_label", "Optimización y Ajustes Técnicos", "Optimization and Technical Adjustments", Style.Label),
            ("credits_tool_optimization", "Blender", "Blender", Style.Value),
            (null, null, null, Style.GapSmall),
            ("credits_tool_rigging_label", "Rigging y Animaciones", "Rigging and Animations", Style.Label),
            ("credits_tool_rigging", "Adobe Mixamo", "Adobe Mixamo", Style.Value),
            (null, null, null, Style.GapLarge),

            (null, null, null, Style.Separator),
            ("credits_sound_header", "MÚSICA Y EFECTOS DE SONIDO", "MUSIC & SOUND EFFECTS", Style.Header),
            (null, null, null, Style.Separator),
            (null, null, null, Style.GapSmall),
            ("credits_sound_pixabay_label", "Música y efectos de sonido libres de regalías", "Royalty-free music and sound effects", Style.Label),
            ("credits_sound_pixabay", "Pixabay (pixabay.com)", "Pixabay (pixabay.com)", Style.Value),
            ("credits_sound_pixabay_note",
                "Gracias a los autores y artistas de Pixabay\npor compartir su trabajo bajo la Licencia de Contenido de Pixabay",
                "Thanks to the Pixabay authors and artists\nfor sharing their work under the Pixabay Content License", Style.Note),
            (null, null, null, Style.GapLarge),
            (null, null, null, Style.Separator),
            ("credits_freesound_header", "EFECTOS DE SONIDO DE FREESOUND", "FREESOUND SOUND EFFECTS", Style.Header),
            (null, null, null, Style.Separator),
            (null, null, null, Style.GapSmall),
            ("credits_freesound_label", "Sonidos de la comunidad", "Community sounds", Style.Label),
            ("credits_freesound", "Freesound (freesound.org)", "Freesound (freesound.org)", Style.Value),
            (null, null, null, Style.GapSmall),
            // Generado desde los nombres de archivo de Audio/ (formato usuario__nombre-del-sonido).
            ("credits_freesound_aemyn_journey_to_the_interweb", "\"Journey To The Interweb\" por aemyn", "\"Journey To The Interweb\" by aemyn", Style.Role),
            ("credits_freesound_alienxxx_fx_26_laser", "\"Fx 26 Laser\" por alienxxx", "\"Fx 26 Laser\" by alienxxx", Style.Role),
            ("credits_freesound_alphatrooper18_laser_shoot2", "\"Laser Shoot2\" por alphatrooper18", "\"Laser Shoot2\" by alphatrooper18", Style.Role),
            ("credits_freesound_daleonfire_laser2", "\"Laser2\" por daleonfire", "\"Laser2\" by daleonfire", Style.Role),
            ("credits_freesound_felixblume_impact_on_a_window", "\"Impact On A Window\" por felixblume", "\"Impact On A Window\" by felixblume", Style.Role),
            ("credits_freesound_iainmccurdy_buzzing_faulty_and_sparking_fluorescent_light", "\"Buzzing Faulty And Sparking Fluorescent Light\" por iainmccurdy", "\"Buzzing Faulty And Sparking Fluorescent Light\" by iainmccurdy", Style.Role),
            ("credits_freesound_malucien_agua", "\"Agua\" por malucien", "\"Agua\" by malucien", Style.Role),
            ("credits_freesound_mirmaximus_sfx_outage_hit", "\"Sfx Outage Hit\" por mirmaximus", "\"Sfx Outage Hit\" by mirmaximus", Style.Role),
            ("credits_freesound_nakhas_data_transfer", "\"Data Transfer\" por nakhas", "\"Data Transfer\" by nakhas", Style.Role),
            ("credits_freesound_sergenious_robo", "\"Robo\" por sergenious", "\"Robo\" by sergenious", Style.Role),
            ("credits_freesound_signaturesoundsorg_game_clicks_from", "\"Game Clicks From\" por signaturesoundsorg", "\"Game Clicks From\" by signaturesoundsorg", Style.Role),
            ("credits_freesound_tboneaudio_data_bolts", "\"Data Bolts\" por tboneaudio", "\"Data Bolts\" by tboneaudio", Style.Role),
            ("credits_freesound_timkahn_glitch_sd_03", "\"Glitch Sd 03\" por timkahn", "\"Glitch Sd 03\" by timkahn", Style.Role),
            ("credits_freesound_xcreenplay_cyber_play", "\"Cyber Play\" por xcreenplay", "\"Cyber Play\" by xcreenplay", Style.Role),
            (null, null, null, Style.GapSmall),
            ("credits_freesound_note",
                "Sonidos de los usuarios de Freesound,\nusados bajo sus respectivas licencias Creative Commons",
                "Sounds by Freesound users,\nused under their respective Creative Commons licenses", Style.Note),
            (null, null, null, Style.GapLarge),

            (null, null, null, Style.Separator),
            ("credits_assets_header", "ANIMACIONES Y EFECTOS VISUALES EXTERNOS", "EXTERNAL ANIMATIONS & VISUAL EFFECTS", Style.Header),
            (null, null, null, Style.Separator),
            (null, null, null, Style.GapSmall),
            ("credits_assets_store_label", "Animaciones y efectos visuales (VFX)", "Animations and visual effects (VFX)", Style.Label),
            ("credits_assets_store", "Unity Asset Store", "Unity Asset Store", Style.Value),
            ("credits_assets_store_note",
                "Hovl Studio · JMO Assets (Cartoon FX)\nUsados bajo la Licencia Estándar de la Unity Asset Store",
                "Hovl Studio · JMO Assets (Cartoon FX)\nUsed under the Unity Asset Store Standard License", Style.Note),
            (null, null, null, Style.GapLarge),

            (null, null, null, Style.Separator),
            ("credits_thanks_header", "AGRADECIMIENTOS", "ACKNOWLEDGMENTS", Style.Header),
            (null, null, null, Style.Separator),
            (null, null, null, Style.GapSmall),
            ("credits_thanks_body",
                "A la comunidad Open Source\ny a las tecnologías libres y de código abierto\nque inspiraron el universo de este juego.",
                "To the Open Source community\nand free and open-source technologies\nthat inspired the universe of this game.", Style.Body),
            (null, null, null, Style.GapLarge),

            (null, null, null, Style.Separator),
            ("credits_end_thanks", "¡GRACIAS POR JUGAR!", "THANK YOU FOR PLAYING!", Style.End),
            (null, null, null, Style.Separator),
            (null, null, null, Style.GapLarge),

            // Debe ser la última: CreditsController detiene el scroll cuando esta línea llega al centro.
            ("credits_exit_prompt", "Presiona [ESPACIO] o [ESC] para salir", "Press [SPACE] or [ESC] to exit", Style.Prompt),
        };

        [MenuItem("Tools/The Last Root/Build Credits Scene (Localized)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Locale es = EnsureLocalization();
            FillStringTable();
            BuildScene();

            Debug.Log($"[CreditsSceneBuilder] Escena '{ScenePath}' y tabla '{TableName}' (es/en) generadas. Locale de proyecto: {es.Identifier.Code}.");
        }

        // ============================================================== //
        // Localización                                                   //
        // ============================================================== //

        internal static Locale EnsureLocalization()
        {
            EnsureFolder(LocalizationDir);
            EnsureFolder(LocalesDir);
            EnsureFolder(TablesDir);

            if (LocalizationEditorSettings.ActiveLocalizationSettings == null)
            {
                var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, LocalizationDir + "/LocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }

            Locale es = EnsureLocale("es", "Spanish (es)");
            EnsureLocale("en", "English (en)");

            // Idioma del sistema primero (si es es/en); en cualquier otro caso, español.
            LocalizationSettings.ProjectLocale = es;
            foreach (var selector in LocalizationEditorSettings.ActiveLocalizationSettings.GetStartupLocaleSelectors())
                if (selector is SpecificLocaleSelector specified)
                    specified.LocaleId = es.Identifier;
            EditorUtility.SetDirty(LocalizationEditorSettings.ActiveLocalizationSettings);

            return es;
        }

        private static Locale EnsureLocale(string code, string assetName)
        {
            Locale locale = LocalizationEditorSettings.GetLocales().FirstOrDefault(l => l.Identifier.Code == code);
            if (locale != null) return locale;

            locale = Locale.CreateLocale(code);
            AssetDatabase.CreateAsset(locale, $"{LocalesDir}/{assetName}.asset");
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        private static void FillStringTable()
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(TableName)
                ?? LocalizationEditorSettings.CreateStringTableCollection(TableName, TablesDir);

            foreach (string code in new[] { "es", "en" })
            {
                var id = new LocaleIdentifier(code);
                var table = (StringTable)collection.GetTable(id) ?? (StringTable)collection.AddNewTable(id);

                foreach (var line in Lines.Where(l => l.key != null))
                    table.AddEntry(line.key, code == "es" ? line.es : line.en);

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
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            // Cámara: fondo negro sólido.
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            // Canvas de créditos.
            GameObject canvasGo = CreateCanvas("Credits_Canvas", 0);
            CreateFullscreenImage(canvasGo.transform, "Background", Color.black);

            var contentGo = new GameObject("Credits_Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(canvasGo.transform, false);
            var content = (RectTransform)contentGo.transform;
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(1100f, 0f);
            content.anchoredPosition = new Vector2(0f, -540f); // fuera de pantalla por abajo (se recalcula en runtime)

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 14f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text lastLine = null;
            int i = 0;
            foreach (var line in Lines)
            {
                i++;
                switch (line.style)
                {
                    case Style.Separator: CreateSeparator(content, i); break;
                    case Style.GapSmall: CreateGap(content, i, 28f); break;
                    case Style.GapLarge: CreateGap(content, i, 110f); break;
                    default: lastLine = CreateLocalizedLine(content, line.key, line.style, font); break;
                }
            }

            // Director + fundido: canvas propio encima de todo; persiste entre escenas durante la transición.
            GameObject director = CreateCanvas("Credits_Director", 100);
            var group = director.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
            CreateFullscreenImage(director.transform, "Fade", Color.black);

            var controller = director.AddComponent<CreditsController>();
            var so = new SerializedObject(controller);
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("lastLine").objectReferenceValue = lastLine;
            so.FindProperty("fader").objectReferenceValue = group;
            // ~2× más líneas que la versión original (atribuciones Pixabay/Freesound/Asset Store): misma velocidad de lectura.
            so.FindProperty("scrollDuration").floatValue = 30f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);

            var buildScenes = EditorBuildSettings.scenes.ToList();
            if (!buildScenes.Any(s => s.path == ScenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }
        }

        private static TMP_Text CreateLocalizedLine(Transform parent, string key, Style style, TMP_FontAsset font)
        {
            var go = new GameObject(key, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) { tmp.font = font; tmp.fontSharedMaterial = font.material; }
            tmp.text = string.Empty; // el texto lo inyecta exclusivamente LocalizeStringEvent
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            ApplyStyle(tmp, style);

            var localize = go.AddComponent<LocalizeStringEvent>();
            localize.StringReference.SetReference(TableName, key);
            var setText = (UnityAction<string>)Delegate.CreateDelegate(
                typeof(UnityAction<string>), tmp, typeof(TMP_Text).GetProperty(nameof(TMP_Text.text)).GetSetMethod());
            UnityEventTools.AddPersistentListener(localize.OnUpdateString, setText);

            return tmp;
        }

        private static void ApplyStyle(TMP_Text tmp, Style style)
        {
            switch (style)
            {
                case Style.Title: Set(tmp, 96, TitleWhite, FontStyles.Bold, 8f); break;
                case Style.Header: Set(tmp, 34, AccentGreen, FontStyles.Bold, 6f); break;
                case Style.Name: Set(tmp, 40, TitleWhite, FontStyles.Normal, 1f); break;
                case Style.Role: Set(tmp, 30, TextNormal, FontStyles.Normal, 0f); break;
                case Style.Note: Set(tmp, 24, TextDim, FontStyles.Italic, 0f); break;
                case Style.Label: Set(tmp, 24, TextDim, FontStyles.UpperCase, 3f); break;
                case Style.Value: Set(tmp, 40, TitleWhite, FontStyles.Bold, 1f); break;
                case Style.Body: Set(tmp, 30, TextNormal, FontStyles.Normal, 0f); tmp.lineSpacing = 12f; break;
                case Style.End: Set(tmp, 64, AccentGreen, FontStyles.Bold, 6f); break;
                case Style.Prompt: Set(tmp, 26, AccentRust, FontStyles.Normal, 2f); break;
            }
        }

        private static void Set(TMP_Text tmp, float size, Color color, FontStyles fontStyle, float spacing)
        {
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = fontStyle;
            tmp.characterSpacing = spacing;
        }

        private static void CreateSeparator(Transform parent, int index)
        {
            var go = new GameObject($"Separator_{index}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(AccentGreen.r, AccentGreen.g, AccentGreen.b, 0.45f);
            img.raycastTarget = false;
            go.GetComponent<LayoutElement>().preferredHeight = 2f;
        }

        private static void CreateGap(Transform parent, int index, float height)
        {
            var go = new GameObject($"Gap_{index}", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
        }

        private static GameObject CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f; // la altura manda: el scroll se mide en alto de pantalla
            return go;
        }

        private static void CreateFullscreenImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
