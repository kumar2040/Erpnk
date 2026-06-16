USE [NatureKnit]
GO

-- 1. Add AssignedGauge to identity.Roles if it doesn't exist
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'identity' AND TABLE_NAME = 'Roles' AND COLUMN_NAME = 'AssignedGauge'
)
BEGIN
    ALTER TABLE [identity].[Roles] ADD [AssignedGauge] NVARCHAR(100) NULL;
    PRINT 'AssignedGauge column added to identity.Roles table.';
END
GO

-- 2. Seed permissions in identity.Permissions
CREATE TABLE #TempPermissions (Name NVARCHAR(100), Description NVARCHAR(500));

INSERT INTO #TempPermissions (Name, Description) VALUES
('Dashboard.View', 'Permission to View Dashboard'),
('Dashboard.Edit', 'Permission to Edit Dashboard'),
('Dashboard.Delete', 'Permission to Delete Dashboard'),
('OrderPlanning.View', 'Permission to View Order Planning'),
('OrderPlanning.Edit', 'Permission to Edit Order Planning'),
('OrderPlanning.Delete', 'Permission to Delete Order Planning'),
('Orders.View', 'Permission to View Orders Dashboard'),
('Orders.Edit', 'Permission to Edit Orders Dashboard'),
('Orders.Delete', 'Permission to Delete Orders Dashboard'),
('Users.View', 'Permission to View Users'),
('Users.Edit', 'Permission to Edit Users'),
('Users.Delete', 'Permission to Delete Users'),
('RoleManagement.View', 'Permission to View Role Management'),
('RoleManagement.Edit', 'Permission to Edit Role Management'),
('RoleManagement.Delete', 'Permission to Delete Role Management'),
('Reports.View', 'Permission to View Reports'),
('Reports.Edit', 'Permission to Edit Reports'),
('Reports.Delete', 'Permission to Delete Reports'),
('Products.View', 'Permission to View Products'),
('Products.Edit', 'Permission to Edit Products'),
('Products.Delete', 'Permission to Delete Products'),
('Tenants.View', 'Permission to View Tenants'),
('Tenants.Edit', 'Permission to Edit Tenants'),
('Tenants.Delete', 'Permission to Delete Tenants');

INSERT INTO [identity].[Permissions] (Id, Name, Description)
SELECT NEWID(), t.Name, t.Description
FROM #TempPermissions t
LEFT JOIN [identity].[Permissions] p ON t.Name = p.Name
WHERE p.Id IS NULL;

DROP TABLE #TempPermissions;
PRINT 'Permissions seeded successfully.';
GO

-- 3. Clean up custom tables we don't need anymore
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'PermissionAuditLog')
    DROP TABLE [dbo].[PermissionAuditLog];
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'UserAppRoles')
    DROP TABLE [dbo].[UserAppRoles];
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'RolePagePermissions')
    DROP TABLE [dbo].[RolePagePermissions];
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'AppRoles')
    DROP TABLE [dbo].[AppRoles];
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'AppPages')
    DROP TABLE [dbo].[AppPages];
PRINT 'Old custom dbo tables dropped.';
GO
