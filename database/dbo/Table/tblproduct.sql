-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Table: dbo.tblproduct  (columns + PK + defaults + identity; indexes/FKs appended below)
CREATE TABLE [dbo].[tblproduct] (
    [product_id] int IDENTITY(1067,1) NOT NULL,
    [category_id] int NULL CONSTRAINT [DF__tblproduc__categ__7B5B524B] DEFAULT ((0)),
    [product_name] varchar(200) NULL CONSTRAINT [DF__tblproduc__produ__7C4F7684] DEFAULT (''),
    [product_desc] varbinary(max) NULL,
    [product_mprice] decimal(10,2) NULL CONSTRAINT [DF__tblproduc__produ__7D439ABD] DEFAULT ((0.00)),
    [productshowsell] char(1) NULL CONSTRAINT [DF__tblproduc__produ__7E37BEF6] DEFAULT ('N'),
    [showatfront] char(1) NULL CONSTRAINT [DF__tblproduc__showa__7F2BE32F] DEFAULT ('N'),
    [product_entrydate] date NULL CONSTRAINT [DF__tblproduc__produ__00200768] DEFAULT (getdate()),
    [product_quantity] int NULL,
    [product_code] varchar(255) NULL,
    [hide] int NULL,
    [color] varchar(200) NULL,
    [box_id] varchar(100) NULL,
    [count1] varchar(100) NULL,
    [count2] varchar(100) NULL,
    [min_wt_value] float NULL,
    [cpy] varchar(100) NULL,
    [dye_type] varchar(20) NULL,
    [unit] varchar(50) NULL,
    [sq] int NULL CONSTRAINT [DF__tblproduct__sq__01142BA1] DEFAULT ((0)),
    [wastage] int NULL CONSTRAINT [DF__tblproduc__wasta__7AF13DF7] DEFAULT ((0)),
    CONSTRAINT [PK__tblprodu__47027DF59F030272] PRIMARY KEY CLUSTERED ([product_id] ASC)
);
