@echo off
rem 2-PC 4-player benchmark, PC A: Host (P1) + Client (P2). Start PC B within about 30 s.
rem When all 3 clients have joined and received the crowd, the host bursts 30000 enemies.
rem Every exe writes one [Bench] line per second and quits after 70 s.
rem Logs: <build>\logs\p0_host_bench2pc.log and p1_client_bench2pc.log
setlocal
if "%LOGSUFFIX%"=="" set LOGSUFFIX=_bench2pc
call "%~dp0launch_2pc_A.bat" -fortress-bench -fortress-bench-burst 30000 -fortress-bench-clients 3 -fortress-bench-delay 3 -fortress-bench-quit 70 -fortress-bench-no-wave %*
endlocal
