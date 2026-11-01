using UnityEngine;

/// <summary>
/// Visual marker for player spawn points in the scene editor.
/// Displays an editor gizmo with heading direction without adding runtime overhead.
/// </summary>
public class SpawnPointMarker : MonoBehaviour
{
    [Header("Gizmo Display")]
    [SerializeField] private Color markerColor = new Color(0.2f, 0.9f, 0.4f, 0.85f);
    [SerializeField] private float radius = 0.5f;

    private void OnDrawGizmos()
    {
        Gizmos.color = markerColor;
        Vector3 center = transform.position + Vector3.up * radius;
        Gizmos.DrawWireSphere(center, radius);

        // Forward heading vector
        Gizmos.color = Color.cyan;
        Vector3 forwardTip = center + transform.forward * 1.8f;
        Gizmos.DrawLine(center, forwardTip);
        Gizmos.DrawLine(forwardTip, forwardTip - transform.forward * 0.4f + transform.right * 0.25f);
        Gizmos.DrawLine(forwardTip, forwardTip - transform.forward * 0.4f - transform.right * 0.25f);
    }
}
