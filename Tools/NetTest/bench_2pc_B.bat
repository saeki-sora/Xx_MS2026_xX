@echo off
rem 2-PC 4-player benchmark, PC B: Client (P3) + Client (P4). Run after bench_2pc_A.bat on PC A.
rem Usage: bench_2pc_B.bat <IP address of PC A>
rem Logs: <build>\logs\p2_client_bench2pc.log and p3_client_bench2pc.log
setlocal
if "%~1"=="" (
  echo Usage: bench_2pc_B.bat ^<IP address of PC A^>
  pause
  exit /b 1
)
if "%LOGSUFFIX%"=="" set LOGSUFFIX=_bench2pc
call "%~dp0launch_2pc_B.bat" %~1 -fortress-bench -fortress-bench-quit 70 -fortress-bench-no-wave
endlocal
