-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tblopeningproductstock  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tblopeningproductstock] (
    [id] int IDENTITY(348,1) NOT NULL,
    [product_id] int NULL,
    [brand_id] int NULL,
    [qty] float NULL,
    [rate] float NULL,
    [measuring_unit] int NULL,
    CONSTRAINT [PK__tblopeni__3213E83FA96599B4] PRIMARY KEY CLUSTERED ([id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [idx_product_id] ON [dbo].[tblopeningproductstock] ([product_id] ASC);
