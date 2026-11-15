@echo off
rem CPU autoplay without network: all four seats are CPU, 10 matches at 2x speed, result table (CSV) written.
rem CSV: %USERPROFILE%\AppData\LocalLow\<company>\<product>\CPU\cpu_<date>.csv  (persistentDataPath\CPU)
rem Log: <build>\logs\p0_ui_cpu_autoplay.log  (lines starting with [CPU])
rem Extra args are passed to the exe, e.g.:  cpu_autoplay.bat -cpu-level hard -cpu-seed 123
rem Same start as bench_offline.bat (the build must start in the game scene for autoplay to run).
setlocal
if "%LOGSUFFIX%"=="" set LOGSUFFIX=_cpu_autoplay
call "%~dp0run_instance.bat" ui 0 - -cpu-all -autoplay 10 -timescale 2 -cpu-csv %*
endlocal
