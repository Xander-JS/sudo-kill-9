#!/usr/bin/env bash
# ==============================================================================
# SCRIPT DE INICIO RÁPIDO PARA EL MICROSERVICIO TTS LOCAL - SUDO-KILL-9
# ==============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# 1. Comprobar instalación de Python 3
PYTHON_BIN=""
if command -v python3 >/dev/null 2>&1; then
    PYTHON_BIN="python3"
elif command -v python >/dev/null 2>&1; then
    PYTHON_BIN="python"
else
    echo "[TTS ERROR] Python 3 no está instalado o no se encuentra en el PATH del sistema." >&2
    echo "[TTS ERROR] Por favor instala Python 3.9 o superior (ej: sudo apt install python3 python3-venv python3-pip)." >&2
    exit 1
fi

# 2. Crear archivo .env si no existe a partir del ejemplo
if [ ! -f ".env" ]; then
    if [ -f ".env.example" ]; then
        echo "[TTS] Creando .env inicial a partir de .env.example..."
        cp .env.example .env
    else
        echo "[TTS] Creando .env con valores predeterminados..."
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
fi

# 3. Crear entorno virtual venv si no existe
NEED_INSTALL=0
if [ ! -d "venv" ] || [ ! -f "venv/bin/activate" ] || [ ! -f "venv/bin/python" ]; then
    echo "[TTS] Creando entorno virtual Python en $SCRIPT_DIR/venv..."
    "$PYTHON_BIN" -m venv venv
    NEED_INSTALL=1
fi

# 4. Verificar dependencias de requirements.txt y cambios
STAMP_FILE="venv/.requirements_installed"
if [ ! -f "venv/bin/uvicorn" ] || [ ! -f "$STAMP_FILE" ]; then
    NEED_INSTALL=1
elif [ -f "requirements.txt" ]; then
    if ! cmp -s requirements.txt "$STAMP_FILE"; then
        echo "[TTS] Se detectaron cambios en requirements.txt..."
        NEED_INSTALL=1
    fi
fi

if [ "$NEED_INSTALL" -eq 1 ]; then
    echo "[TTS] Instalando dependencias de requirements.txt..."
    ./venv/bin/pip install --upgrade pip
    ./venv/bin/pip install -r requirements.txt
    cp requirements.txt "$STAMP_FILE"
    echo "[TTS] Dependencias instaladas y actualizadas correctamente."
fi

# 5. Parámetros opcionales de recarga y compatibilidad
RELOAD_FLAG=""
for arg in "$@"; do
    if [ "$arg" = "--reload" ]; then
        RELOAD_FLAG="--reload"
    fi
done
if [ "$TTS_RELOAD" = "1" ]; then
    RELOAD_FLAG="--reload"
fi

echo "[TTS] Iniciando microservicio FastAPI en http://127.0.0.1:8000..."
exec ./venv/bin/uvicorn app.main:app --host 0.0.0.0 --port 8000 $RELOAD_FLAG
