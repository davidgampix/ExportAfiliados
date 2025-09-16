@echo off
title Generador de Version Portable - Exportador Afiliados
color 0A
cls

echo =========================================================
echo   GENERADOR DE VERSION PORTABLE
echo   Exportador de Afiliados WinForms
echo =========================================================
echo.
echo Este script creara una version portable de la aplicacion
echo que no requiere tener .NET instalado en el equipo destino
echo.
echo =========================================================
echo.

REM Limpiar publicaciones anteriores
echo [1/5] Limpiando compilaciones anteriores...
if exist "bin\Release\net8.0-windows\win-x64\publish" (
    rmdir /s /q "bin\Release\net8.0-windows\win-x64\publish"
)
if exist "Portable" (
    rmdir /s /q "Portable"
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
echo [3/5] Publicando version portable (esto puede tardar varios minutos)...
echo.

REM Publicar como self-contained single file (especificando el proyecto)
dotnet publish AfiliadosExportWin.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o Portable

if errorlevel 1 (
    echo.
    echo ERROR: No se pudo generar la version portable
    pause
    exit /b 1
)

echo.
echo [4/5] Copiando archivo de configuracion...
copy appsettings.json Portable\appsettings.json >nul 2>&1

echo.
echo [5/5] Creando archivo de ejecucion...
echo @echo off > Portable\Ejecutar.bat
echo title Exportador de Afiliados >> Portable\Ejecutar.bat
echo AfiliadosExportWin.exe >> Portable\Ejecutar.bat

echo.
echo =========================================================
echo   PROCESO COMPLETADO EXITOSAMENTE!
echo =========================================================
echo.
echo La version portable se ha creado en:
echo %CD%\Portable
echo.
echo Contenido generado:
echo   - AfiliadosExportWin.exe (aplicacion portable)
echo   - appsettings.json (configuracion)
echo   - Ejecutar.bat (script de inicio)
echo.
echo Tamaño del ejecutable:
for %%A in (Portable\AfiliadosExportWin.exe) do echo   - %%~zA bytes (%%~zA bytes)
echo.
echo Esta carpeta puede copiarse a cualquier PC con Windows x64
echo sin necesidad de instalar .NET Runtime
echo.
echo =========================================================
echo.
echo Presione cualquier tecla para abrir la carpeta Portable...
pause >nul
explorer Portable