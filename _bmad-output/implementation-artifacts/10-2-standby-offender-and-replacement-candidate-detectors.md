---
baseline_commit: 0bec76568ed878dd8729416a6a190d2cde9b820b
---

# Story 10.2: Standby Offender & Replacement Candidate Detectors

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to be told when a specific device is drawing power outside its normal hours of use and when a high-consumption device could be replaced at a known payback, with exact device names and quantified euro figures,
So that I can take targeted action rather than guessing where to investigate.

## Acceptance Criteria

1. **`StandbyDetector.cs` — Eve Home standby detection.** Given a flat with Devices attached (1:1) to PowerPoints that have a `PlugId` with `SmartPlugIntervalData` rows, when a discovery run processes the flat: for each such device, the detector queries the last 30 days of `SmartPlugIntervalData` for the PowerPoint's `PlugId`; rows whose local wall-clock hour falls outside 22:00–08:00 are "out-of-use hours"; each row's Wh value is converted to an average watt figure assuming the Eve Home export's fixed ~10-minute interval cadence; a device is flagged when its mean out-of-use watt draw across those rows exceeds 2 W **and** interval data exists for at least 7 distinct calendar days in the window. For each offender, one `Insight` row is written: `Type = Standby`, `DeviceId` set, `Data` JSON = `{ "deviceName": string, "meanStandbyWatts": decimal, "estimatedMonthlyKwh": decimal, "estimatedMonthlyCost": decimal }`. Cost uses the flat's current active tariff (period-accurate `ResolveTariff` pattern, resolved for "today"). A device with no resolvable tariff is skipped (no Insight written) — no cost figure can be computed.

2. **`StandbyDetector.cs` — Meross-only devices excluded.** Given a flat with Devices whose PowerPoint has a `PlugId` but zero `SmartPlugIntervalData` rows for that `PlugId` (Meross export — daily aggregates only, no sub-daily resolution), when detecting standby offenders: those devices are excluded entirely — no `Insight` is created and no error is surfaced. This is an explicit format limitation (FR-35), not a failure condition.

3. **`StandbyDetector.cs` — insufficient data.** Given a device whose `SmartPlugIntervalData` (for its PowerPoint's `PlugId`, within the last 30 days) spans fewer than 7 distinct calendar days, when invoked: no standby `Insight` row is written for that device; the detector continues to the next device; `InsightRun` proceeds to the next detector regardless.

4. **`ReplacementDetector.cs` — replacement candidate detection.** Given a flat with Devices that have computable annual consumption (measured via a 1:1 `PowerPoint`+`SmartPlugDailyData`, or `ConsumptionApproach.EuLabel`, or `ConsumptionApproach.SelfMeasured`) and a non-empty, recognizable `EuLabelClass`, when a discovery run processes the flat: annual kWh/cost is computed per device (see Dev Notes for the exact per-source formula); devices are ranked by descending annual cost and the top 20% (`ceil(deviceCount × 0.2)`, minimum 1) are taken; among those, devices whose `EuLabelClass` normalizes to "C" or worse on the EU energy-label scale are flagged as replacement candidates. One `Insight` row is written per candidate: `Type = Replacement`, `Data` JSON = `{ "deviceName": string, "estimatedAnnualKwh": decimal, "estimatedAnnualCost": decimal, "suggestedClass": string, "estimatedSavingsEur": decimal }`. `suggestedClass` is one grade better than the device's current class on the ordered scale; `estimatedSavingsEur` is estimated from a fixed savings-per-class-step heuristic (see Dev Notes — this system has no per-device-category wattage-by-class reference table, so an exact target is not derivable). All decimal fields are `decimal` — no `float`/`double` anywhere in the calculation.

5. **`StandbyDetectorTests.cs` and `ReplacementDetectorTests.cs`** in `api.Tests/Features/Insights/`. Standby tests cover: Eve Home device with out-of-use mean draw above 2W over ≥7 days → `Insight` written with correct `Data`; device below 2W threshold → no `Insight`; Meross-only device (daily data, no interval data) → excluded with no error; device with <7 distinct days of interval data → no `Insight`; device with no resolvable tariff → no `Insight`; multi-device PowerPoint (smart strip) → excluded (see Dev Notes — per-device wattage cannot be isolated from strip-level interval data). Replacement tests cover: high-consumption device with `EuLabelClass = "C"` (and legacy `"A+++"`-style values) → `Insight` with correct `suggestedClass`/savings; `EuLabelClass = "A"` or `"B"` → no `Insight`; unrecognized/blank `EuLabelClass` → excluded, no error; device outside the top-20% consumption band → no `Insight`; no devices with a computable consumption approach → no `Insight`s; each of the three consumption sources (measured, EU label, self-measured) produces a correct annual kWh figure.

6. **Gap found during story creation — idempotency guard against duplicate detector writes on queue redelivery.** `ProcessInsightsFunction.cs` (`api/Features/Insights/ProcessInsightsFunction.cs:43-46`) commits `run.Status = Processing` **before** invoking the four detectors. If the Functions host is killed mid-run after some/all `Insight` rows have been written, Azure's at-least-once queue-trigger delivery re-invokes `RunAsync` with the same message, and nothing today prevents the (now real, as of this story) `StandbyDetector`/`ReplacementDetector` writes from duplicating. This was deferred out of Story 10.1 with `blocks: Story 10.2, Story 10.3` (`_bmad-output/implementation-artifacts/deferred-work.md`) because the guard couldn't be designed until a real detector write pattern existed — it exists now. Given a discovery message is dequeued for `runId` (first attempt or a redelivery), when `ProcessInsightsFunction.RunAsync` begins processing (immediately after loading `run`, before setting `Status = Processing` and before any detector runs), then it deletes any pre-existing `Insight` rows where `Insight.RunId == runId`, and commits that delete via `SaveChangesAsync` before the four detectors are invoked. This makes detector writes idempotent under redelivery: any partial write from a killed-mid-run previous attempt is cleared before the detectors run again, so no duplicate `Insight` rows can result no matter how many times the message is redelivered. `BudgetAlertDetector`/`InvoiceDeviationDetector` (Story 10.3, still no-op today) get this guard for free since it lives in the shared `ProcessInsightsFunction`, not per-detector.

## Tasks / Subtasks

- [x] Task 1: `StandbyDetector.cs` real implementation (AC: #1, #2, #3)
  - [x] Query `Rooms` (`AsNoTracking`, `Include(r => r.PowerPoints).ThenInclude(pp => pp.Devices)`) for the flat, exactly like `DecompositionEngine.ComputeAsync` — mirror this pattern, don't invent a new one
  - [x] For each `PowerPoint` with a non-null `PlugId` and exactly 1 `Device` (single-device attribution — see Dev Notes on why smart strips are excluded): query `SmartPlugIntervalData` for that `PlugId` within the last 30 days
  - [x] Classify Eve Home vs Meross by presence/absence of rows in the query result — do not add a new "plug type" column or field anywhere
  - [x] Compute distinct calendar-day count from `Timestamp` (already local wall-clock with a synthetic zero offset — see Dev Notes, no timezone conversion); skip device if < 7 distinct days
  - [x] Filter rows to `Timestamp.Hour >= 22 || Timestamp.Hour < 8` (out-of-use); convert each row's `WhValue` to watts via the 10-minute-interval assumption; compute the mean; skip device if mean ≤ 2 W
  - [x] Resolve the flat's current tariff via the in-memory `ResolveTariff` helper (duplicate verbatim from `KpiCalculator.cs`/`DecompositionEngine.cs` per this codebase's established per-engine duplication convention — do not extract a shared utility, do not recreate the deleted `TariffResolver` class); skip device if no tariff resolves
  - [x] Compute `estimatedMonthlyKwh`/`estimatedMonthlyCost` from mean watts × out-of-use window duration × 30 days (see Dev Notes for the exact formula)
  - [x] Serialize `{ deviceName, meanStandbyWatts, estimatedMonthlyKwh, estimatedMonthlyCost }` (camelCase) and add an `Insight` row (`Type = Standby`, `DeviceId` set, `FlatId`, `RunId`, `CreatedAt = DateTimeOffset.UtcNow`)
  - [x] `SaveChangesAsync(ct)` at the end of `DetectAsync` (detector persists directly, per Story 10.1's established contract)
- [x] Task 2: `ReplacementDetector.cs` real implementation (AC: #4)
  - [x] Same `Rooms`/`PowerPoints`/`Devices` query shape as Task 1
  - [x] Compute annual kWh per device across all 3 sources per the Dev Notes formula (measured via single-device `PowerPoint`+`SmartPlugDailyData`; `EuAnnualKwh` directly for `EuLabel`; `SelfMeasuredKwh` × 365/52 for `SelfMeasured`); skip devices with `ConsumptionApproach.None` and no measured data
  - [x] Resolve current tariff (same helper as Task 1) to get `estimatedAnnualCost = annualKwh × PricePerKwh`; skip device if unresolvable
  - [x] Rank all devices with a computable annual cost descending; take `ceil(count × 0.2)` (minimum 1) as the top-20% band
  - [x] Normalize each candidate's `EuLabelClass` against the ordered scale (see Dev Notes); among the top-20% band, flag devices whose normalized class is "C" or worse; unrecognized/blank classes are excluded, not errors
  - [x] Compute `suggestedClass` (one step better) and `estimatedSavingsEur` (fixed per-step heuristic, see Dev Notes)
  - [x] Serialize `{ deviceName, estimatedAnnualKwh, estimatedAnnualCost, suggestedClass, estimatedSavingsEur }` (camelCase) and add an `Insight` row (`Type = Replacement`, `DeviceId` set)
  - [x] `SaveChangesAsync(ct)` at the end of `DetectAsync`
- [x] Task 3: Idempotency guard in `ProcessInsightsFunction.cs` (AC: #6)
  - [x] Immediately after loading `run` (and confirming it's non-null) and before `run.Status = Processing`, delete any existing `db.Insights` rows where `RunId == discoveryMessage.RunId`
  - [x] `SaveChangesAsync(ct)` for the delete before proceeding to set `Status = Processing` and invoke detectors
- [x] Task 4: Backend tests (AC: #5)
  - [x] `StandbyDetectorTests.cs` — cases listed in AC #5; mirror `DecompositionEngineTests.cs`'s `MakeDb`/`SeedRoomAsync`/`SeedPowerPointAsync`/`SeedDeviceAsync` seeding helpers, add a `SeedIntervalRowAsync` helper for `SmartPlugIntervalData`
  - [x] `ReplacementDetectorTests.cs` — cases listed in AC #5, covering all 3 consumption sources and the class-normalization edge cases
  - [x] Extend `ProcessInsightsFunctionTests.cs` with a redelivery test: seed a pre-existing `Insight` row for a `runId`, re-invoke `RunAsync` with a message for that same `runId`, assert the pre-existing row is gone and only the new detector run's rows remain

## Dev Notes

### Critical corrections / clarifications (verified against current code — the epic text underspecifies these)

- **`PlugId` lives on `PowerPoint`, not `Device`.** The epic's ACs say "Devices linked to Eve Home plugs" — there is no `Device.PlugId`. The real relationship is `PowerPoint.PlugId` → `PowerPoint.Devices`. Use the exact same `Rooms.Include(PowerPoints).ThenInclude(Devices)` query `DecompositionEngine.ComputeAsync` already uses (`api/Features/Decomposition/DecompositionEngine.cs:49-53`).
- **No "plug type" field exists anywhere.** Eve Home vs Meross is not stored — it's inferred structurally: `EveHomeParser.cs` writes to `SmartPlugIntervalData` (sub-daily), `MerossParser.cs` writes only to `SmartPlugDailyData` (daily aggregates). A device's `PlugId` having zero `SmartPlugIntervalData` rows but some `SmartPlugDailyData` rows means Meross — exclude it. Do not add a new column or enum to distinguish plug source.
- **`SmartPlugIntervalData.Timestamp` is already local wall-clock time.** `EveHomeParser.cs:168` explicitly strips any real UTC/offset information and re-wraps the parsed wall-clock value with a synthetic `TimeSpan.Zero` offset — it is *not* a real UTC instant. Do **not** apply `TimeZoneInfo.ConvertTime`/`AppTimeZone` to it (unlike `MeterReading.ReadingDate`, which genuinely needs that conversion in `KpiCalculator`/`DecompositionEngine`/`ReconciliationEngine`). Just read `.Hour` directly for the 22:00–08:00 window check.
- **Interval cadence is assumed, not stored.** Neither `SmartPlugIntervalData` nor `ImportJob` records the interval length. The PRD (`prd.md:430`) and this codebase's Eve Home format description both describe "~10-minute interval records." Hardcode a `private const int IntervalMinutes = 10;` in `StandbyDetector` and convert `watts = whValue * (60m / IntervalMinutes)` per row — do not attempt to infer interval length from consecutive `Timestamp` deltas; that's unnecessary complexity for a format with a documented fixed cadence.
- **The "configured usage window" is not actually configurable anywhere.** The epic AC says "the flat's configured usage window (default 22:00–08:00 local time)" but `Flat.cs` has no such field, and no story (including this epic's remaining Story 10.3/10.4) adds one. Treat 22:00–08:00 as a hardcoded constant (`private const int UsageWindowStartHour = 22; private const int UsageWindowEndHour = 8;`) in `StandbyDetector`. Do **not** add a migration/column for this — it is out of scope and unspecced.
- **Smart power strips (multi-device `PowerPoint`s) are excluded from both detectors' measured-data branch.** `SmartPlugIntervalData`/`SmartPlugDailyData` rows are keyed by `PlugId` (the whole strip), not per-device. `DecompositionEngine` handles this for *kWh attribution* via a weighted-share algorithm (`BuildSmartStripDecomposition`) — but that algorithm distributes an already-known *daily total* across devices using their configured `ConsumptionApproach` as a weight; it cannot recover a *per-device, per-interval* watt reading, which is what standby detection needs (AC #1's "mean out-of-use watt draw" is fundamentally a per-device figure). Replicating `BuildSmartStripDecomposition` here would misattribute standby draw to whichever device happens to get the largest weight. Scope this story to `PowerPoint.Devices.Count == 1` only, exactly like `DecompositionEngine`'s own `Measured` branch condition (`pp.PlugId is not null && pp.Devices.Count == 1`). Multi-device PowerPoints are silently skipped by both detectors — not an error, no Insight, same as any other ineligible device.
- **`EuLabelClass` is free text, not an enum — this is the single biggest landmine in this story.** `Device.EuLabelClass` (`api/Data/Entities/Device.cs`) is `string?`, validated only by `MaximumLength(200)` in `UpdateFlatStructureValidator.cs:27` — no allowed-value constraint anywhere, frontend or backend. `DeviceEditor.tsx:210-218` is a plain `<input type="text">` with placeholder `"e.g. A+++"` (`client/src/locales/en-US/flat-structure.json:53`), confirming users are expected to type both the legacy pre-2021 scale (`A+++`/`A++`/`A+`/`A`/`B`/`C`/`D`) and the modern EU 2021 rescale (`A`–`G`). Per FR-30 (`prd.md:371-372`), `EuLabelClass` is explicitly optional even for `EuLabel`-approach devices ("recorded for potential future use such as replacement-candidate detection" — this story is that future use). **Implementation decision made at story-creation time** (no existing precedent to follow — flag to the user if a different scale/ordering is preferred): normalize by trimming whitespace and uppercasing, then match against this fixed ordered list (best→worst, index = rank):
  `A+++`(0), `A++`(1), `A+`(2), `A`(3), `B`(4), `C`(5), `D`(6), `E`(7), `F`(8), `G`(9)
  "C or below" = rank ≥ 5. "One class better" = rank − 1 (a device already at `A+++` can never be a replacement candidate under this scale, so this edge doesn't need clamping in practice — rank ≥ 5 devices always have a valid rank − 1). Anything that doesn't match exactly (empty, null, "class C", "c-rated", typos) is **not classified** — the device is excluded from replacement detection silently, same treatment as an unconfigured `ConsumptionApproach`. Do not throw, log as error, or attempt fuzzy matching.
- **`estimatedSavingsEur` heuristic — also a story-creation-time product decision, not derivable from existing data.** There is no per-device-category wattage-by-EU-class reference table anywhere in this system (and building one is out of scope for this story). Use a fixed **15% of `estimatedAnnualCost`** as the savings estimate for one class-step improvement (`private const decimal SavingsPerClassStepPercent = 0.15m;`). This is a simplification, not a real energy-efficiency-standards figure — flag to the user post-implementation if a different multiplier is wanted; it's an isolated constant, trivial to change later.
- **"Top 20%" population and rounding.** Rank *all* devices in the flat with a computable annual cost (regardless of `EuLabelClass`) descending by `estimatedAnnualCost`. Band size = `Math.Max(1, (int)Math.Ceiling(count * 0.2m))`. This avoids a flat with only 1–4 devices always producing zero candidates from truncation. Apply the `EuLabelClass` "C or below" filter only within that band, per the epic AC's literal wording ("devices in the top 20% of consumption **whose** EU label class is C or below").
- **Replacement's annual-kWh source formulas** (mirrors `DecompositionEngine.EstimateDailyKwh`/`ResolveStandaloneApproach`, `api/Features/Decomposition/DecompositionEngine.cs:226-241`, plus a new measured-annualization rule for this story):
  - Measured (`PowerPoint.PlugId` not null, exactly 1 `Device`): average daily kWh from the last 30 days of `SmartPlugDailyData` for that `PlugId`, × 365. Skip (not computable) if fewer than 7 distinct dates of daily data exist in that window — same 7-day floor as `StandbyDetector`, for consistency within this story.
  - `ConsumptionApproach.EuLabel`: `device.EuAnnualKwh` directly — it's already an annual figure, no extrapolation.
  - `ConsumptionApproach.SelfMeasured`: `device.SelfMeasuredKwh * (SelfMeasuredPeriod == Weekly ? 52m : 365m)`.
  - `ConsumptionApproach.None` with no `PlugId`/measured data: not computable, excluded silently.
- **Standby's `estimatedMonthlyKwh`/`estimatedMonthlyCost` formula.** Represents the waste from the device staying on during the out-of-use window specifically (the actionable "turn this off / unplug it" quantity), not a 24/7 extrapolation: `estimatedMonthlyKwh = (meanStandbyWatts / 1000m) * outOfUseHoursPerDay * 30m`, where `outOfUseHoursPerDay = 10` (22:00→08:00, derived from the two window constants, don't hardcode `10` separately from `UsageWindowStartHour`/`UsageWindowEndHour`). `estimatedMonthlyCost = estimatedMonthlyKwh * tariff.PricePerKwh`.
- **`ResolveTariff` — duplicate verbatim, do not recreate `TariffResolver`.** `api/Shared/TariffResolver.cs` was deleted in the Epic 9 retrospective cleanup and does not exist. The live pattern (confirmed in `KpiCalculator.cs:155-164` and `DecompositionEngine.cs:246-257`) is an identical private static `ResolveTariff(IReadOnlyList<Tariff> tariffs, DateTimeOffset date)` method, duplicated per engine per this codebase's established convention (`[Project context: Data integrity invariants]`). Resolve for `DateTimeOffset.UtcNow` ("current active tariff" per the epic AC, not a historical date range like `DecompositionEngine`'s day-by-day costing).
- **JSON serialization for `Insight.Data`.** Reuse the existing `internal static InsightsConstants.MessageJsonOptions` (camelCase, defined in `api/Features/Insights/InsightModels.cs`) rather than declaring a fourth ad-hoc `JsonSerializerOptions` instance in this feature folder (10.1's code review already flagged 3 duplicate instances as a minor finding) — it's `internal`, so both detectors (same `EnergyTracker.Api.Features.Insights` namespace) can reference it directly. Define small private records per detector, e.g. `private record StandbyInsightData(string DeviceName, decimal MeanStandbyWatts, decimal EstimatedMonthlyKwh, decimal EstimatedMonthlyCost);`, and `JsonSerializer.Serialize(data, InsightsConstants.MessageJsonOptions)` for the `Insight.Data` string.
- **Detector persistence contract (established in Story 10.1, not this story's decision).** Detectors call `db.Insights.Add(...)` and `SaveChangesAsync(ct)` themselves — `ProcessInsightsFunction` does not persist on their behalf. `DetectAsync(Guid flatId, Guid runId, CancellationToken ct)` signature is locked; do not change it (`ProcessInsightsFunction`'s four call sites depend on it exactly as-is).

### Idempotency guard placement (AC #6)

Add the delete-then-write guard directly to `ProcessInsightsFunction.cs`, right after the existing `run is null` early-return check and before `run.Status = InsightRunStatus.Processing;`:

```csharp
var staleInsights = await db.Insights.Where(i => i.RunId == discoveryMessage.RunId).ToListAsync(ct);
if (staleInsights.Count > 0)
{
    db.Insights.RemoveRange(staleInsights);
    await db.SaveChangesAsync(ct);
}
```

Use load-then-`RemoveRange`, **not** `ExecuteDeleteAsync`/`ExecuteUpdateAsync` — those `Microsoft.EntityFrameworkCore.RelationalQueryableExtensions` methods are relational-provider-only and throw at runtime against the EF Core InMemory provider this codebase's entire test suite uses (`api.Tests` has zero existing usage of either — don't be the first). This is a rare, low-volume per-run cleanup (at most a handful of `Insight` rows per flat), so the extra round-trip from materializing before deleting is not a real cost concern here.

### Project Structure Notes

Modified files only — no new entities, migrations, or DI registrations (all four detectors were already registered `AddScoped` in `Program.cs` in Story 10.1):
- `api/Features/Insights/StandbyDetector.cs` (stub → real implementation)
- `api/Features/Insights/ReplacementDetector.cs` (stub → real implementation)
- `api/Features/Insights/ProcessInsightsFunction.cs` (idempotency guard, AC #6)

New test files:
- `api.Tests/Features/Insights/StandbyDetectorTests.cs`
- `api.Tests/Features/Insights/ReplacementDetectorTests.cs`

Modified test file:
- `api.Tests/Features/Insights/ProcessInsightsFunctionTests.cs` (redelivery/idempotency test, AC #6)

No frontend changes — `InsightsTab.tsx`/`InsightCard.tsx` rendering of `Standby`/`Replacement` card types is Story 10.4's scope. This story only produces the `Insight` rows; nothing consumes them client-side yet.

Follows `api/Features/{Feature}/` VSA slice convention — no new files outside the existing `Insights` slice and its test mirror.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-10-actionable-insights.md#Story 10.2] — epic ACs (verbatim basis for ACs #1–#5 above)
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md#FR-30, #FR-35, #FR-36] — EU label optionality, standby detection scope (Eve Home only), replacement candidate requirements
- [Source: _bmad-output/implementation-artifacts/deferred-work.md, "code review of story-10.1" entry] — idempotency guard deferral, `blocks: Story 10.2, Story 10.3` (basis for AC #6)
- [Source: api/Features/Decomposition/DecompositionEngine.cs] — `Rooms.Include(PowerPoints).ThenInclude(Devices)` query shape, single-device vs smart-strip `Measured` branch condition, `EstimateDailyKwh`/`ResolveStandaloneApproach`, `ResolveTariff` duplication pattern
- [Source: api/Features/SmartPlugImport/EveHomeParser.cs:168, 108-182] — `SmartPlugIntervalData.Timestamp` is local wall-clock with synthetic zero offset, not real UTC; Eve Home vs Meross structural distinction (interval vs daily-only tables)
- [Source: api/Data/Entities/Device.cs, api/Features/FlatStructure/UpdateFlatStructureValidator.cs:27] — `EuLabelClass` is unconstrained free text (`MaximumLength(200)` only)
- [Source: client/src/features/flat-structure/components/DeviceEditor.tsx:206-219, client/src/locales/en-US/flat-structure.json:52-53] — EU label class is a plain text input, placeholder `"e.g. A+++"` confirms the legacy pre-2021 scale is in active use
- [Source: api/Features/Insights/ProcessInsightsFunction.cs, InsightModels.cs] — detector call sites, `DetectAsync` signature contract, `InsightsConstants.MessageJsonOptions`
- [Source: _bmad-output/implementation-artifacts/10-1-insights-infrastructure-data-model-run-tracking-schedule-and-api.md#Dev Notes] — detector stub contract, `TariffResolver` deletion, tenant/error-response/JSON conventions inherited from Story 10.1
- [Source: api.Tests/Features/Decomposition/DecompositionEngineTests.cs] — `MakeDb`/`SeedRoomAsync`/`SeedPowerPointAsync`/`SeedDeviceAsync` test seeding helper pattern to mirror
- [Memory: Epic 9 retro / Epic 10 prep — `TariffResolver` already removed, don't recreate it]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

None — implementation proceeded without needing a debug log; `dotnet build`/`dotnet test` output confirmed correctness at each step.

### Completion Notes List

- `StandbyDetector.cs`: implemented Eve Home standby detection exactly per AC #1–#3 and the Dev Notes formulas. Single-device `PowerPoint`+`PlugId` attribution only (smart strips silently skipped); Meross-only plugs (zero `SmartPlugIntervalData` rows) excluded without error; 7-distinct-day floor computed over all interval rows in the 30-day window (not just out-of-use rows); 22:00–08:00 out-of-use window and 2 W threshold hardcoded as constants; `ResolveTariff` duplicated verbatim from `KpiCalculator.cs`/`DecompositionEngine.cs` per established convention.
- `ReplacementDetector.cs`: implemented replacement-candidate detection per AC #4. Per-device annual kWh computed across all 3 sources (measured single-device plug, `EuLabel`, `SelfMeasured`); `EuLabelClass` normalized against the fixed `A+++`...`G` ordered scale (trim + uppercase, exact match only — unrecognized/blank silently excluded); top-20% band computed over *all* devices with a computable annual cost (`ceil(count × 0.2)`, min 1) before the "C or worse" filter is applied within that band; `suggestedClass` = one step better on the scale; `estimatedSavingsEur` = 15% of `estimatedAnnualCost` (fixed heuristic per Dev Notes — flagging here per story instructions in case a different multiplier is wanted later).
- `ProcessInsightsFunction.cs`: added the idempotency guard exactly as specified in Dev Notes — load-then-`RemoveRange` (not `ExecuteDeleteAsync`, which the EF Core InMemory provider used by the whole test suite doesn't support) of any pre-existing `Insight` rows for the `RunId`, committed before `Status = Processing` and before any detector runs.
- Tests: added `StandbyDetectorTests.cs` (6 cases) and `ReplacementDetectorTests.cs` (9 cases) covering every scenario listed in AC #5, plus a redelivery/idempotency test in `ProcessInsightsFunctionTests.cs`. Full backend suite: 412/412 passing, no regressions. `dotnet format --verify-no-changes` confirms none of the files touched in this story introduce new formatting violations (pre-existing drift elsewhere in the codebase is untouched, out of scope).
- No frontend changes — out of scope per Dev Notes (Story 10.4).

### File List

- `api/Features/Insights/StandbyDetector.cs` (modified — stub → real implementation)
- `api/Features/Insights/ReplacementDetector.cs` (modified — stub → real implementation)
- `api/Features/Insights/ProcessInsightsFunction.cs` (modified — idempotency guard)
- `api.Tests/Features/Insights/StandbyDetectorTests.cs` (new)
- `api.Tests/Features/Insights/ReplacementDetectorTests.cs` (new)
- `api.Tests/Features/Insights/ProcessInsightsFunctionTests.cs` (modified — redelivery test)

## Change Log

- 2026-07-25: Implemented `StandbyDetector`/`ReplacementDetector` real detection logic, added the `ProcessInsightsFunction` idempotency guard, and added corresponding backend tests. All tasks/ACs complete; status moved to review.
- 2026-07-26: Code review round completed — 7 patches applied (see Review Findings below), 1 item deferred; status moved to done.

### Review Findings

- [x] [Review][Patch] `ReplacementDetector` has no fallback from measured to configured-approach data when measured data is insufficient — `DetectAsync` (`api/Features/Insights/ReplacementDetector.cs:52-64`) treats `isSingleDeviceMeasured` as exclusive: if `ComputeMeasuredAnnualKwhAsync` returns `null` (fewer than 7 distinct days of `SmartPlugDailyData`), the device is excluded entirely even if it also has a valid `ConsumptionApproach.EuLabel`/`SelfMeasured` configuration that could compute an annual kWh figure. **Resolved:** fall back to `ComputeApproachAnnualKwh(device)` when measured data is insufficient, instead of excluding the device outright. Fixed.

- [x] [Review][Resolved-No-Change] Multi-device (smart-strip) `PowerPoint` devices remain eligible for `ReplacementDetector` via `EuLabel`/`SelfMeasured` [api/Features/Insights/ReplacementDetector.cs:54-64] — re-examined against AC #4's literal wording ("measured via a **1:1** PowerPoint+SmartPlugDailyData, or `ConsumptionApproach.EuLabel`, or `ConsumptionApproach.SelfMeasured`"): the 1:1 single-device constraint is only stated for the *measured* source. Dev Notes' bullet is titled "excluded from both detectors' **measured-data branch**" — for `StandbyDetector` that's a full exclusion (measured is its only source), but for `ReplacementDetector` it only rules out the measured branch, not the two independent approach-based sources. **Decision:** keep current behavior — multi-device PowerPoint devices stay eligible via `EuLabel`/`SelfMeasured`. Added `DetectAsync_MultiDevicePowerPoint_StillEligibleViaEuLabel` to `ReplacementDetectorTests.cs` to lock in this intentional behavior.
- [x] [Review][Patch] Idempotency-guard delete is not covered by the run's failure-handling try/catch [api/Features/Insights/ProcessInsightsFunction.cs:55-60] — the new `db.Insights.RemoveRange(staleInsights); await db.SaveChangesAsync(ct);` block sits between the `run is null` check and the `try { run.Status = Processing; ... }` block. If this `SaveChangesAsync` throws, the exception propagates uncaught: `run.Status` is never set to `Failed`, no `logger.LogError` fires for this run, unlike every other failure path in `RunAsync`. **Fixed:** moved the guard inside the existing try block.
- [x] [Review][Patch] Redelivery test doesn't verify Task 4's literal requirement ("only the new detector run's rows remain") [api.Tests/Features/Insights/ProcessInsightsFunctionTests.cs:164-183] — `RunAsync_RedeliveredMessage_ClearsStaleInsightsBeforeReprocessing` seeds a flat via `SeedFlatAndRunAsync()`, which creates no `Rooms`/`Tariffs`, so both real detectors necessarily write zero rows on the redelivered run. The assertion `remaining.ShouldNotContain(i => i.InsightId == staleInsight.InsightId)` passes vacuously against an empty list rather than verifying coexistence with genuinely new rows from the redelivered run. **Fixed:** added a `WritingStandbyDetector` test double and renamed the test to `...ClearsStaleInsightsAndKeepsOnlyNewRun`, asserting exactly one remaining row that isn't the stale one.
- [x] [Review][Patch] Non-deterministic tie-break in top-20%-band selection [api/Features/Insights/ReplacementDetector.cs:68-71] — `candidates.OrderByDescending(c => c.AnnualCost).Take(bandSize)` has no secondary sort key; devices tied exactly on `AnnualCost` at the band boundary are chosen based on unspecified enumeration order rather than a deterministic rule. **Fixed:** added `.ThenBy(c => c.Device.DeviceId)`.
- [x] [Review][Patch] N+1 query pattern in both detectors [api/Features/Insights/StandbyDetector.cs:35-38, api/Features/Insights/ReplacementDetector.cs:56-57] — one `SmartPlugIntervalData`/`SmartPlugDailyData` query is issued per `PowerPoint` inside the device loop instead of batching all relevant `PlugId`s into a single query. Low priority — real-world per-flat device counts are small — but worth a follow-up if flats scale up. **Fixed:** both detectors now batch-fetch all relevant `PlugId`s in a single query and group in memory.
- [x] [Review][Patch] Missing test coverage for `EuLabelClass = "A"` [api.Tests/Features/Insights/ReplacementDetectorTests.cs] — AC #5 names both `"A"` and `"B"` should write no `Insight`, but `DetectAsync_ClassAOrB_WritesNoInsight` only seeded `euLabelClass: "B"`. **Fixed:** converted to a `[Theory]` covering both `"A"` and `"B"`.
- [x] [Review][Patch] Missing test coverage for `SelfMeasuredPeriod` non-Weekly branch [api.Tests/Features/Insights/ReplacementDetectorTests.cs] — `ComputeApproachAnnualKwh`'s `SelfMeasured` formula has two branches (`× 52` for Weekly, `× 365` otherwise); only the Weekly branch had a test. **Fixed:** added `DetectAsync_SelfMeasuredDailySource_ComputesCorrectAnnualKwh`.
- [x] [Review][Defer] `ResolveTariff`'s tie-break on equal `ContractStartDate` is arbitrary [api/Features/Insights/StandbyDetector.cs:60-70, api/Features/Insights/ReplacementDetector.cs:135-144] — deferred, pre-existing pattern duplicated verbatim from `KpiCalculator.cs`/`DecompositionEngine.cs` per this story's explicit Dev Notes instruction not to change it; the same latent tie-break issue already exists in both source implementations.

**Dismissed as noise (7):** EU label scale conflating legacy/modern grades into one ordered array (explicit story-creation-time Dev Notes decision, already flagged to the user in the spec itself); `estimatedSavingsEur` 15% heuristic "uncertain" (explicitly spec-mandated value, already flagged in Dev Notes); `MinDistinctDays = 7` reused in `ReplacementDetector` (explicitly mandated by Dev Notes line 76, "same 7-day floor as StandbyDetector, for consistency"); no rounding applied to decimal monetary/energy fields before storage (presentation-layer concern, explicitly out of scope — Story 10.4); no logging around the idempotency-clear path (not spec-required, consistent with other silent-skip paths in these detectors); `StandbyDetector` re-resolving the tariff on every loop iteration instead of once (correctness-neutral inefficiency, trivial style nit); Blind Hunter's "test files not present in diff to verify 412/412 claim" (artifact of the intentionally diff-only review scope, not a real issue).
