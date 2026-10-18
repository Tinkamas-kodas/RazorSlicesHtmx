# Tasks

The first iteration (entry-linked acts) is implemented; the composition rework supersedes parts of it.
Checked items stay valid; unchecked items are the rework.

## 1. Entities and schema

- [x] 1.1 Reshape `WorkAct` (drop `Number`, add `PeriodStartUtc`/`PeriodEndUtc`/`Version`) and verify the solution builds
- [x] 1.2 Reintroduce `WorkActLine` (act id, entry id, duration, `HourlyRate` NOT NULL, start, description) with `FieldLengths`, `DbSet` and mapping (cascade FK to `WorkAct`, restrict FK to `TimeTracking`, index on `TimeTrackingId`); add `Final` to `WorkActStatus`; drop `TimeTracking.WorkActId` and `Version` and their mapping
- [x] 1.3 Generate and review the migration (create `work_act_lines`, copy existing `work_act_id` links into lines, drop the entry columns, widen the status enum), apply it to dev MySQL, and verify the act list query runs

## 2. Data seams

- [x] 2.1 Remove `ITimeEntryCertifier` (Shared) and `TimeEntryCertifier` (Composition) and their DI registration; extend `IWorkActStore` with `DbSet<WorkActLine>`
- [x] 2.2 `IWorkActStore` (tracked `work_acts` + `SaveChangesAsync`) with its composition adapter and DI registration exists

## 3. Feature skeleton and list

- [x] 3.1 Create the `Apskaita.Feature.WorkActs` assembly (endpoints/models/slices/validators folders, `GlobalUsings`, csproj, solution entry) with a `BaseFeatureModule` exposing the "Work acts" nav item under `/work-acts`
- [x] 3.2 Implement the list page + fragment with `ListRequest<WorkActRowModel>` (default sort period end descending) and a multi-project filter
- [x] 3.3 Show the effective status on list rows (`Draft`/`Final`, `Invoiced` when an invoice line references the act) and verify with a test
- [x] 3.4 Require the operational policy on the group, and verify an anonymous and a roleless request are rejected

## 4. Creation flow (act + lines at create, review page)

- [x] 4.1 Build the create dialog (project picker + start/end dates) with validation
- [x] 4.2 Make the dialog post to `create`: server-side FluentValidation with inline field errors; valid input snapshots the period's entries into a new Draft act with its lines (rate = entry rate else project rate) in one save and navigates (`HX-Location`) to `GET review/{id}`; zero entries → toast, nothing created
- [x] 4.3 Add the review page `GET review/{id}`: no validation, renders the act's lines (the Draft edit screen) with the multi-act marker; verify an entry starting exactly at the end instant is excluded
- [x] 4.4 Mark lines whose entry appears in other acts (distinct style + tooltip naming those acts) on the review screen

## 5. Edit, finalize, detail and deletion

- [x] 5.1 Add the Draft act edit screen: persisted per-line edits (row swap + recomputed totals), frozen for Final/Invoiced, and verify with tests
- [x] 5.2 Add the Final action (Draft → Final, freezes lines and deletion) with a confirmation-free button, and verify
- [x] 5.3 Rework the detail view to show the act's lines (read-only) with the multi-act marker and tooltip
- [x] 5.4 Rework deletion: Draft only, removes act + lines, entries untouched; Final/Invoiced refused with a toast; verify

## 6. Time-tracking lock via lines

- [x] 6.1 Replace the `WorkActId` checks in edit/delete/move with "a work act line references this entry" (cross-feature `IQuery<WorkActLine>`), and make the notice list every act referencing the entry
- [x] 6.2 Drive the grid marker and the edit dialog suppression from the same line-existence check (drop `TimeTrackingEntryRow.WorkActId` in favour of a flag), and verify the grid fragment shows the marker
- [x] 6.3 Remove `TimeTracking.Version` usage and verify the entry CRUD tests still pass

## 7. Tests and docs

- [x] 7.1 Rework `WorkActsTests` for the new model (create → lines + totals, review edits do not touch entries, entry in two acts, draft line edit persists, final freezes, delete rules, multi-act marker) and remove the certifier tests
- [x] 7.2 Rework the time-tracking lock tests to seed lines instead of `WorkActId`, including the multi-act notice
- [x] 7.3 Document the feature in `Apskaita/README.md` (flow, statuses, lock rule, what is deferred) and update `entities.md` §3.7/3.8 to the line-based shape, and verify the documented routes match the code

## Workflow follow-up

- Archive the change after review, syncing the `work-acts` and `time-tracking` deltas into `openspec/specs/`.
