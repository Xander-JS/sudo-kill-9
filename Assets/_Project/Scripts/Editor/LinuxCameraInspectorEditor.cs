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
            EditorApplication.update += MonitorizarCursorYEntradaEditor;
        }

        private static void MonitorizarCursorYEntradaEditor()
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.InputSystem.settings != null &&
                UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode !=
                UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus)
            {
                UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode =
                    UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;
            }
#endif
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

            // Limpiar ControladorCamaraJugador de CameraPivot y MainCamera si existían
            // para no interferir con la navegación o selección de la cámara en Scene View
            if (ik.pivotCamara != null)
            {
                var ctrlPivot = ik.pivotCamara.GetComponent<ControladorCamaraJugador>();
                if (ctrlPivot != null)
                {
                    Object.DestroyImmediate(ctrlPivot);
                    dirty = true;
                }
            }

            if (ik.camaraPrincipal != null)
            {
                var ctrlCam = ik.camaraPrincipal.GetComponent<ControladorCamaraJugador>();
                if (ctrlCam != null)
                {
                    Object.DestroyImmediate(ctrlCam);
                    dirty = true;
                }
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

        [MenuItem("Tools/Reparar Navegación y Zoom de Scene View")]
        public static void RepararNavegacionSceneView()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode =
                UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;
#endif

            Tools.current = Tool.View;

            if (SceneView.sceneViews != null)
            {
                foreach (SceneView sv in SceneView.sceneViews)
                {
                    if (sv == null) continue;
                    sv.orthographic = false;
                    sv.in2DMode = false;
                    sv.isRotationLocked = false;
                    sv.Repaint();
                }
            }

            SceneView view = SceneView.lastActiveSceneView;
            if (view == null && SceneView.sceneViews != null && SceneView.sceneViews.Count > 0)
            {
                view = SceneView.sceneViews[0] as SceneView;
            }

            if (view != null)
            {
                LinuxCombatIK ik = Object.FindFirstObjectByType<LinuxCombatIK>();
                if (ik != null)
                {
                    view.LookAt(ik.transform.position + Vector3.up * 1.2f, Quaternion.Euler(20f, 0f, 0f), 4.5f);
                }
                view.Repaint();
            }

            EnsureCameraControllersInScene();

            EditorUtility.DisplayDialog(
                "Navegación de Scene View Restaurada",
                "Se ha desbloqueado el cursor del ratón, restaurado la recepción de eventos de zoom/paneo " +
                "y reenfocado la vista de Escena sobre el personaje Linux.\n\n" +
                "Controles en Scene View:\n" +
                "• Rueda del ratón: Zoom\n" +
                "• Clic derecho + WASD: Vuelo / Navegación libre\n" +
                "• Botón central del ratón: Desplazar pantalla (Pan)\n" +
                "• Alt + Clic derecho: Zoom suave",
                "OK");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode =
                    UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;
#endif
            }

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

        /// <summary>
        /// Dibuja el panel de calibración manual de salida de balas (MuzzlePoint).
        /// </summary>
        public static void DibujarPanelCalibracionMuzzle(LinuxCombatIK ik)
        {
            if (ik == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            EditorGUILayout.LabelField("🎯 CALIBRACIÓN MANUAL: SALIDA DE BALAS (MUZZLE)", headerStyle);
            EditorGUILayout.HelpBox(
                "Ajusta manualmente la posición por donde sale la bala respecto al fusil SCAR-H.\n" +
                "• Mueve los sliders de abajo o arrastra las FLECHAS 3D directamente sobre el cañón en la Escena.\n" +
                "• En la vista de Escena verás una esfera roja con una flecha amarilla indicando el cañón.",
                MessageType.Info);

            ik.GarantizarMuzzlePoint();

            EditorGUI.BeginChangeCheck();

            ik.ajusteManualMuzzle = EditorGUILayout.Toggle(
                new GUIContent("Activar Ajuste Manual", "Conserva tus valores sin que el código los sobreescriba."),
                ik.ajusteManualMuzzle);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Posición del Cañón respecto a la SCAR-H (en metros):", EditorStyles.boldLabel);

            float nuevoX = EditorGUILayout.Slider(
                new GUIContent("Frente / Atrás (X)", "Avanza o retrocede la boca del cañón a lo largo del fusil."),
                ik.offsetMuzzleManual.x, -0.2f, 1.2f);

            float nuevoY = EditorGUILayout.Slider(
                new GUIContent("Arriba / Abajo (Y)", "Sube o baja la boca del cañón respecto al fusil."),
                ik.offsetMuzzleManual.y, -0.3f, 0.3f);

            float nuevoZ = EditorGUILayout.Slider(
                new GUIContent("Izquierda / Derecha (Z)", "Desplaza lateralmente la boca del cañón."),
                ik.offsetMuzzleManual.z, -0.3f, 0.3f);

            Vector3 nuevaRot = EditorGUILayout.Vector3Field("Rotación del Cañón (°)", ik.rotacionMuzzleManual);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ik, "Calibrar Muzzle Manualmente");
                ik.offsetMuzzleManual = new Vector3(nuevoX, nuevoY, nuevoZ);
                ik.rotacionMuzzleManual = nuevaRot;

                if (ik.muzzlePoint != null)
                {
                    Undo.RecordObject(ik.muzzlePoint, "Mover MuzzlePoint");
                    ik.muzzlePoint.localPosition = ik.offsetMuzzleManual;
                    ik.muzzlePoint.localRotation = Quaternion.Euler(ik.rotacionMuzzleManual);
                    EditorUtility.SetDirty(ik.muzzlePoint);
                }

                EditorUtility.SetDirty(ik);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Seleccionar 'MuzzlePoint' (Mover en Escena)"))
            {
                if (ik.muzzlePoint != null)
                {
                    Selection.activeGameObject = ik.muzzlePoint.gameObject;
                    EditorGUIUtility.PingObject(ik.muzzlePoint.gameObject);
                    Tools.current = Tool.Move;
                }
            }
            if (GUILayout.Button("Restablecer Posición Predeterminada"))
            {
                Undo.RecordObject(ik, "Restablecer Muzzle");
                ik.offsetMuzzleManual = new Vector3(0.42f, 0.008f, 0f);
                ik.rotacionMuzzleManual = new Vector3(0f, 90f, 0f);
                if (ik.muzzlePoint != null)
                {
                    Undo.RecordObject(ik.muzzlePoint, "Restablecer MuzzlePoint");
                    ik.muzzlePoint.localPosition = ik.offsetMuzzleManual;
                    ik.muzzlePoint.localRotation = Quaternion.Euler(ik.rotacionMuzzleManual);
                    EditorUtility.SetDirty(ik.muzzlePoint);
                }
                EditorUtility.SetDirty(ik);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        /// <summary>
        /// Dibuja el panel de configuración de la Mira / Crosshair (Posición en pantalla, tamaño, colores y láser).
        /// </summary>
        public static void DibujarPanelConfiguracionMira(LinuxCombatIK ik)
        {
            if (ik == null) return;

            if (ik.crosshairUI == null)
            {
                ik.LocalizarCrosshairUI();
            }

            CrosshairUI ui = ik.crosshairUI;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            EditorGUILayout.LabelField("🎯 CONFIGURACIÓN DE LA MIRA (CROSSHAIR / APUNTADOR)", headerStyle);

            if (ui == null)
            {
                EditorGUILayout.HelpBox(
                    "No se encontró el componente CrosshairUI en la cámara principal ni en el jugador.\n" +
                    "Haz clic en el botón de abajo para agregarlo automáticamente a la MainCamera.",
                    MessageType.Warning);

                if (GUILayout.Button("➕ Agregar CrosshairUI a MainCamera", GUILayout.Height(24)))
                {
                    Camera cam = ik.camaraPrincipal != null ? ik.camaraPrincipal.GetComponent<Camera>() : Camera.main;
                    if (cam != null)
                    {
                        ui = cam.gameObject.AddComponent<CrosshairUI>();
                        ik.crosshairUI = ui;
                        EditorUtility.SetDirty(cam.gameObject);
                        EditorUtility.SetDirty(ik);
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
                return;
            }

            EditorGUILayout.HelpBox(
                "Ajusta la posición de la cruz en pantalla, sus dimensiones, colores y guía láser.\n" +
                "✔ La bala viaja exactamente al punto donde esté situada la cruz en la pantalla.\n" +
                "✔ El torso y el fusil se orientan automáticamente hacia el objetivo enfocado por la cruz.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("1. Posición de la Cruz en Pantalla (Offset):", EditorStyles.boldLabel);

            float nuevoOffX = EditorGUILayout.Slider(
                new GUIContent("Desplazamiento Horizontal (X)", "0 = Centro exacto, - = izquierda, + = derecha."),
                ui.offsetPantallaX, -0.45f, 0.45f);

            float nuevoOffY = EditorGUILayout.Slider(
                new GUIContent("Desplazamiento Vertical (Y)", "0 = Centro exacto, - = abajo, + = arriba."),
                ui.offsetPantallaY, -0.45f, 0.45f);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("2. Dimensiones y Estilo de la Cruz:", EditorStyles.boldLabel);

            int nuevoArm = EditorGUILayout.IntSlider(
                new GUIContent("Longitud de Brazos", "Tamaño de cada una de las 4 líneas de la cruz en píxeles."),
                ui.armLength, 4, 40);

            int nuevoThick = EditorGUILayout.IntSlider(
                new GUIContent("Grosor de Líneas", "Grosor en píxeles de los brazos de la cruz."),
                ui.thickness, 1, 10);

            int nuevoGap = EditorGUILayout.IntSlider(
                new GUIContent("Separación Central (Gap)", "Distancia entre el centro y el inicio de cada brazo."),
                ui.gap, 0, 30);

            bool nuevoDot = EditorGUILayout.Toggle(
                new GUIContent("Mostrar Punto Central", "Muestra un punto preciso en el centro exacto de la mira."),
                ui.showDot);

            int nuevoDotSize = ui.tamanoPunto;
            if (nuevoDot)
            {
                nuevoDotSize = EditorGUILayout.IntSlider(
                    new GUIContent("Tamaño del Punto Central", "Diámetro en píxeles del punto central."),
                    ui.tamanoPunto, 1, 8);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("3. Colores y Detección de Objetivos:", EditorStyles.boldLabel);

            Color nuevoColor = EditorGUILayout.ColorField(
                new GUIContent("Color Normal de la Mira", "Color habitual mientras apuntas."),
                ui.color);

            Color nuevoColorEnemigo = EditorGUILayout.ColorField(
                new GUIContent("Color al Enfocar Enemigo", "Color al que cambia la mira cuando apunta a un objetivo o enemigo."),
                ui.colorAlEnfocarEnemigo);

            bool nuevoRetroceso = EditorGUILayout.Toggle(
                new GUIContent("Expansión Dinámica (Retroceso)", "La mira se abre sutilmente al disparar simulando el retroceso del arma."),
                ui.retrocesoDinamico);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("4. Comportamiento y Guía Láser 3D:", EditorStyles.boldLabel);

            bool nuevoSoloApuntar = EditorGUILayout.Toggle(
                new GUIContent("Solo al Apuntar (Clic Derecho)", "Si está marcado, la mira solo aparece mientras mantienes presionado clic derecho."),
                ui.SoloAlApuntar);

            bool nuevoLaser = EditorGUILayout.Toggle(
                new GUIContent("Activar Guía Láser 3D", "Proyecta un haz láser visible desde la boca del fusil hacia el punto de mira."),
                ui.activarGuiaLaser);

            Texture2D nuevaTextura = (Texture2D)EditorGUILayout.ObjectField(
                new GUIContent("Textura / Sprite Personalizado (Opcional)", "Si deseas usar una imagen PNG de mira en lugar de la cruz procedural."),
                ui.texturaPersonalizada, typeof(Texture2D), false);

            int nuevoTamanoTex = ui.tamanoTextura;
            if (nuevaTextura != null)
            {
                nuevoTamanoTex = EditorGUILayout.IntSlider(
                    new GUIContent("Tamaño de Textura", "Tamaño en píxeles con el que se dibuja el sprite."),
                    ui.tamanoTextura, 12, 128);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ui, "Modificar Ajustes de Mira (CrosshairUI)");
                ui.offsetPantallaX = nuevoOffX;
                ui.offsetPantallaY = nuevoOffY;
                ui.armLength = nuevoArm;
                ui.thickness = nuevoThick;
                ui.gap = nuevoGap;
                ui.showDot = nuevoDot;
                ui.tamanoPunto = nuevoDotSize;
                ui.color = nuevoColor;
                ui.colorAlEnfocarEnemigo = nuevoColorEnemigo;
                ui.retrocesoDinamico = nuevoRetroceso;
                ui.SoloAlApuntar = nuevoSoloApuntar;
                ui.activarGuiaLaser = nuevoLaser;
                ui.texturaPersonalizada = nuevaTextura;
                ui.tamanoTextura = nuevoTamanoTex;

                EditorUtility.SetDirty(ui);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Preajustes Rápidos de Mira:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎯 Centro (0, 0)"))
            {
                Undo.RecordObject(ui, "Centrar Mira");
                ui.offsetPantallaX = 0f;
                ui.offsetPantallaY = 0f;
                EditorUtility.SetDirty(ui);
            }
            if (GUILayout.Button("🔴 Guía Láser"))
            {
                Undo.RecordObject(ui, "Toggle Guia Laser");
                ui.activarGuiaLaser = !ui.activarGuiaLaser;
                EditorUtility.SetDirty(ui);
            }
            if (GUILayout.Button("🔄 Restablecer"))
            {
                Undo.RecordObject(ui, "Restablecer Mira");
                ui.offsetPantallaX = 0f;
                ui.offsetPantallaY = 0f;
                ui.armLength = 12;
                ui.thickness = 2;
                ui.gap = 4;
                ui.showDot = true;
                ui.tamanoPunto = 3;
                ui.color = Color.white;
                ui.colorAlEnfocarEnemigo = new Color(1f, 0.25f, 0.25f, 1f);
                ui.retrocesoDinamico = true;
                ui.SoloAlApuntar = true;
                EditorUtility.SetDirty(ui);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Seleccionar 'CrosshairUI' en Hierarchy"))
            {
                Selection.activeGameObject = ui.gameObject;
                EditorGUIUtility.PingObject(ui.gameObject);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
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

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (GUILayout.Button("🛠️ REPARAR NAVEGACIÓN Y ZOOM DE SCENE VIEW (Clic si se trabó)", GUILayout.Height(26)))
            {
                LinuxCameraPlayModePersistence.RepararNavegacionSceneView();
            }
            EditorGUILayout.EndVertical();

            LinuxCameraPlayModePersistence.DibujarPanelBarrasCamara(ik);
            LinuxCameraPlayModePersistence.DibujarPanelCalibracionMuzzle(ik);
            LinuxCameraPlayModePersistence.DibujarPanelConfiguracionMira(ik);
        }
    }

    /// <summary>
    /// Inspector personalizado para LinuxCombatIK: muestra el panel de barras de cámara arriba del todo,
    /// el panel de calibración de Muzzle, la configuración de la mira y el visor 3D interactivo en Escena.
    /// </summary>
    [CustomEditor(typeof(LinuxCombatIK))]
    public class LinuxCombatIKEditor : UnityEditor.Editor
    {
        private bool mostrarPropiedadesAvanzadas = true;

        public override void OnInspectorGUI()
        {
            LinuxCombatIK ik = (LinuxCombatIK)target;

            // 1. Botón superior de rescate para la navegación de Scene View
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (GUILayout.Button("🛠️ REPARAR NAVEGACIÓN Y ZOOM DE SCENE VIEW (Clic si se trabó)", GUILayout.Height(26)))
            {
                LinuxCameraPlayModePersistence.RepararNavegacionSceneView();
            }
            EditorGUILayout.EndVertical();

            // 2. Panel de barras de cámara
            LinuxCameraPlayModePersistence.DibujarPanelBarrasCamara(ik);

            // 3. Panel de calibración del MuzzlePoint (Salida de balas)
            LinuxCameraPlayModePersistence.DibujarPanelCalibracionMuzzle(ik);

            // 4. Panel de configuración de la Mira / Crosshair
            LinuxCameraPlayModePersistence.DibujarPanelConfiguracionMira(ik);

            // 5. Propiedades avanzadas desplegadas por defecto
            mostrarPropiedadesAvanzadas = EditorGUILayout.Foldout(
                mostrarPropiedadesAvanzadas,
                "Configuración Completa de LinuxCombatIK (Movimiento, IK de Armas y Física)",
                true);

            if (mostrarPropiedadesAvanzadas)
            {
                DrawDefaultInspector();
            }
        }

        private void OnSceneGUI()
        {
            LinuxCombatIK ik = (LinuxCombatIK)target;
            if (ik == null || ik.Weapon == null) return;

            ik.GarantizarMuzzlePoint();
            if (ik.muzzlePoint == null) return;

            // Dibujar Handle 3D de movimiento directamente sobre la boca del cañón
            EditorGUI.BeginChangeCheck();
            Vector3 nuevaPosMundo = Handles.PositionHandle(ik.muzzlePoint.position, ik.muzzlePoint.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ik, "Mover MuzzlePoint con Handle de Escena");
                Undo.RecordObject(ik.muzzlePoint, "Mover MuzzlePoint");

                ik.muzzlePoint.position = nuevaPosMundo;
                ik.offsetMuzzleManual = ik.muzzlePoint.localPosition;
                ik.ajusteManualMuzzle = true;

                EditorUtility.SetDirty(ik);
                EditorUtility.SetDirty(ik.muzzlePoint);
            }

            // Etiqueta flotante indicadora en la Escena
            Handles.Label(
                ik.muzzlePoint.position + Vector3.up * 0.08f,
                $"🎯 Salida de Bala (Muzzle)\nX: {ik.offsetMuzzleManual.x:F3}, Y: {ik.offsetMuzzleManual.y:F3}, Z: {ik.offsetMuzzleManual.z:F3}");
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

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (GUILayout.Button("🛠️ REPARAR NAVEGACIÓN Y ZOOM DE SCENE VIEW", GUILayout.Height(24)))
            {
                LinuxCameraPlayModePersistence.RepararNavegacionSceneView();
            }
            EditorGUILayout.EndVertical();

            LinuxCameraPlayModePersistence.DibujarPanelBarrasCamara(ik);
            LinuxCameraPlayModePersistence.DibujarPanelCalibracionMuzzle(ik);
            LinuxCameraPlayModePersistence.DibujarPanelConfiguracionMira(ik);

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
