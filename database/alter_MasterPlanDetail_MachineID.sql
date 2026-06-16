USE [NatureKnit]
GO

/* Add a dedicated MachineID column so the machine name (e.g. KN-56) and the
   machine id (e.g. 25) are stored in their own columns:
     - [Machine]   = machine name / no  (KN-56)
     - [MachineID] = numeric machine id (25)                               */
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[dbo].[MasterPlanDetail]')
      AND name = 'MachineID'
)
BEGIN
    ALTER TABLE [dbo].[MasterPlanDetail] ADD [MachineID] INT NULL;
END
GO
