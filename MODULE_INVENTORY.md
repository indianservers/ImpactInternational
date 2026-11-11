# Impact International module inventory

The foundational modules and evidence are indexed in EIGHT_PHASE_ROADMAP.md and the existing phase completion reports.

Advanced ID Card Studio extends DigitalCards, CertificateTemplates, Persons, Memberships, governance requests, document versions and private communication storage. Additional tables hold immutable template versions, durable jobs/items, print presets and tenant policy. Entry points: `/app/id-cards`, `/app/my-id`, `/verify/id/:token`. Source: `server/IdCard*.cs`, `client/src/cards`. Tests: `tests/id_cards*.py`, `tests/id_card_design.mjs`. Permissions: members.cards, cards.design, cards.publish, cards.issue, cards.bulk, cards.settings.
