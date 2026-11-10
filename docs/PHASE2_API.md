# Phase 2 API reference

Base URL: `/api/v1`. The live OpenAPI document and schemas are exported in `docs/openapi.json`.

All workspace endpoints use the server session cookie and the active assignment; self-service endpoints authenticate the person through the signed-in user. Mutations require `X-CSRF` from `GET /auth/csrf`. Uploads use multipart form data. Public invitation acceptance still requires a verified signed-in applicant. Public card verification reveals only consented verification fields.

## Membership endpoints

- `GET /api/v1/membership/catalog` — query: tenantId, search, page
- `GET /api/v1/membership/options` — query: search
- `GET /api/v1/members` — query: search, statuses, clubId, categoryId, city, profession, industry, expertise, office, sort, page, size, after
- `POST /api/v1/members`
- `GET /api/v1/directory` — query: search, statuses, clubId, categoryId, city, profession, industry, expertise, office, sort, page, size, after
- `GET /api/v1/members/{id}` — query: directory
- `GET /api/v1/members/{id}/photo` — query: directory
- `POST /api/v1/persons/me`
- `PUT /api/v1/persons/me`
- `PUT /api/v1/persons/me/privacy`
- `PUT /api/v1/members/{id}/person`
- `POST /api/v1/members/duplicates`
- `POST /api/v1/members/{id}/status`
- `GET /api/v1/membership/me`
- `POST /api/v1/directory/{id}/contact-request`
- `POST /api/v1/membership/contact-requests/{id}/respond`
- `GET /api/v1/membership/notification-status`
- `GET /api/v1/membership/views`
- `POST /api/v1/membership/views`
- `GET /api/v1/membership/metrics`
- `GET /api/v1/membership/config`
- `POST /api/v1/membership/categories`
- `PUT /api/v1/membership/policy`
- `POST /api/v1/membership/workflows`
- `POST /api/v1/membership/applications`
- `GET /api/v1/membership/applications` — query: mine, status, page
- `PUT /api/v1/membership/applications/{id}`
- `GET /api/v1/membership/applications/{id}`
- `POST /api/v1/membership/applications/{id}/submit`
- `POST /api/v1/membership/applications/{id}/withdraw`
- `POST /api/v1/membership/applications/{id}/decisions`
- `POST /api/v1/membership/invitations`
- `GET /api/v1/membership/invitations`
- `DELETE /api/v1/membership/invitations/{id}`
- `POST /api/v1/membership/invitations/accept`
- `GET /api/v1/membership/link-requests`
- `POST /api/v1/membership/link-requests/{id}/decide`
- `POST /api/v1/members/{id}/transfers`
- `GET /api/v1/membership/transfers` — query: status, page
- `POST /api/v1/membership/transfers/{id}/decisions`
- `POST /api/v1/membership/transfers/{id}/cancel`
- `POST /api/v1/membership/transfers/{id}/clearance`
- `POST /api/v1/membership/transfers/{id}/retry`
- `POST /api/v1/members/{id}/financial-hold`
- `POST /api/v1/members/{id}/renewals`
- `POST /api/v1/members/{id}/category`
- `POST /api/v1/persons/me/photo`
- `POST /api/v1/members/{id}/documents`
- `GET /api/v1/membership/documents/{id}`
- `DELETE /api/v1/membership/documents/{id}`
- `POST /api/v1/membership/documents/{id}/approve`
- `GET /api/v1/members/{id}/cards`
- `POST /api/v1/members/{id}/cards/reissue`
- `POST /api/v1/membership/cards/{id}/revoke`
- `GET /api/v1/members/{id}/card`
- `GET /api/v1/membership/invitations/{id}/qr`
- `GET /api/v1/cards/verify/{token}`
- `POST /api/v1/membership/imports`
- `GET /api/v1/membership/imports`
- `GET /api/v1/membership/imports/{id}` — query: page
- `POST /api/v1/membership/imports/{id}/mapping`
- `POST /api/v1/membership/imports/{id}/resolve`
- `POST /api/v1/membership/imports/{id}/commit`
- `POST /api/v1/membership/imports/{id}/cancel`
- `POST /api/v1/membership/imports/{id}/retry`
- `POST /api/v1/membership/export`

## Contracts and limits

- Member lists support page/size (maximum 100), deterministic sort, advanced filters, and number-based cursors. Totals count affiliations; dashboard people counts use distinct person IDs.
- Optimistic mutations carry `version`; stale or conflicting state returns 409. Final approval activates once, with database unique constraints as the final guard.
- Draft applications snapshot the active workflow on submission. Decisions require the current stage’s authorized role and distinct reviewers; self approval is forbidden.
- Imports: CSV/XLSX, maximum 10 MB, 10,000 rows, 30 columns, 2,000 characters per cell. Form fields: clubId, categoryId, requestKey. Mapping, duplicate resolutions and final commit are explicit steps. Completed requests are idempotent.
- Export: CSV/XLSX, maximum 10,000 authorized filtered/selected affiliations. Formula-leading CSV text is escaped. Export actions are audited.
- Photos: PNG/JPEG up to 2 MB and 4096 pixels per dimension. Documents: PDF/PNG/JPEG up to 5 MB, matching signatures/extensions, authenticated access, retention controls.
- Transfer clearance is an administrative record. Payment processing belongs to Phase 6. Effective dates can schedule worker execution; blocked execution is visible and retriable.
- Error bodies expose a safe detail and status. Logs redact public QR tokens and never enable sensitive EF parameter values.

See the completion report for authorization grants, migration inventory, retention defaults and remaining operational limits.
