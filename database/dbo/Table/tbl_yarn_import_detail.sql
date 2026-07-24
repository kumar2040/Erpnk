-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_yarn_import_detail  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_yarn_import_detail] (
    [id] int IDENTITY(8756,1) NOT NULL,
    [imp_id] int NOT NULL,
    [yarn] int NOT NULL,
    [cones] int NOT NULL,
    [color] varchar(50) NOT NULL,
    [lot_no] varchar(50) NOT NULL,
    CONSTRAINT [PK_tbl_yarn_import_detail] PRIMARY KEY CLUSTERED ([id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_tbl_yarn_import_detail_lot_no] ON [dbo].[tbl_yarn_import_detail] ([lot_no] ASC);
CREATE NONCLUSTERED INDEX [IX_tbl_yarn_import_detail_imp_id] ON [dbo].[tbl_yarn_import_detail] ([imp_id] ASC);
