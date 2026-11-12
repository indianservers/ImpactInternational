# Committee request failure and page sweep

The Committee directory now loads, filters, and opens existing committee records on the running app at http://127.0.0.1:2233. Its Documents tab was also checked.

## Cause and fix

The committee listing returned HTTP 500. PostgreSQL reported SQLSTATE 42883 (`like_escape(uuid, unknown)`): EF/Npgsql reused generated parameter names between the recursive organization-scope subquery and composed search predicates. Even the empty search sent by the page reproduced it.

`Access.VisibleIds` now uses the same canonical typed GUID SQL literals already used by `Access.Tree`, retaining the narrow recursive projection and tenant/branch restrictions. No free-form request text is interpolated into SQL. Search remains parameterized. This also protects other page queries that compose the shared scope with filters. No schema or permission changes were required.

## Verification

- Backend build: passed, zero warnings/errors. Frontend build and navigation checks: passed.
- Read-only API sweep through port 2233: 239/239 passed. Includes main page queries, accessible detail records, 39 finance queries, empty/nonempty/apostrophe/wildcard searches, certificate sources, restricted-role committee authorization and out-of-scope filters.
- Browser sweep: 82 main/nested views, no visible request errors after primary loading states cleared. Includes Membership, Organizations, Leadership, Governance, Events, Operations, Finance, ID Cards, Intelligence, Communications, account and settings views.
- Committee directory, search, real record details and Documents tab: verified in browser.
- Updated backend runs on the existing API ports 5080/5081; the app remains on 2233. The temporary comparison API on 5090 was stopped.

Evidence: `page-errors-smoke.json`, `page-errors-browser.json`, `committee-fixed.jpg`.

## Re-run

In PowerShell from the repository root:

```powershell
$env:CLUBS_TEST_URL='http://127.0.0.1:2233'
$env:CLUBS_OPENAPI_URL='http://127.0.0.1:5080/openapi/v1.json'
python tests/page_errors_smoke.py
```

The sweep uses existing local demo accounts/data. It creates login sessions and selects scopes, but does not submit business mutations. Binary downloads, long-running streams, private token-only links, external provider callbacks and destructive/financial actions were excluded. Record screens are checked where an accessible fixture exists; finance checks cover the first two explicitly authorized books. Passing this sweep does not establish correctness of every business workflow or third-party integration.
