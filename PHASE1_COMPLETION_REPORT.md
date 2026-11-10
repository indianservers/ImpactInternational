# Phase 1 completion report

Verified on 10 November 2026 (Asia/Kolkata).

Status: the native Phase 1 core is implemented and operational. It is **not yet cleared for a production rollout**; the verification boundaries and remaining work below are deliberate delivery disclosures. No future module is represented as a working feature.

## 1. Implemented features

- React/TypeScript/Vite application with a persistent shell, responsive navigation, lazy page bundles, cached/deduplicated requests, background refresh, debounced search, skeletons and bounded server pages.
- ASP.NET Core 10 API with EF Core/Npgsql, UUID keys, PostgreSQL timestamps, eight migrations, schema export, OpenAPI, structured logs and correlation IDs.
- Personal signup, verification and resend, sign-in/out, password recovery/reset/change, server-side session expiry/revocation, profile and account preferences. No automatic administrator grant on signup.
- Platform-operator provisioning and review of public association registration requests; two fictional associations with different configurable hierarchies.
- Organization creation/editing, lazy tree expansion/collapse, breadcrumbs, search/type/status filters, details, club directory, sorting and pagination.
- Normalized atomic permission grants, scoped role assignments, unassigned-role editing, role assignment/revocation and workspace switching with backend enforcement.
- Association identity/preferences/brand accent, organizational level creation, scoped business audit and separate authentication audit.
- Durable PostgreSQL SMTP outbox with retries, concurrent worker claiming, one-time token consumption, and expired-session/link maintenance.
- Container definitions, reverse-proxy configuration, environment example, operator bootstrap, startup documentation, CI workflow and eight-phase roadmap.

## 2. Database tables

The database contains 18 application tables and EF migration history. EF uses consistently quoted PascalCase SQL identifiers. These are the requested business concepts, not parallel duplicate models:

- Identity: `Users`, `UserProfiles`, `Sessions`, `AuthenticationAudits`.
- Associations/organizations: `Tenants`, `Associations`, `Levels`, `Orgs`, `OrganizationSettings`.
- Authorization: `Roles`, `Permissions`, `RolePermissions`, `Assignments`.
- Configuration/audit: `SystemSettings`, `TenantSettings`, `Audits`.
- Operations: `Mails` (security mail outbox), `RegistrationRequests`.
- Migration tracking: `__EFMigrationsHistory`.

Tenant-aware composite foreign keys cover organizational parents, types, assignments and organization settings. Unique tenant/code and tenant/role-name constraints, scoped list indexes, a partial due-mail index and a name-search trigram index are included. Database triggers enforce hierarchy/type rules and reject immutable level/tenant changes. PostgreSQL adjacency lists with recursive queries avoid a hardcoded association structure.

## 3. API endpoints

The generated `docs/openapi.json` is authoritative. Clubs share the organization aggregate via `GET /api/v1/organizations?clubs=true`; separate duplicate club models/APIs were not introduced. The development mail viewer is absent outside Development.

- `GET /health`
- `GET /api/v1/auth/csrf`
- `POST /api/v1/auth/register`
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/resend-verification`
- `POST /api/v1/auth/forgot-password`
- `POST /api/v1/auth/complete`
- `GET /api/v1/auth/me`
- `POST /api/v1/auth/logout`
- `GET /api/v1/auth/sessions`
- `DELETE /api/v1/auth/sessions/{id}`
- `PUT /api/v1/users/profile`
- `POST /api/v1/auth/change-password`
- `GET /api/v1/workspaces`
- `POST /api/v1/workspaces/{id}/activate`
- `GET /api/v1/dashboard`
- `GET /api/v1/organizations`
- `POST /api/v1/organizations`
- `GET /api/v1/organizations/{id}`
- `PUT /api/v1/organizations/{id}`
- `GET /api/v1/levels`
- `POST /api/v1/levels`
- `GET /api/v1/roles`
- `POST /api/v1/roles`
- `GET /api/v1/permissions`
- `PUT /api/v1/roles/{id}`
- `GET /api/v1/assignments`
- `POST /api/v1/assignments`
- `GET /api/v1/settings`
- `PUT /api/v1/settings`
- `GET /api/v1/audit-logs`
- `POST /api/v1/associations`
- `POST /api/v1/registration-requests/{id}/provision`
- `DELETE /api/v1/assignments/{id}`
- `GET /api/v1/development/mail`
- `POST /api/v1/registration-requests`
- `GET /api/v1/registration-requests`

## 4. UI screens

Public landing; association registration request; signup; sign-in; recovery/reset/verification and resend; dashboard; organization explorer and create/edit drawer; club directory and details; roles/permission matrix and assignments; workspace switcher; association settings/levels; audit log; profile, preferences and security sessions. Upcoming modules are explicitly marked inactive in navigation. Keyboard dialog focus trapping/return and Escape handling are shared. Native controls and icons have accessible names.

Browser review exercised landing/sign-in, workspace activation/persistence, live dashboard, club directory, organization drawer and lazy tree expansion. Desktop 1440-pixel and mobile 390-pixel viewports were reviewed. A mobile overflow defect was corrected. `docs/dashboard.png` is the saved native-browser evidence. Recent profile/preference and administrative-action additions compile and have API coverage where applicable; they have not all received a fresh browser walkthrough.

## 5. Test results

- `npm run build`: PASS; TypeScript/Vite frontend build and .NET build, zero .NET warnings/errors at the final build.
- `dotnet publish server -c Release`: PASS during verification; container images themselves were not built.
- `tests/integration.py`: **60/60 passing** against a real, isolated PostgreSQL `clubs_test` database. Retained evidence: `docs/api-verification.json`.
- Checks cover registration/verification/resend, login/logout/lockout, password reset, token replay, session expiry/revocation, hierarchy/type/duplicate validation, cross-tenant and sibling access denial, unauthorized writes, archive permission, rejected mutation rollback, role creation/edit/assignment/revocation, escalation prevention, workspace activation/revocation, registration request provisioning/idempotency, audit generation, concurrent writes, CSRF, migration history and OpenAPI availability.
- `tests/smtp.py`: PASS against an actual local SMTP protocol sink. The PostgreSQL outbox delivered a verification email; this does not verify a production mail provider.
- `tests/benchmark.py`: PASS with a rollback-only 10,000-club PostgreSQL fixture and retained execution plans.
- Dependency audit: no known vulnerable NuGet packages, including transitives, from configured sources; npm audit reported zero vulnerabilities when checked.
- Eight forward migrations applied to local PostgreSQL. Fresh demo/test creation and subsequent migration upgrades were exercised. Generated downgrade methods were not validated; use tested backups and a roll-forward correction for production rollback until downgrade data preservation is reviewed.
- The GitHub Actions workflow is supplied but has not run on hosted CI. There is no separate pure-unit-test suite or committed browser regression harness; current executable coverage is integration/API/database/SMTP plus tool-driven browser review.

## 6. Performance measurements

Final local API benchmark: 100 sequential warmed organization-list requests, median **5.59 ms**, p95 **7.45 ms** end to end over loopback HTTP on the small seed/integration dataset. The connection/query pool was warm. This is not a concurrent-user or production-scale API load test.

Database benchmark: 10,000 additional fictional clubs in a rollback-only transaction, 30 measured warm scoped recursive list queries after discarding the first sample. `EXPLAIN (ANALYZE, BUFFERS)` measured median **20.930 ms**, p95 **22.246 ms** database execution. Full plans are retained in `docs/explain-plans.txt`; benchmark conditions are in `docs/database-benchmark.json`. The fixture setup needs a longer statement timeout; the normal read timeout is 15 seconds. No member-scale table exists yet.

Final frontend main bundle: approximately 311.16 kB uncompressed / 98.65 kB gzip, with separate page chunks; main CSS approximately 27.38 kB / 7.18 kB gzip. Browser app-shell navigation latency was not instrumented, so the 100 ms visual-navigation target is not claimed as measured.

## 7. Security checks

- Opaque hashed server sessions, HttpOnly/SameSite Strict cookies, Secure cookies outside Development, established password hashing and generic account recovery responses.
- CSRF on mutations; login IP limiting; atomic account failure counters and lockout; request body size limit; security headers and nginx CSP.
- Workspace selection validated against the signed-in user. Backend permission checks, subtree isolation and tenant composite foreign keys block changed-ID attacks.
- Association-wide settings/role definitions require headquarters scope. Role grants cannot exceed the caller's grants. Assigned roles cannot silently change permissions. Role edits and assignments share transaction locking.
- Hierarchy writes are transactional and tenant-serialized. Version tokens and real simultaneous-write tests protect against lost updates.
- Security tokens, passwords and SMTP credentials are excluded from business audit and structured request logs. Denied authenticated access is recorded through an independent context, so denial logging cannot persist rejected business edits.
- Development credentials are generated into ignored local state. Development seeding is refused outside Development. Production platform authority requires explicit operator action on a verified account.

## 8. Remaining defects, limitations and deployment gates

- Docker/Compose execution and hosted CI are unverified because Docker was unavailable and no repository/CI destination was supplied. Validate nginx, container health/startup, non-root volume permissions and the final TLS/proxy topology in the target environment.
- Real SMTP provider credentials, TLS negotiation, deliverability and production secrets are not configured. Only local SMTP transport is verified. Public launch also needs reviewed legal terms/privacy text; current signup notice explicitly identifies a development service.
- OIDC login and MFA enrollment/challenges are architecture extension points, not implemented authentication options. Privileged production rollout must define and implement the required MFA policy.
- The IP limiter is per application replica. Configure trusted forwarded headers and a shared limiter for the real proxy topology before scale-out. The local PostgreSQL cluster uses loopback-only development trust authentication and PostgreSQL 17.6; use a maintained patched deployment with password/TLS and restricted runtime/migration roles in production.
- UI translations, optional dark theme, branding file upload, a complete accessibility audit, and browser regression automation are not included. Language/time-zone preferences and a brand accent are stored; translations are not implied.
- Each level permits one parent level. Existing level/parent rules and unit levels are immutable to protect live data. A future controlled restructuring workflow is needed for changing those rules. Assigned-role definitions are likewise immutable; replacement roles can be created and assignments changed.
- Assignment/scope selection currently presents a bounded first 100 units/assignments; large-association administration needs a paginated searchable scope picker. Parent selection in the organization form is searchable. Large lists are bounded rather than virtualized; deep offset pagination should become cursor-based for future member-scale datasets.
- Audit retention/tamper-evident exports, production backup/restore drills, migrations downgrade review, distributed load/failure tests and 100,000-member performance evidence remain operational work. There are no known failing checks in the retained final integration run, but these untested areas are not represented as passed.

## 9. Phase 2 dependencies

Use existing tenant, association, organizational-unit, user, permission, audit and outbox boundaries. Add separate person/membership/application/invitation concepts with tenant-aware keys. Establish officer-term dates independently of account access. Add cursor pagination/import staging for larger datasets. Resolve production identity/MFA, deployment, legal and recovery policies before onboarding real members. Detailed module boundaries and phases are in `EIGHT_PHASE_ROADMAP.md` and `docs/ARCHITECTURE.md`.
