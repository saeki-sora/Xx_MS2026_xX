@echo off
rem Two windows with the connect UI only (press Host / Client buttons by hand)
call "%~dp0run_instance.bat" ui 0 || exit /b 1
call "%~dp0run_instance.bat" ui 1
