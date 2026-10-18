# Spec Delta

## Purpose

Searchable, paged entity lookups (projects, clients) served by `Apskaita.Shared` so every feature form
can point a picker at the same endpoints and slice instead of re-implementing them.

## ADDED Requirements

### Requirement: Shared lookup endpoints serve projects and clients

`Apskaita.Shared` SHALL expose GET search endpoints `projects` and `clients` and echo endpoints
`projects/selected` and `clients/selected` under a single `/lookup` route group, reading through
`IQuery<T>` only.

#### Scenario: Search projects

- **WHEN** a picker requests the projects lookup with a term
- **THEN** active projects whose name or code contains the term are offered, ordered by name, each with its `Name (Code)` label

#### Scenario: Search clients

- **WHEN** a picker requests the clients lookup with a term
- **THEN** active clients matching the term are offered with their display labels

#### Scenario: Echo a selected row

- **WHEN** a picker resolves a selected project or client id
- **THEN** the value and display label are returned as JSON, or not found when the row is inactive or unknown

#### Scenario: Paging

- **WHEN** more rows match than the page size
- **THEN** the fragment carries a load-more affordance that re-requests the same path and term for the next page

### Requirement: One generic items slice renders every lookup

The shared module SHALL render lookup results from a single slice over the lookup response contract;
entity-specific lookups SHALL NOT add their own result slice unless the item markup itself differs.

#### Scenario: Projects and clients share the slice

- **WHEN** either lookup returns results
- **THEN** the same slice renders the item buttons and the empty state ("No items found")

### Requirement: Lookup routes require the operational role

Every route under `/lookup` SHALL require an authenticated user with the operational role.

#### Scenario: Unauthenticated search

- **WHEN** an unauthenticated user requests a lookup search
- **THEN** access is denied and the user is directed to sign in
