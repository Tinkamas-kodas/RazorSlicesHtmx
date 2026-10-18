# Proposal

## Why

Lookups are duplicated per feature: the time-tracking feature owns a project lookup (two endpoints plus a
result slice) even though the project data belongs to Admin, and the project form still picks a client
from a plain `<select>` fed by loading every client into the page. Every feature that needs a picker
would copy the same pattern again. A shared lookup module in `Apskaita.Shared` — one slice and one
endpoint pair per entity kind — removes the duplication and gives the client picker the same searchable,
paged behavior the project picker already has.

## What Changes

- Add a **shared lookups** module to `Apskaita.Shared`: a generic lookup items slice and GET endpoints
  under `/lookup/...` for projects and clients — a search endpoint (`query` + paging, returns the items
  fragment) and a selected-echo endpoint (`value` → `{value, text}` JSON), built on the library's
  `LookupRequest`/`LookupItem`/`LookupResponse` contracts and reading through `IQuery<T>`.
- **Replace** the time-tracking feature's project lookup: delete `ProjectLookupSection` and
  `_ProjectLookupItems.cshtml`; the entry dialog's `LookupSelect` points at the shared routes.
- **Replace** the project form's client `<select>` with the shared client `LookupSelect` (adds a
  `ClientLabel` echo field to `ProjectUpsertRequest`; the form no longer loads all clients).
- The shared routes require the operational policy; behavior (labels, active-only filtering, echo
  contract) matches what the per-feature lookups did.

## Capabilities

### New Capabilities
- `shared-lookups`: searchable, paged lookup endpoints and the result slice for entity pickers, hosted
  by `Apskaita.Shared` and readable by any feature's forms.

### Modified Capabilities
- `time-tracking`: the project lookup is served by the shared module instead of a feature-local route.

## Impact

- **`Apskaita.Shared`**: new `Lookups/` folder (endpoints + `_LookupItems.cshtml` slice); Shared stays
  free of `DbContext` and feature stores (reads via `IQuery<T>` only), so the architecture rule holds.
- **`Apskaita.Web`**: one `MapLookupEndpoints(app)` call registers the `/lookup` group.
- **`Apskaita.Feature.TimeTracking`**: `Endpoints/TimeTrackingEndpoints.Lookups.cs` and
  `Slices/_ProjectLookupItems.cshtml` deleted; `_TimeTrackingForm.cshtml` paths repointed.
- **`Apskaita.Feature.Admin`**: `_ProjectForm.cshtml` client select → `LookupSelect`;
  `ProjectUpsertRequest` gains `ClientLabel`; the `Clients` option list and its population are dropped
  from the project form path.
- **Out of scope**: invoice-template and unit pickers (stay selects for now), the project color lookup
  (catalog-backed, not an entity), and lookup caching.
