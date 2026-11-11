# Phase 8 initial audit

The supplied brief ends at section 9, “Support secure custom-domain onboarding.” This audit covers the supplied scope and the operational release objective; no missing later text is assumed.

## Verified module inventory

Classification is based on source, migrations and the Phase 1–7 evidence, not the presence of prompts. “Complete and tested” refers to the stated core workflow, not every future aspiration.

- Application shell, authentication/session/CSRF, tenant hierarchy, scoped RBAC: **complete and tested core**; production MFA/SSO, shared multi-replica rate limits and target TLS remain **missing/externally blocked**.
- Membership CRM, lifecycle/transfer approvals, privacy directory, import/export and identity cards: **complete and tested core**; some large selectors and full translation/a11y coverage **partial**.
- Club administration, officer terms/handovers, committees and independent governance: **complete and tested core**; elections/secret ballots **missing**, never implied by appointments.
- Events, invitation/RSVP, capacity/QR admission and participation: **complete and tested free-event core**; richer paid waitlists/cancellation/refund/guest lifecycle **partial**. Paid registration now has central finance guards and coverage.
- Communication Center/provider abstractions/in-app/outbox/campaign review: **implemented and locally protocol tested**; real sender/provider accounts **blocked by external configuration**. Existing personal trigger automation is **partial**, not a visual cross-module workflow engine.
- Meetings, agendas/minutes/quorum, shared tasks, projects, volunteers and verified hours: **complete and tested core**; richer recurrence/resolutions/training and project planning remain **partial**.
- Document Vault/versioning/sharing/retention and certificate issuance/revocation: **complete and tested core**; AV/OCR/legal-hold/full design studio **missing/partial**. No document knowledge index exists.
- Finance: **implemented/tested core**, one operational engine with explicit entity grants, decimal invoices/payments/journals/periods/budgets/claims. SaaS billing, formal FX/consolidation, automated settlement/dispute/refund ingestion and full register exports **missing/partial**. Stripe is **implemented/protocol tested**, external sandbox certification **blocked**.
- Global analytics/catalog, universal cross-module search and controlled natural-language assistant: **missing**. Existing module reports have distinct definitions and access boundaries; aggregation must not bypass them.
- Branding: stored tenant accent/preferences and configurable hierarchy exist; public login identity/terminology/custom domains and feature switches are **partial/missing**.
- Deployment: Docker/Compose/nginx definitions exist; **implemented but not target-tested**. CI still primarily runs foundation checks. No verified local recovery drill or complete release gate exists.

## Release blockers and implementation order

1. Preserve all Phase 7 work. Run baseline integration checks; review any failure before optional additions. Expand CI to real PostgreSQL suites and explicit provider fakes.
2. Centralize authorized, documented metric definitions and truthful current/period metrics. Separate unique people, memberships, joint-project ownership and independent entity books. Historical transfers must not be fabricated as continuous membership.
3. Add bounded universal search that reuses source authorization, plus an allowlisted read-only assistant and current-version document retrieval. Default to local factual tools; remote model use requires explicit tenant/server configuration and per-request acknowledgment. No SQL or mutation tools.
4. Extend the existing communication automation/revision/execution tables and shared governance review. Deliver bounded versioned branches/delays and safe personal follow-ups, not arbitrary scripts or financial actions.
5. Add isolated branding and DNS challenge onboarding; ownership verification is separate from operator TLS/ingress activation. Hostname never grants membership or financial authority.
6. Add liveness/readiness, operational diagnostics, measured search/analytics load and a local database/object/key recovery drill. Document production gates rather than mark an untested production deployment ready.

## Existing architecture and invariants

.NET 10/EF/Npgsql/PostgreSQL, React 19/TS/Vite, opaque cookie sessions and CSRF, global Persons and tenant Memberships, adjacency hierarchy, atomic permissions, shared GovernanceService/ApprovalEngine, NotificationService/Mails and CommunicationWorker, CommunicationStorage, PDFsharp. There are 33 migrations before Phase 8 and no second finance/member/organization engine is needed. Money remains decimal and posted journals immutable. Private object bytes and Data Protection keys are recovery dependencies.

## Initial findings

The hosted workflow still has a Phase 1 title and foundation-only test matrix; release coverage must grow. Public metadata and search must never expose confidential meeting titles, private knowledge excerpts, recipient details or independent entity invoices. Current scope should not be described as historical hierarchy attribution. Remote provider credentials and TLS/domain provisioning cannot be inferred from local test success. No production-ready label is authorized by this audit.
