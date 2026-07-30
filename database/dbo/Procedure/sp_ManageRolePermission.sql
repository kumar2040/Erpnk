/*==============================================================================
  sp_ManageRolePermission  —  per-role View/Edit/Delete grants against
  identity.AppPages, keyed through identity.Permissions ("<PageKey>.View" etc).

  Flags
    1  Upsert       — set/clear a role's View/Edit/Delete for one page
    2  GetByRole    — the role-permission grid's data source
    3  DeleteByRole — clears every grant for a role

  Source: database/pages_crud_migration.sql (not yet confirmed deployed —
  this mirror reflects that file's current content).
==============================================================================*/
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
