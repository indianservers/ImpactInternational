# Phase 8 API inventory

All application routes require the existing authenticated active workspace, CSRF for writes and source permissions. Intelligence routes have a separate 120/minute per-IP process-local limit. Feature switches subtract permissions; they never grant them.

- `GET /api/v1/branding`
- `GET /health/live`
- `GET /health/ready`
- `GET /api/v1/intelligence/metrics`
- `GET /api/v1/intelligence/dashboard`
- `GET /api/v1/intelligence/export`
- `GET /api/v1/intelligence/search`
- `POST /api/v1/intelligence/assistant`
- `POST /api/v1/intelligence/knowledge/{id}/index`
- `GET /api/v1/intelligence/knowledge`
- `GET /api/v1/intelligence/knowledge/indexes`
- `GET /api/v1/intelligence/branding/current`
- `GET /api/v1/intelligence/branding`
- `PUT /api/v1/intelligence/branding`
- `POST /api/v1/intelligence/domains`
- `POST /api/v1/intelligence/domains/{id}/verify`
- `POST /api/v1/intelligence/domains/{id}/activate`
- `POST /api/v1/intelligence/domains/{id}/deactivate`
- `GET /api/v1/intelligence/workflows`
- `POST /api/v1/intelligence/workflows`
- `GET /api/v1/intelligence/workflows/options`
- `POST /api/v1/intelligence/workflows/{id}/test`
- `POST /api/v1/intelligence/workflows/{id}/submit`
- `POST /api/v1/intelligence/workflows/{id}/cancel`
- `POST /api/v1/intelligence/workflows/{id}/copy`
- `GET /api/v1/intelligence/workflows/{id}/executions`
- `POST /api/v1/intelligence/executions/{id}/retry`
- `GET /api/v1/intelligence/operations`

- `PUT /api/v1/intelligence/workflows/{id}` — current inactive draft only; version checked; reviewed rules require a fresh revision.

## Authorization and data contracts

- `intelligence.view` permits the dashboard/catalog; each source metric also checks its module permission. Finance always requires explicit entity grants.
- `intelligence.assistant` permits six vetted read-only tools. A tool never grants access to its source records. Personal membership/certificates/dues use the authenticated user and tenant.
- `intelligence.knowledge` permits authorized full-text retrieval. Indexing additionally requires document management and owning organizational scope. PDF extraction is limited to 5 MB, 100 pages, 30,000 characters/page and 1 million characters/document; two parsers per process.
- Search accepts 2–100 literal characters, filters kinds and caps results at 100. Source validators run again for confidential/private resources. No unfiltered counts or contact snippets.
- Workflow definitions use one of eight triggers, at most eight steps, at most one task producer and two allowlisted actions. Each delay is 0–10,080 minutes; at most 50 sources/day/rule. Draft test returns source hashes without actions. Shared governance approval is independent of the requester. Approved definitions and execution snapshots are database guarded.
- Branding is root settings-managed. Domains start Pending, exact DNS TXT proof makes them Verified, and platform-only activation requires separately provisioned ingress/TLS configuration. Ownership proof alone does not publish a hostname.
- Operations diagnostics require PlatformAdmin and always disclose incomplete production gates. `/health/live` does not query the database; `/health/ready` checks database, private object storage and configured persistent keys.

See the generated OpenAPI document for request schemas and the completion report for explicit limitations.