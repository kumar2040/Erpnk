-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_cone_split  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_cone_split] (
    [id] int IDENTITY(2870,1) NOT NULL,
    [cone] varchar(100) NULL,
    [color] varchar(100) NULL,
    [wt] float NULL,
    [lot] varchar(100) NULL,
    [split_no] int NULL,
    [date_] datetime NOT NULL CONSTRAINT [DF__tbl_cone___date___5C37ACAD] DEFAULT (getdate()),
    [issued_by] int NULL,
    [auth_by] int NULL,
    [return_wt] float NULL,
    [status] int NULL CONSTRAINT [DF__tbl_cone___statu__5D2BD0E6] DEFAULT ((0)),
    [product_id] int NULL,
    [po] varchar(100) NULL,
    CONSTRAINT [PK_tbl_cone_split] PRIMARY KEY CLUSTERED ([id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_tbl_cone_split_cone] ON [dbo].[tbl_cone_split] ([cone] ASC);
