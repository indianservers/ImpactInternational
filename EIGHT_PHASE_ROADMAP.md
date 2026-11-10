# Eight-phase delivery roadmap

## Phase 1 — Foundation

Implemented: React shell and public/authentication pages; PostgreSQL/EF migrations; identity/profile/session storage; verification/reset outbox; association provisioning; configurable hierarchy; scoped RBAC; organization and club management; workspace switching; settings; audit; profile/security; database/API tests; query benchmark; native browser review; container definitions.

Remaining Phase 1 hardening is identified in `PHASE1_COMPLETION_REPORT.md`. This file does not label unverified deployment, MFA, localization, or unrestricted hierarchy restructuring as complete.

## Phase 2 — People, membership and onboarding

Implemented: global person profiles and privacy; multiple scoped affiliations; staged versioned applications with quorum approvals; secure invitations and reviewed account linking; lifecycle/renewal/administrative holds; reviewed and scheduled transfers; CRM and opt-in directory; digital identity and revocable QR; staged CSV/XLSX imports and privacy-aware exports; self-service portal; membership intelligence; transactional outbox notifications; document/import/verification retention; PostgreSQL integration tests and a measured 100,000-person fixture.

See `PHASE2_COMPLETION_REPORT.md` for measurements, limits and delivery evidence. Operational work includes provider SMTP/TLS checks, concurrent-load and disaster-recovery exercises, malware scanning for untrusted documents, and Phase 1 privileged identity hardening. Relationship storage is reserved; family-relationship administration is not exposed. Existing membership leadership snapshots remain labeled migration evidence; Phase 3 now supplies explicit officer appointments and preserved term history.

## Phase 3 — Club administration, leadership, committees and governance

Implemented: club lifecycle/profile approvals and controlled restructuring; configurable officer positions; annual, fiscal and custom calendars; eligible-member appointments, interval capacity and effective-term role sources; immutable leadership history; checklist-based independently approved handovers; committee roles, memberships and workspaces; one shared versioned quorum approval engine with controlled delegation; resolutions and private governance documents; scoped club/district/national dashboards and configurable club health. Three additive PostgreSQL migrations preserve the Phase 1–2 foundation.

See `PHASE3_COMPLETION_REPORT.md` for evidence and limits. The user's Phase 3 governance brief supersedes the earlier ordering. Elections, provider deployment checks, document malware scanning and concurrent production load validation are not claimed complete.

## Phase 4 — Events

Add events, registrations, eligibility snapshots, attendance and event-specific approvals. Reuse PersonId, MembershipId, the authoritative organization tree, current effective office checks, scoped permissions, shared approval engine and outbox. Keep registration requests distinct from event registration. Define event privacy and retention before exposing participant lists. Existing committee event surfaces must remain informational until actual event records exist.

## Phase 5 — Communication

Add consent/preferences, recipient segmentation, campaigns and delivery tracking. Expand the existing transactional outbox with explicit job types, provider throttling and idempotency. Isolate security mail from bulk campaigns.

## Phase 6 — Projects and meetings

Add service projects, volunteer assignments, meeting agendas, recurrence, participation evidence and impact measures. Reuse committee responsibilities and organizational scope; retain source provenance and privacy controls.

## Phase 7 — Finance and advanced governance

Add dues, invoices, receipts, payments, reconciliation and an immutable ledger. Use provider idempotency, explicit money/currency types, dedicated permissions and compliance review. Advanced elections require a separate designed voting protocol; existing appointment approvals do not imply secret ballots or election administration.

## Phase 8 — Reporting, integrations and scale

Add permission-aware reporting projections, external integrations, exports, observability dashboards, disaster recovery, accessibility review and load testing. Measure member-scale workloads before adding Redis caches, closure tables or materialized aggregates. Keep tenant boundaries and normalized source tables intact.
