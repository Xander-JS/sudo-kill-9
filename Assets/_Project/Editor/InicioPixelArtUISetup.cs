using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using System.IO;
using SudoKill9.UI.Inicio;

namespace SudoKill9.Editor
{
    /// <summary>
    /// Modifica ÚNICAMENTE los elementos visuales de la interfaz de usuario (UI Canvas) en la escena Inicio
    /// para aplicar el diseño táctico en Pixel Art:
    /// 1. Tipografía Pixel Art (TMP_FontAsset en modo RASTER + FilterMode.Point sin difuminados).
    /// 2. Paleta de colores y estados (Normal: gris oscuro / verde militar sólido; Hover/Selected: cian brillante o naranja óxido).
    /// 3. Estilo de botones y paneles (Source Image con sprites pixel art, Image Type = Sliced y Borders 9-slice configurados).
    /// 4. CERO alteraciones a las funciones OnClick() ni a los scripts de lógica del menú ni a objetos 3D.
    /// </summary>
    public static class InicioPixelArtUISetup
    {
        private const string InicioScenePath = "Assets/_Project/Scenes/Inicio.unity";
        private const string SourceTtfPath = "Assets/_Project/Fonts/PixelTacticalMono-Bold.ttf";
        private const string FallbackTtfPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        private const string PixelFontAssetDir = "Assets/_Project/Fonts";
        private const string PixelFontAssetPath = "Assets/_Project/Fonts/PixelTactical_Point.asset";

        private const string ButtonSpritePath = "Assets/_Project/Sprites/UI/Inicio/PixelArt_Tactical_Button.png";
        private const string PanelSpritePath = "Assets/_Project/Sprites/UI/Inicio/PixelArt_Tactical_Panel.png";

        // Paleta de Colores Táctica Pixel Art
        // Estado Normal: Sólidos y oscuros (verde militar oscuro / gris carbón táctico)
        private static readonly Color NormalPrimaryBg = new Color(0.14f, 0.22f, 0.18f, 1.0f);   // Verde militar sólido oscuro
        private static readonly Color NormalSecondaryBg = new Color(0.15f, 0.17f, 0.18f, 1.0f); // Gris carbón táctico sólido
        private static readonly Color NormalDangerBg = new Color(0.22f, 0.14f, 0.13f, 1.0f);    // Gris/óxido oscuro para Salir

        // Estado Hover / Selected: Resaltado en Cian Brillante o Naranja Óxido
        private static readonly Color HoverCyanBright = new Color(0.08f, 0.88f, 0.96f, 1.0f);   // Cian brillante táctico
        private static readonly Color HoverRustOrange = new Color(0.92f, 0.42f, 0.12f, 1.0f);   // Naranja óxido táctico
        private static readonly Color PressedDarkColor = new Color(0.06f, 0.45f, 0.50f, 1.0f);
        private static readonly Color PressedRustColor = new Color(0.60f, 0.25f, 0.08f, 1.0f);

        // Colores de Texto
        private static readonly Color TextNormalColor = new Color(0.86f, 0.90f, 0.88f, 1.0f);
        private static readonly Color TextHoverCyan = new Color(0.25f, 1.00f, 1.00f, 1.0f);
        private static readonly Color TextHoverRust = new Color(1.00f, 0.62f, 0.28f, 1.0f);

        [MenuItem("Tools/The Last Root/Apply Pixel Art UI Style (Inicio Canvas Only)")]
        public static void ApplyPixelArtUIMenu()
        {
            ApplyPixelArtUI(interactive: true);
        }

        [InitializeOnLoadMethod]
        private static void AutoApplyPixelArtUIOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.path != InicioScenePath) return;

                TMP_FontAsset existingPixelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixelFontAssetPath);
                GameObject docBox = GameObject.Find("Terms_Document_Box");
                bool needsOverlapFix = docBox != null && docBox.GetComponent<RectMask2D>() == null;

                if (existingPixelFont == null || needsOverlapFix)
                {
                    ApplyPixelArtUI(interactive: false);
                }
            };
        }

        public static void ApplyPixelArtUI(bool interactive)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != InicioScenePath)
            {
                if (interactive)
                {
                    EditorUtility.DisplayDialog(
                        "Escena Incorrecta",
                        "Por seguridad, esta herramienta solo modifica el Canvas de la escena 'Inicio.unity'.",
                        "Entendido");
                }
                return;
            }

            AssetDatabase.Refresh();

            // 1. Configurar Sprites Pixel Art con FilterMode.Point y Borders 9-Slice en el TextureImporter
            Sprite buttonSprite = Configure9SlicePixelSprite(ButtonSpritePath, new Vector4(16f, 12f, 12f, 12f));
            Sprite panelSprite = Configure9SlicePixelSprite(PanelSpritePath, new Vector4(16f, 16f, 16f, 16f));

            // 2. Crear o actualizar la fuente Pixel Art con renderizado RASTER y FilterMode.Point (sin difuminados)
            TMP_FontAsset pixelFont = CreateOrUpdatePixelArtFontAsset();

            // 3. Aplicar los cambios ÚNICAMENTE a los elementos visuales dentro de los Canvas de Inicio
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int updatedTexts = 0;
            int updatedButtons = 0;
            int updatedPanels = 0;

            foreach (Canvas canvas in canvases)
            {
                // Activar pixelPerfect en el Canvas para máxima nitidez de bordes pixel art
                Undo.RecordObject(canvas, "Pixel Perfect Canvas");
                canvas.pixelPerfect = true;

                // 3.0 Ajustar márgenes de la Card de Términos y Servicios para que los botones nunca tapen el texto de abajo
                FixTermsOfServiceCardOverlap(canvas.transform);

                // 3.1 Actualizar TODOS los textos (TextMeshProUGUI) a la fuente Pixel Art con Point filter
                TextMeshProUGUI[] texts = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (TextMeshProUGUI tmp in texts)
                {
                    Undo.RecordObject(tmp, "Apply Pixel Art Font");
                    if (pixelFont != null)
                    {
                        tmp.font = pixelFont;
                        if (pixelFont.material != null)
                        {
                            tmp.fontSharedMaterial = pixelFont.material;
                        }
                    }

                    // Ajustar colores de encabezados para encajar con la paleta militar/óxido/cian
                    string objName = tmp.gameObject.name.ToLowerInvariant();
                    if (objName.Contains("title") && !objName.Contains("sub"))
                    {
                        tmp.color = new Color(0.92f, 0.95f, 0.93f, 1.0f);
                    }
                    else if (objName.Contains("sub"))
                    {
                        tmp.color = HoverCyanBright;
                    }

                    EditorUtility.SetDirty(tmp);
                    updatedTexts++;
                }

                // 3.2 Actualizar TODOS los paneles/modales (Images que no son botones)
                Image[] images = canvas.GetComponentsInChildren<Image>(true);
                foreach (Image img in images)
                {
                    if (img.GetComponent<Button>() != null) continue; // Los botones se procesan abajo

                    string iname = img.gameObject.name.ToLowerInvariant();
                    if (iname.Contains("panel") || iname.Contains("card_container") || iname.Contains("document_box"))
                    {
                        Undo.RecordObject(img, "Apply Pixel Art Panel Sprite");
                        if (panelSprite != null)
                        {
                            img.sprite = panelSprite;
                            img.type = Image.Type.Sliced;
                            img.pixelsPerUnitMultiplier = 1f;
                        }
                        img.color = new Color(0.10f, 0.13f, 0.12f, 0.98f);
                        EditorUtility.SetDirty(img);
                        updatedPanels++;
                    }
                    else if (iname.Contains("accent_line") || iname.Contains("header_accent"))
                    {
                        Undo.RecordObject(img, "Apply Pixel Art Accent Line");
                        img.color = HoverRustOrange;
                        EditorUtility.SetDirty(img);
                    }
                }

                // 3.3 Actualizar TODOS los Botones (Image Sliced + ColorBlock + MenuButtonEffects sin tocar OnClick)
                Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
                foreach (Button btn in buttons)
                {
                    ApplyPixelArtButtonVisuals(btn, buttonSprite);
                    updatedButtons++;
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[InicioPixelArtUISetup] ¡UI Pixel Art aplicada! ({updatedTexts} textos en Point filter, {updatedButtons} botones 9-slice, {updatedPanels} paneles). Funciones OnClick() intactas.");
        }

        // ================================================================================= //
        // 1. CONFIGURACIÓN DEL SPRITE 9-SLICE Y FILTER MODE = POINT (NO FILTER)             //
        // ================================================================================= //

        private static Sprite Configure9SlicePixelSprite(string assetPath, Vector4 borderLBRT)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[InicioPixelArtUISetup] No se encontró el sprite en {assetPath}");
                return null;
            }

            bool needsReimport = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                needsReimport = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                needsReimport = true;
            }

            // REQUISITO: Filter Mode = Point (no filter) y sin compresión borrosa para pixel art puro
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                needsReimport = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                needsReimport = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                needsReimport = true;
            }

            // Configurar los Borders (L, B, R, T) del 9-Slice para que las esquinas pixeladas no se estiren
            if (importer.spriteBorder != borderLBRT)
            {
                importer.spriteBorder = borderLBRT;
                needsReimport = true;
            }

            // También asegurar mediante TextureImporterSettings
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteBorder != borderLBRT || settings.filterMode != FilterMode.Point)
            {
                settings.spriteBorder = borderLBRT;
                settings.filterMode = FilterMode.Point;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                needsReimport = true;
            }

            if (needsReimport)
            {
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        // ================================================================================= //
        // 2. CREACIÓN / CONFIGURACIÓN DE LA FUENTE PIXEL ART CON POINT (NO FILTER)          //
        // ================================================================================= //

        private static TMP_FontAsset CreateOrUpdatePixelArtFontAsset()
        {
            if (!AssetDatabase.IsValidFolder(PixelFontAssetDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Fonts");
            }

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceTtfPath);
            if (sourceFont == null)
            {
                sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FallbackTtfPath);
            }

            if (sourceFont == null)
            {
                Debug.LogWarning("[InicioPixelArtUISetup] No se encontró archivo TTF base para generar la fuente Pixel Art.");
                return null;
            }

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixelFontAssetPath);
            if (fontAsset == null)
            {
                // Usar GlyphRenderMode.RASTER (Bitmap sin suavizado) a tamaño pequeño (16pt) para crear bloques de píxel reales
                // y forzar FilterMode.Point en la textura del Atlas
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFont,
                    16,
                    2,
                    GlyphRenderMode.RASTER,
                    512,
                    512,
                    AtlasPopulationMode.Dynamic);

                if (fontAsset != null)
                {
                    AssetDatabase.CreateAsset(fontAsset, PixelFontAssetPath);

                    if (fontAsset.atlasTexture != null)
                    {
                        fontAsset.atlasTexture.name = "PixelTactical_Point_Atlas";
                        fontAsset.atlasTexture.filterMode = FilterMode.Point;
                        AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
                    }

                    if (fontAsset.material != null)
                    {
                        fontAsset.material.name = "PixelTactical_Point_Material";
                        Shader bitmapShader = Shader.Find("TextMeshPro/Bitmap") ?? Shader.Find("TextMeshPro/Mobile/Bitmap");
                        if (bitmapShader != null)
                        {
                            fontAsset.material.shader = bitmapShader;
                        }
                        if (fontAsset.atlasTexture != null)
                        {
                            fontAsset.material.mainTexture = fontAsset.atlasTexture;
                        }
                        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                    }
                }
            }

            // Asegurar SIEMPRE que todas las texturas de atlas de la fuente estén en modo 'Point (no filter)'
            if (fontAsset != null)
            {
                if (fontAsset.atlasTexture != null)
                {
                    fontAsset.atlasTexture.filterMode = FilterMode.Point;
                    EditorUtility.SetDirty(fontAsset.atlasTexture);
                }

                if (fontAsset.atlasTextures != null)
                {
                    foreach (Texture2D tex in fontAsset.atlasTextures)
                    {
                        if (tex != null)
                        {
                            tex.filterMode = FilterMode.Point;
                            EditorUtility.SetDirty(tex);
                        }
                    }
                }

                if (fontAsset.material != null && fontAsset.material.mainTexture != null)
                {
                    fontAsset.material.mainTexture.filterMode = FilterMode.Point;
                    EditorUtility.SetDirty(fontAsset.material);
                }

                // Pre-poblar los caracteres españoles y símbolos tácticos del menú para que queden guardados en el atlas Point
                string preloadChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789ÁÉÍÓÚÑáéíóúñ[]><:/-._,;!¡?¿()\"'• ";
                fontAsset.TryAddCharacters(preloadChars, out _);

                if (fontAsset.atlasTexture != null)
                {
                    fontAsset.atlasTexture.filterMode = FilterMode.Point;
                }

                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
            }

            return fontAsset;
        }

        // ================================================================================= //
        // 3. CONFIGURACIÓN VISUAL DE BOTONES (SLICED + COLORBLOCK + HOVER CIAN / ÓXIDO)     //
        // ================================================================================= //

        private static void ApplyPixelArtButtonVisuals(Button btn, Sprite buttonSprite)
        {
            if (btn == null) return;

            string btnName = btn.gameObject.name.ToLowerInvariant();

            // Determinar el esquema de color según el rol del botón:
            // - Iniciar Misión / Aceptar: Estado normal Verde Militar oscuro, Hover en Cian Brillante
            // - Salir / Rechazar / Cerrar / Configuración / Créditos: Alternar entre Naranja Óxido y Cian Brillante
            bool isPrimaryMission = btnName.Contains("iniciar") || btnName.Contains("accept");
            bool isRustAccent = btnName.Contains("salir") || btnName.Contains("decline") || btnName.Contains("close") || btnName.Contains("config") || btnName.Contains("credit");

            Color normalBg = isPrimaryMission
                ? NormalPrimaryBg
                : (btnName.Contains("salir") || btnName.Contains("decline") ? NormalDangerBg : NormalSecondaryBg);

            Color hoverBg = isRustAccent ? HoverRustOrange : HoverCyanBright;
            Color pressedBg = isRustAccent ? PressedRustColor : PressedDarkColor;
            Color hoverTxt = isRustAccent ? TextHoverRust : TextHoverCyan;

            // 1. Configurar Image con Sprite Pixel Art en modo Sliced (sin tocar lógica)
            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                Undo.RecordObject(img, "Apply Pixel Art Button Sprite");
                if (buttonSprite != null)
                {
                    img.sprite = buttonSprite;
                    img.type = Image.Type.Sliced;
                    img.pixelsPerUnitMultiplier = 1f;
                }
                img.color = normalBg;
                EditorUtility.SetDirty(img);
            }

            // 2. Configurar ColorBlock del Button (Normal oscuro sólido, Hover/Selected en Cian Brillante o Naranja Óxido)
            // NOTA: No tocamos btn.onClick en absoluto.
            Undo.RecordObject(btn, "Apply Pixel Art Button ColorBlock");
            btn.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white; // Multiplica por img.color (o por sí mismo si MenuButtonEffects no está)
            cb.highlightedColor = hoverBg;
            cb.selectedColor = hoverBg;
            cb.pressedColor = pressedBg;
            cb.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            cb.colorMultiplier = 1.0f;
            cb.fadeDuration = 0.06f; // Transición rápida y seca estilo pixel art táctico
            btn.colors = cb;
            EditorUtility.SetDirty(btn);

            // 3. Si el botón tiene MenuButtonEffects, sincronizar sus colores serializados (Normal y Hover)
            // para que al pasar el ratón en Play Mode use exactamente la nueva paleta (gris/verde militar -> cian brillante / naranja óxido)
            MenuButtonEffects effects = btn.GetComponent<MenuButtonEffects>();
            if (effects != null)
            {
                SerializedObject soEffects = new SerializedObject(effects);
                SerializedProperty normColProp = soEffects.FindProperty("normalColor");
                SerializedProperty hovColProp = soEffects.FindProperty("hoverColor");
                SerializedProperty normTxtProp = soEffects.FindProperty("normalTextColor");
                SerializedProperty hovTxtProp = soEffects.FindProperty("hoverTextColor");

                if (normColProp != null) normColProp.colorValue = normalBg;
                if (hovColProp != null) hovColProp.colorValue = hoverBg;
                if (normTxtProp != null) normTxtProp.colorValue = TextNormalColor;
                if (hovTxtProp != null) hovTxtProp.colorValue = hoverTxt;

                soEffects.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(effects);
            }

            // 4. Ajustar color inicial del texto hijo del botón
            TMP_Text btnText = btn.GetComponentInChildren<TMP_Text>(true);
            if (btnText != null)
            {
                Undo.RecordObject(btnText, "Apply Pixel Art Button Text Color");
                btnText.color = TextNormalColor;
                EditorUtility.SetDirty(btnText);
            }
        }

        /// <summary>
        /// Corrige el tamaño y márgenes de la tarjeta de Términos y Servicios existente en escena
        /// para que el texto del documento nunca se desborde sobre el aviso de estado ni los botones inferiores.
        /// </summary>
        private static void FixTermsOfServiceCardOverlap(Transform canvasTransform)
        {
            Transform overlay = canvasTransform.Find("TermsOfService_FullScreen_Overlay");
            if (overlay == null) return;

            Transform cardBorder = overlay.Find("Terms_Card_Border");
            if (cardBorder != null)
            {
                RectTransform borderRt = cardBorder.GetComponent<RectTransform>();
                if (borderRt != null)
                {
                    Undo.RecordObject(borderRt, "Resize Terms Card Border");
                    borderRt.sizeDelta = new Vector2(960f, 680f);
                }

                Transform cardContainer = cardBorder.Find("Terms_Card_Container");
                if (cardContainer != null)
                {
                    Transform docBox = cardContainer.Find("Terms_Document_Box");
                    if (docBox != null)
                    {
                        RectTransform docBoxRt = docBox.GetComponent<RectTransform>();
                        if (docBoxRt != null)
                        {
                            Undo.RecordObject(docBoxRt, "Adjust Terms Document Box Margins");
                            docBoxRt.anchorMin = Vector2.zero;
                            docBoxRt.anchorMax = Vector2.one;
                            docBoxRt.pivot = new Vector2(0.5f, 0.5f);
                            docBoxRt.offsetMin = new Vector2(28f, 136f); // Deja 136px libres abajo para estado + botones
                            docBoxRt.offsetMax = new Vector2(-28f, -80f); // Deja 80px libres arriba para el encabezado
                        }

                        if (docBox.GetComponent<RectMask2D>() == null)
                        {
                            docBox.gameObject.AddComponent<RectMask2D>();
                        }

                        Transform bodyTextTr = docBox.Find("Terms_Body_Text");
                        if (bodyTextTr != null)
                        {
                            RectTransform bodyRt = bodyTextTr.GetComponent<RectTransform>();
                            if (bodyRt != null)
                            {
                                Undo.RecordObject(bodyRt, "Adjust Terms Body Text Padding");
                                bodyRt.anchorMin = Vector2.zero;
                                bodyRt.anchorMax = Vector2.one;
                                bodyRt.offsetMin = new Vector2(22f, 16f);
                                bodyRt.offsetMax = new Vector2(-22f, -16f);
                            }

                            TMP_Text bodyTmp = bodyTextTr.GetComponent<TMP_Text>();
                            if (bodyTmp != null)
                            {
                                Undo.RecordObject(bodyTmp, "Adjust Terms Body Text Font Size");
                                bodyTmp.fontSize = 13.5f;
                                bodyTmp.enableAutoSizing = true;
                                bodyTmp.fontSizeMin = 11f;
                                bodyTmp.fontSizeMax = 14f;
                                bodyTmp.lineSpacing = 3f;
                                bodyTmp.overflowMode = TextOverflowModes.Truncate;
                                EditorUtility.SetDirty(bodyTmp);
                            }
                        }
                    }

                    Transform statusTr = cardContainer.Find("Status_Notice_Text");
                    if (statusTr != null)
                    {
                        RectTransform statusRt = statusTr.GetComponent<RectTransform>();
                        if (statusRt != null)
                        {
                            Undo.RecordObject(statusRt, "Adjust Status Notice Position");
                            statusRt.anchorMin = new Vector2(0f, 0f);
                            statusRt.anchorMax = new Vector2(1f, 0f);
                            statusRt.pivot = new Vector2(0.5f, 0f);
                            statusRt.anchoredPosition = new Vector2(0f, 92f);
                            statusRt.sizeDelta = new Vector2(-60f, 26f);
                        }
                        TMP_Text statusTmp = statusTr.GetComponent<TMP_Text>();
                        if (statusTmp != null)
                        {
                            Undo.RecordObject(statusTmp, "Adjust Status Notice Font Size");
                            statusTmp.fontSize = 13f;
                            EditorUtility.SetDirty(statusTmp);
                        }
                    }

                    Transform acceptTr = cardContainer.Find("Btn_Accept_Terms");
                    if (acceptTr != null)
                    {
                        RectTransform acceptRt = acceptTr.GetComponent<RectTransform>();
                        if (acceptRt != null)
                        {
                            Undo.RecordObject(acceptRt, "Adjust Accept Button Position");
                            acceptRt.anchoredPosition = new Vector2(165f, 44f);
                            acceptRt.sizeDelta = new Vector2(320f, 50f);
                        }
                    }

                    Transform declineTr = cardContainer.Find("Btn_Decline_Terms");
                    if (declineTr != null)
                    {
                        RectTransform declineRt = declineTr.GetComponent<RectTransform>();
                        if (declineRt != null)
                        {
                            Undo.RecordObject(declineRt, "Adjust Decline Button Position");
                            declineRt.anchoredPosition = new Vector2(-175f, 44f);
                            declineRt.sizeDelta = new Vector2(280f, 50f);
                        }
                    }
                }
            }
        }
    }
}
