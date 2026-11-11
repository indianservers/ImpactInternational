# Phase 4 completion report — enterprise events

## Delivery status

Implemented in the existing React 19 / TypeScript / Vite 8 / ASP.NET Core 10 / EF Core / PostgreSQL stack. This phase adds working event operations to the Phase 1–3 foundation. It reuses the authoritative organization tree, global Person and scoped Membership records, authentication, RBAC, shared ApprovalEngine, notification service/outbox and protected document storage. No replacement engine or browser-only persistence was introduced.

The acceptance path is exercised against real PostgreSQL: scoped club and district creation, independent approval, publication, eligible audience selection, deduplicated invitations, member RSVP, member/consenting-guest registration, capacity and waitlists, opaque passes, authorized check-in, replay/revocation rejection, attendance and scoped reports. Five additive migrations apply successfully both to existing development databases and to an empty database. Build and model checks pass. Physical camera hardware, production delivery and deployment remain outside verified acceptance; see limits below.

## Implemented features

### Creation, approval and publication

- Eighteen configurable event-type presets; club, district and higher-level ownership, committee association and cohosts within authorized scope.
- Six-step wizard: basics and cover, audience, registration, sessions, invitations/reminder, review. Physical/online/hybrid venues, HTTPS meeting URLs, coordinates, original IANA time zone and UTC instants, contacts, language, description and event documents.
- Draft editing and duplication; independent shared governance approval/rejection/change requests, withdrawal and replacement with preserved history. Publication requires approval. Published title/description/venue/online changes use the same independent review engine and queue participant updates only after approval.
- Separate approval, publication, lifecycle and registration-window states. Open/close registration, complete, cancel and archive. End-of-event admission expiry is checked at request time, independently of worker timing. Cancellation closes pending event requests, revokes tickets and preserves attendance/history.

### Discovery, audiences and invitations

- List/cards, month, week, day and agenda calendar views; search, type, venue, organizer/ancestor, dates, status and own-registration filters; preview drawer, bounded keyset pages, saved user filters and calendar export. Multiday events overlap date ranges correctly.
- Audience rules combine organization subtrees, active membership, membership category, effective roles/committees and club city/country. Location criteria do not inspect private home addresses. Overlapping rules yield unique people. Source rule identifiers or owner-subtree context are persisted with each invitation.
- Scheduled and immediate invitation enqueue, consenting guest lists, tracking and reminder/completion jobs. Recipient identity canonicalizes verified account-linked guest email aliases to PersonId, preventing member/guest duplicate invitations without inventing people or memberships.
- Composer templates, per-recipient `{name}`, `{event}`, `{venue}` and `{start}` substitution, message preview and a private response link. Existing notification preferences and suppression remain authoritative.
- Invitation batches page 500 eligible identities at a time, insert parameterized JSON batches with database conflict suppression, and use a separate nonblocking advisory lock for competing enqueue requests. Network mail delivery never runs in the API. A short final state/authority check precedes commit; long fan-out does not monopolize the event admission lock.
- Queued, outbox, delivered, suppressed, retrying and expired-authority outcomes remain distinguishable. Delivered means an actual recorded SMTP send, never merely enqueue success.

### RSVP, booking, guests and admission

- Going/Maybe/Not Going with immutable response history and persisted personal invitation views. RSVP does not reserve a seat.
- Member registration uses an actual current scoped MembershipId. An account alone does not make someone a member. Registration-specific question forms, category, companion names, accessibility/dietary/emergency information, consent and private guest links; guest entry does not create a membership or global role.
- Transactional capacity, session capacity, duplicate participant protection, pending independent registration approvals, waitlist allocation or expiring offers, cancellation and promotion. Cancelling a pending registration also closes its shared approval request.
- Revocable/reissuable random opaque tickets, PNG QR, downloadable PDF pass and badge. Tokens contain no name, email or membership attributes. Invitation response credentials and admission credentials have separate purposes and deadlines.
- Authorized online verification, explicit attendance confirmation, browser camera QR detection where supported, handheld/manual input, participant search, entry point/device, duplicate check rejection, separate session attendance, controlled re-entry and reasoned attendance corrections. Operators are checked at request time, including after the row lock.
- Session agendas, room/speaker overlap validation, room/capacity and attendance boundaries. Concurrent conflicting session creation produces one winner.

### Event operations, feedback and reports

- Event-scoped manager/registration/check-in/volunteer assignments, current shifts, responsibilities, volunteer signup and personal duties. These assignments do not create another global role system. Delegated staff cannot configure association-wide event types or grant unrestricted team authority.
- Private banner/gallery/supporting PDF/PNG/JPEG documents through existing storage, bounded signature/extension validation, authorized downloads, private ETag revalidation and range support. Attendance-backed participation certificates are available after completion.
- Feedback question designer, attended-participant eligibility, identified or anonymous submission, duplicate prevention, privacy-aware summaries and identified exports. Anonymous responses omit RegistrationId and store a coarse day; fewer than five anonymous responses are withheld from summaries and raw anonymous exports are unavailable. Rating averages are computed in PostgreSQL rather than loading every answer into application memory.
- Event and scoped organization intelligence: eligible/invited/RSVP/confirmed/waitlisted/attended, guest/member classification, reserved seats, conversion, no-shows after completion, source-club breakdowns, session bookings/attendance, event type, organizational level and UTC monthly participation. Rates have explicit denominators; zero denominators yield zero. Reports use actual records, not display fixtures. Attendance CSV cells are protected against spreadsheet formula injection.

## Database changes

Sixteen new tables: Events, EventTypes, EventCohosts, EventAudiences, EventSessions, EventInvitations, EventRsvpHistory, EventRegistrations, EventSessionBookings, EventTickets, EventCheckins, EventStaff, EventFeedbackForms, EventFeedbackResponses, EventNotices and EventSavedFilters. Fifteen domain tables use tenant-qualified relationships; saved filters belong to a user and confer no event access. Existing GovernanceDocuments gains nullable EventId. Person, Membership, User, organization, approval, document and audit identifiers remain shared.

Five migrations, following the previous sixteen:

1. `20261110215254_EventFoundation`: event entities, shared-document association, permissions, indexes, checks, capacity and history guards.
2. `20261110221001_EventDeliveryJobs`: bounded notice jobs and invitation/member lookup indexes.
3. `20261110222437_EventLocationAndLookups`: coordinates, shared approval/user foreign keys and same-event session/check-in guards.
4. `20261110223448_EventAuditAndRetention`: registration/event transition audit triggers, lifecycle admission guards, retention-safe updates and end-date index.
5. `20261110234127_EventNonBlockingInvitations`: capacity serialization uses `FOR NO KEY UPDATE`, preserving mutual exclusion while allowing invitation foreign-key inserts.

Database uniqueness covers participant/recipient identity, ticket hashes, session booking and event/session check-in. Event-entry NULL session values use NULLS NOT DISTINCT, closing the common duplicate-NULL loophole. Capacity is defended by both service transactions and a PostgreSQL trigger. RSVP history is immutable. The existing audit table receives transitions with the actor where authenticated; tokens and private registration answers are excluded.

All 21 migrations applied to the empty `clubs_event_phase4_fresh` database. Existing `clubs_demo`, `clubs_test`, `clubs_governance_scale` and `clubs_event_transport` databases were migrated successfully. EF reports no pending model changes. The [schema snapshot](docs/schema.sql) reflects the final migrated database. Production upgrades still require a backup and a rehearsed restore; a full downgrade/restore exercise was not performed.

Private registration answers/contact accommodations and invitation delivery secrets are redacted after the event retention interval (one year by default); participant provenance, counts, decisions and attendance remain. Event documents reuse existing document retention. Four event retention checks and five earlier membership retention checks pass.

## API and screens

The [event API inventory](docs/phase4-api.md) lists all 59 event-related operations generated from the running API. [OpenAPI](docs/openapi.json) contains the schemas. These cover types/options/authorized lookup, CRUD/status/duplication/calendar, audience, invitations/redemption/RSVP, registration/waitlist, sessions, tickets/passes/certificates, verification/check-in/corrections, teams/duties, feedback, documents, activity, analytics/dashboard, exports and saved filters.

Primary routes:

- `/app/events` — discovery, calendar and preview.
- `/app/events/new` — six-step new/edit wizard.
- `/app/events/:id` — contextual workspace with operational tabs.
- `/app/events/:id/checkin` — live scanner/manual desk.
- `/app/my-events` — invitations, bookings, passes and duties.
- `/app/event-intelligence`, `/app/event-settings` — scoped reporting/configuration.
- `/event/:id`, `/event-invitation?token=…` — public event and private guest response flows.

Browser QA used the real demonstration API: saved a new draft through the wizard, reviewed the calendar preview, verified an already checked-in participant without another write, switched from headquarters administrator to ordinary member, saved an RSVP, reloaded and confirmed it persisted. At 390×844, mobile invitation and discovery content width equals viewport width (390 px); desktop was also reviewed. The temporary viewport override was reset. Evidence: [desktop directory](docs/phase4-events-desktop-final.png), [calendar preview](docs/phase4-calendar-preview.png), [saved wizard draft](docs/phase4-wizard-saved.png), [check-in](docs/phase4-checkin.png), [mobile invitations](docs/phase4-mobile-invitations.png), [mobile discovery](docs/phase4-mobile-discovery.png) and [certificate render](docs/phase4-certificate-preview.png).

## Tests executed

All event suites use real PostgreSQL and actual HTTP requests:

- `tests/phase4.py`: **55** core checks, including club event approval/publication, audiences, RSVP, registration, ticket generation, eight simultaneous two-seat bookings and eight same-pass check-ins.
- `tests/phase4_extended.py`: **29** checks, including expiring offers, approved event amendments, reminders, guest feedback after event end, registration approvals, document access/ETag, anonymous feedback thresholds and CSV escaping.
- `tests/phase4_security.py`: **28** checks, including event-only district authority, confidential governance feed isolation, independent approvals, cross-tenant denial, expired senders/staff, raw database capacity enforcement, guest/member classification and concurrent session conflicts.
- `tests/phase4_retention.py`: **4** checks of actual cleanup and retained provenance.
- `tests/smtp.py`: actual local SMTP protocol sink, deliberate first-attempt 451 rejection, persisted retry/backoff and eventual verification, membership invitation, event invitation and registration/pass-link delivery. [SMTP evidence](docs/phase4-smtp.json). No outside provider was contacted.
- `tests/phase4_pdf.py` and `tests/phase4_pdf_unicode.py`: actual secured PDF downloads, PDF parsing/rendering, independently decoded PNG and rendered-PDF QR equality, 64-character opaque/no-PII payload, Latin/Greek text preservation, two-page long-content wrapping and margins. The temporary Unicode fixture is restored in `finally`. [PDF checks](docs/phase4-pdf-verification.json), [QR checks](docs/phase4-qr-verification.json), [Unicode checks](docs/phase4-pdf-unicode.json). Rendered documents were visually inspected.
- Phase 1–3 regressions: **60** foundation, **127** Phase 2, **11** Phase 2 extended, **53** import, **5** membership retention, **126** Phase 3, **15** Phase 3 extended and **20** governance security checks: **417** checks in total. Authentication-heavy suites run with a fresh test API so the real 30/minute authentication limit remains intact.
- Client TypeScript/production build and .NET build pass with zero .NET warnings/errors. Final NuGet transitive and npm audits report no known vulnerabilities from their configured current sources: [NuGet audit](docs/phase4-nuget-audit.txt), [npm audit](docs/phase4-npm-audit.json). EF pending-model check passes.

Machine-readable core, extended, security, retention, concurrency, load, EXPLAIN, calendar, SMTP and PDF evidence is retained under `docs/phase4*.json`. Logs and generated private test files stay in ignored `.infra` and `test-results` directories.

## Performance results

Final isolated fixture: 101,035 people, 206,038 memberships, 10,030 organizations, 10,015 events, 473,903 invitations and 60,302 registrations. A convention has 10,000 confirmed registrations. PostgreSQL 17.6 runs locally; requests use loopback HTTP, shared-machine load and warm caches. Repeated benchmark runs accumulate fictional rows. Data setup uses explicit synthetic SQL; approval correctness is independently API-tested.

- **50,000 invitations:** 21.77 seconds (2,297 recipients/sec), one full-batch sample. Repeating the same request adds zero duplicates in 169.12 ms. A real check-in for the **same event completed in 84.28 ms before enqueue finished**. This measures durable invitation records, not mail delivery; most synthetic recipients lack linked accounts. One batch has no defensible p95/p99 distribution.
- **100 registrations, 20 concurrent workers, 50 seats:** exactly 50 confirmed and 50 waitlisted. p50 **135.15 ms**, p95 **498.47 ms**, p99 **626.90 ms**; zero HTTP errors.
- **100 distinct passes, 20 concurrent check-in workers:** p50 **83.22 ms**, p95 **152.95 ms**, p99 **168.55 ms**; zero unexpected errors. Twenty simultaneous duplicate replays return 409 as expected.
- **`/events?size=30`**, 50 warm sequential reads: p50 **42.04 ms**, p95 **76.52 ms**, p99 **81.89 ms**; zero errors.
- **`/events/{convention}/registrations`**, 50 warm sequential reads: p50 **4.85 ms**, p95 **6.59 ms**, p99 **6.63 ms**; zero errors.
- **`/events/{convention}/analytics`**, 50 warm sequential reads: p50 **168.73 ms**, p95 **190.76 ms**, p99 **192.55 ms**; zero errors.
- **Calendar with 10,015 events**, including 10,000 added across the large club hierarchy: 100 warm sequential reads, p50 **42.08 ms**, p95 **51.65 ms**, p99 **59.38 ms**, zero errors.

[Load measurements](docs/phase4-scale.json), [query plans](docs/phase4-explain.json), [calendar measurements and plan](docs/phase4-calendar-scale.json). Small eight-client correctness races are recorded separately in [concurrency evidence](docs/phase4-concurrency.json).

Implementation changes prompted by measured failures include removing a redundant empty audience UNION, paging eligible IDs directly, indexing lookup paths, avoiding provider raw-parameter collisions in composed organization searches, SQL feedback aggregation, batched conflict-safe invitation inserts, invitation-specific advisory locks and a foreign-key-compatible event capacity lock. The overlap test originally exposed an admission lock timeout; the final version passes. Initial checkpoint data in `docs/phase4-scale-before.json` is historical, not the final measurement or a controlled speedup comparison.

## Security validation and integration status

Existing identity, workspace scope and effective role checks remain authoritative. New permissions are `events.view`, `events.manage`, `events.checkin`, `events.reports` and `events.configure`. Existing known-role templates receive appropriate event capabilities. Event-only managers are restricted to event kinds in the shared request feed and cannot read unrelated confidential resolutions. Draft edits, audience/session freezes, independent approval, expired operational assignment, cross-tenant operations, capacity, cancellation and ticket replays are tested. Event-only organization/person lookup exposes necessary labels rather than private contact details.

Guest links and tickets are cryptographically random, stored as hashes with server-protected recoverable delivery copies, and rejected on expiry/revocation. `no-referrer` prevents invitation query credentials leaking to meeting destinations. CSRF, bounded input/files, event rate limits, current sender authority and existing notification consent are enforced. Paid inputs are rejected server-side. Guest identity and attendance do not silently create accounts, memberships or authority. File signatures are checked, but that is not a malware scan.

PDFsharp is pinned to **6.2.4**, whose installed package declares MIT; embedded Noto Sans includes its OFL license. Event PDFs do not depend on installed operating-system fonts. No payment, SMS, WhatsApp, third-party hosting or alternative mail infrastructure was added. README and the [eight-phase roadmap](EIGHT_PHASE_ROADMAP.md) are updated.

## Known issues and deferred functionality

- Physical camera access/permission and device compatibility were not exercised. Browser camera scanning uses getUserMedia/BarcodeDetector where available; handheld token and authorized manual lookup are the working fallback. No offline check-in, cached guest PII, revocation synchronization or offline guarantee exists.
- Paid registration stays disabled until a real gateway, idempotent payment verification, refunds and the finance model exist. There is no invented payment success.
- SMTP retry/delivery is verified against a local protocol sink. Production provider TLS, quotas, reputation, bounces, webhooks and bulk delivery rate are unverified. SMS/WhatsApp providers are absent. Enqueue throughput is not delivery throughput.
- People without a linked account receive an explicit “No linked account” processing outcome for member invitations. Use the existing verified person/account-linking flow or an explicit consenting guest invitation. No unverified email fallback or automatic account creation is hidden in the worker.
- Companions reserve grouped seats and share the primary registration pass. Attendance rates count primary registrations, not independently identified companions. Use separate guest invitations for individual attendance and session tracking.
- Certificate/badge output is a basic usable template, not a full visual designer. Latin/Greek Unicode was verified; universal CJK coverage and complex-script shaping were not. Anonymous feedback has aggregate rating summaries, not AI-generated text analysis or individual anonymous exports.
- Media uses bounded originals, lazy loading and authorized private revalidation; CDN thumbnails, antivirus and EXIF removal are deferred. Staff require an existing linked account and eligible scoped membership; anonymous guests cannot acquire staff authority.
- No production deployment, SSO/full MFA, disaster recovery, external provider verification, full accessibility certification or production SLO is claimed. Load tests are local synthetic measurements with the sample counts below, not a world-scale capacity guarantee.

## Phase 5 dependencies

Extend the shared notification/outbox system with provider quotas and throttling, security-message priority, campaign job provenance, bounce/delivery webhooks, current consent/unsubscribe handling, verified contact policy for unlinked people and retention. Reuse event invitation/notices rather than building another communication engine. Validate a real SMTP/TLS provider separately from local sink tests. Provider-enabled SMS/WhatsApp require explicit configuration and consent. Payments depend on the later finance/gateway phase; events must remain free-only until that dependency is satisfied.
