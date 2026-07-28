-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_cone_stock  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_cone_stock] (
    [id] int IDENTITY(153093,1) NOT NULL,
    [box_id] int NULL,
    [p_code] varchar(100) NULL,
    [p_color] varchar(100) NULL,
    [p_lot] varchar(100) NULL,
    [lc] varchar(100) NULL,
    [cn_date] date NULL,
    [date_] datetime NULL CONSTRAINT [DF__tbl_cone___date___17036CC0] DEFAULT (getdate()),
    [user_id] int NULL,
    [status] int NULL CONSTRAINT [DF__tbl_cone___statu__17F790F9] DEFAULT ((0)),
    [reg] int NULL,
    [p_wt] float NULL,
    [active] int NULL CONSTRAINT [DF__tbl_cone___activ__18EBB532] DEFAULT ((1)),
    [o_wt] float NULL,
    [to_date] date NULL CONSTRAINT [DF__tbl_cone___to_da__19DFD96B] DEFAULT ('9999-09-09'),
    [verify] int NULL,
    [vf_date] date NULL,
    [vff_status] int NULL CONSTRAINT [DF__tbl_cone___vff_s__1AD3FDA4] DEFAULT ((0)),
    [vff_date] datetime NULL,
    [for_use] int NULL CONSTRAINT [DF__tbl_cone___for_u__1BC821DD] DEFAULT ((0)),
    CONSTRAINT [PK__tbl_cone__3213E83F80F909A5] PRIMARY KEY CLUSTERED ([id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [idx_box_id] ON [dbo].[tbl_cone_stock] ([box_id] ASC);
CREATE NONCLUSTERED INDEX [idx_p_color] ON [dbo].[tbl_cone_stock] ([p_color] ASC);
CREATE NONCLUSTERED INDEX [idx_for_use] ON [dbo].[tbl_cone_stock] ([for_use] ASC);
CREATE NONCLUSTERED INDEX [idx_p_code] ON [dbo].[tbl_cone_stock] ([p_code] ASC);
