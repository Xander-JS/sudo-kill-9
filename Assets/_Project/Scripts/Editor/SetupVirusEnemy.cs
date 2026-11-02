using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using SudoKill9.Enemies.Virus;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Herramienta de configuración con 1-Click:
    /// 1. Genera/Actualiza el Animator Controller de Virus (VirusController.controller).
    /// 2. Configura los parámetros y transiciones de todas las animaciones (Idle, Alerta/Rugido, Carrera, Suelo/Gateo, Ataques, Daño y Muerte).
    /// 3. Configura el GameObject 'virus' en la escena activa (NavMeshAgent, Colliders, VirusStats, VirusAIController).
    /// 4. Configura y hornea el NavMeshSurface en la escena si existe un Plane o suelo.
    /// </summary>
    public static class SetupVirusEnemy
    {
        private const string ControllerPath = "Assets/_Project/Animations/Controllers/Enemies/VirusController.controller";
        private const string ClipsDir = "Assets/_Project/Animations/Clips/Villanos_Animaciones";
        private const string SfxDir = "Assets/_Project/Audio/SFX/Sound_Enemies/Virus";

        [MenuItem("Tools/Setup Virus Enemy & Animator")]
        public static void RunSetup()
        {
            AnimatorController ctrl = ConfigureVirusController();

            GameObject virusGO = GameObject.Find("virus");
            if (virusGO == null)
            {
                // Buscar si tiene otro nombre similar o por componente
                var existingAI = Object.FindFirstObjectByType<VirusAIController>();
                if (existingAI != null) virusGO = existingAI.gameObject;
            }

            if (virusGO != null)
            {
                SetupVirusGameObject(virusGO, ctrl);
                Debug.Log("<color=green>[SetupVirusEnemy]</color> ¡Objeto 'virus' configurado exitosamente con NavMeshAgent, Stats, AIController y Animator!");
            }
            else
            {
                Debug.LogWarning("[SetupVirusEnemy] No se encontró el GameObject 'virus' en la escena activa.");
            }

            SetupNavMeshInScene();
            EnsureAudioListener();

            if (GameObject.Find("Player_Linux") == null && Object.FindFirstObjectByType<LinuxSkeletonController>() == null)
            {
                Debug.LogWarning("[SetupVirusEnemy] No hay jugador en la escena: el Virus nunca detectará, atacará ni sonará. Añade el prefab Player_Linux.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Selection.activeObject = virusGO != null ? (Object)virusGO : ctrl;
        }

        public static AnimatorController ConfigureVirusController()
        {
            // Asegurar que existe el directorio
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Animations/Controllers"))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Animations", "Controllers");
            }
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Animations/Controllers/Enemies"))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Animations/Controllers", "Enemies");
            }

            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
            {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            // Cargar Clips
            AnimationClip idleClip = LoadClip(ClipsDir + "/Respiracion/StandingIdle.fbx", true);
            AnimationClip roarClip = LoadClip(ClipsDir + "/Observado/MutantRoaring.fbx", false);
            AnimationClip runClip = LoadClip(ClipsDir + "/Desplazamiento/ZombieRun.fbx", true);
            if (runClip == null) runClip = LoadClip(ClipsDir + "/Desplazamiento/StandingRunForward.fbx", true);
            AnimationClip crawlClip = LoadClip(ClipsDir + "/Suelo/ZombieCrawl.fbx", true);

            AnimationClip atk0 = LoadClip(ClipsDir + "/Ataques/MutantSwiping.fbx", false);
            AnimationClip atk1 = LoadClip(ClipsDir + "/Ataques/StandingMeleeAttackHorizontal.fbx", false);
            AnimationClip atk2 = LoadClip(ClipsDir + "/Ataques/desgarrar.fbx", false);

            AnimationClip hitClip = LoadClip(ClipsDir + "/RecibiendoDano/Standing_React_Large_From_Front.fbx", false);
            AnimationClip dieClip = LoadClip(ClipsDir + "/Muerte/StandingDeathForward.fbx", false);

            // Limpiar parámetros y estados antiguos
            for (int i = ctrl.parameters.Length - 1; i >= 0; i--) ctrl.RemoveParameter(i);
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);
            foreach (AnimatorStateTransition t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);

            // Parámetros
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsCrawling", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Roar", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            // Crear Estados Principales
            AnimatorState sIdle = sm.AddState("Idle", new Vector3(250f, 50f, 0f));
            sIdle.motion = idleClip;
            sm.defaultState = sIdle;

            AnimatorState sRoar = sm.AddState("Roar", new Vector3(250f, -50f, 0f));
            sRoar.motion = roarClip;

            AnimatorState sRun = sm.AddState("Run", new Vector3(500f, 50f, 0f));
            sRun.motion = runClip;

            AnimatorState sCrawl = sm.AddState("Crawl", new Vector3(500f, 150f, 0f));
            sCrawl.motion = crawlClip;

            // Transiciones Locomoción
            // Idle <-> Run
            var idleToRun = sIdle.AddTransition(sRun);
            idleToRun.hasExitTime = false;
            idleToRun.duration = 0.15f;
            idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            idleToRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrawling");

            var runToIdle = sRun.AddTransition(sIdle);
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0.15f;
            runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // Idle <-> Crawl
            var idleToCrawl = sIdle.AddTransition(sCrawl);
            idleToCrawl.hasExitTime = false;
            idleToCrawl.duration = 0.2f;
            idleToCrawl.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            idleToCrawl.AddCondition(AnimatorConditionMode.If, 0f, "IsCrawling");

            var crawlToIdle = sCrawl.AddTransition(sIdle);
            crawlToIdle.hasExitTime = false;
            crawlToIdle.duration = 0.2f;
            crawlToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // Run <-> Crawl (Cambio de postura al recibir mucho daño)
            var runToCrawl = sRun.AddTransition(sCrawl);
            runToCrawl.hasExitTime = false;
            runToCrawl.duration = 0.25f;
            runToCrawl.AddCondition(AnimatorConditionMode.If, 0f, "IsCrawling");

            var crawlToRun = sCrawl.AddTransition(sRun);
            crawlToRun.hasExitTime = false;
            crawlToRun.duration = 0.25f;
            crawlToRun.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrawling");

            // Roar
            var idleToRoar = sIdle.AddTransition(sRoar);
            idleToRoar.hasExitTime = false;
            idleToRoar.duration = 0.1f;
            idleToRoar.AddCondition(AnimatorConditionMode.If, 0f, "Roar");

            var roarToIdle = sRoar.AddTransition(sIdle);
            roarToIdle.hasExitTime = true;
            roarToIdle.exitTime = 0.9f;
            roarToIdle.duration = 0.2f;

            // Estados de Ataque
            AnimatorState sAtk0 = sm.AddState("Attack_Swipe", new Vector3(750f, -50f, 0f));
            sAtk0.motion = atk0;
            AnimatorState sAtk1 = sm.AddState("Attack_Slash", new Vector3(750f, 20f, 0f));
            sAtk1.motion = atk1;
            AnimatorState sAtk2 = sm.AddState("Attack_Tear", new Vector3(750f, 90f, 0f));
            sAtk2.motion = atk2;

            CreateAttackTransition(sm, sAtk0, sIdle, 0);
            CreateAttackTransition(sm, sAtk1, sIdle, 1);
            CreateAttackTransition(sm, sAtk2, sIdle, 2);

            // Estado de Daño (Hit)
            AnimatorState sHit = sm.AddState("Hit", new Vector3(500f, -120f, 0f));
            sHit.motion = hitClip;

            var anyToHit = sm.AddAnyStateTransition(sHit);
            anyToHit.hasExitTime = false;
            anyToHit.duration = 0.1f;
            anyToHit.canTransitionToSelf = false;
            anyToHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");

            var hitToIdle = sHit.AddTransition(sIdle);
            hitToIdle.hasExitTime = true;
            hitToIdle.exitTime = 0.85f;
            hitToIdle.duration = 0.15f;

            // Estado de Muerte (Death)
            AnimatorState sDie = sm.AddState("Death", new Vector3(750f, -120f, 0f));
            sDie.motion = dieClip;

            var anyToDie = sm.AddAnyStateTransition(sDie);
            anyToDie.hasExitTime = false;
            anyToDie.duration = 0.15f;
            anyToDie.canTransitionToSelf = false;
            anyToDie.AddCondition(AnimatorConditionMode.If, 0f, "Die");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green>[SetupVirusEnemy]</color> VirusController.controller configurado correctamente.");
            return ctrl;
        }

        private static void CreateAttackTransition(AnimatorStateMachine sm, AnimatorState atkState, AnimatorState returnState, int index)
        {
            var anyToAtk = sm.AddAnyStateTransition(atkState);
            anyToAtk.hasExitTime = false;
            anyToAtk.duration = 0.1f;
            anyToAtk.canTransitionToSelf = false;
            anyToAtk.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            anyToAtk.AddCondition(AnimatorConditionMode.Equals, index, "AttackIndex");

            var atkToReturn = atkState.AddTransition(returnState);
            atkToReturn.hasExitTime = true;
            atkToReturn.exitTime = 0.88f;
            atkToReturn.duration = 0.15f;
        }

        public static void SetupVirusGameObject(GameObject go, AnimatorController ctrl)
        {
            Undo.RegisterFullObjectHierarchyUndo(go, "Setup Virus Enemy");

            // 1. Animator
            Animator anim = go.GetComponentInChildren<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;

            // 2. NavMeshAgent
            NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
            if (agent == null) agent = go.AddComponent<NavMeshAgent>();
            agent.speed = 5.5f;
            agent.angularSpeed = 240f;
            agent.acceleration = 14f;
            agent.stoppingDistance = 1.8f;
            agent.radius = 0.45f;
            agent.height = 1.8f;
            agent.baseOffset = 0f;

            // 3. CapsuleCollider
            CapsuleCollider col = go.GetComponent<CapsuleCollider>();
            if (col == null) col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.radius = 0.45f;
            col.height = 1.8f;

            // 4. VirusStats
            VirusStats stats = go.GetComponent<VirusStats>();
            if (stats == null) stats = go.AddComponent<VirusStats>();

            // 5. VirusAudio
            VirusAudio vAudio = go.GetComponent<VirusAudio>();
            if (vAudio == null) vAudio = go.AddComponent<VirusAudio>();

            vAudio.SetClips(
                detection: LoadSfx("deteccion/biomechanical-burst-in-cathedral.wav"),
                normalMove: LoadSfx("movimiento/normal/sergenious__robo.wav"),
                crawlMove: LoadSfx("movimiento/suelo/iainmccurdy__buzzing-faulty-and-sparking-fluorescent-light.wav"),
                attacks: new[]
                {
                    LoadSfx("ataque/golpes/mirmaximus__sfx_outage_hit.wav"),
                    LoadSfx("ataque/zapatazos/tboneaudio__data-bolts.wav"),
                },
                hitDamage: LoadSfx("recibir_daño/timkahn__glitch_sd_03.wav"),
                death: LoadSfx("muerte/felixblume__impact-on-a-window.wav"));
            EditorUtility.SetDirty(vAudio);

            // 6. VirusAIController
            VirusAIController ai = go.GetComponent<VirusAIController>();
            if (ai == null) ai = go.AddComponent<VirusAIController>();

            EditorUtility.SetDirty(go);
        }

        private static AudioClip LoadSfx(string relativePath)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + "/" + relativePath);
            if (clip == null) Debug.LogError($"[SetupVirusEnemy] No se encontró el clip: {SfxDir}/{relativePath}");
            return clip;
        }

        /// <summary>Sin un AudioListener activo no se oye ningún sonido; se añade a la cámara principal si falta.</summary>
        private static void EnsureAudioListener()
        {
            if (Object.FindFirstObjectByType<AudioListener>() != null) return;

            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogWarning("[SetupVirusEnemy] No hay cámara ni AudioListener en la escena: no se oirá ningún sonido.");
                return;
            }

            Undo.AddComponent<AudioListener>(cam.gameObject);
            Debug.Log($"<color=green>[SetupVirusEnemy]</color> AudioListener añadido a '{cam.name}'.");
        }

        public static void SetupNavMeshInScene()
        {
            GameObject plane = GameObject.Find("Plane");
            if (plane == null) return;

            NavMeshSurface surface = plane.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = plane.AddComponent<NavMeshSurface>();
            }

            surface.collectObjects = CollectObjects.All;
            surface.BuildNavMesh();
            EditorUtility.SetDirty(plane);
            Debug.Log("<color=green>[SetupVirusEnemy]</color> NavMeshSurface construido en el Plane.");
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
            {
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            }
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
}
