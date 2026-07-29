---
baseline_commit: 457ff51b4c09b43a3c479bda7b41e8b866171b6f
---

# Story 11.4: `PatchFlatFunction` — Malformed `name` Field Returns 400, Not 500

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer integrating with this API,
I want a wrong-typed `name` field in a PATCH request to return a clear validation error,
so that a malformed request never surfaces as an unhandled server error.

## Acceptance Criteria

1. **Given** `PatchFlatFunction.cs:59`'s `Name: obj["name"]?.GetValue<string>()` — confirmed the only unguarded `GetValue<T>()` call in the entire `api/Features/` tree (every other field on every other PATCH endpoint in this codebase uses the guarded `is JsonValue ... && TryGetValue<T>(...)` pattern) — a request body like `{"name": 123}` throws an uncaught `InvalidOperationException` inside `GetValue<string>()`, propagating as an unhandled 500 rather than the 400 Problem Details response every other malformed-field case on this same endpoint returns, **when** implemented, **then** the `name` field is read using the same guarded `is JsonValue nameVal && nameVal.TryGetValue<string>(out var name)` pattern already used for every other field in this file, returning `400` with `detail: "name must be a string."` on a type mismatch.
2. **Given** the fix, **when** tested, **then** a new test in `PatchFlatFunctionTests.cs` submits `{"name": 123}` and asserts a 400 Problem Details response (not a 500), alongside the existing valid-string and omitted-field cases continuing to pass unmodified.

## Tasks / Subtasks

- [x] Task 1: Replace the unguarded `GetValue<string>()` call with the codebase's established guarded pattern (AC: #1)
  - [x] 1.1 In `api/Features/Flats/PatchFlatFunction.cs`, replace line 59's `Name: obj["name"]?.GetValue<string>(),` (inside the `PatchFlatRequest` constructor call at lines 58-64) with a guarded read placed *before* the `request` object is constructed, exactly mirroring the `annualKwhBaseline`/`plannedAnnualSpend` blocks immediately above it in the same file (lines 38-52):
    ```csharp
    string? name = null;
    if (obj["name"] is JsonValue nameVal && nameVal.TryGetValue<string>(out var n))
        name = n;
    else if (obj.ContainsKey("name") && obj["name"] is not null)
        return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "name must be a string." });
    ```
    Then change the constructor call to `Name: name,` in place of the old expression.
  - [x] 1.2 Do not change anything else in the file — `flat.Name = request.Name.Trim()` at line 73, the `PatchFlatValidator`'s blank/whitespace/max-length rule for `Name`, and the `PatchFlatRequest`/`FlatModels.cs` record shape (`Name` stays `string?`) are all unaffected and must remain exactly as they are today.
  - [x] 1.3 Verify the omitted-name and explicit-`null`-name behaviors are unchanged after the edit: both `obj["name"]` returning C# `null` (value omitted from the JSON body) and `obj["name"]` returning C# `null` because the JSON value is the literal `null` (key present, value null) must still result in `name` staying `null` → no update to `flat.Name` — this is the exact same distinction the file already relies on for `annualKwhBaseline` at lines 41-44 (`obj.ContainsKey(...)` true but `obj["..."]` is `null` for an explicit JSON `null`).
- [x] Task 2: Test coverage (AC: #2)
  - [x] 2.1 In `api.Tests/Features/Flats/PatchFlatFunctionTests.cs`, add a new test `RunAsync_NameNotAString_Returns400BadRequest`, modeled directly on the existing `RunAsync_AnnualKwhBaselineNotANumber_Returns400BadRequest` (lines 275-287 in the current file): seed a flat, submit `MakeRequest("""{"name":123}""")`, assert the result `ShouldBeOfType<BadRequestObjectResult>()` with `StatusCode == 400` (not a thrown exception/500), and assert via a fresh query that `persisted.Name` is unchanged (`"Original Name"`).
  - [x] 2.2 Confirm the existing `RunAsync_ValidNamePatch_Returns200AndUpdatesName` (lines 117-132) and every other existing test in this file that omits or sets `name` continue to pass unmodified — no existing test asserts on the old unguarded-`GetValue` behavior, so none should need editing.
  - [x] 2.3 Run the full backend suite (`dotnet test` from `api.Tests/` — this repo has no root-level `.sln`, so run it from the test project directory per Story 11.3's precedent) and confirm all existing tests still pass.

### Review Findings

- [x] [Review][Patch] New regression test asserts only the 400 status code, never the `detail` message body — `"name must be a string."` could regress silently [api.Tests/Features/Flats/PatchFlatFunctionTests.cs:289-301]
- [x] [Review][Defer] Only one non-string shape (`number`) is exercised for the `name` type-mismatch guard; boolean/array/object values are untested [api.Tests/Features/Flats/PatchFlatFunctionTests.cs:289] — deferred, pre-existing test-coverage pattern (matches this file's existing one-shape-per-field-guard convention)
- [x] [Review][Defer] Validation-order interaction between the `rowVersion` guard and the new `name` guard is untested (which 400 wins if both are invalid) [api/Features/Flats/PatchFlatFunction.cs:56-62] — deferred, pre-existing ordering unchanged by this diff

## Dev Notes

### Why this story exists

`PatchFlatFunction.cs` handles four optional fields (`name`, `annualKwhBaseline`, `plannedAnnualSpend`, `rowVersion`). Three of the four already use the guarded `is JsonValue ... && TryGetValue<T>(out var x)` pattern that safely handles a JSON value of the wrong type by falling through to an explicit 400 branch. `name` is the sole exception: `obj["name"]?.GetValue<string>()` calls `GetValue<T>()` directly, which throws an uncaught `InvalidOperationException` when the underlying JSON value cannot be converted to the requested type (e.g. a JSON number). Confirmed via direct inspection: this is the only unguarded `GetValue<T>()` call across the entire `api/Features/` tree.

### Current state of `PatchFlatFunction.cs` (90 lines today)

- Lines 38-46: `annualKwhBaseline` guarded read — `if (obj["annualKwhBaseline"] is JsonValue kwhVal && kwhVal.TryGetValue<decimal>(out var kwh)) kwhBaseline = kwh; else if (obj.ContainsKey(...)) { ... return 400 branches ... }`. Note this field additionally rejects an explicit JSON `null` with its own message ("cannot be cleared — it is a required field") because it's non-nullable at the DB level — `name` has no such restriction and does not need this extra branch; a `null` `name` should keep silently no-opping, matching current (pre-fix) behavior.
- Lines 48-52: `plannedAnnualSpend` guarded read — simpler shape, no special explicit-null handling (it's nullable/clearable), directly analogous to what `name` needs.
- Line 54-56: `rowVersion` guarded string read, structurally identical to the pattern this story adds for `name`.
- Line 58-64: the `PatchFlatRequest` constructor call — currently line 59 is the only unguarded field: `Name: obj["name"]?.GetValue<string>()`.
- Line 66-71: FluentValidation call — `PatchFlatValidator` already validates `Name` for blank/whitespace and max length `.When(r => r.Name is not null)` (`PatchFlatValidator.cs:10-13`) — this is unaffected by this story; the bug is purely in *reading* the raw JSON before validation ever runs.
- Line 73: `if (request.Name is not null) flat.Name = request.Name.Trim();` — unaffected.

### The exact guarded-string pattern to copy

`PatchTariffFunction.cs:74-77` is the closest analog — a nullable *string* field (`providerName`) using the guarded pattern with its own type-mismatch message:
```csharp
string? providerName = null;
if (obj["providerName"] is JsonValue providerVal && providerVal.TryGetValue<string>(out var provider))
    providerName = provider;
else if (obj.ContainsKey("providerName") && obj["providerName"] is not null)
    return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "providerName must be a string or null." });
```
`UpdateUserSettingsFunction.cs:46` (`locale`) is a second precedent for the same shape. This story's fix for `name` mirrors this exactly, with the message `"name must be a string."` (no "or null" suffix, per AC #1's literal wording — `name` being cleared to `null` is not itself an error case being introduced or changed here, it's already a silent no-op both before and after this fix).

### What NOT to touch

- `PatchFlatValidator.cs` — no change; it already validates `Name` correctly once a valid string reaches it.
- `FlatModels.cs`'s `PatchFlatRequest` record shape — `Name` stays `string?`, no change.
- The `annualKwhBaseline`/`plannedAnnualSpend`/`rowVersion` guarded blocks already in the file — untouched, this story only fixes the `name` field.
- `flat.Name = request.Name.Trim()` at line 73 — untouched.

### Testing Rules (from project context)

- xUnit + Shouldly (`.ShouldBe(...)`, `.ShouldBeOfType<...>()`), matching every existing test in `PatchFlatFunctionTests.cs`.
- Test placement: extend the existing `api.Tests/Features/Flats/PatchFlatFunctionTests.cs` — do not create a new file (this codebase's convention per `project-context.md`: "Test placement: `api.Tests/Features/{Feature}/{Class}Tests.cs` — mirrors `api/Features/{Feature}/`").
- `EF Core InMemory` provider is already in use in this file via `MakeDb()` — no new test infrastructure is needed; this bug is purely an application-layer JSON-parsing defect, not a DB-constraint scenario, so no test-double `AppDbContext` subclass (like `ConcurrencyConflictDbContext` in this same file) is needed here.
- Query by role/label/text is a frontend rule — not applicable to this backend-only story.

### Previous Story Intelligence (Story 11.3)

- Story 11.3 (immediately preceding this one in Epic 11) confirmed `dotnet test` should be run from `api.Tests/` since no root-level `.sln` exists in this repo — do the same here.
- Story 11.3's review found value in explicitly re-confirming existing, adjacent tests still pass unmodified after a targeted fix (rather than assuming it) — apply the same discipline in Task 2.2/2.3 here, even though this story's change is much smaller in scope.
- No git-history pattern beyond the above is directly relevant — this story is a narrow, self-contained one-file-plus-one-test-file fix, unlike 11.3's multi-file schema change.

### Project Structure Notes

- Exactly two files touched: `api/Features/Flats/PatchFlatFunction.cs` (modified) and `api.Tests/Features/Flats/PatchFlatFunctionTests.cs` (modified). No new files, no migration, no schema change.
- Matches this codebase's established guarded-JSON-field-read convention already used by every other field in this same file and by `PatchTariffFunction.cs`/`UpdateUserSettingsFunction.cs` elsewhere — this story brings `PatchFlatFunction.cs`'s `name` field into line with that convention, closing the last outlier.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md#Story 11.4] — epic-level AC and rationale
- [Source: api/Features/Flats/PatchFlatFunction.cs:38-64] — file to modify; the `annualKwhBaseline`/`plannedAnnualSpend`/`rowVersion` guarded blocks are the in-file precedent to mirror structurally
- [Source: api/Features/Flats/PatchFlatValidator.cs, api/Features/Flats/FlatModels.cs] — unaffected by this story, confirmed no changes needed
- [Source: api/Features/Tariffs/PatchTariffFunction.cs:74-77] — closest cross-file precedent for a guarded nullable-string field with its own type-mismatch message
- [Source: api/Features/Settings/UpdateUserSettingsFunction.cs:46] — second cross-file precedent for the same guarded-string shape
- [Source: api.Tests/Features/Flats/PatchFlatFunctionTests.cs:275-287] — `RunAsync_AnnualKwhBaselineNotANumber_Returns400BadRequest`, the direct in-file test pattern to mirror for the new `name`-type-mismatch test
- [Source: _bmad-output/implementation-artifacts/11-3-enforce-unique-plugid-across-power-points.md] — previous story; source of the `dotnet test` from `api.Tests/` note and the "re-confirm adjacent tests still pass" discipline

## Dev Agent Record

### Agent Model Used

claude-sonnet-5

### Debug Log References

- Confirmed the AC #1 premise directly: `dotnet test --filter RunAsync_NameNotAString_Returns400BadRequest` against the unfixed code threw `System.InvalidOperationException: An element of type 'Number' cannot be converted to a 'System.String'.` at `PatchFlatFunction.cs:58` (`JsonValueOfElement.GetValue<T>()`), matching the story's described 500 exactly.
- Note: the new test must include a valid `rowVersion` in its request body. Without it, `rowVersion` validation (lines 54-56) returns its own 400 *before* the `name` field is ever read at line 58-59 (unlike `annualKwhBaseline`, whose own guard fires earlier in the file, before the `rowVersion` check) — an initial version of the test omitting `rowVersion` passed vacuously against the unfixed code, without ever exercising the bug. Added `rowVersion` to the test body to ensure it actually reaches the vulnerable code path.

### Completion Notes List

- Replaced the unguarded `obj["name"]?.GetValue<string>()` call in `PatchFlatFunction.cs` with the guarded `is JsonValue nameVal && nameVal.TryGetValue<string>(out var n)` pattern, mirroring the `plannedAnnualSpend`/`providerName` precedent — a JSON non-string `name` now returns `400` with `detail: "name must be a string."` instead of throwing an unhandled `InvalidOperationException` (500).
- Omitted-name and explicit-JSON-`null`-name behavior is unchanged: both still leave `name` as C# `null`, so `flat.Name` is not updated.
- Added `RunAsync_NameNotAString_Returns400BadRequest` to `PatchFlatFunctionTests.cs`, verified it reproduces the original 500 against the pre-fix code, then verified it passes (400, no persistence change) after the fix.
- Full backend suite (`dotnet test` from `api.Tests/`): 479/479 passed, no regressions.

### File List

- `api/Features/Flats/PatchFlatFunction.cs` (modified)
- `api.Tests/Features/Flats/PatchFlatFunctionTests.cs` (modified)

## Change Log

- 2026-07-29: Story 11.4 implemented — `PatchFlatFunction.cs`'s `name` field now uses the guarded `is JsonValue ... && TryGetValue<string>(...)` pattern already used by every other field in the file, returning `400`/`"name must be a string."` on a type mismatch instead of throwing an unhandled `InvalidOperationException` (500). Added regression test `RunAsync_NameNotAString_Returns400BadRequest`.
