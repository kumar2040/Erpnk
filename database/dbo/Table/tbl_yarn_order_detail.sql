-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_yarn_order_detail  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_yarn_order_detail] (
    [yod_id] int IDENTITY(1,1) NOT NULL,
    [yo_id] int NOT NULL,
    [product_id] varchar(100) NULL,
    [yarn_name] varchar(200) NULL,
    [color] varchar(100) NULL,
    [ply] varchar(20) NULL,
    [order_no] varchar(50) NULL,
    [import_kg] decimal(18,3) NOT NULL CONSTRAINT [DF__tbl_yarn___impor__6D6238AF] DEFAULT ((0)),
    [is_dropped] bit NOT NULL CONSTRAINT [DF__tbl_yarn___is_dr__320C68B7] DEFAULT ((0)),
    [drop_date] datetime NULL,
    [drop_by] varchar(50) NULL,
    [drop_note] varchar(200) NULL,
    CONSTRAINT [PK__tbl_yarn__5EDBA0A8D7277334] PRIMARY KEY CLUSTERED ([yod_id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_yod_yo_id] ON [dbo].[tbl_yarn_order_detail] ([yo_id] ASC);
CREATE NONCLUSTERED INDEX [IX_yod_order_no] ON [dbo].[tbl_yarn_order_detail] ([order_no] ASC);

-- Foreign keys (require referenced tables)
ALTER TABLE [dbo].[tbl_yarn_order_detail] ADD CONSTRAINT [FK__tbl_yarn___yo_id__6C6E1476] FOREIGN KEY ([yo_id]) REFERENCES [dbo].[tbl_yarn_order] ([yo_id]);
