USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =========================================================================
-- sp_ManageRole (targets identity schema tables)
-- Flags: 1=Insert, 2=Update, 3=Delete, 4=GetAll, 5=GetById
-- =========================================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ManageRole]
    @flag         INT,
    @roleId       NVARCHAR(450) = NULL,
    @roleName     NVARCHAR(100) = NULL,
    @description  NVARCHAR(500) = NULL,
    @assignedGauge NVARCHAR(100) = NULL,
    @isActive     BIT           = 1,      -- placeholder for backward compatibility
    @userId       NVARCHAR(450) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- INSERT new role
    IF @flag = 1
    BEGIN
        IF EXISTS (SELECT 1 FROM [identity].[Roles] WHERE [Name] = @roleName)
        BEGIN
            SELECT -1 AS Result, 'Role name already exists.' AS Message;
            RETURN;
        END

        DECLARE @newId NVARCHAR(450) = CAST(NEWID() AS NVARCHAR(450));

        INSERT INTO [identity].[Roles] ([Id], [Name], [NormalizedName], [Description], [AssignedGauge], [CreatedAt], [ConcurrencyStamp])
        VALUES (@newId, @roleName, UPPER(@roleName), ISNULL(@description, ''), @assignedGauge, GETDATE(), NEWID());

        SELECT 1 AS Result, 'Role created successfully.' AS Message;
    END

    -- UPDATE existing role
    ELSE IF @flag = 2
    BEGIN
        UPDATE [identity].[Roles]
        SET [Name]          = @roleName,
            [NormalizedName]= UPPER(@roleName),
            [Description]   = ISNULL(@description, ''),
            [AssignedGauge] = @assignedGauge
        WHERE [Id] = @roleId;

        SELECT 1 AS Result, 'Role updated successfully.' AS Message;
    END

    -- DELETE role
    ELSE IF @flag = 3
    BEGIN
        -- Remove all mapped permissions
        DELETE FROM [identity].[RolePermissions] WHERE [RoleId] = @roleId;
        -- Remove all user mappings
        DELETE FROM [identity].[AspNetUserRoles] WHERE [RoleId] = @roleId;
        -- Delete the role itself
        DELETE FROM [identity].[Roles] WHERE [Id] = @roleId;

        SELECT 1 AS Result, 'Role deleted successfully.' AS Message;
    END

    -- GET ALL roles
    ELSE IF @flag = 4
    BEGIN
        SELECT 
            r.[Id] AS [RoleId],
            r.[Name] AS [RoleName],
            r.[Description],
            r.[AssignedGauge],
            CAST(1 AS BIT) AS [IsActive],
            r.[CreatedAt] AS [CreatedDate],
            COUNT(DISTINCT ua.[UserId]) AS [UserCount],
            COUNT(DISTINCT rp.[PermissionId]) AS [PageCount]
        FROM [identity].[Roles] r
        LEFT JOIN [identity].[AspNetUserRoles] ua ON r.[Id] = ua.[RoleId]
        LEFT JOIN [identity].[RolePermissions] rp ON r.[Id] = rp.[RoleId]
        GROUP BY r.[Id], r.[Name], r.[Description], r.[AssignedGauge], r.[CreatedAt]
        ORDER BY r.[Name];
    END

    -- GET BY ID
    ELSE IF @flag = 5
    BEGIN
        SELECT 
            [Id] AS [RoleId], 
            [Name] AS [RoleName], 
            [Description], 
            [AssignedGauge], 
            CAST(1 AS BIT) AS [IsActive], 
            [CreatedAt] AS [CreatedDate]
        FROM [identity].[Roles]
        WHERE [Id] = @roleId;
    END
END
GO
