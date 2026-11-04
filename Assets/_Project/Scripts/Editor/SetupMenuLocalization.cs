#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SudoKill9.UI;

/// <summary>
/// Configuración de un solo clic de la sincronización de idioma del menú principal.
///
///  1. Abre la escena de Inicio si no está abierta.
///  2. Añade <see cref="MainMenuLocalizationSync"/> a Menu_Canvas.
///  3. Localiza los 4 botones bajo Menu_Buttons_Column y asigna sus pares de texto ES/EN.
///  4. Si existe un control "Idioma" (botón) en la escena, enlaza su onClick a ToggleLanguage.
///  5. Elimina cualquier panel/canvas flotante residual (clutter de versiones anteriores).
///  6. Marca la escena como modificada y la guarda.
///
/// Idempotente.
/// </summary>
public static class SetupMenuLocalization
{
    private const string InicioScenePath = "Assets/_Project/Scenes/GamePlay/Inicio.unity";
    private const string MenuCanvasName = "Menu_Canvas";
    private const string ButtonsColumn  = "Menu_Buttons_Column";

    // Pares de texto ES / EN para cada botón, identificados por una palabra clave del texto español.
    private struct Entry { public string keyword, es, en; }
    private static readonly Entry[] Labels =
    {
        new Entry { keyword = "INICIAR", es = "[ > INICIAR MISIÓN ]",    en = "[ > START MISSION ]" },
        new Entry { keyword = "CONFIG",  es = "[ > CONFIGURACIÓN ]",     en = "[ > SETTINGS ]" },
        new Entry { keyword = "CRÉDIT",  es = "[ > CRÉDITOS ]",          en = "[ > CREDITS ]" },
        new Entry { keyword = "SALIR",   es = "[ > SALIR DEL SISTEMA ]", en = "[ > EXIT SYSTEM ]" },
    };

    private static readonly string[] LegacyClutterNames = { "SelectionPanel", "LanguageToggle" };

    [MenuItem("Tools/Setup Menu Localization")]
    public static void Run()
    {
        if (!EnsureInicioSceneOpen()) return;

        RemoveLegacyClutter();

        GameObject menuCanvas = GameObject.Find(MenuCanvasName);
        if (menuCanvas == null)
        {
            Debug.LogError($"[SetupMenuLocalization] No se encontró '{MenuCanvasName}'.");
            return;
        }

        var sync = menuCanvas.GetComponent<MainMenuLocalizationSync>();
        if (sync == null) sync = menuCanvas.AddComponent<MainMenuLocalizationSync>();

        // Resolver los 4 botones por el texto que muestran.
        Transform column = menuCanvas.transform.Find(ButtonsColumn) ?? menuCanvas.transform;
        var allButtons = column.GetComponentsInChildren<Button>(true);

        var resolved = new List<(Button b, string es, string en)>();
        foreach (var entry in Labels)
        {
            Button match = allButtons.FirstOrDefault(b =>
            {
                var t = b.GetComponentInChildren<TMP_Text>(true);
                string txt = (t != null ? t.text : b.name).ToUpperInvariant();
                return txt.Contains(entry.keyword) || b.name.ToUpperInvariant().Contains(entry.keyword);
            });
            if (match != null) resolved.Add((match, entry.es, entry.en));
            else Debug.LogWarning($"[SetupMenuLocalization] No se encontró botón para '{entry.keyword}'.");
        }

        // Asignar el array serializado 'buttons'.
        var so = new SerializedObject(sync);
        var arr = so.FindProperty("buttons");
        arr.arraySize = resolved.Count;
        for (int i = 0; i < resolved.Count; i++)
        {
            var el = arr.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("button").objectReferenceValue = resolved[i].b;
            el.FindPropertyRelative("spanish").stringValue = resolved[i].es;
            el.FindPropertyRelative("english").stringValue = resolved[i].en;
        }
        so.FindProperty("defaultLanguage").stringValue = "es";
        so.ApplyModifiedPropertiesWithoutUndo();

        // Enlazar un control "Idioma" si existe en esta escena.
        int bound = BindLanguageControls(sync);

        EditorUtility.SetDirty(sync);
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = menuCanvas;

        string idiomaNote = bound > 0
            ? $"{bound} control(es) 'Idioma' enlazado(s) a ToggleLanguage."
            : "No hay control 'Idioma' en la escena de Inicio: el idioma se elige en la escena de " +
              "Configuración (dropdown que escribe settings.locale). El menú se sincroniza al volver " +
              "(Start) y en vivo vía SelectedLocaleChanged.";

        Debug.Log($"[SetupMenuLocalization] Listo. {resolved.Count}/4 botones asignados en Menu_Canvas. {idiomaNote}");
    }

    // ── Enlace de control de idioma ──────────────────────────────────────────

    private static int BindLanguageControls(MainMenuLocalizationSync sync)
    {
        int bound = 0;
        foreach (var btn in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (btn == null) continue;
            var t = btn.GetComponentInChildren<TMP_Text>(true);
            string label = (t != null ? t.text : string.Empty).ToLowerInvariant();
            bool isIdioma = btn.name.ToLowerInvariant().Contains("idioma") ||
                            btn.name.ToLowerInvariant().Contains("language") ||
                            label.Contains("idioma") || label.Contains("language");
            if (!isIdioma) continue;

            // Evitar duplicar: quitar un enlace previo a ToggleLanguage y volver a añadirlo.
            for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                if (btn.onClick.GetPersistentTarget(i) == sync &&
                    btn.onClick.GetPersistentMethodName(i) == nameof(MainMenuLocalizationSync.ToggleLanguage))
                    UnityEventTools.RemovePersistentListener(btn.onClick, i);

            UnityEventTools.AddPersistentListener(btn.onClick, sync.ToggleLanguage);
            bound++;
            Debug.Log($"[SetupMenuLocalization] Enlazado '{btn.name}'.onClick → ToggleLanguage.");
        }
        return bound;
    }

    // ── Limpieza de clutter ──────────────────────────────────────────────────

    private static void RemoveLegacyClutter()
    {
        var toDestroy = new List<GameObject>();
        foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (tr != null && LegacyClutterNames.Contains(tr.name))
                toDestroy.Add(tr.gameObject);

        foreach (var tmp in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tmp == null || string.IsNullOrEmpty(tmp.text)) continue;
            if (tmp.text.ToLowerInvariant().Contains("selecciona idioma"))
            {
                Transform root = tmp.transform;
                while (root.parent != null && root.parent.name != "IntroVideo_Canvas") root = root.parent;
                toDestroy.Add(root.gameObject);
            }
        }

        foreach (var go in toDestroy.Distinct())
        {
            if (go == null) continue;
            Debug.Log($"[SetupMenuLocalization] Eliminado clutter: '{go.name}'.");
            Object.DestroyImmediate(go);
        }
    }

    // ── Escena ────────────────────────────────────────────────────────────────

    private static bool EnsureInicioSceneOpen()
    {
        if (SceneManager.GetActiveScene().path == InicioScenePath) return true;
        if (!System.IO.File.Exists(InicioScenePath))
        {
            Debug.LogError($"[SetupMenuLocalization] No existe la escena de Inicio: '{InicioScenePath}'.");
            return false;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene(InicioScenePath, OpenSceneMode.Single);
        return true;
    }
}
#endif
