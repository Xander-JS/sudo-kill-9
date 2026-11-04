using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using SudoKill9.Enemies.Escudo;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Configuración con 1-Click de escudo_opt (mismo patrón que SetupVirusEnemy):
    /// 1. Reconstruye escudo.controller con los parámetros que espera EnemyAIBase.
    /// 2. Busca 'escudo_opt' en la escena activa (o lo instancia desde escudo_opt.fbx junto al Virus).
    /// 3. Añade/ajusta Animator, NavMeshAgent, CapsuleCollider (ajustado al mesh), EscudoStats, EscudoAudio y EscudoAIController.
    /// 4. Hornea el NavMesh del Plane y marca la escena como modificada.
    /// </summary>
    public static class SetupEscudoEnemy
    {
        private const string ControllerPath = "Assets/_Project/Animations/Clips/escudo.controller";
        private const string ModelPath = "Assets/_Project/Models/Characters/escudo-skeleton/escudo_opt.fbx";
        private const string ClipsDir = "Assets/_Project/Animations/Clips/Enemies_Animaciones";
        // Aún no hay carpeta propia de SFX para Escudo: usa los clips libres de la carpeta del Virus.
        private const string SfxDir = "Assets/_Project/Audio/SFX/Enemies_Sound/Virus";
        private const string GameObjectName = "escudo_opt";

        [MenuItem("Tools/Setup Escudo Enemy & Animator")]
        public static void RunSetup()
        {
            AnimatorController ctrl = ConfigureEscudoController();

            GameObject escudoGO = FindOrCreateEscudo();
            if (escudoGO == null) return;

            SetupEscudoGameObject(escudoGO, ctrl);
            SetupVirusEnemy.SetupNavMeshInScene();
            SetupVirusEnemy.EnsureAudioListener();

            EditorSceneManager.MarkSceneDirty(escudoGO.scene);
            Selection.activeObject = escudoGO;
            Debug.Log("<color=green>[SetupEscudoEnemy]</color> 'escudo_opt' configurado (Stats, Audio 3D, AI, NavMeshAgent, Animator, Collider). Guarda con Ctrl+S.");
        }

        // ---------------------------------------------------------------- Animator Controller

        public static AnimatorController ConfigureEscudoController()
        {
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            AnimationClip idleClip = SetupVirusEnemy.LoadClip(ClipsDir + "/Respiracion/StandingIdle.fbx", true);
            AnimationClip tauntClip = SetupVirusEnemy.LoadClip(ClipsDir + "/Preparacion/StandingTauntChestThump.fbx", false);
            AnimationClip runClip = SetupVirusEnemy.LoadClip(ClipsDir + "/Desplazamiento/MutantRun.fbx", true);
            AnimationClip limpClip = SetupVirusEnemy.LoadClip(ClipsDir + "/Desplazamiento/InjuredRun.fbx", true);
            AnimationClip slamClip = SetupVirusEnemy.LoadClip(ClipsDir + "/Ataques/StandingMeleeAttackDownward.fbx", false);
            AnimationClip bashClip = SetupVirusEnemy.LoadClip(ClipsDir + "/Ataques/StableSwordInwardSlash.fbx", false);
            AnimationClip hitClip = SetupVirusEnemy.LoadClip(ClipsDir + "/RecibiendoDano/Standing_React_Large_From_Front.fbx", false);
            AnimationClip dieClip = SetupVirusEnemy.LoadClip(ClipsDir + "/Muerte/StandingDeathLeft.fbx", false);

            // Limpiar el contenido anterior (el placeholder 'mixamo_com').
            for (int i = ctrl.parameters.Length - 1; i >= 0; i--) ctrl.RemoveParameter(i);
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);
            foreach (AnimatorStateTransition t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);

            // Mismos nombres que los parámetros por defecto de EnemyAIBase.
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsCrawling", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Roar", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            AnimatorState sIdle = AddState(sm, "Idle", idleClip, 250f, 50f);
            sm.defaultState = sIdle;
            AnimatorState sTaunt = AddState(sm, "Roar", tauntClip, 250f, -50f);
            AnimatorState sRun = AddState(sm, "Run", runClip, 500f, 50f);
            // "Crawl" = cojera en vida crítica (IsCrawling); el robot-escudo no se arrastra.
            AnimatorState sLimp = AddState(sm, "Crawl", limpClip, 500f, 150f);
            // Pasos más pesados que el Virus.
            sRun.speed = 0.9f;

            Transition(sIdle, sRun, 0.15f, t => { t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrawling"); });
            Transition(sRun, sIdle, 0.15f, t => t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"));
            Transition(sIdle, sLimp, 0.2f, t => { t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.If, 0f, "IsCrawling"); });
            Transition(sLimp, sIdle, 0.2f, t => t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"));
            Transition(sRun, sLimp, 0.25f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "IsCrawling"));
            Transition(sLimp, sRun, 0.25f, t => t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrawling"));
            Transition(sIdle, sTaunt, 0.1f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "Roar"));

            var tauntToIdle = sTaunt.AddTransition(sIdle);
            tauntToIdle.hasExitTime = true;
            tauntToIdle.exitTime = 0.9f;
            tauntToIdle.duration = 0.2f;

            AnimatorState sSlam = AddState(sm, "Attack_ShieldSlam", slamClip, 750f, -20f);
            AnimatorState sBash = AddState(sm, "Attack_ShieldBash", bashClip, 750f, 60f);
            SetupVirusEnemy.CreateAttackTransition(sm, sSlam, sIdle, 0);
            SetupVirusEnemy.CreateAttackTransition(sm, sBash, sIdle, 1);

            AnimatorState sHit = AddState(sm, "Hit", hitClip, 500f, -120f);
            AnyState(sm, sHit, 0.1f, "Hit");
            var hitToIdle = sHit.AddTransition(sIdle);
            hitToIdle.hasExitTime = true;
            hitToIdle.exitTime = 0.7f; // Blindado: sale antes del stagger que el Virus (0.85).
            hitToIdle.duration = 0.15f;

            AnimatorState sDie = AddState(sm, "Death", dieClip, 750f, -120f);
            AnyState(sm, sDie, 0.15f, "Die");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[SetupEscudoEnemy]</color> escudo.controller configurado.");
            return ctrl;
        }

        private static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion clip, float x, float y)
        {
            if (clip == null) Debug.LogWarning($"[SetupEscudoEnemy] Estado '{name}' sin clip (no se encontró el FBX).");
            AnimatorState state = sm.AddState(name, new Vector3(x, y, 0f));
            state.motion = clip;
            return state;
        }

        private static void Transition(AnimatorState from, AnimatorState to, float duration, System.Action<AnimatorStateTransition> conditions)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            conditions(t);
        }

        private static void AnyState(AnimatorStateMachine sm, AnimatorState to, float duration, string trigger)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        // ---------------------------------------------------------------- GameObject

        private static GameObject FindOrCreateEscudo()
        {
            GameObject go = GameObject.Find(GameObjectName);
            if (go == null)
            {
                var existingAI = Object.FindFirstObjectByType<EscudoAIController>();
                if (existingAI != null) go = existingAI.gameObject;
            }
            if (go != null) return go;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"[SetupEscudoEnemy] No se encontró '{GameObjectName}' en la escena ni el modelo en {ModelPath}.");
                return null;
            }

            go = (GameObject)PrefabUtility.InstantiatePrefab(model, EditorSceneManager.GetActiveScene());
            go.name = GameObjectName;

            // Colocarlo junto al Virus (o en el origen) para que comparta el NavMesh del Plane.
            GameObject virus = SetupVirusEnemy.FindVirusInScene();
            go.transform.position = virus != null ? virus.transform.position + new Vector3(3f, 0f, 0f) : Vector3.zero;
            Undo.RegisterCreatedObjectUndo(go, "Create escudo_opt");
            Debug.Log($"<color=green>[SetupEscudoEnemy]</color> '{GameObjectName}' instanciado desde {ModelPath}.");
            return go;
        }

        public static void SetupEscudoGameObject(GameObject go, AnimatorController ctrl)
        {
            Undo.RegisterFullObjectHierarchyUndo(go, "Setup Escudo Enemy");

            // 1. Animator
            Animator anim = go.GetComponentInChildren<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            EditorUtility.SetDirty(anim);

            // 2. CapsuleCollider ajustado a los bounds del mesh del escudo.
            FitCapsule(go, out float height, out float radius, out Vector3 center);
            CapsuleCollider col = go.GetComponent<CapsuleCollider>();
            if (col == null) col = go.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.center = center;
            col.height = height;
            col.radius = radius;

            // 3. Stats (vida 160 / daño 20 / cooldown 1.8 s)
            EscudoStats stats = go.GetComponent<EscudoStats>();
            if (stats == null) stats = go.AddComponent<EscudoStats>();
            stats.ApplyBalanceDefaults();
            EditorUtility.SetDirty(stats);

            // 4. Audio 3D: dos AudioSources (efectos + bucle de locomoción), vol. 1.0, lineal 4 → 40 m.
            EscudoAudio eAudio = go.GetComponent<EscudoAudio>();
            if (eAudio == null) eAudio = go.AddComponent<EscudoAudio>();
            eAudio.SetClips(
                detection: LoadSfx("deteccion/xcreenplay__cyber-play.wav"),
                normalMove: LoadSfx("movimiento/normal/sergenious__robo.wav"),
                crawlMove: LoadSfx("movimiento/suelo/iainmccurdy__buzzing-faulty-and-sparking-fluorescent-light.wav"),
                attacks: new[]
                {
                    LoadSfx("ataque/golpes/signaturesoundsorg__game_clicks_from.wav"),
                    LoadSfx("ataque/zapatazos/xcreenplay__cyber-play.wav"),
                },
                hitDamage: LoadSfx("recibir_daño/timkahn__glitch_sd_03.wav"),
                death: LoadSfx("muerte/felixblume__impact-on-a-window.wav"));
            ConfigureAudioSources(go, eAudio);

            // 5. NavMeshAgent (más lento y pesado que el Virus: 5.5 / 240 / 14)
            NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
            if (agent == null) agent = go.AddComponent<NavMeshAgent>();

            // 6. AI
            EscudoAIController ai = go.GetComponent<EscudoAIController>();
            if (ai == null) ai = go.AddComponent<EscudoAIController>();
            ai.ApplyBalanceDefaults();
            EditorUtility.SetDirty(ai);

            var aiSO = new SerializedObject(ai);
            aiSO.FindProperty("agent").objectReferenceValue = agent;
            aiSO.FindProperty("animator").objectReferenceValue = anim;
            aiSO.FindProperty("stats").objectReferenceValue = stats;
            aiSO.FindProperty("enemyAudio").objectReferenceValue = eAudio;
            aiSO.ApplyModifiedPropertiesWithoutUndo();

            agent.speed = ai.RunSpeed;
            agent.angularSpeed = 180f;
            agent.acceleration = 10f;
            agent.stoppingDistance = ai.AttackRange * 0.85f;
            agent.radius = radius;
            agent.height = height;
            agent.baseOffset = 0f;
            agent.avoidancePriority = 40; // Empuja a los Virus (50) al cruzarse.
            EditorUtility.SetDirty(agent);

            // Barra de vida World Space sobre la cabeza (herramienta compartida, altura según el tipo de enemigo).
            SetupEnemyHealthBars.Ensure(go.GetComponent<SudoKill9.Enemies.EnemyStatsBase>());
            // VFX de muerte, polvo/propulsión e impacto (herramienta compartida, color según el villano).
            SetupEnemyVFX.Ensure(go.GetComponent<SudoKill9.Enemies.EnemyStatsBase>());

            EditorUtility.SetDirty(go);
        }

        /// <summary>Calcula la cápsula a partir de los bounds de los renderers, en espacio local de la raíz.</summary>
        private static void FitCapsule(GameObject go, out float height, out float radius, out Vector3 center)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                height = 2f; radius = 0.55f; center = new Vector3(0f, 1f, 0f);
                Debug.LogWarning("[SetupEscudoEnemy] Sin renderers: se usa una cápsula por defecto (h 2, r 0.55).");
                return;
            }

            Bounds world = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);

            Vector3 scale = go.transform.lossyScale;
            Vector3 size = new Vector3(world.size.x / scale.x, world.size.y / scale.y, world.size.z / scale.z);
            center = go.transform.InverseTransformPoint(world.center);
            height = size.y;
            // La placa del escudo es ancha: radio = media anchura horizontal mayor, sin superar media altura.
            radius = Mathf.Min(Mathf.Max(size.x, size.z) * 0.5f, height * 0.5f);
        }

        private static void ConfigureAudioSources(GameObject go, EscudoAudio eAudio)
        {
            var so = new SerializedObject(eAudio);
            AudioSource effects = so.FindProperty("effectsSource").objectReferenceValue as AudioSource;
            AudioSource movement = so.FindProperty("movementSource").objectReferenceValue as AudioSource;
            if (effects == null) effects = go.AddComponent<AudioSource>();
            if (movement == null || movement == effects) movement = go.AddComponent<AudioSource>();

            Configure3D(effects, false);
            Configure3D(movement, true);

            so.FindProperty("effectsSource").objectReferenceValue = effects;
            so.FindProperty("movementSource").objectReferenceValue = movement;
            so.FindProperty("effectsVolume").floatValue = 1f;
            so.FindProperty("movementVolume").floatValue = 1f;
            so.FindProperty("minDistance").floatValue = 4f;
            so.FindProperty("maxDistance").floatValue = 40f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(eAudio);
        }

        private static void Configure3D(AudioSource source, bool loop)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = 1f;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 4f;
            source.maxDistance = 40f;
            source.dopplerLevel = 0f;
            EditorUtility.SetDirty(source);
        }

        private static AudioClip LoadSfx(string relativePath)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + "/" + relativePath);
            if (clip == null) Debug.LogError($"[SetupEscudoEnemy] No se encontró el clip: {SfxDir}/{relativePath}");
            return clip;
        }
    }
}
