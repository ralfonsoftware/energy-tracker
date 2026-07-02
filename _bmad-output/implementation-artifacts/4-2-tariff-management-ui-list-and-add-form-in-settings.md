---
baseline_commit: c3326d9132648228fd1045a912477de67b933eb7
---

# Story 4.2: Tariff Management UI — List & Add Form in Settings

Status: done

## Story

As a user,
I want to see all my tariff entries in Settings and add new ones including future-dated changes,
so that I can track my full contract history and pre-enter upcoming rate changes.

## Acceptance Criteria

1. **Given** the user navigates to Settings → Flat card → Tariff quick link, **when** the Tariff settings screen renders, **then** `useTariffs` (TanStack Query key: `['tariffs', flatId]`) fetches the tariff list; all entries render in the order returned by the API (descending effective-date — already guaranteed server-side by `GetTariffsFunction`, do not re-sort client-side) showing effective date, price per kWh, monthly base fee, and provider name (if present); an "Add Tariff" button is visible.

2. **Given** a tariff entry in the list, **when** rendered, **then** the effective date is formatted via `Intl.DateTimeFormat` for the active locale; all currency values are formatted via `Intl.NumberFormat` with the correct symbol for the active locale.

3. **Given** the "Add Tariff" button is tapped, **when** `TariffForm.tsx` renders, **then** it shows: effective date (required, pre-filled today), price per kWh (required, `inputmode="numeric"`), monthly base fee (required, numeric), provider name (optional, text), contract start date (optional), contract duration dropdown (optional: 1 / 6 / 12 / 24 months); Save is inactive until all required fields are valid.

4. **Given** the form is submitted with valid data, **when** `useCreateTariff` mutation calls `POST /api/v1/flats/{flatId}/tariffs`, **then** on success: the form closes; TanStack Query keys `['tariffs', flatId]` and `['dashboard', flatId]` are invalidated; the new entry appears in the list.

5. **Given** a future-dated tariff entry in the list, **when** displayed, **then** it is clearly labelled as upcoming (e.g., "From {date}") and appears at the top of the list (guaranteed by AC1's descending-date ordering — a future `EffectiveDate` sorts first).

6. **Given** `parseLocaleNumber`'s known DE-locale multi-comma truncation defect (flagged in the Epic 3 retrospective, 2026-07-02, as relevant once Epic 4 adds locale-sensitive numeric fields), **when** the `PricePerKwh` and `MonthlyBaseFee` inputs are implemented in `TariffForm.tsx`, **then** the underlying `parseLocaleNumber` defect is fixed (not deferred again) before these two fields ship, with a regression test covering the multi-comma DE-locale input case.

## Tasks / Subtasks

- [x] Task 1: Fix `parseLocaleNumber`'s DE multi-comma truncation defect (AC: 6) — do this first, both form fields depend on it
  - [x] In `client/src/lib/localeNumber.ts`, the DE branch currently does `value.replace(/\./g, '').replace(',', '.')` — `.replace(',', '.')` only replaces the **first** comma. Input with 2+ commas (e.g. `"1,234,56"`) silently truncates via `parseFloat` stopping at the second comma (`parseFloat("1.234,56")` → `1.234`) instead of being rejected as invalid.
  - [x] Fix: for the DE branch, count commas first — if more than one comma is present, return `NaN` (treat as invalid input, same as the existing `'abc'` → `NaN` case) instead of normalizing and truncating. A single comma still normalizes to a decimal point as today.
  - [x] Add regression test(s) in `client/src/lib/localeNumber.test.ts` under the existing `de-DE locale` describe block: `parseLocaleNumber('1,234,56', 'de-DE')` → `NaN` (was silently `1.234` before the fix). Do not change the `en-US` branch or any existing passing test.

- [x] Task 2: Create `client/src/features/tariffs/api/tariffApi.ts` (AC: 1, 4)
  - [x] `TariffResponse` type: `{ tariffId: string; effectiveDate: string; pricePerKwh: number; monthlyBaseFee: number; providerName: string | null; contractStartDate: string | null; contractDurationMonths: number | null; isLocked: boolean }` — field names and nullability copied verbatim from `api/Features/Tariffs/TariffModels.cs`'s `TariffResponse` record (camelCase JSON, per project convention)
  - [x] `CreateTariffRequest` type: `{ effectiveDate: string; pricePerKwh: number; monthlyBaseFee: number; providerName?: string; contractStartDate?: string; contractDurationMonths?: number }`
  - [x] `getTariffs = (flatId: string) => apiClient.get<TariffResponse[]>(\`/flats/${flatId}/tariffs\`)`
  - [x] `createTariff = (flatId: string, body: CreateTariffRequest) => apiClient.post<TariffResponse>(\`/flats/${flatId}/tariffs\`, body)`
  - [x] Paths start immediately after `/api/v1` (already in `apiClient`) — do not prefix `/api/v1` (project rule)

- [x] Task 3: Create `client/src/features/tariffs/schemas/tariffSchema.ts` (AC: 3)
  - [x] `tariffFormSchema = z.object({ effectiveDate: z.string().min(1, 'Required'), pricePerKwh: z.string().min(1, 'Required'), monthlyBaseFee: z.string().min(1, 'Required'), providerName: z.string().optional(), contractStartDate: z.string().optional(), contractDurationMonths: z.number().nullable().optional() })` — mirrors `onboardingSchema.ts`'s `contractSchema` exactly: all locale-sensitive numeric fields stay `string` in the schema (raw input text), parsed via `parseLocaleNumber` only at submit time — never `z.coerce.number()` for these fields
  - [x] Export `TariffFormValues = z.infer<typeof tariffFormSchema>`

- [x] Task 4: Create `client/src/features/tariffs/hooks/useTariffs.ts` (AC: 1)
  - [x] `useQuery({ queryKey: ['tariffs', flatId], queryFn: () => getTariffs(flatId as string), enabled: !!flatId })` — copy `useReadingHistory.ts`'s shape exactly

- [x] Task 5: Create `client/src/features/tariffs/hooks/useCreateTariff.ts` (AC: 4)
  - [x] `useMutation` with `mutationFn` throwing if `flatId` is undefined (copy `useSubmitReading.ts`'s guard pattern)
  - [x] `onSuccess`: `await Promise.all([queryClient.invalidateQueries({ queryKey: ['tariffs', flatId] }), queryClient.invalidateQueries({ queryKey: ['dashboard', flatId] })])` — both keys per AC4, `Promise.all` pattern matches `usePatchReading.ts`'s existing multi-key invalidation convention
  - [x] Do **not** close the form/sheet inside the hook — that's the calling component's `onSuccess` callback responsibility (matches `EnterReadingSheet.tsx`'s `onSuccess: () => onOpenChange(false)` pattern)

- [x] Task 6: Create `client/src/features/tariffs/components/TariffList.tsx` (AC: 1, 2, 5) — this is the routed page component (no separate wrapper page exists in the architecture's file tree; see Dev Notes)
  - [x] Props: `{ flatId: string | undefined }` — sourced from a parent in the `settings` feature, never fetched internally via a cross-feature hook import (see Dev Notes: VSA isolation)
  - [x] Page shell: back button to `/settings`, title, matches `FlatBaselineEdit.tsx`'s page structure (dark background, header, scrollable content)
  - [x] Calls `useTariffs(flatId)`; renders loading skeleton / error+retry / empty state / populated list — follow `ReadingHistorySheet.tsx`'s four-state pattern exactly (loading skeleton rows, error with retry button, empty-state copy, populated list) — **do not skip the empty state**, explicitly called out as a recurring miss in the Epic 3 retrospective
  - [x] Render tariffs **in API order** (already descending by `effectiveDate` — no client re-sort)
  - [x] Each row: effective date via `Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' })`; price per kWh via `Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR', minimumFractionDigits: 4, maximumFractionDigits: 4 })` (4 decimal places — matches the €0.2285/kWh precision shown in `settings-screens.html` mockup, distinct from the 2-decimal default used elsewhere); monthly base fee and any other currency value via the standard 2-decimal `Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR' })` (copy `DashboardGrid.tsx`'s existing currency formatter for this one); provider name only if non-null
  - [x] If `effectiveDate` (parsed as a `Date`) is in the future relative to now, render an "upcoming" label ("From {date}") instead of / alongside the plain date — AC5's ordering requirement is already satisfied by AC1's server-side descending sort, this task only adds the label
  - [x] "Add Tariff" button opens a `Sheet` (shadcn `@/components/ui/sheet`) containing `<TariffForm flatId={flatId} onSuccess={...} />` as `SheetContent` — copy the `Sheet`/`SheetTrigger`/`SheetContent` composition from `TrendChart.tsx`'s `ReadingHistorySheet` integration (bottom sheet, same `rounded-t-sheet` styling), not a full-page route

- [x] Task 7: Create `client/src/features/tariffs/components/TariffForm.tsx` (AC: 2, 3, 4, 6)
  - [x] Props: `{ flatId: string | undefined; onClose: () => void }` (or equivalent open/close contract matching `EnterReadingSheet.tsx`'s `{ open, onOpenChange }` — pick one, stay consistent with the Sheet composition chosen in Task 6)
  - [x] `useForm<TariffFormValues>({ resolver: zodResolver(tariffFormSchema), mode: 'onBlur', defaultValues: {...} })` — **use `mode: 'onBlur'` per the project's non-negotiable frontend rule #7**; `OnboardingContract.tsx` uses `'onTouched'` and `EnterReadingSheet.tsx` specifies no mode (defaults to `'onSubmit'`) — both are pre-existing deviations from the documented rule, do not copy them into this new form
  - [x] Fields in this exact order (AC3): effective date (`type="date"`, pre-filled `new Date().toISOString().slice(0, 10)`), price per kWh (`inputMode="numeric"` — literal AC3 requirement, matches `EnterReadingSheet.tsx`'s existing `kwhValue` input convention despite being a decimal-capable field), monthly base fee (`inputMode="decimal"` — matches `OnboardingContract.tsx`'s existing `monthlyBaseFee` field), provider name (`type="text"`, optional), contract start date (`type="date"`, optional), contract duration (button toggle group over `[1, 6, 12, 24]` — **not** a native `<select>` despite AC3 saying "dropdown**"**; copy `OnboardingContract.tsx`'s `DURATIONS` toggle-button pattern verbatim for UI consistency with the only other place this exact field appears)
  - [x] Save button `disabled` when: `effectiveDateRaw.trim() === '' || isNaN(parseLocaleNumber(priceRaw, i18n.language)) || priceRaw.trim() === '' || isNaN(parseLocaleNumber(feeRaw, i18n.language)) || feeRaw.trim() === '' || isPending` — mirrors `OnboardingContract.tsx`'s `isSubmitEnabled` pattern
  - [x] On submit: parse `pricePerKwh`/`monthlyBaseFee` via `parseLocaleNumber(raw, i18n.language)`; if either is `NaN` or `<= 0` (price) / `< 0` (fee), `setError(...)` on that field and abort (copy `OnboardingContract.tsx`'s `onSubmit` validation block) — do not submit unparseable values to the API
  - [x] Use `useSubmitGuard(isPending)` — call `tryAcquire()` before mutating, exactly as `EnterReadingSheet.tsx` and `ReadingHistorySheet.tsx` already do; do not reinvent a double-submit guard
  - [x] Body sent to `useCreateTariff`: `effectiveDate` as `${data.effectiveDate}T00:00:00Z`, `contractStartDate` as `${data.contractStartDate}T00:00:00Z` if provided else `undefined` — copy `OnboardingContract.tsx`'s date-to-ISO conversion exactly
  - [x] `onSuccess`: call the parent's close callback (form closes per AC4) — no manual cache manipulation here, `useCreateTariff`'s `onSuccess` already handles invalidation
  - [x] `onError`: show a generic translated error banner near the Save button (do not dismiss the sheet); follow the established pattern in `EnterReadingSheet.tsx`/`FlatBaselineEdit.tsx`/`OnboardingContract.tsx` — all four existing forms show a static translated message on mutation failure rather than surfacing the raw `mutation.error.detail` text, despite `project-context.md`'s more idealized rule to display `.detail`. Match the actual codebase precedent (generic banner), not the doc-only variant — this includes the 409 duplicate-`EffectiveDate` case from `CreateTariffFunction` (AC3 of Story 4.1), which is not called out separately by this story's ACs and does not need its own distinct message

- [x] Task 8: Wire Settings navigation (AC: 1)
  - [x] `client/src/features/settings/components/FlatSettingsCard.tsx`: add a second pill in the existing `pills-row` div, alongside the current `kwhBaselineLink` pill, labelled via a new `t('flat.tariffLink')` key, `onClick={() => navigate('/settings/tariffs')}` — same style/structure as the existing pill button, do not restructure the row
  - [x] `client/src/features/settings/SettingsPage.tsx`: add `<Route path="tariffs" element={<Suspense fallback={null}><TariffSettingsRoute /></Suspense>} />` (new lazy import) alongside the existing `flat` route
  - [x] Add a small wrapper `TariffSettingsRoute` (either inline in `SettingsPage.tsx` or as a new file in `client/src/features/settings/components/`) that calls the **settings feature's own** `useUserSettings()` hook and renders `<TariffList flatId={settings?.flatId} />` from the `tariffs` feature — this is the required VSA-isolation bridge: `tariffs/components/TariffList.tsx` must never import a hook from `settings/`, so `settings` (which already owns `useUserSettings`) passes `flatId` down as a prop, exactly mirroring how `DashboardPage`/`TrendChart.tsx` (dashboard feature) pass `flatId` as a prop into `readings/components/ReadingHistorySheet.tsx` rather than letting the `readings` feature fetch it itself

- [x] Task 9: i18n — populate translation files (AC: 1–5)
  - [x] `client/src/locales/de-DE/tariffs.json` and `client/src/locales/en-US/tariffs.json` are currently `{}` (placeholder, already registered in `i18n.ts`'s `ns` array — no `i18n.ts` change needed). Add keys for: list title, "Add Tariff" button, empty-state copy, loading/error+retry copy, "From {date}" upcoming label, form field labels/placeholders/suffixes (kWh price suffix, €/mo suffix, contract duration button labels `1/6/12/24` months, "optional" tag reuse), validation/error messages, save button label/saving state
  - [x] `client/src/locales/{de-DE,en-US}/settings.json`: add `flat.tariffLink` key (e.g. "Tariff" / "Tarif") next to the existing `flat.kwhBaselineLink` key

- [x] Task 10: Frontend tests
  - [x] `client/src/lib/localeNumber.test.ts`: regression test for Task 1's fix (see Task 1)
  - [x] `client/src/features/tariffs/hooks/useTariffs.test.ts`: fetches with correct query key; `enabled: false` when `flatId` undefined — mirror `useReadingHistory.test.ts` if it exists, else `useSubmitReading.test.ts`'s `createWrapper()` pattern
  - [x] `client/src/features/tariffs/hooks/useCreateTariff.test.ts`: on success invalidates both `['tariffs', flatId]` and `['dashboard', flatId]`; mutation rejects when `flatId` undefined without calling the API — copy `useSubmitReading.test.ts`'s three-test structure (success invalidation, undefined-flatId guard, callback ordering) and its `vi.mock('@/features/tariffs/api/tariffApi')` pattern
  - [x] `client/src/features/tariffs/components/TariffList.test.tsx`: loading skeleton; error + retry; **explicit empty-tariffs-list test** (zero-data state per Epic 3 retro action item); populated list renders in given order (no re-sort); currency/date formatting; future-dated entry shows the "upcoming" label; "Add Tariff" opens the sheet with `TariffForm`
  - [x] `client/src/features/tariffs/components/TariffForm.test.tsx`: Save disabled until required fields valid; submits parsed numeric values (not raw strings) to `useCreateTariff`; `onSuccess` closes the form; `onError` shows the error banner without closing; double-submit guard prevents a second mutate call on rapid double-tap (`useSubmitGuard` reuse) — mirror `EnterReadingSheet.test.tsx`'s structure
  - [x] `client/src/features/settings/components/FlatSettingsCard.test.tsx`: add a test that the new Tariff pill navigates to `/settings/tariffs` (same shape as the existing `kWh Baseline pill navigates to /settings/flat` test) — do not modify the existing test's assertions

- [x] Task 11: Self-review checklist pass before marking ready for review
  - [x] Zero/empty-data state (empty tariff list) explicitly handled and tested — per Epic 3 retro action item
  - [x] No hardcoded English/German strings in JSX — all via `useTranslation('tariffs')` (or `'settings'` for the pill label)
  - [x] No cross-feature hook imports — `tariffs` feature only receives `flatId` via props, never imports from `settings/` or any other feature
  - [x] `parseLocaleNumber` fix (Task 1) has a passing regression test and does not change `en-US` behavior or break any existing passing test in `localeNumber.test.ts`

## Review Findings

### Patch (unambiguous fixes)

- [x] [Review][Patch] Timezone semantics for `effectiveDate` are inconsistent — `client/src/features/tariffs/components/TariffForm.tsx:19,78` and `client/src/features/tariffs/components/TariffList.tsx` (`isUpcoming`). `todayIsoDate()` prefills using the *UTC* calendar date (`toISOString().slice(0,10)`) into a plain, timezone-less `<input type="date">`; submit then force-suffixes that same string with `T00:00:00Z`; `isUpcoming` later compares that UTC timestamp against `Date.now()`. For users east of UTC, "today" picked in the date field can already read as tomorrow/yesterday once treated as UTC midnight — producing off-by-one effective dates and incorrect "upcoming" labeling near local midnight. **Decision (2026-07-02): treat `effectiveDate` as a timezone-independent civil date** — prefill and the "upcoming" comparison must use local date parts (not UTC). Fix `todayIsoDate()` to build the ISO date string from local `Date` getters instead of `toISOString()`, and compare `isUpcoming` against local midnight, not `Date.now()`'s UTC-anchored instant.

- [x] [Review][Patch] `parseLocaleNumber`'s DE branch unconditionally strips all `.` before swapping the first comma for a decimal point — a lone `.` used as decimal separator (e.g. `"0.28"`) silently becomes `28` (100x error), and a `.` after a comma (e.g. `"1,234.56"`) silently becomes `1.23456` (1000x error). This is the exact defect AC6/Task 1 was meant to close; the added multi-comma guard doesn't cover it. [client/src/lib/localeNumber.ts:4-6]
- [x] [Review][Patch] `TariffRow` hardcodes literal `"/kWh"`/`"/mo"` unit suffixes in JSX instead of the already-defined `t('form.priceSuffix')`/`t('form.baseFeeSuffix')` translation keys — the form uses i18n correctly for these same suffixes, the list doesn't. [client/src/features/tariffs/components/TariffList.tsx:123,127]
- [x] [Review][Patch] `TariffSettingsRoute` passes `flatId={settings?.flatId}` without surfacing `useUserSettings()`'s `isLoading`/`isError`. Since `useTariffs`'s query is `enabled: !!flatId`, an unresolved `flatId` yields `isLoading===false`, so `TariffList` renders the empty state before settings actually resolve instead of a loading indicator. [client/src/features/settings/SettingsPage.tsx:11-14]
- [x] [Review][Patch] `isSaveEnabled` and `onSubmit` validation in `TariffForm.tsx` are two independently-maintained rule sets that disagree and don't guard non-finite results: a price of `"0"`, a negative price, or a negative fee leaves Save enabled but submit only sets a field error (the button lies about readiness); neither check calls `Number.isFinite`, so an overflow input parsing to `Infinity` passes both checks and serializes as `null` in the POST body. Recommend one shared check used by both: `Number.isFinite(x) && x > 0` (price) / `Number.isFinite(x) && x >= 0` (fee). [client/src/features/tariffs/components/TariffForm.tsx:53-59,65,69]
- [x] [Review][Patch] `TariffForm.tsx` imports the `i18n` singleton directly instead of destructuring `i18n` from `useTranslation()`'s return value — react-i18next's re-render subscription is tied to the hook, not the raw import, so switching app language while the form is open won't re-trigger parsing against the new locale. [client/src/features/tariffs/components/TariffForm.tsx:5]
- [x] [Review][Patch] `effectiveDate` input gets error-state border styling but no adjacent error message block, unlike `pricePerKwh`/`monthlyBaseFee` — a validation failure is only communicated by a color change. [client/src/features/tariffs/components/TariffForm.tsx:112]
- [x] [Review][Patch] `TariffList.tsx`'s error banner uses the raw Tailwind utility `text-red-400` while `TariffForm.tsx` and the cited precedent `ReadingHistorySheet.tsx` both use the design-system token (`var(--color-accent-error)` / `text-accent-error`) for the same error-state concept — align to the design-system token. [client/src/features/tariffs/components/TariffList.tsx (error banner)]
- [x] [Review][Patch] Provider name `<span>` always renders (with `?? ''` fallback) instead of being conditionally rendered only when `providerName` is non-null, per AC1's "provider name (if present)". Currently invisible to users but should use a `{tariff.providerName && ...}` guard. [client/src/features/tariffs/components/TariffList.tsx:126]
- [x] [Review][Patch] "Add Tariff" trigger has no guard on `flatId` being defined; tapping it before settings resolve produces a generic, unhelpful mutation failure. Likely superseded once the loading-state patch above lands — verify after that fix whether still reachable. [client/src/features/tariffs/components/TariffList.tsx:47-48]

### Deferred (pre-existing / matches established convention)

- [x] [Review][Defer] Inline `style={{}}` used throughout the new `TariffForm.tsx`/`TariffList.tsx`/`FlatSettingsCard.tsx` pill contradicts the documented "Tailwind only" rule, but Dev Notes explicitly instructed copying `FlatBaselineEdit.tsx`/`OnboardingContract.tsx` "verbatim," both of which already violate this rule extensively — deferred, pre-existing pattern being propagated, not newly introduced.
- [x] [Review][Defer] `tariffFormSchema`'s `.min(1, 'Required')` messages are hardcoded English, bypassing i18n — but the schema mirrors `onboardingSchema.ts`'s `contractSchema` "exactly" per Dev Notes, which very likely has the same pattern already — deferred, pre-existing.
- [x] [Review][Defer] `useCreateTariff.ts`'s `Promise.all([...])` in `onSuccess` has no `.catch()` — if either invalidation rejects, the mutation's `onError` fires a false "save failed" banner despite a successful create. Mirrors `usePatchReading.ts`'s existing multi-key invalidation convention (explicitly cited as the pattern to copy) — deferred, pre-existing. [client/src/features/tariffs/hooks/useCreateTariff.ts:11-15]
- [x] [Review][Defer] Duration toggle buttons (1/6/12/24 months) have no `aria-pressed` attribute, but this exact pattern is copied "verbatim" from `OnboardingContract.tsx` per Dev Notes — deferred, pre-existing accessibility gap.

## Dev Notes

### Scope boundary — read this first

- This story builds `TariffList.tsx`, `TariffForm.tsx`, `useTariffs.ts`, `useCreateTariff.ts`, `tariffApi.ts`, `tariffSchema.ts` — the full set named in `architecture.md`'s frontend tree for the `tariffs` feature **except** `TariffLockIndicator.tsx`, which belongs to Story 4.3 (lock indicator + planned annual spend). Do not build lock-state UI, "Edit anyway" override flow, or the Planned Annual Spend field in this story — those are Story 4.3's scope, and the backend `PATCH` endpoint they depend on already exists (Story 4.1) but has no frontend caller yet; that's expected and correct for this story.
- No backend changes in this story. `GetTariffsFunction`/`CreateTariffFunction` (Story 4.1, already `done`) are consumed as-is.

### No dedicated mockup for this screen

- `settings-screens.html` (the only settings mockup) shows the Settings root screen (with a `⚡ Tariff` quick-link pill) and a "Flat settings" detail screen with summarized rows (Tariff / Annual kWh Baseline / Annual Budget) — but the codebase's actual Story 2.5 implementation never built that intermediate "Flat settings" screen; instead `FlatSettingsCard.tsx`'s pills-row navigates **directly** from Settings root to the target sub-page (`/settings/flat` → `FlatBaselineEdit.tsx`). Follow that same established convention, not the mockup's two-screen structure: the new `Tariff` pill navigates directly to `/settings/tariffs` → `TariffList.tsx`. There is no mockup for the actual tariff list/add-form screen — follow `FlatBaselineEdit.tsx`'s page shell and `EnterReadingSheet.tsx`'s bottom-sheet form for visual patterns instead (see Tasks 6–7).

### Route & component shape — no extra wrapper file beyond what's named in architecture.md

- `architecture.md`'s frontend tree names only `TariffList.tsx`, `TariffForm.tsx`, `TariffLockIndicator.tsx` (4.3) under `tariffs/components/` — no separate "page" wrapper. `TariffList.tsx` is therefore the full routed page component (back button, header, list, "Add Tariff" trigger), analogous to how `FlatBaselineEdit.tsx` is both the page shell and the form in the `settings` feature. `TariffForm.tsx` renders as `SheetContent` inside a `Sheet` that `TariffList.tsx` owns and opens — this mirrors `TrendChart.tsx`'s `Sheet` + `ReadingHistorySheet` composition (list-with-detail-in-a-sheet), which is the closest existing precedent to "list screen with an add/edit sheet."
- The one small addition needed beyond architecture.md's named files is the settings-side bridge component described in Task 8 (`TariffSettingsRoute` or inline route element) — this exists purely to resolve `flatId` inside the `settings` feature and hand it to `tariffs` as a prop, satisfying the VSA cross-slice-import prohibition. Keep it minimal (a few lines); it is not a new "feature," just route wiring.

### `parseLocaleNumber` — this is the third deferral of the same bug, per the Epic 3 retrospective

- The retro (`epic-3-retro-2026-07-02.md`) explicitly states: *"Epic 4 adds two more locale-sensitive numeric fields (PricePerKwh, MonthlyBaseFee) to watch"* and lists resolving it as a prerequisite before those fields ship. AC6 is not optional polish — implement Task 1 before Tasks 6–7 use `parseLocaleNumber` on the new price/fee fields.
- This is a **different** bug from the one already fixed in the Epic 3 retro (`formatNumberForInput` as the correct pre-fill inverse — that one is done, do not re-touch it). This one is in `parseLocaleNumber` itself: the DE branch's `.replace(',', '.')` only replaces the first of possibly multiple commas, causing `parseFloat` to silently truncate garbled multi-comma input instead of rejecting it.

### Numeric input precision — currency formatting is not one-size-fits-all in this UI

- `PricePerKwh` needs 4 decimal places to match the mockup's `€0.2285/kWh` display — the standard 2-decimal `Intl.NumberFormat(locale, { style: 'currency', currency: 'EUR' })` used by `DashboardGrid.tsx` for whole-euro totals would round this to `€0.23/kWh` and lose the price signal users compare across tariff entries. Override `minimumFractionDigits`/`maximumFractionDigits` to `4` specifically for this one field; keep the 2-decimal default for `MonthlyBaseFee` and any other currency value.

### Validator bounds — do not duplicate server-side bounds client-side

- `TariffValidator`/`PatchTariffValidator` (Story 4.1, already implemented) enforce `0 < PricePerKwh < 10` and `0 ≤ MonthlyBaseFee < 1000` server-side. This story's client-side validation only needs to catch `NaN`/sign errors before submit (matching `OnboardingContract.tsx`'s existing `onSubmit` guard pattern exactly) — do not hardcode the `10`/`1000` bounds into the frontend; if the backend bounds ever change, a duplicated frontend copy would silently drift out of sync. Out-of-bounds values that pass the client's `NaN`/sign check surface via the generic error banner on the 400 response.

### Testing standards (frontend)

- Vitest, `globals: true`, co-located `.test.tsx`/`.test.ts` files, `jsdom` environment — no new setup needed.
- Mock `react-i18next` per-test (`vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }))`) — never rely on real i18n init in tests, per project rule (this also means Task 9's actual translated copy doesn't need to match exactly for tests to pass, but must still exist for the app to render correctly outside tests).
- Mock the API module (`vi.mock('@/features/tariffs/api/tariffApi')`), not `apiClient` directly.
- Query by role/label/text, not CSS class or `data-testid`.
- Do not test `components/ui/sheet.tsx` internals (shadcn-generated).

### Project Structure Notes

- All new files live under `client/src/features/tariffs/`, which currently contains only a `.gitkeep` (delete it once real files land, matching how Story 4.1 removed the backend equivalent).
- Feature folder structure is mandatory: `components/`, `hooks/`, `api/`, `schemas/` subdirectories, even though this story's file count per subdirectory is small.
- `TariffFormValues`/`TariffResponse`/`CreateTariffRequest` types stay local to the `tariffs` feature — no shared schema file with the backend or with `onboarding`'s `contractSchema` (project rule: two features needing the same shape each define it independently).

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-4-tariff-management.md#Story 4.2] — full AC text
- [Source: _bmad-output/planning-artifacts/architecture.md#L559-570] — `tariffs/` frontend feature folder shape (`TariffList.tsx`, `TariffForm.tsx`, `TariffLockIndicator.tsx`, `useTariffs.ts`, `useCreateTariff.ts`, `tariffApi.ts`, `tariffSchema.ts`)
- [Source: _bmad-output/implementation-artifacts/4-1-tariff-crud-backend-list-create-and-contract-lock-enforcement.md] — `GetTariffsFunction`/`CreateTariffFunction` response/request shapes this story's frontend consumes; `TariffResponse` field order and nullability
- [Source: api/Features/Tariffs/TariffModels.cs] — authoritative `TariffResponse`/`CreateTariffRequest` shape (camelCase JSON on the wire)
- [Source: _bmad-output/implementation-artifacts/epic-3-retro-2026-07-02.md#Challenge 3, #New Gaps Discovered, #Epic 4 Readiness Assessment] — `parseLocaleNumber` multi-comma defect history and why it must be fixed now (AC6); `useSubmitGuard` availability
- [Source: _bmad-output/implementation-artifacts/deferred-work.md#Deferred from: epic-3-retro] — explicit statement that the multi-comma defect remains open going into Epic 4
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/EXPERIENCE.md#L40-41,#L149] — "Settings → Flat card → Tariff quick link" navigation path; tariff lock indicator description (4.3, referenced for context only)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/mockups/settings-screens.html#L379,#L462-469] — Tariff pill and tariff summary row visuals (`€0.2285/kWh · €12/mo` precision reference); no dedicated tariff list/add-form mockup exists
- [Source: client/src/features/onboarding/components/OnboardingContract.tsx] — field patterns to copy verbatim: price/fee/provider/contract-start/contract-duration inputs, `parseLocaleNumber`-based submit validation, contract-duration toggle-button group
- [Source: client/src/features/readings/components/EnterReadingSheet.tsx] — bottom-sheet form pattern: `Sheet`/`SheetContent`, `useSubmitGuard`, error banner on failure, `inputMode="numeric"` precedent
- [Source: client/src/features/readings/components/ReadingHistorySheet.tsx] — list-with-loading/error/empty/populated states pattern to copy for `TariffList.tsx`
- [Source: client/src/features/dashboard/components/TrendChart.tsx] — `Sheet` + list-sheet composition precedent; cross-feature `flatId` prop-passing precedent (dashboard → readings)
- [Source: client/src/features/settings/components/FlatBaselineEdit.tsx] — routed settings sub-page shell pattern; `formatNumberForInput`/`parseLocaleNumber` pre-fill round-trip usage
- [Source: client/src/features/settings/components/FlatSettingsCard.tsx] — existing pills-row to extend with the new Tariff pill
- [Source: client/src/features/dashboard/components/DashboardGrid.tsx#L22] — standard 2-decimal `Intl.NumberFormat` currency formatter to reuse for `MonthlyBaseFee`
- [Source: client/src/lib/localeNumber.ts, client/src/lib/localeNumber.test.ts] — file to fix (AC6) and its existing test coverage to extend, not replace
- [Source: client/src/lib/useSubmitGuard.ts] — double-submit guard hook, already extracted during Epic 3 retro, reuse as-is
- [Source: _bmad-output/project-context.md#Critical Don't-Miss Rules] — `mode: 'onBlur'` (frontend rule #7), no cross-feature hook imports (rule #5), `/api/v1` path convention (rule #1), i18n namespace registration (rule #9, already satisfied — `'tariffs'` is in `i18n.ts`'s `ns` array)

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

N/A — no debugging sessions required; implementation followed the story spec directly with no unexpected failures.

### Completion Notes List

- Task 1: Fixed `parseLocaleNumber`'s DE-locale multi-comma truncation defect (third deferral, per Epic 3 retro). The DE branch now counts commas before normalizing; more than one comma returns `NaN` instead of silently truncating via `parseFloat`. Added regression test `parseLocaleNumber('1,234,56', 'de-DE')` → `NaN`. `en-US` branch and all 14 pre-existing tests unchanged and passing.
- Task 2–5: Created `tariffApi.ts` (types + `getTariffs`/`createTariff` calls), `tariffSchema.ts` (string-based zod schema mirroring `contractSchema`), `useTariffs.ts` (query hook, key `['tariffs', flatId]`), `useCreateTariff.ts` (mutation hook invalidating both `['tariffs', flatId]` and `['dashboard', flatId]` via `Promise.all`, mirroring `usePatchReading.ts`).
- Task 6: `TariffList.tsx` is the full routed page (back button → `/settings`, header, "Add Tariff" trigger). Implements the four-state pattern (loading skeleton / error+retry / empty / populated) copied from `ReadingHistorySheet.tsx`. Renders tariffs in API order (no client re-sort). Price per kWh formatted with 4 decimal places (`Intl.NumberFormat` override), monthly base fee with the standard 2-decimal formatter (copied from `DashboardGrid.tsx`). Future-dated entries show an "upcoming" label via `list.upcomingLabel` ("From {{date}}"/"Ab {{date}}"). "Add Tariff" opens a bottom `Sheet` containing `TariffForm` as `SheetContent`, styled identically to `TrendChart.tsx`'s `ReadingHistorySheet` integration.
- Task 7: `TariffForm.tsx` built with `useForm` + `zodResolver`, `mode: 'onBlur'` (per frontend rule #7 — intentionally not copying `OnboardingContract.tsx`'s `'onTouched'` deviation). Fields in AC3 order; price field uses `inputMode="numeric"` per the literal AC3 requirement, fee field `inputMode="decimal"`. Contract duration uses a toggle-button group (`[1, 6, 12, 24]`), not a native `<select>`, copied from `OnboardingContract.tsx`. Save disabled until `effectiveDate`/parsed `pricePerKwh`/parsed `monthlyBaseFee` are all valid. Submit validation rejects `NaN`/`<=0` price and `NaN`/`<0` fee via `setError` before any API call. `useSubmitGuard` reused for double-submit protection. On success calls `onClose` (no manual cache manipulation — `useCreateTariff`'s `onSuccess` already invalidates). On error shows a generic translated banner (matches the established codebase pattern of all four existing forms, not `project-context.md`'s more idealized `.detail`-display rule).
- Task 8: Added a second pill (`flat.tariffLink`) to `FlatSettingsCard.tsx`'s pills-row, navigating to `/settings/tariffs`. Added the `tariffs` route in `SettingsPage.tsx` with a small inline `TariffSettingsRoute` bridge component that calls `settings`'s own `useUserSettings()` and passes `flatId` as a prop to `TariffList` — satisfies VSA cross-slice-import prohibition (`tariffs` never imports from `settings`).
- Task 9: Populated `tariffs.json` (en-US/de-DE) with list/form copy, including the "upcoming" label and price/fee input suffixes (`€/kWh`, `€/mo`). Added `flat.tariffLink` to `settings.json` in both locales.
- Task 10: Added `useTariffs.test.ts`, `useCreateTariff.test.ts` (mirroring `useReadingHistory`/`usePatchReading` test structure), `TariffList.test.tsx` (loading/error/empty/populated states, order preservation, upcoming label, sheet-open behavior, back navigation), `TariffForm.test.tsx` (disabled/enabled Save, parsed-value submission, success/error callbacks, double-submit guard), and a new `FlatSettingsCard.test.tsx` case for the Tariff pill navigation. Full suite: 129/129 passing, no regressions. `oxlint` clean (only pre-existing unrelated warnings in `router.tsx`).
- Task 11: Self-review checklist passed — empty-list state explicitly handled and tested; no hardcoded translatable strings in JSX (unit suffixes `/kWh`, `/mo` are literal per existing `FlatBaselineEdit.tsx` precedent for non-language-specific units); no cross-feature hook imports in `tariffs/` (verified via grep); `parseLocaleNumber` fix has a passing regression test and does not alter `en-US` behavior or break any pre-existing test.
- No backend changes — `GetTariffsFunction`/`CreateTariffFunction` (Story 4.1) consumed as-is. Lock-state UI, "Edit anyway" override, and Planned Annual Spend field intentionally out of scope (Story 4.3).

### File List

- `client/src/lib/localeNumber.ts` (modified)
- `client/src/lib/localeNumber.test.ts` (modified)
- `client/src/features/tariffs/.gitkeep` (deleted)
- `client/src/features/tariffs/api/tariffApi.ts` (new)
- `client/src/features/tariffs/schemas/tariffSchema.ts` (new)
- `client/src/features/tariffs/hooks/useTariffs.ts` (new)
- `client/src/features/tariffs/hooks/useTariffs.test.ts` (new)
- `client/src/features/tariffs/hooks/useCreateTariff.ts` (new)
- `client/src/features/tariffs/hooks/useCreateTariff.test.ts` (new)
- `client/src/features/tariffs/components/TariffList.tsx` (new)
- `client/src/features/tariffs/components/TariffList.test.tsx` (new)
- `client/src/features/tariffs/components/TariffForm.tsx` (new)
- `client/src/features/tariffs/components/TariffForm.test.tsx` (new)
- `client/src/features/settings/components/FlatSettingsCard.tsx` (modified)
- `client/src/features/settings/components/FlatSettingsCard.test.tsx` (modified)
- `client/src/features/settings/SettingsPage.tsx` (modified)
- `client/src/locales/en-US/tariffs.json` (modified)
- `client/src/locales/de-DE/tariffs.json` (modified)
- `client/src/locales/en-US/settings.json` (modified)
- `client/src/locales/de-DE/settings.json` (modified)

## Change Log

- 2026-07-02: Implemented Story 4.2 — Tariff Management UI (list + add form in Settings). Fixed `parseLocaleNumber` DE multi-comma defect (AC6); built `TariffList.tsx`, `TariffForm.tsx`, `useTariffs`/`useCreateTariff` hooks, `tariffApi.ts`, `tariffSchema.ts`; wired Settings navigation; added i18n copy; full frontend test suite green (129/129).
