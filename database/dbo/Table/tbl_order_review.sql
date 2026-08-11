CREATE TABLE [dbo].[tbl_order_review] (
    [id] int IDENTITY(1,1) NOT NULL,
    [order_no] varchar(100) NOT NULL,
    [remark] varchar(max) NOT NULL,
    [date_] datetime2(0) NOT NULL CONSTRAINT [DF_tbl_order_review_date] DEFAULT (getdate()),
    [user_] int NOT NULL,
    [meeting_dash] int NOT NULL CONSTRAINT [DF_tbl_order_review_meeting_dash] DEFAULT ((0)),
    [pc] int NOT NULL,
    CONSTRAINT [PK_tbl_order_review] PRIMARY KEY CLUSTERED ([id] ASC)
);

CREATE NONCLUSTERED INDEX [IX_tbl_order_review_OrderDate]
    ON [dbo].[tbl_order_review] ([order_no] ASC, [date_] DESC);
