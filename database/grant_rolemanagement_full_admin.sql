USE [NatureKnit];   -- <-- change to your app's DB if different
GO

-- =========================================================================
-- Give full RoleManagement (View/Edit/Delete) access to the Admin role,
-- then report which roles/users actually hold it (to confirm targeting).
-- Idempotent.
-- =========================================================================

-- 1. Ensure the RoleManagement permission buckets exist
INSERT INTO [identity].[Permissions] ([Id], [Name], [Description])
SELECT NEWID(), v.[Name], v.[Name]
FROM (VALUES ('RoleManagement.View'), ('RoleManagement.Edit'), ('RoleManagement.Delete')) v([Name])
WHERE NOT EXISTS (SELECT 1 FROM [identity].[Permissions] p WHERE p.[Name] = v.[Name]);

-- 2. Grant them to the 'Admin' role
INSERT INTO [identity].[RolePermissions] ([RoleId], [PermissionId])
SELECT r.[Id], p.[Id]
FROM [identity].[Roles] r
CROSS JOIN [identity].[Permissions] p
WHERE r.[Name] = 'Admin'
  AND p.[Name] IN ('RoleManagement.View', 'RoleManagement.Edit', 'RoleManagement.Delete')
  AND NOT EXISTS (
        SELECT 1 FROM [identity].[RolePermissions] rp
        WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]
  );

PRINT 'Granted RoleManagement View/Edit/Delete to the Admin role.';
GO

-- 3. DIAGNOSTIC — which roles currently hold RoleManagement.Edit?
PRINT '--- Roles holding RoleManagement.Edit ---';
SELECT r.[Name] AS RoleName
FROM [identity].[Roles] r
INNER JOIN [identity].[RolePermissions] rp ON rp.[RoleId] = r.[Id]
INNER JOIN [identity].[Permissions] p ON p.[Id] = rp.[PermissionId]
WHERE p.[Name] = 'RoleManagement.Edit'
ORDER BY r.[Name];

-- 4. DIAGNOSTIC — your users and their roles (find your logged-in account's role)
PRINT '--- Users and their roles ---';
SELECT u.[Email], u.[FirstName] + ' ' + u.[LastName] AS FullName, r.[Name] AS RoleName
FROM [identity].[Users] u
LEFT JOIN [identity].[AspNetUserRoles] ur ON ur.[UserId] = u.[Id]
LEFT JOIN [identity].[Roles] r ON r.[Id] = ur.[RoleId]
ORDER BY u.[Email];
GO
