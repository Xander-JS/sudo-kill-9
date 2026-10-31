using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

/// <summary>
/// Weapon &amp; IK Setup Wizard: sockets the weapon, creates the left-hand IK target and calibrates one weapon
/// pose per animation. Poses are stored in a WeaponPoseProfile asset, so they persist (also when edited in Play mode).
/// </summary>
public class SetupWeaponIK : EditorWindow
{
    private const string WeaponFolder = "Assets/_Project/Models/Environment";
    private const string ClipsFolder = "Assets/_Project/Animations/Clips";
    private const string ProfilePath = "Assets/_Project/Data/WeaponPoseProfile.asset";
    private const string TargetName = "LeftHand_IK_Target";
    private const string FallbackControllerPath = "Assets/_Project/Animations/Controllers/LinuxLocomotion.controller";

    [SerializeField] private GameObject targetPlayer;
    [SerializeField] private int modelIndex;
    [SerializeField] private Vector3 gripPos;
    [SerializeField] private Vector3 gripEuler;
    [SerializeField] private float posRange = 1f;
    [SerializeField] private float weaponPosRange = 0.5f;
    [SerializeField] private float handOffsetRange = 0.6f;
    [SerializeField] private WeaponPoseKey poseKey = WeaponPoseKey.Idle;
    [SerializeField] private WeaponPoseProfile profile;

    // Animation preview (edit mode)
    [SerializeField] private bool animPreview;
    [SerializeField] private float animPreviewTime;

    private string[] modelPaths = new string[0];
    private string[] modelLabels = new string[0];
    private WeaponIKHandler handler;
    private Transform ikTarget;
    private Transform weaponTf;
    private Vector2 scroll;

    [MenuItem("Tools/Weapon & IK Setup Wizard")]
    public static void Open()
    {
        GetWindow<SetupWeaponIK>("Weapon & IK Setup");
    }

    private void OnEnable()
    {
        if (targetPlayer == null)
        {
            targetPlayer = GameObject.Find("Player_LinuxSkeleton");
            if (targetPlayer == null) targetPlayer = GameObject.Find("linux-skeleton");
        }
        RefreshModels();
        BindExistingSetup();
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        StopAnimPreview();
        SaveProfile();
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) StopAnimPreview();
    }

    // ------------------------------------------------------------------ //
    //  Asset scan / binding                                               //
    // ------------------------------------------------------------------ //
    private void RefreshModels()
    {
        List<string> paths = new List<string>();
        if (AssetDatabase.IsValidFolder(WeaponFolder))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { WeaponFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".fbx" || ext == ".obj" || ext == ".prefab") paths.Add(path);
            }
        }
        paths.Sort();

        modelPaths = paths.ToArray();
        modelLabels = new string[modelPaths.Length];
        for (int i = 0; i < modelPaths.Length; i++)
            modelLabels[i] = System.IO.Path.GetFileName(modelPaths[i]);
        modelIndex = Mathf.Clamp(modelIndex, 0, Mathf.Max(0, modelPaths.Length - 1));
    }

    private GameObject SelectedWeapon()
    {
        if (modelPaths.Length == 0 || modelIndex < 0 || modelIndex >= modelPaths.Length) return null;
        return AssetDatabase.LoadAssetAtPath<GameObject>(modelPaths[modelIndex]);
    }

    // Picks up a setup done earlier so the controls work right after reopening the window.
    private void BindExistingSetup()
    {
        handler = targetPlayer != null ? targetPlayer.GetComponentInChildren<WeaponIKHandler>() : null;
        ikTarget = handler != null ? handler.LeftHandTarget : null;
        if (ikTarget != null)
        {
            gripPos = ikTarget.localPosition;
            gripEuler = ikTarget.localEulerAngles;
        }

        weaponTf = handler != null ? handler.Weapon : null;
        if (weaponTf == null) weaponTf = FindSocketedWeapon();

        profile = handler != null ? handler.Profile : null;
        if (profile == null) profile = AssetDatabase.LoadAssetAtPath<WeaponPoseProfile>(ProfilePath);
    }

    // Finds a weapon already parented to the right hand.
    private Transform FindSocketedWeapon()
    {
        if (targetPlayer == null) return null;
        Animator animator = targetPlayer.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman) return null;
        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (rightHand == null) return null;

        string wanted = modelPaths.Length > 0
            ? System.IO.Path.GetFileNameWithoutExtension(modelPaths[Mathf.Clamp(modelIndex, 0, modelPaths.Length - 1)])
            : "SCAR";
        foreach (Transform child in rightHand)
        {
            if (child.name == TargetName) continue;
            if (child.name.StartsWith(wanted, System.StringComparison.OrdinalIgnoreCase) ||
                child.name.StartsWith("SCAR", System.StringComparison.OrdinalIgnoreCase))
                return child;
        }
        return null;
    }

    private static WeaponPoseProfile EnsureProfile()
    {
        WeaponPoseProfile p = AssetDatabase.LoadAssetAtPath<WeaponPoseProfile>(ProfilePath);
        if (p == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            p = CreateInstance<WeaponPoseProfile>();
            AssetDatabase.CreateAsset(p, ProfilePath);
        }
        p.EnsureAllEntries();
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
    }

    private void SaveProfile()
    {
        if (profile != null && EditorUtility.IsDirty(profile)) AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ //
    //  GUI                                                                //
    // ------------------------------------------------------------------ //
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        targetPlayer = (GameObject)EditorGUILayout.ObjectField("Target Player", targetPlayer, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck()) BindExistingSetup();

        EditorGUILayout.BeginHorizontal();
        if (modelPaths.Length > 0)
            modelIndex = EditorGUILayout.Popup("Weapon Model", modelIndex, modelLabels);
        else
            EditorGUILayout.LabelField("Weapon Model", "(none found)");
        if (GUILayout.Button("Rescan", GUILayout.Width(60))) RefreshModels();
        EditorGUILayout.EndHorizontal();

        GameObject weapon = SelectedWeapon();
        EditorGUILayout.ObjectField("Loaded Asset", weapon, typeof(GameObject), false);
        EditorGUILayout.Space();

        string problem = Validate(weapon);
        if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);

        using (new EditorGUI.DisabledScope(problem != null))
        {
            if (GUILayout.Button("Execute Setup", GUILayout.Height(32))) Execute(weapon);
        }

        DrawPoseControls();
        DrawGripControls();

        EditorGUILayout.EndScrollView();
    }

    private string Validate(GameObject weapon)
    {
        if (targetPlayer == null) return "Assign the Target Player (a scene object).";
        if (EditorUtility.IsPersistent(targetPlayer)) return "Target Player must be an object in the open scene, not an asset.";
        if (modelPaths.Length == 0) return "No .fbx/.obj/.prefab found in " + WeaponFolder + ".";
        if (weapon == null) return "The selected model could not be loaded from " + WeaponFolder + ".";

        Animator animator = targetPlayer.GetComponentInChildren<Animator>();
        if (animator == null) return "Target Player has no Animator.";
        if (!animator.isHuman) return "The Animator's avatar is not Humanoid (Rig > Animation Type), so there is no right-hand bone.";
        if (animator.GetBoneTransform(HumanBodyBones.RightHand) == null) return "No right-hand bone found in the avatar.";
        return null;
    }

    // ------------------------------------------------------------------ //
    //  Per-animation pose calibration                                     //
    // ------------------------------------------------------------------ //
    private void DrawPoseControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pose Calibration (one per animation)", EditorStyles.boldLabel);

        if (weaponTf == null || handler == null || profile == null)
        {
            EditorGUILayout.HelpBox("Run Execute Setup first; the calibration controls appear here once the weapon, " +
                                    "handler and pose profile exist.", MessageType.Info);
            return;
        }

        EditorGUI.BeginChangeCheck();
        poseKey = (WeaponPoseKey)EditorGUILayout.EnumPopup("Pose / Animation", poseKey);
        if (EditorGUI.EndChangeCheck())
        {
            PushPreview();
            if (animPreview) StartAnimPreview();
        }

        WeaponPoseProfile.Entry e = profile.Get(poseKey);

        EditorGUI.BeginChangeCheck();
        bool locked = EditorGUILayout.Toggle("Locked (protect from edits)", e.locked);
        if (EditorGUI.EndChangeCheck()) Edit(entry => entry.locked = locked);

        using (new EditorGUI.DisabledScope(e.locked))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto-Orient Barrel Forward")) { Vector3 v = AutoOrientEuler(e.weaponEuler); Edit(entry => entry.weaponEuler = v); }
            if (GUILayout.Button("Reset to Default"))
            {
                WeaponPoseProfile.Entry d = WeaponPoseProfile.DefaultFor(poseKey);
                Edit(entry => { entry.weaponPosition = d.weaponPosition; entry.weaponEuler = d.weaponEuler; entry.handOffset = d.handOffset; });
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Turn 180° (Y)")) Spin(Quaternion.Euler(0f, 180f, 0f));
            if (GUILayout.Button("Flip 180° (X)")) Spin(Quaternion.Euler(180f, 0f, 0f));
            if (GUILayout.Button("Roll 180° (Z)")) Spin(Quaternion.Euler(0f, 0f, 180f));
            EditorGUILayout.EndHorizontal();

            // Weapon rotation
            EditorGUI.BeginChangeCheck();
            Vector3 euler = EditorGUILayout.Vector3Field("Weapon Local Euler", e.weaponEuler);
            euler.x = EditorGUILayout.Slider("  X", euler.x, -180f, 180f);
            euler.y = EditorGUILayout.Slider("  Y", euler.y, -180f, 180f);
            euler.z = EditorGUILayout.Slider("  Z", euler.z, -180f, 180f);
            if (EditorGUI.EndChangeCheck()) Edit(entry => entry.weaponEuler = euler);

            // Weapon position (relative to the hand bone)
            EditorGUILayout.Space(4);
            weaponPosRange = Mathf.Max(0.01f, EditorGUILayout.FloatField("Position Slider Range (±)", weaponPosRange));
            EditorGUI.BeginChangeCheck();
            Vector3 pos = EditorGUILayout.Vector3Field("Weapon Local Position", e.weaponPosition);
            pos.x = EditorGUILayout.Slider("  X", pos.x, -weaponPosRange, weaponPosRange);
            pos.y = EditorGUILayout.Slider("  Y", pos.y, -weaponPosRange, weaponPosRange);
            pos.z = EditorGUILayout.Slider("  Z", pos.z, -weaponPosRange, weaponPosRange);
            if (EditorGUI.EndChangeCheck()) Edit(entry => entry.weaponPosition = pos);

            // Right-hand IK offset (moves the arm away from the torso)
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Hand Offset (character space, right-hand IK)", EditorStyles.boldLabel);
            handOffsetRange = Mathf.Max(0.05f, EditorGUILayout.FloatField("Offset Slider Range (±)", handOffsetRange));
            EditorGUI.BeginChangeCheck();
            Vector3 hand = EditorGUILayout.Vector3Field("Hand Offset", e.handOffset);
            hand.x = EditorGUILayout.Slider("  X (right)", hand.x, -handOffsetRange, handOffsetRange);
            hand.y = EditorGUILayout.Slider("  Y (up)", hand.y, -handOffsetRange, handOffsetRange);
            hand.z = EditorGUILayout.Slider("  Z (forward)", hand.z, -handOffsetRange, handOffsetRange);
            if (EditorGUI.EndChangeCheck()) Edit(entry => entry.handOffset = hand);

            if (GUILayout.Button("Copy this pose to all 4 Run poses")) CopyToRunPoses();
        }

        // Save
        EditorGUILayout.Space(6);
        bool dirty = EditorUtility.IsDirty(profile);
        GUI.backgroundColor = dirty ? new Color(1f, 0.85f, 0.4f) : Color.white;
        if (GUILayout.Button(dirty ? "Save Calibration  (unsaved changes)" : "Save Calibration", GUILayout.Height(28)))
            SaveCalibration();
        GUI.backgroundColor = Color.white;

        DrawPlayTools();
        DrawAnimPreview();
    }

    private void DrawPlayTools()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Play Mode", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Poses are saved in a project asset, so you can calibrate while the game runs: edits apply instantly " +
            "and are kept when you stop. Hold W/A/S/D, Space or Ctrl to see each animation's pose, or hold one pose below.",
            MessageType.None);

        SerializedObject so = new SerializedObject(handler);
        SerializedProperty force = so.FindProperty("forcePose");
        SerializedProperty forced = so.FindProperty("forcedPose");

        EditorGUI.BeginChangeCheck();
        bool holdPose = EditorGUILayout.Toggle("Hold selected pose (Play)", force.boolValue);
        if (EditorGUI.EndChangeCheck())
        {
            force.boolValue = holdPose;
            forced.enumValueIndex = (int)poseKey;
            so.ApplyModifiedProperties();
        }
        else if (force.boolValue && forced.enumValueIndex != (int)poseKey)
        {
            forced.enumValueIndex = (int)poseKey; // keep following the selected pose
            so.ApplyModifiedProperties();
        }
    }

    // Applies an edit to the selected entry: undoable, marks the asset dirty and refreshes the preview.
    private void Edit(System.Action<WeaponPoseProfile.Entry> change)
    {
        Undo.RecordObject(profile, "Edit Weapon Pose");
        change(profile.Get(poseKey));
        EditorUtility.SetDirty(profile);
        PushPreview();
    }

    private void Spin(Quaternion localSpin)
    {
        Vector3 v = (Quaternion.Euler(profile.Get(poseKey).weaponEuler) * localSpin).eulerAngles;
        Edit(entry => entry.weaponEuler = v);
    }

    private void CopyToRunPoses()
    {
        WeaponPoseProfile.Entry source = profile.Get(poseKey);
        Undo.RecordObject(profile, "Copy Pose to Run Poses");
        foreach (WeaponPoseKey k in new[] { WeaponPoseKey.RunForward, WeaponPoseKey.RunBackward, WeaponPoseKey.RunLeft, WeaponPoseKey.RunRight })
        {
            if (k == poseKey) continue;
            WeaponPoseProfile.Entry target = profile.Get(k);
            if (target.locked) continue;
            target.weaponPosition = source.weaponPosition;
            target.weaponEuler = source.weaponEuler;
            target.handOffset = source.handOffset;
        }
        EditorUtility.SetDirty(profile);
        PushPreview();
    }

    private void SaveCalibration()
    {
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupWeaponIK] Weapon pose calibration saved to " + ProfilePath);
        Repaint();
    }

    // Tells the handler which pose to show in the Scene view (edit mode) and refreshes it.
    private void PushPreview()
    {
        if (handler == null) return;
        SerializedObject so = new SerializedObject(handler);
        so.FindProperty("profile").objectReferenceValue = profile;
        so.FindProperty("previewPose").enumValueIndex = (int)poseKey;
        so.ApplyModifiedProperties();
        handler.ApplyOffsets();
        SceneView.RepaintAll();
        Repaint();
    }

    // ------------------------------------------------------------------ //
    //  Animation preview (edit mode, best effort)                         //
    // ------------------------------------------------------------------ //
    private void DrawAnimPreview()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Animation Preview (Scene view, edit mode)", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Not available in Play mode: the real animation is already running.", MessageType.None);
            return;
        }

        AnimationClip clip = animPreview ? ClipFor(poseKey) : null;
        EditorGUILayout.BeginHorizontal();
        if (!animPreview && GUILayout.Button("Preview this animation")) StartAnimPreview();
        if (animPreview && GUILayout.Button("Stop preview")) StopAnimPreview();
        EditorGUILayout.EndHorizontal();

        if (animPreview && clip != null)
        {
            EditorGUI.BeginChangeCheck();
            animPreviewTime = EditorGUILayout.Slider("Time (s)", animPreviewTime, 0f, clip.length);
            if (EditorGUI.EndChangeCheck()) SampleAnimPreview(clip);
        }
        EditorGUILayout.HelpBox(
            "Poses the character with the selected animation so you can judge the arms. IK is only evaluated at " +
            "runtime, so the hand offset shows as a shifted weapon here. If the pose does not change, calibrate in Play mode.",
            MessageType.None);
    }

    private void StartAnimPreview()
    {
        if (Application.isPlaying || targetPlayer == null) return;
        AnimationClip clip = ClipFor(poseKey);
        if (clip == null)
        {
            Debug.LogWarning("[SetupWeaponIK] No animation clip found for " + poseKey + ".");
            animPreview = false;
            return;
        }

        animPreviewTime = Mathf.Clamp(animPreviewTime, 0f, clip.length);
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        animPreview = true;
        SampleAnimPreview(clip);
    }

    private void SampleAnimPreview(AnimationClip clip)
    {
        Animator animator = targetPlayer != null ? targetPlayer.GetComponentInChildren<Animator>() : null;
        if (animator == null || clip == null || !AnimationMode.InAnimationMode()) return;

        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(animator.gameObject, clip, animPreviewTime);
        AnimationMode.EndSampling();
        PushPreview();
    }

    private void StopAnimPreview()
    {
        if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        animPreview = false;
        SceneView.RepaintAll();
    }

    private static AnimationClip ClipFor(WeaponPoseKey key)
    {
        string path = null;
        switch (key)
        {
            case WeaponPoseKey.Idle: path = ClipsFolder + "/quieto.fbx"; break;
            case WeaponPoseKey.RunForward: path = ClipsFolder + "/Run Forward.fbx"; break;
            case WeaponPoseKey.RunBackward: path = ClipsFolder + "/Run Backward.fbx"; break;
            case WeaponPoseKey.RunLeft: path = ClipsFolder + "/Run Left.fbx"; break;
            case WeaponPoseKey.RunRight: path = ClipsFolder + "/Run Right.fbx"; break;
            case WeaponPoseKey.Jump: path = ClipsFolder + "/Rifle Jump.fbx"; break;
            case WeaponPoseKey.Crouch:
                foreach (string guid in AssetDatabase.FindAssets("Crouch", new[] { ClipsFolder }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (p.EndsWith(".fbx")) { path = p; break; }
                }
                break;
        }
        if (path == null) return null;

        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        return null;
    }

    // ------------------------------------------------------------------ //
    //  Auto-orient (heuristic)                                            //
    // ------------------------------------------------------------------ //
    private Vector3 CharacterForward()
    {
        Animator animator = targetPlayer != null ? targetPlayer.GetComponentInChildren<Animator>() : null;
        Transform reference = animator != null ? animator.transform : targetPlayer.transform;
        Vector3 flat = Vector3.ProjectOnPlane(reference.forward, Vector3.up);
        return flat.sqrMagnitude > 1e-4f ? flat.normalized : reference.forward;
    }

    // The longest mesh axis is the barrel, the next is vertical, and the mesh extends away from the pivot (the grip
    // in the hand) toward the muzzle and sights. Computed against the character's forward in the current pose.
    private Vector3 AutoOrientEuler(Vector3 fallback)
    {
        Bounds lb;
        if (!TryGetLocalBounds(weaponTf, out lb))
        {
            Debug.LogWarning("[SetupWeaponIK] The weapon has no meshes to measure.");
            return fallback;
        }

        Vector3 size = lb.size;
        int barrel = LongestAxis(size, -1);
        int vertical = LongestAxis(size, barrel);

        float barrelOffset = lb.center[barrel];
        float verticalOffset = lb.center[vertical];
        if (Mathf.Abs(barrelOffset) < size[barrel] * 0.05f)
            Debug.LogWarning("[SetupWeaponIK] The weapon pivot sits near the middle of the mesh, so the muzzle direction " +
                             "is ambiguous. If the barrel ends up backwards, press 'Turn 180° (Y)'.");

        Vector3 barrelLocal = AxisVector(barrel) * (barrelOffset < 0f ? -1f : 1f);
        Vector3 upLocal = AxisVector(vertical) * (verticalOffset < 0f ? -1f : 1f);

        Quaternion desiredWorld = Quaternion.LookRotation(CharacterForward(), Vector3.up) *
                                  Quaternion.Inverse(Quaternion.LookRotation(barrelLocal, upLocal));
        Quaternion parentRot = weaponTf.parent != null ? weaponTf.parent.rotation : Quaternion.identity;
        return (Quaternion.Inverse(parentRot) * desiredWorld).eulerAngles;
    }

    private static int LongestAxis(Vector3 v, int exclude)
    {
        int best = -1;
        for (int i = 0; i < 3; i++)
        {
            if (i == exclude) continue;
            if (best < 0 || v[i] > v[best]) best = i;
        }
        return best;
    }

    private static Vector3 AxisVector(int i)
    {
        return i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
    }

    // Mesh bounds expressed in the weapon's own local space (rotation-independent).
    private static bool TryGetLocalBounds(Transform weapon, out Bounds result)
    {
        result = new Bounds();
        bool any = false;

        foreach (MeshFilter mf in weapon.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null) any |= Encapsulate(weapon, mf.transform, mf.sharedMesh.bounds, ref result, any);
        foreach (SkinnedMeshRenderer smr in weapon.GetComponentsInChildren<SkinnedMeshRenderer>())
            any |= Encapsulate(weapon, smr.transform, smr.localBounds, ref result, any);

        return any;
    }

    private static bool Encapsulate(Transform weapon, Transform meshTf, Bounds meshBounds, ref Bounds result, bool hasBounds)
    {
        Vector3 c = meshBounds.center;
        Vector3 e = meshBounds.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            Vector3 p = weapon.InverseTransformPoint(meshTf.TransformPoint(corner));
            if (!hasBounds) { result = new Bounds(p, Vector3.zero); hasBounds = true; }
            else result.Encapsulate(p);
        }
        return true;
    }

    // ------------------------------------------------------------------ //
    //  Left-hand grip                                                     //
    // ------------------------------------------------------------------ //
    private void DrawGripControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grip Tuning (LeftHand_IK_Target)", EditorStyles.boldLabel);

        if (handler == null || ikTarget == null)
        {
            EditorGUILayout.HelpBox("Run Execute Setup first; the grip controls appear here once the IK target exists.", MessageType.Info);
            return;
        }

        EditorGUI.BeginChangeCheck();

        posRange = Mathf.Max(0.01f, EditorGUILayout.FloatField("Position Slider Range (±)", posRange));
        gripPos = EditorGUILayout.Vector3Field("Grip Local Position", gripPos);
        gripPos.x = EditorGUILayout.Slider("  X", gripPos.x, -posRange, posRange);
        gripPos.y = EditorGUILayout.Slider("  Y", gripPos.y, -posRange, posRange);
        gripPos.z = EditorGUILayout.Slider("  Z", gripPos.z, -posRange, posRange);

        EditorGUILayout.Space(4);
        gripEuler = EditorGUILayout.Vector3Field("Grip Local Euler Rotation", gripEuler);
        gripEuler.x = EditorGUILayout.Slider("  X", gripEuler.x, -180f, 180f);
        gripEuler.y = EditorGUILayout.Slider("  Y", gripEuler.y, -180f, 180f);
        gripEuler.z = EditorGUILayout.Slider("  Z", gripEuler.z, -180f, 180f);

        if (EditorGUI.EndChangeCheck()) ApplyGrip();

        EditorGUILayout.HelpBox(
            "The grip is stored on the scene's WeaponIKHandler (the same for every pose). Changes made in Play mode " +
            "are lost when you stop; note the values and re-enter them in edit mode.",
            MessageType.None);
    }

    // Writes the grip to the handler's serialized fields (persisted in the scene) and to the target transform.
    private void ApplyGrip()
    {
        Undo.RecordObject(ikTarget, "Adjust Grip");
        ikTarget.localPosition = gripPos;
        ikTarget.localRotation = Quaternion.Euler(gripEuler);

        SerializedObject so = new SerializedObject(handler);
        so.FindProperty("gripLocalPosition").vector3Value = gripPos;
        so.FindProperty("gripLocalEuler").vector3Value = gripEuler;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        SceneView.RepaintAll();
    }

    // ------------------------------------------------------------------ //
    //  Setup pipeline                                                     //
    // ------------------------------------------------------------------ //
    private void Execute(GameObject weaponPrefab)
    {
        string problem = Validate(weaponPrefab);
        if (problem != null) { Debug.LogError("[SetupWeaponIK] " + problem); return; }

        StopAnimPreview();
        Animator animator = targetPlayer.GetComponentInChildren<Animator>();
        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

        // Re-running replaces the previous weapon, keeping the grip already tuned.
        Vector3? keptPos = null;
        Vector3? keptEuler = null;
        foreach (Transform child in rightHand)
        {
            if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) != weaponPrefab) continue;
            Transform oldTarget = child.Find(TargetName);
            if (oldTarget != null)
            {
                keptPos = oldTarget.localPosition;
                keptEuler = oldTarget.localEulerAngles;
            }
            Undo.DestroyObjectImmediate(child.gameObject);
            break;
        }

        // Instantiate and socket the weapon; the handler then applies the previewed pose.
        GameObject weapon = (GameObject)PrefabUtility.InstantiatePrefab(weaponPrefab, rightHand);
        Undo.RegisterCreatedObjectUndo(weapon, "Instantiate Weapon");
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;

        // Colliders on a held weapon would push the character around.
        foreach (Collider c in weapon.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (Rigidbody r in weapon.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);

        // IK target on the weapon.
        float longest;
        Vector3 defaultGrip = DefaultGripPosition(weapon.transform, out longest);
        GameObject targetGo = new GameObject(TargetName);
        targetGo.transform.SetParent(weapon.transform, false);
        targetGo.transform.localPosition = keptPos ?? defaultGrip;
        targetGo.transform.localRotation = Quaternion.Euler(keptEuler ?? Vector3.zero);
        posRange = Mathf.Max(0.01f, longest * 1.5f);

        // Pose profile (existing calibration is never overwritten).
        profile = EnsureProfile();

        // Handler on the Animator's GameObject (required for OnAnimatorIK), everything assigned.
        WeaponIKHandler h = animator.GetComponent<WeaponIKHandler>();
        if (h == null) h = Undo.AddComponent<WeaponIKHandler>(animator.gameObject);

        SerializedObject so = new SerializedObject(h);
        so.FindProperty("profile").objectReferenceValue = profile;
        so.FindProperty("previewPose").enumValueIndex = (int)WeaponPoseKey.Idle;
        so.FindProperty("forcePose").boolValue = false;
        so.FindProperty("leftHandTarget").objectReferenceValue = targetGo.transform;
        so.FindProperty("driveTargetFromFields").boolValue = true;
        so.FindProperty("gripLocalPosition").vector3Value = targetGo.transform.localPosition;
        so.FindProperty("gripLocalEuler").vector3Value = targetGo.transform.localEulerAngles;
        so.FindProperty("weapon").objectReferenceValue = weapon.transform;
        so.FindProperty("driveWeaponTransform").boolValue = true;
        so.ApplyModifiedProperties();
        h.ApplyOffsets();

        // IK Pass on the controller the Animator actually uses.
        AnimatorController ctrl = ResolveController(animator);
        if (ctrl != null) SetupPlayerAnimator.EnableIKPass(ctrl);
        else Debug.LogError("[SetupWeaponIK] No Animator Controller found; IK Pass was not enabled.");

        poseKey = WeaponPoseKey.Idle;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        BindExistingSetup();
        Repaint();
        Debug.Log($"[SetupWeaponIK] '{weaponPrefab.name}' socketed to '{rightHand.name}'. Pose profile: {ProfilePath}. " +
                  $"IK Pass enabled on '{(ctrl != null ? ctrl.name : "none")}'. Calibrate each pose in the wizard and press Save Calibration.");
    }

    private static AnimatorController ResolveController(Animator animator)
    {
        RuntimeAnimatorController rac = animator.runtimeAnimatorController;
        if (rac is AnimatorOverrideController over) rac = over.runtimeAnimatorController;
        if (rac is AnimatorController ac) return ac;

        AnimatorController fallback = AssetDatabase.LoadAssetAtPath<AnimatorController>(FallbackControllerPath);
        if (fallback != null) animator.runtimeAnimatorController = fallback;
        return fallback;
    }

    // Starting guess: the front quarter of the weapon along its longest axis.
    private static Vector3 DefaultGripPosition(Transform weapon, out float longest)
    {
        longest = 1f;
        Renderer[] renderers = weapon.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return Vector3.zero;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        Vector3 centerLocal = weapon.InverseTransformPoint(b.center);
        Vector3 ext = weapon.InverseTransformVector(b.extents);
        Vector3 abs = new Vector3(Mathf.Abs(ext.x), Mathf.Abs(ext.y), Mathf.Abs(ext.z));

        Vector3 axis = Vector3.forward;
        longest = abs.z;
        if (abs.x > longest) { axis = Vector3.right; longest = abs.x; }
        if (abs.y > longest) { axis = Vector3.up; longest = abs.y; }

        return centerLocal + axis * (longest * 0.5f);
    }
}
