@echo off
title Exportador de Afiliados Web
color 0A
cls
echo ========================================
echo   EXPORTADOR DE AFILIADOS WEB
echo ========================================
echo.
echo Usuario: soporte
echo Password: Export2024!
echo.
echo ----------------------------------------
echo.
echo Iniciando aplicacion web...
echo.
cd /d "%~dp0"
dotnet run --urls "http://localhost:5000"
pause