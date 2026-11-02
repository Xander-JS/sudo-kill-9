using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Editor = UnityEditor.Editor;

namespace SudoKill9.EditorTools
{
    /// <summary>
    /// Sistema de Inspector personalizado y ventana de barras (sliders) para ajustar la cámara de Play
    /// (Más Arriba / Más Abajo, Izquierda / Derecha, Cerca / Lejos, Ángulo e Inclinación, Zoom FOV).
    /// Incluye guardado automático al modificar las barras durante el modo Play.
    /// </summary>
    [InitializeOnLoad]
    public static class LinuxCameraPlayModePersistence
    {
        private const string PrefPrefix = "SudoKill9_LinuxCam_";
        private const string KeyPending = PrefPrefix + "PendingSave";
        private const string KeyAlturaPivote = PrefPrefix + "AlturaPivote";
        private const string KeyOffsetVertical = PrefPrefix + "OffsetVertical";
        private const string KeyDistancia = PrefPrefix + "Distancia";
        private const string KeyOffsetLateral = PrefPrefix + "OffsetLateral";
        private const string KeyInclinacion = PrefPrefix + "Inclinacion";
        private const string KeyFOV = PrefPrefix + "FOV";
        private const string KeySensH = PrefPrefix + "SensH";
        private const string KeySensV = PrefPrefix + "SensV";
        private const string KeyAnguloMin = PrefPrefix + "AnguloMin";
        private const string KeyAnguloMax = PrefPrefix + "AnguloMax";
        private const string KeySuavizado = PrefPrefix + "Suavizado";
        private const string KeyInvertY = PrefPrefix + "InvertY";
        private const string KeyColisiones = PrefPrefix + "Colisiones";

        static LinuxCameraPlayModePersistence()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += EnsureCameraControllersInScene;
        }

        public static void EnsureCameraControllersInScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            LinuxCombatIK ik = Object.FindFirstObjectByType<LinuxCombatIK>();
            if (ik == null) return;

            bool dirty = false;

            // Asegurar que el propio objeto con LinuxCombatIK tenga ControladorCamaraJugador
            if (ik.GetComponent<ControladorCamaraJugador>() == null)
            {
                var ctrl = ik.gameObject.AddComponent<ControladorCamaraJugador>();
                ctrl.sistemaCamaraJugador = ik;
                ctrl.VincularYLeerDesdeJugador();
                EditorUtility.SetDirty(ctrl);
                dirty = true;
            }

            // Asegurar que el padre (Player_LinuxSkeleton) también tenga ControladorCamaraJugador para acceso rápido
            if (ik.transform.parent != null && ik.transform.parent.GetComponent<ControladorCamaraJugador>() == null)
            {
                var ctrlParent = ik.transform.parent.gameObject.AddComponent<ControladorCamaraJugador>();
                ctrlParent.sistemaCamaraJugador = ik;
                ctrlParent.VincularYLeerDesdeJugador();
                EditorUtility.SetDirty(ctrlParent);
                dirty = true;
            }

            // Asegurar que CameraPivot también tenga ControladorCamaraJugador
            if (ik.pivotCamara != null && ik.pivotCamara.GetComponent<ControladorCamaraJugador>() == null)
            {
                var ctrlPivot = ik.pivotCamara.gameObject.AddComponent<ControladorCamaraJugador>();
                ctrlPivot.sistemaCamaraJugador = ik;
                ctrlPivot.VincularYLeerDesdeJugador();
                EditorUtility.SetDirty(ctrlPivot);
                dirty = true;
            }

            // Asegurar que MainCamera también tenga ControladorCamaraJugador por si el usuario selecciona la cámara
            if (ik.camaraPrincipal != null && ik.camaraPrincipal.GetComponent<ControladorCamaraJugador>() == null)
            {
                var ctrlCam = ik.camaraPrincipal.gameObject.AddComponent<ControladorCamaraJugador>();
                ctrlCam.sistemaCamaraJugador = ik;
                ctrlCam.VincularYLeerDesdeJugador();
                EditorUtility.SetDirty(ctrlCam);
                dirty = true;
            }

            SincronizarTodosLosControladores(ik);

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(ik.gameObject.scene);
            }
        }

        public static void RegistrarCambioDesdeInspector(LinuxCombatIK ik)
        {
            if (ik == null) return;

            ik.AplicarAjustesCamaraEnTiempoReal();
            SincronizarTodosLosControladores(ik);

            if (Application.isPlaying)
            {
                EditorPrefs.SetBool(KeyPending, true);
                EditorPrefs.SetFloat(KeyAlturaPivote, ik.alturaPivote);
                EditorPrefs.SetFloat(KeyOffsetVertical, ik.offsetVerticalCamara);
                EditorPrefs.SetFloat(KeyDistancia, ik.distanciaCamara);
                EditorPrefs.SetFloat(KeyOffsetLateral, ik.offsetLateralHombro);
                EditorPrefs.SetFloat(KeyInclinacion, ik.inclinacionVerticalOffset);
                EditorPrefs.SetFloat(KeyFOV, ik.campoDeVisionFOV);
                EditorPrefs.SetFloat(KeySensH, ik.sensibilidadHorizontal);
                EditorPrefs.SetFloat(KeySensV, ik.sensibilidadVertical);
                EditorPrefs.SetFloat(KeyAnguloMin, ik.anguloMinimo);
                EditorPrefs.SetFloat(KeyAnguloMax, ik.anguloMaximo);
                EditorPrefs.SetFloat(KeySuavizado, ik.suavizadoCamara);
                EditorPrefs.SetBool(KeyInvertY, ik.invertirEjeVertical);
                EditorPrefs.SetBool(KeyColisiones, ik.evitarColisionesCamara);
            }
            else
            {
                EditorUtility.SetDirty(ik);
                if (ik.pivotCamara != null) EditorUtility.SetDirty(ik.pivotCamara);
                if (ik.camaraPrincipal != null)
                {
                    EditorUtility.SetDirty(ik.camaraPrincipal);
                    Camera cam = ik.camaraPrincipal.GetComponent<Camera>();
                    if (cam != null) EditorUtility.SetDirty(cam);
                }
                EditorSceneManager.MarkSceneDirty(ik.gameObject.scene);
                SceneView.RepaintAll();
            }
        }

        public static void SincronizarTodosLosControladores(LinuxCombatIK ik)
        {
            if (ik == null) return;
            var controladores = Object.FindObjectsByType<ControladorCamaraJugador>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in controladores)
            {
                if (c == null) continue;
                c.sistemaCamaraJugador = ik;
                c.alturaPivote = ik.alturaPivote;
                c.offsetVerticalCamara = ik.offsetVerticalCamara;
                c.distanciaCamara = ik.distanciaCamara;
                c.offsetLateralHombro = ik.offsetLateralHombro;
                c.inclinacionVerticalOffset = ik.inclinacionVerticalOffset;
                c.campoDeVisionFOV = ik.campoDeVisionFOV;
                c.sensibilidadHorizontal = ik.sensibilidadHorizontal;
                c.sensibilidadVertical = ik.sensibilidadVertical;
                c.anguloMinimo = ik.anguloMinimo;
                c.anguloMaximo = ik.anguloMaximo;
                c.suavizadoCamara = ik.suavizadoCamara;
                c.invertirEjeVertical = ik.invertirEjeVertical;
                c.evitarColisionesCamara = ik.evitarColisionesCamara;
                if (!Application.isPlaying)
                {
                    EditorUtility.SetDirty(c);
                }
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (!EditorPrefs.GetBool(KeyPending, false)) return;
                EditorPrefs.SetBool(KeyPending, false);

                LinuxCombatIK ik = Object.FindFirstObjectByType<LinuxCombatIK>();
                if (ik == null) return;

                Undo.RecordObject(ik, "Guardar Ajustes de Cámara de Play Mode");
                ik.alturaPivote = EditorPrefs.GetFloat(KeyAlturaPivote, ik.alturaPivote);
                ik.offsetVerticalCamara = EditorPrefs.GetFloat(KeyOffsetVertical, ik.offsetVerticalCamara);
                ik.distanciaCamara = EditorPrefs.GetFloat(KeyDistancia, ik.distanciaCamara);
                ik.offsetLateralHombro = EditorPrefs.GetFloat(KeyOffsetLateral, ik.offsetLateralHombro);
                ik.inclinacionVerticalOffset = EditorPrefs.GetFloat(KeyInclinacion, ik.inclinacionVerticalOffset);
                ik.campoDeVisionFOV = EditorPrefs.GetFloat(KeyFOV, ik.campoDeVisionFOV);
                ik.sensibilidadHorizontal = EditorPrefs.GetFloat(KeySensH, ik.sensibilidadHorizontal);
                ik.sensibilidadVertical = EditorPrefs.GetFloat(KeySensV, ik.sensibilidadVertical);
                ik.anguloMinimo = EditorPrefs.GetFloat(KeyAnguloMin, ik.anguloMinimo);
                ik.anguloMaximo = EditorPrefs.GetFloat(KeyAnguloMax, ik.anguloMaximo);
                ik.suavizadoCamara = EditorPrefs.GetFloat(KeySuavizado, ik.suavizadoCamara);
                ik.invertirEjeVertical = EditorPrefs.GetBool(KeyInvertY, ik.invertirEjeVertical);
                ik.evitarColisionesCamara = EditorPrefs.GetBool(KeyColisiones, ik.evitarColisionesCamara);

                RegistrarCambioDesdeInspector(ik);
                Debug.Log("<color=green>[Cámara del Jugador]</color> Los ajustes de barras realizados durante Play Mode se han guardado automáticamente en la escena.");
            }
        }

        /// <summary>
        /// Dibuja el panel interactivo de barras de cámara dentro de cualquier Inspector o ventana.
        /// </summary>
        public static void DibujarPanelBarrasCamara(LinuxCombatIK ik)
        {
            if (ik == null)
            {
                EditorGUILayout.HelpBox("No se encontró el componente LinuxCombatIK en la escena actual.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13
            };
            EditorGUILayout.LabelField("🎥 CONTROL DE CÁMARA DE PLAY (BARRAS / SLIDERS)", titleStyle);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "MODO PLAY ACTIVO: Presiona ESC para liberar el ratón y mueve estas barras en vivo.\n" +
                    "✔ Tus cambios se guardarán automáticamente al salir de Play.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Mueve las barras o escribe el valor numérico para subir, bajar, acercar o mover la cámara de Play.",
                    MessageType.None);
            }

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("1. Posición Vertical (Subir / Bajar Cámara)", EditorStyles.boldLabel);

            float nuevaAlturaPivote = EditorGUILayout.Slider(
                new GUIContent("Altura Base (Arriba / Abajo)", "Sube o baja el pivote principal de la cámara respecto al suelo."),
                ik.alturaPivote, -1.0f, 3.5f);

            float nuevoOffsetVertical = EditorGUILayout.Slider(
                new GUIContent("Ajuste Fino Vertical (+/-)", "Negativo = baja más la cámara. Positivo = sube más la cámara."),
                ik.offsetVerticalCamara, -2.5f, 2.5f);

            float alturaTotal = nuevaAlturaPivote + nuevoOffsetVertical;
            EditorGUILayout.LabelField($"   ↳ Altura total resultante sobre el jugador: {alturaTotal:F2} m", EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("2. Distancia y Posición Lateral (Hombro)", EditorStyles.boldLabel);

            float nuevaDistancia = EditorGUILayout.Slider(
                new GUIContent("Distancia (Cerca / Lejos)", "Qué tan cerca o lejos está la cámara detrás del jugador."),
                ik.distanciaCamara, 0.5f, 8.0f);

            float nuevoOffsetLateral = EditorGUILayout.Slider(
                new GUIContent("Posición Lateral (Izq / Der)", "0 = centrada detrás del jugador, + = hombro derecho, - = hombro izquierdo."),
                ik.offsetLateralHombro, -2.5f, 2.5f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("3. Inclinación de Mirada y Zoom (FOV)", EditorStyles.boldLabel);

            float nuevaInclinacion = EditorGUILayout.Slider(
                new GUIContent("Inclinación Vista (Arriba/Abajo)", "Inclina el ángulo de la cámara para mirar más arriba (-) o más abajo (+)."),
                ik.inclinacionVerticalOffset, -45f, 45f);

            float nuevoFOV = EditorGUILayout.Slider(
                new GUIContent("Zoom / Campo de Visión (FOV)", "60 = normal, menor = más cerca (zoom), mayor = más amplio."),
                ik.campoDeVisionFOV, 30f, 110f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("4. Sensibilidad, Límites del Ratón y Colisiones", EditorStyles.boldLabel);

            float nuevaSensH = EditorGUILayout.Slider(
                new GUIContent("Sensibilidad Horizontal (X)", "Velocidad de giro horizontal con el ratón."),
                ik.sensibilidadHorizontal, 0.1f, 10f);

            float nuevaSensV = EditorGUILayout.Slider(
                new GUIContent("Sensibilidad Vertical (Y)", "Velocidad de giro vertical con el ratón."),
                ik.sensibilidadVertical, 0.1f, 10f);

            float nuevoAnguloMin = EditorGUILayout.Slider(
                new GUIContent("Límite Mirar Arriba (Min °)", "Ángulo máximo hacia arriba al mover el ratón."),
                ik.anguloMinimo, -85f, 0f);

            float nuevoAnguloMax = EditorGUILayout.Slider(
                new GUIContent("Límite Mirar Abajo (Max °)", "Ángulo máximo hacia abajo al mover el ratón."),
                ik.anguloMaximo, 0f, 85f);

            float nuevoSuavizado = EditorGUILayout.Slider(
                new GUIContent("Suavizado de Cámara (s)", "0 = respuesta instantánea; mayor = giro más suave."),
                ik.suavizadoCamara, 0f, 0.3f);

            bool nuevoInvertY = EditorGUILayout.Toggle(
                new GUIContent("Invertir Eje Vertical", "Invierte la dirección arriba/abajo del ratón."),
                ik.invertirEjeVertical);

            bool nuevasColisiones = EditorGUILayout.Toggle(
                new GUIContent("Evitar Atravesar Paredes", "Acerca la cámara automáticamente si hay una pared detrás."),
                ik.evitarColisionesCamara);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ik, "Ajustar Barras de Cámara del Jugador");
                ik.alturaPivote = nuevaAlturaPivote;
                ik.offsetVerticalCamara = nuevoOffsetVertical;
                ik.distanciaCamara = nuevaDistancia;
                ik.offsetLateralHombro = nuevoOffsetLateral;
                ik.inclinacionVerticalOffset = nuevaInclinacion;
                ik.campoDeVisionFOV = nuevoFOV;
                ik.sensibilidadHorizontal = nuevaSensH;
                ik.sensibilidadVertical = nuevaSensV;
                ik.anguloMinimo = Mathf.Min(nuevoAnguloMin, nuevoAnguloMax);
                ik.anguloMaximo = Mathf.Max(nuevoAnguloMin, nuevoAnguloMax);
                ik.suavizadoCamara = nuevoSuavizado;
                ik.invertirEjeVertical = nuevoInvertY;
                ik.evitarColisionesCamara = nuevasColisiones;

                RegistrarCambioDesdeInspector(ik);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Preajustes Rápidos (1 Clic):", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cámara Baja (Linux)"))
            {
                Undo.RecordObject(ik, "Preajuste Cámara Baja");
                ik.alturaPivote = 1.25f;
                ik.offsetVerticalCamara = -0.58f;
                ik.distanciaCamara = 2.6f;
                ik.offsetLateralHombro = 0.45f;
                ik.inclinacionVerticalOffset = 0f;
                RegistrarCambioDesdeInspector(ik);
            }
            if (GUILayout.Button("Cámara Media"))
            {
                Undo.RecordObject(ik, "Preajuste Cámara Media");
                ik.alturaPivote = 1.25f;
                ik.offsetVerticalCamara = -0.15f;
                ik.distanciaCamara = 2.6f;
                ik.offsetLateralHombro = 0.45f;
                ik.inclinacionVerticalOffset = 0f;
                RegistrarCambioDesdeInspector(ik);
            }
            if (GUILayout.Button("Cámara Alta"))
            {
                Undo.RecordObject(ik, "Preajuste Cámara Alta");
                ik.alturaPivote = 1.50f;
                ik.offsetVerticalCamara = 0.25f;
                ik.distanciaCamara = 3.0f;
                ik.offsetLateralHombro = 0.45f;
                ik.inclinacionVerticalOffset = 5f;
                RegistrarCambioDesdeInspector(ik);
            }
            EditorGUILayout.EndHorizontal();

            if (ik.camaraPrincipal != null && !Application.isPlaying)
            {
                if (GUILayout.Button("Capturar Posición Actual de MainCamera (si la moviste con las flechas en Escena)"))
                {
                    Undo.RecordObject(ik, "Capturar Posición de MainCamera");
                    if (ik.pivotCamara != null)
                    {
                        ik.alturaPivote = ik.pivotCamara.localPosition.y;
                    }
                    ik.offsetLateralHombro = ik.camaraPrincipal.localPosition.x;
                    ik.offsetVerticalCamara = ik.camaraPrincipal.localPosition.y;
                    ik.distanciaCamara = Mathf.Max(0.5f, Mathf.Abs(ik.camaraPrincipal.localPosition.z));
                    RegistrarCambioDesdeInspector(ik);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }
    }

    /// <summary>
    /// Inspector personalizado para ControladorCamaraJugador (presente en Player_LinuxSkeleton, Model y MainCamera).
    /// </summary>
    [CustomEditor(typeof(ControladorCamaraJugador))]
    public class ControladorCamaraJugadorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            ControladorCamaraJugador ctrl = (ControladorCamaraJugador)target;
            LinuxCombatIK ik = ctrl.ObtenerSistemaCamara();
            LinuxCameraPlayModePersistence.DibujarPanelBarrasCamara(ik);
        }
    }

    /// <summary>
    /// Inspector personalizado para LinuxCombatIK: muestra el panel de barras de cámara arriba del todo
    /// y conserva intactas todas las propiedades del componente debajo.
    /// </summary>
    [CustomEditor(typeof(LinuxCombatIK))]
    public class LinuxCombatIKEditor : UnityEditor.Editor
    {
        private bool mostrarPropiedadesAvanzadas = false;

        public override void OnInspectorGUI()
        {
            LinuxCombatIK ik = (LinuxCombatIK)target;
            LinuxCameraPlayModePersistence.DibujarPanelBarrasCamara(ik);

            mostrarPropiedadesAvanzadas = EditorGUILayout.Foldout(
                mostrarPropiedadesAvanzadas,
                "Configuración Completa de LinuxCombatIK (Movimiento, IK de Armas y Física)",
                true);

            if (mostrarPropiedadesAvanzadas)
            {
                DrawDefaultInspector();
            }
        }
    }

    /// <summary>
    /// Ventana flotante accesible desde el menú superior: Tools > Ajustar Cámara del Jugador (Barras)
    /// </summary>
    public class LinuxCameraTunerWindow : EditorWindow
    {
        [MenuItem("Tools/Ajustar Cámara del Jugador (Barras)")]
        public static void OpenWindow()
        {
            LinuxCameraTunerWindow win = GetWindow<LinuxCameraTunerWindow>("Cámara Jugador");
            win.minSize = new Vector2(360f, 440f);
            win.Show();

            // También seleccionar automáticamente el objeto en el Inspector para que el usuario lo vea ahí mismo
            SeleccionarJugadorEnInspector();
        }

        [MenuItem("Tools/Seleccionar Cámara del Jugador en Inspector")]
        public static void SeleccionarJugadorEnInspector()
        {
            LinuxCombatIK ik = Object.FindFirstObjectByType<LinuxCombatIK>();
            if (ik != null)
            {
                Selection.activeGameObject = ik.gameObject;
                EditorGUIUtility.PingObject(ik.gameObject);
            }
        }

        private void OnGUI()
        {
            LinuxCombatIK ik = Object.FindFirstObjectByType<LinuxCombatIK>();
            LinuxCameraPlayModePersistence.DibujarPanelBarrasCamara(ik);

            if (ik != null)
            {
                if (GUILayout.Button("Resaltar Objeto en Hierarchy e Inspector"))
                {
                    Selection.activeGameObject = ik.gameObject;
                    EditorGUIUtility.PingObject(ik.gameObject);
                }
            }
        }
    }
}
