---
baseline_commit: df5a8346dc526f72bc812dda59df9f6a588f2433
---

# Story 11.5: RFC 9457 `type` Field Consistency Sweep

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer integrating with this API,
I want every error response to include the `type` field RFC 9457 requires,
so that clients get a consistent, spec-compliant Problem Details shape regardless of which endpoint failed.

## Acceptance Criteria

1. **Given** the 15 Functions currently missing `type` on any error response (`TriggerInsightsFunction`, `GetInsightsFunction`, `GetTariffsFunction`, `UpdateFlatStructureFunction`, `GetFlatStructureFunction`, `GetReadingHistoryFunction`, `GetDashboardFunction`, `SubmitReadingFunction`, `PatchReadingFunction`, `UploadFunction`, `GetImportStatusFunction`, `DeleteFlatFunction`, `PatchFlatFunction`, `CreateFlatFunction`, `GetDecompositionFunction`) plus `PatchTariffFunction`'s inconsistent branches, and **plus two additional gaps discovered during this story's creation** — the epic names three Functions as fully "compliant," but direct inspection found only `CompleteOnboardingFunction` actually is: `UpdateUserSettingsFunction.cs`'s 403 branch (lines 94-99) is missing `type`, and `CreateTariffFunction.cs` is missing `type` on **five** of its seven branches (its four 400 branches at L28-31/50-53/58-61/68-71, and its 403 branch at L36-40 — only its two 409 branches at L80/104 actually have `type`) — **when** implemented, **then** every Problem Details error response across all of these Functions (16 files from the epic list + `UpdateUserSettingsFunction.cs` + `CreateTariffFunction.cs` = 18 files total) includes a `type` field using the existing convention already present in `CompleteOnboardingFunction` and the compliant branches of the others (an `https://tools.ietf.org/html/rfc7231#section-6.5.1`-style URI matching the HTTP status semantics, or a domain-specific slug like `PatchTariffFunction`'s existing `"tariff-locked"` where the error is domain-specific rather than a generic HTTP status).
2. **Given** this is a pure error-shape addition, **when** implemented, **then** no existing test asserting `title`/`status`/`detail` needs to change (this is additive), and each modified Function gains or updates at least one test asserting the `type` field's presence and value on its primary error path.

## Tasks / Subtasks

- [x] Task 1: Add `type` to every non-compliant Problem Details response, using the RFC-URI table below for each status code encountered (AC: #1)
  - [x] 1.1 `api/Features/Insights/TriggerInsightsFunction.cs` — L27-30 (400), L36-40 (403), L81-85 (502)
  - [x] 1.2 `api/Features/Insights/GetInsightsFunction.cs` — L25-28 (400), L34-38 (403)
  - [x] 1.3 `api/Features/Tariffs/GetTariffsFunction.cs` — L23-26 (400), L32-36 (403)
  - [x] 1.4 `api/Features/FlatStructure/UpdateFlatStructureFunction.cs` — L29-32 (400), L37-41 (403), L50-53 (400), L58-61 (400), L65-68 (400), L75-78 (400), L85-89 (422), L132-136 (409), L140-143 (409)
  - [x] 1.5 `api/Features/FlatStructure/GetFlatStructureFunction.cs` — L23-26 (400), L32-36 (403)
  - [x] 1.6 `api/Features/Readings/GetReadingHistoryFunction.cs` — L23-26 (400), L32-36 (403)
  - [x] 1.7 `api/Features/Dashboard/GetDashboardFunction.cs` — L23-26 (400), L32-36 (403)
  - [x] 1.8 `api/Features/Readings/SubmitReadingFunction.cs` — L28-31 (400), L36-40 (403), L50-53 (400), L58-61 (400), L68-71 (400)
  - [x] 1.9 `api/Features/Readings/PatchReadingFunction.cs` — L28-31 (400), L36-40 (403), L45-48 (404), L58-61 (400), L66-69 (400), L73-76 (400), L83-86 (400), L110-114 (409)
  - [x] 1.10 `api/Features/SmartPlugImport/UploadFunction.cs` — L28-31 (400), L36-40 (403), L43-46 (400), L52-55 (400), L62-65 (400), L69-72 (400), L102-106 (503)
  - [x] 1.11 `api/Features/SmartPlugImport/GetImportStatusFunction.cs` — L24-27 (400), L32-36 (403), L39-42 (400), L48-51 (404)
  - [x] 1.12 `api/Features/Flats/DeleteFlatFunction.cs` — L23-26 (400), L31-35 (403), L43-46 (400), L51-54 (400), L68-72 (409)
  - [x] 1.13 `api/Features/Flats/PatchFlatFunction.cs` — L23 (400), L28 (403), L36 (400), L44 (400), L45 (400), L52 (400), L56 (400), L62 (400), L76 (400), L91 (409) — single-line object literals, insert `type = "...", ` as the first property on each line
  - [x] 1.14 `api/Features/Flats/CreateFlatFunction.cs` — L31-34 (400), L39-42 (400), L49-52 (400)
  - [x] 1.15 `api/Features/Decomposition/GetDecompositionFunction.cs` — L24 (400), L29 (403), L32 (400), L35 (400), L38 (400) — single-line object literals
  - [x] 1.16 `api/Features/Tariffs/PatchTariffFunction.cs` — L25-28 (400), L33-37 (403), L42-45 (404), L55-58 (400), L65 (400), L71 (400), L77 (400), L83 (400), L89 (400), L93 (400), L109-112 (400), L147-151 (409) — **do NOT touch** L122-128 (the `422`/`"tariff-locked"` branch — already compliant)
  - [x] 1.17 `api/Features/Settings/UpdateUserSettingsFunction.cs` — L94-99 (403) — the one gap not listed in the epic; the other four branches in this file (L37-44, L49-56, L66-72, L77-84) already have `type` and must not change
  - [x] 1.18 `api/Features/Tariffs/CreateTariffFunction.cs` — L28-31 (400 invalid flatId), L36-40 (403), L50-53 (400 invalid JSON), L58-61 (400 body required), L68-71 (400 validation error) — a second gap not listed in the epic (the epic cites this file as fully "compliant," but only its two 409 branches at L80 and L104 actually have `type` — **do NOT touch** those two, they're already correct)
- [x] Task 2: Test coverage — add/extend a `type`-assertion test per modified Function (AC: #2)
  - [x] 2.1 For each of the 18 files above, add or extend a test in the matching `api.Tests/Features/{Feature}/{Class}Tests.cs` file (all 18 test files already exist — confirmed, no new test files needed) asserting the `type` property's presence and exact value on that Function's primary error path, using this codebase's established reflection pattern: `var type = (string)result.Value!.GetType().GetProperty("type")!.GetValue(result.Value)!;` then `type.ShouldBe("https://tools.ietf.org/html/rfc7231#section-6.5.1")` (or the applicable URI/slug) — mirroring the existing `detail`-assertion pattern at `PatchFlatFunctionTests.cs:180` and `:301`.
  - [x] 2.2 Confirm every existing test in all 18 files that asserts on `title`/`status`/`detail` continues to pass unmodified — this is additive only, no existing assertion should need to change.
  - [x] 2.3 Run the full backend suite (`dotnet test` from `api.Tests/` — no root-level `.sln` in this repo) and confirm all existing tests plus the new ones pass with no regressions.

### Review Findings

- [x] [Review][Defer] Test coverage: 5 status codes untested across 11 production sites — deferred, pre-existing scope limit. AC #2 only required one `type` assertion per Function (satisfied everywhere), but no test anywhere in the suite verifies the emitted `type` string for 404, 409, 422, 502, or 503 branches (e.g. `UploadFunction.cs`'s 503 branch, `GetImportStatusFunction.cs`'s 404 branch, most files' 409 branches), and 14 of the 15 newly-added 403 branches also have no assertion. Real coverage gap, but expanding it is broader than this story's AC.
- [x] [Review][Defer] Reflection-based `type` assertion can throw a bare `NullReferenceException` instead of a readable failure [api.Tests/Features/Flats/PatchFlatFunctionTests.cs, api.Tests/Features/Settings/UpdateUserSettingsFunctionTests.cs:206, and 16 other test files] — deferred, pre-existing pattern reused verbatim per spec instruction (mirrors the existing `detail`-assertion pattern at `PatchFlatFunctionTests.cs:180,301`); `GetProperty("type")` returns `null` silently if the property is ever missing/renamed, and the `!` operator defers the crash to `GetValue`, so a future regression produces a confusing NRE stack trace instead of "expected type to be X, found none."
- [x] [Review][Defer] No shared `ProblemTypes` constants class — deferred, pre-existing anti-pattern extended, not introduced. The `type` URI strings are hand-typed dozens of times (58× for the 400 URI alone) across ~20 files with no centralizing constant; any future change to a URI requires a manual multi-file find/replace.
- [x] [Review][Defer] No systemic enforcement test guards the new consistency — deferred, follow-up improvement. Nothing (e.g. a reflection-based sweep over all `IActionResult` error paths) would fail automatically if a future Function ships an error response without a `type` field, so the consistency established here isn't structurally guaranteed going forward.

## Dev Notes

### RFC URI table — use these exact values for every status code below

This codebase currently has **two established conventions in tension** (400 uses the older RFC 7231 URI style; 409 uses the newer RFC 9110 URI style) — both are existing precedent and neither is being unified by this story. Use the table below to stay consistent with whichever convention is *already* established for that status code, and to establish a consistent new value for codes with no existing precedent:

| Status | `type` value | Precedent |
|---|---|---|
| 400 Bad Request | `https://tools.ietf.org/html/rfc7231#section-6.5.1` | Existing — all compliant Functions already use this exact string |
| 403 Forbidden | `https://tools.ietf.org/html/rfc7231#section-6.5.3` | **New** — no current precedent anywhere in the codebase (including `UpdateUserSettingsFunction`'s own 403 branch, per the gap this story also fixes); this sweep establishes it |
| 404 Not Found | `https://tools.ietf.org/html/rfc7231#section-6.5.4` | **New** — no current precedent |
| 409 Conflict | `https://tools.ietf.org/html/rfc9110#section-15.5.10` | Existing — `CreateTariffFunction.cs:80,104` and `CompleteOnboardingFunction.cs:42` already use this exact string; reuse it, do not switch to an RFC 7231 style for 409 |
| 422 Unprocessable Entity (generic, e.g. `UpdateFlatStructureFunction`'s Smart-Plug-assignment case) | `https://tools.ietf.org/html/rfc4918#section-11.2` | **New** — this is the standard WebDAV RFC that originally defined 422 |
| 422 Unprocessable Entity (domain-specific, `PatchTariffFunction`'s `"tariff-locked"`) | `"tariff-locked"` (unchanged) | Existing — already compliant, **do not modify this one branch** |
| 502 Bad Gateway | `https://tools.ietf.org/html/rfc7231#section-6.6.3` | **New** — no current precedent (`TriggerInsightsFunction.cs:81-85` only) |
| 503 Service Unavailable | `https://tools.ietf.org/html/rfc7231#section-6.6.4` | **New** — no current precedent (`UploadFunction.cs:102-106` only) |

### The exact code shape to apply

Multi-line object literals (most files) — add `type = "...",` as the first property:
```csharp
return new BadRequestObjectResult(new
{
    type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
    title = "Bad Request", status = 400,
    detail = "Invalid flatId format."
});
```

Single-line object literals (`PatchFlatFunction.cs`, `GetDecompositionFunction.cs`) — insert `type = "...", ` inline as the first property:
```csharp
return new BadRequestObjectResult(new { type = "https://tools.ietf.org/html/rfc7231#section-6.5.1", title = "Bad Request", status = 400, detail = "Invalid flatId format." });
```

### Full per-file branch inventory (verified 2026-07-29 by direct file inspection — this is the complete, exhaustive set; nothing else in `api/Features/` returns a Problem-Details-shaped object)

19 Functions total return Problem-Details-shaped objects. The epic cites 3 as "compliant" examples (`UpdateUserSettingsFunction`, `CreateTariffFunction`, `CompleteOnboardingFunction`), but direct inspection found only `CompleteOnboardingFunction` is actually fully compliant — `UpdateUserSettingsFunction` has one non-compliant branch (403) and `CreateTariffFunction` has five (four 400s + one 403; only its two 409 branches were actually already compliant). So 18 of the 19 Functions need at least one branch fixed: the 15 from the epic's list + `PatchTariffFunction` (all branches except its one already-compliant `422`/`tariff-locked`) + `UpdateUserSettingsFunction` (1 branch) + `CreateTariffFunction` (5 branches).

Confirmed **not** in scope (verified no Problem-Details-shaped error responses exist in these files): `GetFlatsFunction.cs`, `GetUserSettingsFunction.cs`, `ProcessInsightsFunction.cs`, `ScheduledInsightsFunction.cs`, `ProcessImportFunction.cs` (the latter three are queue/timer-triggered, not HTTP — they don't return `IActionResult` at all).

Branch counts per file (status codes present, matching Task 1's line numbers exactly): see Task 1 above for the authoritative list — every line number there was confirmed via direct `grep`/`Read` of the current file content, not inferred from the epic text.

### What NOT to touch

- `PatchTariffFunction.cs` lines 122-128 (the `422`/`"tariff-locked"` branch) — already fully compliant, has `type` today.
- `UpdateUserSettingsFunction.cs` lines 37-44, 49-56, 66-72, 77-84 — already compliant, has `type` today. Only the 403 branch at L94-99 needs the addition.
- `CreateTariffFunction.cs` lines 80 and 104 (the two 409 branches) — already compliant, has `type` today. Only the four 400 branches and the one 403 branch need the addition.
- No response's `title`, `status`, or `detail` value changes anywhere — this story adds exactly one new property (`type`) per branch, nothing else.
- No new files, no new Functions, no schema/migration changes — this is a pure additive edit to existing anonymous object literals across 18 files.
- `FlatModels.cs`, validators, or any non-error-response code path — untouched.

### Testing Rules (from project context)

- xUnit + Shouldly (`.ShouldBe(...)`, `.ShouldBeOfType<...>()`) — matches every existing test in these 18 files.
- Reflection-based property assertion is this codebase's established pattern for asserting on anonymous-object Problem Details responses (see `PatchFlatFunctionTests.cs:180`, `:301` for the existing `detail`-property precedent) — reuse this exact pattern for `type`, do not introduce a typed Problem Details class or a JSON-serialization-based assertion.
- Test placement: extend the existing `api.Tests/Features/{Feature}/{Class}Tests.cs` file for each — all 18 already exist (confirmed via direct file search), so no new test files are created by this story.
- `EF Core InMemory` provider already in use in all these test files — no new test infrastructure needed; this is an application-layer response-shape addition, not a DB-constraint scenario.

### Previous Story Intelligence (Story 11.4)

- Story 11.4 (immediately preceding this one in Epic 11) reused this codebase's guarded-JSON-field-read pattern as its point of consistency; this story's analogous point of consistency is the `type` URI table above — apply it uniformly, do not invent per-file variations.
- Story 11.4 confirmed `dotnet test` should be run from `api.Tests/` since no root-level `.sln` exists in this repo — do the same here.
- Story 11.4's review found value in explicitly re-confirming existing, adjacent tests still pass unmodified after a targeted fix, and in asserting on the actual message/value content (not just the status code) — apply both disciplines here: after adding `type`, re-run the full suite, and make sure each new test asserts the exact `type` string value, not just its presence.
- Story 11.4's dev-agent record notes an ordering gotcha: some Functions' `rowVersion` (or similarly early) validation branch fires *before* a later field's guard in the same request — when writing a new test for a `type` assertion on a specific branch, ensure the request body is otherwise fully valid up to the point the branch under test is meant to fire, or the test will vacuously hit a different 400 branch instead of the one intended.

### Git Intelligence (recent commits)

- `df5a834` (Story 11.4), `c8805f8` (Story 11.3), `21daef3`/`336495d` (Stories 11.13/11.14), `4ac3900` (Story 11.2) — all recent Epic 11 stories are narrow, single-concern backend fixes following the same shape: targeted code change + matching test-file extension + full `dotnet test` run before completion. This story follows the same shape but is broader in file count (18 files) while narrower in complexity per file (one property addition per branch).

### Project Structure Notes

- 18 files modified in `api/Features/` (16 from the epic's list, i.e. the 15 named Functions + `PatchTariffFunction.cs`'s remaining branches, plus two additional gaps found during story creation: `UpdateUserSettingsFunction.cs`'s one branch and `CreateTariffFunction.cs`'s five branches), each with a matching test file extended in `api.Tests/Features/`. No new files, no migration, no schema change.
- This story closes out the last systemic API-consistency gap named in Epic 11's own text ("noted as pre-existing, systemic in at least four separate deferred-work entries without ever being swept") — and, per the two gaps found above, goes slightly further than the epic's own audit to reach true 100% consistency.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md#Story 11.5] — epic-level AC and rationale, including the 15-Function list and `PatchTariffFunction`'s partial-compliance note
- [Source: api/Features/Tariffs/CreateTariffFunction.cs, api/Features/Onboarding/CompleteOnboardingFunction.cs, api/Features/Settings/UpdateUserSettingsFunction.cs] — the three "compliant" Functions cited by the epic; direct inspection confirms only `CompleteOnboardingFunction` actually is — `UpdateUserSettingsFunction.cs:94-99` (403 branch) and `CreateTariffFunction.cs` (four 400 branches + one 403 branch, only its two 409 branches were compliant) are both gaps discovered during this story's creation, now folded into AC #1
- [Source: api/Features/Tariffs/PatchTariffFunction.cs:122-128] — the existing domain-specific `"tariff-locked"` `type` value precedent for non-generic-HTTP-status errors
- [Source: api.Tests/Features/Flats/PatchFlatFunctionTests.cs:180,301] — the established reflection-based property-assertion pattern to reuse for the new `type` assertions
- [Source: _bmad-output/implementation-artifacts/11-4-patchflatfunction-malformed-name-field-returns-400-not-500.md] — previous story; source of the `dotnet test` from `api.Tests/` note, the "re-confirm adjacent tests still pass" discipline, and the request-body-ordering test gotcha

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- `dotnet test` from `api.Tests/` — final full-suite run: 479 passed, 0 failed, 0 skipped.

### Completion Notes List

- Added a `type` field to every non-compliant Problem Details branch across all 18 files named in the story (16 from the epic's list + `PatchTariffFunction.cs`'s remaining branches + `UpdateUserSettingsFunction.cs`'s one gap + `CreateTariffFunction.cs`'s five gaps), using the exact RFC-URI table from Dev Notes for each status code encountered.
- Verified against direct file inspection before editing each file — every branch and line-number range matched the story's inventory exactly (no drift since story creation).
- Left untouched, as instructed: `PatchTariffFunction.cs` L122-128 (`422`/`"tariff-locked"`), `UpdateUserSettingsFunction.cs` L37-44/49-56/66-72/77-84, and `CreateTariffFunction.cs`'s two 409 branches (L80, L104) — all already compliant.
- For each of the 18 Functions, extended the existing test file with a `type`-assertion on the primary error path (its `InvalidFlatIdGuid`/`InvalidFlatIdFormat`-style 400 test, or the 403 test for `UpdateUserSettingsFunction.cs` since that file's only gap was its 403 branch), using the established reflection pattern (`GetType().GetProperty("type")!.GetValue(...)`) consistent with the existing `detail`-assertion precedent in `PatchFlatFunctionTests.cs`.
- No existing `title`/`status`/`detail` assertions were modified — this was a pure additive change.
- Full backend suite (`dotnet test` from `api.Tests/`): 479 passed, 0 failed — no regressions.

### File List

- `api/Features/Insights/TriggerInsightsFunction.cs`
- `api/Features/Insights/GetInsightsFunction.cs`
- `api/Features/Tariffs/GetTariffsFunction.cs`
- `api/Features/Tariffs/PatchTariffFunction.cs`
- `api/Features/Tariffs/CreateTariffFunction.cs`
- `api/Features/FlatStructure/UpdateFlatStructureFunction.cs`
- `api/Features/FlatStructure/GetFlatStructureFunction.cs`
- `api/Features/Readings/GetReadingHistoryFunction.cs`
- `api/Features/Dashboard/GetDashboardFunction.cs`
- `api/Features/Readings/SubmitReadingFunction.cs`
- `api/Features/Readings/PatchReadingFunction.cs`
- `api/Features/SmartPlugImport/UploadFunction.cs`
- `api/Features/SmartPlugImport/GetImportStatusFunction.cs`
- `api/Features/Flats/DeleteFlatFunction.cs`
- `api/Features/Flats/PatchFlatFunction.cs`
- `api/Features/Flats/CreateFlatFunction.cs`
- `api/Features/Decomposition/GetDecompositionFunction.cs`
- `api/Features/Settings/UpdateUserSettingsFunction.cs`
- `api.Tests/Features/Insights/TriggerInsightsFunctionTests.cs`
- `api.Tests/Features/Insights/GetInsightsFunctionTests.cs`
- `api.Tests/Features/Tariffs/GetTariffsFunctionTests.cs`
- `api.Tests/Features/Tariffs/PatchTariffFunctionTests.cs`
- `api.Tests/Features/Tariffs/CreateTariffFunctionTests.cs`
- `api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs`
- `api.Tests/Features/FlatStructure/GetFlatStructureFunctionTests.cs`
- `api.Tests/Features/Readings/GetReadingHistoryFunctionTests.cs`
- `api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs`
- `api.Tests/Features/Readings/SubmitReadingTests.cs`
- `api.Tests/Features/Readings/PatchReadingFunctionTests.cs`
- `api.Tests/Features/SmartPlugImport/UploadFunctionTests.cs`
- `api.Tests/Features/SmartPlugImport/GetImportStatusFunctionTests.cs`
- `api.Tests/Features/Flats/DeleteFlatFunctionTests.cs`
- `api.Tests/Features/Flats/PatchFlatFunctionTests.cs`
- `api.Tests/Features/Flats/CreateFlatFunctionTests.cs`
- `api.Tests/Features/Decomposition/GetDecompositionFunctionTests.cs`
- `api.Tests/Features/Settings/UpdateUserSettingsFunctionTests.cs`

## Change Log

- 2026-07-29: Story 11.5 created — RFC 9457 `type` field consistency sweep across 16 Functions named in Epic 11 plus two additional gaps (`UpdateUserSettingsFunction.cs`'s 403 branch, `CreateTariffFunction.cs`'s five non-409 branches) discovered during story creation via direct file inspection.
- 2026-07-29: Story 11.5 implemented — added `type` to all non-compliant Problem Details branches across 18 files, extended each corresponding test file with a `type`-assertion, full backend suite passes (479/479, no regressions).
