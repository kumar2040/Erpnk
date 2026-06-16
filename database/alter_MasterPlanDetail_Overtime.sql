USE [NatureKnit]
GO

/* Record whether a machine plan row used overtime and/or Saturday working.
     - [IsOvertime]    : 1 when overtime was applied
     - [OvertimeHours] : extra hours/day used (0 when no OT)
     - [WorkSaturday]  : 1 when Saturdays were treated as working days          */
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MasterPlanDetail]') AND name = 'IsOvertime')
    ALTER TABLE [dbo].[MasterPlanDetail] ADD [IsOvertime] BIT NOT NULL CONSTRAINT [DF_MasterPlanDetail_IsOvertime] DEFAULT(0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MasterPlanDetail]') AND name = 'OvertimeHours')
    ALTER TABLE [dbo].[MasterPlanDetail] ADD [OvertimeHours] DECIMAL(5,2) NOT NULL CONSTRAINT [DF_MasterPlanDetail_OvertimeHours] DEFAULT(0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[MasterPlanDetail]') AND name = 'WorkSaturday')
    ALTER TABLE [dbo].[MasterPlanDetail] ADD [WorkSaturday] BIT NOT NULL CONSTRAINT [DF_MasterPlanDetail_WorkSaturday] DEFAULT(0);
GO
