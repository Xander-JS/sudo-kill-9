@echo off
REM ============================================================
REM  FBX Geometric Optimization — FULL RUN (all environment models)
REM  *** Requires your authorization before running. ***
REM  Run from the project root or double-click this file.
REM ============================================================

set BLENDER_EXE=blender
set SCRIPT=%~dp0optimize_fbx.py

echo.
echo === FBX Optimization FULL RUN ===
echo.
echo This will process all environment models.
echo Originals are NOT overwritten. Outputs are *_opt.fbx files.
echo.
set /p CONFIRM=Type YES to continue:
if /i not "%CONFIRM%"=="YES" (
    echo Aborted.
    pause
    exit /b 0
)

"%BLENDER_EXE%" --background --python "%SCRIPT%" -- ^
  --models ^
  cpu gpu chip ram ^
  targeta_red disco_almacenamiento puertos_conexion ^
  batería_CMOS condensador resistencia ventilador_refrigeracion

echo.
echo Done. Check Tools\optimization_report.md for results.
pause
