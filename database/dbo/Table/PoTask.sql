-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.PoTask  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[PoTask] (
    [PoTaskId] int IDENTITY(1,1) NOT NULL,
    [OrderNo] nvarchar(50) NULL,
    [Stage] tinyint NOT NULL,
    [Status] char(1) NOT NULL CONSTRAINT [DF_PoTask_Status] DEFAULT ('S'),
    [FactoryType] nvarchar(100) NULL,
    [Guage] nvarchar(100) NULL,
    [Title] nvarchar(200) NULL,
    [Detail] nvarchar(max) NULL,
    [RefId] int NULL,
    [PriorityId] tinyint NULL,
    [NotificationDate] datetime NULL,
    [UpdateFrequency] tinyint NULL,
    [PlanningAction] tinyint NULL,
    [CompletionRule] tinyint NOT NULL CONSTRAINT [DF_PoTask_Rule] DEFAULT ((1)),
    [QuorumCount] int NULL,
    [BlockedReason] nvarchar(400) NULL,
    [StartDate] datetime NULL,
    [DueDate] datetime NULL,
    [CompletedDate] datetime NULL,
    [CreatedBy] nvarchar(450) NULL,
    [CreatedDate] datetime NOT NULL CONSTRAINT [DF_PoTask_Created] DEFAULT (getdate()),
    [ModifiedBy] nvarchar(450) NULL,
    [ModifiedDate] datetime NULL,
    [IsActive] bit NOT NULL CONSTRAINT [DF_PoTask_Active] DEFAULT ((1)),
    [LastReminderDate] datetime NULL,
    CONSTRAINT [PK_PoTask] PRIMARY KEY CLUSTERED ([PoTaskId] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_PoTask_OrderNo] ON [dbo].[PoTask] ([OrderNo] ASC);
CREATE NONCLUSTERED INDEX [IX_PoTask_Status_Stage] ON [dbo].[PoTask] ([Status] ASC, [Stage] ASC);
