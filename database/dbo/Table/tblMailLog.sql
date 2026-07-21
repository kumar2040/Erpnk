CREATE TABLE [dbo].[tblMailLog](
	[mail_id] [int] IDENTITY(1,1) NOT NULL,
	[mail_to] [nvarchar](500) NOT NULL,
	[mail_cc] [nvarchar](500) NULL,
	[subject] [nvarchar](255) NOT NULL,
	[body] [nvarchar](max) NOT NULL,
	[mail_type] [varchar](40) NOT NULL,
	[is_sent] [bit] NOT NULL,
	[retry_count] [int] NOT NULL,
	[error_msg] [nvarchar](500) NULL,
	[sent_date] [datetime] NULL,
	[created_date] [datetime] NOT NULL,
	[created_by] [varchar](50) NULL,
PRIMARY KEY CLUSTERED 
(
	[mail_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[tblMailLog] ADD  DEFAULT ((0)) FOR [is_sent]
GO

ALTER TABLE [dbo].[tblMailLog] ADD  DEFAULT ((0)) FOR [retry_count]
GO

ALTER TABLE [dbo].[tblMailLog] ADD  DEFAULT (getdate()) FOR [created_date]
GO

