using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SudoKill9.Enemies;
using SudoKill9.Levels;

/// <summary>
/// Tools > SudoKill9 > Synchronize Linux and Damage in Levels 1 and 2
///
/// 1. Player_Linux.prefab: quita componentes con script perdido (bloqueaban el guardado), cambia la MALLA
///    de linux.fbx (74 MB) a linux_opt.fbx con los huesos remapeados por nombre, y quita cualquier tag EditorOnly.
///    El Avatar se queda en linux.fbx a propósito: el de linux_opt tiene un nodo 'Armature' sobre Hips que el
///    esqueleto del prefab no tiene, y el Animator humanoide no encontraría los huesos (Linux se deslizaría sin animar).
///    En la build solo entra el sub-asset Avatar (KB), no la malla de linux.fbx.
/// 2. Mapa_completo, Nivel_1, Nivel_2: revierte los overrides de malla/avatar/tag de la instancia para que hereden
///    del prefab (el tag EditorOnly de 'Model' eliminaba a Linux en las builds), y comprueba antes/después que arma,
///    MuzzlePoint, CharacterController y offset del modelo no cambian.
/// 3. Nivel_1 / Nivel_2: si falta el jugador se copia desde Mapa_completo; se coloca en SpawnPoint_Start y se
///    reasigna SurvivalLevelController.player.
/// 4. Valida la cadena de daño de cada enemigo del pool y de las hordas.
/// 5. Marca como modificadas y GUARDA las escenas tocadas.
/// </summary>
public static class SetupCombatScenesLinux
{
    private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player_Linux.prefab";
    private const string HeavyFbxPath = "Assets/_Project/Models/Characters/linux-skeleton/linux.fbx";
    private const string OptFbxPath = "Assets/_Project/Models/Characters/linux-skeleton/linux_opt.fbx";
    private const string SourceScenePath = "Assets/_Project/Scenes/GamePlay/Mapa_completo.unity";
    private static readonly string[] LevelScenePaths =
    {
        "Assets/_Project/Scenes/GamePlay/Nivel_1.unity",
        "Assets/_Project/Scenes/GamePlay/Nivel_2.unity",
    };
    private const string SpawnPointName = "SpawnPoint_Start";
    private const string EditorOnlyTag = "EditorOnly";

    private static readonly string[] MeshProps = { "m_Mesh", "m_Bones", "m_RootBone", "m_AABB", "m_Materials" };

    [MenuItem("Tools/SudoKill9/Synchronize Linux and Damage in Levels 1 and 2")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var log = new StringBuilder("[SetupCombatScenesLinux]\n");
        bool ok = true;

        // Abrir las tres escenas juntas: la copia del jugador entre escenas lo necesita.
        Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        var levels = LevelScenePaths.Select(p => EditorSceneManager.OpenScene(p, OpenSceneMode.Additive)).ToList();
        var all = new List<Scene> { source };
        all.AddRange(levels);

        // Foto previa de arma / MuzzlePoint / CharacterController / offset para comparar tras el cambio de malla.
        var before = all.ToDictionary(s => s.path, s => Snapshot(FindPlayerRoot(s)));

        // 1. Prefab
        ok &= FixPrefab(log);

        // 2. Instancias en escena
        foreach (Scene scene in all)
        {
            log.AppendLine($"── {scene.name}");
            GameObject player = FindPlayerRoot(scene);
            if (player == null && scene != source)
            {
                player = CopyIntoScene(FindPlayerRoot(source), scene);
                log.AppendLine(player != null ? "  Jugador copiado desde Mapa_completo." : "  ERROR: no se pudo copiar el jugador.");
            }
            if (player == null) { log.AppendLine("  ERROR: no hay Player_Linux (LinuxCombatIK) en la escena."); ok = false; continue; }

            FixInstance(player, log);
            ok &= CompareSnapshot(before[scene.path], Snapshot(player), log);

            if (scene != source) ok &= SetupLevel(scene, player, log);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // 3. Guardar
        foreach (Scene scene in all)
        {
            bool saved = EditorSceneManager.SaveScene(scene);
            log.AppendLine(saved ? $"Guardada {scene.path}" : $"ERROR: no se pudo guardar {scene.path}");
            ok &= saved;
        }
        if (levels.Count > 0) SceneManager.SetActiveScene(levels[0]);

        log.AppendLine(ok ? "RESULTADO: OK, listo para Play Mode." : "RESULTADO: hay errores, revisa las líneas marcadas.");
        if (ok) Debug.Log(log.ToString()); else Debug.LogError(log.ToString());
    }

    // ───────────────────────────────────────────────────────────── Prefab

    private static bool FixPrefab(StringBuilder log)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            int missing = 0;
            foreach (Transform t in contents.GetComponentsInChildren<Transform>(true))
                missing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            if (missing > 0) log.AppendLine($"Player_Linux.prefab: {missing} componente(s) con script perdido eliminados (p. ej. LinuxStarfield).");

            int tags = ClearEditorOnly(contents);
            if (tags > 0) log.AppendLine($"Player_Linux.prefab: {tags} tag(s) EditorOnly → Untagged.");

            int swapped = SwapToOptimized(contents, log);
            if (missing + tags + swapped == 0) { log.AppendLine("Player_Linux.prefab ya estaba correcto (linux_opt.fbx)."); return true; }

            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath, out bool success);
            log.AppendLine(success
                ? $"Player_Linux.prefab guardado: {swapped} referencia(s) linux.fbx → linux_opt.fbx."
                : "ERROR: no se pudo guardar Player_Linux.prefab (ver consola).");
            return success;
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    // ───────────────────────────────────────────────────────────── Instancia en escena

    private static void FixInstance(GameObject player, StringBuilder log)
    {
        // Malla/avatar: revertir overrides para heredar el linux_opt del prefab.
        int reverted = 0;
        foreach (var smr in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            reverted += RevertProps(smr, MeshProps);
        foreach (var anim in player.GetComponentsInChildren<Animator>(true))
            reverted += RevertProps(anim, "m_Avatar");
        if (reverted > 0) log.AppendLine($"  {reverted} override(s) de malla/avatar revertidos al prefab.");

        // Si algo no viene del prefab (objeto añadido en escena) se cambia en sitio.
        int swapped = SwapToOptimized(player, log);
        if (swapped > 0) log.AppendLine($"  {swapped} referencia(s) linux.fbx en objetos de escena → linux_opt.fbx.");

        // Tag EditorOnly: revertir el override (vuelve al Untagged del prefab) o poner Untagged si es un objeto de escena.
        int tags = 0;
        foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
        {
            if (!t.CompareTag(EditorOnlyTag)) continue;
            if (RevertProps(t.gameObject, "m_TagString") == 0 || t.CompareTag(EditorOnlyTag))
            {
                Undo.RecordObject(t.gameObject, "Untag EditorOnly");
                t.gameObject.tag = "Untagged";
            }
            log.AppendLine($"  Tag EditorOnly quitado de '{t.name}' → '{t.tag}' (ya no se elimina en la build).");
            tags++;
        }

        string meshes = string.Join(", ", player.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Where(r => r.sharedMesh != null).Select(r => $"{r.name}={System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(r.sharedMesh))}"));
        log.AppendLine($"  Mallas: {meshes}");
        Animator a = player.GetComponentsInChildren<Animator>(true).FirstOrDefault(x => x.avatar != null);
        if (a != null) log.AppendLine($"  Avatar: {System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(a.avatar))} (humanoide válido: {a.avatar.isValid && a.avatar.isHuman})");
    }

    private static int RevertProps(Object target, params string[] names)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(target) || PrefabUtility.IsAddedComponentOverride(target)) return 0;
        var so = new SerializedObject(target);
        int n = 0;
        foreach (string name in names)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null || !p.prefabOverride) continue;
            PrefabUtility.RevertPropertyOverride(p, InteractionMode.UserAction);
            n++;
        }
        return n;
    }

    private static int ClearEditorOnly(GameObject root)
    {
        int n = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.CompareTag(EditorOnlyTag)) { t.gameObject.tag = "Untagged"; n++; }
        return n;
    }

    // ───────────────────────────────────────────────────────────── Nivel (spawn + controller + daño)

    private static bool SetupLevel(Scene scene, GameObject player, StringBuilder log)
    {
        LinuxCombatIK ik = player.GetComponentInChildren<LinuxCombatIK>(true);
        if (ik == null) { log.AppendLine("  ERROR: el jugador no tiene LinuxCombatIK."); return false; }

        Transform spawn = FindInScene<Transform>(scene).FirstOrDefault(t => t.name == SpawnPointName);
        if (spawn != null)
        {
            Undo.RecordObject(player.transform, "Place Linux at spawn");
            player.transform.rotation = Quaternion.Euler(0f, spawn.eulerAngles.y, 0f);
            player.transform.position += spawn.position - ik.transform.position; // el cuerpo (CharacterController) sobre el spawn
            log.AppendLine($"  Colocado en {SpawnPointName} {spawn.position}.");
        }
        else log.AppendLine($"  AVISO: no hay '{SpawnPointName}'; el jugador se queda donde está.");

        SurvivalLevelController level = FindInScene<SurvivalLevelController>(scene).FirstOrDefault();
        if (level == null) { log.AppendLine("  ERROR: no hay SurvivalLevelController en la escena."); return false; }

        var so = new SerializedObject(level);
        so.FindProperty("player").objectReferenceValue = ik.transform;
        so.ApplyModifiedProperties();
        log.AppendLine($"  SurvivalLevelController.player = {player.name}/{ik.name}");

        return ValidateDamageChain(level, ik, player, log);
    }

    private static bool ValidateDamageChain(SurvivalLevelController level, LinuxCombatIK ik, GameObject player, StringBuilder log)
    {
        var prefabs = new HashSet<GameObject>();
        var so = new SerializedObject(level);
        var pool = so.FindProperty("enemyPool");
        for (int i = 0; i < pool.arraySize; i++) Add(prefabs, pool.GetArrayElementAtIndex(i).FindPropertyRelative("prefab"));
        var waves = so.FindProperty("scriptedWaves");
        for (int w = 0; w < waves.arraySize; w++)
        {
            var groups = waves.GetArrayElementAtIndex(w).FindPropertyRelative("groups");
            for (int g = 0; g < groups.arraySize; g++) Add(prefabs, groups.GetArrayElementAtIndex(g).FindPropertyRelative("prefab"));
        }

        bool ok = true;
        int requiredMask = 0;
        foreach (GameObject prefab in prefabs)
        {
            var issues = new List<string>();
            var stats = prefab.GetComponent<EnemyStatsBase>();
            if (stats == null) issues.Add("sin EnemyStatsBase en la raíz");

            var solid = prefab.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger).ToArray();
            if (solid.Length == 0) issues.Add("sin collider sólido: las balas lo atraviesan");
            foreach (var c in solid)
            {
                requiredMask |= 1 << c.gameObject.layer;
                // includeInactive: los GameObjects de un asset de prefab no cuentan como activos en jerarquía.
                if (stats != null && c.GetComponentInParent<EnemyStatsBase>(true) != stats)
                    issues.Add($"el collider '{c.name}' no llega a EnemyStatsBase");
            }

            if (prefab.GetComponentInChildren<EnemyHealthBar>(true) == null) issues.Add("sin EnemyHealthBar (Tools/Setup Enemy Health Bars (All))");
            if (prefab.GetComponent<EnemyVFXHandler>() == null) issues.Add("sin EnemyVFXHandler (sin chispa de impacto)");
            float despawn = stats != null ? stats.DespawnDelay : -1f;
            if (stats != null && despawn < 0f) issues.Add("despawnDelay < 0: el cadáver no se destruye");

            string colliders = string.Join("/", solid.Select(c => $"{c.GetType().Name}@{LayerMask.LayerToName(c.gameObject.layer)}"));
            if (issues.Count == 0) log.AppendLine($"  ✓ {prefab.name}: {colliders} → {stats.GetType().Name} → barra + VFX, despawn {despawn:0.##} s");
            else { log.AppendLine($"  ✗ {prefab.name}: {string.Join("; ", issues)}"); ok = false; }
        }

        // El rayo de mira y la bala de LinuxCombatIK.DispararBala() usan capasDisparo.
        var ikSo = new SerializedObject(ik);
        var mask = ikSo.FindProperty("capasDisparo");
        if ((mask.intValue & requiredMask) != requiredMask)
        {
            mask.intValue |= requiredMask;
            ikSo.ApplyModifiedProperties();
            log.AppendLine($"  capasDisparo ampliada con las capas de los enemigos ({LayerList(requiredMask)}).");
        }
        else log.AppendLine($"  capasDisparo incluye las capas de los enemigos ({LayerList(requiredMask)}).");

        foreach (var shooter in player.GetComponentsInChildren<PlayerShooter>(true))
        {
            var ss = new SerializedObject(shooter);
            var hm = ss.FindProperty("hitMask");
            if ((hm.intValue & requiredMask) == requiredMask) continue;
            hm.intValue |= requiredMask;
            ss.ApplyModifiedProperties();
            log.AppendLine($"  PlayerShooter.hitMask ampliada en '{shooter.name}'.");
        }
        return ok;
    }

    private static void Add(HashSet<GameObject> set, SerializedProperty p)
    {
        if (p != null && p.objectReferenceValue is GameObject go) set.Add(go);
    }

    private static string LayerList(int mask)
    {
        var names = new List<string>();
        for (int i = 0; i < 32; i++)
            if ((mask & (1 << i)) != 0) names.Add(string.IsNullOrEmpty(LayerMask.LayerToName(i)) ? i.ToString() : LayerMask.LayerToName(i));
        return names.Count == 0 ? "ninguna" : string.Join(", ", names);
    }

    // ───────────────────────────────────────────────────────────── linux.fbx -> linux_opt.fbx

    /// <summary>Sustituye la malla de linux.fbx por la de linux_opt.fbx (huesos remapeados por nombre). El Avatar no se toca.</summary>
    private static int SwapToOptimized(GameObject root, StringBuilder log)
    {
        GameObject opt = AssetDatabase.LoadAssetAtPath<GameObject>(OptFbxPath);
        if (opt == null) { log.AppendLine($"  ERROR: no existe {OptFbxPath}."); return 0; }
        var optRenderers = opt.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        int changed = 0;

        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null || AssetDatabase.GetAssetPath(smr.sharedMesh) != HeavyFbxPath) continue;

            SkinnedMeshRenderer src = optRenderers.FirstOrDefault(r => r.sharedMesh != null && r.sharedMesh.name == smr.sharedMesh.name)
                                   ?? optRenderers.FirstOrDefault(r => r.name == smr.name)
                                   ?? (optRenderers.Length == 1 ? optRenderers[0] : null);
            if (src == null) { log.AppendLine($"  AVISO: sin malla equivalente en linux_opt para '{smr.name}'."); continue; }

            Dictionary<string, Transform> byName = BonesByName(smr.GetComponentInParent<Animator>(true)?.transform ?? root.transform);
            var bones = new Transform[src.bones.Length];
            var missing = new List<string>();
            for (int i = 0; i < bones.Length; i++)
            {
                string boneName = src.bones[i] != null ? src.bones[i].name : "(null)";
                if (!byName.TryGetValue(boneName, out bones[i])) missing.Add(boneName);
            }
            if (missing.Count > 0)
            {
                log.AppendLine($"  ERROR: '{smr.name}' no se cambia; faltan huesos: {string.Join(", ", missing.Take(8))}");
                continue;
            }

            Undo.RecordObject(smr, "Swap to linux_opt");
            smr.sharedMesh = src.sharedMesh;
            smr.bones = bones;
            if (src.rootBone != null && byName.TryGetValue(src.rootBone.name, out Transform rb)) smr.rootBone = rb;
            smr.localBounds = src.localBounds;
            if (smr.sharedMaterials.Length != src.sharedMesh.subMeshCount) smr.sharedMaterials = src.sharedMaterials;
            changed++;
        }

        return changed;
    }

    private static Dictionary<string, Transform> BonesByName(Transform root)
    {
        var byName = new Dictionary<string, Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (!byName.ContainsKey(t.name)) byName[t.name] = t;
        return byName;
    }

    // ───────────────────────────────────────────────────────────── Foto antes/después

    /// <summary>Clave → valor de lo que el cambio de malla NO debe alterar.</summary>
    private static Dictionary<string, string> Snapshot(GameObject player)
    {
        var s = new Dictionary<string, string>();
        if (player == null) return s;
        foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("SCAR-H") || t.name == "MuzzlePoint" || t.name == "Model")
                s[$"{t.name}"] = $"padre={(t.parent ? t.parent.name : "-")} pos={t.localPosition:F3} rot={t.localEulerAngles:F1}";
        }
        foreach (var cc in player.GetComponentsInChildren<CharacterController>(true))
            s[$"CharacterController@{cc.name}"] = $"enabled={cc.enabled} center={cc.center:F3} height={cc.height:F3} radius={cc.radius:F3}";
        foreach (var anim in player.GetComponentsInChildren<Animator>(true))
            s[$"Animator@{anim.name}"] = $"controller={(anim.runtimeAnimatorController ? anim.runtimeAnimatorController.name : "-")}";
        return s;
    }

    private static bool CompareSnapshot(Dictionary<string, string> a, Dictionary<string, string> b, StringBuilder log)
    {
        bool same = true;
        foreach (var kv in a)
        {
            if (b.TryGetValue(kv.Key, out string v) && v == kv.Value) continue;
            log.AppendLine($"  ✗ CAMBIÓ {kv.Key}: {kv.Value} → {(v ?? "(desaparecido)")}");
            same = false;
        }
        if (same)
        {
            log.AppendLine("  ✓ Sin cambios en arma, MuzzlePoint, offset del modelo, CharacterController ni Animator Controller:");
            foreach (var kv in a.OrderBy(k => k.Key)) log.AppendLine($"      {kv.Key}: {kv.Value}");
        }
        return same;
    }

    // ───────────────────────────────────────────────────────────── Utilidades

    private static GameObject CopyIntoScene(GameObject source, Scene target)
    {
        if (source == null) return null;
        // Copiar/pegar del Editor: conserva el enlace al prefab, sus overrides y las referencias internas.
        Selection.objects = new Object[] { source };
        Unsupported.CopyGameObjectsToPasteboard();
        Selection.activeObject = null;
        SceneManager.SetActiveScene(target);
        Unsupported.PasteGameObjectsFromPasteboard();

        GameObject pasted = Selection.activeGameObject;
        if (pasted == null) return null;
        if (pasted.scene != target) SceneManager.MoveGameObjectToScene(pasted, target);
        pasted.name = source.name;
        Undo.RegisterCreatedObjectUndo(pasted, "Copy Player_Linux");
        return pasted;
    }

    private static GameObject FindPlayerRoot(Scene scene)
    {
        LinuxCombatIK ik = FindInScene<LinuxCombatIK>(scene).FirstOrDefault();
        if (ik != null) return PrefabUtility.GetOutermostPrefabInstanceRoot(ik.gameObject) ?? ik.transform.root.gameObject;
        return scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Player_Linux");
    }

    private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
}
