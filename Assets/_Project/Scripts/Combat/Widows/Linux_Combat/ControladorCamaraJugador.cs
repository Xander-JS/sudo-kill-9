using UnityEngine;

/// <summary>
/// Script dedicado para personalizar la cámara del jugador (Play y Editor) mediante barras (sliders)
/// y valores numéricos en el Inspector:
/// - Subir o bajar la cámara (Altura de Pivote y Ajuste Fino Vertical)
/// - Acercar o alejar la cámara (Distancia al Jugador)
/// - Mover la cámara a la izquierda, centro o derecha del hombro (Offset Lateral)
/// - Inclinar la vista más arriba o más abajo (Inclinación Vertical)
/// - Ajustar el Zoom / Campo de Visión (FOV) y la sensibilidad del ratón
/// Se sincroniza en tiempo real con LinuxCombatIK, CameraPivot y MainCamera.
/// </summary>
[AddComponentMenu("SudoKill9/Controlador de Cámara del Jugador (Barras)")]
public class ControladorCamaraJugador : MonoBehaviour
{
    [Header("1. Barras de Altura y Posición de Cámara (Play)")]
    [Tooltip("Sube o baja el punto base de la cámara respecto a los pies del jugador.")]
    [Range(-1.0f, 3.5f)]
    public float alturaPivote = 1.25f;

    [Tooltip("Ajuste fino para poner la cámara MÁS ARRIBA (+) o MÁS ABAJO (-).")]
    [Range(-2.5f, 2.5f)]
    public float offsetVerticalCamara = -0.58f;

    [Tooltip("Distancia de la cámara detrás del jugador (menor = más cerca, mayor = más lejos).")]
    [Range(0.5f, 8.0f)]
    public float distanciaCamara = 2.6f;

    [Tooltip("Posición lateral sobre el hombro (+ derecha, 0 centro, - izquierda).")]
    [Range(-2.5f, 2.5f)]
    public float offsetLateralHombro = 0.45f;

    [Header("2. Ángulo de Vista y Zoom (FOV)")]
    [Tooltip("Inclina la cámara para mirar más hacia arriba (negativo) o más hacia abajo (positivo).")]
    [Range(-45f, 45f)]
    public float inclinacionVerticalOffset = 0f;

    [Tooltip("Campo de visión (Zoom) de la cámara en modo Play (60 = estándar).")]
    [Range(30f, 110f)]
    public float campoDeVisionFOV = 60f;

    [Header("3. Sensibilidad del Ratón y Colisiones")]
    [Tooltip("Velocidad de giro izquierda/derecha con el ratón.")]
    [Range(0.1f, 10f)]
    public float sensibilidadHorizontal = 2.0f;

    [Tooltip("Velocidad de giro arriba/abajo con el ratón.")]
    [Range(0.1f, 10f)]
    public float sensibilidadVertical = 2.0f;

    [Tooltip("Ángulo mínimo de inclinación vertical con el ratón (mirar hacia arriba).")]
    [Range(-85f, 0f)]
    public float anguloMinimo = -40f;

    [Tooltip("Ángulo máximo de inclinación vertical con el ratón (mirar hacia abajo).")]
    [Range(0f, 85f)]
    public float anguloMaximo = 60f;

    [Tooltip("Suavizado del movimiento de cámara (0 = respuesta inmediata).")]
    [Range(0f, 0.3f)]
    public float suavizadoCamara = 0f;

    [Tooltip("Invierte el eje vertical al mover el ratón.")]
    public bool invertirEjeVertical = false;

    [Tooltip("Evita que la cámara atraviese paredes u obstáculos detrás del jugador.")]
    public bool evitarColisionesCamara = true;

    [Header("4. Calibración Manual del Cañón (Salida de Balas)")]
    [Tooltip("Permite ajustar la posición exacta por donde salen las balas respecto a la SCAR-H.")]
    public bool ajusteManualMuzzle = true;

    [Tooltip("Offset del cañón en metros: X = Frente/Atrás, Y = Arriba/Abajo, Z = Izquierda/Derecha.")]
    public Vector3 offsetMuzzleManual = new Vector3(0.42f, 0.008f, 0f);

    [Tooltip("Rotación del cañón (por defecto 0, 90, 0 para orientar a lo largo del fusil).")]
    public Vector3 rotacionMuzzleManual = new Vector3(0f, 90f, 0f);

    [Header("Referencia Automática")]
    [Tooltip("Referencia al componente LinuxCombatIK del jugador (se detecta automáticamente).")]
    public LinuxCombatIK sistemaCamaraJugador;

    private void OnEnable()
    {
        VincularYLeerDesdeJugador();
    }

    /// <summary>
    /// Busca automáticamente el componente LinuxCombatIK del jugador en la escena.
    /// </summary>
    public LinuxCombatIK ObtenerSistemaCamara()
    {
        if (sistemaCamaraJugador != null) return sistemaCamaraJugador;

        sistemaCamaraJugador = GetComponent<LinuxCombatIK>();
        if (sistemaCamaraJugador == null)
            sistemaCamaraJugador = GetComponentInChildren<LinuxCombatIK>(true);
        if (sistemaCamaraJugador == null)
            sistemaCamaraJugador = GetComponentInParent<LinuxCombatIK>(true);
        if (sistemaCamaraJugador == null)
            sistemaCamaraJugador = Object.FindFirstObjectByType<LinuxCombatIK>();

        return sistemaCamaraJugador;
    }

    /// <summary>
    /// Sincroniza las barras de este componente leyendo los valores actuales de LinuxCombatIK.
    /// </summary>
    public void VincularYLeerDesdeJugador()
    {
        LinuxCombatIK ik = ObtenerSistemaCamara();
        if (ik == null) return;

        alturaPivote = ik.alturaPivote;
        offsetVerticalCamara = ik.offsetVerticalCamara;
        distanciaCamara = ik.distanciaCamara;
        offsetLateralHombro = ik.offsetLateralHombro;
        inclinacionVerticalOffset = ik.inclinacionVerticalOffset;
        campoDeVisionFOV = ik.campoDeVisionFOV;
        sensibilidadHorizontal = ik.sensibilidadHorizontal;
        sensibilidadVertical = ik.sensibilidadVertical;
        anguloMinimo = ik.anguloMinimo;
        anguloMaximo = ik.anguloMaximo;
        suavizadoCamara = ik.suavizadoCamara;
        invertirEjeVertical = ik.invertirEjeVertical;
        evitarColisionesCamara = ik.evitarColisionesCamara;
        ajusteManualMuzzle = ik.ajusteManualMuzzle;
        offsetMuzzleManual = ik.offsetMuzzleManual;
        rotacionMuzzleManual = ik.rotacionMuzzleManual;
    }

    /// <summary>
    /// Envía los valores de las barras a LinuxCombatIK y actualiza inmediatamente la cámara en escena/Play.
    /// </summary>
    public void AplicarCambiosACamara()
    {
        LinuxCombatIK ik = ObtenerSistemaCamara();
        if (ik == null) return;

        if (anguloMinimo > anguloMaximo) anguloMinimo = anguloMaximo;

        ik.alturaPivote = alturaPivote;
        ik.offsetVerticalCamara = offsetVerticalCamara;
        ik.distanciaCamara = distanciaCamara;
        ik.offsetLateralHombro = offsetLateralHombro;
        ik.inclinacionVerticalOffset = inclinacionVerticalOffset;
        ik.campoDeVisionFOV = campoDeVisionFOV;
        ik.sensibilidadHorizontal = sensibilidadHorizontal;
        ik.sensibilidadVertical = sensibilidadVertical;
        ik.anguloMinimo = anguloMinimo;
        ik.anguloMaximo = anguloMaximo;
        ik.suavizadoCamara = suavizadoCamara;
        ik.invertirEjeVertical = invertirEjeVertical;
        ik.evitarColisionesCamara = evitarColisionesCamara;
        ik.ajusteManualMuzzle = ajusteManualMuzzle;
        ik.offsetMuzzleManual = offsetMuzzleManual;
        ik.rotacionMuzzleManual = rotacionMuzzleManual;

        ik.AplicarAjustesCamaraEnTiempoReal();
        if (ik.muzzlePoint != null && ik.Weapon != null && ik.muzzlePoint.parent == ik.Weapon && ajusteManualMuzzle)
        {
            ik.muzzlePoint.localPosition = offsetMuzzleManual;
            ik.muzzlePoint.localRotation = Quaternion.Euler(rotacionMuzzleManual);
        }
    }

    private void OnValidate()
    {
        AplicarCambiosACamara();
    }
}
