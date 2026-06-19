USE [NatureKnit];   -- <-- change to your app's DB if different
GO

-- =========================================================================
-- Give YOUR login account full access to every page, regardless of role name.
-- Set @myEmail to the email you log in with (header shows "System Admin";
-- the seeded admin is admin@nkplm.erp — change if yours differs).
-- Idempotent.
-- =========================================================================
DECLARE @myEmail NVARCHAR(256) = 'admin@nkplm.erp';   -- <<< SET YOUR LOGIN EMAIL

-- 0. Confirm the account + its role(s)
PRINT '--- Your account & roles ---';
SELECT u.[Email], u.[FirstName] + ' ' + u.[LastName] AS FullName, r.[Name] AS RoleName
FROM [identity].[Users] u
LEFT JOIN [identity].[AspNetUserRoles] ur ON ur.[UserId] = u.[Id]
LEFT JOIN [identity].[Roles] r ON r.[Id] = ur.[RoleId]
WHERE u.[Email] = @myEmail;

-- 1. Ensure every registered page has its View/Edit/Delete permission rows
INSERT INTO [identity].[Permissions] ([Id], [Name], [Description])
SELECT NEWID(), ap.[PageKey] + '.' + a.[Action], ap.[PageName] + ' ' + a.[Action]
FROM [identity].[AppPages] ap
CROSS JOIN (VALUES ('View'), ('Edit'), ('Delete')) a([Action])
WHERE NOT EXISTS (SELECT 1 FROM [identity].[Permissions] p WHERE p.[Name] = ap.[PageKey] + '.' + a.[Action]);

-- 1b. Also make sure RoleManagement buckets exist even if it's not in AppPages yet
INSERT INTO [identity].[Permissions] ([Id], [Name], [Description])
SELECT NEWID(), v.[Name], v.[Name]
FROM (VALUES ('RoleManagement.View'),('RoleManagement.Edit'),('RoleManagement.Delete'),
             ('PagesManagement.View'),('PagesManagement.Edit'),('PagesManagement.Delete')) v([Name])
WHERE NOT EXISTS (SELECT 1 FROM [identity].[Permissions] p WHERE p.[Name] = v.[Name]);

-- 2. Grant ALL permissions to EVERY role your account holds
INSERT INTO [identity].[RolePermissions] ([RoleId], [PermissionId])
SELECT DISTINCT ur.[RoleId], p.[Id]
FROM [identity].[AspNetUserRoles] ur
INNER JOIN [identity].[Users] u ON u.[Id] = ur.[UserId]
CROSS JOIN [identity].[Permissions] p
WHERE u.[Email] = @myEmail
  AND NOT EXISTS (
        SELECT 1 FROM [identity].[RolePermissions] rp
        WHERE rp.[RoleId] = ur.[RoleId] AND rp.[PermissionId] = p.[Id]
  );

PRINT 'Granted all permissions to every role held by ' + @myEmail + '. Log out and back in.';
GO
