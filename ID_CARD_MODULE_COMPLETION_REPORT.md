# Advanced ID Card Module — Implementation and Verification

Brand: Impact International. The enhancement and its remaining software acceptance items are implemented in the existing React/ASP.NET/PostgreSQL application. The requested print acceptance target is PDF review; no physical printing is required.

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

Final server build: zero warnings/errors. Client production build passes. Client lint completes successfully with React advisory warnings (existing render-purity, ref/history and effect/dependency patterns); no zero-warning lint claim is made. EF reports no pending model changes. All six enhancement migrations applied to the existing test database; empty-database migration, rollback to IntelligenceFoundation and reapply passed. The schema now has 40 migrations.

- 50 real PostgreSQL core checks passed on the final API.
- 30 extended checks passed on the final API: vault/photo access, inherited layers, partial failure/retry, immutable database guards, cancellation and actual printed QR/front matching.
- 8 editor history/physical-coordinate checks passed.
- 3 QR publication security checks passed (missing, undersized and overlaid QR).
- 8 live browser checks passed: physical drag, undo/redo, duplicate, back-side QR/text, group and ungroup. Screenshot and checks: `docs/idcard-designer.png`, `docs/idcard-ui-checks.json`. Additional mobile, keyboard, inherited artwork and bulk-form acceptance is recorded below.
- All 23 selected Phase 1–8 regression suites were rerun against the acceptance server and passed: 902 assertions, recorded in `docs/idcard-regressions.json`. The isolated provider contract suite also passed all 15 checks.
- Dependency audits report no known vulnerable npm or NuGet packages at verification time; results are in `docs/idcard-npm-audit.json` and `docs/idcard-nuget-audit.txt`.

Independent PDF inspection verifies exact CR80 and A4 dimensions, actual printed field positions, circular photograph clipping, and decoded duplex QR matching for each front ordinal. Generated examples are private ignored test artifacts under `test-results`.

## Measured scale

Actual synthetic PostgreSQL fixture: 101,035 people and 10,013 clubs across tenants, scoped to the selected tenant. One durable worker on the shared development workstation; standard two-sided design, QR, no synthetic photographs. These are measured development results, not production capacity guarantees.

- 1,000 full cards: 146.485 seconds including private PDF/PNG rendering and ZIP packaging; 6.83 cards/second; 391,374,094-byte ZIP with 3,001 entries; CRC verified; API peak RSS 311.4 MiB.
- 10,000 full cards: 1,656.497 seconds; 6.04 cards/second; 3,939,711,615-byte ZIP with 30,001 entries; CRC verified; zero failures; API peak RSS 343.2 MiB.
- Four simultaneous protected downloads completed with identical SHA-256 results. Raw samples: `docs/idcard-scale.json`.
- Existing 1,000-card A4 duplex print: 49.442 seconds, 250 pages, 1,995,530 bytes, peak API RSS 297.8 MiB, zero failures. Existing card records remained unchanged. Evidence: `docs/idcard-print-scale.json`.

Reproduction: run `tests/id_cards.py`, then `tests/id_cards_extended.py`, `tests/id_cards_qr_security.py`, and `node tests/id_card_design.mjs` against the configured PostgreSQL API. Scale scripts require an isolated synthetic fixture; the print benchmark references the measured 1k job and is fixture-specific. Regression runner accepts an optional starting suite. CI includes core/extended card tests and editor helper checks.

## Remaining software acceptance completed

- The bulk form exposes country, current leadership position, membership status, card status, expiry interval, name/number search and explicit membership IDs, in addition to organization/category/join dates. Eligibility preview uses the same server selection filters.
- Existing-card export lists historical published versions (including archived templates), disables replacement selection and preserves original identities/files. Issuance continues to choose only active templates. Template/version lists remain bounded.
- Child designers render frozen parent artwork through the authorized published-version endpoint, without broadening vault access. Tests demonstrate direct parent vault download denied, protected frozen artwork readable, child draft save succeeds, and sibling template access denied.
- Keyboard save works from the name field. Layer controls expose selected/locked state; the canvas is keyboard focusable; front/back controls expose pressed state. Shared dialogs trap keyboard focus, support Escape and restore focus; visible focus styles and mobile image sizing are included.
- A real fictional member was tested at 390×844: no horizontal overflow, full-resolution image load, front/back flip, protected PDF download and correction request. The downloaded PDF was independently opened, measured and rendered. Live desktop checks also verified inherited logo preview, keyboard save, historical versions, filter preview and mutually exclusive export/reissue modes. Evidence: `docs/idcard-mobile.png`, `docs/idcard-inherited-artwork.png`, `docs/idcard-bulk-filters.png`, `docs/idcard-acceptance-ui.json`.
- 23 additional PostgreSQL edge checks cover eligibility expiry, immediate QR invalidation, revoked/expired archive denial, explicit replacement, identity-preserving existing-card export, default template fallback, archived-template generation/publishing, parent asset access, sibling isolation and bulk filters. Evidence: `docs/idcard-acceptance-edges.json`.
- 16 additional real PDF checks pass for A3 portrait/landscape, custom paper and A4 short-edge duplex with bleed/calibration. They verify exact page dimensions, actual member text and decoded QR/front identity. Evidence: `docs/idcard-pdf-acceptance.json`. Including core/extended/security/helper checks, 130 automated card checks passed.
- Keyset paging on the 101,035-person fixture traversed 1,500 distinct memberships without duplicates: 72.16 ms median / 110.41 ms p95 over 30 pages. Public verification: 5.02 ms median / 12.77 ms p95 over 20 requests. Conditions and maximums: `docs/idcard-latency.json`.
- An isolated logical PostgreSQL restore plus 110 referenced private card/image objects returned an authenticated owner's identical PDF bytes and a valid opaque QR. Evidence: `docs/idcard-recovery.json`. The restore used the same host keyring; no off-host production failover is claimed.

## PDF review and operational boundaries

Open `output/pdf/Impact-International-ID-Cards-Review.pdf` to inspect the fictional two-member A4 front/back sheet. Individual card fronts/backs and actual sheet pages were rendered and visually inspected. The user explicitly chose PDF review instead of physical printer certification. The sample is test data; subsequent fixture reissues/revocations can invalidate its QR.

Combined PDF/print outputs remain deliberately bounded at 1,000 cards; ZIP/images support 10,000. Private storage requires room for archives plus original PDFs/PNGs, retained replacements and backups. The database lock deliberately serializes workers across replicas, so extra replicas do not imply horizontal throughput. These are documented operating constraints rather than unverified implementation claims.

No external background-removal or barcode provider, CMYK/ICC workflow, nested grouping, public hosting or production deployment is advertised. Existing secure QR verification, raster photo cropping and flat native groups are the supported editor behavior. PDF review completes the requested print acceptance; physical stock/lamination/printer certification is outside the revised scope. Accessibility checks cover the tested keyboard/member flows and semantic controls; no formal compliance certification is claimed.

`ID_CARD_OPERATIONS.md` documents capacity, the job/PDF bounds, private file/keyring backup, same-version restore, quiescing writers and verification before traffic switching. Off-host deployment topology, key transfer, recovery objectives and production rollout remain environment-specific operational tasks. Existing encryption purposes/application name remain unchanged.
