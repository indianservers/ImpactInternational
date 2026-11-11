# Phase 5 communication API

Authenticated routes require the existing session, active workspace and CSRF token for mutations. Tenant scope and effective permissions are evaluated server-side. All paths below are relative to the deployment origin.

- `GET /api/v1/communications/people`
- `POST /api/v1/communications/conversations`
- `GET /api/v1/communications/conversations`
- `GET /api/v1/communications/conversations/{id}`
- `PUT /api/v1/communications/conversations/{id}`
- `GET /api/v1/communications/conversations/{id}/messages`
- `POST /api/v1/communications/conversations/{id}/messages`
- `POST /api/v1/communications/conversations/{id}/read`
- `POST /api/v1/communications/conversations/{id}/participants`
- `GET /api/v1/communications/conversations/{id}/context`
- `GET /api/v1/communications/stream`
- `GET /api/v1/communications/audiences`
- `POST /api/v1/communications/audiences`
- `POST /api/v1/communications/audiences/preview`
- `GET /api/v1/communications/templates`
- `POST /api/v1/communications/templates`
- `PUT /api/v1/communications/templates/{id}`
- `GET /api/v1/communications/revisions/{id}`
- `GET /api/v1/communications/campaigns`
- `POST /api/v1/communications/campaigns`
- `PUT /api/v1/communications/campaigns/{id}`
- `GET /api/v1/communications/campaigns/{id}`
- `GET /api/v1/communications/campaigns/{id}/recipients`
- `POST /api/v1/communications/campaigns/{id}/status`
- `GET /api/v1/communications/announcements`
- `POST /api/v1/communications/announcements/{id}/acknowledge`
- `GET /api/v1/communications/schedules`
- `GET /api/v1/communications/automations`
- `POST /api/v1/communications/automations`
- `PUT /api/v1/communications/automations/{id}`
- `POST /api/v1/communications/automations/{id}/submit`
- `GET /api/v1/communications/automations/{id}/executions`
- `GET /api/v1/communications/analytics`
- `GET /api/v1/communications/audit`
- `POST /api/v1/communications/messages/{id}/attachments`
- `POST /api/v1/communications/messages/{id}/dispatch`
- `GET /api/v1/communications/attachments/{id}`
- `GET /api/v1/communications/team-inbox`
- `POST /api/v1/communications/team-inbox/{id}/claim`
- `GET /api/v1/communications/unsubscribe`
- `POST /api/v1/communications/unsubscribe`
- `GET /api/v1/communications/webhooks/meta/{id}`
- `POST /api/v1/communications/webhooks/meta/{id}`
- `POST /api/v1/communications/webhooks/twilio/{id}`
- `POST /api/v1/communications/webhooks/email/{id}`
- `GET /api/v1/communications/capabilities`
- `GET /api/v1/communications/organizations`
- `GET /api/v1/communications/options`
- `GET /api/v1/communications/notifications`
- `GET /api/v1/communications/accounts`
- `POST /api/v1/communications/accounts`
- `PUT /api/v1/communications/accounts/{id}`
- `POST /api/v1/communications/accounts/{id}/verify`
- `POST /api/v1/communications/accounts/{id}/sync-templates`
- `GET /api/v1/communications/preferences`
- `PUT /api/v1/communications/preferences`
- `GET /api/v1/communications/policy`
- `PUT /api/v1/communications/policy`
- `GET /api/v1/communications/delivery`
- `POST /api/v1/communications/delivery/{id}/retry`

## Real-time transport

`GET /api/v1/communications/stream` upgrades to an authenticated same-origin WebSocket. Optional conversation selection is validated before its private invalidations are emitted. The server checks current session, workspace, participant and effective role access on each one-second poll. Clients reload authorized API resources on invalidation and reconnect after transport failure; no conversation body is broadcast globally. The development Vite and deployment nginx proxies forward WebSocket upgrades.

## Signed provider callbacks

Provider callbacks are narrowly exempt from cookie CSRF and use provider authentication instead: Meta raw-body HMAC-SHA256; Twilio HMAC-SHA1 over configured callback URL and sorted form parameters; email adapter timestamped HMAC. Do not expose an unsigned relay. Provider IDs deduplicate callbacks; status transitions cannot regress. Meta GET challenge uses a hashed verification token. Email bounce/complaint suppresses the contact. Unsubscribe GET displays confirmation; POST consumes a protected expiring opt-out token and changes promotional consent only.

See [completion report](../PHASE5_COMPLETION_REPORT.md) for supported capabilities and deployment limits.
