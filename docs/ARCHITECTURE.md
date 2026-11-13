# Phase 1 architecture

## Application boundaries

One modular monolith owns identity, authorization, associations, organizations and audit. React consumes versioned same-origin JSON endpoints. PostgreSQL is the business source of truth. EF Core migrations, an exported SQL script, and a separate production migration process manage schema changes. Future modules must reference existing tenant, organization and user keys rather than create parallel identity models.

Tenant is the isolation boundary and Association is its identity/configuration. EF entity splitting maps that single aggregate to `Tenants` and `Associations`; user identity and profile similarly map to `Users` and `UserProfiles`. This avoids duplicate application models while retaining separate relational tables. PostgreSQL identifiers consistently use EF's quoted PascalCase convention. UUID keys and UTC `timestamptz` timestamps are used; charter dates are date-only.

`SystemSettings`, `TenantSettings`, and `OrganizationSettings` reserve distinct configuration scopes. Tenant branding and basic preferences currently live in the association aggregate. Future flexible settings must not duplicate those canonical fields.

## Hierarchy and isolation

An adjacency list (`ParentId`) supports arbitrary association-specific level chains. `Levels.ParentLevelId` specifies the permitted parent type. A club is a configurable level with `IsClub`, never a hardcoded level name. The model supports different hierarchies across associations; one level currently permits one parent level. Multiple alternative parent types would require a relationship table and validation migration.

Composite `(TenantId, Id)` foreign keys on parents, levels, roles, assignments and organization settings prevent cross-tenant relationships. Recursive CTEs run inside PostgreSQL and compose with filters, projections and bounded pagination. Only expanded branches are loaded by the tree UI. No complete hierarchy is materialized in application memory.

Hierarchy writes take a transaction-scoped advisory lock per tenant. PostgreSQL triggers additionally reject self-links, invalid parent types, cycles, tenant moves and existing level relationship changes. Existing unit levels are immutable. Level names can be introduced through configuration; destructive restructuring needs an explicit migration workflow rather than silently invalidating children. Unit versions are optimistic concurrency tokens. Concurrent writes with the same version have one winner and one HTTP 409.

## Authentication and permissions

ASP.NET Identity's password hasher handles passwords. Browser sessions use 256-bit opaque random values in HttpOnly, SameSite Strict cookies; only SHA-256 session hashes are stored. Production cookies are Secure. Sessions expire after eight hours and are checked on every request. Password resets revoke every session; changing a password revokes other sessions. A cleanup worker removes expired sessions and erases stale mail links.

Antiforgery tokens protect all non-GET API routes, including login. Authentication routes have a per-IP fixed-window limiter and accounts lock after five failures. Generic login/recovery/registration responses reduce account disclosure. Forwarded-header trust and distributed rate limiting require deployment-specific configuration; the in-process limiter is per replica.

`Permissions`, `RolePermissions`, `Roles`, and `Assignments` form normalized RBAC. Every assignment has a tenant and organizational root. The session stores the active assignment ID; changing a client workspace ID never grants access. Permissions are read from the database, not inferred from role names. Active-assignment permissions apply to that workspace's descendants. Association-wide settings and role definitions additionally require a headquarters scope. Assignments can grant only permissions already held by the grantor and only within visible scope. Assigned roles cannot be edited in place; create an unassigned replacement role instead. Platform authority is a separately audited operator-managed flag and is not assignable through tenant RBAC.

No authentication or business state is persisted in browser local storage. React Query memory caching is cleared on workspace changes and sign-out. UI visibility uses server-returned grants; backend authorization remains authoritative. Mutations await server validation instead of optimistically changing security-sensitive hierarchy or permissions.

## Mail and background work

Verification/reset issuance and user changes share a database save; email work is durable in `Mails`. A hosted SMTP worker claims due rows using `FOR UPDATE SKIP LOCKED`, retries with exponential backoff, and clears sent URLs. Tokens are checked by hash, expire after one hour, and use an atomic consumption update within a transaction. An SMTP sink integration exercises actual delivery. Production SMTP credentials and deliverability require operator validation.

This is a narrowly scoped reliable outbox, not a general scheduling engine. Future campaigns should add dedicated job types, leases, delivery metrics, idempotency and volume controls. Redis is optional and was unavailable; no Redis dependency was introduced.

## Audit and observability

Authentication activity has its own table. Business audit events include actor ID, tenant, scope, action, resource, outcome and selected organization field changes. Passwords, cookies, verification tokens and SMTP credentials are excluded. Denied authenticated access uses an independent EF context so rejected requests cannot accidentally flush tracked mutations. API logs are structured JSON with method, path (no query string), elapsed time, response status and correlation ID. PostgreSQL slow-query logging and statement/lock timeouts are configured in the local/Compose database.

The audit API resolves actor and scope names and filters within the active subtree. There is no general-purpose audit event editor or delete endpoint. Retention/export policy and tamper-evident external storage remain deployment work.

## Performance choices and future interfaces

React keeps the app shell mounted across navigation, lazy loads page bundles, deduplicates and caches queries for 30 seconds, refreshes in the background, debounces search for 250 ms, and uses contextual skeletons. Tables have 20-row server pages; tree branches also page at 20, so large flat lists are bounded rather than rendered wholesale. Dedicated virtual scrolling is not included because current screens intentionally bound DOM size.

Npgsql provides connection pooling. Parameterized recursive queries use explicit parameter names to avoid provider-generated parameter collisions. Tenant/parent/name and tenant/level/status indexes match observed query patterns; a trigram GIN index supports organization-name substring search. Offset pages use a deterministic name/code plus UUID ordering. Cursor pagination should replace deep offsets for future member-scale tables. Current counts traverse the scoped hierarchy; consider closure tables or cached aggregates only after measured demand.

Future memberships reference person/user, tenant and club IDs with tenant-aware keys; officer terms attach dates to scoped assignments; finance introduces a separate transaction ledger and idempotency boundary; communications consume permission-scoped recipients and durable job interfaces. None of those future module tables or misleading working screens are included here.

OIDC providers can resolve external subjects into the existing user key before session creation. MFA belongs between password/provider verification and session issuance, with separate challenge state and recovery credentials. Neither external-provider login nor MFA enrollment/challenges are implemented in this phase; MFA is optional and is not a prerequisite for login or privileged production access.
