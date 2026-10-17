# Calendar Connectors Specification

## Purpose

Defines per-user calendar connections to providers (Outlook 365 first) and the display of the connected user's calendar events for the current week on the time-tracking page.

## Requirements

### Requirement: Settings lists calendar providers with status

The system SHALL present a user-level Settings view, restricted to the operational role, that lists every registered calendar provider with its connection status (not connected / connected as an account email / error), last sync time, and last sync error when present.

#### Scenario: Operational user opens Settings

- **WHEN** a user with role `user` or `admin` opens the Settings page
- **THEN** each registered provider is shown with its connection status
- **AND** providers without an implementation are shown as "coming soon"

### Requirement: Connect stores an encrypted connector for the current user

Connecting a provider SHALL start an explicit per-user OAuth consent flow and, on success, store a connector row for the current session user holding the refresh token encrypted at rest, unique per user and provider (reconnect replaces).

#### Scenario: Successful consent

- **WHEN** a user completes the provider consent flow
- **THEN** a connector row is created or replaced for that user and provider
- **AND** the refresh token is stored encrypted and never in plaintext
- **AND** the session cookie is unchanged

#### Scenario: Connect when provider is not configured

- **WHEN** the connect flow is requested but the provider scheme is not registered
- **THEN** the request is rejected with HTTP 400

### Requirement: Disconnect removes the connector and forgets tokens

Disconnecting a provider SHALL delete the connector row and forget all stored tokens, and SHALL be antiforgery-protected.

#### Scenario: User disconnects

- **WHEN** a user confirms disconnect for a connected provider
- **THEN** the connector row is deleted
- **AND** subsequent week views show the no-connector state

### Requirement: Week page shows the connected user's events

The time-tracking week page SHALL show the connected user's calendar events for the displayed week in a side panel grouped by day, reading events per page request with no background sync.

#### Scenario: Connected user views a week

- **WHEN** a user with a valid connector opens a week
- **THEN** that week's events render grouped by day with subject, local time range, location/online hint, and organizer when not the user

#### Scenario: No connector

- **WHEN** a user has no connector for any provider
- **THEN** the panel shows a hint linking to Settings

### Requirement: Token refresh and graceful degradation

The system SHALL reuse a valid cached access token, refresh it when expired, and on any provider or network failure degrade gracefully without breaking the page.

#### Scenario: Expired access token

- **WHEN** the cached access token has fewer than five minutes remaining
- **THEN** the system refreshes it with the stored refresh token and continues rendering events

#### Scenario: Provider unavailable

- **WHEN** the calendar provider returns an error or is unreachable
- **THEN** the panel shows an "unavailable" notice with a short reason
- **AND** time entries still render and the response is HTTP 200

### Requirement: Connector data is scoped to the session user

Connector rows SHALL be readable and writable only for the authenticated session user, with no admin-level impersonation.

#### Scenario: Reading another user's connector

- **WHEN** a request attempts to read or disconnect a connector not owned by the session user
- **THEN** the operation does not affect that row

### Requirement: Provider registry pattern

Providers SHALL be resolved by key through a registry of connector implementations, so new providers can be added without changing the Settings view.

#### Scenario: Adding a provider

- **WHEN** a new `ICalendarProviderConnector` implementation is registered
- **THEN** the Settings page lists it automatically

### Requirement: Import an ICS calendar by upload

The system SHALL accept an uploaded `.ics` file, parse its events, and store them as an imported calendar connection for the current user, returning a summary of the calendar name, event count, covered date range, and any parse warnings.

#### Scenario: Valid upload

- **WHEN** a user uploads a well-formed `.ics` file
- **THEN** an imported calendar connection is created for that user with its parsed events
- **AND** a summary is shown and the connections table updates

#### Scenario: Invalid file

- **WHEN** the uploaded file is not a parseable `VCALENDAR`
- **THEN** no connection is created and an error is shown

### Requirement: Imported calendars appear in the connections table

The system SHALL list each imported calendar in the connections table with its account/provider label and event count, alongside OAuth connectors.

#### Scenario: User has an imported calendar

- **WHEN** a user opens the connections view
- **THEN** the imported calendar is listed with its event count

### Requirement: Replace file updates an imported calendar's events

Re-uploading a file for an existing imported calendar SHALL replace its stored events wholesale and record the new import time.

#### Scenario: Replace file

- **WHEN** a user replaces an imported calendar's file
- **THEN** its stored events are replaced with the newly parsed ones

### Requirement: Imported events merge into the week panel

The time-tracking week panel SHALL include events from imported calendars for the displayed week, in the same per-day grouping as connector events.

#### Scenario: Imported event in range

- **WHEN** a user views a week that contains events from an imported calendar
- **THEN** those events render in the panel grouped by day

### Requirement: Delete an imported calendar

The system SHALL allow a user to delete one of their imported calendars and its stored events, antiforgery-protected. Deleting an imported calendar SHALL NOT remove the calendar-event reference already stored on existing time entries, so re-importing the same `.ics` re-links those entries to the re-imported events.

#### Scenario: User deletes an import

- **WHEN** a user deletes an imported calendar
- **THEN** its connection and stored events are removed and the connections table updates

#### Scenario: Existing entries keep their reference

- **WHEN** a user deletes an imported calendar that an existing time entry references
- **THEN** the time entry keeps its stored event reference
- **AND** re-importing the same `.ics` links that entry to the re-imported event again
