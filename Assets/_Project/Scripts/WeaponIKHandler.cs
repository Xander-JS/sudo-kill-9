using UnityEngine;

/// <summary>
/// Two-handed weapon IK with one calibrated pose per animation (see WeaponPoseProfile).
/// The weapon is a child of the right-hand bone: its local pose is blended from the profile, and the right
/// hand is pushed out from the torso by IK. The left hand is locked to LeftHand_IK_Target, a child of the
/// weapon, so the grip follows every pose. Must sit on the same GameObject as the humanoid Animator, and the
/// Animator Controller layer needs "IK Pass" enabled (Tools > Weapon & IK Setup Wizard does both).
/// </summary>
[RequireComponent(typeof(Animator))]
public class WeaponIKHandler : MonoBehaviour
{
    [Header("Calibration")]
    [Tooltip("Saved poses, one per animation. Edited with Tools > Weapon & IK Setup Wizard.")]
    [SerializeField] private WeaponPoseProfile profile;
    [Tooltip("Editor only: which pose the Scene view shows while the game is not running.")]
    [SerializeField] private WeaponPoseKey previewPose = WeaponPoseKey.Idle;
    [Tooltip("Play mode only: hold a single pose regardless of animation, to calibrate it.")]
    [SerializeField] private bool forcePose;
    [SerializeField] private WeaponPoseKey forcedPose = WeaponPoseKey.Idle;

    [Header("Left Hand IK Target")]
    [Tooltip("Child of the weapon named LeftHand_IK_Target. Move/rotate it to align the fingers with the barrel.")]
    [SerializeField] private Transform leftHandTarget;
    [Tooltip("If on, the fields below drive the target's local transform. Turn off to move the target by hand.")]
    [SerializeField] private bool driveTargetFromFields = true;
    [SerializeField] private Vector3 gripLocalPosition;
    [SerializeField] private Vector3 gripLocalEuler;

    [Header("Weapon Socket")]
    [Tooltip("The socketed weapon (child of the right-hand bone).")]
    [SerializeField] private Transform weapon;
    [SerializeField] private bool driveWeaponTransform = true;

    [Header("Blending")]
    [Tooltip("How fast the weapon moves between poses. Higher = snappier.")]
    [SerializeField] private float transitionSpeed = 10f;
    [Tooltip("Fallback when the Animator has no InputX/InputZ: horizontal speed (m/s) above which RunForward is used.")]
    [SerializeField] private float movingSpeedThreshold = 0.5f;

    [Header("Animator Parameters / States")]
    [SerializeField] private string inputXParam = "InputX";
    [SerializeField] private string inputZParam = "InputZ";
    [SerializeField] private string isCrouchingParam = "IsCrouching";
    [SerializeField] private string jumpStateName = "Jump";

    [Header("Right Hand IK")]
    [Range(0f, 1f)] [SerializeField] private float rightHandWeight = 1f;

    [Header("Left Hand Weights")]
    [Range(0f, 1f)] [SerializeField] private float positionWeight = 1f;
    [Range(0f, 1f)] [SerializeField] private float rotationWeight = 1f;

    private Animator animator;
    private Rigidbody body;
    private int inputXHash, inputZHash, crouchHash;
    private bool hasInputParams, hasCrouchParam;

    private Vector3 currentPosition;
    private Quaternion currentRotation = Quaternion.identity;
    private Vector3 currentHandOffset;

    public Transform LeftHandTarget => leftHandTarget;
    public Transform Weapon => weapon;
    public WeaponPoseProfile Profile => profile;

    // Makes the grip point visible in the Scene view while tuning.
    private void OnDrawGizmos()
    {
        if (leftHandTarget == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(leftHandTarget.position, 0.03f);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(leftHandTarget.position, leftHandTarget.position + leftHandTarget.right * 0.08f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(leftHandTarget.position, leftHandTarget.position + leftHandTarget.up * 0.08f);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(leftHandTarget.position, leftHandTarget.position + leftHandTarget.forward * 0.08f);
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        body = GetComponentInParent<Rigidbody>();

        inputXHash = Animator.StringToHash(inputXParam);
        inputZHash = Animator.StringToHash(inputZParam);
        crouchHash = Animator.StringToHash(isCrouchingParam);
        bool hasX = false, hasZ = false;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == inputXHash && p.type == AnimatorControllerParameterType.Float) hasX = true;
            if (p.nameHash == inputZHash && p.type == AnimatorControllerParameterType.Float) hasZ = true;
            if (p.nameHash == crouchHash && p.type == AnimatorControllerParameterType.Bool) hasCrouchParam = true;
        }
        hasInputParams = hasX && hasZ;

        // Start at rest on the exact Idle pose.
        if (profile != null)
        {
            WeaponPoseProfile.Entry idle = profile.Get(WeaponPoseKey.Idle);
            currentPosition = idle.weaponPosition;
            currentRotation = Quaternion.Euler(idle.weaponEuler);
            currentHandOffset = idle.handOffset;
        }
        ApplyGrip();
        if (driveWeaponTransform && weapon != null)
        {
            weapon.localPosition = currentPosition;
            weapon.localRotation = currentRotation;
        }
    }

    private void OnValidate()
    {
        ApplyOffsets();
    }

    /// <summary>Edit mode: snaps the weapon and grip target to the pose chosen for preview.</summary>
    public void ApplyOffsets()
    {
        // In play mode Update owns the weapon pose; only the grip target is refreshed.
        if (!Application.isPlaying && driveWeaponTransform && weapon != null && profile != null)
        {
            WeaponPoseProfile.Entry e = profile.Get(previewPose);
            Vector3 position = e.weaponPosition;

            // No IK in edit mode, so the hand offset is previewed by moving the weapon (Awake resets it for play).
            if (weapon.parent != null)
                position += weapon.parent.InverseTransformVector(OffsetToWorld(e.handOffset));

            weapon.localPosition = position;
            weapon.localRotation = Quaternion.Euler(e.weaponEuler);
        }
        ApplyGrip();
    }

    private void ApplyGrip()
    {
        if (!driveTargetFromFields || leftHandTarget == null) return;
        leftHandTarget.localPosition = gripLocalPosition;
        leftHandTarget.localRotation = Quaternion.Euler(gripLocalEuler);
    }

    // Runs before the Animator evaluates, so the left-hand target (a child of the weapon) is current when IK reads it.
    private void Update()
    {
        if (!driveWeaponTransform || weapon == null || profile == null) return;

        Vector3 targetPosition, targetHand;
        Quaternion targetRotation;
        ComputeTargetPose(out targetPosition, out targetRotation, out targetHand);

        // Frame-rate independent blend factor.
        float t = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);
        currentPosition = Vector3.Lerp(currentPosition, targetPosition, t);
        currentRotation = Quaternion.Slerp(currentRotation, targetRotation, t);
        currentHandOffset = Vector3.Lerp(currentHandOffset, targetHand, t);

        weapon.localPosition = currentPosition;
        weapon.localRotation = currentRotation;
    }

    // Picks the pose(s) for what the character is doing right now and blends them like the Animator does.
    private void ComputeTargetPose(out Vector3 position, out Quaternion rotation, out Vector3 hand)
    {
        float[] w = new float[7];

        if (forcePose)
        {
            w[(int)forcedPose] = 1f;
        }
        else if (hasCrouchParam && animator.GetBool(crouchHash))
        {
            w[(int)WeaponPoseKey.Crouch] = 1f;
        }
        else if (IsInJumpState())
        {
            w[(int)WeaponPoseKey.Jump] = 1f;
        }
        else if (hasInputParams)
        {
            float x = animator.GetFloat(inputXHash);
            float z = animator.GetFloat(inputZHash);
            float magnitude = Mathf.Clamp01(new Vector2(x, z).magnitude);

            float forward = Mathf.Max(0f, z), backward = Mathf.Max(0f, -z);
            float right = Mathf.Max(0f, x), left = Mathf.Max(0f, -x);
            float sum = forward + backward + right + left;

            if (sum < 1e-4f)
            {
                w[(int)WeaponPoseKey.Idle] = 1f;
            }
            else
            {
                float scale = magnitude / sum;
                w[(int)WeaponPoseKey.RunForward] = forward * scale;
                w[(int)WeaponPoseKey.RunBackward] = backward * scale;
                w[(int)WeaponPoseKey.RunRight] = right * scale;
                w[(int)WeaponPoseKey.RunLeft] = left * scale;
                w[(int)WeaponPoseKey.Idle] = 1f - magnitude;
            }
        }
        else
        {
            bool moving = body != null && new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude > movingSpeedThreshold;
            w[(int)(moving ? WeaponPoseKey.RunForward : WeaponPoseKey.Idle)] = 1f;
        }

        position = Vector3.zero;
        hand = Vector3.zero;
        Quaternion first = Quaternion.identity;
        Vector4 q = Vector4.zero;
        bool haveFirst = false;
        float total = 0f;

        for (int i = 0; i < w.Length; i++)
        {
            if (w[i] <= 1e-5f) continue;
            WeaponPoseProfile.Entry e = profile.Get((WeaponPoseKey)i);
            Quaternion r = Quaternion.Euler(e.weaponEuler);
            if (!haveFirst) { first = r; haveFirst = true; }
            if (Quaternion.Dot(r, first) < 0f) r = new Quaternion(-r.x, -r.y, -r.z, -r.w); // shortest arc

            position += e.weaponPosition * w[i];
            hand += e.handOffset * w[i];
            q += new Vector4(r.x, r.y, r.z, r.w) * w[i];
            total += w[i];
        }

        if (total < 1e-5f)
        {
            WeaponPoseProfile.Entry idle = profile.Get(WeaponPoseKey.Idle);
            position = idle.weaponPosition;
            hand = idle.handOffset;
            rotation = Quaternion.Euler(idle.weaponEuler);
            return;
        }

        position /= total;
        hand /= total;
        float len = q.magnitude;
        rotation = len > 1e-5f ? new Quaternion(q.x / len, q.y / len, q.z / len, q.w / len) : first;
    }

    private bool IsInJumpState()
    {
        if (string.IsNullOrEmpty(jumpStateName)) return false;
        return animator.GetCurrentAnimatorStateInfo(0).IsName(jumpStateName) ||
               (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(jumpStateName));
    }

    // Character-space offset (x right, y up, z forward) as a world-space vector.
    private Vector3 OffsetToWorld(Vector3 offset)
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        return right * offset.x + Vector3.up * offset.y + forward * offset.z;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) animator = GetComponent<Animator>();

        // The weapon is a child of the right hand, so moving the hand moves the whole assembly. The left grip
        // target is read before IK moves the hand, so it gets the same shift to stay on the barrel.
        Vector3 reach = OffsetToWorld(currentHandOffset);

        // Right hand: keep the animated pose but push it out from the torso. The hand bone's transform in
        // OnAnimatorIK is the pre-IK animated pose.
        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (rightHand != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, rightHandWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightHand.position + reach);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightHand.rotation);
        }

        if (leftHandTarget == null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            return;
        }

        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, positionWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, rotationWeight);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget.position + reach);
        animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandTarget.rotation);
    }
}
