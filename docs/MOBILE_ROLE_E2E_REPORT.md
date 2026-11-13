# Mobile role and workflow verification

Verified on 12 November 2026. The app remains available on port 2233 with the updated backend.

## Coverage and results

- All 12 seeded roles: login, organization selection, home, mobile navigation and sign-out at 390 × 844. No visible request errors or horizontal overflow in those checks.
- Additional 320px checks: guest home, platform administrator registrations, and the two complete workflows below. Browser emulation was used, not physical devices.
- Applicant journey at 320px: accept invitation → save application → submit → independent secretary review → president approval → applicant sees active membership. Decisions and membership persisted.
- Manager journey at 320px: create task → assign Ravi Kumar → record completion evidence → complete → reload and verify completed status persists.
- 639/639 backend workflow and security checks across membership, governance, events, finance, communications, operations and ID cards.
- 188/188 role permission checks, including organization scope, restricted events and accounts without person profiles.
- After installing the final backend on the main app: 239/239 read-only screen-data checks passed, including committee searches.
- Frontend production build, backend build and navigation checks passed. All 36 feature destinations and 35 nested module tools remain reachable.

## Fixes

1. Restricted events could appear to unrelated accounts without person profiles because null person IDs matched guest records. Event staff, invitations, registrations, audiences and personal-event filtering now require a real person identity before matching those records. Legitimate private-event lifecycle and security tests still pass.
2. Member and guest homes could become blank after shell hot reload because lazy pages retained a different context instance. The shared context now lives in a stable module; reloading the shell preserves the member portal.
3. Removed the mobile sidebar slide transition that interfered with quick menu actions during testing.
4. Added an accessible label to the application statement field.
5. Completed application reviews now show a completed-state message; draft applications explain that submission is needed before review.

No feature destinations or workflow stages were removed.

## Test isolation and repeatability

Mutation suites and browser journeys used a disposable snapshot database, `clubs_mobile_e2e_20261112`, with copied local communication objects and fictional test records. SMTP was pointed at an unused local port; real delivery was not attempted. Original workflow fixture files were restored after testing. The temporary API and frontend were stopped; the main app remains on 2233.

`tests/mobile_roles_setup.py` and `tests/mobile_role_permissions.py` reject database names outside the dedicated mobile-test prefix. Set `CLUBS_TEST_DATABASE` to the disposable database and `CLUBS_TEST_URL` to its API before running them. Seed credentials remain in the ignored local seed-password file.

The older phase2 address fixture now uses a valid Indian PIN. Shared test-client rate-limit retries are opt-in through `CLUBS_TEST_RETRY_RATE_LIMIT=1`; application limits and default test behavior remain unchanged. Historical pending QA domain requests were cleared only in the disposable database before testing the existing domain quota workflow.

Evidence: `mobile-e2e-workflows.json`, `mobile-role-permissions.json`, `mobile-ui-roles.json`, `page-errors-smoke.json`, and `mobile-role-e2e.jpg` in this directory.

## Limits

Real email/SMS/WhatsApp delivery, external payment execution, and physical-device behavior were not exercised. Browser end-to-end journeys cover membership approval and task completion; broader module workflows were verified through the backend suites rather than every possible browser interaction.
