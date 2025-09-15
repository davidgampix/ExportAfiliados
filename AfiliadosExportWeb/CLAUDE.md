# CLAUDE.md

Este archivo proporciona orientación a Claude Code (claude.ai/code) al trabajar con código en este repositorio.

## Descripción del Sistema

Aplicación web ASP.NET Core 8.0 para exportar datos jerárquicos de jugadores afiliados desde bases de datos SQL Server a archivos Excel. El sistema permite a los usuarios buscar afiliados raíz y exportar toda su red de jugadores con actualización de progreso en tiempo real.

## Comandos de Desarrollo

### Ejecutar la aplicación
```bash
# Modo desarrollo (puerto 5000)
dotnet run --urls "http://localhost:5000"

# Usando archivo batch
./Iniciar_Web.bat
```

### Compilación y publicación
```bash
# Compilar para desarrollo
dotnet build

# Publicar para producción
dotnet publish -c Release

# Restaurar paquetes NuGet
dotnet restore
```

### Migraciones de base de datos (Entity Framework)
```bash
# Agregar nueva migración
dotnet ef migrations add <NombreMigracion>

# Actualizar base de datos
dotnet ef database update
```

## Arquitectura y Funcionamiento

### Flujo de Trabajo Principal

1. **Autenticación**: Usuario ingresa con credenciales (soporte/Export2024!)
2. **Selección de base de datos**: Si hay múltiples operaciones configuradas
3. **Búsqueda de afiliado**: Autocompletado con búsqueda predictiva
4. **Exportación**: Proceso asíncrono con actualización en tiempo real
5. **Descarga**: Archivo Excel generado disponible para descarga

### Componentes Principales

#### Backend - Servicios

**AuthService**
- Autenticación JWT con tokens Bearer
- Credenciales configurables en appsettings.json
- Tokens con expiración de 30 días por defecto
- Generación de claims con rol Admin

**DatabaseService**
- Gestión de múltiples conexiones SQL Server
- Ejecución del stored procedure `[_V2_].[GetHierarchicalPlayersEmailVerified]`
- Búsqueda predictiva de afiliados en tabla `_V2_Agent.HierarchicalUsers`
- Timeout de comandos: 600 segundos para consultas grandes

**ExcelExportService**
- Generación de Excel usando ClosedXML
- Formato profesional con:
  - Encabezados con estilo (negrita, fondo verde)
  - Formato de números y fechas
  - Ajuste automático de columnas
  - Filtros automáticos
- Archivos temporales en carpeta `TempExports`
- Limpieza automática después de 1 hora

**SignalR Hub (ExportHub)**
- Comunicación bidireccional en tiempo real
- Reporta progreso durante:
  - Conexión a base de datos
  - Ejecución de stored procedure
  - Generación de Excel
  - Escritura de datos
- Manejo de errores con mensajes descriptivos

#### Base de Datos

**Dos capas de datos:**

1. **SQL Server (Datos de afiliados)**
   - Múltiples bases de datos configurables (SportsBet, CDL, FormoWin)
   - Stored procedure para obtener jerarquía completa de jugadores
   - Búsqueda de afiliados por username

2. **SQLite Local (Historial y operaciones)**
   - Entidad `Operation`: Configuración de bases de datos
   - Entidad `DownloadHistory`: Registro de descargas
   - Gestión con Entity Framework Core

#### Frontend

**Páginas principales:**
- `login.html`: Autenticación de usuarios
- `index.html`: Interfaz principal de exportación
- `admin.html`: Panel de administración (historial de descargas)

**Tecnologías UI:**
- Tailwind CSS + DaisyUI para diseño responsive
- SignalR JavaScript Client para actualizaciones en tiempo real
- Vanilla JavaScript (sin frameworks adicionales)
- Tema claro/oscuro configurable

### Flujo de Datos Detallado

1. **Proceso de Exportación:**
   ```
   Usuario → SignalR Hub → DatabaseService → SQL Server
                ↓
   Excel ← ExcelExportService ← Datos
   ```

2. **Comunicación en Tiempo Real:**
   - Cliente conecta vía WebSocket/SignalR
   - Hub envía actualizaciones de progreso cada 5% del proceso
   - Cliente muestra barra de progreso y mensajes de estado

3. **Gestión de Archivos:**
   - Excel generado en `TempExports/`
   - Nombre: `afiliados_export_{afiliado}_{timestamp}.xlsx`
   - Descarga vía endpoint `/api/export/download/{fileName}`
   - Eliminación automática post-descarga (5 segundos delay)

### Configuración

#### appsettings.json (ÚNICA fuente de configuración para bases de datos)
```json
{
  "AuthSettings": {
    "Username": "soporte",
    "Password": "Export2024!",
    "JwtSecret": "clave-de-al-menos-32-caracteres",
    "ExpirationDays": 30
  },
  "DatabaseSettings": {
    "Databases": [
      {
        "Id": "sportsbet",
        "Name": "SportsBet Afiliados",
        "Server": "servidor",
        "Database": "base_datos",
        "Username": "usuario",
        "Password": "contraseña",
        "IsDefault": true
      },
      {
        "Id": "sportsbetmza",
        "Name": "SportsBet MZA Afiliados",
        "Server": "servidor",
        "Database": "base_datos_mza",
        "Username": "usuario_mza",
        "Password": "contraseña_mza",
        "IsDefault": false
      }
    ]
  }
}
```

**IMPORTANTE**: Las configuraciones de bases de datos se leen ÚNICAMENTE desde `appsettings.json`. La tabla `Operations` en SQLite existe pero no se utiliza para las exportaciones.

### Seguridad

- **Autenticación JWT**: Todos los endpoints requieren token excepto login
- **SignalR**: Requiere token JWT pasado como query parameter
- **CORS**: Configurado para permitir conexiones SignalR
- **Limpieza de archivos**: Automática para evitar acumulación
- **Validación**: En frontend y backend para afiliados

### Endpoints API

#### Autenticación
- `POST /api/auth/login` - Login de usuario
- `POST /api/auth/refresh` - Renovar token

#### Exportación
- `GET /api/export/databases` - Lista bases de datos disponibles
- `GET /api/export/affiliates/search?term={term}` - Buscar afiliados
- `GET /api/export/download/{fileName}` - Descargar Excel
- `POST /api/export/cleanup` - Limpiar archivos temporales

#### Administración
- `GET /api/admin/operations` - Lista operaciones
- `GET /api/admin/history` - Historial de descargas
- `POST /api/admin/operations` - Crear operación
- `PUT /api/admin/operations/{id}` - Actualizar operación
- `DELETE /api/admin/operations/{id}` - Eliminar operación

### Manejo de Errores

- **SQL Timeout**: Reintentar con timeout mayor o verificar stored procedure
- **SignalR Desconexión**: Reconexión automática con exponential backoff
- **Archivo no encontrado**: Verificar limpieza automática o regenerar
- **Autenticación fallida**: Verificar credenciales en appsettings.json

### Optimizaciones

- **Consultas SQL**: Uso de `WITH(NOLOCK)` para búsquedas
- **Reportes de progreso**: Intervalos del 5% para evitar sobrecarga
- **Generación Excel**: Proceso en chunks para archivos grandes
- **Limpieza**: Background task para eliminar archivos antiguos

## Notas Importantes

- El stored procedure debe existir: `[_V2_].[GetHierarchicalPlayersEmailVerified]`
- La tabla de afiliados debe tener la estructura esperada
- Los archivos Excel se generan con formato XLSX (Office 2007+)
- El sistema soporta múltiples usuarios concurrentes
- Los tokens JWT persisten 30 días por defecto