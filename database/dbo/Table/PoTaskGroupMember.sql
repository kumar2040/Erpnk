-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.PoTaskGroupMember  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[PoTaskGroupMember] (
    [GroupMemberId] int IDENTITY(1,1) NOT NULL,
    [GroupId] int NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [IsActive] bit NOT NULL CONSTRAINT [DF_PoTaskGroupMember_Active] DEFAULT ((1)),
    CONSTRAINT [PK_PoTaskGroupMember] PRIMARY KEY CLUSTERED ([GroupMemberId] ASC)
);

-- Indexes
CREATE UNIQUE NONCLUSTERED INDEX [UX_PoTaskGroupMember] ON [dbo].[PoTaskGroupMember] ([GroupId] ASC, [UserId] ASC);

-- Foreign keys (require referenced tables)
ALTER TABLE [dbo].[PoTaskGroupMember] ADD CONSTRAINT [FK_PoTaskGroupMember_Group] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[PoTaskGroup] ([GroupId]);
