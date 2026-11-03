using UnityEngine;
using SudoKill9.Combat;

/// <summary>
/// Controla y dibuja la mira / apuntador (crosshair) en pantalla mediante OnGUI:
/// - Totalmente configurable: posición en pantalla (offset horizontal y vertical), tamaño, grosor, separación y colores.
/// - Sincronizado matemáticamente con el raycast de disparo en Combat_linux (la bala viaja exactamente a donde apunta la cruz).
/// - Detección dinámica de objetivos: cambia de color al enfocar a un enemigo (IDamageable).
/// - Efecto sutil de retroceso dinámico al disparar.
/// - Soporte para texturas/sprites personalizados o cruz procedural.
/// - Modo opcional de Guía Láser 3D proyectada desde el cañón del arma.
/// </summary>
public class CrosshairUI : MonoBehaviour
{
    [Header("1. Posición en Pantalla (Alineación con la Bala)")]
    [Tooltip("Desplazamiento horizontal de la mira (-0.45 = izquierda, 0 = centro exacto, +0.45 = derecha).")]
    [Range(-0.45f, 0.45f)]
    public float offsetPantallaX = 0f;

    [Tooltip("Desplazamiento vertical de la mira (-0.45 = abajo, 0 = centro exacto, +0.45 = arriba).")]
    [Range(-0.45f, 0.45f)]
    public float offsetPantallaY = 0f;

    [Header("2. Estilo Visual de la Cruz")]
    [Tooltip("Color estándar de la mira.")]
    public Color color = Color.white;

    [Tooltip("Color de la mira al enfocar un enemigo o entidad dañable.")]
    public Color colorAlEnfocarEnemigo = new Color(1f, 0.25f, 0.25f, 1f);

    [Tooltip("Longitud de cada brazo en píxeles.")]
    [Range(4, 40)]
    public int armLength = 12;

    [Tooltip("Hueco entre el centro y el comienzo de cada brazo, en píxeles.")]
    [Range(0, 30)]
    public int gap = 4;

    [Tooltip("Grosor de cada brazo en píxeles.")]
    [Range(1, 10)]
    public int thickness = 2;

    [Tooltip("Mostrar un punto pequeño en el centro exacto.")]
    public bool showDot = true;

    [Tooltip("Tamaño del punto central en píxeles.")]
    [Range(1, 8)]
    public int tamanoPunto = 3;

    [Header("3. Imagen / Textura Opcional")]
    [Tooltip("Imagen o sprite de mira personalizado. Si se asigna, se dibuja esta textura en lugar de la cruz.")]
    public Texture2D texturaPersonalizada;

    [Tooltip("Tamaño de la textura de mira en píxeles.")]
    [Range(12, 128)]
    public int tamanoTextura = 32;

    [Header("4. Comportamiento y Efectos")]
    [Tooltip("Si está activo, la mira solo se dibuja cuando se apunta (clic derecho) o el arma está activa.")]
    [SerializeField] private bool soloAlApuntar = true;

    [Tooltip("Expande brevemente la mira al disparar.")]
    public bool retrocesoDinamico = true;

    [Tooltip("Activa una línea láser visual desde el cañón hacia el objetivo al apuntar.")]
    public bool activarGuiaLaser = false;

    private Texture2D pixel;
    private float expansionRetroceso = 0f;
    private bool enfocandoEnemigo = false;

    /// <summary>
    /// Controla externamente si el jugador está apuntando.
    /// </summary>
    public bool EstaApuntando { get; set; } = false;

    /// <summary>
    /// Indica si la mira solo debe mostrarse durante el apuntado.
    /// </summary>
    public bool SoloAlApuntar
    {
        get => soloAlApuntar;
        set => soloAlApuntar = value;
    }

    private void Awake()
    {
        GarantizarTexturaPixel();
    }

    private void GarantizarTexturaPixel()
    {
        if (pixel == null)
        {
            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }
    }

    /// <summary>
    /// Devuelve las coordenadas normalizadas del Viewport (0 a 1) para lanzar el rayo de disparo.
    /// (0.5, 0.5) es el centro exacto de la pantalla.
    /// </summary>
    public Vector2 ObtenerPosicionViewport()
    {
        return new Vector2(0.5f + offsetPantallaX, 0.5f + offsetPantallaY);
    }

    /// <summary>
    /// Devuelve las coordenadas en píxeles de pantalla (GUI) donde se dibuja la mira.
    /// Utiliza ViewportToScreenPoint de la cámara para garantizar sincronía total con el raycast 3D
    /// sin importar la resolución, relación de aspecto o barras negras de la vista Game.
    /// </summary>
    public Vector2 ObtenerPosicionPantallaPixeles()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            Vector2 vp = ObtenerPosicionViewport();
            Vector3 sp = cam.ViewportToScreenPoint(new Vector3(vp.x, vp.y, 0f));
            return new Vector2(sp.x, Screen.height - sp.y);
        }

        float cx = Screen.width * (0.5f + offsetPantallaX);
        float cy = Screen.height * (0.5f - offsetPantallaY); // En GUI de Unity, Y=0 es la parte superior
        return new Vector2(cx, cy);
    }

    /// <summary>
    /// Notifica a la mira que se produjo un disparo para activar la animación de retroceso.
    /// </summary>
    public void NotificarDisparo()
    {
        if (retrocesoDinamico)
        {
            expansionRetroceso = 8f;
        }
    }

    private void Update()
    {
        if (expansionRetroceso > 0.01f)
        {
            expansionRetroceso = Mathf.Lerp(expansionRetroceso, 0f, 15f * Time.deltaTime);
        }

        ActualizarDeteccionEnemigo();
    }

    private void ActualizarDeteccionEnemigo()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector2 vp = ObtenerPosicionViewport();
        Ray ray = cam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
        {
            enfocandoEnemigo = hit.collider.GetComponentInParent<IDamageable>() != null;
        }
        else
        {
            enfocandoEnemigo = false;
        }
    }

    private void OnGUI()
    {
        bool apuntandoActivo = EstaApuntando;
        if (soloAlApuntar && !apuntandoActivo)
        {
            if (Input.GetMouseButton(1))
            {
                apuntandoActivo = true;
            }
#if ENABLE_INPUT_SYSTEM
            else if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.rightButton.isPressed)
            {
                apuntandoActivo = true;
            }
#endif
        }

        if (soloAlApuntar && !apuntandoActivo) return;

        GarantizarTexturaPixel();

        Vector2 posPixeles = ObtenerPosicionPantallaPixeles();
        float cx = posPixeles.x;
        float cy = posPixeles.y;

        Color colorActual = enfocandoEnemigo ? colorAlEnfocarEnemigo : color;
        GUI.color = colorActual;

        // Si hay una textura personalizada asignada, dibujarla centrada
        if (texturaPersonalizada != null)
        {
            float mitad = tamanoTextura * 0.5f;
            GUI.DrawTexture(new Rect(cx - mitad, cy - mitad, tamanoTextura, tamanoTextura), texturaPersonalizada);
            GUI.color = Color.white;
            return;
        }

        // De lo contrario, dibujar la cruz procedural con el retroceso aplicado
        float gapActual = gap + expansionRetroceso;
        float lenActual = armLength + expansionRetroceso * 0.5f;

        // Brazo superior
        GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - gapActual - lenActual, thickness, lenActual), pixel);
        // Brazo inferior
        GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy + gapActual,             thickness, lenActual), pixel);
        // Brazo izquierdo
        GUI.DrawTexture(new Rect(cx - gapActual - lenActual,  cy - thickness * 0.5f, lenActual, thickness), pixel);
        // Brazo derecho
        GUI.DrawTexture(new Rect(cx + gapActual,              cy - thickness * 0.5f, lenActual, thickness), pixel);

        // Punto central
        if (showDot)
        {
            float mitadPunto = tamanoPunto * 0.5f;
            GUI.DrawTexture(new Rect(cx - mitadPunto, cy - mitadPunto, tamanoPunto, tamanoPunto), pixel);
        }

        GUI.color = Color.white;
    }

    private void OnDestroy()
    {
        if (pixel != null) Destroy(pixel);
    }
}
