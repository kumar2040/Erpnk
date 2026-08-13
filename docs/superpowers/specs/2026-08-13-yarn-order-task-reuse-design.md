# Yarn Order Task Reuse Design

**Date:** 2026-08-13  
**Status:** Approved for implementation planning

## Purpose

When a BOM import request is placed, the system must create or reuse one Yarn Order task. A later import request is attached to the latest eligible Yarn Order only while that task has not been started by any assignee. The task must display the Yarn Order number and every production order number contained in it.

## Existing System and Scope

The existing BOM, Yarn Order, and task-management slices already provide the required write and read paths:

- `sp_SaveYarnOrder` creates `tbl_yarn_order` and `tbl_yarn_order_detail` records.
- `BomService.PlaceYarnOrderAsync` invokes that procedure.
- `BomController.AdvanceBomTasksAsync` completes BOM work and currently creates one manual task per production order.
- `PoTask.RefId` can link a task directly to a Yarn Order header.
- `sp_GetPoTask` supplies task cards and detail views.

No new vertical slice or database table is needed. The existing paths will be changed so Yarn Order creation and task creation/reuse occur atomically in the database. The controller's separate manual-task creation will be removed.

## User-Visible Behavior

### First import request

1. Create a Yarn Order header and its yarn lines.
2. Create one Stage 12 Yarn Order task linked with `PoTask.RefId = tbl_yarn_order.yo_id`.
3. Assign it to the configured Yarn-role users.
4. Show the Yarn Order number in the task title and show all contained production order numbers on the task card and detail view.

Example:

```text
Make yarn order - YO-000123
Orders: GT-26012
```

### Later import request

If the latest Yarn Order task is eligible, append or update the incoming yarn lines under its existing Yarn Order number and reuse the same task.

```text
Make yarn order - YO-000123
Orders: GT-26012, GT-26018
```

The response message tells the user whether the request created a new Yarn Order task or was appended to an existing one.

### Eligibility rule

A task may be reused only when all of these conditions are true:

- `PoTask.Stage = 12` (Yarn Order).
- The task is active and its stored status is Scheduled (`S`).
- `PoTask.RefId` identifies an existing active Yarn Order header.
- No active assignee has a `StartDate`.
- No active assignee has status In Progress (`P`) or Completed (`C`).

An overdue card whose stored status is still Scheduled remains eligible if no assignee has started it. Held, cancelled, completed, or in-progress tasks are never eligible. Once any assignee starts the task, every later import request creates a new Yarn Order and a new task.

## Transaction and Concurrency Design

`sp_SaveYarnOrder` owns the complete create-or-append decision in one transaction. This prevents two simultaneous import requests from both observing an eligible task and creating conflicting work.

The procedure will:

1. Parse and normalize the incoming JSON into a temporary row set, grouping duplicate incoming lines by production order, product, color, and ply.
2. Begin a transaction with `SET XACT_ABORT ON`.
3. Acquire a transaction-owned exclusive application lock for the Yarn Order request workflow using `sp_getapplock`.
4. Select the newest eligible Stage 12 task and its linked Yarn Order using locking reads.
5. Reuse its Yarn Order and task when eligible; otherwise create a new Yarn Order header.
6. Update matching detail lines and insert new detail lines. A repeated production-order/product/color/ply line is updated rather than duplicated; a different yarn line is appended.
7. Recalculate `total_kg`, `order_count`, and `line_count` from the persisted detail rows.
8. When a new Yarn Order is required, create its Stage 12 task through the existing task procedure, link `RefId` to the Yarn Order ID, and assign the configured Yarn-role users.
9. When reusing a task, keep its identity and add an update notification for its active assignees.
10. Commit and return the result metadata.

The procedure will follow the project's plain transaction convention rather than adding procedure-level `TRY/CATCH` ceremony. Any error rolls back the Yarn Order and task changes together.

## Write-Path Changes

### Stored procedure

`sp_SaveYarnOrder` gains the assignee information required to create the Stage 12 task and returns:

- `YoNo`
- `YoId`
- `TotalKg`
- `PoTaskId`
- `WasAppended`
- `OrderCount`
- `LineCount`
- procedure-authored `Message`
- `IsSuccess`

Task creation uses the existing task-management procedure and these values:

- Title: `Make yarn order - {YoNo}`
- Stage: `12`
- RefId: Yarn Order ID
- Priority: `2`
- Completion rule: `2` (any assignee)
- Assignees: configured Yarn-role users

The task's primary `OrderNo` remains a display-compatible value, but task-to-Yarn-Order navigation and order aggregation use `RefId` as the authoritative link.

### API service

`BomService` will resolve Yarn-role members through the existing role-management service and pass their user IDs to `sp_SaveYarnOrder`. It will continue returning `IResponse<PlaceYarnOrderResult>` and map the expanded procedure result. The database remains responsible for eligibility, validation, transaction handling, and the user-facing message.

The procedure call will use the project's existing `IGenericRepository` abstraction rather than adding another raw database-access path.

### Controller workflow

After a successful Yarn Order request, the existing BOM task memberships are completed as they are today. `AdvanceBomTasksAsync` will no longer create `Make yarn order` manual tasks, because the transaction has already created or reused exactly one Stage 12 task.

Failure while saving the Yarn Order prevents task creation/reuse. Failure in the later BOM-completion cleanup does not create a duplicate Yarn Order task.

## Task Read Model and Navigation

`sp_GetPoTask` will derive Stage 12 task information from `tbl_yarn_order_detail` through `PoTask.RefId`:

- `OrderNos`: distinct, sorted production order numbers joined for display.
- `OrderCount`: count of distinct production order numbers.
- `LinkUrl`: `/yarn-orders/{RefId}` when the linked header exists.

This applies to board cards, My Tasks, and task details. Existing `PoTaskOrder` behavior remains unchanged for other stages. `PoTaskOrder` is intentionally not used for Yarn Order membership because its active-order uniqueness rule conflicts with production orders that already belong to completed BOM tasks.

A legacy fallback may continue deriving a Yarn Order link from `PoTask.OrderNo`, but only `RefId`-linked Stage 12 tasks participate in automatic reuse.

## Notifications

Creating a new task uses the existing assignment notification. Appending to an unstarted task creates an update notification (`Kind = 'U'`) for its active assignees, identifying the Yarn Order number and newly attached production order numbers. The shared notification-kind documentation will be updated to include `U = task updated`.

## Legacy Data

Existing manual `Make yarn order - {OrderNo}` tasks have no reliable one-to-one Yarn Order reference and may include multiple tasks for one historical Yarn Order. They will not be automatically merged or converted. This avoids cancelling or combining user work based on ambiguous title/order matching.

After deployment, new requests create RefId-linked Stage 12 tasks and only those tasks are eligible for reuse. Historical tasks remain readable through the existing fallback behavior.

## Files Expected to Change

- `database/dbo/Procedure/sp_SaveYarnOrder.sql`
- `database/dbo/Procedure/sp_GetPoTask.sql`
- Existing shared BOM/task DTO files for the expanded response and notification kind
- Existing BOM service interface and implementation
- Existing BOM controller workflow
- Focused automated tests for response mapping and task-creation orchestration

No new UI page, manager, API controller, service slice, or table is required.

## Verification

Automated verification will cover:

- First request reports a newly created Yarn Order task.
- Later request maps an appended response to the same Yarn Order/task IDs.
- Controller workflow no longer creates a manual follow-up task.
- Existing BOM quantity behavior remains unchanged.
- Solution/unit-test builds pass.
- The database project compiles, if the installed SQL build tooling supports it.

Database acceptance checks after the user deploys the SQL scripts:

1. Place an import request with no eligible task: one Yarn Order and one Stage 12 task are created.
2. Place another request before any assignee starts it: the same Yarn Order number and task ID are returned, and both production order numbers are shown.
3. Start one assignee's task and place another request: a new Yarn Order number and task are created.
4. Hold, complete, or cancel a task and place another request: a new task is created.
5. Submit two requests concurrently: each request is persisted once and no duplicate eligible task is created.
6. Open the task: its link navigates to the exact Yarn Order identified by `RefId`.

The implementation will only prepare repository SQL files; it will not execute SQL against the user's database.
