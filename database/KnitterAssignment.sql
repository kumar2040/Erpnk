USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   Stores which knitter is assigned to a planned size line
   (from the "for Master Planning" page).
   Keyed to MasterPlanDetailSize.id so an assignment is removed
   automatically if the size line is deleted.
   One knitter per size line (unique MasterPlanDetailSizeId).
   ============================================================ */
IF OBJECT_ID('[dbo].[KnitterAssignment]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[KnitterAssignment]
    (
        [id]                     INT IDENTITY(1,1) NOT NULL
            CONSTRAINT [PK_KnitterAssignment] PRIMARY KEY,
        [MasterPlanDetailSizeId] INT            NOT NULL,
        [order_id]               INT            NULL,
        [gauge]               NVARCHAR(50)   NULL,
        [machine]             NVARCHAR(50)   NULL,   -- machine name e.g. KN-12
        [machine_id]          INT            NULL,
        [start_date]          DATETIME       NULL,   -- plan start (busy from)
        [end_date]            DATETIME       NULL,   -- plan end   (busy to)
        [size]                NVARCHAR(20)   NULL,   -- size of the allocated line
        [qty]                 DECIMAL(18,2)  NULL,   -- qty allocated for that size
        [card_no]             NVARCHAR(50)   NULL,   -- knitter card no
        [knitter_name]        NVARCHAR(150)  NULL,   -- snapshot of the knitter name
        [status]              NVARCHAR(20)   NOT NULL -- Assigned / Completed
            CONSTRAINT [DF_KnitterAssignment_Status] DEFAULT('Assigned'),
        [completed_date]      DATETIME       NULL,   -- set when marked complete
        [assigned_by]         NVARCHAR(100)  NULL,
        [assigned_date]       DATETIME       NOT NULL
            CONSTRAINT [DF_KnitterAssignment_AssignedDate] DEFAULT(GETDATE()),

        CONSTRAINT [FK_KnitterAssignment_MasterPlanDetailSize]
            FOREIGN KEY ([MasterPlanDetailSizeId])
            REFERENCES [dbo].[MasterPlanDetailSize] ([id])
            ON DELETE CASCADE
    );

    -- One knitter assignment per size line (allows upsert on re-assign).
    CREATE UNIQUE INDEX [UX_KnitterAssignment_SizeLine]
        ON [dbo].[KnitterAssignment] ([MasterPlanDetailSizeId]);
END
GO

-- Add end_date to an already-created table (busy "to" date).
IF COL_LENGTH('dbo.KnitterAssignment', 'end_date') IS NULL
    ALTER TABLE [dbo].[KnitterAssignment] ADD [end_date] DATETIME NULL;
GO
