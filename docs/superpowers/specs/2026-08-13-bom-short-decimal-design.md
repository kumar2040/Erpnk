# BOM Short Quantity Decimal Design

## Goal

The BOM requirement table must preserve the calculated decimal shortage instead of rounding it up to a whole kilogram. For a calculated shortage of 2.42 kg, the editable `SHORT` quantity and the yarn-order quantity must default to 2.42 kg rather than 3 kg.

## Scope

- Change the default order quantity for an import line from the ceiling of the shortage to the exact positive shortage.
- Keep the quantity editable and reject negative values as today.
- Change the numeric input increment from 0.5 kg to 0.01 kg so the browser accepts two-decimal quantities.
- Display the `SHORT` total with two decimal places.
- Preserve the existing import/stock decision, basket aggregation, and API save flow.

No stored procedure, endpoint, database schema, or CSS change is required. The procedure already returns `ShortfallKg` as `DECIMAL(18,3)`, and the existing DTO/API path uses `decimal` end-to-end.

## Data Flow

`knitYarnRequirement` returns the signed decimal shortage. `BomYarnLineDto.ImportKg` converts a negative shortage to its positive import quantity. `OrderQtyKg` will default directly to that exact quantity, flow into the BOM basket, and be sent unchanged as `YarnOrderLineDto.ImportKg` when the yarn order is placed.

## UI Behavior

- Example row: `ImportKg = 2.42` displays an editable value of `2.42 kg`.
- The helper text continues to display `need 2.42`.
- The total shortage cell uses two decimal places instead of whole-number formatting.
- Users may override the suggested quantity in 0.01 kg increments.

## Validation and Tests

Add a unit regression test for `BomYarnLineDto` proving that a `ShortfallKg` of `-2.42m` produces both `ImportKg` and the default `OrderQtyKg` of `2.42m`. Retain coverage for non-short lines and the existing non-negative override behavior where practical. Run the focused unit test, the unit-test project, and a Blazor build after the implementation.
