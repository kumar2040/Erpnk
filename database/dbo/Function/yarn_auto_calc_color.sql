-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.yarn_auto_calc_color  (SQL_SCALAR_FUNCTION)

CREATE FUNCTION dbo.yarn_auto_calc_color (@id VARCHAR(50), @cl VARCHAR(50))
RETURNS VARCHAR(50)
AS
BEGIN
    DECLARE @select_var VARCHAR(30);

    -- We use TOP 1 and COALESCE to handle the "not found" logic efficiently
    SELECT @select_var = CASE 
                            WHEN u_color = 1 THEN 'ivory' 
                            ELSE color_name 
                         END
    FROM tbl_color_yarn
    WHERE product_id = @id AND color_name = @cl;

    -- If no row was found, @select_var remains NULL, so we set the default
    IF @select_var IS NULL 
        SET @select_var = 'Ivory';

    RETURN @select_var;
END;
