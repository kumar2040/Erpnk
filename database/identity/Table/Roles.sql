-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: [identity].[Roles]  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [identity].[Roles] (
    [Id] nvarchar(450) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2(7) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [AssignedGauge] nvarchar(100) NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- Indexes
CREATE UNIQUE NONCLUSTERED INDEX [RoleNameIndex] ON [identity].[Roles] ([NormalizedName] ASC);
