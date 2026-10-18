# AGENTS.md — Apskaita

Apskaita is a .NET 10 accounting/time-tracking app built on the RazorSlicesHtmx library that lives
in the parent folder. Library conventions are in the repo-root `AGENTS.md`; this file covers
Apskaita-specific rules.

## Architecture (vertical slices)

Canonical spec: [`openspec/specs/vertical-slice-architecture/spec.md`](openspec/specs/vertical-slice-architecture/spec.md).

- A feature assembly references only `Apskaita.Entities` and `Apskaita.Shared` — never another feature assembly or the concrete `DbContext`.
- Reads go through `IQuery<T>` (no-tracking; compose LINQ server-side; use `ExistsAsync` for existence checks). Writes go through the owning feature's `internal IStore` only.
- No `ContentService` classes — business logic lives in the feature's endpoints and services.
- One `DbContext`, one migration pipeline; every table has exactly one `IStore` owner.

## Validation

- All field validation is backend-only via FluentValidation validators — never HTML5/frontend validation attributes, never hand-rolled checks in endpoints.
- Existence/uniqueness checks run through `IQuery<T>` inside validators.
- Dialog forms re-render with inline field errors (`is-invalid` + `invalid-feedback` keyed by field name); toasts are for non-field messages only.

## Dates and time zones

- UI display and GET/POST binding use local wall-clock dates.
- The DB stores naive UTC and all comparisons happen in UTC — convert with `BrowserTimeZone` helpers (tz-offset cookie).
- A period end is exclusive: next local midnight.

## HTMX flow conventions

- Multi-step flows are pages, not dialogs. After a successful create, navigate with `WithLocation` to a GET page; the GET display endpoint performs no validation.
- Form field names must match endpoint binding parameter names (`[AsParameters]` models) — mismatches cause `BadHttpRequestException`.
- Toast-only refusals from page-hosted forms need `.WithRetarget("#dialog-host")`.

## Quality gates

- After C# edits, run the ReSharper inspect tool and address findings before reporting done. EF Core-generated-code inspections are intentionally suppressed.
- Build: `dotnet build Apskaita.slnx`. Tests: `dotnet test` (xUnit, endpoint tests via `ApskaitaApiFactory` + InMemory, `[Collection("Integration")]`).

## Change workflow

Feature work is tracked with OpenSpec changes under `openspec/changes/<name>/` (propose → apply → archive);
validate with `openspec validate <name> --strict`.
