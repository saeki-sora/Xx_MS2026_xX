@echo off
rem Late-join test: Host (P1) + 2 Clients (P2, P3) start, the host bursts 30000 swarm enemies,
rem and P4 joins about 15 s later while the crowd is on the field (it must receive the full state
rem of ~30000 enemies and then follow the stream). All log every second and quit after 50 s.
rem Logs: <build>\logs\p0_host_latejoin.log ... p3_client_latejoin.log
rem Extra args are passed to ALL exes.
setlocal
if "%LOGSUFFIX%"=="" set LOGSUFFIX=_latejoin
call "%~dp0run_instance.bat" host 0 - -fortress-bench -fortress-bench-burst 30000 -fortress-bench-clients 2 -fortress-bench-delay 3 -fortress-bench-quit 50 -fortress-bench-no-wave %* || exit /b 1
ping -n 4 127.0.0.1 >nul
call "%~dp0run_instance.bat" client 1 - -fortress-bench -fortress-bench-quit 50 -fortress-bench-no-wave %*
call "%~dp0run_instance.bat" client 2 - -fortress-bench -fortress-bench-quit 50 -fortress-bench-no-wave %*
rem wait about 15 s, then the 4th player joins mid-game
ping -n 16 127.0.0.1 >nul
call "%~dp0run_instance.bat" client 3 - -fortress-bench -fortress-bench-quit 35 -fortress-bench-no-wave %*
endlocal
