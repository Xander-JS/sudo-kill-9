using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SudoKill9.Enemies;
using SudoKill9.Enemies.Escudo;
using SudoKill9.Enemies.Espia;
using SudoKill9.Enemies.Virus;
using SudoKill9.Enemies.Volador;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Herramienta compartida de VFX de enemigos (EnemyVFXHandler). La llaman los cuatro Setup*Enemy
    /// y también puede ejecutarse sola para todos los enemigos de la escena.
    /// </summary>
    public static class SetupEnemyVFX
    {
        // Color de acento por villano (chispas de muerte/impacto, micro-chispas y propulsión).
        public static readonly Color VirusColor = new Color(0.35f, 1f, 0.45f);   // verde malware
        public static readonly Color EscudoColor = new Color(1f, 0.7f, 0.15f);   // ámbar del escudo
        public static readonly Color VoladorColor = new Color(0.25f, 0.85f, 1f); // cian de propulsión
        public static readonly Color EspiaColor = new Color(1f, 0.15f, 0.2f);    // rojo del láser

        [MenuItem("Tools/Setup Enemy VFX (All)")]
        public static void RunForAll()
        {
            EnemyStatsBase[] enemies = Object.FindObjectsByType<EnemyStatsBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (enemies.Length == 0)
            {
                Debug.LogWarning("[SetupEnemyVFX] No hay enemigos (EnemyStatsBase) en la escena activa.");
                return;
            }

            foreach (EnemyStatsBase enemy in enemies)
            {
                Undo.RecordObject(enemy.gameObject, "Setup Enemy VFX");
                Ensure(enemy);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"<color=green>[SetupEnemyVFX]</color> VFX configurados en {enemies.Length} enemigos. Guarda con Ctrl+S.");
        }

        /// <summary>Añade/ajusta EnemyVFXHandler con el estilo del villano. No marca la escena (lo hace quien llama).</summary>
        public static EnemyVFXHandler Ensure(EnemyStatsBase enemy)
        {
            GameObject go = enemy.gameObject;
            EnemyVFXHandler vfx = go.GetComponent<EnemyVFXHandler>();
            if (vfx == null) vfx = Undo.AddComponent<EnemyVFXHandler>(go);
            else Undo.RecordObject(vfx, "Setup Enemy VFX");

            switch (enemy)
            {
                case VirusStats _: vfx.Configure(VirusColor, MovementVfxMode.GroundDust, 0f); break;
                case EscudoStats _: vfx.Configure(EscudoColor, MovementVfxMode.GroundDust, 0f); break;
                // Flota: propulsión hacia abajo, con un hilo de emisión también parado (6/s) para que se lea que vuela.
                case VoladorStats _: vfx.Configure(VoladorColor, MovementVfxMode.Thruster, 6f); break;
                case EspiaStats _: vfx.Configure(EspiaColor, MovementVfxMode.GroundDust, 0f); break;
            }

            EditorUtility.SetDirty(vfx);
            Debug.Log($"<color=green>[SetupEnemyVFX]</color> '{go.name}': EnemyVFXHandler ({vfx.MovementMode}).");
            return vfx;
        }
    }
}
