---
name: ewova-core
description: Use when working in a Unity project that references the EWova.Core SDK (namespaces EWova, EWova.Auth, EWova.Networking, EWova.DeepLink, EWova.XR; classes like EWovaApp, EWovaAuth, AuthApiClient, TokenService, DeepLinkHandler) — covers install via UPM git URL, deep-link/OAuth+PKCE login, calling authenticated APIs, and EWova App deep-link integration.
---

# EWova.Core Unity SDK

This skill targets a **third-party project** — the project you're currently working in installed the
`com.ewova.core` package via a UPM git URL (the files typically live under
`Library/PackageCache/com.ewova.core@*/` or `Packages/com.ewova.core/`, depending on how it was
installed). This document is a quick reference; when you're unsure of an exact signature, prefer
Grep/Read on the actual source under that package path over guessing a method name or parameter.

## What this is

EWova.Core is the Unity foundation SDK for the EWova metaverse ecosystem. It provides:
- **DeepLink** (`EWova.DeepLink`): cross-platform (Android / iOS / Windows) custom URL scheme
  receiving and dispatch.
- **Auth** (`EWova.Auth`): OAuth2 Authorization Code + PKCE login flow, token management and
  automatic renewal.
- **Networking** (`EWova.Networking`): `AuthApiClient` base class that automatically attaches an
  `Authorization: Bearer` header and an SDK-version header to your own API client's requests.
- **EWovaApp** (`EWova`): launching to/from the EWova metaverse app (deep-link launch, carrying
  back world/space info).
- **XR** (`EWova.XR`): a TMP_InputField soft-keyboard fix for standalone XR headsets.

## Install

`package.json` lives at `Assets/EWova.Core` inside the repo (not at the repo root), so a UPM git
URL install needs the `path` query pointing at the subdirectory:

```json
// Packages/manifest.json
{
  "dependencies": {
    "com.ewova.core": "https://github.com/EWova/UnitySDKCore.git?path=Assets/EWova.Core#<branch-or-tag>",
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.unity.nuget.newtonsoft-json": "3.2.1"
  }
}
```

> **TODO (confirm with the EWova team)**: `<branch-or-tag>` is a placeholder — only `dev` and
> `master` branches were observed, no confirmed release tag (e.g. `v1.7.0`) yet. Confirm with the
> EWova team which stable version to pin before third parties install, rather than tracking `dev`
> directly.

Required dependencies (referenced by the asmdefs, but not listed under `package.json`'s UPM
`dependencies` field — add them to your manifest yourself):
- **UniTask** (`Cysharp.Threading.Tasks`) — all Auth / Networking async APIs are `UniTask`-based.
- **Newtonsoft Json for Unity** (`com.unity.nuget.newtonsoft-json`) — serializes tokens / API bodies.
- **TextMesh Pro** — only needed if you use `EWova.XR.XRInputFieldFixer` (built-in Unity package,
  usually already present).

## Initial setup: DeepLink scheme

Both the login flow and EWova-app round-tripping depend on a custom URL scheme (e.g. `myapp://`).
Setup:

1. Unity menu **EWova → DeepLink → Create Config** (`CreateConfigEditor.cs`) creates a
   `DeepLinkConfig` ScriptableObject at `Assets/Resources/DeeplinkConfig.asset`. If you never create
   one manually, the SDK creates a default one at first run (and logs a warning).
2. Set the `MyAppScheme` field in the Inspector (e.g. `myapp`). Rules: must start with a lowercase
   letter, only contain `a-z0-9.`, no consecutive dots, no trailing dot. If left empty, the editor
   derives a default from the Android package name / Company+Product name.
3. This asset is auto-registered into **Preloaded Assets** (`ConfigPreloadValidator`) — no need to
   attach it to a scene manually.
4. Each platform still needs its own native URL-scheme registration (Android
   `AndroidManifest.xml` intent-filter, iOS `Info.plist` URL Types, etc.) — these are Unity Player
   Settings / native config, not part of EWova.Core; consult platform docs separately.

`EWovaApp.DeepLinkScheme` is a fixed constant `"ewova"` — that's the scheme of the **EWova
metaverse app itself** (used by `EWovaApp.LaunchViaDeepLink` to jump into it), a separate thing
from your own project's `MyAppScheme`.

## Concept map

| Namespace | Assembly (asmdef) | Purpose |
|---|---|---|
| `EWova` | `EWova.Core` | `EWovaApp`, `Environment`, `Logger`, `UnityMainThreadDispatcher` |
| `EWova.DeepLink` | `EWova.Core` | `DeepLinkHandler`, `DeepLinkConfig`, `IDeepLinkProvider` |
| `EWova.Auth` | `EWova.Networking` | `EWovaAuth`, `AuthProvider`, `IAuthManager`, `TokenService`, `UserIdentity` |
| `EWova.Networking` | `EWova.Networking` | `AuthApiClient`, `ApiException`, `PlayerLoopHelper` |
| `EWova.XR` | `EWova.XR` | `XRInputFieldFixer` |

## Login flow (OAuth2 Authorization Code + PKCE)

Third parties **usually don't need** to implement `IAuthManager` themselves — use the built-in
singleton `EWovaAuth.Instance` (`EWova.Auth.EWovaAuth : AuthProvider`) directly:

```csharp
using EWova.Auth;
using UnityEngine;

IAuthManager auth = EWovaAuth.Instance;

if (auth.CurrentAuthState != AuthState.Authenticated)
{
    auth.AuthorizeViaBrowser(
        authorizeViaBrowserOptions: new AuthorizeViaBrowserOptions
        {
            LoginBehavior = LoginBehavior.Standard, // Standard / ForceLogin / Silent / SelectAccount
            ConsentRequired = true,
            UiLocales = new[] { "zh-TW" }
        },
        onCompleted: result =>
        {
            switch (result.Status)
            {
                case AuthorizeProcessResult.Success:
                    Debug.Log($"Login succeeded: {auth.CurrentUser?.Payload.Name}");
                    break;
                case AuthorizeProcessResult.Cancelled:
                    Debug.Log("User cancelled login");
                    break;
                case AuthorizeProcessResult.Failed:
                    Debug.LogError($"Login failed: {result.ErrorMessage}");
                    break;
            }
        });
}
```

There's also `UniTask<AuthorizeResult> AuthorizeViaBrowserAsync(...)` for an `await` style. Internal
flow: `Application.OpenURL` opens the system browser → user logs in / consents → the auth server
redirects back via `RedirectUri` (`{MyAppScheme}://callback`) carrying `code`/`state` →
`DeepLinkHandler` receives it → `AuthProvider` validates `state`, exchanges the PKCE
`code_verifier` for a token, and validates the `id_token`'s `nonce`/`aud`/`iss`/`exp`. Full sequence
and error paths are in `references/auth-flow.md`.

**Important limitation**: `EWovaAuth.Instance` is constructed with `ClientId` hardcoded to
`"learning-portfolio-sdk"` (see the TODO comment in `EwovaAuth.cs`, officially noted as a temporary
approach that may be decoupled later). If your app needs a different OAuth client id, `AuthProvider`
is an inheritable abstract class — subclass it and build a custom `EWovaAuthConfig` via
`EWovaAuthConfigFactory.Create(...)` to pass into the base constructor; don't try to `new` an
`EWovaAuthConfig` directly (the constructor is internal). This is the established pattern other
EWova packages use (e.g. the Wristband package's own auth singleton, LearningPortfolioSDK's), not a
one-off hack:

```csharp
public class YourAppAuth : AuthProvider
{
    public static readonly YourAppAuth Instance = new();

    private YourAppAuth()
        : base(EWovaAuthConfigFactory.Create(options =>
        {
            options.ClientId = "your-client-id"; // issued by the EWova platform, not something you invent
            options.Scopes = new List<string>
            {
                "openid", "profile", "email", "roles", "organization", "offline_access"
            };
        }), new Logger("[YourApp]Auth ", LogLevel.Full))
    { }
}
```

`AuthorizeViaBrowser`/`AuthorizeViaBrowserAsync` throws `InvalidOperationException` if called while
already `Authenticated`/`RefreshingToken` — call `Logout()` first if you need to switch accounts.

**Platform limitation**: `AuthorizeViaBrowser` requires
`AuthProvider.IsSupportAuthorizeViaDeepLink == true` (backed by `DeepLinkHandler.IsSupported`).
Platforms without a matching DeepLink provider throw `PlatformNotSupportedException`. In the Editor
there's `MockDeepLinkReceiver` (`AuthProvider.EnableMockDeepLinkReceiver`) to manually paste a deep
link or simulate a login callback using an `admin.ewova` launch ticket, so you can test without
shipping a build.

## Getting and refreshing the access token

```csharp
if (auth.TryGetValidAccessToken(out string token))
{
    // Use directly, not expired
}

// Or request explicitly (auto-refreshes if expired, returns the current value if still valid)
string accessToken = await auth.GetAccessTokenAsync(cancellationToken);
```

- `RefreshAccessTokenAsync` forces a refresh cycle to run (even if the current token isn't expired
  yet), transitioning through `AuthState.RefreshingToken`.
- If a refresh fails and the refresh token is expired/missing, `AuthProvider` auto-`Logout()`s and
  throws `RefreshTokenExpiredException`.
- While logged in, `AuthProvider` checks every 15 seconds internally and auto-renews in the
  background once less than 60 seconds remain (`StartTokenRenewLoop`) — callers usually don't need
  to schedule their own refresh.
- `auth.CurrentUser` (type `UserIdentity?`) wraps the raw `JwtPayload` parsed from the `id_token`'s
  JWT claims (`CurrentUser.Value.Payload.Subject`, `.Name`, `.Email`, `.OrgId`, `.Roles`, etc.). Note
  some auth servers don't return a new `id_token` on refresh, in which case `CurrentUser` keeps its
  old value instead of being cleared — this is expected.

## Calling a protected API

Subclass `AuthApiClient` (`EWova.Networking`); it automatically attaches to every request:
- `Authorization: Bearer {token}` (when `IAuthManager.TryGetValidAccessToken` succeeds)
- an `X-Unity-Sdk` header (core version plus any package versions you report via `CollectPackages`)

```csharp
using EWova.Networking;
using EWova.Auth;
using Cysharp.Threading.Tasks;

public class MyApiClient : AuthApiClient
{
    public MyApiClient(IAuthManager auth, string baseUrl) : base(auth, baseUrl) { }

    public UniTask<MyDto> GetSomethingAsync(CancellationToken ct = default)
    {
        return Send<MyDto>(RequestTask.GET("v1/something", ct: ct));
    }
}

var client = new MyApiClient(EWovaAuth.Instance, "https://api.example.com");
var dto = await client.GetSomethingAsync();
```

- `RequestTask.GET/POST/PUT/DELETE(...)` are static factories for building a request; `body` can be
  a plain object (anything non-string gets `JsonConvert.SerializeObject`'d automatically).
- 4xx responses throw `ApiException` by default (`ThrowApiExceptionFor4xxResponses = true`); disable
  it to inspect the response yourself instead. `ApiException.ErrorCode` / `StatusCode` /
  `ResponseText` are available for error handling.
- `AuthApiClient.DefaultRequestTimeoutSeconds` (default 30s, `static`, applies globally) controls
  `UnityWebRequest.timeout`.
- Image downloads use the built-in `GetTex2D(url, isAbsoluteUrl, ct)`, which returns a `Texture2D`
  (returns `null` and logs on failure instead of throwing — different behavior from `Send<T>`, so
  callers should account for that).

## EWova App deep-link integration

If your app needs to jump into the EWova metaverse app (or be launched by it):

```csharp
using EWova;

// Jump to the EWova app, carrying back the current world/space info (if any)
EWovaApp.LaunchViaDeepLink(EWovaDeepLinkLaunchOption.Default);

// If your app was launched by the EWova app, read the world/space id it passed in
if (EWovaApp.InvocationContext is { } ctx)
{
    Guid? worldId = ctx.WorldGuid;
    int? spaceIndex = ctx.SpaceInstanceIndex;
}
```

`EWovaAuth.LaunchEWovaAppWithLoginAsync(requestAppId, worldId, spaceId)` is another path: it
refreshes the access token → exchanges it for a one-time `launch_ticket` with the auth server →
builds a deep link with that ticket and opens the EWova app, letting it pick up your existing login
session without the user logging in again.

## Environment / DeploymentMode

`EWova.Environment.DeploymentMode` is only switchable in the Editor (via
`Authoring.EWovaEditorPrefs`, menu `DeploymentModeSwitcher`); a Player build always returns
`DeploymentMode.Production`. Its only current effect is which auth server
`EWovaAuthConfigFactory` picks: `https://auth.ewova.com` (Production) vs
`https://auth.ewova.dev` (Development). When testing login in the Editor, a wrong environment here
is a common cause of "credentials are correct but login still fails" — check this switch first.

## Public but not meant for direct third-party use

These classes are technically `public`, but by naming, purpose, and call pattern they're internal
implementation details of `AuthProvider`/`AuthApiClient`, not a stable external API. Third-party
code generally shouldn't (and doesn't need to) call them directly:

- `EWova.Auth.TokenService` / `TokenEndpointException` / `RefreshTokenExpiredException` — created
  and invoked internally by `AuthProvider` (`ExchangeCodeAsync` / `RefreshTokenAsync` /
  `ExchangeLaunchTicketAsync`). Use `IAuthManager.GetAccessTokenAsync`/`RefreshAccessTokenAsync`
  instead of constructing a `TokenService` yourself.
- `EWova.Auth.AuthRequestBuilder`, `EWova.Auth.PkceHelper` — low-level helpers for building OIDC
  URLs / PKCE strings; `AuthProvider` already handles this, normal flows don't need to call them.
- `EWova.Auth.MockDeepLinkReceiver` — its doc comment says it's for Editor-mode testing, but the
  `MonoBehaviour` itself isn't wrapped in `#if UNITY_EDITOR`, so it does get compiled into a
  production build. If you use it, only attach it to a test scene — don't leave it in a scene you
  ship.
- `EWova.HttpUtility` — a Mono-ported general URL/HTML encoding helper with no direct relation to
  the Auth flow; `AuthProvider` only uses it internally to parse the deep-link query string, it's
  not part of the Auth module's intended external surface.

## Common pitfalls

- **Threading**: `AuthApiClient` / `TokenService` requests run over UniTask + `UnityWebRequest`,
  and callbacks return to the main thread; if you need to touch Unity APIs from a non-main-thread
  callback, use `EWova.UnityMainThreadDispatcher.Enqueue(...)`, and check with
  `EWova.Networking.PlayerLoopHelper.IsMainThread` / `ThrowIfNotMainThread()`.
- **`JWT` / `JwtObject` / `JwtException`**: defined in the **global namespace** (not `EWova.Auth`) —
  don't (and can't) prefix them with a namespace in `using`.
- **Don't depend on editor-only concepts in runtime logic**: `Authoring.*`
  (`DeploymentModeSwitcher`, `EWovaEditorPrefs`, `EditorDomainReleaseHelper`, `DevelopTip`,
  `EditorLogger`), the `EditorLoadOrCreateResource` branch of `DeepLinkConfig`, and
  `MockDeepLinkReceiver` only make sense under `UNITY_EDITOR` or in test scenes — don't assume
  they exist in a production build.
- **`PackageInfo` is `internal`** — third-party code can't see it or use it to read the version.
- **`EWovaAuthConfig`'s constructor is internal** — always build it via
  `EWovaAuthConfigFactory.Create(...)`, never `new` it directly.
- **Only one deep-link callback is processed at a time**: `AuthProvider` has a
  `_isProcessingDeepLink` re-entrancy guard — a callback URL arriving while one is already being
  processed is dropped (with a warning log), not queued.
- **A missing DeepLink config fails login silently, not with an exception**: outside the Editor,
  `DeepLinkConfig.LoadOrDefault()` just logs `Debug.LogError` if the resource is missing, and
  `IsSupportAuthorizeViaDeepLink` becomes `false` — login then fails without throwing. Before
  shipping, confirm `Resources/DeeplinkConfig.asset` is actually included in the build.

## Debugging

`EWovaAuth.Instance.LoggerLevel` / `AuthApiClient.LoggerLevel` (a `LogLevel` flags enum:
`Info | Warn | Error`) control log verbosity — set it to `LogLevel.Full` to see internal logs
(usually prefixed `[EWova]xxx`) when debugging login or API issues.

## Further reading

For the full OAuth/PKCE exchange sequence, the `AuthState` state-machine diagram, and the complete
`AuthApiClient`/`TokenService` method signature list, see `references/auth-flow.md`.
