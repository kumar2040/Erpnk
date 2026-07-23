/*==============================================================================
  spDropdown  —  one source for every dropdown option list in the app

  Every dropdown asks this proc for its options by name, so a list can grow
  without an application redeploy: add the row here, ALTER the proc, done.

  Parameters
    @Type      which list to return (see below)
    @Filter1   optional cascade key, meaning depends on the type
    @Filter2   optional second cascade key

  Types
    'YarnOrderStatus'   Order state filter on /yarn-orders. 'Pending' and any
                        later state slot in as another row in the VALUES list.

  Contract — every type MUST return exactly these two columns, these names, so
  they bind to DropDownListModel. Dapper has MatchNamesWithUnderscores off in
  this project, so an alias that doesn't match PascalCase binds silently to null.

    [Id]     the code stored / sent back to the API
    [Value]  what the user reads

  REAL OPTIONS ONLY. The leading row is the control's business, not the data's:
  AutoCompleteSelect prepends "All" (id -1) or "Select" (id 0) from its All
  parameter. Emitting it here would mean every caller either filters it back out
  or ships it to the server as a bogus filter value.

  An unknown type returns an empty rowset, so a mistyped Type renders a dropdown
  holding just its leading row instead of erroring.
==============================================================================*/
CREATE OR ALTER PROCEDURE [dbo].[spDropdown]
    @Type    NVARCHAR(50),
    @Filter1 NVARCHAR(200) = NULL,
    @Filter2 NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Flag NVARCHAR(50) = UPPER(LTRIM(RTRIM(@Type)));

    /* -------------------------------------------------- YarnOrderStatus
       Ordered = the yarn order has at least one vendor order placed against it.
       The codes here are what sp_GetYarnOrders takes as @Status. */
    IF (@Flag = 'YARNORDERSTATUS')
    BEGIN
        SELECT [Id], [Value]
        FROM (VALUES
            ('O', 'Ordered',     1),
            ('N', 'Not ordered', 2)
        ) AS v([Id], [Value], [SortOrder])
        ORDER BY [SortOrder];
        RETURN;
    END

    /* Unknown type -> empty list, shaped so the caller still binds cleanly. */
    SELECT [Id], [Value]
    FROM (VALUES ('', '')) AS v([Id], [Value])
    WHERE 1 = 0;
END
