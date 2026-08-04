/* ---------------------------------------------------------------------
   Yarn vendor order -> ARRIVED detail columns (weight, pragyapan no, LC/TT no).

   These ride alongside invoice_no as the "yarn physically arrived from the
   vendor" event (sp_ManageYarnOrder flag 'I'): entering an invoice number
   sets these too, and they are left untouched when an invoice is cleared
   (the correction path) so a prior arrival record isn't lost by mistake.

   All three are nullable: an order can be invoiced before every field is
   known, and blank is not an error state here.

   Re-runnable: each column is guarded, so applying this twice is a no-op.
   --------------------------------------------------------------------- */
IF COL_LENGTH('dbo.tbl_yarn_vendor_order', 'weight') IS NULL
    ALTER TABLE [dbo].[tbl_yarn_vendor_order] ADD [weight] decimal(18,3) NULL;
GO

IF COL_LENGTH('dbo.tbl_yarn_vendor_order', 'pragyapan_no') IS NULL
    ALTER TABLE [dbo].[tbl_yarn_vendor_order] ADD [pragyapan_no] varchar(50) NULL;
GO

IF COL_LENGTH('dbo.tbl_yarn_vendor_order', 'lc_tt_no') IS NULL
    ALTER TABLE [dbo].[tbl_yarn_vendor_order] ADD [lc_tt_no] varchar(50) NULL;
GO
