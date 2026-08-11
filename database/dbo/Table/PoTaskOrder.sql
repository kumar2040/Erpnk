CREATE TABLE [dbo].[PoTaskOrder] (
    [PoTaskOrderId] int IDENTITY(1,1) NOT NULL,
    [PoTaskId] int NOT NULL,
    [OrderNo] nvarchar(50) NOT NULL,
    [Status] char(1) NOT NULL CONSTRAINT [DF_PoTaskOrder_Status] DEFAULT ('S'),
    [ReviewId] int NULL,
    [AddedDate] datetime NOT NULL CONSTRAINT [DF_PoTaskOrder_AddedDate] DEFAULT (getdate()),
    [AddedBy] nvarchar(450) NULL,
    [CompletedDate] datetime NULL,
    [IsActive] bit NOT NULL CONSTRAINT [DF_PoTaskOrder_IsActive] DEFAULT ((1)),
    CONSTRAINT [PK_PoTaskOrder] PRIMARY KEY CLUSTERED ([PoTaskOrderId] ASC),
    CONSTRAINT [FK_PoTaskOrder_PoTask] FOREIGN KEY ([PoTaskId]) REFERENCES [dbo].[PoTask] ([PoTaskId])
);

CREATE UNIQUE NONCLUSTERED INDEX [UX_PoTaskOrder_TaskOrder]
    ON [dbo].[PoTaskOrder] ([PoTaskId] ASC, [OrderNo] ASC);

CREATE UNIQUE NONCLUSTERED INDEX [UX_PoTaskOrder_ActiveOrder]
    ON [dbo].[PoTaskOrder] ([OrderNo] ASC)
    WHERE [IsActive] = 1;

CREATE NONCLUSTERED INDEX [IX_PoTaskOrder_TaskActive]
    ON [dbo].[PoTaskOrder] ([PoTaskId] ASC, [IsActive] ASC);
