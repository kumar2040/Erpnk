-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.PoTaskGroup  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[PoTaskGroup] (
    [GroupId] int IDENTITY(1,1) NOT NULL,
    [GroupName] nvarchar(120) NOT NULL,
    [FactoryType] nvarchar(100) NULL,
    [IsActive] bit NOT NULL CONSTRAINT [DF_PoTaskGroup_Active] DEFAULT ((1)),
    [CreatedBy] nvarchar(450) NULL,
    [CreatedDate] datetime NOT NULL CONSTRAINT [DF_PoTaskGroup_Created] DEFAULT (getdate()),
    CONSTRAINT [PK_PoTaskGroup] PRIMARY KEY CLUSTERED ([GroupId] ASC)
);
