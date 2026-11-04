using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using SudoKill9.Enemies.Volador;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Configuración con 1-Click del Volador (mismo patrón que SetupVirusEnemy):
    /// 1. Genera/Actualiza VoladorController.controller con los 3 clips dedicados (vuelo, descenso/ataque, muerte aérea).
    /// 2. Busca 'volador_opt' en la escena activa (o lo instancia junto al Virus si no existe).
    /// 3. Añade Animator, NavMeshAgent con altura de vuelo, BoxCollider ajustado a la malla,
    ///    VoladorStats, VoladorAudio (AudioSources 3D + clips) y VoladorAIController.
    /// </summary>
    public static class SetupVoladorEnemy
    {
        private const string GameObjectName = "volador_opt";
        private const string ModelPath = "Assets/_Project/Models/Characters/volador-skeleton/volador_opt.fbx";
        private const string ControllerPath = "Assets/_Project/Animations/Controllers/Enemies/VoladorController.controller";
        private const string VoladorClipsDir = "Assets/_Project/Animations/Clips/Enemies_Animaciones/volador";
        private const string FlyClipPath = VoladorClipsDir + "/Flying.fbx";                 // vuelo continuo
        private const string DiveAttackClipPath = VoladorClipsDir + "/Stylish Flip.fbx";     // descenso y ataque
        private const string AirDeathClipPath = VoladorClipsDir + "/Falling Flat Impact.fbx"; // muerte aérea
        private const string SfxDir = "Assets/_Project/Audio/SFX";

        [MenuItem("Tools/Setup Volador Enemy & Animator")]
        public static void RunSetup()
        {
            AnimatorController ctrl = ConfigureVoladorController();

            GameObject go = FindOrCreateVolador();
            if (go == null)
            {
                Debug.LogError($"[SetupVoladorEnemy] No se pudo encontrar ni instanciar '{GameObjectName}' ({ModelPath}).");
                return;
            }

            SetupVoladorGameObject(go, ctrl);
            Debug.Log($"<color=green>[SetupVoladorEnemy]</color> '{go.name}' configurado con Animator, NavMeshAgent, BoxCollider, Stats, Audio y AIController.");

            // El NavMesh ya lo hornea SetupVirusEnemy; solo se crea si la escena no tiene ninguno.
            if (Object.FindFirstObjectByType<NavMeshSurface>() == null) SetupVirusEnemy.SetupNavMeshInScene();
            SetupVirusEnemy.EnsureAudioListener();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeObject = go;
        }

        public static AnimatorController ConfigureVoladorController()
        {
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Clips dedicados del Volador (Mixamo). Se importan como Humanoid para retargetear al avatar de volador_opt.
            EnsureHumanoid(FlyClipPath);
            EnsureHumanoid(DiveAttackClipPath);
            EnsureHumanoid(AirDeathClipPath);
            AnimationClip flyClip = SetupVirusEnemy.LoadClip(FlyClipPath, true);
            AnimationClip diveAttackClip = SetupVirusEnemy.LoadClip(DiveAttackClipPath, false);
            AnimationClip airDeathClip = SetupVirusEnemy.LoadClip(AirDeathClipPath, false);
            if (flyClip == null || diveAttackClip == null || airDeathClip == null)
            {
                Debug.LogError($"[SetupVoladorEnemy] Faltan clips en {VoladorClipsDir} (Flying, Stylish Flip, Falling Flat Impact).");
            }

            // Limpiar parámetros y estados antiguos
            for (int i = ctrl.parameters.Length - 1; i >= 0; i--) ctrl.RemoveParameter(i);
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);
            foreach (AnimatorStateTransition t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);

            // Mismos parámetros que el Virus: EnemyAIBase los escribe todos aunque el Volador no use
            // IsCrawling/Roar/Hit en el grafo (si faltaran, el Animator avisaría cada frame).
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsCrawling", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Roar", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            // Vuelo continuo: estado por defecto para reposo, alerta, patrulla y persecución.
            AnimatorState sFly = sm.AddState("Flying", new Vector3(300f, 50f, 0f));
            sFly.motion = flyClip;
            sm.defaultState = sFly;

            // Descenso + ataque: Any State → Dive_Attack (Attack, AttackIndex 0) → vuelve a Flying al terminar.
            AnimatorState sDive = sm.AddState("Dive_Attack", new Vector3(600f, -30f, 0f));
            sDive.motion = diveAttackClip;
            SetupVirusEnemy.CreateAttackTransition(sm, sDive, sFly, 0);

            // Muerte aérea: Any State → Air_Death al recibir daño letal (trigger Die). Sin salida.
            AnimatorState sDie = sm.AddState("Air_Death", new Vector3(600f, 130f, 0f));
            sDie.motion = airDeathClip;

            var anyToDie = sm.AddAnyStateTransition(sDie);
            anyToDie.hasExitTime = false;
            anyToDie.duration = 0.1f;
            anyToDie.canTransitionToSelf = false;
            anyToDie.AddCondition(AnimatorConditionMode.If, 0f, "Die");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green>[SetupVoladorEnemy]</color> VoladorController.controller configurado (Flying, Dive_Attack, Air_Death).");
            return ctrl;
        }

        /// <summary>Los FBX de Mixamo llegan como Generic; el avatar de volador_opt es Humanoid y solo retargetea clips Humanoid.</summary>
        private static void EnsureHumanoid(string fbxPath)
        {
            if (!(AssetImporter.GetAtPath(fbxPath) is ModelImporter importer))
            {
                Debug.LogError($"[SetupVoladorEnemy] No se encontró el modelo: {fbxPath}");
                return;
            }
            if (importer.animationType == ModelImporterAnimationType.Human) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            Debug.Log($"<color=green>[SetupVoladorEnemy]</color> '{fbxPath}' reimportado como Humanoid.");
        }

        private static GameObject FindOrCreateVolador()
        {
            GameObject go = GameObject.Find(GameObjectName);
            if (go != null) return go;

            var existingAI = Object.FindFirstObjectByType<VoladorAIController>();
            if (existingAI != null) return existingAI.gameObject;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) return null;

            go = (GameObject)PrefabUtility.InstantiatePrefab(model, EditorSceneManager.GetActiveScene());
            go.name = GameObjectName;

            // Junto al Virus si existe, para probarlos juntos.
            GameObject virus = SetupVirusEnemy.FindVirusInScene();
            if (virus != null) go.transform.position = virus.transform.position + virus.transform.right * 4f;

            Undo.RegisterCreatedObjectUndo(go, "Create Volador Enemy");
            Debug.Log($"<color=green>[SetupVoladorEnemy]</color> '{GameObjectName}' instanciado en la escena.");
            return go;
        }

        public static void SetupVoladorGameObject(GameObject go, AnimatorController ctrl)
        {
            Undo.RegisterFullObjectHierarchyUndo(go, "Setup Volador Enemy");

            // Tamaño de la malla en espacio local (antes de añadir colliders).
            Bounds local = GetLocalMeshBounds(go);

            // 1. Animator
            Animator anim = go.GetComponentInChildren<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;

            // 2. NavMeshAgent: navega por el suelo y el baseOffset lo eleva a la altura de vuelo.
            NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
            if (agent == null) agent = go.AddComponent<NavMeshAgent>();
            agent.speed = 4.2f;
            agent.angularSpeed = 180f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 2.7f;
            agent.radius = Mathf.Clamp(Mathf.Max(local.size.x, local.size.z) * 0.5f, 0.3f, 2f);
            agent.height = Mathf.Max(local.size.y, 0.5f);

            // 3. BoxCollider ajustado a la malla (la máquina no tiene forma de cápsula).
            BoxCollider col = go.GetComponent<BoxCollider>();
            if (col == null) col = go.AddComponent<BoxCollider>();
            col.center = local.center;
            col.size = local.size;

            // 4. VoladorStats (Reset() fija vida/daño/cooldown al añadirlo)
            if (go.GetComponent<VoladorStats>() == null) go.AddComponent<VoladorStats>();

            // 5. VoladorAudio + AudioSources 3D (volumen 1, rolloff lineal 4–40 m)
            VoladorAudio vAudio = go.GetComponent<VoladorAudio>();
            if (vAudio == null) vAudio = go.AddComponent<VoladorAudio>();
            SetupAudioSources(vAudio);

            vAudio.SetClips(
                detection: LoadSfx("mecanica/power06.wav"),
                normalMove: LoadSfx("mecanica/electrical-computer-noise.flac"),
                crawlMove: null, // no se arrastra
                attacks: new[] { LoadSfx("intense-punch.wav") }, // 0: impacto del descenso
                // landingAttackClip (sonido del descenso) se asigna a mano en el inspector de VoladorAudio.
                hitDamage: LoadSfx("mecanica/error-sound-design-2.wav"),
                death: LoadSfx("mecanica/8-bit-explosion.wav"));
            EditorUtility.SetDirty(vAudio);

            // 6. VoladorAIController (Reset() fija rangos y velocidades al añadirlo)
            VoladorAIController ai = go.GetComponent<VoladorAIController>();
            if (ai == null) ai = go.AddComponent<VoladorAIController>();

            // La altura de vuelo vive solo en VoladorAIController.flightHeight; el agente la refleja en la escena.
            agent.baseOffset = ai.FlightHeight;

            // Barra de vida World Space sobre la cabeza (herramienta compartida, altura según el tipo de enemigo).
            SetupEnemyHealthBars.Ensure(go.GetComponent<SudoKill9.Enemies.EnemyStatsBase>());
            // VFX de muerte, polvo/propulsión e impacto (herramienta compartida, color según el villano).
            SetupEnemyVFX.Ensure(go.GetComponent<SudoKill9.Enemies.EnemyStatsBase>());

            EditorUtility.SetDirty(go);
        }

        /// <summary>Crea (o reutiliza) los dos AudioSources y los enlaza a VoladorAudio para que se vean en el inspector.</summary>
        private static void SetupAudioSources(VoladorAudio vAudio)
        {
            var so = new SerializedObject(vAudio);
            SerializedProperty effectsProp = so.FindProperty("effectsSource");
            SerializedProperty movementProp = so.FindProperty("movementSource");

            AudioSource effects = effectsProp.objectReferenceValue as AudioSource;
            AudioSource movement = movementProp.objectReferenceValue as AudioSource;
            AudioSource[] existing = vAudio.GetComponents<AudioSource>();
            if (effects == null) effects = existing.Length > 0 ? existing[0] : vAudio.gameObject.AddComponent<AudioSource>();
            if (movement == null || movement == effects)
            {
                movement = existing.Length > 1 ? existing[1] : vAudio.gameObject.AddComponent<AudioSource>();
            }

            Configure3DSource(effects, false);
            Configure3DSource(movement, true);

            effectsProp.objectReferenceValue = effects;
            movementProp.objectReferenceValue = movement;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Configure3DSource(AudioSource source, bool loop)
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

        /// <summary>Caja que envuelve todas las mallas del modelo, en el espacio local de la raíz.</summary>
        private static Bounds GetLocalMeshBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(new Vector3(0f, 0.75f, 0f), new Vector3(1.5f, 1.5f, 1.5f));

            Bounds world = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);

            // Se transforman las 8 esquinas para que funcione aunque la raíz del FBX esté rotada o escalada.
            Transform t = go.transform;
            Bounds local = new Bounds(t.InverseTransformPoint(world.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = world.center + Vector3.Scale(world.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                local.Encapsulate(t.InverseTransformPoint(corner));
            }
            return local;
        }

        private static AudioClip LoadSfx(string relativePath)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + "/" + relativePath);
            if (clip == null) Debug.LogError($"[SetupVoladorEnemy] No se encontró el clip: {SfxDir}/{relativePath}");
            return clip;
        }
    }
}
