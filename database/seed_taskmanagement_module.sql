USE [NatureKnit];   -- <-- change to your app's DB if different
GO

-- =========================================================================
-- Register the Task Management screen (/task) as a permission-controlled
-- module so it shows in Role Management's Page Permissions grid, grant it to
-- the Admin role, and give it a URL/order for the landing resolver.
-- Idempotent — safe to re-run.
-- =========================================================================
DECLARE @pageKey NVARCHAR(100) = 'TaskManagement';

-- 1. Registry row
IF NOT EXISTS (SELECT 1 FROM [identity].[AppPages] WHERE [PageKey] = @pageKey)
    INSERT INTO [identity].[AppPages] ([PageKey], [PageName], [PageUrl], [IsActive], [DisplayOrder])
    VALUES (@pageKey, 'Task Management', '/task', 1, 70);
ELSE
    UPDATE [identity].[AppPages] SET [PageUrl] = '/task'
    WHERE [PageKey] = @pageKey AND ([PageUrl] IS NULL OR [PageUrl] = '');

-- 2. Permission buckets (View/Edit/Delete) — the page is read-only today, but we
--    create all three so the grid is consistent and future actions can be gated.
INSERT INTO [identity].[Permissions] ([Id], [Name], [Description])
SELECT NEWID(), @pageKey + '.' + a.[Action], 'Task Management ' + a.[Action]
FROM (VALUES ('View'), ('Edit'), ('Delete')) a([Action])
WHERE NOT EXISTS (SELECT 1 FROM [identity].[Permissions] p WHERE p.[Name] = @pageKey + '.' + a.[Action]);

-- 3. Grant View/Edit/Delete to the Admin role
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

PRINT 'TaskManagement module registered and granted to Admin.';
GO
