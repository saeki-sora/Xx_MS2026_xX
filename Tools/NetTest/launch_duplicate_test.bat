@echo off
rem Approval check: joins as P2 again while launch_2p.bat is running.
rem Expected: this window shows "Player 2 is already connected" as the disconnect reason.
set LOGSUFFIX=_dup
call "%~dp0run_instance.bat" client 1
