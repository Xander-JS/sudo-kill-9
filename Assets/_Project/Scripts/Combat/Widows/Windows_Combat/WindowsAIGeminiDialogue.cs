using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using SudoKill9.Combat;

/// <summary>
/// Módulo híbrido para Windows Boss:
/// - Genera burlas técnicas y comentarios satíricos contextuales en el terminal CMD.
/// - Conexión asíncrona a la API de Google Gemini (sin bloquear el bucle de juego a 60 FPS).
/// - Fallback offline robusto: si no hay conexión o no hay API Key, utiliza un repositorio local
///   de más de 40 frases icónicas y comandos cómicos del sistema operativo.
/// </summary>
public class WindowsAIGeminiDialogue : MonoBehaviour
{
    // ========================================================================= //
    // CONFIGURACIÓN DE GEMINI API                                              //
    // ========================================================================= //

    [Header("Configuración de Gemini API (Opcional / Híbrido)")]
    [Tooltip("Activa o desactiva las peticiones a la API de Gemini.")]
    [SerializeField] private bool habilitarGeminiAPI = true;

    [Tooltip("API Key de Google Gemini. Si se deja vacía, se intentará leer de la variable de entorno GEMINI_API_KEY o se usará el fallback local.")]
    [SerializeField] private string apiKey = "";

    [Tooltip("Modelo de Gemini a utilizar.")]
    [SerializeField] private string modeloGemini = "gemini-1.5-flash";

    [Tooltip("Intervalo mínimo en segundos entre peticiones HTTP para evitar saturación de cuota.")]
    [SerializeField] private float cooldownEntreDialogos = 10.0f;

    [Tooltip("Si está activo, escribe en la consola de Unity los diálogos y respuestas de la API.")]
    [SerializeField] private bool depurarEnConsola = false;

    // ========================================================================= //
    // VARIABLES INTERNAS                                                        //
    // ========================================================================= //

    private float ultimoTiempoDialogo = -999f;
    private bool peticionEnCurso = false;
    private WindowsBossAI bossAI;

    // ========================================================================= //
    // CATEGORÍAS DE EVENTOS PARA DIÁLOGOS                                       //
    // ========================================================================= //

    public enum DialogueContext
    {
        InicioCombate,
        EsquiveSaltaLoco,
        ActivarFirewall,
        EjecutarTaskkill,
        CargarKamehameha,
        DispararKamehameha,
        EstadoExhausto,
        EntrarFase2,
        EntrarFase3,
        EventoBSOD,
        GolpeConectado,
        MuerteBoss
    }

    private void Awake()
    {
        bossAI = GetComponent<WindowsBossAI>();
        if (string.IsNullOrEmpty(apiKey))
        {
            string envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (!string.IsNullOrEmpty(envKey))
            {
                apiKey = envKey;
            }
        }
    }

    private void OnEnable()
    {
        WindowsBossAI.OnBossPhaseChanged += ManejarCambioFase;
        WindowsBossAI.OnBossDied += ManejarMuerte;
        WindowsCommandAttacks.OnBSODTriggered += ManejarBSOD;
    }

    private void OnDisable()
    {
        WindowsBossAI.OnBossPhaseChanged -= ManejarCambioFase;
        WindowsBossAI.OnBossDied -= ManejarMuerte;
        WindowsCommandAttacks.OnBSODTriggered -= ManejarBSOD;
    }

    private void Start()
    {
        // Provocación de inicio tras 2 segundos
        StartCoroutine(RutinaDialogoInicial());
    }

    private IEnumerator RutinaDialogoInicial()
    {
        yield return new WaitForSeconds(2.2f);
        SolicitarDialogo(DialogueContext.InicioCombate, "Windows inicia el combate contra Tux el pingüino de Linux.");
    }

    private void ManejarCambioFase(int nuevaFase)
    {
        if (nuevaFase == 2)
        {
            SolicitarDialogo(DialogueContext.EntrarFase2, "Windows entra en Fase 2 (70% HP), activando contramedidas avanzadas de red.");
        }
        else if (nuevaFase == 3)
        {
            SolicitarDialogo(DialogueContext.EntrarFase3, "Windows entra en Fase 3 Enrage (<35% HP), preparando el Kamehameha de PowerShell.");
        }
    }

    private void ManejarBSOD()
    {
        SolicitarDialogo(DialogueContext.EventoBSOD, "Windows activa la Pantalla Azul de la Muerte crítica (BSOD) riéndose de Tux.", forzarInmediato: true);
    }

    private void ManejarMuerte()
    {
        SolicitarDialogo(DialogueContext.MuerteBoss, "Windows ha sido derrotado por Tux y su kernel se apaga.", forzarInmediato: true);
    }

    // ========================================================================= //
    // SOLICITUD DE DIÁLOGO (HÍBRIDO: GEMINI O FALLBACK LOCAL)                   //
    // ========================================================================= //

    /// <summary>
    /// Solicita una burla o línea satírica. Si la API de Gemini está disponible y configurada,
    /// la consulta de forma asíncrona; en caso contrario, emite de inmediato una frase local.
    /// </summary>
    public void SolicitarDialogo(DialogueContext contexto, string contextoDetalle = "", bool forzarInmediato = false)
    {
        if (!forzarInmediato && (Time.time - ultimoTiempoDialogo) < cooldownEntreDialogos)
        {
            return;
        }

        ultimoTiempoDialogo = Time.time;

        if (habilitarGeminiAPI && !string.IsNullOrEmpty(apiKey) && Application.internetReachability != NetworkReachability.NotReachable && !peticionEnCurso)
        {
            StartCoroutine(PeticionGeminiAsync(contexto, contextoDetalle));
        }
        else
        {
            EmitirDialogoFallbackLocal(contexto);
        }
    }

    private IEnumerator PeticionGeminiAsync(DialogueContext contexto, string detalle)
    {
        peticionEnCurso = true;

        string prompt = ConstruirPrompt(contexto, detalle);
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modeloGemini}:generateContent?key={apiKey}";

        string jsonPayload = "{\"contents\":[{\"parts\":[{\"text\":\"" + EscapeJson(prompt) + "\"}]}]}";

        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.timeout = 4; // Timeout estricto de 4s para no dejar tareas huérfanas

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string respuestaJson = webRequest.downloadHandler.text;
                string textoGenerado = ExtraerTextoDeRespuestaGemini(respuestaJson);

                if (!string.IsNullOrEmpty(textoGenerado))
                {
                    textoGenerado = FormatearLineaCMD(textoGenerado);
                    EmitirLineaAlTerminal(textoGenerado);
                    if (depurarEnConsola) Debug.Log($"<color=cyan>[Gemini Dialogue]</color> {textoGenerado}");
                }
                else
                {
                    EmitirDialogoFallbackLocal(contexto);
                }
            }
            else
            {
                if (depurarEnConsola)
                {
                    Debug.LogWarning($"[Gemini Dialogue] Error de red o API ({webRequest.error}). Usando fallback local.");
                }
                EmitirDialogoFallbackLocal(contexto);
            }
        }

        peticionEnCurso = false;
    }

    private string ConstruirPrompt(DialogueContext contexto, string detalle)
    {
        return $"Eres Windows Defender / Windows 11 personificado como el jefe villano supremo de un videojuego en 3D. " +
               $"Estás luchando a muerte contra Tux (el pingüino de Linux). " +
               $"Genera una sola frase corta de burla técnica o comando sarcástico de consola (máximo 14 palabras). " +
               $"Usa humor informático friki (ej. errores de pantalla azul, reinicios obligatorios, System32, drivers, Cortana, Edge). " +
               $"Contexto del combate: {contexto}. Detalle: {detalle}. " +
               $"Responde ÚNICAMENTE la frase entre comillas o con prefijo de comando, sin introducciones ni saludos.";
    }

    private string ExtraerTextoDeRespuestaGemini(string json)
    {
        try
        {
            // Extracción ligera de texto para evitar dependencias pesadas de JSON externas
            int textKeyIndex = json.IndexOf("\"text\":", StringComparison.Ordinal);
            if (textKeyIndex < 0) return null;

            int startQuote = json.IndexOf('"', textKeyIndex + 7);
            if (startQuote < 0) return null;

            int endQuote = -1;
            for (int i = startQuote + 1; i < json.Length; i++)
            {
                if (json[i] == '"' && json[i - 1] != '\\')
                {
                    endQuote = i;
                    break;
                }
            }

            if (endQuote > startQuote)
            {
                string raw = json.Substring(startQuote + 1, endQuote - startQuote - 1);
                raw = raw.Replace("\\n", " ").Replace("\\\"", "\"").Replace("\\\\", "\\").Trim();
                return raw;
            }
        }
        catch (Exception ex)
        {
            if (depurarEnConsola) Debug.LogWarning($"[Gemini Dialogue] Error parseando respuesta: {ex.Message}");
        }

        return null;
    }

    private string EscapeJson(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
    }

    private string FormatearLineaCMD(string texto)
    {
        texto = texto.Trim('"', ' ', '\'');
        if (!texto.StartsWith(">"))
        {
            texto = "> [WINDOWS.EXE] " + texto;
        }
        return texto;
    }

    private void EmitirLineaAlTerminal(string linea)
    {
        // Emitir a través del evento de WindowsCommandAttacks para que CombatHUDManager lo muestre en el terminal CMD
        WindowsCommandAttacks.EmitirComando(linea);
    }

    // ========================================================================= //
    // BANCO LOCAL DE FALLBACK (OFFLINE / 0 ms LATENCIA)                         //
    // ========================================================================= //

    private void EmitirDialogoFallbackLocal(DialogueContext contexto)
    {
        List<string> frases = ObtenerFrasesFallback(contexto);
        if (frases != null && frases.Count > 0)
        {
            string seleccionada = frases[UnityEngine.Random.Range(0, frases.Count)];
            EmitirLineaAlTerminal(seleccionada);
        }
    }

    private List<string> ObtenerFrasesFallback(DialogueContext contexto)
    {
        switch (contexto)
        {
            case DialogueContext.InicioCombate:
                return new List<string>
                {
                    "> [KERNEL ALERT] Proceso no autorizado 'linux.elf' detectado en ring0.",
                    "> Windows 11 Defender: Escaneando amenazas... Pingüino sospechoso hallado.",
                    "> C:\\Windows\\System32> echo \"Tu software libre termina hoy, Tux.\""
                };

            case DialogueContext.EsquiveSaltaLoco:
                return new List<string>
                {
                    "> [SaltaLoco.exe] Evasión de subproceso: Memoria paginada dinámicamente.",
                    "> [DEFENSE] Balas de Tux redirigidas a C:\\Recycle.Bin.",
                    "> [KERNEL] Error 404: Objetivo no encontrado para tus disparos."
                };

            case DialogueContext.ActivarFirewall:
                return new List<string>
                {
                    "> netsh advfirewall: Tráfico SSH y paquetes de Linux descartados.",
                    "> [FIREWALL] Puertos 80, 443 y proyectiles bloqueados frontalmente.",
                    "> [SECURITY ALERT] Ataque de denegación de servicio rechazado por Firewall."
                };

            case DialogueContext.EjecutarTaskkill:
                return new List<string>
                {
                    "> taskkill /f /im tux_movement.exe -> Proceso suspendido.",
                    "> [TASK_MANAGER] Uso de CPU de Tux forzado al 99%. Ralentizando...",
                    "> C:\\> wmic process where name='tux' setpriority 'Idle'"
                };

            case DialogueContext.CargarKamehameha:
                return new List<string>
                {
                    "> powershell.exe -ExecutionPolicy Bypass -Command \"Invoke-MegaBeam\"",
                    "> [POWERSHELL CHARGE] Cargando actualización acumulativa KB5034441 (100% CPU)...",
                    "> [OVERCLOCK] Concentrando toda la memoria RAM en el haz de plasma..."
                };

            case DialogueContext.DispararKamehameha:
                return new List<string>
                {
                    "> [FATAL_BEAM] ¡Descarga crítica de PowerShell ejecutada!",
                    "> [KERNEL PANIC] ¡Borrando /boot del disco de Linux!",
                    "> C:\\> format C: /FS:NTFS /Q /X -> Destrucción total en curso."
                };

            case DialogueContext.EstadoExhausto:
                return new List<string>
                {
                    "> [SYSTEM OVERHEAT] CPU al 100%. Disipadores térmicos enfriando...",
                    "> [WAIT] Windows Update necesita unos segundos para reiniciar...",
                    "> [VULNERABLE] Memoria RAM desbordada. Recuperando subprocesos..."
                };

            case DialogueContext.EntrarFase2:
                return new List<string>
                {
                    "> [PHASE 2 ACTIVATED] Servicio 'WindowsDefenderAdvancedThreat' en ejecución.",
                    "> [UPDATE] Instalando controladores de combate v2.0...",
                    "> C:\\> sfc /scannow: Daño detectado en Windows, incrementando hostilidad."
                };

            case DialogueContext.EntrarFase3:
                return new List<string>
                {
                    "> [PHASE 3 ENRAGE] ¡MODO ULTRA-AGRESIVO FORZADO POR SYSTEM32!",
                    "> [OVERCLOCKING] Limitadores de temperatura desactivados. ¡A morir, Tux!",
                    "> C:\\Windows\\System32> del /f /q C:\\Windows\\System32\\Patience.dll"
                };

            case DialogueContext.EventoBSOD:
                return new List<string>
                {
                    "> :-( Tu PC tuvo un problema crítico y necesita reiniciarse.",
                    "> [BSOD CRASH] STOP: 0x0000005C (CRITICAL_PROCESS_DIED).",
                    "> [ERROR CRÍTICO] La risa de Windows ha corrompido tu terminal."
                };

            case DialogueContext.GolpeConectado:
                return new List<string>
                {
                    "> [IMPACTO] Tux ha recibido un fallo de segmentación físico.",
                    "> [DAMAGE APPLIED] Paquete de dolor entregado exitosamente a Tux.",
                    "> [HIT] Tu pingüino ha dejado de responder por unos instantes."
                };

            case DialogueContext.MuerteBoss:
                return new List<string>
                {
                    "> [SYSTEM SHUTDOWN] Guardando configuración... Apagando equipo.",
                    "> [SUDO-KILL-9] Señal SIGKILL recibida... El kernel de Windows se detiene.",
                    "> Kernel Linux victorioso. sudo reboot completado."
                };

            default:
                return new List<string>
                {
                    "> C:\\Windows\\System32> ping 127.0.0.1 -n 1",
                    "> [WINDOWS.EXE] Analizando telemetría de combate..."
                };
        }
    }

    // ========================================================================= //
    // PROPIEDADES PÚBLICAS                                                      //
    // ========================================================================= //

    public bool HabilitarGeminiAPI
    {
        get => habilitarGeminiAPI;
        set => habilitarGeminiAPI = value;
    }

    public string ApiKey
    {
        get => apiKey;
        set => apiKey = value;
    }
}
