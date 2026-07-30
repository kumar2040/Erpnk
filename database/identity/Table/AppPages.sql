-- Table: [identity].[AppPages] — page/module registry backing Pages Management and the
-- sidebar menu. Created inline in pages_crud_migration.sql (no prior mirror file existed
-- here). Reflects the target shape including MenuId/Icon from database/update-12-56.sql,
-- not yet confirmed deployed.
CREATE TABLE [identity].[AppPages] (
    [AppPageId]    INT IDENTITY(1,1) NOT NULL,
    [PageKey]      NVARCHAR(100) NOT NULL,
    [PageName]     NVARCHAR(200) NOT NULL,
    [PageUrl]      NVARCHAR(500) NULL,
    [IsActive]     BIT NOT NULL CONSTRAINT [DF_AppPages_IsActive] DEFAULT (1),
    [DisplayOrder] INT NOT NULL CONSTRAINT [DF_AppPages_DisplayOrder] DEFAULT (0),
    [CreatedAt]    DATETIME NOT NULL CONSTRAINT [DF_AppPages_CreatedAt] DEFAULT (GETDATE()),
    [MenuId]       INT NULL,
    [Icon]         NVARCHAR(50) NULL,
    CONSTRAINT [PK_AppPages] PRIMARY KEY CLUSTERED ([AppPageId] ASC),
    CONSTRAINT [UQ_AppPages_PageKey] UNIQUE ([PageKey]),
    CONSTRAINT [FK_AppPages_MenuId] FOREIGN KEY ([MenuId])
        REFERENCES [identity].[Menu] ([Id])
);
