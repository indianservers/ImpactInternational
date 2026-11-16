# ICI role workspace

Implemented from **ICI Portal.pdf** in the user's shared [NotebookLM notebook](https://notebook.google.com/notebook/e9b1eee2-1519-46ef-8038-cc45879bbaf0), inspected on 16 November 2026.

Open **Leadership → Role workspace**. Administrators configure verified existing association accounts under **Team & coverage**. National President and Executive Board submit leadership changes under **Requests → Leadership team handover**; a different Executive Board member reviews the handover. No passwords are disclosed or changed.

## Scope and roles

The hierarchy remains International → National → Region → Club. An NVP's “area” is a set of assigned Regions. NVP 1–4 are assignment slots, and RP 1–9 are holders of Regional President offices in their respective Regions, rather than additional role types or hierarchy levels.

- Executive Board: title clearance, final transfers, club creation/mapping, Region-to-NVP mapping, regional event bid/report decisions, leadership handovers including National President, and confidential internal legal case records.
- Office Bearers: own profile/career and assigned-region member/activity reports.
- National President: title clearance, final transfers, club creation/mapping, Region-to-NVP mapping, club event bids, and reviewed leadership handovers excluding their own office.
- National Secretary: listed in the PDF without detailed duties. Own profile/career and assigned-region reports are provided; no extra approval authority is inferred.
- National Treasurer: financial-aid requests and their linked activities, without general finance management or approval authority.
- National Vice President: assigned-region member/activity reports and inward/outward transfer reviews, with four unique slots per national unit.
- National Director: assigned-portfolio activity reports and event review stages.
- National Coordinator: assigned-initiative reports for Mini & Mega Impact, EPS, Young Minds, Skill Proficiency, or Social Media.
- Regional President: regional member/activity reports, inward/outward transfers, own regional activities, event bids/reports, and club event/report decisions.
- Regional Director: regional activity reports restricted to Business, Executive Leadership, Service, Training, or Nari Cell.
- Club President: club member management/reports, transfers, subscriptions, title clearance, and club events/bids/reports.

National Director portfolios also include the source's literal **National pro** designation. Executive Leadership maps to the existing **Leadership** activity category; initiatives are stored separately from the five activity types.

## Decisions and financial records

- Club event publication: Regional President → matching National Director.
- Regional event publication: matching National Director → Executive Board.
- Transfers: source Regional President → destination Regional President → source NVP → destination NVP → National President or Executive Board.
- Club event report: Regional President. Regional event report: Executive Board.
- Club event bid: National President. Regional event bid: Executive Board.
- Title clearance: National President or Executive Board, after administrative payment verification.
- Subscription: administration verifies a payment record before independent approval; the requester cannot approve their own request.

Subscription amounts are calculated server-side at INR365 per active selected member, with at least 20 members on initial registration. A pending or approved subscription reserves each person for that club and financial year, including people holding multiple membership categories. Title clearance is INR100. These are manual receipt/reference recording and verification workflows; they do not initiate bank charges, connect a new payment gateway, or disburse financial aid.

Approved club creation, club mapping, Region-to-NVP mapping, and handovers apply actual database changes inside the reviewed transaction. Legal cases provide internal confidential tracking; the app does not execute external legal actions.

## Privacy and enforcement

Region and portfolio limits apply to APIs, member hierarchy pickers, ordinary event lists, direct event URLs, reports, and approval decisions. Missing coverage grants no national fallback. ICI roles use fixed permission policies; generic assignments and access-bearing officer appointments cannot bypass the role workspace's metadata and handover rules. Only Main Admin/Association Super Admin and Platform Admin retain the Communication Center.

Club and Regional Presidents can replace their own encrypted bank details. Responses expose only the account's final four characters; reports never return full account numbers. Training/leadership career notes are editable only for the signed-in account. Legal narratives use a separate data-protection purpose and are visible only to Executive Board and authorized administrators. Preserve the app's ASP.NET Data Protection key ring when moving deployments so encrypted records remain readable.

## Deployment and verification

Apply EF migrations `IciRoleWorkflows` and `IciTransferReviewStages`; startup installs the fixed role definitions, grants, and corresponding office positions. Startup does not invent role holders, portfolios, or regional coverage. Administrators must configure actual holders.

`python tests/ici_roles_checks.py` rebuilds only the named disposable `clubs_ici_roles_qa_20261116` database from the existing four-level QA template. It runs 91 acceptance checks covering all role contexts, scope tampering, portfolios, ordered decisions, fee verification, duplicate subscriptions/transfers, encrypted records, and real access handovers. It uses fictional credentials and suppresses external delivery in the QA database. Backend and frontend production builds pass. Browser checks covered the Club President workspace, subscription selector/totals, masked profile, role catalog, National Director portfolio report, and administrator assignment/coverage controls.

### Full role verification, 16 November 2026

Signed in through the browser as all eleven roles, selected their corresponding active workspace, and inspected dashboards, activity reports, requests, own profiles, authorized member lists, and the available review tabs. Office Bearers' region selector exposed only the assigned Region. National Director and Coordinator reports displayed their configured portfolio/initiative. A fictional club event completed Regional President then National Director review in the UI. A fictional member transfer completed source RP, destination RP, source NVP, destination NVP, and National President review in the UI.

The follow-up `tests/ici_role_access_regressions.py` runs against the same disposable QA server after the baseline suite creates its fixtures. Its 173 additional checks cover the full role permission matrix, all 16 director/coordinator portfolio configurations, forged dashboard/report filters, event publication/rejection/change requests, stale versions, and review eligibility for actors holding two offices. Combined with the baseline this gives 264 passing API acceptance checks.

Review controls now use server-calculated eligibility. Transfer screens expose the current required reviewer and hide decisions from later-stage or out-of-coverage officials. Event workspaces and full governance proposals hide duplicate-person approvals across two offices. API enforcement remains in place, and cancellation by someone other than the requester also requires the source official's current review authority. Administrator transfer decisions must cover the relevant clubs, including both clubs for final approval.

The test build can be selected using `CLUBS_TEST_ASSEMBLY` without replacing the assembly used by the running app. No live account passwords, memberships, or club mappings were changed by these QA scenarios. Browser snapshots and screenshots are generated under the ignored `test-results` directory.
