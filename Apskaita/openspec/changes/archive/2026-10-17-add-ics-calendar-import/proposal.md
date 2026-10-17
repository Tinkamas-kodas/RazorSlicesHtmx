# Proposal

## Why

Some calendars cannot be connected through OAuth (Google Calendar without client credentials, restricted tenants, read-only shared calendars, Apple/iCloud). A credential-free `.ics` import gives those users the same week-panel view, replacing the earlier Outlook add-in bridge that added install friction without new capability.

## What Changes

- Add an `.ics` (RFC 5545 `VCALENDAR`) upload that the server parses and stores as an imported calendar connection plus its events.
- List imported calendars in the connections table alongside OAuth connectors, each with its event count.
- Support "Replace file" (re-upload replaces the stored events) and delete an imported calendar.
- Merge imported events into the time-tracking week panel like any other connection.
- Deleting an imported calendar keeps the event reference on existing time entries, so re-importing the same `.ics` re-links them.

## Capabilities

### New Capabilities
- _(none)_

### Modified Capabilities
- `calendar-connectors`: adds `.ics` import as a credential-free connection source and panel merge for imported events.

## Impact

- New `Provider = Ics` connector kind and a parsed-events store in the data layer.
- Account/Settings connections table and the time-tracking week panel gain imported-calendar rows and merged events.
- Multipart upload endpoints under the account routes, antiforgery-protected via the existing htmx header hook.
- New dependency for `.ics` parsing (`Ical.Net`), added to central package management.
