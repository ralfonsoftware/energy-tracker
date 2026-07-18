---
baseline_commit: 7b71d85c02899bc6fedf4b3836ed63223836091c
---

# Story 9.4: Smart Power Strip Remainder Formula — Spec Amendment & Fix

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want unconfigured devices on a Smart Power Strip to actually receive a meaningful share of the strip's unattributed consumption,
so that the Decomposition view doesn't silently hide consumption AC3 itself claims is being shared.

## Acceptance Criteria

1. **Given** `DecompositionEngine.cs:170-232`'s (`BuildSmartStripDecomposition`) current proportional-split formula (`device_share = (device_estimated_kWh / sumConfiguredEstimates) * stripMeasuredTotal`, remainder `= (stripMeasuredTotal - sumConfiguredShares) / unconfiguredCount`), under which configured shares always sum to exactly `stripMeasuredTotal` by construction — making `remainder` ≈0 in every mixed strip, contradicting AC3's own prose that unconfigured devices "share the remainder equally" — confirmed as a spec-formula property, not an implementation bug, **when** implemented in `DecompositionEngine.cs`, **then** it follows the approved blended-nominal-weight D-44 amendment (`.decision-log.md#D-44`, 2026-07-18): each unconfigured device gets a nominal weight equal to the average of the strip's configured devices' `device_estimated_kWh` (0 if none are configured); all devices (configured, at their real estimate, and unconfigured, at the nominal weight) are pooled into one proportional split of `stripMeasuredTotal`; when the strip has zero configured devices, every device receives an equal split of `stripMeasuredTotal` (the pre-amendment behavior for that case, preserved unchanged).
2. **Given** the approved formula, **when** implemented, **then** existing configured-device-only strip behavior (fully configured, no unconfigured siblings) is unchanged, a fully-unconfigured strip still splits equally, and a mixed strip now gives every unconfigured device an identical non-zero share while all shares still sum to exactly `stripMeasuredTotal`; regression tests cover all three cases (fully configured, fully unconfigured, mixed).

## Tasks / Subtasks

- [x] Task 1: Replace the proportional-split formula in `BuildSmartStripDecomposition` (AC: 1, 2)
  - [x] In `api/Features/Decomposition/DecompositionEngine.cs`, replace the `shares` computation block (currently lines 186-213 — the `if (sumConfiguredEstimates > 0m) { ... } else { ... }` block) with the blended-nominal-weight formula. Exact math (verbatim from D-44's amendment):
    ```
    configuredIds = devices where ConsumptionApproach != None
    unconfiguredIds = devices where ConsumptionApproach == None
    sumConfiguredEstimates = sum(estimates[id] for id in configuredIds)
    nominalWeight = configuredIds.Count > 0 ? sumConfiguredEstimates / configuredIds.Count : 0m
    poolTotal = sumConfiguredEstimates + (unconfiguredIds.Count * nominalWeight)

    if poolTotal > 0m:
        for id in configuredIds:   shares[id] = (estimates[id] / poolTotal) * stripMeasuredTotal
        for id in unconfiguredIds: shares[id] = (nominalWeight / poolTotal) * stripMeasuredTotal
    else:
        // zero configured devices (or, as an edge case, configured devices whose estimates sum to
        // zero — e.g. an EU-label device with EuAnnualKwh unset) — same branch condition and same
        // equal-split behavior the current code already uses for this case; keep it unchanged.
        equalShare = pp.Devices.Count > 0 ? stripMeasuredTotal / pp.Devices.Count : 0m
        for d in pp.Devices: shares[d.DeviceId] = equalShare
    ```
  - [x] Do not touch anything below the `shares` dictionary build — the `subDevices` loop (lines 215-227) that computes `ratio` and per-device cost from `shares[d.DeviceId]` is formula-agnostic and already correct against whatever `shares` contains. Do not touch `EstimateDailyKwh`, `ResolveStandaloneApproach`, or any other method in this file — this story is scoped to the one `shares`-computation block.
  - [x] Note the "remainder" concept is fully removed, not adjusted — there is no more `sumConfiguredShares` subtraction anywhere in the new formula; all shares (configured and unconfigured alike) come from the same single proportional-split pool. Do not keep a vestigial `remainder`/`sumConfiguredShares` variable around.

- [x] Task 2: Update the existing mixed-strip test to assert real values instead of skipping them (AC: 2)
  - [x] `api.Tests/Features/Decomposition/DecompositionEngineTests.cs:183-209` (`ComputeAsync_SmartPowerStrip_SplitsProportionallyByEstimateWithUnconfiguredRemainder`) currently only asserts `c.IsUnconfigured.ShouldBeTrue()` for the unconfigured `DeviceC` — it never asserts `c.Kwh`, because under the old formula that value was ≈0 (the exact bug this story fixes) and asserting it would have pinned the bug. With DeviceA daily=2, DeviceB daily=6 (`sumConfiguredEstimates=8`, `configuredIds.Count=2` → `nominalWeight=4`), `poolTotal = 8 + 1*4 = 12`, `stripMeasuredTotal=80`: `a.Kwh` must become `80*(2/12) = 13.333...m`, `b.Kwh` must become `80*(6/12) = 40m`, `c.Kwh` must become `80*(4/12) = 26.666...m` (a real, non-zero, equal-to-every-unconfigured-device share). Update the three `ShouldBe` assertions accordingly (use a `tolerance:` argument for the repeating-decimal values, consistent with the existing `(a.Kwh + b.Kwh + c.Kwh).ShouldBe(strip.Kwh, tolerance: 0.01m)` pattern already in this test) and rename the test to something that no longer says "remainder" now that the concept is gone (e.g. `ComputeAsync_SmartPowerStripMixed_UnconfiguredDevicesGetBlendedNominalShare`).
  - [x] Add an explicit `c.Kwh` assertion — this is the whole point of the fix; a test that still doesn't check it would silently permit a regression back to ≈0.

- [x] Task 3: Add a fully-configured-strip regression test (AC: 2)
  - [x] No existing test covers a strip where every device is configured (no unconfigured siblings at all) — `ComputeAsync_SmartPowerStrip_SplitsProportionallyByEstimateWithUnconfiguredRemainder` always included one unconfigured device, and `ComputeAsync_SmartPowerStripAllUnconfigured_SplitsEquallyWithoutDivideByZero` has zero configured devices. Add a new test (e.g. `ComputeAsync_SmartPowerStripFullyConfigured_SplitsProportionallyUnchanged`) with 2+ devices, all `ConsumptionApproach != None`, asserting shares are proportional to estimates and sum to `stripMeasuredTotal` exactly (`unconfiguredIds.Count == 0` makes `poolTotal == sumConfiguredEstimates`, so this must produce byte-identical output to the pre-fix formula for this case — confirms AC2's "unchanged" claim).
  - [x] Verify (by inspection, not just by running) that `ComputeAsync_SmartPowerStripAllUnconfigured_SplitsEquallyWithoutDivideByZero` (`:212-229`) needs zero changes — `configuredIds.Count == 0` → `nominalWeight = 0` → `poolTotal = 0` → same equal-split `else` branch as before. This test should pass unmodified; if it doesn't, the `poolTotal > 0m` branch condition was implemented wrong.

- [x] Task 4: Regression pass (AC: 1, 2)
  - [x] Run `dotnet test api.Tests/` (matching Story 7.1's convention) — confirm all `DecompositionEngineTests` pass, including the untouched `ComputeAsync_CleanPeriod_ResidualWithinTightTolerance` and every other existing test in the file (the formula change is scoped to `BuildSmartStripDecomposition`'s internals only; no other method or return shape changed).
  - [x] Confirm `GetDecompositionFunctionTests.cs` needs no changes — it does not test smart-strip split formula specifics (confirmed via grep during story creation: zero matches for "strip"/"Strip" in that file).

- [x] Task 5: Close out the originating `deferred-work.md` entry (AC: 1)
  - [x] `_bmad-output/implementation-artifacts/deferred-work.md`'s "Deferred from: code review of story-7.1" section has the line: *"Smart Power Strip 'unconfigured remainder' formula is mathematically dead in mixed strips — AC3/D-44's proportional-split formula causes `sum_of_configured_shares` to always equal `strip_measured_total` exactly, so unconfigured devices never receive a meaningful share, contradicting AC3's own prose. **→ Promoted to Story 9.4 (Epic 9)**."* Remove this line (or mark it resolved) once this story ships — it is the exact item this story fixes, and leaving it open after the fix lands would misrepresent deferred-work.md's tracking state.

### Review Findings

- [x] [Review][Patch] Add regression test for zero-sum configured estimates (e.g. EU-label device with `EuAnnualKwh` unset) mixed with unconfigured siblings — verify it correctly falls into the equal-split `else` branch (no dilution, no divide-by-zero) [api.Tests/Features/Decomposition/DecompositionEngineTests.cs]
- [x] [Review][Patch] Add regression test for a mixed strip with exactly one configured device — verify unconfigured devices correctly receive that device's own estimate as `nominalWeight` (a spec-correct degenerate case, currently untested) [api.Tests/Features/Decomposition/DecompositionEngineTests.cs]
- [x] [Review][Patch] Add regression test for a mixed strip with 2+ unconfigured devices — verify each receives an identical non-zero blended share and totals still reconcile to `stripMeasuredTotal` [api.Tests/Features/Decomposition/DecompositionEngineTests.cs]

## Dev Notes

### The bug, precisely

`DecompositionEngine.cs`'s `BuildSmartStripDecomposition` (lines 168-232) computes a Smart Power Strip's per-device kWh split. The current formula:
1. Computes each **configured** device's share as `(its estimate / sum of all configured estimates) * stripMeasuredTotal`.
2. By construction, these configured shares **always sum to exactly `stripMeasuredTotal`** (it's a normalized proportional split of the whole total).
3. The "remainder" for unconfigured devices is then computed as `stripMeasuredTotal - sumConfiguredShares`, which — because of step 2 — is always ≈0 (floating-point/decimal rounding noise only).

This was flagged during Story 7.1's code review (`deferred-work.md`, "Deferred from: code review of story-7.1") as a spec-formula defect, not an implementation bug: AC3's original prose promised unconfigured devices "share the remainder equally," but the formula as written mathematically guarantees that remainder is always ~0 whenever ≥1 device is configured. Ralf resolved this via a design decision (D-44 amendment, 2026-07-18) rather than a code-only fix, since the fix changes what number gets shown to the user — see the exact approved formula in Task 1.

### Why the new formula gives unconfigured devices a real share

The key change: unconfigured devices are no longer computed *after* the configured split is finalized (leaving nothing over). Instead, every device — configured and unconfigured alike — is pooled into **one single proportional split** from the start, where unconfigured devices are assigned a synthetic "nominal weight" (the average of the configured devices' real estimates) so they compete for a fair share of the pool on the same terms as configured devices. Because the pool total (`poolTotal`) now includes the unconfigured devices' nominal weight, the configured devices' shares no longer sum to the full `stripMeasuredTotal` on their own — the unconfigured devices' proportional slice is baked into the same division, not left over as a subtraction.

### Existing code context (read before editing)

- `pp.Devices.ToDictionary(d => d.DeviceId, d => EstimateDailyKwh(d) * dayCount)` (line 179) already gives you the estimate per device — unchanged, reuse as-is.
- `configuredIds` (lines 180-183) is already computed as a `HashSet<Guid>` filtered on `ConsumptionApproach != ConsumptionApproach.None` — reuse this exact predicate for `unconfiguredIds` too (the complement).
- The `subDevices` build loop (lines 215-227), which computes `ratio = shares[id] / stripMeasuredTotal` (or an equal-split fallback when `stripMeasuredTotal == 0m`) and the per-sub-device cost via `costForDailySeries`, is entirely downstream of `shares` and untouched by this fix — it already handles whatever `shares` dictionary it's given.
- `IsConfigured`/`IsUnconfigured` on `SubDeviceDecomposition` (`DecompositionModels.cs:9-10`) are derived from `d.ConsumptionApproach != ConsumptionApproach.None` at line 224 — unaffected by this story, still correct.

### What this story does NOT touch

- `EstimateDailyKwh`, `ResolveStandaloneApproach`, `ResolveTariff`, `TryComputeMainMeterTotal`, `BuildMainMeterDailySeries` — none of these are part of the smart-strip share calculation and are out of scope.
- Any other `DecompositionEngine.ComputeAsync` code path (single-device PowerPoints, standalone devices, orphaned plugs, residual/main-meter reconciliation) — this story is scoped to `BuildSmartStripDecomposition` only.
- No API contract change — `DecompositionResponse`/`DeviceDecomposition`/`SubDeviceDecomposition` shapes are unchanged; only the numeric `Kwh`/`Cost` values for unconfigured sub-devices in mixed strips change.
- No frontend change — `SmartStripCard.tsx` (or any other client component) already renders whatever `Kwh`/`Cost`/`IsUnconfigured` values the API returns; it needs no changes to display the new (larger, non-zero) unconfigured shares correctly, since the response shape is identical.

### Testing Standards Summary

- Test placement: `api.Tests/Features/Decomposition/DecompositionEngineTests.cs` (mirrors `api/Features/Decomposition/DecompositionEngine.cs`), following this project's `api.Tests/Features/{Feature}/{Class}Tests.cs` convention.
- `EF Core InMemory` provider via the file's existing `MakeDb()`/`SeedRoomAsync`/`SeedPowerPointAsync`/`SeedDeviceAsync`/`SeedDailyRowAsync` helpers — reuse these exactly as the existing smart-strip tests do; do not invent new seeding helpers.
- Framework: xUnit + Shouldly (`ShouldBe`, `ShouldBeTrue`, `ShouldAllBe`), matching every other test in this file.
- For repeating-decimal expected values (e.g. `80m * 2m / 12m`), use Shouldly's `tolerance:` parameter exactly as the existing test already does for its summed-shares assertion (`(a.Kwh + b.Kwh + c.Kwh).ShouldBe(strip.Kwh, tolerance: 0.01m)`) — do not round the expected literal by hand in a way that could mask a formula error.
- Three regression cases are required by AC2: fully configured (Task 3, new), fully unconfigured (already exists, `:212-229`, must remain green unmodified), mixed (Task 2, existing test updated with real assertions).

### Project Structure Notes

- Single backend file touched for the fix: `api/Features/Decomposition/DecompositionEngine.cs`.
- Single test file touched: `api.Tests/Features/Decomposition/DecompositionEngineTests.cs`.
- One documentation file touched for cleanup: `_bmad-output/implementation-artifacts/deferred-work.md` (Task 5).
- No new files, no new dependencies, no DB schema change, no migration, no frontend change.
- VSA slice isolation unaffected — this is an internal calculation change within the existing `Decomposition` feature slice; no cross-slice imports.

### Previous Story Intelligence

- Story 9.3 (immediately prior) was a frontend-only, zero-code-change story (live visual verification confirmed no restyle was needed) — no backend patterns or learnings carry forward from it. This story returns to the backend `Decomposition` slice last touched by Story 7.1.
- Story 7.1 (`_bmad-output/implementation-artifacts/7-1-decomposition-backend-engine-api-and-cost-attribution.md`) is the story that originally implemented `DecompositionEngine.cs`, including the now-amended formula, and whose own code review first flagged this exact defect (`deferred-work.md`, "Deferred from: code review of story-7.1"). Its patterns (in-memory `ResolveTariff` duplication, `record` DTOs, `AsNoTracking()` reads, `CancellationToken ct` threading) are already established in the file being edited — this story does not need to introduce any new pattern, only replace the one broken formula block.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-9-unified-save-affordance-fix-and-pre-epic-10-hardening.md#Story 9.4] — original epic AC text (verbatim source for this story's ACs).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/.decision-log.md#D-44] — the approved D-44 amendment (2026-07-18) this story implements; contains the exact formula (`nominal_weight`, `pool_total`, configured/unconfigured share formulas, zero-configured-devices special case).
- [Source: _bmad-output/implementation-artifacts/deferred-work.md#Deferred from: code review of story-7.1] — the original defect report this story resolves and closes out (Task 5).
- [Source: api/Features/Decomposition/DecompositionEngine.cs:168-232] — `BuildSmartStripDecomposition`, the method this story modifies (only the `shares` computation block, lines 186-213).
- [Source: api/Features/Decomposition/DecompositionModels.cs] — `SubDeviceDecomposition`/`DeviceDecomposition` record shapes (unchanged by this story).
- [Source: api.Tests/Features/Decomposition/DecompositionEngineTests.cs:183-229] — existing smart-strip tests (mixed case to update in Task 2, all-unconfigured case that must stay green in Task 3).
- [Source: _bmad-output/implementation-artifacts/7-1-decomposition-backend-engine-api-and-cost-attribution.md] — the story that originally built this engine and file; establishes the coding patterns already in use here.
- [Source: _bmad-output/project-context.md#Backend (xUnit + EF Core InMemory)] — test placement convention (`api.Tests/Features/{Feature}/{Class}Tests.cs`), `decimal` invariant (never `float`/`double` for kWh values — already respected by the existing code and this fix).
- [Source: _bmad-output/planning-artifacts/architecture.md:67] — "Period-accurate tariff costing" as the domain's primary invariant; confirms this story's cost calculations (via `costForDailySeries`, untouched) must continue to reflect the correct tariff per date, which Task 1's change does not affect since it only changes the `shares`/kWh split, not the cost-per-date mechanism.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

None — implementation went green on first `dotnet test` run.

### Completion Notes List

- Replaced the `sumConfiguredShares`/`remainder` subtraction formula in `BuildSmartStripDecomposition` (`DecompositionEngine.cs`) with the D-44 blended-nominal-weight pooled proportional split: `nominalWeight = sumConfiguredEstimates / configuredIds.Count` (0 if none configured), `poolTotal = sumConfiguredEstimates + unconfiguredIds.Count * nominalWeight`, and all devices (configured at their real estimate, unconfigured at the nominal weight) share one `(x / poolTotal) * stripMeasuredTotal` proportional split. The zero-configured-devices equal-split `else` branch (now gated on `poolTotal > 0m`) is unchanged.
- Downstream `subDevices` loop, `EstimateDailyKwh`, and all other methods in the file were not touched, per story scope.
- Updated `ComputeAsync_SmartPowerStrip_SplitsProportionallyByEstimateWithUnconfiguredRemainder` → renamed `ComputeAsync_SmartPowerStripMixed_UnconfiguredDevicesGetBlendedNominalShare`, with real (non-zero) `a.Kwh`/`b.Kwh`/`c.Kwh` assertions using `tolerance: 0.01m` for the repeating-decimal values.
- Added `ComputeAsync_SmartPowerStripFullyConfigured_SplitsProportionallyUnchanged` — confirms the fully-configured case produces byte-identical shares to the pre-fix formula (2/8*80=20, 6/8*80=60).
- Confirmed `ComputeAsync_SmartPowerStripAllUnconfigured_SplitsEquallyWithoutDivideByZero` passed unmodified (equal-split branch unaffected).
- Full `dotnet test api.Tests/` run: 358/358 passed, no regressions.
- Confirmed `GetDecompositionFunctionTests.cs` has zero references to "strip"/"Strip" — no changes needed.
- Removed the resolved "Smart Power Strip 'unconfigured remainder' formula..." line from `deferred-work.md`'s "Deferred from: code review of story-7.1" section.
- ✅ Resolved review finding [Patch]: added `ComputeAsync_SmartPowerStripConfiguredEstimatesSumToZero_FallsBackToEqualSplit` — configured devices whose estimates sum to zero, mixed with unconfigured siblings, correctly fall into the equal-split branch.
- ✅ Resolved review finding [Patch]: added `ComputeAsync_SmartPowerStripSingleConfiguredDevice_UnconfiguredGetItsEstimateAsNominalWeight` — verifies the degenerate single-configured-device case, where `nominalWeight` collapses to that device's own estimate. Uses a tolerance-based assertion since `2m/6m` is a repeating decimal under `decimal` division (initial exact-equality assertion failed with `9.999999999999999999999999999` vs `10m`, confirming the Edge Case Hunter's precision observation — fixed by using tolerance, consistent with the rest of the file's convention for non-terminating-decimal shares).
- ✅ Resolved review finding [Patch]: added `ComputeAsync_SmartPowerStripMultipleUnconfiguredDevices_EachGetsIdenticalNonZeroShare` — verifies 2 unconfigured devices on one strip receive identical non-zero shares and the total reconciles.
- Full `dotnet test api.Tests/` re-run after patches: 361/361 passed.

### File List

- `api/Features/Decomposition/DecompositionEngine.cs` (modified)
- `api.Tests/Features/Decomposition/DecompositionEngineTests.cs` (modified)
- `_bmad-output/implementation-artifacts/deferred-work.md` (modified)

## Change Log

- 2026-07-18: Implemented D-44 blended-nominal-weight formula in `BuildSmartStripDecomposition`, updated/added regression tests (mixed, fully-configured, all-unconfigured cases), closed out the originating deferred-work.md entry. Status → review.
- 2026-07-18: Addressed code review findings — 3 patch items resolved (added regression tests for zero-sum-configured-estimates, single-configured-device, and multiple-unconfigured-device edge cases).
