@echo off
rem Host (P1) + Client (P2) on this PC
call "%~dp0run_instance.bat" host 0 || exit /b 1
rem wait about 3 s (timeout.exe fails when stdin is redirected, ping works everywhere)
ping -n 4 127.0.0.1 >nul
call "%~dp0run_instance.bat" client 1
