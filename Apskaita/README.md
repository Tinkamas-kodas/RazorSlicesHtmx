# Apskaita

ASP.NET Core (net10.0) accounting app built on the RazorSlicesHtmx libraries in this repository.

## Projects

- `src/Apskaita.Web` — app host: layout, auth wiring, composition root, and the data layer (`Data/`: `ApskaitaDbContext`, mappings, migrations, design-time factory).
- `src/Apskaita.Feature.Account` — account, admin user management, calendar connectors (owns `users`, `calendar_connectors` via internal `IAccountStore`).
- `src/Apskaita.Feature.Admin` — admin registries (clients, issuers, units, projects, invoice templates, system settings; internal `IAdminStore`).
- `src/Apskaita.Feature.TimeTracking` — time-tracking week grid and dialogs (internal `ITimeTrackingStore`).
- `src/Apskaita.Shared` — shared infrastructure: nav items, policies, feature URI, calendar connector/reader abstractions, status-toggle UI, logging.
- `src/Apskaita.Entities` — POCO entities + the `IQuery<T>` cross-feature read contract (no EF dependency).
- `tests/Apskaita.Tests` — xUnit v3 tests (run via `dotnet test` from `Apskaita/`).

Feature assemblies read other features' tables only through `IQuery<T>`; write access lives in each
feature's `internal` store, implemented by the composition root (`Apskaita.Web/Composition`).

## Run

```powershell
dotnet run --project src/Apskaita.Web --launch-profile http   # http://localhost:5059
```

MySQL is expected at `localhost:3306` (see `docker-compose.dev.yml`); connection string in `appsettings.json`.

## Authentication (Entra ID / Google)

Sign-in is cookie-based with external OpenID Connect providers; the provider-asserted email is the identity
key (see `plan/feature-auth-providers-1.md`). Providers register only when their `ClientId` is configured.

Dev secrets via user-secrets (`UserSecretsId` is set in `Apskaita.Web.csproj`):

```powershell
dotnet user-secrets --project src/Apskaita.Web set "Auth:Entra:ClientId=<appId>"
dotnet user-secrets --project src/Apskaita.Web set "Auth:Entra:ClientSecret=<secret>"
dotnet user-secrets --project src/Apskaita.Web set "Auth:Entra:TenantId=<tenant-guid>"
```

Entra sign-in app registration must include redirect URIs `http://localhost:5059/signin-entra` and
`https://localhost:7272/signin-entra`; the dedicated connector app uses `…/signin-connect-entra` (see below).
Production hosts replace these with their public URLs.

## Calendar connectors (Outlook 365 + imported .ics)

Per-user calendar connections (see `plan/feature-calendar-connectors-1.md`):

- The **Account** page (`/account/me`) connects the user's own Outlook 365 mailboxes via delegated Microsoft
  Graph consent (`Calendars.Read`, `offline_access`). A user can connect **several accounts of the same
  provider** (unique per user + provider + account); each connection disconnects independently.
- Connect uses a **dedicated, multi-tenant app registration** — *not* the sign-in Entra app — with its own
  credentials in `Auth:Connectors:*` (user-secrets in dev). Every connect sends `prompt=select_account`, so
  adding a connection always authenticates the mailbox instead of silently reusing an existing Microsoft
  session; users from other organizations can consent for themselves (user-consentable scope).
- If the other organization blocks user consent (`AADSTS90093/90094`), the callback redirects back with a
  notice and an **admin consent** link (`https://login.microsoftonline.com/common/adminconsent?client_id=…`).
- Tokens are stored encrypted (ASP.NET Core Data Protection) in `calendar_connectors`; disconnect deletes
  the row and forgets that account's tokens.
- The time tracking week page merges events from **all** of the user's connectors (per-account failures are
  isolated — one dead token does not blank the panel).
- Connector app setup (`az` CLI): audience `AzureADMultipleOrgs`, redirect URIs `…/signin-connect-entra`,
  delegated Graph `Calendars.Read` (`az ad app update --required-resource-accesses`), client secret.
- The ASP.NET Core **Data Protection key ring is persisted to the database** (`data_protection_keys`,
  `builder.Services.AddDataProtection().PersistKeysToDbContext<ApskaitaDbContext>()`, migration
  `DataProtectionKeys`) so connector tokens survive redeployment and scale-out. Consequences:
  - The keys table must exist before first use (run migrations); the app creates keys on demand.
  - Key XML is stored unencrypted by default; on production add an encryptor, e.g.
    `.ProtectKeysWithAzureKeyVault(...)` or `.ProtectKeysWithCertificate(...)` (and keep the
    `data_protection_keys` contents access-controlled).
  - Switching from the previous local key ring (or rotating/deleting keys) makes stored refresh tokens
    undecryptable — affected users just reconnect Outlook on `/account/me`.

### Imported calendars (.ics upload)

Any calendar that can export an `.ics` file (Google Calendar, Apple Calendar, Outlook) can be used without
OAuth. The **Imported calendar (.ics)** card on `/account/me` works in two steps:

1. **Create a named calendar** — type a name (e.g. `nrdcompanies`) and press *Create calendar*. The name is
   what the row shows and is also the stable *account key* of the events, so it must be unique per user and
   cannot be renamed later (create another calendar instead).
2. **Upload an `.ics` file** — each calendar row has a file picker and an *Import* button. Parsing happens
   in the request (`Ical.Net`): line unfolding, time zones with DST, `RRULE` expansion, `EXDATE`/`RDATE` and
   `RECURRENCE-ID` overrides. Only `VEVENT` is read; descriptions, attendees and other components are
   ignored, and the raw file text is never stored.

Notes:

- **Import replaces the events, never the calendar.** Re-uploading swaps the stored rows in one transaction
  and updates the row's *Last sync*; the connector (its name and any time entries linked to its events)
  stays.
- The row shows statistics: stored event count and the covered date range (earliest start → latest end).
- A recurring series is expanded **fully** when the file bounds it (`RRULE` with `UNTIL` or `COUNT`). A
  series with no end in the file is expanded to **90 days after the import** — re-upload to extend it.
  Uploads are capped at **2 MB**.
- Each occurrence of a recurring series is stored as its own event (`<UID>@<utc start>`, with the shared
  series `UID` kept in `group_id`), so logging one date of a series does not mark the others as logged.
  A `RECURRENCE-ID` override in the file is ignored — the series' original time and text win.
- Events appear in the time tracking week panel exactly like Graph events — clicking one opens the
  create/edit dialog and links the entry to that event (`provider=ics`).
- *Disconnect* removes the calendar and cascades to its stored events; time entries keep their reference.
- No background cleanup job: an imported calendar keeps its events until the next import or disconnect.

## Migrations

```powershell
dotnet ef migrations add <Name> --project src/Apskaita.Web   # design-time factory: Web/Data
dotnet run --project src/Apskaita.Web -- --migrate           # apply pending migrations, then exit
```

`--migrate` accepts `--connection=<conn-string>` (defaults to the local dev MySQL). The web host itself
never migrates on startup, so in-process `WebApplicationFactory` tests are unaffected.
