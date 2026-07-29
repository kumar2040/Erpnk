/*==============================================================================
  sp_ManagePage  —  CRUD for identity.AppPages (the page/module registry behind
  Pages Management and the sidebar menu), plus a lookup of identity.Menu.

  Flags
    1  Insert   — creates the page + its 3 permission rows (View/Edit/Delete)
    2  Update   — supports PageKey rename (renames the permission rows too;
                  PermissionId stays stable so existing role grants survive)
    3  ListAll  — every page, MenuTitle joined in so callers don't need to
                  join identity.Menu themselves
    4  Delete   — page + its permission rows + any role grants of them
    5  GetById
    6  ListMenus — identity.Menu rows, for the "which menu does this page
                   nest under" picker

  Source: database/pages_crud_migration.sql (not yet confirmed deployed —
  this mirror reflects that file's current content).
==============================================================================*/
CREATE OR ALTER PROCEDURE [dbo].[sp_ManagePage]
    @flag         INT,
    @appPageId    INT           = NULL,
    @pageKey      NVARCHAR(100) = NULL,
    @pageName     NVARCHAR(200) = NULL,
    @pageUrl      NVARCHAR(500) = NULL,
    @isActive     BIT           = 1,
    @displayOrder INT           = 0,
    @icon         NVARCHAR(50)  = NULL,
    @menuId       INT           = NULL
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
        IF @menuId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [identity].[Menu] WHERE [Id] = @menuId)
        BEGIN
            SELECT -1 AS Result, 'Menu not found.' AS Message; RETURN;
        END

        INSERT INTO [identity].[AppPages] ([PageKey], [PageName], [PageUrl], [IsActive], [DisplayOrder], [Icon], [MenuId])
        VALUES (@pageKey, ISNULL(@pageName, @pageKey), @pageUrl, ISNULL(@isActive, 1), ISNULL(@displayOrder, 0), @icon, @menuId);

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
        IF @menuId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [identity].[Menu] WHERE [Id] = @menuId)
        BEGIN
            SELECT -1 AS Result, 'Menu not found.' AS Message; RETURN;
        END

        UPDATE [identity].[AppPages]
        SET [PageKey]      = @pageKey,
            [PageName]     = ISNULL(@pageName, @pageKey),
            [PageUrl]      = @pageUrl,
            [IsActive]     = ISNULL(@isActive, 1),
            [DisplayOrder] = ISNULL(@displayOrder, 0),
            [Icon]         = @icon,
            [MenuId]       = @menuId
        WHERE [AppPageId] = @appPageId;

        IF @pageKey <> @oldKey
        BEGIN
            UPDATE [identity].[Permissions]
            SET [Name] = @pageKey + '.' + PARSENAME([Name], 1)
            WHERE PARSENAME([Name], 2) = @oldKey AND [Name] LIKE '%.%';
        END

        SELECT 1 AS Result, 'Page updated successfully.' AS Message;
    END

    -- LIST ALL (MenuTitle included so the grid/menu don't need a client-side join)
    ELSE IF @flag = 3
    BEGIN
        SELECT ap.[AppPageId], ap.[PageKey], ap.[PageName], ap.[PageUrl], ap.[IsActive], ap.[DisplayOrder],
               ap.[Icon], ap.[MenuId], m.[Title] AS [MenuTitle]
        FROM [identity].[AppPages] ap
        LEFT JOIN [identity].[Menu] m ON m.[Id] = ap.[MenuId]
        ORDER BY ISNULL(ap.[MenuId], 0), ap.[DisplayOrder], ap.[PageName];
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
        SELECT [AppPageId], [PageKey], [PageName], [PageUrl], [IsActive], [DisplayOrder], [Icon], [MenuId]
        FROM [identity].[AppPages]
        WHERE [AppPageId] = @appPageId;
    END

    -- LIST MENUS (categories a page can nest under, for the Pages Management picker)
    ELSE IF @flag = 6
    BEGIN
        SELECT [Id], [Title] FROM [identity].[Menu] ORDER BY [Title];
    END
END
GO
