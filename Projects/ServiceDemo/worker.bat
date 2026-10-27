@echo off
rem Подопытный для ServiceDemo: просто ждёт, пока его не остановят.
:loop
ping -n 11 127.0.0.1 >nul
goto loop
