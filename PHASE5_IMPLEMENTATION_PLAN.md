# Phase 5 implementation plan

Audit: React 19/TypeScript/Vite 8, ASP.NET Core 10, EF/Npgsql/PostgreSQL 17. Existing Mails/MailDeliveryAttempts are the durable email outbox, NotificationService produces membership/governance/event notifications, and PersonPrivacy holds legacy consent. No WhatsApp, SMS, conversation or campaign infrastructure exists. Preserve all uncommitted Phase 4 work. Foundation 60 and event core 55 baseline checks pass.

1. Extend the single outbox to channel-aware dispatch; retain security-email behavior and add explicit provider capability/status, consent, suppression, authority, retry/dead-letter and quota controls.
2. Add participant-authorized conversations/messages, protected attachment metadata/storage and scoped reconnectable WebSocket invalidations; no hierarchy-wide private-message access.
3. Add versioned templates, typed audiences, frozen campaign/announcement snapshots and shared governance approvals; durable recurrence, event-relative schedules and idempotent opt-in automation.
4. Build one responsive communication workspace using the existing shell, drawers and components. Provider configuration shows actual readiness; AI stays disabled absent authorized configuration.
5. Migrate real PostgreSQL, test provider protocols against local mocks, scopes, consent, webhook signatures/dedup, restart recovery, concurrent campaigns and existing regressions. Benchmark large real-data fixtures and document measured limits.
6. Update schema/OpenAPI, roadmap and PHASE5_COMPLETION_REPORT.md. No real recipient or external provider tests without explicit authorization/configuration.
