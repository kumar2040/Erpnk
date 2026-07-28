-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.PoTaskHistory  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[PoTaskHistory] (
    [HistoryId] int IDENTITY(1,1) NOT NULL,
    [PoTaskId] int NOT NULL,
    [AssigneeId] int NULL,
    [FromStatus] char(1) NULL,
    [ToStatus] char(1) NULL,
    [Note] nvarchar(400) NULL,
    [ChangedBy] nvarchar(450) NULL,
    [ChangedDate] datetime NOT NULL CONSTRAINT [DF_PoTaskHistory_Changed] DEFAULT (getdate()),
    CONSTRAINT [PK_PoTaskHistory] PRIMARY KEY CLUSTERED ([HistoryId] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_PoTaskHistory_Task] ON [dbo].[PoTaskHistory] ([PoTaskId] ASC);
