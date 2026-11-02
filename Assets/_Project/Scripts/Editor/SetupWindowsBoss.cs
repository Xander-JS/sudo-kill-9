using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Herramienta de configuración y 1-Click para el Animator Maestro y la IA de Windows:
    /// Implementa con exactitud el diagrama de flujo diseñado por el usuario:
    /// Entry -> 'Se prepara para atacar'
    ///       -> Según distancia se acerca: 'Trota', 'Camina', 'Rum'
    ///       -> Según distancia ataca: 'Ataque_Patada', 'Salto', 'Tornado', 'Patada'
    ///       -> Regresa a 'Se prepara para atacar' para reiniciar el ciclo
    /// </summary>
    [InitializeOnLoad]
    public static class SetupWindowsBoss
    {
        private const string ClipsDir = "Assets/_Project/Animations/Clips/Animacion_Boss";
        private const string ControllerPath = ClipsDir + "/WindowsBoss_MasterController.controller";

        static SetupWindowsBoss()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                WireLinuxCameraPivotIfOpen(silent: true);
            };
        }

        [MenuItem("Tools/Setup Third-Person Camera Pivot (Linux)")]
        public static void RunCameraPivotSetupFromMenu()
        {
            WireLinuxCameraPivotIfOpen(silent: false);
        }

        public static void WireLinuxCameraPivotIfOpen(bool silent = false)
        {
            var combatIK = Object.FindFirstObjectByType<LinuxCombatIK>();
            if (combatIK == null) return;

            bool needsSetup = combatIK.pivotCamara == null ||
                              combatIK.camaraPrincipal == null ||
                              combatIK.camaraPrincipal.parent != combatIK.pivotCamara ||
                              combatIK.LeftHandTarget == null ||
                              combatIK.LeftHandTarget == combatIK.Weapon;

            if (needsSetup || !silent)
            {
                combatIK.ConfigurarSistemaCamara();
                combatIK.ValidarYRepararReferenciasArmaIK();
                combatIK.ApplyOffsets();

                var returnToMenu = Object.FindFirstObjectByType<ReturnToMenu>();
                if (returnToMenu != null && returnToMenu.enabled)
                {
                    returnToMenu.enabled = false;
                    EditorUtility.SetDirty(returnToMenu);
                }

                EditorUtility.SetDirty(combatIK);
                if (combatIK.pivotCamara != null) EditorUtility.SetDirty(combatIK.pivotCamara.gameObject);
                if (combatIK.camaraPrincipal != null) EditorUtility.SetDirty(combatIK.camaraPrincipal.gameObject);
                if (combatIK.LeftHandTarget != null) EditorUtility.SetDirty(combatIK.LeftHandTarget.gameObject);
                EditorSceneManager.MarkSceneDirty(combatIK.gameObject.scene);

                if (!silent)
                {
                    combatIK.EjecutarPruebasCalidadCamara();
                    Debug.Log("<color=green>[CameraPivot Setup]</color> Jerarquía PlayerRoot -> CameraPivot -> MainCamera y referencias de arma/IK configuradas y verificadas en LinuxCombatIK.");
                }
            }
        }

        [MenuItem("Tools/Setup Windows Boss & Master Animator")]
        public static void RunSetupFromMenu()
        {
            AnimatorController ctrl = ConfigureMasterController(silent: false, forceRebuild: true);
            WireSceneIfOpen(silent: false, ctrl);
            WireLinuxCameraPivotIfOpen(silent: false);
            if (ctrl != null)
            {
                Selection.activeObject = ctrl;
            }
        }

        public static AnimatorController ConfigureMasterController(bool silent = false, bool forceRebuild = false)
        {
            if (!AssetDatabase.IsValidFolder(ClipsDir))
            {
                if (!silent) Debug.LogError($"[SetupWindowsBoss] No existe la carpeta {ClipsDir}");
                return null;
            }

            // 1. Cargar Clips exactos del diagrama
            AnimationClip prepClip         = LoadBossClip(ClipsDir + "/Se prepara para atacar.fbx", true);
            AnimationClip walkClip         = LoadBossClip(ClipsDir + "/Camina.fbx", true);
            AnimationClip trotClip         = LoadBossClip(ClipsDir + "/trota.fbx", true);
            AnimationClip runClip          = LoadBossClip(ClipsDir + "/Rum.fbx", true);

            AnimationClip atkPatadaClip    = LoadBossClip(ClipsDir + "/Ataque_Patada.fbx", false);
            AnimationClip saltoClip        = LoadBossClip(ClipsDir + "/Salto.fbx", false);
            AnimationClip tornadoClip      = LoadBossClip(ClipsDir + "/Tornado.fbx", false);
            AnimationClip patadaClip       = LoadBossClip(ClipsDir + "/Patada.fbx", false);

            AnimationClip hitClip          = LoadBossClip(ClipsDir + "/Se_Cae.fbx", false);
            AnimationClip dieClip          = LoadBossClip(ClipsDir + "/Muerte_1.fbx", false);

            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
            {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                forceRebuild = true;
            }

            // Si ya tiene los 10 estados del diagrama y no se fuerza reconstrucción, salir limpiamente
            if (!forceRebuild && ctrl.layers.Length > 0 && ctrl.layers[0].stateMachine.states.Length == 10)
            {
                if (!silent)
                {
                    Debug.Log("<color=green>[SetupWindowsBoss]</color> ¡WindowsBoss_MasterController verificado según el diagrama!");
                }
                return ctrl;
            }

            // Limpiar parámetros y estados previos
            for (int i = ctrl.parameters.Length - 1; i >= 0; i--)
            {
                ctrl.RemoveParameter(i);
            }

            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            foreach (ChildAnimatorState s in sm.states)
            {
                sm.RemoveState(s.state);
            }
            foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            {
                sm.RemoveAnyStateTransition(t);
            }

            // Parámetros principales
            ctrl.AddParameter("Distancia_Pinguino", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Tiempo_Animacion", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            // ========================================================================= //
            // ESTADOS (Ubicaciones visuales que reproducen el dibujo del usuario)        //
            // ========================================================================= //

            // 1. Inicio / Preparación (arriba al centro)
            AnimatorState sPrep = sm.AddState("Se prepara para atacar", new Vector3(400f, 50f, 0f));
            sPrep.motion = prepClip;
            sm.defaultState = sPrep;

            // 2. Locomoción (fila del medio)
            AnimatorState sTrota = sm.AddState("Trota", new Vector3(180f, 220f, 0f));
            sTrota.motion = trotClip;

            AnimatorState sCamina = sm.AddState("Camina", new Vector3(400f, 220f, 0f));
            sCamina.motion = walkClip;

            AnimatorState sRum = sm.AddState("Rum", new Vector3(620f, 220f, 0f));
            sRum.motion = runClip;

            // 3. Ataques (fila inferior)
            AnimatorState sAtaquePatada = sm.AddState("Ataque_Patada", new Vector3(100f, 420f, 0f));
            sAtaquePatada.motion = atkPatadaClip;

            AnimatorState sSalto = sm.AddState("Salto", new Vector3(300f, 420f, 0f));
            sSalto.motion = saltoClip;

            AnimatorState sTornado = sm.AddState("Tornado", new Vector3(500f, 420f, 0f));
            sTornado.motion = tornadoClip;

            AnimatorState sPatada = sm.AddState("Patada", new Vector3(700f, 420f, 0f));
            sPatada.motion = patadaClip;

            // 4. Daño y Muerte (a un lateral)
            AnimatorState sHit = sm.AddState("Hit", new Vector3(880f, 100f, 0f));
            sHit.motion = hitClip;

            AnimatorState sDie = sm.AddState("Die", new Vector3(880f, 220f, 0f));
            sDie.motion = dieClip;

            // ========================================================================= //
            // ROMBO 1: Dependiendo de la distancia se acerca de una manera diferente    //
            // ========================================================================= //

            // Si durante la preparación el pingüino está ya en rango cuerpo a cuerpo (< 2m), ataca directo
            var prepToPatadaDirect = sPrep.AddTransition(sPatada);
            prepToPatadaDirect.hasExitTime = false;
            prepToPatadaDirect.duration = 0.15f;
            prepToPatadaDirect.AddCondition(AnimatorConditionMode.Less, 2.0f, "Distancia_Pinguino");

            // Si está cerca (<= 3.5m) -> Camina
            var prepToCamina = sPrep.AddTransition(sCamina);
            prepToCamina.hasExitTime = false;
            prepToCamina.duration = 0.2f;
            prepToCamina.AddCondition(AnimatorConditionMode.Less, 3.5f, "Distancia_Pinguino");

            // Si está a distancia media (3.5m a 7.0m) -> Trota
            var prepToTrota = sPrep.AddTransition(sTrota);
            prepToTrota.hasExitTime = false;
            prepToTrota.duration = 0.2f;
            prepToTrota.AddCondition(AnimatorConditionMode.Greater, 3.5f, "Distancia_Pinguino");
            prepToTrota.AddCondition(AnimatorConditionMode.Less, 7.0f, "Distancia_Pinguino");

            // Si está lejos (> 7.0m) -> Rum
            var prepToRum = sPrep.AddTransition(sRum);
            prepToRum.hasExitTime = false;
            prepToRum.duration = 0.2f;
            prepToRum.AddCondition(AnimatorConditionMode.Greater, 7.0f, "Distancia_Pinguino");

            // Transiciones adaptativas entre marchas si el pingüino se aleja o acerca
            var caminaToTrota = sCamina.AddTransition(sTrota);
            caminaToTrota.hasExitTime = false;
            caminaToTrota.duration = 0.25f;
            caminaToTrota.AddCondition(AnimatorConditionMode.Greater, 4.0f, "Distancia_Pinguino");

            var trotaToCamina = sTrota.AddTransition(sCamina);
            trotaToCamina.hasExitTime = false;
            trotaToCamina.duration = 0.25f;
            trotaToCamina.AddCondition(AnimatorConditionMode.Less, 3.2f, "Distancia_Pinguino");

            var trotaToRum = sTrota.AddTransition(sRum);
            trotaToRum.hasExitTime = false;
            trotaToRum.duration = 0.25f;
            trotaToRum.AddCondition(AnimatorConditionMode.Greater, 7.5f, "Distancia_Pinguino");

            var rumToTrota = sRum.AddTransition(sTrota);
            rumToTrota.hasExitTime = false;
            rumToTrota.duration = 0.25f;
            rumToTrota.AddCondition(AnimatorConditionMode.Less, 6.5f, "Distancia_Pinguino");

            // ========================================================================= //
            // ROMBO 2: Dependiendo de la distancia ataca así                             //
            // ========================================================================= //

            // 1. PATADA (Cuerpo a cuerpo <= 2.0m)
            var caminaToPatada = sCamina.AddTransition(sPatada);
            caminaToPatada.hasExitTime = false;
            caminaToPatada.duration = 0.15f;
            caminaToPatada.AddCondition(AnimatorConditionMode.Less, 2.0f, "Distancia_Pinguino");

            var trotaToPatada = sTrota.AddTransition(sPatada);
            trotaToPatada.hasExitTime = false;
            trotaToPatada.duration = 0.15f;
            trotaToPatada.AddCondition(AnimatorConditionMode.Less, 2.0f, "Distancia_Pinguino");

            var rumToPatada = sRum.AddTransition(sPatada);
            rumToPatada.hasExitTime = false;
            rumToPatada.duration = 0.15f;
            rumToPatada.AddCondition(AnimatorConditionMode.Less, 2.0f, "Distancia_Pinguino");

            // 2. ATAQUE_PATADA (Rango corto-medio: 2.0m a 3.8m cuando Tiempo_Animacion <= 0.1)
            var caminaToAtkPatada = sCamina.AddTransition(sAtaquePatada);
            caminaToAtkPatada.hasExitTime = false;
            caminaToAtkPatada.duration = 0.15f;
            caminaToAtkPatada.AddCondition(AnimatorConditionMode.Less, 0.1f, "Tiempo_Animacion");
            caminaToAtkPatada.AddCondition(AnimatorConditionMode.Greater, 2.0f, "Distancia_Pinguino");
            caminaToAtkPatada.AddCondition(AnimatorConditionMode.Less, 3.8f, "Distancia_Pinguino");

            var trotaToAtkPatada = sTrota.AddTransition(sAtaquePatada);
            trotaToAtkPatada.hasExitTime = false;
            trotaToAtkPatada.duration = 0.15f;
            trotaToAtkPatada.AddCondition(AnimatorConditionMode.Less, 0.1f, "Tiempo_Animacion");
            trotaToAtkPatada.AddCondition(AnimatorConditionMode.Greater, 2.0f, "Distancia_Pinguino");
            trotaToAtkPatada.AddCondition(AnimatorConditionMode.Less, 3.8f, "Distancia_Pinguino");

            // 3. TORNADO (Rango medio: 3.8m a 6.5m cuando Tiempo_Animacion <= 0.1)
            var trotaToTornado = sTrota.AddTransition(sTornado);
            trotaToTornado.hasExitTime = false;
            trotaToTornado.duration = 0.2f;
            trotaToTornado.AddCondition(AnimatorConditionMode.Less, 0.1f, "Tiempo_Animacion");
            trotaToTornado.AddCondition(AnimatorConditionMode.Greater, 3.8f, "Distancia_Pinguino");
            trotaToTornado.AddCondition(AnimatorConditionMode.Less, 6.5f, "Distancia_Pinguino");

            var rumToTornado = sRum.AddTransition(sTornado);
            rumToTornado.hasExitTime = false;
            rumToTornado.duration = 0.2f;
            rumToTornado.AddCondition(AnimatorConditionMode.Less, 0.1f, "Tiempo_Animacion");
            rumToTornado.AddCondition(AnimatorConditionMode.Greater, 3.8f, "Distancia_Pinguino");
            rumToTornado.AddCondition(AnimatorConditionMode.Less, 6.5f, "Distancia_Pinguino");

            // 4. SALTO (Rango largo: > 6.5m cuando Tiempo_Animacion <= 0.1)
            var rumToSalto = sRum.AddTransition(sSalto);
            rumToSalto.hasExitTime = false;
            rumToSalto.duration = 0.2f;
            rumToSalto.AddCondition(AnimatorConditionMode.Less, 0.1f, "Tiempo_Animacion");
            rumToSalto.AddCondition(AnimatorConditionMode.Greater, 6.5f, "Distancia_Pinguino");

            var trotaToSalto = sTrota.AddTransition(sSalto);
            trotaToSalto.hasExitTime = false;
            trotaToSalto.duration = 0.2f;
            trotaToSalto.AddCondition(AnimatorConditionMode.Less, 0.1f, "Tiempo_Animacion");
            trotaToSalto.AddCondition(AnimatorConditionMode.Greater, 6.5f, "Distancia_Pinguino");

            // ========================================================================= //
            // RETORNO: Tras ejecutar el ataque, regresar a 'Se prepara para atacar'     //
            // ========================================================================= //

            var atkPatadaToPrep = sAtaquePatada.AddTransition(sPrep);
            atkPatadaToPrep.hasExitTime = true;
            atkPatadaToPrep.exitTime = 0.88f;
            atkPatadaToPrep.duration = 0.25f;

            var saltoToPrep = sSalto.AddTransition(sPrep);
            saltoToPrep.hasExitTime = true;
            saltoToPrep.exitTime = 0.88f;
            saltoToPrep.duration = 0.25f;

            var tornadoToPrep = sTornado.AddTransition(sPrep);
            tornadoToPrep.hasExitTime = true;
            tornadoToPrep.exitTime = 0.88f;
            tornadoToPrep.duration = 0.25f;

            var patadaToPrep = sPatada.AddTransition(sPrep);
            patadaToPrep.hasExitTime = true;
            patadaToPrep.exitTime = 0.88f;
            patadaToPrep.duration = 0.25f;

            // ========================================================================= //
            // DAÑO Y MUERTE                                                             //
            // ========================================================================= //

            var anyToHit = sm.AddAnyStateTransition(sHit);
            anyToHit.hasExitTime = false;
            anyToHit.duration = 0.1f;
            anyToHit.canTransitionToSelf = false;
            anyToHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");

            var hitToPrep = sHit.AddTransition(sPrep);
            hitToPrep.hasExitTime = true;
            hitToPrep.exitTime = 0.85f;
            hitToPrep.duration = 0.2f;

            var anyToDie = sm.AddAnyStateTransition(sDie);
            anyToDie.hasExitTime = false;
            anyToDie.duration = 0.15f;
            anyToDie.canTransitionToSelf = false;
            anyToDie.AddCondition(AnimatorConditionMode.If, 0f, "Die");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            if (!silent)
            {
                Debug.Log("<color=green>[SetupWindowsBoss]</color> ¡WindowsBoss_MasterController configurado con exactitud según el diagrama del usuario!");
            }

            return ctrl;
        }

        private static void WireSceneIfOpen(bool silent, AnimatorController ctrl = null)
        {
            GameObject windowsGO = GameObject.Find("windows");
            if (windowsGO == null) return;

            if (ctrl == null)
            {
                ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            }
            if (ctrl == null) return;

            bool changed = false;

            Animator anim = windowsGO.GetComponent<Animator>();
            if (anim == null) anim = windowsGO.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                if (anim.runtimeAnimatorController != ctrl)
                {
                    anim.runtimeAnimatorController = ctrl;
                    changed = true;
                }
                if (anim.applyRootMotion)
                {
                    anim.applyRootMotion = false;
                    changed = true;
                }
            }

            WindowsBossAI ai = windowsGO.GetComponent<WindowsBossAI>();
            if (ai == null)
            {
                ai = windowsGO.AddComponent<WindowsBossAI>();
                changed = true;
            }

            // Buscar prioritariamente al jugador activo con LinuxCombatIK, luego alternativas
            var combatIK = Object.FindFirstObjectByType<LinuxCombatIK>();
            GameObject playerGO = combatIK != null ? combatIK.gameObject : GameObject.Find("linux");
            if (playerGO == null) playerGO = GameObject.Find("Player_LinuxSkeleton");
            if (playerGO == null) playerGO = GameObject.Find("Player_Linux");
            if (playerGO == null) playerGO = GameObject.Find("linux-skeleton");
            if (playerGO == null)
            {
                var skeleton = Object.FindFirstObjectByType<LinuxSkeletonController>();
                if (skeleton != null) playerGO = skeleton.gameObject;
            }

            SerializedObject so = new SerializedObject(ai);
            SerializedProperty targetProp = so.FindProperty("target");
            SerializedProperty animProp = so.FindProperty("animator");

            if (playerGO != null && targetProp != null && targetProp.objectReferenceValue != playerGO.transform)
            {
                targetProp.objectReferenceValue = playerGO.transform;
                changed = true;
            }

            if (anim != null && animProp != null && animProp.objectReferenceValue != anim)
            {
                animProp.objectReferenceValue = anim;
                changed = true;
            }

            SerializedProperty moverProp = so.FindProperty("moverHaciaObjetivo");
            if (moverProp != null && !moverProp.boolValue)
            {
                moverProp.boolValue = true;
                changed = true;
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(windowsGO);
                EditorUtility.SetDirty(ai);
                EditorSceneManager.MarkSceneDirty(windowsGO.scene);
                if (!silent)
                {
                    Debug.Log($"<color=green>[SetupWindowsBoss]</color> GameObject 'windows' configurado con WindowsBoss_MasterController y apuntando a '{playerGO?.name}'.");
                }
            }
        }

        private static AnimationClip LoadBossClip(string fbxPath, bool loop)
        {
            EnsureHumanoidAndLoop(fbxPath, loop);
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    return c;
                }
            }
            return null;
        }

        private static void EnsureHumanoidAndLoop(string fbxPath, bool loop)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            bool needsReimport = false;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                needsReimport = true;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips != null && clips.Length > 0)
            {
                foreach (ModelImporterClipAnimation c in clips)
                {
                    if (c.loopTime != loop || !c.keepOriginalPositionY)
                    {
                        c.loopTime = loop;
                        c.keepOriginalOrientation = true;
                        c.keepOriginalPositionY = true;
                        c.keepOriginalPositionXZ = true;
                        needsReimport = true;
                    }
                }
                if (needsReimport)
                {
                    importer.clipAnimations = clips;
                }
            }

            if (needsReimport)
            {
                importer.SaveAndReimport();
            }
        }
    }
}
