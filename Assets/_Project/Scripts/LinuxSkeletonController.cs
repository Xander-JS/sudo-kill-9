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
    [SerializeField] private float crouchSpeed = 2.5f;
    [Tooltip("Units/sec^2 while speeding up. High = snappy.")]
    [SerializeField] private float acceleration = 70f;
    [Tooltip("Units/sec^2 while releasing input. High = crisp stop.")]
    [SerializeField] private float deceleration = 90f;
    [Tooltip("Fraction of acceleration/deceleration available in the air.")]
    [Range(0f, 1f)] [SerializeField] private float airControl = 0.5f;

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
    private bool isGrounded;
    private bool isCrouching;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;

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

        // Animator root motion would fight the Rigidbody.
        foreach (Animator anim in GetComponentsInChildren<Animator>())
            anim.applyRootMotion = false;

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
            $"Grounded: {isGrounded}  Crouching: {isCrouching}\n" +
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

        crouchHeld = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
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
    //  Horizontal movement: MoveTowards gives snappy, exact stops      //
    // ---------------------------------------------------------------- //
    private void HandleMovement()
    {
        float speed = isCrouching ? crouchSpeed : walkSpeed;
        Vector3 targetVel = new Vector3(moveInput.x, 0f, moveInput.y) * speed;

        Vector3 vel = rb.linearVelocity;
        Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);

        bool hasInput = moveInput.sqrMagnitude > 0.001f;
        float rate = hasInput ? acceleration : deceleration;
        if (!isGrounded) rate *= airControl;

        horizontal = Vector3.MoveTowards(horizontal, targetVel, rate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(horizontal.x, vel.y, horizontal.z);

        // Face movement direction (top-down).
        if (hasInput)
        {
            Quaternion targetRot = Quaternion.LookRotation(new Vector3(moveInput.x, 0f, moveInput.y), Vector3.up);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, 900f * Time.fixedDeltaTime));
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
