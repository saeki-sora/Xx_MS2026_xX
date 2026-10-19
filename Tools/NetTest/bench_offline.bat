@echo off
rem Benchmark without network: 30000 swarm burst, logs every second, quits after 35 s.
rem Log: <build>\logs\p0_ui_bench_offline.log  (lines starting with [Bench])
rem Extra args are passed to the exe (e.g. -fortress-swarm-spawns-per-frame 0).
rem Runs fullscreen at the primary monitor's real resolution (see run_instance.bat; set MS2026_SCREEN_W/H to force one).
setlocal
set MS2026_FULLSCREEN=1
if "%LOGSUFFIX%"=="" set LOGSUFFIX=_bench_offline
call "%~dp0run_instance.bat" ui 0 - -fortress-bench -fortress-bench-burst 30000 -fortress-bench-delay 5 -fortress-bench-quit 35 -fortress-bench-no-wave %*
endlocal
