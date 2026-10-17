# Proposal

## Why

Apskaita currently registers no authentication, so every endpoint is open. Users need to sign in with a real identity and be limited to the features their role allows.

## What Changes

- Add cookie-session authentication with two OpenID Connect providers selectable on one login page: Microsoft Entra ID and Google.
- Treat the provider-asserted email (normalized lowercase) as the unique identity of a `User`; auto-provision a roleless `User` on first sign-in with an unknown email.
- Add roles `admin` and `user`, assigned only by an admin in the Users registry; a config bootstrap list grants `admin` to the first users.
- Enforce authorization on every feature endpoint and filter navigation by role; roleless users see only a "pending access" page.
- Repurpose `User.EntraObjectId` into a provider-agnostic `SubjectId` and add a unique index on `email` (**BREAKING** schema change to the `users` table).
- Deactivate a `User` to block sign-in.

## Out of scope

- Provider (single) logout: sign-out clears only the local app cookie and leaves the provider session intact. Deliberately excluded.

## Capabilities

### New Capabilities
- `authentication`: provider sign-in, email identity, auto-provisioning, roles, and authorization enforcement across features.

### Modified Capabilities
- _(none — no existing spec's requirements change)_

## Impact

- `Apskaita.Entities` `User` (new `Provider`, `Role`; `EntraObjectId` → `SubjectId`) and an EF migration renaming the column and adding a unique email index.
- `Apskaita.Web` auth setup (cookie + two OIDC schemes), policies, and an `Account` feature (login/pending/logout/challenge endpoints and slices).
- Users registry gains role assignment.
- New packages: `Microsoft.AspNetCore.Authentication.OpenIdConnect` and `Microsoft.AspNetCore.Authentication.Google`.
- Test host gains a stub authentication scheme so existing tests run as a configurable principal.
- `requirements.md` FR-1/§6 updated to reflect two providers and roles.
