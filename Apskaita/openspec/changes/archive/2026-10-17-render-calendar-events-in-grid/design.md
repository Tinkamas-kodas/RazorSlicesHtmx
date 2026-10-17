# Design

## Context

See proposal.md - Why. Today `_Grid.cshtml` renders only entry cards and `_CalendarPanel.cshtml` renders events in a separate side panel; the account page owns connect/disconnect. The event endpoint (`GET event`) redirects to `edit/{id}` when a same-week tracked reference is found. The grid's tracked-reference query is already week-bounded, while the DB unique index on the event triple is global.

## Goals / Non-Goals

**Goals:**
- One timeline: events and entries share the day/time grid, events grey and behind.
- Keep the grid's hide logic week-bounded (what it can cheaply know) and put the global correctness check in the event endpoint.
- Decouple the connections dock from the grid refresh.

**Non-Goals:**
- No schema change; the existing unique index stays the enforcement point.
- No new connect/disconnect endpoints — the dock reuses `/account/connect/{provider}` and `/account/disconnect/{id}`.
- No cross-week hiding in the grid (a cross-week-linked event still shows grey and toasts on click).

## Decisions

- **Event geometry reuses the entry math.** Render events through the same slot/height/overlap computation as `TimeTrackingEntryRow`, emitting them first (or at a lower `z-index`) so entries paint on top. Alternative (a second absolute-positioning pass) rejected as duplicate logic.
- **Hide = week-bounded tracked set.** The grid already loads `CalendarEvent != null` rows within the week bounds; reuse that set to filter events out. Global correctness is NOT the grid's job.
- **`GET event` = global check → toast, else create.** Replace the `Results.Redirect(edit/{id})` branch with: query `entries` for the triple with NO week filter; if found return `_Empty` + `WithToast("This calendar event is already logged.")` (delivered via the HX-Trigger header); else the prefilled create dialog. The `edit/{id}` route itself is unchanged (entry cards still use it).
- **Dock is its own fragment.** A new `hx-get` route (e.g. `connections`) returns a dock partial listing connectors + status; it is NOT inside `#time-tracking-grid`, so `time-tracking-list-refresh` never re-fetches it. Connect/disconnect post to the existing account endpoints and update the dock via OOB.
- **All-day / multi-day events** render in a dedicated all-day row at the top of each day column (grey band), not clamped into the 30-minute slot grid.

## Risks / Trade-offs

- [Stale grid shows an event that was logged in another tab] → the endpoint's global check toasts instead of opening a doomed create; the unique index is the final backstop.
- [Dock and account page both manage connections] → acceptable duplication of the same endpoints; the dock is a convenience surface, account page remains authoritative.
- [Event/entry visual collision] → events always behind + grey; entries keep full color and higher z-order, so the timeline stays readable.
