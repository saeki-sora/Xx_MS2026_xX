@echo off
rem 2-PC 4-player test, PC A (the room owner): Host = P1 and Client = P2 on this PC.
rem Run this first, then launch_2pc_B.bat on the other PC with this PC's IP address.
rem Extra args are passed to both exes (e.g. benchmark options).
setlocal
if "%LOGSUFFIX%"=="" set LOGSUFFIX=_2pc
call "%~dp0run_instance.bat" host 0 - %* || exit /b 1
rem wait about 3 s so the host is listening (timeout.exe fails when stdin is redirected, ping works everywhere)
ping -n 4 127.0.0.1 >nul
call "%~dp0run_instance.bat" client 1 - %*
endlocal
