-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_no_box  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_no_box] (
    [b_id] int IDENTITY(1,1) NOT NULL,
    [product_id] int NOT NULL,
    [box_barcode] varchar(100) NOT NULL,
    [data] varchar(100) NOT NULL,
    [extra] int NOT NULL,
    [lot] varchar(100) NOT NULL,
    [r_] int NOT NULL CONSTRAINT [DF__tbl_no_box__r___4865BE2A] DEFAULT ((0)),
    [c_date] datetime NOT NULL CONSTRAINT [DF__tbl_no_bo__c_dat__4959E263] DEFAULT (getdate()),
    [vf_box] int NOT NULL CONSTRAINT [DF__tbl_no_bo__vf_bo__4A4E069C] DEFAULT ((0)),
    CONSTRAINT [PK__tbl_no_b__4E29C30D79BF1C86] PRIMARY KEY CLUSTERED ([b_id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_tbl_no_box_product_id] ON [dbo].[tbl_no_box] ([product_id] ASC);
