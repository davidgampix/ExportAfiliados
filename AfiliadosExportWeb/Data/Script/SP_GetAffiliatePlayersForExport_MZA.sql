-- =============================================
-- Stored Procedure: [_V2_].[GetAffiliatePlayersForExport]
-- Descripción: Obtiene los jugadores de la red jerárquica de un afiliado para exportación
-- Uso:
--   EXEC [_V2_].[GetAffiliatePlayersForExport] 'NombreAfiliado'                                          -- Todos los estados
--   EXEC [_V2_].[GetAffiliatePlayersForExport] 'NombreAfiliado', 'Habilitado'                            -- Solo Habilitados
--   EXEC [_V2_].[GetAffiliatePlayersForExport] 'NombreAfiliado', 'Habilitado,INCOMPLETE_REGISTRATION'    -- Múltiples estados
--   EXEC [_V2_].[GetAffiliatePlayersForExport] 'NombreAfiliado', 'SELF_EXCLUDED'                         -- Solo Autoexcluidos
-- Base de datos: SportsBetMZA_Afiliados
--
-- Parámetros:
--   @rootAffiliate: Nombre del afiliado raíz
--   @statusCodeFilter: Código(s) de estado separados por coma (opcional, vacío = todos)
--                      Valores posibles (se pueden combinar con coma):
--                      '' = Todos los estados
--                      'Habilitado' = Habilitados
--                      'DISABLED' = Deshabilitados
--                      'DELETED' = Eliminados
--                      'LOCKED' = Bloqueados
--                      'SELF_EXCLUDED' = Autoexcluidos
--                      'VOLUNTARY_PAUSE' = Pausa voluntaria
--                      'INCOMPLETE_REGISTRATION' = Registro incompleto
--                      'PENDING_APPROVAL' = Pendiente de aprobación
--                      'REJECTED' = Rechazados
--                      'TOO_MANY_FAILED_ATTEMPTS' = Demasiados intentos fallidos
--
-- Versión: 4.1
-- Fecha de modificación: 2026-01-22
-- Cambios:
--   v4.1 - Soporte para múltiples StatusCodes separados por coma
--   v4.0 - CAMBIO MAYOR: Reemplazado filtro de StatusId por StatusCode usando la misma
--          lógica que el BO Portal (vista vw_PlayerStatusCodes).
--          Ahora el campo [Estado] muestra el estado calculado (Habilitado, Registro incompleto, etc.)
--          Eliminado el filtro de autoexclusión separado (ahora está integrado en StatusCode).
--          Eliminados filtros de IsDeleted e IsEnabled del WHERE (ahora manejados por StatusCode).
--   v3.1 - Agregado: Campo [Habilitado Calculado] con la misma lógica que Player.AdvancedSearch
--   v3.0 - CORRECCIÓN CRÍTICA: Reemplazado algoritmo de navegación por ParentId
--          con HierarchyId.IsDescendantOf() para evitar incluir jugadores de otras ramas.
--   v2.2 - Agregado: Parámetro @selfExclusionFilter para filtrar por autoexclusión
--   v2.1 - Agregado: Parámetro @statusIds para filtrar por estado(s)
--   v2.0 - Agregado: Afiliado Directo, Estado, Booleanos planos, Fecha Último Depósito, UTM
-- =============================================

-- exec [_V2_].[GetAffiliatePlayersForExport] 'Nicolorenzon'
-- exec [_V2_].[GetAffiliatePlayersForExport] 'Nicolorenzon', 'Habilitado'
-- exec [_V2_].[GetAffiliatePlayersForExport] 'Nicolorenzon', 'Habilitado,INCOMPLETE_REGISTRATION'
-- exec [_V2_].[GetAffiliatePlayersForExport] 'Nicolorenzon', 'SELF_EXCLUDED'
-- exec [_V2_].[GetAffiliatePlayersForExport] 'marcelopetraglia', 'INCOMPLETE_REGISTRATION'

CREATE OR ALTER PROCEDURE [_V2_].[GetAffiliatePlayersForExport]
    @rootAffiliate varchar(500),
    @statusCodeFilter varchar(500) = ''    -- Filtro por StatusCode(s) separados por coma (vacío = todos)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RootId int;
    DECLARE @RootHierarchyId hierarchyid;

    -- Obtener ID del afiliado raíz
    SELECT @RootId = Id
    FROM [_V2_Identity].[Users] WITH (NOLOCK)
    WHERE UserName = @rootAffiliate;

    -- Obtener HierarchyId del afiliado raíz
    SELECT @RootHierarchyId = [HierarchyId]
    FROM [_V2_Agent].[HierarchicalUsers] WITH (NOLOCK)
    WHERE AgentUserId = @RootId;

    -- =============================================
    -- Tabla temporal para datos de auditoría y depósitos
    -- =============================================
    CREATE TABLE #PlayerAuditData (
        PlayerId INT PRIMARY KEY,
        LastLoginDate DATETIME,
        RegistrationDate DATETIME,
        LastDepositDate DATETIME
    );

    -- Obtener todos los PlayerIds únicos usando HierarchyId
    DECLARE @PlayerIds TABLE (PlayerId INT PRIMARY KEY);

    INSERT INTO @PlayerIds (PlayerId)
    SELECT DISTINCT h.PlayerUserId
    FROM [_V2_Agent].[HierarchicalUsers] h WITH (NOLOCK)
    INNER JOIN SportsBetMZA_Online.dbo.Vw_Player pd WITH (NOLOCK) ON pd.Id = h.PlayerUserId
    WHERE h.Discriminator = 'PlayerHierarchicalUser'
    AND h.[HierarchyId].IsDescendantOf(@RootHierarchyId) = 1
    AND h.UserId != @RootId;

    -- Insertar último login
    INSERT INTO #PlayerAuditData (PlayerId, LastLoginDate)
    SELECT
        p.PlayerId,
        (SELECT TOP 1 a.InsDate
         FROM [SportsBetMZA_Online].[Audit].[Activity] a WITH (NOLOCK)
         WHERE a.Player_Id = p.PlayerId
         AND a.ActivityType_Id = 1  -- SystemLogin
         ORDER BY a.InsDate DESC)
    FROM @PlayerIds p;

    -- Actualizar con fecha de registro
    UPDATE #PlayerAuditData
    SET RegistrationDate = COALESCE(
        (SELECT TOP 1 a.InsDate
         FROM [SportsBetMZA_Online].[Audit].[Activity] a WITH (NOLOCK)
         WHERE a.Player_Id = #PlayerAuditData.PlayerId
         AND a.ActivityType_Id = 3  -- SystemRegister
         ORDER BY a.InsDate ASC),
        (SELECT u.InsDate
         FROM [SportsBetMZA_Online].[dbo].[User] u WITH (NOLOCK)
         WHERE u.Id = #PlayerAuditData.PlayerId)
    );

    -- Actualizar con fecha de último depósito
    UPDATE #PlayerAuditData
    SET LastDepositDate = (
        SELECT MAX(dn.InsDate)
        FROM [SportsBetMZA_Online].[Player].[DepositNotifications] dn WITH (NOLOCK)
        WHERE dn.PlayerId = #PlayerAuditData.PlayerId
    );

    -- =============================================
    -- QUERY PRINCIPAL
    -- Usa HierarchyId.IsDescendantOf() para navegación correcta de jerarquía
    -- Usa vw_PlayerStatusCodes para obtener el estado calculado (igual que BO Portal)
    -- =============================================
    SELECT
        -- Identificación
        pd.Id,
        pd.DocumentNumber as DNI,
        pd.FirstName as Nombre,
        pd.LastName as Apellido,

        -- Contacto
        pd.Email,
        ISNULL('(' + pd.AreaCode + ') ', '') + pd.Phone as Telefono,
        pd.Address as Direccion,
        pd.City as Localidad,

        -- Afiliado Directo (el afiliado que tiene asignado el jugador)
        affiliate.UserName as [Afiliado Directo],

        -- =============================================
        -- Estado del jugador (usando la misma lógica que BO Portal)
        -- =============================================
        CASE pvs.StatusCode
            WHEN 'Habilitado' THEN 'Habilitado'
            WHEN 'DISABLED' THEN 'Deshabilitado'
            WHEN 'DELETED' THEN 'Eliminado'
            WHEN 'LOCKED' THEN 'Bloqueado'
            WHEN 'SELF_EXCLUDED' THEN 'Autoexcluido'
            WHEN 'VOLUNTARY_PAUSE' THEN 'Pausa voluntaria'
            WHEN 'INCOMPLETE_REGISTRATION' THEN 'Registro incompleto'
            WHEN 'PENDING_APPROVAL' THEN 'Pendiente de aprobación'
            WHEN 'REJECTED' THEN 'Rechazado'
            WHEN 'TOO_MANY_FAILED_ATTEMPTS' THEN 'Demasiados intentos fallidos'
            ELSE pvs.StatusCode
        END as [Estado],

        -- Código de estado (para referencia técnica)
        pvs.StatusCode as [Estado Código],

        -- Booleanos planos (componentes del estado)
        pd.IsEmailVerified as [Email Verificado],
        pd.IsEnabled as [Habilitado DB],
        pd.ReadTermsCond as [Leyó Términos],
        pd.RenaperValidated as [Validado RENAPER],
        pd.CanPlayCasino as [Puede Casino],
        pd.CanPlaySports as [Puede Deportes],
        pd.CanWithdraw as [Puede Retirar],
        pd.IsSuspicious as Sospechoso,
        pd.PoliticallyExposed as PEP,
        pd.IsTestUser as [Usuario Test],

        -- Indicador de depósito como booleano
        CAST(CASE
            WHEN EXISTS (
                SELECT 1
                FROM [SportsBetMZA_Online].[Player].[DepositNotifications] dn WITH (NOLOCK)
                WHERE dn.PlayerId = pd.Id
            ) THEN 1 ELSE 0
        END AS BIT) as [Ha Depositado],

        -- Fechas
        CONVERT(VARCHAR, pad.RegistrationDate, 120) as [Fecha Registro],
        CONVERT(VARCHAR, pad.LastLoginDate, 120) as [Ultimo Login],

        -- Fecha último depósito
        CONVERT(VARCHAR, pad.LastDepositDate, 120) as [Ultimo Deposito],

        -- Datos UTM de marketing
        mkt.UtmSource as [UTM Source],
        mkt.UtmMedium as [UTM Medium],
        mkt.UtmCampaign as [UTM Campaign],
        mkt.UtmContent as [UTM Content],
        mkt.UtmTerm as [UTM Term]

    FROM [_V2_Agent].[HierarchicalUsers] h WITH (NOLOCK)
    INNER JOIN SportsBetMZA_Online.dbo.Vw_Player pd WITH (NOLOCK) ON pd.Id = h.PlayerUserId
    LEFT JOIN #PlayerAuditData pad ON pad.PlayerId = pd.Id
    LEFT JOIN [_V2_Identity].[Users] affiliate WITH (NOLOCK) ON affiliate.Id = h.ParentId
    LEFT JOIN [SportsBetMZA_Online].[Player].[MarketingData] mkt WITH (NOLOCK) ON mkt.PlayerId = pd.Id
    -- JOIN a la vista de estados (misma lógica que BO Portal)
    LEFT JOIN [SportsBetMZA_Online].[Player].[vw_PlayerStatusCodes] pvs WITH (NOLOCK) ON pvs.PlayerId = pd.Id

    WHERE h.Discriminator = 'PlayerHierarchicalUser'
    AND h.[HierarchyId].IsDescendantOf(@RootHierarchyId) = 1
    AND h.UserId != @RootId
    -- Filtro por StatusCode(s) (opcional, soporta múltiples valores separados por coma)
    AND (
        @statusCodeFilter = ''
        OR pvs.StatusCode IN (SELECT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@statusCodeFilter, ','))
        -- Caso especial: SELF_EXCLUDED puede estar en ReasonsDisabledCode
        OR (CHARINDEX('SELF_EXCLUDED', @statusCodeFilter) > 0 AND pvs.ReasonsDisabledCode LIKE '%SELF_EXCLUDED%')
    );

    -- Limpiar tablas temporales
    DROP TABLE #PlayerAuditData;
END
GO
