@echo off
rem ============================================================
rem  Exhibition: create 4 desktop shortcuts "MS2026 sensor N" (MS2026.exe -grip-sensor N, N = 1..4).
rem  Put this file and make_sensor_shortcuts.ps1 next to MS2026.exe and double-click,
rem  or drag MS2026.exe onto this file, or run it from <project>\Tools\Exhibition (uses Builds\TestBuild).
rem  Extra launch options for every shortcut: set MS2026_EXTRA_ARGS=-gripdebug before running.
rem ============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0make_sensor_shortcuts.ps1" -ExePath "%~1" -ExtraArgs "%MS2026_EXTRA_ARGS%"
pause
