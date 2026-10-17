---
goal: Connect User Mailboxes to Calendar Providers (Outlook 365 First), Import .ics Calendars, and Show the Week's Events on the Time Tracking Page
version: 1.2
date_created: 2026-10-11
last_updated: 2026-10-16
owner: Apskaita
status: 'Implemented (1.0–1.1); Planned (1.2 ICS import)'
tags: [feature, calendar, outlook365, microsoft-graph, oauth, ics-import, settings, time-tracking]
---

# Introduction

![Status: Implemented](https://img.shields.io/badge/status-green)

Add a user-level **Settings** page where each user connects their own calendar account to Apskaita.
The first provider is **Outlook 365 (Microsoft Graph)**; the design keeps a provider registry so Google
Calendar (and others) can be added later. The connected account is stored as a **connector** row related
to the `User`. The time tracking week page then shows that user's calendar events for the displayed week
in a Clockify-style side panel. **Display only** — no interaction with events, no import, no entry
creation from events in this iteration.

**Revision 1.2 (planned, §6)** adds a second, credential-free source for the same panel: importing an exported
**.ics (RFC 5545) file** as a local calendar connection. It replaces the Outlook add-in bridge
(`feature-outlook-addin-bridge-1.md`, removed): an add-in must be installed at the account/mailbox level and
needs delegated Microsoft Graph calendar rights — i.e. exactly what the Outlook 365 connector already does —
so it added install friction without new capability. `.ics` import instead covers calendars that cannot be
connected via OAuth (Google Calendar without client credentials, restricted tenants, read-only shared
calendars, Apple/iCloud export). §6.2 records the storage decision: **parse on upload and store in EF Core
with a 30-day retention sweep — nothing is kept in the browser**.

## 0. Shared Language

| Term | Definition |
|---|---|
| **Calendar provider / connector** | A provider is a calendar source keyed by `outlook365` (future: `google`). A connector is the per-user stored connection (`calendar_connectors` row) holding tokens + account info for one provider. |
| **Connect flow** | Explicit per-user OAuth consent started from `/settings`: OIDC challenge requesting Graph delegated scopes; on success the refresh token is stored encrypted against the current session user. |
| **Access token cache** | The short-lived Graph access token + expiry persisted on the connector row (encrypted), refreshed with the stored refresh token when expired. |
| **Calendar event (view)** | Normalized `CalendarEventInfo` (subject, start, end, location, isOnline, organizer) mapped from Graph `calendarView` items; display-only. |
| **Calendar panel** | The Clockify-like side panel on the time tracking week page listing the connected user's events grouped by day. |

## 1. Requirements & Constraints

- **REQ-001**: A user-level Settings feature (`/settings`, policy `Operational`) lists all known calendar providers with connection status: not connected / connected as `<account email>` / error, plus last sync time and last sync error when present.
- **REQ-002**: Outlook 365 connector: connect button starts an OIDC consent flow requesting delegated scopes `https://graph.microsoft.com/Calendars.Read`, `offline_access`, `openid`, `profile`, `email`; success stores the connector row for the current user (unique per user+provider, reconnect replaces).
- **REQ-003**: Disconnect (`POST`, antiforgery-protected) deletes the connector row and forgets all tokens; reconnect is allowed afterwards.
- **REQ-004**: The time tracking week page shows the connected user's calendar events for the displayed week (`WeekStart..WeekEnd`, inclusive) in a side panel grouped per day: subject, `HH:mm–HH:mm` range, location/online hint, organizer when not the user. Events are read per page request (no background sync).
- **REQ-005**: Token lifecycle: reuse cached access token while valid; refresh via the token endpoint with the stored refresh token + client credentials when expired (< 5 min remaining); refresh failure marks the connector with a sync error and shows a notice, never breaks the page.
- **REQ-006**: Graph failures (401/429/5xx/network) degrade gracefully: panel shows "Calendar unavailable" + short reason; time entries render normally.
- **REQ-007**: No connector → panel shows a hint with a link to `/settings` ("Connect Outlook to see your calendar here").
- **REQ-008**: Events are display-only: no HTMX wiring, no dialogs, no "create entry from event" (explicitly out of scope). *(Superseded 2026-10-13 by `feature-time-tracking-from-calendar-1.md`: events are clickable through a single `GET event` URI; the server returns the create or edit dialog.)*
- **REQ-009**: Provider registry pattern: `ICalendarProviderConnector` (provider key, display label, connect/disconnect support) resolved by key; `Outlook365Connector` is the only implementation now; Settings page renders every registered provider, non-implemented ones as "coming soon" placeholders.
- **SEC-001**: Refresh and access tokens are encrypted at rest with ASP.NET Core Data Protection (`IDataProtector`, purpose `Apskaita.CalendarConnector`); never written to logs, never returned to the client beyond the account email.
- **SEC-002**: Connector rows are scoped to the session user: connect stores under the authenticated cookie principal's user id; read/disconnect only ever touch the current user's row. No admin-level impersonation in this iteration.
- **SEC-003**: The connect flow is CSRF-safe via the OIDC handler's built-in state/nonce; the callback path is not anonymous-accessible in practice (state + correlation cookie) and requires an existing session cookie to attribute the connector.
- **SEC-004**: Graph is called with delegated permissions only (no app-only `Calendars.Read.All`, no admin consent requirement).
- **CON-001**: No changes to the RazorSlicesHtmx library.
- **CON-002**: Reuse the existing Entra app registration (`Apskaita`, `AzureADMyOrg`): same client id/secret/authority as `Auth:Entra:*`; only new redirect URI + delegated Graph permission are added on the app (TASK-013, via `az` CLI).
- **CON-003**: No new NuGet packages — raw `HttpClient` against the Graph v1.0 token/token endpoint (no Microsoft.Graph SDK); Data Protection and `AddOpenIdConnect` are already available.
- **CON-004**: Existing tests (73) keep passing; new endpoints follow the `AddFeature<T>("/route")` + `MapEndpoints(RouteGroupBuilder)` conventions.
- **CON-005**: Single-tenant scope: org (Entra) mailboxes only; personal Outlook.com accounts are out of scope (`AzureADMyOrg` audience).
- **GUD-001**: Follow feature-module layout: `Features/Settings/{Endpoints,Models,Slices,Services}`; connector services under `Features/Settings/Services/`.
- **GUD-002**: Reuse `ExternalCallLoggingHandler` for the Graph/token backchannels (categories `Apskaita.Calendar.Outlook365[.External]`), same as the auth providers.
- **GUD-003**: Settings page uses the existing HTMX patterns: fragment OOB update of the connector card after disconnect, toast on connect/disconnect success.
- **GUD-004**: CSS additions go to `site.css` and `Assets.Version` is bumped (sprite/css cache busting convention).
- **PAT-001**: Dedicated OIDC scheme `connect-entra` for the connect flow with `SignInScheme = null` so the callback stores tokens without re-issuing the session cookie (login flow untouched).
- **PAT-002**: Time tracking page model gains calendar data via a DI abstraction `ICalendarEventReader` so tests can stub events without Graph; the reader resolves the current user's connector internally and returns nothing when absent.
- **PAT-003**: Graph `calendarView` with UTC boundaries; response times converted to the app's local display timezone (same `DateTimeOffset → local` convention as existing `StartedAtLocal` handling).

## 2. Implementation Steps

### Implementation Phase 1 — Connector entity + migration

- GOAL-001: A per-user, per-provider connector row stores encrypted tokens and sync metadata.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | `src/Apskaita.Entities/CalendarConnector.cs`: `Id`, `UserId` (Guid), `Provider` (string enum-backed: `outlook365`), `AccountEmail`, `RefreshTokenProtected` (text), `AccessTokenProtected` (nullable text), `AccessTokenExpiresAtUtc` (nullable), `Scopes`, `ConnectedAtUtc`, `LastSyncAtUtc` (nullable), `LastSyncError` (nullable). | ✔ | |
| TASK-002 | Mapping in `ApskaitaEntityModelBuilderExtensions`: `ConfigureCalendarConnector` — required lengths via `FieldLengths`, `Provider` as MySQL enum (`ToMySqlDefinition`), unique index on `(user_id, provider)`; add `DbSet<CalendarConnector> CalendarConnectors` to `ApskaitaDbContext`. | ✔ | |
| TASK-003 | EF migration `CalendarConnector` in `Apskaita.Data.Migration` creating `calendar_connectors` (snake_case via naming convention); apply to local MySQL. | ✔ | |

### Implementation Phase 2 — Connect plumbing (OIDC scheme + token store)

- GOAL-002: Explicit per-user consent stores an encrypted refresh token for Outlook 365.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-004 | `Features/Settings/Services/ConnectorTokenProtector.cs`: `IDataProtector` wrapper (purpose `Apskaita.CalendarConnector`) — `Protect`/`Unprotect` for token strings. | ✔ | |
| TASK-005 | `Features/Settings/Services/CalendarConnectorRegistry.cs`: `ICalendarProviderConnector` { `Key`, `Label`, `IsImplemented` } + registry collecting all implementations; `Outlook365Connector` (key `outlook365`, label "Outlook 365 / Microsoft 365"). | ✔ | |
| TASK-006 | `Program.cs`: register scheme `connect-entra` (`AddOpenIdConnect`) only when `Auth:Entra:ClientId` is configured: same authority/client id/secret as sign-in, `Scope` = `openid profile email https://graph.microsoft.com/Calendars.Read offline_access`, `CallbackPath = "/signin-connect-entra"`, `SaveTokens = true`, `SignInScheme = null`, `ResponseType = code`; events: `OnTokenValidated` → upsert connector row for the current cookie user (email from `preferred_username`/`email`, refresh token from `ctx.Properties.GetTokenAsync("refresh_token")`, scopes, `ConnectedAtUtc`); `OnRemoteFailure` → redirect `/settings?connectError=1` with log. | ✔ | |
| TASK-007 | Connect endpoint `GET /settings/connect/outlook365` (authenticated): `Results.Challenge` to `connect-entra` with `RedirectUri = "/settings?connected=outlook365"`; 400 when the scheme is not registered (same guard pattern as `/account/challenge/{scheme}`). | ✔ | |

### Implementation Phase 3 — Settings feature UI

- GOAL-003: Users see provider cards with status and connect/disconnect actions.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-008 | `Features/Settings/Endpoints/SettingsEndpoints.cs` + `AddFeature<SettingsEndpoints>("/settings")`, `group.RequireAuthorization(AuthPolicies.Operational)`, nav `new("MAIN", "settings", "Settings", "list", Order: 2, AuthorizationPolicy: AuthPolicies.Operational)`; `GET list` → `_SettingsPage` with connector rows joined to the registry; `POST disconnect/{provider}` → delete row, fragment `_Empty` + OOB card update + toast; `GET /` redirect to `list`. | ✔ | |
| TASK-009 | Slices `_SettingsPage.cshtml` + `_CalendarConnectorCard.cshtml`: card per provider — label, status (connected as email / not connected / coming soon), last sync + error, Connect/Reconnect link (GET challenge) and Disconnect button (POST + antiforgery token); Bootstrap cards, no new JS. | ✔ | |
| TASK-010 | Notice handling for `?connected=` / `?connectError=` query flags → success toast / danger alert fragment on the settings page (server-rendered, no JS parsing). | ✔ | |

### Implementation Phase 4 — Graph event reader

- GOAL-004: Week events are fetched from Graph with resilient token refresh and graceful degradation.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-011 | `Features/Settings/Services/Outlook365Connector.cs`: typed `HttpClient` (`graph` v1.0 base + `token` clients, `ExternalCallLoggingHandler` backchannel); `GetAccessTokenAsync(connector)`: cached token valid → reuse, else POST token endpoint (`grant_type=refresh_token`, client credentials) → store new access token + expiry (encrypted) + `LastSyncAtUtc`; failure → `LastSyncError` update, return null. | ✔ | |
| TASK-012 | `ICalendarEventReader` (in `Apskaita.Shared` or Settings/Services) + `Outlook365EventReader`: resolve current user's connector; none → empty; Graph `GET /me/calendarView?startDateTime=…Z&endDateTime=…Z&$select=subject,isOnlineMeeting,location,organizer,start,end,webLink&` sorted by start; map to `CalendarEventInfo` (subject fallback "(No subject)", `DateTimeOffset` start/end, isOnline, location display); single retry on 5xx; 401 → force refresh once; errors → `LastSyncError` + empty list. | ✔ | |
| TASK-013 | Entra app configuration via `az` CLI (Azure): add delegated permission `Microsoft Graph / Calendars / Read calendar items` (`Calendars.Read`) to the `Apskaita` app (`az ad app update --required-resource-accesses`), add redirect URI `http://localhost:5059/signin-connect-entra` (and `https://localhost:7272/…`); document prod redirect URIs in README. | ✔ | |

### Implementation Phase 5 — Time tracking panel

- GOAL-005: The week page shows events in a Clockify-style panel, display-only.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-014 | `TimeTrackingPageModel` gains `IReadOnlyList<TimeTrackingCalendarEventViewModel> CalendarEvents`, `bool HasCalendarConnector`, `string? CalendarNotice`; `TimeTrackingContentService.CreatePageModelAsync` accepts the optional `ICalendarEventReader` result (endpoint resolves reader from DI, passes events for `WeekStart..WeekEnd`); view model: `Subject, StartLocal, EndLocal, Location, IsOnline, OrganizerDisplayName, DayDate`. | ✔ | |
| TASK-015 | `_Grid.cshtml` (inside `#time-tracking-grid` so `time-tracking-list-refresh` updates it): two-column layout — existing week grid + right side panel `#tt-calendar-panel` listing events grouped by day (day header + event cards: time range, subject, location/online icon, organizer); empty states per REQ-006/REQ-007; plain `<div>` cards, no `hx-*` attributes (REQ-008). | ✔ | |
| TASK-016 | `site.css`: panel + event card styles (Clockify-like: compact cards, colored left border by hash of organizer/subject, muted empty state), responsive fallback (panel below grid under 991px); bump `Assets.Version`. | ✔ | |

### Implementation Phase 6 — Tests + docs

- GOAL-006: Automated coverage with Graph stubbed out; docs updated.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-017 | Unit tests: token protector round-trip; access-token reuse/refresh/expiry logic with fake `HttpMessageHandler`; `calendarView` JSON → `CalendarEventInfo` mapping (subject fallback, offsets → local). | ✔ | |
| TASK-018 | Integration tests (`ApskaitaApiFactory`): settings page renders not-connected + coming-soon states; connect endpoint 400s when scheme unregistered; disconnect deletes row + toast; time tracking list renders stubbed `ICalendarEventReader` events in the panel; reader throwing → notice + 200; no connector → hint. | ✔ | |
| TASK-019 | Docs: README setup section (Graph delegated permission, `/signin-connect-entra` redirect URI, prod notes); `requirements.md` §Integrations updated (Outlook 365 calendar read integration added alongside the planned Clockify import). | ✔ | |

## 3. Acceptance Criteria

- **AC-001**: `GET /settings` (role user/admin) lists "Outlook 365 / Microsoft 365" with Connect action and "not connected" status; other providers appear as coming-soon placeholders.
- **AC-002**: `GET /settings/connect/outlook365` → 302 to `login.microsoftonline.com/…/oauth2/v2.0/authorize` with Graph scopes in `scope`; after consent the connector row exists with an encrypted refresh token (plaintext not present in DB) and the session cookie is unchanged.
- **AC-003**: `POST /settings/disconnect/outlook365` (valid antiforgery) deletes the row; subsequent week page shows the no-connector hint.
- **AC-004**: With a valid connector, `GET /time-tracking/list?weekStart=…` renders the calendar panel containing that week's events (subject + local time ranges, grouped by day); panel refreshes with `time-tracking-list-refresh`.
- **AC-005**: Expired access token → transparent refresh via the token endpoint (one backchannel call logged) and events still render; refresh failure → panel notice + `last_sync_error` persisted, HTTP 200.
- **AC-006**: No event element carries `hx-get`/`hx-post` or opens a dialog (display-only).
- **AC-007**: All existing tests (73) plus new tests pass; `dotnet build` clean.


## 4. Revision 1.2 — Import `.ics` calendars (planned, 2026-10-16)

![Status: Planned](https://img.shields.io/badge/status-Planned-yellow)

*(Routes below use the current `/account/...` prefix — the Settings feature was merged into Account, so the
`/settings` paths in §2/§5 now live under `/account/me`.)*



### 4.1 Shared Language (additions)

| Term | Definition |
|---|---|
| **ICS import** | The user uploads an exported `.ics` (RFC 5545 `VCALENDAR`) file; the server parses `VEVENT`s into stored rows in the same request. No live sync — refreshing means uploading the file again. |
| **Imported calendar** | A `calendar_connectors` row with `Provider = Ics` plus its parsed events; it behaves like any other connection in the connections table and in the week panel. |
| **Import date** | The time the file was parsed and stored (`ConnectedAtUtc`, reset by "Replace file"). It is the retention anchor — **event dates never affect retention**. |
| **Snapshot** | The parsed `VEVENT` rows for one imported calendar; replaced wholesale on "Replace file" and deleted by the retention sweep 30 days after the import date. |

### 4.2 Storage decision (decided 2026-10-16: EF Core + retention sweep)

The uploaded file must live somewhere between "user picked the file" and "the events appear in the panel and
the calendar appears in the connections table". Options considered:

| # | Where the payload lives | Survives reload | Survives new tab / other device | Connections table can list it server-side | Panel request ships the payload | Verdict |
|---|---|---|---|---|---|---|
| A | Browser `sessionStorage` (raw `.ics` text) | yes (same tab) | **no** — tab-scoped | no | **yes, every request** | Rejected as the store |
| B | `localStorage` / IndexedDB (raw `.ics` text) | yes | yes (same browser only) | no | yes | Rejected as the store |
| C | Server staging (`IMemoryCache`, TTL) → permanent parsed rows | yes | yes | yes | no | Rejected — an extra confirm round-trip buys nothing once the upload stores directly |
| D | **Parse on upload → parsed rows in MySQL (EF Core) + retention sweep (30 days from the import date)** | yes | yes | yes | no | **Chosen** |

**Decision: parse on upload and store in EF Core — nothing is kept in the browser.**

1. **Upload → parse → store.** `POST /account/import` (multipart, antiforgery via the global
   `htmx-antiforgery.js` header) validates the file, parses it with `Ical.Net` (§6.4), and writes the
   `Provider = Ics` connector row plus its parsed event rows in one transaction. The response is the
   standard toast + `#connectors-table` OOB update (GUD-003/005) with a short summary fragment: calendar
   name (`X-WR-CALNAME`, fallback file name), event count, covered date range, and parse warnings. No
   preview/confirm round-trip, no staging cache, no hidden `importId`, no browser storage.
2. **Retention (snapshot with an expiry).** A background sweep deletes `Ics` imports whose **import date**
   is older than **30 days** — measured from the import/replace time (`ConnectedAtUtc`), **never from event
   dates** — cascading to their event rows. "Replace file" resets the clock, and the connections table shows
   each imported calendar's expiry date so users know when to refresh. `TimeTracking` rows that reference a
   cleaned-up event keep their stored `CalendarEventReference` (no cascade into `time_trackings`); the panel
   simply stops showing that event.

Why **not** browser storage (`sessionStorage` / `localStorage` / IndexedDB):

- The week panel and the connections table are **server-rendered**; a client-only store means the server can
  neither list the connection nor merge its events unless the whole `.ics` rides along on every request (an
  `hx-include` hidden textarea carrying megabytes of text per panel render).
- `sessionStorage` is tab-scoped and shares a ~5 MB per-origin quota; a year of exported events routinely
  exceeds it, and opening a new tab silently loses the calendar. There is also no `LastSyncAtUtc`, no
  per-user limits, and no cross-device continuity.
- The create-from-event flow (`CalendarEventReference`, `feature-time-tracking-from-calendar-1.md`) needs a
  stable server-side `AccountKey` per calendar, and neither retention nor the per-user caps can be enforced
  client-side.

Why the raw `.ics` text is **not** persisted: only the normalized events are needed for display and for the
event→entry link; storing the raw export would keep untrusted third-party data (other attendees,
descriptions, email addresses) far longer than necessary. The parsed rows are the only copy, and the 30-day
retention is their privacy bound.

### 4.3 Requirements & constraints (revision 1.2)

- **REQ-014**: `CalendarProvider` gains `Ics`; `Setup.CalendarConnectors` gains `CalendarProviderInfo("ics", "Imported calendar (.ics)", CalendarProvider.Ics, Implemented: true)`. The `ics` provider row in `_ConnectorsTable` renders an **upload form** instead of the OAuth "Connect account" link.
- **REQ-015**: `POST /account/import` accepts one `IFormFile` (`multipart/form-data`, antiforgery, `AuthPolicies.Operational`): extension `.ics` **and** `BEGIN:VCALENDAR` on the first non-empty line are required; size ≤ **2 MB**. Otherwise a danger toast/alert fragment names the reason and nothing is stored.
- **REQ-016**: The upload stores directly — no preview/confirm round-trip, no staging cache, and nothing kept in the browser (§6.2). The response carries a summary fragment (name, event count, covered date range, warnings) so the user sees what was imported.
- **REQ-017**: Upload creates (or updates) one `calendar_connectors` row: `Provider = Ics`, `AccountKey = "ics:" + SHA256(calendar name + first import's UID)` hex-truncated (stable per calendar+owner, so re-importing the same calendar updates rather than duplicates — the REQ-010 rule), `AccountEmail`/`DisplayName` = calendar name, `RefreshTokenProtected = ""`, `Scopes = "ics-file-import"` (no tokens), `ConnectedAtUtc` = import time (**also the retention anchor, REQ-024**), `LastSyncAtUtc` = import time, `LastSyncError` = parse-warning summary or NULL.
- **REQ-018**: Parsed events are stored in a new table `calendar_imported_events` (`id`, `connector_id` FK cascade, `external_event_id` (UID, ≤255), `start_utc`, `end_utc`, `subject`, `location`, `organizer_name`, `is_online`, audit columns; index `(connector_id, start_utc)`). `POST /account/ics/{id:guid}/replace` re-parses and replaces the rows in one transaction and resets the retention clock (REQ-024); the raw `.ics` text is **not** persisted.
- **REQ-019**: At most **5** imported calendars per user and **500** stored events per calendar (truncate + warning) so a huge export cannot grow unbounded.
- **REQ-020**: `IcsEventReader : ICalendarEventReader` maps stored rows for `fromLocal..toLocal` into `CalendarEventInfo` (`ProviderKey = "ics"`, `AccountKey = connector.AccountKey`, `EventId = external_event_id`) — imported events are clickable in the panel and link time entries exactly like Graph events (`ICalendarEventReferenceResolver.OwnsAccountAsync` already returns true for owned `Ics` rows).
- **REQ-021**: The week endpoint keeps injecting a single `ICalendarEventReader`. A `CompositeCalendarEventReader` (registered as that service, resolving both readers) merges, sorts by local start, reports `HasConnector` when **any** provider has a connection, and isolates failures per provider (REQ-006 / REQ-004-updated semantics unchanged). `Outlook365Connector` stays a reader but is no longer the registered `ICalendarEventReader`.
- **REQ-022**: Disconnect (`POST /account/disconnect/{id:guid}`) works unchanged for `Ics` rows — delete the connector, cascade removes its events, and the panel drops those events on the next render.
- **REQ-023**: Every imported string (`SUMMARY`, `LOCATION`, `ORGANIZER` `CN`) is untrusted input: render through Razor encoding only (never `RawHtml`), strip control characters at parse time, and never turn ICS text into links or images.
- **REQ-024**: `IcsImportCleanupWorker : BackgroundService` (in `src/Apskaita.Web/Jobs/`) sweeps hourly and deletes `calendar_connectors` rows with `Provider = Ics` whose `ConnectedAtUtc` is older than `CalendarImports:RetentionDays` (**default 30 days**) — measured from the **import/replace time, never from event dates** — cascading to `calendar_imported_events`. It opens its own `IServiceScope` per sweep (`ApskaitaDbContext` is scoped via `AddDbContext`), logs counts at `Information`, is idempotent, and never touches `time_trackings` (entries keep their stored reference). `CalendarImports:Enabled` (default true) turns the sweep off in tests/dev.
- **REQ-025**: Each imported calendar row in the connections table shows its event count and expiry date (`ConnectedAtUtc + RetentionDays`, local date) with the hint "Re-upload to keep it"; OAuth-connected rows are unchanged.
- **SEC-005**: The uploaded file is parsed in memory and never written to disk or to a staging store; the raw text is discarded once the rows are saved. Imported rows are visible only to their owner (SEC-002 unchanged), never logged, and the parsed summary is the only thing returned to the client.
- **SEC-006**: No URL or `webcal:` fetching in this iteration (a remote feed is an SSRF surface); only an uploaded file is parsed.
- **CON-006 (amended 2026-10-16)**: A parser package is approved for this revision, overriding CON-003's no-new-packages rule: **`Ical.Net` 5.2.3** (RFC 5545 library, MIT license, `net8.0` asset resolves for both `net8.0` and `net10.0`, transitive `NodaTime` 3.2.2). The version is pinned centrally in `Apskaita/Directory.Packages.props` (`<PackageVersion Include="Ical.Net" Version="5.2.3" />`) and referenced only from `src/Apskaita.Shared/Apskaita.Shared.csproj`.
- **CON-007**: Adding `Ics` to the `CalendarProvider` enum requires a migration to widen the MySQL `enum` column `calendar_connectors.provider` — purely additive, no data conversion.
- **GUD-005**: Upload and replace reuse the existing HTMX patterns: `hx-encoding="multipart/form-data"` form, fragment response with `#connectors-table` OOB + toast; no new JS file and no `hx-*` beyond what the connectors table already uses.

### 4.3.1 Revision 1.3 (2026-10-16): named imported calendars, no retention sweep

Decided with the user after 1.2 was drafted — the upload-only, auto-named snapshot model was replaced:

| Topic | 1.2 (superseded) | 1.3 (implemented) |
|---|---|---|
| Creating a calendar | implicit — the first upload creates the connector and names it from `X-WR-CALNAME` | explicit: the user types a name (e.g. `nrdcompanies`) and creates an empty calendar, then uploads into it |
| `AccountKey` | `ics:` + SHA256(name + first UID) | the entered name, lower-cased (case-insensitively unique per user, never renamed) |
| Re-import | replaces the connector row **and** its events | replaces **only** the events; the connector row survives every upload |
| Retention | 30-day sweep (`IcsImportCleanupWorker`, `CalendarImports:*`) | **no sweep** — events live until the next import or a disconnect; the worker and the options section were dropped |
| Caps | ≤ 5 calendars per user, ≤ 500 events per calendar | dropped; only the 2 MB upload cap and a 20 000-occurrence safety valve remain |
| Statistics | expiry date per row | event count + covered range (earliest start → latest end) per row |
| Event identity | `external_event_id` = the series `UID` (shared by all its occurrences) | `external_event_id` = `UID` for a single event, `<UID>@<utc start>` per occurrence of a series, plus a nullable `group_id` holding the series `UID` (added 2026-10-17: one `UID` cannot address one date, and the time-entry link is keyed on provider + account + event id) |

Requirements changed by this revision: **REQ-017** (name from the user, no hash), **REQ-018** (`POST
/account/ics/{id}/import` replaces events only; the table also gains a nullable `group_id`), **REQ-019**
(limits dropped), **REQ-024** (sweep removed —
`ConnectedAtUtc` is no longer a retention anchor), **REQ-025** (statistics instead of an expiry hint).
Everything else (REQ-014/015/016/020/021/022/023, SEC-005/006, CON-006/007, GUD-005) is unchanged; the
upload form is a per-row `hx-encoding="multipart/form-data"` form rather than a single page-level form.

### 4.4 Parsing with `Ical.Net` (what the library does and what Apskaita caps)

`Ical.Net` handles the RFC 5545 mechanics, so the hand-written subset from the first draft of this revision is
dropped: line unfolding, quoted-parameter escaping, `VTIMEZONE` (`STANDARD`/`DAYLIGHT` with DST transitions),
`RRULE` expansion (`FREQ`, `INTERVAL`, `UNTIL`, `COUNT`, `BYDAY`, `BYMONTH`, `BYSETPOS`, …), `EXDATE`/`RDATE`,
and `VALUE=DATE` / floating / `Z` / `TZID` datetimes. `RECURRENCE-ID` override rows are **not** merged into
their series by 5.2.3 (the master's `ExceptionInfo` stays empty after `Load`), so they are skipped — an
occurrence that was moved or renamed in the source calendar is imported with the series' original time and text.

What Apskaita does on top of the library:

| Concern | Rule |
|---|---|
| Calendar name | the name the user typed when creating the imported calendar (REQ-017, revision 1.3) — not `X-WR-CALNAME` |
| Event fields used | `UID`, `SUMMARY`, `LOCATION`, `ORGANIZER.CN`; `DESCRIPTION` and attendees are ignored (REQ-023) |
| Occurrence identity | a single event stores `external_event_id = UID` (`group_id = NULL`); an occurrence of a recurring series stores `external_event_id = <UID>@<utc start>` plus `group_id = UID`, because a series shares one `UID` while the time-entry link (provider + account + event id) must address one date |
| Recurrence expansion horizon | a rule that bounds itself (`UNTIL`/`COUNT`, or an `RDATE` list) is expanded **completely**; only an **unbounded** `RRULE` is cut at **today + 90 days** (amended 2026-10-17 — the file's own end beats the horizon; the horizon only guards infinite rules) |
| All-day events | Stored as the existing panel's date-only range |
| Online hint | `IsOnline` is false for imported events — an `.ics` export has no reliable online-meeting flag equivalent to Graph's `isOnlineMeeting` |
| Unsupported components | `VTODO`, `VJOURNAL`, `VFREEBUSY` and scheduling metadata (`METHOD`, `SEQUENCE`) are ignored |
| Caps | 2 MB upload and a 20 000-occurrence safety valve (truncate + warning); parse exceptions → danger toast (revision 1.3 dropped the per-user and per-calendar row caps) |

### 4.5 Implementation phases (revision 1.2)

- GOAL-007: An uploaded `.ics` becomes a per-user imported calendar whose events appear in the connections table and the week panel.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-020 | `Apskaita/Directory.Packages.props`: `<PackageVersion Include="Ical.Net" Version="5.2.3" />`; `src/Apskaita.Shared/Apskaita.Shared.csproj` gains the `PackageReference` (no inline version — central management). `dotnet restore` + `dotnet build` clean. | | |
| TASK-021 | `CalendarProvider.Ics` + `FieldLengths.CalendarImportedEvent` additions; `ConfigureCalendarConnector` enum widening; migration `CalendarProviderIcs` (additive `enum` change) applied to local MySQL. | | |
| TASK-022 | `CalendarImportedEvent` entity (`Id`, `ConnectorId`, `ExternalEventId`, `StartUtc`, `EndUtc`, `Subject`, `Location`, `OrganizerName`, `IsOnline`) + mapping (snake_case, `connector_id` FK cascade, `(connector_id, start_utc)` index), `DbSet CalendarImportedEvents`, migration `CalendarImportedEvents`. | | |
| TASK-023 | `src/Apskaita.Shared/Calendar/Ics/IcsCalendarImporter.cs`: `CalendarCollection.Load(stream)` → calendar name, `GetOccurrences` bounded to today..today+90d, `CalendarEventInfo`-shaped DTO + warnings; enforces the 2 MB / 500-row caps and strips control characters (REQ-015/019/023). | | |
| TASK-024 | `IcsConnectorStore` (in `Apskaita.Feature.Account/Persistence/`): `ImportOwnAsync` (upsert the `Ics` connector by `AccountKey`, replace event rows in one transaction, 5-calendars-per-user limit), `ReplaceAsync`, `GetExpiryUtc(connector)`, and `GetExpiredIcsAsync(retentionDays)` for the sweep. | | |
| TASK-025 | `IcsEventReader : ICalendarEventReader` (stored rows → `CalendarEventInfo` for `fromLocal..toLocal`) + `CompositeCalendarEventReader`; register the composite as `ICalendarEventReader` with both readers as its dependencies (REQ-021). | | |
| TASK-026 | `AccountEndpoints`: `POST import` (multipart + antiforgery → parse → store → toast + `#connectors-table` OOB + `_IcsImportSummary` fragment) and `POST ics/{id:guid}/replace` (ownership verified against the session user); both `AuthPolicies.Operational`; parse/limit failures → danger toast naming the reason, HTTP 200, nothing stored. | | |
| TASK-027 | Slices: `_IcsUploadForm.cshtml` (`<input type="file" accept=".ics,text/calendar">`, `hx-encoding="multipart/form-data"`, target `#ics-import-summary`), `_IcsImportSummary.cshtml` (name, event count, date range, warnings), `_ConnectorsTable` branch for `ics` (upload form instead of the connect link; ICS rows show event count + expiry hint (REQ-025) + "Replace file" + the existing Disconnect). | | |
| TASK-028 | `src/Apskaita.Web/Jobs/IcsImportCleanupWorker.cs`: `BackgroundService` with an hourly `PeriodicTimer`, `IServiceScopeFactory` scope per sweep, bulk delete of expired `Ics` connectors (cascade), `CalendarImports:RetentionDays` (default 30) + `CalendarImports:Enabled` options bound in `Setup` and documented in `appsettings.json`; registered via `AddHostedService` (REQ-024). | | |
| TASK-029 | `site.css`: upload form + summary styles matching the connectors table; `Assets.Version` bump (GUD-004). | | |
| TASK-030 | Tests: importer unit tests on fixture `.ics` files (unfolding, all-day, `TZID` + `VTIMEZONE` DST, weekly/monthly `RRULE` inside the horizon, `EXDATE`, `RECURRENCE-ID`, malformed → error, 2 MB cap, 500-row truncation); integration tests (upload → connector + event rows + OOB table + toast; re-uploading the same calendar updates rather than duplicates; replace → rows swapped and `ConnectedAtUtc` resets; replace/disconnect on a foreign id → 404; disconnect → cascade; sweep deletes only imports older than the retention window measured from `ConnectedAtUtc`, keeps newer ones, and never touches `time_trackings`; composite reader merges Outlook + ICS sorted; untrusted text is encoded). | | |
| TASK-031 | Docs: README `.ics` import section (how to export from Google/Outlook/Apple, limits, snapshot + 30-day expiry semantics — re-upload to refresh); `requirements.md` §Integrations gains "ICS file import (snapshot, 30-day retention)" next to the Outlook 365 integration; `CalendarImports:*` notes in `appsettings`; §4 of this doc updated. | | |

### 4.6 Acceptance criteria (revision 1.2)

- **AC-008**: `GET /account/me` lists an "Imported calendar (.ics)" provider row with a file-upload form, no connected accounts, and no OAuth link for it.
- **AC-009**: `POST /account/import` with a valid `.ics` stores the `Ics` connector + its event rows in one request, updates the connections table via OOB, shows a success toast and a summary fragment (name, event count, covered date range, warnings). No browser storage is involved.
- **AC-010**: A non-`.ics` upload, a file without `BEGIN:VCALENDAR`, a > 2 MB file, or a parse exception → danger toast naming the reason, HTTP 200, nothing stored.
- **AC-011**: `POST /account/ics/{id}/replace` and the existing `POST /account/disconnect/{id}` reject an id the session user does not own → 404 (HTMX error toast).
- **AC-012**: After an import, `GET /time-tracking/list?weekStart=…` shows the imported events merged with Outlook events and sorted by local start; clicking one opens the create/edit dialog as with Graph events (`provider=ics`).
- **AC-013**: "Replace file" replaces the snapshot (event count changes, `ConnectedAtUtc`/`LastSyncAtUtc` update, expiry date moves forward); the connections row shows the event count and the expiry date; "Disconnect" removes the calendar and its events (cascade) with the existing toast.
- **AC-014**: A `.ics` with `<script>` or markup in `SUMMARY`/`LOCATION` renders as escaped text in the panel and the connections table; all existing tests plus the new ones pass and `dotnet build` is clean.
- **AC-015**: The retention sweep deletes only `Ics` imports whose `ConnectedAtUtc` is older than `CalendarImports:RetentionDays` (default 30 days) — newer imports and all OAuth connectors survive regardless of event dates — and `time_trackings` rows keep their stored `CalendarEventReference` untouched.

### 4.7 Out of scope (revision 1.2)

- Remote `.ics` feed subscription (`https://` / `webcal:` auto-sync) — the natural next step once snapshot import works; it needs its own SSRF, timeout and credential story.
- Google Calendar OAuth connector (registry placeholder stays).
- Live sync, diff detection between uploads, incremental merge, and automatic re-upload of a stale snapshot (the expiry hint in the connections table is the affordance instead).
- Persisting the raw `.ics` text, and any non-event component (`VTODO`, `VJOURNAL`) or attendee/description display.
- Recurrence beyond the 90-day import horizon (until the next upload), per-calendar show/hide filters in the panel, sharing an imported calendar with other users, and any admin view of imported calendars.
