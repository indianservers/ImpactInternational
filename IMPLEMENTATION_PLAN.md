# Phase 1 plan

The repository was empty. .NET 10 and Node 24 are installed; no PostgreSQL service, Docker, or database environment variables were found.

Build one ASP.NET Core API using EF Core/Npgsql and a React/Vite client. PostgreSQL adjacency lists plus recursive queries provide configurable hierarchy; composite foreign keys enforce tenant boundaries. Serialize hierarchy mutations per tenant to prevent concurrent cycles. Server-side opaque sessions and atomic scoped permissions control every business endpoint. Use an outbox for verification/recovery mail, with a development-only delivery viewer. Implement bounded lists and lazy routes, then verify against actual PostgreSQL and record measured results. Do not represent later modules as functional.
