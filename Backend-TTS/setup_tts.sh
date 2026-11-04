#!/usr/bin/env bash
# Script delegado a setup_tts.sh
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -f "$SCRIPT_DIR/../setup_tts.sh" ]; then
    exec "$SCRIPT_DIR/../setup_tts.sh" "$@"
else
    exec "$SCRIPT_DIR/start.sh" "$@"
fi
