-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_color_var  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_color_var] (
    [id] int IDENTITY(127737,1) NOT NULL,
    [style_id] varchar(100) NULL,
    [var] varchar(100) NULL,
    [color] varchar(100) NULL,
    [weight] int NULL,
    [setp] varchar(50) NULL,
    [user_] int NULL CONSTRAINT [DF__tbl_color__user___0E6E26BF] DEFAULT ((0)),
    [date_] datetime NULL CONSTRAINT [DF__tbl_color__date___0F624AF8] DEFAULT (getdate()),
    [design] varchar(200) NULL CONSTRAINT [DF__tbl_color__desig__10566F31] DEFAULT (''),
    CONSTRAINT [PK__tbl_colo__3213E83F87CCDB82] PRIMARY KEY CLUSTERED ([id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [idx_var] ON [dbo].[tbl_color_var] ([var] ASC);
CREATE NONCLUSTERED INDEX [idx_style_id] ON [dbo].[tbl_color_var] ([style_id] ASC);
CREATE NONCLUSTERED INDEX [idx_color] ON [dbo].[tbl_color_var] ([color] ASC);
CREATE NONCLUSTERED INDEX [idx_setp] ON [dbo].[tbl_color_var] ([setp] ASC);
CREATE NONCLUSTERED INDEX [idx_weight] ON [dbo].[tbl_color_var] ([weight] ASC);
CREATE NONCLUSTERED INDEX [idx_color_var_style_var] ON [dbo].[tbl_color_var] ([style_id] ASC, [var] ASC, [weight] ASC);
