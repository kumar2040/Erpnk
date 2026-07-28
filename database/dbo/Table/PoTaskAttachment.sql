-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.PoTaskAttachment  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[PoTaskAttachment] (
    [AttachmentId] int IDENTITY(1,1) NOT NULL,
    [PoTaskId] int NOT NULL,
    [FileName] nvarchar(260) NOT NULL,
    [ContentType] nvarchar(120) NULL,
    [SizeBytes] int NOT NULL,
    [Content] varbinary(max) NULL,
    [UploadedBy] nvarchar(450) NULL,
    [UploadedDate] datetime NOT NULL CONSTRAINT [DF_PoTaskAttachment_Uploaded] DEFAULT (getdate()),
    CONSTRAINT [PK_PoTaskAttachment] PRIMARY KEY CLUSTERED ([AttachmentId] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_PoTaskAttachment_Task] ON [dbo].[PoTaskAttachment] ([PoTaskId] ASC);

-- Foreign keys (require referenced tables)
ALTER TABLE [dbo].[PoTaskAttachment] ADD CONSTRAINT [FK_PoTaskAttachment_PoTask] FOREIGN KEY ([PoTaskId]) REFERENCES [dbo].[PoTask] ([PoTaskId]);
