-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_yarn_order  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_yarn_order] (
    [yo_id] int IDENTITY(1,1) NOT NULL,
    [yo_no] varchar(30) NOT NULL,
    [created_date] datetime NOT NULL CONSTRAINT [DF__tbl_yarn___creat__65C116E7] DEFAULT (getdate()),
    [created_by] varchar(50) NULL,
    [total_kg] decimal(18,3) NOT NULL CONSTRAINT [DF__tbl_yarn___total__66B53B20] DEFAULT ((0)),
    [order_count] int NOT NULL CONSTRAINT [DF__tbl_yarn___order__67A95F59] DEFAULT ((0)),
    [line_count] int NOT NULL CONSTRAINT [DF__tbl_yarn___line___689D8392] DEFAULT ((0)),
    [status] varchar(20) NOT NULL CONSTRAINT [DF__tbl_yarn___statu__6991A7CB] DEFAULT ('Placed'),
    CONSTRAINT [PK__tbl_yarn__461FB04BA865E036] PRIMARY KEY CLUSTERED ([yo_id] ASC)
);
