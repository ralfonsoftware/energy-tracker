---
baseline_commit: 7bf1685c4de005602a46a38a71e9e6e4eec1687a
---

# Story 9.9: Reject Explicit Null for AnnualKwhBaseline on PATCH

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer integrating with this API,
I want an explicit attempt to clear `annualKwhBaseline` via PATCH to return a clear error instead of silently doing nothing,
so that I'm not left guessing why my update had no effect.

## Acceptance Criteria

1. **Given** `PatchFlatFunction.cs`'s `annualKwhBaseline` extraction (`:38-42`) currently has no way to distinguish "field omitted from JSON body" (leaves existing value unchanged — correct, keep as-is) from "field explicitly present and `null`" (currently also silently leaves the value unchanged, pinned as current behavior by `PatchFlatFunctionTests.RunAsync_AnnualKwhBaselineExplicitNull_SilentlyLeavesExistingValueUnchanged`), **when** implemented, **then** an explicitly-`null` `annualKwhBaseline` returns `400 Bad Request` with Problem Details body `{ title: "Bad Request", status: 400, detail: "annualKwhBaseline cannot be cleared — it is a required field." }`, while an omitted field still silently leaves the existing value unchanged exactly as today.
2. **Given** the fix, **when** implemented, **then** `PatchFlatFunctionTests.cs`'s existing pinning test (`RunAsync_AnnualKwhBaselineExplicitNull_SilentlyLeavesExistingValueUnchanged`) is renamed and updated to assert the new `400` response instead of the silent no-op, and a new test confirms omitting the field entirely still leaves the existing value unchanged (already covered by `RunAsync_EmptyPatchBody_Returns200NoOpAndPersistsNothing`, but add an explicit test if the rename removes unique coverage for this specific field).

## Tasks / Subtasks

- [x] Task 1: Reject explicit `null` for `annualKwhBaseline` in `PatchFlatFunction.cs` (AC: 1)
  - [x] `api/Features/Flats/PatchFlatFunction.cs:38-42` currently reads:
    ```csharp
    decimal? kwhBaseline = null;
    if (obj["annualKwhBaseline"] is JsonValue kwhVal && kwhVal.TryGetValue<decimal>(out var kwh))
        kwhBaseline = kwh;
    else if (obj.ContainsKey("annualKwhBaseline") && obj["annualKwhBaseline"] is not null)
        return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "annualKwhBaseline must be a number." });
    ```
    Change to split the "key present" branch on whether the value is JSON `null` vs. present-but-wrong-type:
    ```csharp
    decimal? kwhBaseline = null;
    if (obj["annualKwhBaseline"] is JsonValue kwhVal && kwhVal.TryGetValue<decimal>(out var kwh))
        kwhBaseline = kwh;
    else if (obj.ContainsKey("annualKwhBaseline"))
    {
        if (obj["annualKwhBaseline"] is null)
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "annualKwhBaseline cannot be cleared — it is a required field." });
        return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "annualKwhBaseline must be a number." });
    }
    ```
  - [x] Why this works without touching `PatchFlatRequest`/`PatchFlatValidator.cs`/`FlatModels.cs` at all: in `System.Text.Json.Nodes`, `obj["annualKwhBaseline"]` returns a C# `null` reference when the JSON value is literally `null` (there is no `JsonValue` wrapper for JSON `null` — the indexer itself returns `null`). So `obj["annualKwhBaseline"] is JsonValue kwhVal` is already `false` for both "key absent" and "key present with JSON `null`" — the only way to tell them apart is `obj.ContainsKey(...)`, which the existing code already calls. This is the same pattern `plannedAnnualSpend` already uses two lines below (`:47-48`) for its own "not a number" case — no new field, no new request shape, purely a control-flow split inside the existing `else if`.
  - [x] Do **not** add an `AnnualKwhBaselineProvided` flag to `PatchFlatRequest` (unlike `PlannedAnnualSpendProvided`) — that field exists on `PlannedAnnualSpend` because explicit `null` is *valid* there (clears the field, since it's nullable in the DB). `AnnualKwhBaseline` is `.IsRequired()` at the DB level (`FlatConfiguration.cs`) and stays required — this story rejects the null outright at the JSON-parsing layer in the Function, before a `PatchFlatRequest` is even constructed, exactly like the existing "must be a number" case already does. No validator change, no DTO change.

- [x] Task 2: Update and add backend tests (AC: 2)
  - [x] `api.Tests/Features/Flats/PatchFlatFunctionTests.cs:149-166` — rename `RunAsync_AnnualKwhBaselineExplicitNull_SilentlyLeavesExistingValueUnchanged` to `RunAsync_AnnualKwhBaselineExplicitNull_Returns400AndLeavesExistingValueUnchanged` (or similar). Replace the body's assertions: instead of asserting `OkObjectResult` + `persisted.AnnualKwhBaseline.ShouldBe(3500m)`, assert `result.ShouldBeOfType<BadRequestObjectResult>()` and still assert `persisted.AnnualKwhBaseline.ShouldBe(3500m)` (the value must remain unchanged since the request is rejected before any mutation). Update or remove the test's existing comment (`:152-155`, which describes the *old* asymmetric behavior this story fixes) to describe the new behavior instead.
  - [x] `RunAsync_EmptyPatchBody_Returns200NoOpAndPersistsNothing` (`:168-182`) already covers "field omitted → unchanged" via an empty `{}` body, satisfying AC 2's omitted-field requirement — no new test strictly required for this half of AC 2, but consider adding a narrower `RunAsync_AnnualKwhBaselineOmitted_LeavesExistingValueUnchanged` test (mirroring `RunAsync_PlannedAnnualSpendOmitted_LeavesExistingValueUnchanged`, `:198-210`) if you want a test that isolates this field specifically rather than relying on the whole-body-empty case. Optional — not required by any AC.
  - [x] Verify the sibling test `RunAsync_AnnualKwhBaselineNotANumber_Returns400BadRequest` (`:241-253`, body `{"annualKwhBaseline":"not-a-number"}`) still passes unmodified — this exercises the "present but wrong type" branch, which this story's refactor must not change.
  - [x] Run `dotnet test api.Tests/api.Tests.csproj` — full suite must pass, with the renamed test now asserting the new 400 behavior.

### Review Findings

- [x] [Review][Patch] Renamed explicit-null test never asserts the actual `detail` message, only the response type [api.Tests/Features/Flats/PatchFlatFunctionTests.cs:150-166] — fixed: cast `badRequest.Value` via reflection and assert the exact detail string, guarding against a message swap between the null-case and wrong-type-case branches.
- [x] [Review][Patch] New omitted-field test never asserts that `Name` was actually applied, only that `AnnualKwhBaseline` is unchanged [api.Tests/Features/Flats/PatchFlatFunctionTests.cs:183-195] — fixed: added `persisted.Name.ShouldBe("Renamed Flat")`.
- [x] [Review][Defer] No test covers a combined payload (e.g. `{"name":"X","annualKwhBaseline":null}`) to confirm no partial write occurs before the 400 short-circuits [api/Features/Flats/PatchFlatFunction.cs:38-49] — deferred, not required by any AC; the fix returns before any field assignment (`:64-66`) regardless of which other fields are present, so no partial-write path exists in the current code shape.
- [x] [Review][Defer] Only `AnnualKwhBaseline` was audited for the "explicit-null-silently-ignored" defect class; other required non-nullable PATCH fields across the API were not re-audited for the same gap [api/Features/Flats/PatchFlatFunction.cs] — deferred, out of scope for this story (Story 9.9 specifically targets `annualKwhBaseline`; a broader audit would be a separate story if a similar gap is found elsewhere).

## Dev Notes

- **This is a 2-line control-flow change in one file, plus one test rename/update.** No new DTO field, no validator change, no DB/migration change, no frontend change required (see below). Do not expand scope.
- **Why no frontend change is needed:** grepped every `annualKwhBaseline` usage in `client/src/` — `usePatchFlat.ts:16` only ever sets `update.annualKwhBaseline` when `body.annualKwhBaseline !== undefined`, and `FlatBaselineEdit.tsx` (the only UI that PATCHes this field) always sends a parsed number from `parseLocaleNumber(...)`, never `null` — `settingsApi.ts`'s `PatchFlatRequest`-equivalent type already types `annualKwhBaseline` as `number` (not `number | null`) on the frontend. This story only changes behavior for a caller that explicitly sends JSON `null` for this field, which no current frontend code path does — zero UI regression risk.
- **Do not confuse this with `PlannedAnnualSpend`'s pattern.** `PlannedAnnualSpend` supports explicit-null-clears-the-field because it's genuinely optional (nullable column, `PlannedAnnualSpendProvided` flag on the DTO). `AnnualKwhBaseline` is `.IsRequired()`/non-nullable at the DB level (`FlatConfiguration.cs`) — there is no scenario where clearing it to "no baseline" is valid, so the correct fix is rejecting the null, not adding a way to persist it. Do not add a `Provided` flag or make the DB column nullable — that would be solving a different problem than this story asks for.
- **Precedent for this exact fix shape:** the "present but not a number" branch (`:41-42` today) already returns a 400 directly from the Function, before any `PatchFlatRequest`/validator involvement — this story's new "present but null" branch follows the identical pattern, just with a different message. If you find yourself touching `PatchFlatValidator.cs`, `PatchFlatRequest`, or `FlatModels.cs`, you've overcomplicated this — the fix is contained entirely within `PatchFlatFunction.cs`'s existing `if`/`else if` block.
- **Response format:** matches this project's non-negotiable Problem Details pattern — anonymous object literal (`title`, `status`, `detail`), same as every other 400 in this Function. Do not introduce a typed `ProblemDetails` class (pre-existing project-wide gap tracked separately, out of scope here).

### Existing code being modified — current state and what's preserved

- `api/Features/Flats/PatchFlatFunction.cs` — the surrounding tenant-check (`:20,27-28`), body-parsing (`:30-36`), `plannedAnnualSpend` extraction (`:44-48`), validator call (`:57-62`), and the three assignment lines (`:64-66`) are all unrelated to this story and must not change. Only the `annualKwhBaseline` extraction block (`:38-42`) is touched.
- `api/Features/Flats/PatchFlatValidator.cs` — untouched. Its `AnnualKwhBaseline` rule (`:14-18`, range + `DecimalPrecision(4)`, gated `.When(r => r.AnnualKwhBaseline is not null)`) only ever runs when a valid decimal was already extracted — this story's new 400 short-circuits before the validator is ever invoked for the null case, so this rule's behavior is unaffected.
- `api/Features/Flats/FlatModels.cs` — `PatchFlatRequest`'s shape (`Name`, `AnnualKwhBaseline`, `PlannedAnnualSpendProvided`, `PlannedAnnualSpend`) is untouched — no new field added.

### Testing Requirements Summary

- Backend: xUnit + Shouldly, direct `PatchFlatFunction.RunAsync(...)` calls with an in-memory `AppDbContext` and mocked `FunctionContext` — matches every existing test in `PatchFlatFunctionTests.cs`. Use the existing `MakeDb`/`MakeFunctionContext`/`SeedFlatAsync`/`MakeRequest` helpers (`:16-54`) — do not add new test-data builders.
- The renamed pinning test must still assert the DB value is unchanged (`persisted.AnnualKwhBaseline.ShouldBe(3500m)`) — the fix rejects the request before any mutation, so "unchanged" remains true, only the HTTP response type changes from `OkObjectResult` to `BadRequestObjectResult`.

### Project Structure Notes

- No new files. Single-file production change (`PatchFlatFunction.cs`) plus one test file update (`PatchFlatFunctionTests.cs`) — matches this story's narrow, single-slice scope (Flats feature, backend only).
- No conflicts with any `project-context.md` convention — Problem Details format, anonymous object literal, tenant-scoped access check all already followed by the surrounding unmodified code.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-9-unified-save-affordance-fix-and-pre-epic-10-hardening.md#Story 9.9] — verbatim epic AC and the rescope note explaining why a nullable-column approach (matching `PlannedAnnualSpend`) is out of scope absent a real product need.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:245] — original item ("Deferred from: code review of story-4.1") that promoted this story, pinned by `PatchFlatFunctionTests.RunAsync_AnnualKwhBaselineExplicitNull_SilentlyLeavesExistingValueUnchanged`.
- [Source: api/Features/Flats/PatchFlatFunction.cs:38-48] — the exact extraction block this story modifies, and the adjacent `plannedAnnualSpend` extraction whose "present but wrong type" pattern this story's null-check mirrors.
- [Source: api/Features/Flats/PatchFlatValidator.cs:14-18] — confirms the `AnnualKwhBaseline` FluentValidation rule only runs `.When(r => r.AnnualKwhBaseline is not null)`, unaffected by this story's Function-layer short-circuit.
- [Source: api/Features/Flats/FlatModels.cs] — current `PatchFlatRequest` shape, confirming no new field is needed.
- [Source: api.Tests/Features/Flats/PatchFlatFunctionTests.cs:149-166,241-253] — the pinning test this story updates, and the sibling "not a number" test that must keep passing unmodified.
- [Source: client/src/features/settings/hooks/usePatchFlat.ts:16, client/src/features/settings/components/FlatBaselineEdit.tsx, client/src/features/settings/api/settingsApi.ts:8] — confirms no frontend code path ever sends explicit `null` for `annualKwhBaseline`, so this backend-only change carries zero UI regression risk.
- [Source: _bmad-output/project-context.md#Error responses — Problem Details only] — anonymous object literal Problem Details pattern this story's new 400 response follows exactly.
- [Source: _bmad-output/implementation-artifacts/9-8-meter-reset-handling-in-kpi-calculations.md] — previous story (9.8); unrelated feature area (Dashboard/KpiCalculator vs. this story's Flats/PatchFlatFunction), no carryover learnings apply beyond confirming this project's xUnit/Shouldly conventions and the "verify frontend isn't broken by a backend contract change" discipline that story's own compilation-hazard note established.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet test api.Tests/api.Tests.csproj --filter "FullyQualifiedName~PatchFlatFunctionTests"` — RED: 1 failed (renamed test) / 14 passed; GREEN after fix: 15/15 passed
- `dotnet test api.Tests/api.Tests.csproj` — full suite: 365/365 passed
- `npx vitest run` (from `client/`) — full suite: 395/395 passed (backend-only change, frontend unaffected as predicted)
- Code review (2026-07-19): 3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor) — 2 patch, 2 defer, 8 dismissed. Both patches applied and verified: `dotnet test api.Tests/api.Tests.csproj --filter "FullyQualifiedName~PatchFlatFunctionTests"` 15/15 passed; full suite re-run — `dotnet test api.Tests/api.Tests.csproj` 365/365 passed.

### Completion Notes List

- Split the existing `else if (obj.ContainsKey("annualKwhBaseline") && obj["annualKwhBaseline"] is not null)` branch in `PatchFlatFunction.cs` into a nested check: key-present-and-null now returns 400 "annualKwhBaseline cannot be cleared — it is a required field", key-present-and-wrong-type still returns the original "must be a number" message. No changes to `PatchFlatRequest`, `PatchFlatValidator.cs`, or `FlatModels.cs` — exactly as scoped.
- Renamed the pinning test `RunAsync_AnnualKwhBaselineExplicitNull_SilentlyLeavesExistingValueUnchanged` → `RunAsync_AnnualKwhBaselineExplicitNull_Returns400AndLeavesExistingValueUnchanged`, flipped its assertion from `OkObjectResult` to `BadRequestObjectResult` (value still confirmed unchanged in the DB), and rewrote its comment to describe the new behavior.
- Added `RunAsync_AnnualKwhBaselineOmitted_LeavesExistingValueUnchanged` as an isolated test for the omitted-field case (mirroring `RunAsync_PlannedAnnualSpendOmitted_LeavesExistingValueUnchanged`), on top of the existing whole-body-empty coverage.
- Confirmed the sibling `RunAsync_AnnualKwhBaselineNotANumber_Returns400BadRequest` test passes unmodified — the "wrong type" branch is untouched.
- Confirmed via full frontend suite (395/395) that no UI regression occurred — matches the story's Dev Notes prediction that no frontend code path ever sends explicit `null` for this field.
- ✅ Resolved review finding [Patch]: explicit-null test only checked response type, not the actual `detail` message — added an assertion on the exact detail string via reflection, closing the risk that the null-case and wrong-type-case 400 messages could be silently swapped without any test catching it.
- ✅ Resolved review finding [Patch]: omitted-field test never confirmed `Name` was actually applied — added `persisted.Name.ShouldBe("Renamed Flat")`.

### File List

- `api/Features/Flats/PatchFlatFunction.cs` (modified)
- `api.Tests/Features/Flats/PatchFlatFunctionTests.cs` (modified)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified)

## Change Log

- 2026-07-19: Implemented explicit-null rejection for `annualKwhBaseline` on PATCH — a 2-line control-flow split in `PatchFlatFunction.cs`, no DTO/validator/DB changes. Renamed and flipped the existing pinning test, added one isolated omitted-field test. Backend (365 tests) and frontend (395 tests) suites pass with zero regressions. Status → review.
- 2026-07-19: Code review — fixed 2 patch findings (strengthened the explicit-null test to assert the exact `detail` message; strengthened the omitted-field test to assert `Name` was applied), deferred 2 minor/out-of-scope items to `deferred-work.md`, dismissed 8 as noise or explicitly matching spec/established convention. Full suite re-verified green. Status → done.
