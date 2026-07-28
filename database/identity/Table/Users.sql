-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: [identity].[Users]  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [identity].[Users] (
    [Id] nvarchar(450) NOT NULL,
    [FirstName] nvarchar(max) NOT NULL,
    [LastName] nvarchar(max) NOT NULL,
    [BranchId] uniqueidentifier NULL,
    [IsActive] bit NOT NULL,
    [MfaSecret] nvarchar(max) NULL,
    [MfaEnabled] bit NOT NULL,
    [CreatedAt] datetime2(7) NOT NULL,
    [LastLoginAt] datetime2(7) NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset(7) NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    [AssignedGauge] nvarchar(100) NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [EmailIndex] ON [identity].[Users] ([NormalizedEmail] ASC);
CREATE UNIQUE NONCLUSTERED INDEX [UserNameIndex] ON [identity].[Users] ([NormalizedUserName] ASC);
