# Spec Delta

## REMOVED Requirements

### Requirement: Week page shows the connected user's events
**Reason**: Calendar events now render inline in the time-tracking grid (see the `time-tracking` capability); the side panel is replaced by a connections dock that shows no events.
**Migration**: Event display is covered by `time-tracking` "Calendar events enrich the week without breaking it"; connection status is covered by the added "Connections dock lists connections with status".

## ADDED Requirements

### Requirement: Connections dock lists connections with status

The time-tracking view SHALL provide a connections dock, loaded independently of the entry grid, that lists each calendar connection with its status and connect/disconnect actions and shows no calendar events.

#### Scenario: Dock shows connections

- **WHEN** a user opens the time-tracking view
- **THEN** the dock lists each connection with its status (connected, error, or last sync) and connect/disconnect actions
- **AND** no calendar events are listed in the dock

#### Scenario: Dock loads independently

- **WHEN** the entry grid refreshes
- **THEN** the dock is not re-fetched as part of that refresh
