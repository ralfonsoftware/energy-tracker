---
baseline_commit: 7bf1685c4de005602a46a38a71e9e6e4eec1687a
---

# Story 9.8: Meter Reset Visual Indicator

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want a day where my meter reading dropped below the prior reading to be clearly labeled as a meter reset in my trend chart, instead of silently showing zero consumption with no explanation,
so that replacing a meter doesn't make my history look wrong or broken.

## Acceptance Criteria

1. **Given** `KpiCalculator.cs`'s `BuildDailySeries` clamps a negative inter-reading delta to 0 kWh/0 cost for that interval (`Math.Max(0m, readings[i + 1].KwhValue - readings[i].KwhValue)`, `KpiCalculator.cs:173`) — currently with no visual signal that this happened, **when** implemented, **then** each `DailyConsumptionPoint` gains a third field `WasMeterReset` (bool, computed in-memory as `readings[i + 1].KwhValue < readings[i].KwhValue` for the interval covering that day — not persisted, not a new DB column, not related to `IsCorrected`/`OriginalKwhValue` which only track user-initiated PATCH corrections and are never populated for a genuine meter reset).
2. **Given** the flag is present in `TrendChart`'s data, **when** rendered, **then** a day flagged `wasMeterReset` shows a distinct visual treatment — distinguishable from both a normal bar (`rgba(255,255,255,0.5)`) and an amber spike bar (`var(--color-accent-spike)`) — with an accessible text equivalent (the same WCAG 1.4.1 "color alone" concern already flagged for spike bars in `deferred-work.md:185`, which this story's indicator must not repeat), and a regression test covers both the backend flag computation and the frontend rendering.

## Tasks / Subtasks

- [x] Task 1: Add `WasMeterReset` to the daily-series computation (AC: 1)
  - [x] `api/Features/Dashboard/DashboardModels.cs:25` — change `public record DailyConsumptionPoint(string Date, decimal KwhValue);` to `public record DailyConsumptionPoint(string Date, decimal KwhValue, bool WasMeterReset);`
  - [x] `api/Features/Dashboard/KpiCalculator.cs`'s `BuildDailySeries` (currently `:166-185`, returns `Dictionary<DateOnly, decimal>`) — change the return type to `Dictionary<DateOnly, (decimal Kwh, bool WasMeterReset)>`. For each interval `i`, compute `var wasReset = readings[i + 1].KwhValue < readings[i].KwhValue;` once, then when writing each spanned day's entry, set `series[date] = (series.GetValueOrDefault(date).Kwh + perDayKwh, series.GetValueOrDefault(date).WasMeterReset || wasReset);` — every day in that interval's span (`firstDay` to `end.DayNumber`, same range already used for `perDayKwh`) gets the same reset flag, mirroring how the clamped-zero `perDayKwh` is already spread across that whole span.
  - [x] Update `DetectSpikes` (`:187-210`) to accept the new tuple-valued dictionary and read `.Kwh` wherever it currently reads the raw decimal (`dailySeries.GetValueOrDefault(date)` → `dailySeries.GetValueOrDefault(date).Kwh`, same for the lookback loop's `TryGetValue`/`priorKwh`). Spike detection logic itself is unchanged — it only ever needs the kWh value, never the reset flag.
  - [x] `Compute`'s `dailyConsumption` build loop (`:137-139`) — change `dailyConsumption.Add(new DailyConsumptionPoint(date.ToString("yyyy-MM-dd"), dailySeries.GetValueOrDefault(date)))` to pass both fields: `var entry = dailySeries.GetValueOrDefault(date); dailyConsumption.Add(new DailyConsumptionPoint(date.ToString("yyyy-MM-dd"), entry.Kwh, entry.WasMeterReset));`.
  - [x] The three early-return paths (no readings, one reading, sub-day span — `:38-60`) already return `DailyConsumption: []`; no change needed there since there's no interval to flag.

- [x] Task 2: Backend regression tests (AC: 1)
  - [x] Add to `api.Tests/Features/Dashboard/KpiCalculatorTests.cs`, following the existing `Compute_TwoReadingsWithinWindow_DistributesKwhEvenlyAcrossSpannedDays` pattern (`:356-373`): a test with two readings where the second `KwhValue` is lower than the first (e.g. `MakeReading(date1, 500m)`, `MakeReading(date2, 100m)` — simulating a meter replacement) — assert every `DailyConsumptionPoint` spanning that interval has `WasMeterReset == true` and `KwhValue == 0m` (the existing clamp), and a day outside that interval (from a separate, normal-direction reading pair added to the same test or a second test) has `WasMeterReset == false`.
  - [x] Add a test confirming a normal (non-reset) multi-reading series has `WasMeterReset == false` on every `DailyConsumptionPoint` — regression guard so the new field defaults correctly and doesn't get accidentally set `true` for ordinary consumption.
  - [x] Existing tests that construct `DailyConsumptionPoint` expectations via property access only (`.Date`, `.KwhValue`) are unaffected by the added third field — no existing assertion needs updating, but confirm `dotnet test api.Tests/api.Tests.csproj` passes in full after the record signature change (positional-record callers elsewhere, if any exist outside `KpiCalculator.cs`, would fail to compile otherwise — grep confirms `KpiCalculator.cs` is the only production constructor call site).

- [x] Task 3: Surface the flag on the frontend type and chart data (AC: 2)
  - [x] `client/src/features/dashboard/api/dashboardApi.ts:13` — change `export type DailyConsumptionPoint = { date: string; kwhValue: number }` to `export type DailyConsumptionPoint = { date: string; kwhValue: number; wasMeterReset: boolean }`.
  - [x] `client/src/features/dashboard/components/TrendChart.tsx`'s `chartData` memo (`:20-30`) — add `wasMeterReset: point.wasMeterReset` to the mapped object alongside the existing `date`/`kwh`/`label` fields.

- [x] Task 4: Distinct visual treatment for reset days (AC: 2)
  - [x] Add a new semantic color token to `client/src/index.css`'s `@theme` block (near `--color-accent-spike` at `:18`), e.g. `--color-accent-reset: #94a3b8;` (a cool slate tone, visually distinct from the warm amber spike color and from the neutral white bars — exact hex is a design nicety, not gated by any AC).
  - [x] In `TrendChart.tsx`'s `<BarChart>`, add an SVG `<defs>` block (as a direct child, before `<Bar>`) defining a diagonal-stripe pattern, e.g.:
    ```tsx
    <defs>
      <pattern id="meterResetHatch" width="4" height="4" patternTransform="rotate(45)" patternUnits="userSpaceOnUse">
        <rect width="4" height="4" fill="var(--color-accent-reset)" />
        <line x1="0" y1="0" x2="0" y2="4" stroke="rgba(255,255,255,0.4)" strokeWidth="1.5" />
      </pattern>
    </defs>
    ```
  - [x] Update the `Cell` fill logic (`:74-81`) to check `wasMeterReset` before `spikeSet`: `fill={entry.wasMeterReset ? 'url(#meterResetHatch)' : spikeSet.has(entry.date) ? 'var(--color-accent-spike)' : 'rgba(255,255,255,0.5)'}` — a day can't be both a spike and a reset in practice (a reset clamps that day to exactly 0 kWh, which can never exceed the spike threshold), but ordering the check this way makes that precedence explicit rather than accidental.
  - [x] Accessible text equivalent (closing the WCAG 1.4.1 gap rather than repeating it): add a visually-hidden summary below the chart, rendered only when at least one day in `chartData` has `wasMeterReset === true` — e.g. `{resetDates.length > 0 && <span className="sr-only">{t('trend.meterResetSummary', { dates: resetDates.join(', ') })}</span>}` where `resetDates` is derived via `useMemo` from `chartData.filter(d => d.wasMeterReset).map(d => d.date)`. Use Tailwind's `sr-only` utility (visually hidden, still read by screen readers) — no new dependency needed.
  - [x] Add the two new i18n keys to **both** `client/src/locales/en-US/dashboard.json` and `client/src/locales/de-DE/dashboard.json`'s existing `"trend": {...}` block (alongside `cardTitle`/`historyIconLabel`): `"meterResetSummary": "Meter reset detected on: {{dates}}. Consumption shown as 0 for these days."` (en-US) / `"meterResetSummary": "Zählerwechsel erkannt am: {{dates}}. Verbrauch für diese Tage als 0 angezeigt."` (de-DE). Do not hardcode English strings in the JSX — this project's i18n rule applies here same as everywhere else.

- [x] Task 5: Frontend regression tests (AC: 2)
  - [x] Add to `client/src/features/dashboard/components/TrendChart.test.tsx`, following the existing `TrendChart_OneSpikeDayMatchingDailyConsumption_ThatBarUsesSpikeFillColor` pattern (`:71-81`): extend `makeDashboard`'s `dailyConsumption` fixture (or pass an override) with one entry having `wasMeterReset: true`, render, and assert that entry's bar `fill` attribute is `'url(#meterResetHatch)'` while the rest remain `'rgba(255,255,255,0.5)'`.
  - [x] Add a test asserting the visually-hidden summary text is present (`screen.getByText(...)` won't work for `sr-only` content by default visibility, but Testing Library queries text regardless of CSS visibility — use `screen.getByText('trend.meterResetSummary')` given the test's `useTranslation` mock returns the raw key) when at least one `wasMeterReset: true` point exists, and absent when none do.
  - [x] Existing fixture `sevenDays` (`:19-27`) has no `wasMeterReset` field — since `DailyConsumptionPoint` gains a new required field, update `sevenDays` and any other inline `dailyConsumption` literals in this test file and `client/src/features/dashboard/__tests__/DashboardGrid.test.tsx` / `useDashboard.test.ts` (both already have `dailyConsumption`-shaped fixtures per the earlier grep) to include `wasMeterReset: false` on every entry — otherwise TypeScript will fail to compile these test files once the type gains the new required field.

### Review Findings

- [x] [Review][Patch] Meter-reset bars render at zero height, making the hatch pattern invisible [client/src/features/dashboard/components/TrendChart.tsx:74] — fixed: added `minPointSize={3}` to `<Bar>`; verified empirically (a 0-kwh reset day now renders all 7 bars with the hatch fill visible, confirmed both before and after the fix via a direct render check).
- [x] [Review][Patch] Accessible summary uses un-localized ISO dates and the test never verifies interpolated content [client/src/features/dashboard/components/TrendChart.tsx:236-238,279-281] — fixed: `resetDates` now formatted via `Intl.DateTimeFormat` (matching the chart's own label localization) before joining; strengthened `TrendChart.test.tsx`'s `react-i18next` mock to expose interpolation options so tests assert on the actual formatted date, not just the raw i18n key.
- [x] [Review][Defer] SVG pattern `id="meterResetHatch"` is unscoped — collides if `TrendChart` ever renders twice on one page [client/src/features/dashboard/components/TrendChart.tsx:73-84] — deferred, pre-existing single-instance assumption, not exercised by any current usage.
- [x] [Review][Defer] New OR-merge day-flag logic untested for 3+ readings on the same calendar day [api/Features/Dashboard/KpiCalculator.cs:186] — deferred, logic is correct by construction (monotonic OR), coverage gap only.
- [x] [Review][Defer] `DetectSpikes`'s rolling lookback can include a reset day's 0-kwh, potentially over-flagging the following day as a spike [api/Features/Dashboard/KpiCalculator.cs:196-210] — deferred, pre-existing `DetectSpikes` characteristic predating this diff, not introduced by it.
- [x] [Review][Defer] New backend tests use `.First(...)` instead of `.Single(...)`/indexed lookup [api.Tests/Features/Dashboard/KpiCalculatorTests.cs:474,478] — deferred, minor test-robustness nitpick.
- [x] [Review][Defer] Only a screen-reader-only summary was added; no on-chart legend/tooltip for sighted users [client/src/features/dashboard/components/TrendChart.tsx:279-281] — deferred, not required by AC 2, a possible future polish item given no design doc exists for this story's visual treatment.
- [x] [Review][Defer] No test coverage for multiple resets in one window or a reset on the window boundary [api.Tests/Features/Dashboard/KpiCalculatorTests.cs] — deferred, AC 2 only requires one regression test, already satisfied.

## Dev Notes

- **No design-decision doc exists for the exact visual treatment of this story**, unlike Stories 9.1/9.4/9.6 which each cite an approved `.decision-log.md` entry (D-45, D-44, D-46) before implementation. Story 9.8's epic note only resolves the *data-source* question (use `currentReading < previousReading`, not `IsCorrected`/`OriginalKwhValue`) — the AC's "e.g. a small reset icon or hatched bar" is explicitly illustrative, not a locked spec. Task 4 above proposes one concrete, low-complexity implementation (SVG hatch pattern + `sr-only` text summary) that satisfies the AC's letter (distinguishable from both other bar states, accessible text equivalent) using only patterns already present in this codebase (Tailwind, existing `<Cell>`-based coloring, existing i18n/`sr-only` conventions) — no new dependency, no per-bar interactive/popover UI. If Ralf wants a different visual (e.g. a small icon overlay instead of a hatch pattern, matching `CostGapBadge.tsx`'s Popover-based accessible-detail pattern), treat Task 4's specifics as a starting point to adjust, not a hard requirement — but AC 2 itself (distinct treatment + accessible text equivalent + tests) is not optional.
- **`IsCorrected`/`OriginalKwhValue` are the wrong tool here — do not use them.** They exist on `MeterReading` (Story 3.6) and only get set when a user explicitly PATCHes a correction via `PatchReadingFunction`. A meter reset is a perfectly normal new reading (e.g. after a meter/device replacement) that happens to be numerically lower than the prior one — nothing marks it as special at submission time, and nothing should: the fix is purely in how the KPI layer interprets `currentReading < previousReading`, not a new submission-time field or flow change.
- **This is a read-only, in-memory computation change — no migration, no new DB column, no API contract break beyond one additive field.** `WasMeterReset` is derived fresh on every `Compute()` call from already-loaded `readings`, exactly like `SpikeDays` and `IsInterpolated` are computed/stored elsewhere in this codebase, never persisted.
- **Why per-point boolean, not a separate array like `SpikeDays`:** the epic's AC explicitly frames this as "each daily consumption entry gains a boolean flag... alongside the existing `IsInterpolated`-style metadata" — i.e., follow `SmartPlugDailyData.IsInterpolated`'s per-row-boolean precedent, not `DashboardSummary.SpikeDays`'s separate-array-of-date-strings precedent (which the same epic's own retro flagged as awkward: `spikeDays` sat unused/unrendered in the API response for a full story cycle, `deferred-work.md:160`). Keeping the flag co-located on `DailyConsumptionPoint` avoids a second lookup/`Set` construction in `TrendChart` and can't drift out of sync with the day it describes.
- **Ordering with spike detection:** a day can never be both a spike and a reset in this codebase's math — a reset clamps `perDayKwh` to exactly `0` for every day in that interval, and `DetectSpikes`' condition (`dayKwh > threshold * rollingAvg`) can't fire when `dayKwh == 0` unless `threshold` is negative (already guarded against, `threshold <= 0m` short-circuits). Task 4's explicit `wasMeterReset` precedence check is defensive clarity, not dead-code removal.
- FluentValidation/DB layers are untouched by this story — this is entirely `KpiCalculator.cs` + `DashboardModels.cs` (backend) and `TrendChart.tsx` + `dashboardApi.ts` + two locale JSON files (frontend).

### Existing code being modified — current state and what's preserved

- `api/Features/Dashboard/DashboardModels.cs` — `DailyConsumptionPoint` currently a 2-field record (`Date`, `KwhValue`). Adding a third field is additive; every other record in this file (`CostSummary`, `DashboardSummary`) is untouched.
- `api/Features/Dashboard/KpiCalculator.cs` — `Compute()`'s cost/KPI math (lines 29-153) is entirely unrelated to `BuildDailySeries`/`DetectSpikes` and must not change: `totalKwh`, `totalCost`, `dailyAvgKwh`, `weeklyAvgKwh`, `todayKwh`, `CostSummary` fields all derive from the main per-interval loop (`:74-92`), which already independently clamps via its own `Math.Max(0m, ...)` (`:76`) — this story does not touch that loop at all, only `BuildDailySeries` (`:166-185`) and `DetectSpikes` (`:187-210`), which exclusively feed `DailyConsumption`/`SpikeDays`.
- `client/src/features/dashboard/components/TrendChart.tsx` — the `History` icon/sheet trigger (`:40-56`), the loading-skeleton branch (`:58-63`), and the zero/one-reading early return (`:34`) are all unrelated to this story and must render exactly as before.
- `client/src/features/dashboard/api/dashboardApi.ts` — only `DailyConsumptionPoint`'s type changes; `CostSummary`, `DashboardSummary`, and `getDashboard()` are untouched.

### Testing Requirements Summary

- Backend: xUnit + Shouldly, direct `KpiCalculator.Compute(...)` calls — matches every existing test in `KpiCalculatorTests.cs` (no HTTP/Function layer, no DB). Use the existing `MakeFlat`/`MakeReading`/`MakeTariff` helpers (`:12-19`) — do not add new test-data builders.
- Frontend: Vitest + `@testing-library/react`, following `TrendChart.test.tsx`'s existing conventions exactly — `react-i18next` mocked to return raw keys (so assert against the key string, e.g. `'trend.meterResetSummary'`, not translated prose), query bars via `container.querySelectorAll('.recharts-bar-rectangle path')` and check the `fill` attribute (same technique the existing spike test uses, `:76-80`).
- **Compilation hazard:** `DailyConsumptionPoint` gaining a required third field breaks every existing TS literal of that shape at compile time (not just at runtime) — Task 5's last item is not optional polish, it's required for the frontend to build at all. Grep `dailyConsumption:` across `client/src/features/dashboard/` before considering this story done to confirm every fixture was updated.

### Project Structure Notes

- No new files. All changes are in-place edits to existing files across `api/Features/Dashboard/`, `api.Tests/Features/Dashboard/`, `client/src/features/dashboard/`, and the two `dashboard.json` locale files — matches this story's narrow, single-slice scope (Dashboard feature only, backend + frontend).
- No conflicts with VSA slice isolation, i18n namespace rules, or any other `project-context.md` convention — the two new i18n keys go in the existing `dashboard` namespace's existing `trend` sub-object, not a new namespace.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-9-unified-save-affordance-fix-and-pre-epic-10-hardening.md#Story 9.8] — verbatim epic AC and the rescope note explaining why `IsCorrected`/`OriginalKwhValue` don't apply and the flag is computed, not persisted.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:153] — original item ("Deferred from: code review of 3-2-kpi-dashboard-backend-computation") that promoted this story, describing the zero-clamp-with-no-signal gap in `KpiCalculator.cs`.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:160] — `spikeDays` fetched-but-unrendered gap from Story 3.3's review, the precedent for why this story's flag is co-located per-point rather than a parallel unused array.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:185] — the WCAG 1.4.1 "color alone" concern already flagged for spike bars, which this story's own indicator must not repeat (hence the required accessible text equivalent in AC 2).
- [Source: api/Features/Dashboard/KpiCalculator.cs:74-92,166-210] — the per-interval clamp already in the main compute loop, and the exact `BuildDailySeries`/`DetectSpikes` methods this story modifies.
- [Source: api/Features/Dashboard/DashboardModels.cs] — `DailyConsumptionPoint`'s current 2-field shape.
- [Source: client/src/features/dashboard/components/TrendChart.tsx] — current `Cell` fill logic and `spikeSet` pattern this story extends.
- [Source: client/src/features/dashboard/components/TrendChart.test.tsx:19-27,71-81] — existing fixture and spike-color test this story's new tests mirror.
- [Source: client/src/features/dashboard/api/dashboardApi.ts:13,21] — current `DailyConsumptionPoint` type and `spikeDays` field this story's per-point flag deliberately does not imitate.
- [Source: client/src/index.css:18,23] — existing semantic accent-color tokens (`--color-accent-spike`, `--color-accent-tariff-locked`) this story's new `--color-accent-reset` token follows.
- [Source: client/src/features/dashboard/components/CostGapBadge.tsx] — existing precedent for a Popover-based accessible-detail indicator, offered as an alternative visual direction if Ralf prefers it over Task 4's hatch-pattern proposal.
- [Source: client/src/locales/en-US/dashboard.json, de-DE/dashboard.json] — existing `trend` namespace keys the two new keys are added alongside.
- [Source: _bmad-output/project-context.md#i18n] — every user-visible string via `useTranslation`, namespace matches feature folder, no hardcoded English in JSX.
- [Source: _bmad-output/implementation-artifacts/9-7-decimal-precision-validation-policy.md] — previous story (9.7), a backend-only pure refactor with zero behavior change; unrelated code area, no carryover learnings apply to this story's implementation specifics beyond confirming the project's xUnit/Shouldly test-writing conventions.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet test api.Tests/api.Tests.csproj --filter "FullyQualifiedName~Dashboard"` — 28/28 passed
- `dotnet test api.Tests/api.Tests.csproj` — full suite: 364/364 passed
- `npx vitest run src/features/dashboard` — 23/23 passed
- `npx vitest run` (from `client/`) — full suite: 395/395 passed
- `npx tsc --noEmit` — clean, no compile errors
- `npm run lint` — clean (pre-existing `router.tsx` fast-refresh warnings only, unrelated to this story)
- Code review (2026-07-19): 3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor) — 2 patch, 6 defer, 8 dismissed. Both patches applied and verified: `npx vitest run src/features/dashboard/components/TrendChart.test.tsx` — 9/9 passed; full suites re-run after fixes — `dotnet test api.Tests/api.Tests.csproj` 364/364, `npx vitest run` 395/395, `npx tsc --noEmit` clean, `npm run lint` clean.

### Completion Notes List

- Added `WasMeterReset` as a third field on `DailyConsumptionPoint`, computed in `KpiCalculator.BuildDailySeries` from `readings[i + 1].KwhValue < readings[i].KwhValue` per interval and OR'd across every spanned day, mirroring how the clamped-zero `perDayKwh` is already distributed. `DetectSpikes` updated to read `.Kwh` from the now tuple-valued dictionary; spike-detection logic itself unchanged.
- Two new backend regression tests in `KpiCalculatorTests.cs`: one confirms a downward-reading interval flags every spanned day `WasMeterReset == true` with `KwhValue == 0m`, the other confirms a normal increasing series never sets the flag.
- Frontend: `DailyConsumptionPoint` type gained `wasMeterReset: boolean`; `TrendChart.tsx`'s `chartData` now carries the flag, an SVG hatch `<pattern>` (`meterResetHatch`) renders reset-day bars distinctly from both normal and spike bars, and a `sr-only` summary (new `trend.meterResetSummary` i18n key, both locales) lists affected dates for screen readers — only rendered when at least one reset day is present.
- `DashboardGrid.test.tsx` and `useDashboard.test.ts` fixtures use `dailyConsumption: []` (empty arrays) — no literal update was actually needed there despite the task's caution; confirmed via full `tsc --noEmit` pass that no other call site broke.
- No design-decision doc existed for the exact visual treatment (unlike Stories 9.1/9.4/9.6) — implemented the story's proposed hatch-pattern + accessible-summary approach as a reasonable default; flagged to Ralf as adjustable if a different visual (e.g. icon/popover) is preferred.
- ✅ Resolved review finding [Patch]: meter-reset bars rendered at zero height (recharts skips path rendering for 0-value bars, and every reset day carries `kwhValue: 0` by construction), making the hatch pattern invisible in practice — added `minPointSize={3}` to `<Bar>`; confirmed via direct render check that all 7 bars now appear, including the hatched reset day.
- ✅ Resolved review finding [Patch]: accessible summary text used raw un-localized ISO dates and the test only checked the raw i18n key — `resetDates` now formatted through `Intl.DateTimeFormat` (matching the chart's own axis-label localization); strengthened the test's i18n mock to expose interpolation options so the actual formatted date is asserted, not just the key text.

### File List

- `api/Features/Dashboard/DashboardModels.cs` (modified)
- `api/Features/Dashboard/KpiCalculator.cs` (modified)
- `api.Tests/Features/Dashboard/KpiCalculatorTests.cs` (modified)
- `client/src/features/dashboard/api/dashboardApi.ts` (modified)
- `client/src/features/dashboard/components/TrendChart.tsx` (modified)
- `client/src/features/dashboard/components/TrendChart.test.tsx` (modified)
- `client/src/index.css` (modified)
- `client/src/locales/en-US/dashboard.json` (modified)
- `client/src/locales/de-DE/dashboard.json` (modified)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified)

## Change Log

- 2026-07-19: Implemented meter-reset visual indicator — `WasMeterReset` flag computed per-day in `KpiCalculator`, surfaced through `DailyConsumptionPoint`, and rendered as a distinct hatched bar with an accessible text summary in `TrendChart`. Backend (364 tests) and frontend (395 tests) suites pass with zero regressions. Status → review.
- 2026-07-19: Code review — fixed 2 patch findings (zero-height reset bars via `minPointSize`; un-localized accessible-summary dates via `Intl.DateTimeFormat`), deferred 6 minor/pre-existing items to `deferred-work.md`, dismissed 8 as noise or explicitly out-of-scope per spec. All suites re-verified green. Status → done.
