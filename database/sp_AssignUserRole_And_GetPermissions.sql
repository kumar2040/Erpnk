USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =========================================================================
-- sp_AssignUserRole
-- Flags: 1=Assign, 2=Remove, 3=GetByUser, 4=GetAllUsersWithRoles
-- =========================================================================
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

        -- Clear other roles if standard 1-user-to-1-role model
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
            r.[AssignedGauge],
            GETDATE() AS [AssignedDate]
        FROM [identity].[AspNetUserRoles] ua
        INNER JOIN [identity].[Roles] r ON ua.[RoleId] = r.[Id]
        WHERE ua.[UserId] = @userId;
    END

    -- GET ALL users with their roles (for admin overview)
    ELSE IF @flag = 4
    BEGIN
        SELECT 
            u.[Id]        AS [UserId],
            u.[Email],
            u.[FirstName] + ' ' + u.[LastName] AS [FullName],
            r.[Id]        AS [RoleId],
            r.[Name]      AS [RoleName],
            r.[AssignedGauge],
            GETDATE()     AS [AssignedDate]
        FROM [identity].[Users] u
        LEFT JOIN [identity].[AspNetUserRoles] ua ON u.[Id] = ua.[UserId]
        LEFT JOIN [identity].[Roles] r ON ua.[RoleId] = r.[Id]
        ORDER BY u.[Email];
    END
END
GO

-- =========================================================================
-- sp_GetUserPermissions
-- CRITICAL: This is the Zero Trust enforcement query.
-- Returns all page permissions for a user based on their assigned role.
-- Also returns their AssignedGauge for data-level filtering.
-- =========================================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_GetUserPermissions]
    @userId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;

    -- Create a temp table to aggregate permissions
    CREATE TABLE #UserPerms (
        [PageKey] NVARCHAR(100),
        [PageName] NVARCHAR(200),
        [CanView] INT,
        [CanEdit] INT,
        [CanDelete] INT
    );

    -- 1. If user is standard admin or in Identity 'Admin' role, always bootstrap full 'RoleManagement' permissions
    -- to prevent permanent lockout.
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

    -- 2. Insert mapped permissions from database for this user
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

    -- 3. Return the aggregated permissions (taking the MAX values)
    SELECT 
        [PageKey],
        [PageName],
        MAX([CanView]) AS [CanView],
        MAX([CanEdit]) AS [CanEdit],
        MAX([CanDelete]) AS [CanDelete],
        (
            SELECT TOP 1 r2.[AssignedGauge]
            FROM [identity].[AspNetUserRoles] uar2
            INNER JOIN [identity].[Roles] r2 ON uar2.[RoleId] = r2.[Id]
            WHERE uar2.[UserId] = @userId AND r2.[AssignedGauge] IS NOT NULL
        ) AS [AssignedGauge]
    FROM #UserPerms
    GROUP BY [PageKey], [PageName]
    ORDER BY [PageName];

    DROP TABLE #UserPerms;
END
GO
