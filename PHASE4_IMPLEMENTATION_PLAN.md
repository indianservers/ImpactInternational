# Phase 4 implementation plan

Audit: React 19/TypeScript/Vite, ASP.NET Core 10, EF/Npgsql/PostgreSQL 17; shared scoped Access, Person/Membership, GovernanceRequest + ApprovalEngine, NotificationService/MailWorker, DataGrid/drawers and protected document storage. No event module exists. Phase 1 baseline: 60 checks passed before edits.

1. Add tenant-qualified event entities, indexes and constraints; extend existing approvals/documents rather than creating parallel engines.
2. Implement scoped event lifecycle, sessions/audience/team, queued invitations/reminders, distinct RSVP/registration, transactional capacity/waitlist and opaque passes/check-ins.
3. Add feedback, documents/certificates and scoped analytics with explicit denominators.
4. Build lazy event discovery/calendar/dashboard, six-step wizard and contextual event workspace, invitation/pass/check-in/member flows with existing components.
5. Migrate actual PostgreSQL, test cross-tenant/scope/expiry/concurrency and Phases 1–3 regressions; measure invitation and registration/check-in load on separate scale fixtures; verify desktop/mobile UI.
6. Record exact evidence/limits, update API/schema snapshots and roadmap. Payment stays disabled; offline verification is not claimed. No new hosting or external communications service is introduced.
