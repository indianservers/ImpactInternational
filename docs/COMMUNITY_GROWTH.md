# Community discovery, opportunities and recognition

All ten enhancements are available from the activity feed's **Community tools** navigation. Apply the `CommunityGrowthAndRecognition` migration after the preceding Google media migration, deploy the updated API/client, and retain the existing Google OAuth configuration. No existing activity is made public or removed by this migration.

## Event-to-activity posting

In **Activity feed → Post an update**, select **Start from a completed event**. Eligible events must be approved, published, ended and within the publisher's own unit (Platform Admin can select across their tenant). The form copies title, description, venue, date, coordinates, attendance and active-member attendees with a non-voided event check-in. Publishing imports available event Gallery/EventBanner images into the activity's Google folder. Repeating the import skips already imported files. An event can have only one source activity, including if that activity is later archived. Existing event documents remain under their existing retention policy; no local source records are deleted by importing.

## Public-post approvals

**Review queue** shows public submissions from the reviewer's immediate subordinate units. Club submissions go to their region, regional submissions to national leadership, and national submissions to headquarters. An author cannot approve their own submission. Platform Admin can approve across the tenant and can publish directly.

The default policy requires approval for public posts. Platform Admin can configure this in **Review queue → Publication policy**. Internal posts publish immediately. Editing a public post submits it again when approval is required; it immediately disappears from anonymous/public-media access until approved. Rejections require a reason, remain visible to authorized publishing leadership, and can be edited and resubmitted. Policy changes do not silently publish pending records. Approvals reject stale versions and are audited.

## Participation tags and member history

The activity editor tags active members from the unit or its descendant clubs. Members accept or decline tags in **My contributions**. Acceptance is distinct from attendance verification. Public name sharing is a separate opt-in checkbox; unaccepted tags never appear in shared participation lists. Accepted tags appear in the member's complete profile only for viewers already entitled to see that activity and member.

Publishers open **Verify participants & certificates** below a feed post, record attendance evidence, role and individual hours, and verify attendance. Event check-ins can supply the initial verified attendance automatically. Future activities cannot be verified. Declining a tag or withdrawing attendance verification removes its recognition eligibility and revokes any issued certificate. Activity totals are not automatically assigned as individual volunteer hours.

## Certificates and milestones

Certificates require all three conditions: the activity is published, the member has accepted participation, and attendance is verified. The president issues a PDF from the post's participation tools; the member downloads it under **My contributions**. Each certificate has a random public verification URL and QR code. Recipient names are withheld on public verification unless the member opted in. Private activity titles remain private on public verification. Certificate issuance is idempotent; revocation requires a reason. Previously issued records are retained for audit, and a reissued certificate rotates its verification token.

Contribution milestones are derived from current accepted, verified participation in published activities: first contribution, five contributions and ten contributions. Recognition updates when consent, attendance or publication eligibility changes. No arbitrary point totals or unverified tags are treated as attendance.

## Business opportunities and member suggestions

Active members can post requirements with a category, specialty, tags, city and closing date in **Business opportunities**. Search, **Matches my business**, **My posts**, editing, closing and removal are supported. An opportunity accepts one private response per member; only its owner can read responses. Posting is rate-limited. Closed, expired or removed requirements do not accept new responses.

**People for you** and opportunity match panels use active, opted-in professional profiles. Categories, specialties, overlapping business tags, shared skills/interests and city produce explicit match explanations. Hidden profiles and other tenants are excluded. A suggestion opens the existing networking profile and introduction flow without exposing private contact details.

## Activity discovery map

**Explore & map** filters by date range, category, unit, city/location and followed spaces. The world outline is a bundled local GeoJSON asset. Marker data is authorized on the server before it reaches the browser. Exact activity coordinates are used when entered; otherwise known organizational coordinates are labeled approximate. Unlocated activities remain in the list. Map markers open authorized activity details, with zoom controls and keyboard access. Map results are bounded to 500 geolocated activities and list results are paginated.

## Following and weekly digests

Members follow permitted organizations or topics in **Following & digest**. Following never grants access to another club's private content. The preview shows currently permitted followed posts from the last week. Weekly email reminders are explicitly opt-in, scheduled durably in PostgreSQL and respect normal email delivery preferences and suppressions. Opting out suppresses unsent digest messages.

Email contains a generic reminder and links to the live authorized digest, rather than freezing private post titles/media in email. The background worker validates the saved assignment's current authority and serializes queue ownership; losing authority disables that assignment's digest. SMTP must be configured for live delivery. No real email is sent by the QA stack.

## Leadership impact

Presidents and authorized reporting roles open **Leadership impact** for 30-, 90- or 365-day reporting in their authorized organizational scope. It shows published updates, active clubs, clubs without reports, recorded attendance, aggregate volunteer hours, unique verified members, categories and unit drill-down links. Attendance totals can repeat a person across activities; unique verified members are separately deduplicated. An inactive club is excluded from the silent-club count.

## Storage health and recovery

Only Platform Admin can open **Storage health** to see connected/disconnected accounts, tracked attachment usage, failed/stalled uploads and affected activities. Usage is this app's metadata, not an assertion about the Google account's entire quota. Retry asks for the original local file with the same MIME type/size, creates a fresh scoped Drive session, and verifies completion before publishing the attachment. Browser upload failures are recorded without retaining Google session URLs or OAuth tokens in health responses. Reconnect the original Google account before retrying its attachments.

## Validation and deployment

`tests/community_growth_checks.py` covers approval isolation and stale decisions, consent/attendance/certificate dependencies, matching privacy, opportunity response privacy, follows, map visibility, impact scope, event attendance/gallery import, upload recovery and opt-in digest scheduling. `tests/community_growth_browser_checks.py` covers the member, Club President, Regional President, National President and Platform Admin interfaces and mobile layouts. They use only the disposable `clubs_community_growth_qa_20261118` database, API5112, browser1252 and loopback Google provider5113 through `tests/community_growth_runner.py`. The active `clubs_impact` database is untouched.

Google operations are tested against a local protocol double. Live Google OAuth/upload/playback still requires the deployment's real client credentials and account consent, as documented in [Google media setup](GOOGLE_ACTIVITY_MEDIA.md). This feature does not change the Google Photos API limitations. Apply migrations and preserve the Data Protection key ring before enabling the updated application.
