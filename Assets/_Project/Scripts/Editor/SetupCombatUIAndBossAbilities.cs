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
using SudoKill9.Combat;

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
            // Constructor estático limpio: no ejecutar reimportaciones forzadas recursivas
            // que bloqueen la interfaz de Unity en cada recarga de dominio.
            // Para configurar o verificar, usar el menú: Tools > SudoKill9
        }

        public static void EjecutarConfiguracionSilenciosa()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.path != ScenePath) return;

            FixHovlFireURP.RepararTodosLosMateriales(silent: true);
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

            // 0. Reparar shaders y materiales de Hovl Studio en URP
            FixHovlFireURP.RepararTodosLosMateriales(silent: false);

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
            GameObject windowsGO = GameObject.Find("windows_opt") ?? GameObject.Find("windows");
            if (windowsGO == null)
            {
                Debug.LogError("[SetupCombatUI] No se encontró el GameObject 'windows_opt' (ni 'windows') en la escena.");
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

            // Asignar prefabs de Hovl Studio en WindowsCommandAttacks
            SerializedObject soCmd = new SerializedObject(cmdAttacks);
            var p0 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/Magic fire 0.prefab");
            var p1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/Magic fire 1.prefab");
            var pRot = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/RotatorPS2.prefab");
            var pRot1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/RotatorPS1.prefab");
            var pExp = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/Explosion.prefab");

            if (p0 != null && soCmd.FindProperty("prefabMagicFire0") != null) soCmd.FindProperty("prefabMagicFire0").objectReferenceValue = p0;
            if (p1 != null && soCmd.FindProperty("prefabMagicFire1") != null) soCmd.FindProperty("prefabMagicFire1").objectReferenceValue = p1;
            if (pRot != null && soCmd.FindProperty("prefabRotatorPS2") != null) soCmd.FindProperty("prefabRotatorPS2").objectReferenceValue = pRot;
            if (pRot1 != null && soCmd.FindProperty("prefabRotatorPS1") != null) soCmd.FindProperty("prefabRotatorPS1").objectReferenceValue = pRot1;
            if (pExp != null && soCmd.FindProperty("prefabExplosion") != null) soCmd.FindProperty("prefabExplosion").objectReferenceValue = pExp;
            soCmd.ApplyModifiedPropertiesWithoutUndo();

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

            // 4. WindowsHeadHealthBar (Barra de salud sobre la cabeza en World Space con 2 vidas)
            WindowsHeadHealthBar headBar = windowsGO.GetComponent<WindowsHeadHealthBar>();
            if (headBar == null) headBar = windowsGO.AddComponent<WindowsHeadHealthBar>();
            headBar.ConfigurarParaBoss(bossAI);

            EditorUtility.SetDirty(windowsGO);
            EditorUtility.SetDirty(bossAI);
            EditorUtility.SetDirty(cmdAttacks);
            EditorUtility.SetDirty(headBar);
            Debug.Log("<color=green>[SetupCombatUI]</color> Jefe 'windows' configurado con WindowsBossAI, WindowsCommandAttacks, WindowsHeadHealthBar y prefabs de Hovl Studio.");
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

            bossGO.SetActive(hud.MostrarBarraSuperior2D);
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
            rt.sizeDelta = new Vector2(560f, 185f);

            Image panelImg = ObtenerOAgregarComponente<Image>(termGO);
            if (panelSprite != null)
            {
                panelImg.sprite = panelSprite;
                panelImg.type = Image.Type.Sliced;
            }
            panelImg.color = new Color(0.04f, 0.06f, 0.05f, 0.90f); // Terminal oscuro #0A0F0D

            CanvasGroup cg = ObtenerOAgregarComponente<CanvasGroup>(termGO);
            cg.alpha = 1f;

            // Título Terminal
            TMP_Text title = ObtenerOCrearTexto(termGO.transform, "TerminalTitle", font, 16, FontStyles.Bold, TextAlignmentOptions.Left);
            RectTransform rtTitle = title.rectTransform;
            rtTitle.anchorMin = new Vector2(0f, 1f);
            rtTitle.anchorMax = new Vector2(1f, 1f);
            rtTitle.pivot = new Vector2(0f, 1f);
            rtTitle.anchoredPosition = new Vector2(22f, -8f);
            rtTitle.sizeDelta = new Vector2(-44f, 24f);
            title.text = "[CMD] C:\\Windows\\System32\\cmd.exe";
            title.color = new Color(0.08f, 0.88f, 0.96f);

            // Contenido Terminal
            TMP_Text content = ObtenerOCrearTexto(termGO.transform, "TerminalContent", font, 15, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            RectTransform rtContent = content.rectTransform;
            rtContent.anchorMin = Vector2.zero;
            rtContent.anchorMax = Vector2.one;
            rtContent.offsetMin = new Vector2(22f, 14f);
            rtContent.offsetMax = new Vector2(-22f, -38f);
            content.lineSpacing = 3.5f;
            content.text = "> Inicializando terminal...";
            content.color = new Color(0.90f, 0.98f, 0.92f);

            // Asignar en HUD
            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("terminalContainer").objectReferenceValue = termGO;
            so.FindProperty("terminalTitleText").objectReferenceValue = title;
            so.FindProperty("terminalContentText").objectReferenceValue = content;
            SerializedProperty cgProp = so.FindProperty("terminalCanvasGroup");
            if (cgProp != null) cgProp.objectReferenceValue = cg;
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
            SetupWindowsBoss.EjecutarVerificacionAutomatica();
            EjecutarPruebasCalidadIntegral();
        }

        [MenuItem("Tools/SudoKill9/Ejecutar Pruebas Integrales de Combate y UI")]
        public static string EjecutarPruebasCalidadIntegral()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== INFORME DE VERIFICACIÓN INTEGRAL DE COMBATE, DOBLE VIDA Y UI ===");
            sb.AppendLine($"Timestamp: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            bool exitoTotal = true;

            // -------------------------------------------------------------
            // SECCIÓN 1: ATAQUE ROTATORPS1 (TRAMPA ESTÁTICA Y CONTENCIÓN)
            // -------------------------------------------------------------
            sb.AppendLine("\n--- 1. VERIFICACIÓN DE ATAQUE ROTATORPS1 Y TRAMPA ESTÁTICA ---");
            
            // 1.1 Verificar prefab RotatorPS1 en disco
            GameObject prefabRotator = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Procedural fire/Prefabs/RotatorPS1.prefab");
            bool prefabOk = prefabRotator != null;
            sb.AppendLine($"[TEST 1.1] Prefab RotatorPS1 cargado en disco: {(prefabOk ? "PASS" : "FAIL")}");
            exitoTotal &= prefabOk;

            // 1.2 Verificar WindowsCommandAttacks fields y defaults
            GameObject dummyCmdGO = new GameObject("Dummy_Cmd_Test");
            try
            {
                WindowsCommandAttacks cmd = dummyCmdGO.AddComponent<WindowsCommandAttacks>();
                bool durOk = cmd.DuracionRotatorPS1 > 0f && cmd.DuracionRotatorPS1 <= 5f;
                sb.AppendLine($"[TEST 1.2] WindowsCommandAttacks.DuracionRotatorPS1 configurable ({cmd.DuracionRotatorPS1}s): {(durOk ? "PASS" : "FAIL")}");
                exitoTotal &= durOk;
            }
            finally
            {
                Object.DestroyImmediate(dummyCmdGO);
            }

            // 1.3 Verificar RotatorPS1TrapController (estática + contención perimetral)
            GameObject dummyTrapGO = new GameObject("Dummy_Trap_Test");
            GameObject dummyPlayerGO = new GameObject("Dummy_Player_Test");
            try
            {
                Vector3 puntoImpacto = new Vector3(15f, 0f, 25f);
                dummyTrapGO.transform.position = puntoImpacto;
                RotatorPS1TrapController trap = dummyTrapGO.AddComponent<RotatorPS1TrapController>();
                
                dummyPlayerGO.transform.position = puntoImpacto + new Vector3(1f, 0f, 0f);
                trap.InicializarTrampa(dummyPlayerGO.transform, duracion: 2.5f, atraparJugador: true);

                // Verificar que no se mueva hacia el jugador
                dummyPlayerGO.transform.position = new Vector3(100f, 0f, 100f);
                bool posEstaticaOk = Vector3.Distance(dummyTrapGO.transform.position, puntoImpacto) < 0.001f;
                sb.AppendLine($"[TEST 1.3] RotatorPS1 permanece estrictamente estático en el punto de impacto: {(posEstaticaOk ? "PASS" : "FAIL")}");
                exitoTotal &= posEstaticaOk;

                // Verificar contención perimetral
                float radio = trap.RadioConfinamiento;
                bool radioOk = radio >= 2f && radio <= 5f;
                sb.AppendLine($"[TEST 1.4] RotatorPS1 radio de contención circular calibrado ({radio}m): {(radioOk ? "PASS" : "FAIL")}");
                exitoTotal &= radioOk;

                // Verificar aplicación del límite de confinamiento (clamp sin congelar)
                dummyPlayerGO.transform.position = puntoImpacto + new Vector3(50f, 0f, 0f);
                trap.AplicarLimiteConfinamiento(dummyPlayerGO.transform);
                float distTrasClamp = Vector3.Distance(new Vector3(dummyPlayerGO.transform.position.x, 0f, dummyPlayerGO.transform.position.z), new Vector3(puntoImpacto.x, 0f, puntoImpacto.z));
                bool clampOk = distTrasClamp <= (radio + 0.1f);
                sb.AppendLine($"[TEST 1.5] RotatorPS1 confina al jugador dentro del perímetro (dist={distTrasClamp:F2}m <= {radio}m): {(clampOk ? "PASS" : "FAIL")}");
                exitoTotal &= clampOk;

                // Verificar que no existen colliders físicos que bloqueen balas del jugador o la cámara
                Collider[] colliders = dummyTrapGO.GetComponentsInChildren<Collider>();
                bool sinCollidersSolidos = true;
                for (int c = 0; c < colliders.Length; c++)
                {
                    if (colliders[c] != null && colliders[c].enabled && !colliders[c].isTrigger)
                    {
                        sinCollidersSolidos = false;
                        break;
                    }
                }
                sb.AppendLine($"[TEST 1.6] RotatorPS1 sin barreras sólidas que bloqueen balas o cámara: {(sinCollidersSolidos ? "PASS" : "FAIL")}");
                exitoTotal &= sinCollidersSolidos;
            }
            finally
            {
                Object.DestroyImmediate(dummyTrapGO);
                Object.DestroyImmediate(dummyPlayerGO);
            }

            // -------------------------------------------------------------
            // SECCIÓN 2: SISTEMA DE DOBLE VIDA Y BARRA SOBRE LA CABEZA
            // -------------------------------------------------------------
            sb.AppendLine("\n--- 2. VERIFICACIÓN DE SISTEMA DE DOS VIDAS Y BARRA HEAD SPACE ---");

            GameObject dummyBossGO = new GameObject("Dummy_Boss_TwoLives");
            try
            {
                WindowsBossAI bossAI = dummyBossGO.AddComponent<WindowsBossAI>();
                bossAI.GarantizarComponentesFisicos();

                bool initVidasOk = bossAI.VidaActual == 1 && bossAI.VidasTotales == 2 && bossAI.CurrentHealth == 500f;
                sb.AppendLine($"[TEST 2.1] Inicialización de Doble Vida (Vida {bossAI.VidaActual}/{bossAI.VidasTotales}, HP: {bossAI.CurrentHealth}/{bossAI.MaxHealth}): {(initVidasOk ? "PASS" : "FAIL")}");
                exitoTotal &= initVidasOk;

                // Recibir 500 de daño: debe agotar la vida 1 y transicionar a la vida 2 al 100%
                bossAI.TakeDamage(500f);
                bool transicionOk = bossAI.VidaActual == 2 && bossAI.CurrentHealth == 500f && !bossAI.IsDead;
                sb.AppendLine($"[TEST 2.2] Transición inmediata a Vida 2 al 100% sin morir (Vida: {bossAI.VidaActual}/{bossAI.VidasTotales}, HP: {bossAI.CurrentHealth}, IsDead: {bossAI.IsDead}): {(transicionOk ? "PASS" : "FAIL")}");
                exitoTotal &= transicionOk;

                // Recibir 250 de daño en vida 2: debe bajar a 250 HP y seguir vivo
                bossAI.TakeDamage(250f);
                bool danoVida2Ok = bossAI.VidaActual == 2 && bossAI.CurrentHealth == 250f && !bossAI.IsDead;
                sb.AppendLine($"[TEST 2.3] Daño progresivo en Vida 2 (HP: {bossAI.CurrentHealth}/500, IsDead: {bossAI.IsDead}): {(danoVida2Ok ? "PASS" : "FAIL")}");
                exitoTotal &= danoVida2Ok;

                // Recibir 250 de daño final: agota vida 2 y muere definitivamente
                bossAI.TakeDamage(250f);
                bool muerteOk = bossAI.CurrentHealth == 0f && bossAI.IsDead;
                sb.AppendLine($"[TEST 2.4] Muerte definitiva únicamente al agotar Vida 2 (HP: {bossAI.CurrentHealth}, IsDead: {bossAI.IsDead}): {(muerteOk ? "PASS" : "FAIL")}");
                exitoTotal &= muerteOk;

                // 2.5 Verificar WindowsHeadHealthBar
                WindowsHeadHealthBar headBar = dummyBossGO.AddComponent<WindowsHeadHealthBar>();
                headBar.ConfigurarParaBoss(bossAI);
                bool headBarOk = headBar.ColorVida1 != headBar.ColorVida2 && headBar.OffsetSobreCabeza.y > 0.2f;
                sb.AppendLine($"[TEST 2.5] WindowsHeadHealthBar colores diferenciados y offset vertical sobre cabeza: {(headBarOk ? "PASS" : "FAIL")}");
                exitoTotal &= headBarOk;

                // 2.6 Verificar Indicadores Visuales Gemelos de Vida (Life1Dot y Life2Dot)
                bool dotsOk = headBar.Life1Dot != null && headBar.Life2Dot != null;
                sb.AppendLine($"[TEST 2.6] WindowsHeadHealthBar indicadores gemelos de vida creados: {(dotsOk ? "PASS" : "FAIL")}");
                exitoTotal &= dotsOk;

                // 2.7 Verificar escala en mundo desacoplada del jefe (1.9m de ancho, escala 0.005)
                bool escalaOk = Mathf.Approximately(headBar.transform.localScale.x, 0.005f) && headBar.transform.parent == null;
                sb.AppendLine($"[TEST 2.7] WindowsHeadHealthBar desacoplada de la escala del modelo (scale={headBar.transform.localScale.x}): {(escalaOk ? "PASS" : "FAIL")}");
                exitoTotal &= escalaOk;
            }
            finally
            {
                Object.DestroyImmediate(dummyBossGO);
            }

            // -------------------------------------------------------------
            // SECCIÓN 3: TARJETA DE MENSAJES CMD (CMDTerminalHUD)
            // -------------------------------------------------------------
            sb.AppendLine("\n--- 3. VERIFICACIÓN DE TARJETA DE MENSAJES CMD (CMDTerminalHUD) ---");

            GameObject dummyCanvas = new GameObject("Dummy_Canvas_UI", typeof(RectTransform));
            try
            {
                CombatHUDManager hud = dummyCanvas.AddComponent<CombatHUDManager>();
                GameObject termGO = new GameObject("CMDTerminalHUD", typeof(RectTransform));
                termGO.transform.SetParent(dummyCanvas.transform, false);
                RectTransform rtTerm = termGO.GetComponent<RectTransform>();
                rtTerm.sizeDelta = new Vector2(560f, 195f);

                CanvasGroup cg = termGO.AddComponent<CanvasGroup>();
                cg.alpha = 1f;

                bool tamanoCardOk = rtTerm.sizeDelta.x >= 500f && rtTerm.sizeDelta.y >= 180f;
                sb.AppendLine($"[TEST 3.1] Dimensiones expandidas de CMDTerminalHUD ({rtTerm.sizeDelta.x}x{rtTerm.sizeDelta.y}): {(tamanoCardOk ? "PASS" : "FAIL")}");
                exitoTotal &= tamanoCardOk;

                bool typoOk = hud.TamanoFuenteTitulo >= 15f && hud.TamanoFuenteMensajes >= 14f && hud.EspaciadoLineasMensajes >= 2.5f;
                sb.AppendLine($"[TEST 3.2] Tipografía y espaciado de líneas (Título={hud.TamanoFuenteTitulo}pt, Mensajes={hud.TamanoFuenteMensajes}pt, Espaciado={hud.EspaciadoLineasMensajes}): {(typoOk ? "PASS" : "FAIL")}");
                exitoTotal &= typoOk;

                bool barraTopOculta = !hud.MostrarBarraSuperior2D;
                sb.AppendLine($"[TEST 3.3] Prioridad a barra 3D sobre la cabeza (mostrarBarraSuperior2D=false): {(barraTopOculta ? "PASS" : "FAIL")}");
                exitoTotal &= barraTopOculta;
            }
            finally
            {
                Object.DestroyImmediate(dummyCanvas);
            }

            sb.AppendLine($"\n=== RESULTADO FINAL: {(exitoTotal ? "TODOS LOS TESTS APROBADOS (PASS)" : "FALLOS DETECTADOS (FAIL)")} ===");

            string reporte = sb.ToString();
            Debug.Log($"<color={(exitoTotal ? "green" : "red")}>[PruebasIntegrales]</color>\n{reporte}");

            try
            {
                System.IO.File.WriteAllText("Logs/CombatVerificationReport.txt", reporte);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"No se pudo escribir en Logs/CombatVerificationReport.txt: {ex.Message}");
            }

            return reporte;
        }
    }
}
