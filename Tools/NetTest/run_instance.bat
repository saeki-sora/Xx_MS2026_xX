@echo off
rem ============================================================
rem  MS2026 network test launcher (one instance)
rem  Usage: run_instance.bat host^|client^|ui <playerIndex 0-3> [hostAddress] [extra args...]
rem    host/client : connect automatically on startup
rem    ui          : show the connect UI only (manual test / offline)
rem    hostAddress : "-" or empty = 127.0.0.1
rem    extra args  : passed to MS2026.exe as is (benchmark options etc.)
rem
rem  Build folder: set MS2026_BUILD_DIR to override.
rem    default 1: <project>\Builds\TestBuild  (git-ignored)
rem    default 2: %USERPROFILE%\Downloads\TestBuild
rem  Logs are written to <build folder>\logs\p<player>_<role><LOGSUFFIX>.log (outside git).
rem ============================================================
setlocal
set ROLE=%~1
set PLAYER=%~2
set ADDR=%~3
if "%ADDR%"=="" set ADDR=127.0.0.1
if "%ADDR%"=="-" set ADDR=127.0.0.1

set EXTRA=
:collect
if "%~4"=="" goto collected
set EXTRA=%EXTRA% %4
shift /4
goto collect
:collected

if "%MS2026_BUILD_DIR%"=="" (
  if exist "%~dp0..\..\Builds\TestBuild\MS2026.exe" (
    set "MS2026_BUILD_DIR=%~dp0..\..\Builds\TestBuild"
  ) else (
    set "MS2026_BUILD_DIR=%USERPROFILE%\Downloads\TestBuild"
  )
)

if not exist "%MS2026_BUILD_DIR%\MS2026.exe" (
  echo MS2026.exe not found in "%MS2026_BUILD_DIR%".
  echo Build to that folder, or set MS2026_BUILD_DIR.
  pause
  exit /b 1
)

if not exist "%MS2026_BUILD_DIR%\logs" mkdir "%MS2026_BUILD_DIR%\logs"
start "" "%MS2026_BUILD_DIR%\MS2026.exe" -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile "%MS2026_BUILD_DIR%\logs\p%PLAYER%_%ROLE%%LOGSUFFIX%.log" -fortress-start %ROLE% -fortress-player %PLAYER% -fortress-address %ADDR% -fortress-tile%EXTRA%
endlocal
