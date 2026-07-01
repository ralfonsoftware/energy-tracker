---
baseline_commit: 8432120
---

# Story 3.3: KPI Dashboard Frontend — Euro Burn Design & Grid

Status: done

## Story

As a user,
I want to see my KPI figures on the Euro Burn Dashboard with the gradient background encoding my consumption against my daily budget,
so that my energy cost is visible at a glance and the ambient background tells me how I am doing before I read a single number.

## Acceptance Criteria

1. **Dashboard fetches and renders** — Given an authenticated user on the Dashboard, when the page loads, then `useDashboard` (TanStack Query key `['dashboard', flatId]`) fetches `GET /api/v1/flats/{flatId}/dashboard`; on success, the page renders the KPI grid and gradient background.

2. **KPI grid layout** — Given the four KPI tiles, when rendered on phone, then they appear in a 2×2 grid; on tablet (≥ 768 px), they appear 4-across. Each tile is a glass card: `backdrop-filter: blur(20px) saturate(180%)`, `background: rgba(255,255,255,0.08)`, `border: 1px solid rgba(255,255,255,0.14)`, `border-radius: 18px`, `padding: 16px 18px`.

3. **KPI tile content** — Given the four tiles, when rendered with readings present, then:
   - Tile 1 (Daily): headline `{DailyAvgKwh} kWh` at `display-kpi` (22px/700/−0.02em); subline `€{cost.DailyAvgCost}` at `body-sm` (cost subline respects gap state per AC-7/8/9); budget delta `↓ X kWh under budget` in `accent-under-budget` / `↑ X kWh over budget` in `accent-over-budget` / `— at daily budget` at `label-caps`; tertiary caption "based on {annualKwhBaseline} kWh/yr".
   - Tile 2 (Weekly): headline `{WeeklyAvgKwh} kWh`, subline `€{cost.WeeklyAvgCost}`.
   - Tile 3 (Projected monthly): headline `€{cost.ProjectedMonthlyCost}`, no kWh subline.
   - Tile 4 (Today): headline `{TodayKwh} kWh`, subline `vs {DailyBudgetKwh} kWh/day budget`.

4. **Euro Burn Gradient Background** — Given `TodayKwh` and `DailyBudgetKwh` from the dashboard response, when the page renders, then the gradient stop positions shift based on the consumption-to-budget ratio: ≤ −50% clips to the cool edge; ≥ +50% clips to the warm edge; angle is 160 deg on phone, 140 deg on tablet; no midpoint marker is added.

5. **Cold open (no readings)** — Given a flat with no meter readings, when the Dashboard renders, then all KPI tiles show `—` (dash); gradient renders at neutral midpoint; "Last read: never" appears; the Enter Reading CTA is prominently visible.

6. **Loading skeleton** — Given `isLoading === true` on first fetch, when the dashboard renders, then skeleton placeholders fill the KPI tile grid positions — no content flash, no zero-flash.

7. **Cost tile — no gap, sufficient coverage** — Given `response.cost !== null`, `cost.hasCostGap === false`, and `cost.costDetailAvailable === true`, when a cost subline or cost tile renders, then the numeric cost value is displayed normally with no annotation or badge.

8. **Cost tile — gap with sufficient coverage** — Given `response.cost !== null`, `cost.hasCostGap === true`, and `cost.costDetailAvailable === true`, when a cost subline or tile renders, then the numeric cost value is displayed alongside an amber coverage badge reading "Tariff covers {coveredDays} of {totalDays} days"; tapping/clicking the badge opens a `Popover` with the text "Some of your readings predate your tariff. The cost average only covers tariffed days." and a link "Update tariff →" navigating to `/settings`.

9. **Cost tile — insufficient coverage** — Given `response.cost !== null` and `cost.costDetailAvailable === false` (fewer than 7 covered days, regardless of gap status), when a cost subline or tile renders, then the numeric value is suppressed and replaced with `—`; the amber badge "Tariff covers {coveredDays} of {totalDays} days" is shown; the same popover is accessible.

10. **Cost null — no tariff configured** — Given `response.cost === null`, when any cost field renders, then display `—` with no badge and no popover; no error state is shown.

11. **Gap signal scoped to cost** — Given any non-cost KPI (kWh consumption, DailyBudgetKwh), when the dashboard renders, then no gap badge, amber indicator, or popover appears regardless of `cost.hasCostGap`.

12. **Last read date** — Given `LastReadingDate` in the response, when rendered, then "Last read: {formatted date/time}" appears in locale-appropriate format below the KPI grid; null renders as "Last read: never".

## Tasks / Subtasks

- [x] Task 1: Create `dashboardApi.ts` — TypeScript types + API function (AC: 1)
  - [x] `client/src/features/dashboard/api/dashboardApi.ts` — `CostSummary`, `DashboardSummary` types; `getDashboard(flatId: string)` calling `apiClient.get<DashboardSummary>` (see Dev Notes)

- [x] Task 2: Create `useDashboard.ts` — TanStack Query hook (AC: 1)
  - [x] `client/src/features/dashboard/hooks/useDashboard.ts` — `useQuery` with key `['dashboard', flatId]`; enabled only when `flatId` is defined (see Dev Notes)

- [x] Task 3: Create `EuroBurnGradient.tsx` — background gradient component (AC: 4, 5)
  - [x] `client/src/features/dashboard/components/EuroBurnGradient.tsx` — accepts `todayKwh` and `dailyBudgetKwh`; computes ratio; outputs CSS gradient as inline style on a full-bleed wrapper (see Dev Notes)

- [x] Task 4: Create `KpiTile.tsx` — base glass tile component (AC: 2, 3, 5, 6)
  - [x] `client/src/features/dashboard/components/KpiTile.tsx` — accepts `headline`, `subline`, `delta?`, `caption?` props; renders glass card; handles `undefined` headline as skeleton placeholder (see Dev Notes)

- [x] Task 5: Create `CostGapBadge.tsx` — amber gap indicator + popover (AC: 8, 9, 10)
  - [x] `client/src/features/dashboard/components/CostGapBadge.tsx` — renders amber badge with coverage text; wraps shadcn `Popover` with explanation text and "Update tariff →" link to `/settings` (see Dev Notes)

- [x] Task 6: Create `DashboardGrid.tsx` — assembles all four tiles (AC: 2, 3, 5, 7, 8, 9, 10, 11, 12)
  - [x] `client/src/features/dashboard/components/DashboardGrid.tsx` — receives `DashboardSummary | undefined`; composes four `KpiTile` instances; applies `CostGapBadge` where applicable on cost sublines; renders "Last read" line; handles null/undefined states (see Dev Notes)

- [x] Task 7: Wire `DashboardPage.tsx` (AC: 1, 4, 5, 6)
  - [x] `client/src/features/dashboard/DashboardPage.tsx` — call `useDashboard`; render `EuroBurnGradient` + `DashboardGrid`; pass `isLoading` through to skeleton state (see Dev Notes)

- [x] Task 8: Add translation keys (AC: 3, 5, 8, 9, 10, 12)
  - [x] `client/src/locales/en-US/dashboard.json` — create file with all keys (see Dev Notes for full key list)
  - [x] `client/src/locales/de-DE/dashboard.json` — German equivalents

- [x] Task 9: Tests (AC: 1, 5, 6, 7, 8, 9, 10, 11)
  - [x] `client/src/features/dashboard/components/DashboardGrid.test.tsx` — minimum 8 tests (see Dev Notes)
  - [x] `client/src/features/dashboard/hooks/useDashboard.test.ts` — minimum 2 tests

- [x] Task 10: Final verification
  - [x] `cd client && npm run build` exits 0 with zero TypeScript errors
  - [x] `cd client && npm test` — all tests pass including all pre-existing tests
  - [x] `cd client && npm run lint` exits 0
  - [x] Update File List in this story

## Dev Notes

### Prerequisite: Story 3.2 Amendment

**This story depends on the Story 3.2 Amendment being implemented first.** The backend must return the `CostSummary?` nested shape before this story begins. Do NOT start until `dotnet test` passes with the new record shape.

### What Already Exists — Read Before Writing Any Code

- `client/src/features/dashboard/DashboardPage.tsx` — EXISTS as a stub (`return <div>Dashboard</div>`). Task 7 replaces its body — do NOT delete the file.
- `client/src/lib/apiClient.ts` — EXISTS. Use `apiClient.get<T>(path)` for GET requests.
- `client/src/features/settings/api/settingsApi.ts` — has `UserSettings` with `flatId`. The `flatId` for the dashboard query comes from `useUserSettings().data?.flatId`.
- `client/src/features/settings/hooks/useUserSettings.ts` — EXISTS. `queryKey: ['settings']`. Do NOT modify.
- `client/src/index.css` — has all design tokens in `@theme {}`: `--color-accent-under-budget`, `--color-accent-over-budget`, `--color-accent-spike`, `--font-display-kpi` etc.
- `client/src/components/ui/` — shadcn/ui components including `Popover`, `PopoverContent`, `PopoverTrigger`. Use these directly — do NOT hand-edit them.

### Task 1: `dashboardApi.ts` — TypeScript Types + API Function

```typescript
// client/src/features/dashboard/api/dashboardApi.ts
import { apiClient } from '@/lib/apiClient'

export type CostSummary = {
  dailyAvgCost: number
  weeklyAvgCost: number
  projectedMonthlyCost: number
  hasCostGap: boolean
  coveredDays: number
  totalDays: number
  costDetailAvailable: boolean
}

export type DashboardSummary = {
  dailyAvgKwh: number
  weeklyAvgKwh: number
  todayKwh: number
  dailyBudgetKwh: number
  lastReadingDate: string | null
  spikeDays: string[]
  cost: CostSummary | null
}

export const getDashboard = (flatId: string) =>
  apiClient.get<DashboardSummary>(`/flats/${flatId}/dashboard`)
```

Note: `cost` is `CostSummary | null` — never `undefined`. The backend sends `null` when no tariff is configured. TypeScript's `null` and `undefined` are distinct; use explicit null checks (`cost !== null`), not truthiness (`if (cost)`), to handle `CostSummary` with zero values correctly.

### Task 2: `useDashboard.ts` — TanStack Query Hook

```typescript
// client/src/features/dashboard/hooks/useDashboard.ts
import { useQuery } from '@tanstack/react-query'
import { getDashboard } from '@/features/dashboard/api/dashboardApi'

export const useDashboard = (flatId: string | undefined) =>
  useQuery({
    queryKey: ['dashboard', flatId],
    queryFn: () => getDashboard(flatId!),
    enabled: flatId !== undefined,
  })
```

- `flatId` comes from `useUserSettings().data?.flatId` in the page — pass it directly; the hook handles the `undefined` case via `enabled`
- TanStack Query v5: do NOT use `isLoading` for "first fetch" state — use `isPending` instead (`isLoading` is undefined in v5)
- `queryKey: ['dashboard', flatId]` — this key is invalidated by Story 3.4's submit-reading mutation

### Task 3: `EuroBurnGradient.tsx`

```typescript
// client/src/features/dashboard/components/EuroBurnGradient.tsx
type Props = { todayKwh: number; dailyBudgetKwh: number }

export function EuroBurnGradient({ todayKwh, dailyBudgetKwh }: Props) {
  // ratio: 0 = at budget, positive = over budget, negative = under budget
  const ratio = dailyBudgetKwh > 0 ? (todayKwh - dailyBudgetKwh) / dailyBudgetKwh : 0
  const clamped = Math.max(-0.5, Math.min(0.5, ratio))
  // map [-0.5, 0.5] → [0%, 100%] gradient stop position
  const stop = Math.round((clamped + 0.5) * 100)
  // ...gradient CSS using stop value
}
```

- Cold open (no readings): pass `todayKwh = 0` and `dailyBudgetKwh = 0` → ratio = 0 → neutral midpoint gradient
- Angle: `160deg` on phone, `140deg` on tablet — use a CSS custom property or responsive class

### Task 4: `KpiTile.tsx` — Base Glass Tile

```typescript
// client/src/features/dashboard/components/KpiTile.tsx
type Props = {
  headline: string | undefined   // undefined → skeleton
  subline?: React.ReactNode      // accepts string or JSX (for gap badge inline)
  delta?: string
  deltaVariant?: 'under' | 'over' | 'neutral'
  caption?: string
}
```

- Glass card styles: `backdrop-filter: blur(20px) saturate(180%)`, `bg-white/8`, `border border-white/14`, `rounded-[18px]`, `p-4`
- When `headline` is `undefined`: render a skeleton placeholder div (pulse animation) — do NOT render `0` or `—` during loading
- `subline` is `React.ReactNode` so `DashboardGrid` can pass a cost string + `<CostGapBadge>` inline as a fragment

### Task 5: `CostGapBadge.tsx` — Amber Gap Indicator

```typescript
// client/src/features/dashboard/components/CostGapBadge.tsx
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { useNavigate } from 'react-router-dom'

type Props = { coveredDays: number; totalDays: number }

export function CostGapBadge({ coveredDays, totalDays }: Props) {
  const navigate = useNavigate()
  return (
    <Popover>
      <PopoverTrigger asChild>
        <button className="text-amber-400 text-xs inline-flex items-center gap-1">
          ⚠ Tariff covers {coveredDays} of {totalDays} days
        </button>
      </PopoverTrigger>
      <PopoverContent className="w-72 text-sm">
        <p>Some of your readings predate your tariff. The cost average only covers tariffed days.</p>
        <button onClick={() => navigate('/settings')} className="mt-2 underline text-primary">
          Update tariff →
        </button>
      </PopoverContent>
    </Popover>
  )
}
```

- Use i18n keys (not hardcoded strings) — see Task 8 for key names
- The "Update tariff →" link uses `useNavigate` to `/settings` — Epic 4 will add a dedicated tariff management route; until then, settings root is the correct destination

### Task 6: `DashboardGrid.tsx` — Cost Subline Resolution Logic

The cost subline for Tiles 1, 2, and 3 uses the following decision tree — implement this as a helper function `resolveCostSubline`:

```
if cost === null:
  return '—'   (no badge, no popover)
if !cost.costDetailAvailable:
  return <><span>—</span><CostGapBadge coveredDays={...} totalDays={...} /></>
if cost.hasCostGap:
  return <><span>€{formatted}</span><CostGapBadge coveredDays={...} totalDays={...} /></>
else:
  return '€{formatted}'  (plain string, no badge)
```

Tile 3 (Projected monthly) headline is the cost value itself, not a subline — apply the same `costDetailAvailable` check: if false, show `—` as the headline.

**"Last read" line** below the grid:
- `lastReadingDate !== null`: format with `Intl.DateTimeFormat` using the user's locale from `useUserSettings`
- `lastReadingDate === null`: "Last read: never"

### Task 7: `DashboardPage.tsx`

```typescript
// client/src/features/dashboard/DashboardPage.tsx
import { useUserSettings } from '@/features/settings/hooks/useUserSettings'
import { useDashboard } from '@/features/dashboard/hooks/useDashboard'
import { EuroBurnGradient } from '@/features/dashboard/components/EuroBurnGradient'
import { DashboardGrid } from '@/features/dashboard/components/DashboardGrid'

export default function DashboardPage() {
  const { data: settings } = useUserSettings()
  const { data: dashboard, isPending } = useDashboard(settings?.flatId)
  
  return (
    <div className="relative min-h-screen">
      <EuroBurnGradient
        todayKwh={dashboard?.todayKwh ?? 0}
        dailyBudgetKwh={dashboard?.dailyBudgetKwh ?? 0}
      />
      <DashboardGrid dashboard={isPending ? undefined : dashboard} />
    </div>
  )
}
```

- Pass `undefined` to `DashboardGrid` while loading → triggers skeleton state
- `EuroBurnGradient` always renders (neutral midpoint during loading)

### Task 8: Translation Keys

**`client/src/locales/en-US/dashboard.json`** (create new file):

```json
{
  "tile": {
    "dailyAvg": "Daily Average",
    "weeklyAvg": "Weekly Average",
    "projectedMonthly": "Projected Monthly",
    "today": "Today",
    "budgetCaption": "based on {{baseline}} kWh/yr",
    "underBudget": "↓ {{amount}} kWh under budget",
    "overBudget": "↑ {{amount}} kWh over budget",
    "atBudget": "— at daily budget"
  },
  "costGap": {
    "badgeLabel": "Tariff covers {{covered}} of {{total}} days",
    "popoverBody": "Some of your readings predate your tariff. The cost average only covers tariffed days.",
    "popoverCta": "Update tariff →"
  },
  "lastRead": {
    "label": "Last read:",
    "never": "never"
  }
}
```

**`client/src/locales/de-DE/dashboard.json`** (create new file — German equivalents):

```json
{
  "tile": {
    "dailyAvg": "Tagesdurchschnitt",
    "weeklyAvg": "Wochendurchschnitt",
    "projectedMonthly": "Hochgerechneter Monat",
    "today": "Heute",
    "budgetCaption": "basierend auf {{baseline}} kWh/Jahr",
    "underBudget": "↓ {{amount}} kWh unter Budget",
    "overBudget": "↑ {{amount}} kWh über Budget",
    "atBudget": "— im Budget"
  },
  "costGap": {
    "badgeLabel": "Tarif deckt {{covered}} von {{total}} Tagen",
    "popoverBody": "Einige deiner Ablesungen liegen vor deinem Tarif. Der Kostendurchschnitt umfasst nur tarifierte Tage.",
    "popoverCta": "Tarif aktualisieren →"
  },
  "lastRead": {
    "label": "Letzte Ablesung:",
    "never": "noch nie"
  }
}
```

Add `dashboard` namespace to the i18n instance the same way existing namespaces are loaded.

### Task 9: Tests for `DashboardGrid.test.tsx`

Follow the pattern from `OnboardingContract.test.tsx`: mock `useUserSettings` and `useDashboard`; render `DashboardGrid` directly with prop fixtures.

**Minimum 8 test methods:**

1. `DashboardGrid_NoData_AllTilesShowDash` — `dashboard={undefined}` → all headlines show skeleton or `—`
2. `DashboardGrid_NoReadings_AllTilesShowDash` — all KPIs zero, `lastReadingDate: null` → "Last read: never"
3. `DashboardGrid_WithReadings_ShowsKpiValues` — full `DashboardSummary` with `cost` non-null, no gap → `DailyAvgKwh` visible; no badge
4. `DashboardGrid_CostNull_CostSublineShowsDash` — `cost: null` → cost sublines show `—`; no badge present
5. `DashboardGrid_GapWithSufficientCoverage_ShowsValueAndBadge` — `hasCostGap: true`, `costDetailAvailable: true` → cost value shown; badge "Tariff covers X of Y days" present
6. `DashboardGrid_GapInsufficientCoverage_SuppressesValue` — `costDetailAvailable: false` → cost subline shows `—`; badge present
7. `DashboardGrid_NoCostGap_NoBadgeRendered` — `hasCostGap: false`, `costDetailAvailable: true` → no badge anywhere
8. `DashboardGrid_NonCostKpis_NeverShowBadge` — `hasCostGap: true` → kWh tiles have no badge

**`useDashboard.test.ts` (minimum 2):**

1. `useDashboard_WhenFlatIdUndefined_QueryIsDisabled` — `flatId = undefined` → query never fires
2. `useDashboard_WhenFlatIdDefined_QueryFetchesDashboard` — mock `apiClient.get` → hook returns data

### Architecture Compliance Checklist

- [ ] `import type` for all type-only imports (TS6 strict module mode)
- [ ] No barrel files — import directly from the declaring file
- [x] `@/` alias for all imports from `src/` — never relative paths (fixed in review pass: `useDashboard.ts`, `DashboardGrid.tsx`, both test files)
- [ ] TanStack Query v5: `isPending` not `isLoading` for loading state
- [ ] `useQuery({ queryKey, queryFn, enabled })` object form — no positional overload
- [ ] `cost !== null` explicit null check — not truthiness
- [ ] Never use `?.` in Shouldly-style assertions — use `ShouldNotBeNull()` then `!.`
- [x] shadcn `Popover` from `@/components/ui/popover` — never hand-edit `ui/` components (fixed in review pass: `ui/popover.tsx` reverted to shadcn defaults; project styling moved to `CostGapBadge.tsx`'s `className` prop)
- [ ] No `window.confirm` / `window.alert` anywhere
- [ ] All currency values formatted with locale-aware `Intl.NumberFormat` (or i18n interpolation)
- [ ] `useNavigate` from `react-router-dom` for the "Update tariff →" CTA — never `window.location`

### Project Structure — New Files

```
client/src/features/dashboard/
├── api/
│   └── dashboardApi.ts              ← NEW
├── components/
│   ├── EuroBurnGradient.tsx         ← NEW
│   ├── KpiTile.tsx                  ← NEW
│   ├── CostGapBadge.tsx             ← NEW
│   └── DashboardGrid.tsx            ← NEW
├── hooks/
│   └── useDashboard.ts              ← NEW
├── DashboardPage.tsx                ← MODIFY (replace stub)
└── __tests__/
    ├── DashboardGrid.test.tsx       ← NEW
    └── useDashboard.test.ts         ← NEW

client/src/locales/en-US/
└── dashboard.json                   ← NEW

client/src/locales/de-DE/
└── dashboard.json                   ← NEW
```

### References

- Design tokens and glass card pattern: `client/src/index.css` (`@theme {}` block)
- API client pattern: `client/src/features/settings/api/settingsApi.ts`
- Hook pattern: `client/src/features/settings/hooks/useUserSettings.ts`
- Test pattern: `client/src/features/onboarding/components/OnboardingContract.test.tsx`
- Epic 3 KPI tile exact measurements: `_bmad-output/planning-artifacts/epics/epic-3-meter-reading-kpi-dashboard-reading-history.md`
- Backend response contract: `api/Features/Dashboard/DashboardModels.cs` (post-amendment)

## Review Findings

### Decision Needed

All decision-needed items were resolved during review triage (see resolutions folded into Patch/Dismissed below).

### Patch

- [x] [Review][Patch] No error-state handling — a failed query is indistinguishable from "still loading" [client/src/features/dashboard/DashboardPage.tsx] — `DashboardPage.tsx` never checks `isError` from either `useUserSettings` or `useDashboard`; on fetch failure the skeleton spins forever with no error message. Resolution: add an inline error banner (check `isError` on both hooks; show a simple inline error message in place of the skeleton, per the project's existing "inline error state, no global toast" convention for query errors).
- [x] [Review][Dismiss] User without a flat lands on `/` with a permanent skeleton and no redirect — **false positive**: `router.tsx` wraps the `/` route (and all `AppShell` routes) in `OnboardingGate`, which already redirects `hasFlat === false` users to `/onboarding` at the router level. Missed in initial review verification (only `router.tsx`/`App.tsx` were grepped directly, not the `OnboardingGate` component they reference).
- [x] [Review][Patch] AC-5 violated — cold-open gradient does not render at the neutral midpoint [client/src/features/dashboard/DashboardPage.tsx:12-15] — the real backend cold-open response returns `TodayKwh: 0` but a non-zero `DailyBudgetKwh` (`annualBaseline/365`), so the ratio computes to -1 (clamped -0.5), rendering the extreme cool edge instead of neutral. Fix: force the ratio to 0 when `dashboard?.lastReadingDate === null` (the same signal `DashboardGrid` already uses for `isColdOpen`).
- [x] [Review][Patch] Hand-edited generated shadcn primitive violates "never hand-edit `ui/`" rule [client/src/components/ui/popover.tsx] — project-specific glass-surface classes are baked directly into the generated file. Fix: revert to shadcn defaults and move styling into a wrapper component in the feature folder.
- [x] [Review][Patch] Banned `!` non-null assertion [client/src/features/dashboard/hooks/useDashboard.ts:7] — `getDashboard(flatId!)`. Fix: `getDashboard(flatId as string)` or restructure so the assertion isn't needed.
- [x] [Review][Patch] `enabled` check doesn't guard against an empty-string `flatId` [client/src/features/dashboard/hooks/useDashboard.ts:8] — `enabled: flatId !== undefined` passes for `""`, producing a malformed `/flats//dashboard` request. Fix: `enabled: !!flatId`.
- [x] [Review][Patch] Relative imports instead of the `@/` alias in new dashboard files [client/src/features/dashboard/hooks/useDashboard.ts:2, client/src/features/dashboard/components/DashboardGrid.tsx:4, both new test files] — violates the project's explicit alias-only import rule. Fix: switch to `@/features/dashboard/...` imports.
- [x] [Review][Patch] Unvalidated `lastReadingDate` can crash the dashboard render [client/src/features/dashboard/components/DashboardGrid.tsx:68-70] — `Intl.DateTimeFormat.format(new Date(malformedString))` throws `RangeError` for an unparsable date, and no error boundary exists around this render tree. Fix: validate `!Number.isNaN(date.getTime())` before formatting, fall back to `'—'`.
- [x] [Review][Patch] German `atBudget` copy drops the "daily" qualifier present in English [client/src/locales/de-DE/dashboard.json] — `"— im Budget"` vs en-US `"— at daily budget"`, a meaning drift. Fix: `"— im Tagesbudget"` (or equivalent).

### Deferred

- [x] [Review][Dismiss] Inline `style={{}}` on `EuroBurnGradient` [client/src/features/dashboard/components/EuroBurnGradient.tsx:17-19] — accepted as an exception to the "Tailwind classes only" rule; a data-driven 5-stop gradient can't be expressed as static Tailwind classes, same rationale as the existing `--gradient-angle` CSS custom property.
- [x] [Review][Defer] Zero test coverage for `EuroBurnGradient`, `CostGapBadge`, and `DashboardPage` wiring [client/src/features/dashboard/components/EuroBurnGradient.tsx, CostGapBadge.tsx, DashboardPage.tsx] — deferred, pre-existing gap outside Task 9's literal scope (would have caught the AC-5 gradient bug above directly).
- [x] [Review][Defer] `spikeDays` is fetched into `DashboardSummary` but never rendered [client/src/features/dashboard/api/dashboardApi.ts:20] — deferred, no AC in this story covers a spike indicator.
- [x] [Review][Defer] AC-5's "Enter Reading CTA" is unimplemented [DashboardGrid.tsx] — deferred to Story 3.4, explicitly acknowledged in the Dev Agent Record.
- [x] [Review][Defer] `package-lock.json` has unrelated transitive-dependency flag changes (`@types/react-dom` dev→devOptional, `tslib` loses dev/optional flags) [client/package-lock.json] — deferred, likely an npm resolution side-effect of adding `@radix-ui/react-popover`, not a hand-edit.
- [x] [Review][Defer] Architecture Compliance Checklist left entirely unchecked despite the Dev Agent Record claiming full completion — deferred, process nit; several unchecked boxes correspond to the patch items above.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- Backend prerequisite check: `dotnet test api.Tests` — 63/63 passed, confirming the Story 3.2 `CostSummary?` nested shape amendment (`api/Features/Dashboard/DashboardModels.cs`) was already in place before starting.
- `client/src/components/ui/` was empty (no shadcn components generated yet) despite `components.json` being configured. Ran `npx shadcn@latest add popover`; the CLI misresolved the `@/` alias and wrote to a literal `client/@/components/ui/popover.tsx` — moved it to `client/src/components/ui/popover.tsx` and removed the stray `@` directory.
- The generated `Popover` used shadcn's default theme tokens (`bg-popover`, `border`) which don't exist in this project's Tailwind `@theme` (no shadcn CSS variables were ever set up here). Restyled it with the project's existing glass-surface tokens (`bg-glass-surface`-equivalent dark background, `border-glass-border`, `rounded-card`, `text-text-primary`) so the popover is actually visible against the dark UI.
- `useUserSettings()` returns `{ settings, isLoading, isError }`, not `{ data }` as shown in the Dev Notes Task 7 snippet — adjusted `DashboardPage.tsx` to destructure `settings` (verified against actual usage in `App.tsx`, `OnboardingPage.tsx`, `FlatBaselineEdit.tsx`).
- `client/src/components/EuroBurnGradient.tsx` (from Story 1.5) already renders a static, prop-less gradient globally from `AppShell.tsx` on every route. The new dynamic `features/dashboard/components/EuroBurnGradient.tsx` is a separate component rendered only by `DashboardPage`; because both are `fixed inset-0 -z-10` with no intervening stacking contexts, the Dashboard-specific one (later in DOM tree order) paints on top while the Dashboard route is active, and the shell's static one shows on all other routes. No changes to `AppShell.tsx` were needed or made (out of this story's task scope).
- `DashboardSummary` has no `annualKwhBaseline` field (AC-3's Tile-1 caption needs it). Added it as an explicit `annualKwhBaseline?: number` prop threaded from `DashboardPage` (sourced from `useUserSettings`) into `DashboardGrid`, rather than importing the settings hook from within the dashboard feature (would violate VSA slice isolation).
- Added one translation key not listed in the Dev Notes' key table (`tile.todayVsBudget`) to cover AC-3's Tile-4 subline text ("vs {DailyBudgetKwh} kWh/day budget"), since no hardcoded strings are allowed in JSX.
- Currency formatting uses `Intl.NumberFormat(locale, { style: 'currency', currency: 'EUR' })` rather than the Dev Notes' illustrative `€{formatted}` string concatenation, per the project's non-negotiable "all currency values formatted with locale-aware Intl.NumberFormat" rule (string concatenation would misplace the € symbol for de-DE).
- Story status in `sprint-status.yaml` was `backlog`, not `ready-for-dev`, when this session started. Verified the story's hard prerequisite (Story 3.2 backend amendment) was already satisfied via the `dotnet test` run above before proceeding.
- Browser/visual verification was not performed: the Claude-in-Chrome extension was not connected, no local SWA CLI / backend (`local.settings.json`, `swa` CLI) was available to exercise the real authenticated flow, and no headless browser tooling (Playwright) was installed. Correctness was verified via the full Vitest suite (which exercises every AC branch: skeleton, cold-open dashes, KPI values, all four cost-gap states, and badge scoping), `tsc -b` build, and manual review of Tailwind classes against the tokens defined in `client/src/index.css`.

### Completion Notes List

- Implemented all 10 tasks: API module, TanStack Query hook, dynamic Euro Burn gradient, base glass `KpiTile`, `CostGapBadge` (including generating and re-theming the previously-missing shadcn `Popover` primitive), `DashboardGrid` composition with the `resolveCostDisplay` gap/coverage decision tree, `DashboardPage` wiring, en-US/de-DE translation files, and 10 new tests (8 for `DashboardGrid`, 2 for `useDashboard`).
- AC-5's mention of "the Enter Reading CTA is prominently visible" is explicitly out of scope for this story — no task in this story's Tasks/Subtasks section covers building that CTA, and Story 3.4 ("Enter Reading CTA Bottom Sheet and Immediate Dashboard Update") owns it. Only the cold-open dash/gradient/last-read behavior from AC-5 was implemented here.
- Full regression suite: 65/65 Vitest tests pass (10 new + 55 pre-existing); `tsc -b && vite build` exits 0 with no TypeScript errors; `oxlint` exits 0 (only pre-existing, unrelated `router.tsx` fast-refresh warnings).
- Backend regression: `dotnet test api.Tests` — 63/63 passed (run to confirm the 3.2 prerequisite; no backend files were changed by this story).

### File List

**New files:**
- client/src/features/dashboard/api/dashboardApi.ts
- client/src/features/dashboard/components/EuroBurnGradient.tsx
- client/src/features/dashboard/components/KpiTile.tsx
- client/src/features/dashboard/components/CostGapBadge.tsx
- client/src/features/dashboard/components/DashboardGrid.tsx
- client/src/features/dashboard/hooks/useDashboard.ts
- client/src/features/dashboard/__tests__/DashboardGrid.test.tsx
- client/src/features/dashboard/__tests__/useDashboard.test.ts
- client/src/components/ui/popover.tsx (shadcn-generated; reverted to stock shadcn defaults in the review pass)

**Modified files:**
- client/src/features/dashboard/DashboardPage.tsx (replaced stub; review pass added `isError` handling and cold-open gradient signal)
- client/src/features/dashboard/components/EuroBurnGradient.tsx (review pass: `isColdOpen` prop forces neutral midpoint)
- client/src/features/dashboard/components/DashboardGrid.tsx (review pass: `@/` alias import, invalid-date guard on `lastReadingDate`)
- client/src/features/dashboard/components/CostGapBadge.tsx (review pass: project-specific `PopoverContent` styling moved here from `ui/popover.tsx`)
- client/src/features/dashboard/hooks/useDashboard.ts (review pass: `@/` alias import, removed `!` assertion, `enabled: !!flatId`)
- client/src/features/dashboard/__tests__/DashboardGrid.test.tsx (review pass: `@/` alias imports)
- client/src/features/dashboard/__tests__/useDashboard.test.ts (review pass: `@/` alias imports)
- client/src/locales/en-US/dashboard.json (was an empty `{}` stub)
- client/src/locales/de-DE/dashboard.json (was an empty `{}` stub; review pass fixed `atBudget` copy drift)
- client/package.json / client/package-lock.json (added `@radix-ui/react-popover` dependency via `npx shadcn add popover`)

## Change Log

- 2026-07-01: Implemented Story 3.3 — KPI Dashboard frontend (Euro Burn gradient, KPI grid, cost-gap badge/popover, i18n keys, tests). Generated the previously-missing shadcn `Popover` primitive and re-themed it to the project's existing glass-surface design tokens. Status: backlog → review.
- 2026-07-01: Code review pass — fixed AC-5 cold-open gradient bug (now forces neutral midpoint via `isColdOpen`), added `isError` handling to `DashboardPage`, reverted `ui/popover.tsx` to shadcn defaults (moved project styling to `CostGapBadge`), removed banned `!` assertion and empty-string `flatId` gap in `useDashboard`, switched remaining relative imports to the `@/` alias, guarded `lastReadingDate` formatting against invalid dates, and fixed a German copy drift in `atBudget`. Full suite verified (65/65 tests, `tsc -b`, `vite build`, `oxlint` all clean). Status: review → done.
