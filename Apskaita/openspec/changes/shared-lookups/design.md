# Design

## Context

`Apskaita.Shared` is a Razor class library (Sdk.Razor + RazorSlices) that already hosts shared slices and
contracts and references only `Apskaita.Entities` + `RazorSlicesHtmx.AspNetCore` — no `DbContext`, no
feature stores. The library ships the lookup contracts (`LookupRequest`, `LookupItem`, `LookupResponse`
in `RazorSlicesHtmx.AspNetCore.Models`) and the Bootstrap `LookupSelect` helper that renders the
`rsh-lookup-select` web component; the component GETs `data-path` with `query`/`page`/`pageSize`/`path`
and resolves an initial value through `data-resolve-path` (`value` → `{value, text}` JSON).

Today the pattern is implemented twice at best and badly at worst: TimeTracking owns
`project-lookup`/`project-lookup-selected` plus a `Dictionary<Guid, string?>` slice, and the Admin project
form renders every client in a plain `<select>`.

## Goals / Non-Goals

**Goals:**
- One shared implementation of the lookup endpoints + items slice, usable by any form.
- Projects and clients covered; the time-tracking project lookup and the project form's client select
  replaced by it.
- Paged results through the library contract (the current feature-local lookup has no paging).

**Non-Goals:**
- No generic "lookup registry" abstraction — two concrete endpoint pairs, one shared slice.
- No invoice-template/unit lookups (their selects are small and static for now).
- No change to the color lookup (catalog-backed, not an entity query).

## Decisions

- **Shape.** `Apskaita.Shared/Lookups/`:
  - `LookupEndpoints.cs` — a static `MapLookupEndpoints(this IEndpointRouteBuilder)` registering a
    `/lookup` route group with `RequireAuthorization(AuthPolicies.Operational)` and four GETs:
    `projects`, `projects/selected`, `clients`, `clients/selected`. Each list endpoint takes
    `[AsParameters] LookupRequest`, calls `Normalize()`, filters `IQuery<T>` (active only), and returns
    `owner.Result.Fragment(_LookupItems.Create(request.ToResponse(items, hasMore)))`; each selected
    endpoint returns `Results.Ok(new { value, text })` or 404.
  - `_LookupItems.cshtml` — `RazorSlice<LookupResponse>`: the `rsh-lookup-item` buttons (value/text from
    `LookupItem`) plus a "load more" item when `HasMore`, re-getting `Path` with `Page=NextPage`. The
    empty state is entity-neutral ("No items found") — the slice has no per-entity wording; the
    placeholder text of the input carries the entity hint instead.
  - A shared `LookupItemFactory`-style private helper builds labels: project `Name (Code)`, client
    `Info.Name` (with `Info.CompanyCode` when set) — the same labels the pickers show today.
- **Not a feature module.** Shared has no nav item and no `IFeatureModule`; the routes are absolute
  (`/lookup/...`), not `FeatureUri`-relative, so any feature's form can point at them without owning the
  data. The read-only `IQuery<T>` access keeps the vertical-slice rule intact (Shared still references no
  `DbContext`).
- **Registration.** `Apskaita.Web` calls `app.MapLookupEndpoints()` once (next to the feature mapping);
  the group lives outside every feature prefix so `IFeatureUri`/active-feature resolution is unaffected.
- **Replacements.**
  - TimeTracking: delete `ProjectLookupSection` + `_ProjectLookupItems.cshtml`; `_TimeTrackingForm.cshtml`
    uses `path: "/lookup/projects"`, `selectedValueResolvePath: "/lookup/projects/selected"`.
  - Admin project form: `ClientId` becomes `LookupSelect` with
    `selectedValueTextFieldName: f.ClientLabel.Name`; `ProjectUpsertRequest` gains a `ClientLabel`
    property (bound + echoed), `FromEntity` fills it via the same label rule, and the `Clients` option
    list plus its `PopulateOptionsAsync` loading is dropped from the project form path. The validator
    keeps `ClientId NotNull` and gains an exists-check through `IQuery<Client>`.
- **Paging contract.** `hasMore` = `Skip(page*pageSize).Any()`-style probe limited by `Take(pageSize+1)`
  in one query (fetch `pageSize + 1`, trim) — no second round trip.

## Risks / Trade-offs

- [Shared grows an HTTP surface] → it is read-only, policy-protected, and mirrors what features already
  expose; the alternative (each feature re-implementing) is what we are removing.
- [Absolute `/lookup` paths bypass `FeatureUri` prefixing] → accepted: the group is app-wide by design;
  tests and forms use the literal path.
- [Client label rule duplicated between lookup and form echo] → one internal static helper in Shared is
  the single source; both call sites use it.
