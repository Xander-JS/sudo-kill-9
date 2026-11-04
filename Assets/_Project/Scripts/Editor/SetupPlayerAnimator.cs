using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Configuración integral para Player_Linux:
/// - Reconstruye/actualiza LinuxLocomotion.controller con las 8 animaciones de Clips/Animacion Linux:
///   1. quieto.fbx (Idle)
///   2. Start Walking.fbx (Caminar adelante)
///   3. Run Forward.fbx (Correr adelante / W)
///   4. Run Backward.fbx (Correr atrás / S)
///   5. Run Left.fbx (Correr izquierda / A)
///   6. Run Right.fbx (Correr derecha / D)
///   7. Crouched Run.fbx (Agachado / Ctrl o C)
///   8. Rifle Jump.fbx (Salto / Espacio)
/// - Configura automáticamente el Prefab Player_Linux.prefab (física, collider, controlador).
/// - Vincula y actualiza cualquier jugador en la escena activa (Player_Linux, linux_opt, Player_LinuxSkeleton).
/// </summary>
public static class SetupPlayerAnimator
{
    private const string ControllerPath = "Assets/_Project/Animations/Controllers/LinuxLocomotion.controller";
    private const string ClipsDir = "Assets/_Project/Animations/Clips/Animacion Linux";
    private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Player_Linux.prefab";

    [MenuItem("Tools/Setup Complete Locomotion & Animator")]
    [MenuItem("Tools/The Last Root/Setup Player_Linux (Prefab & Scene)")]
    public static void RunFromMenu()
    {
        AnimatorController ctrl = ConfigureController();

        // 1. Configurar el Prefab Player_Linux.prefab en disco
        SetupPlayerLinuxPrefab(ctrl);

        // 2. Buscar cualquier personaje de Linux en la escena abierta y enlazarlo
        GameObject existing = GameObject.Find("Player_Linux") 
                           ?? GameObject.Find("Player_LinuxSkeleton") 
                           ?? GameObject.Find("linux_opt") 
                           ?? GameObject.Find("linux-skeleton");

        if (existing != null)
        {
            ConfigureCharacterInstance(existing, ctrl);
            Debug.Log($"[SetupPlayerAnimator] Controlador y física vinculados al personaje en escena: '{existing.name}'.");
        }
        else
        {
            // Si no existe ninguno en escena, creamos una instancia lista
            PlayerSetup.CreatePlayer();
        }

        Selection.activeObject = ctrl;
        Debug.Log("[SetupPlayerAnimator] ¡Configuración completada con éxito!\n" +
                  "Controles: W (Avanzar/Caminar/Correr), S (Retroceder), A (Izquierda), D (Derecha),\n" +
                  "Espacio (Saltar), Left Ctrl / C (Agacharse), Left Shift (Sprint / Correr a máxima velocidad).");
    }

    public static AnimatorController ConfigureController()
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // Cargar los 8 clips oficiales desde Clips/Animacion Linux
        AnimationClip idle     = LoadClip(ClipsDir + "/quieto.fbx", true);
        AnimationClip walk     = LoadClip(ClipsDir + "/Start Walking.fbx", true);
        AnimationClip forward  = LoadClip(ClipsDir + "/Run Forward.fbx", true);
        AnimationClip backward = LoadClip(ClipsDir + "/Run Backward.fbx", true);
        AnimationClip left     = LoadClip(ClipsDir + "/Run Left.fbx", true);
        AnimationClip right    = LoadClip(ClipsDir + "/Run Right.fbx", true);
        AnimationClip jump     = LoadClip(ClipsDir + "/Rifle Jump.fbx", false);
        AnimationClip crouch   = LoadClip(ClipsDir + "/Crouched Run.fbx", true);

        // Animaciones opcionales de combate
        AnimationClip caminaArma = LoadClip("Assets/_Project/Animations/Clips/Animacion_Boss/Linux_animation/Camina_arma.fbx", true);
        AnimationClip firingQuiet = LoadClip("Assets/_Project/Animations/Clips/Animacion_Boss/Linux_animation/Firing Rifle Quiet.fbx", true);
        AnimationClip death = LoadClip("Assets/_Project/Animations/Clips/Enemies_Animaciones/Muerte/StandingDeathForward.fbx", false);

        // Limpiar parámetros y estados previos para garantizar consistencia
        for (int i = ctrl.parameters.Length - 1; i >= 0; i--) ctrl.RemoveParameter(i);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);
        foreach (AnimatorStateTransition t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);

        // Parámetros de locomoción y combate
        ctrl.AddParameter("InputX", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("InputZ", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("ArmaActiva", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("DisparandoQuieto", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("IsDead", AnimatorControllerParameterType.Bool);

        // Locomotion: 2D Freeform Directional Blend Tree con las 6 posiciones de movimiento
        BlendTree tree;
        AnimatorState locomotion = ctrl.CreateBlendTreeInController("Locomotion", out tree);
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = "InputX";
        tree.blendParameterY = "InputZ";

        AddMotion(tree, idle, Vector2.zero);             // Reposo
        AddMotion(tree, walk, new Vector2(0f, 0.5f));    // Caminar adelante (W)
        AddMotion(tree, forward, new Vector2(0f, 1f));   // Correr adelante (W + Shift)
        AddMotion(tree, backward, new Vector2(0f, -1f)); // Retroceder (S)
        AddMotion(tree, left, new Vector2(-1f, 0f));     // Strafe izquierda (A)
        AddMotion(tree, right, new Vector2(1f, 0f));     // Strafe derecha (D)

        sm.defaultState = locomotion;
        SetStatePosition(sm, locomotion, new Vector3(300f, 100f, 0f));

        // Estado Jump (Rifle Jump)
        AnimatorState sJump = sm.AddState("Jump", new Vector3(600f, 0f, 0f));
        sJump.motion = jump;

        // Estado Crouch (Crouched Run)
        AnimatorState sCrouch = sm.AddState("Crouch", new Vector3(600f, 200f, 0f));
        sCrouch.motion = crouch;

        // Any State -> Jump (trigger)
        AnimatorStateTransition toJump = sm.AddAnyStateTransition(sJump);
        toJump.hasExitTime = false;
        toJump.hasFixedDuration = true;
        toJump.duration = 0.05f;
        toJump.canTransitionToSelf = false;
        toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

        AnimatorStateTransition fromJump = sJump.AddTransition(locomotion);
        fromJump.hasExitTime = true;
        fromJump.exitTime = 0.85f;
        fromJump.hasFixedDuration = true;
        fromJump.duration = 0.2f;

        // Locomotion <-> Crouch (IsCrouching)
        AnimatorStateTransition toCrouch = locomotion.AddTransition(sCrouch);
        toCrouch.hasExitTime = false;
        toCrouch.hasFixedDuration = true;
        toCrouch.duration = 0.15f;
        toCrouch.AddCondition(AnimatorConditionMode.If, 0f, "IsCrouching");

        AnimatorStateTransition fromCrouch = sCrouch.AddTransition(locomotion);
        fromCrouch.hasExitTime = false;
        fromCrouch.hasFixedDuration = true;
        fromCrouch.duration = 0.15f;
        fromCrouch.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");

        // Si existen los clips de combate con arma, integrar los estados
        if (caminaArma != null && firingQuiet != null)
        {
            AnimatorState sCaminaArma = sm.AddState("Camina_arma", new Vector3(300f, -50f, 0f));
            sCaminaArma.motion = caminaArma;

            AnimatorState sDisparoQuieto = sm.AddState("Firing_Rifle_Quiet", new Vector3(600f, -100f, 0f));
            sDisparoQuieto.motion = firingQuiet;

            // Locomotion -> Camina_arma
            var toArma = locomotion.AddTransition(sCaminaArma);
            toArma.hasExitTime = false;
            toArma.duration = 0.2f;
            toArma.AddCondition(AnimatorConditionMode.If, 0f, "ArmaActiva");
            toArma.AddCondition(AnimatorConditionMode.IfNot, 0f, "DisparandoQuieto");

            // Camina_arma -> Locomotion
            var fromArma = sCaminaArma.AddTransition(locomotion);
            fromArma.hasExitTime = false;
            fromArma.duration = 0.25f;
            fromArma.AddCondition(AnimatorConditionMode.IfNot, 0f, "ArmaActiva");

            // AnyState -> DisparoQuieto
            var toDisparo = sm.AddAnyStateTransition(sDisparoQuieto);
            toDisparo.hasExitTime = false;
            toDisparo.duration = 0.2f;
            toDisparo.canTransitionToSelf = false;
            toDisparo.AddCondition(AnimatorConditionMode.If, 0f, "ArmaActiva");
            toDisparo.AddCondition(AnimatorConditionMode.If, 0f, "DisparandoQuieto");

            // DisparoQuieto -> Camina_arma
            var fromDisparoToArma = sDisparoQuieto.AddTransition(sCaminaArma);
            fromDisparoToArma.hasExitTime = false;
            fromDisparoToArma.duration = 0.2f;
            fromDisparoToArma.AddCondition(AnimatorConditionMode.IfNot, 0f, "DisparandoQuieto");
            fromDisparoToArma.AddCondition(AnimatorConditionMode.If, 0f, "ArmaActiva");

            // DisparoQuieto -> Locomotion
            var fromDisparoToLoco = sDisparoQuieto.AddTransition(locomotion);
            fromDisparoToLoco.hasExitTime = false;
            fromDisparoToLoco.duration = 0.25f;
            fromDisparoToLoco.AddCondition(AnimatorConditionMode.IfNot, 0f, "ArmaActiva");

            // Salto y Agachado hacia/desde Camina_arma
            var armaToJump = sCaminaArma.AddTransition(sJump);
            armaToJump.hasExitTime = false;
            armaToJump.duration = 0.05f;
            armaToJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

            var jumpToArma = sJump.AddTransition(sCaminaArma);
            jumpToArma.hasExitTime = true;
            jumpToArma.exitTime = 0.85f;
            jumpToArma.duration = 0.2f;
            jumpToArma.AddCondition(AnimatorConditionMode.If, 0f, "ArmaActiva");

            var crouchToArma = sCrouch.AddTransition(sCaminaArma);
            crouchToArma.hasExitTime = false;
            crouchToArma.duration = 0.15f;
            crouchToArma.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
            crouchToArma.AddCondition(AnimatorConditionMode.If, 0f, "ArmaActiva");
        }

        SetupCrouchedRunArma.Aplicar(ctrl);
        EnableIKPass(ctrl);

        string missing = "";
        if (idle == null) missing += " quieto";
        if (walk == null) missing += " 'Start Walking'";
        if (forward == null) missing += " 'Run Forward'";
        if (backward == null) missing += " 'Run Backward'";
        if (left == null) missing += " 'Run Left'";
        if (right == null) missing += " 'Run Right'";
        if (jump == null) missing += " 'Rifle Jump'";
        if (crouch == null) missing += " 'Crouched Run'";
        if (missing.Length > 0) Debug.LogWarning("[SetupPlayerAnimator] Clips no encontrados:" + missing);
        else Debug.Log("[SetupPlayerAnimator] Todas las 8 animaciones de Clips/Animacion Linux fueron configuradas correctamente en LinuxLocomotion.controller.");

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    /// <summary>
    /// Asegura que el Prefab Player_Linux.prefab tenga Rigidbody, CapsuleCollider,
    /// LinuxSkeletonController y el Animator configurado con LinuxLocomotion.
    /// </summary>
    public static void SetupPlayerLinuxPrefab(RuntimeAnimatorController ctrl)
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogWarning($"[SetupPlayerAnimator] No se encontró el prefab en {PrefabPath}");
            return;
        }

        string path = AssetDatabase.GetAssetPath(prefabAsset);
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        ConfigureCharacterComponents(root, ctrl);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"[SetupPlayerAnimator] Prefab '{PrefabPath}' actualizado y guardado correctamente.");
    }

    /// <summary>
    /// Configura componentes de física y control en cualquier GameObject de Linux (en escena o prefab).
    /// </summary>
    public static void ConfigureCharacterInstance(GameObject target, RuntimeAnimatorController ctrl)
    {
        ConfigureCharacterComponents(target, ctrl);
        EditorUtility.SetDirty(target);
    }

    private static void ConfigureCharacterComponents(GameObject target, RuntimeAnimatorController ctrl)
    {
        // 1. Rigidbody
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb == null) rb = target.AddComponent<Rigidbody>();
        rb.mass = 80f;
        rb.useGravity = false; // El controlador aplica gravedad personalizada
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // 2. CapsuleCollider
        CapsuleCollider col = target.GetComponent<CapsuleCollider>();
        if (col == null) col = target.AddComponent<CapsuleCollider>();
        col.height = 2f;
        col.radius = 0.4f;
        col.center = new Vector3(0f, 1f, 0f);

        // Material sin fricción para evitar atascos en paredes
        PhysicsMaterial noFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/_Project/Physics/Player_NoFriction.asset");
        if (noFriction != null) col.sharedMaterial = noFriction;

        // 3. LinuxSkeletonController
        LinuxSkeletonController controller = target.GetComponent<LinuxSkeletonController>();
        if (controller == null) controller = target.AddComponent<LinuxSkeletonController>();

        // 4. Animator en target o hijos
        Animator anim = target.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
        }
    }

    public static void EnableIKPass(AnimatorController ctrl)
    {
        AnimatorControllerLayer[] layers = ctrl.layers;
        for (int i = 0; i < layers.Length; i++) layers[i].iKPass = true;
        ctrl.layers = layers;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
    }

    private static void SetStatePosition(AnimatorStateMachine sm, AnimatorState state, Vector3 position)
    {
        ChildAnimatorState[] children = sm.states;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].state == state) children[i].position = position;
        }
        sm.states = children;
    }

    private static void AddMotion(BlendTree tree, AnimationClip clip, Vector2 position)
    {
        if (clip != null) tree.AddChild(clip, position);
    }

    private static AnimationClip LoadClip(string fbxPath, bool loop)
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
        foreach (ModelImporterClipAnimation c in clips) c.loopTime = loop;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }
}
