using UnityEngine;

/// <summary>
/// Dibuja una mira (crosshair) en el centro de la pantalla mediante OnGUI.
/// Adjunta al Main Camera o a cualquier GameObject activo.
/// </summary>
public class CrosshairUI : MonoBehaviour
{
    [Header("Mira")]
    [SerializeField] private Color color = Color.white;
    [Tooltip("Longitud de cada brazo en píxeles.")]
    [SerializeField] private int armLength = 12;
    [Tooltip("Hueco entre el centro y el comienzo de cada brazo, en píxeles.")]
    [SerializeField] private int gap = 4;
    [Tooltip("Grosor de cada brazo en píxeles.")]
    [SerializeField] private int thickness = 2;
    [Tooltip("Mostrar un punto pequeño en el centro.")]
    [SerializeField] private bool showDot = true;

    private Texture2D pixel;

    private void Awake()
    {
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
    }

    private void OnGUI()
    {
        float cx = Screen.width  * 0.5f;
        float cy = Screen.height * 0.5f;

        GUI.color = color;

        // Brazo superior
        GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - gap - armLength, thickness, armLength), pixel);
        // Brazo inferior
        GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy + gap,             thickness, armLength), pixel);
        // Brazo izquierdo
        GUI.DrawTexture(new Rect(cx - gap - armLength,  cy - thickness * 0.5f, armLength, thickness), pixel);
        // Brazo derecho
        GUI.DrawTexture(new Rect(cx + gap,              cy - thickness * 0.5f, armLength, thickness), pixel);

        // Punto central
        if (showDot)
            GUI.DrawTexture(new Rect(cx - 1, cy - 1, 3, 3), pixel);

        GUI.color = Color.white;
    }

    private void OnDestroy()
    {
        if (pixel != null) Destroy(pixel);
    }
}
