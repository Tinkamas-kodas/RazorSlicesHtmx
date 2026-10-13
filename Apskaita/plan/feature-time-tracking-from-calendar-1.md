---
goal: Create or Edit Time Entries Directly from Calendar Events
version: 1.1
date_created: 2026-10-13
last_updated: 2026-10-13
owner: Apskaita
status: 'Implemented'
tags: [feature, time-tracking, calendar-connectors, htmx, ef-owned-types]
---

# Introduction

![Status: Implemented](https://img.shields.io/badge/status-Implemented-green)

Make every calendar event in the time-tracking side panel clickable through one endpoint. The server decides which dialog to return: a prefilled create dialog (event start in the browser zone, exact duration, event reference carried invisibly) when no entry exists for the event, or the existing edit dialog when an entry already references it. The event is identified across providers by the value object `CalendarEventReference(CalendarProvider, AccountKey, ExternalEventId)`, stored as an EF owned type in the `time_trackings` table with a unique index so at most one entry ever references a given event.

This supersedes REQ-008 of `feature-calendar-connectors-1.md` ("events are display-only; no create-from-event"), which explicitly deferred this feature.

## 1. Requirements & Constraints

- **REQ-001**: Every event card in `_CalendarPanel` is clickable and requests exactly one URI: `GET event` with query parameters `date`, `time`, `duration`, `weekStart`, `provider`, `account`, `eventId` (event id URL-encoded; Graph ids contain `=` and `-`).
- **REQ-002**: `GET event` returns the **create** dialog (via `Result.ShowDialog`) when no `TimeTracking` references the triple, prefilled with `date`/`time` (display-zone wall time) and `duration` clamped to `[MinDurationMinutes, MaxDurationMinutes]`.
- **REQ-003**: `GET event` returns the existing entry's **edit** dialog when a `TimeTracking` references the triple (values converted to the display zone through the existing `tz-offset` cookie mechanism).
- **REQ-004**: `GET event` validates the reference before any dialog: `provider` parses to `CalendarProvider` (case-insensitive), `account` matches one of the current user's connectors for that provider, `eventId` is non-blank and ≤ 255 chars, `account` ≤ 255 chars. Invalid reference → HTTP 404 (HTMX error middleware renders the toast).
- **REQ-005**: `POST create` persists the reference when the hidden event fields are present (all-or-nothing: partial reference → validation error, no row); manual creation (no reference) is unchanged.
- **REQ-006**: The database enforces at most one `time_trackings` row per `(calendar_provider, account_key, external_event_id)` via a unique index; rows without a reference (all three columns NULL) coexist freely.
- **REQ-007**: A tracked event renders a small "Logged" badge; an untracked event renders none. The badge is an affordance only — click behavior is decided by the server (REQ-002/003), never by the badge.
- **REQ-008**: The recurrence fields are hidden and recurrence is ignored when the create dialog was opened from a calendar event (a recurring batch would violate REQ-006 from the second occurrence onward).
- **REQ-009**: `POST edit` never changes, clears, or requires the reference; `POST delete` removes the entry and thereby frees the event (next panel render shows it untracked and routes to create again).
- **REQ-010**: Graph requests include `id` in `$select`, and `CalendarEventInfo` carries `ProviderKey`, `AccountKey`, `EventId` so the panel can build the `GET event` URL.
- **SEC-001**: The reference is user-supplied data; it is length-capped, enum-validated, and account-ownership-checked (REQ-004). It never influences queries beyond the indexed equality lookup.
- **CON-001**: `CalendarEventReference` is a value object mapped as an EF **owned type in the same table** (`OwnsOne`), following the `CompanyInfo` precedent; no separate table, no loose properties on `TimeTracking`.
- **CON-002**: Owned columns are `calendar_provider` (string-converted enum), `account_key`, `external_event_id` (snake-case convention from property names `CalendarProvider`, `AccountKey`, `ExternalEventId`).
- **CON-003**: All-NULL owned columns materialize as `null` (optional owned reference); `t.CalendarEvent != null` is the tracked-exists predicate in LINQ.
- **CON-004**: `TimeTracking.StartedAtUtc` semantics (naive UTC, browser-zone display) from `feature-calendar-connectors-1.md`'s follow-up remain unchanged; the create-from-event prefill converts display wall time → UTC with the existing `BrowserTimeZone` helpers.
- **CON-005**: Existing endpoints (`create`, `edit/{id}`, `delete/{id}`, `move/{id}`) keep their contracts; `GET event` is additive.
- **GUD-001**: One hx pattern for all events (identical `hx-get`/`hx-target="#dialog-host"`), no per-state markup branching beyond the badge.
- **PAT-001**: Dialog flow follows the existing modal pattern: GET → `Result.ShowDialog`, invalid POST re-renders the dialog fragment, success returns `_Empty` + `ClearDialog()` + `time-tracking-list-refresh` + toast.
- **PAT-002**: Duplicate-race handling follows the DB-as-truth pattern: catch `DbUpdateException` on `POST create` → error toast, no row.

## 2. Implementation Steps

### Implementation Phase 1

- GOAL-001: Add the owned event reference to the schema with its uniqueness guarantee.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Add `src/Apskaita.Entities/CalendarEventReference.cs`: `sealed record CalendarEventReference(CalendarProvider CalendarProvider, string AccountKey, string ExternalEventId)` plus `static bool TryCreate(string? providerKey, string? accountKey, string? externalEventId, out CalendarEventReference? reference)` enforcing case-insensitive enum parse, non-blank values, and ≤ 255-char lengths. | | |
| TASK-002 | In `src/Apskaita.Entities/TimeTracking.cs`, add the nullable owned navigation `public CalendarEventReference? CalendarEvent { get; set; }` with a doc comment (NULL = manual entry). | | |
| TASK-003 | In `src/Apskaita.Entities/FieldLengths.cs` under `TimeTracking`, add `AccountKey = 255` and `ExternalEventId = 255`. | | |
| TASK-004 | In `src/Apskaita.Entities/Mapping/ApskaitaEntityModelBuilderExtensions.cs` `ConfigureTimeTracking`, add `entity.OwnsOne(x => x.CalendarEvent, b => { b.WithOwner(); b.Property(p => p.CalendarProvider).HasConversion<string>(); b.Property(p => p.AccountKey).HasMaxLength(FieldLengths.TimeTracking.AccountKey); b.Property(p => p.ExternalEventId).HasMaxLength(FieldLengths.TimeTracking.ExternalEventId); b.HasIndex(p => new { p.CalendarProvider, p.AccountKey, p.ExternalEventId }).IsUnique(); });`. Verify generated column names are `calendar_provider`, `account_key`, `external_event_id`. | | |
| TASK-005 | Generate migration `AddCalendarEventReference` (`cd Apskaita; dotnet ef migrations add AddCalendarEventReference --project src/Apskaita.Data.Migration`); it must be purely additive (3 nullable columns + unique index, no data conversion). Apply with `dotnet ef database update --project src/Apskaita.Data.Migration`. | | |
| TASK-006 | Cheap disproof check: after apply, insert two manual (NULL-reference) rows and one referenced row against dev MySQL; confirm the index rejects a second row with the identical triple and accepts NULL duplicates. | | |

### Implementation Phase 2

- GOAL-002: Give calendar events a stable cross-provider identity.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-007 | In `src/Apskaita.Web/Shared/Calendar/CalendarEventReader.cs`, extend `CalendarEventInfo` with `string ProviderKey`, `string AccountKey`, `string EventId` (doc: stable identity triple for dedupe). | | |
| TASK-008 | In `src/Apskaita.Web/Shared/Calendar/Outlook365Connector.cs`, add `id` to the Graph `$select` and populate `ProviderKey = ProviderKey`, `AccountKey = connector.AccountKey`, `EventId = item.id` (skip items without an id). | | |
| TASK-009 | Update every `CalendarEventInfo` construction in tests (`CalendarConnectorTests`, `Outlook365ConnectorTests`) for the widened record. | | |

### Implementation Phase 3

- GOAL-003: Make every event card clickable through one URI, with a tracked badge.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-010 | In `TimeTrackingContentService.CreatePageModelAsync`, load tracked references for the displayed week (`db.TimeTrackings` where `CalendarEvent != null` within the existing UTC week bounds; project the triple) into a `HashSet<(string, string, string)>` with case-insensitive provider comparison. | | |
| TASK-011 | Extend `TimeTrackingCalendarEventViewModel` with `ProviderKey`, `AccountKey`, `EventId`, `IsTracked`; populate from the reader result plus the tracked set. | | |
| TASK-012 | In `_CalendarPanel.cshtml`, render every event as `<button type="button">` (or keep the div and add the attributes) with `hx-get="@FeatureUri.Uri($"event?date={evt.Date:yyyy-MM-dd}&time={evt.Start:HH\\:mm}&duration={evt.DurationMinutes}&weekStart={Model.WeekStart:yyyy-MM-dd}&provider={evt.ProviderKey}&account={evt.AccountKey}&eventId={Uri.EscapeDataString(evt.EventId)}")"` and `hx-target="#dialog-host"`; append a `tt-event-tracked-badge` span when `IsTracked`. Update the slice header comment: display-only (REQ-008 of the connectors plan) is superseded by this feature. | | |
| TASK-013 | Add `.tt-event-tracked-badge` styling to `wwwroot/css/site.css` (small muted pill; no layout shift). | | |
| TASK-014 | Update `TimeTrackingList_CalendarEvents_AreDisplayOnly` in `CalendarConnectorTests`: untracked events now carry the `event?` `hx-get`; add an assertion that a stub event whose triple matches a seeded entry renders the badge and no create-style URL difference is required (same URI either way). | | |

### Implementation Phase 4

- GOAL-004: Implement the single `GET event` endpoint and the create/edit persistence of the reference.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-015 | Add `GET event` to `TimeTrackingEndpoints.MapEndpoints`: bind `HttpContext ctx`, `string provider, account, eventId, date, time, int? duration, string? weekStart`, `ApskaitaDbContext db`, `CalendarConnectorStore store`, `CancellationToken ct`. Validate per REQ-004 (404 on invalid). | | |
| TASK-016 | In `GET event`, look up `db.TimeTrackings` by the owned triple (`t.CalendarEvent != null && t.CalendarEvent!.CalendarProvider == parsed && t.CalendarEvent.AccountKey == account && t.CalendarEvent.ExternalEventId == eventId`). Hit → `Result.ShowDialog(_EditDialog.Create(await _content.CreateEditDialogModelAsync(db, id, displayOffset, ct)))`. Miss → build the create model. | | |
| TASK-017 | Extend `TimeTrackingModel` with `string? EventProviderKey`, `string? EventAccountKey`, `string? EventId`, `bool FromCalendarEvent`; round-trip them in both `CreateCreateDialogModel` factories. `GET event`'s miss path fills them plus `Date`/`Start` from query params and `DurationMinutes = Math.Clamp(duration ?? DefaultDurationMinutes, Min, Max)`. | | |
| TASK-018 | Render the three event fields as hidden inputs in `_CreateDialog.cshtml` (shell, not the shared `_TimeTrackingForm`) when `Model.FromCalendarEvent`; hide the entire recurrence block in that case (REQ-008). | | |
| TASK-019 | In `POST create`: when any event field is non-empty, require all three and build `CalendarEventReference.TryCreate` (validation error `invalid_event_reference` otherwise); verify `account` belongs to the current user's connectors for that provider (reuse `CalendarConnectorStore`); attach `CalendarEvent = reference` to every row only when non-recurring (recurring + reference is impossible via REQ-008 UI, reject server-side as defense). Wrap `SaveChangesAsync` in try/catch `DbUpdateException` → dialog fragment cleared + error toast "This calendar event is already logged." + `time-tracking-list-refresh` trigger. | | |
| TASK-020 | Confirm `POST edit/{id}` and `POST move/{id}` never touch `row.CalendarEvent` (owned navigation untouched ⇒ columns untouched). No UI change to `_EditDialog`. | | |

### Implementation Phase 5

- GOAL-005: Validate compilation, behavior, and regression.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-021 | `dotnet build Apskaita.slnx` → 0 warnings / 0 errors. | | |
| TASK-022 | Add endpoint tests (CalendarStubApiFactory): untracked `GET event` → create dialog HTML containing the hidden event fields; tracked → edit dialog HTML (entry description present); forged account → 404; `POST create` with valid reference → row stores the owned triple (reload and assert `CalendarEvent` populated); `POST create` duplicate triple → single row + error toast text; manual create → `CalendarEvent` null. | | |
| TASK-023 | Run the full suite via `tests/Apskaita.Tests/bin/Debug/net10.0/Apskaita.Tests.exe` (no `--filter`; use `-class` for subsets). | | |
| TASK-024 | Manual browser pass with `dotnet watch`: click untracked event → prefilled create dialog (browser-zone start, exact duration, no recurrence fields); save → panel refreshes with "Logged" badge; click same event → edit dialog for the entry; delete entry → badge gone, click opens create again. | | |

## 3. Alternatives

- **ALT-001**: Three loose nullable properties on `TimeTracking`. Rejected by the user in favor of the owned value object (`CompanyInfo` precedent); the value object keeps the identity triple whole and validated.
- **ALT-002**: One composite `calendar_event_ref` string column. Rejected: loses per-provider queryability and explicit shape.
- **ALT-003**: Two client-side routes (`create?...` vs `edit/{id}`) chosen by the rendered badge. Rejected by the user in favor of one URI with a server-side decision — correct even when panel HTML is stale (entry created in another tab).
- **ALT-004**: Re-query Graph on click to resolve the event. Rejected: slow and unnecessary — the click already carries the event's display-zone times and duration.
- **ALT-005**: Allow recurrence on event-derived entries and attach the reference only to the first occurrence. Rejected: ambiguous semantics and the unique index would reject occurrence #2 anyway.

## 4. Dependencies

- **DEP-001**: EF Core owned types (`OwnsOne` in same table, optional reference materialization) on both MySQL (Microting provider) and the InMemory test provider — `CompanyInfo` precedent proves the pattern.
- **DEP-002**: Existing `BrowserTimeZone` cookie/display-offset machinery (browser-zone times for prefill and edit display).
- **DEP-003**: Existing dialog/refresh pattern: `Result.ShowDialog`, `#dialog-host`, `time-tracking-list-refresh`, `CalendarConnectorStore` for account-ownership checks.
- **DEP-004**: Graph `calendarView` event `id` property (added to `$select`).

## 5. Files

- **FILE-001**: `src/Apskaita.Entities/CalendarEventReference.cs` - new value object + `TryCreate`.
- **FILE-002**: `src/Apskaita.Entities/TimeTracking.cs` - nullable owned navigation.
- **FILE-003**: `src/Apskaita.Entities/FieldLengths.cs` - `AccountKey`/`ExternalEventId` caps.
- **FILE-004**: `src/Apskaita.Entities/Mapping/ApskaitaEntityModelBuilderExtensions.cs` - `OwnsOne` + unique index.
- **FILE-005**: `src/Apskaita.Data.Migration/Migrations/*_AddCalendarEventReference.cs` - additive migration.
- **FILE-006**: `src/Apskaita.Web/Shared/Calendar/CalendarEventReader.cs` + `Outlook365Connector.cs` - identity triple.
- **FILE-007**: `src/Apskaita.Web/Features/TimeTracking/Services/TimeTrackingContentService.cs` - tracked set + VM population.
- **FILE-008**: `src/Apskaita.Web/Features/TimeTracking/Models/TimeTrackingPageModel.cs` - VM fields.
- **FILE-009**: `src/Apskaita.Web/Features/TimeTracking/Slices/_CalendarPanel.cshtml` - clickable events + badge.
- **FILE-010**: `src/Apskaita.Web/Features/TimeTracking/Endpoints/TimeTrackingEndpoints.cs` - `GET event` + create persistence.
- **FILE-011**: `src/Apskaita.Web/Features/TimeTracking/Models/TimeTrackingModel.cs` + `Slices/_CreateDialog.cshtml` - hidden reference fields, recurrence suppression.
- **FILE-012**: `src/Apskaita.Web/wwwroot/css/site.css` - badge style.

## 6. Testing

- **TEST-001**: Migration disproof check (TASK-006) against dev MySQL: unique triple rejected, NULL triples coexist.
- **TEST-002**: Endpoint tests per TASK-022 (create/edit dialog selection, 404 on forged reference, owned round-trip, duplicate rejection, manual null reference).
- **TEST-003**: Full xUnit suite green via the in-process runner.
- **TEST-004**: Manual browser pass per TASK-024 including badge state transitions across create → edit → delete → create.
- **TEST-005**: Regression: manual slot-click create, drag-move, and edit of a referenced entry behave exactly as before (reference untouched).

## 7. Risks & Assumptions

- **RISK-001**: The InMemory test provider does not enforce unique indexes; the duplicate-race test proves the query-level guard, while true index enforcement is verified only against dev MySQL (TASK-006).
- **RISK-002**: Graph event ids longer than 255 chars would be rejected by REQ-004; observed ids are ~150-200 chars. If ever hit, raise the cap rather than truncate (truncation breaks identity).
- **RISK-003**: EF optional owned types materialize `null` only when ALL owned columns are NULL; a partially-filled row (impossible through the app) would materialize with defaults — acceptable since writes always go through `TryCreate`.
- **RISK-004**: All-day events (24 h) fit `MaxDurationMinutes = 1440` exactly; multi-day events exceed it and clamp silently. Accepted for now (see ASSUMPTION-002).
- **ASSUMPTION-001**: One entry per event is a global rule (no user column on `time_trackings`); two app users sharing a mailbox cannot both log the same event. Accepted for the single-user dev deployment.
- **ASSUMPTION-002**: Duration is a one-time copy; later event moves/resizes never update the entry, and all-day/multi-day events are clickable with clamped duration.
- **ASSUMPTION-003**: The reference survives edit and move, and is released only by deleting the entry.
- **ASSUMPTION-004**: `GET event` requires the same operational auth policy as the rest of the feature (route group already enforces it).

## 8. Related Specifications / Further Reading

- `plan/feature-calendar-connectors-1.md` - REQ-008 (display-only) is superseded by this plan; its follow-up fixes (browser `tz-offset` cookie, `StartedAtUtc` rename) are prerequisites already implemented.
- `plan/feature-time-tracking-edit-dialog-1.md` - the edit dialog reused by `GET event`.
- `time-tracking-feature-plan.md`, `requirements.md`

## 9. Implementation Notes (2026-10-13)

All tasks in phases 1–5 are complete; suite 107/107 green (`Apskaita.Tests`), solution builds 0 warnings / 0 errors, migration `20261012214448_AddCalendarEventReference` applied to dev MySQL.

Deviations from the plan:

- **Column names carry the owned-navigation prefix**: the snake-case convention expands owned paths, so the columns are `calendar_event_calendar_provider`, `calendar_event_account_key`, `calendar_event_external_event_id` (index `ix_time_trackings_calendar_event_calendar_provider_calendar_eve`, truncated by MySQL's 63-char limit). CON-002's short names are superseded; semantics unchanged.
- **Tracked branch redirects instead of rendering the edit dialog inline**: `_EditDialog` derives its identity and POST target from `Request.RouteValues["id"]`, which the `event` route does not have. `GET event` now answers tracked events with `Results.Redirect(edit/{id})` (htmx follows the redirect; headers preserved). REQ-003's outcome is identical from the user's perspective.
- **App-level dedupe added in front of the index** (defense + testability): `POST create` checks `FindTrackedEntryIdAsync` before insert and returns the same "already logged" toast; the unique index remains the race backstop. This makes the duplicate test meaningful on the InMemory provider (RISK-001 realized: InMemory does not enforce unique indexes).
- **Toast assertions read the `HX-Trigger` header**: `WithToast` delivers toast payloads through the trigger header JSON, not the response body (body is the empty `_Empty` fragment).
- **Identity-less events stay inert**: `_CalendarPanel` only wires `hx-get` when `ProviderKey`+`EventId` are present (stub/legacy events without identity render no URL). The old display-only test was renamed to `TimeTrackingList_IdentitylessCalendarEvents_AreInert` to cover exactly that.
- **Disproof check executed as a temporary EF harness** (rolled-back transaction against dev MySQL): NULL triples coexist, duplicate triple rejected with `Duplicate entry ... for key 'ix_time_trackings_calendar_event_...'`; scratch project removed afterwards.
- **TASK-024 manual browser pass is pending** (requires `dotnet watch` + a live Outlook 365 connector): click untracked event → prefilled create dialog without recurrence fields → save → "Logged" badge → click again → edit dialog → delete → create dialog again.
