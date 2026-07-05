---
title: 'Decimal-precision validation policy across all decimal request fields'
type: 'feature'
created: '2026-07-05'
status: 'done'
context: []
baseline_commit: '2efe6698796f39984ce879d9c9d71865c2200006'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every decimal-typed request field in this API is validated for range (`GreaterThan`/`LessThan`) but not for precision. A value with more decimal places than its DB column's configured scale (`decimal(18,4)` or `decimal(18,6)`) currently passes validation, gets silently rounded by SQL Server on save, and the immediate POST/PATCH response can diverge from the persisted value. Deferred since Story 4.1 as "systemic, needs a policy decision" — now decided as Epic 5 retro action item #2, before Epic 6/7/8 add more decimal fields.

**Approach:** Add FluentValidation's built-in `PrecisionScale(18, scale, ignoreTrailingZeros: true)` rule to every existing decimal `RuleFor` across all 7 validators, using each field's actual DB column scale (4 for most fields, 6 for `PricePerKwh`). `ignoreTrailingZeros: true` accepts mathematically-equivalent values like `12.50000` (no real precision loss) and rejects only genuine excess precision (e.g. `12.123456` into a scale-4 column). On violation: reject with HTTP 400 Problem Details, consistent with every other validation failure in this codebase — never silently truncate or round on the server.

## Boundaries & Constraints

**Always:**
- Precision is `18` for every rule (matches every `decimal(18,N)` column in this schema — there is no other precision in use).
- Scale matches the field's actual DB column exactly: `AnnualKwhBaseline`/`PlannedAnnualSpend`/`MonthlyBaseFee`/`KwhValue`/`EuAnnualKwh`/`SelfMeasuredKwh` → scale `4`; `PricePerKwh` → scale `6`.
- Add `.PrecisionScale(...)` into the *same* rule chain as each field's existing range rule (before any trailing `.When(...)`), not as a separate `RuleFor` — keeps one rule per field, matching this codebase's existing validator style.
- Error message format: `"{field} must have at most N decimal places."` (camelCase field name, matching every existing validator message in this codebase).
- Every validator file that already validates a decimal field gets this rule added — no field is skipped.

**Ask First:** None — this is the retro-approved policy; no further design decisions needed during implementation.

**Never:**
- Do not round, truncate, or auto-correct the value server-side — reject and let the client resend, per this codebase's universal validation-failure convention.
- Do not add precision validation to `OriginalKwhValue` (`MeterReading`) or `SpikeThreshold` (`Flat`) — neither is ever set from user-supplied request input (server-set on correction / hardcoded default respectively), so no validator rule exists or is needed for them.
- Do not change any existing range (`GreaterThan`/`LessThan`) rule's bounds — this story is precision-only.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Valid precision | `pricePerKwh: 0.2534` (scale 4, ≤6 allowed) | 200/201, persists as submitted | N/A |
| Excess precision | `annualKwhBaseline: 1234.56789` (5 decimals, scale-4 field) | 400 Problem Details | `"annualKwhBaseline must have at most 4 decimal places."` |
| Trailing zeros | `plannedAnnualSpend: 500.100000` (6 raw decimals, but scale-4-equivalent) | 200/201 — accepted, not rejected | N/A |
| Nullable field omitted | `plannedAnnualSpend` omitted from PATCH body | Existing `.When(...)` null-guard still applies; rule not evaluated | N/A |

</frozen-after-approval>

## Code Map

- `api/Features/Onboarding/OnboardingValidator.cs` -- `AnnualKwhBaseline`(4), `PricePerKwh`(6), `MonthlyBaseFee`(4)
- `api/Features/Flats/CreateFlatValidator.cs` -- `AnnualKwhBaseline`(4), `PlannedAnnualSpend`(4)
- `api/Features/Flats/PatchFlatValidator.cs` -- `AnnualKwhBaseline`(4), `PlannedAnnualSpend`(4)
- `api/Features/Tariffs/TariffValidator.cs` -- `PricePerKwh`(6), `MonthlyBaseFee`(4)
- `api/Features/Tariffs/PatchTariffValidator.cs` -- `PricePerKwh`(6), `MonthlyBaseFee`(4)
- `api/Features/Readings/ReadingValidator.cs` -- `KwhValue`(4)
- `api/Features/Readings/PatchReadingValidator.cs` -- `KwhValue`(4)
- `api/Features/FlatStructure/UpdateFlatStructureValidator.cs` -- nested `EuAnnualKwh`(4), `SelfMeasuredKwh`(4) inside `RuleForEach(...).ChildRules(...)`
- Test files (one per validator above, matching whichever level already has coverage): `api.Tests/Features/Onboarding/OnboardingValidatorTests.cs`, `api.Tests/Features/Flats/CreateFlatFunctionTests.cs`, `api.Tests/Features/Flats/PatchFlatValidatorTests.cs`, `api.Tests/Features/Tariffs/CreateTariffFunctionTests.cs`, `api.Tests/Features/Tariffs/PatchTariffFunctionTests.cs`, `api.Tests/Features/Readings/SubmitReadingTests.cs`, `api.Tests/Features/Readings/PatchReadingFunctionTests.cs`, `api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs`

## Tasks & Acceptance

**Execution:**
- [x] `api/Features/Onboarding/OnboardingValidator.cs` -- add `.PrecisionScale(18, 4/6, true)` to all 3 decimal rules -- first onboarding write path, sets the pattern
- [x] `api/Features/Flats/CreateFlatValidator.cs` -- add to both decimal rules
- [x] `api/Features/Flats/PatchFlatValidator.cs` -- add to both decimal rules, inside existing `.When(...)` guards
- [x] `api/Features/Tariffs/TariffValidator.cs` -- add to both decimal rules
- [x] `api/Features/Tariffs/PatchTariffValidator.cs` -- add to both decimal rules, inside existing `.When(...)` guards
- [x] `api/Features/Readings/ReadingValidator.cs` -- add to `KwhValue`
- [x] `api/Features/Readings/PatchReadingValidator.cs` -- add to `KwhValue`
- [x] `api/Features/FlatStructure/UpdateFlatStructureValidator.cs` -- add to nested `EuAnnualKwh`/`SelfMeasuredKwh`, inside existing `.When(...)` guards
- [x] Extend each test file listed in Code Map with one excess-precision-rejects-400 case and one trailing-zeros-accepts case per field, following each file's existing bounds-theory `[Theory]` pattern -- proves the I/O Matrix's two decimal-edge scenarios per field

**Acceptance Criteria:**
- Given any of the 8 fields above submitted with more decimal places than its column's scale, when the request is validated, then it returns HTTP 400 Problem Details with a message naming the field and its max decimal places.
- Given the same fields submitted with trailing zeros beyond the scale but no real excess precision (e.g. `12.50000` for a scale-4 field), when validated, then the request succeeds unchanged.
- Given the full existing test suites for all 8 files, when run after this change, then every pre-existing test still passes unmodified.

## Verification

**Commands:**
- `dotnet build` (from `api/`) -- expected: 0 errors
- `dotnet test api.Tests` -- expected: full suite green, including new precision test cases

## Suggested Review Order

**Policy decision — precision/scale choices**

- The precision policy itself: `18` total digits, scale matched per field's DB column (`4` for kWh/money fields, `6` for price-per-kWh) — sets the pattern every other file follows.
  [`OnboardingValidator.cs:10`](../../api/Features/Onboarding/OnboardingValidator.cs#L10)

**Validator rules (7 files, same pattern)**

- `.PrecisionScale(...)` added into the same rule chain as each field's existing range rule, before any trailing `.When(...)` guard.
  [`CreateFlatValidator.cs:9`](../../api/Features/Flats/CreateFlatValidator.cs#L9)

- Nullable-field guard ordering: precision rule sits inside the existing `.When(...)` condition, so omitted PATCH fields correctly skip it.
  [`PatchFlatValidator.cs:13`](../../api/Features/Flats/PatchFlatValidator.cs#L13)

- Two scales in one file (`6` for price, `4` for fee) — the only validator with a mixed-scale case.
  [`TariffValidator.cs:9`](../../api/Features/Tariffs/TariffValidator.cs#L9)

- Same mixed-scale pattern on the PATCH side.
  [`PatchTariffValidator.cs:9`](../../api/Features/Tariffs/PatchTariffValidator.cs#L9)

- Single-field validators (`KwhValue`), simplest case in the set.
  [`ReadingValidator.cs:9`](../../api/Features/Readings/ReadingValidator.cs#L9)

- Same single-field pattern on the correction/PATCH path.
  [`PatchReadingValidator.cs:9`](../../api/Features/Readings/PatchReadingValidator.cs#L9)

- Nested `RuleForEach(...).ChildRules(...)` case — precision rule applied two levels deep inside a Device's `EuAnnualKwh`/`SelfMeasuredKwh`.
  [`UpdateFlatStructureValidator.cs:31`](../../api/Features/FlatStructure/UpdateFlatStructureValidator.cs#L31)

**Test coverage (one excess-precision + one trailing-zeros case per field)**

- Direct-validator tests, no HTTP layer — cheapest to read first.
  [`OnboardingValidatorTests.cs:51`](../../api.Tests/Features/Onboarding/OnboardingValidatorTests.cs#L51)

- Same direct-validator style for the PATCH-side nullable fields.
  [`PatchFlatValidatorTests.cs:108`](../../api.Tests/Features/Flats/PatchFlatValidatorTests.cs#L108)

- Function-level test; note the added `(await db.Flats.CountAsync()).ShouldBe(0)` assertion — confirms nothing persists on rejection.
  [`CreateFlatFunctionTests.cs:165`](../../api.Tests/Features/Flats/CreateFlatFunctionTests.cs#L165)

- Function-level with the tariff-lock interaction: trailing-zeros success cases needed `lockOverride: true` since the seeded tariff defaults to locked — the one non-obvious wrinkle in this diff.
  [`PatchTariffFunctionTests.cs:317`](../../api.Tests/Features/Tariffs/PatchTariffFunctionTests.cs#L317)

- Mixed-scale coverage on the create path.
  [`CreateTariffFunctionTests.cs:256`](../../api.Tests/Features/Tariffs/CreateTariffFunctionTests.cs#L256)

- Reading submit/patch coverage, includes persisted-value assertions matching this codebase's stricter existing convention for this feature.
  [`SubmitReadingTests.cs:142`](../../api.Tests/Features/Readings/SubmitReadingTests.cs#L142)

- Same pattern for corrections.
  [`PatchReadingFunctionTests.cs:163`](../../api.Tests/Features/Readings/PatchReadingFunctionTests.cs#L163)

- Nested-payload test coverage for the Device-level fields.
  [`UpdateFlatStructureFunctionTests.cs:308`](../../api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs#L308)

**Peripherals**

- Two pre-existing gaps logged during review, not introduced by this change.
  [`deferred-work.md:3`](../../_bmad-output/implementation-artifacts/deferred-work.md#L3)
