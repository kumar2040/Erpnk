/*
    Permanently removes all rows from dbo.tbl_order_review in NatureKnit_test
    and resets its identity counter.

    Run this script manually against the intended SQL Server instance.
*/

USE [NatureKnit_test];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'NatureKnit_test'
    THROW 50001, 'Safety check failed: this script must run in NatureKnit_test.', 1;

BEGIN TRANSACTION;

TRUNCATE TABLE [dbo].[tbl_order_review];

COMMIT TRANSACTION;

SELECT N'dbo.tbl_order_review truncated successfully.' AS [Result];
GO
