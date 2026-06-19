USE [NatureKnit];   -- <-- change to your app's DB if different
GO

-- =========================================================================
-- FIX: identity.UserRoleGauges was created with the OLD single-column schema
-- (Gauge). The current procs expect the two-level schema (KnitType + GaugeValue),
-- which caused "Invalid column name 'KnitType'/'GaugeValue'".
--
-- This drops the mis-schema'd table and recreates it correctly, then backfills
-- from the legacy Users.AssignedGauge. Scope rows (if any) are re-entered via the
-- Role Management UI afterwards.
-- =========================================================================

IF OBJECT_ID('identity.UserRoleGauges') IS NOT NULL
BEGIN
    DROP TABLE [identity].[UserRoleGauges];
    PRINT 'Dropped old identity.UserRoleGauges.';
END
GO

CREATE TABLE [identity].[UserRoleGauges] (
    [UserId]     NVARCHAR(450) NOT NULL,
    [RoleId]     NVARCHAR(450) NOT NULL,
    [KnitType]   NVARCHAR(50)  NOT NULL,
    [GaugeValue] NVARCHAR(100) NOT NULL CONSTRAINT [DF_UserRoleGauges_GaugeValue] DEFAULT (''),
    CONSTRAINT [PK_UserRoleGauges] PRIMARY KEY ([UserId], [RoleId], [KnitType], [GaugeValue]),
    CONSTRAINT [FK_UserRoleGauges_AspNetUserRoles]
        FOREIGN KEY ([UserId], [RoleId])
        REFERENCES [identity].[AspNetUserRoles] ([UserId], [RoleId])
        ON DELETE CASCADE
);
PRINT 'Recreated identity.UserRoleGauges with KnitType + GaugeValue.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserRoleGauges_UserId')
    CREATE INDEX [IX_UserRoleGauges_UserId] ON [identity].[UserRoleGauges] ([UserId]) INCLUDE ([KnitType], [GaugeValue]);
GO

-- Best-effort backfill of legacy numeric gauges as knit scopes.
INSERT INTO [identity].[UserRoleGauges] ([UserId], [RoleId], [KnitType], [GaugeValue])
SELECT DISTINCT ua.[UserId], ua.[RoleId], 'knit', LTRIM(RTRIM(u.[AssignedGauge]))
FROM [identity].[AspNetUserRoles] ua
INNER JOIN [identity].[Users] u ON u.[Id] = ua.[UserId]
WHERE u.[AssignedGauge] IS NOT NULL
  AND LTRIM(RTRIM(u.[AssignedGauge])) <> ''
  AND TRY_CONVERT(FLOAT, REPLACE(REPLACE(REPLACE(u.[AssignedGauge], 'GG',''),'G',''),' ','')) IS NOT NULL
  AND NOT EXISTS (
        SELECT 1 FROM [identity].[UserRoleGauges] g
        WHERE g.[UserId] = ua.[UserId] AND g.[RoleId] = ua.[RoleId]
          AND g.[KnitType] = 'knit' AND g.[GaugeValue] = LTRIM(RTRIM(u.[AssignedGauge]))
  );
GO

PRINT 'UserRoleGauges schema fixed. Re-assign scopes in Role Management.';
GO
