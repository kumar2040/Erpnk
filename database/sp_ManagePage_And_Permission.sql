USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =========================================================================
-- sp_ManagePage (Stays as a dummy procedure or manages identity.Permissions)
-- =========================================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ManagePage]
    @flag      INT,
    @appPageId INT           = NULL,
    @pageKey   NVARCHAR(100) = NULL,
    @pageName  NVARCHAR(200) = NULL,
    @pageUrl   NVARCHAR(500) = NULL,
    @isActive  BIT           = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF @flag = 3
    BEGIN
        -- Dynamically list unique page prefixes from identity.Permissions
        SELECT 
            ABS(CHECKSUM(PARSENAME(p.[Name], 2))) AS [AppPageId],
            PARSENAME(p.[Name], 2) AS [PageKey],
            PARSENAME(p.[Name], 2) AS [PageName],
            NULL AS [PageUrl],
            CAST(1 AS BIT) AS [IsActive]
        FROM [identity].[Permissions] p
        WHERE p.[Name] LIKE '%.%'
        GROUP BY PARSENAME(p.[Name], 2)
        ORDER BY PARSENAME(p.[Name], 2);
    END
END
GO

-- =========================================================================
-- sp_ManageRolePermission (Targets identity schema)
-- Flags: 1=Upsert (Save), 2=GetByRole, 3=DeleteByRole
-- =========================================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ManageRolePermission]
    @flag      INT,
    @roleId    NVARCHAR(450) = NULL,
    @appPageId INT           = NULL,
    @canView   BIT           = 0,
    @canEdit   BIT           = 0,
    @canDelete BIT           = 0,
    @pageKey   NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- UPSERT permission (insert or update)
    IF @flag = 1
    BEGIN
        -- Find pageKey by matching the hashed appPageId if pageKey not provided
        IF @pageKey IS NULL AND @appPageId IS NOT NULL
        BEGIN
            SELECT TOP 1 @pageKey = PARSENAME([Name], 2)
            FROM [identity].[Permissions]
            WHERE ABS(CHECKSUM(PARSENAME([Name], 2))) = @appPageId;
        END

        IF @pageKey IS NOT NULL
        BEGIN
            -- View
            DECLARE @viewId UNIQUEIDENTIFIER = (SELECT Id FROM [identity].[Permissions] WHERE Name = @pageKey + '.View');
            IF @viewId IS NOT NULL
            BEGIN
                IF @canView = 1
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [identity].[RolePermissions] WHERE RoleId = @roleId AND PermissionId = @viewId)
                        INSERT INTO [identity].[RolePermissions] (RoleId, PermissionId) VALUES (@roleId, @viewId);
                END
                ELSE
                BEGIN
                    DELETE FROM [identity].[RolePermissions] WHERE RoleId = @roleId AND PermissionId = @viewId;
                END
            END

            -- Edit
            DECLARE @editId UNIQUEIDENTIFIER = (SELECT Id FROM [identity].[Permissions] WHERE Name = @pageKey + '.Edit');
            IF @editId IS NOT NULL
            BEGIN
                IF @canEdit = 1
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [identity].[RolePermissions] WHERE RoleId = @roleId AND PermissionId = @editId)
                        INSERT INTO [identity].[RolePermissions] (RoleId, PermissionId) VALUES (@roleId, @editId);
                END
                ELSE
                BEGIN
                    DELETE FROM [identity].[RolePermissions] WHERE RoleId = @roleId AND PermissionId = @editId;
                END
            END

            -- Delete
            DECLARE @deleteId UNIQUEIDENTIFIER = (SELECT Id FROM [identity].[Permissions] WHERE Name = @pageKey + '.Delete');
            IF @deleteId IS NOT NULL
            BEGIN
                IF @canDelete = 1
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [identity].[RolePermissions] WHERE RoleId = @roleId AND PermissionId = @deleteId)
                        INSERT INTO [identity].[RolePermissions] (RoleId, PermissionId) VALUES (@roleId, @deleteId);
                END
                ELSE
                BEGIN
                    DELETE FROM [identity].[RolePermissions] WHERE RoleId = @roleId AND PermissionId = @deleteId;
                END
            END
        END

        SELECT 1 AS Result, 'Permission saved.' AS Message;
    END

    -- GET all permissions for a role (join with identity.Permissions)
    ELSE IF @flag = 2
    BEGIN
        SELECT 
            ABS(CHECKSUM(PARSENAME(p.[Name], 2))) AS [AppPageId],
            PARSENAME(p.[Name], 2) AS [PageKey],
            PARSENAME(p.[Name], 2) AS [PageName],
            MAX(CASE WHEN PARSENAME(p.[Name], 1) = 'View'   AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END) AS [CanView],
            MAX(CASE WHEN PARSENAME(p.[Name], 1) = 'Edit'   AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END) AS [CanEdit],
            MAX(CASE WHEN PARSENAME(p.[Name], 1) = 'Delete' AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END) AS [CanDelete]
        FROM [identity].[Permissions] p
        LEFT JOIN [identity].[RolePermissions] rp 
            ON p.[Id] = rp.[PermissionId] AND rp.[RoleId] = @roleId
        WHERE p.[Name] LIKE '%.%'
        GROUP BY PARSENAME(p.[Name], 2)
        ORDER BY PARSENAME(p.[Name], 2);
    END

    -- DELETE all permissions for a role
    ELSE IF @flag = 3
    BEGIN
        DELETE FROM [identity].[RolePermissions] WHERE [RoleId] = @roleId;
        SELECT 1 AS Result, 'Permissions cleared.' AS Message;
    END
END
GO
