# Advanced ID Card Module — Implementation and Verification

Brand: Impact International. This enhancement is implemented in the existing React/ASP.NET/PostgreSQL application. It has not been committed or pushed during this request.

## Implemented behavior

- Native Konva front/back editor with millimetre geometry, CR80/custom sizes, 300 DPI output, drag, resize, rotation, multiselection, groups, locks, ordering, undo/redo, grid/guides, safe margins, bleed, typography, uploaded raster artwork and photo cropping.
- Typed registry for 18 authorized membership/organization/card fields. Arbitrary private fields, executable bindings and remote asset fetching are rejected. Authorized sample-member binding and server previews use CRM data.
- Existing certificate templates supply the template master. Immutable published versions, scope/category/position constraints, inherited protected layers, duplication, version concurrency and tenant defaults support reuse. Original membership/person records remain the source of truth.
- Existing DigitalCards supply card identity. Draft, independent review, generation and issuance are separate states. Immutable snapshots freeze field values and private files. Replacement activates transactionally and invalidates the previous QR only when issued. Revocation, audit history, owner downloads and attributed correction/lost-card requests are integrated.
- Existing governance approvals, tenant/scope grants, document/version storage, notifications and audit are reused. No second authentication or membership system was introduced.
- Durable PostgreSQL bulk selections support reviewed jobs, bounded per-item transactions, partial success, failed-item retry, cancellation, expiring private exports and current authorization rechecks. Existing-issued-card exports preserve identities without reissuance.
- ZIP, PDF, paired/front/back images and physical print PDFs are available. A4/A3/custom paper, margins/gaps, duplex long/short edge, calibration and crop/bleed settings use a shared physical renderer. Exact PDF MediaBoxes are preserved after native rendering.
- Member card view and opaque public QR verification use existing application routes and privacy rules. Draft/generated cards do not verify as official cards.

## Evidence

Final server build: zero warnings/errors. Client production build passes. EF reports no pending model changes. All six enhancement migrations applied to the existing test database; empty-database migration, rollback to IntelligenceFoundation and reapply passed. The schema now has 40 migrations.

- 50 real PostgreSQL core checks passed on the final API.
- 29 extended checks passed on the final API: vault/photo access, inherited layers, partial failure/retry, immutable database guards, cancellation and actual printed QR/front matching.
- 8 editor history/physical-coordinate checks passed.
- 3 QR publication security checks passed (missing, undersized and overlaid QR).
- 8 live browser checks passed: physical drag, undo/redo, duplicate, back-side QR/text, group and ungroup. Screenshot and checks: `docs/idcard-designer.png`, `docs/idcard-ui-checks.json`. These checks do not constitute full mobile or accessibility acceptance.
- 23 selected Phase 1–8 regression suites, 902 assertions passed before the final existing-card export addition. Results: `docs/idcard-regressions.json`. Core/extended tests were rerun after that addition; the complete old suite was not rerun again.
- Dependency audits report no known vulnerable npm or NuGet packages at verification time; results are in `docs/idcard-npm-audit.json` and `docs/idcard-nuget-audit.txt`.

Independent PDF inspection verifies exact CR80 and A4 dimensions, actual printed field positions, circular photograph clipping, and decoded duplex QR matching for each front ordinal. Generated examples are private ignored test artifacts under `test-results`.

## Measured scale

Actual synthetic PostgreSQL fixture: 101,035 people and 10,013 clubs across tenants, scoped to the selected tenant. One durable worker on the shared development workstation; standard two-sided design, QR, no synthetic photographs. These are measured development results, not production capacity guarantees.

- 1,000 full cards: 146.485 seconds including private PDF/PNG rendering and ZIP packaging; 6.83 cards/second; 391,374,094-byte ZIP with 3,001 entries; CRC verified; API peak RSS 311.4 MiB.
- 10,000 full cards: 1,656.497 seconds; 6.04 cards/second; 3,939,711,615-byte ZIP with 30,001 entries; CRC verified; zero failures; API peak RSS 343.2 MiB.
- Four simultaneous protected downloads completed with identical SHA-256 results. Raw samples: `docs/idcard-scale.json`.
- Existing 1,000-card A4 duplex print: 49.442 seconds, 250 pages, 1,995,530 bytes, peak API RSS 297.8 MiB, zero failures. Existing card records remained unchanged. Evidence: `docs/idcard-print-scale.json`.

Reproduction: run `tests/id_cards.py`, then `tests/id_cards_extended.py`, `tests/id_cards_qr_security.py`, and `node tests/id_card_design.mjs` against the configured PostgreSQL API. Scale scripts require an isolated synthetic fixture; the print benchmark references the measured 1k job and is fixture-specific. Regression runner accepts an optional starting suite. CI includes core/extended card tests and editor helper checks.

## Limits and remaining acceptance work

The combined PDF/print job limit is 1,000 cards because the PDF renderer buffers the document; use ZIP/image jobs up to 10,000. Private storage must be capacity planned (the measured 10k archive alone is approximately 3.94 GB). The worker serializes jobs using a database advisory lock; horizontal worker throughput has not been benchmarked.

Physical printer feed alignment, card stock, CMYK/ICC output, lamination and commercial printer compatibility have not been certified. Calibrate on the intended printer before production. No external background-removal or barcode provider is configured. Native grouping is flat; this editor is not a complete replacement for Canva. Advanced backend bulk filters exceed the currently exposed basic organization/category/date UI filters. Historical published-version selection for existing-card exports is available through the API; the UI chooses the latest published version. Parent asset access is enforced server-side; inherited artwork preview across restricted vault scopes needs additional UI acceptance. Full mobile member-flow, exhaustive keyboard/accessibility, additional expiration/revocation edge cases, keyset latency and production restore/operational acceptance remain to be verified for this module.

No public hosting or production deployment was performed. Existing encryption purposes/application name remain unchanged to preserve protected data compatibility. The implementation is reviewable locally; the remaining acceptance items above prevent claiming every item of the broad brief has been certified.
