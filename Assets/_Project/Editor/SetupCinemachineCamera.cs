#if UNITY_EDITOR
// ═══════════════════════════════════════════════════════════════════════════
//  SetupCinemachineCamera.cs — "The Last Root"
//  Senior Unity Technical Artist & Tools Architect
//
//  Usage: Tools ▶ Setup 3rd Person Cinemachine Camera
//
//  Phase 1  Install com.unity.cinemachine via PackageManager.Client.Add.
//           Polls asynchronously via EditorApplication.update.
//           Persists "pending" state across the forced domain reload with
//           EditorPrefs so the setup survives the recompilation cycle.
//
//  Phase 2  [InitializeOnLoad] static ctor detects the pending flag after
//           the domain reload, clears it, and runs the full scene wiring
//           entirely through reflection — no Cinemachine assembly references
//           needed at compile time, so the file is valid even before the
//           package exists.
//
//  Supports Cinemachine 2.x  (CinemachineFreeLook  + AxisState)
//       and Cinemachine 3.x  (CinemachineCamera    + OrbitalFollow)
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

[InitializeOnLoad]
public static class SetupCinemachineCamera
{
    // ─────────────────────────────────────────────────────────────────────
    //  Constants
    // ─────────────────────────────────────────────────────────────────────
    private const string k_PackageId  = "com.unity.cinemachine";
    private const string k_PendingKey = "SetupCinemachineCamera_Pending";
    private const string k_PlayerName = "Player_LinuxSkeleton";
    private const string k_LookAtName = "Camera_LookAt_Point";
    private const string k_VCamName   = "CM FreeLook — Player_LinuxSkeleton";

    // ─────────────────────────────────────────────────────────────────────
    //  [InitializeOnLoad] — fires after every domain reload.
    //  If the pending flag is set the package just finished installing;
    //  schedule the scene setup for the next editor frame.
    // ─────────────────────────────────────────────────────────────────────
    static SetupCinemachineCamera()
    {
        if (!EditorPrefs.GetBool(k_PendingKey, false)) return;

        EditorPrefs.DeleteKey(k_PendingKey);

        // delayCall lets Unity finish its own post-reload initialisation first.
        EditorApplication.delayCall += () =>
        {
            Debug.Log("[SetupCinemachine] Domain reload complete — wiring scene.");
            RunSceneSetup();
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Menu entry point
    // ─────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Setup 3rd Person Cinemachine Camera")]
    public static void Install()
    {
        if (EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Please Wait",
                "Unity is still compiling.\nWait for the progress bar to clear, then try again.", "OK");
            return;
        }

        if (IsCinemachineAvailable())
        {
            Debug.Log("[SetupCinemachine] Cinemachine already installed — running scene setup.");
            RunSceneSetup();
            return;
        }

        Debug.Log($"[SetupCinemachine] Installing {k_PackageId}…");
        EditorPrefs.SetBool(k_PendingKey, true);   // survive the upcoming domain reload
        StartInstall();
    }

    [MenuItem("Tools/Setup 3rd Person Cinemachine Camera", true)]
    private static bool Validate()
        => !EditorApplication.isCompiling
        && UnityEngine.SceneManagement.SceneManager.GetActiveScene().isLoaded;

    // ─────────────────────────────────────────────────────────────────────
    //  Async package installation
    // ─────────────────────────────────────────────────────────────────────
    private static AddRequest s_Request;

    private static void StartInstall()
    {
        s_Request = Client.Add(k_PackageId);
        EditorApplication.update += PollInstall;
    }

    private static void PollInstall()
    {
        if (s_Request == null || !s_Request.IsCompleted) return;

        EditorApplication.update -= PollInstall;

        if (s_Request.Status == StatusCode.Success)
        {
            Debug.Log("[SetupCinemachine] Package installed. Refreshing assets — " +
                      "domain reload will complete the setup automatically.");
            // k_PendingKey is still set. After AssetDatabase.Refresh triggers
            // recompilation, [InitializeOnLoad] picks it up.
            AssetDatabase.Refresh();
        }
        else
        {
            EditorPrefs.DeleteKey(k_PendingKey);
            string msg = s_Request.Error?.message ?? "Unknown error";
            Debug.LogError($"[SetupCinemachine] Install failed: {msg}");
            EditorUtility.DisplayDialog("Installation Failed",
                $"Could not install {k_PackageId}:\n\n{msg}", "OK");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Dispatcher — detects CM version and calls the right builder
    // ─────────────────────────────────────────────────────────────────────
    private static void RunSceneSetup()
    {
        // CM 2.x (Unity 2019–2023 LTS)
        Type cm2 = FindType("Cinemachine.CinemachineFreeLook");
        if (cm2 != null) { BuildCM2(cm2); return; }

        // CM 3.x (Unity 6+)
        Type cm3 = FindType("Unity.Cinemachine.CinemachineCamera");
        if (cm3 != null) { BuildCM3(cm3); return; }

        Debug.LogWarning("[SetupCinemachine] Cinemachine types not yet available. " +
                         "If Unity is still compiling, wait and run the tool again.");
        EditorUtility.DisplayDialog("Retry",
            "Cinemachine types are not loaded yet.\n\n" +
            "Wait for compilation to finish, then run the tool again.", "OK");
    }

    // ═════════════════════════════════════════════════════════════════════
    //  CINEMACHINE 2.x  ──  CinemachineFreeLook
    // ═════════════════════════════════════════════════════════════════════
    private static void BuildCM2(Type freeLookType)
    {
        // ── 1. Locate player ──────────────────────────────────────────────
        GameObject player = FindPlayer();
        if (player == null) return;

        // ── 2. CinemachineBrain on Main Camera (disable old controllers) ──
        EnsureBrainCM2();

        // ── 3. LookAt point (reused if it already exists) ─────────────────
        GameObject lookAt = GetOrCreateLookAtPoint(player);

        // ── 4. Remove stale virtual camera ───────────────────────────────
        PurgeExistingVCam();

        // ── 5. Create the FreeLook GameObject + component ─────────────────
        var vcamGO = new GameObject(k_VCamName);
        Undo.RegisterCreatedObjectUndo(vcamGO, "Create CM2 FreeLook");
        Component vcam = Undo.AddComponent(vcamGO, freeLookType);

        // ── 6. Follow + LookAt ────────────────────────────────────────────
        ReflSet_Prop(vcam, "Follow", player.transform);
        ReflSet_Prop(vcam, "LookAt", lookAt.transform);

        // ── 7. Orbit radii (Top / Mid / Bottom) ───────────────────────────
        SetOrbitsCM2(vcam, freeLookType);

        // ── 8. X-Axis  →  Mouse X, invertInput = false ───────────────────
        //    The X-axis controls horizontal orbit (yaw) around the player.
        ConfigAxisCM2(vcam, freeLookType, fieldName: "m_XAxis",
            inputAxisName : "Mouse X",
            maxSpeed      : 300f,
            invertInput   : false,   // ← as specified
            accelTime     : 0.1f,
            decelTime     : 0.1f);

        // ── 9. Y-Axis  →  Mouse Y, invertInput = true ─────────────────────
        //    The Y-axis controls vertical arc (pitch). InvertInput = true
        //    gives natural feel: moving the mouse up looks up.
        ConfigAxisCM2(vcam, freeLookType, fieldName: "m_YAxis",
            inputAxisName : "Mouse Y",
            maxSpeed      : 2f,
            invertInput   : true,    // ← as specified
            accelTime     : 0.1f,
            decelTime     : 0.1f);

        // ── 10. Initial camera position — behind the player ───────────────
        vcamGO.transform.position = player.transform.position
                                  + player.transform.forward * -5f
                                  + Vector3.up * 2.5f;

        EditorUtility.SetDirty(vcamGO);

        // ── 11. Report ────────────────────────────────────────────────────
        string summary =
            $"Virtual Camera : {k_VCamName}\n" +
            $"Follow         : {player.name}\n" +
            $"LookAt         : {lookAt.name} (local pos 0, 1.5, 0)\n\n" +
            "X-Axis  Mouse X  invertInput = false\n" +
            "Y-Axis  Mouse Y  invertInput = true";

        Debug.Log("[SetupCinemachine CM2] ✓ Complete.\n" + summary);
        EditorUtility.DisplayDialog("Cinemachine Setup Complete ✓", summary, "Got it!");
    }

    // ─────────────────────────────────────────────────────────────────────
    //  CM 2.x helpers
    // ─────────────────────────────────────────────────────────────────────

    private static void EnsureBrainCM2()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[SetupCinemachine] No Main Camera found.");
            return;
        }

        // Disable any existing camera controllers that fight CinemachineBrain.
        foreach (MonoBehaviour mb in cam.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue;
            string n = mb.GetType().Name;
            if (n == "ThirdPersonCameraController" || n == "CameraController")
            {
                Undo.RecordObject(mb, $"Disable {n}");
                mb.enabled = false;
                Debug.Log($"[SetupCinemachine] Disabled {n} on Main Camera " +
                          "(would conflict with CinemachineBrain).");
            }
        }

        Type brainType = FindType("Cinemachine.CinemachineBrain");
        if (brainType == null) return;

        if (cam.GetComponent(brainType) == null)
        {
            Undo.AddComponent(cam.gameObject, brainType);
            Debug.Log("[SetupCinemachine] Added CinemachineBrain to Main Camera.");
        }
    }

    private static void SetOrbitsCM2(Component vcam, Type freeLookType)
    {
        FieldInfo fi = freeLookType.GetField("m_Orbits",
            BindingFlags.Public | BindingFlags.Instance);
        if (fi == null) { Debug.LogWarning("[SetupCinemachine] m_Orbits field not found."); return; }

        // m_Orbits is Orbit[] — 3 elements: [0]=Top, [1]=Mid, [2]=Bottom
        var arr = (Array)fi.GetValue(vcam);
        if (arr == null || arr.Length < 3) return;

        ApplyOrbit(arr, 0, height: 4.5f, radius: 1.75f);   // Top ring
        ApplyOrbit(arr, 1, height: 2.5f, radius: 3.0f);    // Middle ring
        ApplyOrbit(arr, 2, height: 0.4f, radius: 1.3f);    // Bottom ring

        fi.SetValue(vcam, arr);   // write array back (already a reference type, but explicit is safer)
    }

    // Orbit is a struct — box → mutate fields → write back into the array.
    private static void ApplyOrbit(Array arr, int idx, float height, float radius)
    {
        object elem = arr.GetValue(idx);
        Type t = elem.GetType();
        t.GetField("m_Height", BindingFlags.Public | BindingFlags.Instance)?.SetValue(elem, height);
        t.GetField("m_Radius", BindingFlags.Public | BindingFlags.Instance)?.SetValue(elem, radius);
        arr.SetValue(elem, idx);   // commit modified struct back into the array slot
    }

    /// <summary>
    /// Mutates a CM2 AxisState struct on the FreeLook component via reflection.
    ///
    /// AxisState is a value type (struct). The pattern is:
    ///   1. GetValue  → boxes the struct onto the heap (returns object).
    ///   2. GetField + SetValue on the *boxed copy* — the CLR keeps the box
    ///      alive, so mutations of its sub-fields are preserved.
    ///   3. SetValue back on the component field — unboxes and commits.
    /// </summary>
    private static void ConfigAxisCM2(
        Component vcam, Type freeLookType,
        string fieldName, string inputAxisName,
        float maxSpeed, bool invertInput,
        float accelTime, float decelTime)
    {
        FieldInfo axisField = freeLookType.GetField(fieldName,
            BindingFlags.Public | BindingFlags.Instance);
        if (axisField == null)
        {
            Debug.LogWarning($"[SetupCinemachine] Field '{fieldName}' not found on {freeLookType.Name}.");
            return;
        }

        object axisState = axisField.GetValue(vcam);     // ① box
        Type   axisType  = axisState.GetType();

        // ② Mutate the boxed copy
        axisType.GetField("m_InputAxisName", BindingFlags.Public | BindingFlags.Instance)
                ?.SetValue(axisState, inputAxisName);
        axisType.GetField("m_MaxSpeed", BindingFlags.Public | BindingFlags.Instance)
                ?.SetValue(axisState, maxSpeed);
        axisType.GetField("m_InvertInput", BindingFlags.Public | BindingFlags.Instance)
                ?.SetValue(axisState, invertInput);
        axisType.GetField("m_AccelTime", BindingFlags.Public | BindingFlags.Instance)
                ?.SetValue(axisState, accelTime);
        axisType.GetField("m_DecelTime", BindingFlags.Public | BindingFlags.Instance)
                ?.SetValue(axisState, decelTime);

        axisField.SetValue(vcam, axisState);              // ③ commit
    }

    // ═════════════════════════════════════════════════════════════════════
    //  CINEMACHINE 3.x  ──  CinemachineCamera + OrbitalFollow
    // ═════════════════════════════════════════════════════════════════════
    private static void BuildCM3(Type camType)
    {
        GameObject player = FindPlayer();
        if (player == null) return;

        EnsureBrainCM3();
        GameObject lookAt = GetOrCreateLookAtPoint(player);
        PurgeExistingVCam();

        var vcamGO = new GameObject(k_VCamName);
        Undo.RegisterCreatedObjectUndo(vcamGO, "Create CM3 Camera");
        Component vcam = Undo.AddComponent(vcamGO, camType);

        ReflSet_Prop(vcam, "Follow", player.transform);
        ReflSet_Prop(vcam, "LookAt", lookAt.transform);

        // OrbitalFollow controls the orbit radius and vertical limits
        Type orbType = FindType("Unity.Cinemachine.CinemachineOrbitalFollow");
        if (orbType != null)
        {
            Component orb = Undo.AddComponent(vcamGO, orbType);
            ReflSet_Field(orb, "Radius", 4f);
        }

        // RotationComposer keeps the look-at target centered
        Type compType = FindType("Unity.Cinemachine.CinemachineRotationComposer");
        if (compType != null)
            Undo.AddComponent(vcamGO, compType);

        // Input axis controller (CM3 uses a separate component for mouse input)
        Type inputCtrlType = FindType("Unity.Cinemachine.CinemachineInputAxisController")
                          ?? FindType("Unity.Cinemachine.InputAxisController");
        if (inputCtrlType != null && vcamGO.GetComponent(inputCtrlType) == null)
            Undo.AddComponent(vcamGO, inputCtrlType);

        vcamGO.transform.position = player.transform.position
                                  + player.transform.forward * -5f
                                  + Vector3.up * 2.5f;

        EditorUtility.SetDirty(vcamGO);

        string summary =
            $"Cinemachine 3.x camera '{k_VCamName}'\n" +
            $"Follow: {player.name}  |  LookAt: {lookAt.name}\n" +
            "OrbitalFollow + RotationComposer + InputAxisController attached.\n\n" +
            "Note: CM3 uses InputAxisController for mouse input.\n" +
            "Configure its axes in the Inspector if needed.";

        Debug.Log("[SetupCinemachine CM3] ✓ Complete.\n" + summary);
        EditorUtility.DisplayDialog("Cinemachine 3 Setup Complete ✓", summary, "Got it!");
    }

    private static void EnsureBrainCM3()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        foreach (MonoBehaviour mb in cam.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue;
            string n = mb.GetType().Name;
            if (n == "ThirdPersonCameraController" || n == "CameraController")
            {
                Undo.RecordObject(mb, $"Disable {n}");
                mb.enabled = false;
            }
        }

        Type brainType = FindType("Unity.Cinemachine.CinemachineBrain");
        if (brainType != null && cam.GetComponent(brainType) == null)
        {
            Undo.AddComponent(cam.gameObject, brainType);
            Debug.Log("[SetupCinemachine] Added CinemachineBrain (CM3) to Main Camera.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Shared helpers
    // ═════════════════════════════════════════════════════════════════════

    private static bool IsCinemachineAvailable()
        => FindType("Cinemachine.CinemachineFreeLook")     != null
        || FindType("Unity.Cinemachine.CinemachineCamera") != null;

    private static GameObject FindPlayer()
    {
        GameObject p = GameObject.Find(k_PlayerName);
        if (p != null) return p;

        Debug.LogError($"[SetupCinemachine] '{k_PlayerName}' not found in the active scene.");
        EditorUtility.DisplayDialog("Setup Failed",
            $"'{k_PlayerName}' not found in the open scene.\n" +
            "Make sure the correct scene is loaded and try again.", "OK");
        return null;
    }

    private static GameObject GetOrCreateLookAtPoint(GameObject player)
    {
        Transform existing = player.transform.Find(k_LookAtName);
        if (existing != null)
        {
            Debug.Log($"[SetupCinemachine] Reusing existing '{k_LookAtName}'.");
            return existing.gameObject;
        }

        var go = new GameObject(k_LookAtName);
        Undo.RegisterCreatedObjectUndo(go, "Create LookAt Point");
        Undo.SetTransformParent(go.transform, player.transform, "Parent LookAt Point");
        go.transform.localPosition = new Vector3(0f, 1.5f, 0f);   // upper-mid chest height
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one;

        Debug.Log($"[SetupCinemachine] Created '{k_LookAtName}' at local (0, 1.5, 0) " +
                  $"under '{player.name}'.");
        return go;
    }

    private static void PurgeExistingVCam()
    {
        GameObject old = GameObject.Find(k_VCamName);
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
            Debug.Log($"[SetupCinemachine] Replaced existing '{k_VCamName}'.");
        }
    }

    // ─── Reflection utilities ────────────────────────────────────────────

    /// <summary>Search all loaded assemblies for a type by full name.</summary>
    private static Type FindType(string fullName)
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }

    /// <summary>Set a public instance property via reflection (logs a warning if missing).</summary>
    private static void ReflSet_Prop(object obj, string propName, object value)
    {
        PropertyInfo p = obj.GetType().GetProperty(propName,
            BindingFlags.Public | BindingFlags.Instance);
        if (p != null) { p.SetValue(obj, value); return; }
        Debug.LogWarning($"[SetupCinemachine] Property '{propName}' not found on {obj.GetType().Name}.");
    }

    /// <summary>Set a public instance field via reflection (logs a warning if missing).</summary>
    private static void ReflSet_Field(object obj, string fieldName, object value)
    {
        FieldInfo f = obj.GetType().GetField(fieldName,
            BindingFlags.Public | BindingFlags.Instance);
        if (f != null) { f.SetValue(obj, value); return; }
        Debug.LogWarning($"[SetupCinemachine] Field '{fieldName}' not found on {obj.GetType().Name}.");
    }
}
#endif
