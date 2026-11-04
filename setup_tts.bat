@echo off
setlocal enabledelayedexpansion

title Sudo-Kill-9 - Instalador/Actualizador Microservicio TTS (Windows)
set "SCRIPT_DIR=%~dp0"

set "INTERACTIVE=1"
if "%~1"=="--no-pause" (
    set "INTERACTIVE=0"
)

if exist "%SCRIPT_DIR%Backend-TTS" (
    cd /d "%SCRIPT_DIR%Backend-TTS"
) else (
    cd /d "%SCRIPT_DIR%"
)

echo ==============================================================================
echo [SETUP TTS] Configurando entorno de voz local para Sudo-Kill-9 (Windows)...
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
    echo [SETUP ERROR] Python 3 no se encuentra instalado o no esta configurado en el PATH de Windows. >&2
    echo [SETUP ERROR] Descarga e instala Python 3.9 o superior desde https://www.python.org/ >&2
    echo [SETUP ERROR] IMPORTANTE: Marca la casilla 'Add python.exe to PATH' durante la instalacion. >&2
    if "!INTERACTIVE!"=="1" pause
    exit /b 1
)

for /f "tokens=*" %%i in ('%PYTHON_CMD% --version 2^>^&1') do echo [SETUP TTS] Python detectado: %%i

:: 2. Crear archivo .env si no existe a partir del ejemplo
if not exist ".env" (
    if exist ".env.example" (
        echo [SETUP TTS] Creando .env inicial a partir de .env.example...
        copy /y .env.example .env >nul
    ) else (
        echo [SETUP TTS] Creando .env inicial con configuracion recomendada...
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
) else (
    echo [SETUP TTS] Archivo .env existente detectado. Conservando configuracion.
)

:: 3. Crear entorno virtual venv si no existe
if not exist "venv\Scripts\python.exe" (
    echo [SETUP TTS] Creando entorno virtual Python en Backend-TTS\venv...
    %PYTHON_CMD% -m venv venv
    if !errorlevel! neq 0 (
        echo [SETUP ERROR] No se pudo crear el entorno virtual venv. >&2
        if "!INTERACTIVE!"=="1" pause
        exit /b 1
    )
) else (
    echo [SETUP TTS] Entorno virtual existente detectado en Backend-TTS\venv.
)

:: 4. Instalar o actualizar dependencias de requirements.txt
echo [SETUP TTS] Actualizando pip e instalando paquetes desde requirements.txt...
venv\Scripts\python.exe -m pip install --upgrade pip
venv\Scripts\python.exe -m pip install -r requirements.txt
if !errorlevel! neq 0 (
    echo [SETUP ERROR] Error al instalar dependencias con pip. >&2
    if "!INTERACTIVE!"=="1" pause
    exit /b 1
)
copy /y requirements.txt venv\.requirements_installed >nul

:: 5. Probar importaciones
echo [SETUP TTS] Verificando dependencias instaladas...
venv\Scripts\python.exe -c "import fastapi, uvicorn, edge_tts, pydantic, dotenv; print('[SETUP TTS] OK: Todas las dependencias cargaron correctamente.')"
if !errorlevel! neq 0 (
    echo [SETUP ERROR] Verificacion de dependencias fallida. >&2
    if "!INTERACTIVE!"=="1" pause
    exit /b 1
)

echo ==============================================================================
echo  [SETUP COMPLETADO CON EXITO]
echo  El microservicio TTS esta listo para sintetizar las voces del jefe Windows.
echo  - Puedes probarlo ejecutando: Backend-TTS\start.bat
echo  - O iniciar Unity para que arranque automaticamente en combate.
echo ==============================================================================
if "!INTERACTIVE!"=="1" pause
