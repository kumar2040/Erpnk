USE [NatureKnit];   -- <-- change to your app's DB if different
GO

-- =========================================================================
-- Grant the Admin role View / Edit / Delete on EVERY registered page.
-- 1. Ensure each AppPages row has its 3 permission buckets.
-- 2. Grant all of them to the 'Admin' role.
-- Idempotent — safe to re-run.
-- =========================================================================

-- 1. Create any missing PageKey.View / .Edit / .Delete permission rows
INSERT INTO [identity].[Permissions] ([Id], [Name], [Description])
SELECT NEWID(), ap.[PageKey] + '.' + a.[Action], ap.[PageName] + ' ' + a.[Action]
FROM [identity].[AppPages] ap
CROSS JOIN (VALUES ('View'), ('Edit'), ('Delete')) a([Action])
WHERE NOT EXISTS (
    SELECT 1 FROM [identity].[Permissions] p
    WHERE p.[Name] = ap.[PageKey] + '.' + a.[Action]
);

-- 2. Grant every permission to the Admin role
INSERT INTO [identity].[RolePermissions] ([RoleId], [PermissionId])
SELECT r.[Id], p.[Id]
FROM [identity].[Roles] r
CROSS JOIN [identity].[Permissions] p
WHERE r.[Name] = 'Admin'
  AND NOT EXISTS (
        SELECT 1 FROM [identity].[RolePermissions] rp
        WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]
  );

DECLARE @granted INT = (
    SELECT COUNT(*) FROM [identity].[RolePermissions] rp
    INNER JOIN [identity].[Roles] r ON r.[Id] = rp.[RoleId]
    WHERE r.[Name] = 'Admin'
);
PRINT 'Admin role now holds ' + CAST(@granted AS VARCHAR) + ' permissions (all pages, View/Edit/Delete).';
GO
