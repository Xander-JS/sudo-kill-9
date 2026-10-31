using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Physics-based, weighty top-down controller for "linux-skeleton".
/// WASD moves on X/Z, Space jumps on Y, Left Ctrl crouches.
/// Uses the new Input System (Keyboard.current), no Input Actions asset required.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class LinuxSkeletonController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 6f;
    [Tooltip("Speed multiplier while holding Left Shift and moving.")]
    [SerializeField] private float sprintMultiplier = 1.7f;
    [Tooltip("Speed multiplier while holding Left Ctrl.")]
    [Range(0.1f, 1f)] [SerializeField] private float crouchMultiplier = 0.4f;
    [Tooltip("SmoothDamp time (s) when starting to move. Lower = snappier.")]
    [SerializeField] private float accelSmoothTime = 0.08f;
    [Tooltip("SmoothDamp time (s) when releasing input. Lower = crisper stop.")]
    [SerializeField] private float decelSmoothTime = 0.05f;
    [Tooltip("Multiplier on the smooth time while airborne. Higher = less air control.")]
    [SerializeField] private float airSmoothMultiplier = 2f;

    [Header("Rotation")]
    [Tooltip("Higher = turns faster. Used as Slerp(t = turnSpeed * deltaTime).")]
    [SerializeField] private float turnSpeed = 12f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string inputXParam = "InputX";
    [SerializeField] private string inputZParam = "InputZ";
    [SerializeField] private string jumpTriggerParam = "Jump";
    [SerializeField] private string isCrouchingParam = "IsCrouching";
    [Tooltip("SmoothDamp time (s) for InputX/InputZ fed to the blend tree.")]
    [SerializeField] private float animInputSmoothTime = 0.1f;

    [Header("Jump & Gravity")]
    [SerializeField] private float jumpHeight = 1.6f;
    [Tooltip("Gravity multiplier while rising.")]
    [SerializeField] private float riseGravityMultiplier = 2f;
    [Tooltip("Gravity multiplier while falling. Higher = heavier landing.")]
    [SerializeField] private float fallGravityMultiplier = 3.5f;
    [Tooltip("Extra multiplier when jump is released early (short hop).")]
    [SerializeField] private float lowJumpMultiplier = 2.5f;
    [SerializeField] private float coyoteTime = 0.08f;
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [Range(0f, 89f)] [SerializeField] private float maxSlopeAngle = 50f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    [Header("Crouch")]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private Vector2 moveInput;
    private bool jumpHeld;
    private bool crouchHeld;
    private bool sprintHeld;
    private bool isRunning;
    private bool isGrounded;
    private bool isCrouching;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;
    private Vector3 smoothVelocityRef;
    private int inputXHash, inputZHash, jumpHash, isCrouchingHash;
    private bool hasInputParams, hasJumpParam, hasIsCrouchingParam;
    private Vector2 animInput;          // smoothed, character-local (x = strafe, y = forward)
    private Vector2 animInputVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        // We apply our own gravity; keep the character upright.
        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        standHeight = capsule.height;
        ApplyCapsule(standHeight);

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // Root motion would fight the Rigidbody.
            animator.applyRootMotion = false;
            inputXHash = Animator.StringToHash(inputXParam);
            inputZHash = Animator.StringToHash(inputZParam);
            jumpHash = Animator.StringToHash(jumpTriggerParam);
            isCrouchingHash = Animator.StringToHash(isCrouchingParam);

            bool hasX = false, hasZ = false;
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == inputXHash && p.type == AnimatorControllerParameterType.Float) hasX = true;
                if (p.nameHash == inputZHash && p.type == AnimatorControllerParameterType.Float) hasZ = true;
                if (p.nameHash == jumpHash && p.type == AnimatorControllerParameterType.Trigger) hasJumpParam = true;
                if (p.nameHash == isCrouchingHash && p.type == AnimatorControllerParameterType.Bool) hasIsCrouchingParam = true;
            }
            hasInputParams = hasX && hasZ;
            if (!hasInputParams || !hasJumpParam || !hasIsCrouchingParam)
                Debug.LogWarning("[LinuxSkeletonController] Animator is missing InputX/InputZ (Float), Jump (Trigger) or " +
                                 "IsCrouching (Bool). Run Tools > Setup Complete Locomotion & Animator.");
        }
        else
        {
            Debug.LogWarning("[LinuxSkeletonController] No Animator found in children.");
        }

#if UNITY_EDITOR
        // Deliver keyboard input to the game even if the Game view isn't focused.
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        if (Keyboard.current == null)
            Debug.LogWarning("[LinuxSkeletonController] No Keyboard detected by the Input System.");
    }

    private void OnGUI()
    {
        if (!showDebug) return;
        GUI.Label(new Rect(10, 10, 500, 90),
            $"Input: {moveInput}  Jump:{jumpHeld}  Crouch:{crouchHeld}\n" +
            $"Grounded: {isGrounded}  Running: {isRunning}  Crouching: {isCrouching}\n" +
            $"Velocity: {(rb != null ? rb.linearVelocity : Vector3.zero)}");
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // Raw (non-smoothed) input: smoothing is done by physics acceleration.
        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        moveInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);

        jumpHeld = kb.spaceKey.isPressed;
        if (kb.spaceKey.wasPressedThisFrame) lastJumpPressedTime = Time.time;

        crouchHeld = kb.leftCtrlKey.isPressed;
        sprintHeld = kb.leftShiftKey.isPressed;

        // Running only while Shift is held, moving, and not crouching.
        // Re-evaluated every frame, so the state holds as long as the keys do.
        isRunning = sprintHeld && moveInput.sqrMagnitude > 0.001f && !isCrouching;

        UpdateAnimator();
    }

    // Feeds the 2D blend tree with the movement direction expressed in the character's own
    // frame: forward/back/left/right relative to where the model faces, never to the camera.
    private void UpdateAnimator()
    {
        if (animator == null) return;

        if (hasInputParams)
        {
            Vector3 worldInput = new Vector3(moveInput.x, 0f, moveInput.y);
            Vector3 local = transform.InverseTransformDirection(worldInput);
            Vector2 target = new Vector2(local.x, local.z);

            animInput = Vector2.SmoothDamp(animInput, target, ref animInputVelocity, animInputSmoothTime);
            animator.SetFloat(inputXHash, animInput.x);
            animator.SetFloat(inputZHash, animInput.y);
        }
        if (hasIsCrouchingParam) animator.SetBool(isCrouchingHash, isCrouching);
    }

    private void FixedUpdate()
    {
        CheckGround();
        HandleCrouch();
        HandleMovement();
        HandleJump();
        ApplyCustomGravity();
    }

    // ---------------------------------------------------------------- //
    //  Ground check (SphereCast from just above the capsule's bottom)  //
    // ---------------------------------------------------------------- //
    private void CheckGround()
    {
        float radius = capsule.radius * 0.95f;
        Vector3 origin = transform.TransformPoint(capsule.center)
                         + Vector3.down * (capsule.height * 0.5f - capsule.radius);
        float castDistance = groundCheckDistance + 0.05f;

        isGrounded = false;
        if (Physics.SphereCast(origin + Vector3.up * 0.05f, radius, Vector3.down, out RaycastHit hit,
                castDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            isGrounded = Vector3.Angle(hit.normal, Vector3.up) <= maxSlopeAngle;
        }

        if (isGrounded) lastGroundedTime = Time.time;
    }

    // ---------------------------------------------------------------- //
    //  Horizontal movement: SmoothDamp on X/Z only, Y is left untouched //
    // ---------------------------------------------------------------- //
    private void HandleMovement()
    {
        float speed = walkSpeed;
        if (isCrouching) speed *= crouchMultiplier;
        else if (isRunning) speed *= sprintMultiplier;
        Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        Vector3 targetVel = inputDirection * speed;

        Vector3 vel = rb.linearVelocity;
        Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);

        bool hasInput = inputDirection.sqrMagnitude > 0.001f;
        float smoothTime = hasInput ? accelSmoothTime : decelSmoothTime;
        if (!isGrounded) smoothTime *= airSmoothMultiplier;

        horizontal = Vector3.SmoothDamp(horizontal, targetVel, ref smoothVelocityRef, smoothTime);

        // Kill the tail of the curve so the character stops exactly.
        if (!hasInput && horizontal.sqrMagnitude < 0.0025f) horizontal = Vector3.zero;

        rb.linearVelocity = new Vector3(horizontal.x, vel.y, horizontal.z);

        // Gradual Y-only rotation toward the input direction (never snaps).
        if (hasInput)
        {
            Quaternion targetRot = Quaternion.LookRotation(inputDirection.normalized, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, turnSpeed * Time.deltaTime));
        }
    }

    // ---------------------------------------------------------------- //
    //  Jump with coyote time + input buffer                            //
    // ---------------------------------------------------------------- //
    private void HandleJump()
    {
        bool buffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool canJump = Time.time - lastGroundedTime <= coyoteTime;

        if (buffered && canJump && !isCrouching)
        {
            // v = sqrt(2 * g * h), using the *rising* gravity so height matches the tooltip.
            float g = Mathf.Abs(Physics.gravity.y) * riseGravityMultiplier;
            float jumpVelocity = Mathf.Sqrt(2f * g * jumpHeight);

            Vector3 vel = rb.linearVelocity;
            rb.linearVelocity = new Vector3(vel.x, jumpVelocity, vel.z);

            lastJumpPressedTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            isGrounded = false;

            if (animator != null && hasJumpParam) animator.SetTrigger(jumpHash);
        }
    }

    // ---------------------------------------------------------------- //
    //  Custom gravity: heavier on the way down                         //
    // ---------------------------------------------------------------- //
    private void ApplyCustomGravity()
    {
        float vy = rb.linearVelocity.y;
        float multiplier;

        if (vy < 0f) multiplier = fallGravityMultiplier;
        else if (vy > 0f && !jumpHeld) multiplier = lowJumpMultiplier;
        else multiplier = riseGravityMultiplier;

        // Keep the character pinned to the ground instead of micro-bouncing.
        if (isGrounded && vy <= 0f) multiplier = 1f;

        rb.AddForce(Physics.gravity * multiplier, ForceMode.Acceleration);
    }

    // ---------------------------------------------------------------- //
    //  Crouch: resize collider, keep feet planted, check ceiling       //
    // ---------------------------------------------------------------- //
    private void HandleCrouch()
    {
        if (crouchHeld && !isCrouching)
        {
            isCrouching = true;
            ApplyCapsule(crouchHeight);
        }
        else if (!crouchHeld && isCrouching && CanStandUp())
        {
            isCrouching = false;
            ApplyCapsule(standHeight);
        }
    }

    // Capsule center is lowered so the bottom of the collider never moves.
    private void ApplyCapsule(float height)
    {
        capsule.height = height;
        capsule.center = new Vector3(capsule.center.x, height * 0.5f, capsule.center.z);
    }

    // Check the volume the standing capsule would occupy, ignoring ourselves.
    private bool CanStandUp()
    {
        float radius = capsule.radius * 0.95f;
        Vector3 basePos = transform.position;
        Vector3 bottom = basePos + Vector3.up * (radius + 0.02f);
        Vector3 top = basePos + Vector3.up * (standHeight - radius);

        Collider[] hits = Physics.OverlapCapsule(bottom, top, radius, groundMask, QueryTriggerInteraction.Ignore);
        foreach (Collider c in hits)
        {
            if (c != capsule && !c.transform.IsChildOf(transform)) return false;
        }
        return true;
    }
}
