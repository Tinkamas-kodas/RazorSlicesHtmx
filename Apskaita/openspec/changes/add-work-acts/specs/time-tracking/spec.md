# Spec Delta

## MODIFIED Requirements

### Requirement: Editing an entry

Activating a rendered entry card, by pointer or keyboard, SHALL open an edit dialog prefilled with that
entry's current values; a valid save updates the entry, refreshes only the grid, and shows a success toast,
while an invalid save keeps the dialog open with the submitted values. An entry referenced by a work act
line SHALL NOT be editable: the save is rejected and the notice names the act(s) the entry belongs to.

#### Scenario: Open and save

- **WHEN** a user activates an entry card and submits a valid edit
- **THEN** the entry is updated, only the grid refreshes, and a success toast appears

#### Scenario: Invalid edit

- **WHEN** a user submits an edit that fails validation
- **THEN** the dialog stays open and all submitted values are preserved

#### Scenario: Entry in work acts

- **WHEN** a user submits an edit for an entry referenced by one or more work act lines
- **THEN** nothing is stored and the notice names the act(s) the entry belongs to

### Requirement: Deleting an entry with confirmation

The edit dialog's delete action SHALL replace the dialog with a confirmation; confirming removes the entry
and refreshes only the grid, and cancelling returns to the edit dialog without changes. An entry referenced
by a work act line SHALL NOT be deletable.

#### Scenario: Confirm delete

- **WHEN** a user confirms deletion
- **THEN** the entry is removed, the dialog closes, and only the grid refreshes

#### Scenario: Cancel delete

- **WHEN** a user opens the delete confirmation and cancels
- **THEN** the edit dialog is shown again and no data changes

#### Scenario: Entry in work acts

- **WHEN** a delete is confirmed for an entry referenced by one or more work act lines
- **THEN** the entry is kept and the notice names the act(s) it belongs to

### Requirement: Moving an entry by drag

The system SHALL move an existing entry to a new date and time supplied by a drag action, updating its
start and refreshing the grid, and SHALL reject the move when the entry is referenced by a work act line.

#### Scenario: Valid move

- **WHEN** a user drags an entry to a new slot
- **THEN** the entry's start is updated to the new display-zone time and the grid refreshes

#### Scenario: Invalid move target

- **WHEN** a move request carries an unparseable date or time
- **THEN** the request is rejected and the entry is unchanged

#### Scenario: Entry in work acts

- **WHEN** a move targets an entry referenced by one or more work act lines
- **THEN** the entry's start is unchanged and the notice names the act(s) it belongs to

## ADDED Requirements

### Requirement: Entries in work acts are recognisable in the grid

An entry referenced by at least one work act line SHALL be visually distinguishable in the week grid, and
activating it SHALL present its current values without offering save or delete.

#### Scenario: Locked entry rendered

- **WHEN** the grid renders an entry that some work act line references
- **THEN** the card is marked as locked and its dialog offers no save or delete action
