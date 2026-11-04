using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace SudoKill9.Audio
{
    /// <summary>
    /// Lanzador portátil, robusto y asíncrono para el microservicio local de síntesis de voz (TTS).
    /// Detecta automáticamente el sistema operativo (Linux, Windows, macOS), comprueba la salud
    /// del microservicio en http://127.0.0.1:8000/health para evitar procesos duplicados, lanza
    /// Backend-TTS/start.sh (o start.bat) en segundo plano con cero congelamiento de fotogramas,
    /// y gestiona el apagado limpio eliminando procesos huérfanos al salir del juego o de Play Mode.
    /// </summary>
    public class TTSProcessLauncher : MonoBehaviour
    {
        [Header("Configuración de Red y Monitoreo")]
        [Tooltip("URL del endpoint de salud del microservicio TTS.")]
        [SerializeField] private string healthUrl = "http://127.0.0.1:8000/health";

        [Tooltip("Tiempo límite en segundos para la prueba inicial de salud.")]
        [SerializeField] private float timeoutHealthCheck = 1.0f;

        [Tooltip("Tiempo máximo en segundos de espera para que el microservicio inicialice.")]
        [SerializeField] private float maxEsperaInicioSegundos = 30.0f;

        [Tooltip("Muestra mensajes informativos en la consola de Unity.")]
        [SerializeField] private bool depurarEnConsola = true;

        public static TTSProcessLauncher Instance { get; private set; }
        public static bool IsServerReady => serverSaludable;
        public static int ServerPid => serverProcessId;
        public static bool IsSpawnedByUnity => servidorIniciadoPorUnity;

        public static event Action<bool> OnServerStatusChanged;

        private static Process serverProcess = null;
        private static int serverProcessId = -1;
        private static bool servidorIniciadoPorUnity = false;
        private static bool serverSaludable = false;
        private static bool verificacionEnCurso = false;

        private Coroutine corrutinaInicio;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Application.quitting -= AlCerrarAplicacion;
                Application.quitting += AlCerrarAplicacion;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (Application.isPlaying && !serverSaludable && !verificacionEnCurso)
            {
                IniciarSecuencia();
            }
        }

        private static void AlCerrarAplicacion()
        {
            DetenerServidorEstatico(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                DetenerServidorEstatico(false);
            }
        }

        private void OnApplicationQuit()
        {
            DetenerServidorEstatico(false);
        }

        /// <summary>
        /// Punto de entrada estático para asegurar que el microservicio esté en ejecución.
        /// Seguro de invocar desde WindowsBossAI o WindowsVoiceManager en Awake() o Start().
        /// </summary>
        public static void EnsureServerRunning()
        {
            if (!Application.isPlaying) return;
            if (serverSaludable || verificacionEnCurso) return;

            if (Instance == null)
            {
                var existing = FindFirstObjectByType<TTSProcessLauncher>();
                if (existing != null)
                {
                    Instance = existing;
                }
                else
                {
                    GameObject launcherGO = new GameObject("TTSProcessLauncher");
                    launcherGO.hideFlags = HideFlags.DontSave;
                    Instance = launcherGO.AddComponent<TTSProcessLauncher>();
                }
            }

            Instance.IniciarSecuencia();
        }

        public void IniciarSecuencia()
        {
            if (verificacionEnCurso) return;
            if (corrutinaInicio != null) StopCoroutine(corrutinaInicio);
            corrutinaInicio = StartCoroutine(SecuenciaVerificarYIniciar());
        }

        private IEnumerator SecuenciaVerificarYIniciar()
        {
            verificacionEnCurso = true;

            // 1. Health check preliminar: verificar si el microservicio ya está funcionando
            using (UnityWebRequest req = UnityWebRequest.Get(healthUrl))
            {
                req.timeout = Mathf.CeilToInt(timeoutHealthCheck);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success && req.responseCode == 200)
                {
                    string respText = req.downloadHandler != null ? req.downloadHandler.text : "";
                    if (respText.Contains("Sudo-Kill-9") || respText.Contains("TTS") || respText.Contains("ok"))
                    {
                        serverSaludable = true;
                        servidorIniciadoPorUnity = false;
                        if (depurarEnConsola)
                        {
                            Debug.Log($"<color=green>[TTSProcessLauncher]</color> Microservicio TTS ya activo y respondiendo en {healthUrl}. No se requiere spawn de nuevo proceso.");
                        }
                        OnServerStatusChanged?.Invoke(true);
                        verificacionEnCurso = false;
                        corrutinaInicio = null;
                        yield break;
                    }
                }
            }

            // 2. Si no responde, resolver ruta portátil del backend
            string backendDir = ObtenerRutaBackend();
            if (string.IsNullOrEmpty(backendDir))
            {
                Debug.LogWarning("[TTSProcessLauncher] No se encontró el directorio 'Backend-TTS'. Las voces funcionarán con fallback silencioso.");
                verificacionEnCurso = false;
                corrutinaInicio = null;
                yield break;
            }

            string scriptPath = ObtenerRutaScriptInicio(backendDir);
            if (string.IsNullOrEmpty(scriptPath))
            {
                Debug.LogWarning($"[TTSProcessLauncher] No se encontró el script de inicio (start.sh o start.bat) en: {backendDir}");
                verificacionEnCurso = false;
                corrutinaInicio = null;
                yield break;
            }

            if (depurarEnConsola)
            {
                Debug.Log($"<color=cyan>[TTSProcessLauncher]</color> Lanzando microservicio TTS de forma asíncrona desde: {scriptPath}");
            }

            // 3. Iniciar el proceso en hilo de fondo para no bloquear el framerate
            bool procesoIniciado = false;
            string errorInicio = null;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    serverProcess = IniciarProcesoEnFondo(scriptPath, backendDir);
                    if (serverProcess != null && !serverProcess.HasExited)
                    {
                        serverProcessId = serverProcess.Id;
                        servidorIniciadoPorUnity = true;
                        procesoIniciado = true;
#if UNITY_EDITOR
                        UnityEditor.SessionState.SetInt("SudoKill9_TTS_PID", serverProcessId);
                        UnityEditor.SessionState.SetBool("SudoKill9_TTS_Spawned", true);
#endif
                    }
                }
                catch (Exception ex)
                {
                    errorInicio = ex.Message;
                }
            });

            // Esperar brevemente confirmación del inicio del subproceso (máx 3.0s sin congelar fotogramas)
            float tEsperandoProceso = 0f;
            while (!procesoIniciado && string.IsNullOrEmpty(errorInicio) && tEsperandoProceso < 3.0f)
            {
                tEsperandoProceso += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!string.IsNullOrEmpty(errorInicio))
            {
                Debug.LogError($"[TTSProcessLauncher] Excepción al lanzar proceso TTS: {errorInicio}");
                verificacionEnCurso = false;
                corrutinaInicio = null;
                yield break;
            }

            if (serverProcess == null)
            {
                Debug.LogWarning("[TTSProcessLauncher] El subproceso TTS no pudo ser instanciado.");
                verificacionEnCurso = false;
                corrutinaInicio = null;
                yield break;
            }

            // 4. Polling no bloqueante con timeout monitoreando /health
            float tInicio = Time.realtimeSinceStartup;
            while ((Time.realtimeSinceStartup - tInicio) < maxEsperaInicioSegundos)
            {
                yield return new WaitForSecondsRealtime(0.5f);

                // Comprobar si el proceso murió inesperadamente
                if (serverProcess != null && serverProcess.HasExited)
                {
                    Debug.LogWarning($"[TTSProcessLauncher] El proceso TTS terminó de forma imprevista con código {serverProcess.ExitCode}.");
                    serverProcessId = -1;
                    servidorIniciadoPorUnity = false;
#if UNITY_EDITOR
                    UnityEditor.SessionState.EraseInt("SudoKill9_TTS_PID");
                    UnityEditor.SessionState.EraseBool("SudoKill9_TTS_Spawned");
#endif
                    break;
                }

                using (UnityWebRequest pingReq = UnityWebRequest.Get(healthUrl))
                {
                    pingReq.timeout = 1;
                    yield return pingReq.SendWebRequest();

                    if (pingReq.result == UnityWebRequest.Result.Success && pingReq.responseCode == 200)
                    {
                        string body = pingReq.downloadHandler != null ? pingReq.downloadHandler.text : "";
                        if (body.Contains("Sudo-Kill-9") || body.Contains("TTS") || body.Contains("ok"))
                        {
                            serverSaludable = true;
                            if (depurarEnConsola)
                            {
                                Debug.Log($"<color=green>[TTSProcessLauncher]</color> ¡Microservicio TTS en línea y verificado exitosamente! (PID: {serverProcessId})");
                            }
                            OnServerStatusChanged?.Invoke(true);
                            verificacionEnCurso = false;
                            corrutinaInicio = null;
                            yield break;
                        }
                    }
                }
            }

            if (!serverSaludable)
            {
                Debug.LogWarning($"[TTSProcessLauncher] El microservicio TTS no respondió en {maxEsperaInicioSegundos}s. Si era la primera ejecución, pip puede haber tardado instalando paquetes.");
            }

            verificacionEnCurso = false;
            corrutinaInicio = null;
        }

        /// <summary>
        /// Comprueba la salud del microservicio TTS de forma síncrona.
        /// Útil para herramientas del Editor, tests unitarios y menús de diagnóstico.
        /// </summary>
        public static bool ProbarSaludServidorSincrono(string url = "http://127.0.0.1:8000/health", int timeoutMs = 1500)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
                    var response = client.GetAsync(url).GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode)
                    {
                        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        if (!string.IsNullOrEmpty(body) && (body.Contains("Sudo-Kill-9") || body.Contains("TTS") || body.Contains("ok")))
                        {
                            serverSaludable = true;
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Fallback silencioso si no hay respuesta o conexión rechazada
            }
            return false;
        }

        /// <summary>
        /// Resuelve de manera portátil la ruta absoluta hacia Backend-TTS sin asumir rutas fijas de usuario.
        /// Compatible con Unity Editor, Standalone Linux/Windows Players y macOS .app bundles.
        /// </summary>
        public static string ObtenerRutaBackend()
        {
            string[] candidatas = new string[]
            {
                Path.GetFullPath(Path.Combine(Application.dataPath, "../Backend-TTS")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "../../Backend-TTS")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Backend-TTS")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "Backend-TTS")),
                Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, "Backend-TTS")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Backend-TTS")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backend-TTS")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../Backend-TTS"))
            };

            foreach (string candidata in candidatas)
            {
                if (Directory.Exists(candidata))
                {
                    return candidata;
                }
            }

            return null;
        }

        /// <summary>
        /// Selecciona start.bat para Windows o start.sh para Linux/macOS.
        /// </summary>
        public static string ObtenerRutaScriptInicio(string backendDir)
        {
            if (string.IsNullOrEmpty(backendDir) || !Directory.Exists(backendDir)) return null;

            bool esWindows = Application.platform == RuntimePlatform.WindowsEditor || 
                             Application.platform == RuntimePlatform.WindowsPlayer ||
                             System.Environment.OSVersion.Platform == PlatformID.Win32NT;

            if (esWindows)
            {
                string bat = Path.Combine(backendDir, "start.bat");
                if (File.Exists(bat)) return bat;
            }
            else
            {
                string sh = Path.Combine(backendDir, "start.sh");
                if (File.Exists(sh)) return sh;
            }

            return null;
        }

        private static void AsegurarPermisosEjecucion(string scriptPath, string backendDir)
        {
            bool esUnix = Application.platform == RuntimePlatform.LinuxEditor ||
                          Application.platform == RuntimePlatform.LinuxPlayer ||
                          Application.platform == RuntimePlatform.OSXEditor ||
                          Application.platform == RuntimePlatform.OSXPlayer ||
                          System.Environment.OSVersion.Platform == PlatformID.Unix;

            if (esUnix)
            {
                try
                {
                    if (File.Exists(scriptPath))
                    {
                        using (Process p = Process.Start(new ProcessStartInfo("chmod", $"+x \"{scriptPath}\"")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }))
                        {
                            p?.WaitForExit(1000);
                        }
                    }

                    string startSh = Path.Combine(backendDir, "start.sh");
                    if (File.Exists(startSh) && startSh != scriptPath)
                    {
                        using (Process p = Process.Start(new ProcessStartInfo("chmod", $"+x \"{startSh}\"")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }))
                        {
                            p?.WaitForExit(1000);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TTSProcessLauncher] Advertencia al aplicar chmod +x: {ex.Message}");
                }
            }
        }

        private static Process IniciarProcesoEnFondo(string scriptPath, string backendDir)
        {
            AsegurarPermisosEjecucion(scriptPath, backendDir);

            ProcessStartInfo psi = new ProcessStartInfo();
            bool esWindows = Application.platform == RuntimePlatform.WindowsEditor || 
                             Application.platform == RuntimePlatform.WindowsPlayer ||
                             System.Environment.OSVersion.Platform == PlatformID.Win32NT;

            if (esWindows)
            {
                psi.FileName = "cmd.exe";
                psi.Arguments = $"/c \"\"{scriptPath}\" --no-pause\"";
            }
            else
            {
                psi.FileName = File.Exists("/bin/bash") ? "/bin/bash" : "bash";
                psi.Arguments = $"\"{scriptPath}\"";
            }

            psi.WorkingDirectory = backendDir;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            Process proc = new Process();
            proc.StartInfo = psi;

            proc.OutputDataReceived += (sender, args) =>
            {
                // Consumir salida para evitar saturar el búfer del pipe
            };

            proc.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data) && args.Data.Contains("ERROR"))
                {
                    Debug.LogWarning($"[TTS Backend] {args.Data}");
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            return proc;
        }

        /// <summary>
        /// Cierra limpiamente el proceso y sus procesos hijos para no dejar servidores uvicorn huérfanos.
        /// Solo actúa si el proceso fue iniciado por Unity, salvo que se invoque con forzar=true.
        /// </summary>
        public static void DetenerServidorEstatico(bool forzar = false)
        {
#if UNITY_EDITOR
            if (serverProcessId <= 0)
            {
                int sessionPid = UnityEditor.SessionState.GetInt("SudoKill9_TTS_PID", -1);
                if (sessionPid > 0)
                {
                    serverProcessId = sessionPid;
                    servidorIniciadoPorUnity = UnityEditor.SessionState.GetBool("SudoKill9_TTS_Spawned", false);
                }
            }
            UnityEditor.SessionState.EraseInt("SudoKill9_TTS_PID");
            UnityEditor.SessionState.EraseBool("SudoKill9_TTS_Spawned");
#endif

            if (!forzar && (!servidorIniciadoPorUnity || serverProcessId <= 0))
            {
                return;
            }

            int pid = serverProcessId;
            serverProcessId = -1;
            servidorIniciadoPorUnity = false;
            serverSaludable = false;

            if (pid <= 0) return;

            try
            {
                bool esWindows = Application.platform == RuntimePlatform.WindowsEditor || 
                                 Application.platform == RuntimePlatform.WindowsPlayer ||
                                 System.Environment.OSVersion.Platform == PlatformID.Win32NT;

                if (esWindows)
                {
                    using (Process killer = Process.Start(new ProcessStartInfo("taskkill", $"/F /T /PID {pid}")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }))
                    {
                        killer?.WaitForExit(2000);
                    }
                }
                else
                {
                    // 1. Enviar SIGTERM primero al proceso principal para apagado ordenado de uvicorn
                    try
                    {
                        using (Process kill = Process.Start(new ProcessStartInfo("kill", $"-TERM {pid}")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }))
                        {
                            kill?.WaitForExit(500);
                        }
                    }
                    catch { }

                    // Enviar SIGTERM a procesos descendientes directos
                    try
                    {
                        using (Process pkill = Process.Start(new ProcessStartInfo("pkill", $"-TERM -P {pid}")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }))
                        {
                            pkill?.WaitForExit(500);
                        }
                    }
                    catch { }

                    // 2. Dar margen breve para que uvicorn cierre sockets limpiamente
                    if (serverProcess != null && !serverProcess.HasExited)
                    {
                        serverProcess.WaitForExit(1000);
                    }

                    // 3. Forzar terminación inmediata (SIGKILL) si algún proceso persiste
                    try
                    {
                        using (Process pkill9 = Process.Start(new ProcessStartInfo("pkill", $"-9 -P {pid}")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }))
                        {
                            pkill9?.WaitForExit(300);
                        }
                    }
                    catch { }

                    try
                    {
                        using (Process kill9 = Process.Start(new ProcessStartInfo("kill", $"-9 {pid}")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }))
                        {
                            kill9?.WaitForExit(300);
                        }
                    }
                    catch { }
                }

                if (serverProcess != null && !serverProcess.HasExited)
                {
                    serverProcess.Kill();
                    serverProcess.Dispose();
                }

                Debug.Log($"<color=orange>[TTSProcessLauncher]</color> Microservicio TTS (PID {pid}) terminado limpiamente.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TTSProcessLauncher] Excepción al terminar proceso TTS: {ex.Message}");
            }
            finally
            {
                serverProcess = null;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inicia el microservicio directamente desde herramientas del Editor (Play Mode o Edit Mode).
        /// </summary>
        public static void IniciarServidorDesdeEditor()
        {
            if (ProbarSaludServidorSincrono())
            {
                Debug.Log("<color=green>[TTS Setup]</color> El microservicio TTS ya está en ejecución y saludable en http://127.0.0.1:8000/health.");
                return;
            }

            string backendDir = ObtenerRutaBackend();
            if (string.IsNullOrEmpty(backendDir))
            {
                Debug.LogError("[TTS Setup] No se encontró el directorio 'Backend-TTS'.");
                return;
            }

            string scriptPath = ObtenerRutaScriptInicio(backendDir);
            if (string.IsNullOrEmpty(scriptPath))
            {
                Debug.LogError($"[TTS Setup] No se encontró script de inicio en: {backendDir}");
                return;
            }

            try
            {
                serverProcess = IniciarProcesoEnFondo(scriptPath, backendDir);
                if (serverProcess != null && !serverProcess.HasExited)
                {
                    serverProcessId = serverProcess.Id;
                    servidorIniciadoPorUnity = true;
                    UnityEditor.SessionState.SetInt("SudoKill9_TTS_PID", serverProcessId);
                    UnityEditor.SessionState.SetBool("SudoKill9_TTS_Spawned", true);
                    Debug.Log($"<color=green>[TTS Setup]</color> Microservicio TTS lanzado exitosamente desde el Editor (PID: {serverProcessId}).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TTS Setup] Error al iniciar microservicio TTS: {ex.Message}");
            }
        }

        /// <summary>
        /// Detiene el microservicio directamente desde el Editor.
        /// </summary>
        public static void DetenerServidorDesdeEditor()
        {
            int pid = serverProcessId;
            if (pid <= 0)
            {
                pid = UnityEditor.SessionState.GetInt("SudoKill9_TTS_PID", -1);
            }

            if (pid > 0)
            {
                DetenerServidorEstatico(forzar: true);
                Debug.Log($"<color=orange>[TTS Setup]</color> Microservicio TTS (PID {pid}) detenido desde el Editor.");
                return;
            }

            // Si no hay PID registrado pero el microservicio está activo en el puerto 8000
            if (ProbarSaludServidorSincrono())
            {
                bool esWindows = Application.platform == RuntimePlatform.WindowsEditor || 
                                 System.Environment.OSVersion.Platform == PlatformID.Win32NT;
                if (!esWindows)
                {
                    try
                    {
                        using (Process p = Process.Start(new ProcessStartInfo("/bin/bash", "-c \"pkill -f 'uvicorn app.main:app'\"")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }))
                        {
                            p?.WaitForExit(1000);
                        }
                        serverSaludable = false;
                        Debug.Log("<color=orange>[TTS Setup]</color> Señal de terminación enviada a la instancia de uvicorn en puerto 8000.");
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[TTS Setup] No se pudo enviar señal a uvicorn: {ex.Message}");
                    }
                }
            }

            Debug.Log("<color=yellow>[TTS Setup]</color> No hay ningún proceso TTS registrado para detener.");
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void RegistrarCallbackEditor()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                DetenerServidorEstatico(false);
            }
        }
#endif
    }
}
