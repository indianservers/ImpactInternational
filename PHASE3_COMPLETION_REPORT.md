# Phase 3 completion report

Delivered 11 November 2026: club administration, leadership, terms, committees and governance on the existing React / ASP.NET Core / EF Core / PostgreSQL application. The Phase 3 governance brief supersedes the earlier roadmap's events-first ordering. Phase 1 and Phase 2 were extended, not rebuilt.

## Implemented behavior

- Club directory with server search, pagination, status/country/ancestor filters, sorting, list/cards and responsive workspaces. Club profiles include identity, location, charter, operational settings, meeting schedule and policies. New clubs require governance activation; the legacy organization creation route also creates a pending proposal. Status transitions and mergers are controlled; closure/merger checks prevent orphaned active memberships. Profile approval policy and separate restructuring permission prevent silent hierarchy moves. Organization changes retain before/after history.
- Configurable officer positions for organizational levels: titles, codes, reporting relationships, maximum concurrent holders, mandatory offices, membership eligibility, minimum tenure, duration, approval policy and optional explicit application-role mapping. Reporting cycles are rejected; live definitions are frozen where necessary.
- Annual, fiscal and custom calendars with inherited terms. Eligible people come from existing Person and Membership records. Appointment proposals, independent approval/rejection, future activation, date-bound authority, revocation and completion preserve position/organization label snapshots and append-only leadership history. Capacity is checked across actual overlapping intervals, including positions allowing multiple holders.
- Handovers connect distinct outgoing/incoming approved officers in the same office. Recorded snapshots, responsibilities, checklist, notes and shared documents require incoming acceptance and independent approval; archived handovers and completed outgoing appointments remain available.
- Committee formation, configurable committee roles, member responsibilities, effective memberships, chair approval, removal history, activity and private document workspaces. A chair can manage ordinary membership in their own committee without acquiring organization administration or assigning leadership directly. Closing and leadership removal use governance review.
- Versioned, staged approvals with reviewer roles, quorum, revisions, withdrawal, independent decisions, optimistic concurrency and optional bounded delegation. Governance and membership use the same workflow/stage/decision tables and the same `ApprovalEngine.Record` implementation. Submitted workflows remain frozen; revisions retain old decisions and payloads. No requester, nominee or incoming handover subject can approve their own request.
- Governance resolutions with number, subject, text, meeting/effective dates and optional supporting document. Private PDF/PNG/JPEG documents have bounded upload size, signature/extension checks, authorized downloads, successor-sharing policy and retention cleanup. No public file URL is introduced.
- Club, district, national and international administrative views aggregate actual scoped records: club status, distinct active people, joining activity, pending work, current/expiring officers, vacancies and committees. Club health uses configurable, transparent weights for leadership coverage, membership threshold, governance backlog and profile completeness. Finance/events/attendance remain future capabilities.
- Ordinary member navigation remains simple. Personal leadership is displayed separately from application roles. Workspace switches clear scoped caches, close the mobile menu and navigate to the new dashboard without a full-page reload.

## Foundation and security

One organization hierarchy, global Person model, scoped membership model and RBAC engine remain authoritative. Tenant-qualified foreign keys and scoped lookups prevent cross-association references. Application access derives from explicit role assignments, never a display title. Office-sourced assignments retain their source appointment; request-time checks enforce the real current time, organization state, source membership and effective term even if the worker is delayed. Background imports recheck effective authority before committing. Removing an office source does not remove an independent manual role.

Tenant advisory transaction locks serialize sensitive transitions. Appointment interval/capacity guards and source-assignment checks also exist in PostgreSQL. Audit/history guards preserve prior terms and organization changes. Successors do not inherit confidential historic documents by default, and term-sourced reviewers cannot browse pre-term terminal requests unless independently authorized for audit history. Delegation requires effective existing reviewer authority, expires, respects the workflow's delegation policy and cannot count the same principal twice toward quorum.

An EF/Npgsql raw-query parameter-renaming collision was discovered by composing hierarchy queries repeatedly and with search predicates. The shared tree query uses canonical typed GUID literals, with a narrowly documented analyzer suppression, so no untrusted text enters SQL. Search and other user predicates remain parameterized. The scoped dashboard and legacy organization search regressions both pass.

## Database and migrations

Sixteen additive tables: `OfficerPositions`, `LeadershipTerms`, `OfficerAppointments`, `OfficerAppointmentHistory`, `LeadershipHandovers`, `HandoverChecklistItems`, `Committees`, `CommitteeRoles`, `CommitteeMemberships`, `CommitteeActivities`, `GovernanceRequests`, `GovernanceDelegations`, `GovernanceResolutions`, `GovernanceDocuments`, `ClubHealthConfigurations`, `OrganizationHistory`.

Existing `Orgs` gains club operational/profile fields; `Assignments` gains its optional appointment source. The existing membership workflow schema gains workflow kind, stage delegation and governance decision targets/principals. A decision must target exactly one membership application or governance request. Appropriate tenant, current-term, expiry and lookup indexes accompany the constraints.

Applied successfully to existing `clubs_demo`, `clubs_test` and the governance scale clone, retaining all 13 previous migrations:

1. `20261110202022_GovernanceFoundation`: schema, shared workflow extensions, permissions/templates, history, interval and source guards.
2. `20261110204615_GovernanceHistoryHardening`: immutable title/organization snapshots and supporting indexes/templates.
3. `20261110210002_GovernanceLegacyDefaults`: classifies legacy membership workflows, safely resolves old active-workflow conflicts and repairs historical club operational defaults.

Final EF check reports no pending model changes. Fresh database recreation and downgrade/restore were not independently exercised in this phase; additive upgrades of the existing databases were exercised. The compatibility data migration intentionally does not fabricate a reverse data rollback. Take and verify backups before deployment migrations.

## Source and routes

New backend files: `GovernanceModels.cs`, `GovernanceSetup.cs`, `GovernanceService.cs`, `ApprovalEngine.cs`, `GovernanceEndpoints.cs`, `ClubGovernanceEndpoints.cs`, `CommitteeEndpoints.cs`, `GovernanceDocuments.cs`, `HandoverEndpoints.cs`, `GovernanceWorker.cs`, and three migration/designer pairs under `server/Migrations`.

Foundation changes: `Access.cs`, `Models.cs`, `MembershipModels.cs`, `MembershipSetup.cs`, `MembershipWorkflowEndpoints.cs`, `MembershipWorker.cs`, `MaintenanceWorker.cs`, `Operations.cs`, `Program.cs` and the EF model snapshot. These connect effective authorization, seeding, shared decisions, legacy mutation controls, retention and background processing.

New frontend files: `GovernanceUI.tsx` and `pages/GovernanceClubs.tsx`, `GovernanceLeadership.tsx`, `GovernanceCommittees.tsx`, `GovernanceApprovals.tsx`, `GovernanceSettings.tsx`, `GovernanceDocuments.tsx`. `App.tsx`, `index.css`, the organization views, member portal and member profile integrate navigation, actions and office history.

Frontend routes under `/app`: `clubs`, `clubs/:id`, `club-administration`, `leadership`, `leadership-calendar`, `handovers`, `committees`, `committees/:id`, `governance`, `governance-settings`, `governance-documents`, `resolutions`, `foundation-overview`. Existing organization/member routes remain in place.

Exact HTTP methods and paths are recorded in [docs/PHASE3_API.md](docs/PHASE3_API.md), generated from the running API. [docs/openapi.json](docs/openapi.json) and [docs/schema.sql](docs/schema.sql) were refreshed from the final running development database/API. New tests are `phase3.py`, `phase3_extended.py`, `phase3_security.py`, `phase3_scale.py` and the resumable fictional `phase3_demo.py` seed utility.

## Verification

Final native build: TypeScript/Vite production build and .NET build succeed, with zero .NET warnings/errors. No new dependencies were needed. npm audit reports zero vulnerabilities; NuGet reports no vulnerable packages from its configured sources. These are advisory scans, not a penetration test.

Real PostgreSQL/API checks passed:

- Phase 3 core: **126** checks.
- Phase 3 extended handover, restructuring, revisions, quorum and delegation: **15** checks.
- Phase 3 security/compatibility: **20** checks, including district and national aggregation against recursive SQL truth, delayed-worker permission expiry, background-import atomicity, inherited calendars, historical confidentiality and legacy-route controls.
- Phase 1 regression: **60** checks.
- Phase 2 regression: **127** checks; extended scope/privacy: **11**; imports: **53**; retention: **5** checks.
- Actual local SMTP sink: verification delivery and membership-invitation failure/backoff/retry/body delivery passed.

The 35 requested scenarios are covered across the core/extended/security suites and scale harness: club creation/code/parent/profile/lifecycle (1–6), officer configuration and proposal/capacity/approval/rejection/future/expiry/self/history (7–17), handover (18), committees and chair scope/removal (19–22), governance and concurrent decisions (23–24), resolution/documents (25–26), directory/report scope and health (27–30), cross-club/tenant/history (31–33), migration presence/model validation (34), and measured hierarchy performance (35). Security tests add direct database truth checks; tests use disposable fictional records and do not delete prior history.

Authentication rate limiting remained enabled. An early combined regression run exhausted the shared IP bucket; rerunning the suite with a fresh test API passed without weakening the limiter. Retained JSON evidence is under `docs/phase3*-verification.json` and `docs/phase3-regression-*.json`.

Browser review on the clean demo verified the four-card dashboard, actual officer/history drawer, club workspace leadership tab, responsive club cards at a 390×844 override, ordinary-member navigation and automatic workspace return to the correct dashboard. Viewport override was reset. API tests exercise the write workflows; this was not a full browser automation of every form or a formal accessibility audit. Screenshots: [dashboard](docs/phase3-dashboard.png), [leadership](docs/phase3-leadership.png), [club workspace](docs/phase3-club-workspace.png), [mobile](docs/phase3-mobile.png), [member home](docs/phase3-member-workspace.png).

## Measured performance

The separate synthetic PostgreSQL fixture contains 101,035 people, 206,038 memberships, 55,000 appointments (50,000 historical and 5,000 current), 55,000 appointment-history rows, 5,000 committees, 25,000 committee memberships and 3,000 pending governance requests, over the existing large Phase 2 hierarchy. Historical fixture dates are synthetic benchmark data, not fabricated production history.

Final warm sequential loopback HTTP p95, 30 samples after a warmup: club directory **74.93 ms**, search **74.62 ms**, current officers **89.54 ms**, historical officers **118.49 ms**, committees **41.98 ms**, governance requests **40.02 ms**, aggregate dashboard **268.29 ms**. Median dashboard is 240.09 ms. No under-200-ms dashboard claim is made.

PostgreSQL EXPLAIN ANALYZE p95 execution times: current-leadership query **37.91 ms**, committee members **0.10 ms**, district hierarchy **8.67 ms**. SQL values exclude HTTP/projection/authentication costs. Tenant-qualified membership count predicates corrected an observed index-use problem: directory p95 improved from 550 ms in the retained initial run to about 75 ms in the final run. No cache/closure-table infrastructure was added without evidence.

See [final measurements](docs/phase3-scale.json), [query plans](docs/phase3-explain.json) and [initial baseline](docs/phase3-scale-before.json). Measurements use PostgreSQL 17.6 on this Windows machine and warm sequential requests; they do not establish concurrent production capacity or a service-level objective. Source fixtures and commands remain repeatable in `tests/phase3_scale.py`.

## Limits, outstanding defects and Phase 4 prerequisites

No known blocking functional defect remained in the exercised acceptance flows. Unexercised scenarios can still contain defects. Production hosting/containers, provider TLS/deliverability, multiple-replica load, disaster recovery and migration downgrade were not validated here. Existing Phase 1 identity and deployment hardening remain operational gates; MFA is optional. Document uploads have type/size/signature checks but no malware scanning or content-disarm service. Files remain bounded database blobs rather than a scalable external object-storage pipeline. No election or secret-ballot administration is implemented. Analytics use live indexed queries; concurrent-load validation and dashboard latency budgets remain follow-up work.

Handover snapshots cover implemented operational responsibilities; future finance, events and project assets cannot be transferred until those modules exist. Independent manual roles remain until explicitly removed; completing an office cannot revoke unrelated grants. Effective authority is enforced immediately even though display/status cleanup is eventually processed by a worker. Approved resolutions are recorded governance artifacts, not cryptographic signatures or a legal records-management certification.

Before Phase 4 events: define event entities and privacy/retention, reuse PersonId/MembershipId and authoritative organization scope, recheck effective appointment authority on queued actions, use the shared approval engine/outbox, and introduce event permissions explicitly. Do not infer event access from committee/officer labels or enable placeholder event actions. The revised [eight-phase roadmap](EIGHT_PHASE_ROADMAP.md) preserves these boundaries.
