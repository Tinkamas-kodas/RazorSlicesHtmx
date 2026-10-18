# Design

## Context

`WorkAct`/`WorkActLine` exist as entities and MySQL tables (from `InitialCreate`) but no feature reads or
writes them. Time entries are freely editable. The architecture rule is: reads through `IQuery<T>`, writes
through the owning feature's internal store, cross-feature capability checks through a public interface in
`Apskaita.Shared`. An earlier iteration of this change linked entries to acts via `TimeTracking.WorkActId`
(one act per entry); the product direction now is line-based acts whose entries may appear in several acts.

## Goals / Non-Goals

**Goals:**
- A work act is a document of its own: its lines snapshot the work (duration, rate, start, description)
  and keep a reference to the entry they came from.
- An entry referenced by any act line cannot be edited, deleted or moved, so the act's numbers stay
  traceable to real work.
- Status flow Draft → Final → Invoiced with the invoice link derived from the existing
  `InvoiceLine.WorkActId`.
- Keep one owner per table: WorkActs owns `work_acts` and `work_act_lines`; TimeTracking owns
  `time_trackings` and only reads the act tables.

**Non-Goals:**
- No export (CSV/Excel) — planned separately.
- No invoice creation from acts (only the derived `Invoiced` display); no act splitting; no act numbers.
- No adding or removing lines after creation (edit the fields only).

## Decisions

- **Lines are copies with a pointer.** `WorkActLine` = `WorkActId` (cascade FK), `TimeTrackingId` (FK,
  restrict), `DurationMinutes`, `HourlyRate` (int, NOT NULL — entry rate else project rate at snapshot
  time), `StartedAtUtc` (copied), `Description` (copied, editable). `TimeTracking` keeps no act column:
  the many-to-many lives entirely in `work_act_lines`. A drift-free direct link (the earlier design) was
  dropped because an entry must be able to appear in several acts.
- **Period = UTC instants from local boundaries.** `PeriodStartUtc`/`PeriodEndUtc` are computed with
  `BrowserTimeZone.WallToUtc` from the dates the user picked (end = next local midnight, exclusive), the
  same way the week grid bounds work. Selection is a plain instant comparison, so the covered entry set is
  viewer independent while the UI still shows local dates.
- **Totals are stored, recomputed on every save.** Hours = Σ duration/60, amount = Σ duration/60 × line
  rate, each rounded to 2 decimals away from zero. Lines can change while the act is Draft, so the
  denormalized totals are refreshed whenever lines or status are saved.
- **Entry lock = line existence.** TimeTracking edit/delete/move reject an entry when
  `work_act_lines` references it; the notice lists the act(s) by project and period. Implemented as a
  cross-feature read (`IQuery<WorkActLine>` + `IQuery<WorkAct>`), so the `ITimeEntryCertifier` seam and
  `TimeTracking.Version` are removed — there is no cross-feature write left.
- **Creation is one step; the review page shows the stored act.** The dialog posts project + period; the
  endpoint validates it (FluentValidation, inline errors re-render the dialog), snapshots the covered
  entries into a new Draft act with its lines in one save, and answers with `HX-Location` to
  `GET review/{id}` — the same line screen the list's Edit action opens. A period with no entries creates
  nothing and toasts. No transient draft state exists; the review never edits entries.
- **Editing a saved act** (Draft only) persists each line edit immediately (single-row htmx swap plus
  refreshed totals), mirroring the review screen's look with real rows.
- **Status.** `Draft` = lines editable, act deletable; **Final** (a button) freezes lines and blocks
  deletion; `Invoiced` is derived at read time: a Final act referenced by an `InvoiceLine.WorkActId` is
  presented as Invoiced and read-only. The stored enum is `Draft|Final|Invoiced`; nothing writes
  `Invoiced` yet.
- **Multi-act recognisability.** On review/edit/detail, a line whose entry is referenced by other acts is
  rendered with a distinct style and a tooltip naming those acts (project · period), computed with one
  extra query over `work_act_lines` joined to `work_acts`.
- **Race protection = the act's `Version` token only.** Concurrent edits of the same act lose via
  `DbUpdateConcurrencyException` → refresh toast. Concurrent creation over the same entries is allowed by
  design (each act gets its own lines), so no entry-level token is needed.
- **Feature shape.** Assembly `Apskaita.Feature.WorkActs`, route group `/work-acts`, nav item
  `new("MAIN","work-acts","Work acts","",Order: 3, Operational)`, sections by composition (`List`,
  `Create` incl. review, `Detail`/`Edit`) exactly like `TimeTrackingEndpoints`.
- **Migration is data-preserving.** `Up` creates `work_act_lines`, copies every non-null
  `time_trackings.work_act_id` link into a line (rate = `COALESCE(entry rate, project rate)`), then drops
  `work_act_id`/`version` from `time_trackings` and widens the `work_acts.status` enum.

## Risks / Trade-offs

- [Lines drift from their entries when entries are edited before any act exists] accepted: the lock only
  starts at snapshot time, and each act's numbers describe the work as certified in that act.
- [An entry in several acts makes "delete the act to fix the entry" ambiguous] the lock notice lists every
  act referencing the entry, and deleting any one of them releases only that act's line — the entry stays
  locked while another act still references it.
- [Dropping `time_trackings.work_act_id` is destructive] the migration copies the links into lines first;
  `Down` restores the column from lines that are unique per entry.
- [The act is created before the user reviews it] accepted: creation is the snapshot, the review page is
  where lines get corrected; a wrong act is deleted (while Draft) and recreated.
