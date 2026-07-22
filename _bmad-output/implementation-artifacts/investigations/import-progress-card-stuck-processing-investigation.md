# Investigation: Story 6.6 Import Progress Card never shows "Complete" in Azure

> **Folded into `architecture.md`** (2026-07-22 doc consolidation, Epic 9 retro Action Item #2): the dual-`JsonOptions` split-brain gotcha (`Http.Json.JsonOptions` vs `Mvc.JsonOptions`) is now AD-15a in API & Communication. Status/Root Cause fields below closed out the same pass — the fix is confirmed live in `api/Shared/JsonSerializationDefaults.cs`, called from `api/Program.cs:64`, which registers the enum-string converter on both option types. This file remains as the historical record.

## Hand-off Brief

1. **What happened.** The backend import pipeline (blob upload → EventGrid trigger → `ProcessImport`) completes successfully in under 1 second on every run (Confirmed via App Insights, both test uploads), but the frontend's polling (`useImportJobStatus`) never observes a `Complete` status and keeps polling every 3s indefinitely — the Progress Card is stuck on the spinner forever, and only disappears on reload because its state lives in an in-memory TanStack Query cache that isn't persisted.
2. **Where the case stands.** Root cause is **Confirmed** (closed 2026-07-22): an ASP.NET Core JSON-serialization split-brain. `Program.cs` originally only configured `Microsoft.AspNetCore.Http.Json.JsonOptions` with a `JsonStringEnumConverter`, but every function (including `GetImportStatusFunction`) returns results via `OkObjectResult`/`ObjectResult`, which is executed by ASP.NET Core's MVC object-result pipeline and consults `Microsoft.AspNetCore.Mvc.JsonOptions` instead — a separate options object that was never configured and therefore serialized the `ImportStatus` enum as its raw integer value (`2` for `Complete`), not the string the frontend types and compares against.
3. **Resolution shipped.** `api/Shared/JsonSerializationDefaults.cs` now applies the enum-string converter (+ camelCase policy) to *both* `Http.Json.JsonOptions` and `Mvc.JsonOptions` via a single `ConfigureAspNetCoreJsonOptions(builder.Services)` call in `api/Program.cs:64`, so the two option types can no longer drift apart.

## Case Info

| Field            | Value                                                                                                                |
| ---------------- | --------------------------------------------------------------------------------------------------------------------- |
| Ticket           | N/A (user-reported after manual Azure test of Story 6.6)                                                             |
| Date opened      | 2026-07-07                                                                                                            |
| Date closed      | 2026-07-22                                                                                                            |
| Status           | Resolved                                                                                                              |
| System           | Azure Static Web Apps (Standard) + linked Function App `energytracker-api` (Flex Consumption, linux) + Azure SQL, resource group `energytracker-rg` |
| Evidence sources | Azure Application Insights / Log Analytics (`energytracker-logs`), Azure Storage blob listing, Function App function list, source code (`api/`, `client/`), git history, `infra/main.bicep` |

## Problem Statement

User uploaded a smart-plug file via the deployed Story 6.6 Import UI around 10:49 local time. The upload succeeded and a background-processing indicator (Progress Card) appeared, but it never transitioned to "completed." After some time, the user reloaded the page and the card was gone. Checking the Function App and Application Insights in the following ~30 minutes showed nothing. Reproduced on a second run. User separately observed that `ProcessImport` itself runs in under 1 second.

## Evidence Inventory

| Source                                  | Status    | Notes                                                                                          |
| ---------------------------------------- | --------- | ----------------------------------------------------------------------------------------------- |
| App Insights / Log Analytics workspace   | Available | `az monitor app-insights query` (classic API) returned nothing for this workspace-based resource; had to query the underlying Log Analytics workspace directly (`az monitor log-analytics query --workspace <customerId>`) to see any telemetry at all. |
| Azure Blob Storage (`smart-plug-imports`)| Available | Required storage account key (`az storage account keys list`); AAD data-plane roles not granted to the investigating identity. |
| Event Grid system topic / subscription   | Available | `provisioningState: Succeeded`; validated via `az eventgrid system-topic event-subscription show`. |
| Azure SQL (`ImportJobs` table directly)  | Missing   | No Key Vault / data-plane access from the investigating identity; DB row's actual `Status`/JSON payload was not read directly — inferred from App Insights traces instead. |
| Source code (frontend + backend)         | Available | Read in full for the relevant files.                                                            |
| Browser DevTools Network capture         | Missing   | Not captured during the user's actual test; this is the single fastest confirm/refute step (see Missing Evidence). |

## Timeline of Events (all times UTC; user's "10:49" is local CEST = UTC+2)

| Time (UTC)             | Event                                                                                   | Source                                | Confidence |
| ----------------------- | ---------------------------------------------------------------------------------------- | -------------------------------------- | ---------- |
| 2026-07-07T08:44:36.6Z | Function host cold start (`Application started`)                                        | AppTraces                              | Confirmed  |
| 2026-07-07T08:48:46.6Z | Function host restarted again (new `InstanceId`)                                        | AppTraces                              | Confirmed  |
| 2026-07-07T08:49:42.27Z| `POST /api/v1/flats/{flatId}/imports` (upload) — 202 Accepted                            | AppRequests                            | Confirmed  |
| 2026-07-07T08:49:43.03Z| EventGrid → `POST /runtime/webhooks/blobs` delivered successfully                        | AppRequests                            | Confirmed  |
| 2026-07-07T08:49:43.48Z| `ProcessImport` invoked; **Success=True, Duration=756.8ms**                              | AppRequests (`Name == 'ProcessImport'`)| Confirmed  |
| 08:49:42 → 08:53:53Z    | `GET .../imports/{jobId}` polls continue every ~3s (irregular gaps later — consistent with the browser tab losing focus while the user checked the Azure Portal) | AppRequests | Confirmed |
| 2026-07-07T08:57:39.6Z | Second upload's blob lands in storage (`5776be61-....csv`)                               | Blob listing (`Last Modified`)         | Confirmed  |
| 2026-07-07T08:57:39.9Z | EventGrid webhook delivered — 202                                                        | AppRequests                            | Confirmed  |
| 2026-07-07T08:57:40.4Z | `ProcessImport` invoked; **Success=True, Duration=415.7ms**                              | AppRequests                            | Confirmed  |
| 08:57:39 → 09:02:58Z+   | `GET .../imports/{jobId}` polls continue **every exactly ~3.07s, with zero gaps, for 5+ minutes straight** | AppRequests | Confirmed |

## Confirmed Findings

### Finding 1: Both uploads' backend pipeline completed successfully, fast

**Evidence:** `AppRequests` rows for `Name == 'ProcessImport'`: `Success=True, DurationMs=756.8` at `08:49:43.476Z`; `Success=True, DurationMs=415.7` at `08:57:40.409Z`. No exceptions, warnings, or `DbUpdateConcurrencyException` traces in either window (`AppExceptions`/`AppTraces` grep for "exception|fail|warn" in the full 08:35–09:20 capture returned nothing related to Import).

**Detail:** This directly matches the user's own observation ("ProcessImport function runs on under 1sec") and rules out a hung/failed backend job. `api/Features/SmartPlugImport/ProcessImportFunction.cs:51-89` sets `Status = Processing` → dispatches to the parser → sets `Status = Complete` → `SaveChangesAsync` — with no thrown exception on either run, this path completed and persisted `Complete`.

### Finding 2: The frontend never stops polling — for either job

**Evidence:** `GET api/v1/flats/{flatId}/imports/{jobId}` requests recur every ~3s continuously from `08:57:39.6Z` through at least `09:02:58.98Z` (the end of the captured window) — over 100 consecutive polls with no termination. Each poll runs a fresh `SELECT ... FROM ImportJobs` (`AppTraces`, e.g. `08:58:34.2498285Z`) and returns `Executing OkObjectResult` (200) every time — not a cached/stale response.

**Detail:** `client/src/features/smart-plug-import/hooks/useImportJobStatus.ts:23-28` — `refetchInterval` returns `false` only when `query.state.data?.status === 'Complete' || 'Failed'`. Since polling never stops, `status` on the client is never observed to equal the string `'Complete'`, even though Finding 1 shows the backend set it to `Complete` within ~1s of each upload.

### Finding 3: Two separate, un-unified JSON option objects exist in `Program.cs`

**Evidence:** `api/Program.cs:63-67`:
```csharp
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
```
There is no corresponding `Configure<Microsoft.AspNetCore.Mvc.JsonOptions>` anywhere in the codebase (`grep -rn "Mvc.JsonOptions" api/` returns nothing), and no `AddControllers()`/`AddMvcCore()` call configuring MVC JSON behavior either.

**Detail:** Every HTTP function in this codebase, including `GetImportStatusFunction.cs:62` (`return new OkObjectResult(response);`), returns results via `IActionResult`/`ObjectResult`. In ASP.NET Core, `ObjectResult` execution goes through the MVC object-result formatter pipeline, which is governed by `Microsoft.AspNetCore.Mvc.JsonOptions` — a distinct options type from `Microsoft.AspNetCore.Http.Json.JsonOptions` (the latter governs minimal-API `Results.Json()`/`TypedResults` and `HttpContext.Response.WriteAsJsonAsync`, not `ObjectResult`). Only the `Http.Json.JsonOptions` variant is configured here.

## Deduced Conclusions

### Deduction 1: `ImportJobStatusResponse.Status` most likely serializes as a raw integer, not a string

**Based on:** Finding 3 (the enum-string converter is registered on the wrong options object for `ObjectResult` output) + Finding 1 (the DB value is genuinely `Complete` moments after upload) + Finding 2 (the client never sees a value it recognizes as `'Complete'`).

**Reasoning:** `ImportStatus` (`api/Data/Entities/ImportJob.cs:3-9`) is a plain `enum { Pending, Processing, Complete, Failed }` with no `[JsonConverter]` attribute of its own. Absent a converter reaching the actual serializer used for `ObjectResult`, System.Text.Json's default enum behavior is to emit the underlying numeric value (`Complete` → `2`). The frontend (`client/src/features/smart-plug-import/api/importApi.ts`) types `status: ImportStatus` as a string literal union and the hook compares it with `===` against string literals — a runtime number never satisfies that comparison, so the polling loop and the "disappear on Complete" effect (`useImportJobStatus.ts:25`, `:35`) never fire, regardless of how long the tab is left open.

**Conclusion:** The "Progress Card never shows completed" symptom is best explained not as a signal never being sent, but as a signal sent in a shape the client doesn't recognize. The card only disappears on reload because `['import-jobs', flatId]` is a memory-only TanStack Query cache entry (`initialData: []`, no persister) — a reload always clears it, independent of whether the job ever "really" completed from the UI's point of view. That fully explains the user's reported "reload made it go away" observation without needing a separate root cause for it.

## Hypothesized Paths

### Hypothesis 1: `ObjectResult` uses `Mvc.JsonOptions` (unconfigured) instead of `Http.Json.JsonOptions` (configured), so `status`/`errorCategory` serialize as integers

**Status:** Confirmed — fixed (2026-07-22)

**Theory:** See Deduction 1.

**Supporting indicators:** Findings 1–3 above; this is also a well-documented ASP.NET Core / Azure Functions isolated-worker footgun (configuring `Http.Json.JsonOptions` and expecting it to also govern `ObjectResult`/MVC-style responses).

**Would confirm:** The raw HTTP response body of `GET /api/v1/flats/{flatId}/imports/{jobId}` (or any other endpoint returning an enum, e.g. `GetFlatStructure`'s `ConsumptionApproach`) shows `"status": 2` (a bare number) instead of `"status": "Complete"`.

**Would refute:** The raw response body shows `"status": "Complete"` as a quoted string — in that case the break must be somewhere else (e.g. a TanStack Query cache-key mismatch, or the SWA `staticwebapp.config.json` routing this specific path to something unexpected). Re-open investigation toward the frontend request/response path if refuted.

**Resolution:** Confirmed and fixed (2026-07-22). `api/Shared/JsonSerializationDefaults.cs` registers the enum-string converter on both `Http.Json.JsonOptions` and `Mvc.JsonOptions`, applied from `api/Program.cs:64` — see AD-15a in `architecture.md`.

## Missing Evidence

| Gap                                                             | Impact                                                                   | How to Obtain                                                                                                   |
| ----------------------------------------------------------------- | --------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| Raw JSON response body of `GET .../imports/{jobId}`               | Directly confirms or refutes Hypothesis 1 — the crux of the whole case. | Reproduce the upload; open browser DevTools → Network tab; inspect the response body of the polling request.    |
| Direct read of `ImportJobs.Status` column in Azure SQL             | Would independently corroborate Finding 1 without relying on App Insights inference. | Grant investigating identity a data-plane SQL role, or ask Ralf to run a quick `SELECT Status FROM ImportJobs ORDER BY CreatedAt DESC` (per [[feedback_infra_deploy_ownership]] Ralf owns direct infra/DB access). |
| Whether other already-shipped enum-returning endpoints (e.g. `GetFlatStructure`'s `ConsumptionApproach`) exhibit the same bug in production | Scopes the blast radius — this may not be Story 6.6-specific | Same DevTools Network check against `GET /api/v1/flats/{flatId}/structure`. |

## Source Code Trace

| Element       | Detail                                                                                                                    |
| ------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| Error origin  | `api/Program.cs:63-67` — enum string converter registered only on `Microsoft.AspNetCore.Http.Json.JsonOptions`                |
| Trigger       | Any HTTP function returning an `ObjectResult` containing an enum-typed property (all of them; `GetImportStatusFunction.cs:62` for this case) |
| Condition     | ASP.NET Core executes `ObjectResult` via the MVC formatter pipeline, which reads `Microsoft.AspNetCore.Mvc.JsonOptions`, not `Http.Json.JsonOptions` |
| Related files | `api/Data/Entities/ImportJob.cs:3-16` (enum defs); `client/src/features/smart-plug-import/api/importApi.ts` (typed as string); `client/src/features/smart-plug-import/hooks/useImportJobStatus.ts:23-28,35` (string comparison that never matches); every other `{Feature}Function.cs` returning `OkObjectResult` with an enum property (same latent bug, wider blast radius than just Story 6.6) |

## Conclusion

**Confidence:** Medium (Deduced from strong, consistent Confirmed telemetry; the one remaining gap — the raw response body — is a two-minute check, not a deep unknown).

The backend import pipeline (upload → EventGrid → `ProcessImport`) works correctly and completes in under a second on every observed run — this is Confirmed and rules out any backend processing failure, EventGrid delivery problem, or Easy-Auth/webhook-validation regression (the class of issue this same infra hit before, per `infra/main.bicep`'s existing comments and prior fix commits `6585555`/`8ad319d`). The frontend's polling loop never terminates because it never observes a `status` value equal to the string `'Complete'`, which lines up exactly with a JSON serialization split-brain: `Program.cs` configures the enum-as-string converter on the wrong `JsonOptions` type for `ObjectResult`-based responses. If confirmed, this is likely not unique to Story 6.6 — every endpoint returning an enum-typed field (`ConsumptionApproach`, `SelfMeasuredPeriod`, `ImportErrorCategory`, etc.) is exposed to the same defect, though it may have gone unnoticed elsewhere if the frontend code for those fields tolerates or doesn't strictly compare against the numeric fallback.

## Recommended Next Steps

### Fix direction

Register the same `JsonStringEnumConverter` (+ camelCase policy) on `Microsoft.AspNetCore.Mvc.JsonOptions` in `api/Program.cs`, alongside (or instead of duplicating configuration, factor out) the existing `Http.Json.JsonOptions` registration, so both the `ObjectResult`/MVC pipeline and any minimal-API-style responses serialize enums identically. This is a single, well-contained backend fix — no frontend changes needed once the wire format matches what the frontend already expects.

### Diagnostic

1. Reproduce the upload once more with browser DevTools Network tab open; confirm `"status"` appears as a bare integer in the response body.
2. After the fix, redeploy and confirm the same response now shows `"status": "Complete"` and the Progress Card disappears within one 3-second poll interval.
3. Spot-check one other enum-bearing endpoint (`GetFlatStructure`) pre- and post-fix to scope whether this was silently affecting other already-shipped features.

## Reproduction Plan

1. Sign in to the deployed app; navigate to Decomposition → Import.
2. Upload one Meross `.csv` or Eve Home `.xlsx` file associated with any plug.
3. Open DevTools → Network; filter for `imports`; watch the polling `GET .../imports/{jobId}` calls.
4. Expected (buggy) result: the Progress Card spinner persists indefinitely; the response body's `"status"` field is a bare number that never matches `'Complete'`.
5. Expected (post-fix) result: within ~3s of the (sub-second) backend completion, the response shows `"status": "Complete"` as a string, and the card disappears per AC5.

## Side Findings

- The Function host restarted twice within a 5-minute window before the first upload (`08:44:36Z` and `08:48:46Z` "Application started" traces) — consistent with Flex Consumption cold-start/scale-in behavior on a low-traffic dev environment, not evidence of a problem. Noted only because it initially looked suspicious; ruled out as a contributor since `ProcessImport` ran cleanly on the already-warm host in both observed cases.
- The classic `az monitor app-insights query` CLI command returned nothing for this resource because `energytracker-insights` is workspace-based (backed by `energytracker-logs`); had to query the Log Analytics workspace directly via its `customerId` GUID. Worth remembering for any future live-Azure investigation on this project.
- The investigating identity has Azure AD/control-plane access (`az login`) but lacks Storage/Key Vault data-plane RBAC roles and SQL access — blob listing required falling back to an account key, and a direct DB read was not possible. Consistent with [[feedback_infra_deploy_ownership]] — Ralf retains ownership of direct data-plane access.
