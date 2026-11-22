/* =====================================================================
   sp_SyncOrderReviews — synchronize orders, reviews, collections,
   stylesheets, products, and cone/plm stock from the MySQL source
   exposed through linked server SERVER2012.

   Includes:
     1. dbo.tbl_order
     2. dbo.tbl_order_review (filtered date_ > '2026-10-08')
     3. dbo.tbl_order_collection
     4. dbo.tbl_stylesheet
     5. dbo.tblproduct
     6. dbo.tbl_cone_stock
     7. dbo.tbl_plm_stock
   ===================================================================== */
CREATE OR ALTER PROCEDURE [dbo].[sp_SyncOrderReviews]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @reviewsPulled int = 0;
    DECLARE @ordersPulled int = 0;
    DECLARE @collectionsPulled int = 0;
    DECLARE @stylesPulled int = 0;
    DECLARE @productsPulled int = 0;
    DECLARE @coneStockPulled int = 0;
    DECLARE @plmStockPulled int = 0;
    DECLARE @identityTable varchar(50) = NULL;

    BEGIN TRY
        /* -------------------------------------------------------------
           1. dbo.tbl_order
        ------------------------------------------------------------- */
        SET IDENTITY_INSERT dbo.tbl_order ON;
        SET @identityTable = 'dbo.tbl_order';

        INSERT INTO dbo.tbl_order
        (
            order_id, order_no, product_name, order_buyer, order_date, order_ldate,
            order_color, order_size, order_yarn, order_status, order_unit_price,
            order_pics, order_nk, order_ms, remark, xxxs, xxs, xs, s, m, l, xl,
            xxl, osfa, order_packing, [18m], [2y], [3y], [4y], [5y], [6y], [7y],
            tp, [8y], [9y], [10y], [11y], [12y], [14y], order_setting, xxxl, date_u,
            user_od, pcfab
        )
        SELECT
            q.order_id, q.order_no, q.product_name, q.order_buyer, q.order_date, q.order_ldate,
            q.order_color, q.order_size, q.order_yarn, q.order_status, q.order_unit_price,
            q.order_pics, q.order_nk, q.order_ms, q.remark, q.xxxs, q.xxs, q.xs, q.s, q.m, q.l, q.xl,
            q.xxl, q.osfa, q.order_packing, q.[18m], q.[2y], q.[3y], q.[4y], q.[5y], q.[6y], q.[7y],
            q.tp, q.[8y], q.[9y], q.[10y], q.[11y], q.[12y], q.[14y], q.order_setting, q.xxxl, q.date_u,
            q.user_od, q.pcfab
        FROM OPENQUERY
        (
            SERVER2012,
            'SELECT order_id, order_no, product_name, order_buyer, CAST(order_date AS DATE) AS order_date,
                    CAST(order_ldate AS DATE) AS order_ldate, order_color, order_size, order_yarn,
                    order_status, order_unit_price, order_pics, order_nk, order_ms, remark, xxxs, xxs, xs,
                    s, m, l, xl, xxl, osfa, order_packing, `18m`, `2y`, `3y`, `4y`, `5y`, `6y`, `7y`, tp,
                    `8y`, `9y`, `10y`, `11y`, `12y`, `14y`, order_setting, xxxl, CAST(date_u AS DATETIME) AS date_u,
                    user_od, pcfab
             FROM db_natureknit.tbl_order'
        ) AS q
        WHERE NOT EXISTS
        (
            SELECT 1 FROM dbo.tbl_order AS local_order WHERE local_order.order_id = q.order_id
        );

        SET @ordersPulled = @@ROWCOUNT;
        SET IDENTITY_INSERT dbo.tbl_order OFF;
        SET @identityTable = NULL;

        /* -------------------------------------------------------------
           2. dbo.tbl_order_review (date_ > '2026-10-08')
        ------------------------------------------------------------- */
        SET IDENTITY_INSERT dbo.tbl_order_review ON;
        SET @identityTable = 'dbo.tbl_order_review';

        INSERT INTO dbo.tbl_order_review
        (
            id, order_no, remark, date_, user_, meeting_dash, pc
        )
        SELECT
            q.id, q.order_no, q.remark, q.date_, q.user_, q.meeting_dash, q.pc
        FROM OPENQUERY
        (
            SERVER2012,
            'SELECT id, order_no, remark, date_, user_, meeting_dash, pc
             FROM db_natureknit.tbl_order_review
             WHERE date_ > ''2026-10-08'''
        ) AS q
        WHERE NOT EXISTS
        (
            SELECT 1 FROM dbo.tbl_order_review AS local_review WHERE local_review.id = q.id
        );

        SET @reviewsPulled = @@ROWCOUNT;
        SET IDENTITY_INSERT dbo.tbl_order_review OFF;
        SET @identityTable = NULL;

        /* -------------------------------------------------------------
           3. dbo.tbl_order_collection
        ------------------------------------------------------------- */
        SET IDENTITY_INSERT dbo.tbl_order_collection ON;
        SET @identityTable = 'dbo.tbl_order_collection';

        INSERT INTO dbo.tbl_order_collection
        (
            id, order_no, collection, typ, date_t, order_quick
        )
        SELECT
            q.id, q.order_no, q.collection, q.typ, q.date_t, q.order_quick
        FROM OPENQUERY
        (
            SERVER2012,
            'SELECT id, order_no, collection, typ,
                    CAST(NULLIF(date_t, ''0000-00-00 00:00:00'') AS DATETIME) AS date_t,
                    order_quick
             FROM db_natureknit.tbl_order_collection'
        ) AS q
        WHERE NOT EXISTS
        (
            SELECT 1 FROM dbo.tbl_order_collection AS local_coll WHERE local_coll.id = q.id
        );

        SET @collectionsPulled = @@ROWCOUNT;
        SET IDENTITY_INSERT dbo.tbl_order_collection OFF;
        SET @identityTable = NULL;

        /* -------------------------------------------------------------
           4. dbo.tbl_stylesheet
        ------------------------------------------------------------- */
        SET IDENTITY_INSERT dbo.tbl_stylesheet ON;
        SET @identityTable = 'dbo.tbl_stylesheet';

        INSERT INTO dbo.tbl_stylesheet
        (
            style_id, style_no, style_date, style_status, style_buyer, style_product,
            style_discription, style_remark, style_ref, style_knit, style_print,
            style_tailor, style_product_id, style_weight, style_cat, style_structure,
            style_target, style_osfa, style_osize, style_color, style_ply, style_embd,
            style_oven, stripom, tyeom, stromcol, tieomcol, multipleyan, style_person,
            collection_type, nolink, spacedye, type, dep, style_guage, style_comp,
            style_comp_rem, style_printime, style_print_f, stylr_emb_time, style_tailor_type,
            tailoring_charge, net_wet, child_typ, woven_w, woven_l, woven_fringe,
            loom_type, weave_factory, final_measurement, quick_style, parent_style,
            computer_y, tailoring_target, child_size
        )
        SELECT
            q.style_id, q.style_no, q.style_date, q.style_status, q.style_buyer, q.style_product,
            q.style_discription, q.style_remark, q.style_ref, q.style_knit, q.style_print,
            q.style_tailor, q.style_product_id, q.style_weight, q.style_cat, q.style_structure,
            q.style_target, q.style_osfa, q.style_osize, q.style_color, q.style_ply, q.style_embd,
            q.style_oven, q.stripom, q.tyeom, q.stromcol, q.tieomcol, q.multipleyan, q.style_person,
            q.collection_type, q.nolink, q.spacedye, q.type, q.dep, q.style_guage, q.style_comp,
            q.style_comp_rem, q.style_printime, q.style_print_f, q.stylr_emb_time, q.style_tailor_type,
            q.tailoring_charge, q.net_wet, q.child_typ, q.woven_w, q.woven_l, q.woven_fringe,
            q.loom_type, q.weave_factory, q.final_measurement, q.quick_style, q.parent_style,
            q.computer_y, q.tailoring_target, q.child_size
        FROM OPENQUERY
        (
            SERVER2012,
            'SELECT style_id, style_no, CAST(NULLIF(style_date, ''0000-00-00'') AS DATE) AS style_date,
                    style_status, style_buyer, style_product, style_discription, style_remark, style_ref,
                    style_knit, style_print, style_tailor, style_product_id, style_weight, style_cat,
                    style_structure, style_target, style_osfa, style_osize, style_color, style_ply,
                    style_embd, style_oven, stripom, tyeom, stromcol, tieomcol, multipleyan, style_person,
                    collection_type, nolink, spacedye, type, dep, style_guage, style_comp, style_comp_rem,
                    style_printime, style_print_f, stylr_emb_time, style_tailor_type, tailoring_charge,
                    net_wet, child_typ, woven_w, woven_l, woven_fringe, loom_type, weave_factory,
                    final_measurement, quick_style, parent_style, computer_y, tailoring_target, child_size
             FROM db_natureknit.tbl_stylesheet'
        ) AS q
        WHERE NOT EXISTS
        (
            SELECT 1 FROM dbo.tbl_stylesheet AS local_style WHERE local_style.style_id = q.style_id
        );

        SET @stylesPulled = @@ROWCOUNT;
        SET IDENTITY_INSERT dbo.tbl_stylesheet OFF;
        SET @identityTable = NULL;

        /* -------------------------------------------------------------
           5. dbo.tblproduct
        ------------------------------------------------------------- */
        SET IDENTITY_INSERT dbo.tblproduct ON;
        SET @identityTable = 'dbo.tblproduct';

        INSERT INTO dbo.tblproduct
        (
            product_id, category_id, product_name, product_desc, product_mprice,
            productshowsell, showatfront, product_entrydate, product_quantity,
            product_code, hide, color, box_id, count1, count2, min_wt_value,
            cpy, dye_type, unit, sq, wastage
        )
        SELECT
            q.product_id, q.category_id, q.product_name, q.product_desc, q.product_mprice,
            q.productshowsell, q.showatfront, q.product_entrydate, q.product_quantity,
            q.product_code, q.hide, q.color, q.box_id, q.count1, q.count2, q.min_wt_value,
            q.cpy, q.dye_type, q.unit, q.sq, q.wastage
        FROM OPENQUERY
        (
            SERVER2012,
            'SELECT product_id, category_id, product_name, product_desc, product_mprice,
                    productshowsell, showatfront,
                    CAST(NULLIF(product_entrydate, ''0000-00-00'') AS DATE) AS product_entrydate,
                    product_quantity, product_code, hide, color, box_id, count1, count2, min_wt_value,
                    cpy, dye_type, unit, sq, wastage
             FROM db_natureknit.tblproduct'
        ) AS q
        WHERE NOT EXISTS
        (
            SELECT 1 FROM dbo.tblproduct AS local_prod WHERE local_prod.product_id = q.product_id
        );

        SET @productsPulled = @@ROWCOUNT;
        SET IDENTITY_INSERT dbo.tblproduct OFF;
        SET @identityTable = NULL;

        /* -------------------------------------------------------------
           6. dbo.tbl_cone_stock
        ------------------------------------------------------------- */
        SET IDENTITY_INSERT dbo.tbl_cone_stock ON;
        SET @identityTable = 'dbo.tbl_cone_stock';

        INSERT INTO dbo.tbl_cone_stock
        (
            id, box_id, p_code, p_color, p_lot, lc, cn_date, date_, user_id,
            status, reg, p_wt, active, o_wt, to_date, verify, vf_date,
            vff_status, vff_date, for_use
        )
        SELECT
            q.id, q.box_id, q.p_code, q.p_color, q.p_lot, q.lc, q.cn_date, q.date_, q.user_id,
            q.status, q.reg, q.p_wt, q.active, q.o_wt, q.to_date, q.verify, q.vf_date,
            q.vff_status, q.vff_date, q.for_use
        FROM OPENQUERY
        (
            SERVER2012,
            'SELECT id, box_id, p_code, p_color, p_lot, lc,
                    CAST(NULLIF(cn_date, ''0000-00-00'') AS DATE) AS cn_date,
                    CAST(NULLIF(date_, ''0000-00-00 00:00:00'') AS DATETIME) AS date_,
                    user_id, status, reg, p_wt, active, o_wt,
                    CAST(NULLIF(to_date, ''0000-00-00'') AS DATE) AS to_date,
                    verify,
                    CAST(NULLIF(vf_date, ''0000-00-00'') AS DATE) AS vf_date,
                    vff_status,
                    CAST(NULLIF(vff_date, ''0000-00-00 00:00:00'') AS DATETIME) AS vff_date,
                    for_use
             FROM db_natureknit.tbl_cone_stock'
        ) AS q
        WHERE NOT EXISTS
        (
            SELECT 1 FROM dbo.tbl_cone_stock AS local_cone WHERE local_cone.id = q.id
        );

        SET @coneStockPulled = @@ROWCOUNT;
        SET IDENTITY_INSERT dbo.tbl_cone_stock OFF;
        SET @identityTable = NULL;

        /* -------------------------------------------------------------
           7. dbo.tbl_plm_stock
        ------------------------------------------------------------- */
        SET IDENTITY_INSERT dbo.tbl_plm_stock ON;
        SET @identityTable = 'dbo.tbl_plm_stock';

        INSERT INTO dbo.tbl_plm_stock
        (
            id, po, product_id, color, cone, date_, status, knitter,
            lot_no, ex, weight, parent, veryfy, vf, vfd
        )
        SELECT
            q.id, q.po, q.product_id, q.color, q.cone,
            ISNULL(q.date_, '1900-01-01'),
            ISNULL(q.status, 1),
            ISNULL(q.knitter, 0),
            q.lot_no, q.ex,
            ISNULL(q.weight, 0.0),
            ISNULL(q.parent, '0'),
            ISNULL(q.veryfy, 0),
            ISNULL(q.vf, 0),
            ISNULL(q.vfd, '1900-01-01')
        FROM OPENQUERY
        (
            SERVER2012,
            'SELECT id, po, product_id, color, cone,
                    CAST(IFNULL(NULLIF(date_, ''0000-00-00 00:00:00''), ''1900-01-01 00:00:00'') AS DATETIME) AS date_,
                    IFNULL(status, 1) AS status,
                    IFNULL(knitter, 0) AS knitter,
                    lot_no, ex,
                    IFNULL(weight, 0.0) AS weight,
                    IFNULL(parent, ''0'') AS parent,
                    IFNULL(veryfy, 0) AS veryfy,
                    IFNULL(vf, 0) AS vf,
                    CAST(IFNULL(NULLIF(vfd, ''0000-00-00 00:00:00''), ''1900-01-01 00:00:00'') AS DATETIME) AS vfd
             FROM db_natureknit.tbl_plm_stock'
        ) AS q
        WHERE NOT EXISTS
        (
            SELECT 1 FROM dbo.tbl_plm_stock AS local_plm WHERE local_plm.id = q.id
        );

        SET @plmStockPulled = @@ROWCOUNT;
        SET IDENTITY_INSERT dbo.tbl_plm_stock OFF;
        SET @identityTable = NULL;

    END TRY
    BEGIN CATCH
        IF @identityTable IS NOT NULL
        BEGIN
            DECLARE @sqlOff nvarchar(200) = N'SET IDENTITY_INSERT ' + @identityTable + N' OFF;';
            BEGIN TRY
                EXEC sp_executesql @sqlOff;
            END TRY
            BEGIN CATCH
            END CATCH;
        END;

        THROW;
    END CATCH;

    -- Return scalar review count as primary for C# API wrapper compatibility
    SELECT
        @reviewsPulled AS pulled,
        @ordersPulled AS orders_pulled,
        @collectionsPulled AS collections_pulled,
        @stylesPulled AS styles_pulled,
        @productsPulled AS products_pulled,
        @coneStockPulled AS cone_stock_pulled,
        @plmStockPulled AS plm_stock_pulled;
END;
