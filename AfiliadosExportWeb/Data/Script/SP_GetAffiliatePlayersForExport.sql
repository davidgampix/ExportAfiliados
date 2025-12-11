-- =============================================
-- Stored Procedure: [_V2_].[GetAffiliatePlayersForExport]
-- Descripción: Obtiene los jugadores de la red jerárquica de un afiliado para exportación
-- Uso:
--   EXEC [_V2_].[GetAffiliatePlayersForExport] 'NombreAfiliado'                    -- Todos los estados, sin autoexcluidos
--   EXEC [_V2_].[GetAffiliatePlayersForExport] 'NombreAfiliado', '1'               -- Solo Aprobados, sin autoexcluidos
--   EXEC [_V2_].[GetAffiliatePlayersForExport] 'NombreAfiliado', '0,1'             -- Pendientes y Aprobados, sin autoexcluidos
--   EXEC [_V2_].[GetAffiliatePlayersForExport] 'NombreAfiliado', '', 0             -- Todos los estados, sin autoexcluidos
--   EXEC [_V2_].[GetAffiliatePlayersForExport] 'NombreAfiliado', '', 1             -- Todos los estados, solo autoexcluidos
--   EXEC [_V2_].[GetAffiliatePlayersForExport] 'NombreAfiliado', '', 2             -- Todos los estados, todos (con y sin autoexclusión)
--   EXEC [_V2_].[GetAffiliatePlayersForExport] 'NombreAfiliado', '1', 1            -- Solo Aprobados, solo autoexcluidos
-- Base de datos: SportsBet_Afiliados
--
-- Parámetros:
--   @rootAffiliate: Nombre del afiliado raíz
--   @statusIds: Lista de IDs de estado separados por coma (opcional, vacío = todos)
--               0 = Pendiente de aprobacion
--               1 = Aprobado
--               2 = Rechazado
--   @selfExclusionFilter: Filtro de autoexclusión (opcional, default = 0)
--               0 = Solo sin autoexclusión vigente (default, comportamiento original)
--               1 = Solo con autoexclusión vigente
--               2 = Todos (incluye con y sin autoexclusión)
--
-- Versión: 2.2
-- Fecha de modificación: 2025-12-11
-- Cambios:
--   v2.2 - Agregado: Parámetro @selfExclusionFilter para filtrar por autoexclusión
--   v2.1 - Agregado: Parámetro @statusIds para filtrar por estado(s)
--   v2.0 - Agregado: Afiliado Directo, Estado, Booleanos planos, Fecha Último Depósito, UTM
-- =============================================

-- exec [_V2_].[GetAffiliatePlayersForExport] 'Nicolorenzon'
-- exec [_V2_].[GetAffiliatePlayersForExport] 'Nicolorenzon', '1'
-- exec [_V2_].[GetAffiliatePlayersForExport] 'Nicolorenzon', '0,1'
-- exec [_V2_].[GetAffiliatePlayersForExport] 'Nicolorenzon', '', 1   -- Solo autoexcluidos
-- exec [_V2_].[GetAffiliatePlayersForExport] 'Nicolorenzon', '', 2   -- Todos

CREATE OR ALTER PROCEDURE [_V2_].[GetAffiliatePlayersForExport]
    @rootAffiliate varchar(500),
    @statusIds varchar(50) = '',      -- Lista de IDs separados por coma, vacío = todos
    @selfExclusionFilter int = 0      -- 0 = sin autoexcluidos, 1 = solo autoexcluidos, 2 = todos
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RootId int;

    -- Parsear los status IDs a una tabla temporal
    CREATE TABLE #StatusFilter (StatusId int);

    IF @statusIds IS NOT NULL AND @statusIds <> ''
    BEGIN
        INSERT INTO #StatusFilter (StatusId)
        SELECT CAST(value AS int)
        FROM STRING_SPLIT(@statusIds, ',')
        WHERE RTRIM(LTRIM(value)) <> '';
    END

    -- Crear tabla temporal para la jerarquía
    CREATE TABLE #AffiliateHierarchy (
        UserId int,
        ParentId int,
        Level int,
        INDEX IX_AffiliateHierarchy_Level NONCLUSTERED (Level)
    );

    -- Tabla temporal para usuarios procesados
    CREATE TABLE #ProcessedUsers (
        UserId int PRIMARY KEY
    );

    -- Obtener ID del afiliado raíz
    SELECT @RootId = Id
    FROM [_V2_Identity].[Users] WITH (NOLOCK)
    WHERE UserName = @rootAffiliate;

    -- Insertar sub-afiliados directos
    INSERT INTO #AffiliateHierarchy (UserId, ParentId, Level)
    SELECT UserId, ParentId, Level
    FROM [_V2_Agent].[HierarchicalUsers] h WITH (NOLOCK)
    WHERE ParentId = @RootId
    AND Discriminator = 'AffiliateHierarchicalUser';

    -- Registrar usuarios procesados (primer nivel)
    INSERT INTO #ProcessedUsers (UserId)
    SELECT DISTINCT UserId FROM #AffiliateHierarchy;

    DECLARE @CurrentLevel int = 1;
    DECLARE @RowsAffected int = 1;

    SELECT @CurrentLevel = (Level + 1)
    FROM [_V2_Agent].[HierarchicalUsers] WITH (NOLOCK)
    WHERE agentuserid = @RootId;

    -- Construir jerarquía recursivamente nivel por nivel
    WHILE @RowsAffected > 0
    BEGIN
        INSERT INTO #AffiliateHierarchy (UserId, ParentId, Level)
        SELECT DISTINCT
            h.UserId,
            h.ParentId,
            @CurrentLevel + 1
        FROM [_V2_Agent].[HierarchicalUsers] h WITH (NOLOCK)
        WHERE h.parentid IN (
            SELECT userid
            FROM #ProcessedUsers
        )
        AND h.userid NOT IN (
            SELECT userid FROM #AffiliateHierarchy
        );

        SET @RowsAffected = @@ROWCOUNT;

        IF @RowsAffected > 0
        BEGIN
            INSERT INTO #ProcessedUsers (UserId)
            SELECT DISTINCT h.UserId
            FROM #AffiliateHierarchy h
            WHERE h.Level = @CurrentLevel + 1
            AND NOT EXISTS (
                SELECT 1
                FROM #ProcessedUsers p
                WHERE p.UserId = h.UserId
            );
        END

        SET @CurrentLevel = @CurrentLevel + 1;
    END

    -- =============================================
    -- Tabla temporal para datos de auditoría y depósitos
    -- =============================================
    CREATE TABLE #PlayerAuditData (
        PlayerId INT PRIMARY KEY,
        LastLoginDate DATETIME,
        RegistrationDate DATETIME,
        LastDepositDate DATETIME
    );

    -- Obtener todos los PlayerIds únicos (aplicando filtro de status si existe)
    DECLARE @PlayerIds TABLE (PlayerId INT);

    INSERT INTO @PlayerIds
    SELECT DISTINCT h.PlayerUserId
    FROM [_V2_Agent].[HierarchicalUsers] h WITH (NOLOCK)
    INNER JOIN #AffiliateHierarchy pa ON pa.UserId = h.parentid
    LEFT JOIN SportsBet_Online.dbo.Vw_Player pd WITH (NOLOCK) ON pd.Id = h.PlayerUserId
    WHERE h.Discriminator <> 'AffiliateHierarchicalUser'
    AND pd.IsDeleted = 0
    AND pd.IsEnabled = 1
    AND pd.Id IS NOT NULL
    AND (NOT EXISTS (SELECT 1 FROM #StatusFilter) OR pd.StatusId IN (SELECT StatusId FROM #StatusFilter))

    UNION

    SELECT DISTINCT h.PlayerUserId
    FROM [_V2_Agent].[HierarchicalUsers] h WITH (NOLOCK)
    LEFT JOIN SportsBet_Online.dbo.Vw_Player pd WITH (NOLOCK) ON pd.Id = h.PlayerUserId
    WHERE h.Discriminator <> 'AffiliateHierarchicalUser'
    AND pd.IsDeleted = 0
    AND pd.IsEnabled = 1
    AND h.ParentId = @RootId
    AND pd.Id IS NOT NULL
    AND (NOT EXISTS (SELECT 1 FROM #StatusFilter) OR pd.StatusId IN (SELECT StatusId FROM #StatusFilter));

    -- Insertar último login
    INSERT INTO #PlayerAuditData (PlayerId, LastLoginDate)
    SELECT DISTINCT
        p.PlayerId,
        (SELECT TOP 1 a.InsDate
         FROM [SportsBet_Online].[Audit].[Activity] a WITH (NOLOCK)
         WHERE a.Player_Id = p.PlayerId
         AND a.ActivityType_Id = 1  -- SystemLogin
         ORDER BY a.InsDate DESC)
    FROM @PlayerIds p;

    -- Actualizar con fecha de registro
    UPDATE #PlayerAuditData
    SET RegistrationDate = COALESCE(
        (SELECT TOP 1 a.InsDate
         FROM [SportsBet_Online].[Audit].[Activity] a WITH (NOLOCK)
         WHERE a.Player_Id = #PlayerAuditData.PlayerId
         AND a.ActivityType_Id = 3  -- SystemRegister
         ORDER BY a.InsDate ASC),
        (SELECT u.InsDate
         FROM [SportsBet_Online].[dbo].[User] u WITH (NOLOCK)
         WHERE u.Id = #PlayerAuditData.PlayerId)
    );

    -- Actualizar con fecha de último depósito
    UPDATE #PlayerAuditData
    SET LastDepositDate = (
        SELECT MAX(dn.InsDate)
        FROM [SportsBet_Online].[Player].[DepositNotifications] dn WITH (NOLOCK)
        WHERE dn.PlayerId = #PlayerAuditData.PlayerId
    );

    -- =============================================
    -- QUERY PRINCIPAL - Parte 1: Jugadores bajo sub-afiliados
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

        -- Estado del jugador según BO
        pd.StatusName as Estado,

        -- Booleanos planos
        pd.IsEmailVerified as [Email Verificado],
        pd.IsEnabled as Habilitado,
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
                FROM [SportsBet_Online].[Player].[DepositNotifications] dn WITH (NOLOCK)
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
    INNER JOIN #AffiliateHierarchy pa ON pa.UserId = h.parentid
    LEFT JOIN SportsBet_Online.dbo.Vw_Player pd WITH (NOLOCK) ON pd.Id = h.PlayerUserId
    LEFT JOIN #PlayerAuditData pad ON pad.PlayerId = pd.Id
    LEFT JOIN [_V2_Identity].[Users] affiliate WITH (NOLOCK) ON affiliate.Id = h.ParentId
    LEFT JOIN [SportsBet_Online].[Player].[MarketingData] mkt WITH (NOLOCK) ON mkt.PlayerId = pd.Id

    WHERE h.Discriminator <> 'AffiliateHierarchicalUser'
    AND pd.IsDeleted = 0
    AND pd.IsEnabled = 1
    AND (NOT EXISTS (SELECT 1 FROM #StatusFilter) OR pd.StatusId IN (SELECT StatusId FROM #StatusFilter))
    -- Filtro de autoexclusión: 0 = sin autoexcluidos, 1 = solo autoexcluidos, 2 = todos
    AND (
        @selfExclusionFilter = 2  -- Todos
        OR (@selfExclusionFilter = 0 AND NOT EXISTS (
            SELECT 1
            FROM [SportsBet_Online].[Player].[Limit] l WITH (NOLOCK)
            WHERE l.PlayerId = pd.Id
            AND l.Name = 'Limite_De_AutoExclusion'
            AND l.Type = 1
            AND GETDATE() BETWEEN l.StartDate AND l.EndDate
        ))
        OR (@selfExclusionFilter = 1 AND EXISTS (
            SELECT 1
            FROM [SportsBet_Online].[Player].[Limit] l WITH (NOLOCK)
            WHERE l.PlayerId = pd.Id
            AND l.Name = 'Limite_De_AutoExclusion'
            AND l.Type = 1
            AND GETDATE() BETWEEN l.StartDate AND l.EndDate
        ))
    )

    UNION ALL

    -- =============================================
    -- QUERY PRINCIPAL - Parte 2: Jugadores directos del afiliado raíz
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

        -- Afiliado Directo (el afiliado raíz en este caso)
        @rootAffiliate as [Afiliado Directo],

        -- Estado del jugador según BO
        pd.StatusName as Estado,

        -- Booleanos planos
        pd.IsEmailVerified as [Email Verificado],
        pd.IsEnabled as Habilitado,
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
                FROM [SportsBet_Online].[Player].[DepositNotifications] dn WITH (NOLOCK)
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
    LEFT JOIN SportsBet_Online.dbo.Vw_Player pd WITH (NOLOCK) ON pd.Id = h.PlayerUserId
    LEFT JOIN #PlayerAuditData pad ON pad.PlayerId = pd.Id
    LEFT JOIN [SportsBet_Online].[Player].[MarketingData] mkt WITH (NOLOCK) ON mkt.PlayerId = pd.Id

    WHERE h.Discriminator <> 'AffiliateHierarchicalUser'
    AND pd.IsDeleted = 0
    AND pd.IsEnabled = 1
    AND h.ParentId = @RootId
    AND (NOT EXISTS (SELECT 1 FROM #StatusFilter) OR pd.StatusId IN (SELECT StatusId FROM #StatusFilter))
    -- Filtro de autoexclusión: 0 = sin autoexcluidos, 1 = solo autoexcluidos, 2 = todos
    AND (
        @selfExclusionFilter = 2  -- Todos
        OR (@selfExclusionFilter = 0 AND NOT EXISTS (
            SELECT 1
            FROM [SportsBet_Online].[Player].[Limit] l WITH (NOLOCK)
            WHERE l.PlayerId = pd.Id
            AND l.Name = 'Limite_De_AutoExclusion'
            AND l.Type = 1
            AND GETDATE() BETWEEN l.StartDate AND l.EndDate
        ))
        OR (@selfExclusionFilter = 1 AND EXISTS (
            SELECT 1
            FROM [SportsBet_Online].[Player].[Limit] l WITH (NOLOCK)
            WHERE l.PlayerId = pd.Id
            AND l.Name = 'Limite_De_AutoExclusion'
            AND l.Type = 1
            AND GETDATE() BETWEEN l.StartDate AND l.EndDate
        ))
    )

    -- Limpiar tablas temporales
    DROP TABLE #AffiliateHierarchy;
    DROP TABLE #ProcessedUsers;
    DROP TABLE #PlayerAuditData;
    DROP TABLE #StatusFilter;
END
GO
