USE [NatureKnit]
GO

-- 1. Add AssignedGauge to identity.Users if it doesn't exist
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'identity' AND TABLE_NAME = 'Users' AND COLUMN_NAME = 'AssignedGauge'
)
BEGIN
    ALTER TABLE [identity].[Users] ADD [AssignedGauge] NVARCHAR(100) NULL;
    PRINT 'AssignedGauge column added to identity.Users table.';
END
ELSE
BEGIN
    PRINT 'AssignedGauge column already exists in identity.Users.';
END
GO

-- 2. Drop column from identity.Roles is optional, let's keep it but stop using it so we don't break existing data queries during transit.
-- If we want to clean it up, we can do it, but keeping it ensures backward compatibility during migration.
GO

-- 3. Re-create sp_ManageRole (remove AssignedGauge from Role creation/updates)
CREATE OR ALTER PROCEDURE [dbo].[sp_ManageRole]
    @flag         INT,
    @roleId       NVARCHAR(450) = NULL,
    @roleName     NVARCHAR(100) = NULL,
    @description  NVARCHAR(500) = NULL,
    @assignedGauge NVARCHAR(100) = NULL, -- Deprecated, ignored
    @isActive     BIT           = 1,
    @userId       NVARCHAR(450) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- INSERT new role
    IF @flag = 1
    BEGIN
        IF EXISTS (SELECT 1 FROM [identity].[Roles] WHERE [Name] = @roleName)
        BEGIN
            SELECT -1 AS Result, 'Role name already exists.' AS Message;
            RETURN;
        END

        DECLARE @newId NVARCHAR(450) = CAST(NEWID() AS NVARCHAR(450));

        INSERT INTO [identity].[Roles] ([Id], [Name], [NormalizedName], [Description], [CreatedAt], [ConcurrencyStamp])
        VALUES (@newId, @roleName, UPPER(@roleName), ISNULL(@description, ''), GETDATE(), NEWID());

        SELECT 1 AS Result, 'Role created successfully.' AS Message;
    END

    -- UPDATE existing role
    ELSE IF @flag = 2
    BEGIN
        UPDATE [identity].[Roles]
        SET [Name]          = @roleName,
            [NormalizedName]= UPPER(@roleName),
            [Description]   = ISNULL(@description, '')
        WHERE [Id] = @roleId;

        SELECT 1 AS Result, 'Role updated successfully.' AS Message;
    END

    -- DELETE role
    ELSE IF @flag = 3
    BEGIN
        DELETE FROM [identity].[RolePermissions] WHERE [RoleId] = @roleId;
        DELETE FROM [identity].[AspNetUserRoles] WHERE [RoleId] = @roleId;
        DELETE FROM [identity].[Roles] WHERE [Id] = @roleId;

        SELECT 1 AS Result, 'Role deleted successfully.' AS Message;
    END

    -- GET ALL roles
    ELSE IF @flag = 4
    BEGIN
        SELECT 
            r.[Id] AS [RoleId],
            r.[Name] AS [RoleName],
            r.[Description],
            CAST(NULL AS NVARCHAR(100)) AS [AssignedGauge], -- Return NULL for roles
            CAST(1 AS BIT) AS [IsActive],
            r.[CreatedAt] AS [CreatedDate],
            COUNT(DISTINCT ua.[UserId]) AS [UserCount],
            COUNT(DISTINCT rp.[PermissionId]) AS [PageCount]
        FROM [identity].[Roles] r
        LEFT JOIN [identity].[AspNetUserRoles] ua ON r.[Id] = ua.[RoleId]
        LEFT JOIN [identity].[RolePermissions] rp ON r.[Id] = rp.[RoleId]
        GROUP BY r.[Id], r.[Name], r.[Description], r.[CreatedAt]
        ORDER BY r.[Name];
    END

    -- GET BY ID
    ELSE IF @flag = 5
    BEGIN
        SELECT 
            [Id] AS [RoleId], 
            [Name] AS [RoleName], 
            [Description], 
            CAST(NULL AS NVARCHAR(100)) AS [AssignedGauge], -- Return NULL for roles
            CAST(1 AS BIT) AS [IsActive], 
            [CreatedAt] AS [CreatedDate]
        FROM [identity].[Roles]
        WHERE [Id] = @roleId;
    END
END
GO

-- 4. Re-create sp_AssignUserRole (update flag 4 to fetch AssignedGauge from identity.Users)
CREATE OR ALTER PROCEDURE [dbo].[sp_AssignUserRole]
    @flag       INT,
    @userId     NVARCHAR(450) = NULL,
    @roleId     NVARCHAR(450) = NULL,
    @assignedBy NVARCHAR(450) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- ASSIGN role to user
    IF @flag = 1
    BEGIN
        IF EXISTS (SELECT 1 FROM [identity].[AspNetUserRoles] WHERE [UserId] = @userId AND [RoleId] = @roleId)
        BEGIN
            SELECT -1 AS Result, 'User already has this role.' AS Message;
            RETURN;
        END

        DELETE FROM [identity].[AspNetUserRoles] WHERE [UserId] = @userId;

        INSERT INTO [identity].[AspNetUserRoles] ([UserId], [RoleId])
        VALUES (@userId, @roleId);
        
        SELECT 1 AS Result, 'Role assigned to user.' AS Message;
    END

    -- REMOVE role from user
    ELSE IF @flag = 2
    BEGIN
        DELETE FROM [identity].[AspNetUserRoles]
        WHERE [UserId] = @userId AND [RoleId] = @roleId;
        SELECT 1 AS Result, 'Role removed from user.' AS Message;
    END

    -- GET roles for a specific user
    ELSE IF @flag = 3
    BEGIN
        SELECT 
            0 AS [UserRoleId],
            ua.[UserId],
            r.[Id] AS [RoleId],
            r.[Name] AS [RoleName],
            r.[Description],
            (SELECT TOP 1 u.[AssignedGauge] FROM [identity].[Users] u WHERE u.[Id] = ua.[UserId]) AS [AssignedGauge],
            GETDATE() AS [AssignedDate]
        FROM [identity].[AspNetUserRoles] ua
        INNER JOIN [identity].[Roles] r ON ua.[RoleId] = r.[Id]
        WHERE ua.[UserId] = @userId;
    END

    -- GET ALL users with their roles
    ELSE IF @flag = 4
    BEGIN
        SELECT 
            u.[Id]        AS [UserId],
            u.[Email],
            u.[FirstName] + ' ' + u.[LastName] AS [FullName],
            r.[Id]        AS [RoleId],
            r.[Name]      AS [RoleName],
            u.[AssignedGauge], -- Fetch from Users table
            GETDATE()     AS [AssignedDate]
        FROM [identity].[Users] u
        LEFT JOIN [identity].[AspNetUserRoles] ua ON u.[Id] = ua.[UserId]
        LEFT JOIN [identity].[Roles] r ON ua.[RoleId] = r.[Id]
        ORDER BY u.[Email];
    END
END
GO

-- 5. Re-create sp_GetUserPermissions (fetch AssignedGauge from identity.Users table)
CREATE OR ALTER PROCEDURE [dbo].[sp_GetUserPermissions]
    @userId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;

    CREATE TABLE #UserPerms (
        [PageKey] NVARCHAR(100),
        [PageName] NVARCHAR(200),
        [CanView] INT,
        [CanEdit] INT,
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
        PARSENAME(p.[Name], 2) AS [PageKey],
        PARSENAME(p.[Name], 2) AS [PageName],
        MAX(CASE WHEN PARSENAME(p.[Name], 1) = 'View'   AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END) AS [CanView],
        MAX(CASE WHEN PARSENAME(p.[Name], 1) = 'Edit'   AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END) AS [CanEdit],
        MAX(CASE WHEN PARSENAME(p.[Name], 1) = 'Delete' AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END) AS [CanDelete]
    FROM [identity].[Permissions] p
    INNER JOIN [identity].[RolePermissions] rp ON p.[Id] = rp.[PermissionId]
    INNER JOIN [identity].[AspNetUserRoles] ua ON rp.[RoleId] = ua.[RoleId]
    INNER JOIN [identity].[Roles] r ON ua.[RoleId] = r.[Id]
    WHERE ua.[UserId] = @userId
    GROUP BY PARSENAME(p.[Name], 2);

    SELECT 
        [PageKey],
        [PageName],
        MAX([CanView]) AS [CanView],
        MAX([CanEdit]) AS [CanEdit],
        MAX([CanDelete]) AS [CanDelete],
        (
            SELECT TOP 1 u.[AssignedGauge]
            FROM [identity].[Users] u
            WHERE u.[Id] = @userId
        ) AS [AssignedGauge]
    FROM #UserPerms
    GROUP BY [PageKey], [PageName]
    ORDER BY [PageName];

    DROP TABLE #UserPerms;
END
GO
