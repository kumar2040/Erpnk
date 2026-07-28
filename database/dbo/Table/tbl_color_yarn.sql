-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_color_yarn  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_color_yarn] (
    [id] int IDENTITY(2338,1) NOT NULL,
    [product_id] int NULL,
    [color_name] varchar(200) NULL,
    [date_] datetime NULL CONSTRAINT [DF__tbl_color__date___1332DBDC] DEFAULT (getdate()),
    [user] int NULL,
    [u_color] int NULL CONSTRAINT [DF__tbl_color__u_col__14270015] DEFAULT ((0)),
    CONSTRAINT [PK__tbl_colo__3213E83FE415342C] PRIMARY KEY CLUSTERED ([id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [idx_product_id] ON [dbo].[tbl_color_yarn] ([product_id] ASC);
CREATE NONCLUSTERED INDEX [idx_color_name] ON [dbo].[tbl_color_yarn] ([color_name] ASC);
