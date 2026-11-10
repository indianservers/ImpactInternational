# Phase 2 implementation plan

The Phase 1 React/ASP.NET/EF Core monolith, eight migrations, session/CSRF flow, scoped RBAC, hierarchy triggers, outbox and UI shell are retained. Its frontend/backend builds and 60 PostgreSQL integration checks passed before changes. Existing uncommitted Phase 1 work is preserved.

1. Add a global person identity, tenant-scoped memberships/categories/history, privacy and professional details. Account linking is explicit and reviewed; contacts are not globally unique.
2. Add versioned approval workflows, drafts, token invitations, lifecycle/renewal and effective-dated transfer transactions. Reuse existing roles and add atomic membership permissions.
3. Extend the existing mail outbox/SMTP transport for transactional notifications and delivery attempts; add secure database-backed documents and opaque QR cards.
4. Add a reusable configurable grid, CRM/profile/directory, application/review workspace, import/export, and member portal in the existing shell, including light/dark themes.
5. Run additive migrations against preserved local data, seed fictional demonstration memberships separately, retain Phase 1 regressions, add Phase 2 authorization/concurrency/privacy tests and a separate 100,000-person benchmark database. Record actual results and limitations.

Sites guidance was reviewed; the existing ASP.NET/PostgreSQL server cannot run in Cloudflare Workers, so implementation remains in this checkout with its existing container deployment boundary. No second application or email infrastructure will be introduced.
