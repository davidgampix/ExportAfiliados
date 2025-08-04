# Exportador de Datos de Afiliados

Script Python para exportar datos de jugadores de afiliados desde SQL Server a Excel.

## Archivos incluidos

- `main.py` - Script principal
- `requirements.txt` - Dependencias de Python
- `instalar_dependencias.bat` - Instalador automático

## Instalación

1. Ejecutar `instalar_dependencias.bat` para instalar las librerías necesarias

## Uso

```bash
python main.py
```

El programa solicita:
- Nombre del afiliado raíz
- Genera archivo Excel con timestamp: `afiliados_export_{afiliado}_{timestamp}.xlsx`

## Dependencias

- pandas: Manipulación de datos
- pyodbc: Conexión SQL Server
- openpyxl: Generación de Excel

## Funcionalidades

- Conexión segura a SQL Server
- Ejecución del stored procedure `GetHierarchicalPlayersEmailVerified`
- Export a Excel con formato automático
- Manejo de errores y logging
- Opción para abrir archivo generado

## Configuración

La conexión está configurada para:
- Servidor: 54.226.82.137
- Base de datos: SportsBet_Afiliados
- Driver: ODBC Driver 17 for SQL Server
