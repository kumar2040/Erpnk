USE [NatureKnit]
GO

-- =========================================================================
-- PER-USER LANDING PAGE
-- A user lands on the first page (lowest DisplayOrder) that is active, has a
-- URL, and that they can View. Admins land on the first active page with a URL.
-- Admins control routing/order via the Pages / Modules screen.
-- =========================================================================

-- 1. Give the known modules their routes + a sensible order (idempotent).
UPDATE [identity].[AppPages] SET [PageUrl] = '/main-dashboard',   [DisplayOrder] = 1   WHERE [PageKey] = 'Dashboard'        AND ([PageUrl] IS NULL OR [PageUrl] = '');
UPDATE [identity].[AppPages] SET [PageUrl] = '/orders-dashboard', [DisplayOrder] = 5   WHERE [PageKey] = 'Orders'           AND ([PageUrl] IS NULL OR [PageUrl] = '');
UPDATE [identity].[AppPages] SET [PageUrl] = '/order-planning',   [DisplayOrder] = 10  WHERE [PageKey] = 'OrderPlanning'     AND ([PageUrl] IS NULL OR [PageUrl] = '');
UPDATE [identity].[AppPages] SET [PageUrl] = '/for-master-planing',[DisplayOrder] = 20 WHERE [PageKey] = 'ForMasterPlaning'  AND ([PageUrl] IS NULL OR [PageUrl] = '');
UPDATE [identity].[AppPages] SET [PageUrl] = '/knit-gantt-chart', [DisplayOrder] = 30  WHERE [PageKey] = 'KnitGanttChart'    AND ([PageUrl] IS NULL OR [PageUrl] = '');
UPDATE [identity].[AppPages] SET [PageUrl] = '/planing-report',   [DisplayOrder] = 40  WHERE [PageKey] = 'PlaningReport'     AND ([PageUrl] IS NULL OR [PageUrl] = '');
UPDATE [identity].[AppPages] SET [PageUrl] = '/manage-users',     [DisplayOrder] = 50  WHERE [PageKey] = 'Users'             AND ([PageUrl] IS NULL OR [PageUrl] = '');
UPDATE [identity].[AppPages] SET [PageUrl] = '/role-management',  [DisplayOrder] = 60  WHERE [PageKey] = 'RoleManagement'    AND ([PageUrl] IS NULL OR [PageUrl] = '');
GO

-- 2. Landing resolver
CREATE OR ALTER PROCEDURE [dbo].[sp_GetUserLandingPage]
    @userId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @isAdmin BIT = 0;
    IF EXISTS (
        SELECT 1 FROM [identity].[AspNetUserRoles] ur
        INNER JOIN [identity].[Roles] r ON ur.[RoleId] = r.[Id]
        WHERE ur.[UserId] = @userId AND r.[Name] = 'Admin'
    ) OR EXISTS (
        SELECT 1 FROM [identity].[Users] WHERE [Id] = @userId AND [Email] = 'admin@nkplm.erp'
    )
        SET @isAdmin = 1;

    SELECT TOP 1 ap.[PageUrl]
    FROM [identity].[AppPages] ap
    WHERE ap.[IsActive] = 1
      AND ap.[PageUrl] IS NOT NULL AND LTRIM(RTRIM(ap.[PageUrl])) <> ''
      AND (
            @isAdmin = 1
            OR EXISTS (
                SELECT 1
                FROM [identity].[Permissions] p
                INNER JOIN [identity].[RolePermissions] rp ON rp.[PermissionId] = p.[Id]
                INNER JOIN [identity].[AspNetUserRoles] ur ON ur.[RoleId] = rp.[RoleId]
                WHERE ur.[UserId] = @userId AND p.[Name] = ap.[PageKey] + '.View'
            )
      )
    ORDER BY ap.[DisplayOrder], ap.[PageName];
END
GO

PRINT 'Landing page migration applied.';
GO
