# Task Management Mockup Design

## Goal

Create a standalone `mocktask.html` concept for a modern Task Management page. The existing Blazor page, its route, and its supporting files must remain unchanged.

## Visual Direction

Use a clean light workspace with a restrained navy shell and the existing NatureKnit logo's blue-to-cyan gradient as the primary accent. The interface should feel contemporary and operational rather than decorative: generous spacing, clear type hierarchy, rounded cards, subtle borders, and minimal shadows.

## Page Structure

The mockup will include:

- A slim navy navigation sidebar with the existing logo treatment.
- A top header containing the page title, current context, search, notifications, and user identity.
- A compact filter toolbar for date range, order number, factory, gauge/method, and manual sync.
- Six clickable summary cards: Scheduled, In Progress, On Hold, Work Load, Completed, and Overdue.
- A responsive task board with Scheduled, In Progress, Completed, and Overdue columns.
- Sample task cards showing order number, gauge, date range, machine count, status, and quantity.

## Interaction

The file will be self-contained HTML, CSS, and JavaScript. Summary cards will filter or focus board columns, task search will filter sample cards, and compact controls will provide enough interaction to communicate the intended UX. No API calls or database integration are in scope.

## Responsive Behavior

The desktop view will show four board columns. Medium screens will use two columns, and narrow screens will stack the board and collapse the sidebar into a compact top treatment. Controls will wrap without horizontal page overflow.

## Isolation and Safety

Only a new `mocktask.html` file and this design document are in scope. Existing `.razor`, `.razor.cs`, `.razor.css`, routing, services, database scripts, and live URLs will not be modified.

## Verification

Open the standalone mockup locally in a browser and verify desktop layout, responsive behavior, filtering interactions, visual consistency with the logo palette, and absence of console errors. Confirm through Git status that no existing task-page source was changed.
