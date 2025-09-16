@echo off
title Generador de Version Portable Optimizada - Exportador Afiliados
color 0A
cls

echo =========================================================
echo   GENERADOR DE VERSION PORTABLE OPTIMIZADA
echo   Exportador de Afiliados WinForms
echo =========================================================
echo.
echo Este script creara una version portable optimizada:
echo   - Archivo unico ejecutable
echo   - Inicio mas rapido (ReadyToRun)
echo   - Tamano reducido (Trimming)
echo   - No requiere .NET instalado
echo.
echo =========================================================
echo.

REM Limpiar publicaciones anteriores
echo [1/5] Limpiando compilaciones anteriores...
if exist "PortableOptimizado" (
    rmdir /s /q "PortableOptimizado"
)

echo.
echo [2/5] Compilando aplicacion en modo Release...
dotnet build -c Release
if errorlevel 1 (
    echo.
    echo ERROR: No se pudo compilar la aplicacion
    pause
    exit /b 1
)

echo.
echo [3/5] Publicando version portable optimizada...
echo       (Esto puede tardar 2-3 minutos)
echo.

REM Publicar con optimizaciones pero sin trimming (evita errores)
dotnet publish AfiliadosExportWin.csproj -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:PublishReadyToRun=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    -o PortableOptimizado

if errorlevel 1 (
    echo.
    echo ERROR: No se pudo generar la version portable
    pause
    exit /b 1
)

echo.
echo [4/5] Copiando archivo de configuracion...
copy appsettings.json PortableOptimizado\appsettings.json >nul 2>&1

echo.
echo [5/5] Creando archivos adicionales...

REM Crear archivo de ejecución
echo @echo off > PortableOptimizado\Ejecutar.bat
echo title Exportador de Afiliados >> PortableOptimizado\Ejecutar.bat
echo start AfiliadosExportWin.exe >> PortableOptimizado\Ejecutar.bat

REM Crear README
echo Exportador de Afiliados - Version Portable > PortableOptimizado\LEEME.txt
echo ========================================== >> PortableOptimizado\LEEME.txt
echo. >> PortableOptimizado\LEEME.txt
echo Requisitos: >> PortableOptimizado\LEEME.txt
echo - Windows 10/11 x64 >> PortableOptimizado\LEEME.txt
echo - NO requiere .NET instalado >> PortableOptimizado\LEEME.txt
echo. >> PortableOptimizado\LEEME.txt
echo Como usar: >> PortableOptimizado\LEEME.txt
echo 1. Ejecutar "Ejecutar.bat" o "AfiliadosExportWin.exe" >> PortableOptimizado\LEEME.txt
echo 2. La configuracion esta en "appsettings.json" >> PortableOptimizado\LEEME.txt
echo 3. Los archivos Excel se guardan en Documentos\ExportacionesAfiliados >> PortableOptimizado\LEEME.txt
echo. >> PortableOptimizado\LEEME.txt
echo Nota: La primera vez puede tardar unos segundos en iniciar >> PortableOptimizado\LEEME.txt
echo       mientras se descomprime el ejecutable. >> PortableOptimizado\LEEME.txt

echo.
echo =========================================================
echo   PROCESO COMPLETADO EXITOSAMENTE!
echo =========================================================
echo.
echo La version portable optimizada se ha creado en:
echo %CD%\PortableOptimizado
echo.

REM Mostrar información del archivo
for %%A in (PortableOptimizado\AfiliadosExportWin.exe) do (
    set /a size=%%~zA/1024/1024
    echo Tamano del ejecutable: %%~zA bytes (aprox !size! MB^)
)

echo.
echo Archivos generados:
dir /b PortableOptimizado
echo.
echo Esta carpeta puede copiarse a cualquier PC con Windows x64
echo sin necesidad de instalar .NET Runtime
echo.
echo =========================================================
echo.
echo Presione cualquier tecla para abrir la carpeta...
pause >nul
explorer PortableOptimizado