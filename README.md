# Impact International — Global Clubs Management

React/TypeScript/Vite frontend, ASP.NET Core 10 API, EF Core/Npgsql and PostgreSQL. The repository was empty at the start of implementation. No SQLite, browser storage, or mock production API is used.

## Run on this computer

The ignored `.infra` directory contains a locally initialized PostgreSQL 17.6 cluster bound to `127.0.0.1:55432`. It is a development-only trust-authenticated cluster, never a production deployment. `.infra/seed-password` contains the randomly generated local demo password. No password is checked into source.

From the project root in PowerShell:

```powershell
# Start the local database if it is stopped.
.infra/pgsql/bin/pg_ctl.exe -D .infra/pgdata -l .infra/postgres.log start

# Terminal 1: API and clean fictional demonstration database
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ConnectionStrings__Database='Host=127.0.0.1;Port=55432;Database=clubs_demo;Username=clubs'
dotnet run --project server --no-launch-profile --urls http://127.0.0.1:5080

# Terminal 2: frontend
npm --prefix client ci
npm run dev
```

Vite prints the available URL. This session used **http://127.0.0.1:5174** because port 5173 was occupied. Keep the browser and API on the same hostname; Vite proxies API requests so cookies remain same-origin.

Demo accounts: `admin@example.test`, `manager@example.test`, `member@example.test`, `secretary@example.test`, `president@example.test`, `applicant@example.test`. Read the generated password locally with `Get-Content .infra/seed-password`. Choose an authorized workspace after signing in. The administrator has two associations and a separate club-member workspace, demonstrating different hierarchies and permissions.

For a fresh installation, install .NET SDK 10, Node 24, Python 3.12+, and a maintained PostgreSQL 17+ distribution. Set `ConnectionStrings__Database` to your database, then:

```powershell
dotnet tool restore
dotnet restore server
npm --prefix client ci
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:CLUBS_SEED_PASSWORD=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(18))
dotnet run --project server --no-launch-profile -- --migrate --seed --seed-phase2 --seed-phase3 --seed-phase4
```

Retain your generated seed password privately. Development seed is refused outside Development and is skipped when users already exist. Migrations also work without `--seed`. PostgreSQL `pg_trgm` extension creation requires a migration operator with permission to create that extension. Use a separate restricted runtime database role in production.

## Verification

```powershell
npm run build
dotnet list server package --vulnerable --include-transitive
npm --prefix client audit
```

The integration suite uses an isolated **clubs_test** PostgreSQL database, not an in-memory provider. Create it, apply migrations, and seed with the same private development seed password. Start a second API on `http://127.0.0.1:5081` with its connection string pointing to clubs_test, then:

```powershell
pip install -r tests/requirements.txt
python tests/integration.py
python tests/phase2.py
python tests/phase2_extended.py
python tests/phase2_imports.py
python tests/phase3.py
python tests/phase3_extended.py
python tests/phase3_security.py
python tests/phase4.py
python tests/phase4_extended.py
python tests/phase4_security.py
python tests/phase4_pdf.py
python tests/phase4_pdf_unicode.py
python tests/benchmark.py
python tests/smtp.py
```

`CLUBS_TEST_URL` changes the test API address. Direct SQL checks intentionally target `clubs_test` on loopback port 55432. `CLUBS_PSQL` can supply a psql executable; otherwise the Windows executable under `.infra/pgsql/bin` is used. `CLUBS_SEED_PASSWORD` supplies test credentials; local runs can read the ignored password file. Tests create fictional rows and do not remove them; use a disposable database. The benchmark inserts 10,000 fictional clubs inside a transaction and rolls the fixture back. SMTP testing runs its own local SMTP sink and API process on ports 2526 and 5082.

Results are written to ignored `test-results/`; retained delivery evidence is under `docs/`. Live OpenAPI is at `http://127.0.0.1:5080/openapi/v1.json`; `/health` checks database connectivity.

## Email and account administration

Development verification/recovery messages are visible at `/api/v1/development/mail`. Open the returned relative link on the Vite origin. This endpoint exists only in Development. Do not expose Development mode on the internet.

Set `PublicOrigin`, `Smtp__Host`, `Smtp__Port`, `Smtp__From`, `Smtp__Username`, and `Smtp__Password` for SMTP delivery. TLS defaults to enabled. The PostgreSQL outbox retries failures with backoff and uses row locks to coordinate replicas. Delivery is at least once; an SMTP acknowledgement followed by a process crash can cause a duplicate message. Links expire after one hour and can only be consumed once. The local SMTP transport test does not verify a real provider's TLS, credentials, or deliverability.

For production, register and verify the first operator account, then run this explicitly from a trusted operator terminal with the production connection configured:

```powershell
dotnet run --project server --no-launch-profile -- --grant-platform-admin operator@example.org
```

The operator command requires an existing verified user and records an authentication audit event. Public signup never grants administrative rights. Platform admins can provision associations in the UI; association admins configure levels and scoped assignments.

## Containers

Copy `.env.example` to `.env`, populate private secrets and public hostname, then run `docker compose up --build`. The compose stack starts PostgreSQL, runs migrations as a separate job, then starts the API and nginx frontend. It does not seed production accounts. The frontend listens only on host loopback port 8080. Put an HTTPS reverse proxy in front of it; production session cookies require HTTPS. Configure trusted forwarded headers and authentication rate limits for your actual proxy topology before exposing traffic. Persist database and Data Protection volumes and protect backups and keys.

Docker was not installed in the execution environment, so image builds and Compose startup have not been exercised. Native frontend/backend compilation, PostgreSQL migrations, API integration checks and browser review were exercised. See `PHASE1_COMPLETION_REPORT.md` for exact evidence and remaining gaps.

## Project layout

- `server/`: API endpoints, domain/EF models, scoped access service, hierarchy operations, outbox and maintenance workers, migrations.
- `client/src/`: persistent shell, shared controls, lazy routes, cached API requests and responsive pages.
- `tests/`: real PostgreSQL/API checks, database benchmark and SMTP transport test.
- `docs/`: schema export, OpenAPI snapshot, architecture, evidence and dashboard screenshot.

The [Npgsql EF Core 10 documentation](https://www.npgsql.org/efcore/release-notes/10.0.html) and [ASP.NET Core antiforgery guidance](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0) informed the database provider and CSRF implementation. Dependency advisories found during setup were resolved with compatible patched packages.

## Phase 2 membership CRM

The membership domain extends the existing users, organizational hierarchy, scoped roles, sessions, audit and mail outbox. A global person may have multiple association/club affiliations. Membership-managed access grants are distinct from officer roles.

Open Members for CRM and intelligence, Applications for staged reviews, Membership operations for transfers/identity reviews/invitations/delivery, Import members for staged CSV/XLSX processing, and My membership for self-service and digital ID. Directory visibility is opt-in. New registrations have no administrative access; invitations create drafts and never bypass approvals.

For an existing Phase 1 database, set the connection and Development environment, retain the existing private seed password, then run:

```powershell
dotnet run --project server --no-launch-profile -- --migrate --seed-phase2
```

This applies additive migrations before idempotent fictional demo seeding. Omit `--seed-phase2` on operational databases. A new production association receives default membership categories/policy/workflow through the existing provisioning path. SMTP must be configured for real email delivery; the portal accurately reports queued/sent/retrying/suppressed status.

The scale benchmark deliberately targets a separate disposable database named `clubs_scale_phase2`. Stop connections to `clubs_test` before cloning it with PostgreSQL `createdb -T clubs_test clubs_scale_phase2`; run migrations on the clone, then start an API on 5083 for that database. Set `CLUBS_TEST_URL=http://127.0.0.1:5083` and run `python tests/phase2_scale.py --fixture` once, then omit `--fixture` for repeat measurements. This creates over 100,000 fictional people and 105,000 affiliations, and retains the isolated fixture. Never run it against operational data. `python tests/phase2_import_memory.py <test-api-pid>` samples API working set across 1,000- and 5,000-row import suites.

See [Phase 2 completion report](PHASE2_COMPLETION_REPORT.md), [API reference](docs/PHASE2_API.md), and [roadmap](EIGHT_PHASE_ROADMAP.md). Evidence and screenshots are in `docs/`. Test fixtures are fictional and remain in disposable databases. Preview servers are stopped at delivery; use the commands above to restart them.


## Phase 3 club administration and governance

Club workspaces and directories, officer definitions and calendars, independently reviewed appointments, effective-term access, preserved leadership history and handovers, committees, resolutions, private governance documents and scoped dashboards extend the existing foundation. Membership and governance approvals share one decision engine and workflow schema. Titles alone confer no application access; manual role assignments remain independent of appointments.

See [PHASE3_COMPLETION_REPORT.md](PHASE3_COMPLETION_REPORT.md) for migrations, API routes, verification, measurements and operational limits. The clean demo includes three approved title-only offices, a committee and a pending resolution. Phase 4 events and Phase 5 communications now extend these shared services.

Run governance tests in the listed order against the disposable clubs_test database. They reuse fictional member fixtures and an independent reviewer. Restart the test API between large suite groups to reset its authentication IP limiter; do not disable it. Scale measurements use a separate disposable clone of the Phase 2 scale database named clubs_governance_scale. Point an API and CLUBS_TEST_URL at that database, run `python tests/phase3_scale.py --fixture` once, then omit `--fixture` on repeat runs.

## Phase 4 — events

Event discovery and five calendar views are at `/app/events`; the six-step wizard at `/app/events/new`; personal invitations, registrations and passes at `/app/my-events`; scoped reports at `/app/event-intelligence`. Event owners manage audiences, invitations, sessions, operational teams, documents and feedback from the event workspace. Submission uses the existing independent governance approval engine before publication. RSVP never reserves capacity. Free registration, waitlists, revocable QR passes and live check-in use PostgreSQL transactions and constraints.

See [Phase 4 completion report](PHASE4_COMPLETION_REPORT.md), [event API inventory](docs/phase4-api.md), [OpenAPI](docs/openapi.json) and [schema](docs/schema.sql). Five additive event migrations bring the total to 21. Apply migrations before running the API. `--seed-phase4` installs tenant event-type presets; it does not create operational event attendance. `tests/phase4_demo.py` creates clearly fictional demonstration events through real APIs and is restricted to a Development API.

The event suites create fictional records in the disposable test database. Restart the test API between authentication-heavy suite groups: the existing 30-per-minute authentication limit is deliberately retained. Retention QA uses `python tests/phase4_retention.py --prepare`, a test API restart, then `python tests/phase4_retention.py`. PDF QA downloads actual app PDFs, renders them and independently decodes their QR; it needs the optional Python QA dependencies in `tests/requirements.txt`.

Scale tests must use the isolated `clubs_governance_scale` clone and `CLUBS_TEST_URL` pointing to its separate API, never the demonstration or production database. `tests/phase4_scale.py` changes synthetic membership categories and creates substantial fixtures; `tests/phase4_calendar_scale.py` prepares 10,000 fictional calendar events. They are explicit benchmark workflows, not normal startup tasks. SMTP QA starts a local sink on 2526 and API on 5082; set `CLUBS_SMTP_DATABASE=clubs_event_transport` to the separately migrated and seeded disposable transport database, avoiding a large old outbox backlog.

PDFsharp 6.2.4 is pinned under its MIT package license. Embedded Noto Sans and its OFL license are in `server/Assets/Fonts`; PDFs do not depend on installed Windows fonts. Tested Latin/Greek coverage does not imply universal script shaping. Paid booking is disabled; no gateway exists. Camera scanning requires a compatible browser and permission; physical camera hardware was not exercised. Check-in requires connectivity and has no offline mode. See the report for delivery, companion attendance, privacy and production deployment limits.


## Phase 5 — unified communications

Open `/app/communications` for the permission-aware inbox, private/group/support/committee/event conversations, announcements, campaign wizard, schedules, template/audience libraries, automations, delivery tracking, analytics and communication settings. Messages, receipts, notes, files and campaign jobs are persisted in PostgreSQL; conversation history is keyset-paginated and virtualized. Existing identity, membership, hierarchy, RBAC, approvals, audit and mail outbox remain the shared infrastructure.

Apply `dotnet run --project server --no-launch-profile -- --migrate` before upgrading. Five additive communication migrations bring the total to 26; no Phase 5 seed flag is required. Existing roles receive scoped communication permissions. `tests/phase5_demo.py` creates fictional conversations and drafts through a Development API only.

Set `Communications__StoragePath` to a private persistent directory and retain Data Protection keys. Compose supplies separate durable volumes for both. Multi-replica installations must share private object storage and keys. nginx and Vite forward WebSocket upgrades; the outer HTTPS proxy must also support them. Never expose the object directory as static content. Docker/Compose deployment remains unverified on this machine.

Provider accounts are configured by authorized administrators in the UI; credentials are encrypted server-side. Meta requires the deployment-selected `Communications__MetaVersion` (`META_GRAPH_VERSION` in Compose), verified phone identity, app secret, callback verification and approved supported templates. Twilio requires verified sender ownership and exact public callback URL. SMTP uses the existing server configuration; after verifying your domain/sender with the provider, explicitly allow that address via `Smtp__VerifiedSenders__0` (`SMTP_VERIFIED_SENDER` in Compose). The email HMAC callback requires a trusted provider relay; it is not a native SES/SNS endpoint. No real provider account or external recipient was used in verification.

Communication mail commits a dispatch claim before network delivery. Interrupted or ambiguous submissions are quarantined as Uncertain for reconciliation instead of blind retry. Definitive transient failures retry with bounded backoff; security mail retains priority. Legacy authentication mail remains at least once. Consent, suppressions, current sender authority, quiet hours and quotas are checked again at send time.

With the disposable, migrated and seeded test API on 5081, run `python tests/phase5.py`, `python tests/phase5_security.py`, and `python tests/phase5_extended.py`; restart between authentication-heavy groups rather than disabling the authentication limiter. Run `dotnet run --project tests/CommunicationTimeChecks` for DST checks. Install Python dependencies from `tests/requirements.txt`. `python tests/phase5_providers.py` owns a separate API on 5084 and local fake HTTP/SMTP endpoints, using only the disposable `clubs_communication_transport` database. It requires a migrated clone with fictional accounts. It disables its mock accounts and stops its child processes afterward.

`tests/phase5_scale.py` is an explicit large benchmark against the isolated `clubs_governance_scale` fixture, never normal startup or operational data. It adds substantial synthetic records and two 50,000-recipient campaigns. It records local enqueue/fan-out/read latency and restart/dedup evidence; these are not external provider throughput guarantees.

See [Phase 5 completion report](PHASE5_COMPLETION_REPORT.md), [communication API inventory](docs/phase5-api.md), [OpenAPI](docs/openapi.json) and [schema](docs/schema.sql) for evidence, supported capabilities and remaining limitations. AI assistance is explicitly disabled; private conversations are not sent to an AI provider.


## Phase 6 — club operations and community impact

Open `/app/operations` for meetings/calendar, agendas, minutes, attendance/quorum, source-linked tasks, service projects, volunteers/shifts/hours, impact, the protected document vault and certificate studio. Existing governance approvals remain at `/app/governance`; public certificate verification is `/verify/certificates/:token`. Member profiles and the membership portal include verified service and issued certificate records.

Three additive operations migrations bring the total to 29. Apply them before upgrading; no Phase 6 seed flag is required:

```powershell
dotnet run --project server --no-launch-profile -- --migrate
# Use an absolute persistent private object path for consistent native launches.
$env:Communications__StoragePath=Join-Path (Get-Location).Path '.infra/communication-objects'
```

Existing known roles receive scoped operations permissions; future provisioning reuses role templates. Custom roles need explicit grants. Preserve the existing database, Data Protection keys and private object volume together. New vault objects use the same Phase 5 storage service; earlier document records retain their original authorization and content. Do not serve the private directory as static files. nginx has a same-origin protected PDF preview exception; Docker/proxy startup remains unverified.

On the migrated disposable `clubs_test` API at 5081, run the following in order, restarting the API between authentication-heavy groups rather than disabling its rate limit:

```powershell
python tests/phase6.py
python tests/phase6_extended.py
python tests/phase6_workflows.py
python tests/phase6_edges.py
python tests/phase6_scope.py
```

The suites use actual PostgreSQL and fictional identities. Extended tests require the core fixture and existing Phase 4 event/check-in fixtures. Optional PDF rendering/QR dependencies are in `tests/requirements.txt`. Edge QA explicitly adds 1,200 fictional members to a separate test club to verify quorum beyond the display bound. Scope/conflict tests remove temporary role assignments afterward. No ordinary test sends to real recipients.

`python tests/phase6_demo.py` targets only the Development demo API on 5080 and creates fictional meeting/agenda/project/task records through real APIs. It does not seed production or fabricate approved impact.

`tests/phase6_scale.py` is restricted to the isolated existing `clubs_governance_scale` fixture. Its default is read-only measurement. `--fixture` explicitly adds large synthetic meeting/project/volunteer/document metadata fixtures; `--tasks` explicitly adds 50,000 completed task records. Do not repeat fixture preparation casually and never target operational data. `tests/phase6_certificate_scale.py` prepares 50 real certificate records from that synthetic verified-hour fixture, then explicitly sets issuance state by benchmark SQL to isolate PDF rendering. Correctness suites separately exercise actual independent approvals and two-recipient issuance. Both scale scripts own and stop their API on 5087. The document benchmark measures metadata, not 10,000 distinct complete file downloads.

[Phase 6 completion report](PHASE6_COMPLETION_REPORT.md), [operations API inventory](docs/phase6-api.md), [OpenAPI](docs/openapi.json), [schema](docs/schema.sql) and [roadmap](EIGHT_PHASE_ROADMAP.md) describe implemented scope, test evidence, measurements and incomplete advanced functionality. The studio currently supports positioned text, A4 orientation and colored backgrounds/borders; full image/signature/font design is not complete. Planning estimates are not financial transactions or ledger entries.


## Phase 7 finance

Finance is available at `/app/finance` and the personal portal at `/app/finance/member`. Create a financial entity, open an accounting period and explicitly grant entity access before posting. Hierarchy roles do not automatically grant access to independent books. Existing Club Treasurer roles receive finance application permissions; an entity grant is still required.

Run `dotnet run --project server --no-launch-profile -- --migrate` to apply four additive finance migrations (33 total). Back up the database and Data Protection keys together. Native money uses PostgreSQL NUMERIC/C# decimal; posted history is immutable and corrections use source documents/reversals. A completed transfer or reimbursement record is evidence of an external action, not an instruction to move money.

Stripe is the only implemented checkout adapter. Configure an entity-bound server alias using the commented settings in `.env.example`, bind it in Financial settings, and complete external sandbox/merchant setup before collecting funds. Pay Now is unavailable without configuration. Signature verification and provider session checks, not browser redirects, authorize capture. Tests never call real payment providers.

```powershell
python tests/phase7.py
python tests/phase7_extended.py
python tests/phase7_sources.py
python tests/phase7_security.py
python tests/phase7_providers.py
# Explicit isolated benchmark mutations only:
python tests/phase7_scale.py --fixture
python tests/phase7_billing_scale.py --run
```

Tests require the existing fictional Phase 1–6 fixtures and PostgreSQL test database. The provider suite starts its own API on 5084 and loopback HTTP fake on 5086; other integration suites use 5081. Scale fixtures are restricted to `clubs_governance_scale` and use 5087. `tests/phase7_demo.py` creates fictional showcase records through a Development API on 5080.

[Phase 7 completion report](PHASE7_COMPLETION_REPORT.md), [API inventory](docs/phase7-api.md), [regression evidence](docs/phase7-regression-results.json) and [roadmap](EIGHT_PHASE_ROADMAP.md) identify implemented core workflows, measured results and advanced gaps. Formal consolidation, complete fee/tax policies, additional payment adapters, external sandbox certification and every detailed register/Excel export are not complete.


## Phase 8 intelligence and release controls

Open `/app/intelligence` for scoped metrics, a metric catalog, a read-only assistant, PDF knowledge retrieval, independently reviewed follow-up workflows, branding/domain controls and operator diagnostics. Ctrl-K opens authorized cross-module search. Existing source permissions and explicit finance entity grants still apply. Optional OpenAI routing is server-configured and tenant-opt-in; local tools require no model credentials.

Apply the additive intelligence migration (34 total) before starting. See [Phase 8 report](PHASE8_COMPLETION_REPORT.md), [API inventory](docs/phase8-api.md), [initial audit](PHASE8_INITIAL_AUDIT.md), [regressions](docs/phase8-regressions.json), [scale measurements](docs/phase8-scale.json) and [recovery drill](docs/phase8-recovery.json). 54 new integration and 15 provider-contract checks pass; 848 selected prior-phase assertions pass. Production readiness remains blocked by the explicit gaps in the report, including off-host recovery, provider certification and deployment/TLS verification. The complete aspirational eight-phase scope is not marked finished.

## Advanced ID Card Studio

Open `/app/id-cards` for design, reviewed issuance, bulk jobs and print presets; `/app/my-id` shows official member cards. The module reuses membership, DigitalCards, certificate templates, governance, vault and tenant authorization. See [completion report](ID_CARD_MODULE_COMPLETION_REPORT.md) for verification and measured scale results. Apply migrations before starting the upgraded API. Combined PDFs support up to 1,000 cards; ZIP/image jobs support 10,000. Official output requires independent review.

The ID Card Studio software acceptance follow-up exposes all bulk filters and historical export versions, verifies scoped inherited artwork, mobile/keyboard flows, PDF geometry, expiry/revocation, keyset latency and local restore. See [operations](ID_CARD_OPERATIONS.md) and [PDF review sample](output/pdf/Impact-International-ID-Cards-Review.pdf).
