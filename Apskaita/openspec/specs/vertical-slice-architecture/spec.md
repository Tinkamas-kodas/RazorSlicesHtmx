# Vertical Slice Architecture Specification

## Purpose

Defines how Apskaita is split into feature assemblies (vertical slices) with compile-time-enforced data ownership, so each feature owns its writes, publishes read-only access, and never reaches into another feature's data.

## Requirements

### Requirement: Feature assembly isolation

Each feature assembly SHALL reference only `Apskaita.Entities` and `Apskaita.Shared`, and SHALL NOT reference another feature assembly or the concrete `DbContext`.

#### Scenario: Feature references only shared contracts

- **WHEN** a feature assembly's project references are inspected
- **THEN** they include `Apskaita.Entities` and `Apskaita.Shared` only
- **AND** no other `Apskaita.Feature.*` assembly and no `ApskaitaDbContext` type is referenced

#### Scenario: Shared does not reach the data layer

- **WHEN** `Apskaita.Shared` is compiled
- **THEN** it references no `DbContext` and no feature `IStore`

### Requirement: Read access through a generic no-tracking query seam

Cross-feature reads SHALL go through the read-only contract `IQuery<T>` published in `Apskaita.Entities`, whose `All()` is no-tracking by contract, so tracked entities never cross a feature line.

#### Scenario: Reading another feature's data

- **WHEN** a feature needs to read data owned by another feature
- **THEN** it injects `IQuery<TEntity>` and composes LINQ server-side (`Where`/`OrderBy`/`Select`/`Skip`/`Take`)
- **AND** the query translates to a single SQL query and materializes no-tracking entities

#### Scenario: Tracked entity cannot cross the seam

- **WHEN** a consumer receives entities from `IQuery<T>.All()`
- **THEN** those entities are not tracked and cannot be persisted by another feature's `SaveChanges`

### Requirement: Write capability is internal to the owning feature

Each table's write capability SHALL be exposed only through an `internal` `IStore` (with mutable `DbSet`) declared inside the owning feature assembly, visible to the composition root via `InternalsVisibleTo`.

#### Scenario: Only the owner and composition can write

- **WHEN** code outside the owning feature assembly attempts to use its `IStore`
- **THEN** compilation fails because the interface is `internal`
- **AND** only `Apskaita.Web` (composition root) implements it via `InternalsVisibleTo`

### Requirement: Single data context and migration pipeline

The system SHALL use exactly one `DbContext`, one `Migrations` assembly/folder, and one `MigrateAsync` call site, with no per-feature contexts or migration histories.

#### Scenario: Applying schema

- **WHEN** migrations are applied
- **THEN** a single `MigrateAsync` runs against the one context
- **AND** the web host never migrates in-process, leaving test hosts unaffected

### Requirement: One owner per table

Every table SHALL have exactly one `IStore` owner, and there SHALL be no cross-feature write path; when a feature must mutate another feature's data, the owning feature hosts the endpoint under the required route group.

#### Scenario: Admin user management hosted by owner

- **WHEN** an administrative user-management action must write the `users` table
- **THEN** the owning feature (`Account`) hosts the endpoint, registered under the required route group
- **AND** no other feature writes that table
