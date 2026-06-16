USE [NatureKnit]
GO

/* factory_type distinguishes which department a plan row belongs to
   ('knit' / 'weave' / 'silk' / 'other' / 'linen').
   weaveAnalysisForPlaning filters on factory_type = 'weave' and
   machinePlaning treats NULL/''/'knit' as knit rows, so doPlan must
   populate it (see doPlan.sql).                                       */
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[dbo].[MasterPlanDetail]')
      AND name = 'factory_type'
)
BEGIN
    ALTER TABLE [dbo].[MasterPlanDetail] ADD [factory_type] NVARCHAR(20) NULL;
END
GO
