# Certified people and the learning portal

Platform Admin → Settings & access → Certified people (`/app/certified-people`) manages both independent learners and club members with certifications. The Platform Admin dashboard also links to this register.

- `/signup?portal=1` offers account registration without National, Region or Club selection. Email verification and password login use the existing authentication flow. Existing accounts should sign in rather than register again.
- Signed-in users open **My certifications** (`/app/my-certifications`) and join the learning portal. This is available without a role/workspace. Joining creates no club membership, officer appointment or organization access.
- A `PortalParticipant` associates an existing global Person with the Impact tenant. Club membership and certifications remain separate; a club member completing Train the Trainer keeps the same Person ID.
- Admins can add alumni by email before they create an account. When that email is verified and the person joins the portal, the existing profile is claimed. Ambiguous email matches require duplicate-profile resolution rather than automatic merging.
- The register includes enrolled participants plus anyone with existing certification records. Filter by program and active club/independent status, or search names and emails. Results paginate at 30 people.
- Use the existing program manager for Train the Trainer and other courses; maintain certificate number, workshop batch, issuer, training center, dates, verification URL and notes on each person.
- Certification entries are self-reported until Platform Admin verifies the current version. Editing invalidates verification. Certificate expiry is shown separately from verification.
- Ordinary participants can manage only their own certifications. Existing member management permissions continue to apply inside authorized club scopes; only Platform Admin can view the independent participant register or verify certificates.

## Database and checks

Apply migration `PortalParticipantsAndCertificationVerification` after the preceding migrations before deploying this version. It adds the participant relation and verification metadata; existing Person, Membership and MemberCertification rows are preserved. The active application database was not modified during QA.

QA database: `clubs_certified_people_qa_20261118`, cloned from the previous isolated community-growth database. Run `python tests/certified_people_runner.py` after a Release build to migrate and start API5115/web1255. With `CLUBS_TEST_DATABASE=clubs_certified_people_qa_20261118` and `CLUBS_TEST_URL=http://127.0.0.1:5115`, run `tests/certified_people_checks.py`, then `tests/certified_people_browser_checks.py`. Evidence is under `docs/qa/certified-people`.
