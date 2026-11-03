using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using TMPro;
using SudoKill9.UI;
using SudoKill9.UI.Inicio;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Herramienta Editor 1-Clic para la configuración completa de:
    /// - Canvas táctico Pixel Art (CombatHUDManager, BSODGlitchEffect, VictoryDefeatScreen)
    /// - Windows Boss (WindowsBossAI con 3 fases y WindowsCommandAttacks con SaltaLoco, taskkill, firewall, Kamehameha y BSOD)
    /// - Integración de salud de Linux (PlayerHealth y LinuxCombatIK)
    /// - Verificación automática de la escena prueba_Expe_K.unity.
    /// </summary>
    [InitializeOnLoad]
    public static class SetupCombatUIAndBossAbilities
    {
        private const string ScenePath = "Assets/_Project/Scenes/Experimento_K/prueba_Expe_K.unity";
        private const string ClipsDir = "Assets/_Project/Animations/Clips/Animacion_Boss";
        private const string ControllerPath = ClipsDir + "/WindowsBoss_MasterController.controller";
        private const string FontPath = "Assets/_Project/Fonts/PixelTactical_Point.asset";
        private const string ButtonSpritePath = "Assets/_Project/Sprites/UI/Inicio/PixelArt_Tactical_Button.png";
        private const string PanelSpritePath = "Assets/_Project/Sprites/UI/Inicio/PixelArt_Tactical_Panel.png";

        static SetupCombatUIAndBossAbilities()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                const string folder = "Assets/_Project/Scenes/Experimento_K";
                AssetDatabase.ImportAsset(folder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
                
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (activeScene.path == ScenePath)
                {
                    EjecutarConfiguracionSilenciosa();
                    EjecutarPruebasVerificacion();
                }
            };
        }

        public static void EjecutarConfiguracionSilenciosa()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.path != ScenePath) return;

            AnimatorController ctrl = AsegurarEstadosEspecialesEnController();
            ConfigurarJugadorLinux();
            ConfigurarJefeWindows(ctrl);
            ConfigurarCanvasCombate();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveOpenScenes();
        }

        [MenuItem("Tools/SudoKill9/Configurar Combate Completo y HUD Boss", priority = 10)]
        public static void EjecutarConfiguracionCompleta()
        {
            Debug.Log("<color=cyan>[SetupCombatUI]</color> === Iniciando Configuración Integral de Combate y HUD ===");

            // 1. Abrir escena si no está activa
            AsegurarEscenaAbierta();

            // 2. Configurar estados especiales en WindowsBoss_MasterController
            AnimatorController ctrl = AsegurarEstadosEspecialesEnController();

            // 3. Configurar al jugador Linux (PlayerHealth y LinuxCombatIK)
            ConfigurarJugadorLinux();

            // 4. Configurar al Jefe Windows (WindowsBossAI y WindowsCommandAttacks)
            ConfigurarJefeWindows(ctrl);

            // 5. Configurar Canvas Táctico de Combate (HUD, BSOD, Victory/Defeat)
            ConfigurarCanvasCombate();

            // 6. Guardar escena
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("<color=green>[SetupCombatUI]</color> ¡Configuración integral completada y guardada con éxito en prueba_Expe_K.unity!");

            // 7. Ejecutar verificación
            EjecutarPruebasVerificacion();
        }

        private static void AsegurarEscenaAbierta()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }
        }

        private static AnimatorController AsegurarEstadosEspecialesEnController()
        {
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
            {
                Debug.LogError($"[SetupCombatUI] No se encontró el controller en {ControllerPath}");
                return null;
            }

            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

            // Cargar clips especiales
            AnimationClip saltaLocoClip   = LoadClip(ClipsDir + "/SaltaLoco.fbx", false);
            AnimationClip poseCansadoClip = LoadClip(ClipsDir + "/PoseCansado.fbx", true);
            AnimationClip gritoClip       = LoadClip(ClipsDir + "/Grito.fbx", false);
            AnimationClip seRieClip       = LoadClip(ClipsDir + "/Se rie.fbx", false);

            AnimatorState sPrep = null;
            foreach (var s in sm.states)
            {
                if (s.state.name == "Se prepara para atacar")
                {
                    sPrep = s.state;
                    break;
                }
            }

            // Asegurar estado SaltaLoco
            AsegurarEstadoConRetorno(sm, "SaltaLoco", saltaLocoClip, new Vector3(100f, -50f, 0f), sPrep, 0.85f);

            // Asegurar estado PoseCansado
            AsegurarEstadoConRetorno(sm, "PoseCansado", poseCansadoClip, new Vector3(300f, -50f, 0f), sPrep, 0.95f);

            // Asegurar estado Grito
            AsegurarEstadoConRetorno(sm, "Grito", gritoClip, new Vector3(500f, -50f, 0f), sPrep, 0.85f);

            // Asegurar estado Se rie
            AsegurarEstadoConRetorno(sm, "Se rie", seRieClip, new Vector3(700f, -50f, 0f), sPrep, 0.85f);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        private static AnimatorState AsegurarEstadoConRetorno(
            AnimatorStateMachine sm,
            string nombreEstado,
            AnimationClip clip,
            Vector3 posicion,
            AnimatorState estadoRetorno,
            float exitTime)
        {
            AnimatorState estado = null;
            foreach (var s in sm.states)
            {
                if (s.state.name == nombreEstado)
                {
                    estado = s.state;
                    break;
                }
            }

            if (estado == null)
            {
                estado = sm.AddState(nombreEstado, posicion);
            }

            if (clip != null)
            {
                estado.motion = clip;
            }

            // Asegurar transición de salida hacia estadoRetorno si no la tiene
            if (estadoRetorno != null && estado != estadoRetorno)
            {
                bool tieneTransicion = false;
                foreach (var t in estado.transitions)
                {
                    if (t.destinationState == estadoRetorno)
                    {
                        tieneTransicion = true;
                        break;
                    }
                }

                if (!tieneTransicion)
                {
                    var trans = estado.AddTransition(estadoRetorno);
                    trans.hasExitTime = true;
                    trans.exitTime = exitTime;
                    trans.duration = 0.2f;
                }
            }

            return estado;
        }

        private static AnimationClip LoadClip(string path, bool loop)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    return c;
                }
            }
            return null;
        }

        private static void ConfigurarJugadorLinux()
        {
            var combatIK = Object.FindFirstObjectByType<LinuxCombatIK>();
            if (combatIK == null)
            {
                Debug.LogWarning("[SetupCombatUI] No se encontró LinuxCombatIK en la escena.");
                return;
            }

            GameObject playerGO = combatIK.gameObject;

            // Asegurar componente PlayerHealth
            PlayerHealth health = playerGO.GetComponent<PlayerHealth>();
            if (health == null)
            {
                health = playerGO.AddComponent<PlayerHealth>();
            }

            SerializedObject soHealth = new SerializedObject(health);
            SerializedProperty maxHpProp = soHealth.FindProperty("maxHealth");
            SerializedProperty invincProp = soHealth.FindProperty("invincibleOnHit");
            SerializedProperty durProp = soHealth.FindProperty("invincibilityDuration");

            if (maxHpProp != null && maxHpProp.intValue < 5) maxHpProp.intValue = 5;
            if (invincProp != null) invincProp.boolValue = true;
            if (durProp != null) durProp.floatValue = 0.45f;
            soHealth.ApplyModifiedPropertiesWithoutUndo();

            // Asignar playerHealth a combatIK
            SerializedObject soCombat = new SerializedObject(combatIK);
            SerializedProperty phProp = soCombat.FindProperty("playerHealth");
            if (phProp != null)
            {
                phProp.objectReferenceValue = health;
            }
            soCombat.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(health);
            EditorUtility.SetDirty(combatIK);
            Debug.Log($"<color=green>[SetupCombatUI]</color> Jugador '{playerGO.name}' configurado con PlayerHealth (5 HP) y LinuxCombatIK.");
        }

        private static void ConfigurarJefeWindows(AnimatorController ctrl)
        {
            GameObject windowsGO = GameObject.Find("windows");
            if (windowsGO == null)
            {
                Debug.LogError("[SetupCombatUI] No se encontró el GameObject 'windows' en la escena.");
                return;
            }

            // 1. Animator
            Animator anim = windowsGO.GetComponent<Animator>();
            if (anim == null) anim = windowsGO.GetComponentInChildren<Animator>();
            if (anim != null && ctrl != null)
            {
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
            }

            // 2. WindowsBossAI
            WindowsBossAI bossAI = windowsGO.GetComponent<WindowsBossAI>();
            if (bossAI == null) bossAI = windowsGO.AddComponent<WindowsBossAI>();

            bossAI.GarantizarComponentesFisicos();

            // Buscar jugador para Target
            var combatIK = Object.FindFirstObjectByType<LinuxCombatIK>();
            if (combatIK != null)
            {
                bossAI.Target = combatIK.transform;
            }

            // 3. WindowsCommandAttacks
            WindowsCommandAttacks cmdAttacks = windowsGO.GetComponent<WindowsCommandAttacks>();
            if (cmdAttacks == null) cmdAttacks = windowsGO.AddComponent<WindowsCommandAttacks>();

            cmdAttacks.GarantizarComponentesVisuales();

            // Asignar referencia cruzada en WindowsBossAI
            SerializedObject soBoss = new SerializedObject(bossAI);
            SerializedProperty cmdProp = soBoss.FindProperty("commandAttacks");
            if (cmdProp != null)
            {
                cmdProp.objectReferenceValue = cmdAttacks;
            }
            SerializedProperty targetProp = soBoss.FindProperty("target");
            if (targetProp != null && combatIK != null)
            {
                targetProp.objectReferenceValue = combatIK.transform;
            }
            soBoss.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(windowsGO);
            EditorUtility.SetDirty(bossAI);
            EditorUtility.SetDirty(cmdAttacks);
            Debug.Log("<color=green>[SetupCombatUI]</color> Jefe 'windows' configurado con WindowsBossAI y WindowsCommandAttacks.");
        }

        private static void ConfigurarCanvasCombate()
        {
            // Asegurar EventSystem
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
            }

            // Cargar recursos visuales
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Sprite buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSpritePath);
            Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);

            // Buscar o crear Canvas
            GameObject canvasGO = GameObject.Find("CombatUI_Canvas");
            if (canvasGO != null && canvasGO.GetComponent<RectTransform>() == null)
            {
                Object.DestroyImmediate(canvasGO);
                canvasGO = null;
            }
            if (canvasGO == null)
            {
                canvasGO = new GameObject("CombatUI_Canvas", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(canvasGO, "Create CombatUI_Canvas");
            }

            Canvas canvas = canvasGO.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (canvasGO.GetComponent<GraphicRaycaster>() == null)
            {
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            // 1. HUD Manager
            CombatHUDManager hudManager = canvasGO.GetComponent<CombatHUDManager>();
            if (hudManager == null) hudManager = canvasGO.AddComponent<CombatHUDManager>();

            // Construir o actualizar elementos visuales
            ConstruirBarraBoss(canvasGO.transform, font, panelSprite, hudManager);
            ConstruirBarraTux(canvasGO.transform, font, panelSprite, hudManager);
            ConstruirTerminalCMD(canvasGO.transform, font, panelSprite, hudManager);

            // 2. BSOD Glitch Effect
            ConstruirPanelBSOD(canvasGO.transform, font);

            // 3. Victory Defeat Screen
            ConstruirPanelVictoriaDerrota(canvasGO.transform, font, buttonSprite, panelSprite);

            EditorUtility.SetDirty(canvasGO);
            EditorUtility.SetDirty(hudManager);
        }

        private static void ConstruirBarraBoss(Transform parent, TMP_FontAsset font, Sprite panelSprite, CombatHUDManager hud)
        {
            GameObject bossGO = ObtenerOCrearHijo(parent, "BossHealthBar");

            RectTransform rt = ObtenerOAgregarComponente<RectTransform>(bossGO);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -25f);
            rt.sizeDelta = new Vector2(620f, 75f);

            // Marco de fondo
            Image panelImg = ObtenerOAgregarComponente<Image>(bossGO);
            if (panelSprite != null)
            {
                panelImg.sprite = panelSprite;
                panelImg.type = Image.Type.Sliced;
            }
            panelImg.color = new Color(0.12f, 0.08f, 0.08f, 0.92f); // Óxido oscuro táctico

            // Título
            TMP_Text title = ObtenerOCrearTexto(bossGO.transform, "BossTitle", font, 18, FontStyles.Bold, TextAlignmentOptions.Center);
            RectTransform rtTitle = title.rectTransform;
            rtTitle.anchorMin = new Vector2(0f, 1f);
            rtTitle.anchorMax = new Vector2(1f, 1f);
            rtTitle.pivot = new Vector2(0.5f, 1f);
            rtTitle.anchoredPosition = new Vector2(0f, -6f);
            rtTitle.sizeDelta = new Vector2(-20f, 24f);
            title.text = "WINDOWS_11_DEFENDER.EXE";
            title.color = new Color(1f, 0.88f, 0.70f);

            // Contenedor de barras
            GameObject barArea = ObtenerOCrearHijo(bossGO.transform, "BarArea");
            RectTransform rtBarArea = ObtenerOAgregarComponente<RectTransform>(barArea);
            rtBarArea.anchorMin = new Vector2(0f, 0f);
            rtBarArea.anchorMax = new Vector2(1f, 1f);
            rtBarArea.offsetMin = new Vector2(18f, 22f);
            rtBarArea.offsetMax = new Vector2(-18f, -30f);

            // Fondo negro de la barra
            Image barBg = ObtenerOAgregarComponente<Image>(barArea);
            barBg.color = new Color(0.04f, 0.04f, 0.06f, 0.95f);

            // Barra Decay (Amarillo/Naranja lento)
            GameObject decayGO = ObtenerOCrearHijo(barArea.transform, "DecayBar");
            RectTransform rtDecay = ObtenerOAgregarComponente<RectTransform>(decayGO);
            rtDecay.anchorMin = Vector2.zero;
            rtDecay.anchorMax = Vector2.one;
            rtDecay.sizeDelta = Vector2.zero;
            Image decayImg = ObtenerOAgregarComponente<Image>(decayGO);
            decayImg.type = Image.Type.Filled;
            decayImg.fillMethod = Image.FillMethod.Horizontal;
            decayImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            decayImg.fillAmount = 1f;
            decayImg.color = new Color(0.85f, 0.35f, 0.10f, 0.90f);

            // Barra Frontal (Ámbar dorado)
            GameObject fillGO = ObtenerOCrearHijo(barArea.transform, "FillBar");
            RectTransform rtFill = ObtenerOAgregarComponente<RectTransform>(fillGO);
            rtFill.anchorMin = Vector2.zero;
            rtFill.anchorMax = Vector2.one;
            rtFill.sizeDelta = Vector2.zero;
            Image fillImg = ObtenerOAgregarComponente<Image>(fillGO);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
            fillImg.color = new Color(0.96f, 0.71f, 0.10f); // #F4B41A

            // Subtítulo de Fase
            TMP_Text subtitle = ObtenerOCrearTexto(bossGO.transform, "BossSubtitle", font, 12, FontStyles.Normal, TextAlignmentOptions.Left);
            RectTransform rtSub = subtitle.rectTransform;
            rtSub.anchorMin = new Vector2(0f, 0f);
            rtSub.anchorMax = new Vector2(0.75f, 0f);
            rtSub.pivot = new Vector2(0f, 0f);
            rtSub.anchoredPosition = new Vector2(18f, 5f);
            rtSub.sizeDelta = new Vector2(0f, 18f);
            subtitle.text = "ESTADO: ESCANEANDO ARCHIVOS DE SISTEMA... [FASE 1]";
            subtitle.color = new Color(0.75f, 0.85f, 0.90f);

            // Número HP
            TMP_Text hpNumber = ObtenerOCrearTexto(bossGO.transform, "BossHPNumber", font, 12, FontStyles.Bold, TextAlignmentOptions.Right);
            RectTransform rtHp = hpNumber.rectTransform;
            rtHp.anchorMin = new Vector2(0.75f, 0f);
            rtHp.anchorMax = new Vector2(1f, 0f);
            rtHp.pivot = new Vector2(1f, 0f);
            rtHp.anchoredPosition = new Vector2(-18f, 5f);
            rtHp.sizeDelta = new Vector2(0f, 18f);
            hpNumber.text = "500 / 500 HP";
            hpNumber.color = new Color(1f, 0.95f, 0.75f);

            // Asignar en HUD
            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("bossBarContainer").objectReferenceValue = bossGO;
            so.FindProperty("bossHealthFillImage").objectReferenceValue = fillImg;
            so.FindProperty("bossHealthDecayImage").objectReferenceValue = decayImg;
            so.FindProperty("bossTitleText").objectReferenceValue = title;
            so.FindProperty("bossSubtitleText").objectReferenceValue = subtitle;
            so.FindProperty("bossHealthNumberText").objectReferenceValue = hpNumber;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConstruirBarraTux(Transform parent, TMP_FontAsset font, Sprite panelSprite, CombatHUDManager hud)
        {
            GameObject tuxGO = ObtenerOCrearHijo(parent, "TuxHealthPlate");

            RectTransform rt = ObtenerOAgregarComponente<RectTransform>(tuxGO);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(30f, -25f);
            rt.sizeDelta = new Vector2(320f, 65f);

            Image panelImg = ObtenerOAgregarComponente<Image>(tuxGO);
            if (panelSprite != null)
            {
                panelImg.sprite = panelSprite;
                panelImg.type = Image.Type.Sliced;
            }
            panelImg.color = new Color(0.14f, 0.22f, 0.18f, 0.94f); // #23372E Verde militar táctico

            // Título
            TMP_Text title = ObtenerOCrearTexto(tuxGO.transform, "TuxStatusTitle", font, 15, FontStyles.Bold, TextAlignmentOptions.Left);
            RectTransform rtTitle = title.rectTransform;
            rtTitle.anchorMin = new Vector2(0f, 1f);
            rtTitle.anchorMax = new Vector2(1f, 1f);
            rtTitle.pivot = new Vector2(0f, 1f);
            rtTitle.anchoredPosition = new Vector2(16f, -6f);
            rtTitle.sizeDelta = new Vector2(-32f, 22f);
            title.text = "TUX_OS [KERNEL OK]";
            title.color = new Color(0.08f, 0.88f, 0.96f); // Cian brillante #14E0F5

            // Contenedor de barra
            GameObject barArea = ObtenerOCrearHijo(tuxGO.transform, "BarArea");
            RectTransform rtBarArea = ObtenerOAgregarComponente<RectTransform>(barArea);
            rtBarArea.anchorMin = new Vector2(0f, 0f);
            rtBarArea.anchorMax = new Vector2(1f, 1f);
            rtBarArea.offsetMin = new Vector2(16f, 12f);
            rtBarArea.offsetMax = new Vector2(-16f, -28f);

            Image barBg = ObtenerOAgregarComponente<Image>(barArea);
            barBg.color = new Color(0.05f, 0.08f, 0.06f, 0.95f);

            // Barra Frontal
            GameObject fillGO = ObtenerOCrearHijo(barArea.transform, "FillBar");
            RectTransform rtFill = ObtenerOAgregarComponente<RectTransform>(fillGO);
            rtFill.anchorMin = Vector2.zero;
            rtFill.anchorMax = Vector2.one;
            rtFill.sizeDelta = Vector2.zero;
            Image fillImg = ObtenerOAgregarComponente<Image>(fillGO);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
            fillImg.color = new Color(0.08f, 0.88f, 0.96f);

            // Número HP
            TMP_Text hpNumber = ObtenerOCrearTexto(tuxGO.transform, "TuxHPNumber", font, 11, FontStyles.Bold, TextAlignmentOptions.Right);
            RectTransform rtHp = hpNumber.rectTransform;
            rtHp.anchorMin = new Vector2(0.5f, 1f);
            rtHp.anchorMax = new Vector2(1f, 1f);
            rtHp.pivot = new Vector2(1f, 1f);
            rtHp.anchoredPosition = new Vector2(-16f, -8f);
            rtHp.sizeDelta = new Vector2(0f, 18f);
            hpNumber.text = "5 / 5 HP";
            hpNumber.color = new Color(0.85f, 1f, 0.95f);

            // Asignar en HUD
            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("tuxBarContainer").objectReferenceValue = tuxGO;
            so.FindProperty("tuxHealthFillImage").objectReferenceValue = fillImg;
            so.FindProperty("tuxStatusTitleText").objectReferenceValue = title;
            so.FindProperty("tuxHealthNumberText").objectReferenceValue = hpNumber;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConstruirTerminalCMD(Transform parent, TMP_FontAsset font, Sprite panelSprite, CombatHUDManager hud)
        {
            GameObject termGO = ObtenerOCrearHijo(parent, "CMDTerminalHUD");

            RectTransform rt = ObtenerOAgregarComponente<RectTransform>(termGO);
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-25f, -25f);
            rt.sizeDelta = new Vector2(460f, 155f);

            Image panelImg = ObtenerOAgregarComponente<Image>(termGO);
            if (panelSprite != null)
            {
                panelImg.sprite = panelSprite;
                panelImg.type = Image.Type.Sliced;
            }
            panelImg.color = new Color(0.04f, 0.06f, 0.05f, 0.88f); // Terminal oscuro #0A0F0D

            // Título Terminal
            TMP_Text title = ObtenerOCrearTexto(termGO.transform, "TerminalTitle", font, 12, FontStyles.Bold, TextAlignmentOptions.Left);
            RectTransform rtTitle = title.rectTransform;
            rtTitle.anchorMin = new Vector2(0f, 1f);
            rtTitle.anchorMax = new Vector2(1f, 1f);
            rtTitle.pivot = new Vector2(0f, 1f);
            rtTitle.anchoredPosition = new Vector2(14f, -6f);
            rtTitle.sizeDelta = new Vector2(-28f, 18f);
            title.text = "[CMD] C:\\Windows\\System32\\cmd.exe";
            title.color = new Color(0.08f, 0.88f, 0.96f);

            // Contenido Terminal
            TMP_Text content = ObtenerOCrearTexto(termGO.transform, "TerminalContent", font, 10, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            RectTransform rtContent = content.rectTransform;
            rtContent.anchorMin = Vector2.zero;
            rtContent.anchorMax = Vector2.one;
            rtContent.offsetMin = new Vector2(14f, 10f);
            rtContent.offsetMax = new Vector2(-14f, -24f);
            content.lineSpacing = -10f;
            content.text = "> Inicializando terminal...";
            content.color = new Color(0.85f, 0.95f, 0.90f);

            // Asignar en HUD
            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("terminalContainer").objectReferenceValue = termGO;
            so.FindProperty("terminalTitleText").objectReferenceValue = title;
            so.FindProperty("terminalContentText").objectReferenceValue = content;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConstruirPanelBSOD(Transform parent, TMP_FontAsset font)
        {
            GameObject bsodGO = ObtenerOCrearHijo(parent, "BSOD_GlitchOverlay");

            RectTransform rt = ObtenerOAgregarComponente<RectTransform>(bsodGO);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            CanvasGroup cg = ObtenerOAgregarComponente<CanvasGroup>(bsodGO);
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            Image fondo = ObtenerOAgregarComponente<Image>(bsodGO);
            fondo.color = new Color(0f, 0.47f, 0.84f, 1f); // #0078D7

            // Carita :(
            TMP_Text face = ObtenerOCrearTexto(bsodGO.transform, "CaraTriste", font, 90, FontStyles.Normal, TextAlignmentOptions.Left);
            RectTransform rtFace = face.rectTransform;
            rtFace.anchorMin = new Vector2(0.12f, 0.65f);
            rtFace.anchorMax = new Vector2(0.35f, 0.85f);
            rtFace.anchoredPosition = Vector2.zero;
            face.text = ":(";
            face.color = Color.white;

            // Mensaje principal
            TMP_Text msg = ObtenerOCrearTexto(bsodGO.transform, "MensajePrincipal", font, 24, FontStyles.Normal, TextAlignmentOptions.Left);
            RectTransform rtMsg = msg.rectTransform;
            rtMsg.anchorMin = new Vector2(0.12f, 0.35f);
            rtMsg.anchorMax = new Vector2(0.88f, 0.65f);
            rtMsg.anchoredPosition = Vector2.zero;
            msg.text = "Tu PC tuvo un problema y necesita reiniciarse.\n" +
                       "Estamos recopilando información sobre el error y luego se reiniciará automáticamente.\n\n" +
                       "100% completado";
            msg.color = Color.white;

            // Código de detención
            TMP_Text stopCode = ObtenerOCrearTexto(bsodGO.transform, "CodigoDetencion", font, 16, FontStyles.Normal, TextAlignmentOptions.Left);
            RectTransform rtStop = stopCode.rectTransform;
            rtStop.anchorMin = new Vector2(0.12f, 0.15f);
            rtStop.anchorMax = new Vector2(0.88f, 0.35f);
            rtStop.anchoredPosition = Vector2.zero;
            stopCode.text = "Código de detención: CRITICAL_PROCESS_DIED\n" +
                            "Lo que falló: win32kfull.sys (Windows Defender Rootkit)";
            stopCode.color = Color.white;

            BSODGlitchEffect effect = ObtenerOAgregarComponente<BSODGlitchEffect>(bsodGO);

            SerializedObject so = new SerializedObject(effect);
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.FindProperty("fondoAzul").objectReferenceValue = fondo;
            so.FindProperty("textoCaraTriste").objectReferenceValue = face;
            so.FindProperty("textoMensajePrincipal").objectReferenceValue = msg;
            so.FindProperty("textoCodigoDetencion").objectReferenceValue = stopCode;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConstruirPanelVictoriaDerrota(Transform parent, TMP_FontAsset font, Sprite buttonSprite, Sprite panelSprite)
        {
            GameObject screenGO = ObtenerOCrearHijo(parent, "VictoryDefeatScreen");

            RectTransform rt = ObtenerOAgregarComponente<RectTransform>(screenGO);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            CanvasGroup cg = ObtenerOAgregarComponente<CanvasGroup>(screenGO);
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // 1. Panel de Victoria
            GameObject vicGO = ObtenerOCrearHijo(screenGO.transform, "VictoryPanel");
            RectTransform rtVic = ObtenerOAgregarComponente<RectTransform>(vicGO);
            rtVic.anchorMin = new Vector2(0.5f, 0.5f);
            rtVic.anchorMax = new Vector2(0.5f, 0.5f);
            rtVic.sizeDelta = new Vector2(680f, 380f);
            Image vicImg = ObtenerOAgregarComponente<Image>(vicGO);
            if (panelSprite != null) { vicImg.sprite = panelSprite; vicImg.type = Image.Type.Sliced; }
            vicImg.color = new Color(0.08f, 0.16f, 0.14f, 0.96f); // Verde militar oscuro

            TMP_Text vicTitle = ObtenerOCrearTexto(vicGO.transform, "VicTitle", font, 28, FontStyles.Bold, TextAlignmentOptions.Center);
            vicTitle.rectTransform.anchoredPosition = new Vector2(0f, 130f);
            vicTitle.rectTransform.sizeDelta = new Vector2(640f, 40f);
            vicTitle.text = "[SUDO KILL -9 EXITOSO]";
            vicTitle.color = new Color(0.08f, 0.95f, 1f);

            TMP_Text vicSub = ObtenerOCrearTexto(vicGO.transform, "VicSub", font, 14, FontStyles.Normal, TextAlignmentOptions.Center);
            vicSub.rectTransform.anchoredPosition = new Vector2(0f, 85f);
            vicSub.rectTransform.sizeDelta = new Vector2(640f, 30f);
            vicSub.text = "PROCESO WINDOWS_11_DEFENDER.EXE TERMINADO FORZOSAMENTE";
            vicSub.color = new Color(0.85f, 0.95f, 0.90f);

            TMP_Text vicStats = ObtenerOCrearTexto(vicGO.transform, "VicStats", font, 13, FontStyles.Normal, TextAlignmentOptions.Center);
            vicStats.rectTransform.anchoredPosition = new Vector2(0f, 15f);
            vicStats.rectTransform.sizeDelta = new Vector2(600f, 80f);
            vicStats.text = "[ESTADO]: PRIVILEGIOS DE ROOT ASEGURADOS\n[SALIDA]: Exit code 0";
            vicStats.color = new Color(0.75f, 0.85f, 0.80f);

            Button vicBtnRestart = CrearBotonTactico(vicGO.transform, "Btn_ReiniciarMision", "REINICIAR MISIÓN", new Vector2(-150f, -120f), font, buttonSprite);
            Button vicBtnMenu = CrearBotonTactico(vicGO.transform, "Btn_MenuPrincipal", "MENÚ PRINCIPAL", new Vector2(150f, -120f), font, buttonSprite);

            // 2. Panel de Derrota
            GameObject defGO = ObtenerOCrearHijo(screenGO.transform, "DefeatPanel");
            RectTransform rtDef = ObtenerOAgregarComponente<RectTransform>(defGO);
            rtDef.anchorMin = new Vector2(0.5f, 0.5f);
            rtDef.anchorMax = new Vector2(0.5f, 0.5f);
            rtDef.sizeDelta = new Vector2(680f, 380f);
            Image defImg = ObtenerOAgregarComponente<Image>(defGO);
            if (panelSprite != null) { defImg.sprite = panelSprite; defImg.type = Image.Type.Sliced; }
            defImg.color = new Color(0.18f, 0.06f, 0.06f, 0.96f); // Rojo carmesí oscuro

            TMP_Text defTitle = ObtenerOCrearTexto(defGO.transform, "DefTitle", font, 28, FontStyles.Bold, TextAlignmentOptions.Center);
            defTitle.rectTransform.anchoredPosition = new Vector2(0f, 130f);
            defTitle.rectTransform.sizeDelta = new Vector2(640f, 40f);
            defTitle.text = "[KERNEL PANIC: SYSTEM CRASH]";
            defTitle.color = new Color(0.95f, 0.22f, 0.18f);

            TMP_Text defSub = ObtenerOCrearTexto(defGO.transform, "DefSub", font, 14, FontStyles.Normal, TextAlignmentOptions.Center);
            defSub.rectTransform.anchoredPosition = new Vector2(0f, 85f);
            defSub.rectTransform.sizeDelta = new Vector2(640f, 30f);
            defSub.text = "FATAL EXCEPTION: MEMORY_MANAGEMENT_VIOLATION";
            defSub.color = new Color(1f, 0.80f, 0.80f);

            TMP_Text defCause = ObtenerOCrearTexto(defGO.transform, "DefCause", font, 13, FontStyles.Normal, TextAlignmentOptions.Center);
            defCause.rectTransform.anchoredPosition = new Vector2(0f, 15f);
            defCause.rectTransform.sizeDelta = new Vector2(600f, 80f);
            defCause.text = "[CAUSA]: El pingüino Linux fue neutralizado por Windows Defender.\n[ACCIÓN]: Reintente la ejecución con permisos elevados.";
            defCause.color = new Color(0.90f, 0.75f, 0.75f);

            Button defBtnRetry = CrearBotonTactico(defGO.transform, "Btn_Reintentar", "REINTENTAR", new Vector2(-150f, -120f), font, buttonSprite);
            Button defBtnMenu = CrearBotonTactico(defGO.transform, "Btn_MenuPrincipal", "MENÚ PRINCIPAL", new Vector2(150f, -120f), font, buttonSprite);

            VictoryDefeatScreen screenComp = ObtenerOAgregarComponente<VictoryDefeatScreen>(screenGO);

            SerializedObject so = new SerializedObject(screenComp);
            so.FindProperty("victoryPanel").objectReferenceValue = vicGO;
            so.FindProperty("defeatPanel").objectReferenceValue = defGO;
            so.FindProperty("screenCanvasGroup").objectReferenceValue = cg;
            so.FindProperty("victoryTitleText").objectReferenceValue = vicTitle;
            so.FindProperty("victorySubtitleText").objectReferenceValue = vicSub;
            so.FindProperty("victoryStatsText").objectReferenceValue = vicStats;
            so.FindProperty("victoryRestartButton").objectReferenceValue = vicBtnRestart;
            so.FindProperty("victoryMenuButton").objectReferenceValue = vicBtnMenu;
            so.FindProperty("defeatTitleText").objectReferenceValue = defTitle;
            so.FindProperty("defeatSubtitleText").objectReferenceValue = defSub;
            so.FindProperty("defeatCauseText").objectReferenceValue = defCause;
            so.FindProperty("defeatRetryButton").objectReferenceValue = defBtnRetry;
            so.FindProperty("defeatMenuButton").objectReferenceValue = defBtnMenu;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button CrearBotonTactico(Transform parent, string nombre, string texto, Vector2 pos, TMP_FontAsset font, Sprite btnSprite)
        {
            GameObject btnGO = ObtenerOCrearHijo(parent, nombre);
            RectTransform rt = ObtenerOAgregarComponente<RectTransform>(btnGO);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(250f, 50f);

            Image img = ObtenerOAgregarComponente<Image>(btnGO);
            if (btnSprite != null)
            {
                img.sprite = btnSprite;
                img.type = Image.Type.Sliced;
            }
            img.color = new Color(0.10f, 0.16f, 0.22f, 0.95f);

            Button btn = ObtenerOAgregarComponente<Button>(btnGO);

            // Efectos tácticos interactivos
            if (btnGO.GetComponent<MenuButtonEffects>() == null)
            {
                btnGO.AddComponent<MenuButtonEffects>();
            }

            TMP_Text label = ObtenerOCrearTexto(btnGO.transform, "Text", font, 14, FontStyles.Bold, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
            label.text = texto;
            label.color = new Color(0.85f, 0.95f, 1f);

            return btn;
        }

        private static GameObject ObtenerOCrearHijo(Transform parent, string nombre)
        {
            Transform t = parent.Find(nombre);
            if (t != null)
            {
                if (t.GetComponent<RectTransform>() != null)
                {
                    return t.gameObject;
                }
                Object.DestroyImmediate(t.gameObject);
            }
            GameObject go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text ObtenerOCrearTexto(Transform parent, string nombre, TMP_FontAsset font, float tamano, FontStyles estilo, TextAlignmentOptions alineacion)
        {
            GameObject go = ObtenerOCrearHijo(parent, nombre);
            TextMeshProUGUI tmp = ObtenerOAgregarComponente<TextMeshProUGUI>(go);
            if (font != null) tmp.font = font;
            tmp.fontSize = tamano;
            tmp.fontStyle = estilo;
            tmp.alignment = alineacion;
            return tmp;
        }

        private static T ObtenerOAgregarComponente<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        private static void EjecutarPruebasVerificacion()
        {
            // Ejecutar la verificación oficial de combate de SetupWindowsBoss
            SetupWindowsBoss.EjecutarVerificacionAutomatica();
        }
    }
}
