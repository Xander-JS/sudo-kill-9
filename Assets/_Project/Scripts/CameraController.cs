using UnityEngine;

/// <summary>
/// ARPG-style follow camera: high, pulled-back, fixed rotation, elastic position.
/// Attach to the Main Camera.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Player to follow. If empty, the first LinuxSkeletonController found is used.")]
    [SerializeField] private Transform target;

    [Header("Framing")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 15f, -15f);
    [Tooltip("Fixed camera rotation (Euler). 45 on X = classic isometric tilt.")]
    [SerializeField] private Vector3 fixedRotation = new Vector3(45f, 0f, 0f);

    [Header("Smoothing")]
    [Tooltip("Approx. time (s) for the camera to catch up. Higher = more elastic.")]
    [SerializeField] private float smoothTime = 0.25f;

    private Vector3 velocity;

    private void Start()
    {
        if (target == null)
        {
            LinuxSkeletonController player = FindFirstObjectByType<LinuxSkeletonController>();
            if (player != null) target = player.transform;
        }

        // Start already framed on the target instead of sweeping in from elsewhere.
        if (target != null) transform.position = target.position + offset;
        transform.rotation = Quaternion.Euler(fixedRotation);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        transform.position = Vector3.SmoothDamp(transform.position, target.position + offset, ref velocity, smoothTime);
        transform.rotation = Quaternion.Euler(fixedRotation);
    }
}
