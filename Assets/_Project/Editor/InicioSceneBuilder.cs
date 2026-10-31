using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;
using SudoKill9.UI.Inicio;

namespace SudoKill9.Editor
{
    /// <summary>
    /// Herramienta de Editor para construir la escena de Inicio (Menú Principal).
    /// Ejecuta: Tools > The Last Root > Build Complete Inicio Menu Scene
    /// No altera ninguna otra escena ni afecta assets compartidos.
    /// </summary>
    public static class InicioSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Inicio.unity";
        private const string BgmPath = "Assets/_Project/Audio/BGM/electronic-future-loop.ogg";
        private const string FontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        // Rutas de los modelos 3D de enemigos del proyecto
        private const string ModelEspiaPath = "Assets/_Project/Models/Characters/espia-skeleton/espia.fbx";
        private const string ModelEscudoPath = "Assets/_Project/Models/Characters/escudo-skeleton/escudo.fbx";
        private const string ModelVoladorPath = "Assets/_Project/Models/Characters/volador-skeleton/volador.fbx";
        private const string ModelVirusPath = "Assets/_Project/Models/Characters/virus-skeleton/virus.fbx";
        private const string ModelWindowsBossPath = "Assets/_Project/Models/Characters/windows-skeleton/windows.fbx";

        [MenuItem("Tools/The Last Root/Build Complete Inicio Menu Scene")]
        public static void BuildMenuScene()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(ScenePath);
                }
                else
                {
                    Debug.LogWarning("[InicioSceneBuilder] Operación cancelada por el usuario.");
                    return;
                }
            }

            Debug.Log("[InicioSceneBuilder] Iniciando rediseño completo de la escena Inicio...");

            // 1. Limpieza rigurosa de objetos viejos/duplicados
            CleanOldSceneObjects();

            // 2. Configurar Cámara y Audio
            Camera mainCam = SetupCameraAndAudio();

            // 3. Localizar posición de Tux
            Transform tuxTransform = GetLinuxCharacterTransform();

            // 4. Configurar Iluminación Cinemática
            SetupLighting(tuxTransform != null ? tuxTransform.position : Vector3.zero);

            // 5. Configurar Suelo de Circuito
            SetupFloor();

            // 6. Generar Horda de Bloques Enemigos Categorizados y Jefe Final hacia Tux
            SetupEnemyHorde(tuxTransform != null ? tuxTransform.position : new Vector3(0f, 0f, 2f));

            // 7. Construir Canvas y HUD Táctico (Sin duplicados y perfectamente alineado)
            SetupHUDCanvas(mainCam);

            // 8. Actualizar Build Settings
            UpdateBuildSettings();

            // 9. Guardar escena
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("[InicioSceneBuilder] ¡Escena de Inicio rediseñada con éxito con horda enemiga y jefe final!");
        }

        private static void CleanOldSceneObjects()
        {
            // Eliminar TODOS los Canvas existentes para evitar duplicados
            Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas c in allCanvases)
            {
                Object.DestroyImmediate(c.gameObject);
            }

            // Eliminar todos los EventSystem duplicados
            UnityEngine.EventSystems.EventSystem[] eventSystems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var es in eventSystems)
            {
                Object.DestroyImmediate(es.gameObject);
            }

            // Eliminar contenedores previos
            DestroyIfExists("Enemy_Horde_Container");
            DestroyIfExists("Menu_Environment");
            DestroyIfExists("Menu_Lighting");
        }

        private static void DestroyIfExists(string name)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null) Object.DestroyImmediate(obj);
        }

        private static Transform GetLinuxCharacterTransform()
        {
            GameObject linux = GameObject.Find("linux");
            if (linux == null) linux = GameObject.Find("Player_LinuxSkeleton");

            if (linux != null)
            {
                Animator anim = linux.GetComponentInChildren<Animator>();
                if (anim != null) anim.applyRootMotion = false;

                Animation legacy = linux.GetComponent<Animation>();
                if (legacy != null) Object.DestroyImmediate(legacy);

                LinuxSkeletonController ctrl = linux.GetComponent<LinuxSkeletonController>();
                if (ctrl != null) Object.DestroyImmediate(ctrl);

                return linux.transform;
            }
            return null;
        }

        private static Camera SetupCameraAndAudio()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();

            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.06f, 0.10f, 1f);

            // Parallax sutil
            if (cam.GetComponent<MenuParallaxCamera>() == null)
            {
                cam.gameObject.AddComponent<MenuParallaxCamera>();
            }

            // Audio BGM
            AudioSource audio = cam.GetComponent<AudioSource>();
            if (audio == null) audio = cam.gameObject.AddComponent<AudioSource>();

            AudioClip bgm = AssetDatabase.LoadAssetAtPath<AudioClip>(BgmPath);
            if (bgm != null)
            {
                audio.clip = bgm;
                audio.loop = true;
                audio.playOnAwake = true;
                audio.volume = 0.65f;
            }

            if (cam.GetComponent<AudioListener>() == null)
            {
                cam.gameObject.AddComponent<AudioListener>();
            }

            return cam;
        }

        private static void SetupLighting(Vector3 targetPos)
        {
            GameObject lightingRoot = new GameObject("Menu_Lighting");

            // Key Light frontal suave
            GameObject keyObj = new GameObject("Key_Light_Front");
            keyObj.transform.SetParent(lightingRoot.transform);
            Light keyLight = keyObj.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.85f, 0.92f, 1f);
            keyLight.intensity = 1.1f;
            keyObj.transform.rotation = Quaternion.Euler(30f, 25f, 0f);

            // Rim Light cian de contraluz para Tux
            GameObject rimObj = new GameObject("Rim_Light_Cyan_Back");
            rimObj.transform.SetParent(lightingRoot.transform);
            Light rimLight = rimObj.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(0f, 0.85f, 1f);
            rimLight.intensity = 1.6f;
            rimObj.transform.rotation = Quaternion.Euler(20f, 195f, 0f);

            // Luz roja amenazante para el Jefe Final
            GameObject bossLightObj = new GameObject("Boss_Threat_Light_Red");
            bossLightObj.transform.SetParent(lightingRoot.transform);
            Light bossLight = bossLightObj.AddComponent<Light>();
            bossLight.type = LightType.Point;
            bossLight.color = new Color(1f, 0.15f, 0.15f);
            bossLight.intensity = 4.0f;
            bossLight.range = 14f;
            bossLightObj.transform.position = new Vector3(0f, 4f, -7f);
        }

        private static void SetupFloor()
        {
            GameObject envRoot = new GameObject("Menu_Environment");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Menu_Circuit_Floor";
            floor.transform.SetParent(envRoot.transform);
            floor.transform.position = new Vector3(0f, -0.01f, 0f);
            floor.transform.localScale = new Vector3(6f, 1f, 6f);

            Material floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            floorMat.color = new Color(0.05f, 0.07f, 0.11f);
            floorMat.SetFloat("_Smoothness", 0.35f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;
        }

        // ============================================================== //
        // GENERACIÓN DE LA HORDA Y EL JEFE FINAL                         //
        // ============================================================== //

        private static void SetupEnemyHorde(Vector3 tuxTargetPos)
        {
            GameObject hordeRoot = new GameObject("Enemy_Horde_Container");

            // Cargar referencias de modelos
            GameObject prefabEspia = AssetDatabase.LoadAssetAtPath<GameObject>(ModelEspiaPath);
            GameObject prefabEscudo = AssetDatabase.LoadAssetAtPath<GameObject>(ModelEscudoPath);
            GameObject prefabVolador = AssetDatabase.LoadAssetAtPath<GameObject>(ModelVoladorPath);
            GameObject prefabVirus = AssetDatabase.LoadAssetAtPath<GameObject>(ModelVirusPath);
            GameObject prefabWindows = AssetDatabase.LoadAssetAtPath<GameObject>(ModelWindowsBossPath);

            // Materiales de colores para las categorías
            Material matGrunt = CreateColorMaterial("Mat_Enemy_Grunt", new Color(0.25f, 0.30f, 0.38f), 0.4f);
            Material matTank = CreateColorMaterial("Mat_Enemy_Tank", new Color(0.40f, 0.25f, 0.15f), 0.2f);
            Material matFlyer = CreateColorMaterial("Mat_Enemy_Flyer", new Color(0.18f, 0.12f, 0.30f), 0.6f);
            Material matVirus = CreateColorMaterial("Mat_Enemy_Virus", new Color(0.12f, 0.35f, 0.22f), 0.3f);
            Material matBoss = CreateColorMaterial("Mat_Final_Boss", new Color(0.08f, 0.08f, 0.12f), 0.8f);

            // 1. EL JEFE FINAL (Bloque único monumental en el fondo central)
            CreateFinalBossMonolith(hordeRoot.transform, tuxTargetPos, prefabWindows, matBoss);

            // 2. OLA 1: GRUNTS / ESPÍAS (Cápsulas ágiles marchando en primera y segunda línea)
            CreateCategoryGroup(hordeRoot.transform, "1_GRUNTS_Espias (Capsulas)", PrimitiveType.Capsule,
                EnemyTypeCategory.Soldier_Espia, prefabEspia, matGrunt, tuxTargetPos,
                new Vector3[] {
                    new Vector3(-2.8f, 0.9f, -1.0f),
                    new Vector3(-1.4f, 0.9f, -2.0f),
                    new Vector3( 1.5f, 0.9f, -1.8f),
                    new Vector3( 2.8f, 0.9f, -1.2f),
                    new Vector3(-0.6f, 0.9f, -3.2f),
                    new Vector3( 0.8f, 0.9f, -3.0f)
                },
                new Vector3(0.9f, 1.8f, 0.9f), false);

            // 3. OLA 2: TANKS / ESCUDOS (Cubos robustos pesados en los flancos de contención)
            CreateCategoryGroup(hordeRoot.transform, "2_TANKS_Escudos (Cubos Blindados)", PrimitiveType.Cube,
                EnemyTypeCategory.Tank_Escudo, prefabEscudo, matTank, tuxTargetPos,
                new Vector3[] {
                    new Vector3(-4.2f, 1.0f, -2.5f),
                    new Vector3( 4.0f, 1.0f, -2.3f),
                    new Vector3(-3.2f, 1.0f, -4.5f),
                    new Vector3( 3.2f, 1.0f, -4.3f)
                },
                new Vector3(1.8f, 1.8f, 1.4f), false);

            // 4. OLA 3: VOLADORES (Esferas / Diamantes flotantes en el aire a 2.5m - 3.5m)
            CreateCategoryGroup(hordeRoot.transform, "3_FLYERS_Voladores (Esferas Aereas)", PrimitiveType.Sphere,
                EnemyTypeCategory.Flyer_Volador, prefabVolador, matFlyer, tuxTargetPos,
                new Vector3[] {
                    new Vector3(-2.2f, 2.8f, -2.5f),
                    new Vector3( 2.4f, 3.1f, -2.8f),
                    new Vector3(-0.5f, 3.4f, -4.5f),
                    new Vector3( 1.2f, 2.7f, -4.2f)
                },
                new Vector3(1.2f, 1.2f, 1.2f), true);

            // 5. OLA 4: VIRUS (Esferas bajas avanzadas)
            CreateCategoryGroup(hordeRoot.transform, "4_VIRUS_Crawlers (Esferas Bajas)", PrimitiveType.Sphere,
                EnemyTypeCategory.Virus_Crawler, prefabVirus, matVirus, tuxTargetPos,
                new Vector3[] {
                    new Vector3(-1.8f, 0.4f, 0.2f),
                    new Vector3( 2.0f, 0.4f, 0.4f),
                    new Vector3(-0.2f, 0.4f, -1.0f)
                },
                new Vector3(0.8f, 0.8f, 0.8f), false);
        }

        private static void CreateFinalBossMonolith(Transform parent, Vector3 targetPos, GameObject prefab, Material mat)
        {
            GameObject bossGroup = new GameObject("0_FINAL_BOSS_Windows (Monolito Unico)");
            bossGroup.transform.SetParent(parent, false);

            // Bloque principal colosal
            GameObject bossMonolith = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bossMonolith.name = "BOSS_Final_Windows_Placeholder";
            bossMonolith.transform.SetParent(bossGroup.transform, false);
            bossMonolith.transform.position = new Vector3(0f, 2.8f, -7.0f);
            bossMonolith.transform.localScale = new Vector3(3.2f, 5.6f, 2.2f);
            bossMonolith.GetComponent<Renderer>().sharedMaterial = mat;

            // Orientar hacia el frente mirando a Tux
            Vector3 lookDir = targetPos - bossMonolith.transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                bossMonolith.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }

            // Corona / Halo luminoso superior del Boss
            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            halo.name = "Boss_Crown_Accent";
            halo.transform.SetParent(bossMonolith.transform, false);
            halo.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            halo.transform.localScale = new Vector3(1.05f, 0.08f, 1.05f);
            Material haloMat = CreateColorMaterial("Mat_Boss_Halo", new Color(1f, 0.1f, 0.2f), 0.9f);
            halo.GetComponent<Renderer>().sharedMaterial = haloMat;
            Object.DestroyImmediate(halo.GetComponent<Collider>());

            // Componente de reemplazo futuro
            EnemyPlaceholder ep = bossMonolith.AddComponent<EnemyPlaceholder>();
            ep.category = EnemyTypeCategory.FinalBoss_Windows;
            ep.replacementModelPrefab = prefab;

            // Animación ambiental de presencia
            bossMonolith.AddComponent<MenuHordeAmbience>();
            Object.DestroyImmediate(bossMonolith.GetComponent<Collider>());
        }

        private static void CreateCategoryGroup(Transform parent, string groupName, PrimitiveType shape,
            EnemyTypeCategory category, GameObject replacementPrefab, Material mat, Vector3 targetPos,
            Vector3[] positions, Vector3 scale, bool isFlyer)
        {
            GameObject group = new GameObject(groupName);
            group.transform.SetParent(parent, false);

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject block = GameObject.CreatePrimitive(shape);
                block.name = $"{category}_{i + 1}_Placeholder";
                block.transform.SetParent(group.transform, false);
                block.transform.position = positions[i];
                block.transform.localScale = scale;
                block.GetComponent<Renderer>().sharedMaterial = mat;

                // Orientar el bloque hacia Tux para dar la sensación de avance
                Vector3 lookDir = targetPos - block.transform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    block.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                }

                // Ojos/Visor amenazante en el bloque
                AddEyeAccent(block.transform, isFlyer);

                // Script de reemplazo futuro
                EnemyPlaceholder ep = block.AddComponent<EnemyPlaceholder>();
                ep.category = category;
                ep.replacementModelPrefab = replacementPrefab;

                // Script de flotación / respiración
                MenuHordeAmbience amb = block.AddComponent<MenuHordeAmbience>();
                SerializedObject so = new SerializedObject(amb);
                so.FindProperty("isFlyer").boolValue = isFlyer;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Quitar colisionador
                Object.DestroyImmediate(block.GetComponent<Collider>());
            }
        }

        private static void AddEyeAccent(Transform parent, bool isFlyer)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eye.name = "Visor_Accent";
            eye.transform.SetParent(parent, false);
            eye.transform.localPosition = new Vector3(0f, isFlyer ? 0f : 0.25f, 0.48f);
            eye.transform.localScale = new Vector3(0.5f, 0.12f, 0.15f);

            Material eyeMat = CreateColorMaterial("Mat_Visor_Red", new Color(1f, 0.2f, 0.2f), 0.95f);
            eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
            Object.DestroyImmediate(eye.GetComponent<Collider>());
        }

        private static Material CreateColorMaterial(string matName, Color color, float smoothness)
        {
            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m.name = matName;
            m.color = color;
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        // ============================================================== //
        // UTILIDAD DE REEMPLAZO AUTOMÁTICO EN 1 CLIC                     //
        // ============================================================== //

        [MenuItem("Tools/The Last Root/Swap All Placeholders With Real Enemies")]
        public static void SwapAllPlaceholders()
        {
            EnemyPlaceholder[] placeholders = Object.FindObjectsByType<EnemyPlaceholder>(FindObjectsSortMode.None);
            if (placeholders.Length == 0)
            {
                Debug.LogWarning("[InicioSceneBuilder] No se encontraron bloques EnemyPlaceholder en la escena.");
                return;
            }

            int count = 0;
            foreach (var p in placeholders)
            {
                if (p.replacementModelPrefab != null)
                {
                    p.ReplaceWithModel();
                    count++;
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[InicioSceneBuilder] ¡{count} bloques placeholder fueron reemplazados por sus modelos 3D reales!");
        }

        // ============================================================== //
        // CANVAS Y HUD TÁCTICO                                           //
        // ============================================================== //

        private static void SetupHUDCanvas(Camera cam)
        {
            GameObject canvasObj = new GameObject("Menu_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            MainMenuController menuController = canvasObj.AddComponent<MainMenuController>();

            // EventSystem
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            // 1. Título Header (Arriba Izquierda)
            CreateTitleHeader(canvasObj.transform, fontAsset);

            // 2. Columna de Botones (Izquierda Centro)
            CreateButtonsMenu(canvasObj.transform, menuController, fontAsset);

            // 3. Modales
            GameObject optionsPanel = CreateOptionsModal(canvasObj.transform, menuController, fontAsset);
            GameObject creditsPanel = CreateCreditsModal(canvasObj.transform, menuController, fontAsset);

            SerializedObject so = new SerializedObject(menuController);
            so.FindProperty("optionsPanel").objectReferenceValue = optionsPanel;
            so.FindProperty("creditsPanel").objectReferenceValue = creditsPanel;
            so.FindProperty("bgmAudioSource").objectReferenceValue = cam.GetComponent<AudioSource>();
            so.FindProperty("gameplaySceneName").stringValue = "SampleScene";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateTitleHeader(Transform parent, TMP_FontAsset font)
        {
            GameObject headerObj = new GameObject("Header_Container");
            headerObj.transform.SetParent(parent, false);

            RectTransform rt = headerObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(80f, -60f);
            rt.sizeDelta = new Vector2(600f, 160f);

            GameObject titleObj = new GameObject("Title_Text");
            titleObj.transform.SetParent(headerObj.transform, false);
            TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
            if (font != null) titleText.font = font;
            titleText.text = "SUDO KILL -9";
            titleText.fontSize = 58;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(0.95f, 0.98f, 1f);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = Vector2.zero;
            titleRt.sizeDelta = new Vector2(600f, 70f);

            GameObject subObj = new GameObject("Subtitle_Text");
            subObj.transform.SetParent(headerObj.transform, false);
            TMP_Text subText = subObj.AddComponent<TextMeshProUGUI>();
            if (font != null) subText.font = font;
            subText.text = "> THE LAST ROOT // PROTOCOL: INITIATED";
            subText.fontSize = 18;
            subText.color = new Color(0.1f, 0.85f, 0.95f);
            RectTransform subRt = subObj.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0f, 1f);
            subRt.anchorMax = new Vector2(1f, 1f);
            subRt.pivot = new Vector2(0f, 1f);
            subRt.anchoredPosition = new Vector2(4f, -75f);
            subRt.sizeDelta = new Vector2(600f, 35f);

            GameObject lineObj = new GameObject("Accent_Line");
            lineObj.transform.SetParent(headerObj.transform, false);
            Image lineImg = lineObj.AddComponent<Image>();
            lineImg.color = new Color(0.1f, 0.85f, 0.95f, 0.75f);
            RectTransform lineRt = lineObj.GetComponent<RectTransform>();
            lineRt.anchorMin = new Vector2(0f, 1f);
            lineRt.anchorMax = new Vector2(0f, 1f);
            lineRt.pivot = new Vector2(0f, 1f);
            lineRt.anchoredPosition = new Vector2(0f, -115f);
            lineRt.sizeDelta = new Vector2(420f, 2f);
        }

        private static void CreateButtonsMenu(Transform parent, MainMenuController controller, TMP_FontAsset font)
        {
            GameObject menuContainer = new GameObject("Menu_Buttons_Column");
            menuContainer.transform.SetParent(parent, false);

            RectTransform rt = menuContainer.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(80f, -50f);
            rt.sizeDelta = new Vector2(400f, 320f);

            VerticalLayoutGroup layout = menuContainer.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateMenuButton(menuContainer.transform, "[ > INICIAR MISIÓN ]", font, () => controller.StartGame(), true);
            CreateMenuButton(menuContainer.transform, "[ > CONFIGURACIÓN ]", font, () => controller.OpenOptions(), false);
            CreateMenuButton(menuContainer.transform, "[ > CRÉDITOS ]", font, () => controller.OpenCredits(), false);
            CreateMenuButton(menuContainer.transform, "[ > SALIR DEL SISTEMA ]", font, () => controller.QuitGame(), false);
        }

        private static GameObject CreateMenuButton(Transform parent, string label, TMP_FontAsset font, UnityEngine.Events.UnityAction action, bool isPrimary)
        {
            GameObject btnObj = new GameObject("Btn_" + label.Replace(" ", "_"));
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.pivot = new Vector2(0f, 0.5f);

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = 56f;
            le.preferredHeight = 56f;
            le.flexibleHeight = 0f;

            Image img = btnObj.AddComponent<Image>();
            img.color = isPrimary 
                ? new Color(0.12f, 0.28f, 0.45f, 0.90f) 
                : new Color(0.08f, 0.12f, 0.18f, 0.85f);

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(action);

            btnObj.AddComponent<MenuButtonEffects>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = isPrimary ? new Color(0.4f, 1f, 1f) : Color.white;

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.pivot = new Vector2(0f, 0.5f);
            textRt.offsetMin = new Vector2(24f, 0f);
            textRt.offsetMax = new Vector2(-15f, 0f);

            return btnObj;
        }

        private static GameObject CreateOptionsModal(Transform parent, MainMenuController controller, TMP_FontAsset font)
        {
            GameObject modal = new GameObject("Panel_Options");
            modal.transform.SetParent(parent, false);

            RectTransform rt = modal.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(520f, 360f);

            Image bg = modal.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.14f, 0.96f);

            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(modal.transform, false);
            TMP_Text title = titleObj.AddComponent<TextMeshProUGUI>();
            if (font != null) title.font = font;
            title.text = "// CONFIGURACIÓN DE AUDIO";
            title.fontSize = 24;
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.2f, 0.9f, 1f);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -25f);
            titleRt.sizeDelta = new Vector2(460f, 40f);

            CreateCloseButton(modal.transform, () => controller.CloseOptions(), font);

            modal.SetActive(false);
            return modal;
        }

        private static GameObject CreateCreditsModal(Transform parent, MainMenuController controller, TMP_FontAsset font)
        {
            GameObject modal = new GameObject("Panel_Credits");
            modal.transform.SetParent(parent, false);

            RectTransform rt = modal.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(580f, 400f);

            Image bg = modal.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.14f, 0.96f);

            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(modal.transform, false);
            TMP_Text title = titleObj.AddComponent<TextMeshProUGUI>();
            if (font != null) title.font = font;
            title.text = "// CRÉDITOS DEL PROYECTO";
            title.fontSize = 24;
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.2f, 0.9f, 1f);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -25f);
            titleRt.sizeDelta = new Vector2(500f, 40f);

            GameObject contentObj = new GameObject("Credits_Body");
            contentObj.transform.SetParent(modal.transform, false);
            TMP_Text body = contentObj.AddComponent<TextMeshProUGUI>();
            if (font != null) body.font = font;
            body.text = "<b>SUDO KILL -9: THE LAST ROOT</b>\n\n" +
                        "Desarrollado para el proyecto de Interfaces.\n\n" +
                        "• <b>Protagonista:</b> Tux (Linux Skeleton)\n" +
                        "• <b>Jefe Final:</b> Windows Skeleton\n" +
                        "• <b>Horda Enemiga:</b> Espías, Escudos, Voladores y Virus\n\n" +
                        "<i>> Acceso de superusuario concedido.</i>";
            body.fontSize = 17;
            body.lineSpacing = 8;
            body.color = new Color(0.85f, 0.9f, 0.95f);
            RectTransform bodyRt = contentObj.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRt.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRt.pivot = new Vector2(0.5f, 0.5f);
            bodyRt.anchoredPosition = new Vector2(0f, -10f);
            bodyRt.sizeDelta = new Vector2(500f, 240f);

            CreateCloseButton(modal.transform, () => controller.CloseCredits(), font);

            modal.SetActive(false);
            return modal;
        }

        private static void CreateCloseButton(Transform parent, UnityEngine.Events.UnityAction action, TMP_FontAsset font)
        {
            GameObject closeObj = new GameObject("Btn_Close");
            closeObj.transform.SetParent(parent, false);

            RectTransform rt = closeObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-15f, -15f);
            rt.sizeDelta = new Vector2(40f, 40f);

            Image img = closeObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.3f, 0.45f, 0.8f);

            Button btn = closeObj.AddComponent<Button>();
            btn.onClick.AddListener(action);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(closeObj.transform, false);
            TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = "X";
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
        }

        private static void UpdateBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            bool inicioFound = false;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == ScenePath)
                {
                    inicioFound = true;
                    break;
                }
            }

            if (!inicioFound)
            {
                var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
                newScenes[0] = new EditorBuildSettingsScene(ScenePath, true);
                for (int i = 0; i < scenes.Length; i++)
                {
                    newScenes[i + 1] = scenes[i];
                }
                EditorBuildSettings.scenes = newScenes;
                Debug.Log("[InicioSceneBuilder] Escena Inicio registrada en el índice 0 de Build Settings.");
            }
        }
    }
}
