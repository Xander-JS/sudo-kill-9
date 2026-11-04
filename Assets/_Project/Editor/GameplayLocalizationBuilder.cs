using System.Collections.Generic;
using System.IO;
using System.Linq;
using SudoKill9.Levels;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Crea / actualiza la String Table Collection "Gameplay" (es/en) con todos los textos del HUD de
    /// supervivencia y verifica que Nivel_1 / Nivel_2 solo usan claves existentes y traducidas.
    /// Idempotente: re-ejecutarlo sobrescribe las traducciones con las de <see cref="Entries"/>.
    /// </summary>
    public static class GameplayLocalizationBuilder
    {
        private const string TablesDir = "Assets/_Project/Localization/Tables";
        private const string LevelScenesDir = "Assets/_Project/Scenes/GamePlay";
        private static readonly string[] LevelScenes = { "Nivel_1", "Nivel_2" };

        private static readonly (string key, string es, string en)[] Entries =
        {
            // HUD permanente
            (GameplayKeys.Survive, "SOBREVIVE", "SURVIVE"),
            (GameplayKeys.LevelIntro, "NIVEL {0} · SOBREVIVE {1}s", "LEVEL {0} · SURVIVE {1}s"),
            (GameplayKeys.ClearCounter, "ELIMINA A LOS RESTANTES: {0}", "ELIMINATE REMAINING: {0}"),
            (GameplayKeys.Hostiles, "HOSTILES: {0}", "HOSTILES: {0}"),

            // Avisos
            (GameplayKeys.FinalStretch, "¡ÚLTIMOS {0} SEGUNDOS!", "FINAL {0} SECONDS"),
            (GameplayKeys.ClearRemaining, "ELIMINA A LOS ENEMIGOS RESTANTES", "ELIMINATE THE REMAINING ENEMIES"),

            // Panel de resultado
            (GameplayKeys.LevelCompleted, "¡NIVEL {0} COMPLETADO!", "LEVEL {0} COMPLETED!"),
            (GameplayKeys.DefeatTitle, "KERNEL PANIC", "KERNEL PANIC"), // término técnico: se mantiene igual en ambos idiomas
            (GameplayKeys.ContinueHint, "Pulsa [{0}] para continuar", "Press [{0}] to continue"),
            (GameplayKeys.RetryHint, "Pulsa [{0}] para reintentar", "Press [{0}] to retry"),
            (GameplayKeys.KeyPrefix + "space", "ESPACIO", "SPACE"),
            (GameplayKeys.KeyPrefix + "enter", "ENTER", "ENTER"),

            // Hordas programadas
            (GameplayKeys.WaveHorde, "¡HORDA ENTRANTE!", "HORDE INCOMING"),
            (GameplayKeys.WaveVirusPatrol, "PATRULLA DE VIRUS DETECTADA", "VIRUS PATROL DETECTED"),
            (GameplayKeys.WaveMultiplePatrols, "MÚLTIPLES PATRULLAS EN CAMINO", "MULTIPLE PATROLS INBOUND"),
            (GameplayKeys.WaveHoldOn, "¡RESISTE!", "HOLD ON!"),
            (GameplayKeys.WaveShieldWall, "MURO DE ESCUDOS AL FRENTE", "SHIELD WALL AHEAD"),
            (GameplayKeys.WaveAirRaid, "¡ATAQUE AÉREO!", "AIR RAID"),
            (GameplayKeys.WaveFlanked, "¡TE ESTÁN FLANQUEANDO!", "FLANKED!"),
            (GameplayKeys.WaveSiege, "FORMACIÓN DE ASEDIO", "SIEGE FORMATION"),
            (GameplayKeys.WaveFinal, "!! OLEADA FINAL !!", "!! FINAL WAVE !!"),
        };

        [MenuItem("Tools/Survival Levels/Sync Gameplay Localization (es-en)")]
        public static void Sync()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = EditorSceneManager.GetActiveScene().path;

            FillTable();
            int problems = ValidateTable() + SyncLevelScenes();

            if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous);

            if (problems == 0)
                Debug.Log($"<color=green>[GameplayLocalization]</color> Tabla '{GameplayKeys.Table}' sincronizada ({Entries.Length} claves es/en) y escenas de nivel verificadas.");
            else
                Debug.LogError($"[GameplayLocalization] Sincronización terminada con {problems} problema(s); revisa los mensajes anteriores.");
        }

        /// <summary>Crea la colección "Gameplay" si no existe y escribe todas las entradas en es y en.</summary>
        internal static void FillTable()
        {
            CreditsSceneBuilder.EnsureLocalization();

            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(GameplayKeys.Table)
                ?? LocalizationEditorSettings.CreateStringTableCollection(GameplayKeys.Table, TablesDir);

            foreach (string code in new[] { "es", "en" })
            {
                var id = new LocaleIdentifier(code);
                var table = (StringTable)collection.GetTable(id) ?? (StringTable)collection.AddNewTable(id);
                foreach (var e in Entries)
                    table.AddEntry(e.key, code == "es" ? e.es : e.en);
                EditorUtility.SetDirty(table);
            }

            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
        }

        private static int ValidateTable()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(GameplayKeys.Table);
            int problems = 0;
            foreach (string code in new[] { "es", "en" })
            {
                var table = (StringTable)collection.GetTable(new LocaleIdentifier(code));
                if (table == null) { Debug.LogError($"[GameplayLocalization] Falta la tabla {GameplayKeys.Table}_{code}."); problems++; continue; }

                foreach (var shared in collection.SharedData.Entries)
                {
                    string value = table.GetEntry(shared.Id)?.Value;
                    if (string.IsNullOrWhiteSpace(value) || value == shared.Key)
                    {
                        Debug.LogError($"[GameplayLocalization] '{shared.Key}' está vacía o sin traducir en {code}.");
                        problems++;
                    }
                }
            }
            return problems;
        }

        /// <summary>
        /// Abre cada nivel y comprueba que los avisos de horda / mensaje de victoria son claves válidas.
        /// Si encuentra un texto heredado (p. ej. "AIR RAID") lo convierte a su clave y guarda la escena.
        /// </summary>
        private static int SyncLevelScenes()
        {
            var keys = new HashSet<string>(Entries.Select(e => e.key));
            var legacy = Entries.GroupBy(e => e.en).ToDictionary(g => g.Key, g => g.First().key);
            int problems = 0;

            foreach (string name in LevelScenes)
            {
                string path = $"{LevelScenesDir}/{name}.unity";
                if (!File.Exists(path)) { Debug.LogWarning($"[GameplayLocalization] No existe {path}; se omite."); continue; }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool dirty = false;

                foreach (var controller in Object.FindObjectsByType<SurvivalLevelController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var so = new SerializedObject(controller);

                    var completed = so.FindProperty("completedMessageKey");
                    dirty |= Resolve(completed, keys, legacy, $"{name}: completedMessageKey", ref problems, GameplayKeys.LevelCompleted);

                    var waves = so.FindProperty("scriptedWaves");
                    for (int i = 0; i < waves.arraySize; i++)
                    {
                        var prop = waves.GetArrayElementAtIndex(i).FindPropertyRelative("announcementKey");
                        if (string.IsNullOrEmpty(prop.stringValue)) continue; // sin aviso: válido
                        dirty |= Resolve(prop, keys, legacy, $"{name}: scriptedWaves[{i}]", ref problems, null);
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                if (dirty)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            return problems;
        }

        private static bool Resolve(SerializedProperty prop, HashSet<string> keys, Dictionary<string, string> legacy,
            string label, ref int problems, string fallbackKey)
        {
            string value = prop.stringValue;
            if (keys.Contains(value)) return false;

            if (legacy.TryGetValue(value, out string key) || (fallbackKey != null && (key = fallbackKey) != null))
            {
                Debug.Log($"[GameplayLocalization] {label}: '{value}' → '{key}'.");
                prop.stringValue = key;
                return true;
            }

            Debug.LogError($"[GameplayLocalization] {label}: la clave '{value}' no existe en la tabla '{GameplayKeys.Table}'.");
            problems++;
            return false;
        }
    }
}
