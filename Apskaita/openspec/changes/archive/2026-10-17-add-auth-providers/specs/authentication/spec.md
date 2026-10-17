# Spec Delta

## Purpose

Defines how users authenticate through external OpenID Connect providers, how their identity and role are established, and how access to features is enforced.

## ADDED Requirements

### Requirement: Sign in with two external providers

The system SHALL offer sign-in through exactly two OpenID Connect providers, Microsoft Entra ID and Google, selectable on one login page, and establish the session as a cookie authentication scheme.

#### Scenario: Login page

- **WHEN** an unauthenticated user opens the login page
- **THEN** both Microsoft and Google options are shown
- **AND** choosing one redirects to that provider's authorization endpoint

#### Scenario: Invalid provider

- **WHEN** a sign-in challenge names an unknown provider scheme
- **THEN** the request is rejected with HTTP 400

### Requirement: Login offers only configured providers

The login page SHALL render a provider button only for a provider whose credentials are configured, so an unconfigured provider is never offered.

#### Scenario: Provider not configured

- **WHEN** a provider has no client id configured
- **THEN** the login page does not render that provider's button

#### Scenario: No provider configured

- **WHEN** neither provider has credentials configured
- **THEN** the login page shows a message that no sign-in providers are available

### Requirement: Email is the identity

The system SHALL use the provider-asserted email, normalized by trimming and lowercasing, as the unique identity key of a `User`, and SHALL reuse the same `User` row when the same email signs in through either provider.

#### Scenario: Same email via different providers

- **WHEN** a user signs in with Google and later with the same email via Entra
- **THEN** the same `User` row is used
- **AND** its provider and subject id reflect the most recent sign-in

### Requirement: Auto-provision roleless users

On first successful sign-in with an unknown email, the system SHALL create a `User` with no role, active status, a display name from the provider claim, and the provider and subject id stored.

#### Scenario: First sign-in

- **WHEN** a valid provider ticket presents an email with no existing `User`
- **THEN** a roleless active `User` is created and a session is issued

### Requirement: Reject deactivated users

The system SHALL refuse to issue a session for a `User` whose account is deactivated.

#### Scenario: Deactivated user signs in

- **WHEN** a valid provider ticket resolves to a `User` with `IsActive = false`
- **THEN** no session is issued and a deactivated message is shown

### Requirement: Bootstrap admin from configuration

The system SHALL grant the `admin` role automatically on sign-in to a roleless user whose email is listed in the configured admin-emails.

#### Scenario: Configured admin signs in

- **WHEN** a roleless user whose email is in the admin-emails list signs in
- **THEN** their role is set to `admin`

### Requirement: Roles are assigned only by an admin

The system SHALL recognize exactly the roles `admin` and `user`, and SHALL allow role assignment only by an `admin` through the Users registry.

#### Scenario: Admin assigns a role

- **WHEN** an admin sets a user's role to `user` or `admin`
- **THEN** the change is persisted and reflected on that user's next authorization check

### Requirement: Enforce authorization on features

The system SHALL require an authenticated user for every feature endpoint, require role `admin` for administrative routes, and require role `user` or `admin` for operational features.

#### Scenario: User role reaches an admin route

- **WHEN** a user with role `user` requests an administrative endpoint
- **THEN** the request is rejected with HTTP 403

#### Scenario: Roleless user reaches a feature

- **WHEN** a roleless authenticated user requests an operational feature
- **THEN** the request is rejected and only the pending-access page is available

### Requirement: Redirect unauthenticated requests appropriately

The system SHALL redirect unauthenticated full-page requests to the login page and, for HTMX fragment requests, emit a redirect header so the login page is not swapped into a content pane.

#### Scenario: Unauthenticated HTMX fragment

- **WHEN** an unauthenticated HTMX fragment request is received
- **THEN** the response carries a redirect header to the login page rather than login HTML

### Requirement: Return URL is local

The system SHALL accept only a local path as a post-login return URL, to prevent open redirects.

#### Scenario: External return URL

- **WHEN** a sign-in request carries an absolute external return URL
- **THEN** it is not honored as a redirect target

### Requirement: Session cookie hardening

The session cookie SHALL be HTTP-only, SameSite=Lax, and secured per request scheme.

#### Scenario: Cookie attributes

- **WHEN** a session cookie is issued
- **THEN** it is marked HTTP-only and SameSite=Lax
