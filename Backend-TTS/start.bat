@echo off
setlocal enabledelayedexpansion

title Sudo-Kill-9 - Microservicio TTS Local (Windows)
cd /d "%~dp0"

set "INTERACTIVE=1"
set "PASSTHROUGH_ARGS="

:parse_args
if "%~1"=="" goto after_args
if "%~1"=="--no-pause" (
    set "INTERACTIVE=0"
    shift
    goto parse_args
)
set "PASSTHROUGH_ARGS=!PASSTHROUGH_ARGS! %1"
shift
goto parse_args
:after_args

echo ==============================================================================
echo [TTS] Verificando e iniciando Microservicio TTS para Windows Boss...
echo ==============================================================================

:: 1. Comprobar instalación de Python en Windows
set "PYTHON_CMD="
py -3 --version >nul 2>nul && set "PYTHON_CMD=py -3"

if not defined PYTHON_CMD (
    python --version >nul 2>nul && set "PYTHON_CMD=python"
)

if not defined PYTHON_CMD (
    python3 --version >nul 2>nul && set "PYTHON_CMD=python3"
)

if not defined PYTHON_CMD (
    for /d %%D in ("%LocalAppData%\Programs\Python\Python3*") do (
        if exist "%%D\python.exe" (
            set "PYTHON_CMD=%%D\python.exe"
        )
    )
)

if not defined PYTHON_CMD (
    echo [TTS ERROR] Python 3 no se encuentra instalado o no esta configurado en el PATH de Windows. >&2
    echo [TTS ERROR] Por favor descarga e instala Python 3.9 o superior desde https://www.python.org/ >&2
    echo [TTS ERROR] IMPORTANTE: Marca la casilla 'Add python.exe to PATH' durante la instalacion. >&2
    if "!INTERACTIVE!"=="1" pause
    exit /b 1
)

:: 2. Crear archivo .env si no existe a partir del ejemplo
if not exist ".env" (
    if exist ".env.example" (
        echo [TTS] Creando .env inicial a partir de .env.example...
        copy /y .env.example .env >nul
    ) else (
        echo [TTS] Creando .env inicial con configuracion recomendada...
        (
            echo SERVER_HOST=0.0.0.0
            echo SERVER_PORT=8000
            echo TTS_VOICE=es-MX-JorgeNeural
            echo TTS_RATE=+5%%
            echo TTS_VOLUME=+0%%
            echo TTS_PITCH=-5Hz
            echo TTS_MAX_TEXT_LENGTH=500
            echo LRU_CACHE_CAPACITY=128
        ) > .env
    )
)

:: 3. Crear entorno virtual venv si no existe
set "NEED_INSTALL=0"
if not exist "venv\Scripts\python.exe" (
    echo [TTS] Creando entorno virtual Python en Backend-TTS\venv...
    %PYTHON_CMD% -m venv venv
    if !errorlevel! neq 0 (
        echo [TTS ERROR] No se pudo crear el entorno virtual venv. >&2
        if "!INTERACTIVE!"=="1" pause
        exit /b 1
    )
    set "NEED_INSTALL=1"
)

:: 4. Verificar dependencias de requirements.txt y cambios
if not exist "venv\Scripts\uvicorn.exe" set "NEED_INSTALL=1"
if not exist "venv\.requirements_installed" (
    set "NEED_INSTALL=1"
) else (
    fc /b requirements.txt venv\.requirements_installed >nul 2>nul
    if !errorlevel! neq 0 (
        echo [TTS] Se detectaron cambios en requirements.txt...
        set "NEED_INSTALL=1"
    )
)

if "!NEED_INSTALL!"=="1" (
    echo [TTS] Instalando dependencias de requirements.txt (fastapi, edge-tts, uvicorn)...
    venv\Scripts\python.exe -m pip install --upgrade pip
    venv\Scripts\python.exe -m pip install -r requirements.txt
    if !errorlevel! neq 0 (
        echo [TTS ERROR] Error al instalar las dependencias con pip. >&2
        if "!INTERACTIVE!"=="1" pause
        exit /b 1
    )
    copy /y requirements.txt venv\.requirements_installed >nul
    echo [TTS] Dependencias instaladas y actualizadas correctamente.
)

:: 5. Iniciar microservicio FastAPI con Uvicorn
echo [TTS] Iniciando microservicio FastAPI en http://127.0.0.1:8000...
venv\Scripts\python.exe -m uvicorn app.main:app --host 0.0.0.0 --port 8000 !PASSTHROUGH_ARGS!
