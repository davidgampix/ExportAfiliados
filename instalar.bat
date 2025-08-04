@echo off
echo === Instalador Exportador de Afiliados ===
echo.

REM Verificar Python
python --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python no instalado
    echo Descargue desde https://python.org
    pause
    exit /b 1
)

echo Instalando dependencias...
pip install pandas==2.1.4 pyodbc==5.0.1 openpyxl==3.1.2 xlsxwriter==3.1.9

if errorlevel 1 (
    echo ERROR: Fallo en instalacion
    pause
    exit /b 1
)

echo.
echo === Instalacion completada ===
echo Use ejecutar.bat para iniciar la aplicacion
pause
