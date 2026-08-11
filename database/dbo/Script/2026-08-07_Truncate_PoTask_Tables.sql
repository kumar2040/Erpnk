/*
    Permanently removes all data from the PO task tables in NatureKnit_test.
    TRUNCATE resets identity values. Foreign keys are dropped and recreated
    because SQL Server does not allow a referenced table to be truncated while
    a foreign key exists, even when the referencing table is empty.

    Run this script manually against the intended SQL Server instance.
*/

USE [NatureKnit_test];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'NatureKnit_test'
    THROW 50001, 'Safety check failed: this script must run in NatureKnit_test.', 1;

BEGIN TRANSACTION;

ALTER TABLE [dbo].[PoTaskAssignee]
    DROP CONSTRAINT [FK_PoTaskAssignee_PoTask];

ALTER TABLE [dbo].[PoTaskAttachment]
    DROP CONSTRAINT [FK_PoTaskAttachment_PoTask];

ALTER TABLE [dbo].[PoTaskChecklist]
    DROP CONSTRAINT [FK_PoTaskChecklist_PoTask];

ALTER TABLE [dbo].[PoTaskOrder]
    DROP CONSTRAINT [FK_PoTaskOrder_PoTask];

ALTER TABLE [dbo].[PoTaskGroupMember]
    DROP CONSTRAINT [FK_PoTaskGroupMember_Group];

TRUNCATE TABLE [dbo].[PoPlanSnapshot];
TRUNCATE TABLE [dbo].[PoTaskAssignee];
TRUNCATE TABLE [dbo].[PoTaskAttachment];
TRUNCATE TABLE [dbo].[PoTaskChecklist];
TRUNCATE TABLE [dbo].[PoTaskGroupMember];
TRUNCATE TABLE [dbo].[PoTaskHistory];
TRUNCATE TABLE [dbo].[PoTaskNotification];
TRUNCATE TABLE [dbo].[PoTaskOrder];
TRUNCATE TABLE [dbo].[PoTask];
TRUNCATE TABLE [dbo].[PoTaskGroup];

ALTER TABLE [dbo].[PoTaskAssignee] WITH CHECK
    ADD CONSTRAINT [FK_PoTaskAssignee_PoTask]
        FOREIGN KEY ([PoTaskId]) REFERENCES [dbo].[PoTask] ([PoTaskId]);

ALTER TABLE [dbo].[PoTaskAttachment] WITH CHECK
    ADD CONSTRAINT [FK_PoTaskAttachment_PoTask]
        FOREIGN KEY ([PoTaskId]) REFERENCES [dbo].[PoTask] ([PoTaskId]);

ALTER TABLE [dbo].[PoTaskChecklist] WITH CHECK
    ADD CONSTRAINT [FK_PoTaskChecklist_PoTask]
        FOREIGN KEY ([PoTaskId]) REFERENCES [dbo].[PoTask] ([PoTaskId]);

ALTER TABLE [dbo].[PoTaskOrder] WITH CHECK
    ADD CONSTRAINT [FK_PoTaskOrder_PoTask]
        FOREIGN KEY ([PoTaskId]) REFERENCES [dbo].[PoTask] ([PoTaskId]);

ALTER TABLE [dbo].[PoTaskGroupMember] WITH CHECK
    ADD CONSTRAINT [FK_PoTaskGroupMember_Group]
        FOREIGN KEY ([GroupId]) REFERENCES [dbo].[PoTaskGroup] ([GroupId]);

COMMIT TRANSACTION;

SELECT N'PoTask tables truncated successfully.' AS [Result];
GO
