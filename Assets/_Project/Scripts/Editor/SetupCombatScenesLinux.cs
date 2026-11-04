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
/// 1. Player_Linux.prefab: cambia la malla y el Avatar de linux.fbx (74 MB) por linux_opt.fbx,
///    reasignando los huesos por nombre. Al hacerse en el prefab, Mapa_completo, Nivel_1 y Nivel_2 lo heredan.
/// 2. Nivel_1 / Nivel_2: si falta el jugador se copia el de Mapa_completo (con su conexión al prefab);
///    si existe, se sincronizan los valores de sus componentes con los de Mapa_completo.
/// 3. Coloca a Linux en SpawnPoint_Start y reasigna 'player' en SurvivalLevelController.
/// 4. Valida la cadena de daño: cada enemigo del pool/hordas tiene EnemyStatsBase, un collider sólido en una capa
///    incluida en LinuxCombatIK.capasDisparo, EnemyHealthBar y EnemyVFXHandler. Corrige la máscara si hace falta.
/// Las escenas quedan abiertas y marcadas como modificadas: guardar con Ctrl+S / File > Save.
/// </summary>
public static class SetupCombatScenesLinux
{
    private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player_Linux.prefab";
    private const string HeavyFbxPath = "Assets/_Project/Models/Characters/linux-skeleton/linux.fbx";
    private const string OptFbxPath = "Assets/_Project/Models/Characters/linux-skeleton/linux_opt.fbx";
    private const string SourceScenePath = "Assets/_Project/Scenes/GamePlay/Mapa_completo.unity";
    private static readonly string[] TargetScenePaths =
    {
        "Assets/_Project/Scenes/GamePlay/Nivel_1.unity",
        "Assets/_Project/Scenes/GamePlay/Nivel_2.unity",
    };
    private const string SpawnPointName = "SpawnPoint_Start";

    private static readonly HashSet<string> SkippedProps = new HashSet<string>
    {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Script", "m_EditorHideFlags", "m_EditorClassIdentifier", "m_Father", "m_Children",
    };

    [MenuItem("Tools/SudoKill9/Synchronize Linux and Damage in Levels 1 and 2")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var log = new StringBuilder("[SetupCombatScenesLinux]\n");

        // 1. Prefab -> linux_opt.fbx
        if (!SwapPrefabToOptimized(log)) { Debug.LogError(log.ToString()); return; }

        // 2. Abrir origen + destinos juntos (aditivo) para poder copiar entre escenas.
        Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        GameObject sourcePlayer = FindPlayerRoot(source);
        if (sourcePlayer == null)
        {
            Debug.LogError(log.Append("No se encontró Player_Linux (LinuxCombatIK) en Mapa_completo.").ToString());
            return;
        }
        // Por si la escena tiene un override de malla: el origen también debe usar linux_opt.
        if (SwapToOptimized(sourcePlayer, log) > 0) EditorSceneManager.MarkSceneDirty(source);

        var targets = new List<Scene>();
        foreach (string path in TargetScenePaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { log.AppendLine($"Falta la escena {path}."); continue; }
            targets.Add(EditorSceneManager.OpenScene(path, OpenSceneMode.Additive));
        }

        foreach (Scene scene in targets)
        {
            log.AppendLine($"── {scene.name}");
            SetupScene(scene, sourcePlayer, log);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // Mapa_completo solo era la fuente: se cierra si no se tocó.
        if (targets.Count > 0)
        {
            SceneManager.SetActiveScene(targets[0]);
            if (!source.isDirty) EditorSceneManager.CloseScene(source, true);
        }

        log.AppendLine("Listo. Guarda las escenas abiertas (File > Save / Ctrl+S).");
        Debug.Log(log.ToString());
    }

    // ───────────────────────────────────────────────────────────── Escena de combate

    private static void SetupScene(Scene scene, GameObject sourcePlayer, StringBuilder log)
    {
        GameObject player = FindPlayerRoot(scene);
        if (player == null)
        {
            player = CopyIntoScene(sourcePlayer, scene);
            log.AppendLine(player != null ? "  Jugador copiado desde Mapa_completo." : "  ERROR: no se pudo copiar el jugador.");
            if (player == null) return;
        }
        else
        {
            int n = SyncHierarchy(sourcePlayer, player, log);
            log.AppendLine($"  Jugador existente sincronizado con Mapa_completo ({n} componentes).");
        }

        int swapped = SwapToOptimized(player, log);
        if (swapped > 0) log.AppendLine($"  {swapped} override(s) de malla/avatar en escena pasados a linux_opt.");

        LinuxCombatIK ik = player.GetComponentInChildren<LinuxCombatIK>(true);
        if (ik == null) { log.AppendLine("  ERROR: el jugador no tiene LinuxCombatIK."); return; }

        PlaceAtSpawn(scene, player, ik.transform, log);

        SurvivalLevelController level = FindInScene<SurvivalLevelController>(scene).FirstOrDefault();
        if (level == null) { log.AppendLine("  ERROR: no hay SurvivalLevelController en la escena."); return; }

        var so = new SerializedObject(level);
        so.FindProperty("player").objectReferenceValue = ik.transform;
        so.ApplyModifiedProperties();
        log.AppendLine($"  SurvivalLevelController.player = {ik.name}");

        ValidateDamageChain(level, ik, player, log);
    }

    private static void PlaceAtSpawn(Scene scene, GameObject playerRoot, Transform body, StringBuilder log)
    {
        Transform spawn = FindInScene<Transform>(scene).FirstOrDefault(t => t.name == SpawnPointName);
        if (spawn == null) { log.AppendLine($"  AVISO: no hay '{SpawnPointName}'; el jugador se queda donde está."); return; }

        Undo.RecordObject(playerRoot.transform, "Place Linux at spawn");
        playerRoot.transform.rotation = Quaternion.Euler(0f, spawn.eulerAngles.y, 0f);
        // Se mueve la raíz para que el cuerpo (el que lleva el CharacterController) quede sobre el spawn.
        playerRoot.transform.position += spawn.position - body.position;
        log.AppendLine($"  Colocado en {SpawnPointName} {spawn.position}.");
    }

    // ───────────────────────────────────────────────────────────── Cadena de daño

    private static void ValidateDamageChain(SurvivalLevelController level, LinuxCombatIK ik, GameObject player, StringBuilder log)
    {
        var prefabs = new HashSet<GameObject>();
        var so = new SerializedObject(level);
        var pool = so.FindProperty("enemyPool");
        for (int i = 0; i < pool.arraySize; i++)
            Add(prefabs, pool.GetArrayElementAtIndex(i).FindPropertyRelative("prefab"));
        var waves = so.FindProperty("scriptedWaves");
        for (int w = 0; w < waves.arraySize; w++)
        {
            var groups = waves.GetArrayElementAtIndex(w).FindPropertyRelative("groups");
            for (int g = 0; g < groups.arraySize; g++)
                Add(prefabs, groups.GetArrayElementAtIndex(g).FindPropertyRelative("prefab"));
        }

        int requiredMask = 0;
        foreach (GameObject prefab in prefabs)
        {
            var issues = new List<string>();
            var stats = prefab.GetComponent<EnemyStatsBase>();
            if (stats == null) issues.Add("sin EnemyStatsBase en la raíz (SurvivalLevelController no lo contaría)");

            var solid = prefab.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger).ToArray();
            if (solid.Length == 0) issues.Add("sin collider sólido: las balas lo atraviesan");
            foreach (var c in solid)
            {
                requiredMask |= 1 << c.gameObject.layer;
                if (stats != null && c.GetComponentInParent<EnemyStatsBase>() != stats)
                    issues.Add($"el collider '{c.name}' no llega a EnemyStatsBase por GetComponentInParent");
            }

            if (prefab.GetComponentInChildren<EnemyHealthBar>(true) == null) issues.Add("sin EnemyHealthBar (Tools/Setup Enemy Health Bars (All))");
            if (prefab.GetComponent<EnemyVFXHandler>() == null) issues.Add("sin EnemyVFXHandler (no habrá chispa de impacto)");

            log.AppendLine(issues.Count == 0
                ? $"  ✓ {prefab.name}: collider + EnemyStatsBase + barra + VFX"
                : $"  ✗ {prefab.name}: {string.Join("; ", issues)}");
        }

        // LinuxCombatIK.capasDisparo debe incluir las capas de los enemigos (lo usan el rayo de mira y la bala).
        var ikSo = new SerializedObject(ik);
        var mask = ikSo.FindProperty("capasDisparo");
        if ((mask.intValue & requiredMask) != requiredMask)
        {
            mask.intValue |= requiredMask;
            ikSo.ApplyModifiedProperties();
            log.AppendLine($"  capasDisparo ampliada con las capas de los enemigos ({LayerList(requiredMask)}).");
        }
        else log.AppendLine($"  capasDisparo ya incluye las capas de los enemigos ({LayerList(requiredMask)}).");

        // PlayerShooter (raycast alternativo) si lo hubiera en el jugador.
        foreach (var shooter in player.GetComponentsInChildren<PlayerShooter>(true))
        {
            var ss = new SerializedObject(shooter);
            var hm = ss.FindProperty("hitMask");
            if ((hm.intValue & requiredMask) == requiredMask) continue;
            hm.intValue |= requiredMask;
            ss.ApplyModifiedProperties();
            log.AppendLine($"  PlayerShooter.hitMask ampliada en '{shooter.name}'.");
        }
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

    private static bool SwapPrefabToOptimized(StringBuilder log)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(OptFbxPath) == null)
        {
            log.AppendLine($"No se encontró {OptFbxPath}.");
            return false;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            int n = SwapToOptimized(contents, log);
            if (n > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                log.AppendLine($"Player_Linux.prefab: {n} referencia(s) de linux.fbx sustituidas por linux_opt.fbx.");
            }
            else log.AppendLine("Player_Linux.prefab ya usa linux_opt.fbx.");
            return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    /// <summary>Sustituye malla (huesos remapeados por nombre) y Avatar de linux.fbx por los de linux_opt.fbx.</summary>
    private static int SwapToOptimized(GameObject root, StringBuilder log)
    {
        GameObject opt = AssetDatabase.LoadAssetAtPath<GameObject>(OptFbxPath);
        var optRenderers = opt.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Avatar optAvatar = AssetDatabase.LoadAllAssetsAtPath(OptFbxPath).OfType<Avatar>().FirstOrDefault();
        int changed = 0;

        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null || AssetDatabase.GetAssetPath(smr.sharedMesh) != HeavyFbxPath) continue;

            SkinnedMeshRenderer src = optRenderers.FirstOrDefault(r => r.sharedMesh != null && r.sharedMesh.name == smr.sharedMesh.name)
                                   ?? optRenderers.FirstOrDefault(r => r.name == smr.name)
                                   ?? (optRenderers.Length == 1 ? optRenderers[0] : null);
            if (src == null) { log.AppendLine($"  AVISO: sin malla equivalente en linux_opt para '{smr.name}'."); continue; }

            // Huesos del esqueleto existente (el que mueve el Animator) con el mismo nombre que los de linux_opt.
            Transform skeletonRoot = smr.GetComponentInParent<Animator>(true)?.transform ?? root.transform;
            var byName = new Dictionary<string, Transform>();
            foreach (var t in skeletonRoot.GetComponentsInChildren<Transform>(true))
                if (!byName.ContainsKey(t.name)) byName[t.name] = t;

            var bones = new Transform[src.bones.Length];
            var missing = new List<string>();
            for (int i = 0; i < bones.Length; i++)
            {
                string boneName = src.bones[i] != null ? src.bones[i].name : null;
                if (boneName == null || !byName.TryGetValue(boneName, out bones[i])) missing.Add(boneName ?? "(null)");
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

        foreach (var anim in root.GetComponentsInChildren<Animator>(true))
        {
            if (anim.avatar == null || AssetDatabase.GetAssetPath(anim.avatar) != HeavyFbxPath) continue;
            if (optAvatar == null || !optAvatar.isValid) { log.AppendLine("  AVISO: linux_opt.fbx no tiene Avatar válido; se mantiene el de linux.fbx."); continue; }
            Undo.RecordObject(anim, "Swap avatar to linux_opt");
            anim.avatar = optAvatar;
            changed++;
        }
        return changed;
    }

    // ───────────────────────────────────────────────────────────── Copia / sincronización del jugador

    private static GameObject CopyIntoScene(GameObject source, Scene target)
    {
        // Copiar/pegar del Editor: conserva la conexión con Player_Linux.prefab y todos sus overrides.
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

    /// <summary>
    /// Copia los valores de cada componente de 'src' a su homólogo (misma ruta, tipo e índice) en 'dst'.
    /// Las referencias internas al jugador se remapean; las de assets se copian; las que apuntan a otros objetos
    /// de Mapa_completo se dejan como estaban en el nivel. El Transform raíz no se toca (lo coloca el spawn).
    /// </summary>
    private static int SyncHierarchy(GameObject src, GameObject dst, StringBuilder log)
    {
        int count = 0;
        foreach (Transform s in src.GetComponentsInChildren<Transform>(true))
        {
            string path = RelativePath(src.transform, s);
            Transform d = path.Length == 0 ? dst.transform : dst.transform.Find(path);
            if (d == null) { log.AppendLine($"  AVISO: falta '{path}' en el jugador del nivel."); continue; }

            foreach (var group in s.GetComponents<Component>().Where(c => c != null).GroupBy(c => c.GetType()))
            {
                Component[] srcComps = group.ToArray();
                Component[] dstComps = d.GetComponents(group.Key);
                for (int i = 0; i < srcComps.Length; i++)
                {
                    if (srcComps[i] is Transform && path.Length == 0) continue;
                    Component dc = i < dstComps.Length ? dstComps[i] : Undo.AddComponent(d.gameObject, group.Key);
                    if (dc == null) continue;
                    CopyValues(srcComps[i], dc, src.transform, dst.transform);
                    count++;
                }
            }
        }
        return count;
    }

    private static void CopyValues(Component src, Component dst, Transform srcRoot, Transform dstRoot)
    {
        var s = new SerializedObject(src);
        var d = new SerializedObject(dst);
        SerializedProperty it = s.GetIterator();
        bool enter = true;
        while (it.Next(enter))
        {
            enter = true;
            if (SkippedProps.Contains(it.name)) { enter = false; continue; }

            SerializedProperty target = d.FindProperty(it.propertyPath);
            if (target == null) { enter = false; continue; }

            if (it.isArray && it.propertyType != SerializedPropertyType.String) { target.arraySize = it.arraySize; d.ApplyModifiedPropertiesWithoutUndo(); d.Update(); continue; }
            if (it.propertyType == SerializedPropertyType.ObjectReference)
            {
                if (TryRemap(it.objectReferenceValue, srcRoot, dstRoot, out Object mapped)) target.objectReferenceValue = mapped;
                continue;
            }
            if (it.propertyType == SerializedPropertyType.ManagedReference) { enter = false; continue; }
            if (!it.hasVisibleChildren && !it.hasChildren) d.CopyFromSerializedPropertyIfDifferent(it);
        }
        d.ApplyModifiedProperties(); // registra Undo
    }

    private static bool TryRemap(Object value, Transform srcRoot, Transform dstRoot, out Object mapped)
    {
        mapped = value;
        if (value == null || EditorUtility.IsPersistent(value)) return true;

        Transform t = value is GameObject go ? go.transform : (value as Component)?.transform;
        if (t == null || !(t == srcRoot || t.IsChildOf(srcRoot))) return false; // objeto de otra parte de Mapa_completo: no tocar

        string path = RelativePath(srcRoot, t);
        Transform dt = path.Length == 0 ? dstRoot : dstRoot.Find(path);
        if (dt == null) return false;
        if (value is GameObject) { mapped = dt.gameObject; return true; }

        var comp = (Component)value;
        int index = System.Array.IndexOf(t.GetComponents(comp.GetType()), comp);
        Component[] candidates = dt.GetComponents(comp.GetType());
        if (index < 0 || index >= candidates.Length) return false;
        mapped = candidates[index];
        return true;
    }

    // ───────────────────────────────────────────────────────────── Utilidades

    private static GameObject FindPlayerRoot(Scene scene)
    {
        LinuxCombatIK ik = FindInScene<LinuxCombatIK>(scene).FirstOrDefault();
        if (ik != null) return PrefabUtility.GetOutermostPrefabInstanceRoot(ik.gameObject) ?? ik.transform.root.gameObject;
        return scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Player_Linux");
    }

    private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

    private static string RelativePath(Transform root, Transform t)
    {
        var parts = new List<string>();
        for (Transform c = t; c != null && c != root; c = c.parent) parts.Add(c.name);
        parts.Reverse();
        return string.Join("/", parts);
    }
}
