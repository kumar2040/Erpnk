-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tbl_stylesheet_extrayarn  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tbl_stylesheet_extrayarn] (
    [id] int IDENTITY(66,1) NOT NULL,
    [style_id] int NULL,
    [yarn_id] int NULL,
    [date_t] datetime NULL CONSTRAINT [DF__tbl_style__date___5AEE82B9] DEFAULT (getdate()),
    [wt] int NULL,
    CONSTRAINT [PK__tbl_styl__3213E83F63CC6D63] PRIMARY KEY CLUSTERED ([id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [idx_style_id] ON [dbo].[tbl_stylesheet_extrayarn] ([style_id] ASC);
