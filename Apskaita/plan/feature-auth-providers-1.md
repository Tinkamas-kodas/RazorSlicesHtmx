 ---
goal: Authenticate Users via Microsoft Entra ID or Google with Email Identity and Admin-assigned Roles
version: 1.0
date_created: 2026-10-10
last_updated: 2026-10-10
owner: Apskaita
status: 'Planned'
tags: [feature, authentication, authorization, entra, google, oidc, roles]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Add real authentication to Apskaita, which currently has none wired up (`Program.cs` registers no
authentication). Users sign in with **Microsoft Entra ID** or **Google** — both providers offered on one
login page, user picks. The **email asserted by the provider is the identity**: on first sign-in with an
unknown email, a `User` row is auto-provisioned **without a role**; an **admin assigns the role**
(`admin` or `user`) in the existing Users registry. No domain/tenant restriction on who may sign in.

## 0. Shared Language

| Term | Definition |
|---|---|
| **Identity email** | The `email` claim from the provider's ID token, normalized (trim + lowercase, invariant). The unique business key of `User`. |
| **Subject id** | The provider's stable user id (`oid` for Entra, `sub` for Google). Stored for audit/linkage; **not** the identity key. |
| **Provision** | Creating a `User` row on first successful sign-in with an unknown email. Provisioned row: `Role = null`, `IsActive = true`. |
| **Roleless** | A provisioned user with no role yet. Can sign in, sees only the "pending access" page; no feature navigation. |
| **Admin** | Role `admin`: full access including all `/admin/*` registries and role assignment. |
| **User** | Role `user`: operational features (time tracking and, later, invoices/work acts). Not `/admin/*`. |
| **Bootstrap admin** | An email listed in config `Auth:AdminEmails`; on sign-in, if the user has no role, it is set to `admin`. Solves the chicken-and-egg of "who is the first admin". |

## 1. Requirements & Constraints

- **REQ-001**: Sign-in must be offered via exactly two external providers: Microsoft Entra ID (OpenID Connect) and Google (OpenID Connect), selectable on one login page.
- **REQ-002**: The session is an ASP.NET Core **cookie** authentication scheme; both providers sign into it. No local passwords.
- **REQ-003**: The provider-asserted email (normalized lowercase) is the unique identity key of `User`.
- **REQ-004**: First sign-in with an unknown email auto-provisions a `User` row (`Role = null`, `IsActive = true`, `DisplayName` from claim, provider + subject id stored).
- **REQ-005**: A `User` with `IsActive = false` must be rejected at sign-in (no session issued, "deactivated" message).
- **REQ-006**: Roles are exactly `admin` and `user`; assignment happens only by an admin in the Users registry (list → set role).
- **REQ-007**: Bootstrap admin: an email in config `Auth:AdminEmails` gets `Role = admin` automatically on sign-in when the user has no role.
- **REQ-008**: Every feature endpoint requires an authenticated user; `/admin/*` additionally requires role `admin`; operational features require role `user` **or** `admin`.
- **REQ-009**: Roleless users authenticate successfully but see only a "pending access" page; navigation is empty (nav filtering via `NavigationItem.AuthorizationPolicy` already supported by the library).
- **REQ-010**: Unauthenticated requests redirect to `/account/login`; HTMX requests must use `HX-Redirect` so fragment calls do not swap the login page into a detail pane.
- **REQ-011**: The side nav shows the signed-in user's email/display name and a Sign out action (POST, antiforgery-protected, cookie sign-out).
- **REQ-012**: `requirements.md` FR-1 ("authentication only via Azure AD") and §6 must be updated to reflect two providers + roles.
- **SEC-001**: Never trust an email without a valid provider-signed ID token; validation is delegated to the OIDC handlers (issuer/signature/nonce). No custom token parsing.
- **SEC-002**: Session cookie: `HttpOnly`, `SameSite=Lax`, `SecurePolicy = SameAsRequest`; `returnUrl` must be a local path (open-redirect protection).
- **SEC-003**: Client secrets come from configuration (user-secrets in dev, env/secret store in prod) — never committed.
- **CON-001**: No changes to the RazorSlicesHtmx library are required: `app.MapGroup(...)` already returns `RouteGroupBuilder` (supports `.RequireAuthorization()`), and `AdminNavItem`/`NavigationItem` already carry `AuthorizationPolicy`.
- **CON-002**: `User.EntraObjectId` is repurposed as a provider-agnostic `SubjectId`; EF migration renames the column, drops its unique index, and adds a unique index on `email`.
- **CON-003**: Existing integration tests (56) must keep passing: `ApskaitaApiFactory` gets a stub authentication scheme so tests run as a configurable principal (default: admin).
- **CON-004**: Login, logout, pending, and the OIDC callback endpoints are anonymous/cookie-only — they must not require a role.
- **GUD-001**: Use plain `AddOpenIdConnect` (Entra, authority `https://login.microsoftonline.com/{Auth:Entra:TenantId}`, default `common`) and `AddGoogleOidc` — no `Microsoft.Identity.Web` dependency needed for this scope.
- **GUD-002**: Provisioning/role/claims logic lives in **one** shared hook (`AccountProvisioner` invoked from `CookieAuthenticationEvents.OnSigningIn`), so both providers behave identically.
- **GUD-003**: Follow existing feature-module conventions: new `Features/Account/` module (Endpoints/Models/Slices) registered via `builder.Services.AddFeature<AccountEndpoints>("/account")`.
- **PAT-001**: Login page is a full-page RazorSlice (no HTMX swap); provider buttons are plain links to `/account/challenge/{scheme}?returnUrl=…` — challenge endpoints are GET with antiforgery-free redirect initiation per OIDC convention.
- **PAT-002**: Role assignment in Users registry follows the existing HTMX pattern: fragment dialog/inline select + OOB row update + toast.

## 2. Implementation Steps

### Implementation Phase 1 — Identity schema (entities + migration)

- GOAL-001: `User` is keyed by unique normalized email and carries provider/subject/role.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | `src/Apskaita.Entities/User.cs`: rename `EntraObjectId` → `SubjectId`; add `Provider` (string, `"entra"`/`"google"`); make `Email` required; add `Role` (nullable string, `"admin"`/`"user"`). | | |
| TASK-002 | `Mapping/ApskaitaEntityModelBuilderExtensions.cs`: map `subject_id`, `provider`, `role` columns; `email` required + unique index `ux_users_email`; drop unique index on subject id; keep `FieldLengths` entries consistent. | | |
| TASK-003 | EF migration `AuthIdentity` in `Apskaita.Data.Migration`: rename column, add new columns, backfill (`email = lower(trim(email))`, `provider = 'entra'`, `subject_id` from old values), replace indexes. Handle existing rows without email by failing loudly in a documented script step (or backfill placeholder) — decide at migration time against actual data. | | |

### Implementation Phase 2 — Authentication plumbing

- GOAL-002: Cookie session with two OIDC providers and shared provisioning.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-004 | `Apskaita/Directory.Packages.props`: add `Microsoft.AspNetCore.Authentication.OpenIdConnect` and `Microsoft.AspNetCore.Authentication.Google` at the latest stable 10.0.x (match ASP.NET 10.0.12 line); reference in `Apskaita.Web.csproj`. | | |
| TASK-005 | `Program.cs`: `AddAuthentication` — default scheme cookie (`Apskaita.Cookie`), `LoginPath=/account/login`, `LogoutPath=/account/logout`, `AccessDeniedPath=/account/denied`, cookie policy per SEC-002; register `AddOpenIdConnect` (Entra: client id/secret from `Auth:Entra:*`, authority from tenant, `common` default, `responseType=code`, `GetClaimsFromUserInfoEndpoint=false`, scopes `openid profile email`) and `AddGoogleOidc` (`Auth:Google:*`). Both set `SignInScheme = cookie`, distinct `CallbackPath`/`SignedOutCallbackPath`. | | |
| TASK-006 | `Apskaita.Web/Features/Account/Services/AccountProvisioner.cs`: given normalized email, display name, provider, subject id → find `User` by email; create if missing (`Role=null`, `IsActive=true`); reject when `IsActive=false`; if `Role=null` and email ∈ `Auth:AdminEmails` → set `admin`; update `LastLoginAtUtc`, `Provider`, `SubjectId`; return role for claims. | | |
| TASK-007 | `Apskaita.Web/Features/Account/Authentication/CookieEvents.cs`: `CookieAuthenticationEvents.OnSigningIn` — run `AccountProvisioner` against the principal's email claim (fail closed if email claim missing), replace identity with claims: `Name`=email, `DisplayName`, `ClaimTypes.Role` (when role present), provider id claim; `OnRedirectToLogin` — emit `HX-Redirect` header for HTMX requests instead of 302. | | |
| TASK-008 | Policies in `Program.cs`: `"Admin"` = `RequireRole("admin")`; `"Operational"` = `RequireRole("user","admin")`; fallback policy `RequireAuthenticatedUser()` for MVC/endpoint routing. `app.UseAuthentication()` before `UseAntiforgery`/`UseHtmxErrorHandling` ordering verified. | | |
| TASK-009 | `appsettings.json` + `appsettings.Development.json`: `Auth` section (`Entra:TenantId`, `Entra:ClientId`, `Entra:ClientSecret`, `Google:ClientId`, `Google:ClientSecret`, `AdminEmails`); secrets via user-secrets in dev; README documents app registration steps (redirect URIs `/account/signin-entra`, `/account/signin-google`). | | |

### Implementation Phase 3 — Account feature UI + role assignment

- GOAL-003: Login/pending/logout pages and admin role editing.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-010 | `Features/Account/Endpoints/AccountEndpoints.cs` (`AddFeature<AccountEndpoints>("/account")`): `GET login` (anonymous, `_LoginPage` slice with two provider buttons), `GET challenge/{scheme}` (anonymous → challenge to provider with validated local `returnUrl`), `POST logout` (cookie sign-out → provider logout URLs where supported, then `/account/login`), `GET pending` (authenticated, roleless landing). | | |
| TASK-011 | `Slices/_LoginPage.cshtml` + `Slices/_PendingPage.cshtml`: Bootstrap-styled, no nav shell (anonymous layout variant); pending page explains "awaiting role assignment" + Sign out. | | |
| TASK-012 | `_SideNav.cshtml`: footer block with signed-in display name/email + Sign out POST button (uses existing antiforgery meta token). | | |
| TASK-013 | Users registry: add role editing — row shows current role badge; admin action (dialog or inline select) `POST set-role/{id}` with `Admin` policy, validating role ∈ {user, admin}; OOB row update + toast; `UsersContentService` gains `SetRoleAsync`. | | |

### Implementation Phase 4 — Authorization enforcement across features

- GOAL-004: Every feature gated; nav filtered by role.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-014 | In each feature's `MapEndpoints(RouteGroupBuilder group)`: `group.RequireAuthorization("Admin")` for the seven `/admin/*` modules, `"Operational"` for time-tracking (and future invoice features). Account module endpoints stay anonymous/cookie-only per CON-004. | | |
| TASK-015 | Nav policies: `AdminNavItem` entries for admin features get `AuthorizationPolicy: "Admin"`; time-tracking gets `"Operational"` — nav then self-filters for roleless users (empty nav + pending page). | | |
| TASK-016 | Root behavior: `GET /` for authenticated roleless user → `/account/pending`; unauthenticated → login (via fallback policy + `LoginPath`). | | |

### Implementation Phase 5 — Tests + docs

- GOAL-005: Auth-aware test factory and new auth tests; documentation updated.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-017 | `ApskaitaApiFactory`: `ConfigureTestServices` replaces default authenticate scheme with a stub handler returning a configurable principal (default admin role); expose per-test principal override so tests can simulate roleless/user/admin. Existing 56 tests pass unchanged under admin principal. | | |
| TASK-018 | New tests: provisioning creates roleless user with normalized email + subject; `AdminEmails` bootstrap grants admin on first sign-in; deactivated user is rejected; `user` role → 200 on time-tracking, 403 on `/admin/clients`; roleless → pending only; HTMX unauthenticated → `HX-Redirect` header. | | |
| TASK-019 | Update `requirements.md` FR-1/§3/§6 (two providers, email identity, roles) and `README`/setup docs with app-registration + config notes. | | |

## 3. Acceptance Criteria

- **AC-001**: `GET /` unauthenticated → 302 `/account/login`; login page renders Microsoft and Google buttons.
- **AC-002**: `GET /account/challenge/entra` → 302 to `login.microsoftonline.com/…`; `…/google` → 302 to Google OAuth; invalid scheme → 400.
- **AC-003**: A valid provider ticket for a new email creates a `User` row (email lowercased, `Role=null`, `IsActive=true`, provider + subject stored) and issues the session cookie.
- **AC-004**: Roleless session: `/` → pending page; nav contains no items; `/time-tracking` → 403; `/admin/users` → 403.
- **AC-005**: Admin assigns `user` → that user sees time-tracking (200) and no admin nav; admin sees everything including `/admin/*`.
- **AC-006**: Deactivated `User` cannot sign in: provider round-trip ends without session cookie; retrying does not create/modify rows beyond `LastLoginAtUtc` untouched.
- **AC-007**: Same email via Google then Entra reuses the **same** `User` row (email identity); `Provider`/`SubjectId` reflect the latest sign-in.
- **AC-008**: Unauthenticated HTMX fragment request → `HX-Redirect` to login (not a login page swapped into the detail pane).
- **AC-009**: `dotnet build` clean; all existing 56 Apskaita tests + RazorSlicesHtmx suites pass.

## 4. First Verification Check

Before building UI: wire Phase 2 plumbing, then run the app with a **test-only fake provider handler** (a stub `AuthenticationHandler` registered under the Entra scheme name in a dev/test configuration) and assert AC-003 + AC-004 end-to-end: fake ticket → provisioned row → roleless pending → admin bootstrap via `Auth:AdminEmails`. This disproves the approach cheaply if provisioning, claim mapping, or cookie events misbehave.

## 5. Assumptions & Open Questions

| # | Item | Assumption / Status |
|---|---|---|
| A-1 | Bootstrap admin mechanism | **Assumed**: config `Auth:AdminEmails` list (auto-grants admin on sign-in when roleless). Alternative considered: EF seed migration with a fixed email — rejected (needs schema data coupling). |
| A-2 | Roleless experience | **Assumed**: pending page only, no data access. |
| A-3 | Admin ⊇ user access | **Assumed**: admin can do everything a `user` can (Operational policy includes both roles). |
| A-4 | Cross-provider merge | **Assumed**: same email = same person; no explicit account-linking UI; latest provider/subject overwrites. |
| A-5 | Session lifetime | **Assumed**: cookie defaults (sliding 8h). |
| A-6 | Provider sign-out | **Assumed**: local cookie sign-out only in v1; provider logout (id_token_hint) as a follow-up. |
| Q-1 | Existing `users` table data | Check actual rows before writing the migration backfill (email-less rows have no sane default). |
| Q-2 | Entra tenant choice | `common` (any org + personal MS accounts) assumed per "no restriction"; if only work accounts, use the tenant id or `organizations`. |
