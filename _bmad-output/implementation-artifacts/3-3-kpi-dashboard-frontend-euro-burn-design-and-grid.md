---
baseline_commit: 8432120
---

# Story 3.3: KPI Dashboard Frontend — Euro Burn Design & Grid

Status: backlog

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

- [ ] Task 1: Create `dashboardApi.ts` — TypeScript types + API function (AC: 1)
  - [ ] `client/src/features/dashboard/api/dashboardApi.ts` — `CostSummary`, `DashboardSummary` types; `getDashboard(flatId: string)` calling `apiClient.get<DashboardSummary>` (see Dev Notes)

- [ ] Task 2: Create `useDashboard.ts` — TanStack Query hook (AC: 1)
  - [ ] `client/src/features/dashboard/hooks/useDashboard.ts` — `useQuery` with key `['dashboard', flatId]`; enabled only when `flatId` is defined (see Dev Notes)

- [ ] Task 3: Create `EuroBurnGradient.tsx` — background gradient component (AC: 4, 5)
  - [ ] `client/src/features/dashboard/components/EuroBurnGradient.tsx` — accepts `todayKwh` and `dailyBudgetKwh`; computes ratio; outputs CSS gradient as inline style on a full-bleed wrapper (see Dev Notes)

- [ ] Task 4: Create `KpiTile.tsx` — base glass tile component (AC: 2, 3, 5, 6)
  - [ ] `client/src/features/dashboard/components/KpiTile.tsx` — accepts `headline`, `subline`, `delta?`, `caption?` props; renders glass card; handles `undefined` headline as skeleton placeholder (see Dev Notes)

- [ ] Task 5: Create `CostGapBadge.tsx` — amber gap indicator + popover (AC: 8, 9, 10)
  - [ ] `client/src/features/dashboard/components/CostGapBadge.tsx` — renders amber badge with coverage text; wraps shadcn `Popover` with explanation text and "Update tariff →" link to `/settings` (see Dev Notes)

- [ ] Task 6: Create `DashboardGrid.tsx` — assembles all four tiles (AC: 2, 3, 5, 7, 8, 9, 10, 11, 12)
  - [ ] `client/src/features/dashboard/components/DashboardGrid.tsx` — receives `DashboardSummary | undefined`; composes four `KpiTile` instances; applies `CostGapBadge` where applicable on cost sublines; renders "Last read" line; handles null/undefined states (see Dev Notes)

- [ ] Task 7: Wire `DashboardPage.tsx` (AC: 1, 4, 5, 6)
  - [ ] `client/src/features/dashboard/DashboardPage.tsx` — call `useDashboard`; render `EuroBurnGradient` + `DashboardGrid`; pass `isLoading` through to skeleton state (see Dev Notes)

- [ ] Task 8: Add translation keys (AC: 3, 5, 8, 9, 10, 12)
  - [ ] `client/src/locales/en-US/dashboard.json` — create file with all keys (see Dev Notes for full key list)
  - [ ] `client/src/locales/de-DE/dashboard.json` — German equivalents

- [ ] Task 9: Tests (AC: 1, 5, 6, 7, 8, 9, 10, 11)
  - [ ] `client/src/features/dashboard/components/DashboardGrid.test.tsx` — minimum 8 tests (see Dev Notes)
  - [ ] `client/src/features/dashboard/hooks/useDashboard.test.ts` — minimum 2 tests

- [ ] Task 10: Final verification
  - [ ] `cd client && npm run build` exits 0 with zero TypeScript errors
  - [ ] `cd client && npm test` — all tests pass including all pre-existing tests
  - [ ] `cd client && npm run lint` exits 0
  - [ ] Update File List in this story

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
- [ ] `@/` alias for all imports from `src/` — never relative paths
- [ ] TanStack Query v5: `isPending` not `isLoading` for loading state
- [ ] `useQuery({ queryKey, queryFn, enabled })` object form — no positional overload
- [ ] `cost !== null` explicit null check — not truthiness
- [ ] Never use `?.` in Shouldly-style assertions — use `ShouldNotBeNull()` then `!.`
- [ ] shadcn `Popover` from `@/components/ui/popover` — never hand-edit `ui/` components
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

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

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
- client/src/locales/en-US/dashboard.json
- client/src/locales/de-DE/dashboard.json

**Modified files:**
- client/src/features/dashboard/DashboardPage.tsx (replace stub)
