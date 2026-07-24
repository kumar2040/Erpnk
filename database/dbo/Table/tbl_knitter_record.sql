-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_knitter_record  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_knitter_record] (
    [kr_id] int IDENTITY(298796,1) NOT NULL,
    [knitter_id] varchar(100) NULL,
    [po] varchar(100) NULL,
    [style_no] varchar(100) NULL,
    [kpics] int NULL,
    [color] varchar(100) NULL,
    [size] varchar(10) NULL,
    [kcone_id] varchar(100) NULL,
    [cone_wt] int NULL,
    [kr_status] int NULL,
    [machine_no] int NULL,
    [return] int NULL,
    [order_id] int NULL,
    [flag] int NULL,
    [i_time] time(7) NULL,
    [date_ty] datetime NULL CONSTRAINT [DF__tbl_knitt__date___43D61337] DEFAULT (getdate()),
    CONSTRAINT [PK__tbl_knit__72360DB7163FCE0B] PRIMARY KEY CLUSTERED ([kr_id] ASC)
);

-- Indexes
CREATE UNIQUE NONCLUSTERED INDEX [idx_index_1] ON [dbo].[tbl_knitter_record] ([kr_id] ASC);
CREATE NONCLUSTERED INDEX [idx_order] ON [dbo].[tbl_knitter_record] ([order_id] ASC);
CREATE NONCLUSTERED INDEX [idx_style_no] ON [dbo].[tbl_knitter_record] ([style_no] ASC);
