@echo off
REM ============================================================
REM  FBX Geometric Optimization — Pilot Run (3 models)
REM  Run from the project root or double-click this file.
REM ============================================================
REM  Requires Blender to be on PATH, e.g.:
REM    set PATH=%PATH%;C:\Program Files\Blender Foundation\Blender 4.2
REM  Or edit BLENDER_EXE below to the full path.
REM ============================================================

set BLENDER_EXE=blender
set SCRIPT=%~dp0optimize_fbx.py

echo.
echo === FBX Optimization Pilot (cpu, gpu, ventilador_refrigeracion) ===
echo.

"%BLENDER_EXE%" --background --python "%SCRIPT%" -- --models cpu gpu ventilador_refrigeracion

echo.
echo Done. Check Tools\optimization_report.md for results.
pause
