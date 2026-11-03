#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.IO;

public static class SetupHealthUI
{
    private const string PlayerName       = "Player_LinuxSkeleton";
    private const string CanvasName       = "HUD_Canvas";
    private const string PanelName        = "HealthPanel";
    private const string ContainerName    = "HeartsContainer";
    private const int    DefaultMaxHealth = 5;
    private const int    TargetDisplay    = 3; // Display 4 (0-indexed)
    private const string TargetCameraName = "Camera_1";

    private const string SpritesFolder  = "Assets/_Project/Sprites/UI/Health";
    private const string FullHeartPath  = SpritesFolder + "/heart_full.png";
    private const string EmptyHeartPath = SpritesFolder + "/heart_empty.png";

    [MenuItem("Tools/Setup Pixel Art Health Bar")]
    public static void Run()
    {
        Undo.SetCurrentGroupName("Setup Pixel Art Health Bar");
        int undoGroup = Undo.GetCurrentGroup();

        EnsureHeartSprites();

        Sprite fullSprite  = AssetDatabase.LoadAssetAtPath<Sprite>(FullHeartPath);
        Sprite emptySprite = AssetDatabase.LoadAssetAtPath<Sprite>(EmptyHeartPath);

        EnsureDisplayActivator();

        Camera targetCam = FindTargetCamera();
        Canvas canvas = GetOrCreateCanvas(targetCam);

        RectTransform panel = GetOrCreatePanel(canvas.transform);
        RectTransform container = GetOrCreateContainer(panel);

        HeartHealthUI heartUI = GetOrAddComponent<HeartHealthUI>(canvas.gameObject);
        heartUI.HeartsContainer = container;

        if (fullSprite != null)
        {
            Undo.RecordObject(heartUI, "Assign heart sprites");
            heartUI.FullHeartSprite  = fullSprite;
            heartUI.EmptyHeartSprite = emptySprite;
            EditorUtility.SetDirty(heartUI);
        }

        GameObject player = GameObject.Find(PlayerName);
        if (player == null)
        {
            Debug.LogWarning($"[SetupHealthUI] '{PlayerName}' not found in scene. " +
                             "PlayerHealth will be linked when the player is added.");
        }
        else
        {
            PlayerHealth ph = GetOrAddComponent<PlayerHealth>(player);
            Undo.RecordObject(heartUI, "Link PlayerHealth");
            heartUI.PlayerHealth = ph;
            EditorUtility.SetDirty(heartUI);
        }

        CreatePlaceholderHearts(container, heartUI, DefaultMaxHealth);

        Selection.activeGameObject = canvas.gameObject;
        Undo.CollapseUndoOperations(undoGroup);

        string camNote = targetCam != null
            ? $"Canvas linked to '{targetCam.name}' (Display {TargetDisplay + 1})."
            : $"WARNING: '{TargetCameraName}' not found. Canvas set to Overlay on Display {TargetDisplay + 1}.";

        Debug.Log($"[SetupHealthUI] Health bar created. {camNote}");
    }

    // ── Display activator ────────────────────────────────────────────────────

    private static void EnsureDisplayActivator()
    {
        if (Object.FindFirstObjectByType<DisplayActivator>() != null) return;

        var go = new GameObject("DisplayActivator");
        Undo.RegisterCreatedObjectUndo(go, "Create DisplayActivator");
        go.AddComponent<DisplayActivator>();
    }

    // ── Find camera targeting Display 4 ──────────────────────────────────────

    private static Camera FindTargetCamera()
    {
        GameObject camGO = GameObject.Find(TargetCameraName);
        if (camGO != null)
        {
            Camera cam = camGO.GetComponent<Camera>();
            if (cam != null) return cam;
        }

        foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam.targetDisplay == TargetDisplay)
                return cam;
        }

        return null;
    }

    // ── Heart sprite generation ─────────────────────────────────────────────

    private static void EnsureHeartSprites()
    {
        if (!Directory.Exists(SpritesFolder))
            Directory.CreateDirectory(SpritesFolder);

        if (AssetDatabase.LoadAssetAtPath<Sprite>(FullHeartPath) == null)
            CreateHeartPNG(FullHeartPath, full: true);

        if (AssetDatabase.LoadAssetAtPath<Sprite>(EmptyHeartPath) == null)
            CreateHeartPNG(EmptyHeartPath, full: false);

        AssetDatabase.Refresh();
        ConfigureSpriteImporter(FullHeartPath);
        ConfigureSpriteImporter(EmptyHeartPath);
    }

    private static void CreateHeartPNG(string path, bool full)
    {
        const int size = 16;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        string[] rows = {
            "....##..##....",
            "...##########.",
            "..############",
            ".#############",
            ".#############",
            ".#############",
            ".#############",
            "..############",
            "..############",
            "...##########.",
            "....########..",
            ".....######...",
            "......####....",
            ".......##.....",
        };

        Color fillColor    = full ? new Color(0.9f, 0.15f, 0.2f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
        Color outlineColor = full ? new Color(0.5f, 0.05f, 0.1f, 1f) : new Color(0.15f, 0.15f, 0.15f, 1f);
        Color clear = Color.clear;

        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;

        int offsetY = (size - rows.Length) / 2;

        for (int row = 0; row < rows.Length; row++)
        {
            int y = size - 1 - (row + offsetY);
            string line = rows[row];
            int offsetX = (size - line.Length) / 2;
            for (int col = 0; col < line.Length; col++)
            {
                if (line[col] == '#')
                {
                    int x = col + offsetX;
                    if (x >= 0 && x < size && y >= 0 && y < size)
                        pixels[y * size + x] = fillColor;
                }
            }
        }

        var final = new Color[size * size];
        System.Array.Copy(pixels, final, pixels.Length);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (pixels[y * size + x].a > 0f) continue;
                bool adj = false;
                if (x > 0 && pixels[y * size + (x - 1)].a > 0f) adj = true;
                if (x < size - 1 && pixels[y * size + (x + 1)].a > 0f) adj = true;
                if (y > 0 && pixels[(y - 1) * size + x].a > 0f) adj = true;
                if (y < size - 1 && pixels[(y + 1) * size + x].a > 0f) adj = true;
                if (adj) final[y * size + x] = outlineColor;
            }
        }

        tex.SetPixels(final);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        File.WriteAllBytes(path, png);
    }

    private static void ConfigureSpriteImporter(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType        = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 16;
        importer.filterMode         = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    // ── Canvas (ScreenSpaceCamera on Display 4) ─────────────────────────────

    private static Canvas GetOrCreateCanvas(Camera targetCam)
    {
        GameObject canvasGO = GameObject.Find(CanvasName);
        if (canvasGO != null)
        {
            Canvas c = canvasGO.GetComponent<Canvas>();
            if (c != null)
            {
                ConfigureCanvas(c, targetCam);
                return c;
            }
        }

        canvasGO = new GameObject(CanvasName);
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create HUD Canvas");

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        ConfigureCanvas(canvas, targetCam);

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        return canvas;
    }

    private static void ConfigureCanvas(Canvas canvas, Camera targetCam)
    {
        if (targetCam != null)
        {
            canvas.renderMode    = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera   = targetCam;
            canvas.planeDistance  = 1f;
        }
        else
        {
            canvas.renderMode    = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = TargetDisplay;
        }
        canvas.sortingOrder = 10;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }
    }

    // ── Panel ─────────────────────────────────────────────────────────────────

    private static RectTransform GetOrCreatePanel(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find(PanelName);
        if (existing != null)
            return existing.GetComponent<RectTransform>();

        var panelGO = new GameObject(PanelName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(panelGO, "Create HealthPanel");
        panelGO.transform.SetParent(canvasTransform, false);

        RectTransform rt = panelGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -20f);
        rt.sizeDelta        = new Vector2(220f, 48f);

        var img = panelGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.35f);

        return rt;
    }

    // ── Hearts container ──────────────────────────────────────────────────────

    private static RectTransform GetOrCreateContainer(RectTransform panel)
    {
        Transform existing = panel.Find(ContainerName);
        if (existing != null)
            return existing.GetComponent<RectTransform>();

        var containerGO = new GameObject(ContainerName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(containerGO, "Create HeartsContainer");
        containerGO.transform.SetParent(panel, false);

        RectTransform rt = containerGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 8f);
        rt.offsetMax = new Vector2(-8f, -8f);

        var hlg = containerGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 6f;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;

        var csf = containerGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        return rt;
    }

    // ── Placeholder hearts ────────────────────────────────────────────────────

    private static void CreatePlaceholderHearts(RectTransform container, HeartHealthUI heartUI, int count)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(container.GetChild(i).gameObject);

        for (int i = 0; i < count; i++)
        {
            var heartGO = new GameObject($"Heart_{i + 1}", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(heartGO, "Create Heart Image");
            heartGO.transform.SetParent(container, false);

            var rt = heartGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(32f, 32f);

            var img = heartGO.GetComponent<Image>();
            img.sprite         = heartUI.FullHeartSprite;
            img.preserveAspect = true;
            img.color          = heartUI.FullHeartSprite != null ? Color.white : new Color(1f, 0.2f, 0.2f, 1f);
        }
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp != null) return comp;

        Undo.RecordObject(go, $"Add {typeof(T).Name}");
        comp = Undo.AddComponent<T>(go);
        return comp;
    }
}
#endif
