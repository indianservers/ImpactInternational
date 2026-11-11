# Impact International module inventory

The foundational modules and evidence are indexed in EIGHT_PHASE_ROADMAP.md and the existing phase completion reports.

Advanced ID Card Studio extends DigitalCards, CertificateTemplates, Persons, Memberships, governance requests, document versions and private communication storage. Additional tables hold immutable template versions, durable jobs/items, print presets and tenant policy. Entry points: `/app/id-cards`, `/app/my-id`, `/verify/id/:token`. Source: `server/IdCard*.cs`, `client/src/cards`. Tests: `tests/id_cards*.py`, `tests/id_card_design.mjs`. Permissions: members.cards, cards.design, cards.publish, cards.issue, cards.bulk, cards.settings.

Acceptance follow-up: advanced bulk filters, historical export versions and protected inherited artwork preview are exposed in the UI. Mobile/keyboard, PDF geometry/QR matching, expiry/revocation and local recovery checks are documented in ID_CARD_MODULE_COMPLETION_REPORT.md; operations and backup guidance is in ID_CARD_OPERATIONS.md.
