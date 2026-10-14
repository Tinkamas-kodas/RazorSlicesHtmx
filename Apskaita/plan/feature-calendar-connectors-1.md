---
goal: Connect User Mailboxes to Calendar Providers (Outlook 365 First) and Show the Week's Events on the Time Tracking Page
version: 1.0
date_created: 2026-10-11
last_updated: 2026-10-11
owner: Apskaita
status: 'Implemented'
tags: [feature, calendar, outlook365, microsoft-graph, oauth, settings, time-tracking]
---

# Introduction

![Status: Implemented](https://img.shields.io/badge/status-green)

Add a user-level **Settings** page where each user connects their own calendar account to Apskaita.
The first provider is **Outlook 365 (Microsoft Graph)**; the design keeps a provider registry so Google
Calendar (and others) can be added later. The connected account is stored as a **connector** row related
to the `User`. The time tracking week page then shows that user's calendar events for the displayed week
in a Clockify-style side panel. **Display only** — no interaction with events, no import, no entry
creation from events in this iteration.

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

## 4. Out of Scope (next iterations)

- Creating/updating time entries from calendar events; import persistence; two-way sync.
- Google Calendar connector implementation (registry slot reserved).
- Admin view of other users' connectors; token re-use for write scopes (`Calendars.ReadWrite`).
- Background/precomputed sync jobs and caching layers beyond the access-token cache.

## 5. Revision 1.1 — corrected requirements (2026-10-12)

The first implementation assumed one connector per (user, provider) and reused the sign-in Entra app.
Both were wrong:

- **REQ-010**: a user can connect **many accounts of the same provider** (e.g. two organizations' mailboxes).
  Connector rows are keyed by `AccountKey` (normalized account email); unique index is
  `(user_id, provider, account_key)` (migration `CalendarConnectorMultiAccount`). Reconnecting the same
  mailbox updates its row; a different mailbox adds a new one.
- **REQ-011**: connectors use a **dedicated multi-tenant app registration** (`Apskaita Connectors`,
  audience `AzureADMultipleOrgs`, authority `common`, own `Auth:Connectors:*` credentials) — never the
  sign-in app. Delegated `Calendars.Read` is user-consentable, so no admin action is required in the
  other organization under default policy.
- **REQ-012**: every connect challenge sends `prompt=select_account`, so creating a new connection always
  authenticates the mailbox rather than silently reusing an existing Microsoft SSO session.
- **REQ-013**: when the other tenant blocks user consent (`AADSTS90093/90094`), the callback redirects to
  `/settings?connectError=admin-consent`, which renders the admin consent URL
  (`login.microsoftonline.com/common/adminconsent?client_id=…`) for that organization's admin.
- **REQ-004 updated**: the time tracking panel merges events from **all** of the user's connectors, sorted
  by local start; per-account failures are isolated (one dead token does not blank the panel).
- **UI**: the provider card lists connections (display name, email, last sync, error) with per-connection
  `POST /settings/disconnect/{id:guid}` (ownership verified against the session user) and a single
  "Connect account" action.
