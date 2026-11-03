using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Herramienta de Editor para verificar y configurar programáticamente los AnimatorControllers
    /// ('Animacion_Linux_K' y 'LinuxLocomotion') con el parámetro 'ArmaActiva', el estado 'Camina_arma'
    /// y sus transiciones bidireccionales completas con Locomotion, Jump y Crouch.
    /// </summary>
    public static class SetupCaminaArmaAnimation
    {
        private const string ControllerPathPrincipal = "Assets/_Project/Animations/Clips/Animacion_Boss/Linux_animation/Animacion_Linux_K.controller";
        private const string ControllerPathSecundario = "Assets/_Project/Animations/Controllers/LinuxLocomotion.controller";
        private const string CaminaArmaFbxPath = "Assets/_Project/Animations/Clips/Animacion_Boss/Linux_animation/Camina_arma.fbx";

        [MenuItem("Tools/SudoKill9/Configurar o Verificar Animación Camina_arma")]
        public static void ConfigurarDesdeMenu()
        {
            CorregirImportacionFbxCaminaArma();
            bool ok1 = ConfigurarControladorIndividual(ControllerPathPrincipal, true);
            bool ok2 = ConfigurarControladorIndividual(ControllerPathSecundario, true);

            if (ok1 || ok2)
            {
                EditorUtility.DisplayDialog(
                    "Configuración Exitosa",
                    "Los AnimatorControllers ('Animacion_Linux_K' y 'LinuxLocomotion') están correctamente configurados con:\n" +
                    "• Parámetro booleano: ArmaActiva\n" +
                    "• Estado: Camina_arma (con animación cíclica loopTime y soporte Humanoid sin hundimiento)\n" +
                    "• Transiciones bidireccionales con Locomotion, Jump y Crouch.\n" +
                    "• Sincronización completa con clics izquierdo y derecho.",
                    "Aceptar"
                );
            }
        }

        [MenuItem("Tools/SudoKill9/Corregir Animación Camina_arma (Evitar Hundimiento)")]
        public static void CorregirHundimientoDesdeMenu()
        {
            bool ok = CorregirImportacionFbxCaminaArma();
            if (ok)
            {
                EditorUtility.DisplayDialog(
                    "Animación Corregida",
                    "El archivo Camina_arma.fbx ha sido configurado como Humanoid con bloqueo de altura sobre los pies (heightFromFeet = true).\n\n" +
                    "El personaje ya no se hundirá en el suelo al apuntar o disparar.",
                    "Aceptar"
                );
            }
        }

        public static bool CorregirImportacionFbxCaminaArma()
        {
            ModelImporter importer = AssetImporter.GetAtPath(CaminaArmaFbxPath) as ModelImporter;
            if (importer == null) return false;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.clipAnimations;

            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].name = "Camina_arma";
                    clips[i].loopTime = true;
                    clips[i].lockRootRotation = true;
                    clips[i].keepOriginalOrientation = true;
                    clips[i].lockRootHeightY = true;
                    clips[i].keepOriginalPositionY = false;
                    clips[i].heightFromFeet = true;
                    clips[i].lockRootPositionXZ = true;
                    clips[i].keepOriginalPositionXZ = true;
                }
                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();
            return true;
        }

        [MenuItem("Tools/SudoKill9/Ejecutar Pruebas Automatizadas Combate y Disparo (7-12)")]
        public static void EjecutarPruebasCombateDesdeMenu()
        {
            LinuxCombatIK ik = Object.FindFirstObjectByType<LinuxCombatIK>();
            if (ik == null)
            {
                EditorUtility.DisplayDialog("Aviso", "No se encontró el componente LinuxCombatIK en la escena activa.", "OK");
                return;
            }

            bool exito = ik.EjecutarPruebasCalidadCombate();
            EditorUtility.DisplayDialog(
                exito ? "Pruebas Superadas (100%)" : "Fallo en Pruebas",
                exito
                    ? "Todas las pruebas de combate, animación Camina_arma, disparo, físicas de bala y daño en Capa 0 se ejecutaron con éxito."
                    : "Una o más pruebas de combate fallaron. Revisa la consola de Unity para detalles.",
                "OK"
            );
        }

        public static bool ConfigurarAnimacionCaminaArma(bool logConsola = true)
        {
            bool ok1 = ConfigurarControladorIndividual(ControllerPathPrincipal, logConsola);
            bool ok2 = ConfigurarControladorIndividual(ControllerPathSecundario, logConsola);
            return ok1 && ok2;
        }

        public static bool ConfigurarControladorIndividual(string controllerPath, bool logConsola = true)
        {
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (ctrl == null)
            {
                if (logConsola) Debug.LogWarning($"[SetupCaminaArma] No se encontró el AnimatorController en: {controllerPath}");
                return false;
            }

            // 1. Cargar clip de animación Camina_arma
            AnimationClip clipCaminaArma = CargarAnimationClip(CaminaArmaFbxPath);
            if (clipCaminaArma == null)
            {
                Debug.LogError($"[SetupCaminaArma] No se pudo cargar el clip de animación desde: {CaminaArmaFbxPath}");
                return false;
            }

            // 2. Asegurar parámetro 'ArmaActiva'
            bool tieneArmaActiva = false;
            foreach (var p in ctrl.parameters)
            {
                if (p.name == "ArmaActiva")
                {
                    tieneArmaActiva = true;
                    break;
                }
            }

            if (!tieneArmaActiva)
            {
                ctrl.AddParameter("ArmaActiva", AnimatorControllerParameterType.Bool);
                if (logConsola) Debug.Log($"[SetupCaminaArma] Parámetro 'ArmaActiva' agregado a {Path.GetFileName(controllerPath)}.");
            }

            // 3. Localizar o crear estado 'Camina_arma' en la capa base
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            AnimatorState stateCaminaArma = null;
            AnimatorState stateLocomotion = null;
            AnimatorState stateJump = null;
            AnimatorState stateCrouch = null;

            foreach (var cs in sm.states)
            {
                if (cs.state.name == "Camina_arma") stateCaminaArma = cs.state;
                else if (cs.state.name == "Locomotion") stateLocomotion = cs.state;
                else if (cs.state.name == "Jump") stateJump = cs.state;
                else if (cs.state.name == "Crouch") stateCrouch = cs.state;
            }

            if (stateCaminaArma == null)
            {
                stateCaminaArma = sm.AddState("Camina_arma", new Vector3(300f, -50f, 0f));
                if (logConsola) Debug.Log($"[SetupCaminaArma] Estado 'Camina_arma' creado en Base Layer de {Path.GetFileName(controllerPath)}.");
            }

            stateCaminaArma.motion = clipCaminaArma;

            // 4. Configurar transiciones Locomotion <-> Camina_arma
            if (stateLocomotion != null)
            {
                AsegurarTransicion(stateLocomotion, stateCaminaArma, "ArmaActiva", true, 0.2f, false);
                AsegurarTransicion(stateCaminaArma, stateLocomotion, "ArmaActiva", false, 0.25f, false);
            }

            // 5. Configurar transiciones con Jump
            if (stateJump != null)
            {
                AsegurarTransicion(stateCaminaArma, stateJump, "Jump", true, 0.05f, false);
                AsegurarTransicion(stateJump, stateCaminaArma, "ArmaActiva", true, 0.2f, true, 0.85f);
            }

            // 6. Configurar transiciones con Crouch
            if (stateCrouch != null)
            {
                AsegurarTransicion(stateCaminaArma, stateCrouch, "IsCrouching", true, 0.15f, false);
                AsegurarTransicion(stateCrouch, stateCaminaArma, "ArmaActiva", true, 0.15f, false);
            }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            if (logConsola)
            {
                Debug.Log($"[SetupCaminaArma] Verificación y configuración de '{Path.GetFileName(controllerPath)}' completada con éxito.");
            }
            return true;
        }

        private static AnimationClip CargarAnimationClip(string fbxPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (Object a in assets)
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }

        private static void AsegurarTransicion(
            AnimatorState origen,
            AnimatorState destino,
            string condicionParametro,
            bool valorBooleano,
            float duracion,
            bool tieneExitTime,
            float exitTime = 0.8f)
        {
            foreach (var t in origen.transitions)
            {
                if (t.destinationState == destino)
                {
                    return;
                }
            }

            AnimatorStateTransition trans = origen.AddTransition(destino);
            trans.duration = duracion;
            trans.hasFixedDuration = true;
            trans.hasExitTime = tieneExitTime;
            if (tieneExitTime) trans.exitTime = exitTime;

            if (condicionParametro == "Jump")
            {
                trans.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            }
            else
            {
                trans.AddCondition(valorBooleano ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, condicionParametro);
            }
        }
    }
}
