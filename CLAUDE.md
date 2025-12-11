# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

Multi-project repository containing three implementations of an affiliate data exporter that retrieves hierarchical player data from SQL Server and exports it to Excel:

1. **Python Script** (legacy): Simple command-line exporter
2. **AfiliadosExportWeb**: ASP.NET Core 8.0 web application with real-time SignalR updates
3. **AfiliadosExportWin**: Windows Forms desktop application (.NET 8.0)

All implementations connect to the same SQL Server databases and execute the stored procedure `[_V2_].[GetHierarchicalPlayersEmailVerified]`.

## Common Development Commands

### Web Application (AfiliadosExportWeb)

```bash
# Navigate to web project
cd AfiliadosExportWeb

# Restore NuGet packages
dotnet restore

# Run in development mode (port 5000)
dotnet run --urls "http://localhost:5000"

# Alternative: use batch file
./Iniciar_Web.bat

# Build for development
dotnet build

# Publish for production
dotnet publish -c Release

# Entity Framework migrations
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

### Windows Forms Application (AfiliadosExportWin)

```bash
# Navigate to WinForms project
cd AfiliadosExportWin/AfiliadosExportWin

# Restore NuGet packages
dotnet restore

# Run application
dotnet run

# Build for development
dotnet build

# Publish as portable executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Python Script (Legacy)

```bash
# Install dependencies
pip install -r requirements.txt
# Or use: instalar.bat

# Run exporter
python exportador.py
# Or use: ejecutar.bat
```

## Architecture Overview

### Shared Components

All three implementations share:
- **Database**: SQL Server with databases for different operations (SportsBet, SportsBetMZA)
- **Stored Procedure**: `[_V2_].[GetHierarchicalPlayersEmailVerified]` - retrieves hierarchical player data
- **Excel Generation**: Export to `.xlsx` format with formatted headers and auto-sizing
- **Configuration**: Database connections configured in `appsettings.json`

### Web Application Architecture

**Backend Services:**
- `AuthService`: JWT authentication with 30-day tokens, credentials in appsettings.json
- `DatabaseService`: Multi-database SQL Server connection management, executes stored procedures, predictive affiliate search
- `ExcelExportService`: Excel generation using ClosedXML with professional formatting
- `OperationService`: Manages database configurations (stored in SQLite, not used for exports)
- `HistoryService`: Tracks download history in SQLite

**SignalR Hub (`ExportHub`):**
- Real-time bidirectional communication for progress updates
- Reports progress during: DB connection, SP execution, Excel generation, file writing
- Requires JWT authentication via query parameter

**Data Layers:**
1. **SQL Server** (affiliate data): Multiple databases configured in appsettings.json
2. **SQLite Local** (app data): `Operation` and `DownloadHistory` entities managed by Entity Framework Core

**Frontend:**
- Pages: `login.html`, `index.html` (main export), `admin.html` (download history)
- Tech: Tailwind CSS + DaisyUI, SignalR JavaScript Client, vanilla JavaScript
- Features: Real-time progress bar, theme switching, autocomplete affiliate search

**Authentication Flow:**
1. User logs in with credentials (username: soporte, password: Export2024!)
2. Receives JWT token valid for 30 days
3. Token required for all API calls and SignalR connections

**Export Flow:**
```
User → SignalR Hub → DatabaseService → SQL Server
             ↓
Excel ← ExcelExportService ← Data
```

**File Management:**
- Excel files generated in `TempExports/` folder
- Naming: `afiliados_export_{affiliate}_{timestamp}.xlsx`
- Auto-cleanup after 1 hour
- Download via `/api/export/download/{fileName}`
- Automatic deletion 5 seconds post-download

### Windows Forms Architecture

**Services:**
- `DatabaseService`: Connects to multiple SQL Server databases from appsettings.json
- `ExcelExportService`: Generates Excel files with ClosedXML

**Features:**
- Database selector dropdown
- Affiliate input with validation
- Progress bar with real-time updates
- Export cancellation support via `CancellationToken`
- Auto-open generated file option
- Activity log display

**Export Flow:**
- User selects database and enters affiliate name
- Click "Exportar" initiates async export with cancellation support
- Progress updates shown in real-time
- File saved to user-selected location

### Python Script Architecture (Legacy)

Simple single-file script with:
- Direct pyodbc connection to SQL Server
- pandas for data manipulation
- openpyxl for Excel generation
- Terminal-based progress indicator with colored output
- Hardcoded connection to SportsBet_Afiliados database

## Configuration

### Database Configuration (Web & WinForms)

**CRITICAL**: Database connections are configured ONLY in `appsettings.json`. The SQLite `Operations` table exists but is NOT used for exports.

```json
{
  "DatabaseSettings": {
    "Databases": [
      {
        "Id": "sportsbet",
        "Name": "SportsBet Afiliados",
        "Server": "54.226.82.137",
        "Database": "SportsBet_Afiliados",
        "Username": "SportsBetLogin",
        "Password": "password",
        "IsDefault": true
      }
    ]
  }
}
```

### Authentication Configuration (Web Only)

```json
{
  "AuthSettings": {
    "Username": "soporte",
    "Password": "Export2024!",
    "JwtSecret": "minimum-32-character-secret",
    "ExpirationDays": 30
  }
}
```

## Key API Endpoints (Web)

### Authentication
- `POST /api/auth/login` - User login
- `GET /api/auth/validate` - Validate JWT token

### Export
- `GET /api/export/databases` - List available databases
- `GET /api/export/affiliates/search?term={term}&databaseId={id}` - Search affiliates (autocomplete)
- `GET /api/export/download/{fileName}` - Download generated Excel file
- `POST /api/export/cleanup` - Manually clean temp files

### Admin
- `GET /api/admin/history` - View download history (last 100 records)
- `DELETE /api/admin/history/{id}` - Soft delete history record
- `DELETE /api/admin/history/{id}/permanent` - Permanently delete history record and file
- `GET /api/admin/statistics` - Get download statistics (totals, top affiliates, downloads by operation)
- `GET /api/admin/operations` - List operations (SQLite, not used for exports)
- `POST /api/admin/operations` - Create operation
- `PUT /api/admin/operations/{id}` - Update operation
- `DELETE /api/admin/operations/{id}` - Delete operation

### SignalR Hub
- `/exportHub` - WebSocket endpoint for real-time export progress (requires JWT via `?access_token=`)

## Database Schema

### SQL Server (External)

> **Documentación detallada:** Ver [docs/database_analysis/SportsBet_Database_Analysis.md](docs/database_analysis/SportsBet_Database_Analysis.md) para análisis completo del stored procedure y vista Vw_Player.

**Required stored procedure:**
```sql
[_V2_].[GetHierarchicalPlayersEmailVerified] @rootAffiliate
```
Returns hierarchical player data for affiliate tree. Builds hierarchy recursively, retrieves player data from `Vw_Player`, login/registration dates from audit tables, and filters out deleted/disabled users and those with active self-exclusion.

**Required table for search:**
```sql
_V2_Agent.HierarchicalUsers
```
Contains affiliate usernames for autocomplete. Filtered by `Discriminator = 'AffiliateHierarchicalUser'`.

### SQLite (Web App Only)

**Operation Table:**
- Fields: Id, Code (unique), Name, ConnectionString, Server, Database, IsActive, IsDefault, CreatedAt, UpdatedAt
- Purpose: Display in admin panel (NOT used for actual exports)
- Soft delete via IsActive flag

**DownloadHistory Table:**
- Fields: Id, AffiliateCode, Username, OperationId (FK), FileName, FilePath, FileSizeBytes, RecordCount, DownloadedAt, ProcessingTime, IsDeleted, DeletedAt, DeletedBy
- Purpose: Track export history with full audit trail
- Supports soft delete (IsDeleted flag) and permanent delete

## Common Issues & Solutions

### SQL Timeout
- Connection timeout: 600 seconds in connection string
- Command timeout: Set to 0 (unlimited) when executing stored procedure
- Increase connection timeout in `DatabaseService.cs` if needed: `Connection Timeout=900;`

### SignalR Connection Issues
- Verify JWT token is passed correctly: `/exportHub?access_token={token}`
- Check CORS configuration allows SignalR
- Verify WebSockets are enabled

### File Not Found After Export
- Check `TempExports/` folder exists
- Verify auto-cleanup hasn't removed file (1 hour expiration)
- Regenerate export if needed

### Authentication Failures (Web)
- Verify credentials in `appsettings.json` under `AuthSettings`
- Check JWT secret is at least 32 characters
- Ensure token hasn't expired (default 30 days)

### WinForms Deployment
- Portable version: Use `dotnet publish` with `--self-contained true`
- Ensure `appsettings.json` is copied to output directory
- Check ODBC Driver 17 for SQL Server is installed on target machine

## Development Notes

- **SQL queries** use `WITH(NOLOCK)` for performance during searches
- **Progress reporting** happens at 5% intervals to avoid overhead
- **Excel generation** processes data in chunks for large datasets
- **Excel row limit**: Max 1,048,575 rows per sheet; auto-creates multiple sheets if exceeded
- **Background cleanup** task runs automatically in web app (files older than 1 hour)
- **Multi-tenant support** via multiple database configurations
- **Cancellation support** in both Web (SignalR) and WinForms via `CancellationToken`
- **Random wait messages** displayed every 5 seconds during long exports for better UX
- **Affiliate search** returns TOP 20 results filtered by discriminator 'AffiliateHierarchicalUser'

## SignalR Export States

The ExportHub reports the following states during export:
1. `connecting` (0%) - Connecting to database
2. `executing` (10%) - Executing stored procedure
3. `data_loaded` (50%) - Data retrieved from SQL Server
4. `generating_excel` (60%) - Starting Excel generation
5. `writing_excel` (60-95%) - Writing data to Excel (progress updates every 5%)
6. `saving_excel` (95%) - Saving file to disk
7. `completed` (100%) - Export finished successfully
8. `cancelled` - Export was cancelled by user
9. `error` - Export failed

## Security Considerations

- JWT tokens authenticate all web API endpoints except login
- SignalR requires JWT authentication via query parameter
- CORS configured to allow SignalR connections
- Temp files auto-deleted to prevent data accumulation (1 hour + 5 seconds post-download)
- Frontend and backend validation for affiliate inputs
- Token storage: localStorage (persistent) or sessionStorage (session only) based on "Remember me" checkbox
- JWT claims include: Name, Role (Admin), LoginTime

## NuGet Dependencies (Web)

- `Microsoft.AspNetCore.SignalR` - Real-time communication
- `Microsoft.Data.SqlClient` - SQL Server connectivity
- `ClosedXML` - Excel generation
- `Dapper` - Lightweight ORM for SQL queries
- `Microsoft.AspNetCore.Authentication.JwtBearer` - JWT authentication
- `Microsoft.EntityFrameworkCore.Sqlite` - Local SQLite storage
- `Swashbuckle.AspNetCore` - Swagger API documentation

## Procedimiento para agregar campos nuevos

Cuando el usuario solicite agregar un campo nuevo a la exportación, seguir estos pasos:

### 1. Verificar disponibilidad del campo

Consultar el archivo [docs/database_analysis/SportsBet_Database_Analysis.md](docs/database_analysis/SportsBet_Database_Analysis.md) para verificar:
- Si el campo existe en la vista `[dbo].[Vw_Player]`
- El tipo de dato del campo
- Si es nullable
- La tabla de origen

**Si el campo NO existe o la descripción no coincide con lo solicitado:**

1. Preguntar al usuario: "¿De qué tabla se obtiene este campo?"
2. Una vez que el usuario proporcione la tabla/origen:
   - Agregar el campo al stored procedure con el JOIN necesario
   - **IMPORTANTE:** Documentar el nuevo campo/tabla en `docs/database_analysis/SportsBet_Database_Analysis.md`:
     - Si es una tabla nueva, agregarla en la sección "Tablas involucradas"
     - Si es un campo nuevo de una tabla existente, agregarlo en la sección correspondiente
     - Incluir: nombre del campo, tipo de dato, tabla de origen y descripción

### 2. Modificar el Stored Procedure

Una vez confirmado que el campo existe:
1. Editar el SP en `AfiliadosExportWeb/Data/Script/SP_GetAffiliatePlayersForExport.sql`
2. Agregar el campo en el SELECT de ambas partes del UNION ALL
3. Ejecutar el SP en SQL Server para aplicar cambios

### 3. Preguntar sobre filtros

**IMPORTANTE:** Después de agregar el campo al SP, preguntar al usuario:

> "¿Necesitas que este campo también funcione como filtro en el frontend?
> Si es así, deberé:
> - Agregar un parámetro de entrada al stored procedure
> - Agregar el control de filtro en el frontend (debajo de los filtros existentes)
> - Actualizar el backend para pasar el parámetro"

### 4. Si requiere filtro, implementar según tipo de dato

**Para campos booleanos (bit):**
- Agregar select con opciones: "Todos", "Sí", "No"
- Ejemplo: `@filterFieldName INT` (0=No, 1=Sí, 2=Todos)

**Para campos de texto/catálogo:**
- Multi-select con checkboxes y chips (como el filtro de estados)
- Ejemplo: `@fieldIds VARCHAR(100)` (IDs separados por coma)

**Para campos numéricos/rangos:**
- Inputs de rango (mínimo/máximo)
- Ejemplo: `@minValue DECIMAL, @maxValue DECIMAL`

**Para campos de fecha:**
- Date pickers de rango (desde/hasta)
- Ejemplo: `@dateFrom DATE, @dateTo DATE`

### 5. Archivos a modificar para filtros

| Archivo | Cambio |
|---------|--------|
| `SP_GetAffiliatePlayersForExport.sql` | Agregar parámetro y lógica WHERE |
| `Models/ExportRequest.cs` | Agregar propiedad para el filtro |
| `Services/DatabaseService.cs` | Pasar parámetro al SP |
| `Hubs/ExportHub.cs` | Pasar parámetro desde request |
| `wwwroot/index.html` | Agregar control UI en `#filtersSection` |
| `wwwroot/js/app.js` | Manejar el nuevo filtro en `startExport()` |

### 6. Ubicación de filtros en el frontend

Los filtros se agregan dentro del div `#filtersSection` en `index.html`:
- Este div aparece solo cuando se selecciona un afiliado
- Usar grid de 2 columnas (`grid-cols-1 md:grid-cols-2`)
- Seguir el patrón de los filtros existentes (Estado, Autoexclusión)
