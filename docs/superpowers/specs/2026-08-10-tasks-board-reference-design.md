# Tasks Board Reference Design

Date: 2026-08-10

## Objective

Restyle the existing `/tasks` page to match the approved compact Task Management reference while preserving all current dynamic data, filtering, permissions, navigation, modals, and task actions.

## Scope

The implementation changes only the existing Tasks page markup and its isolated stylesheet:

- `src/NkplmErp.Blazor/Pages/PoTasks/PoTasks.razor`
- `src/NkplmErp.Blazor/Pages/PoTasks/PoTasks.razor.css`

No API, database, DTO, manager, service, or code-behind behavior changes are required for this visual redesign.

## Approved Page Structure

### Header and controls

- Use the page title `Task Management`.
- Remove the `Team operations` subtitle.
- Keep the existing All/My tasks scope selector, date range, order search, factory filter, Report action, and Add task action.
- Render the order search as one compact control with a search icon and `Search Order No` placeholder. Do not render a separate visible label.
- Keep controls aligned in one compact toolbar where space allows and wrap them without overlap at narrower widths.

### Summary strip

- Keep all six dynamic values: Work Load, Scheduled, In Progress, Completed, On Hold, and Over Due.
- Present them in the approved compact horizontal status strip.
- Preserve the existing click behavior and selected-status behavior.
- Keep status identity consistent across the summary strip, board headers, and card rails.

### Board columns

- Preserve the current dynamic column selection and `VisibleColumns` behavior.
- In the normal workload view, render equal-width columns with compact headers.
- Column headers use solid status-colored pills with white text and the existing count.
- Header pills are 20px high with compact spacing. Decorative status icons are visually small and consistent.
- The scheduled-column add control is compact and aligned with the header pill.
- Columns retain their status-tinted border and light neutral interior.

### Task cards

- Preserve every current click target, task title selection rule, order detail action, metadata value, assignee rollup, attachment count, and priority value.
- Use compact card spacing and 11px card typography.
- Use regular weight for card titles to match the reference.
- Use black card text in the light interface; retain a readable light equivalent in dark mode.
- Add a 3px left status rail whose color matches the containing workflow column.
- Use 20px high rounded status/priority pills with 11px white labels on their colored backgrounds.
- Keep cards visually dense without clipping long titles or removing information.

## Status Mapping

The existing status flags remain the source of truth:

- `S`: Scheduled, blue/cyan
- `P`: In Progress, orange
- `H`: On Hold, gray/purple as already defined by the application
- `C`: Completed, green
- `O`: Over Due, red

The same status color must be used for the summary marker, column pill, column border, task-card left rail, and status pill where applicable.

## Responsive Behavior

- Preserve the current single-status flow view.
- Four-column workload layout remains the desktop target.
- At narrower widths, columns reflow without horizontal text clipping.
- Toolbar controls wrap as complete controls; the search icon and input must remain on one line.
- No fixed-width element may force the page beyond the available viewport.

## Accessibility

- The order search uses an accessible label even though no visible label is shown.
- Existing buttons and links remain semantic and keyboard reachable.
- Color is reinforced by status text and existing labels rather than being the only status indicator.
- Visible task-card text remains at least 11px.
- Focus styles remain visible for interactive controls.

## Consistency Constraints

- Keep all page-specific styles in `PoTasks.razor.css`.
- Do not introduce hardcoded counts or task content; all displayed values continue to come from the existing page state.
- Do not duplicate models, managers, or data-loading logic.
- Do not alter the current report route, task drawer, order modal, add-task modal, filtering callbacks, or polling/data-refresh behavior.
- Preserve unrelated user changes already present in the dirty worktree.

## Verification

- Build `NkplmErp.Blazor` without starting a second long-running app instance.
- Confirm the Tasks page renders in workload and single-status views.
- Confirm All/My tasks, date range, order search, factory selection, Report, Add task, task-card click, and order-detail click still work.
- Check desktop four-column layout and responsive two-column/one-column layouts.
- Compare title, toolbar, summary strip, column pills, card typography, colored rails, and status-pill sizing against the approved preview.

