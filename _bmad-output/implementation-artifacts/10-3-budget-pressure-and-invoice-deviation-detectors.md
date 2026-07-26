---
baseline_commit: 22c5e31831ae1634738acf0a9a3c1f326f67b62a
---

# Story 10.3: Budget Pressure & Invoice Deviation Detectors

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to be warned when my projected annual spend is tracking over budget and when my rolling annual consumption is diverging significantly from my baseline, with exact euro and kWh figures,
So that I can act before the invoice arrives rather than after.

## Acceptance Criteria

1. **`BudgetAlertDetector.cs` — budget pressure detection.** Given a flat with `PlannedAnnualSpend` configured (non-null) and a rolling 30-day reading window (see Dev Notes for the exact window algorithm — a reading at or before "now − 30 days" anchoring at least one more recent reading), when a discovery run processes the flat: the detector computes `dailyAverageCost` over that window using period-accurate tariff resolution (per-period `ResolveTariff`, duplicated per this codebase's established convention — periods with no resolvable tariff are excluded from the cost sum); `projectedAnnualCost = dailyAverageCost × 365m`; when `projectedAnnualCost > PlannedAnnualSpend`, one `Insight` row is written: `Type = Budget`, `DeviceId = null`, `Data` JSON = `{ "projectedAnnualCost": decimal, "plannedAnnualSpend": decimal, "overspendEur": decimal }` where `overspendEur = projectedAnnualCost − plannedAnnualSpend`. All fields are `decimal` — no `float`/`double`.

2. **`BudgetAlertDetector.cs` — no alert when within budget.** Given the same computation where `projectedAnnualCost ≤ PlannedAnnualSpend`, when the run completes, no `Budget` `Insight` row is written and no error is generated.

3. **`BudgetAlertDetector.cs` — skip conditions.** Given a flat where `PlannedAnnualSpend` is `null`, **or** the flat has fewer than 30 days of rolling-window `MeterReadings` coverage (no reading exists at or before "now − 30 days", or fewer than 2 readings qualify — see Dev Notes), when invoked: the detector skips execution for that flat and exits cleanly — no `Insight` written, no error, `InsightRun` proceeds to the next detector regardless.

4. **`InvoiceDeviationDetector.cs` — invoice deviation detection.** Given a flat with a rolling 60-day reading window (same anchor-based algorithm as AC #3, with `windowDays = 60`), when a discovery run processes the flat: the detector computes `dailyAverageKwh` over that window (sum of clamped-non-negative consecutive-reading deltas ÷ actual window span in days); `projectedAnnualKwh = dailyAverageKwh × 365m`; `baselineKwh = flat.AnnualKwhBaseline` (always configured — see Dev Notes critical correction, this is a non-nullable required field, never actually null); `deviation = |projectedAnnualKwh − baselineKwh| / baselineKwh`; when `deviation ≥ 0.10m` (≥10%), the detector resolves the flat's current tariff (`ResolveTariff` for `DateTimeOffset.UtcNow`) — if none resolves, skip (no Insight, cost figure not computable) — otherwise one `Insight` row is written: `Type = InvoiceDeviation`, `DeviceId = null`, `Data` JSON = `{ "projectedAnnualKwh": decimal, "baselineKwh": decimal, "deviationPct": decimal, "impliedDeltaEur": decimal, "direction": "above" | "below" }` where `deviationPct = deviation × 100m` (percentage points, e.g. `15.0` for a 15% deviation — see Dev Notes), `direction = "above"` when `projectedAnnualKwh > baselineKwh` else `"below"`, and `impliedDeltaEur = (projectedAnnualKwh − baselineKwh) × tariff.PricePerKwh` (signed, matching `direction`). All decimal fields are `decimal` — no `float`/`double`.

5. **`InvoiceDeviationDetector.cs` — no insight within threshold.** Given the same computation where `deviation < 0.10m`, when the run completes, no `InvoiceDeviation` `Insight` row is written and no error is generated.

6. **`InvoiceDeviationDetector.cs` — skip condition.** Given a flat with fewer than 60 days of rolling-window `MeterReadings` coverage (same anchor test as AC #3, `windowDays = 60`), when invoked: the detector skips execution for that flat and exits cleanly — no `Insight` written, no error. (The epic's literal "`AnnualKwhBaseline` is null" skip condition does not apply to this codebase — see Dev Notes critical correction; there is no reachable null-baseline case to test.)

7. **`BudgetAlertDetectorTests.cs` and `InvoiceDeviationDetectorTests.cs`** in `api.Tests/Features/Insights/`. Budget tests cover: 30-day window with `projectedAnnualCost > PlannedAnnualSpend` → `Insight` written with correct `projectedAnnualCost`/`plannedAnnualSpend`/`overspendEur`; `projectedAnnualCost ≤ PlannedAnnualSpend` → no `Insight`; `PlannedAnnualSpend = null` → skip, no `Insight`; fewer than 30 days of reading-window coverage → skip, no `Insight`; a period within the window whose date predates every configured tariff → excluded from the cost sum without throwing. Invoice deviation tests cover: consumption trending +15% above baseline → `Insight` with `direction = "above"` and correct `deviationPct`/`impliedDeltaEur`; −12% below baseline → `Insight` with `direction = "below"`; +8% (below the ±10% threshold) → no `Insight`; fewer than 60 days of reading-window coverage → skip, no `Insight`; window computed correctly but no tariff resolves for "now" → skip, no `Insight`.

## Tasks / Subtasks

- [x] Task 1: Shared rolling-window helper pattern (AC: #1, #3, #4, #6)
  - [x] In each detector independently (per this codebase's established per-engine duplication convention — do not extract a shared utility class), implement a private static window-resolution method: given `readings` (ascending by `ReadingDate`), `windowDays`, and `now`, find the last reading with `ReadingDate <= now.AddDays(-windowDays)` (the "anchor"); if none exists, return "insufficient data". Take all readings from the anchor onward (inclusive); if fewer than 2 remain, return "insufficient data". This guarantees the window's actual span (`last.ReadingDate − anchor.ReadingDate`) is always `≥ windowDays`
  - [x] Query pattern: `db.MeterReadings.AsNoTracking().Where(r => r.FlatId == flatId).OrderBy(r => r.ReadingDate).ToListAsync(ct)` — exactly matches `GetDashboardFunction.cs:38-41`'s existing query shape, load all readings once, window in memory
- [x] Task 2: `BudgetAlertDetector.cs` real implementation (AC: #1, #2, #3)
  - [x] Load `flat` (`AsNoTracking`, `SingleOrDefaultAsync` by `FlatId`); if `flat is null` or `flat.PlannedAnnualSpend is null`, skip (no writes) — call `SaveChangesAsync(ct)` and return, mirroring `ReplacementDetector`'s early-return style
  - [x] Resolve the 30-day window per Task 1; if insufficient data, skip
  - [x] Load `tariffs` for the flat; iterate consecutive reading pairs in the window, accumulating `totalCost` via period-accurate `ResolveTariff(tariffs, periodStartDate)` per pair (same per-interval style as `KpiCalculator.Compute`'s main loop, `api/Features/Dashboard/KpiCalculator.cs:74-92`) — periods with no resolvable tariff contribute 0 to `totalCost` and are simply skipped (do not attempt `KpiCalculator`'s full uncovered-days bookkeeping; this detector doesn't need to report a cost-gap flag)
  - [x] `dailyAverageCost = totalCost / actualWindowDays` where `actualWindowDays` is the window's real span in days (decimal); `projectedAnnualCost = dailyAverageCost * 365m`
  - [x] If `projectedAnnualCost > flat.PlannedAnnualSpend.Value`: serialize `{ projectedAnnualCost, plannedAnnualSpend, overspendEur }` (camelCase, via `InsightsConstants.MessageJsonOptions`) and add an `Insight` row (`Type = Budget`, `DeviceId = null`, `FlatId`, `RunId`, `CreatedAt = DateTimeOffset.UtcNow`)
  - [x] `SaveChangesAsync(ct)` at the end of `DetectAsync`, matching Standby/Replacement's persistence contract
- [x] Task 3: `InvoiceDeviationDetector.cs` real implementation (AC: #4, #5, #6)
  - [x] Load `flat` (`AsNoTracking`, `SingleOrDefaultAsync`); if `flat is null`, skip
  - [x] Resolve the 60-day window per Task 1; if insufficient data, skip
  - [x] Compute `totalKwh` as the sum of `Math.Max(0m, next.KwhValue - prev.KwhValue)` over consecutive window pairs (meter-reset clamping, matching `KpiCalculator`'s existing clamp pattern); `dailyAverageKwh = totalKwh / actualWindowDays`; `projectedAnnualKwh = dailyAverageKwh * 365m`
  - [x] `baselineKwh = flat.AnnualKwhBaseline`; `deviation = Math.Abs(projectedAnnualKwh - baselineKwh) / baselineKwh` (no zero-guard needed — `AnnualKwhBaseline` is validated `GreaterThan(0)` at every write path, see Dev Notes)
  - [x] If `deviation < 0.10m`, no insight — return after `SaveChangesAsync(ct)`
  - [x] Resolve current tariff via the same `ResolveTariff(tariffs, DateTimeOffset.UtcNow)` helper (duplicated verbatim, same as Task 2/Standby/Replacement); if `null`, skip (no insight)
  - [x] Compute `direction`, `deviationPct = deviation * 100m`, `impliedDeltaEur = (projectedAnnualKwh - baselineKwh) * tariff.PricePerKwh`; serialize and add an `Insight` row (`Type = InvoiceDeviation`, `DeviceId = null`)
  - [x] `SaveChangesAsync(ct)` at the end of `DetectAsync`
- [x] Task 4: Backend tests (AC: #7)
  - [x] `BudgetAlertDetectorTests.cs` — cases listed in AC #7; mirror `ProcessInsightsFunctionTests.cs`'s `MakeDb`/`SeedFlatAndRunAsync`-style seeding helpers, extended with a `SeedReadingAsync`/`SeedTariffAsync` helper for `MeterReading`/`Tariff` rows
  - [x] `InvoiceDeviationDetectorTests.cs` — cases listed in AC #7, covering both deviation directions and both skip paths
  - [x] No changes needed to `ProcessInsightsFunctionTests.cs` — the idempotency guard added in Story 10.2 already covers all four detectors uniformly (confirmed: it clears every stale `Insight` row for the `RunId` before any detector runs, regardless of `Type`)

## Dev Notes

### Critical corrections / clarifications (verified against current code — the epic text underspecifies or mis-describes these)

- **`Flat.AnnualKwhBaseline` can never be `null` — the epic's "given `AnnualKwhBaseline` is null" skip condition (epic AC for `InvoiceDeviationDetector`) is unreachable in this codebase.** `Flat.AnnualKwhBaseline` (`api/Data/Entities/Flat.cs:8`) is a non-nullable `decimal`, configured `.IsRequired()` in `FlatConfiguration.cs:16`, and every write path (`CompleteOnboardingFunction`, `CreateFlatFunction`, `PatchFlatFunction`) validates it `GreaterThan(0).LessThan(20000)` before persisting. A `Flat` row cannot exist without a positive baseline. AC #6 above and this story's tests only cover the reading-window insufficiency skip, not a baseline-null skip — do not attempt to add a `decimal?` cast or null-check for this field; it would be dead code. Contrast with `PlannedAnnualSpend` (`decimal?` on `Flat.cs:10`, genuinely optional per FR-37/Q-4 — collected in Onboarding Step 2, editable in Settings), whose null-skip in `BudgetAlertDetector` (AC #3) **is** real and must be implemented.
- **The idempotency guard from Story 10.2 already covers this story's two detectors — nothing further to add.** `ProcessInsightsFunction.cs`'s redelivery guard (added in Story 10.2, `api/Features/Insights/ProcessInsightsFunction.cs:52-64`) deletes all pre-existing `Insight` rows for the `RunId` before any of the four detectors run, regardless of `Type`. This was explicitly designed to cover `BudgetAlertDetector`/`InvoiceDeviationDetector` "for free" (Story 10.2 AC #6). Do not add a second guard in this story.
- **`ResolveTariff` — duplicate verbatim per detector, do not extract a shared utility, do not recreate the deleted `TariffResolver` class.** Same pattern as `KpiCalculator.cs:158-167`, `DecompositionEngine.cs`, and Story 10.2's `StandbyDetector`/`ReplacementDetector`: a private static `ResolveTariff(IReadOnlyList<Tariff> tariffs, DateTimeOffset date)` duplicated in each new detector file (`[Project context: Data integrity invariants]`).
- **"30/60 days of MeterReadings" — story-creation-time algorithm decision, not spelled out by the epic.** The epic says "at least 30 days of `MeterReadings`" / "at least 60 days of `MeterReadings`" without defining what that means precisely for irregularly-spaced manual readings. Implementation decision: find the last reading at or before `now.AddDays(-windowDays)` (the "anchor" — proves at least `windowDays` of history exists before the window), then use every reading from that anchor forward (inclusive) as the window; require at least 2 readings total (anchor + something more recent) to compute a delta. This exactly mirrors the existing `KpiCalculator`/`GetDashboardFunction` pattern of loading all readings ascending and computing deltas in-memory (`api/Features/Dashboard/GetDashboardFunction.cs:38-41`), just scoped to a trailing window instead of the whole history. Flag to the user if a stricter "N distinct calendar days" definition (like Story 10.2's `StandbyDetector`) is preferred instead — this is a reasonable but not the only valid interpretation.
- **`deviationPct` scale — story-creation-time decision.** The epic's field name (`deviationPct`, not `deviation`) implies a percentage, not a raw fraction. This story computes the internal `deviation` ratio (e.g. `0.15m` for 15%) per the epic's literal formula, then multiplies by `100m` only when populating the `deviationPct` JSON field (`15.0m`, not `0.15m`). The `≥ 0.10m` / `< 0.10m` threshold checks in Dev Notes/Tasks always operate on the raw ratio, not the ×100 value — do not compare `deviationPct` against `10m` directly in code (compare `deviation` against `0.10m` before the ×100 conversion).
- **`BudgetAlertDetector`'s per-period tariff costing is intentionally simpler than `KpiCalculator`'s.** `KpiCalculator.Compute` tracks `uncoveredDays` to report a `HasCostGap` flag to the dashboard UI — this detector has no such UI surface and doesn't need that bookkeeping. Periods with no resolvable tariff simply contribute `0m` to `totalCost` and are not separately tracked; this can only under-count cost in the (currently unreachable in practice) case of a reading predating every tariff, which is an acceptable simplification for a v1 budget alert, not a defect to over-engineer around.
- **PRD FR-37 says "rolling monthly projection × 12"; the epic AC (this story's actual spec) says `dailyAverageCost × 365`.** These are the same idea at different granularity (30-day monthly ×12 ≈ 360 days vs. a direct ×365 annualization) — not a real conflict. Follow the epic's literal `× 365` formula; it is the more precise version and is what this story's AC #1 mandates.
- **`DeviceId` is `null` for both `Budget` and `InvoiceDeviation` insight types** — these are flat-level findings, not device-level, unlike `Standby`/`Replacement`. `InsightConfiguration` already declares `DeviceId` as nullable (Story 10.1), no schema change needed.
- **JSON serialization for `Insight.Data`.** Same as Story 10.2: reuse `InsightsConstants.MessageJsonOptions` (`api/Features/Insights/InsightModels.cs`, `internal`, camelCase) — do not declare a new `JsonSerializerOptions` instance. Define a small private record per detector, e.g. `private record BudgetInsightData(decimal ProjectedAnnualCost, decimal PlannedAnnualSpend, decimal OverspendEur);` and `private record InvoiceDeviationInsightData(decimal ProjectedAnnualKwh, decimal BaselineKwh, decimal DeviationPct, decimal ImpliedDeltaEur, string Direction);`.
- **Detector persistence contract (established in Story 10.1, not this story's decision).** Detectors call `db.Insights.Add(...)` and `SaveChangesAsync(ct)` themselves — `ProcessInsightsFunction` does not persist on their behalf. `DetectAsync(Guid flatId, Guid runId, CancellationToken ct)` signature is locked; both stubs (`BudgetAlertDetector.cs`, `InvoiceDeviationDetector.cs`) already match it exactly — `ProcessInsightsFunction`'s four call sites depend on it as-is, do not change it.
- **Both detectors currently take only `AppDbContext db` in their constructor** (`api/Features/Insights/BudgetAlertDetector.cs:8`, `InvoiceDeviationDetector.cs:8`) — no new dependencies are needed for this story's computation (no external services, no config beyond what's on `Flat`/`Tariff`/`MeterReading`), so the constructor signature does not need to change. Both are already registered `AddScoped` in `Program.cs` from Story 10.1.

### Project Structure Notes

Modified files only — no new entities, migrations, or DI registrations:
- `api/Features/Insights/BudgetAlertDetector.cs` (stub → real implementation)
- `api/Features/Insights/InvoiceDeviationDetector.cs` (stub → real implementation)

New test files:
- `api.Tests/Features/Insights/BudgetAlertDetectorTests.cs`
- `api.Tests/Features/Insights/InvoiceDeviationDetectorTests.cs`

No changes to `ProcessInsightsFunction.cs` or its tests — the idempotency guard from Story 10.2 already applies uniformly to all four detectors (see Dev Notes).

No frontend changes — `InsightsTab.tsx`/`InsightCard.tsx` rendering of `Budget`/`InvoiceDeviation` card types is Story 10.4's scope. This story only produces the `Insight` rows; nothing consumes them client-side yet.

Follows `api/Features/{Feature}/` VSA slice convention — no new files outside the existing `Insights` slice and its test mirror.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-10-actionable-insights.md#Story 10.3] — epic ACs (verbatim basis for ACs #1–#6 above)
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md#FR-37, #FR-43] — budget pressure alert and invoice deviation hint requirements; Q-4/Q-8 resolved decisions (±10% threshold, `PlannedAnnualSpend` collected at Onboarding Step 2)
- [Source: api/Features/Insights/BudgetAlertDetector.cs, InvoiceDeviationDetector.cs] — existing stub contract (`DetectAsync(flatId, runId, ct)` signature, constructor shape) from Story 10.1
- [Source: api/Features/Dashboard/GetDashboardFunction.cs:29-48, KpiCalculator.cs:29-167] — `MeterReadings`/`Tariffs` query shape, period-accurate `ResolveTariff` pattern, per-interval clamped-delta costing style to mirror (simplified, no cost-gap bookkeeping needed)
- [Source: api/Data/Entities/Flat.cs, FlatConfiguration.cs:16-18, Onboarding/OnboardingValidator.cs:11-29, Flats/CreateFlatValidator.cs, Flats/PatchFlatValidator.cs] — `AnnualKwhBaseline` non-nullable/always-positive invariant; `PlannedAnnualSpend` genuinely nullable
- [Source: api/Features/Insights/ProcessInsightsFunction.cs:52-64] — Story 10.2's idempotency guard already covers this story's detectors, confirmed no further changes needed
- [Source: api/Features/Insights/InsightModels.cs] — `InsightsConstants.MessageJsonOptions`, `InsightDto`/`InsightsResponse` contracts
- [Source: _bmad-output/implementation-artifacts/10-2-standby-offender-and-replacement-candidate-detectors.md#Dev Notes] — `ResolveTariff` duplication convention, detector persistence contract, JSON serialization convention inherited from Story 10.2
- [Source: api.Tests/Features/Insights/ProcessInsightsFunctionTests.cs] — `MakeDb`/`SeedFlatAndRunAsync`/`MakeFunctionContext` seeding helper pattern to mirror and extend with reading/tariff seeding
- [Source: api.Tests/Features/Dashboard/KpiCalculatorTests.cs] — `MakeFlat`/`MakeReading`/`MakeTariff` helper pattern for reading/tariff test fixtures
- [Memory: Epic 9 retro / Epic 10 prep — `TariffResolver` already removed, don't recreate it]
- [Memory: Enum JSON serialization fix — confirms `InsightRunStatus`/`InsightType` enum serialization is already correctly wired project-wide; not a concern for this story]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

None — implementation proceeded without needing scratch debug logging; all test failures (none occurred) would have been iterated locally before this record was written.

### Completion Notes List

- Implemented `BudgetAlertDetector.DetectAsync` per AC #1–#3: anchor-based 30-day rolling window, period-accurate `ResolveTariff` cost accumulation (uncovered periods contribute 0, no uncovered-days bookkeeping per Dev Notes' simplification), `projectedAnnualCost = dailyAverageCost * 365m`, `Budget` `Insight` written only when `projectedAnnualCost > PlannedAnnualSpend`.
- Implemented `InvoiceDeviationDetector.DetectAsync` per AC #4–#6: anchor-based 60-day rolling window, clamped-delta `dailyAverageKwh`/`projectedAnnualKwh`, `deviation` computed against `flat.AnnualKwhBaseline` (no null-guard, confirmed always non-null/positive per Dev Notes), `InvoiceDeviation` `Insight` written only when `deviation >= 0.10m` and a current tariff resolves; `deviationPct` is the `x100` percentage while the `>= 0.10m` threshold check always operates on the raw ratio.
- The anchor-based window-resolution helper (`ResolveWindow`) is duplicated verbatim in both detector files per this codebase's established per-detector duplication convention (same as `ResolveTariff`) — no shared utility class was extracted.
- No changes made to `ProcessInsightsFunction.cs` or its tests — Story 10.2's idempotency guard already covers these two detectors uniformly, confirmed by reading `ProcessInsightsFunction.cs:52-64` and re-running `ProcessInsightsFunctionTests.cs` (all pass unmodified).
- Added `BudgetAlertDetectorTests.cs` (5 cases: over-budget insight with exact figures, within-budget no-insight, `PlannedAnnualSpend = null` skip, insufficient-window skip, period-predates-every-tariff exclusion without throwing) and `InvoiceDeviationDetectorTests.cs` (5 cases: +15% above/−12% below with exact figures, +8% below-threshold no-insight, insufficient-window skip, no-tariff-resolves skip). All window/deviation test figures are derived from a single captured `now` per test so the expected decimals are exact and independent of clock drift between test setup and detector execution.
- Full regression: `dotnet test api.Tests/api.Tests.csproj` → 425/425 passed (47 in the Insights feature). No lint step exists for the backend project beyond the build itself; `dotnet build` succeeds with 0 warnings/errors.

### File List

- `api/Features/Insights/BudgetAlertDetector.cs` (modified — stub to real implementation)
- `api/Features/Insights/InvoiceDeviationDetector.cs` (modified — stub to real implementation)
- `api.Tests/Features/Insights/BudgetAlertDetectorTests.cs` (new)
- `api.Tests/Features/Insights/InvoiceDeviationDetectorTests.cs` (new)

## Change Log

- 2026-07-26: Implemented `BudgetAlertDetector` and `InvoiceDeviationDetector` real detection logic (AC #1–#6); added `BudgetAlertDetectorTests.cs`/`InvoiceDeviationDetectorTests.cs` covering all cases in AC #7. Status moved to review.
- 2026-07-26: Code review round completed — 6 patches applied (see Review Findings below), 6 items deferred, 4 dismissed as noise; status moved to done.

### Review Findings

- [x] [Review][Patch] `ResolveWindow` doesn't guard against a near-zero actual window span, unlike the sibling `KpiCalculator.cs:55` (`if (totalDays < 1.0)`) [api/Features/Insights/BudgetAlertDetector.cs:48-49,76-90, api/Features/Insights/InvoiceDeviationDetector.cs:44-45,88-102] — `MeterReadings` has a unique index on `(FlatId, ReadingDate)` so an exact-zero span can't occur, but `ReadingDate` is a `DateTimeOffset` with no minimum-spacing validation between readings, so a near-zero (sub-day) span is reachable and would wildly inflate `projectedAnnualCost`/`projectedAnnualKwh` — exactly the failure mode `KpiCalculator`'s established floor exists to prevent. The `ResolveWindow` doc comment's claim that the span is "always >= windowDays" is also only true when the latest ingested reading coincides with `now`. **Fixed:** added `if (actualWindowDays < 1.0m) { skip }` mirroring `KpiCalculator`, and corrected both `ResolveWindow` doc comments.
- [x] [Review][Patch] No regression test for `ReplacementDetector`'s measured→approach fallback — the exact bug fixed in Story 10.2's own review round (`?? ComputeApproachAnnualKwh(device)` engaging when measured data is insufficient) has zero direct test coverage; confirmed no test in `ReplacementDetectorTests.cs` seeds a device with fewer than 7 distinct measured days plus a valid `EuLabel`/`SelfMeasured` config. **Fixed:** added `DetectAsync_MeasuredDataInsufficientWithValidEuLabelConfig_FallsBackToApproachAnnualKwh`.
- [x] [Review][Patch] `sprint-status.yaml` header comment contradicts the live field directly below it [_bmad-output/implementation-artifacts/sprint-status.yaml:2 vs :8] — comment says "Story 10.3 implementation complete — ready for review", live `last_updated:` field says "Story 10.3 story file created — ready for dev"; both contradict this same diff's `development_status: review` entry. **Fixed:** synced the live field to match reality.
- [x] [Review][Patch] No boundary test at exactly the 10% deviation threshold [api.Tests/Features/Insights/InvoiceDeviationDetectorTests.cs] — `InvoiceDeviationDetector` uses strict `deviation < 0.10m`, so exactly 10.0% triggers an insight, but this boundary is untested (only 15%, 12%, and 8% cases exist). **Fixed:** added `DetectAsync_DeviationExactlyTenPercentThreshold_WritesInsight`.
- [x] [Review][Patch] No test for the "flat not found" skip branch in either new detector [api.Tests/Features/Insights/BudgetAlertDetectorTests.cs, InvoiceDeviationDetectorTests.cs] — both detectors guard `if (flat is null ...)` but no test exercises a `flatId` resolving to no `Flat` row. **Fixed:** added `DetectAsync_FlatNotFound_SkipsWithNoInsight` to both test files.
- [x] [Review][Patch] Story 10.2 doc's Change Log has no entry documenting the review round completing [_bmad-output/implementation-artifacts/10-2-standby-offender-and-replacement-candidate-detectors.md] — `Status:` field reads `done` and a full `### Review Findings` section exists below it, but the only `## Change Log` entry predates that section and still said "status moved to review." **Fixed:** added a follow-up Change Log entry.
- [x] [Review][Defer] `ResolveTariff` (and its already-known arbitrary equal-`ContractStartDate` tie-break, previously deferred in Story 10.2's review) is now duplicated verbatim into two more files [api/Features/Insights/BudgetAlertDetector.cs:97, api/Features/Insights/InvoiceDeviationDetector.cs:109] — deferred, pre-existing pattern, six independent copies of the same tie-break logic now exist across the codebase.
- [x] [Review][Defer] `BudgetAlertDetector`'s cost average divides by the full window span while tariff-uncovered periods contribute 0 to the numerator [api/Features/Insights/BudgetAlertDetector.cs:35-46] — deferred, pre-existing/spec-acknowledged simplification; systematically under-estimates `projectedAnnualCost` for flats with incomplete tariff history, an intentional scope decision vs `KpiCalculator`'s uncovered-days bookkeeping per this story's Dev Notes.
- [x] [Review][Defer] `ComputeApproachAnnualKwh`'s `SelfMeasured` branch treats any non-`Weekly` `SelfMeasuredPeriod` as `Daily` via ternary rather than an exhaustive switch [api/Features/Insights/ReplacementDetector.cs:128] — deferred, safe today (enum has only `Weekly`/`Daily`) but would silently mis-annualize a future third enum member.
- [x] [Review][Defer] Redelivery test depends on `SeedFlatAndRunAsync()`'s current minimalism without asserting or documenting why [api.Tests/Features/Insights/ProcessInsightsFunctionTests.cs] — deferred, same class of vacuous-assertion risk this exact test's prior version was already fixed for once in Story 10.2's review round.
- [x] [Review][Defer] Potential race on overlapping `ProcessInsights` redelivery for the same `RunId` [api/Features/Insights/ProcessInsightsFunction.cs:59-64] — deferred, pre-existing architecture from Story 10.2's idempotency guard, not introduced by this story; no DB-level lease/lock serializes concurrent invocations.
- [x] [Review][Defer] `PowerPointConfiguration` has no unique constraint on `PlugId` — deferred, pre-existing schema gap unrelated to this diff (file not touched); two `PowerPoint`s could share a plug and double-count/misattribute smart-plug data across `StandbyDetector`/`ReplacementDetector`.

**Dismissed as noise (4):** `ReplacementDetector.ComputeMeasuredAnnualKwh`'s `.Last()` on rows grouped by date (verified `(FlatId, PlugId, Date)` has a DB unique index, so each group has exactly one row — no non-determinism possible); `AnnualKwhBaseline` divide-by-zero via a zero/negative value (verified all three write paths — `CreateFlatValidator`, `PatchFlatValidator`, `OnboardingValidator` — enforce `GreaterThan(0)`; no reachable code path persists a non-positive value, consistent with this codebase's validate-at-the-boundary convention); `ResolveWindow` duplicated between `BudgetAlertDetector`/`InvoiceDeviationDetector` (explicitly spec-mandated — Task 1 says "do not extract a shared utility class" — by design, not a defect); `SaveChangesAsync` called on early-return/skip paths with no pending tracked changes (explicitly spec-mandated, "mirroring `ReplacementDetector`'s early-return style," harmless with `AsNoTracking`, consistent across all four detectors).
