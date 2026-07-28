-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_yarn_vendor_order  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_yarn_vendor_order] (
    [vyo_id] int IDENTITY(1,1) NOT NULL,
    [yo_id] int NOT NULL,
    [vyo_no] varchar(40) NOT NULL,
    [vendor] varchar(150) NULL,
    [created_date] datetime NOT NULL CONSTRAINT [DF__tbl_yarn___creat__25A691D2] DEFAULT (getdate()),
    [created_by] varchar(50) NULL,
    [total_kg] decimal(18,3) NOT NULL CONSTRAINT [DF__tbl_yarn___total__269AB60B] DEFAULT ((0)),
    [line_count] int NOT NULL CONSTRAINT [DF__tbl_yarn___line___278EDA44] DEFAULT ((0)),
    [departure_date] date NULL,
    [arrival_date] date NULL,
    [status] varchar(20) NOT NULL CONSTRAINT [DF__tbl_yarn___statu__2882FE7D] DEFAULT ('Placed'),
    CONSTRAINT [PK__tbl_yarn__A674C705E75567A6] PRIMARY KEY CLUSTERED ([vyo_id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_vyo_yo_id] ON [dbo].[tbl_yarn_vendor_order] ([yo_id] ASC);

-- Foreign keys (require referenced tables)
ALTER TABLE [dbo].[tbl_yarn_vendor_order] ADD CONSTRAINT [FK__tbl_yarn___yo_id__24B26D99] FOREIGN KEY ([yo_id]) REFERENCES [dbo].[tbl_yarn_order] ([yo_id]);
