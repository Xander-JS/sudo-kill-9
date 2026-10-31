using UnityEngine;

/// <summary>
/// Marks a primitive as a level blockout placeholder.
/// Stores the component type and a model ID for future asset replacement.
/// </summary>
public class EnvironmentPlaceholder : MonoBehaviour
{
    public enum ComponentType
    {
        Floor,
        ChipArena,
        TransistorCover,
        BadSector
    }

    [Tooltip("Type of motherboard component this placeholder represents.")]
    public ComponentType componentType;

    [Tooltip("ID of the 3D model that will replace this primitive in production.")]
    public string TargetModelID;
}
