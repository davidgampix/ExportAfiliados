-- =============================================
-- Stored Procedure: [_V2_].[GetAffiliatePlayersForExport]
-- Descripción: Obtiene los jugadores de la red jerárquica de un afiliado para exportación
-- Uso: EXEC [_V2_].[GetAffiliatePlayersForExport] 'NombreAfiliado'
-- Base de datos: SportsBet_Afiliados
--
-- Nombre anterior: [_V2_].[GetHierarchicalPlayersEmailVerified]
-- Este archivo contiene la versión ORIGINAL del SP antes de modificaciones
-- Fecha de extracción: 2025-12-11
-- =============================================

-- exec [_V2_].[GetAffiliatePlayersForExport] 'Nicolorenzon'

CREATE PROCEDURE [_V2_].[GetAffiliatePlayersForExport]
    @rootAffiliate varchar(500)
AS
BEGIN
    DECLARE @RootId int;

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
        -- Insertar siguiente nivel de sub-afiliados
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
            -- Registrar nuevos usuarios procesados evitando duplicados
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

    -- Crear tabla temporal para datos de auditoría (más performante que múltiples subconsultas)
    CREATE TABLE #PlayerAuditData (
        PlayerId INT PRIMARY KEY,
        LastLoginDate DATETIME,
        RegistrationDate DATETIME
    );

    -- Obtener todos los PlayerIds únicos primero
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

    UNION

    SELECT DISTINCT h.PlayerUserId
    FROM [_V2_Agent].[HierarchicalUsers] h WITH (NOLOCK)
    LEFT JOIN SportsBet_Online.dbo.Vw_Player pd WITH (NOLOCK) ON pd.Id = h.PlayerUserId
    WHERE h.Discriminator <> 'AffiliateHierarchicalUser'
    AND pd.IsDeleted = 0
    AND pd.IsEnabled = 1
    AND h.ParentId = @RootId
    AND pd.Id IS NOT NULL;

    -- Obtener último login para todos los jugadores de una vez
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

    -- ===========================================
    -- QUERY PRINCIPAL - Parte 1: Jugadores bajo sub-afiliados de la jerarquía
    -- ===========================================
    SELECT
        pd.Id,
        pd.FirstName as Nombre,
        pd.LastName as Apellido,
        pd.DocumentNumber as DNI,
        pd.Email,
        ISNULL('(' + pd.AreaCode + ') ', '') + pd.Phone as Telefono,
        pd.Address as Direccion,
        pd.City as Localidad,
        pd.BrandAffiliate as Afiliado,
        CASE WHEN pd.IsEmailVerified = 1 THEN 'Sí' ELSE 'No' END as [Email Verificado],
        CASE
            WHEN EXISTS (
                SELECT 1
                FROM [SportsBet_Online].[Player].[DepositNotifications] dn WITH (NOLOCK)
                WHERE dn.PlayerId = pd.Id
            ) THEN 'Sí'
            ELSE 'No'
        END as [Ha realizado depósito],
        CONVERT(VARCHAR, pad.RegistrationDate, 120) as [Fecha de Registro],
        CONVERT(VARCHAR, pad.LastLoginDate, 120) as [Último Login]
    FROM [_V2_Agent].[HierarchicalUsers] h WITH (NOLOCK)
    INNER JOIN #AffiliateHierarchy pa ON pa.UserId = h.parentid
    LEFT JOIN SportsBet_Online.dbo.Vw_Player pd WITH (NOLOCK) ON pd.Id = h.PlayerUserId
    LEFT JOIN #PlayerAuditData pad ON pad.PlayerId = pd.Id
    WHERE h.Discriminator <> 'AffiliateHierarchicalUser'
    AND pd.IsDeleted = 0
    AND pd.IsEnabled = 1
    -- Excluir jugadores con autoexclusión vigente
    AND NOT EXISTS (
        SELECT 1
        FROM [SportsBet_Online].[Player].[Limit] l WITH (NOLOCK)
        WHERE l.PlayerId = pd.Id
        AND l.Name = 'Limite_De_AutoExclusion'
        AND l.Type = 1
        AND GETDATE() BETWEEN l.StartDate AND l.EndDate
    )

    UNION ALL

    -- ===========================================
    -- QUERY PRINCIPAL - Parte 2: Jugadores directos del afiliado raíz
    -- ===========================================
    SELECT
        pd.Id,
        pd.FirstName as Nombre,
        pd.LastName as Apellido,
        pd.DocumentNumber as DNI,
        pd.Email,
        ISNULL('(' + pd.AreaCode + ') ', '') + pd.Phone as Telefono,
        pd.Address as Direccion,
        pd.City as Localidad,
        pd.BrandAffiliate as Afiliado,
        CASE WHEN pd.IsEmailVerified = 1 THEN 'Sí' ELSE 'No' END as [Email Verificado],
        CASE
            WHEN EXISTS (
                SELECT 1
                FROM [SportsBet_Online].[Player].[DepositNotifications] dn WITH (NOLOCK)
                WHERE dn.PlayerId = pd.Id
            ) THEN 'Sí'
            ELSE 'No'
        END as [Ha realizado depósito],
        CONVERT(VARCHAR, pad.RegistrationDate, 120) as [Fecha de Registro],
        CONVERT(VARCHAR, pad.LastLoginDate, 120) as [Último Login]
    FROM [_V2_Agent].[HierarchicalUsers] h WITH (NOLOCK)
    LEFT JOIN SportsBet_Online.dbo.Vw_Player pd WITH (NOLOCK) ON pd.Id = h.PlayerUserId
    LEFT JOIN #PlayerAuditData pad ON pad.PlayerId = pd.Id
    WHERE h.Discriminator <> 'AffiliateHierarchicalUser'
    AND pd.IsDeleted = 0
    AND pd.IsEnabled = 1
    AND h.ParentId = @RootId
    -- Excluir jugadores con autoexclusión vigente
    AND NOT EXISTS (
        SELECT 1
        FROM [SportsBet_Online].[Player].[Limit] l WITH (NOLOCK)
        WHERE l.PlayerId = pd.Id
        AND l.Name = 'Limite_De_AutoExclusion'
        AND l.Type = 1
        AND GETDATE() BETWEEN l.StartDate AND l.EndDate
    )

    -- Limpiar tablas temporales
    DROP TABLE #AffiliateHierarchy;
    DROP TABLE #ProcessedUsers;
    DROP TABLE #PlayerAuditData;
END
