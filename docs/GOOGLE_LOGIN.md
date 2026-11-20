# Google sign-in

The login page uses Firebase Authentication's Google popup. The server verifies the Firebase ID token's RSA signature, project, issuer, expiry, verified email, Google provider and recent authentication. It also checks Firebase's current account status and token revocation time. Only an existing, verified and unlocked Impact account with the same email can sign in. Google sign-in does not create users, memberships or role assignments. New users follow the existing signup and approval process.

Firebase project: `impact-international-22932`. Enable Google in Firebase Authentication → Sign-in method, and add `members.impactinternational.in` to Authentication → Settings → Authorized domains. `localhost` is supported for local development. The public web configuration is in `client/src/firebase.ts`; server project and API key settings are in `server/appsettings.json` and can be overridden with `Firebase__ProjectId` and `Firebase__ApiKey`. No service account private key is required. Analytics is not initialized by the sign-in integration.

`POST /api/v1/auth/google` accepts `{ "idToken": "..." }`, requires the normal CSRF token, and uses the existing authentication rate limit and eight-hour HttpOnly session cookie. Firebase credentials live only in memory and are cleared after the exchange. The server retains responsibility for workspace effectiveness, membership restrictions, and authorization. Password and email OTP behavior is preserved.

The reverse proxy must permit the Firebase auth iframe and Google popup scripts in its Content Security Policy. See `client/nginx.conf`. Use `Cross-Origin-Opener-Policy: same-origin-allow-popups`.

Run token verification regression checks with `dotnet run --project tests/GoogleAuthChecks`. Production verification also checks the login button and Google popup, rejection of forged tokens, and the existing password login. A successful real Google account login must be checked interactively by an authorized account holder.
