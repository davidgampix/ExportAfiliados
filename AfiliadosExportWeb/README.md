# Exportador de Afiliados - Aplicación Web

Aplicación web ASP.NET Core para exportar datos de jugadores afiliados desde SQL Server a Excel con progreso en tiempo real.

## Características

- ✅ Interfaz web moderna con Tailwind CSS y DaisyUI
- ✅ Comunicación en tiempo real con SignalR
- ✅ Procesamiento asíncrono con indicadores de progreso
- ✅ Soporte para múltiples bases de datos (configurable)
- ✅ Generación de Excel con formato profesional
- ✅ Descarga directa del archivo generado
- ✅ Limpieza automática de archivos temporales
- ✅ Tema claro/oscuro

## Requisitos

- .NET 8.0 SDK o superior
- SQL Server con acceso a la base de datos de afiliados
- ODBC Driver 17 for SQL Server (o configurar otro driver)

## Instalación

1. Clonar o descargar el proyecto

2. Restaurar paquetes NuGet:
```bash
dotnet restore
```

3. Configurar la base de datos en `appsettings.json`:
```json
{
  "DatabaseSettings": {
    "Databases": [
      {
        "Id": "sportsbet",
        "Name": "SportsBet Afiliados",
        "Server": "tu-servidor",
        "Database": "tu-base-datos",
        "Username": "tu-usuario",
        "Password": "tu-contraseña",
        "IsDefault": true
      }
    ]
  }
}
```

## Ejecución

### Modo desarrollo:
```bash
dotnet run
```

La aplicación estará disponible en:
- http://localhost:5000
- https://localhost:5001 (con HTTPS)

### Modo producción:
```bash
dotnet publish -c Release
```

## Uso

1. Abrir la aplicación en el navegador
2. Seleccionar la base de datos (si hay múltiples configuradas)
3. Ingresar el nombre del afiliado raíz
4. Click en "Iniciar Exportación"
5. Ver el progreso en tiempo real
6. Descargar el archivo Excel generado

## Arquitectura

### Backend (C#)
- **Controllers**: API REST endpoints
- **Hubs**: SignalR para comunicación en tiempo real
- **Services**: 
  - `DatabaseService`: Conexión y consultas SQL
  - `ExcelExportService`: Generación de archivos Excel
- **Models**: DTOs y configuraciones

### Frontend (JavaScript)
- **SignalR Client**: Recepción de actualizaciones en tiempo real
- **Tailwind CSS + DaisyUI**: Diseño responsive y moderno
- **Vanilla JavaScript**: Sin dependencias adicionales

## Tecnologías

- **ASP.NET Core 8.0**: Framework web
- **SignalR**: Comunicación en tiempo real
- **Dapper**: Micro ORM para SQL
- **ClosedXML**: Generación de Excel
- **Tailwind CSS**: Framework CSS
- **DaisyUI**: Componentes UI

## Configuración adicional

### Agregar más bases de datos

Editar `appsettings.json`:
```json
{
  "DatabaseSettings": {
    "Databases": [
      {
        "Id": "sportsbet",
        "Name": "SportsBet Afiliados",
        "Server": "servidor1",
        "Database": "db1",
        "Username": "user1",
        "Password": "pass1",
        "IsDefault": true
      },
      {
        "Id": "otherdb",
        "Name": "Otra Base de Datos",
        "Server": "servidor2",
        "Database": "db2",
        "Username": "user2",
        "Password": "pass2",
        "IsDefault": false
      }
    ]
  }
}
```

### Timeout de comandos SQL

Modificar en `DatabaseConfig.cs`:
```csharp
public string GetConnectionString()
{
    return $"...Connection Timeout=30;Command Timeout=600;";
}
```

## Seguridad

⚠️ **Importante**: 
- No exponer credenciales en producción
- Usar Azure Key Vault o similares para secretos
- Configurar HTTPS en producción
- Implementar autenticación si es necesario

## Solución de problemas

### Error de conexión SQL
- Verificar credenciales en `appsettings.json`
- Verificar conectividad con el servidor SQL
- Verificar que el ODBC Driver esté instalado

### Error de SignalR
- Verificar que WebSockets esté habilitado
- Revisar configuración de CORS si es necesario
- Verificar logs en la consola del navegador

### Archivos temporales
- Los archivos se eliminan automáticamente después de 1 hora
- Limpiar manualmente: `POST /api/export/cleanup`

## Licencia

Proyecto interno - Uso privado