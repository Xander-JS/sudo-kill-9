using UnityEngine;

/// <summary>
/// Marks a primitive as a level blockout placeholder.
/// Stores the component type, a target model ID, and notes for future asset replacement.
/// </summary>
public class EnvironmentPlaceholder : MonoBehaviour
{
    public enum ComponentType
    {
        Floor,
        ChipArena,
        TransistorCover,
        BadSector,
        CPU,
        Chip,
        RAM,
        Capacitor,
        CMOSBattery,
        Resistor,
        StorageDisk,
        GPU,
        NetworkCard,
        CoolerFan,
        IOPort,
        Boundary
    }

    [Header("Asset Replacement Metadata")]
    [Tooltip("Type of motherboard component this placeholder represents.")]
    public ComponentType componentType;

    [Tooltip("ID or Name of the final 3D model that will replace this primitive in production.")]
    public string targetModelID;

    // Backward compatibility for existing scripts
    public string TargetModelID
    {
        get => targetModelID;
        set => targetModelID = value;
    }

    [Tooltip("Recommended scale or pivot instructions for the 3D modeler/tech artist.")]
    [TextArea(2, 4)]
    public string replacementNotes;

    [Header("Gizmo Visualization")]
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private Color gizmoColor = new Color(0f, 0.8f, 1f, 0.35f);

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;

        Gizmos.color = gizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}
