-- Table: [identity].[Menu] — sidebar menu categories (created by database/menu.sql,
-- not yet confirmed deployed). AppPages.MenuId references this.
CREATE TABLE [identity].[Menu] (
    [Id]    INT IDENTITY(1,1) NOT NULL,
    [Title] NVARCHAR(100) NOT NULL,
    CONSTRAINT [PK_Menu] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_Menu_Title] UNIQUE ([Title])
);
