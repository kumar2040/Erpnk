USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   Child table of MasterPlanDetail.
   Stores the per-machine planning allocation broken down by
   style_no / color / size, so a single machine plan row
   (MasterPlanDetail.MasterPlanChildId) can carry many
   style+color+size lines, each with its own qty.
   ============================================================ */
IF OBJECT_ID('[dbo].[MasterPlanDetailSize]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MasterPlanDetailSize]
    (
        [id]                  INT IDENTITY(1,1) NOT NULL
            CONSTRAINT [PK_MasterPlanDetailSize] PRIMARY KEY,
        [MasterPlanDetailId]  INT            NOT NULL,
        [order_id]            INT            NULL,
        [style_no]            NVARCHAR(50)   NULL,
        [color]               NVARCHAR(100)  NULL,
        [size]                NVARCHAR(20)   NULL,
        [qty]                 DECIMAL(18,2)  NOT NULL DEFAULT(0),

        CONSTRAINT [FK_MasterPlanDetailSize_MasterPlanDetail]
            FOREIGN KEY ([MasterPlanDetailId])
            REFERENCES [dbo].[MasterPlanDetail] ([MasterPlanChildId])
            ON DELETE CASCADE
    );

    CREATE INDEX [IX_MasterPlanDetailSize_MasterPlanDetailId]
        ON [dbo].[MasterPlanDetailSize] ([MasterPlanDetailId]);
END
GO
