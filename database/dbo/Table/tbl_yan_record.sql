-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_yan_record  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_yan_record] (
    [id] int IDENTITY(132897,1) NOT NULL,
    [order_id] int NOT NULL,
    [order_no] varchar(100) NOT NULL,
    [knitter] varchar(50) NOT NULL,
    [cone_wt] float NOT NULL,
    [cone_no] varchar(100) NOT NULL,
    [c_date] date NOT NULL,
    [color] varchar(100) NOT NULL,
    [var] int NOT NULL,
    [status] int NOT NULL,
    [product_id] int NOT NULL,
    [pics] int NOT NULL,
    [weight] float NOT NULL,
    [ret_wt] float NOT NULL,
    [ret_date] date NOT NULL,
    [linking] varchar(100) NOT NULL,
    [lr_wt] float NOT NULL,
    [mending] varchar(100) NOT NULL,
    [mr_wt] float NOT NULL,
    [emb] varchar(100) NOT NULL,
    [er_wt] int NOT NULL,
    [i_date] datetime NOT NULL CONSTRAINT [DF__tbl_yan_r__i_dat__5772F790] DEFAULT (getdate()),
    [r_id] int NOT NULL,
    [remarks] varchar(200) NOT NULL CONSTRAINT [DF__tbl_yan_r__remar__58671BC9] DEFAULT ('1'),
    [r_status] int NOT NULL CONSTRAINT [DF__tbl_yan_r__r_sta__595B4002] DEFAULT ((0)),
    [plan_id] int NOT NULL,
    CONSTRAINT [PK_tbl_yan_record] PRIMARY KEY CLUSTERED ([id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_tbl_yan_record_cone_no] ON [dbo].[tbl_yan_record] ([cone_no] ASC);
CREATE NONCLUSTERED INDEX [IX_tbl_yan_record_knitter] ON [dbo].[tbl_yan_record] ([knitter] ASC);
CREATE NONCLUSTERED INDEX [IX_tbl_yan_record_order_id] ON [dbo].[tbl_yan_record] ([order_id] ASC);
