-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.PoTaskNotification  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[PoTaskNotification] (
    [NotificationId] int IDENTITY(1,1) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [PoTaskId] int NULL,
    [Kind] char(1) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Body] nvarchar(400) NULL,
    [IsRead] bit NOT NULL CONSTRAINT [DF_PoTaskNotification_Read] DEFAULT ((0)),
    [CreatedDate] datetime NOT NULL CONSTRAINT [DF_PoTaskNotification_Created] DEFAULT (getdate()),
    [IsPushed] bit NOT NULL CONSTRAINT [DF_PoTaskNotification_Pushed] DEFAULT ((0)),
    CONSTRAINT [PK_PoTaskNotification] PRIMARY KEY CLUSTERED ([NotificationId] ASC)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_PoTaskNotification_User] ON [dbo].[PoTaskNotification] ([UserId] ASC, [IsRead] ASC, [CreatedDate] DESC);
CREATE NONCLUSTERED INDEX [IX_PoTaskNotification_Pending] ON [dbo].[PoTaskNotification] ([IsPushed] ASC);
