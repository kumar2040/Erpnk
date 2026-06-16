USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =========================================================================
-- Create tables for Zero Trust Role Management
-- Run this ONCE to set up the schema
-- =========================================================================

-- 1. AppPages: Register all pages/modules in the system
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AppPages')
BEGIN
    CREATE TABLE [dbo].[AppPages] (
        [AppPageId]   INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [PageKey]     NVARCHAR(100) NOT NULL UNIQUE,   -- e.g. 'OrderPlanning'
        [PageName]    NVARCHAR(200) NOT NULL,           -- e.g. 'Order Planning'
        [PageUrl]     NVARCHAR(500) NULL,               -- e.g. '/order-planning'
        [IsActive]    BIT           NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'AppPages table created.';
END
GO

-- 2. AppRoles: Custom role definitions with optional gauge restriction
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AppRoles')
BEGIN
    CREATE TABLE [dbo].[AppRoles] (
        [RoleId]        INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RoleName]      NVARCHAR(100) NOT NULL UNIQUE,
        [Description]   NVARCHAR(500) NULL,
        [AssignedGauge] NVARCHAR(100) NULL,  -- NULL = no restriction (Admin), value = restricted to that gauge/factory
        [IsActive]      BIT           NOT NULL DEFAULT 1,
        [CreatedDate]   DATETIME      NOT NULL DEFAULT GETDATE(),
        [ModifiedDate]  DATETIME      NULL
    );
    PRINT 'AppRoles table created.';
END
GO

-- 3. RolePagePermissions: Per-role, per-page access flags
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RolePagePermissions')
BEGIN
    CREATE TABLE [dbo].[RolePagePermissions] (
        [PermissionId] INT  IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RoleId]       INT  NOT NULL,
        [AppPageId]    INT  NOT NULL,
        [CanView]      BIT  NOT NULL DEFAULT 0,
        [CanEdit]      BIT  NOT NULL DEFAULT 0,
        [CanDelete]    BIT  NOT NULL DEFAULT 0,
        [ModifiedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [FK_RolePagePermissions_AppRoles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AppRoles]([RoleId]),
        CONSTRAINT [FK_RolePagePermissions_AppPages] FOREIGN KEY ([AppPageId]) REFERENCES [dbo].[AppPages]([AppPageId]),
        CONSTRAINT [UQ_RolePagePermissions] UNIQUE ([RoleId], [AppPageId])
    );
    PRINT 'RolePagePermissions table created.';
END
GO

-- 4. UserRoles: User to role assignment
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'UserAppRoles')
BEGIN
    CREATE TABLE [dbo].[UserAppRoles] (
        [UserRoleId]   INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId]       NVARCHAR(450) NOT NULL,   -- FK to AspNetUsers.Id
        [RoleId]       INT           NOT NULL,
        [AssignedDate] DATETIME      NOT NULL DEFAULT GETDATE(),
        [AssignedBy]   NVARCHAR(450) NULL,
        CONSTRAINT [FK_UserAppRoles_AppRoles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AppRoles]([RoleId]),
        CONSTRAINT [UQ_UserAppRoles] UNIQUE ([UserId], [RoleId])
    );
    PRINT 'UserAppRoles table created.';
END
GO

-- 5. AuditLog: Log every access attempt for Zero Trust compliance
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PermissionAuditLog')
BEGIN
    CREATE TABLE [dbo].[PermissionAuditLog] (
        [AuditId]    BIGINT        IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId]     NVARCHAR(450) NOT NULL,
        [PageKey]    NVARCHAR(100) NOT NULL,
        [Action]     NVARCHAR(50)  NOT NULL,  -- 'View', 'Edit', 'Delete'
        [IsGranted]  BIT           NOT NULL,
        [IpAddress]  NVARCHAR(50)  NULL,
        [LogDate]    DATETIME      NOT NULL DEFAULT GETDATE()
    );
    PRINT 'PermissionAuditLog table created.';
END
GO

-- Seed default pages
IF NOT EXISTS (SELECT 1 FROM [dbo].[AppPages] WHERE [PageKey] = 'Dashboard')
BEGIN
    INSERT INTO [dbo].[AppPages] ([PageKey], [PageName], [PageUrl]) VALUES
    ('Dashboard',      'Main Dashboard',    '/dashboard'),
    ('OrderPlanning',  'Order Planning',    '/order-planning'),
    ('Orders',         'Orders Dashboard',  '/orders'),
    ('Users',          'User Management',   '/users'),
    ('RoleManagement', 'Role Management',   '/role-management'),
    ('Reports',        'Reports',           '/reports');
    PRINT 'Default AppPages seeded.';
END
GO

-- Seed Admin role (no gauge restriction)
IF NOT EXISTS (SELECT 1 FROM [dbo].[AppRoles] WHERE [RoleName] = 'Admin')
BEGIN
    INSERT INTO [dbo].[AppRoles] ([RoleName], [Description], [AssignedGauge])
    VALUES ('Admin', 'Full system access with no data restrictions', NULL);
    PRINT 'Admin role seeded.';
END
GO

PRINT 'Zero Trust Role Management schema setup complete.';
