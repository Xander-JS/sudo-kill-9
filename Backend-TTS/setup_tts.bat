@echo off
set "SCRIPT_DIR=%~dp0"
if exist "%SCRIPT_DIR%..\setup_tts.bat" (
    call "%SCRIPT_DIR%..\setup_tts.bat" %*
) else (
    call "%SCRIPT_DIR%start.bat" %*
)
