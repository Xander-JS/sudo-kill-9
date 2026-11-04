# Sistema de Voz Sintética (TTS) para Windows Boss | SUDO-KILL-9

Guía completa de arquitectura, despliegue y configuración del sistema de voz en tiempo real para el jefe supremo **Windows** en el videojuego *Sudo-Kill-9*.

---

## 1. Arquitectura del Sistema

El villano Windows cuenta con una arquitectura híbrida de tres capas desacopladas, diseñada para que **ningún servicio externo o de red pueda bloquear el bucle de juego (60+ FPS), el movimiento NavMeshAgent ni las animaciones de combate**:

```
+-----------------------------------------------------------------------------------+
|                              CLIENTE UNITY                                        |
|                                                                                   |
|  [WindowsBossAI] (HFSM Combate Local - 0 ms)                                      |
|         │                                                                         |
|         ▼                                                                         |
|  [WindowsAIGeminiDialogue]                                                        |
|         │                                                                         |
|         ├─► Inmediato (0 ms) ──► [WindowsHeadHealthBar] (Burbuja de texto 3D)     |
|         │                                                                         |
|         └─► Inmediato (0 ms) ──► [WindowsVoiceManager]                            |
|                                         │                                         |
|                                   HTTP POST JSON                                  |
|                                 (Timeout max 2.0s)                                |
|                                         │                                         |
+─────────────────────────────────────────┼─────────────────────────────────────────+
                                          ▼
+-----------------------------------------------------------------------------------+
|                   MICROSERVICIO PYTHON LOCAL (FastAPI :8000)                      |
|                                                                                   |
|  [main.py] ──► [schemas.py: Validación Pydantic]                                  |
|                      │                                                            |
|                      ▼                                                            |
|           [LRU In-Memory Cache] ──(Hit: < 1 ms)──► Retorno directo MP3            |
|                      │ (Miss)                                                     |
|                      ▼                                                            |
|           [edge-tts Communicate.stream()] ──(Zero Disk I/O)──► Stream BytesIO     |
|                      │                                                            |
|                      ▼ Retorno HTTP 200 (audio/mpeg)                              |
+─────────────────────────────────────────┼─────────────────────────────────────────+
                                          │
+─────────────────────────────────────────┼─────────────────────────────────────────+
|                                         ▼                                         |
|  [DownloadHandlerAudioClip] ──► [AudioSource 3D Spatial] ──► Altavoces / Tux      |
+-----------------------------------------------------------------------------------+
```

### Principios Fundamentales
1. **Cero Bloqueo de Gameplay**: La navegación, las colisiones, los combos de ataque y los disparos se ejecutan localmente en el hilo principal sin esperar a la voz.
2. **Visualización Inmediata**: La frase aparece al instante en la consola y en la burbuja 3D sobre la cabeza del jefe.
3. **Timeout Estricto de 2.0s**: Si la red o el servicio demoran más de 2 segundos de reloj real (`Time.realtimeSinceStartup`), la petición se cancela silenciosamente y el juego continúa sin interrupción.
4. **Cero Escritura en Disco (Zero Disk I/O)**: El servidor Python genera el audio en memoria RAM (`io.BytesIO`) y lo transmite a Unity directamente.
5. **Caché en Memoria RAM**: Las frases recurrentes de combate (provocaciones, fallbacks) se almacenan en una caché LRU, respondiendo en **menos de 1 milisegundo**.

---

## 2. Requisitos Previos

- **Python**: 3.10 o superior (instalado en el sistema anfitrión o mediante contenedor).
- **Conexión a Internet**: Necesaria para la síntesis de frases inéditas a través de los servidores de Microsoft Edge TTS (las frases en caché no requieren conexión).
- **Puerto de Red**: Puerto `8000` libre en `127.0.0.1` (o `0.0.0.0`).
- **Unity**: 6000.x o compatible con soporte para `AudioType.MPEG`.

---

## 3. Puesta en Marcha del Microservicio Python

El código del servidor reside en el directorio `Backend-TTS/`. Se proporcionan tres métodos de ejecución:

### Opción A: Arranque Automático con Script (Recomendado)
Desde la raíz del repositorio o dentro de `Backend-TTS/`:

```bash
chmod +x Backend-TTS/start.sh
./Backend-TTS/start.sh
```

El script se encarga de:
- Copiar `.env.example` a `.env` si no existe.
- Crear el entorno virtual `Backend-TTS/venv`.
- Instalar o actualizar las dependencias (`fastapi`, `uvicorn`, `edge-tts`, `pydantic`, `python-dotenv`).
- Levantar el servidor en `http://127.0.0.1:8000` con recarga en caliente (`--reload`).

---

### Opción B: Ejecución Manual con Entorno Virtual

```bash
cd Backend-TTS

# 1. Crear entorno virtual
python3 -m venv venv
source venv/bin/activate

# 2. Instalar dependencias
pip install --upgrade pip
pip install -r requirements.txt

# 3. Configurar variables de entorno
cp .env.example .env

# 4. Iniciar servicio
uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```

---

### Opción C: Despliegue con Docker

El servicio incluye un `Dockerfile` optimizado y seguro (ejecutado con usuario no-root):

```bash
cd Backend-TTS

# 1. Construir imagen
docker build -t sudo-kill-9-tts:latest .

# 2. Ejecutar contenedor
docker run -d \
  --name windows-boss-tts \
  -p 8000:8000 \
  --restart unless-stopped \
  sudo-kill-9-tts:latest

# 3. Ver logs
docker logs -f windows-boss-tts
```

---

## 4. Configuración de Voces y Parámetros

El microservicio se configura mediante el archivo `Backend-TTS/.env`:

| Variable | Valor por Defecto | Descripción |
|---|---|---|
| `SERVER_HOST` | `0.0.0.0` | IP de escucha del servidor. |
| `SERVER_PORT` | `8000` | Puerto HTTP para la API. |
| `TTS_VOICE` | `es-MX-JorgeNeural` | Voz por defecto de Edge TTS. |
| `TTS_RATE` | `+5%` | Velocidad de habla (+5% es ágil y desafiante). |
| `TTS_VOLUME` | `+0%` | Ganancia de volumen. |
| `TTS_PITCH` | `-5Hz` | Ajuste de tono (-5Hz otorga resonancia grave y amenazante). |
| `TTS_MAX_TEXT_LENGTH` | `500` | Límite de seguridad en caracteres por frase. |
| `LRU_CACHE_CAPACITY` | `128` | Cantidad de audios guardados en memoria RAM. |

### Voces Recomendadas

| Identificador | Idioma | Estilo |
|---|---|---|
| `es-MX-JorgeNeural` | Español (México) | Masculino, autoritario, técnico. **(Recomendado oficial)** |
| `es-ES-AlvaroNeural` | Español (España) | Masculino, formal, firme y serio. |
| `en-US-ChristopherNeural` | Inglés (EE.UU.) | Ejecutivo, frío, corporativo. |
| `en-US-GuyNeural` | Inglés (EE.UU.) | Masculino, enérgico y dinámico. |

---

## 5. Endpoints del Microservicio

### 1. `POST /generate_audio`
Genera el stream MP3 en memoria a partir del texto proporcionado.

- **Payload JSON**:
```json
{
  "text": "Tu kernel de Linux será purgado de este sistema.",
  "voice": "es-MX-JorgeNeural",
  "rate": "+5%",
  "pitch": "-5Hz"
}
```
- **Respuesta**:
  - `200 OK`: `Content-Type: audio/mpeg`, con los bytes de audio MP3 listos para streaming.
  - `400 Bad Request`: Texto vacío o mayor al límite de caracteres.
  - `500 Internal Server Error`: Fallo en el motor de síntesis.

### 2. `GET /health`
Consulta el estado de salud del servicio y el número de audios almacenados en memoria caché:
```json
{
  "status": "ok",
  "service": "Sudo-Kill-9 Windows Boss TTS Microservice",
  "default_voice": "es-MX-JorgeNeural",
  "cached_audios_count": 12
}
```

### 3. `GET /voices`
Devuelve la lista detallada de voces recomendadas con sus metadatos.

### 4. `POST /clear_cache`
Limpia la memoria caché LRU.

---

## 6. Configuración en Unity (`WindowsVoiceManager.cs`)

El script [`WindowsVoiceManager.cs`](file:///home/kevin/Universidad/interfaces/juego/sudo-kill-9/Assets/_Project/Scripts/Audio/WindowsVoiceManager.cs) se adjunta al GameObject de Windows (`windows_opt`):

### Propiedades del Inspector
- **Server Url**: `http://127.0.0.1:8000/generate_audio` (o la IP del servidor en red local si se ejecuta en otra máquina).
- **Habilitar Voz**: `true` (permite silenciar la voz instantáneamente desde el Inspector o menú de opciones).
- **Timeout Segundos**: `2.0` (tiempo máximo tolerado para no retrasar el combate).
- **Voice Id**: `es-MX-JorgeNeural` (dejar vacío para usar el valor del servidor).
- **Speech Rate**: `+5%`.
- **Speech Pitch**: `-5Hz`.
- **Interrumpir Frase Anterior**: `true` (si el jefe lanza una burla urgente o sufre un impacto, la nueva frase corta la anterior de inmediato).
- **Audio Source**: Componente `AudioSource` 3D configurado automáticamente:
  - `Spatial Blend = 1.0` (100% 3D en el espacio del mapa).
  - `Rolloff Mode = Logarithmic`.
  - `Min Distance = 4.0m`.
  - `Max Distance = 40.0m`.

---

## 7. Auto-Arranque Portátil desde Unity (`TTSProcessLauncher.cs`)

Para evitar que los desarrolladores o jugadores tengan que abrir una terminal manualmente y escribir comandos:

1. **Auto-Detección y Cero Hardcoding**:
   - `TTSProcessLauncher` resuelve dinámicamente la ruta a `Backend-TTS/` a partir de `Application.dataPath`, compatible con Unity Editor, Standalone Linux/Windows y macOS bundles.
2. **Chequeo Previo de Salud (`/health`)**:
   - Antes de iniciar cualquier proceso, consulta `http://127.0.0.1:8000/health`. Si el servicio ya está corriendo, **NO genera procesos duplicados**.
3. **Arranque en Segundo Plano Desacoplado**:
   - Se ejecuta en un subproceso asíncrono (`Task.Run`) sin bloquear fotogramas ni pausar el juego.
4. **Instalación Automática de Dependencias**:
   - `start.sh` (Linux/macOS) y `start.bat` (Windows) comprueban si existe el entorno `venv`. Si no existe, lo crean e instalan `requirements.txt` automáticamente.
   - Si se detectan cambios en `requirements.txt` tras un `git pull`, se actualizan los paquetes automáticamente.
5. **Apagado Limpio (Zero Orphan Processes)**:
   - Al salir de Play Mode en Unity o al cerrar el juego (`OnApplicationQuit`), si el proceso fue iniciado por Unity, se finaliza limpiamente mediante señales ordenadas (SIGTERM -> SIGKILL en Unix / `taskkill /F /T` en Windows).
   - El PID del proceso se persiste en `UnityEditor.SessionState` para no perder la referencia ante recargas de dominio (Domain Reloads).

### Herramientas de Editor (`Tools > SudoKill9 > TTS`)
- **Verificar Estado y Rutas Portátiles**: Inspecciona las rutas detectadas y realiza un sondeo HTTP en vivo.
- **Iniciar Microservicio TTS**: Levanta el microservicio directamente desde el Editor sin entrar a Play Mode.
- **Detener Microservicio TTS**: Detiene el proceso del microservicio de forma inmediata.

---

## 8. Scripts Rápidos para Compañeros de Equipo (Git Pull)

Cuando los miembros del equipo clonen el repositorio o bajen cambios:

### En Linux / macOS:
```bash
./setup_tts.sh
```

### En Windows:
```cmd
setup_tts.bat
```

Ambos scripts se encargan de:
1. Detectar el intérprete de Python (con soporte para `py -3`, `python3`, `python` y rutas en `%LocalAppData%`).
2. Configurar el archivo `.env`.
3. Crear el entorno virtual `venv` si no existe.
4. Instalar o actualizar dependencias de `requirements.txt`.
5. Ejecutar una verificación rápida para confirmar que el entorno está 100% operativo.

---

## 9. Pruebas y Verificación

### Prueba Rápida con Terminal (`curl`)

```bash
# Probar generación de audio y medir tiempo de respuesta
curl -s -o test_voice.mp3 -w "HTTP: %{http_code} | Tiempo: %{time_total}s\n" \
  -X POST http://127.0.0.1:8000/generate_audio \
  -H "Content-Type: application/json" \
  -d '{"text": "Preparando actualización forzada de Windows Defender.", "voice": "es-MX-JorgeNeural"}'

# Segunda llamada (debe ser un Cache Hit en < 1 milisegundo)
curl -s -o /dev/null -w "Cache Hit Tiempo: %{time_total}s\n" \
  -X POST http://127.0.0.1:8000/generate_audio \
  -H "Content-Type: application/json" \
  -d '{"text": "Preparando actualización forzada de Windows Defender.", "voice": "es-MX-JorgeNeural"}'
```

### Verificación Automatizada en Unity Editor
1. En la barra superior de Unity, abre el menú:
   `Tools > SudoKill9 > Ejecutar Verificación de Combate`
2. El sistema ejecutará las 27 pruebas de combate:
   - **Test 25**: Verifica la API Key y el pool de fallbacks de `WindowsAIGeminiDialogue`.
   - **Test 26**: Verifica la configuración 3D de `WindowsVoiceManager` y la ejecución no bloqueante de `Speak()`.
   - **Test 27**: Verifica la resolución de rutas portátiles de `TTSProcessLauncher` y la existencia de los scripts de inicio.
3. El resultado quedará registrado en `Temp/TestResults_WindowsBossCombat.txt` con estado **PASSED**.

---

## 10. Solución de Problemas (Troubleshooting)

### ¿Qué ocurre si el microservicio Python no puede iniciar?
Unity no se bloquea ni lanza excepciones críticas. `WindowsVoiceManager` detecta el fallo silenciosamente, la burbuja de diálogo 3D muestra el texto normalmente y el combate continúa fluido a 60 FPS.

### Error: Python 3 no se encuentra instalado (en Windows)
Asegúrate de tener Python 3.9 o superior instalado desde [python.org](https://www.python.org/) y de haber marcado la casilla **"Add python.exe to PATH"** durante la instalación.

### Latencia de la primera frase
La primera vez que Windows dice una frase inédita, `edge-tts` requiere entre 0.8s y 1.4s para contactar con los servidores de síntesis de Microsoft. Las frases recurrentes de combate se entregan en **0.001 segundos** gracias a la caché LRU en memoria.
