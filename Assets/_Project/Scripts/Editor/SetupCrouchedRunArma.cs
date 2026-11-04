using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Configura de forma NO destructiva las animaciones avanzadas en LinuxLocomotion.controller:
/// 1. "Crouched_Run_Arma" (clip Crouched Run): al caminar mientras se apunta o dispara (CorrerApuntando).
/// 2. "Jump" (clip Rifle Jump): salto físico con AnyState -> Jump y transiciones fluidas de retorno.
/// 3. "StandingDeathForward" (clip StandingDeathForward): animación de muerte al recibir daño fatal (Die / IsDead).
/// </summary>
[InitializeOnLoad]
public static class SetupCrouchedRunArma
{
    public const string ControllerPath = "Assets/_Project/Animations/Controllers/LinuxLocomotion.controller";
    public const string ClipCrouchedRunPath = "Assets/_Project/Animations/Clips/Animacion Linux/Crouched Run.fbx";
    public const string ClipRifleJumpPath = "Assets/_Project/Animations/Clips/Animacion Linux/Rifle Jump.fbx";
    public const string ClipDeathForwardPath = "Assets/_Project/Animations/Clips/Enemies_Animaciones/Muerte/StandingDeathForward.fbx";

    public const string ParamCorrerApuntando = "CorrerApuntando";
    public const string ParamJump = "Jump";
    public const string ParamDie = "Die";
    public const string ParamIsDead = "IsDead";

    public const string StateCrouchedRun = "Crouched_Run_Arma";
    public const string StateJump = "Jump";
    public const string StateDeath = "StandingDeathForward";

    static SetupCrouchedRunArma()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl != null && (!TieneEstado(ctrl, StateCrouchedRun) || !TieneEstado(ctrl, StateDeath) || !TieneEstado(ctrl, StateJump)))
            {
                Aplicar(ctrl);
            }
        };
    }

    [MenuItem("Tools/SudoKill9/Configurar Animaciones Player_Linux (Crouched Run, Salto, Muerte)")]
    [MenuItem("Tools/SudoKill9/Aplicar Crouched Run al Apuntar-Disparar en Movimiento")]
    public static void AplicarDesdeMenu()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) { Debug.LogError("[SetupPlayerCombatAnimations] No se encontró " + ControllerPath); return; }
        Aplicar(ctrl);
    }

    public static bool TieneEstado(AnimatorController ctrl, string stateName)
    {
        if (ctrl == null || ctrl.layers.Length == 0) return false;
        foreach (var s in ctrl.layers[0].stateMachine.states)
            if (s.state.name == stateName) return true;
        return false;
    }

    public static void Aplicar(AnimatorController ctrl)
    {
        if (ctrl == null) return;

        // Asegurar parámetros requeridos
        AsegurarParametro(ctrl, ParamCorrerApuntando, AnimatorControllerParameterType.Bool);
        AsegurarParametro(ctrl, ParamJump, AnimatorControllerParameterType.Trigger);
        AsegurarParametro(ctrl, ParamDie, AnimatorControllerParameterType.Trigger);
        AsegurarParametro(ctrl, ParamIsDead, AnimatorControllerParameterType.Bool);
        AsegurarParametro(ctrl, "ArmaActiva", AnimatorControllerParameterType.Bool);
        AsegurarParametro(ctrl, "DisparandoQuieto", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        // 1. CONFIGURAR RIFLE JUMP
        ConfigurarSalto(ctrl, sm);

        // 2. CONFIGURAR CROUCHED RUN AL APUNTAR/DISPARAR
        ConfigurarCrouchedRun(ctrl, sm);

        // 3. CONFIGURAR STANDING DEATH FORWARD (MUERTE)
        ConfigurarMuerte(ctrl, sm);

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("<color=green>[SetupPlayerCombatAnimations]</color> Estados 'Crouched_Run_Arma', 'Jump' y 'StandingDeathForward' configurados exitosamente en LinuxLocomotion.controller.");
    }

    private static void ConfigurarSalto(AnimatorController ctrl, AnimatorStateMachine sm)
    {
        AnimationClip clipJump = CargarClip(ClipRifleJumpPath, false);
        if (clipJump == null) return;

        AnimatorState sJump = null;
        foreach (var s in sm.states)
        {
            if (s.state.name == StateJump) { sJump = s.state; break; }
        }
        if (sJump == null)
        {
            sJump = sm.AddState(StateJump, new Vector3(600f, 0f, 0f));
        }
        sJump.motion = clipJump;
        sJump.writeDefaultValues = true;

        // Verificar transición AnyState -> Jump
        bool tieneToJump = false;
        foreach (var t in sm.anyStateTransitions)
        {
            if (t.destinationState == sJump)
            {
                tieneToJump = true;
                break;
            }
        }
        if (!tieneToJump)
        {
            var toJump = sm.AddAnyStateTransition(sJump);
            toJump.hasExitTime = false;
            toJump.hasFixedDuration = true;
            toJump.duration = 0.05f;
            toJump.canTransitionToSelf = false;
            toJump.AddCondition(AnimatorConditionMode.If, 0f, ParamJump);
        }

        // Salida a Locomotion si no existe
        AnimatorState sLoco = null;
        foreach (var s in sm.states) if (s.state.name == "Locomotion") sLoco = s.state;
        if (sLoco != null)
        {
            bool tieneFromJump = false;
            foreach (var t in sJump.transitions) if (t.destinationState == sLoco) tieneFromJump = true;
            if (!tieneFromJump)
            {
                var fromJump = sJump.AddTransition(sLoco);
                fromJump.hasExitTime = true;
                fromJump.exitTime = 0.85f;
                fromJump.hasFixedDuration = true;
                fromJump.duration = 0.2f;
            }
        }
    }

    private static void ConfigurarCrouchedRun(AnimatorController ctrl, AnimatorStateMachine sm)
    {
        AnimationClip clipCR = CargarClip(ClipCrouchedRunPath, true);
        if (clipCR == null) return;

        AnimatorState sCrouchArma = null;
        foreach (var s in sm.states)
        {
            if (s.state.name == StateCrouchedRun) { sCrouchArma = s.state; break; }
        }
        if (sCrouchArma == null)
        {
            sCrouchArma = sm.AddState(StateCrouchedRun, new Vector3(900f, -50f, 0f));
        }
        sCrouchArma.motion = clipCR;
        sCrouchArma.writeDefaultValues = true;
        sCrouchArma.iKOnFeet = false;

        // Recrear AnyState -> Crouched_Run_Arma
        for (int i = sm.anyStateTransitions.Length - 1; i >= 0; i--)
        {
            if (sm.anyStateTransitions[i].destinationState == sCrouchArma)
            {
                sm.RemoveAnyStateTransition(sm.anyStateTransitions[i]);
            }
        }

        var toCR = sm.AddAnyStateTransition(sCrouchArma);
        toCR.hasExitTime = false;
        toCR.hasFixedDuration = true;
        toCR.duration = 0.15f;
        toCR.canTransitionToSelf = false;
        toCR.AddCondition(AnimatorConditionMode.If, 0f, ParamCorrerApuntando);
        toCR.AddCondition(AnimatorConditionMode.If, 0f, "ArmaActiva");

        // Salidas hacia Firing_Rifle_Quiet, Camina_arma, Locomotion
        AnimatorState sFiring = null, sCamina = null, sLoco = null, sJump = null;
        foreach (var s in sm.states)
        {
            if (s.state.name == "Firing_Rifle_Quiet") sFiring = s.state;
            else if (s.state.name == "Camina_arma") sCamina = s.state;
            else if (s.state.name == "Locomotion") sLoco = s.state;
            else if (s.state.name == StateJump) sJump = s.state;
        }

        foreach (var t in sCrouchArma.transitions)
        {
            sCrouchArma.RemoveTransition(t);
        }

        if (sFiring != null)
        {
            var t = sCrouchArma.AddTransition(sFiring);
            t.hasExitTime = false; t.hasFixedDuration = true; t.duration = 0.15f;
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, ParamCorrerApuntando);
            t.AddCondition(AnimatorConditionMode.If, 0f, "DisparandoQuieto");
        }
        if (sCamina != null)
        {
            var t = sCrouchArma.AddTransition(sCamina);
            t.hasExitTime = false; t.hasFixedDuration = true; t.duration = 0.2f;
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, ParamCorrerApuntando);
            t.AddCondition(AnimatorConditionMode.If, 0f, "ArmaActiva");
        }
        if (sLoco != null)
        {
            var t = sCrouchArma.AddTransition(sLoco);
            t.hasExitTime = false; t.hasFixedDuration = true; t.duration = 0.25f;
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "ArmaActiva");
        }

        // Conectar también Jump -> Crouched_Run_Arma si aterriza apuntando y moviéndose
        if (sJump != null)
        {
            bool tieneJumpToCR = false;
            foreach (var t in sJump.transitions) if (t.destinationState == sCrouchArma) tieneJumpToCR = true;
            if (!tieneJumpToCR)
            {
                var tJumpToCR = sJump.AddTransition(sCrouchArma);
                tJumpToCR.hasExitTime = true;
                tJumpToCR.exitTime = 0.85f;
                tJumpToCR.hasFixedDuration = true;
                tJumpToCR.duration = 0.15f;
                tJumpToCR.AddCondition(AnimatorConditionMode.If, 0f, ParamCorrerApuntando);
            }
        }
    }

    private static void ConfigurarMuerte(AnimatorController ctrl, AnimatorStateMachine sm)
    {
        AnimationClip clipDeath = CargarClip(ClipDeathForwardPath, false);
        if (clipDeath == null)
        {
            Debug.LogWarning("[SetupPlayerCombatAnimations] No se pudo cargar clip de muerte desde: " + ClipDeathForwardPath);
            return;
        }

        AnimatorState sDeath = null;
        foreach (var s in sm.states)
        {
            if (s.state.name == StateDeath) { sDeath = s.state; break; }
        }
        if (sDeath == null)
        {
            sDeath = sm.AddState(StateDeath, new Vector3(900f, 200f, 0f));
        }
        sDeath.motion = clipDeath;
        sDeath.writeDefaultValues = true;
        sDeath.iKOnFeet = false;

        // Limpiar transiciones existentes desde AnyState hacia Death para evitar duplicados
        for (int i = sm.anyStateTransitions.Length - 1; i >= 0; i--)
        {
            if (sm.anyStateTransitions[i].destinationState == sDeath)
            {
                sm.RemoveAnyStateTransition(sm.anyStateTransitions[i]);
            }
        }

        // AnyState -> Death por Trigger "Die"
        var toDeathTrigger = sm.AddAnyStateTransition(sDeath);
        toDeathTrigger.hasExitTime = false;
        toDeathTrigger.hasFixedDuration = true;
        toDeathTrigger.duration = 0.12f;
        toDeathTrigger.canTransitionToSelf = false;
        toDeathTrigger.AddCondition(AnimatorConditionMode.If, 0f, ParamDie);

        // AnyState -> Death por Bool "IsDead"
        var toDeathBool = sm.AddAnyStateTransition(sDeath);
        toDeathBool.hasExitTime = false;
        toDeathBool.hasFixedDuration = true;
        toDeathBool.duration = 0.12f;
        toDeathBool.canTransitionToSelf = false;
        toDeathBool.AddCondition(AnimatorConditionMode.If, 0f, ParamIsDead);

        // Sin transiciones de salida para que permanezca en el suelo al morir
        foreach (var t in sDeath.transitions)
        {
            sDeath.RemoveTransition(t);
        }
    }

    private static AnimationClip CargarClip(string fbxPath, bool loop)
    {
        AnimationClip clip = FirstClip(fbxPath);
        if (clip != null && clip.isLooping != loop)
        {
            SetLoop(fbxPath, loop);
            clip = FirstClip(fbxPath);
        }
        return clip;
    }

    private static AnimationClip FirstClip(string fbxPath)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        return null;
    }

    private static void SetLoop(string fbxPath, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips != null)
        {
            foreach (ModelImporterClipAnimation c in clips) c.loopTime = loop;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static void AsegurarParametro(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
    {
        foreach (var p in ctrl.parameters) if (p.name == name) return;
        ctrl.AddParameter(name, type);
    }
}
