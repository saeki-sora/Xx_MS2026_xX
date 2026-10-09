@echo off
rem 2-PC 4-player test, PC B: Client = P3 and Client = P4, connecting to PC A.
rem Usage: launch_2pc_B.bat <IP address of PC A> [extra args...]
setlocal
if "%~1"=="" (
  echo Usage: launch_2pc_B.bat ^<IP address of PC A^>
  echo   Find it on PC A: open Command Prompt, type ipconfig, use the IPv4 Address.
  pause
  exit /b 1
)
set HOSTIP=%~1
set EXTRA=
:collect
if "%~2"=="" goto collected
set EXTRA=%EXTRA% %2
shift /2
goto collect
:collected
if "%LOGSUFFIX%"=="" set LOGSUFFIX=_2pc
call "%~dp0run_instance.bat" client 2 %HOSTIP%%EXTRA% || exit /b 1
ping -n 3 127.0.0.1 >nul
call "%~dp0run_instance.bat" client 3 %HOSTIP%%EXTRA%
endlocal
