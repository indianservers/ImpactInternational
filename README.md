# ClubSphere — Global Clubs Management, Phases 1–3

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
dotnet run --project server --no-launch-profile -- --migrate --seed --seed-phase2 --seed-phase3
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

See [PHASE3_COMPLETION_REPORT.md](PHASE3_COMPLETION_REPORT.md) for migrations, API routes, verification, measurements and operational limits. The clean demo includes three approved title-only offices, a committee and a pending resolution. Phase 4 events remain future work.

Run governance tests in the listed order against the disposable clubs_test database. They reuse fictional member fixtures and an independent reviewer. Restart the test API between large suite groups to reset its authentication IP limiter; do not disable it. Scale measurements use a separate disposable clone of the Phase 2 scale database named clubs_governance_scale. Point an API and CLUBS_TEST_URL at that database, run `python tests/phase3_scale.py --fixture` once, then omit `--fixture` on repeat runs.
