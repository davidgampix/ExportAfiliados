# SportsBet Database Analysis

Análisis de la base de datos SportsBet_Afiliados y SportsBet_Online.

**Servidor:** 54.226.82.137
**Base de datos principal:** SportsBet_Afiliados
**Base de datos online:** SportsBet_Online

---

## Stored Procedure: `[_V2_].[GetHierarchicalPlayersEmailVerified]`

### Parámetro

| Parámetro | Tipo | Descripción |
|-----------|------|-------------|
| `@rootAffiliate` | varchar(500) | Nombre de usuario del afiliado raíz |

### Funcionamiento

1. **Obtención del ID raíz**
   - Busca el ID del afiliado en `[_V2_Identity].[Users]` por `UserName`

2. **Construcción de la jerarquía (recursiva)**
   - Usa tablas temporales `#AffiliateHierarchy` y `#ProcessedUsers`
   - Comienza con sub-afiliados directos del afiliado raíz
   - Itera nivel por nivel (WHILE loop) hasta que no haya más sub-afiliados
   - Solo incluye registros con `Discriminator = 'AffiliateHierarchicalUser'`

3. **Recopilación de datos de auditoría**
   - Crea tabla temporal `#PlayerAuditData` con:
     - **LastLoginDate**: Último login (`ActivityType_Id = 1`)
     - **RegistrationDate**: Fecha de registro (`ActivityType_Id = 3` o `InsDate` del usuario)

4. **Query principal (UNION ALL de 2 partes)**
   - **Parte 1**: Jugadores bajo sub-afiliados de la jerarquía
   - **Parte 2**: Jugadores directos del afiliado raíz

### Columnas retornadas

| Columna | Descripción |
|---------|-------------|
| `Id` | ID del jugador |
| `Nombre` | Nombre del jugador (FirstName) |
| `Apellido` | Apellido del jugador (LastName) |
| `DNI` | Número de documento |
| `Email` | Email del jugador |
| `Telefono` | Teléfono con código de área |
| `Direccion` | Dirección |
| `Localidad` | Ciudad |
| `Afiliado` | BrandAffiliate del jugador |
| `Email Verificado` | Sí/No |
| `Ha realizado depósito` | Sí/No (según tabla `DepositNotifications`) |
| `Fecha de Registro` | Formato YYYY-MM-DD HH:MM:SS |
| `Último Login` | Formato YYYY-MM-DD HH:MM:SS |

### Filtros aplicados

- `IsDeleted = 0` (no eliminados)
- `IsEnabled = 1` (habilitados)
- `Discriminator <> 'AffiliateHierarchicalUser'` (solo jugadores, no afiliados)
- **Excluye jugadores con autoexclusión vigente** (tabla `[Player].[Limit]`)

### Tablas involucradas

| Base de datos | Tabla | Propósito |
|---------------|-------|-----------|
| SportsBet_Afiliados | `[_V2_Identity].[Users]` | Obtener ID del afiliado raíz |
| SportsBet_Afiliados | `[_V2_Agent].[HierarchicalUsers]` | Jerarquía de afiliados y jugadores |
| SportsBet_Online | `[dbo].[Vw_Player]` | Datos de jugadores |
| SportsBet_Online | `[Audit].[Activity]` | Auditoría de login y registro |
| SportsBet_Online | `[Player].[DepositNotifications]` | Verificar si realizó depósito |
| SportsBet_Online | `[Player].[Limit]` | Límites de autoexclusión |
| SportsBet_Online | `[dbo].[User]` | Fecha de creación del usuario |

### Optimizaciones implementadas

- `WITH (NOLOCK)` en todas las consultas
- Índice no-clustered en `#AffiliateHierarchy.Level`
- Tabla `#PlayerAuditData` para evitar subconsultas repetidas
- Procesamiento por niveles para construir la jerarquía

### Ejemplo de uso

```sql
EXEC [_V2_].[GetHierarchicalPlayersEmailVerified] 'Nicolorenzon'
```

---

## Vista: `[dbo].[Vw_Player]`

Vista que consolida datos de jugadores desde múltiples tablas.

### Tablas fuente

| Tabla | Propósito |
|-------|-----------|
| `dbo.[User]` | Datos de usuario base (login, email, estado) |
| `Player.Account` | Cuenta del jugador (balance, moneda, estado) |
| `Player.Data` | Datos personales del jugador |
| `dbo.webpages_Membership` | Contraseña encriptada |
| `PlayerRef.Status` | Catálogo de estados |
| `Player.BrandAffiliate` | Catálogo de afiliados (LEFT JOIN) |

### Campos detallados

#### Identificación y Cuenta

| Campo | Tipo | Nullable | Origen | Descripción |
|-------|------|----------|--------|-------------|
| `Id` | int | NO | Player.Data | ID único del jugador |
| `UserName` | nvarchar(255) | YES | dbo.User | Nombre de usuario para login |
| `Password` | nvarchar(128) | NO | webpages_Membership | Contraseña encriptada |
| `DocumentType` | varchar(3) | NO | Player.Data | Tipo de documento (DNI, etc.) |
| `DocumentNumber` | varchar(20) | NO | Player.Data | Número de documento |
| `IdentificationNumber` | bigint | YES | Player.Data | Número de identificación alternativo |
| `Cuil` | varchar(15) | YES | Player.Data | CUIL/CUIT argentino |

#### Datos Personales

| Campo | Tipo | Nullable | Origen | Descripción |
|-------|------|----------|--------|-------------|
| `FirstName` | nvarchar(510) | YES | Player.Data | Nombre |
| `LastName` | nvarchar(510) | YES | Player.Data | Apellido |
| `Email` | nvarchar(100) | YES | dbo.User | Correo electrónico |
| `Phone` | varchar(20) | YES | Player.Data | Teléfono |
| `AreaCode` | varchar(11) | YES | Player.Data | Código de área telefónico |
| `Birthday` | date | YES | Player.Data | Fecha de nacimiento |
| `Gender` | char(1) | YES | Player.Data | Género (M/F) |
| `MaritalStatus` | char(1) | YES | Player.Data | Estado civil |

#### Ubicación

| Campo | Tipo | Nullable | Origen | Descripción |
|-------|------|----------|--------|-------------|
| `Address` | nvarchar(2000) | YES | Player.Data | Dirección completa |
| `City` | varchar(100) | YES | Player.Data | Ciudad/Localidad |
| `PostalCode` | varchar(10) | YES | Player.Data | Código postal |
| `Country` | nvarchar(255) | YES | Player.Data | País de residencia |
| `CountryBirth` | nvarchar(255) | YES | Player.Data | País de nacimiento |
| `BirthPlace` | varchar(100) | YES | Player.Data | Lugar de nacimiento |

#### Cuenta y Balance

| Campo | Tipo | Nullable | Origen | Descripción |
|-------|------|----------|--------|-------------|
| `CurrencyCode` | char(3) | YES | Player.Account | Moneda (ARS, USD, etc.) |
| `CasinoBalance` | decimal | NO | Player.Account | Balance actual en casino |
| `StatusId` | int | NO | Player.Account | ID de estado de cuenta |
| `StatusName` | nvarchar(50) | NO | PlayerRef.Status | Nombre del estado |

#### Permisos de Juego

| Campo | Tipo | Nullable | Origen | Descripción |
|-------|------|----------|--------|-------------|
| `CanPlayPoker` | bit | NO | Player.Data | Puede jugar poker |
| `CanPlaySports` | bit | NO | Player.Data | Puede apostar deportes |
| `CanPlayCasinoLive` | bit | NO | Player.Data | Puede jugar casino en vivo |
| `CanPlayCasino` | bit | NO | Player.Data | Puede jugar casino |
| `CanWithdraw` | bit | YES | Player.Data | Puede retirar fondos |

#### Estado del Usuario

| Campo | Tipo | Nullable | Origen | Descripción |
|-------|------|----------|--------|-------------|
| `IsEnabled` | bit | NO | dbo.User | Usuario habilitado |
| `IsDeleted` | bit | NO | dbo.User | Usuario eliminado (soft delete) |
| `IsEmailVerified` | bit | NO | Player.Data | Email verificado |
| `IsFirstLogin` | bit | NO | Player.Data | Es primer inicio de sesión |
| `IsTestUser` | bit | NO | Player.Account | Usuario de prueba |
| `IsSuspicious` | bit | NO | Player.Data | Marcado como sospechoso |
| `ShouldChangePasswordOnNextLogin` | bit | NO | Player.Data | Debe cambiar contraseña |

#### Poker

| Campo | Tipo | Nullable | Origen | Descripción |
|-------|------|----------|--------|-------------|
| `PokerNickName` | nvarchar(100) | YES | Player.Data | Apodo en poker |
| `IsPokerNickNameOk` | bit | YES | Player.Data | Apodo de poker aprobado |

#### Compliance / PLAFT / UIF

| Campo | Tipo | Nullable | Origen | Descripción |
|-------|------|----------|--------|-------------|
| `PoliticallyExposed` | bit | YES | Player.Data | Persona políticamente expuesta |
| `PoliticallyExposedNosis` | bit | YES | Player.Data | PEP según NOSIS |
| `ObligatedSubjectNosis` | bit | YES | Player.Data | Sujeto obligado según NOSIS |
| `ObligatedSubjectUif` | bit | YES | Player.Data | Sujeto obligado UIF |
| `NumberHomonymsNosis` | int | YES | Player.Data | Cantidad de homónimos en NOSIS |
| `RenaperValidated` | bit | YES | Player.Data | Validado en RENAPER |
| `PlaftRepet` | bit | YES | Player.Data | Registrado en PLAFT/REPET |
| `AvoidWithdrawFraudCheck` | bit | NO | Player.Account | Omitir verificación de fraude en retiros |

#### Otros

| Campo | Tipo | Nullable | Origen | Descripción |
|-------|------|----------|--------|-------------|
| `BrandAffiliate` | nvarchar(50) | YES | Player.BrandAffiliate | Nombre del afiliado de marca |
| `Source` | varchar(40) | NO | Player.Data | Origen/fuente del registro |
| `Language` | nvarchar(50) | YES | Player.Data | Idioma preferido |
| `StartPage_Id` | int | YES | Player.Data | Página de inicio preferida |
| `InsDate` | datetime | NO | dbo.User | Fecha de creación del usuario |
| `ReadTermsCond` | bit | YES | Player.Data | Leyó términos y condiciones |
| `Occupation` | varchar(100) | YES | Player.Data | Ocupación laboral |
| `OriginMoneyFunds` | varchar(60) | YES | Player.Data | Origen de los fondos |

### Historial de cambios

| Fecha | Autor | Cambio |
|-------|-------|--------|
| 2023-09-05 | David Caceres | Campo `AvoidWithdrawFraudCheck` |
| 2023-09-07 | Pablo B | Campos `PoliticallyExposedNosis`, `ObligatedSubjectNosis` |
| 2024-03-12 | Pablo B | Campo `ReadTermsCond` |
| 2024-05-02 | Pablo B | Campo `CanWithdraw` |
| 2024-05-06 | Pablo B | Campos `Gender`, `Country` |
| 2024-05-07 | Fede | Campo `IsSuspicious` |
| 2024-08-23 | Pablo B | Campos `CountryBirth`, `Occupation` |
| 2024-08-28 | David Caceres | Campos `PlaftRepet`, `ObligatedSubjectUif` |
| 2024-12-06 | Maciel | Campos `AreaCode`, `OriginMoneyFunds`, `MaritalStatus` |
| 2025-03-26 | EzequielG | Campo `PoliticallyExposed` |
| 2025-03-31 | EzequielG | Campo `BirthPlace` |

---

## Relaciones entre tablas

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         SportsBet_Afiliados                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│  [_V2_Identity].[Users]  ◄──────┐                                           │
│       │                         │                                           │
│       │ (UserName)              │ (ParentId/AgentUserId)                    │
│       ▼                         │                                           │
│  [_V2_Agent].[HierarchicalUsers] ────────────────────────────────────────┐  │
│       │                                                                   │  │
│       │ (PlayerUserId)                                                    │  │
└───────┼───────────────────────────────────────────────────────────────────┼──┘
        │                                                                   │
        ▼                                                                   │
┌─────────────────────────────────────────────────────────────────────────────┐
│                          SportsBet_Online                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│  [dbo].[Vw_Player] ◄─────────────────────────────────────────────────────┘  │
│       │                                                                      │
│       ├──► [dbo].[User]                                                      │
│       ├──► [Player].[Account]                                                │
│       ├──► [Player].[Data]                                                   │
│       ├──► [dbo].[webpages_Membership]                                       │
│       ├──► [PlayerRef].[Status]                                              │
│       └──► [Player].[BrandAffiliate]                                         │
│                                                                              │
│  [Audit].[Activity] ─────► Login/Registro timestamps                         │
│  [Player].[DepositNotifications] ─────► Verificar depósitos                  │
│  [Player].[Limit] ─────► Autoexclusiones                                     │
└─────────────────────────────────────────────────────────────────────────────┘
```
