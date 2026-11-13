# Phase 2 completion report

Delivered 11 November 2026, Asia/Kolkata. Phase 2 extends the existing ASP.NET Core 10, React and PostgreSQL application. The acceptance journey is implemented and verified: verified signup, application or invitation, authorized independent approvals, active membership, self-service profile and digital identity, and consented scoped directory visibility.

## Implemented features

- Global person identity with optional reviewed account link, multiple association/club affiliations, generated membership numbers, categories, validity and renewals. Shared family email/mobile details are allowed. Duplicate candidates require explicit review and never cause automatic identity merges.
- Scoped Membership CRM with search, status and advanced filters, configurable/reorderable/resizable columns, selected exports, database-backed saved views, list/card layouts, mobile cards, quick-view drawers and full profile tabs.
- Profile tabs: overview, personal details, professional information, membership history, leadership, participation, documents, privacy/preferences. Linked profiles are owner-managed; an administrator may edit an unlinked global profile only when authorized for all its affiliations.
- Verified-applicant drafts, editing, submission, withdrawal, request-for-changes, rejection and staged approval. Workflow versions freeze on submission. Stage role, sequence, distinct-reviewer quorum, self-approval denial and optimistic versions are enforced server-side. Concurrent decisions cannot activate twice.
- Scoped invitation links/QR, target-email binding, usage limits, expiry and revocation. Acceptance creates a draft, never an active membership. Account linking requires officer identity verification. WhatsApp opens a composer; the application does not claim a message was sent.
- Validated active/inactive/suspended/expired/resigned/terminated/deceased transitions, effective-date/reason history, category changes, renewal idempotency, administrative holds and immediate access reevaluation. Membership-managed Club Member grants remain separate from officer roles.
- Source/destination/optional association transfer review, requester self-approval denial, rejection/cancellation, administrative clearance, future scheduling, durable completion, visible blocked state and retry. Source history and PersonId are preserved. Clearance does not assert a payment occurred.
- Opt-in active/unexpired directory with independent email, phone, photo, professional and contact-request consent. DTOs redact fields before serialization. Birth dates, emergency contacts and sensitive documents are excluded from directory/list/export/public QR responses. Contact requests have accept/decline flows.
- Server-rendered SVG digital ID with actual PNG QR, branding, membership number and validity. Public verification uses an opaque token and minimal consented fields. Revocation, suspension, expiry and transfer invalidate cards; reissue does not revive old QR codes.
- Real CSV/XLSX preview, mapping, field/scoped assignment validation, duplicate resolution, persisted progress/errors, durable leases, cancellation before final commit, atomic commit, idempotent retries and permission revalidation. Existing profiles are never overwritten. Scoped filtered/selected exports are audited and CSV formula prefixes escaped.
- Self-service across all own affiliations without administrative workspace selection: profile/privacy editing, ID preview/download/reissue/revoke, applications, transfers, notifications and configured club contacts. Profile completion excludes sensitive optional fields.
- PostgreSQL-derived distinct people, affiliations, joins, growth, opening-cohort retention, renewals and category/club/country/trend metrics. Effective history determines historical cohort validity; numbers are not hardcoded.
- Transactional notifications extend the existing PostgreSQL mail outbox with templates, inbox content, preferences, SMTP attempt history, retry/backoff and accurate queued/sent/retrying/suppressed status. No campaign or Events module was introduced.
- Existing branding/shell and Phase 1 routes preserved; lazy routes, loading/empty/error states and persistent light/dark preference added. Upcoming participation capabilities remain labeled.

## Database changes

Reused Users/UserProfiles, Tenants/Associations, Orgs/Levels, Roles/RolePermissions, Assignments, Sessions, Audits and Mails. UserProfiles adds Theme and repairs legacy empty locale/time-zone values. Assignments adds nullable MembershipId for managed grants. Mails adds subject/body/recipient, tenant/org scope and suppression; nullable UserId supports invitations to unregistered recipients. There remains one authentication, hierarchy, authorization and SMTP system.

New tables:

- Persons, PersonContacts, PersonAddresses, PersonTags, PersonPrivacy.
- Memberships, MembershipCategories, MembershipHistory, MembershipPolicies, MembershipRenewals, MembershipDocuments, MemberRelationships, MemberActivities.
- MembershipWorkflows, MembershipApprovalStages, MembershipStageRoles, MembershipApplications, MembershipApprovals, MembershipInvitations, PersonLinkRequests.
- MembershipTransfers, TransferDecisions, DigitalCards, DigitalCardVerifications.
- MemberImportJobs, MemberImportRows, MemberSavedViews, NotificationTemplates, MailDeliveryAttempts, MemberContactRequests, RoleAssignmentHistory.

The eight Phase 1 migrations remain. Five additive Phase 2 migrations were applied to clubs_test, clubs_demo and clubs_scale_phase2:

1. `20261110182649_MembershipFoundation`: domain tables, tenant foreign keys, atomic permissions, constraints, sequence, search indexes, immutable decisions/history and membership identity/club guards.
2. `20261110184942_MembershipRoleHistory`: assignment history, existing-assignment snapshots, tenant lock refinement, display-name and expiry indexes.
3. `20261110193404_MembershipSearchAndPreferenceDefaults`: membership-number trigram index and legacy preference repair.
4. `20261110193859_MembershipDirectoryReadIndex`: covering scoped membership read index.
5. `20261110195901_MembershipDocumentRetention`: allows empty document content only for expired records so retention can clear bytes without weakening active-upload constraints.

Composite tenant foreign keys reject cross-association relationships. Partial unique indexes reject duplicate live person/club/category affiliations. History/decisions are append-only. Existing officer history begins with an explicitly labeled snapshot, without invented term dates. [Exported schema](docs/schema.sql).

## APIs and screens

[API endpoint inventory and contracts](docs/PHASE2_API.md) and [OpenAPI schemas](docs/openapi.json) cover members, persons, directory, self-service, applications/decisions, invitations/link review, transfers/clearance, imports/resolution/retry, exports, metrics, saved views, workflow/policy/categories, documents, cards and contact requests. Public verification is `/api/v1/cards/verify/{token}`. Mutations reuse session authentication and X-CSRF; operational endpoints require current scoped grants.

New UI routes: `/app/members`, `/app/members/:id`, `/app/directory`, `/app/my-membership`, `/app/apply`, `/app/approvals`, `/app/transfers`, `/app/imports`, `/app/membership-insights`, `/app/membership-settings`, `/join`, `/verify`.

## Verification results

- Frontend TypeScript/Vite and ASP.NET Core builds passed; backend had zero warnings/errors.
- Phase 1 regression: 60 PostgreSQL-backed checks passed.
- Core membership suite: 127 checks passed, covering the requested signup-to-membership journey, concurrency, shared contacts, multiple affiliations, lifecycle, real QR decoding/revocation, privacy, changed-ID denial, documents, exports, search/paging and aggregation against SQL truth.
- Extended suite: 11 checks passed for quorum, frozen workflow versions, clearance blocking, future worker execution and cross-district read/create denial while permitting authorized descendants.
- Import suite: 53 checks passed at both 1,000 and 5,000 rows, including actual XLSX, invalid rows/formulas, duplicate resolution, atomic commit, revoked/restored permissions and safe idempotent retries.
- Retention suite: 5 checks passed for expired document/mail cleanup, staging PII redaction, preserved import provenance and old verification-log removal.
- Real local SMTP test forced a temporary failure, observed persisted future backoff, then made the retry due and verified delivery plus failed/successful attempt records for both verification mail and a real membership invitation notification. No real-provider deliverability claim.
- npm audit: zero reported vulnerabilities. NuGet: no known vulnerable direct/transitive packages from configured feeds.
- Browser: sign-in, scope selection, member search/quick view/profile, actual administrative creation and resulting persisted row, digital-ID portal, theme persistence after reload, desktop and 390x844 mobile review. Mobile header overflow was fixed; final content width was 375 within a 390 viewport. CRM renders cards on mobile. No console errors in the final observed screen. This is not a complete automated browser/accessibility suite.

Evidence is retained under docs: phase2-verification.json, phase2-extended-verification.json, phase2-import-verification.json, phase2-smtp-verification.txt, and screenshots phase2-members.png, phase2-member-created.png, phase2-portal-dark.png, phase2-mobile.png.

## Measured performance

The isolated fictional fixture contains **101,035 people, 106,038 memberships, 10,013 clubs and 3,000 pending applications**, with two populated association hierarchies and small regression tenants. Conditions: PostgreSQL 17.6, this Windows computer, local loopback, warm pools, sequential traffic, 30 samples after one warmup per case, no concurrent user load.

- SQL member page p95: **0.54 ms**; search **9.56 ms**; member count **24.4 ms**; opt-in directory count **97.68 ms**.
- API member list p95: **72.18 ms**; search **70.36 ms**; directory **136.1 ms**; membership-number sort **71.63 ms**.

These measured cases meet the database <100 ms and API <200 ms p95 targets. EXPLAIN ANALYZE BUFFERS uses TIMING OFF to avoid per-node clock overhead; statement Execution Time remains measured. [Measurements](docs/phase2-scale.json), [plans](docs/phase2-explain.json), and [initial measurements](docs/phase2-scale-before.json) retain actual conditions and evidence. The final count projection removes an unnecessary person join. This is not a concurrent load or deep-offset benchmark.

Optimizations: narrow recursive scope IDs; a separately selected sole-root fast path that rechecks root invariants in the data statement; indexed union name/number search; page IDs selected before bounded profile projection; covering indexes; deterministic paging and number cursors. Privacy/authorization results are not cached. Complete hierarchies/member datasets are not loaded into frontend state.

The same API process handled 1,000-row validation/commit in **7.558 s**, peaking at **311.3 MiB RSS**, then 5,000 rows in **13.042 s**, peaking at **362.9 MiB**. Measurements include worker polling; RSS sampled every 200 ms across the entire integration suite, including JIT/pools/endpoints. Five times the input increased peak working set by 51.6 MiB after warmup. This demonstrates bounded measured operation, not constant memory at arbitrary sizes or proof against leaks. Input is bounded to 10 MB/10,000 rows and EF write tracking clears every 200 rows. [Memory/throughput evidence](docs/phase2-import-memory.json).

## Security and retention

Atomic grants cover members view/manage, personal/contact access, import/export, invite, approve, transfer approve, linking, documents, cards, workflow management and directory access. Permissions and organizational subtree are enforced server-side. Active/unexpired managed membership is checked immediately, independently of cleanup. Shared person identity cannot authorize another affiliation. Cross-club, cross-district and cross-tenant attempts were tested.

Upload signatures/extensions/sizes are checked; photo metadata is removed and dimensions bounded. XLSX expansion is bounded and macros/formulas rejected. Documents remain authenticated attachments. Public QR is rate limited and token paths redacted in logs. Existing hashed opaque sessions, CSRF, lockout/auth limiting, independent denial auditing and transaction guards remain. Export/administrative mutations are audited.

Startup/hourly maintenance removes expired sessions; clears expired/consumed mail links, bodies and raw recipients; redacts retained import payload/staging source/candidate/error text after 30 days for terminal jobs without deleting provenance IDs; clears document bytes after RetainUntil and marks Expired; removes QR verification event logs after 90 days. Identity-document content defaults to 90-day retention. Organizations still need policy for profile/audit metadata and backups.

## Remaining issues and operational gates

- No production deployment is claimed. TLS/proxy topology, legal/privacy text, secrets, restricted database users, SMTP TLS/deliverability, backups/restores and concurrent/failure load tests remain rollout gates. The loopback trust-authenticated development cluster is not production configuration.
- SMTP is at-least-once; a crash after acceptance but before commit may duplicate delivery. Scale-out needs shared rate limiting and securely persisted/shared ASP.NET Data Protection keys.
- Document antivirus/content-disarm is not integrated. Add scanning before untrusted operational uploads. Bounded blobs currently reside in PostgreSQL.
- Imports are bounded atomic jobs; split larger files. Permission revocation and retry are tested; production crash/replica chaos recovery remains unverified.
- Deep offsets, every sort/filter combination, sustained concurrent workloads, full WCAG review and translations remain unverified. Directory/professional privacy preferences are enforced, while birthday/marketing preferences are stored for Phase 4.
- Relationship storage is reserved without relationship-administration API/UI. Full dated officer terms remain Phase 7. Events/participation are explicitly upcoming rather than fabricated.
- Phase 1 organization-restructuring and privileged-identity limitations remain. Its historical dark-theme and 100,000-member evidence gaps are superseded here.

## Phase 3 dependencies

Reuse PersonId for participants and MembershipId for eligibility snapshots, existing Orgs/Levels/scoped grants, privacy, audit and outbox. Add event/registration/meeting/attendance source records before enabling participation views. Keep event registration separate from membership applications. Financial eligibility remains an administrative hold until Phase 6 provides actual dues/payment/ledger evidence.

Setup and disposable-database test commands are in README.md. Task-owned preview/test processes are stopped at delivery. No commit or remote publish was requested.
