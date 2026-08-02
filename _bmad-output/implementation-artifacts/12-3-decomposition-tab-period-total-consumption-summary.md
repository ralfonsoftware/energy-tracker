---
baseline_commit: 4cea089
---

# Story 12.3: Decomposition Tab — Period Total Consumption Summary

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to see my total kWh and cost for the currently selected period displayed alongside the period selector,
so that I can easily relate the individual Room and Device breakdown figures to the whole period total.

## Acceptance Criteria

1. **Given** `DecompositionTab.tsx` renders successfully (`isUnavailable = false`), **when** the period's data loads, **then** a Period Total summary tile renders directly alongside/below `PeriodSelector.tsx`, above the Residual card, showing `totalKwh` and `totalCost` for the selected period — reusing the existing glass-surface `KpiTile` visual pattern; no new API call, purely consumes the already-fetched `DecompositionResponse.totalKwh`/`totalCost` fields (both already present in the contract since Story 7.1; `totalCost` is currently unused in the tab).

2. **Given** the query is loading, **when** the tab renders, **then** the Period Total tile shows `KpiTile`'s built-in skeleton state (its `headline={undefined}` branch), sized to avoid layout shift when data arrives — it is the same `KpiTile` element in both states, not a separate generic skeleton block.

3. **Given** `DecompositionResponse.isUnavailable = true`, **when** the unavailable state renders, **then** the Period Total tile is **not** shown — consistent with FR-34 (no partial/zero figures for unavailable periods) and the existing Residual-card suppression behavior.

4. **Given** the active Locale, **when** `totalKwh`/`totalCost` render, **then** values are formatted via the same `Intl.NumberFormat` helpers already used elsewhere in the Decomposition feature — no hardcoded formatting.

5. **Given** `DecompositionTab.test.tsx`, **when** run, **then** tests cover: tile renders correct kWh/cost on success; tile shows skeleton while loading; tile is absent when `isUnavailable = true`.

## Tasks / Subtasks

- [x] Task 1: Add the Period Total tile to `DecompositionTab.tsx` (AC: #1, #2, #3, #4)
  - [x] 1.1 Import `KpiTile` from `@/features/dashboard/components/KpiTile` (cross-feature import — see Dev Notes "Why this is a deliberate exception to VSA slice isolation" before assuming this needs a duplicated local component).
  - [x] 1.2 Add local `formatNumber`/`formatKwh`/`formatCurrency` helpers to `DecompositionTab.tsx`, copied verbatim from `ResidualCard.tsx`'s existing three functions (same per-file duplication convention already used in `ResidualCard.tsx`, `RoomCard.tsx`, `DeviceCard.tsx`, `SmartStripCard.tsx` — do not extract a shared helper, do not import from a sibling component file).
  - [x] 1.3 Compute `const showPeriodTotal = !isCustomRangeIncomplete && !isError && !data?.isUnavailable` alongside the component's other derived values (after the `useDecomposition` call).
  - [x] 1.4 Render immediately after `<PeriodSelector .../>` and before the `isCustomRangeIncomplete` message block:
    ```tsx
    {showPeriodTotal && (
      <KpiTile
        label={t('periodTotal.label')}
        headline={data ? formatKwh(data.totalKwh) : undefined}
        subline={data ? formatCurrency(data.totalCost) : undefined}
      />
    )}
    ```
    `data` is `undefined` while pending (per `useDecomposition`'s `enabled` gating and TanStack Query v5's `isPending` semantics), so `headline`/`subline` naturally resolve to `undefined` in that case, which is exactly what triggers `KpiTile`'s internal skeleton — no separate loading branch needed.

- [x] Task 2: Add the new translation key (AC: #1)
  - [x] 2.1 Add `"periodTotal": { "label": "Period total" }` to `client/src/locales/en-US/decomposition.json`.
  - [x] 2.2 Add `"periodTotal": { "label": "Zeitraum gesamt" }` to `client/src/locales/de-DE/decomposition.json`. Both locale files must gain the key — the frontend rule "every feature namespace added to `ns: [...]`" doesn't apply here (the `decomposition` namespace already exists), but every existing key must stay present in both files (missing keys silently fall back to the key string, no build error).

- [x] Task 3: Test coverage in `DecompositionTab.test.tsx` (AC: #5)
  - [x] 3.1 `DecompositionTab_NormalResponse_RendersPeriodTotalTileWithKwhAndCost`: `mockDecomposition({ data: makeResponse({ totalKwh: 123.4, totalCost: 45.6 }) })`, render, assert `screen.getByText('periodTotal.label')` is present and a sibling element's text content matches `/123.4/` (kWh) and another matches `/45[.,]6/` (cost) — mirror `ResidualCard.test.tsx`'s locale-tolerant regex pattern for currency.
  - [x] 3.2 `DecompositionTab_Loading_PeriodTotalTileShowsSkeleton`: `mockDecomposition({ isPending: true })`, render, assert `screen.getByText('periodTotal.label')` is present (the tile itself renders) and `screen.getByRole('status', { name: 'loading' })` is present (KpiTile's skeleton element) — do not assert on the generic `.animate-pulse` count here, that's the pre-existing three-block test (`DecompositionTab_Loading_RendersThreeSkeletonBlocks`) and is unrelated/unchanged.
  - [x] 3.3 `DecompositionTab_Unavailable_PeriodTotalTileNotRendered`: reuse the existing `mockDecomposition({ data: makeResponse({ isUnavailable: true }) })` setup, assert `screen.queryByText('periodTotal.label')` is `null`.

### Review Findings

- [x] [Review][Patch] Unescaped regex dot weakens period-total success test [client/src/features/decomposition/components/DecompositionTab.test.tsx:167]

## Dev Notes

### Why this story exists

Pure UI surfacing gap, not a data or backend gap — `DecompositionResponse.totalKwh` is already consumed internally by `ResidualCard.tsx` for its percentage calculation, and `totalCost` is already on the wire (since Story 7.1) but never rendered anywhere in `DecompositionTab.tsx`. Sourced directly from Ralf via `bmad-correct-course` on 2026-08-01 (`sprint-change-proposal-2026-08-01-period-total.md`), not from architecture review or brainstorming like Stories 12.1/12.2 — thematically unrelated to Epic 12's device-lifecycle scope, but placed here per Ralf's explicit choice rather than reopening the already-`done` Epic 7. **No dependency on Stories 12.1 or 12.2** — this story touches none of the files either of those stories touched (`DecompositionEngine.cs`, `UpdateFlatStructureFunction.cs`, the flat-structure client files) and can be implemented independently of them.

### Scope discipline — this is a frontend-only, one-file-of-substance story

No backend/API/migration changes. No new component file. No new hook. `DecompositionResponse` already carries both fields (`client/src/features/decomposition/api/decompositionApi.ts:37-38`) — do not add anything to that type or touch `decompositionApi.ts`/`useDecomposition.ts` at all.

### Why importing `KpiTile` across feature slices is correct here, not a violation

Project-context.md's VSA isolation rule reads: *"flatId is sourced from `useParams()` or passed as a prop — never by importing a hook from another feature slice"* and *"cross-slice hook imports are forbidden."* That rule is scoped to **hooks** (which carry data-fetching/state coupling across slices) — `KpiTile.tsx` (`client/src/features/dashboard/components/KpiTile.tsx`) is a pure, stateless presentational component with no hooks, no `flatId`, no data fetching of its own; it only exists in the `dashboard` folder because that's the first feature that needed it. The sprint-change-proposal that spawned this story is explicit: *"Architecture: no changes. No new component, pattern, migration, or API contract."* "No new component" means reuse via direct import, not duplicate the visual pattern into a second file. This is the first cross-feature import of `KpiTile` in the codebase (previously only `DashboardGrid.tsx` used it) — that's expected, not a sign something's wrong.

Contrast this with the codebase's *other* established convention — per-file duplication of the tiny `formatNumber`/`formatKwh`/`formatCurrency` trio (every one of `DeviceCard.tsx`, `RoomCard.tsx`, `ResidualCard.tsx`, `SmartStripCard.tsx` defines its own copy verbatim). That convention applies to trivial, feature-local formatting helpers with no shared component to anchor to — it does not mean "never share a component." Task 1.2 follows the duplication convention for the formatters; Task 1.1 follows the reuse convention for `KpiTile` itself. Do not duplicate `KpiTile` into a new `PeriodTotalTile.tsx`, and do not try to extract the formatters into a shared `lib/format.ts` — either would be scope creep beyond what this small story calls for.

### `KpiTile` contract (read `client/src/features/dashboard/components/KpiTile.tsx` in full — 38 lines)

```ts
type Props = {
  label: string
  headline: ReactNode | undefined // undefined → skeleton
  subline?: ReactNode
  delta?: string
  deltaVariant?: 'under' | 'over' | 'neutral'
  caption?: string
}
```
Only `label`/`headline`/`subline` are needed here — omit `delta`/`deltaVariant`/`caption` entirely (they're for the Dashboard's budget-delta use case, not applicable to Decomposition). The `headline === undefined` skeleton branch already renders `role="status" aria-label="loading"` with a fixed-height pulse block — this is exactly AC2's "sized to avoid layout shift" requirement, satisfied for free by the component, not something to build.

### Exact render-order and gating logic in `DecompositionTab.tsx` (read in full during story creation — 98 lines)

Current structure (top to bottom): `PeriodSelector` → `isCustomRangeIncomplete` message → generic 3-block skeleton (gated `!isCustomRangeIncomplete && isPending`) → error block (gated `!isCustomRangeIncomplete && !isPending && isError`) → `DecompositionUnavailable` (gated `!isPending && !isError && data?.isUnavailable`) → success content (`ResidualCard` + interpolated banner + `RoomCard`s, gated `!isPending && !isError && data !== undefined && !data.isUnavailable`).

The new tile's guard (`showPeriodTotal`) deliberately mirrors this existing gating style (boolean combination of the same three query-state flags already in scope) rather than introducing a new pattern. It must exclude the `isCustomRangeIncomplete` case for the same reason the generic skeleton does: when a custom range has no dates yet, `useDecomposition`'s `enabled: false` still leaves the query in TanStack Query v5's `isPending = true` status even though no fetch is happening — showing a loading tile there would be misleading (it would never resolve until the user picks dates). This is not itself a new AC — it's this story's implementation correctly extending an already-established guard pattern in the file, not a scope addition.

### Formatting (AC4)

Copy `ResidualCard.tsx`'s three functions verbatim:
```ts
const formatNumber = (value: number) =>
  new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 1 }).format(value)
const formatKwh = (value: number) => `${formatNumber(value)} kWh`
const formatCurrency = (value: number) =>
  new Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR' }).format(value)
```
Requires `import i18n from '@/lib/i18n'` in `DecompositionTab.tsx` (not currently imported there).

### What NOT to touch

- `decompositionApi.ts`, `useDecomposition.ts` — no changes; both fields already exist on the wire.
- `ResidualCard.tsx` — already consumes `totalKwh` for its own percentage; leave its props/logic untouched.
- Backend (`DecompositionEngine.cs` and everything under `api/Features/Decomposition/`) — zero backend changes in this story.
- `PeriodSelector.tsx` — untouched; the new tile is a sibling rendered after it, not a prop/composition change to the selector itself.

### Testing Rules (from project context)

- Vitest + `@testing-library/react`, `globals: true` (no `describe`/`it`/`expect` imports).
- Query by role/text, not CSS class/`data-testid` — except the pre-existing `.animate-pulse` count assertion in the untouched loading test, which stays as-is.
- `react-i18next` is mocked in this file (`t: (k) => k`) — assert against raw translation keys (`'periodTotal.label'`), not rendered English/German text.
- Run `npm test -- --run`, `npx tsc -b`, and `npm run lint` (all from `client/`) before considering this story done — `npx tsc --noEmit` is a silent no-op in this repo, per Story 12.1/12.2's explicit correction; do not use it.

### Project Structure Notes

- Files touched: `client/src/features/decomposition/components/DecompositionTab.tsx` (modified), `client/src/features/decomposition/components/DecompositionTab.test.tsx` (modified), `client/src/locales/en-US/decomposition.json` (modified), `client/src/locales/de-DE/decomposition.json` (modified).
- No new files. No backend files. No migration.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md#Story 12.3] — epic-level AC, used verbatim (no gaps found this time, unlike Stories 12.1/12.2 — the epic text matches the actual current-state code exactly)
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-08-01-period-total.md] — full change proposal; origin, rationale, confirmed design decisions (KpiTile reuse, both kWh+cost shown, suppressed in unavailable state)
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md:437] — FR-54 (applied)
- [Source: _bmad-output/planning-artifacts/epics/requirements-inventory.md:68,151,208] — FR-54 and UX-DR21 (applied)
- [Source: client/src/features/decomposition/components/DecompositionTab.tsx] — full file read; exact render structure and gating logic above
- [Source: client/src/features/decomposition/components/DecompositionTab.test.tsx] — full file read; existing mock/test conventions (`mockDecomposition`, `makeResponse`) to extend, not replace
- [Source: client/src/features/decomposition/api/decompositionApi.ts] — `DecompositionResponse` type; `totalKwh`/`totalCost` already present
- [Source: client/src/features/decomposition/components/ResidualCard.tsx] — full file read; the `formatNumber`/`formatKwh`/`formatCurrency` pattern duplicated verbatim, and the `data.isUnavailable`-suppression precedent this story's tile mirrors
- [Source: client/src/features/dashboard/components/KpiTile.tsx] — full file read; component contract, skeleton behavior
- [Source: client/src/features/dashboard/components/DashboardGrid.tsx:98-115] — existing `KpiTile` usage pattern (`headline={x === undefined ? undefined : formatKwh(...)}`) this story's usage mirrors
- [Source: client/src/locales/en-US/decomposition.json, de-DE/decomposition.json] — full files read; both need the new `periodTotal.label` key
- [Source: _bmad-output/project-context.md] — VSA slice isolation rule (scoped to hooks, not presentational components — see Dev Notes above), i18n rules, testing rules, "no cross-feature hook imports" clarified

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

### Completion Notes List

- Implemented the Period Total `KpiTile` in `DecompositionTab.tsx` per Dev Notes exactly: cross-feature import of `KpiTile` (no duplication), per-file duplicated `formatNumber`/`formatKwh`/`formatCurrency` helpers copied verbatim from `ResidualCard.tsx`, `showPeriodTotal` gating mirroring the existing query-state guard style.
- Added `periodTotal.label` translation key to both `en-US/decomposition.json` and `de-DE/decomposition.json`.
- Added the 3 new tests specified in Task 3.1–3.3; all pass.
- Deviation from Dev Notes wording on task 3.2: the note said the pre-existing `DecompositionTab_Loading_RendersThreeSkeletonBlocks` test is "unrelated/unchanged" — in practice it broke, because `KpiTile`'s own loading skeleton also carries the `.animate-pulse` class, so the existing `.animate-pulse` count assertion in that test went from 3 to 4 once the tile renders during the loading state (`showPeriodTotal` is true while pending, per AC2). Updated that one assertion's expected count from 3 to 4 with an explanatory comment; no other change to that test.
- Full verification run: `npm test -- --run` (484/484 passed, 69 files), `npx tsc -b` (clean), `npm run lint` (clean — only pre-existing unrelated `router.tsx` fast-refresh warnings).

### File List

- `client/src/features/decomposition/components/DecompositionTab.tsx` (modified)
- `client/src/features/decomposition/components/DecompositionTab.test.tsx` (modified)
- `client/src/locales/en-US/decomposition.json` (modified)
- `client/src/locales/de-DE/decomposition.json` (modified)

## Change Log

- 2026-08-02: Implemented Story 12.3 — added the Period Total `KpiTile` (kWh + cost) alongside `PeriodSelector` in `DecompositionTab.tsx`, reusing `KpiTile` cross-feature and duplicating the formatter trio per convention. Added `periodTotal.label` to both locale files. Added 3 new tests (AC #5); updated 1 pre-existing test's `.animate-pulse` count assertion (3→4) to account for the tile's own loading skeleton. Full regression pass green (484 tests, tsc -b clean, lint clean). Status → review.
