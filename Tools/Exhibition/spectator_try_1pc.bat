@echo off
rem ============================================================
rem  Try the spectator page (phones send germs) on ONE PC.
rem  Double-click this file:
rem    1. starts the game (Builds\SpectatorTest\MS2026.exe) straight into a match (offline host)
rem    2. opens the spectator page in your web browser after a few seconds
rem  The page uses port 47795 so it does not clash with a Unity editor that is still holding 47790.
rem  Close the game window to finish.
rem ============================================================
setlocal
set "BUILD=%~dp0..\..\Builds\SpectatorTest"
if not "%~1"=="" set "BUILD=%~1"
if not exist "%BUILD%\MS2026.exe" (
  echo MS2026.exe was not found in "%BUILD%".
  echo Build the game to Builds\SpectatorTest first, or drag the build folder onto this file.
  pause
  exit /b 1
)
set PORT=47795
echo Starting the game...
start "" "%BUILD%\MS2026.exe" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -fortress-start host -fortress-player 0 -spectator-port %PORT%
echo Waiting for the game to open the spectator page...
ping -n 9 127.0.0.1 >nul
start "" "http://localhost:%PORT%/"
echo.
echo Spectator page: http://localhost:%PORT%/
echo In the game window, press F10 to see the page status and this PC's addresses.
endlocal
