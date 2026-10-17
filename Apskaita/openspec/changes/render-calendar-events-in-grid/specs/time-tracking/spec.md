# Spec Delta

## MODIFIED Requirements

### Requirement: Calendar events enrich the week without breaking it

The time-tracking grid SHALL render the connected user's calendar events for the displayed week as grey cards on the same day and time layout as entries, always behind entry cards, and SHALL hide an event when a time entry in the displayed week references it. A failure to read events SHALL NOT prevent the entries from rendering.

#### Scenario: Event in the displayed week

- **WHEN** the grid renders a week that contains a calendar event
- **THEN** the event appears as a grey card positioned by its start and duration, behind any overlapping entry

#### Scenario: Event already logged this week

- **WHEN** a time entry in the displayed week references the event
- **THEN** the event is not rendered because the entry represents it

#### Scenario: Calendar read fails

- **WHEN** the calendar reader returns an error or is unavailable
- **THEN** the entries still render and a notice is shown

### Requirement: Logging an entry from a calendar event

Each calendar event card in the grid SHALL route through a single endpoint carrying the event's display date, time, duration, week, and identity triple. When no entry anywhere references the event the endpoint SHALL return a prefilled create dialog; when an entry anywhere already references it the endpoint SHALL return an empty response with an "already logged" notice instead of a dialog.

#### Scenario: Untracked event

- **WHEN** a user activates an event that no entry references
- **THEN** a create dialog is returned prefilled from the event, with duration clamped to the allowed range

#### Scenario: Tracked event

- **WHEN** a user activates an event that an existing entry references
- **THEN** no dialog opens and an "already logged" notice is shown

### Requirement: Editing and moving keep the event reference; deleting releases it

Editing or moving an entry SHALL leave its event reference unchanged, and deleting an entry SHALL release the event so it renders in the grid again and its next activation returns a create dialog.

#### Scenario: Entry deleted

- **WHEN** an entry that references an event is deleted
- **THEN** the event renders in the grid again and its next activation returns a create dialog

## REMOVED Requirements

### Requirement: Tracked events show a badge and suppress recurrence
**Reason**: The "Logged" badge is obsolete — a same-week logged event is now hidden from the grid rather than badged.
**Migration**: Recurrence suppression moves to the added "Recurrence is suppressed for event-derived entries" requirement; the badge is replaced by hiding, in "Calendar events enrich the week without breaking it".

## ADDED Requirements

### Requirement: Recurrence is suppressed for event-derived entries

A create dialog opened from a calendar event SHALL hide recurrence controls and ignore recurrence, because a recurring batch would collide with the one-entry-per-event rule.

#### Scenario: Event create dialog

- **WHEN** a create dialog is opened from an event
- **THEN** no recurrence controls are offered
