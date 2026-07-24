-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.PoTaskChecklist  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[PoTaskChecklist] (
    [ChecklistId] int IDENTITY(1,1) NOT NULL,
    [PoTaskId] int NOT NULL,
    [Text] nvarchar(400) NOT NULL,
    [IsDone] bit NOT NULL CONSTRAINT [DF_PoTaskChecklist_Done] DEFAULT ((0)),
    [SortOrder] int NOT NULL CONSTRAINT [DF_PoTaskChecklist_Sort] DEFAULT ((0)),
    [CreatedDate] datetime NOT NULL CONSTRAINT [DF_PoTaskChecklist_Created] DEFAULT (getdate()),
    CONSTRAINT [PK_PoTaskChecklist] PRIMARY KEY CLUSTERED ([ChecklistId] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_PoTaskChecklist_Task] ON [dbo].[PoTaskChecklist] ([PoTaskId] ASC);

-- Foreign keys (require referenced tables)
ALTER TABLE [dbo].[PoTaskChecklist] ADD CONSTRAINT [FK_PoTaskChecklist_PoTask] FOREIGN KEY ([PoTaskId]) REFERENCES [dbo].[PoTask] ([PoTaskId]);
