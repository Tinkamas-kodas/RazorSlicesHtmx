---
goal: Add Time Tracking Card Editing Through a Shared Form
version: 1.0
date_created: 2026-08-08
last_updated: 2026-08-08
owner: Apskaita
status: 'Planned'
tags: [feature, time-tracking, htmx, razor-slices]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Make each rendered time entry keyboard- and pointer-activatable, open its existing values in a modal dialog, and provide save, delete, and copy commands. Use one shared Razor slice for form fields and separate create/edit dialog shells so each workflow owns its command buttons.

## 1. Requirements & Constraints

- **REQ-001**: Activating a time entry card must open an edit dialog for that entry.
- **REQ-002**: Create and edit must use separate dialog slices that both render one shared form-fields slice; field markup duplication is prohibited.
- **REQ-003**: Edit mode must load and allow updates to date, start time, duration, hourly-rate override, project, and description.
- **REQ-004**: A successful edit must close the dialog, emit `time-tracking-list-refresh`, refresh only `#time-tracking-grid`, and show a success toast.
- **REQ-005**: Validation failures must keep the dialog open and preserve all submitted values.
- **REQ-006**: GET/POST edit and GET/POST delete requests for a missing entry must return HTTP 404 without creating, updating, or deleting data.
- **REQ-007**: The edit dialog must provide `Save`, `Delete`, and `Copy` buttons in addition to modal close/cancel behavior.
- **REQ-008**: `Delete` in the edit dialog must request `GET delete/{id}` and replace the edit dialog with an explicit confirmation dialog. Confirming must send `POST delete/{id}`, then close the dialog and refresh only the active grid.
- **REQ-009**: `Copy` must submit the current edit form values to `POST create`, create a new entry, preserve the source entry, close the dialog, and refresh only the active grid.
- **REQ-010**: Replace the Default/Override radio pair with one `Override` checkbox styled as a Bootstrap toggle button.
- **REQ-011**: Render the hourly-rate number input and the `Override` toggle in one horizontal input-group row, with the number input first and the toggle immediately to its right.
- **REQ-012**: When Override is unchecked, clear and disable `HourlyRate`; when checked, enable and focus the input and require a value before browser submission.
- **SEC-001**: The entry ID in the route is authoritative; do not trust or require a posted entry ID.
- **CON-004**: `TimeTrackingModel` must not contain an `Id`; edit endpoint signatures must bind `Guid id` from the route and `[FromForm] TimeTrackingModel request` separately without a wrapper model. Runtime verification showed `[AsParameters]` returns HTTP 415 for the URL-encoded form body.
- **CON-001**: Keep the existing `TimeTrackingModelValidator` validation rules and project lookup contract.
- **CON-002**: Preserve `CreatedAtUtc` and `CreatedByUserId` during edit; update only mutable fields plus `UpdatedAtUtc` and `UpdatedByUserId`.
- **CON-003**: No automated test project currently exists in the solution; do not introduce a new test project as part of this narrow feature.
- **GUD-001**: Use a native `<button type="button">` for each interactive entry card and expose a visible `:focus-visible` state.
- **GUD-002**: Put all editable controls, including date and start time, in the shared fields slice so create and edit validation render identical fields.
- **GUD-003**: Keep the checkbox outside the submitted domain model: it must not have a form field name. Derive its initial checked state from `Model.HourlyRate.HasValue` and continue posting only nullable `HourlyRate`.
- **PAT-001**: Follow the existing HTMX modal flow: GET uses `Result.ShowDialog`, invalid POST returns the dialog fragment, and successful POST returns `_Empty`, clears the dialog, triggers grid refresh, and adds a toast.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Reuse the existing form model while keeping identity exclusively in the route.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Keep `src/Apskaita.Web/Features/TimeTracking/Models/TimeTrackingModel.cs` free of identity and routing concerns. Do not add an `Id` property or introduce an `{ Id, Model }` wrapper type. | | |
| TASK-002 | In `src/Apskaita.Web/Features/TimeTracking/Services/TimeTrackingContentService.cs`, retain the existing create model factory and add `CreateEditDialogModelAsync(ApskaitaDbContext db, Guid id, CancellationToken ct)`. Query the entry and its project label, including inactive projects, and return `null` when the entry does not exist. Map `StartedAtLocal` to `DateOnly` and `TimeOnly`, and map duration, hourly rate, project ID/label, and description into the existing `TimeTrackingModel`. | | |
| TASK-003 | Keep the existing invalid-submission model factory identity-free. It must normalize and preserve only submitted form fields and validation errors. | | |

### Implementation Phase 2

- GOAL-002: Share field markup while keeping create and edit command shells separate.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-004 | Add `src/Apskaita.Web/Features/TimeTracking/Slices/_TimeTrackingForm.cshtml`. Move the validation summary and all labeled controls for date, start time, duration, hourly-rate mode/override, project lookup, field validation, and description from `_CreateDialog` into this shared slice. The slice must not contain a `<form>`, modal header/footer, endpoint URL, identity, or command buttons. | | |
| TASK-005 | Refactor `_CreateDialog.cshtml` into a create-specific shell. Keep the `Create time entry` title and explicit `hx-post` to `create`, render `_TimeTrackingForm`, and provide `Cancel` and `Create` buttons. | | |
| TASK-006 | Add `_EditDialog.cshtml` as an edit-specific shell without requiring the ID in its model. Inject `IHttpContextAccessor`, read the server-rendered `HttpContext.Request.Path` for the active `edit/{id}` request, and assign that path to the form's `hx-post`. This is required because opening a modal does not push `edit/{id}` into the browser URL. Render `_TimeTrackingForm` and provide `Delete`, `Copy`, `Cancel`, and `Save` buttons. | | |
| TASK-007 | Make `Save` the edit form's only submit button. Make `Delete` a `type="button"` that reads `id` from `HttpContext.Request.RouteValues`, sends `hx-get` to `delete/{id}`, and replaces `closest .modal-content` with `outerHTML`. Make `Copy` a `type="button"` with `hx-post` to `create`, `hx-include="closest form"`, and the same modal target/swap. Add `_DeleteDialog.cshtml`; read its `id` from route values, render an explicit irreversible-action message, post confirmation to `delete/{id}`, and provide Cancel that requests `edit/{id}` back into `closest .modal-content`. | | |

### Implementation Phase 3

- GOAL-003: Add edit endpoints and persist updates without changing create behavior.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-008 | In `src/Apskaita.Web/Features/TimeTracking/Endpoints/TimeTrackingEndpoints.cs`, retain GET create and bind POST create as `[FromForm] TimeTrackingModel request`. Render `_CreateDialog` for validation failures and use the same successful create response for a new slot and for Copy. | | |
| TASK-009 | Add `GET edit/{id:guid}`. Call `CreateEditDialogModelAsync`; return 404 for `null`, otherwise return `Result.ShowDialog(_EditDialog.Create(model))`. The current request URL supplies the edit dialog's implicit Save/Delete target. | | |
| TASK-010 | Add `POST edit/{id:guid}` with parameters `Guid id`, `[FromForm] TimeTrackingModel request`, `IValidator<TimeTrackingModel> validator`, `ApskaitaDbContext db`, and `CancellationToken ct`. Load the tracked entity by route ID and return 404 when absent. Validate `request` and return `_EditDialog` as a fragment with submitted values and errors when invalid. | | |
| TASK-011 | In the valid edit path, verify that `request.ProjectId` references an existing project, using the same field-level `not_found` error contract as create. Apply `ProjectId`, resolved `StartedAtLocal`, `DurationInMinutes`, `HourlyRate`, and trimmed `Description`; set `UpdatedAtUtc = DateTime.UtcNow` and the current placeholder `UpdatedByUserId = Guid.Empty`; then call `SaveChangesAsync`. | | |
| TASK-012 | Return `_Empty` after a successful edit, call `ClearDialog()`, emit `time-tracking-list-refresh`, and show a success toast titled `Time entry`. The existing grid URL must remain the source of the currently displayed `weekStart`; moving an entry outside that week must make it disappear after refresh. | | |
| TASK-013 | Add `GET delete/{id:guid}` to load the entry by route ID, return 404 when absent, and return `_DeleteDialog` as a fragment replacing the current edit modal content. Add `POST delete/{id:guid}` to load the entity by route ID, return 404 when absent, remove it, save, clear the dialog, emit `time-tracking-list-refresh`, and show a warning toast. Do not add a copy route. | | |

### Implementation Phase 4

- GOAL-004: Make entry cards accessible edit triggers without breaking grid layout.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-014 | In `src/Apskaita.Web/Features/TimeTracking/Slices/_Grid.cshtml`, replace each `.tt-entry-card` `<span>` with `<button type="button">`. Add `hx-get` for `edit/{entry.Id}`, `hx-target="#dialog-host"`, and an `aria-label` containing the start/end time and description. Keep the existing child spans and positioning styles. | | |
| TASK-015 | In `src/Apskaita.Web/wwwroot/css/site.css`, change `.tt-entry-card` from `pointer-events: none` to `pointer-events: auto`; normalize button font, text alignment, and non-left borders; add `cursor: pointer`, a hover treatment, and a high-contrast `.tt-entry-card:focus-visible` outline that does not resize the card. | | |

### Implementation Phase 5

- GOAL-005: Validate compilation and end-to-end HTMX behavior.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-016 | Run `dotnet build .\src\Apskaita.Web\Apskaita.Web.csproj --no-restore` and require a successful build. Record the existing `GenerateHtmlNamesAttribute` warning separately if it remains. | | |
| TASK-017 | Verify create regression behavior: click an empty slot, submit a valid entry, confirm the dialog closes and only `#time-tracking-grid` refreshes. | | |
| TASK-018 | Verify edit success and failure behavior: activate a card with pointer and keyboard, update every field, validate error preservation, save, and confirm the updated card appears without a full-page swap. | | |
| TASK-019 | Verify copy behavior: change values in edit mode, click `Copy`, and confirm `POST create` creates a second entry from the current form values while the original entry remains unchanged. | | |
| TASK-020 | Verify delete behavior: click `Delete`, confirm the edit modal is replaced by the delete dialog, cancel back to the same edit dialog without mutation, reopen delete, confirm it, and verify the dialog closes and only the grid refreshes. | | |
| TASK-021 | Verify missing-entry behavior for GET/POST `edit/{unknown-guid}` and GET/POST `delete/{unknown-guid}`; require HTTP 404 and no database mutation. | | |

### Implementation Phase 6

- GOAL-006: Simplify hourly-rate override selection to one compact input-and-toggle row.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-022 | In `src/Apskaita.Web/Features/TimeTracking/Slices/_TimeTrackingForm.cshtml`, remove both `tt-hourly-rate-default` and `tt-hourly-rate-override` radio inputs and their two-button group. Keep `isHourlyRateOverride = Model.HourlyRate.HasValue` as the single source of initial UI state. | | |
| TASK-023 | Render `@f.HourlyRate.Input(type: "number")` and one unnamed `<input type="checkbox" class="btn-check">` inside the same Bootstrap `input-group flex-nowrap`. Associate a `<label class="btn btn-outline-secondary">Override</label>` with the checkbox, place it immediately after the number input, and add `aria-controls="@f.HourlyRate.Id"`. | | |
| TASK-024 | Implement the checkbox `change` behavior without adding a JavaScript file: checked enables the hourly-rate input, sets `required`, and focuses it; unchecked clears the value, removes `required`, and disables the input. On render, set checkbox `checked`, input `disabled`, and input `required` consistently from `isHourlyRateOverride`. Preserve `min="1"`, validation output, and the existing default-rate help text. | | |
| TASK-025 | Run the web-project build and verify both create and edit dialogs in the browser. At desktop and mobile widths, require the number input and toggle to remain on one non-overlapping row. Verify Tab/Space operation, unchecked submission binding `HourlyRate = null`, checked submission binding the entered positive integer, and an invalid value preserving the checked toggle plus validation message. | | |

## 3. Alternatives

- **ALT-001**: Use one `_UpsertDialog` for create and edit. Rejected because edit has `Save`, `Delete`, and `Copy` commands while create has only `Create`; separate shells keep command semantics explicit while `_TimeTrackingForm` prevents field duplication.
- **ALT-002**: Keep the entry card as a `<span>` with click handlers and keyboard attributes. Rejected because a native button provides activation and focus semantics without custom JavaScript.
- **ALT-003**: Use one `POST upsert` endpoint for both modes. Rejected because separate route semantics make authorization, not-found handling, and accidental insert prevention explicit while still sharing the dialog.
- **ALT-004**: Add a dedicated `copy/{id}` route. Rejected because Copy can post the current form to the existing create command; delete intentionally keeps dedicated GET/POST routes because it has a separate confirmation dialog.
- **ALT-005**: Add `IsHourlyRateOverride` to `TimeTrackingModel`. Rejected because nullability already represents the domain state and the checkbox only controls whether the existing nullable field participates in form submission.

## 4. Dependencies

- **DEP-001**: Existing RazorSlicesHtmx `Result.ShowDialog`, `ClearDialog`, `WithTrigger`, and `WithToast` behavior.
- **DEP-002**: Existing Bootstrap modal host `#dialog-host` and lookup-select assets loaded by the application layout.
- **DEP-003**: Existing `time-tracking-list-refresh` listener on `#time-tracking-grid`.

## 5. Files

- **FILE-001**: `src/Apskaita.Web/Features/TimeTracking/Models/TimeTrackingModel.cs` - remain identity-free and continue representing form fields only.
- **FILE-002**: `src/Apskaita.Web/Features/TimeTracking/Services/TimeTrackingContentService.cs` - load edit form values.
- **FILE-003**: `src/Apskaita.Web/Features/TimeTracking/Endpoints/TimeTrackingEndpoints.cs` - map create plus GET/POST edit and GET/POST delete responses.
- **FILE-004**: `src/Apskaita.Web/Features/TimeTracking/Slices/_TimeTrackingForm.cshtml` - shared form controls and validation.
- **FILE-005**: `src/Apskaita.Web/Features/TimeTracking/Slices/_Grid.cshtml` - make entry cards open edit.
- **FILE-006**: `src/Apskaita.Web/wwwroot/css/site.css` - enable pointer/focus interaction for entry buttons.
- **FILE-007**: `src/Apskaita.Web/Features/TimeTracking/Slices/_CreateDialog.cshtml` - create-specific modal shell and commands.
- **FILE-008**: `src/Apskaita.Web/Features/TimeTracking/Slices/_EditDialog.cshtml` - edit-specific modal shell with save, delete, and copy.
- **FILE-009**: `src/Apskaita.Web/Features/TimeTracking/Slices/_DeleteDialog.cshtml` - delete confirmation and return-to-edit commands.

## 6. Testing

- **TEST-001**: Build the changed web project and all referenced projects.
- **TEST-002**: Manually verify create, POST edit, GET/POST delete, and copy-through-POST-create flows with HTMX response targeting.
- **TEST-003**: Manually verify server validation preserves edit mode and submitted values.
- **TEST-004**: Manually verify pointer, Tab, Enter, Space, and Escape behavior for card and modal interaction.
- **TEST-005**: Manually verify GET/POST edit and GET/POST delete not-found behavior and no mutation.
- **TEST-006**: Confirm that Copy inserts a new entity from current form values and never modifies the source entity.
- **TEST-007**: Verify the single-line hourly-rate input/toggle layout, checkbox keyboard behavior, null/default submission, positive override submission, and validation re-render state.

## 7. Risks & Assumptions

- **RISK-001**: `.tt-day-overlay` currently has `pointer-events: none`; the card button must explicitly override it with `pointer-events: auto`, and this must be verified in the browser.
- **RISK-002**: Overlapping time entries can partially cover one another; the existing stacking order remains unchanged, so only each visible card area is guaranteed clickable.
- **RISK-003**: Moving an entry between weeks removes it from the current grid after refresh, which is correct but may surprise a user without a sufficiently clear toast.
- **RISK-004**: Delete and copy buttons live inside the edit `<form>`; both must explicitly use `type="button"` so they never submit the edit form.
- **RISK-005**: A checked toggle with an empty input would otherwise submit as `null` and silently revert to the default rate; toggling the input's native `required` attribute prevents that inconsistent state.
- **ASSUMPTION-001**: Editing an entry may select only projects offered by the current active-project lookup, while an already selected inactive project remains displayable through the server-provided label.
- **ASSUMPTION-002**: User identity remains represented by `Guid.Empty` until authentication supplies a real current-user ID.
- **ASSUMPTION-003**: Copy immediately submits the current editable values to create and receives a new ID plus fresh create/update audit timestamps.

## 8. Related Specifications / Further Reading

- `time-tracking-feature-plan.md`
- `requirements.md`
- `src/Apskaita.Web/Features/TimeTracking/Endpoints/TimeTrackingEndpoints.cs`