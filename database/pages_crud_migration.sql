USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =========================================================================
-- PAGES / MODULES REGISTRY — real CRUD
-- -------------------------------------------------------------------------
-- Until now a "page" was only implied by identity.Permissions rows named
-- "<PageKey>.View/.Edit/.Delete". This introduces identity.AppPages as the
-- authoritative registry (editable name/url/active/order). Inserting a page
-- also creates its 3 permission rows; deleting removes them (and any role
-- grants), so the existing role-permission machinery keeps working by PageKey.
-- =========================================================================

-- 1. Registry table
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'identity' AND TABLE_NAME = 'AppPages'
)
BEGIN
    CREATE TABLE [identity].[AppPages] (
        [AppPageId]    INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_AppPages] PRIMARY KEY,
        [PageKey]      NVARCHAR(100) NOT NULL,
        [PageName]     NVARCHAR(200) NOT NULL,
        [PageUrl]      NVARCHAR(500) NULL,
        [IsActive]     BIT NOT NULL CONSTRAINT [DF_AppPages_IsActive] DEFAULT (1),
        [DisplayOrder] INT NOT NULL CONSTRAINT [DF_AppPages_DisplayOrder] DEFAULT (0),
        [CreatedAt]    DATETIME NOT NULL CONSTRAINT [DF_AppPages_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [UQ_AppPages_PageKey] UNIQUE ([PageKey])
    );
    PRINT 'Table identity.AppPages created.';
END
ELSE
BEGIN
    PRINT 'Table identity.AppPages already exists.';
END
GO

-- 2. Backfill from the page keys currently implied by identity.Permissions
INSERT INTO [identity].[AppPages] ([PageKey], [PageName], [IsActive], [DisplayOrder])
SELECT k.[PageKey], k.[PageKey], 1, 0
FROM (
    SELECT DISTINCT PARSENAME(p.[Name], 2) AS [PageKey]
    FROM [identity].[Permissions] p
    WHERE p.[Name] LIKE '%.%'
) k
WHERE k.[PageKey] IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM [identity].[AppPages] ap WHERE ap.[PageKey] = k.[PageKey]);
GO

-- =========================================================================
-- 3. sp_ManagePage — CRUD
--    Flags: 1=Insert, 2=Update, 3=ListAll, 4=Delete, 5=GetById
-- =========================================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ManagePage]
    @flag        INT,
    @appPageId   INT           = NULL,
    @pageKey     NVARCHAR(100) = NULL,
    @pageName    NVARCHAR(200) = NULL,
    @pageUrl     NVARCHAR(500) = NULL,
    @isActive    BIT           = 1,
    @displayOrder INT          = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- INSERT
    IF @flag = 1
    BEGIN
        SET @pageKey = LTRIM(RTRIM(@pageKey));
        IF @pageKey IS NULL OR @pageKey = ''
        BEGIN
            SELECT -1 AS Result, 'Page key is required.' AS Message; RETURN;
        END
        IF EXISTS (SELECT 1 FROM [identity].[AppPages] WHERE [PageKey] = @pageKey)
        BEGIN
            SELECT -1 AS Result, 'A page with this key already exists.' AS Message; RETURN;
        END

        INSERT INTO [identity].[AppPages] ([PageKey], [PageName], [PageUrl], [IsActive], [DisplayOrder])
        VALUES (@pageKey, ISNULL(@pageName, @pageKey), @pageUrl, ISNULL(@isActive, 1), ISNULL(@displayOrder, 0));

        -- Create the 3 permission buckets if missing
        INSERT INTO [identity].[Permissions] ([Id], [Name], [Description])
        SELECT NEWID(), @pageKey + '.' + a.[Action], @pageKey + ' ' + a.[Action]
        FROM (VALUES ('View'), ('Edit'), ('Delete')) a([Action])
        WHERE NOT EXISTS (SELECT 1 FROM [identity].[Permissions] p WHERE p.[Name] = @pageKey + '.' + a.[Action]);

        SELECT 1 AS Result, 'Page created successfully.' AS Message;
    END

    -- UPDATE (supports rename of PageKey, which renames the permission rows;
    -- PermissionId stays stable so existing role grants are preserved)
    ELSE IF @flag = 2
    BEGIN
        DECLARE @oldKey NVARCHAR(100) = (SELECT [PageKey] FROM [identity].[AppPages] WHERE [AppPageId] = @appPageId);
        IF @oldKey IS NULL
        BEGIN
            SELECT -1 AS Result, 'Page not found.' AS Message; RETURN;
        END

        SET @pageKey = LTRIM(RTRIM(@pageKey));
        IF @pageKey IS NULL OR @pageKey = '' SET @pageKey = @oldKey;

        IF @pageKey <> @oldKey AND EXISTS (SELECT 1 FROM [identity].[AppPages] WHERE [PageKey] = @pageKey AND [AppPageId] <> @appPageId)
        BEGIN
            SELECT -1 AS Result, 'Another page already uses this key.' AS Message; RETURN;
        END

        UPDATE [identity].[AppPages]
        SET [PageKey]      = @pageKey,
            [PageName]     = ISNULL(@pageName, @pageKey),
            [PageUrl]      = @pageUrl,
            [IsActive]     = ISNULL(@isActive, 1),
            [DisplayOrder] = ISNULL(@displayOrder, 0)
        WHERE [AppPageId] = @appPageId;

        IF @pageKey <> @oldKey
        BEGIN
            UPDATE [identity].[Permissions]
            SET [Name] = @pageKey + '.' + PARSENAME([Name], 1)
            WHERE PARSENAME([Name], 2) = @oldKey AND [Name] LIKE '%.%';
        END

        SELECT 1 AS Result, 'Page updated successfully.' AS Message;
    END

    -- LIST ALL
    ELSE IF @flag = 3
    BEGIN
        SELECT [AppPageId], [PageKey], [PageName], [PageUrl], [IsActive], [DisplayOrder]
        FROM [identity].[AppPages]
        ORDER BY [DisplayOrder], [PageName];
    END

    -- DELETE (page + its permission rows + any role grants of them)
    ELSE IF @flag = 4
    BEGIN
        DECLARE @delKey NVARCHAR(100) = (SELECT [PageKey] FROM [identity].[AppPages] WHERE [AppPageId] = @appPageId);
        IF @delKey IS NULL
        BEGIN
            SELECT -1 AS Result, 'Page not found.' AS Message; RETURN;
        END

        DELETE rp
        FROM [identity].[RolePermissions] rp
        INNER JOIN [identity].[Permissions] p ON p.[Id] = rp.[PermissionId]
        WHERE PARSENAME(p.[Name], 2) = @delKey AND p.[Name] LIKE '%.%';

        DELETE FROM [identity].[Permissions]
        WHERE PARSENAME([Name], 2) = @delKey AND [Name] LIKE '%.%';

        DELETE FROM [identity].[AppPages] WHERE [AppPageId] = @appPageId;

        SELECT 1 AS Result, 'Page deleted successfully.' AS Message;
    END

    -- GET BY ID
    ELSE IF @flag = 5
    BEGIN
        SELECT [AppPageId], [PageKey], [PageName], [PageUrl], [IsActive], [DisplayOrder]
        FROM [identity].[AppPages]
        WHERE [AppPageId] = @appPageId;
    END
END
GO

-- =========================================================================
-- 4. sp_ManageRolePermission — switch AppPageId to the real AppPages id
--    (keeps the role-permission screen consistent with the new registry).
--    Flags: 1=Upsert, 2=GetByRole, 3=DeleteByRole
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

    IF @flag = 1
    BEGIN
        IF @pageKey IS NULL AND @appPageId IS NOT NULL
            SELECT @pageKey = [PageKey] FROM [identity].[AppPages] WHERE [AppPageId] = @appPageId;

        IF @pageKey IS NOT NULL
        BEGIN
            DECLARE @viewId UNIQUEIDENTIFIER = (SELECT Id FROM [identity].[Permissions] WHERE Name = @pageKey + '.View');
            IF @viewId IS NOT NULL
            BEGIN
                IF @canView = 1
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [identity].[RolePermissions] WHERE RoleId = @roleId AND PermissionId = @viewId)
                        INSERT INTO [identity].[RolePermissions] (RoleId, PermissionId) VALUES (@roleId, @viewId);
                END
                ELSE DELETE FROM [identity].[RolePermissions] WHERE RoleId = @roleId AND PermissionId = @viewId;
            END

            DECLARE @editId UNIQUEIDENTIFIER = (SELECT Id FROM [identity].[Permissions] WHERE Name = @pageKey + '.Edit');
            IF @editId IS NOT NULL
            BEGIN
                IF @canEdit = 1
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [identity].[RolePermissions] WHERE RoleId = @roleId AND PermissionId = @editId)
                        INSERT INTO [identity].[RolePermissions] (RoleId, PermissionId) VALUES (@roleId, @editId);
                END
                ELSE DELETE FROM [identity].[RolePermissions] WHERE RoleId = @roleId AND PermissionId = @editId;
            END

            DECLARE @deleteId UNIQUEIDENTIFIER = (SELECT Id FROM [identity].[Permissions] WHERE Name = @pageKey + '.Delete');
            IF @deleteId IS NOT NULL
            BEGIN
                IF @canDelete = 1
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [identity].[RolePermissions] WHERE RoleId = @roleId AND PermissionId = @deleteId)
                        INSERT INTO [identity].[RolePermissions] (RoleId, PermissionId) VALUES (@roleId, @deleteId);
                END
                ELSE DELETE FROM [identity].[RolePermissions] WHERE RoleId = @roleId AND PermissionId = @deleteId;
            END
        END

        SELECT 1 AS Result, 'Permission saved.' AS Message;
    END

    ELSE IF @flag = 2
    BEGIN
        SELECT
            ap.[AppPageId] AS [AppPageId],
            ap.[PageKey]   AS [PageKey],
            ap.[PageName]  AS [PageName],
            MAX(CASE WHEN PARSENAME(p.[Name], 1) = 'View'   AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END) AS [CanView],
            MAX(CASE WHEN PARSENAME(p.[Name], 1) = 'Edit'   AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END) AS [CanEdit],
            MAX(CASE WHEN PARSENAME(p.[Name], 1) = 'Delete' AND rp.[RoleId] IS NOT NULL THEN 1 ELSE 0 END) AS [CanDelete]
        FROM [identity].[AppPages] ap
        LEFT JOIN [identity].[Permissions] p ON PARSENAME(p.[Name], 2) = ap.[PageKey] AND p.[Name] LIKE '%.%'
        LEFT JOIN [identity].[RolePermissions] rp ON rp.[PermissionId] = p.[Id] AND rp.[RoleId] = @roleId
        WHERE ap.[IsActive] = 1
        GROUP BY ap.[AppPageId], ap.[PageKey], ap.[PageName], ap.[DisplayOrder]
        ORDER BY ap.[DisplayOrder], ap.[PageName];
    END

    ELSE IF @flag = 3
    BEGIN
        DELETE FROM [identity].[RolePermissions] WHERE [RoleId] = @roleId;
        SELECT 1 AS Result, 'Permissions cleared.' AS Message;
    END
END
GO

PRINT 'Pages CRUD migration applied.';
GO
