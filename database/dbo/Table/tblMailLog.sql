-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tblMailLog  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tblMailLog] (
    [mail_id] int IDENTITY(1,1) NOT NULL,
    [mail_to] nvarchar(500) NOT NULL,
    [mail_cc] nvarchar(500) NULL,
    [subject] nvarchar(255) NOT NULL,
    [body] nvarchar(max) NOT NULL,
    [mail_type] varchar(40) NOT NULL,
    [is_sent] bit NOT NULL CONSTRAINT [DF__tblMailLo__is_se__34E8D562] DEFAULT ((0)),
    [retry_count] int NOT NULL CONSTRAINT [DF__tblMailLo__retry__35DCF99B] DEFAULT ((0)),
    [error_msg] nvarchar(500) NULL,
    [sent_date] datetime NULL,
    [created_date] datetime NOT NULL CONSTRAINT [DF__tblMailLo__creat__36D11DD4] DEFAULT (getdate()),
    [created_by] varchar(50) NULL,
    CONSTRAINT [PK__tblMailL__9D7A09AEE92005B0] PRIMARY KEY CLUSTERED ([mail_id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_tblMailLog_pending] ON [dbo].[tblMailLog] ([created_date] ASC);
