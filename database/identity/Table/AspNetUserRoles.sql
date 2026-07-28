-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: [identity].[AspNetUserRoles]  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [identity].[AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY CLUSTERED ([UserId] ASC, [RoleId] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_AspNetUserRoles_RoleId] ON [identity].[AspNetUserRoles] ([RoleId] ASC);

-- Foreign keys (require referenced tables)
ALTER TABLE [identity].[AspNetUserRoles] ADD CONSTRAINT [FK_AspNetUserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [identity].[Roles] ([Id]);
ALTER TABLE [identity].[AspNetUserRoles] ADD CONSTRAINT [FK_AspNetUserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]);
