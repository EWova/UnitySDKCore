# EWova.Auth: full flow and method reference

Expands on details `../SKILL.md` doesn't cover. Source lives under
`Packages/Networking/Auth/` (asmdef name is `EWova.Networking`, but the namespace is
`EWova.Auth`).

## AuthState state machine

`EWova.Auth.AuthState` (`Models/AuthState.cs`):

```
Initializing(0) → Unauthenticated(1) ⇄ Authenticating(2) → Authenticated(3) ⇄ RefreshingToken(4)
                        ↑___________________________________________|  (refresh failure / logout)
```

- `Initializing`: object was just constructed, no state set yet (immediately transitions to
  `Unauthenticated` if the constructor leaves it here).
- `Authenticating`: a `code`→token or `launch_ticket`→token exchange is in progress.
- `Authenticated`: holds a valid access token; also starts the background renewal loop
  (checks every 15 seconds).
- `RefreshingToken`: `IsAuthenticated` is still `true` (`AuthProvider.IsAuthenticated` is defined as
  `Authenticated || RefreshingToken`), but a refresh_token exchange is in flight.

## Full authorization-code sequence (`AuthorizeViaBrowser`)

1. Caller calls `IAuthManager.AuthorizeViaBrowser(options, onCompleted)`.
2. `AuthProvider` creates an internal `AuthorizeProcess`: generates `code_verifier`
   (`PkceHelper.GenerateCodeVerifier`, 32 bytes → Base64Url), `code_challenge` (SHA256 + Base64Url,
   S256 method), `state`, and `nonce` (each a 16-byte random value). This process has a 10-minute
   timeout (`IsExpired`).
3. `AuthRequestBuilder.BuildAuthorizeUrl` assembles:
   `{Issuer}/oidc/authorization?response_type=code&client_id=...&redirect_uri={scheme}://callback&scope=...&state=...&nonce=...&code_challenge=...&code_challenge_method=S256&prompt=...&ui_locales=...`
   where `prompt` is composed from `AuthorizeViaBrowserOptions.ConsentRequired` (adds `consent`)
   and `LoginBehavior` (`ForceLogin→login` / `Silent→none` / `SelectAccount→select_account` /
   `Standard→nothing added`).
4. `Application.OpenURL(authorizeUrl)` opens the system browser.
5. After the user logs in/consents, the auth server redirects to `RedirectUri` (i.e.
   `{MyAppScheme}://callback?code=...&state=...`), triggered by the platform's `IDeepLinkProvider`
   firing `DeepLinkHandler.Default`'s event → `AuthProvider.OnDeepLinkHandlerActivated` →
   `InternalHandleAuthenticationUrl`.
6. `HandleDeepLink` checks:
   - Whether the scheme matches `RedirectUri`'s scheme (ignored if not — might be a deep link for
     something else).
   - Whether an `error` query parameter is present → treated as an authorization failure.
   - If both `code` and `state` are present → validates `state` against the current
     `AuthorizeProcess.State` using a constant-time comparison
     (`CryptographicOperations.FixedTimeEquals`, CSRF protection), and that the process hasn't
     expired or already completed.
7. Calls `TokenService.ExchangeCodeAsync(code, codeVerifier, nonce, ct)`:
   `POST {Issuer}/oidc/token`, `grant_type=authorization_code`,
   `body = AuthRequestBuilder.BuildExchangeCodeBody(...)` (`application/x-www-form-urlencoded`).
   The resulting `TokenResponse` is converted to a `TokenSet`, and the `id_token` payload's
   `nonce` (compared against the value from step 2), `aud` (compared against `ClientId`), `iss`
   (compared against `Issuer`), and `exp` (not expired) are all validated — any mismatch throws
   `TokenEndpointException`.
8. On success, `CurrentTokens` is set (which also parses out `CurrentUser`), the state transitions
   to `Authenticated`, and `AuthorizeProcess.Complete()` fires `onCompleted(AuthorizeResult.Ok())`.

## Launch ticket flow (cross-app login handoff with the EWova app)

For the case where a user is already logged into App A and jumps to App B (or the EWova metaverse
app) without wanting to log in again:

1. App A: `TokenService.CreateLaunchTicketAsync(accessToken, appId, ct)` →
   `POST {LaunchTicketEndpoint}` (`/oidc/launch-ticket`), with `Authorization: Bearer {accessToken}`,
   body `{ "appId": "..." }`, returning `LaunchTicketResponse { launchTicket, deepLink }`.
2. App A: builds the URL via
   `AuthRequestBuilder.BuildLaunchEWovaAppUrlWithLaunchTick(scheme, appId, launchTicket, worldId, spaceId)`
   and calls `Application.OpenURL` to launch App B.
3. App B receives a deep link carrying a `launch_ticket` query parameter →
   `HandleDeepLink` detects `launch_ticket` instead of `code`/`state` →
   `TokenService.ExchangeLaunchTicketAsync(launchTicket, ct)`:
   `POST {TokenEndpoint}`, `grant_type=urn:ewova:params:oauth:grant-type:launch-ticket`.
4. App B gets its own `TokenSet` directly, completing login with no user interaction.

`EWovaAuth.LaunchEWovaAppWithLoginAsync` bundles steps 1+2 into one method: it first calls
`RefreshAccessTokenAsync` to ensure a fresh token → `CreateLaunchTicketAsync` → builds the URL →
`OpenURL`. On failure (`ApiException` or any other exception) it only logs — it doesn't throw, so
callers who need to know success/failure must watch the logs or modify the method.

## Refresh token flow

`RefreshInternalAsync` (shared by `GetAccessTokenAsync` / `RefreshAccessTokenAsync`):

- If a refresh is already running concurrently (`_isRefreshing`), a new call waits for the existing
  one to finish and reuses its result — it won't fire a duplicate API call.
- If there's no `CurrentTokens`, no `RefreshToken`, or the `RefreshToken` is expired → immediately
  `Logout()`s and throws `RefreshTokenExpiredException`.
- `TokenService.RefreshTokenAsync` has built-in retry (up to 3 attempts, exponential backoff
  1s/2s/4s), but `RefreshTokenExpiredException` and `JwtException` (deterministic errors) are not
  retried — they throw immediately.
- If the refresh itself fails but the old access token hasn't expired yet, the state falls back to
  `Authenticated` (not misreported as `Unauthenticated`); if the old token has also expired, the
  state becomes `Unauthenticated`.

## `AuthApiClient` / `RequestTask` method overview

(`Packages/Networking/AuthApiClient*.cs`)

| Member | Description |
|---|---|
| `AuthApiClient(IAuthManager authManager, string baseUrl, Logger logger = null)` | Constructor; a trailing `/` on `baseUrl` is stripped automatically |
| `static int DefaultRequestTimeoutSeconds` | Global timeout in seconds (default 30), `static`, affects all client instances |
| `bool IsUserAuthenticated` / `AuthState AuthState` / `UserProfile AuthenticatedUserProfile` | Forwarded from the bound `IAuthManager` |
| `bool TryGetValidAccessToken(out string token)` | Forwards `IAuthManager.TryGetValidAccessToken` |
| `protected UniTask<T> Send<T>(RequestTask task, Action<RequestTask> postProcRequestTask = null)` | Main entry point for sending a request; `postProcRequestTask` can modify headers before sending (e.g. manually overriding Authorization) |
| `UniTask<Texture2D> GetTex2D(string url, bool isAbsoluteUrl, CancellationToken ct)` | Dedicated image download; returns `null` and logs on failure instead of throwing |
| `RequestTask.GET/POST/PUT/DELETE(backendUrlOrAbsoluteUrl, isAbsoluteUrl, acceptType, body, contentType, throwApiExceptionFor4xxResponses, ct)` | Builds a request; when `isAbsoluteUrl=false` the final URL is `{baseUrl}/{path}` |
| `void Dispose()` | Async disposal (the internal CTS is only actually cancelled after switching back to the main thread); query state via `IsDisposed` |
| `protected virtual void CollectPackages(List<SdkPackageInfo> list)` | Override to report your own package's version in the `X-Unity-Sdk` header |

`ApiException` (`Networking/ApiException.cs`): `ErrorCode` (`ApiErrorCode` enum:
`BadRequest/Unauthorized/Forbidden/NotFound/TooManyRequests/ServerError/DeserializationError/NetworkError/Unknown`),
`StatusCode`, `Uri`, `ResponseText`, and convenience properties `IsServerError`/`IsClientError`.

## `EWovaAuthConfig` fields (produced by `EWovaAuthConfigFactory.Create`, read-only record)

| Field | Value |
|---|---|
| `Issuer` / `BaseAuthUrl` | Production: `https://auth.ewova.com`; Development: `https://auth.ewova.dev` |
| `AuthorizationEndpoint` | `/oidc/authorization` |
| `TokenEndpoint` | `/oidc/token` |
| `LaunchTicketEndpoint` | `/oidc/launch-ticket` |
| `ExchangeTicketEndpoint` | `/token` (no actual usage found in current code paths) |
| `RedirectUri` | `{DeepLinkHandler.Default.Scheme}://callback` |
| `ClientId` / `Scopes` | Determined by the `Options` passed to `EWovaAuthConfigFactory.Create` |
