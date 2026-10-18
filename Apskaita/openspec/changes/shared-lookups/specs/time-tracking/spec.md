# Spec Delta

## MODIFIED Requirements

### Requirement: Project lookup for the dialog

The entry dialog's project picker SHALL be served by the shared lookup module (`/lookup/projects` and
`/lookup/projects/selected`); the time-tracking feature SHALL NOT expose its own project lookup routes
or result slice.

#### Scenario: Search projects

- **WHEN** a user searches the project picker
- **THEN** active projects whose name or code match are offered by the shared lookup

#### Scenario: Echo a selected project

- **WHEN** a selected project id is resolved
- **THEN** its display label is returned by the shared lookup, or not found when it is inactive or unknown
