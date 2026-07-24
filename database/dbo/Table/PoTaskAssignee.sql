-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.PoTaskAssignee  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[PoTaskAssignee] (
    [AssigneeId] int IDENTITY(1,1) NOT NULL,
    [PoTaskId] int NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [Status] char(1) NOT NULL CONSTRAINT [DF_PoTaskAssignee_Status] DEFAULT ('S'),
    [StartDate] datetime NULL,
    [CompletedDate] datetime NULL,
    [Note] nvarchar(400) NULL,
    [AssignedBy] nvarchar(450) NULL,
    [AssignedDate] datetime NOT NULL CONSTRAINT [DF_PoTaskAssignee_Assigned] DEFAULT (getdate()),
    [IsActive] bit NOT NULL CONSTRAINT [DF_PoTaskAssignee_Active] DEFAULT ((1)),
    CONSTRAINT [PK_PoTaskAssignee] PRIMARY KEY CLUSTERED ([AssigneeId] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_PoTaskAssignee_Task] ON [dbo].[PoTaskAssignee] ([PoTaskId] ASC);
CREATE NONCLUSTERED INDEX [IX_PoTaskAssignee_User] ON [dbo].[PoTaskAssignee] ([UserId] ASC);
CREATE UNIQUE NONCLUSTERED INDEX [UX_PoTaskAssignee_TaskUser] ON [dbo].[PoTaskAssignee] ([PoTaskId] ASC, [UserId] ASC);

-- Foreign keys (require referenced tables)
ALTER TABLE [dbo].[PoTaskAssignee] ADD CONSTRAINT [FK_PoTaskAssignee_PoTask] FOREIGN KEY ([PoTaskId]) REFERENCES [dbo].[PoTask] ([PoTaskId]);
