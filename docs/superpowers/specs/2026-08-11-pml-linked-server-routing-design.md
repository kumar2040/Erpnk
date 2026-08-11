# PML Linked-Server Routing Design

## Goal

Route the existing MySQL synchronization and mirroring procedures through the SQL Server linked server `PML` and its remote database `db_nature`, replacing the obsolete `MYSQL_NatureKnit` / `db_natureknit` routing.

## Scope

Update the four existing standalone procedure scripts that reference `MYSQL_NatureKnit`:

- `database/doPlan.sql`
- `database/saveKnitterAssignment.sql`
- `database/sp_SyncKnitterRecords.sql`
- `database/sp_SyncOrderReviews.sql`

Add one deployment script under `database/dbo/Script/` that targets `NatureKnit_test` and applies the four procedure definitions together.

## Behavior

- `doPlan` continues mirroring WEAVE plans to `tbl_weave_plandetail`.
- `saveKnitterAssignment` continues replacing the remote `tbl_production_plan_detail` rows for a plan.
- `sp_SyncKnitterRecords` continues incrementally reading knitter records by its existing watermarks.
- `sp_SyncOrderReviews` continues incrementally reading order reviews.
- Existing best-effort error handling, parameters, result sets, and transaction behavior remain unchanged.
- Only the linked-server and remote-database identifiers change:
  - `MYSQL_NatureKnit` becomes `PML`.
  - `db_natureknit` becomes `db_nature`.

## Deployment and Safety

The consolidated deployment script starts with `USE [NatureKnit_test]` and contains no data-deletion operation beyond the existing remote per-plan replacement inside `saveKnitterAssignment` when that procedure is later invoked normally. Creating or altering the procedures does not invoke them.

Per repository rules, Codex will not execute the deployment script against the database. The user will deploy it. Verification will consist of repository searches and static checks confirming that the obsolete identifiers are absent from the affected scripts and the deployment artifact targets `NatureKnit_test`.

## Success Criteria

- No `MYSQL_NatureKnit` or `db_natureknit` reference remains in the four affected procedure scripts.
- Each remote call uses linked server `PML` and database `db_nature`.
- A single test deployment artifact is available for `NatureKnit_test`.
- Existing unrelated working-tree changes are untouched.
