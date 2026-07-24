-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.PoPlanSnapshot  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[PoPlanSnapshot] (
    [SnapshotId] int IDENTITY(1,1) NOT NULL,
    [OrderNo] nvarchar(50) NOT NULL,
    [ParamHash] binary(32) NOT NULL,
    [ParamJson] nvarchar(max) NULL,
    [CapturedBy] nvarchar(450) NULL,
    [CapturedDate] datetime NOT NULL CONSTRAINT [DF_PoPlanSnapshot_Captured] DEFAULT (getdate()),
    CONSTRAINT [PK_PoPlanSnapshot] PRIMARY KEY CLUSTERED ([SnapshotId] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_PoPlanSnapshot_Order] ON [dbo].[PoPlanSnapshot] ([OrderNo] ASC, [CapturedDate] DESC);
