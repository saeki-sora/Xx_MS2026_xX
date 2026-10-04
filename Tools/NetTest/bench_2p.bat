@echo off
rem Benchmark Host (P1) + Client (P2) on this PC: once the client has the full state,
rem the host bursts 30000 swarm enemies. Both log every second and quit after 45 s.
rem Logs: <build>\logs\p0_host_bench.log / p1_client_bench.log  (lines starting with [Bench])
rem Extra args are passed to BOTH exes, e.g. the "before" settings:
rem   bench_2p.bat -fortress-swarm-spawns-per-frame 0 -fortress-replica-separation -1 -fortress-net-packet-queue 128
setlocal
if "%LOGSUFFIX%"=="" set LOGSUFFIX=_bench
call "%~dp0run_instance.bat" host 0 - -fortress-bench -fortress-bench-burst 30000 -fortress-bench-clients 1 -fortress-bench-delay 3 -fortress-bench-quit 45 -fortress-bench-no-wave %* || exit /b 1
rem wait about 3 s (timeout.exe fails when stdin is redirected, ping works everywhere)
ping -n 4 127.0.0.1 >nul
call "%~dp0run_instance.bat" client 1 - -fortress-bench -fortress-bench-quit 45 -fortress-bench-no-wave %*
endlocal
