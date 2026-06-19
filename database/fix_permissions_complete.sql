USE [NatureKnit];   -- <-- change to your app's DB if different
GO
DECLARE @msg NVARCHAR(200);

-- =========================================================================
-- ONE-SHOT FIX for "403 / no permissions" caused by sp_GetUserPermissions
-- erroring on the wrong UserRoleGauges schema. Runs in the correct order:
--   1) fix UserRoleGauges schema   2) ensure scope TVP type
--   3) re-create the correct sp_GetUserPermissions
--   4) ensure permission rows      5) grant everything to YOUR account
-- Idempotent — safe to re-run.
-- =========================================================================

-- 1. Fix UserRoleGauges schema (drop stale single-column version)
IF OBJECT_ID('identity.UserRoleGauges') IS NOT NULL
   AND COL_LENGTH('identity.UserRoleGauges', 'KnitType') IS NULL
BEGIN
    DROP TABLE [identity].[UserRoleGauges];
    PRINT 'Dropped stale UserRoleGauges.';
END
GO
IF OBJECT_ID('identity.UserRoleGauges') IS NULL
BEGIN
    CREATE TABLE [identity].[UserRoleGauges] (
        [UserId]     NVARCHAR(450) NOT NULL,
        [RoleId]     NVARCHAR(450) NOT NULL,
        [KnitType]   NVARCHAR(50)  NOT NULL,
        [GaugeValue] NVARCHAR(100) NOT NULL CONSTRAINT [DF_UserRoleGauges_GaugeValue] DEFAULT (''),
        CONSTRAINT [PK_UserRoleGauges] PRIMARY KEY ([UserId], [RoleId], [KnitType], [GaugeValue]),
        CONSTRAINT [FK_UserRoleGauges_AspNetUserRoles]
            FOREIGN KEY ([UserId], [RoleId])
            REFERENCES [identity].[AspNetUserRoles] ([UserId], [RoleId]) ON DELETE CASCADE
    );
    PRINT 'Created UserRoleGauges (KnitType + GaugeValue).';
END
GO

-- 2. Scope TVP type
IF NOT EXISTS (SELECT 1 FROM sys.types WHERE name = 'UserScopeList' AND is_table_type = 1)
    CREATE TYPE [dbo].[UserScopeList] AS TABLE ([KnitType] NVARCHAR(50) NOT NULL, [GaugeValue] NVARCHAR(100) NOT NULL);
GO

-- 2b. Correct sp_AssignUserRole (multi-role + scope TVP). If the DB still has an
--     older version without @scopes, the assign throws and nothing saves.
CREATE OR ALTER PROCEDURE [dbo].[sp_AssignUserRole]
    @flag       INT,
    @userId     NVARCHAR(450) = NULL,
    @roleId     NVARCHAR(450) = NULL,
    @assignedBy NVARCHAR(450) = NULL,
    @scopes     [dbo].[UserScopeList] READONLY
AS
BEGIN
    SET NOCOUNT ON;

    IF @flag = 1
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM [identity].[AspNetUserRoles] WHERE [UserId]=@userId AND [RoleId]=@roleId)
            INSERT INTO [identity].[AspNetUserRoles] ([UserId],[RoleId]) VALUES (@userId,@roleId);

        DELETE FROM [identity].[UserRoleGauges] WHERE [UserId]=@userId AND [RoleId]=@roleId;

        INSERT INTO [identity].[UserRoleGauges] ([UserId],[RoleId],[KnitType],[GaugeValue])
        SELECT DISTINCT @userId, @roleId, LTRIM(RTRIM(s.[KnitType])), LTRIM(RTRIM(ISNULL(s.[GaugeValue],'')))
        FROM @scopes s WHERE LTRIM(RTRIM(s.[KnitType])) <> '';

        SELECT 1 AS Result, 'Role assigned to user.' AS Message;
    END
    ELSE IF @flag = 2
    BEGIN
        DELETE FROM [identity].[AspNetUserRoles] WHERE [UserId]=@userId AND [RoleId]=@roleId;
        SELECT 1 AS Result, 'Role removed from user.' AS Message;
    END
    ELSE IF @flag = 3
    BEGIN
        SELECT 0 AS [UserRoleId], ua.[UserId], r.[Id] AS [RoleId], r.[Name] AS [RoleName], r.[Description],
            STUFF((SELECT ';' + g.[KnitType] + '|' + g.[GaugeValue] FROM [identity].[UserRoleGauges] g
                   WHERE g.[UserId]=ua.[UserId] AND g.[RoleId]=ua.[RoleId] ORDER BY g.[KnitType],g.[GaugeValue]
                   FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'),1,1,'') AS [AssignedGauge],
            GETDATE() AS [AssignedDate]
        FROM [identity].[AspNetUserRoles] ua INNER JOIN [identity].[Roles] r ON ua.[RoleId]=r.[Id]
        WHERE ua.[UserId]=@userId;
    END
    ELSE IF @flag = 4
    BEGIN
        SELECT u.[Id] AS [UserId], u.[Email], u.[FirstName]+' '+u.[LastName] AS [FullName],
            r.[Id] AS [RoleId], r.[Name] AS [RoleName],
            STUFF((SELECT ';' + g.[KnitType] + '|' + g.[GaugeValue] FROM [identity].[UserRoleGauges] g
                   WHERE g.[UserId]=u.[Id] AND g.[RoleId]=r.[Id] ORDER BY g.[KnitType],g.[GaugeValue]
                   FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'),1,1,'') AS [AssignedGauge],
            GETDATE() AS [AssignedDate]
        FROM [identity].[Users] u
        LEFT JOIN [identity].[AspNetUserRoles] ua ON u.[Id]=ua.[UserId]
        LEFT JOIN [identity].[Roles] r ON ua.[RoleId]=r.[Id]
        ORDER BY u.[Email];
    END
END
GO

-- 3. Correct sp_GetUserPermissions (two-level scope encoding)
CREATE OR ALTER PROCEDURE [dbo].[sp_GetUserPermissions]
    @userId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    CREATE TABLE #UserPerms ([PageKey] NVARCHAR(100),[PageName] NVARCHAR(200),[CanView] INT,[CanEdit] INT,[CanDelete] INT);

    IF EXISTS (SELECT 1 FROM [identity].[AspNetUserRoles] ur INNER JOIN [identity].[Roles] ar ON ur.[RoleId]=ar.[Id]
               WHERE ur.[UserId]=@userId AND ar.[Name]='Admin')
       OR EXISTS (SELECT 1 FROM [identity].[Users] WHERE [Id]=@userId AND [Email]='admin@nkplm.erp')
    BEGIN
        INSERT INTO #UserPerms VALUES ('RoleManagement','Role Management',1,1,1);
    END

    INSERT INTO #UserPerms
    SELECT PARSENAME(p.[Name],2), PARSENAME(p.[Name],2),
        MAX(CASE WHEN PARSENAME(p.[Name],1)='View'   AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END),
        MAX(CASE WHEN PARSENAME(p.[Name],1)='Edit'   AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END),
        MAX(CASE WHEN PARSENAME(p.[Name],1)='Delete' AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END)
    FROM [identity].[Permissions] p
    INNER JOIN [identity].[RolePermissions] rp ON p.[Id]=rp.[PermissionId]
    INNER JOIN [identity].[AspNetUserRoles] ua ON rp.[RoleId]=ua.[RoleId]
    WHERE ua.[UserId]=@userId
    GROUP BY PARSENAME(p.[Name],2);

    DECLARE @scopeCsv NVARCHAR(MAX) =
        STUFF((SELECT ';' + s.[KnitType] + '|' + s.[GaugeValue]
               FROM (SELECT DISTINCT [KnitType],[GaugeValue] FROM [identity].[UserRoleGauges] WHERE [UserId]=@userId) s
               ORDER BY s.[KnitType], s.[GaugeValue]
               FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 1, '');

    SELECT [PageKey],[PageName], MAX([CanView]) AS [CanView], MAX([CanEdit]) AS [CanEdit],
           MAX([CanDelete]) AS [CanDelete], @scopeCsv AS [AssignedGauge]
    FROM #UserPerms GROUP BY [PageKey],[PageName] ORDER BY [PageName];

    DROP TABLE #UserPerms;
END
GO

-- 4. Ensure every page has View/Edit/Delete permission rows
INSERT INTO [identity].[Permissions] ([Id],[Name],[Description])
SELECT NEWID(), ap.[PageKey] + '.' + a.[Action], ap.[PageName] + ' ' + a.[Action]
FROM [identity].[AppPages] ap
CROSS JOIN (VALUES ('View'),('Edit'),('Delete')) a([Action])
WHERE NOT EXISTS (SELECT 1 FROM [identity].[Permissions] p WHERE p.[Name]=ap.[PageKey]+'.'+a.[Action]);

INSERT INTO [identity].[Permissions] ([Id],[Name],[Description])
SELECT NEWID(), v.[Name], v.[Name]
FROM (VALUES ('RoleManagement.View'),('RoleManagement.Edit'),('RoleManagement.Delete'),
             ('PagesManagement.View'),('PagesManagement.Edit'),('PagesManagement.Delete')) v([Name])
WHERE NOT EXISTS (SELECT 1 FROM [identity].[Permissions] p WHERE p.[Name]=v.[Name]);
GO

-- 5. Grant ALL permissions to every role your login account holds
DECLARE @myEmail NVARCHAR(256) = 'admin@nkplm.erp';   -- <<< SET YOUR LOGIN EMAIL

PRINT '--- Your account & roles ---';
SELECT u.[Email], u.[FirstName]+' '+u.[LastName] AS FullName, r.[Name] AS RoleName
FROM [identity].[Users] u
LEFT JOIN [identity].[AspNetUserRoles] ur ON ur.[UserId]=u.[Id]
LEFT JOIN [identity].[Roles] r ON r.[Id]=ur.[RoleId]
WHERE u.[Email]=@myEmail;

INSERT INTO [identity].[RolePermissions] ([RoleId],[PermissionId])
SELECT DISTINCT ur.[RoleId], p.[Id]
FROM [identity].[AspNetUserRoles] ur
INNER JOIN [identity].[Users] u ON u.[Id]=ur.[UserId]
CROSS JOIN [identity].[Permissions] p
WHERE u.[Email]=@myEmail
  AND NOT EXISTS (SELECT 1 FROM [identity].[RolePermissions] rp WHERE rp.[RoleId]=ur.[RoleId] AND rp.[PermissionId]=p.[Id]);

PRINT 'Done. Log out and log back in.';
GO
