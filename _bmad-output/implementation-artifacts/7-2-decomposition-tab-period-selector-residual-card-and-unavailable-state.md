---
baseline_commit: 31b8394907757363ae39175c5aba7eb0ac525fb1
---

# Story 7.2: Decomposition Tab — Period Selector, Residual Card & Unavailable State

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to select a date range from a dropdown and immediately see my Residual (unattributed kWh) as the first card in a glass surface — even when it is zero — so I always know how much of my consumption is unaccounted for,
And when no smart plug data exists for the period, I want a clear unavailable state with a direct link to import data.

## Acceptance Criteria

1. **Given** the Decomposition tab renders, **when** it mounts, **then** `DecompositionTab.tsx` renders a period selector dropdown with options: "This week", "This month" (default), "Last month", "This year", "Custom range"; selecting "Custom range" reveals a start/end date picker pair; the selected period's `startDate`/`endDate` (both `yyyy-MM-dd`) drive the TanStack Query key `['decomposition', flatId, { startDate, endDate }]`; every new selection (including toggling in/out of Custom range) triggers an immediate refetch that shows the loading skeleton (AC5) — no stale data is shown while a new period loads (do not use `placeholderData`/`keepPreviousData`).

2. **Given** `useDecomposition(flatId, startDate, endDate)` via TanStack Query, **when** data loads successfully and `response.isUnavailable === false`, **then** the Residual card renders first, before all Room cards, regardless of `response.residual.kwh`; it is never suppressed, including when `residual.kwh === 0`.

3. **Given** the Residual card renders, **when** `residual.kwh > 0` or `residual.kwh === 0`, **then** in both cases: the card uses the glass surface system (`rounded-card border border-glass-border`, `residual-tint` amber background per DESIGN.md `Progress Card`/mockup `.residual-card` treatment — see Dev Notes for exact styling); the card header shows "Residual" (i18n key `decomposition:residual.title`); the kWh value and attributed cost (`residual.kwh`, `residual.cost`) are shown; no collapse or hide control is present; the card is visually first in the list.

4. **Given** `response.hasInterpolatedData === true`, **when** the tab renders, **then** a non-blocking info banner appears above the Residual/Room cards (below the period selector): "Some values have been interpolated from incomplete import data." (i18n key `decomposition:interpolatedBanner`); it does not block interaction with any card. (Backend guarantee: `hasInterpolatedData` is always `false` when `isUnavailable === true` — see Dev Notes — so no extra guard is needed to keep the banner out of the unavailable state.)

5. **Given** `response.isUnavailable === true`, **when** the tab renders, **then** instead of the Residual/Room cards, an unavailable state renders with: an informational icon, heading "No smart plug data for this period" (i18n key `decomposition:unavailable.heading`), body copy "Upload a smart plug export to see your breakdown" (i18n key `decomposition:unavailable.body`), and a primary CTA button "Import Data" (i18n key `decomposition:unavailable.cta`) that navigates to `/decomposition/import` (the existing Import surface route — see Dev Notes); the Residual card is not shown in this state (FR-34).

6. **Given** the query is in a loading state (`isPending === true`), **when** the tab renders, **then** skeleton placeholders render matching the height of the Residual card and two Room cards (three stacked skeleton blocks, `animate-pulse`); no layout shift occurs when real data arrives (skeleton block heights match the real card's rendered height class, not an arbitrary guess).

7. **Given** the query returns an error (`isError === true`), **when** the tab renders, **then** an error state with "Couldn't load decomposition" (i18n key `decomposition:loadError`) and a Retry button (`refetch()`, i18n key `decomposition:retry`) is shown, mirroring the exact `isError`/`refetch()` pattern already used in `ReadingHistorySheet.tsx` and `TariffList.tsx` (see Dev Notes).

8. **Given a gap found during story creation** — the epic's AC2/AC3 describe "Residual card renders first before all Room cards," implying Room cards already exist as a renderable concept, but Epic Story 7.3's own AC1 ("`RoomCard.tsx` renders... child DeviceCards are rendered in a vertical list within the room... rooms ordered by descending kWh") is the story that actually specifies `RoomCard.tsx`'s device-list content and the "rich/measured" vs "compact/estimated" `DeviceCard` variants — none of which this story's own ACs (1–7 above) require or mention — **when** `DecompositionTab.tsx` renders `response.rooms` for a non-unavailable response, **then** it renders one minimal `RoomCard.tsx` per room showing only: room name (`RoomDecomposition.roomName`), room kWh (`RoomDecomposition.kwh`), and room cost (`RoomDecomposition.cost`) in the card header — glass card styling, no device list, no ordering requirement beyond the array order the API returns (backend already orders by `Room.SortOrder`, per `DecompositionEngine.cs`). `RoomCard.tsx` is intentionally a placeholder for this story; Story 7.3 will extend the same file to add the `DeviceCard`-per-room vertical list and descending-kWh ordering. Do not build `DeviceCard.tsx`, `SmartStripCard.tsx`, or any device-level rendering in this story — that is out of scope and belongs to 7.3.

9. **Given a gap found during story creation** — the epic AC text for the Residual card header ("shows 'Residual'"), the unavailable-state heading ("'No smart plug data for this period'"), body ("'Upload a smart plug export to see your breakdown'"), and CTA ("'Import Data'") are each explicitly quoted with a `(i18n key)` annotation in the epic, but the UX mockup (`decomposition-tab.html`) and `EXPERIENCE.md`'s microcopy table use different literal wording for the same states (mockup: residual title "Unattributed", unavailable heading "No smart plug data for Jun 2026" (dynamic month), body "Import smart plug exports to see a breakdown by room and device.", CTA "Import Smart Plug Data"; `EXPERIENCE.md`: unavailable body "Decomposition data is unavailable" / "No data for this period. Import smart plug data to see a breakdown." for the closely related but distinct "Empty Decomposition" row) — **when** writing the `decomposition.json` locale strings for this story, **then** use the epic's literal quoted AC text verbatim as the source of truth for every i18n key listed in ACs 3, 5, and 7 above (these are the testable strings); treat the mockup's and `EXPERIENCE.md`'s wording as visual/tonal reference only, not literal copy to reproduce. This resolves a genuine three-way wording conflict between epic, mockup, and UX experience doc found during story creation — flag no further action needed, this AC is the resolution.

10. **Given a gap found during story creation** — none of the epic/PRD/architecture text specifies exact calendar boundaries for "This week", "This month", "Last month", or "This year" (only `EXPERIENCE.md`'s Interaction Primitives section names the dropdown options and says "Default for Decomposition: This month" — no start/end-of-period semantics) — **when** `DecompositionTab.tsx` computes `startDate`/`endDate` for each non-Custom option, **then** use these calendar boundaries (a story-creation decision, not specified elsewhere — document as such, do not treat as negotiable epic text): `"This week"` = Monday of the current ISO week → today; `"This month"` (default) = the 1st of the current calendar month → today; `"Last month"` = the 1st of the previous calendar month → the last day of the previous calendar month (a fully completed period, unlike the other three which are month/week/year-to-date); `"This year"` = January 1st of the current year → today. All dates are computed from the client's local `Date`, formatted via the existing `toLocalDateString` helper (`client/src/lib/localDate.ts`) — do not introduce a new date-formatting utility.

## Tasks / Subtasks

- [x] Task 1: `decompositionApi.ts` — API client + response types (AC: 1, 2, 3, 4, 5, 8)
  - [x] Create `client/src/features/decomposition/api/decompositionApi.ts` mirroring `dashboardApi.ts`'s shape: TypeScript types matching the backend `DecompositionResponse` JSON shape exactly (camelCase, decimals as `number`, `DateOnly` as ISO `yyyy-MM-dd` strings) — `PeriodRange { startDate: string; endDate: string }`, `ResidualItem { kwh: number; cost: number }`, `DeviceDecomposition { deviceId: string; name: string; kwh: number; cost: number; approach: 'Measured' | 'EuLabel' | 'SelfMeasured' | 'None'; isSmartStrip: boolean; subDevices: SubDeviceDecomposition[] | null }`, `SubDeviceDecomposition { deviceId: string; name: string; kwh: number; cost: number; isConfigured: boolean; isUnconfigured: boolean }`, `RoomDecomposition { roomId: string; roomName: string; kwh: number; cost: number; devices: DeviceDecomposition[] }`, `DecompositionResponse { period: PeriodRange; totalKwh: number; totalCost: number; isUnavailable: boolean; hasInterpolatedData: boolean; residual: ResidualItem; rooms: RoomDecomposition[] }`.
  - [x] `getDecomposition(flatId: string, startDate: string, endDate: string) => apiClient.get<DecompositionResponse>(\`/flats/${flatId}/decomposition?startDate=${startDate}&endDate=${endDate}\`)`. Only `deviceId`/`name`/`kwh`/`cost`/`approach`/`isSmartStrip`/`subDevices` need to exist on the type for this story (this story doesn't render them), but define them fully now since `DecompositionEngine.cs` already returns them and Story 7.3 will consume them from this same file — do not create a second, narrower type file.

- [x] Task 2: Period calculation helper (AC: 1, 10)
  - [x] Create `client/src/features/decomposition/lib/periods.ts` exporting a `PeriodOption` union type (`'thisWeek' | 'thisMonth' | 'lastMonth' | 'thisYear' | 'custom'`) and a `resolvePeriodRange(option: Exclude<PeriodOption, 'custom'>, today: Date = new Date()): { startDate: string; endDate: string }` function implementing AC10's exact boundaries, using `toLocalDateString` from `@/lib/localDate` for formatting. `'custom'` is handled separately in `PeriodSelector.tsx` (user-entered dates, not computed).
  - [x] Unit test `periods.test.ts`: one test per option asserting exact boundary dates against a fixed injected `today` (e.g. `new Date(2026, 5, 15)` for a mid-June "today" — verifies This week's Monday, This month's 1st, Last month's full-May range, This year's Jan 1).

- [x] Task 3: `useDecomposition` hook (AC: 1)
  - [x] Create `client/src/features/decomposition/hooks/useDecomposition.ts`: `useDecomposition(flatId: string | undefined, startDate: string | undefined, endDate: string | undefined)` → `useQuery({ queryKey: ['decomposition', flatId, { startDate, endDate }], queryFn: () => getDecomposition(flatId as string, startDate as string, endDate as string), enabled: !!flatId && !!startDate && !!endDate })`. Mirrors `useDashboard.ts`'s shape exactly. Do **not** pass `placeholderData: keepPreviousData` — AC1 requires the skeleton on every period change, not stale-data blending.

- [x] Task 4: `PeriodSelector.tsx` (AC: 1, 9, 10)
  - [x] Create `client/src/features/decomposition/components/PeriodSelector.tsx` as a hand-rolled listbox on the existing `Popover`/`PopoverTrigger`/`PopoverContent` primitives from `@/components/ui/popover`, following `FlatSwitcher.tsx`'s/`LocaleDropdown.tsx`'s exact convention (pill trigger button with `▾`, `aria-haspopup="listbox"`, `PopoverContent role="listbox"`, each option a `button role="option" aria-selected={...}`) — do **not** install `@radix-ui/react-select` or `@radix-ui/react-dropdown-menu`; neither is in `package.json` today and every existing dropdown in this codebase (flat switcher, locale) uses the Popover-based pattern instead.
  - [x] Props: `{ value: PeriodOption; customRange: { startDate: string; endDate: string } | null; onChange: (option: PeriodOption) => void; onCustomRangeChange: (range: { startDate: string; endDate: string }) => void }`. Selecting "Custom range" from the listbox sets `value = 'custom'` and reveals two native `type="date"` inputs (matching `TariffForm.tsx`'s exact `inputClass`/`colorScheme: 'dark'`/error-border styling — see Dev Notes) below the trigger for start/end; typing a date in either input calls `onCustomRangeChange`. No react-hook-form/zod needed — this is a read-only period selector driving a query key, not a submitted form (AD-17's "one zod schema per form" rule applies to submitted forms/mutations, not this).
  - [x] Label text for each option uses the epic's literal option names as i18n keys: `decomposition:period.thisWeek`, `decomposition:period.thisMonth`, `decomposition:period.lastMonth`, `decomposition:period.thisYear`, `decomposition:period.custom`.

- [x] Task 5: `ResidualCard.tsx` (AC: 2, 3, 9)
  - [x] Create `client/src/features/decomposition/components/ResidualCard.tsx`. Props: `{ kwh: number; cost: number; totalKwh: number }`. Styling per DESIGN.md's `residual-tint` token and the mockup's `.residual-card` block: `rounded-card` (18px), `background: var(--color-residual-tint)`, `border: 1px solid rgba(251,191,36,0.2)` (amber at 20%, matches `ImportProgressCard.tsx`'s existing inline-style precedent for the same token pair — reuse that exact `style={{ background: 'var(--color-residual-tint)', border: '1px solid rgba(251,191,36,0.2)' }}` object rather than a new Tailwind arbitrary-value class).
  - [x] Content: header text "Residual" (`decomposition:residual.title`), kWh value (`Intl.NumberFormat` + " kWh" suffix, matching `DashboardGrid.tsx`'s local `formatNumber`/`formatKwh` pattern — duplicate a local formatter in this file per this codebase's no-shared-formatting-utility convention, do not import from `dashboard/`), cost (`Intl.NumberFormat(..., { style: 'currency', currency: 'EUR' })`). Percentage-of-total (`kwh / totalKwh`, guard `totalKwh === 0` → omit) is optional polish matching the mockup's "15% of total" subline — include it if straightforward, but it is not independently tested by any AC.
  - [x] No collapse/expand affordance, no `onClick` — per AC3, this card is never interactive beyond being visually present.

- [x] Task 6: `RoomCard.tsx` — minimal placeholder (AC: 8)
  - [x] Create `client/src/features/decomposition/components/RoomCard.tsx`. Props: `{ room: RoomDecomposition }`. Renders one glass card (`rounded-card border border-glass-border bg-glass-surface`) with room name, kWh, and cost in a header row — mirror `TrendChart.tsx`'s card header structure (`text-label-caps text-text-tertiary` for a section-style label, or `text-body font-semibold text-white` for the room name per the mockup's `.room-name`/`.room-summary` split). No device list, no children — Story 7.3 extends this same file.

- [x] Task 7: `DecompositionUnavailable.tsx` (AC: 5, 9)
  - [x] Create `client/src/features/decomposition/components/DecompositionUnavailable.tsx`. Props: `{ onImport: () => void }`. Renders an informational icon (Lucide `Upload` or `Info`, 48px, `text-text-tertiary`, matching mockup's `.empty-icon`), heading (`decomposition:unavailable.heading`), body (`decomposition:unavailable.body`), and a primary CTA button (`decomposition:unavailable.cta`) calling `onImport` — styled per mockup's `.empty-cta` (full-width, `rgba(255,255,255,0.10)` background, `rounded-card`).

- [x] Task 8: `DecompositionTab.tsx` — orchestration (AC: 1, 2, 4, 5, 6, 7, 8)
  - [x] Create `client/src/features/decomposition/components/DecompositionTab.tsx`. Props: `{ flatId: string | undefined }`. Owns period state: `const [period, setPeriod] = useState<PeriodOption>('thisMonth')` and `const [customRange, setCustomRange] = useState<{startDate: string; endDate: string} | null>(null)`. Derives `{ startDate, endDate }` via `period === 'custom' ? customRange : resolvePeriodRange(period)` (guard `customRange === null` while Custom range is selected but no dates entered yet — pass `undefined`/`undefined` to `useDecomposition`, which the hook's `enabled` gate already handles).
  - [x] Render order (top to bottom, matching mockup): `<PeriodSelector />` → (if `isPending`) skeleton block → (if `isError`) error+retry block → (if `data.isUnavailable`) `<DecompositionUnavailable onImport={() => navigate('/decomposition/import')} />` → (else) `{data.hasInterpolatedData && <InterpolatedBanner />}` then `<ResidualCard ... />` then `{data.rooms.map(room => <RoomCard key={room.roomId} room={room} />)}`.
  - [x] `useNavigate()` from `react-router-dom` for the unavailable-state CTA, navigating to `/decomposition/import` — the existing route already registered in `DecompositionPage.tsx` (`<Route path="import" element={...} />`), reachable at `/decomposition/import` since `DecompositionPage` is mounted at `/decomposition/*` in `router.tsx`. Do not invent a new route.
  - [x] Interpolated-data banner: inline in this file (small enough not to warrant its own component file) — `rounded-2xl`, `background: rgba(96,165,250,0.08)`, `border: 1px solid rgba(96,165,250,0.2)` per DESIGN.md's `accent-info` token, text `decomposition:interpolatedBanner`.
  - [x] Skeleton: three stacked `animate-pulse` blocks — one at the Residual card's typical height, two at Room card height — reuse `rounded-card border border-glass-border bg-glass-surface` shells with `bg-white/10` pulse fill, matching `TrendChart.tsx`'s existing skeleton-block convention (`animate-pulse rounded-t bg-white/10`).
  - [x] Error state: exact `isError`/`refetch()` pattern from `ReadingHistorySheet.tsx`/`TariffList.tsx` — `role="alert"` paragraph with `decomposition:loadError`, then a button calling `refetch()` labeled `decomposition:retry`.

- [x] Task 9: Wire `DecompositionTab` into `DecompositionPage.tsx` (AC: 1–7)
  - [x] Edit `client/src/features/decomposition/DecompositionPage.tsx`'s `DecompositionRoot` function: add `<DecompositionTab flatId={settings?.flatId} />` immediately after the existing `<ImportProgressCard flatId={settings?.flatId} />` line (keep the existing header/upload-icon/`ImportProgressCard` unchanged — this story only adds content below it).

- [x] Task 10: i18n — `decomposition.json` (both locales) (AC: 1, 3, 4, 5, 7, 9)
  - [x] Add to `client/src/locales/en-US/decomposition.json` (currently only `{ "importButton": "..." }` — keep that key, add alongside it): `period.thisWeek`, `period.thisMonth`, `period.lastMonth`, `period.thisYear`, `period.custom`, `period.customStartLabel`, `period.customEndLabel`, `residual.title` ("Residual"), `interpolatedBanner` ("Some values have been interpolated from incomplete import data."), `unavailable.heading` ("No smart plug data for this period"), `unavailable.body` ("Upload a smart plug export to see your breakdown"), `unavailable.cta` ("Import Data"), `loadError` ("Couldn't load decomposition"), `retry` ("Retry").
  - [x] Add the same keys with German translations to `client/src/locales/de-DE/decomposition.json`, matching this codebase's factual/no-exclamation-marks voice (`EXPERIENCE.md` Voice and Tone).
  - [x] `decomposition` namespace is already registered in `client/src/lib/i18n.ts`'s `ns: [...]` array (added when the file was created) — no change needed there.

- [x] Task 11: Tests
  - [x] `periods.test.ts` (Task 2).
  - [x] `useDecomposition.test.ts`: mock `decompositionApi`, assert query key shape and `enabled` gating on missing `flatId`/dates — mirror `useDashboard.test.ts`'s structure.
  - [x] `PeriodSelector.test.tsx`: renders all 5 options; selecting "Custom range" reveals two date inputs; selecting a non-custom option calls `onChange` and does not show date inputs.
  - [x] `ResidualCard.test.tsx`: renders with `kwh: 0` and asserts the card is still present (not suppressed) — this is the AC2/AC3 regression test.
  - [x] `DecompositionTab.test.tsx` (mock `useDecomposition`): covers all 4 response states — loading skeleton, error+retry (`refetch` called on click), `isUnavailable: true` renders `DecompositionUnavailable` and not `ResidualCard`, normal response renders `ResidualCard` first followed by `RoomCard`s, and `hasInterpolatedData: true` renders the banner. Mock `useNavigate` to assert the unavailable CTA navigates to `/decomposition/import`.
  - [x] Run `npm run lint` and the full `npm test` (or `npx vitest run`) from `client/` — zero regressions in existing suites (`DashboardGrid.test.tsx`, `TrendChart.test.tsx`, etc. must still pass unchanged).

### Review Findings

- [x] [Review][Patch] Selecting "Custom range" before both dates are entered leaves `DecompositionTab` showing the loading skeleton indefinitely — TanStack Query keeps `isPending: true` for a disabled query (`enabled: !!flatId && !!startDate && !!endDate`) with no cached data, and `DecompositionTab.tsx` has no branch distinguishing "genuinely fetching" from "query disabled, waiting on user input." Dev Notes assumed the `enabled` gate "already handles" this case, but that only prevents a wasted request — it doesn't prevent a stuck UI. **Decision (2026-07-13):** show a short inline prompt in place of the skeleton/cards while custom dates are incomplete. **Fixed**: added `isCustomRangeIncomplete` guard in `DecompositionTab.tsx`, new i18n key `decomposition:period.selectRange` ("Select a start and end date" / "Wähle ein Start- und Enddatum") in both locales, new regression test `DecompositionTab_CustomRangeSelectedWithoutDates_ShowsSelectRangePromptInsteadOfSkeleton`. [client/src/features/decomposition/components/DecompositionTab.tsx:21-49]
- [x] [Review][Patch] No validation that the custom-range start date is ≤ the end date — a user can pick an inverted range in `PeriodSelector.tsx` and the app silently fires `useDecomposition` with the reversed dates; no client-side guard exists and no test covers this path. **Decision (2026-07-13):** auto-swap the two values silently when the newly-entered date would invert the range, so the range sent to the query is always valid with no extra UI. **Fixed**: both date `onChange` handlers now swap start/end when the new value would invert the range; new tests `PeriodSelector_StartDateAfterEndDate_SwapsToKeepRangeValid` and `PeriodSelector_EndDateBeforeStartDate_SwapsToKeepRangeValid`. [client/src/features/decomposition/components/PeriodSelector.tsx]
- [x] [Review][Patch] Custom-range date inputs carry both a visually-hidden `<label htmlFor>` and a redundant `aria-label` with identical text — some assistive tech prioritizes `aria-label` over the associated label, making the `sr-only` label pointless. **Fixed**: removed the redundant `aria-label` from both inputs; accessible name now comes solely from the associated `<label>`. [client/src/features/decomposition/components/PeriodSelector.tsx]
- [x] [Review][Patch] Weak test assertions don't fully prove the behavior they name — `PeriodSelector_CustomRangeSelected_RevealsStartAndEndDateInputs` only checked that two inputs display `/2026-06/`-matching values without confirming which is start vs. end (a swapped-fields bug would still pass), and `DecompositionTab_NormalResponse_RendersResidualCardBeforeRoomCards` only exercised a single-room fixture via `compareDocumentPosition`, so it couldn't catch an ordering regression across multiple rooms. **Fixed**: the first now asserts each input's value via `getByLabelText(...).toHaveValue(...)`; the second (renamed `..._RendersResidualCardBeforeAllRoomCardsInOrder`) uses a 3-room fixture and checks pairwise document-position ordering across all of them. [client/src/features/decomposition/components/PeriodSelector.test.tsx, client/src/features/decomposition/components/DecompositionTab.test.tsx]
- [x] [Review][Patch] `dateInputClass` in `PeriodSelector.tsx` doesn't match `TariffForm.tsx`'s `inputClass` verbatim as the Dev Notes and Task 4 explicitly require — missing `placeholder:text-white/30`, `transition-[border-color,box-shadow]`, and `disabled:*` utility classes. **Fixed**: `dateInputClass` now matches `TariffForm.tsx`'s `inputClass` verbatim. [client/src/features/decomposition/components/PeriodSelector.tsx]
- [x] [Review][Defer] Popover trigger declares `aria-haspopup="listbox"` but never reflects open/closed state via `aria-expanded={isOpen}` [client/src/features/decomposition/components/PeriodSelector.tsx:477-484] — deferred, pre-existing: `FlatSwitcher.tsx`'s Popover-based dropdown (the convention this component was told to follow verbatim) has the same gap
- [x] [Review][Defer] Hand-rolled listbox (`role="option"` buttons) has no keyboard navigation — no arrow-key handling, `aria-activedescendant`, or roving tabindex [client/src/features/decomposition/components/PeriodSelector.tsx] — deferred, pre-existing: `FlatSwitcher.tsx` has the same gap
- [x] [Review][Defer] Static DOM ids (`decomposition-period-start`/`decomposition-period-end`) would collide if `PeriodSelector` ever rendered twice on the same page [client/src/features/decomposition/components/PeriodSelector.tsx] — deferred, not required by any AC, no current multi-instance usage

## Dev Notes

### Backend is already done — this is a pure frontend story

Story 7.1 (done, `31b8394`) built `GET /api/v1/flats/{flatId}/decomposition?startDate={yyyy-MM-dd}&endDate={yyyy-MM-dd}` returning `DecompositionResponse` exactly as typed in Task 1. No backend changes are needed or in scope for this story. Confirmed via `api/Features/Decomposition/GetDecompositionFunction.cs` and `DecompositionModels.cs` (read directly during story creation) — the response shape in Task 1 was copied field-for-field from the actual C# records, not from the epic's prose description (which is slightly stale — e.g. it doesn't mention the `PeriodRange` wrapper or that `Approach` serializes as a string post the enum-serialization fix, see `[[project_enum_json_serialization_fix]]`-equivalent memory).

### `isUnavailable` and `hasInterpolatedData` are mutually exclusive by construction

`DecompositionEngine.ComputeAsync` returns early with `hasInterpolatedData: false` whenever `isUnavailable: true` (zero `SmartPlugDailyData` rows in range short-circuits before the interpolation check even runs). AC4's banner and AC5's unavailable state can never both be true for the same response — do not add defensive code to suppress the banner during the unavailable state; it is structurally impossible for both to be true. If the frontend `DecompositionTab.tsx` implementation renders them via separate `if` branches (as Task 8 specifies), this is automatically correct with no extra guard.

### Scope boundary with Story 7.3 — do not build device-level UI

This story's own ACs (1–7) never mention `DeviceCard`, `SmartStripCard`, badges ("Measured"/"Estimated"), sub-device rows, or opacity treatments for unconfigured sub-devices — all of that is Epic Story 7.3's scope (`epic-7-consumption-decomposition.md#Story 7.3`), which is still `backlog` in `sprint-status.yaml` as of this story's creation. AC8 above resolves the only place the epic's 7.2 prose brushes up against 7.3's territory (Room cards existing at all) by scoping `RoomCard.tsx` to a name/kWh/cost-only placeholder. Do not read ahead into 7.3's epic text and pre-build `DeviceCard.tsx` — that duplicates work Story 7.3 will do with its own full context (device approach badges, EU-label/self-measured disclaimers, Smart Power Strip sub-device opacity rules) and risks producing a shape that doesn't match what 7.3 actually specifies.

### Popover-based dropdown — no new dependency

`client/src/components/ui/` has only `dialog.tsx`, `popover.tsx`, `sheet.tsx` — no `select.tsx` or `dropdown-menu.tsx`, and `package.json` has no `@radix-ui/react-select`/`@radix-ui/react-dropdown-menu`. Every existing dropdown-style control in this codebase (`FlatSwitcher.tsx`, the Settings locale dropdown) is hand-rolled on `Popover`/`PopoverTrigger`/`PopoverContent`. `PeriodSelector.tsx` must follow this exact convention (Task 4) — do not run `npx shadcn add select` or `dropdown-menu`; that would introduce a new UI pattern inconsistent with the rest of the app for no benefit.

### Custom range date inputs — reuse `TariffForm.tsx`'s exact styling, not react-hook-form

`TariffForm.tsx` (`client/src/features/tariffs/components/TariffForm.tsx:170-206`) is the only existing precedent for a `type="date"` input in this codebase. Its `inputClass` (`'w-full h-[52px] px-4 rounded-[12px] bg-white/[0.08] border text-white text-base ... colorScheme: 'dark''`) and inline `style={{ borderColor: ..., colorScheme: 'dark' }}` pattern should be reused verbatim for the two Custom-range date inputs in `PeriodSelector.tsx`. Unlike `TariffForm.tsx`, do **not** wire these through `react-hook-form`/`zod` — that pattern exists there because the tariff form is a submitted mutation with a validation-on-blur UX; the period selector is a live, un-submitted UI control whose value flows straight into a query key via plain `useState`/callback props (AD-17's form-schema rule doesn't apply to non-form UI state).

### Reused formatting/date utilities — do not create new ones

- `toLocalDateString(date: Date): string` and the local-date helpers in `client/src/lib/localDate.ts` already produce `yyyy-MM-dd` — reuse for all period boundary calculations (Task 2). Do not write a second date-formatting function.
- No shared kWh/currency formatter exists anywhere in the frontend — every card component (`DashboardGrid.tsx`, `KpiTile.tsx`) defines its own local `formatNumber`/`formatKwh`/`formatCurrency` via `Intl.NumberFormat`. `ResidualCard.tsx` and `RoomCard.tsx` should each define their own small local formatters following this exact convention, not attempt to extract a shared one (that would be a premature abstraction spanning two components, contrary to this codebase's established per-component duplication pattern for display formatting).

### Error/retry pattern — exact precedent exists twice

`ReadingHistorySheet.tsx` and `TariffList.tsx` both destructure `refetch` directly from their `useQuery`-wrapping hook and call it from a plain `onClick`, with an `isError`-gated `role="alert"` paragraph above the retry button. `useDecomposition` (Task 3) must expose `refetch` (TanStack's default `useQuery` return already includes it — no extra work needed) so `DecompositionTab.tsx` can follow this pattern identically. Do not build a custom retry-with-backoff mechanism; `retry: 1` is already the global default in `queryClient.ts`, and per-story custom retry logic is not requested by any AC.

### Import navigation target already exists

`/decomposition/import` is already a live route: `DecompositionPage.tsx` registers `<Route path="import" element={<Suspense fallback={null}><ImportRoute /></Suspense>} />` under the `/decomposition/*` wildcard mount in `router.tsx`, and the existing header upload-icon button in `DecompositionRoot` already navigates there via `navigate('/decomposition/import')`. AC5's unavailable-state CTA and Task 8 both reuse this exact route — no new route registration needed.

### Deferred, not blocking — "Set up your flat" prompt

`EXPERIENCE.md`'s state-pattern table lists a "'Set up your flat' prompt if Flat Structure is not configured" as part of the Decomposition empty state, but neither this story's own ACs, Epic 7.2's AC text, nor the backend `DecompositionResponse` shape (Story 7.1) carry a signal distinguishing "no smart plug data" from "Flat Structure never configured" — `isUnavailable` covers only the former. Implementing this UX nuance would require a new backend field, which is out of scope for a frontend-only story and not required by any AC above. Flag for a future story/backlog item if the product wants this distinction; do not attempt to infer it client-side from `rooms.length === 0` on a *non*-unavailable response (that's a different, valid state — a flat with rooms but no devices at all — not "unconfigured").

### Testing conventions

Frontend: Vitest + `@testing-library/react`, co-located `.test.tsx`/`.test.ts` files, `globals: true` (no `vitest` imports for `describe`/`it`/`expect`), mock `react-i18next`'s `useTranslation` per `project-context.md`'s established convention, wrap any component using `useNavigate`/routing hooks in `MemoryRouter`, wrap any TanStack Query consumer in a fresh `QueryClientProvider` per test. Mirror `DashboardGrid.test.tsx`/`TrendChart.test.tsx`'s existing setup exactly.

### Project Structure Notes

- New files:
  - `client/src/features/decomposition/api/decompositionApi.ts`
  - `client/src/features/decomposition/lib/periods.ts` (+ `periods.test.ts`)
  - `client/src/features/decomposition/hooks/useDecomposition.ts` (+ `.test.ts`)
  - `client/src/features/decomposition/components/PeriodSelector.tsx` (+ `.test.tsx`)
  - `client/src/features/decomposition/components/ResidualCard.tsx` (+ `.test.tsx`)
  - `client/src/features/decomposition/components/RoomCard.tsx` (minimal placeholder — extended by Story 7.3)
  - `client/src/features/decomposition/components/DecompositionUnavailable.tsx`
  - `client/src/features/decomposition/components/DecompositionTab.tsx` (+ `.test.tsx`)
- Modified files:
  - `client/src/features/decomposition/DecompositionPage.tsx` — one new `<DecompositionTab flatId={settings?.flatId} />` line in `DecompositionRoot`.
  - `client/src/locales/en-US/decomposition.json`, `client/src/locales/de-DE/decomposition.json` — new keys added alongside the existing `importButton` key.
- No backend files touched. No entity/migration/infra changes. No `deploy.sh` run.
- This story does **not** create `DeviceCard.tsx` or `SmartStripCard.tsx` — see Dev Notes scope-boundary section above.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-7-consumption-decomposition.md#Story 7.2] — authoritative AC text (verbatim, reproduced above as ACs 1–7; ACs 8–10 added during story creation to resolve genuine gaps between epic prose, the UX mockup, `EXPERIENCE.md`, and this story's boundary with Story 7.3).
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md#FR-32, FR-33, FR-34] — decomposition view, Residual always shown, unavailable state.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/mockups/decomposition-tab.html] — visual reference for all three states (data available, empty/unavailable, interpolated-data banner) — layout, glass card treatment, exact CSS token values (`residual-tint`, `accent-info`) confirmed against `DESIGN.md`.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/DESIGN.md#Colors, #Components] — `residual-tint`, `accent-info`, glass card base spec, Progress Card pattern (residual-tint reuse precedent).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/EXPERIENCE.md#Component Patterns, #State Patterns, #Interaction Primitives] — Residual card behavioral rules, period dropdown options list, Decomposition state-pattern table (used for visual/tonal reference only per AC9's resolution, not literal copy).
- [Source: _bmad-output/planning-artifacts/architecture.md:606-617, 359 (query-key pattern), 271-273 (AD-16)] — planned `decomposition/` feature-folder file layout, `['decomposition', flatId, { startDate, endDate }]` query key, TanStack-Query-only server state rule.
- [Source: _bmad-output/implementation-artifacts/7-1-decomposition-backend-engine-api-and-cost-attribution.md] — previous story; confirms `DecompositionResponse` shape, confirms `isUnavailable`/`hasInterpolatedData` mutual exclusivity via `DecompositionEngine.cs` behavior, notes Story 7.3's own open question about zero-device `PowerPoint`s (not this story's concern).
- [Source: api/Features/Decomposition/DecompositionModels.cs, DecompositionEngine.cs, GetDecompositionFunction.cs] — read directly during story creation; ground truth for Task 1's TypeScript types and the `isUnavailable`/`hasInterpolatedData` mutual-exclusivity claim in Dev Notes.
- [Source: client/src/features/decomposition/DecompositionPage.tsx] — existing routing (`/decomposition/*`, `/decomposition/import`), `DecompositionRoot`/`ImportRoute` structure this story extends.
- [Source: client/src/features/dashboard/hooks/useDashboard.ts, api/dashboardApi.ts, components/DashboardGrid.tsx, components/TrendChart.tsx] — hook/API-client shape precedent, local-formatter-per-component convention, skeleton-block styling precedent.
- [Source: client/src/components/FlatSwitcher.tsx] — Popover-based hand-rolled dropdown precedent for `PeriodSelector.tsx`.
- [Source: client/src/features/tariffs/components/TariffForm.tsx:170-206] — native `type="date"` input styling precedent for the Custom range fields.
- [Source: client/src/features/readings/components/ReadingHistorySheet.tsx, client/src/features/tariffs/components/TariffList.tsx] — `isError`/`refetch()` retry-button precedent.
- [Source: client/src/features/smart-plug-import/components/ImportProgressCard.tsx] — existing `var(--color-residual-tint)` inline-style reuse precedent for `ResidualCard.tsx`.
- [Source: client/src/lib/localDate.ts, client/src/lib/i18n.ts, client/src/lib/apiClient.ts, client/src/lib/queryClient.ts] — shared utilities this story reuses as-is.
- [Source: _bmad-output/project-context.md] — TanStack Query v5 gotchas (`isPending` convention for queries), zod/RHF form rules (scoped to submitted forms), i18n namespace registration (already done for `decomposition`), frontend naming conventions.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

None — no unexpected failures required debug-log capture. `ResidualCard.test.tsx`'s initial zero-kwh assertion (`/0/`) matched both the kWh and currency values ambiguous-selector failure; narrowed to `/0 kWh/` and it passed immediately after.

### Completion Notes List

- Backend (Story 7.1) was already complete; this story is pure frontend. All 11 tasks implemented test-first (RED confirmed via import-resolution failure before each new file, then GREEN).
- `resolvePeriodRange` (Task 2): 6 unit tests covering all 4 non-custom options plus two edge cases (Monday-as-today for "This week", January-as-today for "Last month" year rollover).
- `useDecomposition` (Task 3): mirrors `useDashboard.ts` exactly; `enabled` gate verified disabled when `flatId` or either date is undefined.
- `PeriodSelector` (Task 4): hand-rolled Popover-based listbox per `FlatSwitcher.tsx` convention — no new Radix dependency added. Custom-range date inputs reuse `TariffForm.tsx`'s exact `inputClass`/`colorScheme: dark` styling, driven by plain `useState` (no react-hook-form/zod, per Dev Notes).
- `ResidualCard` (Task 5): reuses `ImportProgressCard.tsx`'s exact `residual-tint`/amber-border inline-style object. Zero-kwh regression test confirms the card is never suppressed (AC2/AC3).
- `RoomCard` (Task 6): intentionally minimal (name/kwh/cost only) — no dedicated test file per story's test plan; covered indirectly via `DecompositionTab.test.tsx`.
- `DecompositionUnavailable` (Task 7): icon/heading/body/CTA per mockup; no dedicated test file — covered via `DecompositionTab.test.tsx`.
- `DecompositionTab` (Task 8): orchestrates period state, the four query-state branches (loading/error/unavailable/data), and the interpolated-data banner. Skeleton blocks use `bg-white/10` pulse fill on `rounded-card border-glass-border` shells (94px for Residual, 52px × 2 for Rooms) per the codebase's `ReadingHistorySheet.tsx`/`TrendChart.tsx` skeleton convention.
- Wired into `DecompositionPage.tsx` (Task 9) directly below the existing `ImportProgressCard`.
- i18n (Task 10): all keys added to both `en-US`/`de-DE` `decomposition.json` using the epic's literal AC text verbatim (per AC9); German copy follows the codebase's factual, no-exclamation-mark voice. Namespace was already registered in `i18n.ts`.
- Full verification (Task 11): `npx vitest run` → 55 files / 331 tests passed (zero regressions); `npm run lint` → clean (only pre-existing `router.tsx` fast-refresh warnings, unrelated to this story); `npx tsc --noEmit` → no errors.

### File List

**New files:**
- `client/src/features/decomposition/api/decompositionApi.ts`
- `client/src/features/decomposition/lib/periods.ts`
- `client/src/features/decomposition/lib/periods.test.ts`
- `client/src/features/decomposition/hooks/useDecomposition.ts`
- `client/src/features/decomposition/hooks/useDecomposition.test.ts`
- `client/src/features/decomposition/components/PeriodSelector.tsx`
- `client/src/features/decomposition/components/PeriodSelector.test.tsx`
- `client/src/features/decomposition/components/ResidualCard.tsx`
- `client/src/features/decomposition/components/ResidualCard.test.tsx`
- `client/src/features/decomposition/components/RoomCard.tsx`
- `client/src/features/decomposition/components/DecompositionUnavailable.tsx`
- `client/src/features/decomposition/components/DecompositionTab.tsx`
- `client/src/features/decomposition/components/DecompositionTab.test.tsx`

**Modified files:**
- `client/src/features/decomposition/DecompositionPage.tsx` — added `<DecompositionTab flatId={settings?.flatId} />` below `ImportProgressCard`.
- `client/src/locales/en-US/decomposition.json` — added `period.*`, `residual.title`, `interpolatedBanner`, `unavailable.*`, `loadError`, `retry` keys.
- `client/src/locales/de-DE/decomposition.json` — same keys, German translations.

## Change Log

- 2026-07-13: Implemented Decomposition tab period selector, Residual card, unavailable state, and minimal Room card placeholder (all 11 tasks, ACs 1–10). 331/331 frontend tests pass, lint and typecheck clean. Status moved to `review`.
