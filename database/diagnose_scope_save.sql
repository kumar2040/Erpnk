-- =========================================================================
-- DIAGNOSE: why user scope isn't saving.
-- Run this in SSMS against the SAME database the app connects to
-- (check your user-secrets ConnectionStrings:DefaultConnection for the DB name).
-- Read-only — it only reports state.
-- =========================================================================
USE [NatureKnit];   -- <-- change to your app's DB if different (e.g. NkplmErp)
GO

PRINT '--- 1. Does the scope table exist? ---';
SELECT CASE WHEN OBJECT_ID('identity.UserRoleGauges') IS NOT NULL
            THEN 'YES — identity.UserRoleGauges exists'
            ELSE 'NO  — table MISSING (run multigauge_migration.sql)' END AS UserRoleGauges_Table;

PRINT '--- 2. Does the table-valued type exist? ---';
SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.types WHERE name='UserScopeList' AND is_table_type=1)
            THEN 'YES — dbo.UserScopeList type exists'
            ELSE 'NO  — type MISSING (run multigauge_migration.sql)' END AS UserScopeList_Type;

PRINT '--- 3. Does sp_AssignUserRole accept the @scopes TVP? ---';
SELECT ISNULL(
    (SELECT STRING_AGG(name, ', ') FROM sys.parameters WHERE object_id = OBJECT_ID('dbo.sp_AssignUserRole')),
    '<<sp_AssignUserRole NOT FOUND>>') AS sp_AssignUserRole_Params;
-- Expected to include: @flag, @userId, @roleId, @assignedBy, @scopes
-- If @scopes is MISSING, an older proc overwrote it -> re-run multigauge_migration.sql LAST.

PRINT '--- 4. Current saved scope rows (most recent assignments) ---';
IF OBJECT_ID('identity.UserRoleGauges') IS NOT NULL
    SELECT TOP 50 g.[UserId], u.[Email], g.[RoleId], r.[Name] AS RoleName, g.[KnitType], g.[GaugeValue]
    FROM [identity].[UserRoleGauges] g
    LEFT JOIN [identity].[Users] u ON u.[Id] = g.[UserId]
    LEFT JOIN [identity].[Roles] r ON r.[Id] = g.[RoleId]
    ORDER BY u.[Email];
ELSE
    SELECT 'table missing' AS info;
GO
