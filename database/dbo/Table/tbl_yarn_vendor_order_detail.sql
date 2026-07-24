-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_yarn_vendor_order_detail  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_yarn_vendor_order_detail] (
    [vyod_id] int IDENTITY(1,1) NOT NULL,
    [vyo_id] int NOT NULL,
    [product_id] varchar(100) NULL,
    [yarn_name] varchar(200) NULL,
    [color] varchar(100) NULL,
    [ply] varchar(20) NULL,
    [order_no] varchar(50) NULL,
    [import_kg] decimal(18,3) NOT NULL CONSTRAINT [DF__tbl_yarn___impor__2C538F61] DEFAULT ((0)),
    CONSTRAINT [PK__tbl_yarn__F8E44982E4402F2B] PRIMARY KEY CLUSTERED ([vyod_id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_vyod_vyo_id] ON [dbo].[tbl_yarn_vendor_order_detail] ([vyo_id] ASC);

-- Foreign keys (require referenced tables)
ALTER TABLE [dbo].[tbl_yarn_vendor_order_detail] ADD CONSTRAINT [FK__tbl_yarn___vyo_i__2B5F6B28] FOREIGN KEY ([vyo_id]) REFERENCES [dbo].[tbl_yarn_vendor_order] ([vyo_id]);
