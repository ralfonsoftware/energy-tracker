---
title: 'Backfill HTTP-level Function tests for CompleteOnboardingFunction and PatchFlatFunction'
type: 'chore'
created: '2026-07-03'
status: 'done'
route: 'one-shot'
context: []
---

# Backfill HTTP-level Function tests for CompleteOnboardingFunction and PatchFlatFunction

## Intent

**Problem:** `CompleteOnboardingFunction` and `PatchFlatFunction` had only narrow validator-level tests, no dedicated HTTP-level Function tests — an Epic 3 retro action item phrased as "consider backfilling" that slipped through the entirety of Epic 4 and was re-committed as a hard requirement in the Epic 4 retro (Action Item #3).

**Approach:** Add `CompleteOnboardingFunctionTests.cs` and `PatchFlatFunctionTests.cs`, following the established pattern already used by `PatchTariffFunctionTests.cs`/`GetTariffsFunctionTests.cs` (EF Core InMemory + mocked `FunctionContext`, calling `RunAsync` directly, asserting on the returned `IActionResult` and persisted DB state). Covers the happy path, ownership/authorization boundaries, validation failures, and malformed-input edge cases for both functions.

## Suggested Review Order

**CompleteOnboardingFunction coverage**

- Happy path — creates both the Flat and its first Tariff in one transaction; entry point for understanding the function's dual-write shape.
  [`CompleteOnboardingFunctionTests.cs:63`](../../api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs#L63)

- Provider name and contract duration pass-through — the two Tariff fields not exercised by the happy-path test.
  [`CompleteOnboardingFunctionTests.cs:87`](../../api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs#L87)

- `ContractStartDate` default-to-`UtcNow` vs. explicit-value precedence — the exact default behavior Story 4.4 documented as unchanged by the `EffectiveDate`/`ContractStartDate` consolidation.
  [`CompleteOnboardingFunctionTests.cs:104`](../../api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs#L104), [`CompleteOnboardingFunctionTests.cs:122`](../../api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs#L122)

- 409 conflict on double-onboarding — asserts zero rows written, not just the status code.
  [`CompleteOnboardingFunctionTests.cs:139`](../../api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs#L139)

- Malformed JSON and empty-`FlatName` validation — both 400 paths, both assert nothing was persisted.
  [`CompleteOnboardingFunctionTests.cs:158`](../../api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs#L158), [`CompleteOnboardingFunctionTests.cs:172`](../../api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs#L172)

**PatchFlatFunction coverage**

- Ownership boundary — wrong-owner 403 with a persisted-unchanged assertion, the highest-risk path for a tenant-scoped PATCH.
  [`PatchFlatFunctionTests.cs:85`](../../api.Tests/Features/Flats/PatchFlatFunctionTests.cs#L85)

- Invalid `flatId` and flat-not-found — the two guard clauses before any DB write is attempted.
  [`PatchFlatFunctionTests.cs:57`](../../api.Tests/Features/Flats/PatchFlatFunctionTests.cs#L57), [`PatchFlatFunctionTests.cs:71`](../../api.Tests/Features/Flats/PatchFlatFunctionTests.cs#L71)

- Single-field patches for each of the three independently-conditional fields.
  [`PatchFlatFunctionTests.cs:101`](../../api.Tests/Features/Flats/PatchFlatFunctionTests.cs#L101), [`PatchFlatFunctionTests.cs:118`](../../api.Tests/Features/Flats/PatchFlatFunctionTests.cs#L118)

- Multi-field patch in one request — proves the three conditional updates don't interfere.
  [`PatchFlatFunctionTests.cs:133`](../../api.Tests/Features/Flats/PatchFlatFunctionTests.cs#L133)

- `PlannedAnnualSpend`'s omitted-vs-explicit-null distinction (via its `*Provided` flag) vs. `AnnualKwhBaseline`'s silent no-op on explicit null — the asymmetry is pinned by test, not fixed; see `deferred-work.md`.
  [`PatchFlatFunctionTests.cs:199`](../../api.Tests/Features/Flats/PatchFlatFunctionTests.cs#L199), [`PatchFlatFunctionTests.cs:213`](../../api.Tests/Features/Flats/PatchFlatFunctionTests.cs#L213), [`PatchFlatFunctionTests.cs:150`](../../api.Tests/Features/Flats/PatchFlatFunctionTests.cs#L150)

- Empty-object no-op patch and non-object JSON body — the two boundary shapes of the manual `JsonNode` parsing path.
  [`PatchFlatFunctionTests.cs:169`](../../api.Tests/Features/Flats/PatchFlatFunctionTests.cs#L169), [`PatchFlatFunctionTests.cs:185`](../../api.Tests/Features/Flats/PatchFlatFunctionTests.cs#L185)

- Malformed JSON, wrong-typed field, and empty-string validation — remaining 400 paths.
  [`PatchFlatFunctionTests.cs:228`](../../api.Tests/Features/Flats/PatchFlatFunctionTests.cs#L228), [`PatchFlatFunctionTests.cs:242`](../../api.Tests/Features/Flats/PatchFlatFunctionTests.cs#L242), [`PatchFlatFunctionTests.cs:256`](../../api.Tests/Features/Flats/PatchFlatFunctionTests.cs#L256)
