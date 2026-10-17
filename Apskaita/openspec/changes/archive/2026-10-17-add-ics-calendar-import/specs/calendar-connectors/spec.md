# Spec Delta

## ADDED Requirements

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
