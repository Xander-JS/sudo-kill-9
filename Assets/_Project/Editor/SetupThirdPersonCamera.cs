#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click setup tool: "Tools ▶ Setup 3rd Person Mouse Camera".
/// Finds the Main Camera and Player_LinuxSkeleton, attaches
/// ThirdPersonCameraController with sensible defaults, and removes
/// the old CameraController / PlayerAim to avoid conflicts.
/// Fully undoable (Ctrl+Z).
/// </summary>
public static class SetupThirdPersonCamera
{
    [MenuItem("Tools/Setup 3rd Person Mouse Camera")]
    public static void Setup()
    {
        // ── 1. Locate Main Camera ──────────────────────────────────────────
        Camera cam = Camera.main;
        if (cam == null)
        {
            EditorUtility.DisplayDialog("Setup Failed",
                "No Main Camera found.\nTag a camera as 'MainCamera' and try again.", "OK");
            Debug.LogError("[SetupThirdPersonCamera] No camera tagged 'MainCamera' in the scene.");
            return;
        }

        // ── 2. Locate Player_LinuxSkeleton ────────────────────────────────
        GameObject player = GameObject.Find("Player_LinuxSkeleton");
        if (player == null)
        {
            EditorUtility.DisplayDialog("Setup Failed",
                "'Player_LinuxSkeleton' not found in the active scene.\nCheck the GameObject name and try again.", "OK");
            Debug.LogError("[SetupThirdPersonCamera] 'Player_LinuxSkeleton' not found.");
            return;
        }

        // ── 3. Remove legacy / conflicting camera scripts ─────────────────

        // Old CameraController (top-down / aim-based)
        var oldCtrl = cam.GetComponent<CameraController>();
        if (oldCtrl != null)
        {
            Undo.DestroyObjectImmediate(oldCtrl);
            Debug.Log("[SetupThirdPersonCamera] Removed CameraController.");
        }

        // PlayerAim drives body rotation via Mouse.current — conflicts with
        // ThirdPersonCameraController's syncPlayerRotation.
        var oldAim = player.GetComponent<PlayerAim>();
        if (oldAim != null)
        {
            Undo.DestroyObjectImmediate(oldAim);
            Debug.Log("[SetupThirdPersonCamera] Removed PlayerAim.");
        }

        // ── 4. Add (or get existing) ThirdPersonCameraController ──────────
        var ctrl = cam.GetComponent<ThirdPersonCameraController>();
        if (ctrl == null)
            ctrl = Undo.AddComponent<ThirdPersonCameraController>(cam.gameObject);

        // ── 5. Apply sensible defaults ────────────────────────────────────
        ctrl.target              = player.transform;
        ctrl.distance            = 4f;
        ctrl.heightOffset        = 1.4f;
        ctrl.sensitivityX        = 3f;
        ctrl.sensitivityY        = 2f;
        ctrl.minPitch            = -20f;
        ctrl.maxPitch            = 60f;
        ctrl.positionSmoothTime  = 0.10f;
        ctrl.syncPlayerRotation  = true;
        ctrl.playerRotationSpeed = 15f;

        // ── 6. Park the camera behind the player ──────────────────────────
        Undo.RecordObject(cam.transform, "Position camera behind player");
        Vector3 pivot   = player.transform.position + Vector3.up * ctrl.heightOffset;
        Vector3 camPos  = pivot + player.transform.forward * -ctrl.distance + Vector3.up * 0.5f;
        cam.transform.position = camPos;
        cam.transform.LookAt(pivot);

        // ── 7. Mark scene dirty & confirm ─────────────────────────────────
        EditorUtility.SetDirty(cam.gameObject);
        EditorUtility.SetDirty(player);

        string summary =
            $"Camera → {player.name}\n" +
            $"Distance: {ctrl.distance}  |  Height: {ctrl.heightOffset}\n" +
            $"Sensitivity X: {ctrl.sensitivityX}  |  Y: {ctrl.sensitivityY}\n" +
            $"Pitch: [{ctrl.minPitch}°, {ctrl.maxPitch}°]\n" +
            $"Player rotation sync: {ctrl.syncPlayerRotation}";

        Debug.Log("[SetupThirdPersonCamera] Done.\n" + summary);
        EditorUtility.DisplayDialog("Setup Complete ✓", summary, "Got it!");
    }

    // Validate: grey-out menu item when not in a loaded scene.
    [MenuItem("Tools/Setup 3rd Person Mouse Camera", true)]
    private static bool ValidateSetup()
        => UnityEngine.SceneManagement.SceneManager.GetActiveScene().isLoaded;
}
#endif
