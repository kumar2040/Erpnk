-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_stylesheet_wapweft_yarn  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_stylesheet_wapweft_yarn] (
    [id] int IDENTITY(3026,1) NOT NULL,
    [style_id] int NULL,
    [product_id] int NULL,
    [body_parts] varchar(50) NULL,
    [weight_y] float NULL,
    [date_entry] datetime NULL CONSTRAINT [DF__tbl_style__date___5DCAEF64] DEFAULT (getdate()),
    [user_id] int NULL,
    CONSTRAINT [PK__tbl_styl__3213E83FF972E2CC] PRIMARY KEY CLUSTERED ([id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [idx_style_id] ON [dbo].[tbl_stylesheet_wapweft_yarn] ([style_id] ASC);
CREATE NONCLUSTERED INDEX [idx_product_id] ON [dbo].[tbl_stylesheet_wapweft_yarn] ([product_id] ASC);
