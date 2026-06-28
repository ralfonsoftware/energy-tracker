---
baseline_commit: 13dddaf2c8b3ce14e47046b998f0d092499bb6f9
---

# Story 1.4: SWA Easy Auth & TenantResolver Middleware

Status: done

## Story

As an authenticated user,
I want all app routes to require authentication via Azure Entra ID,
so that my energy data is protected and I am returned to the originally requested route after signing in.

## Acceptance Criteria

1. **Unauthenticated route access triggers OIDC redirect** — Given an unauthenticated browser session, when any app route is accessed (including deep links to `/settings`, `/insights`, etc.), then SWA Easy Auth intercepts the request and redirects to the Azure Entra ID OIDC login flow.

2. **Post-login redirect to original route** — Given a successful OIDC login, when the auth callback completes, then the user lands on the originally requested route, not the app root.

3. **Session persists across browser restart** — Given an authenticated session, when the browser is closed and reopened, then the session persists without requiring re-authentication (until natural session expiry or sign-out).

4. **TenantResolver middleware extracts UserId** — Given any HTTP Function in the Functions app, when a request arrives with the `X-MS-CLIENT-PRINCIPAL` header injected by SWA Easy Auth, then `TenantResolver` middleware registered in `Program.cs` extracts the `userId` field from the decoded principal and makes the resolved `UserId` available in `FunctionContext.Items["UserId"]`; a missing or malformed header returns HTTP 403 Problem Details.

5. **OIDC provider is config-driven** — Given a change to the OIDC provider configuration (environment variable / config file swap), when the app is redeployed, then all auth flows route through the new OIDC provider with zero code changes.

## Tasks / Subtasks

- [x] Task 1: Configure SWA Easy Auth routes in `staticwebapp.config.json` (AC: 1, 2, 3, 5)
  - [x] Add `auth.identityProviders.azureActiveDirectory` section referencing `AZURE_CLIENT_ID` app setting and `AZURE_AD_TENANT_ID` app setting in the issuer URL
  - [x] Add route rule `/.auth/*` → `allowedRoles: ["anonymous"]`
  - [x] Add route rule `/api/*` → `allowedRoles: ["anonymous"]` (auth enforced by TenantResolver in Functions)
  - [x] Add route rule `/*` → `allowedRoles: ["authenticated"]`
  - [x] Add `responseOverrides` section: `401` → redirect to `/.auth/login/aad` with `post_login_redirect_uri` preserved
  - [x] Keep existing `navigationFallback` rewrite

- [x] Task 2: Create `TenantResolverMiddleware` (AC: 4)
  - [x] Create `api/Shared/TenantResolverMiddleware.cs` implementing `IFunctionsWorkerMiddleware`
  - [x] Add internal `ClientPrincipal` and `UserClaim` record types
  - [x] Implement `TryResolveUserId(HttpContext, out string)` as `internal static` method (enables unit testing)
  - [x] Skip auth check for non-HTTP triggers (`context.GetHttpContext()` returns `null`)
  - [x] On success: store `userId` in `context.Items["UserId"]` and call `next(context)`
  - [x] On failure: write HTTP 403 Problem Details response, do NOT call `next(context)`

- [x] Task 3: Create `FunctionContextExtensions` helper (AC: 4)
  - [x] Create `api/Shared/FunctionContextExtensions.cs` with `GetUserId(this FunctionContext)` extension method
  - [x] Method returns the stored `UserId` string or throws `InvalidOperationException` if not resolved

- [x] Task 4: Register middleware in `Program.cs` (AC: 4)
  - [x] Change `builder.ConfigureFunctionsWebApplication()` to pass a `WorkerOptions` delegate that calls `worker.UseMiddleware<TenantResolverMiddleware>()`

- [x] Task 5: Create `authClient.ts` frontend auth wrappers (AC: 1, 2, 3)
  - [x] Create `client/src/lib/authClient.ts`
  - [x] Export `getMe()` — `GET /.auth/me` → returns `SwaAuthUser | null`
  - [x] Export `login(returnUrl?)` — navigates to `/.auth/login/aad` with `post_login_redirect_uri`
  - [x] Export `logout(returnUrl?)` — navigates to `/.auth/logout` with `post_logout_redirect_uri`
  - [x] Define `SwaAuthUser` and `MeResponse` TypeScript interfaces (no `any` types)

- [x] Task 6: Write unit tests for `TenantResolverMiddleware` (AC: 4)
  - [x] Create `api.Tests/Shared/TenantResolverMiddlewareTests.cs`
  - [x] Test: valid header with `userId` → `TryResolveUserId` returns `true`, extracts correct `userId`
  - [x] Test: missing `X-MS-CLIENT-PRINCIPAL` header → returns `false`
  - [x] Test: header present but not valid Base64 → returns `false`
  - [x] Test: valid Base64 but JSON missing `userId` field → returns `false`
  - [x] Test: valid Base64 but `userId` is empty string → returns `false`
  - [x] Test: valid Base64 but `userId` is whitespace → returns `false`

- [x] Task 7: Final verification (AC: 1–5)
  - [x] `dotnet test` passes — 18 passed (11 prior + 6 new TenantResolver + 1 UnitTest1 stub), 0 failed, no regressions
  - [x] `dotnet build` exits 0
  - [x] File List updated

## Dev Notes

### Architecture References (AD-9, AD-12)

**AD-9 — SWA Easy Auth → Functions trust `X-MS-CLIENT-PRINCIPAL` header:**
- SWA validates the OIDC token at the edge. The Functions app NEVER validates tokens — only reads the pre-validated header.
- `X-MS-CLIENT-PRINCIPAL` is Base64-encoded JSON injected by SWA on every authenticated request.
- Functions app reads this header via `TenantResolverMiddleware` registered in `Program.cs`.
- A missing or malformed header → HTTP 403 Problem Details (NOT 401 — SWA owns 401).

**AD-12 — Tenant isolation via middleware:**
- `TenantResolver` runs in Azure Functions middleware pipeline, before any Function handler.
- Every Function has access to the resolved `UserId` via `FunctionContext.Items["UserId"]`.
- All EF Core queries MUST be scoped to the resolved `UserId` (via `Flat`) — never raw unscoped queries.
- Enforcement at Function layer, not inside engines or calculators.

### `X-MS-CLIENT-PRINCIPAL` Header Format

The decoded JSON structure from SWA Easy Auth (AAD provider):
```json
{
  "identityProvider": "aad",
  "userId": "00000000-0000-0000-0000-000000000000",
  "userDetails": "user@example.com",
  "userRoles": ["authenticated", "anonymous"],
  "claims": [
    { "typ": "sub", "val": "..." },
    { "typ": "oid", "val": "..." }
  ]
}
```

**Use `userId` field directly** — this is SWA's stable normalized identifier (maps to AAD OID). The architecture's reference to "OIDC `sub` claim" is satisfied by this field in the SWA context. Do NOT try to extract the `sub` from the claims array; it's fragile and unnecessary.

### ASP.NET Core Integration — `GetHttpContext()` vs `GetHttpRequestDataAsync()`

The project uses `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` (already in csproj). This enables ASP.NET Core HTTP integration.

- **Use `context.GetHttpContext()`** (from the AspNetCore extensions package), NOT `GetHttpRequestDataAsync()`
- `GetHttpContext()` returns `null` for non-HTTP triggers (timer, blob, queue) — handle this explicitly to skip auth check
- Headers are at `httpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL"]`
- Write 403 response via `httpContext.Response.StatusCode = 403` + `WriteAsJsonAsync(...)`

### TenantResolverMiddleware — Complete Implementation Pattern

```csharp
// api/Shared/TenantResolverMiddleware.cs
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnergyTracker.Api.Shared;

public sealed class TenantResolverMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();

        if (httpContext is null)
        {
            // Non-HTTP trigger (timer, blob, queue) — skip auth check
            await next(context);
            return;
        }

        if (!TryResolveUserId(httpContext, out var userId))
        {
            httpContext.Response.StatusCode = 403;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                title = "Forbidden",
                status = 403,
                detail = "Missing or invalid authentication context."
            });
            return; // Do NOT call next()
        }

        context.Items["UserId"] = userId;
        await next(context);
    }

    internal static bool TryResolveUserId(Microsoft.AspNetCore.Http.HttpContext httpContext, out string userId)
    {
        userId = string.Empty;

        var headerValue = httpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL"].FirstOrDefault();
        if (string.IsNullOrEmpty(headerValue))
            return false;

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(headerValue));
            var principal = JsonSerializer.Deserialize<ClientPrincipal>(decoded, _jsonOptions);

            if (principal is null || string.IsNullOrWhiteSpace(principal.UserId))
                return false;

            userId = principal.UserId;
            return true;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed record ClientPrincipal(
    [property: JsonPropertyName("identityProvider")] string IdentityProvider,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("userDetails")] string UserDetails,
    [property: JsonPropertyName("userRoles")] IReadOnlyList<string> UserRoles,
    [property: JsonPropertyName("claims")] IReadOnlyList<UserClaim>? Claims = null
);

internal sealed record UserClaim(
    [property: JsonPropertyName("typ")] string Typ,
    [property: JsonPropertyName("val")] string Val
);
```

### FunctionContextExtensions Pattern

```csharp
// api/Shared/FunctionContextExtensions.cs
using Microsoft.Azure.Functions.Worker;

namespace EnergyTracker.Api.Shared;

public static class FunctionContextExtensions
{
    public static string GetUserId(this FunctionContext context)
    {
        if (context.Items.TryGetValue("UserId", out var userId) && userId is string userIdStr)
            return userIdStr;
        throw new InvalidOperationException(
            "UserId not resolved. Ensure TenantResolverMiddleware is registered in Program.cs.");
    }
}
```

### Program.cs Change (Task 4)

Existing `Program.cs` uses:
```csharp
builder.ConfigureFunctionsWebApplication();
```

Change to:
```csharp
builder.ConfigureFunctionsWebApplication(worker =>
{
    worker.UseMiddleware<TenantResolverMiddleware>();
});
```

All other `Program.cs` code (OpenTelemetry, DbContext registration) remains unchanged.

### `staticwebapp.config.json` — Complete Updated Version

The current file only has `navigationFallback` and one route. Replace entirely:
```json
{
  "auth": {
    "identityProviders": {
      "azureActiveDirectory": {
        "registration": {
          "openIdIssuer": "https://login.microsoftonline.com/{AZURE_AD_TENANT_ID}/v2.0",
          "clientIdSettingName": "AZURE_CLIENT_ID"
        }
      }
    }
  },
  "routes": [
    {
      "route": "/.auth/*",
      "allowedRoles": ["anonymous"]
    },
    {
      "route": "/api/*",
      "allowedRoles": ["anonymous"]
    },
    {
      "route": "/*",
      "allowedRoles": ["authenticated"]
    }
  ],
  "responseOverrides": {
    "401": {
      "redirect": "/.auth/login/aad",
      "statusCode": 302
    }
  },
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/api/*", "/_framework/*", "/assets/*", "/*.{ico,png,svg,woff,woff2,css,js}"]
  }
}
```

**Important:** `{AZURE_AD_TENANT_ID}` is NOT an app setting reference — it must be replaced with the actual tenant ID GUID from the Azure infrastructure (Story 1.2). `AZURE_CLIENT_ID` IS an app setting reference (SWA resolves the curly-brace-less `clientIdSettingName` pattern from Azure App Settings). AC5 (provider-swappable) is satisfied because secrets come from app settings and the provider type can be changed by editing this config file without touching application code.

### `authClient.ts` — Complete Implementation

Create `client/src/lib/authClient.ts`:
```typescript
export interface SwaAuthUser {
  identityProvider: string;
  userId: string;
  userDetails: string;
  userRoles: string[];
  claims?: Array<{ typ: string; val: string }>;
}

interface MeResponse {
  clientPrincipal: SwaAuthUser | null;
}

export async function getMe(): Promise<SwaAuthUser | null> {
  const res = await fetch('/.auth/me');
  if (!res.ok) return null;
  const data: MeResponse = await res.json();
  return data.clientPrincipal;
}

export function login(returnUrl: string = window.location.href): void {
  window.location.href = `/.auth/login/aad?post_login_redirect_uri=${encodeURIComponent(returnUrl)}`;
}

export function logout(returnUrl: string = '/'): void {
  window.location.href = `/.auth/logout?post_logout_redirect_uri=${encodeURIComponent(returnUrl)}`;
}
```

This file lives in `client/src/lib/` alongside future `apiClient.ts`, `queryClient.ts`, `i18n.ts`. Only create `authClient.ts` — do NOT create the other lib files (those belong to stories 1.5 and 2.1).

### Testing `TryResolveUserId`

`TryResolveUserId` is `internal static` and takes an `HttpContext`. Tests use `DefaultHttpContext` from `Microsoft.AspNetCore.Http`:

```csharp
// Build a valid X-MS-CLIENT-PRINCIPAL header value
private static string MakeHeader(string userId)
{
    var obj = new { identityProvider = "aad", userId, userDetails = "u@test.com", userRoles = new[] { "authenticated" } };
    var json = JsonSerializer.Serialize(obj);
    return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
}
```

The test project `api.Tests` already references `api` project. To access `internal` types from tests, add `InternalsVisibleTo` to `api/energy-tracker-api.csproj`:
```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
    <_Parameter1>EnergyTracker.Api.Tests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

Verify the test project's assembly name is `EnergyTracker.Api.Tests` before adding this.

### Directory Structure — New Files

```
api/
  Shared/
    TenantResolverMiddleware.cs   ← NEW
    FunctionContextExtensions.cs  ← NEW
api.Tests/
  Shared/
    TenantResolverMiddlewareTests.cs  ← NEW
client/src/
  lib/
    authClient.ts  ← NEW
staticwebapp.config.json  ← MODIFIED
api/Program.cs              ← MODIFIED
api/energy-tracker-api.csproj ← MODIFIED (InternalsVisibleTo)
```

### Patterns from Previous Stories

Preserve all patterns established in stories 1.1–1.3:
- No Data Annotation attributes on entity classes (no new entities in this story)
- Record types for all DTOs
- `async` methods suffixed `Async`, accept `CancellationToken ct`
- Problem Details RFC 9457 for all error responses
- `decimal` for all kWh/monetary values (no new numeric types in this story)

### What This Story Does NOT Implement

- Any actual Azure Functions with business logic (no HTTP endpoints in this story)
- `TariffResolver.cs` — later story
- `LocaleResolver.cs` — story 2.1
- `useAuth` React hook — story 1.5 (app shell) will add it
- Onboarding gate — story 2.2
- No new EF Core migrations required

### Deferred Work Context

See `_bmad-output/implementation-artifacts/deferred-work.md` for any deferred items from previous stories. `UnitTest1.Test1()` empty stub was noted but remains; do not remove it in this story.

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- `ConfigureFunctionsWebApplication(worker => ...)` overload not available on `FunctionsApplicationBuilder` — registered middleware via `builder.UseMiddleware<TenantResolverMiddleware>()` as a separate call after `ConfigureFunctionsWebApplication()`.
- `HttpResponse.WriteAsJsonAsync()` extension not available without explicit `using Microsoft.AspNetCore.Http;` — switched to `JsonSerializer.Serialize()` + `WriteAsync()` to avoid the dependency.
- `InternalsVisibleTo` requires exact assembly name `api.Tests` (from project file name), not `EnergyTracker.Api.Tests`.
- `authClient.ts` already existed from Story 1.1 scaffold with an object-export pattern; updated to named-function exports with `returnUrl` parameters and `SwaAuthUser` interface per story spec. No other files in `client/src/lib/` were touched.

### Completion Notes List

- **TenantResolverMiddleware**: Implements `IFunctionsWorkerMiddleware`. Skips auth for non-HTTP triggers (`GetHttpContext()` returns null). On valid `X-MS-CLIENT-PRINCIPAL` header: decodes Base64 JSON, extracts `userId` field (SWA's normalized stable identifier), stores in `FunctionContext.Items["UserId"]`. On missing/malformed header: writes HTTP 403 Problem Details and short-circuits (does NOT call next).
- **FunctionContextExtensions.GetUserId()**: Convenience extension method for downstream Functions to read the resolved UserId from context without string literals.
- **Program.cs**: Middleware registered as `builder.UseMiddleware<TenantResolverMiddleware>()` after `ConfigureFunctionsWebApplication()`.
- **staticwebapp.config.json**: Fully replaced with auth config. `/.auth/*` and `/api/*` remain anonymous; `/*` requires `authenticated` role; 401s redirect to `/.auth/login/aad`. `{AZURE_AD_TENANT_ID}` in the issuer URL is a placeholder that must be replaced with the actual tenant GUID from Azure infra (Story 1.2).
- **authClient.ts**: Updated from object-export to named function exports. `getMe()` returns `SwaAuthUser | null` directly. `login(returnUrl?)` and `logout(returnUrl?)` pass `post_login_redirect_uri`/`post_logout_redirect_uri` parameters.
- **Tests**: 6 unit tests on `TryResolveUserId` covering valid header, missing header, invalid base64, missing userId field, empty userId, whitespace userId. All pass. Total suite: 18 passed, 0 failed.

### File List

| File | Action |
|------|--------|
| `staticwebapp.config.json` | Modified — added SWA Easy Auth config, route rules, responseOverrides |
| `api/Shared/TenantResolverMiddleware.cs` | Created |
| `api/Shared/FunctionContextExtensions.cs` | Created |
| `api/Program.cs` | Modified — added `using EnergyTracker.Api.Shared` + `builder.UseMiddleware<TenantResolverMiddleware>()` |
| `api/energy-tracker-api.csproj` | Modified — added `InternalsVisibleTo` for `api.Tests` assembly |
| `client/src/lib/authClient.ts` | Modified — updated to named exports, SwaAuthUser interface, returnUrl params |
| `api.Tests/Shared/TenantResolverMiddlewareTests.cs` | Created |

### Review Findings

- [x] [Review][Patch] `getMe()` unhandled rejections on network failure and non-JSON response body [`client/src/lib/authClient.ts:getMe`]
- [x] [Review][Patch] `getMe()` returns `undefined` instead of `null` when `clientPrincipal` key absent in response body [`client/src/lib/authClient.ts:getMe`]
- [x] [Review][Patch] 401 `responseOverrides` redirect missing `post_login_redirect_uri` — breaks AC2 post-login return to original route [`staticwebapp.config.json`]
- [x] [Review][Defer] `InternalsVisibleTo` via MSBuild `<AssemblyAttribute>` may conflict with auto-generated `AssemblyInfo.cs` if added in future [`api/energy-tracker-api.csproj`] — deferred, pre-existing

## Change Log

- Story created from epics.md and architecture.md (Date: 2026-06-28)
- Implemented SWA Easy Auth routes, TenantResolverMiddleware, FunctionContextExtensions, authClient.ts; 6 unit tests added; all 18 tests pass (Date: 2026-06-28)
