USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =========================================================================
-- MULTI-ROLE + TWO-LEVEL (DEPARTMENT + VALUE) SCOPE MIGRATION
-- -------------------------------------------------------------------------
-- A user may hold MULTIPLE roles. Each role assignment grants a SET of scope
-- entries. A scope entry is a (KnitType, GaugeValue) pair:
--
--   KnitType   = department / factory_type : knit | weave | silk | linen | other
--   GaugeValue = the MasterPlanDetail.Guage value within that department:
--                  knit  -> gauge number  (e.g. '7')          ('' = all gauges)
--                  weave -> factory name   (e.g. 'Gyatri Pashmina')
--                  silk/linen/other -> tailor/master id (e.g. 't1')
--                GaugeValue = '' means "the whole department".
--
-- A plan row (factory_type, Guage) is visible to a user when ANY of their
-- scope entries satisfies:
--      entry.KnitType = row.factory_type
--      AND (entry.GaugeValue = '' OR entry.GaugeValue = row.Guage)
--
-- Empty scope set => unrestricted (admin sees everything).
-- =========================================================================

-- -------------------------------------------------------------------------
-- 1. Scope table: one row per (User, Role, KnitType, GaugeValue)
--    GaugeValue is NOT NULL; '' encodes "whole department".
-- -------------------------------------------------------------------------
-- Drop a stale table left over from an earlier single-column (Gauge) version so the
-- correct two-level (KnitType + GaugeValue) schema below always applies.
IF OBJECT_ID('identity.UserRoleGauges') IS NOT NULL
   AND COL_LENGTH('identity.UserRoleGauges', 'KnitType') IS NULL
BEGIN
    DROP TABLE [identity].[UserRoleGauges];
    PRINT 'Dropped stale single-column identity.UserRoleGauges.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'identity' AND TABLE_NAME = 'UserRoleGauges'
)
BEGIN
    CREATE TABLE [identity].[UserRoleGauges] (
        [UserId]     NVARCHAR(450) NOT NULL,
        [RoleId]     NVARCHAR(450) NOT NULL,
        [KnitType]   NVARCHAR(50)  NOT NULL,
        [GaugeValue] NVARCHAR(100) NOT NULL CONSTRAINT [DF_UserRoleGauges_GaugeValue] DEFAULT (''),
        CONSTRAINT [PK_UserRoleGauges] PRIMARY KEY ([UserId], [RoleId], [KnitType], [GaugeValue]),
        CONSTRAINT [FK_UserRoleGauges_AspNetUserRoles]
            FOREIGN KEY ([UserId], [RoleId])
            REFERENCES [identity].[AspNetUserRoles] ([UserId], [RoleId])
            ON DELETE CASCADE
    );
    PRINT 'Table identity.UserRoleGauges created.';
END
ELSE
BEGIN
    PRINT 'Table identity.UserRoleGauges already exists.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserRoleGauges_UserId')
    CREATE INDEX [IX_UserRoleGauges_UserId] ON [identity].[UserRoleGauges] ([UserId]) INCLUDE ([KnitType], [GaugeValue]);
GO

-- -------------------------------------------------------------------------
-- 2. Table-valued type for passing a scope set into sp_AssignUserRole.
-- -------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.types WHERE name = 'UserScopeList' AND is_table_type = 1)
BEGIN
    CREATE TYPE [dbo].[UserScopeList] AS TABLE (
        [KnitType]   NVARCHAR(50)  NOT NULL,
        [GaugeValue] NVARCHAR(100) NOT NULL
    );
    PRINT 'Type dbo.UserScopeList created.';
END
GO

-- -------------------------------------------------------------------------
-- 3. Best-effort backfill from the legacy single Users.AssignedGauge.
--    We only know the department for numeric values (=> knit). Non-numeric
--    legacy values (factory/master names) can't be classified, so those
--    users must be re-assigned scopes via the UI.
-- -------------------------------------------------------------------------
INSERT INTO [identity].[UserRoleGauges] ([UserId], [RoleId], [KnitType], [GaugeValue])
SELECT DISTINCT ua.[UserId], ua.[RoleId], 'knit', LTRIM(RTRIM(u.[AssignedGauge]))
FROM [identity].[AspNetUserRoles] ua
INNER JOIN [identity].[Users] u ON u.[Id] = ua.[UserId]
WHERE u.[AssignedGauge] IS NOT NULL
  AND LTRIM(RTRIM(u.[AssignedGauge])) <> ''
  AND TRY_CONVERT(FLOAT, REPLACE(REPLACE(REPLACE(u.[AssignedGauge], 'GG',''),'G',''),' ','')) IS NOT NULL
  AND NOT EXISTS (
        SELECT 1 FROM [identity].[UserRoleGauges] g
        WHERE g.[UserId] = ua.[UserId] AND g.[RoleId] = ua.[RoleId]
          AND g.[KnitType] = 'knit' AND g.[GaugeValue] = LTRIM(RTRIM(u.[AssignedGauge]))
  );
GO

-- =========================================================================
-- 4. sp_AssignUserRole (multi-role + structured scope set via TVP)
--    Flags: 1=Assign/UpsertScopes, 2=Remove, 3=GetByUser, 4=GetAllUsersWithRoles
--    Scope set is returned (flags 3/4) encoded as: knit|;weave|Gyatri Pashmina;silk|t1
--    (entries ';'-separated, KnitType and GaugeValue '|'-separated, '' = whole dept).
-- =========================================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_AssignUserRole]
    @flag       INT,
    @userId     NVARCHAR(450) = NULL,
    @roleId     NVARCHAR(450) = NULL,
    @assignedBy NVARCHAR(450) = NULL,
    @scopes     [dbo].[UserScopeList] READONLY
AS
BEGIN
    SET NOCOUNT ON;

    -- ASSIGN role (additive) + (re)set this assignment's scope set
    IF @flag = 1
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM [identity].[AspNetUserRoles] WHERE [UserId] = @userId AND [RoleId] = @roleId)
            INSERT INTO [identity].[AspNetUserRoles] ([UserId], [RoleId]) VALUES (@userId, @roleId);

        DELETE FROM [identity].[UserRoleGauges] WHERE [UserId] = @userId AND [RoleId] = @roleId;

        INSERT INTO [identity].[UserRoleGauges] ([UserId], [RoleId], [KnitType], [GaugeValue])
        SELECT DISTINCT @userId, @roleId, LTRIM(RTRIM(s.[KnitType])), LTRIM(RTRIM(ISNULL(s.[GaugeValue], '')))
        FROM @scopes s
        WHERE LTRIM(RTRIM(s.[KnitType])) <> '';

        SELECT 1 AS Result, 'Role assigned to user.' AS Message;
    END

    -- REMOVE role (scope rows cascade-delete via FK)
    ELSE IF @flag = 2
    BEGIN
        DELETE FROM [identity].[AspNetUserRoles] WHERE [UserId] = @userId AND [RoleId] = @roleId;
        SELECT 1 AS Result, 'Role removed from user.' AS Message;
    END

    -- GET roles for a specific user (one row per role; scope set encoded)
    ELSE IF @flag = 3
    BEGIN
        SELECT
            0 AS [UserRoleId],
            ua.[UserId],
            r.[Id]   AS [RoleId],
            r.[Name] AS [RoleName],
            r.[Description],
            STUFF((
                SELECT ';' + g.[KnitType] + '|' + g.[GaugeValue]
                FROM [identity].[UserRoleGauges] g
                WHERE g.[UserId] = ua.[UserId] AND g.[RoleId] = ua.[RoleId]
                ORDER BY g.[KnitType], g.[GaugeValue]
                FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, '') AS [AssignedGauge],
            GETDATE() AS [AssignedDate]
        FROM [identity].[AspNetUserRoles] ua
        INNER JOIN [identity].[Roles] r ON ua.[RoleId] = r.[Id]
        WHERE ua.[UserId] = @userId;
    END

    -- GET ALL users with their roles (one row per user-role; scope set encoded)
    ELSE IF @flag = 4
    BEGIN
        SELECT
            u.[Id]        AS [UserId],
            u.[Email],
            u.[FirstName] + ' ' + u.[LastName] AS [FullName],
            r.[Id]        AS [RoleId],
            r.[Name]      AS [RoleName],
            STUFF((
                SELECT ';' + g.[KnitType] + '|' + g.[GaugeValue]
                FROM [identity].[UserRoleGauges] g
                WHERE g.[UserId] = u.[Id] AND g.[RoleId] = r.[Id]
                ORDER BY g.[KnitType], g.[GaugeValue]
                FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, '') AS [AssignedGauge],
            GETDATE()     AS [AssignedDate]
        FROM [identity].[Users] u
        LEFT JOIN [identity].[AspNetUserRoles] ua ON u.[Id] = ua.[UserId]
        LEFT JOIN [identity].[Roles] r ON ua.[RoleId] = r.[Id]
        ORDER BY u.[Email];
    END
END
GO

-- =========================================================================
-- 5. sp_GetUserPermissions
--    Zero Trust. Returns the user's FULL scope set (union across all roles),
--    encoded the same way, in [AssignedGauge]. Empty => unrestricted.
-- =========================================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_GetUserPermissions]
    @userId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;

    CREATE TABLE #UserPerms (
        [PageKey]   NVARCHAR(100),
        [PageName]  NVARCHAR(200),
        [CanView]   INT,
        [CanEdit]   INT,
        [CanDelete] INT
    );

    IF EXISTS (
        SELECT 1
        FROM [identity].[AspNetUserRoles] ur
        INNER JOIN [identity].[Roles] ar ON ur.[RoleId] = ar.[Id]
        WHERE ur.[UserId] = @userId AND ar.[Name] = 'Admin'
    ) OR EXISTS (
        SELECT 1 FROM [identity].[Users] WHERE [Id] = @userId AND [Email] = 'admin@nkplm.erp'
    )
    BEGIN
        INSERT INTO #UserPerms ([PageKey], [PageName], [CanView], [CanEdit], [CanDelete])
        VALUES ('RoleManagement', 'Role Management', 1, 1, 1);
    END

    INSERT INTO #UserPerms ([PageKey], [PageName], [CanView], [CanEdit], [CanDelete])
    SELECT
        PARSENAME(p.[Name], 2),
        PARSENAME(p.[Name], 2),
        MAX(CASE WHEN PARSENAME(p.[Name], 1) = 'View'   AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END),
        MAX(CASE WHEN PARSENAME(p.[Name], 1) = 'Edit'   AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END),
        MAX(CASE WHEN PARSENAME(p.[Name], 1) = 'Delete' AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END)
    FROM [identity].[Permissions] p
    INNER JOIN [identity].[RolePermissions] rp ON p.[Id] = rp.[PermissionId]
    INNER JOIN [identity].[AspNetUserRoles] ua ON rp.[RoleId] = ua.[RoleId]
    INNER JOIN [identity].[Roles] r ON ua.[RoleId] = r.[Id]
    WHERE ua.[UserId] = @userId
    GROUP BY PARSENAME(p.[Name], 2);

    DECLARE @scopeCsv NVARCHAR(MAX) =
        STUFF((
            SELECT ';' + s.[KnitType] + '|' + s.[GaugeValue]
            FROM (SELECT DISTINCT [KnitType], [GaugeValue] FROM [identity].[UserRoleGauges] WHERE [UserId] = @userId) s
            ORDER BY s.[KnitType], s.[GaugeValue]
            FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, '');

    SELECT
        [PageKey],
        [PageName],
        MAX([CanView])   AS [CanView],
        MAX([CanEdit])   AS [CanEdit],
        MAX([CanDelete]) AS [CanDelete],
        @scopeCsv        AS [AssignedGauge]   -- encoded scope set; NULL/'' = unrestricted
    FROM #UserPerms
    GROUP BY [PageKey], [PageName]
    ORDER BY [PageName];

    DROP TABLE #UserPerms;
END
GO

PRINT 'Multi-role + two-level scope migration applied.';
GO
