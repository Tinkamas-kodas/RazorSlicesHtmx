# Spec Delta

## REMOVED Requirements

### Requirement: Week page shows the connected user's events
**Reason**: Calendar events now render inline in the time-tracking grid (see the `time-tracking` capability); the side panel is replaced by a connections dock that shows no events.
**Migration**: Event display is covered by `time-tracking` "Calendar events enrich the week without breaking it"; connection status is covered by the added "Connections dock lists connections with status".

### Requirement: Imported events merge into the week panel
**Reason**: Imported events now merge into the same in-grid rendering as connector events; there is no week panel.
**Migration**: Renamed to the added "Imported events merge into the week grid".

## MODIFIED Requirements

### Requirement: Token refresh and graceful degradation

The system SHALL reuse a valid cached access token, refresh it when expired, and on any provider or network failure degrade gracefully without breaking the page.

#### Scenario: Expired access token

- **WHEN** the cached access token has fewer than five minutes remaining
- **THEN** the system refreshes it with the stored refresh token and continues rendering events

#### Scenario: Provider unavailable

- **WHEN** the calendar provider returns an error or is unreachable
- **THEN** the grid shows an "unavailable" notice with a short reason
- **AND** time entries still render and the response is HTTP 200

## ADDED Requirements

### Requirement: Connections dock lists connections with status

The time-tracking view SHALL provide a connections dock that is collapsed by default, expands on demand,
and lists each calendar connection as one compact row with a health dot. The dock SHALL be loaded
independently of the entry grid, SHALL show no calendar events, SHALL show no import statistics, and SHALL
offer only connect and import actions — disconnecting and calendar creation stay on the account page,
which the dock links to.

#### Scenario: Dock shows connections

- **WHEN** a user opens the time-tracking view
- **THEN** the dock is collapsed to a labelled rail
- **AND** expanding it lists each connection with a health dot and, where applicable, a connect or import action
- **AND** the rail's own label is not repeated inside the expanded body
- **AND** no calendar events, no event statistics and no disconnect action are shown

#### Scenario: Dock loads independently

- **WHEN** the entry grid refreshes
- **THEN** the dock is not re-fetched as part of that refresh

#### Scenario: OAuth connection health

- **WHEN** the dock lists a connector-backed account
- **THEN** the dot is green when its last sync reported no error and red when it did, with the reason in the tooltip

#### Scenario: Imported calendar health is week-relative

- **WHEN** the dock lists an imported calendar whose stored events do not overlap the displayed week
- **THEN** the dot is red and the tooltip shows the covered range
- **AND** the dot is green when the stored events do overlap the displayed week

#### Scenario: Importing from the dock

- **WHEN** a user activates an imported calendar's import action in the dock
- **THEN** a dialog offers the file picker and the calendar's events are replaced on submit
- **AND** only the dock refreshes afterwards

### Requirement: Imported events merge into the week grid

The time-tracking grid SHALL include events from imported calendars for the displayed week, rendered exactly like connector events.

#### Scenario: Imported event in range

- **WHEN** a user views a week that contains events from an imported calendar
- **THEN** those events render in the grid on the same day/time layout as connector events
