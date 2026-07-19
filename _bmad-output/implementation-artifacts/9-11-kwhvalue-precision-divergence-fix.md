---
baseline_commit: 395a4b28d8b077653339f545d4adde7dda19ed36
---

# Story 9.11: Regression Test — KwhValue Precision Rejection

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the team maintaining this app,
I want a regression test proving a >4-decimal-place `kwhValue` reading is rejected rather than silently accepted and rounded,
so that this correctness property can't silently regress if `ReadingValidator.cs` is ever touched.

## Rescope Note — Read Before Starting

**This is a test-only story. No production code changes are expected.**

The item that originally created this story (`deferred-work.md:153`, from the Story 3.1 code review) claimed: *"`KwhValue` with >4 decimal places: response reflects pre-save in-memory value; DB stores `decimal(18,4)` rounded value — EF does not refresh the entity after `SaveChangesAsync`."*

**Verified false during Epic 9 planning (2026-07-18)** and re-confirmed while writing this story (2026-07-19): `SubmitReadingFunction.cs:64` calls `validator.ValidateAsync(request, ct)` and returns 400 **before** `db.MeterReadings.Add(reading)` (line 85) or `SaveChangesAsync` (line 86) are ever reached. `ReadingValidator.cs:10-13` already has:

```csharp
RuleFor(r => r.KwhValue).GreaterThan(0m)
    .WithMessage("kwhValue must be greater than 0.")
    .DecimalPrecision(4)
    .WithMessage("kwhValue must have at most 4 decimal places.");
```

`.DecimalPrecision(4)` (from `api/Shared/DecimalPrecisionValidatorExtensions.cs`, added by Story 9.7) wraps FluentValidation's `.PrecisionScale(18, 4, true)`, matching `MeterReadingConfiguration.cs:15`'s `decimal(18,4)` column exactly. A reading with more than 4 non-trailing-zero decimal places cannot reach `SaveChangesAsync` — no response/persisted-value divergence can occur. **Do not attempt to "fix" a divergence bug — there isn't one.** This story's only job is closing the test-coverage gap.

## Critical Finding — An Overlapping Test Already Exists

`api.Tests/Features/Readings/SubmitReadingTests.cs:142-155` already has:

```csharp
[Fact]
public async Task RunAsync_KwhValueExceedsFourDecimalPlaces_Returns400()
{
    var (flat, db) = await SeedFlatAsync();
    var fn = new SubmitReadingFunction(db, new ReadingValidator());
    var req = MakeRequest(new { kwhValue = 123.56789m, readingDate = DateTimeOffset.UtcNow });
    var ctx = MakeFunctionContext();

    var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

    result.ShouldBeOfType<BadRequestObjectResult>();
    var readings = await db.MeterReadings.CountAsync();
    readings.ShouldBe(0);
}
```

This already proves "400 + no row written" for a >4-decimal-place value. **The one thing it does not assert is the response's `detail` message text**, which is what this story's AC actually requires ("the existing 'kwhValue must have at most 4 decimal places' message"). Do not write a second, near-duplicate test that only re-proves 400 + zero rows — that adds no regression-detection value beyond what already exists. Instead, **add the message assertion to close the real gap**, using the reflection-based Problem Details pattern already established elsewhere in this test file's sibling suite (see Dev Notes below). Whether you extend the existing test in place or add a new one asserting the message is your call — either satisfies the AC — but do not leave the message unasserted.

## Acceptance Criteria

1. **Given** `ReadingValidator.cs`'s existing `.DecimalPrecision(4)` rule on `KwhValue`, **when** a `SubmitReadingFunction` request is made with a `KwhValue` carrying more than 4 non-trailing-zero decimal places (e.g. `12.34567`), **then** the response is 400 Problem Details with the existing `"kwhValue must have at most 4 decimal places."` message in its `detail` field, and no `MeterReading` row is written — verified by a test in `api.Tests/Features/Readings/SubmitReadingTests.cs`.

## Tasks / Subtasks

- [x] Task 1: Close the message-assertion gap in `SubmitReadingTests.cs` (AC: #1)
  - [x] Decide: extend `RunAsync_KwhValueExceedsFourDecimalPlaces_Returns400` in place, or add a new fact — either is acceptable, just don't duplicate the existing 400+zero-rows assertions without adding the message check.
  - [x] Cast the `BadRequestObjectResult.Value` and read its `detail` property via reflection, exactly like `PatchFlatFunctionTests.cs:180`: `var detail = (string)badRequest.Value!.GetType().GetProperty("detail")!.GetValue(badRequest.Value)!;`
  - [x] Assert `detail.ShouldBe("kwhValue must have at most 4 decimal places.")` — copy the exact string from `ReadingValidator.cs:13`, including the trailing period.
  - [x] Keep (or add) the existing assertions: `result.ShouldBeOfType<BadRequestObjectResult>()` and `(await db.MeterReadings.CountAsync()).ShouldBe(0)`.
  - [x] Optionally use the AC's own example value `12.34567m` if writing a new fact, for a request body distinct from the pre-existing `123.56789m` case. (Extended the existing fact in place rather than duplicating; kept its original `123.56789m` value — no need for a second value.)
- [x] Task 2: Verify the full backend test suite is green (AC: #1)
  - [x] Run `dotnet test` from the `api.Tests` directory (or repo root) and confirm all tests pass, including the modified/new fact.

### Review Findings

- [x] [Review][Defer→Fixed] Add boundary-adjacent precision test cases [api.Tests/Features/Readings/SubmitReadingTests.cs] — addressed post-review: added `RunAsync_KwhValueWithExactlyFourSignificantDecimalPlaces_Succeeds` (123.4567m, exactly 4 significant decimals, expects 201) and `RunAsync_NegativeKwhValueWithExcessDecimalPlaces_Returns400WithBothMessages` (-1.23456m, expects 400 with both `GreaterThan`/`DecimalPrecision` messages joined). Culture-specific decimal separators were not added as a case — this function deserializes the request body via `System.Text.Json`, which parses JSON numbers into `decimal` using invariant culture regardless of server locale; there is no culture-sensitive parsing path here to test.
- [x] [Review][Defer→Fixed] Assert the full Problem Details payload shape [api.Tests/Features/Readings/SubmitReadingTests.cs:153] — addressed post-review: extended `RunAsync_KwhValueExceedsFourDecimalPlaces_Returns400` to also assert `title.ShouldBe("Validation Error")` and `status.ShouldBe(400)` alongside the existing `detail` assertion. Note: `SubmitReadingFunction.cs`'s validation-error response only has `{ title, status, detail }` — no `type` field (unlike some other Functions in this codebase, e.g. `CompleteOnboardingFunction.cs`) — so `type` was not asserted since it doesn't exist on this response.

## Dev Notes

- **No production code changes.** `ReadingValidator.cs`, `SubmitReadingFunction.cs`, and `MeterReadingConfiguration.cs` are correct as-is and must not be modified by this story.
- **Reflection pattern for Problem Details assertions** — this codebase's anonymous-object error responses (`{ title, status, detail }`) have no typed Problem Details class (by design — Critical Don't-Miss Rule #6/#9 equivalent), so tests read `detail` via `GetType().GetProperty(...)`. Copy `PatchFlatFunctionTests.cs:180` verbatim rather than inventing a new assertion style.
- **Test placement**: `api.Tests/Features/Readings/SubmitReadingTests.cs` — this file already exists with the full fixture helpers (`MakeDb`, `MakeFunctionContext`, `SeedFlatAsync`, `MakeRequest`); do not duplicate these helpers, add the new/modified fact alongside the existing ones (test file's existing fact at lines 142-155 is the one to touch or sit beside).
- **Backend test framework**: xUnit + EF Core `InMemory` provider + Shouldly assertions + Moq for `FunctionContext` — matches every other test in this file; no new package or pattern needed.
- **Functions are tested by calling `RunAsync` directly** with a mock `AppDbContext` (InMemory) + `FunctionContext` — never spin up an actual HTTP pipeline, per this project's established testing convention.

### Project Structure Notes

- Single file touched: `api.Tests/Features/Readings/SubmitReadingTests.cs`. No new files, no new folders, no migration, no API contract change.
- Fully aligned with existing project structure — `api.Tests/Features/{Feature}/{Class}Tests.cs` mirrors `api/Features/{Feature}/`, already followed by this file.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-9-unified-save-affordance-fix-and-pre-epic-10-hardening.md#Story 9.11] — canonical AC and rescope note for this story.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:153] — original (now-stale) item that promoted this story from the Story 3.1 review; the claimed divergence bug does not exist.
- [Source: api/Features/Readings/ReadingValidator.cs] — current validator; `.DecimalPrecision(4)` rule and exact error message text.
- [Source: api/Features/Readings/SubmitReadingFunction.cs] — confirms validation runs before `SaveChangesAsync` (lines 64 vs. 85-86).
- [Source: api/Data/Configurations/MeterReadingConfiguration.cs:15] — `decimal(18,4)` column matching the validator's precision/scale.
- [Source: api.Tests/Features/Readings/SubmitReadingTests.cs:142-155] — the pre-existing overlapping test this story must extend or sit beside, not duplicate.
- [Source: api.Tests/Features/Flats/PatchFlatFunctionTests.cs:180] — the exact reflection-based pattern for asserting an anonymous Problem Details `detail` message.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet test api.Tests --filter "FullyQualifiedName~SubmitReadingTests"` — 11/11 passed after adding the `detail` message assertion.
- `dotnet test api.Tests` (full suite) — 380/380 passed, no regressions.

### Completion Notes List

- Confirmed the rescope note's premise: `SubmitReadingFunction.cs` validates before ever calling `SaveChangesAsync`, so no production code was touched — this story is test-only, as scoped.
- Rather than adding a near-duplicate fact, extended the existing `RunAsync_KwhValueExceedsFourDecimalPlaces_Returns400` test with a `detail` message assertion, using the exact reflection pattern already established in `PatchFlatFunctionTests.cs:180`. Kept the test's original `123.56789m` value since a second distinct value would add no coverage the AC requires.
- Full backend suite green (380/380), no regressions introduced.

### File List

- `api.Tests/Features/Readings/SubmitReadingTests.cs` (modified)

## Change Log

- 2026-07-19: Extended `RunAsync_KwhValueExceedsFourDecimalPlaces_Returns400` with an assertion on the 400 response's `detail` message text, closing the test-coverage gap identified by this story. No production code changed.
