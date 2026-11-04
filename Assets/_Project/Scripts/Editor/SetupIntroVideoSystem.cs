#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using SudoKill9.UI.Inicio;

/// <summary>
/// Configuración de un solo clic del flujo de video de introducción en la escena de
/// Inicio, integrándose con la UI artística existente (sin paneles superpuestos ni
/// selector de idioma en la pantalla principal):
///
///  1. Abre la escena de Inicio y añade "Mapa_completo" a Build Settings.
///  2. Elimina cualquier UI redundante creada por versiones anteriores (el panel
///     flotante "Selecciona idioma" y cualquier toggle de idioma del menú).
///  3. Construye/reutiliza un overlay de video "IntroVideo_Canvas" DESACTIVADO.
///  4. Enlaza el botón real "[ > INICIAR MISIÓN ]" de Menu_Canvas a
///     IntroVideoManager.StartMission (reemplazando su acción anterior).
///  5. El idioma lo decide la pantalla de Configuración (GameSettings.LocaleCode);
///     aquí no se crea ningún selector.
///  6. Localiza los clips EN/ES y cablea todas las referencias por código.
///
/// Idempotente: se puede ejecutar varias veces sin duplicar nada.
/// </summary>
public static class SetupIntroVideoSystem
{
    private const string InicioScenePath = "Assets/_Project/Scenes/Inicio.unity";
    private const string TargetScenePath = "Assets/_Project/Scenes/Experimento_K/Scene_mapa/Mapa_completo.unity";
    private const string TargetSceneName = "Mapa_completo";

    private const string IntroFolder = "Assets/_Project/Intro";
    private static readonly string[] EnglishClipNames = { "video_final_en", "video_en", "intro_en" };
    private static readonly string[] SpanishClipNames = { "video_final_es", "video_final_es1", "video_es", "intro_es" };

    private const string MenuCanvasName = "Menu_Canvas";
    private const string ButtonsColumn  = "Menu_Buttons_Column";
    private const string OverlayCanvas  = "IntroVideo_Canvas";
    private const string VideoPanelName = "VideoPanel";

    // Nombres de objetos considerados "clutter" de versiones anteriores.
    private static readonly string[] LegacyClutterNames = { "SelectionPanel", "LanguageToggle" };

    [MenuItem("Tools/Setup Intro Video Flow")]
    public static void Run()
    {
        if (!EnsureInicioSceneOpen()) return;

        EnsureSceneInBuildSettings(TargetScenePath);

        VideoClip englishClip = FindClip(EnglishClipNames);
        VideoClip spanishClip = FindClip(SpanishClipNames);
        if (spanishClip == null) spanishClip = englishClip; // respaldo permitido por la especificación

        // 1) Limpiar UI redundante (panel flotante de idioma + toggles antiguos).
        RemoveLegacyClutter();

        // 2) Overlay de video (desactivado inicialmente).
        Canvas overlay = GetOrCreateOverlayCanvas();
        GameObject videoPanel = BuildVideoPanel(overlay.transform,
            out RawImage videoImage, out AspectRatioFitter aspectFitter,
            out VideoPlayer videoPlayer, out Button skipButton);

        // 3) Menú existente + botón real.
        GameObject menuCanvas = GameObject.Find(MenuCanvasName);
        if (menuCanvas == null)
        {
            Debug.LogError($"[SetupIntroVideoSystem] No se encontró '{MenuCanvasName}' en la escena.");
            return;
        }

        Button startButton = FindStartButton(menuCanvas.transform);
        if (startButton == null)
        {
            Debug.LogError("[SetupIntroVideoSystem] No se encontró el botón 'INICIAR MISIÓN' en Menu_Canvas.");
            return;
        }

        // 4) Componente manager (en el overlay, que permanece activo; el panel es el que se oculta).
        IntroVideoManager manager = overlay.GetComponent<IntroVideoManager>();
        if (manager == null) manager = overlay.gameObject.AddComponent<IntroVideoManager>();

        var so = new SerializedObject(manager);
        so.FindProperty("menuCanvas").objectReferenceValue   = menuCanvas;
        so.FindProperty("videoOverlay").objectReferenceValue = videoPanel;
        so.FindProperty("videoImage").objectReferenceValue   = videoImage;
        so.FindProperty("aspectFitter").objectReferenceValue = aspectFitter;
        so.FindProperty("videoPlayer").objectReferenceValue  = videoPlayer;
        so.FindProperty("skipButton").objectReferenceValue   = skipButton;
        so.FindProperty("englishClip").objectReferenceValue  = englishClip;
        so.FindProperty("spanishClip").objectReferenceValue  = spanishClip;
        so.FindProperty("targetSceneName").stringValue       = TargetSceneName;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 5) Enlazar el botón real: quitar su acción anterior y apuntar a StartMission.
        RewireButton(startButton, manager);

        // 6) Overlay desactivado para que el menú se vea correctamente primero.
        videoPanel.SetActive(false);

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(startButton);
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = menuCanvas;
        Debug.Log($"[SetupIntroVideoSystem] Listo. Botón '{startButton.name}' → StartMission. " +
                  $"Idioma leído de Configuración (GameSettings.LocaleCode). " +
                  $"EN={(englishClip ? englishClip.name : "<falta>")}, ES={(spanishClip ? spanishClip.name : "<falta>")}. " +
                  $"Overlay desactivado; '{TargetSceneName}' en Build Settings.");
    }

    // ── Escena ────────────────────────────────────────────────────────────────

    private static bool EnsureInicioSceneOpen()
    {
        if (SceneManager.GetActiveScene().path == InicioScenePath) return true;
        if (!System.IO.File.Exists(InicioScenePath))
        {
            Debug.LogError($"[SetupIntroVideoSystem] No existe la escena de Inicio: '{InicioScenePath}'.");
            return false;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene(InicioScenePath, OpenSceneMode.Single);
        return true;
    }

    private static void EnsureSceneInBuildSettings(string scenePath)
    {
        if (!System.IO.File.Exists(scenePath))
        {
            Debug.LogError($"[SetupIntroVideoSystem] La escena objetivo no existe: '{scenePath}'.");
            return;
        }
        var scenes = EditorBuildSettings.scenes.ToList();
        int idx = scenes.FindIndex(s => s.path == scenePath);
        if (idx >= 0)
        {
            if (!scenes[idx].enabled)
            {
                scenes[idx] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            return;
        }
        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[SetupIntroVideoSystem] Añadida a Build Settings: '{scenePath}'.");
    }

    // ── Limpieza de clutter ──────────────────────────────────────────────────

    private static void RemoveLegacyClutter()
    {
        var toDestroy = new List<GameObject>();

        // Barrido por nombre exacto en toda la escena (incluye objetos inactivos).
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            if (LegacyClutterNames.Contains(t.name))
                toDestroy.Add(t.gameObject);
        }

        // Barrido semántico: cualquier contenedor con el texto "Selecciona idioma".
        foreach (var tmp in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tmp == null || string.IsNullOrEmpty(tmp.text)) continue;
            if (tmp.text.ToLowerInvariant().Contains("selecciona idioma"))
            {
                // Sube hasta el panel raíz bajo IntroVideo_Canvas y lo destruye.
                Transform root = tmp.transform;
                while (root.parent != null && root.parent.name != OverlayCanvas) root = root.parent;
                toDestroy.Add(root.gameObject);
            }
        }

        foreach (var go in toDestroy.Distinct())
        {
            if (go == null) continue;
            Debug.Log($"[SetupIntroVideoSystem] Eliminado clutter: '{go.name}'.");
            Object.DestroyImmediate(go);
        }
    }

    // ── Clips ─────────────────────────────────────────────────────────────────

    private static VideoClip FindClip(string[] candidateNames)
    {
        foreach (string name in candidateNames)
            foreach (string ext in new[] { ".mp4", ".mov", ".webm", ".m4v" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<VideoClip>($"{IntroFolder}/{name}{ext}");
                if (clip != null) return clip;
            }

        foreach (string guid in AssetDatabase.FindAssets("t:VideoClip", new[] { IntroFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            foreach (string name in candidateNames)
                if (file.Contains(name.ToLowerInvariant()))
                    return AssetDatabase.LoadAssetAtPath<VideoClip>(path);
        }
        return null;
    }

    // ── Overlay de video ──────────────────────────────────────────────────────

    private static Canvas GetOrCreateOverlayCanvas()
    {
        var go = GameObject.Find(OverlayCanvas);
        if (go != null)
        {
            var c = go.GetComponent<Canvas>();
            if (c != null) { ConfigureOverlayCanvas(c, go); return c; }
        }
        go = new GameObject(OverlayCanvas);
        var canvas = go.AddComponent<Canvas>();
        ConfigureOverlayCanvas(canvas, go);
        go.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
        return canvas;
    }

    private static void ConfigureOverlayCanvas(Canvas canvas, GameObject go)
    {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // por encima del menú
        var scaler = go.GetComponent<CanvasScaler>() ?? go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    private static GameObject BuildVideoPanel(Transform parent, out RawImage videoImage,
        out AspectRatioFitter aspectFitter, out VideoPlayer videoPlayer, out Button skipButton)
    {
        GameObject panel = GetOrCreateChild(parent, VideoPanelName);
        StretchFull(EnsureRect(panel));
        var bg = GetOrAdd<Image>(panel);
        bg.color = Color.black;

        GameObject screen = GetOrCreateChild(panel.transform, "VideoScreen");
        StretchFull(EnsureRect(screen));
        videoImage = GetOrAdd<RawImage>(screen);
        videoImage.color = Color.white;
        aspectFitter = GetOrAdd<AspectRatioFitter>(screen);
        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspectFitter.aspectRatio = 16f / 9f;

        videoPlayer = GetOrAdd<VideoPlayer>(panel);
        videoPlayer.playOnAwake       = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.isLooping         = false;
        videoPlayer.source            = VideoSource.VideoClip;
        videoPlayer.renderMode        = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode   = VideoAudioOutputMode.Direct;
        videoPlayer.skipOnDrop        = true;

        skipButton = BuildButton(panel.transform, "Btn_Skip", "Saltar  >>",
            new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(200f, 52f));
        var skipImg = skipButton.GetComponent<Image>();
        if (skipImg != null) skipImg.color = new Color(0f, 0f, 0f, 0.65f);

        return panel;
    }

    // ── Botón real + re-wiring ───────────────────────────────────────────────

    private static Button FindStartButton(Transform menuCanvas)
    {
        Transform column = menuCanvas.Find(ButtonsColumn);
        Transform search = column != null ? column : menuCanvas;
        foreach (var b in search.GetComponentsInChildren<Button>(true))
        {
            if (b.name.ToUpperInvariant().Contains("INICIAR")) return b;
            var txt = b.GetComponentInChildren<TMP_Text>();
            if (txt != null && txt.text.ToUpperInvariant().Contains("INICIAR")) return b;
        }
        return null;
    }

    private static void RewireButton(Button button, IntroVideoManager manager)
    {
        var onClick = button.onClick;
        for (int i = onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(onClick, i);
        UnityEventTools.AddPersistentListener(onClick, manager.StartMission);
    }

    // ── Helpers de UI ─────────────────────────────────────────────────────────

    private static Button BuildButton(Transform parent, string name, string text,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = GetOrCreateChild(parent, name);
        var rt = EnsureRect(go);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot     = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = GetOrAdd<Image>(go);
        if (img.color == Color.white) img.color = new Color(0.14f, 0.14f, 0.16f, 1f);
        var btn = GetOrAdd<Button>(go);
        btn.targetGraphic = img;

        GameObject labelGo = GetOrCreateChild(go.transform, "Label");
        StretchFull(EnsureRect(labelGo));
        var tmp = GetOrAdd<TextMeshProUGUI>(labelGo);
        tmp.text = text;
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        return btn;
    }

    private static GameObject GetOrCreateChild(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static RectTransform EnsureRect(GameObject go) =>
        go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component =>
        go.GetComponent<T>() ?? go.AddComponent<T>();

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }
}
#endif
