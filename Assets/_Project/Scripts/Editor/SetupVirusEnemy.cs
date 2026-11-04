using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections.Generic;
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
        private const string ClipsDir = "Assets/_Project/Animations/Clips/Enemies_Animaciones";
        private const string SfxDir = "Assets/_Project/Audio/SFX/Enemies_Sound/Virus";
        private const string OptModelPath = "Assets/_Project/Models/Characters/virus-skeleton/virus_opt.fbx";
        private const string HeavyModelPath = "Assets/_Project/Models/Characters/virus-skeleton/virus.fbx";
        private const string OptName = "virus_opt";
        private const string LegacyName = "virus";

        // ---------------------------------------------------------------- Modelo optimizado

        /// <summary>El Virus de la escena (virus_opt o, si aún no se ha migrado, el pesado). Lo usan los demás Setup para colocarse a su lado.</summary>
        public static GameObject FindVirusInScene()
        {
            GameObject go = GameObject.Find(OptName);
            if (go == null) go = GameObject.Find(LegacyName);
            if (go == null)
            {
                var ai = Object.FindFirstObjectByType<VirusAIController>();
                if (ai != null) go = ai.gameObject;
            }
            return go;
        }

        /// <summary>
        /// Sustituye cada Virus que no sea virus_opt.fbx (instancias de virus.fbx o cualquier objeto con VirusStats/VirusAIController)
        /// por una instancia de virus_opt en el mismo sitio, y devuelve todas las instancias optimizadas de la escena.
        /// Si no hay ningún Virus, instancia uno en el origen.
        /// </summary>
        private static List<GameObject> FindOrCreateOptimizedViruses()
        {
            var result = new List<GameObject>();
            GameObject optModel = AssetDatabase.LoadAssetAtPath<GameObject>(OptModelPath);
            if (optModel == null)
            {
                Debug.LogError($"[SetupVirusEnemy] No se encontró el modelo optimizado en {OptModelPath}.");
                return result;
            }

            foreach (GameObject go in FindAllVirusRoots())
            {
                result.Add(IsInstanceOf(go, OptModelPath) ? go : ReplaceWithOptimized(go, optModel));
            }

            if (result.Count == 0)
            {
                GameObject optGO = (GameObject)PrefabUtility.InstantiatePrefab(optModel, EditorSceneManager.GetActiveScene());
                optGO.name = OptName;
                Undo.RegisterCreatedObjectUndo(optGO, "Create virus_opt");
                Debug.Log($"<color=green>[SetupVirusEnemy]</color> No había Virus en la escena: '{OptName}' instanciado en el origen.");
                result.Add(optGO);
            }
            return result;
        }

        /// <summary>Raíces de Virus en la escena activa: instancias de virus.fbx / virus_opt.fbx y objetos con componentes de Virus.</summary>
        private static List<GameObject> FindAllVirusRoots()
        {
            var roots = new List<GameObject>();
            void Add(GameObject go)
            {
                if (go != null && go.scene == EditorSceneManager.GetActiveScene() && !roots.Contains(go)) roots.Add(go);
            }

            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (IsInstanceOf(t.gameObject, HeavyModelPath) || IsInstanceOf(t.gameObject, OptModelPath)) Add(t.gameObject);
            }
            foreach (VirusStats s in Object.FindObjectsByType<VirusStats>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Add(s.gameObject);
            foreach (VirusAIController ai in Object.FindObjectsByType<VirusAIController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Add(ai.gameObject);
            return roots;
        }

        /// <summary>
        /// Instancia virus_opt en lugar de 'old' (mismo padre, orden, posición, rotación y escala), le traspasa los valores
        /// de VirusStats, VirusAudio, VirusAIController, NavMeshAgent y CapsuleCollider y borra 'old' (con Undo).
        /// </summary>
        private static GameObject ReplaceWithOptimized(GameObject old, GameObject optModel)
        {
            GameObject optGO = (GameObject)PrefabUtility.InstantiatePrefab(optModel, old.scene);
            optGO.name = OptName;
            Undo.RegisterCreatedObjectUndo(optGO, "Create virus_opt");

            optGO.transform.SetParent(old.transform.parent, false);
            optGO.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
            optGO.transform.localPosition = old.transform.localPosition;
            optGO.transform.localRotation = old.transform.localRotation;
            optGO.transform.localScale = old.transform.localScale;
            optGO.SetActive(old.activeSelf);

            TransferComponents(old, optGO);

            string source = IsInstanceOf(old, HeavyModelPath) ? "virus.fbx" : "modelo no optimizado";
            Debug.Log($"<color=green>[SetupVirusEnemy]</color> '{old.name}' ({source}) reemplazado por '{OptName}' (virus_opt.fbx); valores de componentes traspasados.");
            Undo.DestroyObjectImmediate(old);
            return optGO;
        }

        private static bool IsInstanceOf(GameObject go, string modelPath)
        {
            return PrefabUtility.IsAnyPrefabInstanceRoot(go)
                && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == modelPath;
        }

        /// <summary>
        /// Copia los valores del inspector de los componentes del Virus. Las referencias que apuntaban a componentes
        /// del objeto viejo se redirigen a los equivalentes del nuevo. El Animator NO se copia: virus_opt usa su propio Avatar.
        /// </summary>
        private static void TransferComponents(GameObject from, GameObject to)
        {
            System.Type[] types =
            {
                typeof(NavMeshAgent), typeof(CapsuleCollider), typeof(VirusStats), typeof(VirusAudio), typeof(VirusAIController),
            };

            foreach (System.Type type in types)
            {
                Component src = from.GetComponent(type);
                Component dst = to.GetComponent(type);
                if (dst == null) dst = to.AddComponent(type); // VirusAIController añade sus RequireComponent si faltan
                if (src != null) EditorUtility.CopySerialized(src, dst);
            }

            foreach (System.Type type in types) RemapReferences(to.GetComponent(type), from, to);
        }

        private static void RemapReferences(Component comp, GameObject from, GameObject to)
        {
            var so = new SerializedObject(comp);
            SerializedProperty p = so.GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null) continue;
                if (!(p.objectReferenceValue is Component refComp) || !refComp.transform.IsChildOf(from.transform)) continue;

                // Animator (en hijos) → el del modelo nuevo; el resto → mismo tipo en la raíz nueva (o null, y Awake lo resuelve).
                p.objectReferenceValue = refComp is Animator
                    ? to.GetComponentInChildren<Animator>()
                    : to.GetComponent(refComp.GetType());
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/Setup Virus Enemy & Animator")]
        public static void RunSetup()
        {
            AnimatorController ctrl = ConfigureVirusController();

            List<GameObject> viruses = FindOrCreateOptimizedViruses();
            foreach (GameObject v in viruses)
            {
                SetupVirusGameObject(v, ctrl);
                Debug.Log($"<color=green>[SetupVirusEnemy]</color> '{v.name}' (virus_opt.fbx) configurado con NavMeshAgent, Stats, Audio, AIController y Animator.");
            }
            GameObject virusGO = viruses.Count > 0 ? viruses[0] : null;

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

        internal static void CreateAttackTransition(AnimatorStateMachine sm, AnimatorState atkState, AnimatorState returnState, int index)
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

            // Barra de vida World Space sobre la cabeza (herramienta compartida, altura según el tipo de enemigo).
            SetupEnemyHealthBars.Ensure(go.GetComponent<SudoKill9.Enemies.EnemyStatsBase>());
            // VFX de muerte, polvo/propulsión e impacto (herramienta compartida, color según el villano).
            SetupEnemyVFX.Ensure(go.GetComponent<SudoKill9.Enemies.EnemyStatsBase>());

            EditorUtility.SetDirty(go);
        }

        private static AudioClip LoadSfx(string relativePath)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + "/" + relativePath);
            if (clip == null) Debug.LogError($"[SetupVirusEnemy] No se encontró el clip: {SfxDir}/{relativePath}");
            return clip;
        }

        /// <summary>Sin un AudioListener activo no se oye ningún sonido; se añade a la cámara principal si falta.</summary>
        internal static void EnsureAudioListener()
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

        internal static AnimationClip LoadClip(string fbxPath, bool loop)
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
