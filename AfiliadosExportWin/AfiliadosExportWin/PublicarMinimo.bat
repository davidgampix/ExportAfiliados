@echo off
title Generador de Version Minima - Exportador Afiliados
color 0A
cls

echo =========================================================
echo   GENERADOR DE VERSION MINIMA (Framework-Dependent)
echo =========================================================
echo.
echo IMPORTANTE: Esta version requiere .NET 8 Desktop Runtime
echo instalado en el equipo destino (solo ~3MB)
echo.
echo Descargar Runtime de:
echo https://dotnet.microsoft.com/download/dotnet/8.0
echo.
echo =========================================================
echo.

REM Limpiar publicaciones anteriores
echo [1/4] Limpiando compilaciones anteriores...
if exist "Minimo" (
    rmdir /s /q "Minimo"
)

echo.
echo [2/4] Publicando version minima...
echo.

REM Publicar framework-dependent (requiere .NET instalado pero pesa mucho menos)
dotnet publish AfiliadosExportWin.csproj -c Release ^
    -r win-x64 ^
    --no-self-contained ^
    -p:PublishSingleFile=false ^
    -p:PublishReadyToRun=true ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    -o Minimo

if errorlevel 1 (
    echo.
    echo ERROR: No se pudo generar la version minima
    pause
    exit /b 1
)

echo.
echo [3/4] Copiando archivo de configuracion...
copy appsettings.json Minimo\appsettings.json >nul 2>&1

echo.
echo [4/4] Creando archivos adicionales...

REM Crear archivo de ejecución
echo @echo off > Minimo\Ejecutar.bat
echo AfiliadosExportWin.exe >> Minimo\Ejecutar.bat

REM Crear instrucciones
echo IMPORTANTE - LEER ANTES DE EJECUTAR > Minimo\IMPORTANTE.txt
echo ==================================== >> Minimo\IMPORTANTE.txt
echo. >> Minimo\IMPORTANTE.txt
echo Esta version requiere .NET 8 Desktop Runtime instalado >> Minimo\IMPORTANTE.txt
echo. >> Minimo\IMPORTANTE.txt
echo Si la aplicacion no inicia, descargue e instale: >> Minimo\IMPORTANTE.txt
echo https://dotnet.microsoft.com/download/dotnet/8.0 >> Minimo\IMPORTANTE.txt
echo. >> Minimo\IMPORTANTE.txt
echo Seleccione: .NET Desktop Runtime 8.0.x (Windows x64) >> Minimo\IMPORTANTE.txt

echo.
echo =========================================================
echo   PROCESO COMPLETADO!
echo =========================================================
echo.
echo Version minima creada en: %CD%\Minimo
echo.

REM Calcular tamaño total
set totalsize=0
for /r Minimo %%f in (*) do set /a totalsize+=%%~zf
set /a totalsize_mb=%totalsize%/1024/1024

echo Tamano total: Aprox %totalsize_mb% MB
echo.
echo NOTA: Esta version requiere .NET 8 Desktop Runtime
echo       instalado en el equipo destino
echo.
echo =========================================================
echo.
pause
explorer Minimo