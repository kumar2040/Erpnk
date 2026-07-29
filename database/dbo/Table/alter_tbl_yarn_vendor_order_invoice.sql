/* ---------------------------------------------------------------------
   Yarn vendor order -> INVOICE columns.

   Entering an invoice number is the "the yarn physically arrived from the
   vendor and is ready for use" event: it completes that vendor sub-order.
   A parent yarn order is only complete once EVERY vendor sub-order under
   it carries an invoice number (see sp_GetYarnOrders @Status 'C').

   invoice_no is deliberately nullable and NOT unique: vendors reuse and
   re-issue numbers, and a blank means "still outstanding" -- that blank
   is what the Pending/Completed split is computed from, so it must stay
   representable. Clearing it back to NULL reopens the order.

   Re-runnable: each column is guarded, so applying this twice is a no-op.
   --------------------------------------------------------------------- */
IF COL_LENGTH('dbo.tbl_yarn_vendor_order', 'invoice_no') IS NULL
    ALTER TABLE [dbo].[tbl_yarn_vendor_order] ADD [invoice_no] varchar(50) NULL;
GO

IF COL_LENGTH('dbo.tbl_yarn_vendor_order', 'invoice_date') IS NULL
    ALTER TABLE [dbo].[tbl_yarn_vendor_order] ADD [invoice_date] datetime NULL;
GO

IF COL_LENGTH('dbo.tbl_yarn_vendor_order', 'invoice_by') IS NULL
    ALTER TABLE [dbo].[tbl_yarn_vendor_order] ADD [invoice_by] varchar(50) NULL;
GO

/* sp_GetYarnOrders filters the whole list on "does this header still have a
   vendor order with a blank invoice_no", once per header row. Indexing the
   (yo_id, invoice_no) pair lets that EXISTS/NOT EXISTS be answered from the
   index instead of touching the table. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_vyo_yo_id_invoice'
                 AND object_id = OBJECT_ID('dbo.tbl_yarn_vendor_order'))
    CREATE NONCLUSTERED INDEX [IX_vyo_yo_id_invoice]
        ON [dbo].[tbl_yarn_vendor_order] ([yo_id] ASC, [invoice_no] ASC);
GO
