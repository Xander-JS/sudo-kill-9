#!/usr/bin/env bash
# ==============================================================================
# SCRIPT DE INSTALACIÓN Y ACTUALIZACIÓN DEL MICROSERVICIO TTS - SUDO-KILL-9
# Ejecutar cuando se clona el repositorio o al bajar cambios de git.
# ==============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Si se ejecuta desde la raíz del proyecto, entrar a Backend-TTS
if [ -d "$SCRIPT_DIR/Backend-TTS" ]; then
    TTS_DIR="$SCRIPT_DIR/Backend-TTS"
else
    TTS_DIR="$SCRIPT_DIR"
fi

cd "$TTS_DIR"
echo "=============================================================================="
echo "[SETUP TTS] Configurando entorno de voz local para Sudo-Kill-9..."
echo "[SETUP TTS] Directorio de trabajo: $TTS_DIR"
echo "=============================================================================="

# 1. Comprobar instalación de Python 3
PYTHON_BIN=""
if command -v python3 >/dev/null 2>&1; then
    PYTHON_BIN="python3"
elif command -v python >/dev/null 2>&1; then
    PYTHON_BIN="python"
else
    echo "[SETUP ERROR] Python 3 no está instalado o no se encuentra en el PATH del sistema." >&2
    echo "[SETUP ERROR] Por favor instala Python 3.9 o superior:" >&2
    echo "  Ubuntu/Debian: sudo apt update && sudo apt install -y python3 python3-venv python3-pip" >&2
    echo "  Arch Linux:    sudo pacman -S python python-pip" >&2
    echo "  Fedora:        sudo dnf install -y python3 python3-pip" >&2
    exit 1
fi

PY_VERSION=$("$PYTHON_BIN" --version 2>&1)
echo "[SETUP TTS] Python detectado: $PY_VERSION ($PYTHON_BIN)"

# 2. Configurar .env a partir de .env.example
if [ ! -f ".env" ]; then
    if [ -f ".env.example" ]; then
        echo "[SETUP TTS] Creando .env inicial a partir de .env.example..."
        cp .env.example .env
    else
        echo "[SETUP TTS] Creando archivo .env con valores predeterminados..."
        cat << 'EOF' > .env
SERVER_HOST=0.0.0.0
SERVER_PORT=8000
TTS_VOICE=es-MX-JorgeNeural
TTS_RATE=+5%
TTS_VOLUME=+0%
TTS_PITCH=-5Hz
TTS_MAX_TEXT_LENGTH=500
LRU_CACHE_CAPACITY=128
EOF
    fi
else
    echo "[SETUP TTS] Archivo .env existente detectado. Conservando configuración."
fi

# 3. Crear entorno virtual venv si no existe
if [ ! -d "venv" ] || [ ! -f "venv/bin/activate" ] || [ ! -f "venv/bin/python" ]; then
    echo "[SETUP TTS] Creando entorno virtual Python en $TTS_DIR/venv..."
    "$PYTHON_BIN" -m venv venv
else
    echo "[SETUP TTS] Entorno virtual existente detectado en $TTS_DIR/venv."
fi

# 4. Instalar o actualizar dependencias de requirements.txt
echo "[SETUP TTS] Actualizando pip e instalando paquetes desde requirements.txt..."
./venv/bin/pip install --upgrade pip
./venv/bin/pip install -r requirements.txt
cp requirements.txt "venv/.requirements_installed"

# 5. Probar importaciones críticas
echo "[SETUP TTS] Verificando librerías instaladas en el entorno virtual..."
./venv/bin/python -c "import fastapi, uvicorn, edge_tts, pydantic, dotenv; print('[SETUP TTS] OK: Todas las dependencias (fastapi, uvicorn, edge-tts, pydantic) cargaron correctamente.')"

# 6. Asegurar permisos de ejecución en scripts bash
chmod +x "$TTS_DIR/start.sh"
if [ -f "$SCRIPT_DIR/setup_tts.sh" ]; then
    chmod +x "$SCRIPT_DIR/setup_tts.sh"
fi

echo "=============================================================================="
echo " [SETUP COMPLETADO CON ÉXITO]"
echo " El microservicio TTS está 100% listo para sintetizar las voces del jefe Windows."
echo " - Puedes iniciarlo manualmente con: ./Backend-TTS/start.sh"
echo " - O dejar que Unity lo inicie automáticamente al entrar en combate con Windows Boss."
echo "=============================================================================="
