using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// One-click setup: rebuilds LinuxLocomotion.controller (2D directional blend tree, Jump, Crouch)
/// and makes sure the player in the open scene uses it.
/// </summary>
public static class SetupPlayerAnimator
{
    private const string ControllerPath = "Assets/_Project/Animations/Controllers/LinuxLocomotion.controller";
    private const string ClipsDir = "Assets/_Project/Animations/Clips";

    [MenuItem("Tools/Setup Complete Locomotion & Animator")]
    public static void RunFromMenu()
    {
        AnimatorController ctrl = ConfigureController();

        GameObject existing = GameObject.Find("linux-skeleton");
        if (existing != null)
        {
            // A hand-made player: keep it, only wire the animator.
            Animator anim = existing.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
            }
            Debug.Log("[SetupPlayerAnimator] Controller assigned to existing 'linux-skeleton'.");
        }
        else
        {
            // Builds Player_LinuxSkeleton (physics, controller script, model, camera) using this controller.
            PlayerSetup.CreatePlayer();
        }

        Selection.activeObject = ctrl;
        Debug.Log("[SetupPlayerAnimator] Done. Press Play: WASD move, Space jump, Left Ctrl crouch.");
    }

    public static AnimatorController ConfigureController()
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        string crouchFbx = FindCrouchFbx();
        AnimationClip idle     = LoadClip(ClipsDir + "/quieto.fbx", true);
        AnimationClip forward  = LoadClip(ClipsDir + "/Run Forward.fbx", true);
        AnimationClip backward = LoadClip(ClipsDir + "/Run Backward.fbx", true);
        AnimationClip left     = LoadClip(ClipsDir + "/Run Left.fbx", true);
        AnimationClip right    = LoadClip(ClipsDir + "/Run Right.fbx", true);
        AnimationClip jump     = LoadClip(ClipsDir + "/Rifle Jump.fbx", false);
        AnimationClip crouch   = crouchFbx != null ? LoadClip(crouchFbx, true) : null;

        // Start clean so running this again never duplicates anything.
        for (int i = ctrl.parameters.Length - 1; i >= 0; i--) ctrl.RemoveParameter(i);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);
        foreach (AnimatorStateTransition t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);

        ctrl.AddParameter("InputX", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("InputZ", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);

        // Locomotion: 2D Freeform Directional blend tree.
        BlendTree tree;
        AnimatorState locomotion = ctrl.CreateBlendTreeInController("Locomotion", out tree);
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = "InputX";
        tree.blendParameterY = "InputZ";
        AddMotion(tree, idle, Vector2.zero);
        AddMotion(tree, forward, new Vector2(0f, 1f));
        AddMotion(tree, backward, new Vector2(0f, -1f));
        AddMotion(tree, left, new Vector2(-1f, 0f));
        AddMotion(tree, right, new Vector2(1f, 0f));
        sm.defaultState = locomotion;
        SetStatePosition(sm, locomotion, new Vector3(300f, 100f, 0f));

        AnimatorState sJump = sm.AddState("Jump", new Vector3(600f, 0f, 0f));
        sJump.motion = jump;
        AnimatorState sCrouch = sm.AddState("Crouch", new Vector3(600f, 200f, 0f));
        sCrouch.motion = crouch;

        // Any State -> Jump (trigger), then back to Locomotion when the clip ends.
        AnimatorStateTransition toJump = sm.AddAnyStateTransition(sJump);
        toJump.hasExitTime = false;
        toJump.hasFixedDuration = true;
        toJump.duration = 0.05f;
        toJump.canTransitionToSelf = false;
        toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

        AnimatorStateTransition fromJump = sJump.AddTransition(locomotion);
        fromJump.hasExitTime = true;
        fromJump.exitTime = 0.85f;
        fromJump.hasFixedDuration = true;
        fromJump.duration = 0.2f;

        // Locomotion <-> Crouch (IsCrouching)
        AnimatorStateTransition toCrouch = locomotion.AddTransition(sCrouch);
        toCrouch.hasExitTime = false;
        toCrouch.hasFixedDuration = true;
        toCrouch.duration = 0.15f;
        toCrouch.AddCondition(AnimatorConditionMode.If, 0f, "IsCrouching");

        AnimatorStateTransition fromCrouch = sCrouch.AddTransition(locomotion);
        fromCrouch.hasExitTime = false;
        fromCrouch.hasFixedDuration = true;
        fromCrouch.duration = 0.15f;
        fromCrouch.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");

        EnableIKPass(ctrl);

        string missing = "";
        if (idle == null) missing += " quieto";
        if (forward == null) missing += " 'Run Forward'";
        if (backward == null) missing += " 'Run Backward'";
        if (left == null) missing += " 'Run Left'";
        if (right == null) missing += " 'Run Right'";
        if (jump == null) missing += " 'Rifle Jump'";
        if (crouch == null) missing += " 'Crouch*'";
        if (missing.Length > 0) Debug.LogWarning("[SetupPlayerAnimator] Clips not found:" + missing);
        else Debug.Log("[SetupPlayerAnimator] LinuxLocomotion.controller configured.");

        return ctrl;
    }

    // OnAnimatorIK is silently skipped unless the layer has IK Pass enabled.
    // ctrl.layers returns copies, so the array must be written back.
    public static void EnableIKPass(AnimatorController ctrl)
    {
        AnimatorControllerLayer[] layers = ctrl.layers;
        for (int i = 0; i < layers.Length; i++) layers[i].iKPass = true;
        ctrl.layers = layers;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
    }

    // AnimatorState has no position; it lives on the ChildAnimatorState struct held by the state machine.
    private static void SetStatePosition(AnimatorStateMachine sm, AnimatorState state, Vector3 position)
    {
        ChildAnimatorState[] children = sm.states;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].state == state) children[i].position = position;
        }
        sm.states = children;
    }

    private static void AddMotion(BlendTree tree, AnimationClip clip, Vector2 position)
    {
        if (clip != null) tree.AddChild(clip, position);
    }

    // First .fbx in the clips folder whose name starts with "Crouch" (e.g. "Crouched Run").
    private static string FindCrouchFbx()
    {
        foreach (string guid in AssetDatabase.FindAssets("Crouch", new[] { ClipsDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".fbx")) return path;
        }
        return null;
    }

    // Loads the first real clip in the fbx, forcing Loop Time to the wanted value first.
    private static AnimationClip LoadClip(string fbxPath, bool loop)
    {
        AnimationClip clip = FirstClip(fbxPath);
        if (clip != null && clip.isLooping != loop)
        {
            SetLoop(fbxPath, loop);
            clip = FirstClip(fbxPath); // reimport invalidates the old reference
        }
        return clip;
    }

    private static AnimationClip FirstClip(string fbxPath)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        return null;
    }

    private static void SetLoop(string fbxPath, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation c in clips) c.loopTime = loop;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }
}
