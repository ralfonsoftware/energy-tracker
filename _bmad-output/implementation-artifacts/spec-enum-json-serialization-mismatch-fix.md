---
title: 'Fix enum serialization mismatch between Http.Json.JsonOptions and Mvc.JsonOptions'
type: 'bugfix'
created: '2026-07-07'
status: 'done'
context: []
baseline_commit: 'd6de771f515ae172686ed4ed94b8700ed5d85193'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `api/Program.cs` registers a `JsonStringEnumConverter` only on `Microsoft.AspNetCore.Http.Json.JsonOptions`, but every HTTP function returns `IActionResult`/`ObjectResult` (`OkObjectResult`, etc.), which ASP.NET Core serializes via the separate, unconfigured `Microsoft.AspNetCore.Mvc.JsonOptions`. Enum-typed response fields (`ImportJobStatusResponse.Status`, `ErrorCategory`, `ConsumptionApproach`, `SelfMeasuredPeriod`) therefore serialize as raw integers instead of strings. This is confirmed as the root cause of Story 6.6's Progress Card never showing "Complete" (investigation: `_bmad-output/implementation-artifacts/investigations/import-progress-card-stuck-processing-investigation.md`) — the frontend polls status as a string literal and never matches a number.

**Approach:** Extract the enum-as-string + camelCase configuration into one shared, testable method and apply it to both `Http.Json.JsonOptions` and `Mvc.JsonOptions` in `Program.cs`, so every `ObjectResult` response serializes enums identically to what the frontend already expects.

## Boundaries & Constraints

**Always:** Both JSON option objects must end up with the exact same `PropertyNamingPolicy` (camelCase) and the exact same `JsonStringEnumConverter` behavior — no divergence between the two paths going forward.

**Ask First:** None — this is a pure serialization-config fix with a single correct outcome.

**Never:** Do not add per-property `[JsonConverter]` attributes on the enums themselves (that would be a second, redundant mechanism). Do not touch the frontend — it already expects string enum values; only the backend wire format is wrong.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Enum serialized via `ObjectResult` | `ImportJobStatusResponse` with `Status = ImportStatus.Complete` | JSON body contains `"status":"Complete"` (string), not `"status":2` | N/A |
| Enum serialized via minimal-API/`Http.Json` path | Any endpoint using `Results.Json`/`WriteAsJsonAsync` with an enum | Continues to serialize as string (already worked; must not regress) | N/A |
| Nullable enum | `SelfMeasuredPeriod?` = `null` | Serializes as JSON `null`, not `0` | N/A |

</frozen-after-approval>

## Code Map

- `api/Program.cs` -- currently configures `Http.Json.JsonOptions` only (lines 63-67); add matching `Mvc.JsonOptions` configuration
- `api/Shared/JsonSerializationDefaults.cs` -- new shared helper applying the naming policy + enum converter to a `JsonSerializerOptions` instance, used by both registrations
- `api/Features/SmartPlugImport/GetImportStatusFunction.cs` -- primary reproduction target (`OkObjectResult` with `ImportJobStatusResponse.Status`)
- `api.Tests/Shared/JsonSerializationDefaultsTests.cs` -- new test file verifying the shared helper serializes enums as strings and nulls as null

## Tasks & Acceptance

**Execution:**
- [x] `api/Shared/JsonSerializationDefaults.cs` -- add a static class with one method `Apply(JsonSerializerOptions options)` that sets `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` and adds a `JsonStringEnumConverter` -- centralizes the config so both JsonOptions registrations and the test consult the exact same logic
- [x] `api/Program.cs` -- replace the inline lambda body in the existing `Configure<Http.Json.JsonOptions>` call with `JsonSerializationDefaults.Apply(options.SerializerOptions)`; add a new `builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options => JsonSerializationDefaults.Apply(options.JsonSerializerOptions))` call directly after it -- makes `ObjectResult` responses use the same enum-as-string behavior as the minimal-API path
- [x] `api.Tests/Shared/JsonSerializationDefaultsTests.cs` -- new xUnit test: build a `JsonSerializerOptions`, call `JsonSerializationDefaults.Apply`, serialize a small record containing `ImportStatus.Complete` and a `null` nullable enum, assert the JSON contains `"status":"Complete"` and the nullable field serializes as `null` -- this is the only test in the suite that exercises real JSON output (per project-context.md, existing tests call `RunAsync` directly and never serialize), so it is the sole guard against this exact class of regression recurring

**Acceptance Criteria:**
- Given `GetImportStatusFunction` returns an `OkObjectResult` wrapping `ImportJobStatusResponse` with `Status = ImportStatus.Complete`, when serialized by the app's configured JSON options, then the output contains `"status":"Complete"` (string), not a numeric value.
- Given the same fix, when any other enum-bearing `ObjectResult` response is serialized (e.g. `ConsumptionApproach`), then it also serializes as its string name — no endpoint is left on the old numeric behavior.

## Design Notes

`Microsoft.AspNetCore.Mvc.JsonOptions.JsonSerializerOptions` and `Microsoft.AspNetCore.Http.Json.JsonOptions.SerializerOptions` are two distinct configuration objects in ASP.NET Core — `ObjectResult` (MVC pipeline) reads the former, minimal-API `Results.Json`/`HttpResponse.WriteAsJsonAsync` read the latter. This codebase only had the latter configured, which is why the bug was invisible in this file alone — it looks correct until you know `ObjectResult` doesn't consult it.

## Verification

**Commands:**
- `dotnet test api.Tests --filter FullyQualifiedName~JsonSerializationDefaultsTests` -- expected: new test passes, proving enums serialize as strings and nulls stay null
- `dotnet build api` -- expected: builds clean, no new warnings

**Manual checks (if no CLI):**
- After deploying, reproduce the Story 6.6 upload once more with browser DevTools Network open; confirm the `GET .../imports/{jobId}` response body now shows `"status":"Complete"` as a string and the Progress Card disappears within one 3s poll.

## Suggested Review Order

**The fix — wiring both JsonOptions**

- The missing registration: `ObjectResult` responses read `Mvc.JsonOptions`, which was never configured — this line closes that gap.
  [`Program.cs:70`](../../api/Program.cs#L70)

- The pre-existing registration, refactored to share the same logic instead of duplicating it inline.
  [`Program.cs:63`](../../api/Program.cs#L63)

**Shared helper**

- Single source of truth for the enum-as-string + camelCase config, applied identically to both option types.
  [`JsonSerializationDefaults.cs:12`](../../api/Shared/JsonSerializationDefaults.cs#L12)

- Idempotency guard added after review — prevents duplicate converter registration if `Apply` is ever called twice on one instance.
  [`JsonSerializationDefaults.cs:16`](../../api/Shared/JsonSerializationDefaults.cs#L16)

**Tests — peripherals**

- Proves the real production enums serialize as their declared names, not just a synthetic stand-in type.
  [`JsonSerializationDefaultsTests.cs:14`](../../api.Tests/Shared/JsonSerializationDefaultsTests.cs#L14)

- Proves the actual `Mvc.JsonOptions`/`Http.Json.JsonOptions` types — wired exactly as `Program.cs` wires them — both serialize correctly, closing the gap where a unit test on the helper alone wouldn't catch a future miswiring.
  [`JsonSerializationDefaultsTests.cs:57`](../../api.Tests/Shared/JsonSerializationDefaultsTests.cs#L57)
