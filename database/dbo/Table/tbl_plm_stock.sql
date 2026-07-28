-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_plm_stock  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_plm_stock] (
    [id] int IDENTITY(1,1) NOT NULL,
    [po] varchar(100) NULL,
    [product_id] int NULL,
    [color] varchar(100) NULL,
    [cone] varchar(100) NULL,
    [date_] datetime2(0) NOT NULL CONSTRAINT [DF_tbl_plm_stock_date] DEFAULT (getdate()),
    [status] int NOT NULL CONSTRAINT [DF_tbl_plm_stock_status] DEFAULT ((1)),
    [knitter] int NULL CONSTRAINT [DF_tbl_plm_stock_knitter] DEFAULT ((0)),
    [lot_no] varchar(100) NULL,
    [ex] varchar(100) NULL,
    [weight] float NOT NULL,
    [parent] varchar(11) NOT NULL CONSTRAINT [DF_tbl_plm_stock_parent] DEFAULT ('0'),
    [veryfy] int NOT NULL CONSTRAINT [DF_tbl_plm_stock_veryfy] DEFAULT ((0)),
    [vf] int NOT NULL,
    [vfd] datetime NOT NULL,
    CONSTRAINT [PK_tbl_plm_stock] PRIMARY KEY CLUSTERED ([id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_tbl_plm_stock_cone] ON [dbo].[tbl_plm_stock] ([cone] ASC);
CREATE NONCLUSTERED INDEX [IX_tbl_plm_stock_product_id] ON [dbo].[tbl_plm_stock] ([product_id] ASC);
CREATE NONCLUSTERED INDEX [IX_tbl_plm_stock_color] ON [dbo].[tbl_plm_stock] ([color] ASC);
