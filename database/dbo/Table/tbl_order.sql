-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_order  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_order] (
    [order_id] int IDENTITY(208074,1) NOT NULL,
    [order_no] varchar(100) NULL,
    [product_name] varchar(200) NULL,
    [order_buyer] varchar(200) NULL,
    [order_date] date NULL,
    [order_ldate] date NULL,
    [order_color] varchar(200) NULL,
    [order_size] varchar(100) NULL,
    [order_yarn] varchar(200) NULL,
    [order_status] int NULL,
    [order_unit_price] varchar(100) NULL,
    [order_pics] int NULL,
    [order_nk] varchar(200) NULL,
    [order_ms] int NULL,
    [remark] varchar(max) NULL,
    [xxxs] int NULL CONSTRAINT [DF__tbl_order__xxxs__46B27FE2] DEFAULT ((0)),
    [xxs] int NULL CONSTRAINT [DF__tbl_order__xxs__47A6A41B] DEFAULT ((0)),
    [xs] int NULL,
    [s] int NULL,
    [m] int NULL,
    [l] int NULL,
    [xl] int NULL,
    [xxl] int NULL,
    [osfa] int NULL,
    [order_packing] int NULL,
    [18m] int NULL,
    [2y] int NULL,
    [3y] int NULL,
    [4y] int NULL,
    [5y] int NULL,
    [6y] int NULL,
    [7y] int NULL,
    [tp] int NULL,
    [8y] int NULL,
    [9y] int NULL,
    [10y] int NULL,
    [11y] int NULL,
    [12y] int NULL,
    [14y] int NULL,
    [order_setting] int NULL CONSTRAINT [DF__tbl_order__order__489AC854] DEFAULT ((1)),
    [xxxl] int NULL CONSTRAINT [DF__tbl_order__xxxl__498EEC8D] DEFAULT ((0)),
    [date_u] datetime NULL CONSTRAINT [DF__tbl_order__date___4A8310C6] DEFAULT (getdate()),
    [user_od] int NULL CONSTRAINT [DF__tbl_order__user___4B7734FF] DEFAULT ((0)),
    [pcfab] int NULL CONSTRAINT [DF__tbl_order__pcfab__4C6B5938] DEFAULT ((0)),
    CONSTRAINT [PK__tbl_orde__4659622967954276] PRIMARY KEY CLUSTERED ([order_id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [idx_order_buyer] ON [dbo].[tbl_order] ([order_buyer] ASC);
CREATE NONCLUSTERED INDEX [idx_fast_index] ON [dbo].[tbl_order] ([order_no] ASC, [product_name] ASC);
CREATE NONCLUSTERED INDEX [idx_product_name] ON [dbo].[tbl_order] ([product_name] ASC);
CREATE NONCLUSTERED INDEX [idx_order_color] ON [dbo].[tbl_order] ([order_color] ASC);
CREATE NONCLUSTERED INDEX [idx_order_ldate] ON [dbo].[tbl_order] ([order_ldate] ASC);
