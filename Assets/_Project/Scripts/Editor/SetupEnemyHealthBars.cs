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
    /// Herramienta compartida de barras de vida World Space para enemigos (EnemyHealthBar).
    /// La llaman los cuatro Setup*Enemy y también puede ejecutarse sola para todos los enemigos de la escena.
    /// </summary>
    public static class SetupEnemyHealthBars
    {
        // Metros por encima del punto más alto del mesh (medido en la pose de bind al configurar).
        public const float VirusHeadroom = 0.35f;   // humanoide; al arrastrarse la barra se queda a la altura de pie
        public const float EscudoHeadroom = 0.45f;  // el golpe de escudo levanta la placa por encima de la cabeza
        public const float VoladorHeadroom = 0.40f; // el vuelo (baseOffset) y el balanceo ±0.12 m mueven la barra con él
        public const float EspiaHeadroom = 0.30f;   // esbelto; deja libre el ojo de donde sale el láser
        public const float DefaultHeadroom = 0.35f;

        [MenuItem("Tools/Setup Enemy Health Bars (All)")]
        public static void RunForAll()
        {
            EnemyStatsBase[] enemies = Object.FindObjectsByType<EnemyStatsBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (enemies.Length == 0)
            {
                Debug.LogWarning("[SetupEnemyHealthBars] No hay enemigos (EnemyStatsBase) en la escena activa.");
                return;
            }

            foreach (EnemyStatsBase enemy in enemies)
            {
                Undo.RegisterFullObjectHierarchyUndo(enemy.gameObject, "Setup Enemy Health Bar");
                Ensure(enemy);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"<color=green>[SetupEnemyHealthBars]</color> {enemies.Length} barras de vida configuradas. Guarda con Ctrl+S.");
        }

        /// <summary>Crea o actualiza la barra del enemigo con su altura recomendada. No marca la escena (lo hace quien llama).</summary>
        public static EnemyHealthBar Ensure(EnemyStatsBase enemy)
        {
            float headroom = HeadroomFor(enemy);
            EnemyHealthBar bar = EnemyHealthBar.CreateOrUpdate(enemy, headroom, out bool created);
            if (created) Undo.RegisterCreatedObjectUndo(bar.gameObject, "Create Enemy Health Bar");

            foreach (Component c in bar.GetComponentsInChildren<Component>(true)) EditorUtility.SetDirty(c);

            float localHeight = bar.transform.position.y - enemy.transform.position.y;
            Debug.Log($"<color=green>[SetupEnemyHealthBars]</color> '{enemy.name}': barra a {localHeight:0.00} m sobre el pivote (cabeza + {headroom:0.00} m).");
            return bar;
        }

        private static float HeadroomFor(EnemyStatsBase enemy) => enemy switch
        {
            VirusStats _ => VirusHeadroom,
            EscudoStats _ => EscudoHeadroom,
            VoladorStats _ => VoladorHeadroom,
            EspiaStats _ => EspiaHeadroom,
            _ => DefaultHeadroom,
        };
    }
}
