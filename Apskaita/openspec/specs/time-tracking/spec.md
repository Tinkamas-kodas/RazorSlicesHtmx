# Time Tracking Specification

## Purpose

Server-rendered weekly time tracking: listing a week's entries in a grid, creating, editing, deleting, and moving entries through dialogs, and logging entries directly from calendar events.

## Requirements

### Requirement: Week grid page and fragment

The time-tracking feature SHALL expose a page route that renders the shell with an empty grid placeholder, and a grid route that returns the grid fragment for the requested week.

#### Scenario: Full page request

- **WHEN** a user requests the time-tracking page
- **THEN** the shell with controls and an empty grid placeholder is returned
- **AND** the placeholder loads the grid fragment on its own

#### Scenario: Grid requested over HTMX

- **WHEN** the grid route is requested as an HTMX fragment
- **THEN** only the grid fragment is returned

#### Scenario: Direct navigation to the grid route

- **WHEN** the grid route is requested without HTMX
- **THEN** the user is redirected to the page route with the query preserved

### Requirement: Week navigation and display-zone scoping

The grid SHALL show the week containing an anchor date, offer previous and next week navigation, and select entries by converting the display-zone week bounds to the stored UTC values.

#### Scenario: Anchor week

- **WHEN** a week start is supplied
- **THEN** the grid shows the seven-day week beginning on that date

#### Scenario: Previous and next week

- **WHEN** the user navigates to the previous or next week
- **THEN** the anchor shifts by seven days in the chosen direction

#### Scenario: Entry outside the displayed week

- **WHEN** an entry's start falls outside the displayed week
- **THEN** it is not listed in that week's grid

### Requirement: Entry rows show project color and summary

Each listed entry SHALL render with its project's color and its start time, duration, and description.

#### Scenario: Rendering an entry

- **WHEN** the grid renders an entry joined to its project
- **THEN** the card shows the project color, start, duration, and description

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

### Requirement: Creating an entry

The system SHALL create a time entry from a dialog; a valid submission adds the entry, refreshes only the grid, and shows a success toast, while an invalid submission keeps the dialog open with the submitted values.

#### Scenario: Valid create

- **WHEN** a user submits a valid create form
- **THEN** the entry is stored, only the grid refreshes, and a success toast appears

#### Scenario: Invalid create

- **WHEN** a user submits a create form that fails validation
- **THEN** the dialog stays open and the submitted values are preserved

### Requirement: Weekly recurrence batch on create

Creating an entry with recurrence enabled SHALL produce one entry for each occurrence within the target week.

#### Scenario: Recurring create

- **WHEN** a user creates a recurring entry for a set of weekdays in the week
- **THEN** one entry is created per selected occurrence

### Requirement: Editing an entry

Activating a rendered entry card, by pointer or keyboard, SHALL open an edit dialog prefilled with that entry's current values; a valid save updates the entry, refreshes only the grid, and shows a success toast, while an invalid save keeps the dialog open with the submitted values.

#### Scenario: Open and save

- **WHEN** a user activates an entry card and submits a valid edit
- **THEN** the entry is updated, only the grid refreshes, and a success toast appears

#### Scenario: Invalid edit

- **WHEN** a user submits an edit that fails validation
- **THEN** the dialog stays open and all submitted values are preserved

### Requirement: Deleting an entry with confirmation

The edit dialog's delete action SHALL replace the dialog with a confirmation; confirming removes the entry and refreshes only the grid, and cancelling returns to the edit dialog without changes.

#### Scenario: Confirm delete

- **WHEN** a user confirms deletion
- **THEN** the entry is removed, the dialog closes, and only the grid refreshes

#### Scenario: Cancel delete

- **WHEN** a user opens the delete confirmation and cancels
- **THEN** the edit dialog is shown again and no data changes

### Requirement: Copying an entry

The edit dialog's copy action SHALL submit the current form values to create, producing a new entry while leaving the source entry unchanged.

#### Scenario: Copy an entry

- **WHEN** a user changes values and clicks copy
- **THEN** a new entry is created from the current values and the original is unchanged

### Requirement: Moving an entry by drag

The system SHALL move an existing entry to a new date and time supplied by a drag action, updating its start and refreshing the grid.

#### Scenario: Valid move

- **WHEN** a user drags an entry to a new slot
- **THEN** the entry's start is updated to the new display-zone time and the grid refreshes

#### Scenario: Invalid move target

- **WHEN** a move request carries an unparseable date or time
- **THEN** the request is rejected and the entry is unchanged

### Requirement: Hourly-rate override toggle

The hourly-rate override SHALL be a single toggle beside the rate input; when off the rate is cleared and disabled so the entry uses the default, and when on the input is enabled, focused, and required before submission.

#### Scenario: Override off

- **WHEN** the override toggle is off
- **THEN** the rate input is cleared and disabled and the default rate applies

#### Scenario: Override on

- **WHEN** the override toggle is on
- **THEN** the rate input is enabled and required and a value must be entered

### Requirement: Create and edit share one form slice

Create and edit SHALL use separate dialog shells that both render a single shared form-fields slice, so field markup is not duplicated.

#### Scenario: Field markup reuse

- **WHEN** either the create or edit dialog renders
- **THEN** the editable fields come from the same shared form slice

### Requirement: Entry identity comes from the route and missing entries are not found

The entry id used for edit, delete, and move SHALL come from the route, never a posted form value, and a request naming a non-existent entry SHALL return HTTP 404 without changing data.

#### Scenario: Id not trusted from form

- **WHEN** an edit request is submitted
- **THEN** the target entry is identified solely by the route id

#### Scenario: Unknown entry

- **WHEN** an edit, delete, or move request names a non-existent entry id
- **THEN** the response is HTTP 404 and no data changes

### Requirement: Logging an entry from a calendar event

Each calendar event card in the grid SHALL route through a single endpoint carrying the event's display date, time, duration, week, and identity triple. When no entry anywhere references the event the endpoint SHALL return a prefilled create dialog; when an entry anywhere already references it the endpoint SHALL return an empty response with an "already logged" notice instead of a dialog.

#### Scenario: Untracked event

- **WHEN** a user activates an event that no entry references
- **THEN** a create dialog is returned prefilled from the event, with duration clamped to the allowed range

#### Scenario: Tracked event

- **WHEN** a user activates an event that an existing entry references
- **THEN** no dialog opens and an "already logged" notice is shown

### Requirement: Event reference is validated before any dialog

The event endpoint SHALL validate the supplied reference before returning any dialog, rejecting an unknown provider, an account not owned by the current user for that provider, or a blank or oversized event id with HTTP 404.

#### Scenario: Forged account

- **WHEN** the request names an account that is not one of the current user's connectors for the provider
- **THEN** the response is HTTP 404 and no dialog is returned

### Requirement: At most one entry per event

The system SHALL enforce, via a unique index on the event identity triple, that no more than one entry references a given event, while entries without a reference coexist freely.

#### Scenario: Duplicate reference on create

- **WHEN** creating an entry whose event triple already exists
- **THEN** no second row is created and an "already logged" message is shown

#### Scenario: Manual entries

- **WHEN** entries are created without any event reference
- **THEN** any number of such rows coexist

### Requirement: Creating from an event persists the reference atomically

Creating an entry from an event SHALL persist the full identity triple, and a partial reference SHALL be rejected as a validation error without creating a row.

#### Scenario: Complete reference

- **WHEN** a create request carries all three reference fields and passes ownership checks
- **THEN** the new entry stores the reference

#### Scenario: Partial reference

- **WHEN** a create request carries only some of the reference fields
- **THEN** validation fails and no row is created

### Requirement: Editing and moving keep the event reference; deleting releases it

Editing or moving an entry SHALL leave its event reference unchanged, and deleting an entry SHALL release the event so it renders in the grid again and its next activation returns a create dialog.

#### Scenario: Entry deleted

- **WHEN** an entry that references an event is deleted
- **THEN** the event renders in the grid again and its next activation returns a create dialog

### Requirement: Project lookup for the dialog

The feature SHALL offer a project lookup that lists active projects matching a search term and echoes the label of a selected project.

#### Scenario: Search projects

- **WHEN** a user searches the project picker
- **THEN** active projects whose name or code match are offered

#### Scenario: Echo a selected project

- **WHEN** a selected project id is resolved
- **THEN** its display label is returned, or not found when it is inactive or unknown

### Requirement: Access requires the operational role

Every time-tracking route SHALL require an authenticated user with the operational role.

#### Scenario: Unauthenticated or roleless

- **WHEN** an unauthenticated or roleless user requests a time-tracking route
- **THEN** access is denied and the user is directed to sign in or to the pending-access page

### Requirement: Recurrence is suppressed for event-derived entries

A create dialog opened from a calendar event SHALL hide recurrence controls and ignore recurrence, because a recurring batch would collide with the one-entry-per-event rule.

#### Scenario: Event create dialog

- **WHEN** a create dialog is opened from an event
- **THEN** no recurrence controls are offered
