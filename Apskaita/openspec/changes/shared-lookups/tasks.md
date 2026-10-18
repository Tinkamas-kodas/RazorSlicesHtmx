# Tasks

## 1. Shared lookup module

- [ ] 1.1 Add `Apskaita.Shared/Lookups/LookupEndpoints.cs` with `MapLookupEndpoints`: `/lookup` group, operational policy, GETs `projects`, `projects/selected`, `clients`, `clients/selected` over `IQuery<Project>`/`IQuery<Client>` using `LookupRequest`/`LookupResponse`, with shared label helpers (`Name (Code)` for projects, name + company code for clients)
- [ ] 1.2 Add `Apskaita.Shared/Lookups/_LookupItems.cshtml` — generic `RazorSlice<LookupResponse>`: item buttons, "No items found" empty state, load-more row when `HasMore`
- [ ] 1.3 Register `app.MapLookupEndpoints()` in the web host next to the feature mapping, and verify the host starts

## 2. Replace the time-tracking project lookup

- [ ] 2.1 Delete `ProjectLookupSection` (`TimeTrackingEndpoints.Lookups.cs`) and `Slices/_ProjectLookupItems.cshtml`, and repoint `_TimeTrackingForm.cshtml` to `/lookup/projects` (+ `/selected`)
- [ ] 2.2 Move the lookup assertions into shared-lookup tests and verify the entry dialog still searches and echoes projects

## 3. Replace the project form's client select

- [ ] 3.1 Add `ClientLabel` to `ProjectUpsertRequest` (bound, echoed in `FromEntity`), drop the `Clients` option list and its population from the project form path, and switch the field to `LookupSelect` on `/lookup/clients`
- [ ] 3.2 Add a client-exists rule to `ProjectUpsertRequestValidator` through `IQuery<Client>`, and verify create/edit still reject an unknown client

## 4. Tests and docs

- [ ] 4.1 Add endpoint tests: project search (name/code match, active-only, order), client search, both echoes (found + inactive/unknown → 404), paging load-more, anonymous → login redirect
- [ ] 4.2 Update `Apskaita/README.md` (lookup section: shared routes, which pickers use them) and verify documented routes match the code

## Workflow follow-up

- Archive the change after review, syncing the `shared-lookups` delta into `openspec/specs/` and the
  `time-tracking` modification into the main spec.
