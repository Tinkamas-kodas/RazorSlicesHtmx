# Tasks

## 1. Grid event rendering

- [ ] 1.1 Add in-grid event geometry to the week model (grey cards sharing the entry slot/height/overlap math, emitted behind entries) and verify the grid renders an event at its start/duration
- [ ] 1.2 Filter out events referenced by a same-week entry using the existing week-bounded tracked set, and verify a logged event is absent while its entry shows
- [ ] 1.3 Render all-day and multi-day events in a dedicated all-day row, and verify an all-day event shows as a grey band
- [ ] 1.4 Add grey event-card and all-day-row styles to `site.css` and bump `Assets.Version`, and verify events read as background vs colored entries
- [ ] 1.5 Remove the "Logged" badge markup and its CSS, and verify no badge renders

## 2. Event click behavior

- [ ] 2.1 Change `GET event` to a global (no week filter) tracked-reference check: found → `_Empty` + "already logged" toast; not found → prefilled create dialog, and verify both branches
- [ ] 2.2 Remove the redirect-to-`edit/{id}` branch from `GET event`, and verify the endpoint never returns a redirect
- [ ] 2.3 Keep recurrence suppressed for event-derived creates, and verify the event create dialog has no recurrence controls
- [ ] 2.4 Add endpoint tests (unlogged → create dialog; cross-week-logged → toast, no dialog; forged reference → 404), and verify they pass

## 3. Connections dock

- [ ] 3.1 Add a `connections` fragment route returning the dock (connectors + status, no events), loaded by its own `hx-get` outside `#time-tracking-grid`, and verify it renders independently of the grid
- [ ] 3.2 Wire connect/disconnect in the dock to the existing `/account/connect/{provider}` and `/account/disconnect/{id}` endpoints with OOB dock update + toast, and verify an action refreshes only the dock
- [ ] 3.3 Replace `_CalendarPanel.cshtml` with the dock partial and remove the side-panel event list, and verify no calendar events appear in the dock
- [ ] 3.4 Add dock tests (status rendering, no events, independent load), and verify they pass

## 4. Regression and docs

- [ ] 4.1 Verify deleting a referencing entry makes the event reappear in the grid and its next click opens create, and add/adjust a test
- [ ] 4.2 Update the time-tracking and calendar-connector docs to describe in-grid events and the connections dock, and verify the documented routes match the code

## Workflow follow-up

- Archive the change after review, syncing the `time-tracking` and `calendar-connectors` deltas into `openspec/specs/`.
