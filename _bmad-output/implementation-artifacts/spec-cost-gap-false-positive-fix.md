---
title: 'Cost-gap badge false positive fix'
type: 'bugfix'
created: '2026-07-04'
status: 'done'
route: 'one-shot'
context: []
---

# Cost-gap badge false positive fix

## Intent

**Problem:** The dashboard's ⚠ "Tarif deckt N von N Tagen" cost-gap badge could appear even when
tariff coverage was genuinely 100%, because `HasCostGap` compared raw (unrounded) `coveredDays`/
`totalDays` decimal values while the displayed `CoveredDays`/`TotalDays` were `Math.Ceiling`-rounded.
Floating-point non-associativity in .NET's `TimeSpan.TotalDays` (`ticks * DaysPerTick`) made summed
per-interval day counts land microseconds below the single full-span computation whenever reading
timestamps weren't exactly day-aligned — tripping the flag while the displayed ratio still read
"185 of 185," confirmed live for a real user and root-caused in
`_bmad-output/implementation-artifacts/investigations/cost-gap-badge-mismatch-investigation.md`.

**Approach:** Stop accumulating *covered* days (the source of the floating-point drift) and instead
accumulate only *uncovered* days per interval; derive `CoveredDays = TotalDays − uncoveredDays` and key
`HasCostGap` off `uncoveredDays > 0`. A fully-covered flat never sums any per-interval `TotalDays` at
all (`uncoveredDays` stays exactly `0m`), eliminating the drift, while a genuine sub-day gap still
reports a real positive `uncoveredDays` and correctly trips the flag — preserving the original intent
of catching gaps too small to survive `Math.Ceiling` rounding.

## Suggested Review Order

**Fix: uncovered-days accumulation**

- Loop now tracks only uncovered period-days instead of covered ones — see the comment for why this eliminates the drift.
  [`KpiCalculator.cs:64`](../../api/Features/Dashboard/KpiCalculator.cs#L64)

- `CoveredDays` is derived by subtraction from `TotalDays`, not accumulated independently.
  [`KpiCalculator.cs:108`](../../api/Features/Dashboard/KpiCalculator.cs#L108)

- `HasCostGap` keys off `uncoveredDays > 0m` directly, not a comparison between two independently-rounded values.
  [`KpiCalculator.cs:127`](../../api/Features/Dashboard/KpiCalculator.cs#L127)

**Regression tests**

- Reproduces the exact real-world reported bug: 3 non-day-aligned readings, one tariff, must show `HasCostGap == false`.
  [`KpiCalculatorTests.cs:238`](../../api.Tests/Features/Dashboard/KpiCalculatorTests.cs#L238)

- Companion test locking in the trade-off the review caught: a genuine sub-day gap must still trip `HasCostGap`, even when both day counts round to the same integer.
  [`KpiCalculatorTests.cs:266`](../../api.Tests/Features/Dashboard/KpiCalculatorTests.cs#L266)
