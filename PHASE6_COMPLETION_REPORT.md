# Phase 6 completion report — club operations and community impact

Delivered 11 November 2026. The core meeting, task, project, volunteer, impact, document and certificate workflows are implemented in the existing application. PostgreSQL remains authoritative. This report separates verified functionality from advanced requirements that remain incomplete; it does not claim that every feature in the master brief is finished.

## Architecture and prior-phase audit

Reviewed the existing PostgreSQL/EF model, arbitrary organizational hierarchy, membership and effective-role sources, leadership/committees, shared governance review engine, event registration/check-in, Communication Center, notification/outbox workers, protected documents and PDF/font/QR infrastructure. No reusable meeting/project/task engine existed. Event team task text and leadership handover checklists were specialized source records, not a general task engine.

Phase 6 adds one reusable operations task domain and connects source records to it. It reuses existing users and global people, memberships, organizations, roles, audit, governance workflows/decisions, event check-ins, OrganizationSettings for agenda templates, GovernanceDocuments for vault metadata, CommunicationStorage for private object bytes, NotificationService and Mails for notices, and existing PDFsharp/QRCoder/Noto font support. No extra member database, approval engine, storage service, payment processor or microservice was introduced.

Before implementation, prior-phase suites were run. The foundation test exposed a fixture issue: its unfiltered 100-row assignment list could omit the newly created test role in the accumulated database. A bounded optional roleId filter was added to the existing API and the test now uses it. The final foundation suite passes. All other baseline suites passed.

## Implemented workflows

### Meetings, agenda and minutes

Meeting records support organizational owner, meeting type, organizer, chair/secretary, optional committee and existing event/session links, start/end and timezone, location/HTTPS online link, privacy, explicit required/optional people, attendance and configurable voting categories/minimum/percentage quorum. API transitions cover Draft, Scheduled, In Progress, Completed, Archived, Postponed and Cancelled, with audit and preserved revisions.

The calendar offers month, week and agenda views, selected-range reads, My Meetings and search. Authorized officers create/reschedule meetings; attendees can RSVP; online links and actual ICS downloads work. Local form times resolve in the selected meeting zone; edit forms receive the original zone-local values. Nonexistent DST times are rejected and ambiguous times select the later occurrence. Local-calendar recurrence generates at most 52 additional occurrences, with DST gap/ambiguity handling. It is bounded eager series generation, not a complete RRULE editor.

Structured agenda items persist order, title, description, presenter, duration, category/outcome/status. The editor supports drag reordering and move buttons. Organization agenda templates are saved and loaded through existing settings. Agenda publication uses shared independent approval and preserves earlier revisions.

Completed meetings accept new minutes revisions. Draft submission, independent approval, published immutable content and corrected successor drafts preserve history. Approved minutes export to a real PDF. The initial editor is narrative text; full structured proceedings/resolution editing remains limited as described below.

Manual attendance supports expected/present/absent/excused/late/online/guest classifications and reasoned corrections. Quorum uses distinct active eligible memberships and configured categories; required voters are max(minimum, ceiling(eligible × percentage)). Physical attendance does not itself confer a vote. SQL aggregates count all attendance independently of the bounded 1,000-row display. A 1,200-voter fixture verifies this boundary.

Explicit meeting invitations permit narrow reads from ancestor workspaces without granting organizational write permissions. A meeting linked to an event/session reads the existing event check-ins and rejects a second meeting attendance write. Existing event QR/check-in and corrections remain authoritative. Event registration participation is counted separately from voting eligibility.

### Tasks and follow-through

The shared task model links meetings, projects, committees, resolutions, leadership handovers or administrative work. It records assignee, priority, dates, status, dependencies, blockers, completion evidence, comments and immutable history. Assigned members can update their own permitted tasks; scoped managers control assignment/details. Dependency cycles are rejected and incomplete dependencies block completion. Blocked/completed states require explanation/evidence.

Confidential meeting tasks cannot be assigned to an uninvited person, preventing notice content from bypassing the source meeting's privacy. Task listing also filters inaccessible meeting sources. A bounded transactional worker marks reminder cycles and queues notices through the existing notification/outbox infrastructure. In-app delivery uses the Communication Center's consent, quiet-hour and current sender-permission checks. Email notices retain the existing operational notification transport semantics; no universal exactly-once SMTP guarantee is made.

### Service projects and joint participation

A project master stores category, lead organization, manager/committee, description/objectives, dates/location, target beneficiaries, planned volunteers/hours, estimated budget/currency and shared review reference. Project proposal approval is separate from impact verification. Controlled transitions support planning, execution, on-hold/cancelled, completion, impact verification and archive. Activities and source-linked tasks attach to the same master project.

Participating organizations use unique relationships instead of cloned projects. Lead scopes can manage the project. Partner clubs receive only an approved summary and their own participation relationship; draft/submitted proposals and lead private operations are not exposed. Partner access cannot operate the lead project. Impact is aggregated from the one project and deduplicated by project/metric/source key. Budget figures are planning estimates; no expenses, payments or accounting ledger are represented as complete.

### Volunteers, shifts and verified hours

Volunteer profiles extend existing people with skills, interests, availability, training and language. Approved-project opportunities/shifts record supervisor, role/title, location, dates/deadline, capacity and required training text. Members apply and confirm; coordinators review/assign. Repeated applications return the same shift/person relationship. Full shifts produce waiting-list records. PostgreSQL locks and version checks prevent concurrent approvals from exceeding capacity.

Actual check-in/out and breaks create submitted hour records; scheduled duration is never treated as actual service. Documented manual intervals can be submitted with reasons. Overlapping non-rejected claims within the association are rejected; independent verification also rechecks overlap. Submitters/recipients cannot verify their own hours. Only verified values enter achievements, analytics and service certificate eligibility. Training profile text is self-reported; it is not an accredited training register.

### Impact intelligence and corrections

Configurable metric definitions include name, unit, description, category, data type, aggregation and evidence requirement. Supported numeric aggregation is Sum, Maximum, Average or Count. Impact records distinguish estimates and submitted/verified/rejected outcomes. Required evidence and duplicate source keys are enforced. Shared governance review requires an independent authorized impact verifier.

A proposed correction to verified impact preserves the current approved total until independent approval. Approval applies the corrected value and preserves the prior verified record in immutable revision history. Estimates, pending values and rejected submissions are excluded from verified totals. Unique source keys prevent repeated reporting of the same declared source within a joint project; they do not constitute a global beneficiary identity/deduplication system.

Operations dashboards query real scoped meetings, project categories/statuses, tasks, verified hours/volunteer engagement, joint projects and verified impact. The same components serve club and ancestor workspaces. Member profiles and the membership portal show authoritative verified service hours/project participation and issued certificates; existing leadership views remain intact.

### Protected document vault

The vault extends GovernanceDocuments with folder/tags, private/organization visibility, explicit member shares, operation reference, current version and review reference. New content uses the existing private random-key filesystem object store, not PostgreSQL file binaries. Earlier document routes retain their authorization; they cannot bypass new object-backed vault permissions.

Actual upload, drag/drop and multiple-file upload, title/tag search, metadata/sharing, version append, historical download, PDF/image preview delivery, optional shared review and retention expiry are implemented. New uploads allow signature/extension checked PDF, PNG and JPEG up to 5MB. Explicit shares grant record-level read across the association without broader organizational write access. Private downloads are session protected, no-store and nosniff; preview uses the same authorization with inline disposition, same-origin framing and actual byte ranges. Expired objects are immediately inaccessible; bounded cleanup removes version bytes while retaining metadata/hashes/audit. Legacy database-backed documents remain available through their existing authorization rather than being silently migrated or exposed by operations.view.

The storage deployment retains Phase 5's private object volume and Data Protection keys. nginx includes the protected same-origin preview response configuration. Container/proxy deployment has not been exercised on this machine. Malware scanning and several richer sharing/folder capabilities remain incomplete.

### Certificate studio, issuance and verification

The studio provides reusable versioned templates, A4 portrait/landscape, background/border color, positioned text, font size, alignment, dynamic placeholders, drag placement, live sample preview and actual PDF preview. Approved certificate records freeze the layout and resolved source values. New template edits do not alter historical issuances.

The preparation UI uses searchable named source records rather than raw IDs; ordinary members can select only their own identity in general operational pickers. Preparation validates supported source eligibility: active scoped membership, actual event check-in, verified project service hours or approved leadership appointment. Missing placeholders are rejected. Up to 100 distinct recipients can be prepared together. The natural issuance key (organization/template/version/person/source/replacement) returns an existing record on retry. Membership-source issuance is currently one recipient at a time; project/event batch sources support multiple recipients. Each recipient passes shared independent review before issuance. Requester, certificate recipient and project manager conflicts are bound into the existing subject-user review guard.

Issued certificates have unique numbers, random 256-bit verification tokens, protected token storage, a real QR PDF, revocation and explicit reissue linkage. A revoked predecessor is preserved. Authorized bulk ZIP generation renders up to 50 issued PDFs. Public verification is rate limited and returns validity, number, frozen type, issuer organization, issue date and revocation status; recipient name appears only when explicitly enabled. Contacts, person/membership IDs and private source data are excluded. The actual generated PDF was rendered, visually inspected and its QR independently decoded before checking the public API.

## Database changes

Three additive migrations bring the total to **29**:

- `20261111014651_ClubOperations`: 18 operations tables, vault metadata extension, tenant-qualified relationships, scoped permissions, uniqueness/checks and immutable revision/minutes/certificate-content guards.
- `20261111020039_OperationsObjectStorageGuard`: extends the earlier governance document constraint to accept protected object keys while retaining type/size checks and legacy content compatibility.
- `20261111021932_OperationsIndexes`: project status/date, volunteer assignment/hour, task ownership/status/due, revision, document cursor and certificate source lookup indexes; vault version concurrency metadata.

New tables: ClubMeetings, MeetingAttendances, MeetingMinutes, OperationTasks, OperationComments, ServiceProjects, ProjectOrganizations, ProjectActivities, VolunteerProfiles, VolunteerShifts, VolunteerAssignments, VolunteerHours, ImpactMetrics, ProjectImpacts, OperationRevisions, DocumentVersions, CertificateTemplates and IssuedCertificates.

All 29 migrations applied successfully from an empty `clubs_operations_fresh` database. Demo, integration, scale and provider-transport databases were upgraded additively. EF reports no pending model changes. Existing roles are granted appropriate operations permissions by migration and future provisioning uses the same role-template path. Custom roles need explicit grants. No Phase 6 seed flag is required. See [schema export](docs/schema.sql). Production rollback must preserve both metadata and private objects; a destructive downgrade/restore was not tested.

## API and UI routes

See [Phase 6 route inventory](docs/phase6-api.md) and the updated [OpenAPI snapshot](docs/openapi.json). Route groups include meetings/agenda/attendance/minutes/actions through tasks; reusable tasks/comments/status; projects/activities/impact/corrections; volunteers/profiles/opportunities/shifts/assignments/attendance/hours/verification; document metadata/versions/protected delivery; template preview/version editing; certificate preparation/PDF/ZIP/revocation/public verification; operations options/templates/history/analytics; member achievements. Review decisions reuse existing governance endpoints.

The route-level lazy operations module preserves the app shell:

- `/app/operations`: scoped operations overview and impact summary.
- `/app/operations/meetings`: month/week/agenda directory, search, My Meetings and creation drawer.
- `/app/operations/meetings/:id`: overview, agenda, minutes, attendance, tasks, documents and history.
- `/app/operations/tasks`: assigned/scoped tasks, filters, status/evidence/comments and creation.
- `/app/operations/projects`: project directory and proposal drawer.
- `/app/operations/projects/:id`: overview/planning, tasks, volunteers, activities, budget estimate, documents, impact/reports and history.
- `/app/operations/volunteers`: opportunities, assignments, directory and verified/manual hours.
- `/app/operations/impact`: verified scoped outcomes and metric definitions.
- `/app/operations/documents`: protected vault, preview/version/sharing drawers.
- `/app/operations/certificates`: template library, visual studio, preparation and issued/pending records.
- `/verify/certificates/:token`: minimal public verification.
- Existing member profile/portal: service and certificate achievements.

Native browser review covered a member overview, officer calendar, actual meeting form persistence/workspace, actual template save/library and a dark mobile project workspace. The asynchronous organization selector was corrected to preserve the active workspace and support search. Organization/person names are resolved in bounded queries rather than displayed as raw record IDs. Mobile project client/scroll widths were equal (390px in the final observation), and the temporary viewport override was reset.

Screenshots: [overview](docs/phase6-operations-desktop.png), [meeting workspace](docs/phase6-meeting-workspace.png), [certificate studio](docs/phase6-certificate-studio.png), [saved template](docs/phase6-certificate-library.png), [mobile project](docs/phase6-project-mobile.png). UI review does not constitute a full accessibility/browser/device matrix or production PDF-viewer validation.

## Verification evidence

**135 new Phase 6 checks passed**: core 62, extended 28, dependency/correction/bulk workflows 14, preview/large-quorum/timezone edges 10, and cross-club/conflict-of-interest/selector scope 21. These exercise actual ASP.NET APIs and PostgreSQL, not an in-memory provider. Extra temporary reviewer/partner assignments in security tests are removed afterward. See [test results](docs/phase6.json), [extended results](docs/phase6-extended.json), [workflow results](docs/phase6-workflows.json), [edge results](docs/phase6-edges.json) and [scope results](docs/phase6-scope.json).

The final prior-phase API regression run passed **602 checks**: Phase 1–4 suites total 524 and Phase 5 API/security/extended suites total 78. All **37** local provider protocol checks passed again, plus **7** DST/quiet-time checks. The legacy SMTP transport rerun passed actual local delivery/retry for verification, membership invitations, event invitations and registration/pass access notices. No external recipient or real provider was used. [Regression inventory](docs/phase6-regression-results.json), [time checks](docs/phase6-time-checks.txt), [SMTP evidence](docs/phase6-smtp-checks.txt).

Backend build and frontend TypeScript/Vite production build passed. NuGet transitive advisories and npm audit reported zero known vulnerabilities at verification time: [NuGet](docs/phase6-nuget-audit.txt), [npm](docs/phase6-npm-audit.json). PDF preview/issued files were checked as actual PDF bytes; certificate QR was independently decoded from the rendered PDF. The PDF skill's read-only render/inspect workflow informed visual verification; PDF generation remains the application's existing .NET engine.

Security coverage includes active workspace/tenant/club scope, explicit confidential meeting participation, hidden private source tasks, independent approvals and conflicted recipients/managers, private vault access/sharing/version binding, signed-safe download headers/ranges, expired bytes, immutable history, eligibility checks, missing placeholders, public verification privacy, revoked PDFs, volunteer capacity races, overlap and self-verification denial, verified-only aggregation and correction preservation.

## Representative measurements

The isolated fixture contains 101,035 Persons, 10,030 Orgs, 20,001 ClubMeetings, 3,001 ServiceProjects, 50,000 VolunteerAssignments, 250,000 VolunteerHours, 10,000 GovernanceDocuments, 50,000 OperationTasks. Forty warm sequential API samples after one warmup measured p95: /meetings 36.28ms; /projects 21.36ms; /volunteers/hours 165.21ms; /documents 26.51ms; /tasks 19.69ms; /operations/analytics 282.19ms. All samples succeeded.

Actual 50-recipient eligibility/placeholder preparation measured 3.50 seconds. Five bulk ZIP requests each generated and parsed 50 real PDFs: p50 222.65ms, p95 376.95ms, zero errors, approximately 962,324 ZIP bytes.

See [API scale results](docs/phase6-scale.json), [representative EXPLAIN ANALYZE plans](docs/phase6-explain.json) and [bulk certificate measurements](docs/phase6-certificate-scale.json). All measurements use PostgreSQL 17.6 and loopback ASP.NET on this shared local Windows machine. They are test checkpoints, not guaranteed production targets.

The initial checkpoint in `docs/phase6-scale-initial.json` had no large task fixture and a different cache/load state. It is historical evidence, not a controlled optimization comparison. Fixtures contain explicitly synthetic SQL-approved/verified records for read/render benchmarks. Correctness suites separately exercise actual independent reviews. Document scale measures 10,000 metadata records; only its seed object was uploaded, not 10,000 distinct complete file/version histories. No production SLO, sustained multi-user load, object-storage throughput or external delivery guarantee is asserted.

## Incomplete functionality and known limits

- Recurrence supports bounded local-day series, not full monthly/yearly RRULEs, series-wide exception editing or an independent background recurrence service. Organization-wide/default meeting-type quorum policy presets and frozen historical voting rolls are not supplied; the current configured rule uses current active eligibility.
- Minutes are a narrative revision editor. Agenda presenter/category/outcome fields exist in the API, while the initial UI focuses on title, discussion, duration and ordering. Inline resolution drafting, automatic action extraction, item-specific attachments and a complete rich-text minutes review/correction designer are not implemented. Guest handling relies on existing identities or linked event guest registration.
- Task assignment/dependencies/comments/evidence are implemented; arbitrary team assignment, dedicated per-task attachment browsing and every requested My/Team/Committee/overdue shortcut are not complete. Some filters/dependency changes are API capabilities rather than full visual controls.
- Project master planning is entered at creation and frozen by review. Rich draft/rejected-proposal editing, detailed contribution administration, expenditure summaries, contribution allocation reports and configurable project-category administration need follow-up. Partner access is summary/own-relationship only. Phase 6 supplies estimates, not financial records.
- Volunteer training is self-reported text, not accredited eligibility evidence. Waiting lists require coordinator action; automatic promotion offers and sophisticated skills/availability matching are not supplied. Hours use trusted recorded attendance or independently reviewed manual evidence; overlap is association scoped. Manual adjustment policy and exhaustive project/shift period rules need further hardening.
- Impact metrics currently aggregate numeric values. Evidence is text/protected-reference based; there is no global beneficiary identity register, automatic cross-source beneficiary deduplication, external evidence verification or complete national country/trend comparison suite. Approved corrections preserve history; rich rejection/correction UI is limited.
- The vault provides free-form folders/tags and private/organization visibility plus explicit member sharing, with inherited legacy committee authorization. Full nested folder administration, role-specific ACL matrices, bulk sharing/version governance, malware/EXIF scanning, legal holds, S3 integration and file-orphan reconciliation are not supplied. Legacy document bytes are not migrated out of PostgreSQL automatically. Expiry is per document; retention policy management is not universal across all operations records.
- The certificate studio currently supports positioned text, a fixed bundled font, A4 orientation and colored border/background. Logo/seal/signature image placement, arbitrary shapes/page sizes, font library, autosizing every long script and full training/committee certificate eligibility workflows remain incomplete. Source types supported are Membership, Event, Project and Leadership. Custom labels do not independently verify a qualification. Review is per recipient; preparation is bounded to 100 and PDF ZIP to 50, not an unbounded bulk export job. Public verification has rate limiting but no dedicated public-view analytics dashboard or cryptographic PDF signature. It validates the issuance record rather than arbitrary uploaded PDF bytes. The printed certificate date is frozen at preparation; the public issue timestamp records approval.
- Member achievements initially show verified hours/projects/certificates alongside existing leadership views; a unified chronology of all meeting attendance, committee service and manual recognition remains incomplete. Dashboards are shared scoped components, not every requested role-specific drilldown.
- Lists and histories are bounded, and selected APIs offer keyset cursors. Several initial frontend directories display only their first bounded page; full cursor navigation and virtualized operational grids are incomplete. Large-history and concurrent production deployments need further UI/load testing.
- Docker/Compose/nginx deployment, real provider activation/TLS/deliverability, multi-replica object storage/keys, backups/disaster recovery, physical QR hardware, formal accessibility and broad browser/device coverage remain unverified. Phase 1–5 documented limitations remain in force. No test contacted real recipients. AI remains disabled.

## Phase 7 dependencies

Finance should reference authoritative project IDs, participating organization relationships, reviewed planning estimates/currencies, activities and verified service records. Add a separate designed ledger with explicit money/currency semantics, receipts, reconciliation, payment-provider idempotency and accounting permissions. Do not reinterpret estimates, certificate issuance or verified impact as financial approval or booked expense. Reuse existing people, organization scope, independent governance, audit, private document versions and notification/outbox infrastructure. Define retention/legal holds and approved evidence requirements before attaching regulated financial documents. Preserve source references and avoid double-counting joint-project allocations.

Legacy document interoperability was verified: original governance records retain their source authorization and retention rules, and the new vault refuses version or sharing mutations on those records. Linked event attendance also preserves existing event management and roster permissions.
