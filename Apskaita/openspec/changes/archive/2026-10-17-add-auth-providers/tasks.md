# Tasks

## 1. Identity schema

- [x] 1.1 Update `User` (rename `EntraObjectId` → `SubjectId`; add `Provider`, required `Email`, nullable `Role`) and verify the entity compiles
- [x] 1.2 Map `subject_id`, `provider`, `role`; add unique index on `email`; drop the subject-id unique index, and verify a model build produces the expected columns
- [x] 1.3 Ensure the schema carries `SubjectId`/`Provider`/`Role` and the unique email index (delivered via `InitialCreate`; no separate rename migration needed) and verify it applies against dev MySQL
- [x] 1.4 Add unit tests for email normalization and the provisioning decision, and verify they pass

## 2. Authentication plumbing

- [x] 2.1 Add the OpenIdConnect and Google authentication packages to central package management and reference them in `Apskaita.Web`, and verify restore succeeds
- [x] 2.2 Register the cookie default scheme plus the two OIDC providers with the SEC-002 cookie policy and distinct callback paths, and verify the app starts and `/account/login` renders the configured providers
- [x] 2.3 Implement `AccountProvisioner` (find-or-create by normalized email, reject inactive, bootstrap admin from config, update last-login/provider/subject) and verify it with unit tests
- [x] 2.4 Wire `CookieAuthenticationEvents.OnSigningIn` to run provisioning and emit claims, and `OnRedirectToLogin` to send the HTMX redirect header, and verify with an integration test
- [x] 2.5 Define the `Admin` and `Operational` policies and the authenticated fallback policy, and verify a misconfigured pipeline fails at startup
- [x] 2.6 Add the `Auth` configuration section (Entra/Google ids and secrets, `AdminEmails`) with dev user-secrets, and verify the app reads it

## 3. Account feature UI

- [x] 3.1 Add `AccountEndpoints` (`/account`) with login, challenge/{scheme}, logout, and pending, and verify each route responds
- [x] 3.2 Add the login and pending slices (anonymous layout variant) and verify they render without the nav shell
- [x] 3.3 Add the signed-in user block and Sign out action to the side nav, and verify sign-out clears the session
- [x] 3.4 Add role assignment to the Users registry (list → set role, `Admin` policy, OOB row update + toast) and verify an admin can change a role end-to-end

## 4. Authorization enforcement

- [x] 4.1 Apply `Admin` to the `/admin/*` route groups and `Operational` to time-tracking, leaving account routes anonymous/cookie-only, and verify role-based 200/403 responses
- [x] 4.2 Set navigation authorization policies so roleless users see an empty nav, and verify nav filtering
- [x] 4.3 Implement root routing (roleless → pending, unauthenticated → login), and verify both redirects

## 5. Test host and docs

- [x] 5.1 Give `ApskaitaApiFactory` a stub authentication scheme with a configurable principal (default admin), and verify the existing suite passes unchanged
- [x] 5.2 Add auth tests (provisioning, bootstrap admin, deactivated rejection, role access, HTMX redirect, conditional login providers), and verify they pass
- [x] 5.3 Update `requirements.md` FR-1/§3/§6 and the README app-registration/config notes, and verify the documented redirect URIs match the code

## Workflow follow-up

- Archive the change after review, syncing the `authentication` capability into `openspec/specs/`.
