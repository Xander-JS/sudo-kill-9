using UnityEngine;

/// <summary>
/// Third-Person orbit camera for "The Last Root".
/// Attach to Main Camera. Rotates via Mouse X / Mouse Y (legacy Input axes).
/// Auto-finds Player_LinuxSkeleton on Start — zero manual setup required.
/// </summary>
public class ThirdPersonCameraController : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────────
    //  Inspector fields
    // ──────────────────────────────────────────────────────────────────────

    [Header("Target")]
    [Tooltip("Player pivot. Auto-assigned on Start if left empty.")]
    public Transform target;

    [Header("Orbit Distance")]
    [Tooltip("Distance from the camera to the target pivot.")]
    public float distance = 4f;
    [Tooltip("World-Y height offset above the target's foot pivot.")]
    public float heightOffset = 1.4f;

    [Header("Mouse Sensitivity")]
    public float sensitivityX = 3f;   // horizontal (yaw)
    public float sensitivityY = 2f;   // vertical   (pitch)

    [Header("Pitch Clamp  (degrees)")]
    public float minPitch = -20f;
    public float maxPitch =  60f;

    [Header("Smoothing")]
    [Tooltip("SmoothDamp time for camera position. Lower = snappier.")]
    public float positionSmoothTime = 0.10f;

    [Header("Player Sync")]
    [Tooltip("Rotate the player body's Y to match the camera yaw every frame.")]
    public bool  syncPlayerRotation  = true;
    [Tooltip("Slerp speed for the body-to-yaw sync.")]
    public float playerRotationSpeed = 15f;

    // ──────────────────────────────────────────────────────────────────────
    //  Public read-only state (LinuxSkeletonController reads Yaw for movement)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Current horizontal aim angle (degrees). Used by LinuxSkeletonController
    /// to make WASD move in the camera-forward direction.</summary>
    public float Yaw   { get; private set; }

    /// <summary>Current vertical aim angle (degrees).</summary>
    public float Pitch { get; private set; }

    // ──────────────────────────────────────────────────────────────────────
    //  Private
    // ──────────────────────────────────────────────────────────────────────

    private Rigidbody playerRb;
    private Vector3   posVelocity;

    // ──────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ──────────────────────────────────────────────────────────────────────

    private void Start()
    {
        // Auto-find player if not assigned (e.g. when Setup Tool hasn't run yet).
        if (target == null)
        {
            var player = FindFirstObjectByType<LinuxSkeletonController>();
            if (player != null)
            {
                target = player.transform;
                Debug.Log("[ThirdPersonCamera] Auto-assigned target → " + target.name);
            }
            else
            {
                Debug.LogWarning("[ThirdPersonCamera] Player_LinuxSkeleton not found. Assign 'target' manually.");
                return;
            }
        }

        playerRb = target.GetComponent<Rigidbody>();

        // Seed angles from current camera transform so the camera doesn't
        // snap on the first frame.
        Yaw   = transform.eulerAngles.y;
        Pitch = transform.eulerAngles.x;

        // Position immediately on the target so there is no sweep-in.
        transform.position = DesiredPosition();
        transform.LookAt(Pivot());

        // Lock & hide cursor for mouse-look.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // ── 1. Read mouse axes (legacy Input — requires Input Handling = "Both") ──
        Yaw   += Input.GetAxis("Mouse X") * sensitivityX;
        Pitch -= Input.GetAxis("Mouse Y") * sensitivityY;   // subtract = natural feel (up = up)
        Pitch  = Mathf.Clamp(Pitch, minPitch, maxPitch);

        // ── 2. Smooth-damp position toward orbit point ──
        transform.position = Vector3.SmoothDamp(
            transform.position, DesiredPosition(), ref posVelocity, positionSmoothTime);

        // ── 3. Always face the pivot (look-at handles pitch automatically) ──
        transform.LookAt(Pivot());

        // ── 4. Sync player body Y to camera yaw ──
        if (syncPlayerRotation)
            SyncPlayerYaw();
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Point the camera looks at — player position + height offset.</summary>
    private Vector3 Pivot() => target.position + Vector3.up * heightOffset;

    /// <summary>Desired camera world position: orbit point behind+above player.</summary>
    private Vector3 DesiredPosition()
    {
        Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
        return Pivot() + rot * new Vector3(0f, 0f, -distance);
    }

    /// <summary>Smoothly rotate the player body to face the camera's horizontal aim.</summary>
    private void SyncPlayerYaw()
    {
        Quaternion targetRot = Quaternion.Euler(0f, Yaw, 0f);
        if (playerRb != null)
            // rb.rotation is safe to set in LateUpdate (no physics conflict since
            // LinuxSkeletonController sets freezeRotation = true).
            playerRb.rotation = Quaternion.Slerp(
                playerRb.rotation, targetRot, playerRotationSpeed * Time.deltaTime);
        else
            target.rotation = Quaternion.Slerp(
                target.rotation, targetRot, playerRotationSpeed * Time.deltaTime);
    }
}
