using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
using SudoKill9.UI.Inicio;

namespace SudoKill9.Editor
{
    /// <summary>
    /// Configura exclusivamente la escena de Inicio con:
    /// 1. Atmósfera estilo Call of Duty: Warzone equilibrada (bien iluminada y perfectamente visible,
    ///    con contraste táctico, desaturación militar, matiz frío/verdoso en sombras y viñeta).
    /// 2. Pared de Fondo (Cubo detrás del personaje): escala X e Y ampliada para cubrir toda la cámara y material de búnker con Smoothness = 0.
    /// 3. Card de Términos y Servicios (EULA / Protocolo Root) a pantalla completa que el jugador acepta para ver el menú de Inicio.
    /// 4. CERO modificaciones a la posición/rotación/animaciones/scripts del personaje ni a otras escenas.
    /// </summary>
    public static class InicioWarzoneAtmosphereSetup
    {
        private const string InicioScenePath = "Assets/_Project/Scenes/Inicio.unity";
        private const string TextureDir = "Assets/_Project/Models/Environment/Inicio_modelos/Texture";
        private const string AlbedoPath = TextureDir + "/Warzone_BunkerWall_Albedo.png";
        private const string NormalPath = TextureDir + "/Warzone_BunkerWall_Normal.png";
        private const string BunkerWallMatPath = "Assets/_Project/Materials/Mat_Warzone_BunkerWall.mat";
        private const string BunkerFloorMatPath = "Assets/_Project/Materials/Mat_Warzone_BunkerFloor.mat";
        private const string VolumeProfileDir = "Assets/_Project/Settings";
        private const string VolumeProfilePath = VolumeProfileDir + "/InicioWarzoneVolumeProfile.asset";
        private const string FontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        [MenuItem("Tools/The Last Root/Apply Warzone Atmosphere & Terms Card (Inicio Only)")]
        public static void ApplyWarzoneAtmosphereMenu()
        {
            ApplyWarzoneAtmosphere(interactive: true);
        }

        [InitializeOnLoadMethod]
        private static void AutoApplyOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.path != InicioScenePath) return;

                // Si aún no está creada la Card de Términos y Servicios o hay que actualizar la iluminación demasiado oscura:
                GameObject tosCard = GameObject.Find("TermsOfService_FullScreen_Overlay");
                if (tosCard == null)
                {
                    ApplyWarzoneAtmosphere(interactive: false);
                }
            };
        }

        public static void ApplyWarzoneAtmosphere(bool interactive)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != InicioScenePath)
            {
                if (interactive)
                {
                    EditorUtility.DisplayDialog(
                        "Escena Incorrecta",
                        "Por seguridad, esta herramienta solo modifica la escena 'Inicio.unity'. Ábrela primero antes de continuar.",
                        "Entendido");
                }
                return;
            }

            Debug.Log("[WarzoneAtmosphere] Ajustando iluminación visible Warzone, Pared de Búnker y Card de Términos y Servicios...");

            // 1. Crear/Configurar Materiales de Búnker Viejo (Smoothness = 0, bien legibles)
            Material bunkerWallMat = CreateOrUpdateBunkerWallMaterial();
            Material bunkerFloorMat = CreateOrUpdateBunkerFloorMaterial();

            // 2. Localizar o asegurar el Cubo de Pared de Fondo y ajustar Scale (X, Y) y Material
            SetupBackgroundBunkerWall(bunkerWallMat, bunkerFloorMat);

            // 3. Ajustar Iluminación Direccional y de Relleno para que Tux y los props se vean perfectamente claros pero dramáticos
            SetupVisibleWarzoneLighting();

            // 4. Configurar Global Volume equilibrado (sin aplastar negros, manteniendo estética Warzone)
            VolumeProfile profile = CreateOrUpdateWarzoneVolumeProfile();
            SetupGlobalVolumeAndCameraPostProcessing(profile);

            // 5. Crear la Card de Términos y Servicios a pantalla completa en el Canvas de Inicio
            SetupTermsOfServiceCard();

            // 6. Marcar la escena como modificada y guardar
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log("[WarzoneAtmosphere] ¡Iluminación corregida y Card de Términos y Servicios añadida con éxito en Inicio.unity!");
        }

        // ================================================================================= //
        // 1. MATERIAL DE PARED DE BÚNKER VIEJA / DAÑADA (SMOOTHNESS = 0)                    //
        // ================================================================================= //

        private static Material CreateOrUpdateBunkerWallMaterial()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }

            AssetDatabase.Refresh();

            TextureImporter normalImporter = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.wrapMode = TextureWrapMode.Repeat;
                normalImporter.SaveAndReimport();
            }

            TextureImporter albedoImporter = AssetImporter.GetAtPath(AlbedoPath) as TextureImporter;
            if (albedoImporter != null)
            {
                albedoImporter.wrapMode = TextureWrapMode.Repeat;
                albedoImporter.SaveAndReimport();
            }

            Texture2D albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(BunkerWallMatPath);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, BunkerWallMatPath);
            }
            else
            {
                mat.shader = urpLit;
            }

            // Tono concreto militar desgastado bien visible (no negro)
            Color bunkerTint = new Color(0.78f, 0.82f, 0.80f, 1.0f);
            mat.SetColor("_BaseColor", bunkerTint);
            mat.color = bunkerTint;

            if (albedoTex != null)
            {
                mat.SetTexture("_BaseMap", albedoTex);
                mat.mainTexture = albedoTex;
            }

            if (normalTex != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 1.25f);
            }

            // REQUISITO ESTRICTO: Smoothness = 0 para eliminar cualquier reflejo plástico
            mat.SetFloat("_Smoothness", 0.0f);
            mat.SetFloat("_Glossiness", 0.0f);
            mat.SetFloat("_Metallic", 0.0f);
            mat.SetFloat("_SpecularHighlights", 0.0f);
            mat.SetFloat("_EnvironmentReflections", 0.0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");

            Vector2 tiling = new Vector2(5.0f, 2.5f);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetTextureScale("_BumpMap", tiling);
            mat.mainTextureScale = tiling;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateBunkerFloorMaterial()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>(BunkerFloorMatPath);
            if (floorMat == null)
            {
                floorMat = new Material(urpLit);
                AssetDatabase.CreateAsset(floorMat, BunkerFloorMatPath);
            }

            Texture2D albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);

            Color floorColor = new Color(0.55f, 0.58f, 0.57f, 1.0f);
            floorMat.SetColor("_BaseColor", floorColor);
            floorMat.color = floorColor;

            if (albedoTex != null) floorMat.SetTexture("_BaseMap", albedoTex);
            if (normalTex != null)
            {
                floorMat.EnableKeyword("_NORMALMAP");
                floorMat.SetTexture("_BumpMap", normalTex);
                floorMat.SetFloat("_BumpScale", 0.8f);
            }

            floorMat.SetFloat("_Smoothness", 0.05f);
            floorMat.SetFloat("_Metallic", 0.0f);
            Vector2 floorTiling = new Vector2(8.0f, 8.0f);
            floorMat.SetTextureScale("_BaseMap", floorTiling);
            floorMat.SetTextureScale("_BumpMap", floorTiling);

            EditorUtility.SetDirty(floorMat);
            return floorMat;
        }

        // ================================================================================= //
        // 2. CUBO DE PARED DE FONDO (ESCALA X e Y + MATERIAL BÚNKER)                        //
        // ================================================================================= //

        private static void SetupBackgroundBunkerWall(Material bunkerWallMat, Material bunkerFloorMat)
        {
            GameObject linuxObj = GameObject.Find("linux") ?? GameObject.Find("Player_LinuxSkeleton");
            Camera cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();

            GameObject backgroundCube = FindBackgroundWallCube(linuxObj, cam);

            if (backgroundCube == null)
            {
                backgroundCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                backgroundCube.name = "Cube_Bunker_Wall";
                backgroundCube.transform.position = new Vector3(-0.5f, 2.2f, -1.6f);
                backgroundCube.transform.rotation = Quaternion.identity;
                backgroundCube.transform.localScale = new Vector3(24.0f, 12.0f, 0.35f);
            }
            else
            {
                Undo.RecordObject(backgroundCube.transform, "Scale Background Wall Cube");
                Vector3 currentScale = backgroundCube.transform.localScale;
                float targetScaleX = Mathf.Max(Mathf.Abs(currentScale.x), 24.0f);
                float targetScaleY = Mathf.Max(Mathf.Abs(currentScale.y), 12.0f);
                float targetScaleZ = Mathf.Abs(currentScale.z) > 0.01f ? currentScale.z : 0.35f;

                backgroundCube.transform.localScale = new Vector3(targetScaleX, targetScaleY, targetScaleZ);
            }

            MeshRenderer wallRenderer = backgroundCube.GetComponent<MeshRenderer>();
            if (wallRenderer != null)
            {
                Undo.RecordObject(wallRenderer, "Assign Warzone Bunker Wall Material");
                wallRenderer.sharedMaterial = bunkerWallMat;
            }

            MeshRenderer[] allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MeshRenderer mr in allRenderers)
            {
                string objName = mr.gameObject.name.ToLowerInvariant();
                if (objName == "plane" || objName.Contains("floor") || objName.Contains("suelo") || objName.Contains("piso"))
                {
                    Undo.RecordObject(mr, "Assign Bunker Floor Material");
                    mr.sharedMaterial = bunkerFloorMat;
                }
            }
        }

        private static GameObject FindBackgroundWallCube(GameObject linuxObj, Camera cam)
        {
            MeshFilter[] meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject bestCandidate = null;
            float bestScore = float.MinValue;

            float camZ = cam != null ? cam.transform.position.z : 4.428f;
            float tuxZ = linuxObj != null ? linuxObj.transform.position.z : 1.917f;

            foreach (MeshFilter mf in meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                GameObject go = mf.gameObject;

                if (go.GetComponent<EnemyPlaceholder>() != null) continue;
                if (go.transform.parent != null && go.transform.parent.name.Contains("Enemy_Horde")) continue;
                if (linuxObj != null && go.transform.IsChildOf(linuxObj.transform)) continue;

                string meshName = mf.sharedMesh.name.ToLowerInvariant();
                string objName = go.name.ToLowerInvariant();

                bool isCubeMesh = meshName.Contains("cube") || objName.Contains("cube") || objName.Contains("cubo") || objName.Contains("pared") || objName.Contains("wall") || objName.Contains("muro");
                if (!isCubeMesh) continue;

                float z = go.transform.position.z;
                if (z < camZ)
                {
                    float score = 0f;
                    if (objName.StartsWith("cube") || objName.Contains("pared") || objName.Contains("wall") || objName.Contains("muro"))
                        score += 100f;
                    if (z < tuxZ)
                        score += 50f;
                    score += Mathf.Abs(go.transform.localScale.x * go.transform.localScale.y);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCandidate = go;
                    }
                }
            }

            return bestCandidate;
        }

        // ================================================================================= //
        // 3. ILUMINACIÓN VISIBLE PERO TÁCTICA Y DRAMÁTICA (CORRECCIÓN DE EXCESO DE OSCURIDAD)//
        // ================================================================================= //

        private static void SetupVisibleWarzoneLighting()
        {
            // IMPORTANTE: En esta escena la cámara está en Z = +4.43 mirando hacia -Z (Euler Y ≈ 181°).
            // Para que una Directional Light ilumine DE FRENTE al personaje, la mesa, la pizarra y la pared
            // (es decir, que los rayos viajen desde +Z hacia -Z), su rotación Euler Y debe estar alrededor de 155° - 165°,
            // o bien usamos una rotación frontal clara + luces de búnker cenitales.

            // Ambiente Trilight equilibrado: sombras visibles con detalle, nada negro empastado
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.42f, 0.40f, 1.0f);     // Luz ambiental superior fría/verdosa clara
            RenderSettings.ambientEquatorColor = new Color(0.26f, 0.30f, 0.29f, 1.0f); // Luz ambiental media legible
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.19f, 0.18f, 1.0f);  // Rebote inferior suave
            RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.reflectionIntensity = 0.45f;

            // Desactivar niebla densa que oscurecía la pared del fondo
            RenderSettings.fog = false;

            Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Light mainDirectional = null;
            Light secondaryDirectional = null;

            foreach (Light l in allLights)
            {
                Undo.RecordObject(l, "Adjust Warzone Light");
                Undo.RecordObject(l.transform, "Adjust Warzone Light Transform");

                string lname = l.gameObject.name.ToLowerInvariant();

                if (l.type == LightType.Directional)
                {
                    if (lname.Contains("rim") || lname.Contains("cyan") || lname.Contains("back"))
                    {
                        secondaryDirectional = l;
                    }
                    else if (mainDirectional == null)
                    {
                        mainDirectional = l;
                    }
                }
            }

            if (mainDirectional == null)
            {
                GameObject dirLightObj = new GameObject("Directional Light");
                mainDirectional = dirLightObj.AddComponent<Light>();
                mainDirectional.type = LightType.Directional;
            }

            // En la escena original:
            // Key_Light_Front tenía Euler(30, 25, 0) y Rim_Light_Cyan_Back tenía Euler(20, 195, 0).
            // Como la cámara mira hacia Euler Y=181 (hacia -Z), la luz que apunta en la MISMA dirección que la cámara
            // (iluminando la cara frontal de Tux, la pizarra, el casillero y el muro) es la que tiene Euler Y ≈ 165° - 195°!
            // Antes Key_Light_Front (Y=25) apuntaba desde atrás del muro y el muro la bloqueaba con sombras activadas.
            mainDirectional.color = new Color(0.92f, 0.95f, 0.93f, 1.0f); // Blanco táctico ligeramente frío
            mainDirectional.intensity = 1.15f;                            // Bien iluminado y dramático
            mainDirectional.shadows = LightShadows.Soft;
            mainDirectional.shadowStrength = 0.65f;                       // Sombras suaves que dejan ver todo el detalle
            mainDirectional.transform.rotation = Quaternion.Euler(28.0f, 165.0f, 0.0f); // Ilumina desde el frente/cámara hacia Tux y el muro

            if (secondaryDirectional != null)
            {
                // Luz de relleno lateral suave desde el otro ángulo (sin sombras para no bloquearse con el muro)
                secondaryDirectional.color = new Color(0.65f, 0.78f, 0.75f, 1.0f); // Tono frío/verdoso suave de relleno
                secondaryDirectional.intensity = 0.55f;
                secondaryDirectional.shadows = LightShadows.None;
                secondaryDirectional.transform.rotation = Quaternion.Euler(22.0f, 205.0f, 0.0f);
            }

            // Añadir una luz puntual táctica cálida sobre la mesa/casillero para dar profundidad tipo hangar de Warzone
            GameObject bunkerLampObj = GameObject.Find("Warzone_Bunker_Overhead_Light");
            if (bunkerLampObj == null)
            {
                bunkerLampObj = new GameObject("Warzone_Bunker_Overhead_Light");
                GameObject lightingRoot = GameObject.Find("Menu_Lighting");
                if (lightingRoot != null) bunkerLampObj.transform.SetParent(lightingRoot.transform);
            }
            Light bunkerLamp = bunkerLampObj.GetComponent<Light>();
            if (bunkerLamp == null) bunkerLamp = bunkerLampObj.AddComponent<Light>();
            bunkerLamp.type = LightType.Point;
            bunkerLamp.color = new Color(0.95f, 0.88f, 0.74f, 1.0f); // Luz tungsteno suave de búnker
            bunkerLamp.intensity = 1.8f;
            bunkerLamp.range = 12.0f;
            bunkerLamp.shadows = LightShadows.None;
            bunkerLampObj.transform.position = new Vector3(-0.8f, 1.8f, 2.2f);

            RenderSettings.sun = mainDirectional;
        }

        // ================================================================================= //
        // 4. POST-PROCESSING EQUILIBRADO (ESTILO WARZONE VISIBLE Y NÍTIDO)                  //
        // ================================================================================= //

        private static VolumeProfile CreateOrUpdateWarzoneVolumeProfile()
        {
            if (!AssetDatabase.IsValidFolder(VolumeProfileDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Settings");
            }

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                if (profile.components[i] != null)
                {
                    Object.DestroyImmediate(profile.components[i], true);
                }
            }
            profile.components.Clear();

            // 1. Tonemapping Neutral (Mantiene la visibilidad de los tonos medios mucho mejor que ACES sin oscurecer en exceso)
            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.Neutral);

            // 2. Color Adjustments (Exposición positiva clara +0.35, contraste +18, desaturación táctica -20)
            ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.active = true;
            colorAdjustments.postExposure.Override(0.35f); // Levanta la claridad general para que todo se vea nítido
            colorAdjustments.contrast.Override(18.0f);     // Contraste marcado pero sin aplastar las sombras
            colorAdjustments.saturation.Override(-20.0f);  // Desaturado estilo militar Warzone
            colorAdjustments.colorFilter.Override(new Color(0.94f, 0.98f, 0.96f, 1.0f));

            // 3. Shadows, Midtones, Highlights (Tono frío/verdoso sutil en las sombras sin oscurecerlas)
            ShadowsMidtonesHighlights smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.active = true;
            // Cuarto valor (W) en +0.05 para levantar ligeramente el detalle en las sombras
            smh.shadows.Override(new Vector4(0.88f, 0.98f, 0.94f, 0.05f));
            smh.midtones.Override(new Vector4(0.96f, 1.00f, 0.98f, 0.03f));
            smh.highlights.Override(new Vector4(0.98f, 0.99f, 0.97f, 0.0f));

            // 4. Vignette pronunciado en los bordes pero dejando el centro 100% despejado
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.active = true;
            vignette.color.Override(new Color(0.02f, 0.04f, 0.03f, 1.0f));
            vignette.center.Override(new Vector2(0.5f, 0.5f));
            vignette.intensity.Override(0.40f);
            vignette.smoothness.Override(0.38f);
            vignette.rounded.Override(false);

            // 5. Bloom ligero
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.20f);
            bloom.scatter.Override(0.5f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static void SetupGlobalVolumeAndCameraPostProcessing(VolumeProfile profile)
        {
            Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Volume globalVolume = null;

            foreach (Volume v in volumes)
            {
                if (v.isGlobal)
                {
                    globalVolume = v;
                    break;
                }
            }

            if (globalVolume == null)
            {
                GameObject volObj = new GameObject("Warzone_Global_Volume");
                globalVolume = volObj.AddComponent<Volume>();
            }

            Undo.RecordObject(globalVolume, "Configure Warzone Global Volume");
            globalVolume.isGlobal = true;
            globalVolume.priority = 10f;
            globalVolume.weight = 1.0f;
            globalVolume.sharedProfile = profile;

            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Camera cam in cameras)
            {
                Undo.RecordObject(cam, "Configure Camera Background");
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.10f, 0.13f, 0.12f, 1.0f);
                cam.allowHDR = true;

                UniversalAdditionalCameraData urpCamData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (urpCamData == null)
                {
                    urpCamData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }

                Undo.RecordObject(urpCamData, "Enable Camera Post Processing");
                urpCamData.renderPostProcessing = true;
                urpCamData.renderShadows = true;
                urpCamData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                urpCamData.antialiasingQuality = AntialiasingQuality.High;
            }
        }

        // ================================================================================= //
        // 5. CARD DE TÉRMINOS Y SERVICIOS A PANTALLA COMPLETA (ESTILO WARZONE / TERMINAL)   //
        // ================================================================================= //

        private static void SetupTermsOfServiceCard()
        {
            Canvas menuCanvas = null;
            GameObject canvasObj = GameObject.Find("Menu_Canvas");
            if (canvasObj != null)
            {
                menuCanvas = canvasObj.GetComponent<Canvas>();
            }
            if (menuCanvas == null)
            {
                menuCanvas = Object.FindFirstObjectByType<Canvas>();
            }
            if (menuCanvas == null)
            {
                Debug.LogWarning("[WarzoneAtmosphere] No se encontró Menu_Canvas para añadir la Card de Términos y Servicios.");
                return;
            }

            // Si ya existía una versión previa del overlay, reemplazarla limpiamente
            Transform existingOverlay = menuCanvas.transform.Find("TermsOfService_FullScreen_Overlay");
            if (existingOverlay != null)
            {
                Object.DestroyImmediate(existingOverlay.gameObject);
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            // 1. Contenedor de Pantalla Completa (Tapa absolutamente toda la pantalla al iniciar)
            GameObject overlayObj = new GameObject("TermsOfService_FullScreen_Overlay");
            overlayObj.transform.SetParent(menuCanvas.transform, false);
            overlayObj.transform.SetAsLastSibling(); // Encima de todos los botones y textos del menú

            RectTransform overlayRt = overlayObj.AddComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;

            // Fondo oscuro opaco táctico que cubre el 100% de la pantalla
            Image overlayBg = overlayObj.AddComponent<Image>();
            overlayBg.color = new Color(0.03f, 0.05f, 0.05f, 0.97f);
            overlayBg.raycastTarget = true;

            CanvasGroup cg = overlayObj.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            TermsOfServiceCard tosController = overlayObj.AddComponent<TermsOfServiceCard>();

            // 2. Card Central Táctica (Marco estilo Call of Duty: Warzone / Acuerdo de Operador)
            GameObject cardBorderObj = new GameObject("Terms_Card_Border");
            cardBorderObj.transform.SetParent(overlayObj.transform, false);
            RectTransform borderRt = cardBorderObj.AddComponent<RectTransform>();
            borderRt.anchorMin = new Vector2(0.5f, 0.5f);
            borderRt.anchorMax = new Vector2(0.5f, 0.5f);
            borderRt.pivot = new Vector2(0.5f, 0.5f);
            borderRt.anchoredPosition = Vector2.zero;
            borderRt.sizeDelta = new Vector2(960f, 680f);

            Image borderImg = cardBorderObj.AddComponent<Image>();
            borderImg.color = new Color(0.28f, 0.38f, 0.34f, 0.95f); // Borde verde/gris militar táctico

            // Interior de la Card
            GameObject cardObj = new GameObject("Terms_Card_Container");
            cardObj.transform.SetParent(cardBorderObj.transform, false);
            RectTransform cardRt = cardObj.AddComponent<RectTransform>();
            cardRt.anchorMin = Vector2.zero;
            cardRt.anchorMax = Vector2.one;
            cardRt.offsetMin = new Vector2(2f, 2f);
            cardRt.offsetMax = new Vector2(-2f, -2f);

            Image cardBg = cardObj.AddComponent<Image>();
            cardBg.color = new Color(0.07f, 0.09f, 0.09f, 0.99f);

            // Barra superior de acento táctico (estilo Warzone)
            GameObject topBarObj = new GameObject("Top_Tactical_Header_Bar");
            topBarObj.transform.SetParent(cardObj.transform, false);
            RectTransform topBarRt = topBarObj.AddComponent<RectTransform>();
            topBarRt.anchorMin = new Vector2(0f, 1f);
            topBarRt.anchorMax = new Vector2(1f, 1f);
            topBarRt.pivot = new Vector2(0.5f, 1f);
            topBarRt.anchoredPosition = Vector2.zero;
            topBarRt.sizeDelta = new Vector2(0f, 64f);

            Image topBarImg = topBarObj.AddComponent<Image>();
            topBarImg.color = new Color(0.11f, 0.15f, 0.14f, 1f);

            // Línea de acento ámbar/verde militar bajo el encabezado
            GameObject headerLineObj = new GameObject("Header_Accent_Line");
            headerLineObj.transform.SetParent(topBarObj.transform, false);
            RectTransform headerLineRt = headerLineObj.AddComponent<RectTransform>();
            headerLineRt.anchorMin = new Vector2(0f, 0f);
            headerLineRt.anchorMax = new Vector2(1f, 0f);
            headerLineRt.pivot = new Vector2(0.5f, 0f);
            headerLineRt.anchoredPosition = Vector2.zero;
            headerLineRt.sizeDelta = new Vector2(0f, 3f);
            Image headerLineImg = headerLineObj.AddComponent<Image>();
            headerLineImg.color = new Color(0.76f, 0.65f, 0.32f, 1f); // Dorado/ámbar táctico Warzone

            // Título del Acuerdo
            GameObject titleObj = new GameObject("Title_Text");
            titleObj.transform.SetParent(topBarObj.transform, false);
            TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
            if (font != null) titleText.font = font;
            titleText.text = "ACUERDO DE LICENCIA Y TÉRMINOS DE SERVICIO // PROTOCOLO ROOT";
            titleText.fontSize = 19f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            titleText.color = new Color(0.92f, 0.95f, 0.93f, 1f);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = new Vector2(28f, 0f);
            titleRt.offsetMax = new Vector2(-28f, 0f);

            // Caja de texto del documento de Términos y Condiciones (Con máscara RectMask2D y amplio margen inferior de 136px para botones)
            GameObject bodyBoxObj = new GameObject("Terms_Document_Box");
            bodyBoxObj.transform.SetParent(cardObj.transform, false);
            RectTransform bodyBoxRt = bodyBoxObj.AddComponent<RectTransform>();
            bodyBoxRt.anchorMin = Vector2.zero;
            bodyBoxRt.anchorMax = Vector2.one;
            bodyBoxRt.offsetMin = new Vector2(28f, 136f); // 136px libres abajo para el texto de estado y los botones
            bodyBoxRt.offsetMax = new Vector2(-28f, -80f); // 80px libres arriba para el header

            Image bodyBoxBg = bodyBoxObj.AddComponent<Image>();
            bodyBoxBg.color = new Color(0.04f, 0.06f, 0.06f, 1f);
            bodyBoxObj.AddComponent<RectMask2D>(); // Evita que cualquier texto pueda salirse de la caja sobre los botones

            GameObject bodyTextObj = new GameObject("Terms_Body_Text");
            bodyTextObj.transform.SetParent(bodyBoxObj.transform, false);
            TMP_Text bodyText = bodyTextObj.AddComponent<TextMeshProUGUI>();
            if (font != null) bodyText.font = font;
            bodyText.fontSize = 13.5f;
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = 11f;
            bodyText.fontSizeMax = 14f;
            bodyText.lineSpacing = 3f;
            bodyText.overflowMode = TextOverflowModes.Truncate;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.color = new Color(0.78f, 0.83f, 0.80f, 1f);
            bodyText.text =
                "<b>AVISO DE SEGURIDAD DEL SISTEMA — SUDO KILL -9: THE LAST ROOT</b>\n" +
                "Lee detenidamente los términos de servicio y condiciones antes de desplegar al operador en el kernel:\n\n" +
                "<b>1. ACEPTACIÓN DEL PROTOCOLO DE OPERACIONES</b>\n" +
                "Al presionar <b>[ ACEPTAR Y CONTINUAR ]</b>, el usuario asume el control directo del agente táctico <b>Tux (Linux Skeleton)</b> con privilegios elevados (`root`) para neutralizar procesos hostiles, spyware y el monolito del sistema.\n\n" +
                "<b>2. USO DE PRIVILEGIOS `SUDO KILL -9`</b>\n" +
                "Toda ejecución de señales `SIGKILL (-9)` en la simulación es definitiva. El operador se compromete a defender la integridad de la arquitectura libre frente a oleadas de procesos corruptos.\n\n" +
                "<b>3. TELEMETRÍA LOCAL Y CONFIGURACIÓN</b>\n" +
                "Esta aplicación almacena únicamente preferencias locales de audio y controles en tu dispositivo. Ningún dato personal es transmitido a servidores externos.\n\n" +
                "<b>4. ADVERTENCIA FOTOSENSIBLE Y DE RENDIMIENTO</b>\n" +
                "Incluye efectos de iluminación táctica y combate intenso. Al continuar, confirmas haber leído y aceptado los Términos de Servicio.";

            RectTransform bodyTextRt = bodyTextObj.GetComponent<RectTransform>();
            bodyTextRt.anchorMin = Vector2.zero;
            bodyTextRt.anchorMax = Vector2.one;
            bodyTextRt.offsetMin = new Vector2(22f, 16f);
            bodyTextRt.offsetMax = new Vector2(-22f, -16f);

            // Línea de estado de confirmación (Separada claramente entre la caja de texto y los botones)
            GameObject statusObj = new GameObject("Status_Notice_Text");
            statusObj.transform.SetParent(cardObj.transform, false);
            TMP_Text statusText = statusObj.AddComponent<TextMeshProUGUI>();
            if (font != null) statusText.font = font;
            statusText.text = "> ESTADO: ESPERANDO AUTORIZACIÓN DEL OPERADOR PARA ACCEDER AL MENÚ PRINCIPAL...";
            statusText.fontSize = 13f;
            statusText.fontStyle = FontStyles.Italic;
            statusText.alignment = TextAlignmentOptions.MidlineLeft;
            statusText.color = new Color(0.76f, 0.65f, 0.32f, 0.95f);

            RectTransform statusRt = statusObj.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0f, 0f);
            statusRt.anchorMax = new Vector2(1f, 0f);
            statusRt.pivot = new Vector2(0.5f, 0f);
            statusRt.anchoredPosition = new Vector2(0f, 92f);
            statusRt.sizeDelta = new Vector2(-60f, 26f);

            // Botón ACEPTAR Y CONTINUAR (En la franja inferior dedicada, y = 44px)
            GameObject acceptBtnObj = CreateTacticalCardButton(
                cardObj.transform,
                "Btn_Accept_Terms",
                "[ ACEPTAR Y CONTINUAR ]",
                font,
                new Vector2(165f, 44f),
                new Vector2(320f, 50f),
                new Color(0.22f, 0.38f, 0.26f, 1f),
                new Color(0.88f, 0.98f, 0.90f, 1f));

            // Botón RECHAZAR / SALIR (En la franja inferior dedicada, y = 44px)
            GameObject declineBtnObj = CreateTacticalCardButton(
                cardObj.transform,
                "Btn_Decline_Terms",
                "[ RECHAZAR Y SALIR ]",
                font,
                new Vector2(-175f, 44f),
                new Vector2(280f, 50f),
                new Color(0.22f, 0.12f, 0.12f, 0.95f),
                new Color(0.90f, 0.75f, 0.75f, 1f));

            Button acceptBtn = acceptBtnObj.GetComponent<Button>();
            Button declineBtn = declineBtnObj.GetComponent<Button>();

            // Vincular eventos persistentes para que funcione incluso sin código adicional
            UnityEditor.Events.UnityEventTools.AddPersistentListener(acceptBtn.onClick, tosController.AcceptTerms);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(declineBtn.onClick, tosController.DeclineTerms);

            SerializedObject so = new SerializedObject(tosController);
            so.FindProperty("overlayCanvasGroup").objectReferenceValue = cg;
            so.FindProperty("acceptButton").objectReferenceValue = acceptBtn;
            so.FindProperty("declineButton").objectReferenceValue = declineBtn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateTacticalCardButton(
            Transform parent,
            string name,
            string label,
            TMP_FontAsset font,
            Vector2 anchoredPos,
            Vector2 size,
            Color bgColor,
            Color textColor)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = cb;

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            TMP_Text tmp = txtObj.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 18f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;

            RectTransform txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            return btnObj;
        }
    }
}
