USE [NatureKnit]
GO

-- =========================================================================
-- Register the "Pages / Modules" screen itself as a permission-controlled
-- module so it shows up in Role Management's Page Permissions grid, and grant
-- it to the Admin role so administrators keep access.
-- Idempotent — safe to re-run.
-- =========================================================================

DECLARE @pageKey NVARCHAR(100) = 'PagesManagement';

-- 1. Registry row
IF NOT EXISTS (SELECT 1 FROM [identity].[AppPages] WHERE [PageKey] = @pageKey)
BEGIN
    INSERT INTO [identity].[AppPages] ([PageKey], [PageName], [PageUrl], [IsActive], [DisplayOrder])
    VALUES (@pageKey, 'Pages / Modules', '/pages-management', 1, 100);
    PRINT 'AppPages row for PagesManagement created.';
END

-- 2. Permission buckets (PagesManagement.View / .Edit / .Delete)
INSERT INTO [identity].[Permissions] ([Id], [Name], [Description])
SELECT NEWID(), @pageKey + '.' + a.[Action], 'Pages / Modules ' + a.[Action]
FROM (VALUES ('View'), ('Edit'), ('Delete')) a([Action])
WHERE NOT EXISTS (SELECT 1 FROM [identity].[Permissions] p WHERE p.[Name] = @pageKey + '.' + a.[Action]);

-- 3. Grant View/Edit/Delete to the Admin role so admins keep managing pages
INSERT INTO [identity].[RolePermissions] ([RoleId], [PermissionId])
SELECT r.[Id], p.[Id]
FROM [identity].[Roles] r
CROSS JOIN [identity].[Permissions] p
WHERE r.[Name] = 'Admin'
  AND p.[Name] IN (@pageKey + '.View', @pageKey + '.Edit', @pageKey + '.Delete')
  AND NOT EXISTS (
        SELECT 1 FROM [identity].[RolePermissions] rp
        WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]
  );

PRINT 'PagesManagement module registered and granted to Admin.';
GO
