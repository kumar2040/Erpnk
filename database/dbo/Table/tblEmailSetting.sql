CREATE TABLE [dbo].[tblEmailSetting](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[EmailType] [nvarchar](300) NULL,
	[MailServer] [nvarchar](300) NULL,
	[Port] [nvarchar](300) NULL,
	[SenderName] [nvarchar](300) NULL,
	[SenderEmail] [nvarchar](300) NULL,
	[EmailFormat] [nvarchar](300) NULL,
	[Password] [nvarchar](300) NULL,
	[Username] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
