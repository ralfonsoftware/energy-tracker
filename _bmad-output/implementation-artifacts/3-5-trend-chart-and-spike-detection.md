---
baseline_commit: 6afb7b5
---

# Story 3.5: Trend Chart & Spike Detection

Status: done

## Story

As a user,
I want to see a bar chart of my daily consumption history with spike days highlighted in amber,
so that I can immediately spot unusual consumption and know to check the Insights tab for context.

## Acceptance Criteria

1. **Spike detection algorithm** — Given `KpiCalculator` computing the dashboard, when spike detection runs, then a day is flagged as a spike when its consumption exceeds `Flat.SpikeThreshold × 7-day rolling average` (default `2.0`); flagged dates are returned in `SpikeDays` as `"yyyy-MM-dd"` strings; `SpikeThreshold` is stored as `decimal` on the `Flats` entity and is user-configurable per Flat (both **already exist** — `Flat.SpikeThreshold` was added in Story 2.4 with EF default `2.0`; this story implements the detection algorithm that consumes it, not the column).

2. **Daily consumption series** — Given readings spaced arbitrarily in time (not necessarily daily), when the dashboard is computed, then `DashboardSummary` exposes a `DailyConsumption` array of the last 7 calendar days (oldest → newest, ending on the current date in the app's fixed timezone, `Europe/Berlin` — see Round 2 Review Findings), each with a derived daily kWh value — see Dev Notes "Daily Series Derivation Algorithm" for the exact, mandatory algorithm.

3. **Trend chart rendering** — Given `TrendChart.tsx`, when rendered with dashboard data, then a recharts bar chart displays daily consumption bars; spike-day bars render with `fill: #f59e0b` (`accent-spike` amber); non-spike bars use the standard subdued color (`rgba(255,255,255,0.5)`); the chart card has standard glass card treatment (`border-radius: 18px`, `backdrop-filter: blur(20px) saturate(180%)`, `background: rgba(255,255,255,0.08)`, `border: 1px solid rgba(255,255,255,0.14)`).

4. **No spike interaction** — Given a spike bar is tapped, when the interaction occurs, then no banner or notification is generated on the Dashboard — full spike context is accessible only from the Insights tab (not built yet; out of scope — just don't add any tap handler that shows one).

5. **Reading history entry point** — Given the trend chart card header, when rendered, then a 20×20px clock/list icon in `text-secondary` is positioned top-right with a minimum 44×44pt tap target and explicit `aria-label`; tapping it opens the Reading History bottom sheet.

## Tasks / Subtasks

- [x] **Task 0: Backend — daily consumption series + spike detection in `KpiCalculator`** (AC: 1, 2)
  - [x] `api/Features/Dashboard/DashboardModels.cs` — add `public record DailyConsumptionPoint(string Date, decimal KwhValue);` and append `DailyConsumptionPoint[] DailyConsumption` as the new final positional parameter of `DashboardSummary` (append-only, matches the `LastKwhValue` precedent from Story 3.4)
  - [x] `api/Features/Dashboard/KpiCalculator.cs` — add `BuildDailySeries`, `DetectSpikes` private static methods; wire into all four `return new DashboardSummary(...)` branches — see Dev Notes for the exact algorithm and literal code
  - [x] Update `api.Tests/Features/Dashboard/KpiCalculatorTests.cs` — existing tests use named-argument construction via `_calculator.Compute(...)`, not direct `DashboardSummary` construction, so they will NOT break on the new field, but the 0/1-reading tests should gain a `result.DailyConsumption.ShouldBeEmpty()` assertion; add new tests per Dev Notes "New Backend Tests"
  - [x] `api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs` — add one assertion to `RunAsync_ValidFlatWithReadings_Returns200WithComputedSummary`: `summary.DailyConsumption.Length.ShouldBe(7)` (structural check only — see Dev Notes on why values aren't asserted here)
  - [x] `dotnet test api.Tests` passes

- [x] **Task 1: `dashboardApi.ts` — add `dailyConsumption`** (AC: 2)
  - [x] `client/src/features/dashboard/api/dashboardApi.ts` — add `export type DailyConsumptionPoint = { date: string; kwhValue: number }` and `dailyConsumption: DailyConsumptionPoint[]` to `DashboardSummary`

- [x] **Task 2: Fix existing test fixtures that construct `DashboardSummary` as object literals** (AC: 2)
  - [x] `client/src/features/dashboard/__tests__/DashboardGrid.test.tsx` — add `dailyConsumption: []` to both fixture objects (TypeScript will fail to compile without it)
  - [x] `client/src/features/dashboard/__tests__/useDashboard.test.ts` — same

- [x] **Task 3: `ResizeObserver` test stub** (AC: 3 — required for any recharts component to render in jsdom)
  - [x] `client/src/test-setup.ts` — add a guarded `ResizeObserver` stub (same pattern as the existing `matchMedia` guard) — see Dev Notes "Why This Is Required" before touching this file

- [x] **Task 4: `TrendChart.tsx` — 7-day bar chart with spike coloring** (AC: 3, 4, 5)
  - [x] `client/src/features/dashboard/components/TrendChart.tsx` (NEW) — see Dev Notes Task 4 for full implementation guidance and literal code template

- [x] **Task 5: `ReadingHistorySheet.tsx` — minimal placeholder sheet** (AC: 5)
  - [x] `client/src/features/readings/components/ReadingHistorySheet.tsx` (NEW) — see Dev Notes "Story Boundary: Reading History Sheet" — this is an intentionally minimal placeholder; Story 3.6 replaces the body content with real `useReadingHistory` data, it does not recreate the file

- [x] **Task 6: Wire `TrendChart` into `DashboardPage.tsx`** (AC: 3, 5)
  - [x] `client/src/features/dashboard/DashboardPage.tsx` (MODIFY) — render `<TrendChart dashboard={dashboard} flatId={settings?.flatId} />` below `<DashboardGrid />`

- [x] **Task 7: Translation keys** (AC: 3, 5)
  - [x] `client/src/locales/en-US/dashboard.json` — add `trend.cardTitle`, `trend.historyIconLabel`
  - [x] `client/src/locales/de-DE/dashboard.json` — German equivalents
  - [x] `client/src/locales/en-US/readings.json` — add `history.title`, `history.comingSoon`
  - [x] `client/src/locales/de-DE/readings.json` — German equivalents

- [x] **Task 8: Tests** (AC: all)
  - [x] `client/src/features/dashboard/components/TrendChart.test.tsx` (NEW) — minimum 6 tests (see Dev Notes Task 8)
  - [x] `client/src/features/readings/components/ReadingHistorySheet.test.tsx` (NEW) — minimum 2 tests

- [x] **Task 9: Final verification**
  - [x] `dotnet test api.Tests` exits 0
  - [x] `cd client && npm run build` exits 0 with zero TypeScript errors
  - [x] `cd client && npm test` — all tests pass including all pre-existing tests
  - [x] `cd client && npm run lint` exits 0
  - [x] Update File List in this story

### Review Findings

_Code review run 2026-07-02 — 0 decision-needed (2 resolved), 3 patch (all applied), 7 defer, 10 dismissed as noise._

**Patch (resolved from decision-needed):**

- [x] [Review][Patch] `BuildDailySeries` bucketed each reading's consumption by the calendar date of `ReadingDate.Date` (whatever offset the `DateTimeOffset` carries), while `windowStart`/`windowEnd` were derived from `now.Date` where `now` = `DateTimeOffset.UtcNow` — a reading recorded shortly after local midnight in a non-UTC offset could land on a different UTC calendar day than its local day, mismatching which day consumption is attributed to vs. the window. Decision (2026-07-02): normalize both sides through a single fixed app timezone (`Europe/Berlin`) rather than UTC, since there's no per-Flat/User timezone field yet — a real per-flat timezone system is out of scope for this patch. Fixed: added `AppTimeZone` constant, converted both `BuildDailySeries`'s reading dates and `windowEnd` via `TimeZoneInfo.ConvertTime`. `api/Features/Dashboard/KpiCalculator.cs`
- [x] [Review][Patch] Weekday label parsed as UTC but formatted in local timezone — `new Intl.DateTimeFormat(i18n.language, { weekday: 'narrow' }).format(new Date(point.date))` parsed the `"yyyy-MM-dd"` string as UTC midnight per ECMA-262 but rendered in the browser's local timezone, shifting the displayed weekday back by one day for any user west of UTC. Fixed: added `timeZone: 'UTC'` to the `Intl.DateTimeFormat` options. [`client/src/features/dashboard/components/TrendChart.tsx`]
- [x] [Review][Patch] `DetectSpikes` guarded `rollingAvg > 0m` but never guarded `threshold` itself — if `flat.SpikeThreshold` is ever `0` or negative, `dayKwh > threshold * rollingAvg` becomes true for virtually any positive `dayKwh`, flagging nearly every day as a spike. Fixed: added `threshold <= 0m` to the existing guard clause. [`api/Features/Dashboard/KpiCalculator.cs:DetectSpikes`]

**Defer (includes 1 resolved decision-needed):**

- [x] [Review][Defer] `TrendChart` hides the entire card — including the Reading History entry point (history icon) — whenever `chartData.length === 0` (0 or 1 total readings), per an undocumented-in-AC dev decision ("mirrors the KPI tiles' cold-open dash state"). AC-5 does not gate icon visibility on data availability, so brand-new users currently have no way to reach Reading History via this entry point until they log a 2nd reading. Decision (2026-07-02): keep as-is — matches the existing cold-open dash pattern and has zero practical impact today since Reading History is still just a "coming soon" placeholder; revisit only if Story 3.6 makes this a real usability problem. `client/src/features/dashboard/components/TrendChart.tsx` — deferred, no functional impact until Story 3.6

- [x] [Review][Defer] `BuildDailySeries`/`DetectSpikes` walk the entire reading history on every dashboard load (unbounded O(n)) even though only ~14 days of data are ever needed, duplicating work the existing per-interval totals loop already does. Not a correctness issue at expected data volumes (manual meter readings, not high-frequency data). [`api/Features/Dashboard/KpiCalculator.cs`] — deferred, performance optimization not correctness-blocking
- [x] [Review][Defer] Rolling-average spike detection has no defense against a prior flagged spike inflating the following day's baseline, potentially masking a second consecutive spike. Inherent to the mandated algorithm (verified byte-for-byte compliant with spec's literal code) — changing it means changing the spec. [`api/Features/Dashboard/KpiCalculator.cs:DetectSpikes`] — deferred, spec-mandated algorithm limitation
- [x] [Review][Defer] `ReadingValidator` has no upper bound on `ReadingDate`; a future-dated reading contributes to running totals/cost but silently falls outside the `DailyConsumption`/`SpikeDays` window. [`api/Features/Readings/ReadingValidator.cs`] — deferred, pre-existing gap from Story 3.1, not introduced by this diff
- [x] [Review][Defer] No defensive re-sort/tie-break in `BuildDailySeries` for readings sharing the same instant; relies on the pre-existing "readings is pre-sorted ascending" invariant already trusted elsewhere in the file. [`api/Features/Dashboard/KpiCalculator.cs`] — deferred, pre-existing pattern, not new to this diff
- [x] [Review][Defer] Negative period deltas (meter resets/corrections) are silently clamped to 0 kWh and spread across the span with no visual signal in the trend chart. Reuses the existing clamp behavior already used for `totalKwh`/cost elsewhere in `KpiCalculator`, consistent with established behavior. [`api/Features/Dashboard/KpiCalculator.cs:BuildDailySeries`] — deferred, consistent with existing clamp pattern, not a new design choice
- [x] [Review][Defer] `ResizeObserver` test stub always reports a fixed `320×90` and its mock entry is missing most of the real `ResizeObserverEntry` interface (`borderBoxSize`, etc.) — harmless today but a landmine for the next chart component that reads those fields. Also, nothing in the frontend defends against a `dailyConsumption` array with a length other than 7 or duplicate dates (React `key`/`Cell` collision), though the backend contract guarantees 7 unique dates by construction. [`client/src/test-setup.ts`, `client/src/features/dashboard/components/TrendChart.tsx`] — deferred, test-infra debt and defensive-only concern, no reachable bug today

### Review Findings (Round 2 — 2026-07-02, verifying round-1 patches)

_0 decision-needed, 3 patch (all applied), 6 defer, 8 dismissed as noise._

**Patch:**

- [x] [Review][Patch] AC-2's own text ("ending on the current UTC date") now contradicts the round-1 fix, which deliberately switched the `DailyConsumption` window to a fixed app timezone (`Europe/Berlin`) instead of UTC. Fixed: updated AC-2 wording to reflect the applied behavior. `_bmad-output/implementation-artifacts/3-5-trend-chart-and-spike-detection.md` (AC-2)
- [x] [Review][Patch] Dev Notes' "mandatory, implement exactly as specified" literal code blocks (`BuildDailySeries`, the `windowEnd` wiring snippet, `DetectSpikes`, and the Task 4 `TrendChart.tsx` template) were never updated after the round-1 patches — they still show the pre-patch code (no `AppTimeZone`/`ConvertTime`, no `threshold <= 0m` guard, no `timeZone: 'UTC'`). Anyone copying these blocks as ground truth (e.g. Story 3.6, the future Insights 30-day chart in Epic 8) would reintroduce all three fixed bugs. Fixed: synced all four code blocks to match the shipped implementation. `_bmad-output/implementation-artifacts/3-5-trend-chart-and-spike-detection.md` (Dev Notes)
- [x] [Review][Patch] `AppTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")` (introduced by the round-1 timezone patch) is an eager `static readonly` field with no fallback — the first use of `TimeZoneInfo` anywhere in the backend. If the runtime lacks IANA tzdata (e.g. `InvariantGlobalization` enabled, minimal container image), this throws on first access, wrapped in a `TypeInitializationException` that permanently fails every dashboard request for the process lifetime. Flagged independently by all three review layers. Fixed: wrapped the lookup in a try/catch falling back to `TimeZoneInfo.Utc`. [`api/Features/Dashboard/KpiCalculator.cs`]

**Defer:**

- [x] [Review][Defer] None of the three round-1 patches (timezone conversion, `threshold <= 0m` guard, UTC weekday label) have dedicated regression tests — the exact bugs just fixed have zero test coverage, so a future refactor could silently reintroduce any of them. [`api.Tests/Features/Dashboard/KpiCalculatorTests.cs`, `client/src/features/dashboard/components/TrendChart.test.tsx`] — deferred, test-coverage gap, no reachable bug today
- [x] [Review][Defer] No test covers a reading period that straddles the 7-day display window boundary (begins before the window, ends inside it) — all current window-related tests use readings entirely outside the asserted window, so the far more common real-world partial-window case is untested. [`api.Tests/Features/Dashboard/KpiCalculatorTests.cs`] — deferred, test-coverage gap
- [x] [Review][Defer] Spike days are communicated to the user via bar color alone (amber vs. translucent white), with no secondary indicator (pattern/icon/label) and no accessible text equivalent for the underlying kWh values — a WCAG 1.4.1 (Use of Color) concern, inconsistent with the component's otherwise accessibility-conscious patterns (44×44 tap targets, explicit `aria-label`). Needs a UX decision (tooltip, per-bar label, pattern fill), not a quick patch. [`client/src/features/dashboard/components/TrendChart.tsx`] — deferred, needs UX/accessibility design input
- [x] [Review][Defer] `TimeZoneInfo.ConvertTime` (introduced by the round-1 timezone patch) can throw for a `ReadingDate` near `DateTimeOffset.MaxValue`, whereas the previous `.Date` truncation silently produced a wrong-but-non-crashing date. Same root cause as the already-deferred "no upper bound on `ReadingDate`" gap, now with a crash instead of silently wrong data; requires malicious/corrupt input to reach, extremely low practical likelihood. [`api/Features/Dashboard/KpiCalculator.cs`] — deferred, same root cause as an already-deferred item, negligible reachability
- [x] [Review][Defer] `perDayKwh = periodKwh / spanDays` has no rounding, which can produce long repeating decimals — harmless today since nothing displays the raw per-day value, but worth rounding before any future tooltip/label surfaces it (the story's own reference mockup shows one as a nice-to-have). [`api/Features/Dashboard/KpiCalculator.cs:BuildDailySeries`] — deferred, no current consumer of the unrounded value
- [x] [Review][Defer] Extends the existing "TrendChart hides entry point at 0/1 readings" deferred item: because `DashboardPage.tsx` passes `isPending ? undefined : dashboard`, the card also flickers away on every background refetch that resolves back to a 0/1-reading state, not just on initial load. [`client/src/features/dashboard/DashboardPage.tsx`, `client/src/features/dashboard/components/TrendChart.tsx`] — deferred, same accepted cold-open decision, minor UX nuance

## Dev Notes

### Design Gap Resolved During Story Creation — Daily Series Derivation Algorithm

**Why this needed a decision:** FR-16/FR-17 and the epic AC describe "daily consumption" and a "7-day rolling average," but Meter Readings are entered at arbitrary, sparse intervals (Story 3.1 explicitly supports retroactive/irregular entry) — there is no `SmartPlugDailyData`-style daily table for meter readings (that table is Epic 6, R2, smart-plug-only, per AD-2). Neither the PRD, architecture, nor epic specify how to turn sparse point-in-time readings into a calendar-day series. This is the same category of gap Story 3.4 hit with `LastKwhValue` — a one-field/one-algorithm gap discovered during story creation, not a reinterpretation of scope. The algorithm below is the resolution; implement it exactly as specified so the tests below are deterministic.

**Algorithm — `BuildDailySeries`:** For each consecutive reading pair `(prev, next)`, the period's consumption (`Math.Max(0, next.KwhValue - prev.KwhValue)`, same clamping already used for `totalKwh`) is spread **evenly** across the calendar days the period spans, and accumulated into a `Dictionary<DateOnly, decimal>` (additive — a day touched by two different period boundaries sums both contributions). This mirrors the existing period-delta computation already in `KpiCalculator.Compute` (reuse the same `Math.Max(0m, ...)` clamp) but at daily granularity instead of full-history granularity.

```csharp
// Round-2 review update: bucketing now converts through a fixed AppTimeZone (Europe/Berlin)
// instead of each reading's raw stored offset — see Review Findings Round 2, Patch 1.
private static readonly TimeZoneInfo AppTimeZone = ResolveAppTimeZone();

private static TimeZoneInfo ResolveAppTimeZone()
{
    try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"); }
    catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
}

private static Dictionary<DateOnly, decimal> BuildDailySeries(IReadOnlyList<MeterReading> readings)
{
    var series = new Dictionary<DateOnly, decimal>();
    for (var i = 0; i < readings.Count - 1; i++)
    {
        var start = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(readings[i].ReadingDate, AppTimeZone).Date);
        var end = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(readings[i + 1].ReadingDate, AppTimeZone).Date);
        var periodKwh = Math.Max(0m, readings[i + 1].KwhValue - readings[i].KwhValue);
        var spanDays = Math.Max(1, end.DayNumber - start.DayNumber);
        var perDayKwh = periodKwh / spanDays;
        // Two readings on the same calendar day: attribute the whole delta to that one day.
        var firstDay = end.DayNumber > start.DayNumber ? start.DayNumber + 1 : end.DayNumber;
        for (var d = firstDay; d <= end.DayNumber; d++)
        {
            var date = DateOnly.FromDayNumber(d);
            series[date] = series.GetValueOrDefault(date) + perDayKwh;
        }
    }
    return series;
}
```

Note: the first reading's own date never receives a value from this loop (there's no prior period to derive it from) — this matches how `DailyAvgKwh` already requires ≥2 readings before producing anything non-zero.

**Algorithm — `DetectSpikes`:** Evaluates spikes **only within the 7-day display window** (nothing else consumes `SpikeDays` yet — the Insights tab's own 30-day chart is Epic 8/R2 and will need its own call with its own window when built). For each day in the window, the rolling average is computed from whichever of the preceding 7 calendar days actually have data in the series (partial baseline is fine — a brand-new Flat won't have 7 days of history yet). A day with **zero** prior days of data is never flagged (no baseline to compare against) and a day whose rolling average is exactly `0` is never flagged (avoids every first positive reading trivially "exceeding" a zero baseline). The comparison is strict `>` (exceeds), matching the epic's literal wording — a day exactly at `threshold × average` is NOT a spike.

```csharp
private static string[] DetectSpikes(
    Dictionary<DateOnly, decimal> dailySeries, DateOnly windowStart, DateOnly windowEnd, decimal threshold)
{
    var spikes = new List<string>();
    for (var date = windowStart; date <= windowEnd; date = date.AddDays(1))
    {
        var dayKwh = dailySeries.GetValueOrDefault(date);
        decimal rollingSum = 0m;
        var priorDaysWithData = 0;
        for (var lookback = 1; lookback <= 7; lookback++)
        {
            if (dailySeries.TryGetValue(date.AddDays(-lookback), out var priorKwh))
            {
                rollingSum += priorKwh;
                priorDaysWithData++;
            }
        }
        if (priorDaysWithData == 0 || threshold <= 0m) continue;
        var rollingAvg = rollingSum / priorDaysWithData;
        if (rollingAvg > 0m && dayKwh > threshold * rollingAvg)
            spikes.Add(date.ToString("yyyy-MM-dd"));
    }
    return spikes.ToArray();
}
```

**Wiring into `Compute`:** Add this block in the `readings.Count >= 2 && totalDays >= 1.0` branch (the main branch, right after the existing per-interval loop — it doesn't depend on tariffs so ordering relative to the cost block doesn't matter), and add `DailyConsumption: []` to the other three early-return branches (0 readings, 1 reading, sub-day span):

```csharp
var dailySeries = BuildDailySeries(readings);
var windowEnd = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, AppTimeZone).Date);
var windowStart = windowEnd.AddDays(-6);
var dailyConsumption = new List<DailyConsumptionPoint>();
for (var date = windowStart; date <= windowEnd; date = date.AddDays(1))
    dailyConsumption.Add(new DailyConsumptionPoint(date.ToString("yyyy-MM-dd"), dailySeries.GetValueOrDefault(date)));
var spikeDays = DetectSpikes(dailySeries, windowStart, windowEnd, flat.SpikeThreshold);
```

Then in the final `return new DashboardSummary(...)` of that branch: `SpikeDays: spikeDays` (replacing the current hardcoded `[]`) and `DailyConsumption: dailyConsumption.ToArray()`.

**New Backend Tests** (add to `KpiCalculatorTests.cs`, following the existing `MakeFlat`/`MakeReading`/`Now` fixture pattern — `Now` is fixed at `2026-06-30T12:00:00Z`):
1. `Compute_MultipleReadings_DailyConsumption_HasSevenEntriesEndingAtNowDate` — any 2+ readings with `totalDays >= 1`; assert `result.DailyConsumption.Length == 7`, first entry date `"2026-06-24"`, last entry date `"2026-06-30"` (window is `Now.Date` and the 6 preceding days).
2. `Compute_TwoReadingsWithinWindow_DistributesKwhEvenlyAcrossSpannedDays` — readings on `2026-06-27` (100 kWh) and `2026-06-29` (106 kWh); assert the `2026-06-28` and `2026-06-29` entries are each `3m` (6 kWh / 2 days), and `2026-06-27` is `0m` (first reading's own date gets nothing).
3. `Compute_DaySpikeAboveThreshold_IsFlaggedInSpikeDays` — construct 8+ daily readings such that 7 preceding days each contribute 1 kWh/day (rolling avg = 1) and the 8th day contributes 3 kWh (> 2.0× threshold); assert that day's date string is in `result.SpikeDays`.
4. `Compute_DayExactlyAtThreshold_IsNotFlaggedAsSpike` — same setup but the spike day is exactly `2 × rollingAvg`; assert `result.SpikeDays` does NOT contain that date (strict `>`, boundary anchor).
5. `Compute_CustomSpikeThreshold_UsesConfiguredValueNotDefault` — `MakeFlat` with `SpikeThreshold: 1.2m`; a day at 1.3× the rolling average should flag as a spike (would NOT flag at the default 2.0×) — proves the flat's configured value is actually read, not a hardcoded `2.0`.
6. `Compute_FirstDayInWindowNoPriorData_IsNotFlaggedAsSpike` — a lone reading pair landing on the very first readings ever, no prior 7 days of any data — assert not flagged even though its raw value could numerically exceed some default.
7. `Compute_ZeroOrOneReading_DailyConsumptionIsEmpty` — extend the existing `Compute_NoReadings_...` and `Compute_OneReading_...` tests with `result.DailyConsumption.ShouldBeEmpty()`.

Use `Shouldly` assertions (`ShouldBe`, `ShouldContain`, `ShouldNotContain`, `ShouldBeEmpty`) matching every existing test in the file.

### Story Boundary: Reading History Sheet (do not build Story 3.6 early)

Story 3.6 ("Reading History — View & Correction", currently `backlog`) owns: `GET /api/v1/flats/{flatId}/readings` (`GetReadingHistoryFunction` — **does not exist yet**), `useReadingHistory` hook, the reverse-chronological list, tap-to-edit, and the `PATCH .../readings/{readingId}` correction flow. None of that backend or data-fetching exists today. This story's AC-5 only requires that tapping the trend chart's clock/list icon **opens a bottom sheet** — it does not require the sheet to show real data (that's explicitly 3.6's job).

Build `ReadingHistorySheet.tsx` as a **minimal, inert placeholder**: a shadcn `Sheet` (reuse the already-generated `client/src/components/ui/sheet.tsx` from Story 3.4 — do not regenerate it) styled with the same glass/radius treatment as `EnterReadingSheet.tsx` (`rounded-t-sheet`, `border-glass-border`, `bg-[rgba(10,15,25,0.92)]` etc.), containing only a title (`t('history.title')`) and a static message (`t('history.comingSoon')`). No API calls, no `useReadingHistory` hook, no list rendering. When Story 3.6 is implemented, it extends this same file's body — it does not create a new file or a differently-named component.

### Task 4: `TrendChart.tsx`

```typescript
// client/src/features/dashboard/components/TrendChart.tsx
import { useMemo, useState } from 'react'
import { BarChart, Bar, XAxis, Cell, ResponsiveContainer } from 'recharts'
import { History } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { Sheet, SheetTrigger, SheetContent } from '@/components/ui/sheet'
import { ReadingHistorySheet } from '@/features/readings/components/ReadingHistorySheet'
import type { DashboardSummary } from '@/features/dashboard/api/dashboardApi'

type Props = {
  dashboard: DashboardSummary | undefined
  flatId: string | undefined
}

export function TrendChart({ dashboard, flatId }: Props) {
  const { t } = useTranslation('dashboard')
  const [historyOpen, setHistoryOpen] = useState(false)

  const spikeSet = useMemo(() => new Set(dashboard?.spikeDays ?? []), [dashboard?.spikeDays])
  const chartData = useMemo(
    () =>
      (dashboard?.dailyConsumption ?? []).map(point => ({
        date: point.date,
        kwh: point.kwhValue,
        label: new Intl.DateTimeFormat(i18n.language, { weekday: 'narrow', timeZone: 'UTC' }).format(
          new Date(point.date)
        ),
      })),
    [dashboard?.dailyConsumption]
  )

  // No trend data can exist yet with 0 or 1 total readings — hide the card rather than
  // render a misleading all-zero 7-day chart (mirrors the KPI tiles' cold-open dash state).
  if (dashboard !== undefined && chartData.length === 0) return null

  return (
    <div className="relative z-10 mx-4 mb-6 rounded-card border border-glass-border bg-glass-surface p-4 backdrop-blur-[20px] backdrop-saturate-[180%]">
      <div className="mb-3 flex items-center justify-between">
        <span className="text-label-caps text-text-tertiary">{t('trend.cardTitle')}</span>
        <Sheet open={historyOpen} onOpenChange={setHistoryOpen}>
          <SheetTrigger asChild>
            <button
              type="button"
              aria-label={t('trend.historyIconLabel')}
              className="flex h-11 w-11 items-center justify-center text-text-secondary"
            >
              <History size={20} />
            </button>
          </SheetTrigger>
          <SheetContent side="bottom">
            <ReadingHistorySheet flatId={flatId} />
          </SheetContent>
        </Sheet>
      </div>
      {dashboard === undefined ? (
        <div className="flex h-[90px] items-end gap-1.5">
          {Array.from({ length: 7 }).map((_, i) => (
            <div key={i} className="h-1/2 flex-1 animate-pulse rounded-t bg-white/10" />
          ))}
        </div>
      ) : (
        <ResponsiveContainer width="100%" height={90}>
          <BarChart data={chartData} barCategoryGap={6}>
            <XAxis
              dataKey="label"
              axisLine={false}
              tickLine={false}
              interval={0}
              tick={{ fill: 'rgba(255,255,255,0.35)', fontSize: 10 }}
            />
            <Bar dataKey="kwh" radius={[4, 4, 2, 2]}>
              {chartData.map(entry => (
                <Cell
                  key={entry.date}
                  fill={spikeSet.has(entry.date) ? 'var(--color-accent-spike)' : 'rgba(255,255,255,0.5)'}
                />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      )}
    </div>
  )
}
```

- No tap handler on `Cell`/`Bar` beyond recharts' defaults — AC-4 requires that tapping a spike bar produces **no** banner/notification, so deliberately don't wire `onClick`.
- The `Sheet`/`SheetTrigger`/`SheetContent` pattern here is a single shared root (the corrected pattern from Story 3.4's review-fix round, not the original dual-root bug) — follow this structure exactly, don't reintroduce two `Sheet` roots.
- `History` is the recommended lucide-react icon (clock-with-arrow, semantically "history"); any equivalent clock/list icon already available in `lucide-react` at 20×20px satisfies the AC if `History` doesn't read well — the icon choice itself isn't AC-critical, the size/label/target are.

### Why `ResizeObserver` Must Be Stubbed (Task 3)

`recharts`' `ResponsiveContainer` (`client/node_modules/recharts/es6/component/ResponsiveContainer.js`) sizes itself via the browser's real `ResizeObserver` API, calling `observer.observe(containerRef.current)` and reading `entry.contentRect` in the callback. jsdom does not implement `ResizeObserver` at all, and `getBoundingClientRect()` on any jsdom element always returns `{width: 0, height: 0}`. Without a stub, `ResponsiveContainer` will never receive a non-zero size and `TrendChart`'s bars will not render in tests — this will silently produce a chart-shaped test that always passes with zero assertions actually exercised, or fails confusingly depending on how the test is written. Add this guarded stub next to the existing `matchMedia` guard (same file, same pattern) so any future chart (e.g. the Insights 30-day chart, Epic 8) also benefits:

```typescript
// client/src/test-setup.ts — append after the matchMedia block
if (!window.ResizeObserver) {
  window.ResizeObserver = class {
    private callback: ResizeObserverCallback
    constructor(callback: ResizeObserverCallback) {
      this.callback = callback
    }
    observe(target: Element) {
      this.callback(
        [{ target, contentRect: { width: 320, height: 90 } } as ResizeObserverEntry],
        this as unknown as ResizeObserver
      )
    }
    unobserve() {}
    disconnect() {}
  }
}
```

### Task 8: Test Guidance

Follow the existing pattern from `client/src/features/dashboard/__tests__/DashboardGrid.test.tsx` (mock nothing beyond i18n/matchMedia which are already globally set up; render with prop fixtures directly).

**`TrendChart.test.tsx`** (minimum 6):
1. `dashboard === undefined` (loading): renders 7 skeleton placeholder bars, no `BarChart`
2. `dashboard` with `dailyConsumption: []` (0/1-reading state): component renders `null` — assert nothing renders (e.g. `container.firstChild` is null, or use `queryByLabelText` for the history icon and assert it's absent)
3. `dashboard` with 7 populated days, none in `spikeDays`: all bars render with the non-spike fill color
4. `dashboard` with one date present in `spikeDays` matching one of the 7 `dailyConsumption` dates: that bar renders with `fill: var(--color-accent-spike)` (or the resolved `#f59e0b`) — query via the rendered SVG `path`/`rect` fill attribute
5. History icon renders with `aria-label` matching the translation key and a `h-11 w-11` (44×44) tap target class
6. Clicking the history icon opens the sheet (assert `ReadingHistorySheet`'s title text becomes visible, mirroring how `EnterReadingSheet.test.tsx` asserts sheet-open state)

**`ReadingHistorySheet.test.tsx`** (minimum 2):
1. Renders the title and "coming soon" text from the `readings` namespace
2. Does not call any API module (no `vi.mock` needed for an api file — confirms no network hook is invoked; simplest check is that the component renders synchronously with no loading/error state)

### Architecture Compliance Checklist

- [ ] `import type` for all type-only imports (TS6 strict module mode)
- [ ] No barrel files — import directly from the declaring file
- [ ] `@/` alias for all imports — never relative paths
- [ ] No `!` non-null assertions in feature code
- [ ] All user-visible strings via `useTranslation('dashboard')` / `useTranslation('readings')` — no hardcoded strings in JSX
- [ ] Never hand-edit `client/src/components/ui/sheet.tsx` — reuse as-is, style via wrapper `className` only
- [ ] `decimal` care on the backend: `DailyConsumptionPoint.KwhValue` is `decimal`, `DetectSpikes`/`BuildDailySeries` use `decimal` arithmetic throughout, never `double`/`float`
- [ ] `CancellationToken` threading unaffected — no new async calls introduced in `KpiCalculator` (still pure computation, no DB access)
- [ ] JSON field naming stays camelCase automatically via the existing `Program.cs` serializer config — no manual attribute needed on `DailyConsumptionPoint`

### Previous Story Intelligence (Story 3.4)

- `useUserSettings()` shape is `{ settings, isLoading, isError }`; `useDashboard(flatId)` returns the full TanStack Query result (`data`, `isPending`, `isError`) — confirmed again from the current `DashboardPage.tsx`.
- Generated shadcn primitives (`ui/sheet.tsx`, from Story 3.4) are intentionally left stock/non-functional-looking; all real theming happens in the feature-folder wrapper component. `ReadingHistorySheet.tsx` follows the same convention already established by `EnterReadingSheet.tsx` — don't touch `ui/sheet.tsx`.
- The single-shared-`Sheet`-root pattern (one `<Sheet>` wrapping one `<SheetTrigger asChild>` + one `<SheetContent>`) is the *corrected* pattern after Story 3.4's review found the original dual-root version produced orphaned `aria-controls`. `TrendChart.tsx` must use the single-root form from the start — do not reintroduce the dual-root bug.
- `window.matchMedia` is stubbed in `client/src/test-setup.ts` (guarded `if (!window.matchMedia)`) — this story adds a second, similarly-guarded stub for `ResizeObserver` right next to it.
- `class-variance-authority` and `@radix-ui/react-dialog` are already dependencies (added in Stories 3.3/3.4) — `Sheet` reuse in this story adds no new npm dependencies except `recharts`, which is **already** in `client/package.json` (`"recharts": "^3.9.0"`) but has not been used by any component yet — this is the first real usage; `client/node_modules/recharts/es6/component/ResponsiveContainer.js` was read directly during story creation to confirm the `ResizeObserver` dependency (see Dev Notes above), so this isn't a guess.

### Git Intelligence

Recent commits (`8b1c6c1` Story 3.1 backend, `8432120`/`75925fb`/`80604ce` Story 3.2 backend, `003d8a0` Story 3.3 frontend, `6afb7b5` Story 3.4 CTA+sheet+animation) show backend-first-when-needed, frontend-VSA-slice-per-story rhythm, with small, explicitly-scoped backend additions bolted onto otherwise-frontend stories when the API contract is missing a field (3.4's `LastKwhValue`). This story follows the same shape: one backend algorithm addition (`KpiCalculator`) plus one new frontend component pair (`TrendChart`, `ReadingHistorySheet`). Keep the backend change scoped to the daily-series/spike-detection algorithm only — do not refactor the existing per-interval cost loop or touch `GetDashboardFunction.cs` (it already passes `flat` into `Compute`, which is all `DetectSpikes` needs).

### Project Structure — New/Modified Files

```
api/Features/Dashboard/
├── DashboardModels.cs               ← MODIFY (add DailyConsumptionPoint, DailyConsumption field)
└── KpiCalculator.cs                 ← MODIFY (BuildDailySeries, DetectSpikes, wire into 4 branches)

api.Tests/Features/Dashboard/
├── KpiCalculatorTests.cs            ← MODIFY (7 new tests + 2 existing-test assertions)
└── GetDashboardFunctionTests.cs     ← MODIFY (1 structural assertion)

client/src/features/dashboard/
├── api/dashboardApi.ts              ← MODIFY (add DailyConsumptionPoint type, dailyConsumption field)
├── components/
│   ├── TrendChart.tsx               ← NEW
│   └── TrendChart.test.tsx          ← NEW
├── DashboardPage.tsx                ← MODIFY (render TrendChart below DashboardGrid)
└── __tests__/
    ├── DashboardGrid.test.tsx       ← MODIFY (add dailyConsumption: [] to fixtures)
    └── useDashboard.test.ts         ← MODIFY (add dailyConsumption: [] to fixture)

client/src/features/readings/components/
├── ReadingHistorySheet.tsx          ← NEW (minimal placeholder — see Story Boundary note)
└── ReadingHistorySheet.test.tsx     ← NEW

client/src/test-setup.ts             ← MODIFY (add ResizeObserver stub)

client/src/locales/en-US/dashboard.json  ← MODIFY (trend.cardTitle, trend.historyIconLabel)
client/src/locales/de-DE/dashboard.json  ← MODIFY (German equivalents)
client/src/locales/en-US/readings.json   ← MODIFY (history.title, history.comingSoon)
client/src/locales/de-DE/readings.json   ← MODIFY (German equivalents)
```

### References

- Epic source: `_bmad-output/planning-artifacts/epics/epic-3-meter-reading-kpi-dashboard-reading-history.md#Story 3.5`
- FR-16/FR-17: `_bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md` §4.6
- `Flat.SpikeThreshold` origin: `_bmad-output/planning-artifacts/epics/epic-2-onboarding-locale-selection.md` (Story 2.4 AC) — already implemented in `api/Data/Entities/Flat.cs` and `api/Data/Configurations/FlatConfiguration.cs` (default `2.0m`, no migration needed for this story)
- Architecture placement: `_bmad-output/planning-artifacts/architecture.md` (`TrendChart.tsx` under `dashboard/components/`; `KpiCalculator.cs` owns spike detection)
- Design tokens (`accent-spike`, glass card, `label-caps`): `_bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/DESIGN.md` — all referenced tokens already exist in `client/src/index.css` (`--color-accent-spike: #f59e0b`, `--color-glass-surface`, `--color-glass-border`, `--radius-card`, `text-label-caps` utility) — no new theme tokens required
- Visual reference (7-bar layout, day-of-week labels, budget line, amber spike bar): `_bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/mockups/euro-burn-dashboard.html` lines 704–745 (`.trend-card`, `.chart-area`, `.bar-spike`) — read directly during story creation; the budget reference line shown there is a nice-to-have, not a required AC
- Reading history entry point / Story 3.6 boundary: `_bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/EXPERIENCE.md` lines 34, 98–99, 184, 321
- Previous story: `_bmad-output/implementation-artifacts/3-4-enter-reading-cta-bottom-sheet-and-immediate-dashboard-update.md` (Sheet pattern, `matchMedia` stub precedent, `LastKwhValue` append-only precedent)
- Existing code read in full during story creation: `api/Features/Dashboard/{DashboardModels,KpiCalculator,GetDashboardFunction}.cs`, `client/src/features/dashboard/{api/dashboardApi.ts,DashboardPage.tsx,components/DashboardGrid.tsx,hooks/useDashboard.ts}`, `client/src/features/readings/components/EnterReadingSheet.tsx`, `client/src/test-setup.ts`, `api.Tests/Features/Dashboard/{KpiCalculatorTests,GetDashboardFunctionTests}.cs`

## Change Log

- Story created: 2026-07-02 — Trend Chart & Spike Detection; Epic 3 fifth story; resolved two undocumented gaps during creation (daily-series derivation algorithm for sparse meter readings; Story 3.5/3.6 Reading History sheet boundary)
- Story implemented: 2026-07-02 — all 10 tasks complete; backend daily-series/spike-detection algorithm, `TrendChart.tsx`, `ReadingHistorySheet.tsx` placeholder, wired into `DashboardPage.tsx`; 69 backend tests and 90 frontend tests passing (0 regressions)

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- Discovered during Task 8: recharts `Bar` entrance animation renders at `height: 0` on first paint because `animationElapsedTime` never advances past `0` in jsdom (no real `requestAnimationFrame` ticking) — `Rectangle` bails out and renders no `<path>` when `height === 0`, so bars were invisible to test queries. Fixed by adding `isAnimationActive={false}` to the `<Bar>` in `TrendChart.tsx` (verified via an isolated recharts repro harness logging the raw shape props before/after the flag). This is a deviation from the Dev Notes literal code template (which didn't include the prop) — the story author's template was not itself run through this project's Vitest/jsdom setup, so this issue wasn't caught during story creation; the fix is scoped to `TrendChart.tsx` only.

### Completion Notes List

- Task 0: `BuildDailySeries`/`DetectSpikes` implemented exactly per Dev Notes algorithm; 7 new `KpiCalculatorTests` + 2 extended assertions + 1 `GetDashboardFunctionTests` assertion, all passing; `MakeFlat` test helper gained an optional `spikeThreshold` parameter (previously hardcoded `2.0m`) to support the custom-threshold test.
- Tasks 1–2: `dashboardApi.ts` and both dependent test fixtures updated append-only, matching the `LastKwhValue` precedent from Story 3.4.
- Task 3: `ResizeObserver` stub added to `test-setup.ts`, guarded like the existing `matchMedia` stub.
- Task 4: `TrendChart.tsx` built from the Dev Notes literal template with one addition — `isAnimationActive={false}` on `<Bar>` (see Debug Log) — and a `className` added to `SheetContent` in the trigger sheet to apply the same glass/rounded-sheet treatment as `EnterReadingSheet.tsx` (the literal template left `SheetContent` unstyled; styling is delegated to `ReadingHistorySheet.tsx`'s Story Boundary guidance rather than contradicted).
- Task 5: `ReadingHistorySheet.tsx` built as a minimal placeholder (drag handle, title, static message) rendered as a plain child of `TrendChart`'s own `<SheetContent>` — not a second `Sheet`/`SheetContent` root — per the single-shared-root pattern from Story 3.4's review fix.
- Task 6: `TrendChart` wired into `DashboardPage.tsx` below `DashboardGrid`, sharing the same `isPending ? undefined : dashboard` prop resolution already used for the grid.
- Task 7: 4 new translation keys added across `en-US`/`de-DE` `dashboard.json` and `readings.json`; both namespaces were already registered in `i18n.ts`.
- Task 8: `TrendChart.test.tsx` (6 tests) and `ReadingHistorySheet.test.tsx` (2 tests) — all passing.
- Task 9: `dotnet test api.Tests` → 69/69 passing; `npm run build` → 0 TypeScript errors; `npm test` → 90/90 passing (17 files, 0 regressions); `npm run lint` → exit 0 (only pre-existing, unrelated `router.tsx` warnings).

### File List

**Backend**
- `api/Features/Dashboard/DashboardModels.cs` (MODIFY) — added `DailyConsumptionPoint` record, `DailyConsumption` field on `DashboardSummary`
- `api/Features/Dashboard/KpiCalculator.cs` (MODIFY) — added `BuildDailySeries`, `DetectSpikes`; wired into all four `Compute` branches
- `api.Tests/Features/Dashboard/KpiCalculatorTests.cs` (MODIFY) — 7 new tests, 2 extended assertions, `MakeFlat` gained `spikeThreshold` parameter
- `api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs` (MODIFY) — 1 new structural assertion

**Frontend**
- `client/src/features/dashboard/api/dashboardApi.ts` (MODIFY) — added `DailyConsumptionPoint` type, `dailyConsumption` field
- `client/src/features/dashboard/components/TrendChart.tsx` (NEW)
- `client/src/features/dashboard/components/TrendChart.test.tsx` (NEW)
- `client/src/features/dashboard/DashboardPage.tsx` (MODIFY) — renders `TrendChart` below `DashboardGrid`
- `client/src/features/dashboard/__tests__/DashboardGrid.test.tsx` (MODIFY) — added `dailyConsumption: []` to fixtures
- `client/src/features/dashboard/__tests__/useDashboard.test.ts` (MODIFY) — added `dailyConsumption: []` to fixture
- `client/src/features/readings/components/ReadingHistorySheet.tsx` (NEW)
- `client/src/features/readings/components/ReadingHistorySheet.test.tsx` (NEW)
- `client/src/test-setup.ts` (MODIFY) — added guarded `ResizeObserver` stub
- `client/src/locales/en-US/dashboard.json` (MODIFY) — added `trend.cardTitle`, `trend.historyIconLabel`
- `client/src/locales/de-DE/dashboard.json` (MODIFY) — German equivalents
- `client/src/locales/en-US/readings.json` (MODIFY) — added `history.title`, `history.comingSoon`
- `client/src/locales/de-DE/readings.json` (MODIFY) — German equivalents

**Story tracking**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (MODIFY) — status transitions
- `_bmad-output/implementation-artifacts/3-5-trend-chart-and-spike-detection.md` (MODIFY) — this file
