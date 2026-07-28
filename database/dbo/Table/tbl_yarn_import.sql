-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_yarn_import  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_yarn_import] (
    [id] int IDENTITY(493,1) NOT NULL,
    [vendor_id] varchar(100) NOT NULL,
    [lc] varchar(100) NOT NULL,
    [inv_no] varchar(100) NOT NULL,
    [inv_date] date NOT NULL,
    [entry_date] datetime NOT NULL CONSTRAINT [DF__tbl_yarn___entry__61F08603] DEFAULT (getdate()),
    CONSTRAINT [PK_tbl_yarn_import] PRIMARY KEY CLUSTERED ([id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_tbl_yarn_import_lc] ON [dbo].[tbl_yarn_import] ([lc] ASC);
CREATE NONCLUSTERED INDEX [IX_tbl_yarn_import_inv_no] ON [dbo].[tbl_yarn_import] ([inv_no] ASC);
