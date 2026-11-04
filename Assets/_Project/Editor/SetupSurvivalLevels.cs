using System.Collections.Generic;
using System.IO;
using System.Linq;
using SudoKill9.Levels;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Genera Nivel_1 y Nivel_2 a partir de Mapa_completo:
    /// 1. Copia la escena y deja solo el entorno ('Map') y al jugador ya montado (cámara, IK, combate).
    /// 2. Crea SpawnPoint_Start y coloca allí a Linux.
    /// 3. Añade un NavMeshSurface al mapa, lo hornea y guarda el NavMeshData junto a la escena.
    /// 4. Reparte puntos de aparición sobre el NavMesh.
    /// 5. Crea el 'LevelController' (SurvivalLevelController + SurvivalLevelHUD) con el preset de cada nivel.
    /// 6. Registra ambas escenas en EditorBuildSettings.
    /// Los textos del HUD son claves de la String Table 'Gameplay' (ver GameplayLocalizationBuilder).
    /// Volver a ejecutarlo regenera las escenas desde cero (pide confirmación).
    /// </summary>
    public static class SetupSurvivalLevels
    {
        private const string SourceScene = "Assets/_Project/Scenes/GamePlay/Mapa_completo.unity";
        private const string ScenesDir = "Assets/_Project/Scenes/GamePlay";
        private const string EnemiesDir = "Assets/_Project/Prefabs/Characters/Enemies";
        private const string FontPath = "Assets/_Project/Fonts/PixelTactical_Point.asset";
        private const string MapRootName = "Map";
        private const int SpawnPointCount = 12;

        private static readonly Color SpawnMarkerColor = new Color(0.95f, 0.3f, 0.2f, 0.85f);

        [MenuItem("Tools/Survival Levels/Build Nivel_1 + Nivel_2")]
        public static void BuildBoth()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            GameplayLocalizationBuilder.FillTable(); // los avisos de las hordas son claves de esta tabla

            bool exists = File.Exists(ScenePath("Nivel_1")) || File.Exists(ScenePath("Nivel_2"));
            if (exists && !EditorUtility.DisplayDialog("Survival Levels",
                    "Nivel_1 / Nivel_2 ya existen. ¿Regenerarlas desde Mapa_completo? Se perderán los cambios manuales.",
                    "Regenerar", "Cancelar"))
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Survival Levels", "Nivel_1…", 0.1f);
                BuildLevel("Nivel_1", ConfigureLevel1);
                EditorUtility.DisplayProgressBar("Survival Levels", "Nivel_2…", 0.6f);
                BuildLevel("Nivel_2", ConfigureLevel2);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorSceneManager.OpenScene(ScenePath("Nivel_1"));
            Debug.Log("<color=green>[SetupSurvivalLevels]</color> Nivel_1 y Nivel_2 generados y registrados en Build Settings.");
        }

        [MenuItem("Tools/Survival Levels/Rebake NavMesh (escena abierta)")]
        public static void RebakeOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            var map = FindRoot(scene, MapRootName);
            if (map == null) { Debug.LogError("[SetupSurvivalLevels] No hay un objeto raíz 'Map' en la escena abierta."); return; }
            BakeNavMesh(scene, map);
            SnapSpawnPointsToNavMesh();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // ------------------------------------------------------------------ Construcción de escena

        private static string ScenePath(string name) => $"{ScenesDir}/{name}.unity";

        private static void BuildLevel(string sceneName, System.Action<SerializedObject> configure)
        {
            string path = ScenePath(sceneName);
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            if (!AssetDatabase.CopyAsset(SourceScene, path))
            {
                Debug.LogError($"[SetupSurvivalLevels] No se pudo copiar {SourceScene} → {path}");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameObject map = FindRoot(scene, MapRootName);
            var playerIK = Object.FindFirstObjectByType<LinuxCombatIK>();
            GameObject player = playerIK != null ? playerIK.gameObject : FindRoot(scene, "Player_Linux");
            if (map == null || player == null)
            {
                Debug.LogError($"[SetupSurvivalLevels] '{sceneName}': falta 'Map' o el jugador en la escena base.");
                return;
            }

            // Solo entorno + jugador: fuera el resto de raíces (p. ej. el modelo de Windows del jefe).
            GameObject playerRoot = player.transform.root.gameObject;
            foreach (var root in scene.GetRootGameObjects())
                if (root != map && root != playerRoot) Object.DestroyImmediate(root);

            // Organización
            var levelRoot = new GameObject("--- LEVEL ---").transform;

            var start = new GameObject("SpawnPoint_Start");
            start.transform.SetParent(levelRoot, false);
            start.transform.SetPositionAndRotation(playerRoot.transform.position, playerRoot.transform.rotation);
            start.AddComponent<SpawnPointMarker>();

            BakeNavMesh(scene, map);

            // Linux sobre el NavMesh en el punto de inicio
            if (NavMesh.SamplePosition(start.transform.position, out NavMeshHit startHit, 5f, NavMesh.AllAreas))
                start.transform.position = startHit.position;
            playerRoot.transform.position = start.transform.position;

            Transform[] spawnPoints = CreateSpawnPoints(levelRoot, map, start.transform.position);

            var controllerGO = new GameObject("LevelController");
            controllerGO.transform.SetParent(levelRoot, false);
            var controller = controllerGO.AddComponent<SurvivalLevelController>();
            var hud = controllerGO.AddComponent<SurvivalLevelHUD>();

            var hudSO = new SerializedObject(hud);
            hudSO.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            hudSO.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(controller);
            so.FindProperty("player").objectReferenceValue = player.transform;
            SetObjectArray(so.FindProperty("spawnPoints"), spawnPoints);
            configure(so);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings(path);
        }

        private static GameObject FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);

        private static void BakeNavMesh(Scene scene, GameObject map)
        {
            var surface = map.GetComponent<NavMeshSurface>();
            if (surface == null) surface = map.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children; // solo el entorno, no el jugador
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.BuildNavMesh();

            if (surface.navMeshData == null)
            {
                Debug.LogError("[SetupSurvivalLevels] El horneado del NavMesh no produjo datos.");
                return;
            }

            // Persistir el NavMeshData como asset junto a la escena (si no, se pierde al recargar).
            string dir = Path.Combine(Path.GetDirectoryName(scene.path) ?? ScenesDir, scene.name).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Path.GetDirectoryName(dir).Replace('\\', '/'), scene.name);
            string assetPath = $"{dir}/NavMesh-{map.name}.asset";
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
            EditorUtility.SetDirty(surface);
        }

        /// <summary>Anillos de puntos repartidos por el mapa, ajustados al NavMesh y lejos del inicio.</summary>
        private static Transform[] CreateSpawnPoints(Transform levelRoot, GameObject map, Vector3 start)
        {
            Bounds bounds = GetMapBounds(map);
            var parent = new GameObject("SurvivalSpawnPoints").transform;
            parent.SetParent(levelRoot, false);

            float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
            var points = new List<Transform>();
            for (int i = 0; i < SpawnPointCount; i++)
            {
                // Anillo exterior (pares) e interior (impares), desfasados
                bool outer = i % 2 == 0;
                float angle = (i / (float)SpawnPointCount) * Mathf.PI * 2f + (outer ? 0f : Mathf.PI / SpawnPointCount);
                float r = radius * (outer ? 0.8f : 0.45f);
                Vector3 p = bounds.center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
                p.y = bounds.center.y;

                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, radius * 0.25f, NavMesh.AllAreas)) continue;
                if ((hit.position - start).sqrMagnitude < 12f * 12f) continue; // no encima del inicio

                var sp = new GameObject($"EnemySpawn_{points.Count:00}");
                sp.transform.SetParent(parent, false);
                sp.transform.position = hit.position;
                var marker = new SerializedObject(sp.AddComponent<SpawnPointMarker>());
                marker.FindProperty("markerColor").colorValue = SpawnMarkerColor;
                marker.ApplyModifiedPropertiesWithoutUndo();
                points.Add(sp.transform);
            }

            if (points.Count == 0) Debug.LogWarning("[SetupSurvivalLevels] No se pudo colocar ningún spawn point sobre el NavMesh; se usará solo el anillo alrededor del jugador.");
            return points.ToArray();
        }

        private static void SnapSpawnPointsToNavMesh()
        {
            var parent = GameObject.Find("SurvivalSpawnPoints");
            if (parent == null) return;
            foreach (Transform sp in parent.transform)
                if (NavMesh.SamplePosition(sp.position, out NavMeshHit hit, 8f, NavMesh.AllAreas)) sp.position = hit.position;
        }

        private static Bounds GetMapBounds(GameObject map)
        {
            var plane = map.transform.Find("Plane");
            var renderers = plane != null
                ? plane.GetComponentsInChildren<Renderer>()
                : map.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return new Bounds(map.transform.position, new Vector3(100f, 1f, 100f));

            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ Presets de nivel

        private static GameObject Enemy(string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemiesDir}/{name}.prefab");
            if (go == null) Debug.LogError($"[SetupSurvivalLevels] Falta el prefab {EnemiesDir}/{name}.prefab");
            return go;
        }

        /// <summary>
        /// Nivel 1 (40 s): solo Virus y Espía. Apariciones sueltas; en los últimos 12 s
        /// sube la frecuencia y llegan patrullas de Virus con Espías cubriendo desde atrás.
        /// </summary>
        private static void ConfigureLevel1(SerializedObject so)
        {
            const float duration = 40f, finalStretch = 12f;
            float final = 1f - finalStretch / duration; // inicio del tramo final (0.7)
            GameObject virus = Enemy("virus_opt"), espia = Enemy("espia_opt");

            so.FindProperty("levelNumber").intValue = 1;
            so.FindProperty("duration").floatValue = duration;
            so.FindProperty("finalStretchSeconds").floatValue = finalStretch;
            so.FindProperty("victoryCondition").enumValueIndex = (int)VictoryCondition.SurviveTimer;
            so.FindProperty("completedMessageKey").stringValue = GameplayKeys.LevelCompleted;
            so.FindProperty("nextSceneName").stringValue = "Nivel_2";

            SetPool(so.FindProperty("enemyPool"),
                ("Virus", virus, 3f, 0f, SpawnPlacement.AroundPlayer),
                ("Espia", espia, 1.5f, 0.15f, SpawnPlacement.AroundPlayer));

            so.FindProperty("spawnInterval").animationCurveValue = Linear((0f, 3.5f), (0.4f, 2.8f), (final, 2.2f), (final + 0.01f, 1.6f), (1f, 1f));
            so.FindProperty("batchSize").animationCurveValue = Linear((0f, 1f), (final, 1f), (final + 0.01f, 2f), (1f, 3f));
            so.FindProperty("maxAlive").animationCurveValue = Linear((0f, 3f), (final, 6f), (1f, 10f));

            SetWaves(so.FindProperty("scriptedWaves"),
                Wave("Virus patrol", 26f, SpawnPlacement.AroundPlayer, 1, GameplayKeys.WaveVirusPatrol,
                    (virus, 3, 0f), (espia, 1, 6f)),
                Wave("Pincer patrol", 16f, SpawnPlacement.AroundPlayer, 2, GameplayKeys.WaveMultiplePatrols,
                    (virus, 4, 0f), (espia, 2, 7f)),
                Wave("Last stand", 7f, SpawnPlacement.BehindPlayer, 2, GameplayKeys.WaveHoldOn,
                    (virus, 4, 0f), (espia, 2, 6f)));
        }

        /// <summary>
        /// Nivel 2 (60 s): los cuatro enemigos. Goteo mixto + hordas coordinadas (Escudo bloqueando el avance,
        /// Voladores desde el aire, Espías cubriendo) y una oleada final masiva que hay que limpiar para ganar.
        /// </summary>
        private static void ConfigureLevel2(SerializedObject so)
        {
            const float duration = 60f;
            GameObject virus = Enemy("virus_opt"), espia = Enemy("espia_opt"),
                       escudo = Enemy("escudo_opt"), volador = Enemy("volador_opt");

            so.FindProperty("levelNumber").intValue = 2;
            so.FindProperty("duration").floatValue = duration;
            so.FindProperty("finalStretchSeconds").floatValue = 15f;
            so.FindProperty("victoryCondition").enumValueIndex = (int)VictoryCondition.SurviveTimerThenClear;
            so.FindProperty("completedMessageKey").stringValue = GameplayKeys.LevelCompleted;
            so.FindProperty("nextSceneName").stringValue = "Mapa_completo";
            so.FindProperty("fallbackSceneName").stringValue = "Inicio";

            SetPool(so.FindProperty("enemyPool"),
                ("Virus", virus, 3f, 0f, SpawnPlacement.AroundPlayer),
                ("Espia", espia, 2f, 0f, SpawnPlacement.AroundPlayer),
                ("Escudo", escudo, 1.2f, 0.15f, SpawnPlacement.AheadOfPlayer),
                ("Volador", volador, 1.5f, 0.25f, SpawnPlacement.AroundPlayer));

            so.FindProperty("spawnInterval").animationCurveValue = Linear((0f, 3.5f), (0.5f, 2.2f), (0.8f, 1.5f), (1f, 1f));
            so.FindProperty("batchSize").animationCurveValue = Linear((0f, 1f), (0.5f, 2f), (1f, 3f));
            so.FindProperty("maxAlive").animationCurveValue = Linear((0f, 5f), (0.6f, 10f), (1f, 14f));

            SetWaves(so.FindProperty("scriptedWaves"),
                Wave("Shield wall", 50f, SpawnPlacement.AheadOfPlayer, 1, GameplayKeys.WaveShieldWall,
                    (escudo, 2, 0f), (virus, 3, 0f)),
                Wave("Air raid", 40f, SpawnPlacement.AroundPlayer, 2, GameplayKeys.WaveAirRaid,
                    (volador, 3, 0f), (espia, 2, 6f)),
                Wave("Pincer", 29f, SpawnPlacement.AroundPlayer, 2, GameplayKeys.WaveFlanked,
                    (virus, 4, 0f), (escudo, 1, 0f), (volador, 2, 0f)),
                Wave("Siege", 18f, SpawnPlacement.AheadOfPlayer, 2, GameplayKeys.WaveSiege,
                    (escudo, 2, 0f), (espia, 3, 6f), (volador, 2, 0f)),
                Wave("Final wave", 7f, SpawnPlacement.AroundPlayer, 3, GameplayKeys.WaveFinal,
                    (virus, 8, 0f), (escudo, 3, 0f), (volador, 4, 0f), (espia, 4, 6f)));
        }

        // ------------------------------------------------------------------ Helpers de SerializedProperty

        private struct WaveDef
        {
            public string label, announcement;
            public float secondsBeforeEnd;
            public SpawnPlacement placement;
            public int entryPoints;
            public (GameObject prefab, int count, float extra)[] groups;
        }

        private static WaveDef Wave(string label, float secondsBeforeEnd, SpawnPlacement placement, int entryPoints,
            string announcement, params (GameObject, int, float)[] groups) => new WaveDef
        {
            label = label, secondsBeforeEnd = secondsBeforeEnd, placement = placement,
            entryPoints = entryPoints, announcement = announcement, groups = groups
        };

        private static void SetWaves(SerializedProperty arr, params WaveDef[] waves)
        {
            arr.arraySize = waves.Length;
            for (int i = 0; i < waves.Length; i++)
            {
                var w = waves[i];
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("label").stringValue = w.label;
                e.FindPropertyRelative("secondsBeforeEnd").floatValue = w.secondsBeforeEnd;
                e.FindPropertyRelative("placement").enumValueIndex = (int)w.placement;
                e.FindPropertyRelative("entryPoints").intValue = w.entryPoints;
                e.FindPropertyRelative("memberDelay").floatValue = w.groups.Sum(g => g.count) > 12 ? 0.15f : 0.25f;
                e.FindPropertyRelative("announcementKey").stringValue = w.announcement;

                var groups = e.FindPropertyRelative("groups");
                groups.arraySize = w.groups.Length;
                for (int g = 0; g < w.groups.Length; g++)
                {
                    var ge = groups.GetArrayElementAtIndex(g);
                    ge.FindPropertyRelative("prefab").objectReferenceValue = w.groups[g].prefab;
                    ge.FindPropertyRelative("count").intValue = w.groups[g].count;
                    ge.FindPropertyRelative("extraDistance").floatValue = w.groups[g].extra;
                }
            }
        }

        private static void SetPool(SerializedProperty arr, params (string label, GameObject prefab, float weight, float unlock, SpawnPlacement placement)[] entries)
        {
            arr.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("label").stringValue = entries[i].label;
                e.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
                e.FindPropertyRelative("weight").floatValue = entries[i].weight;
                e.FindPropertyRelative("unlockAtProgress").floatValue = entries[i].unlock;
                e.FindPropertyRelative("placement").enumValueIndex = (int)entries[i].placement;
            }
        }

        private static void SetObjectArray(SerializedProperty arr, Object[] values)
        {
            arr.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        /// <summary>Curva con tangentes lineales (escalones nítidos al entrar en el tramo final).</summary>
        private static AnimationCurve Linear(params (float t, float v)[] keys)
        {
            var curve = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }
    }
}
