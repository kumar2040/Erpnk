-- Ensure tables exist and populate them with sample data

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblcustomer')
BEGIN
    CREATE TABLE [dbo].[tblcustomer] (
        [customer_id] INT PRIMARY KEY IDENTITY(1,1),
        [customer_name] NVARCHAR(100) NOT NULL,
        [email] NVARCHAR(100),
        [phone] NVARCHAR(20),
        [city] NVARCHAR(50),
        [created_at] DATETIME DEFAULT GETDATE()
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_order')
BEGIN
    CREATE TABLE [dbo].[tbl_order] (
        [order_id] INT PRIMARY KEY IDENTITY(1,1),
        [order_date] DATETIME NOT NULL,
        [customer_id] INT,
        [status] NVARCHAR(50),
        [amount] DECIMAL(18, 2),
        CONSTRAINT FK_tbl_order_tblcustomer FOREIGN KEY ([customer_id]) REFERENCES [tblcustomer]([customer_id])
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_po')
BEGIN
    CREATE TABLE [dbo].[tbl_po] (
        [po_id] INT PRIMARY KEY IDENTITY(1,1),
        [po_number] NVARCHAR(50) NOT NULL,
        [order_id] INT,
        [supplier_name] NVARCHAR(100),
        [created_date] DATETIME DEFAULT GETDATE(),
        CONSTRAINT FK_tbl_po_tbl_order FOREIGN KEY ([order_id]) REFERENCES [tbl_order]([order_id])
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_stylesheet')
BEGIN
    CREATE TABLE [dbo].[tbl_stylesheet] (
        [stylesheet_id] INT PRIMARY KEY IDENTITY(1,1),
        [name] NVARCHAR(100) NOT NULL,
        [content] NVARCHAR(MAX),
        [is_active] BIT DEFAULT 1
    );
END

-- Populate tblcustomer if empty
IF (SELECT COUNT(*) FROM tblcustomer) = 0
BEGIN
    INSERT INTO tblcustomer (customer_name, email, phone, city) VALUES
    ('Global Retailers', 'contact@globalretail.com', '123-456-7890', 'New York'),
    ('Prime Supplies', 'info@primesupplies.net', '987-654-3210', 'London'),
    ('Elite Goods', 'sales@elitegoods.biz', '555-012-3456', 'Tokyo'),
    ('Standard Merchants', 'support@standard.com', '444-555-6666', 'Berlin'),
    ('Apex Traders', 'deals@apextraders.com', '333-222-1111', 'Paris');
END

-- Populate tbl_order if empty
IF (SELECT COUNT(*) FROM tbl_order) = 0
BEGIN
    INSERT INTO tbl_order (order_date, customer_id, status, amount) VALUES
    ('2026-01-10', 1, 'Running', 12500.00),
    ('2026-01-15', 2, 'Completed', 8900.50),
    ('2026-02-01', 3, 'NotStarted', 15000.00),
    ('2026-02-05', 4, 'Running', 5400.00),
    ('2026-02-10', 5, 'NotStarted', 21000.00),
    ('2025-12-01', 1, 'Completed', 3200.00),
    ('2024-11-15', 2, 'Completed', 4500.00);
END

-- Populate tbl_po if empty
IF (SELECT COUNT(*) FROM tbl_po) = 0
BEGIN
    INSERT INTO tbl_po (po_number, order_id, supplier_name) VALUES
    ('PO-2026-001', 1, 'Fabrics Inc.'),
    ('PO-2026-002', 2, 'Thread & Co.'),
    ('PO-2026-003', 3, 'Buttons Galore'),
    ('PO-2026-004', 4, 'Zippers Plus'),
    ('PO-2026-005', 5, 'Leather World');
END

-- Populate tbl_stylesheet if empty
IF (SELECT COUNT(*) FROM tbl_stylesheet) = 0
BEGIN
    INSERT INTO tbl_stylesheet (name, content, is_active) VALUES
    ('Modern Blue', 'background-color: midnightblue; color: white;', 1),
    ('Sleek Dark', 'background-color: #1a1a1a; color: #f0f0f0;', 1),
    ('Corporate Light', 'background-color: #ffffff; color: #333333;', 1);
END
