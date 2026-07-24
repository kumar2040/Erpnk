-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_knitter_record_data  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_knitter_record_data] (
    [d_id] int IDENTITY(298707,1) NOT NULL,
    [r_id] int NULL,
    [knitter] varchar(200) NULL,
    [cone_id] varchar(100) NULL,
    [pics] int NULL,
    [status] int NULL,
    [knd] date NULL,
    [krd] datetime NULL CONSTRAINT [DF__tbl_knitter__krd__3F115E1A] DEFAULT (getdate()),
    [cone_wt] int NULL,
    [order_id] varchar(100) NULL,
    [req_wt] int NULL,
    [barcode] varchar(100) NULL,
    [ret_pic] int NULL,
    [forward] int NULL,
    [ret_wt] int NULL,
    [r_status] int NULL,
    [for_pics] int NULL,
    [p_typ] varchar(20) NULL,
    [will_ret_daate] datetime NULL,
    [plan_id] int NULL CONSTRAINT [DF__tbl_knitt__plan___40058253] DEFAULT ((0)),
    [setting_pc] int NULL CONSTRAINT [DF__tbl_knitt__setti__40F9A68C] DEFAULT ((0)),
    CONSTRAINT [PK__tbl_knit__D95F582BD706B48D] PRIMARY KEY CLUSTERED ([d_id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [idx_plan_id] ON [dbo].[tbl_knitter_record_data] ([plan_id] ASC);
CREATE NONCLUSTERED INDEX [idx_plan_id_2] ON [dbo].[tbl_knitter_record_data] ([plan_id] ASC);
CREATE NONCLUSTERED INDEX [idx_knitter] ON [dbo].[tbl_knitter_record_data] ([knitter] ASC);
CREATE NONCLUSTERED INDEX [idx_order_id] ON [dbo].[tbl_knitter_record_data] ([order_id] ASC);
