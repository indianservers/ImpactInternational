# Phase 7 completion report

## Delivery status

**Core finance workflows are implemented and verified. This is not a claim that every advanced item in the Phase 7 master brief is finished.** Four additive migrations extend the existing application to 33 migrations and 28 finance tables. No member, organization, approval, invoice, or payment master was duplicated. The application now has one operational finance engine; SaaS platform billing remains outside it.

Backend and frontend builds pass. **129 new financial checks and 737 prior-phase regression assertions passed.** The database rejected direct attempts to alter posted journals, finalized invoice lines and unsupported paid balances. Full production deployment, external Stripe sandbox certification and the advanced capabilities listed below remain outstanding.

## Implemented workflows

### Entity ownership and financial authority

Financial entities explicitly own books, native currency, decimal precision (0–4), country, fiscal start month, accrual policy, address/tax registration, invoice and receipt sequences, accounts, periods and gateway aliases. Organizational units are not automatically legal entities. Collection organizations require an explicit recorded authority. Entity-specific user grants (Owner, Treasurer, Billing, Reviewer, Reader) work together with scoped application permissions. Hierarchy or association super-administrator status alone does not grant access to an independent entity's books.

Existing Club Treasurer roles receive finance permissions and access to the shared approval/document interfaces. They still require an explicit financial entity grant. Member access uses existing Person/User/Membership identities and authorized association workspaces. Bank numbers/routing details use the existing Data Protection service and are returned only as masked numbers; no raw card details or CVV are accepted.

### Fees, assessment and invoices

Immutable effective-dated dues definitions support category/organization eligibility, configurable purposes and components, monthly/quarterly/half-yearly/annual cycles through configured month counts, once-only joining/life charges, actual-calendar-day proration, early period assessment, due-date grace days and authorized documented concessions/full waivers. Stable charge codes prevent overlapping definitions and duplicate membership/period billing across definition versions. Assessment freezes the plan and calculated invoice lines. Life/once charges reuse the original assessment across later periods.

Draft central invoices support multiple decimal-calculated lines, quantities, discounts, configured tax percentages, linked people, projects/events, descriptions and notes. Issue allocates a unique entity invoice number, freezes issuer details/content and posts receivables. Financial balance and document state are separate. Issued history is corrected through credit notes, not editing/cancellation; draft cancellation requires authority and reason. Amount-only credits consume original positive lines in stable identifier order, using cumulative tax rounding, including zero-value waived components. PDFs and notification actions use real saved records.

Durable queued dues jobs process 50 memberships per transaction, retain a cursor and stop visibly on errors. Retries preserve already committed chunks and unique assessment keys. Scheduled auto-bill plans enqueue recurring periods; jobs recheck creator authority. Manual invoice notifications are limited to once per 24 hours and overdue/upcoming reminders to once per seven days. A member without a linked user does not create an invalid in-app recipient.

### Payments, allocation, settlement and refunds

Manual bank transfer, cash, cheque and UPI records start Pending. An independent financial verifier, distinct from both recorder and linked payer, must verify them before receipt generation or allocation. Verification posts collection clearing and member advances. Partial allocations debit advances and credit the matching payer/entity/currency receivable. Overpayments remain unapplied. Unique references and request keys prevent duplicate recording; allocations have independent retry keys.

Settlement is a separate evidenced action posting clearing to a selected cash/bank account and fees to expense. It does not happen automatically on gateway capture. Bank statement imports accept up to 500 structured rows, deduplicate import keys and match a settled payment's historical posting without posting it again. Manual bank/provider refunds and chargebacks are separately evidenced corrective records; allocated refunds reopen receivables, unallocated refunds reduce advances. These record completed external actions and do not initiate a bank or gateway transfer.

Receipt PDFs are generated only for verified payments and show refunded amounts and separate settlement state. They do not imply donation deductibility. All underlying financial arithmetic uses C# decimal/PostgreSQL NUMERIC, never binary floating-point calculations. Browser formatting is presentation only; authoritative totals are computed by the server.

### Provider integration

A provider interface and actual Stripe Checkout HTTP adapter are implemented. Server configuration binds an account alias to one financial entity; secrets remain outside the database/API/UI. Nonproduction accepts test credentials; custom API bases are restricted to Development loopback test doubles. Currently enabled checkout currencies are USD/EUR/GBP/INR/CAD/AUD/SGD/AED with two decimal places and JPY with zero. Other native currencies can have books but online checkout remains unavailable until their provider precision policy is implemented.

Checkout uses frozen server amounts, an internal intent identifier and a provider idempotency header. Page refresh resumes an active intent. Redirects never prove payment. The webhook verifies the raw body HMAC, timestamp freshness and event identity, then retrieves the authoritative provider session and compares paid status, currency, amount and client reference before one verified payment/allocation. Duplicate signed events are recognized before another provider request, with an additional transactional check for races. Out-of-order failed/expired messages cannot downgrade captured payments.

**Only Stripe is implemented. Razorpay, Cashfree and other adapters are not active.** No production credentials, live charges or external sandbox transactions were used. Local protocol tests exercised actual HttpClient requests and signed callback handling against a loopback Stripe test double. Production merchant onboarding, external sandbox/Stripe CLI verification, endpoint registration, provider account/contract review and operational monitoring are required before enabling collections. Supported payment methods are those provided by the configured account; no standalone card/UPI/net-banking support is claimed.

### Accounting and inter-entity positions

The account template distinguishes assets, liabilities, funds/equity, income and expenses. Every posting has an entity, native currency policy, date/open period, source, narration, creator and immutable debit/credit lines. Transactions and deferred PostgreSQL triggers require balanced positive journals. Posted lines/journals and financial audit entries are append-only. Manual journals can be reversed through distinct entries; system documents require their own credit/refund/correction path. Period closure/reopening requires authority and an audit reason; posted period dates and entity accounting identity are protected.

Invoices may carry billed components for an explicitly authorized same-currency beneficiary entity. Creation requires authority in both entities. Issue records the collector's liability and beneficiary's receivable/income; this does not move bank funds. An explicit, completed, referenced transfer with authority in both books posts matching settlement journals atomically. Fully collected entitlements are required. Reports do not silently combine legally independent bank accounts or perform undefined elimination/consolidation.

### Budgets, claims, reimbursements and funding

Annual/district/national/committee/event/project budget definitions support multiple lines, shared independent governance approval, preserved revisions and superseded versions. Submitted claims are commitments; approval consumes the commitment and books an expense/payable. Actual budget use means approved accrued expense, not just cash paid. Available = approved limit − submitted commitments − approved actual expense; reimbursement does not subtract the expense again. Revision views retain predecessor-linked actuals.

Claims reuse the private document vault, multiple gross expense lines, category, claimant/payee and project/event/budget references. Repeated document evidence is rejected, the shared review snapshot is bound to its request and claimant, and approval requires the reviewer to have access to original evidence under vault rules. Self/requester approval and self reimbursement are blocked. Partial reimbursements are separately recorded and post payables versus cash/bank. Shared governance may be configured with multiple review stages.

Individuals reuse Persons; organization contributors use a named counterparty without inventing a member record. Donation/sponsorship pledges do not increase cash. Recognizing a receivable issues a central invoice; collection/receipts use the same payment engine. Historical reporting rates are immutable; the decimal translation endpoint returns original amount/currency, translated amount and the dated/source-identified rate without rewriting native books.

### Events, projects and financial portals

Paid event prices are bound to the event's authorized collector and verified member/guest classification, not a cheaper browser-selected label. Existing registration/capacity/pass checks remain authoritative. Paid registrations remain Pending Payment, use the central invoice engine, and confirm only after full verified allocation or an authorized credit. A partial refund removes confirmation. Existing project IDs link central contributions, pledges, invoices, budget/expense records and journal reports; Phase 6 estimates are unchanged and never treated as booked expenses.

The existing shell exposes Finance overview, invoice directory/detail/PDF, dues schedules/assessment/jobs, payments/receipts, bank reconciliation, accounts/journals, budgets/revisions/actuals, expenses/approvals/reimbursements, donations/sponsorships, event/project views, inter-entity records, reports, settings/audit and My finances. Multi-line drawers use server-calculated money; cached queries include the active workspace. Entity dashboards do not invent bank balances. Pay Now appears only when a configured adapter is available. The member view exposes only that user's financial records in the authorized association.

## Reports and exports

The API/UI provides 22 named views: dues register, outstanding, ageing, collections, invoice register, receipts, refunds, reconciliation, income statement, balance sheet, trial balance, general ledger, account ledger, bank/cash book, expenses, budget definitions/actual drilldown, event finance, project finance, donations, inter-entity positions, financial audit and member statement. Dates/entity/native currency and source/person/account filters are enforced server-side where applicable. Journal-derived statements reconcile; current invoice allocations and ageing are explicitly distinguished from historical as-of receivables. Ledger entries support keyset pagination.

CSV and PDF exports currently provide **account summaries**, clearly labeled in the UI; they do not export every detailed register or a full unbounded ledger. Detailed directories/registers are bounded, with paging available for the main invoice/payment/journal/statement/audit paths. No Excel workbook export or formal consolidated financial statements are claimed.

## Database and API

Migrations:

- `20261111042130_FinancialFoundation`: finance entities/grants/collection organizations, accounts/periods, journals/lines, plans, invoices/lines/credits, payments/allocations/refunds/intents/webhooks, banks/statements, expenses/reimbursements, budgets, pledges, entitlements/transfers, reporting rates, jobs and audit; tenant/entity-qualified relationships and immutable/balanced posting safeguards.
- `20261111043443_FinancialBillingIdentity`: stable charge codes, invoice request hashes, effective-plan overlap protection and Treasurer permissions.
- `20261111044010_FinancialPostingGuards`: posting/read indexes, source-reconciled payment/invoice balances, required source journals and protected/non-overlapping periods.
- `20261111045505_PaidEventFinanceState`: additive Pending Payment registration state and verified-payment guards integrated with the existing event capacity/lifecycle trigger.

All 33 migrations applied to an empty isolated database. All four finance migrations were rolled back to Phase 6 and reapplied there. This is **empty QA rollback validation**, not a promise that live financial history can be rolled back. Pending paid bookings or referenced documents can block a destructive downgrade; use backups and forward repairs for live books. No pending EF model changes remain. Existing demo, test and scale databases were upgraded without replacing prior phases.

See [API route inventory](docs/phase7-api.md), [OpenAPI](docs/openapi.json) and [schema](docs/schema.sql). Finance permissions are versioned through the existing Access system; decision execution reuses GovernanceService/ApprovalEngine. NotificationService, the existing mail/in-app outbox and CommunicationWorker carry finance updates with preferences and effective entity sender checks.

## Validation evidence

- Core finance: **51** checks — arithmetic, numbering, receipts, verification, partial/full/unapplied funds, settlement, refunds/chargebacks, bank matching, credit preservation, journal reversal, periods, dues/life/waivers, reports and tenant/member boundaries.
- Extended: **29** — shared budget/expense review, duplicate evidence, commitments/revisions/reimbursements, same-currency entitlements/settlement, pledges, actual-day proration, effective-plan overlap, durable billing, historical rates, concurrent allocation and direct unbalanced SQL rejection.
- Event/project integration: **10** — pending paid bookings, central invoice, verified alternative confirmation, partial refund, project ledger and preserved planning estimates.
- Security/rounding: **21** — explicit entity access/revocation, privilege separation, input nulls, request hashes, native currency, server gateway bindings, database balance/content guards, payer conflicts, historical conversion, in-app delivery/unlinked identities and cumulative tax credit rounding.
- Provider protocol: **18** — checkout/idempotency/resume, redirects, signature freshness, server-side amount validation, duplicate/out-of-order events, clearing, concurrent replay and balanced books.
- Prior Phases 1–6: **737** assertions across 18 suites. The first regression run exposed an incomplete synthetic Person privacy fixture; fixture creation was corrected and the affected imports suite and remaining suites passed. No prior membership implementation was replaced.

[Core](docs/phase7.json), [extended](docs/phase7-extended.json), [sources](docs/phase7-sources.json), [security](docs/phase7-security.json), [providers](docs/phase7-providers.json), [regression inventory](docs/phase7-regression-results.json), [migration evidence](docs/phase7-migrations.json).

Final backend build: zero warnings/errors. Frontend TypeScript/Vite build passed. [NuGet audit](docs/phase7-nuget-audit.txt) and [npm audit](docs/phase7-npm-audit.json) report no known vulnerabilities in the checked dependency sets. This does not replace a production security assessment.

Browser QA verified the dark desktop workspace, a real two-line draft saved through the UI, and a mobile invoice at 390px outer width with no page overflow. The temporary viewport was reset; existing theme was preserved. The actual issued invoice PDF was rendered and visually checked. [Desktop](docs/phase7-finance-desktop.png), [saved draft](docs/phase7-browser-draft.png), [mobile](docs/phase7-invoice-mobile.png), [PDF preview](docs/phase7-invoice-preview.png). Broad device/accessibility/print localization testing remains outstanding.

## Measured performance

Local shared Windows workstation, PostgreSQL 17.6 loopback, .NET 10 Debug, warm sequential HTTP reads; 30 reads per route, zero errors. Final fixture: **101,035 people, 101,009 invoices, 101,009 journals and 202,018 journal lines**.

- Invoice directory p95: **28.84 ms**.
- Trial balance report p95: **308.02 ms**.
- General ledger report p95: **273.87 ms**.
- Entity dashboard p95: **168.39 ms**.
- Real API durable assessment: **1,009 member invoices in 28.127 seconds**, including 15.289 seconds to first progress/idle worker pickup. Invoice uniqueness, actual issuance and balanced source postings were enabled.
- Signed duplicate callback load: **80 events at concurrency 8, p95 7.15 ms**, zero errors and one captured payment. This measures repeated delivery, not 80 distinct online payments.

The 100,000-invoice base was generated directly by a guarded SQL fixture with balanced source journals and database triggers enabled; its synthetic customer invoices are not 100,000 separate API membership assessments or settled bank transactions. The separate 1,009-member job exercised the real billing path. Formal production concurrency, 100,000-member API billing, 100,000 member-linked balance distribution and distinct-payment/provider settlement throughput remain unmeasured. No universal latency/SLO claim is made.

[Scale](docs/phase7-scale.json), [actual billing](docs/phase7-billing-scale.json), [callback replay](docs/phase7-webhook-load.json), [EXPLAIN ANALYZE](docs/phase7-explain.json). The scale test defaults to read-only; `--fixture` is explicit. Actual billing throughput requires `--run` on the isolated scale database.

## Known limitations and remaining acceptance work

- SaaS subscription revenue/billing is not implemented. Operational entities cannot select a SaaS purpose, and no automatic cross-platform consolidation/collection is provided.
- Fee rules do not yet implement family/dependent billing, age/country concession matrices, installment schedules, automatic late-fee policies, fee-specific independent waiver approval or activation-triggered immediate catch-up for members added after a completed period job. Renewals can be assessed early and recurring dates are supported, but not every renewal business rule is automated.
- Books are accrual and one native currency per entity. Reporting conversion is explicit and dated; transaction-level multi-currency/revaluation, cash-basis accounting, formal hierarchy consolidation/eliminations and dedicated district/national/international comparative dashboards remain to be designed. Operators select only explicitly authorized entity books.
- Taxes are configurable line percentages and issuer registration details, not a country tax-code/exemption/filing engine. No automatic deductibility, VAT/GST compliance, e-invoicing certification or jurisdiction-specific donation wording is asserted.
- Stripe is the sole adapter. External sandbox certification, automated provider refund initiation, dispute/chargeback ingestion, payout/settlement polling, split-payment contracts, bank feeds, partial settlements and multiple bank account disbursement policies remain unfinished. Refund/disbursement/transfer endpoints record authorized completed external evidence; they do not move funds.
- Paid events currently require a signed-in Person, direct registration without a separate registration approval and no companions. Anonymous guest checkout, category matrices beyond verified Member/Guest, cancelled-booking automatic refunds and the complete paid waitlist/refund/session-reservation lifecycle need further work. Financial staff may confirm a fully reconciled manual alternative explicitly.
- Claims reuse configurable shared stages, but there is no dedicated manager/treasurer policy editor, category claim limits, partial claim approval or automatic receipt-content fraud detection. Rejected proposals require a new corrected record. Supporting evidence is not automatically made visible to reviewers; its existing vault permissions must be satisfied.
- Budget actuals use linked approved expense accruals and predecessor revisions. Purchase-order commitments, encumbrances, cost-center accounting, all budget revision edge policies and every requested contribution/funding drilldown are incomplete.
- Financial configuration is currently mostly additive. Advanced configuration still uses some record identifiers; searchable pagers, full account/bank/entity editing, exchange-rate administration and settings conflict UX need more refinement. Certificate/event branding infrastructure is reused at the font/shell level; finance PDFs currently use a fixed clean text layout, not a full logo/theme template studio.
- CSV/PDF are account-summary exports; Excel and complete register exports/background prepared report archives remain incomplete. Some bounded directories, option selectors and member history need additional paging/search controls. Current-balance ageing is labeled explicitly rather than claimed as a reconstructed historical as-of debtor report.
- Notifications currently use email/in-app with existing preferences/outbox and bounded reminder policy. Finance-specific approved WhatsApp/SMS templates, all requested lifecycle automations and configurable escalation schedules are not implemented. Do not infer live sender/provider readiness from the presence of these channels in Phase 5.
- Tenant-level financial advisory serialization favors correctness and deadlock avoidance for cross-entity posting. Large concurrent independent-entity write throughput and optimized bulk-query posting require further measurement. No cache bypasses the ledger. Live backup/restore, Docker/proxy rollout, operational alerting, a complete accessibility audit and financial administrator penetration testing remain unverified.

## Phase 8 dependencies

Prioritize production observability, durable gateway retry/settlement/dispute operations, external sandbox certification, backup/restore and protected-key recovery, ledger reconciliation monitors and migration/change-control procedures. Add reviewed country policies, report projections/exports, member balance pagination, formal consolidation/FX policy and full lifecycle handling before treating the advanced requirements above as complete. Preserve immutable source journals and entity grants; do not replace accounting balances with cached UI totals. Existing elections/secret-ballot governance remains a separate future protocol, unrelated to financial approval stages.
