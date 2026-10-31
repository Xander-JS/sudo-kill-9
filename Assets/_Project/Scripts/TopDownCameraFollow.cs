using UnityEngine;

/// <summary>Smoothly follows a target from a fixed top-down offset.</summary>
public class TopDownCameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 14f, -8f);
    public float smoothTime = 0.12f;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null) return;
        transform.position = Vector3.SmoothDamp(transform.position, target.position + offset, ref velocity, smoothTime);
        transform.rotation = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
    }
}
