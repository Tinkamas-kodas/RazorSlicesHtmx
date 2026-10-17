# Tasks

## 1. Storage and parsing

- [x] 1.1 Add the `Ics` provider kind to the connector model and verify the enum-backed mapping accepts it
- [x] 1.2 Add a parsed-events store (rows keyed to an `Ics` connector) and a migration creating it, and verify the migration applies against dev MySQL
- [x] 1.3 Add `Ical.Net` to central package management and reference it, and verify restore succeeds
- [x] 1.4 Implement `.ics` parse → normalized event rows (name from `X-WR-CALNAME` fallback file name, count, date range, warnings) and verify with unit tests over sample files

## 2. Import endpoints

- [x] 2.1 Add the create + upload endpoints (`POST /account/ics/create` for a named calendar, `POST /account/ics/{id}/import` multipart, antiforgery via the htmx header hook) that parse and store events in one transaction and return the toast + connections-table OOB update, and verify a valid upload stores events
- [x] 2.2 Implement "Replace file" (re-upload replaces the stored events and records the new import time) and verify the events are replaced
- [x] 2.3 Implement delete of an imported calendar (cascades to events) and verify the row and events are removed

## 3. Connections table and week panel

- [x] 3.1 Render imported calendars in the connections table with event count, and verify they appear alongside OAuth connectors
- [x] 3.2 Merge imported events into the week panel's per-day grouping, and verify an in-range imported event renders

## 4. Delete keeps references

- [x] 4.1 Confirm deleting an imported calendar keeps the event reference on existing `TimeTracking` entries, and verify with a test
- [x] 4.2 Confirm re-importing the same `.ics` re-links those entries to the re-imported events, and verify with a test

## 5. Tests and docs

- [x] 5.1 Add integration tests (upload valid/invalid, replace, delete, panel merge, reference kept on delete/re-import) and verify they pass
- [x] 5.2 Document the `.ics` import path in the README/requirements integrations section, and verify the documented routes match the code

## Workflow follow-up

- Archive the change after review, syncing the new `calendar-connectors` requirements into `openspec/specs/`.
