# ID Card Studio operations

## PDF acceptance target

The requested acceptance target is PDF review; physical printer/stock/ICC certification is outside this delivery. Open `output/pdf/Impact-International-ID-Cards-Review.pdf` at actual size (100%, no fit-to-page). The fictional member fronts are on page 1 and their mirrored, calibrated backs on page 2. Automated checks independently measure MediaBoxes and text offsets and decode the QR at its physical placement. A3 portrait/landscape, custom sheets, bleed and short-edge duplex have separate automated PDF checks.

## Bounded processing and capacity

ZIP/image jobs support 10,000 memberships. Combined PDF jobs support 1,000 cards because native PDF generation buffers the document. Split larger print selections into disjoint jobs; never reissue merely to print. Choose existing-card export and the matching historical published version. Job approval is still required. PDF output is RGB; no CMYK/ICC conversion or external background-removal/barcode provider is advertised.

The PostgreSQL advisory lock deliberately serializes the durable worker across processes. Adding replicas supplies availability, not additional card throughput. Keep this invariant unless job claiming and packaging are redesigned and tested. Peak measured API RSS was 343.2 MiB for a 10k ZIP run. A 10k archive was 3.94 GB, in addition to original private PDFs/PNGs and database rows. Measure the actual design/photo mix and reserve room for originals, retained replacements, active jobs, temporary output and backups. Monitor free bytes, worker errors, pending age, failures and expired output cleanup. Seven-day job expiry deletes the generated archive, not historical official card files.

## Backup and restore

Back up PostgreSQL with pg_dump (custom format), the private object storage root configured by `Communications:StoragePath`, and the ASP.NET data-protection keyring configured by `DataProtection:Path`. The default development object root is `.infra/communication-objects`; production should use explicit durable private paths. Keep the application name `ClubSphere` and cryptographic purposes unchanged. Protect backup keys and storage access with the same controls as the originals. A database-only backup cannot restore card files.

Quiesce writers/workers for a consistent database/object backup, or use coordinated snapshots and retain all referenced object versions. Restore into a separate database/storage/keyring first, start the same application version with those paths, run migrations when upgrading, then authenticate an owner and verify issued PDF bytes, QR status, tenant isolation and pending-job behavior before switching traffic. Do not restore over a live database while its workers run.

`tests/id_cards_recovery.py` performs a local isolated logical database restore, copies all referenced card files and frozen image assets, verifies an authenticated owner's PDF is byte-identical and checks the public QR. This is a local same-host/keyring exercise, not a claim that off-host infrastructure or production failover has been certified. Production topology, retention, recovery objectives and off-host key restoration remain deployment decisions.

## Verification commands

Run server/client builds; `tests/id_cards.py`, `tests/id_cards_extended.py`, `tests/id_cards_edges.py`, `tests/id_cards_qr_security.py`, `tests/id_cards_pdf_acceptance.py`, and `node tests/id_card_design.mjs`. Tests create fictional records in the configured development database. The Phase 1–8 regression runner uses a separate fixture clone. Recovery and scale scripts require local PostgreSQL tools and isolated test databases. Provider tests accept `CLUBS_TEST_DATABASE` and must use an isolated fixture to avoid shared-tenant quotas. `tests/id_cards_latency.py` records real keyset and QR timings on the existing scale fixture; `tests/id_cards_print_scale.py` accepts `CLUBS_PRINT_SOURCE_JOB` or uses the measured 1k job from the scale evidence.
