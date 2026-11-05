@echo off
rem ============================================================
rem  Grip controller (Arduino) check. Changes nothing: only looks, and opens the COM ports for a few seconds to read.
rem  Put this file and check_grip_sensor.ps1 next to MS2026.exe and double-click,
rem  or drag MS2026.exe onto this file. The result is saved on the desktop (MS2026_*.txt).
rem ============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0check_grip_sensor.ps1" -ExePath "%~1"
pause
