# Spec Delta

## Purpose

Certifying a project's work for a period: a work act gathers the project's time entries between two dates
into its own line rows (a frozen copy of duration, rate, start and description that references the entry
it came from), moves through Draft → Final → Invoiced, and can be listed, edited and deleted.

## ADDED Requirements

### Requirement: Work act contains work act lines

A work act SHALL record one project, the client derived from that project, its currency, a period, the
totals in hours and in the act currency, and a status. The act's content SHALL be its work act lines:
each line stores duration in minutes, an hourly rate that is never null, a start date and time, a
description, and a reference to the time tracking entry it was made from. An act SHALL NOT carry a
human-readable number, and one entry SHALL be allowed to appear in the lines of several acts.

#### Scenario: Line fields at creation

- **WHEN** a line is snapshotted from a time entry during act creation
- **THEN** it stores that entry's duration, start and description and the entry's hourly rate, falling back to the project rate when the entry has none

#### Scenario: Entry in several acts

- **WHEN** a new act is created over an entry that another act already references through a line
- **THEN** the new act is created with its own line and the other act and its lines are unchanged

### Requirement: Creating an act starts with a project and period

The work-acts feature SHALL offer a creation dialog that asks only for a project and a start and end
date. Submitting a valid dialog SHALL create the act with status `Draft` and one line per entry of that
project whose start falls inside the period, in one save, and SHALL then open the act's review page;
the dialog's own fields SHALL be validated server-side and re-rendered inline when invalid.

#### Scenario: Valid submission creates and reviews

- **WHEN** a user submits a valid project and period that contains entries
- **THEN** the act and exactly one line per covered entry are stored, the totals are the sums over the lines, and the browser navigates to the review page addressed by the act id

#### Scenario: Invalid dialog fields

- **WHEN** a submitted project or date fails validation
- **THEN** the dialog is re-rendered with the submitted values and inline field errors, and nothing is stored

#### Scenario: Entries of other projects

- **WHEN** the period also contains entries of other projects
- **THEN** those entries are not listed and are not included

#### Scenario: Empty period

- **WHEN** a user submits a valid period that contains no entries of the project
- **THEN** no act is created and a notice says there is nothing to certify

### Requirement: The review page displays the act's lines

The review page of an act SHALL list its lines — date and time, duration, description and hourly rate —
with the totals, without any query validation, and SHALL NOT modify the underlying time entries:
editing a line there changes only the work-act line.

#### Scenario: Review lists the lines

- **WHEN** a user opens the review page of an act
- **THEN** each line is shown as a row with its stored values and the totals, and no time entry is changed

#### Scenario: Line edit touches only the line

- **WHEN** a user saves a changed row on the review page
- **THEN** only the work-act line is updated, the act totals are recomputed and stored, and the referenced time entry is unchanged

### Requirement: Draft act lines are editable

An act in status `Draft` SHALL offer an edit screen listing its lines; changing a line's duration, hourly
rate or description SHALL persist immediately, refreshing only that row and the recomputed totals. Lines
of a `Final` or `Invoiced` act SHALL NOT be editable.

#### Scenario: Persisted line edit

- **WHEN** a user saves a changed row on a Draft act's edit screen
- **THEN** only that line is updated, the act totals are recomputed and stored, and the row and totals re-render

#### Scenario: Frozen act rejects line edit

- **WHEN** a line edit is submitted for a Final act
- **THEN** nothing is stored and a notice says the act is final

### Requirement: Finalizing an act freezes it

A Draft act SHALL offer a Final action that sets its status to `Final`; a Final act SHALL have immutable
lines and SHALL NOT be deletable or editable.

#### Scenario: Draft finalized

- **WHEN** a user finalizes a Draft act
- **THEN** its status becomes Final, its lines can no longer be edited, and the refreshed view shows the frozen state

### Requirement: Invoiced status derives from the invoice link

An act referenced by an invoice line's work-act link SHALL be presented with status `Invoiced` and SHALL
be read-only; the act itself SHALL NOT gain a new link column.

#### Scenario: Final act referenced by an invoice

- **WHEN** an invoice line references the act
- **THEN** the list and detail show `Invoiced` instead of `Final`

### Requirement: A line shared with other acts is recognisable

On the review, edit and detail screens a line whose referenced entry also appears in the lines of other
acts SHALL be visually distinct and SHALL carry a tooltip naming those acts by project and period.

#### Scenario: Shared entry line

- **WHEN** a listed line's entry is referenced by at least one other act
- **THEN** the row is marked and its tooltip lists the other acts

#### Scenario: Sole act line

- **WHEN** a listed line's entry belongs to no other act
- **THEN** the row is rendered without the marker or tooltip

### Requirement: Work act list filters by project and orders by period end

The work-acts feature SHALL list acts ordered by period end descending, SHALL allow filtering by any number
of projects, and SHALL page and sort through the standard list-request mechanism.

#### Scenario: Default listing

- **WHEN** a user opens the work acts list
- **THEN** acts are shown newest period end first, each with its project, period, hours, amount, currency and status

#### Scenario: Filter by several projects

- **WHEN** a user selects more than one project in the filter
- **THEN** only acts of those projects are listed

### Requirement: Deleting a Draft act removes its lines

A Draft act SHALL be deletable behind a confirmation; deleting it SHALL remove the act and its lines and
SHALL leave the referenced time entries untouched. A Final or Invoiced act SHALL NOT be deletable.

#### Scenario: Draft act deleted

- **WHEN** a user confirms deletion of a Draft act
- **THEN** the act and its lines are gone, the entries they referenced still exist, and the list refreshes

#### Scenario: Final act not deletable

- **WHEN** a delete is requested for a Final act
- **THEN** nothing is removed and a notice says the act is final

#### Scenario: Cancel delete

- **WHEN** a user opens the delete confirmation and cancels
- **THEN** the act and its lines are unchanged

### Requirement: Act periods are stored as UTC instants and shown in the viewer's zone

An act's period SHALL be stored as UTC instants derived from the local date boundaries the user chose, and
SHALL be displayed converted back to the viewer's zone, so the covered entry set never depends on who
views the act.

#### Scenario: Boundary instant

- **WHEN** a user chooses a period ending on a local date
- **THEN** the stored end is the following local midnight converted to UTC, and an entry starting exactly then is not covered

#### Scenario: Viewer in another zone

- **WHEN** a user in a different time zone opens the act
- **THEN** the same lines are shown and the period is displayed in that viewer's local dates

### Requirement: Access requires the operational role

Every work-acts route SHALL require an authenticated user with the operational role.

#### Scenario: Unauthenticated or roleless

- **WHEN** an unauthenticated or roleless user requests a work-acts route
- **THEN** access is denied and the user is directed to sign in or to the pending-access page
