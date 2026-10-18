# Proposal

## Why

A project's work has to be certified for a period before it can be invoiced: a work act ("darbų aktas")
lists the work done between two dates and freezes it as the billing basis. Today the entries can be edited
or deleted at any time, so nothing can be safely invoiced. The domain already carries unused
`WorkAct`/`WorkActLine` entities and tables from an earlier design; this change makes work acts real as
line-based documents: the act owns copies of the work (its lines), while the time entries stay the raw
record of what happened.

## What Changes

- Add a **Work acts** feature: create an act for a project and a period, review the entries it covers as
  draft lines, and save the act.
- The act's content is its **work act lines**: each line stores duration in minutes, an hourly rate
  (never null — the entry's rate, else the project rate, or an explicit override), a start date and time,
  a description, and a reference to the time entry it was made from. Lines are snapshots: editing a line
  never changes the entry, and editing an entry never changes existing lines.
- One entry **may appear in several acts** (many-to-many through the lines); there is no act link column on
  the entry.
- An entry that is referenced by any work act line becomes **locked**: edit, delete and drag-move of the
  entry are rejected, and the notice names the act(s) it belongs to. Locked entries are marked in the grid.
- Status flow **Draft → Final → Invoiced**: Draft acts have editable lines; Final freezes them; an act
  referenced by an invoice line is presented as Invoiced (derived, no new column).
- Act **totals** (hours and amount) are computed from the lines and stored on every save.
- **Deleting** a Draft act removes its lines; the entries themselves are untouched. Final/Invoiced acts
  cannot be deleted.
- On the review/edit/detail screens a line whose entry also appears in **other acts** is visually distinct
  and carries a tooltip naming those acts.
- A **list** of acts, filterable by multiple projects, ordered by period end descending.
- Periods are stored as UTC instants derived from the user's local date boundaries and displayed in the
  viewer's zone.

## Capabilities

### New Capabilities
- `work-acts`: certifying a project's work for a period — line-based acts, creation flow, statuses, list,
  deletion.

### Modified Capabilities
- `time-tracking`: entries referenced by a work act line are locked (edit, delete, move rejected; grid
  marker); the lock no longer depends on a column on the entry.

## Impact

- **Entities/schema**: `WorkAct` gains `Final` in its status enum; `WorkActLine` is reintroduced with the
  new shape (act id, entry id, duration, rate NOT NULL, start, description); `TimeTracking` loses
  `WorkActId` and `Version`; one migration recreates `work_act_lines` (copying any existing
  `work_act_id` links into lines first) and drops the entry columns.
- **Assemblies**: new `Apskaita.Feature.WorkActs` (endpoints, models, slices, store); the
  `ITimeEntryCertifier` seam in `Apskaita.Shared` is removed — the act and its lines are written by the
  WorkActs store, and the time-tracking feature only *reads* `work_act_lines` through `IQuery<T>`.
- **Existing code**: `TimeTrackingEndpoints` edit/delete/move check line existence instead of
  `WorkActId`; the grid marker and dialog suppression stay, driven by the same check.
- **Out of scope**: Excel/CSV export, invoice creation from acts (the `InvoiceLine.WorkActId` link stays
  write-unused; only the derived status is implemented), adding/removing lines after creation.
- No change to calendar connectors or the week grid's event rendering.
