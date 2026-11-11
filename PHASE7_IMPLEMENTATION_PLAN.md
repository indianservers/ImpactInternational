# Phase 7 implementation plan

The repository audit found no existing invoice/payment/accounting engine. Event `Paid` is explicitly disabled, project `EstimatedBudget` is a planning estimate, and SaaS subscriptions have no ledger. Reuse global Persons/Memberships, Orgs/Tenants, scoped Access, governance approvals, NotificationService/outbox, CommunicationStorage and PDFsharp.

1. Add explicit financial entities and user grants; never infer access to independent entity books solely from hierarchy. Establish currency precision, periods, account templates and immutable balanced posting.
2. Implement effective-dated dues, idempotent assessment, frozen invoices, credit notes, manual receipt verification, allocations, refunds, settlement and inter-entity positions with one engine.
3. Add provider-independent checkout with a Stripe adapter, signed/replay-protected webhooks and server verification. Keep credentials in server configuration; no live test payments.
4. Reuse governance for expense and budget review; connect event/project sources, pledges and central finance reports. Add durable billing/reminder work.
5. Build finance/entity/member workspaces in the existing shell, real forms/actions and protected financial PDFs/CSV.
6. Validate PostgreSQL constraints, financial invariants, permissions, concurrency, prior-phase regression and measured large fixtures. Document actual capability and remaining limits in the completion report and roadmap.

Accounting policy: accrual, one native currency per entity initially. Allocation entitlements do not transfer funds. Bank book includes reconciled cash; gateway capture initially sits in clearing. Configured tax rates are operator supplied, not jurisdictional advice. Formal multi-currency consolidation requires a separate reviewed policy.
