# Proposal

## Why

Calendar events currently render in a separate side panel next to the time-tracking grid, and the account page duplicates connection management. Showing events inline in the grid (grey, behind entries) gives one unified week view where logged time and its source events line up on the same timeline, and frees the side panel to become a lightweight connections dock.

## What Changes

- Render calendar events **inside the time-tracking grid** as grey cards positioned on the same day/time layout as entries, always behind entry cards.
- **Hide** an event in the grid when a time entry in the **displayed week** references it (the entry already represents it); drop the "Logged" badge.
- Clicking an event opens the prefilled **create** dialog; if any entry **anywhere** already references that event, return an empty response + "already logged" toast instead of a dialog (the global unique index makes a duplicate create impossible).
- Remove the redirect-to-edit branch from the event endpoint.
- Replace the side calendar panel with a **connections dock**: connection status plus connect/disconnect, loaded by its own `hx-get` (not tied to the grid refresh), showing **no** calendar events.

## Capabilities

### New Capabilities
- _(none)_

### Modified Capabilities
- `time-tracking`: events render in-grid (grey, behind, hidden when a same-week entry references them); event click is create-or-toast (no edit redirect); badge removed.
- `calendar-connectors`: the week side panel becomes a connections dock (status + connect/disconnect, no events).

## Impact

- `TimeTrackingEndpoints`: `GET event` (global tracked check → toast vs create dialog; drop edit redirect); grid model gains in-grid event geometry; new dock fragment route.
- Slices: `_Grid.cshtml` renders event cards; `_CalendarPanel.cshtml` replaced by a connections-dock partial; all-day row added.
- Reads still go through `IQuery<TimeTracking>` / `ICalendarEventReader`; connect/disconnect reuse the existing `/account/connect/{provider}` and `/account/disconnect/{id}` endpoints.
- No schema change — the existing unique index on the event reference is the enforcement point.
