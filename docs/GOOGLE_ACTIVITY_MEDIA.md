# Activity feeds and Google Drive media

The activity feed is at **Networking → Activity feed**, with a shortcut on **Activities → Events**. Club and Regional Presidents can publish in their own units. National Presidents can read and report on all descendant region and club activities. Ordinary active members can read permitted activities, react and comment. Publishing, editing, archiving, uploads and media removal are checked by the API.

## Audiences

- **Club:** members of the originating club and authorized officers overseeing it.
- **Region:** members within the originating region and authorized leadership.
- **National:** members within the originating national unit and authorized leadership.
- **Public:** visitors can view the text and media without an account.

An activity can target its own organizational level or an ancestor; a region cannot choose an arbitrary club audience. The current organization model is International → National → Region → Club. National oversight follows that actual tree, including every descendant club. Units in another national branch do not inherit private national activities. Existing updates migrate to their previous organization-and-leadership audience; they are not made public.

Public feeds use `/activities/{tenantId}`. Individual activities have shareable `/activities/{tenantId}/{postId}` links. Private activities never appear in the anonymous API. Images and videos use the same audience checks as the activity, including after an audience change or archive. Already downloaded copies cannot be recalled.

## Why Drive rather than Google Photos

Google Photos' [developer policy](https://developers.google.com/photos/support/api-policy) prohibits using its API to host website media or non-personal enterprise media. Its [API changes](https://developers.google.com/photos/support/updates) removed shared-album methods after March 31, 2025. This integration therefore requests no Photos permissions. It uses administrator-owned **Google Drive folders per activity** for the album organization and renders the activity gallery inside Impact.

## Configure Google once for the deployment

1. In Google Cloud, enable Google Drive API and configure the OAuth consent screen for the app's intended users. Use a **Web application** OAuth client. Complete Google's consent/verification requirements as applicable; testing-mode grants may expire and require reconnection.
2. Register the exact deployment callback, for example `https://impact.example.org/api/v1/google-media/callback`. During local development use the browser-facing Vite origin, not a different API origin. The session cookie must reach the callback.
3. Configure these server-side settings using deployment secrets or environment variables:

   - `GoogleMedia__ClientId`
   - `GoogleMedia__ClientSecret`
   - `GoogleMedia__RedirectUri` (defaults to `PublicOrigin` plus `/api/v1/google-media/callback`)
   - `DataProtection__Path` pointing to a persistent private key-ring directory. Keep this key ring with database backups so saved Google tokens remain decryptable.

   Docker Compose accepts `GOOGLE_MEDIA_CLIENT_ID`, `GOOGLE_MEDIA_CLIENT_SECRET`, and `GOOGLE_MEDIA_REDIRECT_URI` from the environment. Never place a real secret in source control or frontend configuration.
4. Apply the `ActivityGoogleDriveMedia` EF migration, then run the updated API and client.
5. Platform Admin opens **Activity feed → Google media storage → Connect Google account**, signs in to Google, and approves the requested permissions. The page shows the required callback and configuration readiness. Declined consent does not add an account.

Permissions are `openid`, `email`, and `https://www.googleapis.com/auth/drive.file`. The Drive scope allows this app's files, rather than access to every file in the administrator's Drive. OAuth uses PKCE, an expiring single-use state bound to the initiating application session, encrypted access/refresh tokens, and server-side refresh. No Google access or refresh token is returned to members or stored in browser storage.

Multiple Google accounts can be connected. The default account is used for **new** activity folders; switching it does not move existing activity folders. Reconnect the original account to restore access to its older activities. Disconnect revokes the Google token and stops media display from that account; it leaves the Google files in place.

## Storage and playback

The browser uploads directly to a Google Drive resumable session, in 8 MB chunks. A session is restricted to a single preallocated file. The API verifies the completed file's size, MIME type, activity folder and app-specific identifier before adding it to the feed. Upload retry keeps the saved activity and resumes its pending file instead of creating another activity. Failed or cancelled sessions are not published as gallery items.

Supported attachments: JPG, PNG, WEBP, GIF, MP4, WEBM and MOV, up to **20 MB per image**, **500 MB per video**, and **20 attachments per activity** including pending uploads. Video playback depends on the viewer's codec support; MP4 and WEBM are preferred. Uploads consume the connected Google account's storage and Google API/transfer quotas.

Drive copies remain private; the app does not create anyone-with-link Google permissions. Playback streams from Google through the API with byte-range support and no persistent media storage or whole-file buffering. This still uses server network bandwidth. Nginx disables media buffering/temp files and permits the direct Google upload connection in CSP. Other reverse proxies should do the same. High-traffic deployments may need purpose-built object storage/CDN capacity; Google Drive is not an unlimited CDN.

The database contains activity text, audience, Google IDs and attachment metadata, not attachment bytes. Removing an attachment hides it from the feed and retains the private Drive copy for administrator recovery. Abandoned remote uploads and unwanted retained files can be cleaned up by the account owner in Google Drive. Manually deleting or sharing app folders in Google changes what is available outside the app; do not publicly share its private root folder.

## Verification

Tests use the disposable `clubs_activity_media_qa_20261117` database, API `5112`, Vite `1252`, and a loopback fake Google provider `5113`. They never call real Google APIs or change `clubs_impact`. Create a clone from an appropriate four-level QA fixture, build the API to `.infra/activity-media-build`, and apply migrations to the clone before starting `python tests/activity_media_runner.py`.

`python tests/activity_media_checks.py` verifies audience isolation, president oversight, OAuth state/session protection, least privilege scopes, encrypted credentials, account switching, direct upload completion, public/restricted streaming and byte ranges. `python tests/activity_media_browser_checks.py` verifies image and real WebM uploads, simulated interruption/resume, public visibility changes, mobile layouts and account settings. Evidence is in `docs/qa/activity-media/`.

These checks verify the protocol against a local provider double. A live Google consent, upload and playback smoke test remains necessary after configuring the deployment's own OAuth credentials. No real Google account was connected during implementation.
