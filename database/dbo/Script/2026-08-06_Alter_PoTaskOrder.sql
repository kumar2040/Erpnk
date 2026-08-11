/*
    Upgrade an existing dbo.PoTaskOrder table to the schema declared in
    dbo/Table/PoTaskOrder.sql.

    This script is intentionally idempotent. It does not attempt to invent
    values for the three required key columns when they are missing.
*/
SET NOCOUNT ON;

IF OBJECT_ID(N'[dbo].[PoTaskOrder]', N'U') IS NULL
    THROW 50001, 'dbo.PoTaskOrder does not exist. Deploy dbo/Table/PoTaskOrder.sql instead.', 1;

IF COL_LENGTH(N'dbo.PoTaskOrder', N'PoTaskOrderId') IS NULL
    THROW 50002, 'PoTaskOrderId is missing. It must be created as an IDENTITY column by rebuilding the table.', 1;

IF COL_LENGTH(N'dbo.PoTaskOrder', N'PoTaskId') IS NULL
    THROW 50003, 'PoTaskId is missing. Add and populate it before running this upgrade.', 1;

IF COL_LENGTH(N'dbo.PoTaskOrder', N'OrderNo') IS NULL
    THROW 50004, 'OrderNo is missing. Add and populate it before running this upgrade.', 1;

IF COL_LENGTH(N'dbo.PoTaskOrder', N'Status') IS NULL
    ALTER TABLE [dbo].[PoTaskOrder]
        ADD [Status] char(1) NOT NULL
            CONSTRAINT [DF_PoTaskOrder_Status] DEFAULT ('S') WITH VALUES;

IF COL_LENGTH(N'dbo.PoTaskOrder', N'ReviewId') IS NULL
    ALTER TABLE [dbo].[PoTaskOrder] ADD [ReviewId] int NULL;

IF COL_LENGTH(N'dbo.PoTaskOrder', N'AddedDate') IS NULL
    ALTER TABLE [dbo].[PoTaskOrder]
        ADD [AddedDate] datetime NOT NULL
            CONSTRAINT [DF_PoTaskOrder_AddedDate] DEFAULT (getdate()) WITH VALUES;

IF COL_LENGTH(N'dbo.PoTaskOrder', N'AddedBy') IS NULL
    ALTER TABLE [dbo].[PoTaskOrder] ADD [AddedBy] nvarchar(450) NULL;

IF COL_LENGTH(N'dbo.PoTaskOrder', N'CompletedDate') IS NULL
    ALTER TABLE [dbo].[PoTaskOrder] ADD [CompletedDate] datetime NULL;

IF COL_LENGTH(N'dbo.PoTaskOrder', N'IsActive') IS NULL
    ALTER TABLE [dbo].[PoTaskOrder]
        ADD [IsActive] bit NOT NULL
            CONSTRAINT [DF_PoTaskOrder_IsActive] DEFAULT ((1)) WITH VALUES;

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints
               WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[PoTaskOrder]')
                 AND [parent_column_id] = COLUMNPROPERTY(OBJECT_ID(N'[dbo].[PoTaskOrder]'), N'Status', 'ColumnId'))
    ALTER TABLE [dbo].[PoTaskOrder]
        ADD CONSTRAINT [DF_PoTaskOrder_Status] DEFAULT ('S') FOR [Status];

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints
               WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[PoTaskOrder]')
                 AND [parent_column_id] = COLUMNPROPERTY(OBJECT_ID(N'[dbo].[PoTaskOrder]'), N'AddedDate', 'ColumnId'))
    ALTER TABLE [dbo].[PoTaskOrder]
        ADD CONSTRAINT [DF_PoTaskOrder_AddedDate] DEFAULT (getdate()) FOR [AddedDate];

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints
               WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[PoTaskOrder]')
                 AND [parent_column_id] = COLUMNPROPERTY(OBJECT_ID(N'[dbo].[PoTaskOrder]'), N'IsActive', 'ColumnId'))
    ALTER TABLE [dbo].[PoTaskOrder]
        ADD CONSTRAINT [DF_PoTaskOrder_IsActive] DEFAULT ((1)) FOR [IsActive];

IF NOT EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[PoTaskOrder]')
      AND [type] = 'PK'
)
    ALTER TABLE [dbo].[PoTaskOrder]
        ADD CONSTRAINT [PK_PoTaskOrder]
            PRIMARY KEY CLUSTERED ([PoTaskOrderId] ASC);

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[PoTaskOrder]')
      AND [name] = N'FK_PoTaskOrder_PoTask'
)
    ALTER TABLE [dbo].[PoTaskOrder] WITH CHECK
        ADD CONSTRAINT [FK_PoTaskOrder_PoTask]
            FOREIGN KEY ([PoTaskId]) REFERENCES [dbo].[PoTask] ([PoTaskId]);

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[PoTaskOrder]')
      AND [name] = N'UX_PoTaskOrder_TaskOrder'
)
BEGIN
    IF EXISTS
    (
        SELECT 1 FROM [dbo].[PoTaskOrder]
        GROUP BY [PoTaskId], [OrderNo]
        HAVING COUNT(*) > 1
    )
        THROW 50005, 'Duplicate PoTaskId/OrderNo rows must be resolved before creating UX_PoTaskOrder_TaskOrder.', 1;

    CREATE UNIQUE NONCLUSTERED INDEX [UX_PoTaskOrder_TaskOrder]
        ON [dbo].[PoTaskOrder] ([PoTaskId] ASC, [OrderNo] ASC);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[PoTaskOrder]')
      AND [name] = N'UX_PoTaskOrder_ActiveOrder'
)
BEGIN
    IF EXISTS
    (
        SELECT 1 FROM [dbo].[PoTaskOrder]
        WHERE [IsActive] = 1
        GROUP BY [OrderNo]
        HAVING COUNT(*) > 1
    )
        THROW 50006, 'An active order belongs to multiple BOM tasks. Resolve it before creating UX_PoTaskOrder_ActiveOrder.', 1;

    CREATE UNIQUE NONCLUSTERED INDEX [UX_PoTaskOrder_ActiveOrder]
        ON [dbo].[PoTaskOrder] ([OrderNo] ASC)
        WHERE [IsActive] = 1;
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[PoTaskOrder]')
      AND [name] = N'IX_PoTaskOrder_TaskActive'
)
    CREATE NONCLUSTERED INDEX [IX_PoTaskOrder_TaskActive]
        ON [dbo].[PoTaskOrder] ([PoTaskId] ASC, [IsActive] ASC);

PRINT 'dbo.PoTaskOrder upgrade completed.';
