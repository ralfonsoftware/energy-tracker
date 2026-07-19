---
baseline_commit: a20cd66441ade70aa1129b883fbd5f923600af15
---

# Story 9.7: Shared Decimal-Precision Validator Extension Method

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the team maintaining this app,
I want the decimal-precision validation rule extracted into one shared helper instead of ten hardcoded call sites,
so that a future change to the precision policy can't drift across files.

## Acceptance Criteria

1. **Given** eight validator files each hardcode `.PrecisionScale(18, N, true)` inline with no shared constant or extension method (`OnboardingValidator.cs`, `CreateFlatValidator.cs`, `PatchFlatValidator.cs`, `TariffValidator.cs`, `PatchTariffValidator.cs`, `ReadingValidator.cs`, `PatchReadingValidator.cs`, `UpdateFlatStructureValidator.cs` — 16 call sites total, confirmed by full-repo grep), **when** implemented, **then** a shared extension method (`RuleFor(...).DecimalPrecision(scale)`, defaulting `precision` to 18 since every current field uses it) replaces every inline `.PrecisionScale(18, N, true)` call, with identical validation behavior — verified by every existing test in `OnboardingValidatorTests.cs` and `PatchFlatValidatorTests.cs` (the only two validators with dedicated decimal-precision test coverage) continuing to pass unmodified. This is a pure DRY refactor, not a behavior change — no test assertions, error messages, or validation outcomes may change.

**Rescope note (2026-07-18, resolved during Epic 9 planning):** the item that originally created this story (`deferred-work.md`, pre-dating Story 6.0) claimed decimal values passing validation could still exceed their DB column's scale, causing response/persisted-value divergence. **Verified false** — Story 6.0's decimal-precision-validation-policy pass (2026-07-05/06) already applied `.PrecisionScale(18, N, true)` to every field, matching each field's DB column scale exactly (`PricePerKwh` 18,6 ↔ `decimal(18,6)`; `MonthlyBaseFee`/`KwhValue`/`AnnualKwhBaseline`/`PlannedAnnualSpend`/`EuAnnualKwh`/`SelfMeasuredKwh` 18,4 ↔ `decimal(18,4)`). Values with excess precision are already rejected with 400 before reaching the DB — no correctness bug remains. Do not attempt to "fix" a divergence bug; there isn't one. This story's only job is eliminating the 16-site duplication.

## Tasks / Subtasks

- [x] Task 1: Create the shared `DecimalPrecision` extension method (AC: 1)
  - [x] Create `api/Shared/DecimalPrecisionValidatorExtensions.cs`, following this project's existing `api/Shared/*Extensions.cs` naming/placement convention (`AppDbContextExtensions.cs`, `FunctionContextExtensions.cs` — both static extension classes in `EnergyTracker.Api.Shared`).
  - [x] Implement two overloads — one for non-nullable `decimal`, one for nullable `decimal?` — because FluentValidation 12's built-in `PrecisionScale` itself has separate non-nullable/nullable overloads, and this codebase's 16 call sites are a real mix of both (`decimal` on `TariffValidator`/`ReadingValidator`/etc.; `decimal?` on `UpdateFlatStructureValidator`'s `EuAnnualKwh`/`SelfMeasuredKwh`). Signature: `public static IRuleBuilderOptions<T, decimal> DecimalPrecision<T>(this IRuleBuilderOptions<T, decimal> ruleBuilder, int scale, int precision = 18) => ruleBuilder.PrecisionScale(precision, scale, true);` and the matching `decimal?` overload. The `true` (ignoreTrailingZeros) argument is hardcoded — every existing call site already passes `true`, and no AC or existing test requires it to vary.
  - [x] Add `using FluentValidation;` at the top (needed for `IRuleBuilderOptions<T, TProperty>` and the built-in `PrecisionScale` this wraps).

- [x] Task 2: Replace all 16 inline `.PrecisionScale(18, N, true)` call sites with `.DecimalPrecision(N)` (AC: 1)
  - [x] `api/Features/Tariffs/TariffValidator.cs:12` (`PricePerKwh`, scale 6) and `:16` (`MonthlyBaseFee`, scale 4) — add `using EnergyTracker.Api.Shared;`, replace both `.PrecisionScale(18, 6, true)` → `.DecimalPrecision(6)` and `.PrecisionScale(18, 4, true)` → `.DecimalPrecision(4)`. Leave every surrounding `.WithMessage(...)` call exactly as-is — `DecimalPrecision` returns the same `IRuleBuilderOptions<T, TProperty>`, so the existing `.WithMessage("pricePerKwh must have at most 6 decimal places.")` chained immediately after continues to work unchanged.
  - [x] `api/Features/Tariffs/PatchTariffValidator.cs:11` (`PricePerKwh`, scale 6) and `:16` (`MonthlyBaseFee`, scale 4) — same replacement pattern. Note the trailing `.When(...)` calls after `.WithMessage(...)` on both rules must stay exactly where they are (after the message, not after `.DecimalPrecision`).
  - [x] `api/Features/FlatStructure/UpdateFlatStructureValidator.cs:30` (`EuAnnualKwh`, `decimal?`, scale 4) and `:34` (`SelfMeasuredKwh`, `decimal?`, scale 4) — these two are nested inside `RuleForEach(...).ChildRules(...)` blocks three levels deep (`room` → `pp` → `d`); use the `decimal?` overload here since both properties are `decimal?` in `FlatStructureModels.cs:14-15`.
  - [x] `api/Features/Readings/ReadingValidator.cs:11` (`KwhValue`, scale 4).
  - [x] `api/Features/Readings/PatchReadingValidator.cs:11` (`KwhValue`, scale 4).
  - [x] `api/Features/Flats/PatchFlatValidator.cs:15` (`AnnualKwhBaseline`, scale 4) and `:20` (`PlannedAnnualSpend`, scale 4).
  - [x] `api/Features/Flats/CreateFlatValidator.cs:12` (`AnnualKwhBaseline`, scale 4) and `:16` (`PlannedAnnualSpend`, scale 4).
  - [x] `api/Features/Onboarding/OnboardingValidator.cs:12` (`AnnualKwhBaseline`, scale 4), `:16` (`PricePerKwh`, scale 6), `:20` (`MonthlyBaseFee`, scale 4), `:26` (`PlannedAnnualSpend`, scale 4).
  - [x] Every one of these 8 files needs `using EnergyTracker.Api.Shared;` added alongside its existing `using FluentValidation;` (and, for `UpdateFlatStructureValidator.cs`, its existing `using EnergyTracker.Api.Data.Entities;`).

- [x] Task 3: Verify no behavior change (AC: 1)
  - [x] Run `dotnet test api.Tests/api.Tests.csproj` — every test must pass unmodified, especially `OnboardingValidatorTests.cs` (10 precision-related tests: `Validate_AnnualKwhBaselineExceedsFourDecimalPlaces_Fails`, `Validate_AnnualKwhBaselineWithTrailingZerosBeyondFourDecimals_Succeeds`, `Validate_PricePerKwhExceedsSixDecimalPlaces_Fails`, `Validate_PricePerKwhWithTrailingZerosBeyondSixDecimals_Succeeds`, `Validate_MonthlyBaseFeeExceedsFourDecimalPlaces_Fails`, `Validate_MonthlyBaseFeeWithTrailingZerosBeyondFourDecimals_Succeeds`, `Validate_PlannedAnnualSpendExceedsFourDecimalPlaces_Fails`, `Validate_PlannedAnnualSpendWithTrailingZerosBeyondFourDecimals_Succeeds`, plus two more) and `PatchFlatValidatorTests.cs` (`Validate_AnnualKwhBaselineExceedsFourDecimalPlaces_Fails`, `Validate_AnnualKwhBaselineWithTrailingZerosBeyondFourDecimals_Succeeds`, `Validate_PlannedAnnualSpendExceedsFourDecimalPlaces_Fails`, `Validate_PlannedAnnualSpendWithTrailingZerosBeyondFourDecimals_Succeeds`). A single failing assertion here means the extension method's semantics diverged from the original `.PrecisionScale(18, N, true)` call — do not "fix" the test, fix the extension method.
  - [x] Do not add, remove, or rename any test in these two files — this story's only sanctioned change is production code (the 8 validator files + 1 new extension file).

### Review Findings

- [x] [Review][Defer] `DecimalPrecision`/`DecimalPrecision` (nullable overload) accept `scale`/`precision` with no bounds guard (e.g. `scale <= 0`, or `precision < scale`) — a future misuse would only surface as an opaque failure inside FluentValidation's internals. All 16 current call sites pass valid hardcoded literals, so this has zero effect on this story's behavior; adding a guard would also contradict this story's explicit "thin wrapper, not a reimplementation" constraint. `api/Shared/DecimalPrecisionValidatorExtensions.cs:7-13` — deferred, no current impact.
- [x] [Review][Defer] `DecimalPrecision` extension methods are typed against `IRuleBuilderOptions<T, TProperty>`, not `IRuleBuilder<T, TProperty>`, so they can only be chained after another rule that already returns `IRuleBuilderOptions` (e.g. `.GreaterThan(...)`) — a validator wanting precision-scale checking as the first rule in a chain won't compile. Matches how all 16 existing call sites already use it; no current call site is affected. `api/Shared/DecimalPrecisionValidatorExtensions.cs:7-13` — deferred, no current impact.

## Dev Notes

- **Scope is intentionally narrow: 1 new file + 8 edited files, zero behavior change.** No new validation rule, no new DB migration, no new API contract change. Do not expand scope to "while I'm in here" cleanups of unrelated validator code (e.g. the `.WithMessage` wording inconsistencies, or the pre-existing lower-bound-message gap noted in `deferred-work.md:242` — both out of scope, already tracked separately).
- FluentValidation 12.1.1 (`project-context.md`) already provides `PrecisionScale(int precision, int scale, bool ignoreTrailingZeros)` as a built-in extension on `IRuleBuilderOptions<T, decimal>` and `IRuleBuilderOptions<T, decimal?>` — confirmed by the 16 existing call sites already compiling and running correctly against both property types. `DecimalPrecision` is a thin wrapper, not a reimplementation — it must delegate to `PrecisionScale` internally, not duplicate its logic.
- `precision` defaults to 18 per the epic's explicit instruction ("defaulting to precision 18 since every current field uses it") — confirmed true by grep: all 16 call sites pass `18` as the first `PrecisionScale` argument. Every call site in Task 2 therefore only needs to pass `scale` (the second argument); do not pass `precision` explicitly at any call site.
- **Do not reorder the FluentValidation rule chains.** Every call site's `.PrecisionScale(...)` sits between a range check (`.GreaterThan(...)`/`.LessThan(...)`/`.GreaterThanOrEqualTo(...)`) and a `.WithMessage(...)`/`.When(...)` — only the `.PrecisionScale(18, N, true)` token itself is replaced with `.DecimalPrecision(N)`; everything before and after stays exactly where it is.
- This is a backend-only, single-slice-agnostic change — `api/Shared/` is the established location for genuinely cross-cutting backend helpers used by multiple feature slices (`AppDbContextExtensions`, `FunctionContextExtensions` are both consumed across Functions in different feature folders already), so adding a third `*Extensions.cs` file there does not violate this project's VSA slice-isolation convention (that convention applies to frontend feature-hook imports, not backend cross-cutting `Shared/` helpers).

### Existing code being modified — current state and what's preserved

All 8 files currently pass FluentValidation 12's built-in `.PrecisionScale(18, N, true)` inline, immediately followed by a field-specific `.WithMessage(...)` (and, on `Patch*` validators, a trailing `.When(...)` guard for optional fields). None of the 8 files' non-precision rules (range checks, `NotEmpty`, `MaximumLength`, `IsInEnum`, etc.) are touched by this story. The two files with dedicated precision tests (`OnboardingValidatorTests.cs`, `PatchFlatValidatorTests.cs`) must produce byte-identical pass/fail outcomes after the refactor — these are the regression guard for this story, not new tests to write.

### Testing Standards Summary

- Backend: xUnit, direct validator instantiation + `.Validate(request)` (no HTTP/Function layer needed for these tests) — matches the existing pattern in `OnboardingValidatorTests.cs`/`PatchFlatValidatorTests.cs`.
- No InMemory EF Core DB involved in validator tests — `PrecisionScale`/`DecimalPrecision` is a FluentValidation-level check that runs before any DB access, unrelated to `project-context.md`'s "InMemory doesn't enforce `decimal(18,4)` precision" caveat (that caveat is about the DB layer, not this validator layer).
- Optional, not required by any AC: a small dedicated `api.Tests/Shared/DecimalPrecisionValidatorExtensionsTests.cs` directly unit-testing the new extension method in isolation (e.g. a throwaway `TestModel { decimal Value }` with a validator using `.DecimalPrecision(4)`) would give faster, more targeted feedback than relying solely on the two existing validator test files — add only if time permits.

### Project Structure Notes

- New file: `api/Shared/DecimalPrecisionValidatorExtensions.cs` — matches the existing `api/Shared/{Subject}Extensions.cs` naming convention exactly.
- No frontend changes, no new migration, no new API contract — this story is entirely contained within `api/Features/*/`+`api/Shared/`.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-9-unified-save-affordance-fix-and-pre-epic-10-hardening.md#Story 9.7] — verbatim epic AC and the rescope note explaining why this is a pure DRY refactor, not a bug fix.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:44] — original item ("Deferred from: code review of decimal-precision-validation-policy, 2026-07-05") that promoted this story, listing all 8 affected files.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:211,245] — the two other deferred items referencing decimal-precision validation, both verified resolved/false during Epic 9 planning per the epic's rescope note; do not re-open them here.
- [Source: api/Shared/FunctionContextExtensions.cs] — existing `EnergyTracker.Api.Shared` static extension class, the naming/placement pattern `DecimalPrecisionValidatorExtensions.cs` follows.
- [Source: api/Features/Tariffs/TariffValidator.cs:9-19, PatchTariffValidator.cs:9-19] — 4 of the 16 call sites (`PricePerKwh` scale 6, `MonthlyBaseFee` scale 4).
- [Source: api/Features/FlatStructure/UpdateFlatStructureValidator.cs:29-36] — the 2 call sites on `decimal?` properties, nested inside triple `ChildRules`.
- [Source: api/Features/FlatStructure/FlatStructureModels.cs:14-15,43-44] — confirms `EuAnnualKwh`/`SelfMeasuredKwh` are `decimal?`, requiring the nullable overload.
- [Source: api/Features/Readings/ReadingValidator.cs:9-12, PatchReadingValidator.cs:9-12] — 2 call sites (`KwhValue` scale 4).
- [Source: api/Features/Flats/PatchFlatValidator.cs:13-22, CreateFlatValidator.cs:10-18] — 4 call sites (`AnnualKwhBaseline`, `PlannedAnnualSpend`, both scale 4).
- [Source: api/Features/Onboarding/OnboardingValidator.cs:10-28] — 4 call sites (`AnnualKwhBaseline` scale 4, `PricePerKwh` scale 6, `MonthlyBaseFee` scale 4, `PlannedAnnualSpend` scale 4).
- [Source: api.Tests/Features/Onboarding/OnboardingValidatorTests.cs, api.Tests/Features/Flats/PatchFlatValidatorTests.cs] — the only two validator test files with dedicated decimal-precision assertions; the regression bar this story must clear unmodified.
- [Source: _bmad-output/project-context.md#Technology Stack] — FluentValidation 12.1.1; `AddValidatorsFromAssembly()` DI registration (unaffected by this story — no new validator classes are registered, only an existing one's internals refactored).

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet test api.Tests/api.Tests.csproj` — full suite: 362/362 passed
- `dotnet test api.Tests/api.Tests.csproj --filter "FullyQualifiedName~OnboardingValidatorTests|FullyQualifiedName~PatchFlatValidatorTests"` — 31/31 passed (regression guard)
- `grep -rn "PrecisionScale" api --include="*.cs"` after refactor — only match is the new `DecimalPrecisionValidatorExtensions.cs` itself; confirms all 16 inline call sites were replaced

### Completion Notes List

- Created `api/Shared/DecimalPrecisionValidatorExtensions.cs` with two `DecimalPrecision<T>` overloads (`decimal` and `decimal?`), each a thin wrapper delegating to FluentValidation's built-in `PrecisionScale(precision, scale, true)`, `precision` defaulting to 18.
- Replaced all 16 inline `.PrecisionScale(18, N, true)` call sites across the 8 listed validator files with `.DecimalPrecision(N)`, adding `using EnergyTracker.Api.Shared;` to each. No other rule chain ordering, `.WithMessage(...)`, or `.When(...)` calls were touched.
- Full backend test suite (362 tests) passes unmodified; the two dedicated precision-test files (`OnboardingValidatorTests.cs`, `PatchFlatValidatorTests.cs`) pass byte-for-byte unchanged, confirming zero behavior change.
- Did not add the optional `DecimalPrecisionValidatorExtensionsTests.cs` (explicitly marked optional in Dev Notes, not required by any AC) — existing regression coverage from the two validator test files was sufficient to confirm correctness.

### File List

- `api/Shared/DecimalPrecisionValidatorExtensions.cs` (new)
- `api/Features/Tariffs/TariffValidator.cs` (modified)
- `api/Features/Tariffs/PatchTariffValidator.cs` (modified)
- `api/Features/FlatStructure/UpdateFlatStructureValidator.cs` (modified)
- `api/Features/Readings/ReadingValidator.cs` (modified)
- `api/Features/Readings/PatchReadingValidator.cs` (modified)
- `api/Features/Flats/PatchFlatValidator.cs` (modified)
- `api/Features/Flats/CreateFlatValidator.cs` (modified)
- `api/Features/Onboarding/OnboardingValidator.cs` (modified)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified)

## Change Log

- 2026-07-19: Extracted the shared `DecimalPrecision` extension method (`api/Shared/DecimalPrecisionValidatorExtensions.cs`) and replaced all 16 inline `.PrecisionScale(18, N, true)` call sites across 8 validator files. Pure DRY refactor, zero behavior change — full test suite (362 tests) passes unmodified. Status → review.
