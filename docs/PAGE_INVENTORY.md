# App screen inventory

The app has six main areas. Existing route URLs remain available as tabs, tool views, record details or public entry points; they are not additional items in the main navigation.

## Main areas and merged tools

- Home: overview and foundation summary.
- People: members (list, directory, import, insights), applications/transfers, and personal membership (membership, ID, join, profile/security).
- Organization: structure/clubs, leadership/calendar/handovers/committees, and decisions/resolutions/documents.
- Activities: events/invitations/reports, meetings/projects/tasks/volunteers/impact/records, and messages/outreach.
- Finance & tools: financial books/billing/reports, ID card design/issuance/printing, and intelligence/knowledge/workflows.
- Settings & access: roles, audit, organization settings and membership/governance/event configuration.

## Main app routes

- `/app` — role-aware home.
- `/app/members`
- `/app/directory`
- `/app/members/:id`
- `/app/my-membership`
- `/app/apply`
- `/app/approvals`
- `/app/imports`
- `/app/membership-insights`
- `/app/membership-settings`
- `/app/transfers`
- `/app/organizations`
- `/app/intelligence/*`
- `/app/id-cards/*`
- `/app/my-id`
- `/app/finance/*`
- `/app/operations/*`
- `/app/communications/*`
- `/app/events`
- `/app/events/new`
- `/app/events/:id`
- `/app/events/:id/checkin`
- `/app/my-events`
- `/app/event-settings`
- `/app/event-intelligence`
- `/app/clubs`
- `/app/clubs/:id`
- `/app/club-administration`
- `/app/leadership`
- `/app/leadership-calendar`
- `/app/handovers`
- `/app/committees`
- `/app/committees/:id`
- `/app/governance`
- `/app/governance-settings`
- `/app/governance-documents`
- `/app/resolutions`
- `/app/foundation-overview`
- `/app/administration`
- `/app/settings`
- `/app/audit`
- `/app/profile`

## Finance tool and detail routes

- `/app/finance` — overview.
- `/app/finance/member`
- `/app/finance/invoices/:id`
- `/app/finance/*`
- `/app/finance/invoices`
- `/app/finance/dues`
- `/app/finance/payments`
- `/app/finance/reconciliation`
- `/app/finance/accounts`
- `/app/finance/budgets`
- `/app/finance/expenses`
- `/app/finance/funding`
- `/app/finance/sources`
- `/app/finance/allocations`
- `/app/finance/reports`
- `/app/finance/settings`

## Operations tool and detail routes

- `/app/operations` — overview.
- `/app/operations/meetings`
- `/app/operations/meetings/:id`
- `/app/operations/tasks`
- `/app/operations/projects`
- `/app/operations/projects/:id`
- `/app/operations/volunteers`
- `/app/operations/impact`
- `/app/operations/documents`
- `/app/operations/certificates`

## Intelligence tool and detail routes

- `/app/intelligence` — overview.
- `/app/intelligence/assistant`
- `/app/intelligence/knowledge`
- `/app/intelligence/workflows`
- `/app/intelligence/metrics`
- `/app/intelligence/branding`
- `/app/intelligence/operations`

## ID Card Studio tool and detail routes

- `/app/id-cards` — overview.
- `/app/id-cards/templates`
- `/app/id-cards/designer/:id`
- `/app/id-cards/issuances`
- `/app/id-cards/bulk`
- `/app/id-cards/print`
- `/app/id-cards/settings`

## Communications views

These are grouped into Messages, Outreach and Manage; the selected view uses the same communication screen.
- `/app/communications/Inbox` — All Inbox
- `/app/communications/Mine` — My conversations
- `/app/communications/Team` — Team inbox
- `/app/communications/WhatsApp` — WhatsApp
- `/app/communications/Email` — Email
- `/app/communications/SMS` — SMS
- `/app/communications/Announcements` — Announcements
- `/app/communications/Campaigns` — Campaigns
- `/app/communications/Schedules` — Scheduled messages
- `/app/communications/Templates` — Templates
- `/app/communications/Audiences` — Audiences
- `/app/communications/Automations` — Automation rules
- `/app/communications/Analytics` — Intelligence
- `/app/communications/Delivery` — Delivery tracking
- `/app/communications/Settings` — Settings

## Public and account entry routes

- `/verify/id/:token`
- `/verify/certificates/:token`
- `/event/:id`
- `/event-invitation`
- `/`
- `/join`
- `/verify`
- `/request-organization`
- `/login`
- `/signup`
- `/forgot-password`
- `/resend-verification`
- `/complete`

## Phone behavior

- Five-button bottom navigation: the first four available areas and More.
- More opens the organization selector and all six areas; the menu supports Escape and keyboard focus containment.
- Primary task tabs show only the selected task’s tools; a labeled native picker replaces tool rows on phones.
- Finance, Operations, ID Cards, Intelligence and Communications use grouped task navigation instead of long rows of links.
- Member and shared operational grids become record cards, with additional fields and actions in expandable details.
- Forms use one column; full-screen drawers, larger controls, safe-area spacing and local table scrolling support narrow screens.

## Validation scope

Build and navigation/route checks cover compilation and destination preservation. Phone browser checks are recorded in the delivery summary; this inventory does not imply every workflow has been manually tested.
