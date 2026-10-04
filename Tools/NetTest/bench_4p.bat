@echo off
rem Benchmark Host (P1) + 3 Clients (P2-P4) on this PC: once all 3 clients are connected and have the
rem full state, the host bursts 30000 swarm enemies. All 4 log every second and quit after 50 s.
rem Logs: <build>\logs\p0_host_bench4.log, p1_client_bench4.log, p2_client_bench4.log, p3_client_bench4.log
rem Extra args are passed to ALL exes (e.g. -fortress-swarm-snapshot-rate 20).
rem Note: 4 games on one PC share the CPU, so FPS is lower than on 4 PCs. Use it for correctness,
rem traffic and "does every client look the same" checks; compare FPS on real PCs.
setlocal
if "%LOGSUFFIX%"=="" set LOGSUFFIX=_bench4
call "%~dp0run_instance.bat" host 0 - -fortress-bench -fortress-bench-burst 30000 -fortress-bench-clients 3 -fortress-bench-delay 3 -fortress-bench-quit 50 -fortress-bench-no-wave %* || exit /b 1
rem wait about 3 s (timeout.exe fails when stdin is redirected, ping works everywhere)
ping -n 4 127.0.0.1 >nul
call "%~dp0run_instance.bat" client 1 - -fortress-bench -fortress-bench-quit 50 -fortress-bench-no-wave %*
call "%~dp0run_instance.bat" client 2 - -fortress-bench -fortress-bench-quit 50 -fortress-bench-no-wave %*
call "%~dp0run_instance.bat" client 3 - -fortress-bench -fortress-bench-quit 50 -fortress-bench-no-wave %*
endlocal
