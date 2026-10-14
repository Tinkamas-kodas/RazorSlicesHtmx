# Architecture: Vertical Slices with a Shared Data Core

Status: IMPLEMENTED (stages 0–8 complete, 2026-10-14) — see progress log below
Date: 2026-10-13 · refreshed 2026-10-14
Owner: Apskaita

## 1. Goal and non-goals

**Goal.** Split Apskaita into feature assemblies (vertical slices) with compile-time-enforced
ownership: each feature owns its write access, publishes read contracts, and never reaches into
another feature's data. UI, services, and slices live per feature.

**Non-goals (for now).**
- No per-feature `DbContext` and no per-feature migration histories — one context, one pipeline
  (single deploy → independent-deployability payoff does not exist yet).
- No CQRS bus, no repository/specification framework, no DKNet adoption.
- No microservices, no separate databases.

**The one rule that makes it work.** A feature touches other features' data only through the
read-only generic contract `IQuery<T>` published in `Apskaita.Entities` — inject
`IQuery<Project>`, `IQuery<User>`, ... and compose LINQ directly against the entity. Write
capability (`IStore` with `DbSet`) is declared *inside* the owning feature assembly and is
`internal` — only the feature itself and the composition layer (`Apskaita.Web`) can use it.

## 2. Dependency direction

```mermaid
graph BT
    E[Apskaita.Entities<br/>POCOs + IQuery&lt;T&gt; + read catalogs]
    S[Apskaita.Shared<br/>auth, connectors, infra]
    FA[Apskaita.Feature.Account<br/>internal IAccountStore]
    FD[Apskaita.Feature.Admin<br/>internal IAdminStore]
    FT[Apskaita.Feature.TimeTracking<br/>internal ITimeTrackingStore]
    W[Apskaita.Web<br/>layout + DbContext + migrations + composition]
    E
    S --> E
    FA --> E
    FA --> S
    FD --> E
    FD --> S
    FT --> E
    FT --> S
    W --> FA
    W --> FD
    W --> FT
    W --> S
    W --> E
```

- Features reference `Entities` + `Shared` only — never each other, never the concrete `DbContext`.
- `Shared` references `Entities` (or nothing) — never a `DbContext`, never an `IStore`.
- `Web` references everything: it is the composition root that implements the private stores.

## 3. Target project layout with class names

### 3.1 `Apskaita.Entities` — POCOs + shared read contracts (no `DbContext`)

```
Apskaita.Entities/
  Entities/
    BaseEntity.cs            (existing)
    User.cs                  (existing)
    Company.cs, CompanyInfo.cs, Client.cs, Issuer.cs, Unit.cs, Project.cs,
    InvoiceTemplate.cs, Invoice.cs, InvoiceLine.cs, InvoiceSequenceState.cs,
    WorkAct.cs, WorkActLine.cs, SystemSettings.cs, TimeTracking.cs,
    CalendarConnector.cs, CalendarEventReference.cs   (existing)
    FieldLengths.cs          (existing)
  Contracts/                 (NEW)
    IQuery.cs                → public interface IQuery<TEntity> where T : class
                              { IQueryable<TEntity> All(); }   // no-tracking by contract
  ── REMOVED from this project (see 3.6): ApskaitaDbContext.cs, Mapping/*, DataAccessServiceCollectionExtensions.cs
```

Notes:
- No named catalog interfaces and no DTO records. Cross-feature reads inject `IQuery<TEntity>`
  directly and compose LINQ against the entity (`IQuery<Project>`, `IQuery<User>`, ...). One
  generic contract, zero per-feature plumbing types.
- Accepted trade-offs: entity shape is visible across feature lines (schema refactors ripple
  through consumers — compile-safe in one repo), and `IQuery<User>` exposes every column
  including the password hash. If that ever becomes unacceptable, add a DTO catalog for that
  one table — nothing else changes.

### 3.2 `Apskaita.Shared` — infrastructure, provider connectors (NEW project)

Absorbs `Apskaita.Shared` and `Apskaita.Web/Shared/*`, `Apskaita.Web/Logging/*`,
`Apskaita.Web/Features/Account/Authentication/*`.

```
Apskaita.Shared/
  Routing/
    FeatureUri.cs            → IFeatureUri, HttpFeatureUri        (from Web.Shared/Routing)
  Auth/
    AuthPolicies.cs          (from Web.Shared)
    AccountCookieEvents.cs   (from Web/Features/Account/Authentication)
    EntraIdConnectSetup.cs   (OIDC provider config helpers, from Web/Setup.cs where extractable)
  Calendar/
    ICalendarProviderConnector.cs, CalendarProviderInfo.cs          (existing, from Web/Shared/Calendar)
    CalendarConnectorRegistry.cs                                     (existing)
    Outlook365Connector.cs                                           (existing; now depends on
                                                                       ICalendarConnectorTokenStore, not the store class)
    CalendarEventReader.cs   → ICalendarEventReader + reader         (existing)
    ConnectorAuthOptions.cs                                          (existing)
    ConnectorTokenProtector.cs                                       (existing)
    ICalendarConnectorTokenStore.cs   (NEW abstraction — Shared defines, Feature.Account implements:
                                       GetTokensAsync/SaveTokensAsync/ClearAsync by connector id)
  Ui/
    AdminNavItem.cs          (from Web.Shared)
    ProjectColorCatalog.cs   (from Web.Shared/ProjectColors)
  Web/
    BrowserTimeZone.cs       (from Web/Shared)
    RequestLoggingMiddleware.cs   (from Web/Logging)
    ExternalCallLoggingHandler.cs (from Web/Logging)
  SharedServicesCollectionExtensions.cs   (NEW: services.AddApskaitaShared(...))
```

`Shared` must not reference `ApskaitaDbContext` or any `IStore`. It may reference `Entities`
POCOs/records (e.g. `CalendarEventReference`).

### 3.3 `Apskaita.Feature.Account` (NEW project; absorbs `Apskaita.Web/Features/Account`)

```
Apskaita.Feature.Account/
  Persistence/
    IAccountStore.cs         → internal interface (write capability, Account-only):
                                { DbSet<User> Users { get; } DbSet<CalendarConnector> Connectors { get; } }
                                [InternalsVisibleTo("Apskaita.Web")] on the assembly.
    AccountConnectorTokenStore.cs → internal, implements Shared.ICalendarConnectorTokenStore over IAccountStore
  Endpoints/
    AccountEndpoints.cs      (moved from Web; registered under /account)
    UsersEndpoints.cs        (MOVED from Admin's UserRegistry — Account owns the users table, so
                              Account hosts the admin user-management UI; registered under the
                              /admin/users route group; nav placement under the Admin group is a
                              NavigationItem concern, not an assembly concern)
    ConnectorEndpoints.cs    (connector connect/disconnect/read-events actions, moved with the feature)
  Services/
    AccountProvisioner.cs    (existing)
    AccountContentService.cs (existing; absorbs connector page-model creation from Web)
    CalendarConnectorStore.cs (MOVED from Web/Shared/Calendar — now uses IAccountStore, not DbContext)
  Models/                    (existing Account + connector view models)
  Slices/                    (_Page.cshtml, _LoginPage.cshtml, _PendingPage.cshtml,
                             _ConnectorsTable.cshtml, connector dialogs)
  Validators/                (if any)
  FeatureAccountServices.cs  (NEW: public static IServiceCollection AddFeatureAccount(this IServiceCollection)
                              — registers internal impls: ICalendarConnectorTokenStore; the
                                IAccountStore→context adapter is registered by Web)
```

### 3.4 `Apskaita.Feature.Admin` (rename/move of `Apskaita.Feature.Admin`)

```
Apskaita.Feature.Admin/
  Persistence/
    IAdminStore.cs           → internal interface:
                                { DbSet<Client> Clients, DbSet<Issuer> Issuers, DbSet<Unit> Units,
                                  DbSet<Project> Projects, DbSet<InvoiceTemplate> InvoiceTemplates,
                                  DbSet<Company> Companies, DbSet<SystemSettings> SystemSettings,
                                  DbSet<Invoice> Invoices, DbSet<InvoiceLine> InvoiceLines,
                                  DbSet<InvoiceSequenceState> InvoiceSequenceStates,
                                  DbSet<WorkAct> WorkActs, DbSet<WorkActLine> WorkActLines }
                                (invoices/work-acts stay Admin-owned until a Feature.Invoicing exists)
  ClientsRegistry/  IssuerRegistry/  UnitRegistry/  ProjectRegistry/
  InvoiceTemplateRegistry/  SystemConfig/   (existing folder layout kept:
    Endpoints/ Models/ Services/ Slices/ Validators/)
    ClientsEndpoints.cs, IssuersEndpoints.cs, UnitsEndpoints.cs, ProjectsEndpoints.cs,
    InvoiceTemplatesEndpoints.cs, SystemSettingsEndpoints.cs   → switched to IAdminStore
    (UserRegistry/ folder is REMOVED — UsersEndpoints moves to Feature.Account, see 3.3)
  Common/                    (existing Models/Slices)
  FeatureAdminServices.cs    (NEW: AddFeatureAdmin(...) — no catalog impls needed; IQuery<T> comes from Web)
```

### 3.5 `Apskaita.Feature.TimeTracking` (NEW project; absorbs `Apskaita.Web/Features/TimeTracking`)

```
Apskaita.Feature.TimeTracking/
  Persistence/
    ITimeTrackingStore.cs    → internal interface: { DbSet<TimeTracking> Entries { get; } }
  Endpoints/
    TimeTrackingEndpoints.cs (moved; project-lookup endpoints switch from db.Projects to IQuery<Project>,
                              user display names from IQuery<User>)
  Services/
    TimeTrackingContentService.cs (moved)
  Models/                    (moved: row models, search/query records, upsert requests)
  Slices/                    (moved: _ListPage, _Grid, _CreateDialog, _EditDialog, _DeleteConfirm, lookup slices)
  Validators/                (moved: TimeTrackingModelValidator)
  FeatureTimeTrackingServices.cs (NEW: AddFeatureTimeTracking(...))
```

### 3.6 `Apskaita.Web` — host, layout, composition root, data

```
Apskaita.Web/
  Program.cs, Setup.cs       (Setup.cs shrinks: pipeline + auth config + wiring calls)
  Data/
    ApskaitaDbContext.cs     (MOVED from Entities; implements nothing — plain context, IDataProtectionKeyContext kept)
    ApskaitaDbContextFactory.cs (MOVED from Apskaita.Data.Migration — design-time for `dotnet ef` against Web)
    Mapping/                 (MOVED from Entities: ApskaitaEntityModelBuilderExtensions.cs, MySqlEnumTypeExtensions.cs)
    Migrations/              (MOVED from Apskaita.Data.Migration; namespace updated)
    DataAccessCollectionExtensions.cs (NEW/absorbed: AddApskaitaData(...) — AddDbContext, MigrateAsync-at-startup helper)
  Composition/               (implements the features' internal stores; needs InternalsVisibleTo grants)
    AccountStore.cs          → sealed class AccountStore(ApskaitaDbContext db) : Feature.Account.IAccountStore
                                { public DbSet<User> Users => db.Set<User>(); public DbSet<CalendarConnector> Connectors => db.Set<CalendarConnector>(); }
    AdminStore.cs            → : Feature.Admin.IAdminStore (db.Set<T>() per table)
    TimeTrackingStore.cs     → : Feature.TimeTracking.ITimeTrackingStore
    QueryImpl.cs             → sealed class Query<TEntity>(ApskaitaDbContext db) : IQuery<TEntity>
                                { public IQueryable<TEntity> All() => db.Set<TEntity>().AsNoTracking(); }
    CompositionServices.cs   → AddScoped<IAccountStore, AccountStore>(), AddScoped<IAdminStore, AdminStore>(),
                                AddScoped<ITimeTrackingStore, TimeTrackingStore>(),
                                AddScoped(typeof(IQuery<>), typeof(Query<>))
  Layout/                    (stays: _Layout.cshtml, _SideNav.cshtml, AppFeaturePageRenderer.cs, Models/)
  wwwroot/                   (stays: css/site.css, js/*, icons/)
```

**Retired projects:** `Apskaita.Data.Migration` (absorbed into `Web/Data`), `Apskaita.Shared`
(merged into `Apskaita.Shared`). `Apskaita.Entities` loses `ApskaitaDbContext`, `Mapping/`, and
`DataAccessServiceCollectionExtensions`.

## 4. Table ownership map

| Table | Owner (`IStore`) | Cross-feature read surface |
|---|---|---|
| `users` | Account (also hosts the `/admin/users` UI) | `IQuery<User>` |
| `calendar_connectors` | Account | none (Account UI only) |
| `clients`, `issuers`, `units`, `projects`, `invoice_templates`, `companies`, `system_settings` | Admin | `IQuery<T>` per table as needed |
| `invoices`, `invoice_lines`, `invoice_sequence_states`, `work_acts`, `work_act_lines` | Admin (until `Feature.Invoicing`) | none yet |
| `time_trackings` | TimeTracking | none yet |
| `data_protection_keys` | Web host (infrastructure) | n/a |

## 5. Contract rules

1. `IQuery<T>.All()` is **no-tracking by contract** (`AsNoTracking()` in `Query<T>`). Tracked
   entities must never cross a feature line — with a shared context, a tracked entity mutated by a
   consumer would be silently persisted by the owner's next `SaveChanges`.
2. Owner-facing `IStore` exposes mutable `DbSet` (tracked, writes allowed) and is `internal` to the
   feature assembly + `[InternalsVisibleTo("Apskaita.Web")]` — misuse is a compile error, not a
   review finding.
3. Cross-feature reads compose server-side (`Where`/`OrderBy`/`Select`/`Skip`/`Take`) over
   injected `IQuery<TEntity>` so `ListRequest` paging/sort and lookup endpoints keep translating
   to one SQL query.
4. There is no cross-feature write path. If a feature needs to mutate another feature's data
   (e.g. admin user management), the *owning* feature hosts the endpoint and registers it under
   the required route group — writes never cross an assembly line.
5. `Shared` never references `Entities`' `DbContext`-related code or any `IStore`.
6. Every table has exactly one `IStore` owner.

## 6. DI wiring (end state, `Setup.cs`)

```csharp
services.AddApskaitaData();                    // Web/Data: DbContext + Query<T> + store adapters
services.AddApskaitaShared();                  // Shared: connectors, logging, FeatureUri, policies
services.AddFeatureAccount();                  // token store (+ UsersEndpoints under /admin/users)
services.AddFeatureAdmin();                    // registry content services
services.AddFeatureTimeTracking();
services.AddFeature<TimeTrackingEndpoints>("/time-tracking");   // existing AddFeature<T> pattern
services.AddFeature<AccountEndpoints>("/account");
services.AddFeature<ClientsEndpoints>("/admin/clients");
// ... existing registrations unchanged
```

## 7. Staged implementation

Each stage ends green: `dotnet build Apskaita/Apskaita.slnx` + `dotnet test --project tests/Apskaita.Tests/Apskaita.Tests.csproj` (107 tests) + app boots.

| Stage | Work | Validation | Status |
|---|---|---|---|
| **0. Baseline** | Current tree builds/tests green (done 2026-10-13: 107/107) | — | ✅ done |
| **8. Test isolation** (added) | Serialize `ApskaitaApiFactory`-based classes via `[Collection("Integration")]` — fixes parallel-host flakiness | repeated green runs | ✅ done (`45fca63`) |
| **1. Contracts** | Add `Contracts/IQuery.cs` to `Apskaita.Entities` + `Query<T>` impl and open-generic registration in Web. No behavior change. | build + tests | ✅ done (`5191bf5`) |
| **2. `Apskaita.Shared`** | New project; move `Web.Shared` (FeatureUri, AuthPolicies, AdminNavItem, ProjectColorCatalog), `Web/Shared/*` (Calendar/*, BrowserTimeZone), `Web/Logging/*`, `AccountCookieEvents`. Add `ICalendarConnectorTokenStore`; refactor `Outlook365Connector` to depend on it (keep `CalendarConnectorStore` temporarily in Web as a stop-gap implementation). Delete `Apskaita.Shared`. | build + tests + connector smoke | ✅ done (`3c203b4`) |
| **3. Account pilot** | New `Apskaita.Feature.Account`; move `Web/Features/Account/*` + `CalendarConnectorStore` + Admin's `UsersEndpoints` (re-registered under `/admin/users`); declare internal `IAccountStore` + IVT; `AccountStore` adapter + `Query<T>` in Web/Composition; implement `AccountConnectorTokenStore`. Web registers everything. | build + tests + `/account` and `/admin/users` smoke | ✅ done (`b6a4470`; Users completed in 4c) |
| **4c. Users → Account** (added) | Move `Admin/Common` → `Apskaita.Shared`; relocate `UserRegistry/{Endpoints,Models,Services,Slices}` + `UsersEndpoints` → `Apskaita.Feature.Account` on `IAccountStore.Users`; dissolve `UsersContentService` per converged pattern; convert Users test to endpoint-level | build + tests | ✅ done (`0abd901` Common→Shared, `a58c499` Users→Account) |
| **4. Admin** | Rename `Apskaita.Feature.Admin` → `Apskaita.Feature.Admin`; declare internal `IAdminStore`; switch all registry endpoints/services from `ApskaitaDbContext` to `IAdminStore`. | build + tests + admin pages smoke | ✅ done (`104f9e5`, `5b8ab2d`; replication `ef201b6`…`6f8bbd7`; `UserRegistry` moved out in 4c) |
| **5. TimeTracking** | New `Apskaita.Feature.TimeTracking`; move feature; internal `ITimeTrackingStore`; switch lookups to `IQuery<Project>`/`IQuery<User>`. | build + tests + grid/dialog smoke | ✅ done (`c11cfe8`) |
| **6. Data move** | Move `ApskaitaDbContext`, `Mapping/`, `Migrations/`, design-time factory into `Web/Data`; retire `Apskaita.Data.Migration`; strip `Entities` to POCOs+contracts; regenerate migrations if namespace churn requires. | build + tests + fresh-DB `MigrateAsync` run | ✅ done (`b70aefa`) — fresh-DB run pending (see caveats) |
| **7. Hardening (optional)** | Remove the four `DisableAntiforgery()` calls in TimeTracking endpoints (header hook fixed 2026-10-13); clean recursive `artifacts/build-temp` output loops in Web + Admin. | tests + POST smoke | ✅ done (`3189339` antiforgery, `68f26ef` output guard) |

Stage 3 is the pilot: if the pattern proves out on Account, stages 4–5 are mechanical.

### Progress log (2026-10-14, HEAD `45fca63`)

**Pattern converged during implementation** (applies to all registry dissolutions, now the target
shape for 4c/5 as well):

- GET endpoints read via `IQuery<T>` (no-tracking); POST endpoints write via the feature's
  `IStore` (tracked load → mutate → `SaveChangesAsync`). `ContentService` classes are fully
  dissolved — no service layer between endpoints and the seams.
- Pure request behavior lives on the DTO partial (`Normalize()`, static `CreateDefault()`,
  static `FromEntity(entity)`, `ToCompanyInfo()` where applicable); single-use mapping is
  inlined into the endpoint; no `{Feature}Forms` helper class — shared helpers are private
  statics on the endpoints class.
- Registry tests moved to endpoint level (`ApskaitaApiFactory` + antiforgery-token scrape +
  `HX-Request`). Integration classes are serialized via `[Collection("Integration")]`.

**Commits**: Stage 1 `5191bf5` · Stage 2 `3c203b4` · Stage 3 pilot `b6a4470` · Stage 4a rename
`104f9e5` · Stage 4b `IAdminStore` + 6 registries `5b8ab2d` · Project nav props + FK migration
`90f3ea0` · Projects dissolution `9f674c3` / `0258af1` / `5ee0d55` / `787d106` · replication
Clients `ef201b6`, Issuers `04f8e6f`, Units `2c2b0c6`, InvoiceTemplates `b1afc14`, SystemConfig
`6f8bbd7` · naming `7af968d`/`0abeac3` · test isolation `45fca63`.

**Caveats — both closed (2026-10-14):**

- Migration `AddProjectClientTemplateForeignKeys` is applied to live MySQL —
  `dotnet run --project src/Apskaita.Web -- --migrate` now reports `Migrations applied.` with no
  FK violations, so the schema is up-to-date.
- `/admin/projects/list` nav-projection SQL is runtime-smoked by `ProjectsListTests`
  (endpoint level, `ApskaitaApiFactory` + `[Collection("Integration")]`): the full page renders the
  joined `ClientName`/`TemplateName` columns, an HTMX GET returns the fragment without the page
  shell, and `?Search=` filters the projected rows server-side.

**Next in order**: none — all stages implemented (see completion log below).

### Completion log (2026-10-14, HEAD `68f26ef`)

- **4c** (`0abd901`, `a58c499`): status-toggle `Common` moved to `Apskaita.Shared/Ui/Common`
  (Shared converted to `Sdk.Razor` + `RazorSlices`); `UserRegistry` moved to
  `Apskaita.Feature.Account` and `UsersContentService` dissolved — GET via `IQuery<User>`,
  POST via `IAccountStore` (tracked toggle / set-role); Users toggle test converted to
  endpoint level.
- **5** (`c11cfe8`): `Apskaita.Feature.TimeTracking` extracted; `TimeTrackingContentService`
  dissolved — GET via `IQuery<TimeTracking>`/`IQuery<Project>`, POST via `ITimeTrackingStore`;
  pure dialog behavior moved onto the `TimeTrackingModel` partial (`CreateDefault`,
  `FromRequest`, `CreateFromEvent`, `CreateOccurrences`, `CreateSavedMessage`); the
  calendar-event ownership check now crosses the feature line through the public
  `ICalendarEventReferenceResolver` seam (Shared, implemented by Account's
  `CalendarConnectorStore`).
- **6** (`b70aefa`): `ApskaitaDbContext` + `Mapping/` + `Migrations/` + design-time factory
  moved into `Apskaita.Web/Data/` (namespaces kept); `MigrationsAssembly` retargeted to
  `Apskaita.Web`; `Apskaita.Data.Migration` retired — migrations are applied with
  `dotnet run --project src/Apskaita.Web -- --migrate [--connection=...]` (single
  `MigrateAsync` call site in `MigrationRunner`; the web host never migrates, so in-process
  test hosts are unaffected); `Apskaita.Entities` stripped to POCOs + `Contracts/` with zero
  EF references — features can no longer even see the context.
- **7** (`3189339`, `68f26ef`): the four `DisableAntiforgery()` calls removed (time-tracking
  POSTs now validated through the htmx antiforgery header hook; tests scrape the token via the
  shared `TestAntiforgery` helper); recursive `artifacts/build-temp` output trees deleted and an
  `Apskaita/Directory.Build.props` guard target removes nested output copies before every build.

**Acceptance criteria — verified mechanically**: zero `ApskaitaDbContext` references in the three
feature assemblies (and in `Apskaita.Shared` code); all three `IStore`s are `internal` with
`InternalsVisibleTo("Apskaita.Web")` + `("Apskaita.Tests")`; cross-feature reads go through
`IQuery<T>`; `Apskaita.Data.Migration` and `Apskaita.Shared` are gone; solution builds
0 warnings / 0 errors and the suite is 107/107 (exe) / 126 passed (VS Code runner).

### Follow-ups after plan completion (2026-10-14, HEAD `851231b`)

TimeTracking slice hygiene continued on top of the completed plan — no stage-0–8 rework, all
changes stay inside the seams the plan defines:

- `IQuery<T>.ExistsAsync(Expression<Func<TEntity,bool>>, ct)` added to the read seam (server-side
  `SELECT 1`, no materialization) and `IQuery<T>` made invariant — a predicate is an input position
  (`b1e07e7`).
- The TimeTracking validator's project-existence rule uses that seam instead of EF `AnyAsync`, so
  the feature needs no provider dependency (`4bf9cfd`).
- `TimeTrackingEndpoints` split into a `partial` module shell plus one nested section per file
  (Week / Entries / CalendarEvents / Lookups) — same routes, same behavior (`542dc14`).
- Pure grid geometry extracted into `Models/` (`TimeTrackingGridGeometry`, `WeekOf`, `WorkSlots`,
  `EntryViewModel.From`, `CalendarEventViewModel.From`, `TimeTrackingEntryRow`) with 13 unit tests
  (`1e10ca4`).
- `BaseFeatureModule` gained `ResultBuilder` + `MapSubgroup` so section collaborators compose
  inside one module and share one result facade (`851231b`).
- `ProjectsListTests` closes the projects-list runtime-smoke caveat (above); the exe suite is now
  124/124, still 0 warnings / 0 errors.

## 8. Project-reference gotchas (verified in this repo)

- Feature classlibs with `.cshtml` slices need `FrameworkReference Microsoft.AspNetCore.App`,
  `RazorSlices` package, and the generator project-ref with
  `OutputItemType="Analyzer" ReferenceOutputAssembly="false" PrivateAssets="all"`.
- Static web assets from classlibs serve under `/_content/<AssemblyName>/...` (Admin already relies on this).
- `Directory.Packages.props` central management applies to all new projects; keep versions out of csproj.
- `dotnet ef` commands target `Apskaita.Web` after stage 6 (`--project src/Apskaita.Web`).
- Tests keep referencing `Apskaita.Web` (WebApplicationFactory); run via
  `dotnet test --project tests/Apskaita.Tests/Apskaita.Tests.csproj` from `Apskaita/`.

## 9. Acceptance criteria

1. No feature assembly references `ApskaitaDbContext`, another feature, or any EF write API
   (`DbSet`, `Add`/`Update`/`Remove`) outside its own `Persistence/` folder.
2. `IAccountStore`, `IAdminStore`, `ITimeTrackingStore` are `internal`; only `Apskaita.Web`
   (via `InternalsVisibleTo`) implements them.
3. All cross-feature data access goes through injected `IQuery<T>`; `IQuery<T>.All()` is
   no-tracking; no feature writes another feature's tables.
4. One `DbContext`, one `Migrations` folder, one `MigrateAsync` at startup; zero behavior change
   to routes, HTMX responses, or the 107 tests.
5. `Apskaita.Data.Migration` and `Apskaita.Shared` no longer exist; solution builds clean.
