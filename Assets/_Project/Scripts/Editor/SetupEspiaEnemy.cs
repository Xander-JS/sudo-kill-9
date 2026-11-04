using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using SudoKill9.Enemies.Espia;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Configuración con 1-Click de espia_opt (mismo patrón que SetupEscudoEnemy):
    /// 1. Crea/reconstruye EspiaController.controller con los parámetros que espera EnemyAIBase.
    /// 2. Busca 'espia_opt' en la escena activa (o lo instancia desde espia_opt.fbx detrás del Virus).
    /// 3. Añade/ajusta Animator, NavMeshAgent, CapsuleCollider (ajustado al mesh), EspiaStats, EspiaAudio,
    ///    EspiaAIController y el punto de salida del láser (hijo del hueso Head).
    /// 4. Hornea el NavMesh del Plane y marca la escena como modificada (guardar con Ctrl+S).
    /// </summary>
    public static class SetupEspiaEnemy
    {
        private const string ControllerPath = "Assets/_Project/Animations/Controllers/Enemies/EspiaController.controller";
        private const string ModelPath = "Assets/_Project/Models/Characters/espia-skeleton/espia_opt.fbx";
        private const string ClipsDir = "Assets/_Project/Animations/Clips/Enemies_Animaciones";
        // Clip compartido con el jugador (LinuxLocomotion / Animacion_Linux_K): se carga SIN tocar su importación.
        private const string ShootClipPath = "Assets/_Project/Animations/Clips/Animacion_Boss/Linux_animation/Firing Rifle Quiet.fbx";
        // Aún no hay carpeta propia de SFX para Espia: estados comunes desde la del Virus, disparos desde SFX/disparo.
        private const string SfxDir = "Assets/_Project/Audio/SFX/Enemies_Sound/Virus";
        private const string LaserSfxDir = "Assets/_Project/Audio/SFX/disparo";
        private const string GameObjectName = "espia_opt";
        private const string MuzzleName = "EyeMuzzle";

        [MenuItem("Tools/Setup Espia Enemy & Animator")]
        public static void RunSetup()
        {
            AnimatorController ctrl = ConfigureEspiaController();

            GameObject espiaGO = FindOrCreateEspia();
            if (espiaGO == null) return;

            SetupEspiaGameObject(espiaGO, ctrl);
            SetupVirusEnemy.SetupNavMeshInScene();
            SetupVirusEnemy.EnsureAudioListener();

            EditorSceneManager.MarkSceneDirty(espiaGO.scene);
            Selection.activeObject = espiaGO;
            Debug.Log("<color=green>[SetupEspiaEnemy]</color> 'espia_opt' configurado (Stats, Audio 3D + láser, AI a distancia, NavMeshAgent, Animator, Collider). Guarda con Ctrl+S.");
        }

        // ---------------------------------------------------------------- Animator Controller

        public static AnimatorController ConfigureEspiaController()
        {
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            AnimationClip idleClip = SetupVirusEnemy.LoadClip(ClipsDir + "/Respiracion/StandingIdle.fbx", true);
            AnimationClip runClip = SetupVirusEnemy.LoadClip(ClipsDir + "/Desplazamiento/StandingRunForward.fbx", true);
            AnimationClip limpClip = SetupVirusEnemy.LoadClip(ClipsDir + "/Desplazamiento/InjuredRun.fbx", true);
            AnimationClip shootClip = LoadClipReadOnly(ShootClipPath);
            AnimationClip hitClip = SetupVirusEnemy.LoadClip(ClipsDir + "/RecibiendoDano/Standing_React_Large_From_Front.fbx", false);
            AnimationClip dieClip = SetupVirusEnemy.LoadClip(ClipsDir + "/Muerte/StandingDeathForward.fbx", false);

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
            // "Roar" = fijar blanco (0.8 s): pose de reposo tensa mientras suena la detección; el espía no ruge.
            AnimatorState sLock = AddState(sm, "Roar", idleClip, 250f, -50f);
            sLock.speed = 1.6f;
            AnimatorState sRun = AddState(sm, "Run", runClip, 500f, 50f);
            AnimatorState sLimp = AddState(sm, "Crawl", limpClip, 500f, 150f);

            Transition(sIdle, sRun, 0.15f, t => { t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrawling"); });
            Transition(sRun, sIdle, 0.15f, t => t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"));
            Transition(sIdle, sLimp, 0.2f, t => { t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); t.AddCondition(AnimatorConditionMode.If, 0f, "IsCrawling"); });
            Transition(sLimp, sIdle, 0.2f, t => t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"));
            Transition(sRun, sLimp, 0.25f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "IsCrawling"));
            Transition(sLimp, sRun, 0.25f, t => t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrawling"));
            Transition(sIdle, sLock, 0.1f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "Roar"));
            var lockToIdle = sLock.AddTransition(sIdle);
            lockToIdle.hasExitTime = true;
            lockToIdle.exitTime = 0.9f;
            lockToIdle.duration = 0.2f;

            // Dos disparos del ojo con el mismo clip de disparo: el rápido a 1.15x y el cargado a 0.85x (más pesado).
            AnimatorState sShot = AddState(sm, "Attack_EyeLaser", shootClip, 750f, -20f);
            sShot.speed = 1.15f;
            AnimatorState sCharged = AddState(sm, "Attack_EyeLaserCharged", shootClip, 750f, 60f);
            sCharged.speed = 0.85f;
            SetupVirusEnemy.CreateAttackTransition(sm, sShot, sIdle, 0);
            SetupVirusEnemy.CreateAttackTransition(sm, sCharged, sIdle, 1);

            AnimatorState sHit = AddState(sm, "Hit", hitClip, 500f, -120f);
            AnyState(sm, sHit, 0.1f, "Hit");
            var hitToIdle = sHit.AddTransition(sIdle);
            hitToIdle.hasExitTime = true;
            hitToIdle.exitTime = 0.85f; // Mismo stagger que el Virus.
            hitToIdle.duration = 0.15f;

            AnimatorState sDie = AddState(sm, "Death", dieClip, 750f, -120f);
            AnyState(sm, sDie, 0.15f, "Die");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[SetupEspiaEnemy]</color> EspiaController.controller configurado.");
            return ctrl;
        }

        /// <summary>Primer clip del FBX sin modificar su importación (no cambia loopTime de clips compartidos).</summary>
        private static AnimationClip LoadClipReadOnly(string fbxPath)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            }
            return null;
        }

        private static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion clip, float x, float y)
        {
            if (clip == null) Debug.LogWarning($"[SetupEspiaEnemy] Estado '{name}' sin clip (no se encontró el FBX).");
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

        private static GameObject FindOrCreateEspia()
        {
            GameObject go = GameObject.Find(GameObjectName);
            if (go == null)
            {
                var existingAI = Object.FindFirstObjectByType<EspiaAIController>();
                if (existingAI != null) go = existingAI.gameObject;
            }
            if (go != null) return go;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"[SetupEspiaEnemy] No se encontró '{GameObjectName}' en la escena ni el modelo en {ModelPath}.");
                return null;
            }

            go = (GameObject)PrefabUtility.InstantiatePrefab(model, EditorSceneManager.GetActiveScene());
            go.name = GameObjectName;

            // Detrás del Virus (Escudo va a +X, Volador a la derecha): ya empieza a distancia de tiro.
            GameObject virus = SetupVirusEnemy.FindVirusInScene();
            go.transform.position = virus != null ? virus.transform.position + new Vector3(0f, 0f, -6f) : Vector3.zero;
            Undo.RegisterCreatedObjectUndo(go, "Create espia_opt");
            Debug.Log($"<color=green>[SetupEspiaEnemy]</color> '{GameObjectName}' instanciado desde {ModelPath}.");
            return go;
        }

        public static void SetupEspiaGameObject(GameObject go, AnimatorController ctrl)
        {
            Undo.RegisterFullObjectHierarchyUndo(go, "Setup Espia Enemy");

            // 1. Animator
            Animator anim = go.GetComponentInChildren<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            EditorUtility.SetDirty(anim);

            // 2. CapsuleCollider ajustado a los bounds del mesh.
            FitCapsule(go, out float height, out float radius, out Vector3 center);
            CapsuleCollider col = go.GetComponent<CapsuleCollider>();
            if (col == null) col = go.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.center = center;
            col.height = height;
            col.radius = radius;

            // 3. Stats: idénticas al Virus (100 / 15 / 1.5 s / crítico 30 %).
            EspiaStats stats = go.GetComponent<EspiaStats>();
            if (stats == null) stats = go.AddComponent<EspiaStats>();
            stats.ApplyBalanceDefaults();
            EditorUtility.SetDirty(stats);

            // 4. Audio: attackClips = exactamente 2 disparos láser [0 rápido, 1 cargado].
            EspiaAudio eAudio = go.GetComponent<EspiaAudio>();
            if (eAudio == null) eAudio = go.AddComponent<EspiaAudio>();
            eAudio.SetClips(
                detection: LoadSfx(SfxDir, "deteccion/xcreenplay__cyber-play.wav"),
                normalMove: LoadSfx(SfxDir, "movimiento/normal/sergenious__robo.wav"),
                crawlMove: LoadSfx(SfxDir, "movimiento/suelo/iainmccurdy__buzzing-faulty-and-sparking-fluorescent-light.wav"),
                attacks: new[]
                {
                    LoadSfx(LaserSfxDir, "alphatrooper18__laser_shoot2.wav"), // 0: disparo rápido (corto y seco)
                    LoadSfx(LaserSfxDir, "daleonfire__laser2.wav"),           // 1: disparo cargado (más largo)
                },
                hitDamage: LoadSfx(SfxDir, "recibir_daño/timkahn__glitch_sd_03.wav"),
                death: LoadSfx(SfxDir, "muerte/felixblume__impact-on-a-window.wav"));
            ConfigureAudioSources(go, eAudio);

            // 5. NavMeshAgent
            NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
            if (agent == null) agent = go.AddComponent<NavMeshAgent>();

            // 6. AI a distancia + punto de salida del láser.
            EspiaAIController ai = go.GetComponent<EspiaAIController>();
            if (ai == null) ai = go.AddComponent<EspiaAIController>();
            ai.ApplyBalanceDefaults();
            EditorUtility.SetDirty(ai);

            var aiSO = new SerializedObject(ai);
            aiSO.FindProperty("agent").objectReferenceValue = agent;
            aiSO.FindProperty("animator").objectReferenceValue = anim;
            aiSO.FindProperty("stats").objectReferenceValue = stats;
            aiSO.FindProperty("enemyAudio").objectReferenceValue = eAudio;
            aiSO.FindProperty("eyeMuzzle").objectReferenceValue = EnsureEyeMuzzle(go, anim, center, height);
            aiSO.ApplyModifiedPropertiesWithoutUndo();

            agent.speed = ai.RunSpeed;
            agent.angularSpeed = 240f;
            agent.acceleration = 12f;
            agent.stoppingDistance = ai.StoppingDistance; // 7.5 m: se para lejos, no pegado al jugador
            agent.radius = radius;
            agent.height = height;
            agent.baseOffset = 0f;
            agent.avoidancePriority = 60; // Cede el paso a los cuerpo a cuerpo (Virus 50, Escudo 40).
            EditorUtility.SetDirty(agent);

            // Barra de vida World Space sobre la cabeza (herramienta compartida, altura según el tipo de enemigo).
            SetupEnemyHealthBars.Ensure(go.GetComponent<SudoKill9.Enemies.EnemyStatsBase>());
            // VFX de muerte, polvo/propulsión e impacto (herramienta compartida, color según el villano).
            SetupEnemyVFX.Ensure(go.GetComponent<SudoKill9.Enemies.EnemyStatsBase>());

            EditorUtility.SetDirty(go);
        }

        /// <summary>Crea (o reutiliza) 'EyeMuzzle' delante de la cara, como hijo del hueso Head para que siga la animación.</summary>
        private static Transform EnsureEyeMuzzle(GameObject go, Animator anim, Vector3 capsuleCenter, float height)
        {
            Transform head = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            Transform parent = head != null ? head : go.transform;

            Transform muzzle = FindDeep(go.transform, MuzzleName);
            if (muzzle == null)
            {
                muzzle = new GameObject(MuzzleName).transform;
                Undo.RegisterCreatedObjectUndo(muzzle.gameObject, "Create EyeMuzzle");
            }
            muzzle.SetParent(parent, false);

            Vector3 worldPos = head != null
                ? head.position + go.transform.up * (height * 0.05f) + go.transform.forward * (height * 0.06f)
                : go.transform.TransformPoint(capsuleCenter + Vector3.up * height * 0.4f + Vector3.forward * 0.15f);
            muzzle.position = worldPos;
            muzzle.rotation = go.transform.rotation;

            if (head == null) Debug.LogWarning("[SetupEspiaEnemy] Sin hueso Head humanoide: EyeMuzzle cuelga de la raíz. Ajusta su posición al ojo a mano.");
            return muzzle;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private static void FitCapsule(GameObject go, out float height, out float radius, out Vector3 center)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                height = 1.8f; radius = 0.4f; center = new Vector3(0f, 0.9f, 0f);
                Debug.LogWarning("[SetupEspiaEnemy] Sin renderers: se usa una cápsula por defecto (h 1.8, r 0.4).");
                return;
            }

            Bounds world = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);

            Vector3 scale = go.transform.lossyScale;
            Vector3 size = new Vector3(world.size.x / scale.x, world.size.y / scale.y, world.size.z / scale.z);
            center = go.transform.InverseTransformPoint(world.center);
            height = size.y;
            // Silueta humanoide esbelta: radio a partir de la profundidad/anchura menor (los brazos en T inflan la X).
            radius = Mathf.Min(Mathf.Min(size.x, size.z) * 0.5f, height * 0.25f);
            radius = Mathf.Max(radius, 0.25f);
        }

        private static void ConfigureAudioSources(GameObject go, EspiaAudio eAudio)
        {
            var so = new SerializedObject(eAudio);
            AudioSource effects = so.FindProperty("effectsSource").objectReferenceValue as AudioSource;
            AudioSource movement = so.FindProperty("movementSource").objectReferenceValue as AudioSource;
            AudioSource laser = so.FindProperty("laserSource").objectReferenceValue as AudioSource;
            if (effects == null) effects = go.AddComponent<AudioSource>();
            if (movement == null || movement == effects) movement = go.AddComponent<AudioSource>();
            if (laser == null || laser == effects || laser == movement) laser = go.AddComponent<AudioSource>();

            Configure3D(effects, false);
            Configure3D(movement, true);
            eAudio.ConfigureLaserSource(laser); // 8 → 60 m, curva personalizada, spread 40°
            EditorUtility.SetDirty(laser);

            so.FindProperty("effectsSource").objectReferenceValue = effects;
            so.FindProperty("movementSource").objectReferenceValue = movement;
            so.FindProperty("laserSource").objectReferenceValue = laser;
            so.FindProperty("effectsVolume").floatValue = 1f;
            so.FindProperty("movementVolume").floatValue = 0.8f;
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

        private static AudioClip LoadSfx(string dir, string relativePath)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(dir + "/" + relativePath);
            if (clip == null) Debug.LogError($"[SetupEspiaEnemy] No se encontró el clip: {dir}/{relativePath}");
            return clip;
        }
    }
}
