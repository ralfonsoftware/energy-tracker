# Investigation: Cost-gap warning badge shown despite "185 of 185 days" full coverage

## Hand-off Brief

1. **What happened.** The dashboard shows a ⚠ "Tarif deckt 185 von 185 Tagen" badge (implying full
   tariff coverage) alongside the cost figures, even though a fully-covering gap warning should only
   render when coverage is actually incomplete — the rounded numbers shown to the user say 100%
   covered while the underlying flag that triggers the badge disagrees.
2. **Where the case stands. CONCLUDED.** Root cause Confirmed and bit-exact reproduced with this
   user's real data: `KpiCalculator.cs`'s `HasCostGap` flag compares raw (unrounded) day counts,
   and .NET's `TimeSpan.TotalDays` is implemented as `ticks * DaysPerTick` (multiplication by a
   precomputed reciprocal, not division). Summing two such multiplications (`coveredDays`) versus
   one multiplication of the combined ticks (`totalDays`) is a classic floating-point
   non-associativity case: for this user's actual reading timestamps, `coveredDays` lands
   `2.8e-14` days (~2.5 microseconds) below `totalDays`, tripping the strict `<` comparison to
   `true` even though real-world coverage is 100%. Both values still round to 185 via
   `Math.Ceiling`, producing the exact "185 of 185, but still warned" contradiction observed.
3. **What's needed next.** No further diagnosis needed. Apply the fix direction below (make
   `HasCostGap` agree with the same rounded/derived values used for display) and add a regression
   test using non-day-aligned reading timestamps (see Reproduction Plan) — existing tests all use
   exact-midnight, exact-day-multiple timestamps, which never exposes this rounding artifact.

## Case Info

| Field            | Value                                                                                  |
| ---------------- | --------------------------------------------------------------------------------------- |
| Ticket           | N/A (user-reported, screenshot)                                                          |
| Date opened      | 2026-07-04                                                                                |
| Status           | Concluded                                                                                 |
| System           | Production PWA, `ker.ralfonsoftware.de`, dashboard for flat "Zuhause"                    |
| Evidence sources | Frontend source (`client/src/features/dashboard`), backend source (`api/Features/Dashboard`, `api/Shared/TariffResolver.cs`), existing xUnit tests (`api.Tests/Features/Dashboard`) |

## Problem Statement

User: "Ich habe einen Tarif, der seit 01.10.2024 gültig ist und trotzdem bekomme ich einen Hinweis,
dass 185 von 185 Tage abgedeckt sind" — a tariff has been valid since 2024-10-01, yet the app still
shows a coverage-gap warning reading "185 of 185 days covered." Screenshot shows the ⚠ badge next to
Tagesdurchschnitt (1,46 €), Wochendurchschnitt (10,25 €), and Hochgerechneter Monat (43,96 €) tiles,
all displaying "TARIF DECKT 185 VON 185 TAGEN."

## Timeline of Events

| Time                     | Event                                                                 | Source                          | Confidence |
| ------------------------ | ---------------------------------------------------------------------- | -------------------------------- | ---------- |
| 2024-10-01T00:00:00Z     | Tariff "Erdgas SüdWest" `ContractStartDate`                            | `Tariffs` table                  | Confirmed  |
| 2025-12-31T10:08:00Z     | Earliest `MeterReading` for this flat (6405.0000 kWh)                  | `MeterReadings` table            | Confirmed  |
| 2026-07-01 – 2026-07-03  | Story 3.4/3.5/4.4 commits touch `KpiCalculator.cs`; `686a2f9` renames `EffectiveDate`→`ContractStartDate` and drops the old column via migration `20260703114416` | `git log` | Confirmed  |
| 2026-07-03T10:10:00Z     | Second `MeterReading` (7358.0000 kWh)                                  | `MeterReadings` table            | Confirmed  |
| 2026-07-04T09:29:00Z     | Third/latest `MeterReading` (7363.0000 kWh) — shown as "11:29" local (CEST, UTC+2) | `MeterReadings` table | Confirmed  |
| 2026-07-04T11:34 (local) | User views dashboard, sees ⚠ "Tarif deckt 185 von 185 Tagen" badge next to correct-looking cost figures | User screenshot | Confirmed  |

## Evidence Inventory

| Source                                                    | Status    | Notes                                                                 |
| ---------------------------------------------------------- | --------- | ---------------------------------------------------------------------- |
| `client/src/features/dashboard/components/CostGapBadge.tsx` | Available | Renders ⚠ + `costGap.badgeLabel` unconditionally when mounted        |
| `client/src/features/dashboard/components/DashboardGrid.tsx` | Available | `resolveCostDisplay` — decides when `CostGapBadge` is mounted          |
| `api/Features/Dashboard/KpiCalculator.cs`                   | Available | Computes `HasCostGap`, `CoveredDays`, `TotalDays`                      |
| `api/Features/Dashboard/GetDashboardFunction.cs`             | Available | Confirms ALL readings/tariffs for the flat are loaded, unfiltered      |
| `api/Shared/TariffResolver.cs`                              | Available | Canonical per-project-context tariff resolution (DB-query form)        |
| `api.Tests/Features/Dashboard/KpiCalculatorTests.cs`        | Available | Covers large, obvious gaps; no test for a sub-day/rounding-masked gap  |
| Production DB rows (this user's `Tariff`/`MeterReading` records) | Available (provided by user) | 3 `MeterReading` rows, 1 `Tariff` row for flat `d3155f5b-e946-444c-adeb-08ded6762ffa` |
| `git log` on `KpiCalculator.cs` and the tariff schema             | Available | Commit `686a2f9` (2026-07-03) renamed `EffectiveDate`→`ContractStartDate`; ruled out as contributing cause once actual DB values were checked (see Deduction 2) |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | ---------------- | -------- | ------ | ----- |
| 1 | Query this flat's tariffs + earliest/latest `MeterReading.ReadingDate` in prod DB | High | Done | User provided both tables; confirmed Hypothesis A refuted, Hypothesis B confirmed |
| 2 | Write a targeted `KpiCalculatorTests` repro with ~185 daily readings at varying times of day, single tariff predating all of them | Medium | Done | Bit-exact Python simulation of .NET's real `TimeSpan.TotalDays` formula reproduced `HasCostGap == true` using this user's exact timestamps; a `dotnet` xUnit version is the recommended regression test (see Reproduction Plan) |
| 3 | `KpiCalculator`'s private `ResolveTariff` duplicates `TariffResolver.ResolveAsync` logic in-memory instead of using the documented canonical path | Low | Open | Not the root cause here (logic is equivalent), but drifts from the documented invariant in `project-context.md` ("TariffResolver.ResolveAsync is the only correct path") — flagged as a side finding |
| 4 | Verify data integrity of the `EffectiveDate`→`ContractStartDate` migration (`686a2f9`) for tariffs that had a pre-existing non-null `ContractStartDate` before consolidation | Low | Open | Ruled out for *this* flat's tariff (values are self-consistent with 100% real coverage) — but the backfill (`UPDATE ... WHERE ContractStartDate IS NULL`) silently drops the old `EffectiveDate` for any tariff where `ContractStartDate` was already non-null pre-migration; worth an audit query across all flats |

## Confirmed Findings

### Finding 1: Badge is mounted whenever `hasCostGap` is true, independent of what numbers it displays

**Evidence:** `client/src/features/dashboard/components/DashboardGrid.tsx:40-47`

**Detail:** `resolveCostDisplay` mounts `<CostGapBadge coveredDays totalDays />` whenever
`cost.hasCostGap` is true (or, separately, whenever `!cost.costDetailAvailable`). The badge's own
`⚠` glyph (`CostGapBadge.tsx:18`) is unconditional — it does not itself re-check whether
`coveredDays === totalDays`. So it is entirely possible to render "⚠ Tarif deckt 185 von 185 Tagen":
the label uses the (rounded) `coveredDays`/`totalDays` props, but the decision to show the badge at
all was made from `hasCostGap`, a separately-computed boolean.

### Finding 2: `HasCostGap` and the displayed day counts are computed from different roundings of the same quantities

**Evidence:** `api/Features/Dashboard/KpiCalculator.cs:97-117`

```csharp
var totalDaysInt = (int)Math.Ceiling(totalDays);
var coveredDaysInt = Math.Min((int)Math.Ceiling(coveredDays), totalDaysInt);
...
HasCostGap: coveredDays < (decimal)totalDays,       // raw, unrounded comparison
CoveredDays: coveredDaysInt,                         // Math.Ceiling-rounded
TotalDays: totalDaysInt,                             // Math.Ceiling-rounded
```

**Detail:** The inline comment at lines 112-113 states this is intentional: comparing raw values "so
a genuine intra-day coverage gap isn't masked by `Math.Ceiling` rounding both sides up to the same
integer." That reasoning is sound in isolation, but it creates exactly the symptom reported: any
raw-value gap too small to survive `Math.Ceiling` rounding still flips `HasCostGap` to `true`, while
the numbers shown to the user (`CoveredDays`/`TotalDays`, both rounded) read as identical — "185 of
185" — giving no visual indication of what the warning is even about.

### Finding 3: Existing tests only cover large, unambiguous gaps

**Evidence:** `api.Tests/Features/Dashboard/KpiCalculatorTests.cs:189-235`

**Detail:** `Compute_AllIntervalsCovered_HasCostGapFalseAndFullDenominator` (10/10 days, no gap) and
`Compute_FirstIntervalUntariffed_DividedByCoveredDaysOnlyAndHasCostGapTrue` (10/20 days, an obvious
10-day gap) are the only tests exercising `HasCostGap`. Neither constructs a scenario with many
readings and a gap small enough to round away — the exact shape of the user's report — so this path
is untested.

### Finding 4: The screenshot's cost figures are exactly reproduced from the current DB rows using the current algorithm

**Evidence:** User-provided `MeterReadings`/`Tariffs` rows; `KpiCalculator.cs:64-118`

**Detail:** From the 3 provided readings (6405.0000 → 7358.0000 → 7363.0000 kWh) and the single
tariff (`PricePerKwh = 0.2829`, `ContractStartDate = 2024-10-01`):

- `totalKwh` = (7358−6405) + (7363−7358) = 953 + 5 = 958 kWh
- `totalDays` ≈ 184.9729 days → `dailyAvgKwh` ≈ 5.179 kWh/day → rounds to **5,2 kWh** (matches Tagesdurchschnitt)
- `weeklyAvgKwh` = `dailyAvgKwh` × 7 ≈ 36.25 kWh → rounds to **36,3 kWh** (matches Wochendurchschnitt)
- `totalCost` = 958 × 0.2829 = 270.9582 €; `coveredDaysInt` = 185 → `dailyAvgCost` ≈ 1.4646 € → rounds to **1,46 €**
- `weeklyAvgCost` = 1.4646 × 7 ≈ 10.25 € → matches **10,25 €**
- `projectedMonthlyCost` = `dailyAvgKwh` × 0.2829 × 30 ≈ 43.96 € → matches **43,96 €**

All five displayed figures match the screenshot exactly, confirming the screenshot reflects a live,
current computation over the current DB state — not stale/cached data and not a deployment-version
mismatch.

## Deduced Conclusions

### Deduction 1: The badge's message and its trigger condition are logically decoupled

**Based on:** Finding 1, Finding 2

**Reasoning:** The frontend badge is a pure function of `hasCostGap` (whether to show) and
`coveredDays`/`totalDays` (what to print). The backend computes the first from raw values and the
latter two from rounded values. There is no invariant in the code tying "badge is shown" to "the
printed ratio is less than 100%."

**Conclusion:** Whenever raw `coveredDays` falls short of raw `totalDays` by less than one day (in
either direction of the `Math.Ceiling` rounding), the UI will show a contradictory "fully covered but
still warned" state. This is a design-level bug (not a one-off glitch): it reproduces any time the
raw gap is sub-day, regardless of the underlying cause of that gap.

### Deduction 2: The `EffectiveDate`→`ContractStartDate` schema consolidation (commit `686a2f9`, 2026-07-03) is not the cause

**Based on:** User-provided `Tariff` and `MeterReading` rows; Finding 4 (below)

**Reasoning:** The commit the day before this report renamed the field `KpiCalculator.ResolveTariff`
reads from `EffectiveDate` to `ContractStartDate` (functionally identical comparison logic). If the
migration's backfill (`UPDATE Tariffs SET ContractStartDate = EffectiveDate WHERE ContractStartDate
IS NULL`) had silently discarded a materially different original value for this specific tariff row,
the resulting `ContractStartDate` would not line up with the user's real cost history. Re-deriving
`DailyAvgCost` (1.46 €), `WeeklyAvgCost` (10.25 €), and `ProjectedMonthlyCost` (43.96 €) directly from
the provided rows and the current `ContractStartDate` value reproduces all three displayed figures
exactly (see Finding 4), which would not happen if the wrong start date were in play.

**Conclusion:** The migration is not implicated in this specific symptom. It remains a latent
data-integrity risk for other flats (see Investigation Backlog #4) but is not the mechanism behind
the badge shown in the screenshot.

## Hypothesized Paths

### Hypothesis A: A small portion of the flat's reading history predates the tariff's `ContractStartDate`

**Status:** Refuted

**Theory:** `GetDashboardFunction.cs:38-41` loads every `MeterReading` ever recorded for the flat,
unfiltered. If any reading had a `ReadingDate` earlier than the tariff's `ContractStartDate`, that
period would be excluded from `coveredDays` while still counted in `totalDays`.

**Resolution:** Refuted by the actual data. The flat's earliest `MeterReading.ReadingDate` is
2025-12-31T10:08:00Z; the flat's only `Tariff.ContractStartDate` is 2024-10-01T00:00:00Z — over a
year earlier. All 3 readings (and both intervals between them) postdate the tariff's start, so every
period resolves to the same tariff; there is no pre-tariff reading to exclude.

### Hypothesis B: Floating-point non-associativity in .NET's `TimeSpan.TotalDays` between the single-shot `totalDays` and the summed `coveredDays`

**Status:** Confirmed

**Theory:** `totalDays` is computed once via `(readings[^1].ReadingDate - readings[0].ReadingDate).TotalDays`
(`KpiCalculator.cs:52`). `coveredDays` is instead an accumulation of per-interval
`(decimal)(...).TotalDays` values (`KpiCalculator.cs:74,79`) — here, 2 intervals, since this flat has
3 readings taken at different times of day (10:08, 10:10, 09:29). .NET implements
`TimeSpan.TotalDays` as `ticks * DaysPerTick` (multiplication by the precomputed reciprocal
`1.0 / TicksPerDay`, not division) — a well-known source of double-rounding drift versus a
mathematically-equivalent single computation.

**Supporting indicators / confirmation:** Replicating .NET's exact formula in Python (same IEEE 754
double semantics) with this flat's real tick counts:

```
ticks r0->r1 (2025-12-31T10:08Z -> 2026-07-03T10:10Z): 158,977,200,000,000
ticks r1->r2 (2026-07-03T10:10Z -> 2026-07-04T09:29Z):     839,400,000,000

totalDays_direct   (single multiplication of combined ticks) = 184.97291666666666
coveredDays_summed (sum of 2 separate multiplications)       = 184.97291666666663
difference: coveredDays_summed - totalDays_direct = -2.842e-14 days (~2.46 microseconds)

Math.Ceiling(totalDays_direct)   = 185  -> displayed TotalDays
Math.Ceiling(coveredDays_summed) = 185  -> displayed CoveredDays
coveredDays_summed < totalDays_direct   = True -> HasCostGap
```

This bit-exact reproduces the observed symptom: displayed "185 von 185 Tagen" (both round to 185)
while `HasCostGap` is `true` (raw values differ by ~2.5 microseconds), triggering the ⚠ badge with no
real coverage gap. Re-deriving the displayed cost figures (1.46 € / 10.25 € / 43.96 €) from the same
data and current algorithm matches the screenshot exactly, confirming this is the live, current
computation rather than stale data.

**Would confirm:** ✅ Done — bit-exact arithmetic reproduction above.

**Would refute:** N/A — confirmed.

**Resolution:** Confirmed via faithful replication of .NET's `TimeSpan.TotalDays` formula
(`ticks * DaysPerTick`) applied to this flat's exact reading timestamps, reproducing `HasCostGap ==
true` with `CoveredDays == TotalDays == 185` — exactly the reported symptom.

## Missing Evidence

None remaining — the user-provided `Tariff`/`MeterReading` rows closed the one open gap (discriminating
Hypothesis A vs. B). Backlog item #4 (auditing other flats for migration data loss) is a separate,
lower-priority follow-up, not required to close this case.

## Source Code Trace

| Element       | Detail                                                                                          |
| ------------- | -------------------------------------------------------------------------------------------------- |
| Error origin  | `api/Features/Dashboard/KpiCalculator.cs:114` (`HasCostGap` computed from raw values) combined with `client/src/features/dashboard/components/DashboardGrid.tsx:40` (badge mount condition) |
| Trigger       | `GET /v1/flats/{flatId}/dashboard` → `GetDashboardFunction.RunAsync` → `KpiCalculator.Compute`      |
| Condition     | Raw `coveredDays` strictly less than raw `totalDays`, by any amount — even sub-day — while both round to the same integer via `Math.Ceiling` |
| Related files | `api/Features/Dashboard/DashboardModels.cs` (DTO shape), `api/Shared/TariffResolver.cs` (canonical resolver, not used here), `client/src/features/dashboard/api/dashboardApi.ts` (frontend types) |

## Conclusion

**Confidence:** High (root cause Confirmed and bit-exact reproduced from this user's real data).

The dashboard's cost-gap badge is shown while its own displayed "covered of total days" ratio reads
as 100%, because `HasCostGap` (drives visibility, `KpiCalculator.cs:114`) is computed from raw,
unrounded day counts, while `CoveredDays`/`TotalDays` (drive the label text) are computed from
`Math.Ceiling`-rounded versions of the same underlying quantities. For this specific flat, there is
no real coverage gap at all (single tariff since 2024-10-01, all 3 readings from 2025-12-31 onward,
every interval resolves to that tariff). The `true` value of `HasCostGap` is purely an artifact of
floating-point non-associativity in .NET's `TimeSpan.TotalDays` (`ticks * DaysPerTick`): summing two
independently-computed per-interval day counts (`coveredDays`) does not bit-for-bit reproduce one
computation over the combined tick span (`totalDays`), landing `coveredDays` about 2.5 microseconds
below `totalDays` — enough to flip a strict `<` comparison, not enough to survive `Math.Ceiling`
rounding in the displayed integers. This is expected to reproduce for most/all flats with 2+ reading
intervals whose timestamps aren't perfectly day-aligned — i.e., essentially every real user, not a
rare corner case — which existing tests never caught because their fixture dates are all exact
day-multiples (`date1.AddDays(10)`), a pattern that happens to avoid exposing the rounding artifact.

## Recommended Next Steps

### Fix direction

Root-cause fix (single mechanism, not conditional): make `HasCostGap` and the displayed
`CoveredDays`/`TotalDays` derive from the same values so they cannot mathematically diverge when
coverage is actually 100%. Two ways to do this, in order of preference:

1. **Compare the rounded values already computed for display:** `HasCostGap: coveredDaysInt <
   totalDaysInt` instead of the raw decimal comparison. Simplest change, one line
   (`KpiCalculator.cs:114`); loses the ability to flag a *sub-day* real gap as a warning, which is an
   acceptable trade-off given a sub-day gap is inherently invisible in a day-granularity badge anyway.
2. **Compute `coveredDays` as `totalDays` minus the sum of *uncovered* period lengths** (rather than
   accumulating covered period lengths independently) — this makes `coveredDays` and `totalDays`
   share the same base value by construction, so they can only diverge when an interval genuinely has
   no resolvable tariff, not from summation order. Preserves sub-day-gap sensitivity if that's wanted.

Either eliminates the "100% covered but still warned" contradiction as a class of bug, not just for
this user.

### Diagnostic

None outstanding — root cause is Confirmed. Optional: run backlog item #4 (audit other flats for the
`EffectiveDate`→`ContractStartDate` migration data-loss risk) as an unrelated follow-up.

## Reproduction Plan

Minimal repro using this exact flat's real data (no need for 185 synthetic readings — 3 readings, 2
intervals, is already sufficient):

```csharp
var tariff = MakeTariff(new DateTimeOffset(2024, 10, 1, 0, 0, 0, TimeSpan.Zero), 0.2829m);
var readings = new List<MeterReading> {
    MakeReading(new DateTimeOffset(2025, 12, 31, 10, 8, 0, TimeSpan.Zero), 6405.0000m),
    MakeReading(new DateTimeOffset(2026, 7, 3, 10, 10, 0, TimeSpan.Zero), 7358.0000m),
    MakeReading(new DateTimeOffset(2026, 7, 4, 9, 29, 0, TimeSpan.Zero), 7363.0000m),
};
var result = _calculator.Compute(flat, readings, new List<Tariff> { tariff }, Now);

result.Cost!.CoveredDays.ShouldBe(185);
result.Cost!.TotalDays.ShouldBe(185);
result.Cost!.HasCostGap.ShouldBeFalse(); // FAILS on current code — reproduces the reported bug
```

Expected: `HasCostGap` should be `false` (100% real coverage, matching `CoveredDays == TotalDays`).
On the current code this assertion fails (`HasCostGap` is `true`), confirming the bug independent of
any DB access — every input above is copied directly from the user's own data.

## Side Findings

- `KpiCalculator.ResolveTariff` (`KpiCalculator.cs:142-151`) reimplements `TariffResolver.ResolveAsync`'s
  selection logic (latest `ContractStartDate <= date`) as an in-memory loop rather than using the
  canonical resolver — reasonable for avoiding N async DB calls in a loop, but it duplicates logic that
  `project-context.md` calls "the only correct path" for period-accurate tariff costing. Not the root
  cause here (the two are currently logically equivalent), but a drift risk if `TariffResolver` ever
  changes.
