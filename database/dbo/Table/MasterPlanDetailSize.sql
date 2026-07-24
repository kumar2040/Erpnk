-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.MasterPlanDetailSize  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[MasterPlanDetailSize] (
    [id] int IDENTITY(1,1) NOT NULL,
    [MasterPlanDetailId] int NOT NULL,
    [order_id] int NULL,
    [style_no] nvarchar(50) NULL,
    [color] nvarchar(100) NULL,
    [size] nvarchar(20) NULL,
    [qty] decimal(18,2) NOT NULL CONSTRAINT [DF__MasterPlanD__qty__1D4655FB] DEFAULT ((0)),
    [status] int NOT NULL CONSTRAINT [DF_MasterPlanDetailSize_status] DEFAULT ((0)),
    CONSTRAINT [PK_MasterPlanDetailSize] PRIMARY KEY CLUSTERED ([id] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_MasterPlanDetailSize_MasterPlanDetailId] ON [dbo].[MasterPlanDetailSize] ([MasterPlanDetailId] ASC);

-- Foreign keys (require referenced tables)
ALTER TABLE [dbo].[MasterPlanDetailSize] ADD CONSTRAINT [FK_MasterPlanDetailSize_MasterPlanDetail] FOREIGN KEY ([MasterPlanDetailId]) REFERENCES [dbo].[MasterPlanDetail] ([MasterPlanChildId]);
