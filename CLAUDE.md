# NkplmErp Project Rules & Standards

## UI Guidelines (NkplmErp.Blazor)

### 1. Page File Structure
Every page uses three files:
- `PageName.razor` (markup)
- `PageName.razor.cs` (code-behind)
- `PageName.razor.css` (Blazor CSS isolation — scoped automatically to the page)

### 2. Dynamic UI Data
- Avoid hardcoding UI values.
- Pull list/dropdown data from `spDropdown` (one shared proc, keyed by `@Flag`) via `IDropdownManager.GetDropDownListAsync(flag, filter, filter2)`.
- Hardcoding is only allowed when it's genuinely the best approach — ask for approval with a reason first.

### 3. CSS Practices
- Component/page-specific styles go in that page's own `.razor.css` file.
- Cross-cutting styling uses Tailwind utility classes (`tailwind.input.css` → `tailwind.output.css`).
- `wwwroot/*.css` (`site.css`, `dashboard.css`, etc.) is for true app-wide concerns (layout shell, fonts, resets) only — not per-feature styling.

### 4. UI Manager Pattern
Each UI feature gets a `Services/<Feature>/Manager/` folder:
- `Interface/I{Name}Manager.cs`
- `Implementation/{Name}Manager.cs`
- `Route/{Name}Endpoint.cs` (route/URL constants)

### 5. Standard Response Wrapping
- Manager interfaces/implementations return `IResponse<T>` (`Response<T>.Success(data)` / `Response<T>.Fail(message)`), from `NkplmErp.Shared/Wrapper`.
  ```csharp
  Task<IResponse<ResponseModel>> FunctionNameAsync(RequestModel request);
  Task<IResponse<List<ResponseModel>>> FunctionNameAsync(RequestModel request);
  ```
- New managers must deserialize the `Response<T>` envelope, not call `ReadFromJsonAsync<T>` against a raw body — a mismatch here fails silently (empty UI, swallowed exception), not with a visible error. Check what the controller actually returns before wrapping — an endpoint can legitimately return a raw model instead of the envelope.

### 6. Dependency Injection
Inject shared services via `[Inject]`:
```csharp
[Inject] private ITaskManagementManager TaskMgr { get; set; } = default!;
```

### 7. Shared UI Components
Reusable, app-wide components live in `NkplmErp.Blazor/Components/` — e.g. `AutoCompleteSelect`, `DatePicker`, `PanelModal`. Same 3-file treatment: `Name.razor`, `Name.razor.cs`, `Name.razor.css`.

### 8. Shared HTTP Access
Typed API clients (`<Feature>ApiClient`, e.g. `PoTaskApiClient`) wrap `HttpClient` for the Blazor side. New features should use a typed client rather than calling `HttpClient` ad hoc from a manager.

### 9. Shared Models
- Request/response DTOs, grouped per feature: `NkplmErp.Shared/DTOs/<Feature>/`, referenced by both API and Blazor.
- Entities live in `NkplmErp.Domain/Entities/`.
- `NkplmErp.Blazor` has no project reference to `NkplmErp.API` — never `using NkplmErp.API.*` from Blazor. Anything that needs to be shared belongs in `NkplmErp.Shared`.
- Never duplicate a model definition between API and UI.

### 10. Shared wrappers / data access
`NkplmErp.Shared/Wrapper/` (`IResponse`, `Response<T>`) and `NkplmErp.Shared/DataAccess/` (`Dapper/`, `GenericRepository/`) hold every global, cross-domain wrapper and data-access helper. New global types go here, not into a feature folder.

### 11. Lookup Patterns
Prefer `Dictionary<string, T>` for repeated key lookups instead of repeated `FirstOrDefault` — O(1) instead of O(n) per lookup.

---

## API Guidelines (NkplmErp.API / NkplmErp.Application / NkplmErp.Infrastructure)

### 1. Controller Responsibilities
Controllers stay minimal — call the service, return its response. No business logic in a controller.
```csharp
public async Task<IActionResult> FunctionNameAsync(RequestModel request)
{
    var response = await _service.FunctionNameAsync(request);
    return Ok(response);
}
```
Always return `IActionResult`.

### 2. Service Layer Structure
- Interfaces: `NkplmErp.Application/Interfaces/<Feature>/I<Feature>Service.cs`
- Implementations: `NkplmErp.Infrastructure/Services/<Feature>/<Feature>Service.cs`
- All service methods return `IResponse<T>`.
- **No business logic in C#.** The stored procedure owns validation, conversion, and the user-facing message. The service's only logic is success/fail off what the proc returned — the message shown in the UI is the proc's own. Dates travel as strings end-to-end; the proc does `TRY_CONVERT`, never C# `DateTime.TryParse`.

### 3. Error Handling and Logging
- `try-catch` around service logic; log exceptions; return standardized failure responses.
- Procedures stay plain: `BEGIN TRANSACTION / logic / END` — no TRY/CATCH ceremony inside the proc.

### 4. Reusability
- Before scaffolding a new vertical slice, check whether an existing service/manager/proc already covers the need. If a screen already reads the data, reuse it; if something blocks reuse (a permission gate, a missing param), fix that instead of building a parallel stack.
- Extract repeated logic into reusable helpers rather than duplicating it.

### 5. Middleware for Cross-Cutting Concerns
Use middleware (e.g. `GlobalExceptionHandler`) for logging/auditing rather than duplicating it per controller action.

### 6. Configuration via DI
Store settings in `appsettings.json` (never commit secrets); register typed settings classes through DI.

### 7. Utility / Helper Placement
Reusable helpers (hashing, Excel generation, token utilities) belong in `NkplmErp.Infrastructure`, or `NkplmErp.Security` for auth/token concerns — not duplicated per feature.

### 8. API Simplicity
Keep endpoints simple, stable, and free of UI-specific branching. `NkplmErp.Maui` consumes the same API, so mobile-readiness is a real constraint, not aspirational.

### 9. Registering New Services — the step that's easiest to forget
Every new `I<Feature>Service` must be registered in `NkplmErp.API/Program.cs` (`AddScoped<I<Feature>Service, <Feature>Service>()`) — do this immediately when the interface is created, not last. Missing it doesn't fail at startup (DI validation doesn't catch it); it fails at request time with a 500 before any feature code runs.

---

## Database Guidelines (NkplmErp.Database)

- One database project: `database/NkplmErp.Database.sqlproj`. No per-domain database projects.
- Schema folders: `database/dbo/Table/`, `database/dbo/Procedure/`, `database/dbo/Function/`.
- Shared procs already in place: `spDropdown.sql` (every dropdown list, one proc keyed by `@Flag`), `spEmailSetting.sql`.
- Prefer one stored procedure per feature/page, using a `@Flag`/mode parameter to switch operations, mirroring the controller's actions.
- A table's `ALTER` script is not the whole job — fold new/changed columns into that table's own `CREATE` script under `dbo/Table/` too, or the file stops describing the real table.
- Never execute SQL against the user's database. Hand over the `.sql` file; the user deploys it. Verify what actually landed (re-query) before treating a change as live — don't assume the file "worked."

---

## Naming Rules

- Match names across every layer: `PoTask.razor` / `PoTaskManager.cs` / `IPoTaskManager.cs` / `PoTaskEndpoint.cs` / `PoTaskController.cs` / `PoTaskService.cs` / `IPoTaskService.cs` / `sp_GetPoTask.sql` / `PoTaskRequestModel.cs` / `PoTaskResponseModel.cs`.
- The same function name is used in UI code-behind, manager, controller, and service interface/implementation for the same action.
- Stored-procedure `@Flag`/`@StatusFlag` values follow the same short-code convention as the C# side (e.g. `S`/`P`/`C`/`O`/`H` matching `TaskStatus`).
- New feature folder names may be hyphenated (`Yarn-Orders`) with an underscored namespace (`Yarn_Orders`) — match whatever the closest existing sibling feature already does rather than inventing a third style.

---

## New Feature Flow (Vertical Slice — mandatory for every new feature since 2026-07-20)

Before step 1: check whether an existing service/manager/proc already covers the need. Build a new slice only when the data, proc, or write path is genuinely new — and say out loud what already exists and why it isn't enough before proposing a new stack.

1. `NkplmErp.Shared/DTOs/<Feature>/<Feature>RequestModel.cs` + `<Feature>ResponseModel.cs`
2. `NkplmErp.Application/Interfaces/<Feature>/I<Feature>Service.cs`
3. `NkplmErp.Infrastructure/Services/<Feature>/<Feature>Service.cs`
4. `NkplmErp.API/Controllers/<Feature>/<Feature>Controller.cs`
5. Register the service in `NkplmErp.API/Program.cs` — immediately, not last.
6. `NkplmErp.Blazor/Services/<Feature>/Manager/{Interface,Implementation,Route}/`
7. `NkplmErp.Blazor/Pages/<Feature>/<Feature>.razor` (+ `.razor.cs`, `.razor.css`)

- **API→DB:** `IGenericRepository` (never raw ADO.NET in new code).
- **UI→API:** `HttpClient` via the Manager pattern above.
- **Moving files between projects does not fix their namespace automatically** — a moved file can keep publishing into its old project's namespace and still compile green. After moving files, grep the declared namespace against the file's actual project.

---

## Workflow Rules

1. Render a real UI mockup before implementing any visual change — a prose description isn't sufficient.
2. Follow these rules by default. If an exception is genuinely required, ask before deviating and state the reason.
3. Prefer dynamic, reusable, maintainable architecture over hardcoded or duplicated solutions — but don't build reuse machinery for a need that isn't real yet.
4. When a rule conflicts with a real requirement, raise it and confirm the alternative before proceeding.
5. This document describes how code in this repo should look going forward — it is not a standing instruction to refactor existing files to match it. Apply it to new work and to whatever's already being touched; don't restructure anything else unless explicitly asked.
