---
baseline_commit: 80e934ae56c6bd71ba23c27b55eea3f419719084
---

# Story 10.4: Insights Tab — Trend Chart, Insight Cards & Discovery Progress

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to open the Insights tab and see my 30-day trend chart alongside auto-discovered insight cards, with a visible progress indicator during a discovery run and prior cards staying visible underneath,
And when I tap "Refresh insights" it immediately shows the run in progress rather than waiting.

## Acceptance Criteria

1. **`InsightsTab.tsx` mounts.** Renders inside the existing `InsightsPage.tsx` stub (already routed at `/insights` in `router.tsx`, already registered in `i18n.ts`'s `ns` array with empty `insights.json` locale files). The `TrendChart` component (from Epic 3, `client/src/features/dashboard/components/TrendChart.tsx`) renders at the top at full width, defaulting to a **30-day** window (see AC #11–#13 — the existing component defaults to 7 days and must be extended, not forked) and user-adjustable to **7 / 30 / 90 days** via the period selector (AC #15). Below the chart, `useInsights(flatId)` queries `GET /api/v1/flats/{flatId}/insights` (`['insights', flatId]`). A "Refresh insights" button is present.

2. **Discovery in progress.** Given `useInsights(flatId).data.runStatus?.status` is `'Processing'` or `'Pending'`, `InsightDiscoveryProgress.tsx` renders above the insight cards with an animated progress indicator and the i18n label `insights:progress.label` ("Discovering insights…"). Prior insight cards from the previous run remain visible and interactive below the progress component. `useInsights` polls every 5 seconds via `refetchInterval` while status is `Pending`/`Processing`; the poll that observes `Complete` or `Failed` is itself the final refresh (FR-39) — no extra fetch step needed, mirrors `useImportJobStatus.ts`'s pattern exactly.

3. **Discovery complete with results.** Given `runStatus?.status === 'Complete'` and `insights.length > 0`: `InsightDiscoveryProgress.tsx` is hidden; `InsightCard.tsx` renders one card per insight in a scrollable vertical list (`grid grid-cols-1 gap-3 md:grid-cols-2` — the exact responsive-grid convention used by `RoomCard.tsx`/`SmartStripCard.tsx`); cards ordered by `createdAt desc` (already the case — `GetInsightsFunction.cs` returns them pre-sorted `OrderByDescending(i => i.CreatedAt)`, no client-side re-sort needed).

4. **`InsightCard` — `type: 'Standby'`.** Shows: standby icon; `data.deviceName`; `t('card.standby.watts', { watts: data.meanStandbyWatts })` → "Drawing {{watts}} W outside usage hours"; `t('card.standby.cost', { cost: formatCurrency(data.estimatedMonthlyCost) })` → "Estimated monthly cost: {{cost}}", `formatCurrency` = `Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR' })` (exact pattern from `DashboardGrid.tsx`/`RoomCard.tsx`).

5. **`InsightCard` — `type: 'Replacement'`.** Shows: replacement icon; `data.deviceName`; `t('card.replacement.annualCost', { cost: formatCurrency(data.estimatedAnnualCost) })`; `t('card.replacement.savings', { savings: formatCurrency(data.estimatedSavingsEur) })` → "Potential savings: {{savings}}/year"; `data.suggestedClass` shown as the suggested EU label class.

6. **`InsightCard` — `type: 'Budget'`.** Shows: `border-l-[3px] border-accent-error` left border (the `accent-error` token already exists — `client/src/index.css:22`, `--color-accent-error: #f87171`); budget warning icon; `t('card.budget.projected', { cost: formatCurrency(data.projectedAnnualCost) })`; `t('card.budget.planned', { cost: formatCurrency(data.plannedAnnualSpend) })`; `t('card.budget.overspend', { cost: formatCurrency(data.overspendEur) })` → "Over by: {{cost}}".

7. **`InsightCard` — `type: 'InvoiceDeviation'`.** Shows: invoice icon; `t('card.invoiceDeviation.projected', { kwh: formatKwh(data.projectedAnnualKwh) })` → "Projected annual usage: {{kwh}} kWh"; `t('card.invoiceDeviation.baseline', { kwh: formatKwh(data.baselineKwh) })` → "Your baseline: {{kwh}} kWh"; `t('card.invoiceDeviation.delta', { delta: formatCurrency(Math.abs(data.impliedDeltaEur)) })` → "Implied difference: {{delta}}" plus a direction indicator driven by `data.direction` (`'above' | 'below'`) — e.g. an up/down arrow or `t('card.invoiceDeviation.above')`/`t('card.invoiceDeviation.below')` suffix. `formatKwh` = `Intl.NumberFormat(i18n.language, { maximumFractionDigits: 1 })` (exact pattern from `ResidualCard.tsx`/`DeviceCard.tsx`).

8. **Empty result set.** Given `runStatus?.status === 'Complete'` and `insights.length === 0`: show `t('emptyState.noFindings')` → "No findings this run. Everything looks within normal range."; trend chart remains visible above.

9. **Insufficient data.** Given the state-precedence resolution in AC #14 selects "insufficient data": show `t('emptyState.insufficientData')` → "Not enough data for insights. Add readings and import smart plug data to generate insight cards."; trend chart still renders if any readings exist (it already does — `TrendChart`'s only hide condition is `dashboard !== undefined && chartData.length === 0`, i.e. truly zero usable readings, unaffected by this story).

10. **Trigger a refresh.** `useTriggerInsights(flatId)` fires `POST /api/v1/flats/{flatId}/insights/trigger` when "Refresh insights" is tapped. On the `202` response (`TriggerInsightsFunction.cs` always returns `AcceptedResult`, never a bare 200 — do not check `res.ok` against `200`), `queryClient.invalidateQueries({ queryKey: ['insights', flatId] })` fires in `onSuccess`, immediately refetching and transitioning the UI to the "Processing" state per AC #2. The button is `disabled` while `runStatus?.status` is `'Pending'` or `'Processing'`.

11. **Backend — parametrize the trend window (closes an epic/PRD gap).** The epic's AC #1 assumes the Epic-3 `TrendChart` already supports a 30-day view; it doesn't — `KpiCalculator.cs:136` hardcodes `windowStart = windowEnd.AddDays(-6)` (7 days total), and PRD **FR-16** explicitly requires *"The Insights tab's trend chart defaults to a 30-day period — a broader window than the Dashboard's shorter sparkline."* `GetDashboardFunction.cs`'s route (`v1/flats/{flatId}/dashboard`) gains an optional `?days=` query parameter (`req.Query["days"]`, `int.TryParse`, default/fallback `7` when absent or non-positive — not a hard validation error, this is a display parameter, not user input). `KpiCalculator.Compute` gains a trailing `int days = 7` parameter; `windowStart = windowEnd.AddDays(-(days - 1))` generalizes the existing `AddDays(-6)`. `DetectSpikes` already takes `windowStart`/`windowEnd` as parameters and needs **no change** — its 7-day rolling-average lookback is independent of the outer window size. The Dashboard tab's existing call site is untouched (no `days` param → defaults to 7 → byte-for-byte same behavior as today — **no regression**).

12. **Backend — expose reading-history length for the "insufficient data" gate.** `DashboardSummary` gains a new field `ReadingHistoryDays` (int, unconditional — not gated on tariff configuration, unlike `Cost.TotalDays`) so the frontend has a real signal for AC #9/#14 instead of guessing from `dailyConsumption`'s fixed-length zero-padded array. Computed from the *already-existing* `totalDays` local (`KpiCalculator.cs:52`, full reading-history span, independent of the `days` window param) as `(int)Math.Floor(totalDays)`. The three early-return branches (0 readings, 1 reading, sub-day span — `KpiCalculator.cs:37-60`) all set it to `0`. JSON field: `readingHistoryDays` (camelCase, per project convention).

13. **Frontend — `TrendChart` accepts an optional `days` prop.** Default `7` (Dashboard's existing call site — `DashboardPage.tsx:48` — is unchanged and needs no edit). Two behavior changes gated on `days`: (a) `useDashboard`/`dashboardApi.getDashboard` must pass the window size to the backend (`?days=` query string) and the query key must include it — `['dashboard', flatId, { days }]`, per the project's established `[resource, flatId, { ...params }]` key convention — so the Dashboard tab's 7-day cache entry and the Insights tab's 7/30/90-day cache entries (AC #15) never collide or overwrite each other; (b) the x-axis label formatting: the current `weekday: 'narrow'` per-bar label (fine for 7 bars) is illegible/repetitive across 30 or 90 bars — for `days > 7`, use sparse ticks (e.g. `recharts`' `interval` prop set so ~5–6 labels show regardless of `days`) formatted with `Intl.DateTimeFormat(i18n.language, { day: 'numeric', month: 'short' })` instead of the narrow-weekday format; this must generalize across all three selectable periods, not just handle 30 as a special case. **Existing reading-correction invalidations need no change** — `useSubmitReading.ts`/`usePatchReading.ts` already call `invalidateQueries({ queryKey: ['dashboard', flatId] })` (no `days` in the key), and TanStack Query v5 invalidation is prefix-matching by default, so the 7-day, 30-day, and 90-day cached queries are all correctly invalidated by the existing 2-element key.

14. **`InsightsTab` state precedence — resolved decision, not fully spelled out by the epic.** The epic's AC #9 condition ("no completed run exists and fewer than 30 days of readings") and AC #8's condition (`Complete` + empty `insights`) can both describe an empty-array response, so precedence must be explicit. Using `runStatus` from `useInsights` and `readingHistoryDays` from `useDashboard(flatId, 30)`, evaluate in this order:
   1. `runStatus?.status === 'Pending' || runStatus?.status === 'Processing'` → progress state (AC #2), regardless of the other signals.
   2. `insights.length > 0` → render cards (AC #3–#7), regardless of run status or `readingHistoryDays`.
   3. `readingHistoryDays < 30` → "insufficient data" empty state (AC #9).
   4. Otherwise (≥30 days of history, no insights, not currently running — covers both `runStatus === null`, i.e. no `InsightRun` has ever been created for this flat yet, and `runStatus.status === 'Complete'` with a genuine zero-finding result) → "no findings" empty state (AC #8).

   `runStatus` is `null` (not an object with empty fields) when no `InsightRun` row exists yet for the flat — `GetInsightsFunction.cs:45-47` returns `RunStatusDto? runStatus = mostRecentRun is null ? null : ...`. Guard every `runStatus.status` access with `?.`.

15. **Period selector — added per product decision, extends the epic's original scope.** The Insights trend chart lets the user pick among **7 / 30 / 90 days**, defaulting to **30 days** (FR-16's default). New `InsightsPeriodSelector.tsx` mirrors `decomposition/components/PeriodSelector.tsx`'s shadcn `Popover` dropdown pattern (trigger button reading `"{{days}} days ▾"`, `PopoverContent` with three `role="option"` buttons, `aria-haspopup="listbox"`/`role="listbox"` — the same accessible-dropdown shape already vetted by Story 8.3's overlay/dropdown-visibility audit, FR-46) rather than a free-form date-range picker — there is no "custom" option, only the three fixed values. It renders in the trend card header (alongside or replacing the existing history icon — developer's placement call, both must remain reachable and tappable at the 44px minimum touch target). Selecting a period re-drives `useDashboard(flatId, days)` — this is exactly AC #13's existing `days` param/query-key plumbing; Task 1's backend `days` handling is already fully generic (any positive value works, not just 7), so **no additional backend change is needed for 90**, only the value the frontend happens to send. **Scope boundary:** only the trend chart's window is affected. The Insights list's `readingHistoryDays < 30` empty-state gate (AC #14) and the detectors' own fixed 30/60-day minimums (Story 10.3) are completely independent of this selector and must stay hard-coded to their existing thresholds regardless of which period the user has picked for the chart.

## Tasks / Subtasks

- [x] Task 1: Backend — parametrize dashboard trend window + expose reading-history length (AC: #11, #12, #15)
  - [x] `KpiCalculator.Compute(..., int days = 7)`; replace `windowStart = windowEnd.AddDays(-6)` with `windowEnd.AddDays(-(days - 1))`
  - [x] Add `ReadingHistoryDays` to `DashboardSummary` (`DashboardModels.cs`); set `(int)Math.Floor(totalDays)` on the main path, `0` on all three early-return branches
  - [x] `GetDashboardFunction.cs`: parse optional `req.Query["days"]` (`int.TryParse`, fallback `7` on absence/parse failure/non-positive value); clamp to a defensive upper bound (e.g. `Math.Min(days, 365)`) since it's client-controlled input — no need to allowlist exactly `{7, 30, 90}`, any reasonable positive value must work generically since the frontend now sends three different values (AC #15) and may add more later; pass through to `calculator.Compute(flat, readings, tariffs, DateTimeOffset.UtcNow, days)`
  - [x] Update `KpiCalculatorTests.cs`/`GetDashboardFunctionTests.cs`: add cases for `days: 30` and `days: 90` (window/spike detection over both windows) and assert `ReadingHistoryDays` on existing fixture scenarios (0/1/short/long history)
- [x] Task 2: Frontend — `insightsApi.ts` + hooks (AC: #1, #2, #10, #14)
  - [x] `client/src/features/insights/api/insightsApi.ts`: `RunStatusDto`, discriminated-union `InsightDto` (`Standby`/`Replacement`/`Budget`/`InvoiceDeviation`, each with its own typed `data` shape per AC #4–#7), `InsightsResponse` (`runStatus: RunStatusDto | null`), `getInsights(flatId)`, `triggerInsights(flatId)`
  - [x] `client/src/features/insights/hooks/useInsights.ts`: `useQuery({ queryKey: ['insights', flatId], queryFn: () => getInsights(flatId as string), enabled: !!flatId, refetchInterval: ... })` — mirror `useImportJobStatus.ts`'s `refetchInterval` predicate (5000ms while `Pending`/`Processing`, else `false`)
  - [x] `client/src/features/insights/hooks/useTriggerInsights.ts`: `useMutation` + `await queryClient.invalidateQueries({ queryKey: ['insights', flatId] })` in `onSuccess`
- [x] Task 3: Frontend — extend `dashboardApi`/`useDashboard`/`TrendChart` for a parametrized window (AC: #1, #13)
  - [x] `dashboardApi.ts`: `DashboardSummary` gains `readingHistoryDays: number`; `getDashboard(flatId: string, days = 7)` appends `?days=${days}` to the request path
  - [x] `useDashboard.ts`: accept `days = 7`; `queryKey: ['dashboard', flatId, { days }]`
  - [x] `TrendChart.tsx`: accept optional `days?: number` prop (default `7`); switch x-axis label formatting per AC #13(b) when `days > 7`, generalized for 30 and 90, not a single 30-only branch; `DashboardPage.tsx`'s existing `<TrendChart dashboard=... flatId=... />` call needs no changes (implicit default)
  - [x] Update `TrendChart.test.tsx`, `useDashboard.test.ts`, `DashboardGrid.test.tsx` (and any other `DashboardSummary` fixture) to include `readingHistoryDays`
- [x] Task 4: Frontend — `InsightsPeriodSelector.tsx` + period state wiring (AC: #15)
  - [x] New component in `client/src/features/insights/components/`, mirrors `decomposition/components/PeriodSelector.tsx`'s `Popover`/`PopoverTrigger`/`PopoverContent` structure (shadcn `Popover`, `role="listbox"`/`role="option"`); three fixed options `[7, 30, 90]`, no custom range, no popover-in-popover/sheet nesting (per project-context.md's dropdown/overlay rules)
  - [x] `InsightsTab.tsx` owns `const [days, setDays] = useState<7 | 30 | 90>(30)`; passed to both `useDashboard(flatId, days)` and `<TrendChart days={days} .../>`; unrelated to the `readingHistoryDays`/insights-empty-state logic (AC #14), which stays fixed at the 30-day threshold regardless of this selection
- [x] Task 5: Frontend — `InsightCard.tsx` (AC: #4, #5, #6, #7)
  - [x] One component, `switch`/discriminated union on `insight.type`; icon + border-left accent per type (reuse existing tokens — `accent-spike` amber for Standby, `accent-under-budget`/`accent-success` green for Replacement, `accent-info` blue for InvoiceDeviation, `accent-error` red mandated for Budget by AC #6); `formatCurrency`/`formatKwh` helpers per AC #4/#7
  - [x] `deviceId` on `InsightDto` is present but unused by any AC's rendering spec (card text uses `data.deviceName` from the JSON payload, not a device lookup) — do not add a device deep-link, out of scope
- [x] Task 6: Frontend — `InsightDiscoveryProgress.tsx` (AC: #2)
  - [x] Visual style: mirror `ImportProgressCard.tsx`'s "processing" branch (amber-tinted glass card per DESIGN.md's shared processing-state pattern, `animate-spin` ring) — this is the established app-wide progress-indicator look, not a new pattern
- [x] Task 7: Frontend — `InsightsTab.tsx` composition + state precedence (AC: #1, #2, #3, #8, #9, #14, #15)
  - [x] Structure mirrors `DecompositionTab.tsx`: header, loading skeleton, error+retry, then the AC #14 precedence chain
  - [x] `InsightsPage.tsx` (already a routed stub) renders `InsightsTab.tsx` with `flatId` from `useUserSettings().settings?.flatId`, matching `DecompositionPage.tsx`'s `DecompositionRoot` composition
  - [x] `InsightsPeriodSelector.tsx` (Task 4) renders in the trend card header; its `days` state feeds `TrendChart`/`useDashboard` only — do not thread it into the AC #14 precedence chain or the insights list in any way
- [x] Task 8: i18n content (AC: all)
  - [x] Populate `client/src/locales/en-US/insights.json` and `de-DE/insights.json` (currently `{}` stubs) with every key referenced above: `progress.label`, `card.standby.*`, `card.replacement.*`, `card.budget.*`, `card.invoiceDeviation.*`, `emptyState.noFindings`, `emptyState.insufficientData`, refresh button label, `period.sevenDays`/`period.thirtyDays`/`period.ninetyDays`, trend chart card title/history icon label (reuse `dashboard:trend.cardTitle`/`dashboard:trend.historyIconLabel` keys if `TrendChart` is rendered with the `dashboard` namespace already active, or add `insights`-namespaced equivalents if the component's `useTranslation('dashboard')` call stays fixed — confirm which before writing keys, see Dev Notes)
- [x] Task 9: Tests (AC: all)
  - [x] Backend: `KpiCalculatorTests.cs` (days param incl. 90, `ReadingHistoryDays`), `GetDashboardFunctionTests.cs` (`?days=30`/`?days=90` query string parsing, default fallback, clamp)
  - [x] Frontend: `useInsights.test.ts`, `useTriggerInsights.test.ts`, `InsightCard.test.tsx` (one case per type), `InsightDiscoveryProgress.test.tsx`, `InsightsPeriodSelector.test.tsx` (selecting each option calls `onChange`), `InsightsTab.test.tsx` (all four AC #14 precedence branches, the trigger-button-disabled state, and that switching the period selector does not change which empty/progress/card state is shown), `TrendChart.test.tsx` (30-day and 90-day label formatting cases)

### Review Findings

- [x] [Review][Patch] Failed discovery run is indistinguishable from "no findings" — When `runStatus?.status === 'Failed'`, `InsightsTab.tsx`'s AC #14 precedence chain (which has no `Failed` branch) falls through to the "otherwise" case and shows `emptyState.noFindings` ("Everything looks within normal range"), silently hiding a real failure. Fixed (2026-07-26): added a new `emptyState.runFailed` i18n string (en-US/de-DE) and a branch on `runStatus?.status === 'Failed'` before the `readingHistoryDays` check. [`client/src/features/insights/components/InsightsTab.tsx:100-101`]

- [x] [Review][Patch] AC #14 precedence violated: discovery progress renders simultaneously with empty-state text — While `isDiscovering` is true and `insights.length === 0` (the very first run for a flat), `InsightDiscoveryProgress` renders *alongside* `emptyState.noFindings`/`emptyState.insufficientData` instead of instead of it, contradicting AC #14 step 1 ("progress state ... regardless of the other signals"). Fixed (2026-07-26): added an `isDiscovering ? null : ...` branch between the cards check and the empty-state chain, so the empty-state text is suppressed while discovering — but the `insights.length > 0` cards branch is intentionally left ungated by `isDiscovering` per AC #2's requirement that prior cards stay visible below the progress indicator. [`client/src/features/insights/components/InsightsTab.tsx:94-106`]

- [x] [Review][Patch] Primary (period-selected) dashboard query's errors are never surfaced or retried — `isError`/retry only check `isHistoryError`/`isInsightsError`; the `dashboard` query driving the visible `TrendChart` has no `isError` check and its `refetch` is never called by the retry button, so a failed fetch leaves the chart stuck on its loading skeleton with no way to recover. Fixed (2026-07-26): destructured `isError`/`refetch` from the primary `useDashboard` call and included both in `isError`/the retry handler. [`client/src/features/insights/components/InsightsTab.tsx:17-22,38,79`]

- [x] [Review][Patch] `useInsights`'s `refetchInterval` omits the error-guard the spec requires it to mirror — AC #2 says to mirror `useImportJobStatus.ts`'s polling predicate "exactly," but that precedent also returns `false` when `query.state.status === 'error'`; `useInsights.ts` omits this, so a transient fetch failure during an active run leaves stale `Processing` data cached and polling continues every 5s indefinitely instead of backing off. Fixed (2026-07-26): added the matching `if (query.state.status === 'error') return false` guard. [`client/src/features/insights/hooks/useInsights.ts:8-11`]

- [x] [Review][Patch] Refresh button has no double-submit guard against its own in-flight request — `disabled={isDiscovering}` only reflects server-reported `runStatus` (populated after the invalidated query refetches); nothing checks `triggerInsights.isPending`, so two quick clicks can fire two trigger mutations before the first round-trip completes. Fixed (2026-07-26): added `|| triggerInsights.isPending` to the `disabled` condition. [`client/src/features/insights/components/InsightsTab.tsx:57`]

- [x] [Review][Patch] Refresh button stays enabled and fails silently when `flatId` is undefined — With `flatId` undefined (e.g. settings still loading), `isDiscovering` is `false` (disabled `useInsights` query never runs), so the button is clickable. Clicking calls `triggerInsights.mutate()`, whose `mutationFn` throws `Error('flatId is required')`; nothing reads `triggerInsights.isError`, so the click silently does nothing. Fixed (2026-07-26): added `|| !flatId` to the `disabled` condition. [`client/src/features/insights/components/InsightsTab.tsx:57`]

- [x] [Review][Patch] Loading skeleton bar count doesn't reflect the selected `days` window — `TrendChart.tsx`'s loading branch still hardcodes `Array.from({ length: 7 })` regardless of the new `days` prop this story added; selecting 30/90-day view shows a 7-bar skeleton that jumps to 30/90 bars once data loads. Fixed (2026-07-26): changed to `Array.from({ length: days })`. [`client/src/features/dashboard/components/TrendChart.tsx:83`]

- [x] [Review][Patch] Dead test scaffolding — `InsightsTab.test.tsx` mocks `useReadingHistory`/`usePatchReading`, hooks `InsightsTab.tsx` never imports, most likely leftover from a copy-pasted test file. Fixed (2026-07-26): removed the two dead `vi.mock` calls. [`client/src/features/insights/components/InsightsTab.test.tsx`]

- [x] [Review][Patch] Test name overpromises coverage — `useDashboard_DaysArgProvided_QueryFetchesDashboardWithThatWindowAndDistinctCacheKey` never inspects the query cache or asserts two cache entries coexist; it only checks `getDashboard` was called with `('flat-1', 30)`. Fixed (2026-07-26): renamed the existing test (drop the "DistinctCacheKey" claim) and added a new `useDashboard_DifferentDaysArgsSharingAQueryClient_UseDistinctCacheEntries` test that shares one `QueryClient` across two `renderHook` calls with `days=7`/`days=30` and asserts both cache entries coexist with correct, non-colliding data. [`client/src/features/dashboard/__tests__/useDashboard.test.ts`]

- [x] [Review][Patch] Test name claims broader coverage than it has — `RunAsync_NonPositiveDaysQueryParam_FallsBackToSeven` only exercises `days=0`; a genuinely negative value (e.g. `-1`) is never asserted through this test despite the "NonPositive" name. Fixed (2026-07-26): added a sibling `RunAsync_NegativeDaysQueryParam_FallsBackToSeven` test covering `?days=-1`. [`api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs`]

- [x] [Review][Defer] `TrendChart`'s date-label formatter doesn't react to i18n language changes — `labelFormatter`/`chartData` memoize on `[days]`/`[dashboard?.dailyConsumption, labelFormatter]`, never on `i18n.language`, so switching locale at runtime leaves stale-locale labels until `days` changes. Pre-existing before this diff (the original inline formatter had the same missing dependency); this diff only relocated it into its own memo. — deferred, pre-existing [`client/src/features/dashboard/components/TrendChart.tsx:16-21`]

## Dev Notes

### Critical corrections / clarifications (verified against current code — the epic text underspecifies these)

- **The epic's AC #1 phrase "the 30-day TrendChart component (from Epic 3)... already implemented" is not accurate as written.** `TrendChart.tsx` (Epic 3, Story 3.5) renders whatever `dashboard.dailyConsumption` contains, and `KpiCalculator.cs` currently hard-codes that array to a 7-day window (`windowStart = windowEnd.AddDays(-6)`). PRD **FR-16** is explicit that the Insights tab's chart must default to a 30-day period, distinct from the Dashboard's shorter sparkline — this is real, spec'd scope (AC #11–#13 above), not an epic wording slip to silently ignore, and not a green-field new component to build either — it's a parametrization of the existing one. Do not fork a second trend-chart component.
- **`InsightsResponse.runStatus` is nullable, not always an object.** The epic's AC #2 phrasing ("`useInsights(flatId)` returns `runStatus.status = Processing`...") reads as if `runStatus` always exists. `GetInsightsFunction.cs:45-47` returns `null` when no `InsightRun` has ever been created for the flat (brand-new flat, scheduled job hasn't reached it yet). Every access must be `runStatus?.status`, and AC #14's precedence chain treats `runStatus === null` as equivalent to "not currently running" for state-selection purposes (falls through to the empty-state branches).
- **"Fewer than 30 days of readings" (epic AC #9) has no existing frontend-computable signal today.** No response body — not `InsightsResponse`, not the current `DashboardSummary` — exposes total reading-history span independent of tariff configuration (`CostSummary.TotalDays` exists but is `null` whenever the flat has no tariff, which is exactly a case where you'd still want to know the reading history length). AC #12 closes this gap with a new unconditional `ReadingHistoryDays` field, reusing a local variable (`totalDays`) `KpiCalculator.cs` already computes on every call — no new query, no new computation, just a new field on the existing response.
- **`TriggerInsightsFunction.cs` returns `AcceptedResult` (202) unconditionally**, including on the "an active run already exists" branch (`existingRun is not null` → still 202 with the existing `runId`, not a 409 or 200). `apiClient.post`'s `request()` helper treats any non-`res.ok` (i.e. non-2xx) as an error via `problem.detail` — 202 is `res.ok === true`, so no special-casing is needed in `triggerInsights()`; the epic's "returns 202" language in AC #10 is literal and already satisfied by the existing backend, nothing to adapt.
- **Period selector (AC #15) — confirmed product decision, not epic-derived.** The epic's finalized Story 10.4 ACs don't mention a period selector (only a static 30-day chart), and `EXPERIENCE.md` (`ux-designs/.../EXPERIENCE.md:98,170`) only mentions one in passing as an early exploration shared conceptually with Decomposition's period selector, with the "30 days ▾" pill in `insights-tab.html`'s mockup left non-functional/decorative. This story's scope was explicitly expanded at story-creation time (product decision, confirmed with the user) to make it real and functional: 7 / 30 / 90 days, default 30. Build it as a genuinely interactive `Popover` dropdown (`InsightsPeriodSelector.tsx`, Task 4) — not a copy of the mockup's decorative pill.

### Reuse — do not reinvent

- **`TrendChart.tsx`** (`client/src/features/dashboard/components/TrendChart.tsx`) — extend with a `days` prop (AC #13), don't fork. Its `ReadingHistorySheet` bottom-sheet integration (clock icon → reading history/correction) carries over to the Insights tab for free once the same component is reused, matching `EXPERIENCE.md:184`'s expectation that the history icon is present on both the Dashboard sparkline and the Insights chart.
- **`useImportJobStatus.ts`**'s `refetchInterval` predicate shape is the exact precedent for `useInsights`'s polling logic (Task 2) — same 5-vs-3-second-interval idea, same "stop once terminal" logic.
- **`ImportProgressCard.tsx`**'s "processing" branch (amber glass card, `animate-spin` ring, `var(--color-residual-tint)` background) is the established app-wide async-processing visual — reuse its look for `InsightDiscoveryProgress.tsx` rather than inventing new styling. DESIGN.md (`:343`) confirms this amber-tinted glass card is shared between Smart Plug import progress and insight discovery progress by design.
- **`DecompositionTab.tsx`**'s structure (loading skeleton → error+retry → unavailable/empty → content) is the precedent for `InsightsTab.tsx`'s composition, and `RoomCard.tsx`/`SmartStripCard.tsx`'s `grid grid-cols-1 gap-X md:grid-cols-2` is the precedent for the insight-card grid (AC #3).
- **`decomposition/components/PeriodSelector.tsx`** is the direct structural precedent for `InsightsPeriodSelector.tsx` (Task 4) — same shadcn `Popover`/`PopoverTrigger`/`PopoverContent` dropdown, same `role="listbox"`/`role="option"`/`aria-selected` accessibility shape. Simplify it: no `customRange` state, no date `<input>` fields, just three fixed `[7, 30, 90]` options mapped through `t('period.sevenDays' | 'period.thirtyDays' | 'period.ninetyDays')`.
- **`InsightsPage.tsx` already exists** (`client/src/features/insights/InsightsPage.tsx`) as a one-line stub, already lazy-imported and routed at `/insights` in `router.tsx:7,29`. The `insights` i18n namespace is already registered in `i18n.ts`'s `ns` array, and both locale JSON files exist as empty `{}` stubs — this story fills them in, no new registration needed.
- **`i18n` namespace for `TrendChart`**: the component currently does `useTranslation('dashboard')` unconditionally (`TrendChart.tsx:16`) and its strings (`trend.cardTitle`, `trend.historyIconLabel`, `trend.meterResetSummary`) live in `dashboard.json`. Reusing the component as-is on the Insights tab means those dashboard-namespaced strings render even on the Insights page — this is almost certainly fine (they're generic "trend chart" labels, not Dashboard-specific wording), but if visually distinct copy is wanted for the Insights context, `TrendChart` would need an additional namespace/prop, which is a larger change than this story's scope implies. Default to leaving `TrendChart`'s namespace untouched; only revisit if `dashboard:trend.cardTitle`'s literal wording ("Trend") reads oddly in context.
- **Icon choice** (AC #4–#7 "standby icon"/"replacement icon"/"budget warning icon"/"invoice icon") is not pinned by the epic to specific `lucide-react` names. Reasonable choices already consistent with icons used elsewhere in the app (`lucide-react` is the only icon library, per `client/src/features/dashboard/components/TrendChart.tsx`'s `History` import and `DecompositionPage.tsx`'s `Upload`): `Zap` (standby), `Recycle` (replacement), `AlertTriangle` (budget), `Receipt` or `FileWarning` (invoice deviation). Not a hard requirement — pick any semantically-fitting `lucide-react` icon.
- **Accent colors** for card left-borders/icon tint: only Budget's `accent-error` is epic-mandated (AC #6). For the other three, reuse existing tokens rather than inventing new ones — see Task 4's mapping (`accent-spike` amber / `accent-under-budget` green / `accent-info` blue), chosen to match the amber/green/blue families already visible in the `insights-tab.html` mockup without introducing new CSS variables.

### Data shapes (from Stories 10.1–10.3's backend, verified against current code)

- `StandbyInsightData`: `{ deviceName: string; meanStandbyWatts: number; estimatedMonthlyKwh: number; estimatedMonthlyCost: number }` (`StandbyDetector.cs`, Story 10.2)
- `ReplacementInsightData`: `{ deviceName: string; estimatedAnnualKwh: number; estimatedAnnualCost: number; suggestedClass: string; estimatedSavingsEur: number }` (`ReplacementDetector.cs`, Story 10.2)
- `BudgetInsightData`: `{ projectedAnnualCost: number; plannedAnnualSpend: number; overspendEur: number }` (`BudgetAlertDetector.cs`, Story 10.3)
- `InvoiceDeviationInsightData`: `{ projectedAnnualKwh: number; baselineKwh: number; deviationPct: number; impliedDeltaEur: number; direction: 'above' | 'below' }` (`InvoiceDeviationDetector.cs`, Story 10.3)
- All decimal fields serialize as plain JSON numbers (not strings) — project-wide convention, already correct server-side; no client-side parsing beyond standard `JSON.parse` (handled by `apiClient`).
- `InsightDto.type` serializes as its C# enum **name** (`"Standby"`, `"Replacement"`, `"Budget"`, `"InvoiceDeviation"`) via the global `JsonStringEnumConverter` — matches `RunStatusDto.status`'s convention (`"Pending"`/`"Processing"`/`"Complete"`/`"Failed"`).

### Project Structure Notes

New files (all under the already-scaffolded `client/src/features/insights/` slice — matches `architecture.md:627-636`'s planned tree exactly):
- `client/src/features/insights/api/insightsApi.ts`
- `client/src/features/insights/hooks/useInsights.ts`
- `client/src/features/insights/hooks/useTriggerInsights.ts`
- `client/src/features/insights/components/InsightsTab.tsx`
- `client/src/features/insights/components/InsightCard.tsx`
- `client/src/features/insights/components/InsightDiscoveryProgress.tsx`
- `client/src/features/insights/components/InsightsPeriodSelector.tsx`

Modified files:
- `client/src/features/insights/InsightsPage.tsx` (stub → renders `InsightsTab`)
- `client/src/features/dashboard/components/TrendChart.tsx` (add optional `days` prop)
- `client/src/features/dashboard/hooks/useDashboard.ts` (add `days` param, extend query key)
- `client/src/features/dashboard/api/dashboardApi.ts` (add `readingHistoryDays` field, `days` query param)
- `client/src/locales/en-US/insights.json`, `client/src/locales/de-DE/insights.json` (empty stubs → real content)
- `api/Features/Dashboard/KpiCalculator.cs`, `DashboardModels.cs`, `GetDashboardFunction.cs`

No new backend entities, migrations, or DI registrations. No changes to `TriggerInsightsFunction.cs`/`GetInsightsFunction.cs`/`ProcessInsightsFunction.cs` (Story 10.1's contracts already match this story's needs exactly).

Follows `features/{feature}/{components,hooks,api}/` VSA slice convention (`insights/` already has all three subfolders scaffolded per `project-context.md`'s "feature folder structure is mandatory" rule).

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-10-actionable-insights.md#Story 10.4] — epic ACs (verbatim basis for ACs #1–#10 above)
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md#FR-16, #FR-17] — 30-day Insights trend chart requirement (basis for AC #11–#13); spike detection (already satisfied, no change needed)
- [Source: _bmad-output/planning-artifacts/architecture.md:627-636] — planned `insights/` feature-folder file tree (component/hook/api names followed exactly)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/EXPERIENCE.md:34-35,98-99,136-137,157,170,184,269-282,293] — Insights tab flow, card visual language, empty-state copy source, tablet 2-column grid, period-dropdown mention (made a real functional requirement by AC #15, see Dev Notes)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/mockups/insights-tab.html] — visual reference only (glass card, icon-circle, border-left accent, tag pill styling) — not a literal functional spec; the epic ACs govern behavior
- [Source: api/Features/Dashboard/KpiCalculator.cs, DashboardModels.cs, GetDashboardFunction.cs] — existing 7-day trend window implementation being parametrized (AC #11-#12)
- [Source: api/Features/Insights/InsightModels.cs, GetInsightsFunction.cs, TriggerInsightsFunction.cs] — `InsightsResponse`/`InsightDto`/`RunStatusDto` contracts (Story 10.1, unchanged by this story)
- [Source: api/Features/Insights/StandbyDetector.cs, ReplacementDetector.cs (Story 10.2), BudgetAlertDetector.cs, InvoiceDeviationDetector.cs (Story 10.3)] — per-type `Insight.Data` JSON shapes consumed by `InsightCard.tsx`
- [Source: client/src/features/smart-plug-import/hooks/useImportJobStatus.ts, components/ImportProgressCard.tsx] — polling and processing-card precedent for `useInsights`/`InsightDiscoveryProgress`
- [Source: client/src/features/decomposition/components/DecompositionTab.tsx, RoomCard.tsx, SmartStripCard.tsx, PeriodSelector.tsx] — tab composition, responsive-grid, and `Popover`-dropdown precedents (`PeriodSelector.tsx` is the direct basis for `InsightsPeriodSelector.tsx`, AC #15)
- [Source: client/src/features/dashboard/components/TrendChart.tsx, DashboardPage.tsx, hooks/useDashboard.ts, api/dashboardApi.ts] — component/hook/api being extended
- [Source: client/src/features/readings/hooks/useSubmitReading.ts, usePatchReading.ts] — existing `['dashboard', flatId]` invalidation call sites, confirmed prefix-match-safe against the new `{ days }` key segment
- [Source: client/src/router.tsx:7,29, client/src/lib/i18n.ts:30, client/src/features/insights/InsightsPage.tsx, client/src/locales/{en-US,de-DE}/insights.json] — pre-existing scaffolding this story builds on
- [Source: _bmad-output/implementation-artifacts/10-3-budget-pressure-and-invoice-deviation-detectors.md#Dev Notes] — established convention for documenting epic-text gaps as explicit story-creation-time decisions, followed by this story for ACs #11-#15
- [Memory: Epic 9 retro / Epic 10 prep — TariffResolver already removed, don't recreate it — not directly relevant to this frontend-heavy story but confirms no backend tariff-resolution helper should be added here]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

None — no scratch debug logging was needed; one iteration mismatch was resolved directly (see Completion Notes: recharts tick-label DOM selector).

### Completion Notes List

- **Task 1 (backend):** `KpiCalculator.Compute` gained a trailing `int days = 7` param; `windowStart` now generalizes to `windowEnd.AddDays(-(days - 1))` and is shared by both `dailyConsumption` and `DetectSpikes` (spike detection now scans the full requested window, not a hardcoded 7 days — verified with a dedicated test placing a spike outside the default 7-day range but inside a 30-day window). Added `ReadingHistoryDays` (`(int)Math.Floor(totalDays)`, `0` on all three early-return branches) to `DashboardSummary`. `GetDashboardFunction.cs` parses `?days=`, defaults to 7 on absence/parse failure/non-positive, clamps to 365.
- **Task 2 (frontend insights API/hooks):** `insightsApi.ts` discriminated-union `InsightDto` typed per detector's `Data` shape (Story 10.1–10.3 contracts, unchanged). `useInsights` mirrors `useImportJobStatus`'s `refetchInterval` predicate (5000ms while `Pending`/`Processing`, else `false`). `useTriggerInsights` invalidates `['insights', flatId]` in `onSuccess`.
- **Task 3 (dashboard window plumbing):** `dashboardApi.getDashboard(flatId, days = 7)` appends `?days=`; `useDashboard` query key is now `['dashboard', flatId, { days }]` (prefix-match-safe against the existing 2-element reading-correction invalidation call sites — verified, no changes needed there). `TrendChart` accepts an optional `days` prop; x-axis switches from narrow-weekday to day/month labels with sparse `interval` ticks (~5–6 labels) when `days > 7`. Also fixed a real (not just theoretical) i18n bug the Dev Notes flagged as worth revisiting: `dashboard:trend.cardTitle` was hardcoded `"Last 7 Days"`, which would have been literally wrong when the same component renders a 30- or 90-day window on the Insights tab — changed to `"Last {{days}} Days"` with the `days` value interpolated (byte-for-byte identical output at the default `days=7`, so the Dashboard tab has no regression).
- **Task 4 (period selector):** `InsightsPeriodSelector.tsx` mirrors `decomposition/PeriodSelector.tsx`'s `Popover` structure, simplified to three fixed `[7, 30, 90]` options.
- **Task 5 (`InsightCard`):** one component, `switch` on `insight.type`, left-border accent per type (`accent-spike`/`accent-under-budget`/`accent-error`/`accent-info`). `formatKwh` here is deliberately just the bare `Intl.NumberFormat` (no `" kWh"` suffix, unlike `RoomCard`'s local `formatKwh` helper) because the AC #7 copy templates already supply the literal `" kWh"` unit text — appending it in both places would have doubled it.
- **Task 6 (`InsightDiscoveryProgress`):** mirrors `ImportProgressCard.tsx`'s processing-branch visual (amber glass card, `animate-spin` ring) exactly, per Dev Notes.
- **Task 7 (`InsightsTab` composition):** implements the AC #14 precedence chain with `runStatus` from `useInsights` and `readingHistoryDays` from a *dedicated* `useDashboard(flatId, 30)` call (separate from the period-selector-driven `useDashboard(flatId, days)` call that feeds `TrendChart`) exactly as AC #14 specifies — this keeps the empty-state gate's cache entry stable and unaffected by the user switching the trend-chart period. Progress indicator and insight cards are not mutually exclusive: per AC #2, `InsightDiscoveryProgress` renders whenever a run is `Pending`/`Processing` *in addition to* whichever cards/empty-state body the precedence chain selects, not instead of it. `TrendChart` gained an optional `headerExtra` slot (rendered next to the existing history icon) so `InsightsPeriodSelector` can live in the trend card header without forking the component; the Dashboard tab's call site passes no `headerExtra` and is visually unchanged.
- **Task 8 (i18n):** populated both locale files with every key referenced by the ACs.
- **Task 9 (tests):** Backend — 12 new `KpiCalculatorTests.cs` cases (default/30/90-day window sizing, 30-day spike-window widening, `ReadingHistoryDays` across 0/1/sub-day/long-history) and 8 new `GetDashboardFunctionTests.cs` cases (`?days=` absent/30/90/non-positive/unparsable/clamped, `ReadingHistoryDays` presence). Frontend — new test files for every new component/hook (`useInsights`, `useTriggerInsights`, `InsightCard` one case per type, `InsightDiscoveryProgress`, `InsightsPeriodSelector`, `InsightsTab` covering all AC #14 branches plus the refresh-button disabled state and period-switch stability), plus fixture/test updates to `TrendChart.test.tsx` (30/90-day label cases, using `.recharts-cartesian-axis-tick-value` — discovered via DOM inspection that recharts renders tick-line groups and tick-label `<text>` groups in separate SVG z-index layers, not nested under `.recharts-xAxis`, so an initially-scoped selector silently matched zero elements), `useDashboard.test.ts`, and `DashboardGrid.test.tsx` (added `readingHistoryDays` to all `DashboardSummary` fixtures).
- **Full regression:** backend `dotnet test api.Tests` → 445/445 passed. Frontend `vitest run` → 439/439 passed across 68 files. `tsc --noEmit` and `oxlint` both clean (only pre-existing unrelated `router.tsx` fast-refresh warnings). `npm run build` (client) succeeds.
- **Not verified in a live browser:** the SWA CLI (`swa`) required for local Easy-Auth simulation isn't installed in this environment, so the feature could not be click-tested end-to-end against a running dev server. Verification relied on the automated test suites above plus a successful production build.

### File List

**New:**
- `client/src/features/insights/api/insightsApi.ts`
- `client/src/features/insights/hooks/useInsights.ts`
- `client/src/features/insights/hooks/useInsights.test.ts`
- `client/src/features/insights/hooks/useTriggerInsights.ts`
- `client/src/features/insights/hooks/useTriggerInsights.test.ts`
- `client/src/features/insights/components/InsightsTab.tsx`
- `client/src/features/insights/components/InsightsTab.test.tsx`
- `client/src/features/insights/components/InsightCard.tsx`
- `client/src/features/insights/components/InsightCard.test.tsx`
- `client/src/features/insights/components/InsightDiscoveryProgress.tsx`
- `client/src/features/insights/components/InsightDiscoveryProgress.test.tsx`
- `client/src/features/insights/components/InsightsPeriodSelector.tsx`
- `client/src/features/insights/components/InsightsPeriodSelector.test.tsx`

**Modified:**
- `api/Features/Dashboard/KpiCalculator.cs`
- `api/Features/Dashboard/DashboardModels.cs`
- `api/Features/Dashboard/GetDashboardFunction.cs`
- `api.Tests/Features/Dashboard/KpiCalculatorTests.cs`
- `api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs`
- `client/src/features/insights/InsightsPage.tsx`
- `client/src/features/dashboard/components/TrendChart.tsx`
- `client/src/features/dashboard/components/TrendChart.test.tsx`
- `client/src/features/dashboard/hooks/useDashboard.ts`
- `client/src/features/dashboard/__tests__/useDashboard.test.ts`
- `client/src/features/dashboard/api/dashboardApi.ts`
- `client/src/features/dashboard/__tests__/DashboardGrid.test.tsx`
- `client/src/locales/en-US/insights.json`
- `client/src/locales/de-DE/insights.json`
- `client/src/locales/en-US/dashboard.json`
- `client/src/locales/de-DE/dashboard.json`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-07-26: Implemented Story 10.4 — backend trend-window parametrization + `ReadingHistoryDays`, full `insights` feature slice (API, hooks, `InsightsTab`/`InsightCard`/`InsightDiscoveryProgress`/`InsightsPeriodSelector`), `TrendChart` `days`/`headerExtra` extension, i18n content. Status moved to review.
