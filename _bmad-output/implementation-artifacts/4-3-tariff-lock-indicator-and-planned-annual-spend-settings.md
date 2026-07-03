---
baseline_commit: b0a20abe91d48b79926ab00bef4b7a7c1908eeaa
---

# Story 4.3: Tariff Lock Indicator & Planned Annual Spend Settings

Status: done

## Story

As a user,
I want price fields on active contracts to be visibly locked and uneditable, and I want to update my planned annual spend near the tariff configuration,
so that locked rates cannot be accidentally modified and my budget target is easy to find.

## Acceptance Criteria

1. **Given** a tariff entry with an active contract period, **when** its edit form opens, **then** `PricePerKwh` and `MonthlyBaseFee` render as read-only and visually greyed out, each with an inline lock icon (`accent-tariff-locked` #d97706) and the label "Locked — contract active until {month year}"; non-price fields remain fully editable; the lock state is immediately visible on form open — no dialog or tap-to-reveal.

2. **Given** the locked price fields, **when** the user taps the lock icon or an adjacent "Edit anyway" affordance, **then** a confirmation dialog explains that overriding will change the contract's locked rate and asks the user to confirm; on confirm, the price fields become editable and the subsequent `PATCH` includes `LockOverride: true`; on cancel, the fields remain locked and no request is sent.

3. **Given** a PATCH request with modified price fields on a locked tariff submitted directly to the API without `LockOverride: true`, **when** the backend receives it, **then** HTTP 422 Problem Details with `type: "tariff-locked"` is returned — lock enforced server-side regardless of UI state; only an explicit `LockOverride: true` bypasses it.

4. **Given** the Tariff settings screen, **when** rendered below the tariff list, **then** a "Planned Annual Spend" field shows the current value (`Flats.PlannedAnnualSpend` decimal); an edit control allows the user to update it; saving calls `PATCH /api/v1/flats/{flatId}` (existing `PatchFlatFunction`, which already accepts `plannedAnnualSpend`) and takes effect immediately on future budget pressure alert evaluations (FR-7, FR-37).

5. **Given** `PatchFlatValidator`, **when** `PlannedAnnualSpend` is provided, **then** it must be `> 0` and `< 50000` (€/year); values outside this range return HTTP 400 Problem Details and the value is not saved — closing the gap where this field previously had no bound, per the numeric-bound convention established in the Epic 3 retrospective (2026-07-02).

6. **Given** the Annual kWh Baseline or tariff price per kWh values, **when** shown alongside the Planned Annual Spend field, **then** helper text displays the auto-derived value: `({AnnualKwhBaseline} kWh × {PricePerKwh} €/kWh) + ({MonthlyBaseFee} × 12)` so the user can compare it against their manually set target.

7. **Given** all monetary values in the tariff forms, **when** displayed, **then** they are formatted via `Intl.NumberFormat` for the active locale; stored values in the database remain locale-neutral fixed-decimal.

## Tasks / Subtasks

### Backend

- [x] Task 1: Add `PlannedAnnualSpend` bounds to `PatchFlatValidator` (AC: 5)
  - [x] `api/Features/Flats/PatchFlatValidator.cs` currently has **no rule at all** for `PlannedAnnualSpend` — add: `RuleFor(r => r.PlannedAnnualSpend).GreaterThan(0m).LessThan(50000m).WithMessage("plannedAnnualSpend must be greater than 0 and less than 50000.").When(r => r.PlannedAnnualSpendProvided && r.PlannedAnnualSpend is not null);`
  - [x] The `.When()` guard is critical: `PlannedAnnualSpendProvided && PlannedAnnualSpend is not null` — NOT just `PlannedAnnualSpend is not null`. `PatchFlatFunction.cs` already supports explicit `null` to **clear** the planned-spend override (see `PlannedAnnualSpendProvided` flag pattern, `api/Features/Flats/PatchFlatFunction.cs:44-54`); an explicit-null clear request must stay valid and bypass the bound check. Only bound-check when a real value is provided.
  - [x] No `PatchFlatFunction.cs` changes needed — it already calls `validator.ValidateAsync` and returns HTTP 400 Problem Details on failure via the existing generic path (`PatchFlatFunction.cs:57-62`).

- [x] Task 2: Backend tests for Task 1 (AC: 5)
  - [x] `api.Tests/Features/Flats/PatchFlatValidatorTests.cs`: add tests mirroring the existing `AnnualKwhBaseline` bound tests exactly (`Validate_AnnualKwhBaselineAtOrAboveUpperBound_Fails` / `..JustUnderUpperBound_Succeeds` / `..Null_Succeeds`) — add `Validate_PlannedAnnualSpendAtOrAboveUpperBound_Fails` (50000m), `Validate_PlannedAnnualSpendAtOrBelowLowerBound_Fails` (0m), `Validate_PlannedAnnualSpendJustUnderUpperBound_Succeeds` (49999m), `Validate_PlannedAnnualSpendNotProvided_Succeeds` (`PlannedAnnualSpendProvided: false, PlannedAnnualSpend: null`), and `Validate_PlannedAnnualSpendExplicitNullClear_Succeeds` (`PlannedAnnualSpendProvided: true, PlannedAnnualSpend: null`) — this last one is the regression test that proves the clear-override path still works after adding the bound.
  - [x] No changes needed to `PatchTariffFunctionTests.cs` / `PatchTariffValidatorTests.cs` — the lock-enforcement backend (AC3) was fully implemented and tested in Story 4.1; this story only adds a frontend caller for the existing endpoint.

### Frontend — shared primitive

- [x] Task 3: Create `client/src/components/ui/dialog.tsx` (AC: 2)
  - [x] **This file does not exist yet.** `@radix-ui/react-dialog` is already a dependency (`client/package.json`) — it's what `client/src/components/ui/sheet.tsx` is built on (`sheet.tsx` imports `* as SheetPrimitive from "@radix-ui/react-dialog"`). Generate the standard shadcn `Dialog` component using the same primitive but with a **centered modal** content style (not `sheet.tsx`'s slide-in-from-edge variant) — `Dialog`, `DialogTrigger`, `DialogPortal`, `DialogOverlay`, `DialogContent`, `DialogHeader`, `DialogFooter`, `DialogTitle`, `DialogDescription`, `DialogClose`. Follow `sheet.tsx`'s structural pattern (`React.forwardRef`, `cn()` utility, `data-[state=open]`/`data-[state=closed]` animation classes) for consistency, but center the content (`left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2`) instead of anchoring to an edge.
  - [x] This is a generated `components/ui/` file per project convention — do not hand-tune it beyond what's needed for the confirmation dialog to render correctly; build feature-specific styling (dark glass theme, copy) in the tariffs feature, not in this primitive.

### Frontend — API & hooks

- [x] Task 4: Extend `client/src/features/tariffs/api/tariffApi.ts` (AC: 1, 2, 3)
  - [x] Add `PatchTariffRequest` type: `{ pricePerKwh?: number; monthlyBaseFee?: number; providerName?: string; contractStartDate?: string; contractDurationMonths?: number; lockOverride?: boolean }` — field names/shape copied from `api/Features/Tariffs/TariffModels.cs`'s `PatchTariffRequest` record (camelCase JSON). **Deliberately no `effectiveDate` field** — `PatchTariffRequest` on the backend has no `EffectiveDate` property; it is immutable via PATCH (the unique index `IX_Tariffs_FlatId_EffectiveDate` treats it as a natural key). Do not add it to the frontend type or the edit form.
  - [x] Add `patchTariff = (flatId: string, tariffId: string, body: PatchTariffRequest) => apiClient.patch<TariffResponse>(\`/flats/${flatId}/tariffs/${tariffId}\`, body)`

- [x] Task 5: Create `client/src/features/tariffs/hooks/usePatchTariff.ts` (AC: 1, 2, 3)
  - [x] `useMutation` with `mutationFn: ({ tariffId, body }: { tariffId: string; body: PatchTariffRequest }) => { if (!flatId) throw new Error('flatId is required'); return patchTariff(flatId, tariffId, body) }` — copy `useCreateTariff.ts`'s `flatId`-guard pattern exactly (hook takes `flatId` as its own argument, mirroring `useCreateTariff(flatId)`)
  - [x] `onSuccess`: invalidate both `['tariffs', flatId]` and `['dashboard', flatId]` via `Promise.all` — copy `useCreateTariff.ts`'s invalidation set verbatim (a price change on any tariff can change past-tariff-to-current cost figures shown on the dashboard, same reasoning as create)
  - [x] Do not close the form/sheet inside the hook — calling component's responsibility (matches `useCreateTariff.ts`)

### Frontend — lock indicator + edit form

- [x] Task 6: Create `client/src/features/tariffs/components/TariffLockIndicator.tsx` (AC: 1)
  - [x] Props: `{ contractStartDate: string; contractDurationMonths: number }` (only rendered when `tariff.isLocked === true`, so both are guaranteed non-null by the caller)
  - [x] Compute the "locked until" date as `contractStartDate` + `contractDurationMonths` calendar months, using **local date parts, not UTC** — this repeats the exact timezone bug class Story 4.2's review fixed for `effectiveDate`/`isUpcoming` (`todayIsoDate()` in `TariffForm.tsx`, `isUpcoming()` in `TariffList.tsx` — both were patched to use local `Date` getters instead of `toISOString()` because UTC-anchored date math produces off-by-one results for users east of UTC near local midnight). Apply the same fix pattern here: parse `contractStartDate` into a local `Date`, add `contractDurationMonths` via `setMonth(getMonth() + n)`, then format with `Intl.NumberFormat`/`Intl.DateTimeFormat` — do not use `toISOString()`/`getUTCMonth()` anywhere in this calculation.
  - [x] Format the resulting date as "month year" via `Intl.DateTimeFormat(i18n.language, { month: 'long', year: 'numeric' })` (destructure `i18n` from `useTranslation()`'s return — do not import the `i18n` singleton directly, per the Story 4.2 review finding that direct-import breaks re-render on language switch)
  - [x] Render: lock icon (`lucide-react`'s `Lock` icon, `color: '#d97706'` — the `accent-tariff-locked` token from `DESIGN.md`) + text `t('form.lockedLabel', { date: formattedDate })` where the translation string is `"Locked — contract active until {{date}}"` (copy voice-and-tone verbatim from `DESIGN.md`'s do/don't table: `"Locked — contract active until Dec 2026 (D-28)"`)

- [x] Task 7: Extend `client/src/features/tariffs/components/TariffForm.tsx` to support edit mode (AC: 1, 2, 3, 7)
  - [x] **Do not create a new file.** `architecture.md`'s frontend tree names only `TariffForm.tsx` for this concern (plus `TariffList.tsx`, `TariffLockIndicator.tsx`) — extend the existing component with an optional `tariff?: TariffResponse` prop. `tariff` undefined = create mode (current behavior, unchanged); `tariff` defined = edit mode (new).
  - [x] **Edit mode field differences from create mode:**
    - `effectiveDate`: render as a **read-only label** (e.g. `t('form.effectiveDateReadonly', { date: formatDate(tariff.effectiveDate) })`), not an input — there is no `EffectiveDate` field in `PatchTariffRequest` (see Task 4), so it cannot be submitted in edit mode.
    - `pricePerKwh` / `monthlyBaseFee`: prefilled from `tariff.pricePerKwh`/`tariff.monthlyBaseFee` via `formatNumberForInput(value, i18n.language)` (never raw `String(value)` — this is the exact pre-fill bug class fixed project-wide in the Epic 3 retro; `formatNumberForInput` is the correct inverse of `parseLocaleNumber`, already used by `FlatBaselineEdit.tsx`). If `tariff.isLocked === true` and the user has not confirmed the override dialog yet, render both inputs `disabled` + visually greyed (reduced opacity, muted border) with `<TariffLockIndicator contractStartDate={tariff.contractStartDate} contractDurationMonths={tariff.contractDurationMonths} />` shown directly beneath them (AC1: visible on open, no reveal step). Add a text/icon "Edit anyway" affordance next to the lock indicator (tapping either the lock icon or this affordance opens the confirmation `Dialog` from Task 3).
    - Confirmation dialog (AC2): on confirm, set local state `overrideConfirmed = true`, which re-enables the two price inputs and hides the lock indicator for the remainder of this form session; on cancel, close the dialog, fields stay locked, **no request is sent** (this is a pure client-side UI state change — do not call any mutation here).
    - `providerName`, `contractStartDate`, `contractDurationMonths`: always fully editable regardless of lock state (AC1: "non-price fields remain fully editable") — reuse the exact same field markup as create mode, just prefilled from `tariff`.
  - [x] **CRITICAL — backend rejects combined price + contract-term PATCH requests.** `PatchTariffFunction.cs:83-90` explicitly returns HTTP 400 if a single PATCH body contains both a price field (`pricePerKwh`/`monthlyBaseFee`) and a contract-term field (`contractStartDate`/`contractDurationMonths`) at the same time (`"Cannot update price fields and contract terms in the same request."`). Since this form lets the user edit price AND contract-term fields together in one screen, **the submit handler must never send both categories in a single `patchTariff` call.** Use `react-hook-form`'s `formState.dirtyFields` to build the request:
    ```
    const priceDirty = dirtyFields.pricePerKwh || dirtyFields.monthlyBaseFee
    const contractDirty = dirtyFields.providerName || dirtyFields.contractStartDate || dirtyFields.contractDurationMonths

    if (priceDirty) await mutateAsync({ tariffId, body: { pricePerKwh, monthlyBaseFee, lockOverride: overrideConfirmed || undefined } })
    if (contractDirty) await mutateAsync({ tariffId, body: { providerName, contractStartDate, contractDurationMonths } })
    if (!priceDirty && !contractDirty) return // nothing to save — Save button should already be disabled in this state
    ```
    Send the two requests **sequentially** (`await` each), not in parallel — if the first fails, surface the error banner and do not send the second (matches the existing single-error-banner UX; do not attempt partial-failure rollback, that's out of scope). `providerName` has no restriction and is safe to bundle with the contract-term request.
    Because price inputs are `disabled` while locked-and-not-overridden, `dirtyFields.pricePerKwh` can only become true after `overrideConfirmed` — this is what makes `lockOverride` safe to send only on the price request, never on the contract-term request (backend doesn't need it there and ignores it if present, but omitting it is cleaner).
  - [x] `onSuccess` (both requests, or the single request if only one category is dirty): call the parent's close callback, same as create mode.
  - [x] `onError`: reuse the existing generic translated error banner pattern (do not surface `.detail`, matches all four existing forms per Story 4.2's Dev Notes finding) — including the 422 `tariff-locked` case from AC3, which should be unreachable in normal UI flow (price inputs are disabled unless `overrideConfirmed`) but must still degrade to the generic banner if it somehow occurs (e.g. a stale lock state).
  - [x] Title: `t('form.title')` ("Add Tariff") in create mode, `t('form.editTitle')` ("Edit Tariff") in edit mode.
  - [x] Save button `disabled` logic in edit mode: disabled when nothing is dirty, OR any dirty field fails the same `isValidPrice`/`isValidFee` checks already used in create mode, OR `isPending`.

- [x] Task 8: Wire tap-to-edit in `client/src/features/tariffs/components/TariffList.tsx` (AC: 1, 2, 3)
  - [x] Add local state `const [editingTariff, setEditingTariff] = useState<TariffResponse | null>(null)`.
  - [x] Wrap each `TariffRow`'s content in a `<button type="button" onClick={() => setEditingTariff(tariff)}>` (copy `ReadingHistorySheet.tsx`'s exact tap-to-edit pattern: `<li><button onClick={() => setEditingReading(reading)}>...</button></li>`, `client/src/features/readings/components/ReadingHistorySheet.tsx:74-89`) — keep existing row content/styling, just make it interactive.
  - [x] Reuse the same `Sheet` composition already built for "Add Tariff" (Story 4.2, `TariffList.tsx:63-84`) rather than introducing a second `Sheet` instance: `<Sheet open={addOpen || editingTariff !== null} onOpenChange={open => { if (!open) { setAddOpen(false); setEditingTariff(null) } }}>` with `<TariffForm flatId={flatId} tariff={editingTariff ?? undefined} onClose={() => { setAddOpen(false); setEditingTariff(null) }} />` inside `SheetContent`. This keeps one Sheet, one set of styling, and lets `TariffForm`'s own `tariff` prop (Task 7) decide create-vs-edit rendering.

### Frontend — Planned Annual Spend on the Tariff screen

- [x] Task 9: Extend the settings-side VSA bridge in `client/src/features/settings/SettingsPage.tsx` (AC: 4, 5, 6)
  - [x] **Do not import `usePatchFlat` or `useUserSettings` into the `tariffs` feature** — this would violate the no-cross-feature-hook-import rule (project rule #5) that Story 4.2 already established for `flatId`. Instead, extend `TariffSettingsRoute` (the existing bridge component, `SettingsPage.tsx:11-15`) to also own the Planned-Annual-Spend mutation and pass data + a save callback down as props — mirroring exactly how `flatId` is already passed down:
    ```tsx
    function TariffSettingsRoute() {
      const { settings, isLoading, isError } = useUserSettings()
      const { mutate: patchFlat, isPending: isSavingSpend, isError: isSpendSaveError } = usePatchFlat()
      if (isLoading || isError) return null
      return (
        <TariffList
          flatId={settings?.flatId}
          annualKwhBaseline={settings?.annualKwhBaseline}
          plannedAnnualSpend={settings?.plannedAnnualSpend}
          onSavePlannedAnnualSpend={value =>
            settings?.flatId && patchFlat({ flatId: settings.flatId, body: { plannedAnnualSpend: value } })
          }
          isSavingPlannedAnnualSpend={isSavingSpend}
          isPlannedAnnualSpendSaveError={isSpendSaveError}
        />
      )
    }
    ```
  - [x] `usePatchFlat` and `settingsApi.patchFlat` already exist and are unchanged (`client/src/features/settings/hooks/usePatchFlat.ts`, `client/src/features/settings/api/settingsApi.ts:31-32`) — reuse as-is, do not duplicate a second PATCH-flat implementation inside the `tariffs` feature.

- [x] Task 10: Add the "Planned Annual Spend" section to `client/src/features/tariffs/components/TariffList.tsx` (AC: 4, 5, 6, 7)
  - [x] Extend `Props` to `{ flatId: string | undefined; annualKwhBaseline?: number; plannedAnnualSpend?: number | null; onSavePlannedAnnualSpend: (value: number) => void; isSavingPlannedAnnualSpend: boolean; isPlannedAnnualSpendSaveError: boolean }`.
  - [x] Render a new section below the tariff list (inside the existing `px-6` content area, after the `<ul>`/empty-state block): a labelled input pre-filled from `plannedAnnualSpend` (via `formatNumberForInput`, same pre-fill rule as Task 7) with a "Save" button, disabled until the value is dirty and passes `Number.isFinite(parsed) && parsed > 0` (do **not** hardcode the `50000` upper bound client-side — same rule as Story 4.2's Dev Notes for the `10`/`1000` tariff bounds: a duplicated frontend copy would silently drift from the backend; an out-of-range value surfaces via the generic error banner on the 400 response).
  - [x] On Save: parse via `parseLocaleNumber`, call `onSavePlannedAnnualSpend(parsedValue)`. Show a generic translated error banner when `isPlannedAnnualSpendSaveError` is true (same convention as every other form in this codebase — do not surface `.detail`).
  - [x] **Auto-derived helper text (AC6):** formula is `(AnnualKwhBaseline kWh × PricePerKwh €/kWh) + (MonthlyBaseFee × 12)` — the exact same formula already implemented in `OnboardingContract.tsx`'s `autoCalcSpend` (`client/src/features/onboarding/components/OnboardingContract.tsx:82-85`: `kwhNum * priceNum + feeNum * 12`). Source `PricePerKwh`/`MonthlyBaseFee` from the **currently active tariff**, not a new API call — `TariffList.tsx` already has the full tariff list from `useTariffs(flatId)` (list is descending by `effectiveDate`); the active tariff is the first entry where `!isUpcoming(tariff.effectiveDate)` (reuse the existing `isUpcoming` helper, `TariffList.tsx:42`). If no such entry exists (e.g. only future-dated tariffs, or an empty list), omit the helper text entirely rather than showing a broken/zero calculation.
  - [x] Format the computed total and all currency values via `Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR' })` (AC7) — reuse the `formatCurrency` helper already defined in this file (`TariffList.tsx:23-24`).

### i18n

- [x] Task 11: Populate translation keys (AC: 1, 2, 4, 6)
  - [x] `client/src/locales/{en-US,de-DE}/tariffs.json` — add under `form`: `editTitle` ("Edit Tariff"/"Tarif bearbeiten"), `effectiveDateReadonly` ("Effective {{date}}"/"Gültig ab {{date}}"), `lockedLabel` ("Locked — contract active until {{date}}"/German equivalent, following the exact voice-and-tone precedent in `DESIGN.md`'s do/don't table), `editAnywayButton` ("Edit anyway"/"Trotzdem bearbeiten"), `overrideDialogTitle`, `overrideDialogDescription` (explains overriding changes the contract's locked rate), `overrideDialogConfirm`, `overrideDialogCancel`. Add under `list` (or a new `budget` section): `budgetTitle` ("Planned Annual Spend"/"Geplante Jahresausgaben"), `budgetSaveButton`, `budgetSavingLabel`, `budgetErrorMessage`, `budgetHelperText` with the AC6 formula as a template string, e.g. `"({{kwh}} kWh × {{price}}/kWh) + ({{fee}} × 12)"`.
  - [x] `client/src/locales/{en-US,de-DE}/settings.json` and `i18n.ts`'s `ns` array: **no changes needed** — `'tariffs'` and `'settings'` namespaces are already registered (verified in `client/src/lib/i18n.ts:23-34`).

### Testing

- [x] Task 12: Frontend tests
  - [x] `client/src/features/tariffs/hooks/usePatchTariff.test.ts`: mirrors `useCreateTariff.test.ts` — successful patch invalidates `['tariffs', flatId]` and `['dashboard', flatId]`; mutation guard when `flatId` undefined.
  - [x] `client/src/features/tariffs/components/TariffLockIndicator.test.tsx`: renders the correct "month year" label given a `contractStartDate`/`contractDurationMonths` pair; verify the local-date-parts computation with a case that would fail under naive UTC math (e.g. a start date near a month boundary combined with a locale west/east of UTC in the test's fixed system time).
  - [x] `client/src/features/tariffs/components/TariffForm.test.tsx`: extend existing suite — edit mode with `tariff.isLocked === true` renders price inputs disabled + `TariffLockIndicator` visible on mount (no extra interaction needed); tapping "Edit anyway" opens the confirmation dialog; confirming enables price inputs and does **not** call the mutation; cancelling keeps fields locked and does not call the mutation; submitting after confirming sends `lockOverride: true`; submitting with only a non-price field dirty (e.g. `providerName`) sends exactly one `patchTariff` call with no price fields; submitting with both a price field and a contract-term field dirty sends **two sequential** `patchTariff` calls, each containing only its own category — this is the most important regression test in this story given the backend's mutual-exclusion rule (Task 7).
  - [x] `client/src/features/tariffs/components/TariffList.test.tsx`: extend existing suite — tapping a tariff row opens the edit sheet with that tariff's data prefilled; Planned Annual Spend section renders current value, accepts a new value, calls `onSavePlannedAnnualSpend` with the parsed number on Save; helper text shows the auto-derived formula when an active (non-upcoming) tariff exists, and is omitted when only upcoming tariffs exist or the list is empty.
  - [x] `client/src/features/settings/components/FlatSettingsCard.test.tsx` / a new test for `SettingsPage.tsx`'s `TariffSettingsRoute`: verify it passes `annualKwhBaseline`, `plannedAnnualSpend`, and a working `onSavePlannedAnnualSpend` callback through to `TariffList` (mock `TariffList` to assert props, mirroring how `TariffSettingsRoute`'s existing `flatId`-passing behavior would be tested).

- [x] Task 13: Backend tests — already covered by Task 2 (`PatchFlatValidatorTests.cs`). No new `api.Tests/Features/Tariffs/*` tests required — Story 4.1 already fully covers PATCH lock/override behavior (`PatchTariffFunctionTests.cs:90-149`).

- [x] Task 14: Self-review checklist pass before marking ready for review
  - [x] Verify no `patchTariff` call ever sends both a price field and a contract-term field in the same request body (Task 7's core risk) — trace through the dirty-fields branching logic manually against at least the three cases: price-only dirty, contract-only dirty, both dirty.
  - [x] Verify `PlannedAnnualSpend`'s explicit-null clear path (used by `FlatBaselineEdit.tsx`, unrelated to this story but sharing the validator) still succeeds after Task 1's bound is added — this is the regression case Task 2 explicitly tests for.
  - [x] No hardcoded English/German strings in new JSX — all via `useTranslation('tariffs')`.
  - [x] No cross-feature hook imports — `tariffs` feature still only receives `flatId` (and now `annualKwhBaseline`/`plannedAnnualSpend`/the save callback) via props from `settings`, never by importing `settings/` hooks directly.
  - [x] `TariffLockIndicator`'s date math uses local `Date` getters throughout, never `toISOString()`/`getUTCMonth()`/UTC-anchored `Date.now()` comparisons (matches the Story 4.2 review-fixed pattern).

### Review Findings

- [x] [Review][Patch] Clearing `providerName`/`contractStartDate`/`contractDurationMonths` silently no-ops — `PatchTariffFunction` only applies a field when it is `not null`; there is no explicit-clear ("Provided" flag) semantics like `PlannedAnnualSpend` has on `Flats`. `onSubmitEdit` collapses an emptied field to `undefined`, which is dropped from the JSON body, so attempting to clear `providerName` (or un-set `contractStartDate`/`contractDurationMonths`) always no-ops with no error shown to the user. **Decision (resolved):** fix now — extend `PatchTariffRequest`/`PatchTariffFunction` with the same explicit-null-clear "Provided" flag pattern already used for `Flats.PlannedAnnualSpend`, and update the frontend to send explicit `null` (not `undefined`) for a cleared field. [`client/src/features/tariffs/components/TariffForm.tsx:184-192`; `api/Features/Tariffs/PatchTariffFunction.cs:94-99`; `api/Features/Tariffs/TariffModels.cs`]

- [x] [Review][Patch] Tariff edit sheet can be dismissed mid-mutation, losing partial-failure feedback — `TariffList.tsx`'s `Sheet` `onOpenChange` closes unconditionally even while `TariffForm`'s sequential PATCH calls are awaiting; dismissing (Escape/overlay/close) during the pending window unmounts `TariffForm` and silently drops success/error surfacing for the in-flight request, and if the price PATCH already succeeded before the contract-term PATCH fails or the sheet closes, that partial change is left applied with nothing shown to the user. [`client/src/features/tariffs/components/TariffList.tsx:88`; `client/src/features/tariffs/components/TariffForm.tsx:172-198`]

- [x] [Review][Patch] `PlannedAnnualSpendSection` has stale-value and premature-dirty-reset bugs — its local `raw` input state is seeded from the `plannedAnnualSpend` prop only on mount with no resync when the prop later changes (e.g. after a successful save re-fetches settings), and `handleSave` calls the fire-and-forget `mutate` then immediately `setDirty(false)`, re-disabling Save regardless of whether the mutation actually succeeds — a failed save (e.g. AC5's out-of-range 400) leaves the user unable to retry without making an unrelated edit first. [`client/src/features/tariffs/components/TariffList.tsx` — `PlannedAnnualSpendSection`]

- [x] [Review][Patch] `onSubmitEdit` re-implements price/fee validity inline instead of reusing the existing `isValidPrice`/`isValidFee` helpers already defined in the same file — risks the two checks drifting apart over time. [`client/src/features/tariffs/components/TariffForm.tsx:158-166`]

- [x] [Review][Patch] `formatDate` (edit-mode readonly effective date) reintroduces the UTC-vs-local off-by-one bug the story's own Dev Notes say was already fixed elsewhere — it does `new Date(isoDate)` + default-timezone `Intl.DateTimeFormat` instead of the local-date-parts approach used by `toLocalDateInputValue`/`TariffLockIndicator`, so users west of UTC can see the effective date rendered one day early. [`client/src/features/tariffs/components/TariffForm.tsx:211-212`]

- [x] [Review][Patch] `PatchFlatValidatorTests` boundary test names imply a range but only assert the exact boundary value — `Validate_PlannedAnnualSpendAtOrAboveUpperBound_Fails`/`..AtOrBelowLowerBound_Fails` only test `50000`/`0` exactly, not values further outside the range. [`api.Tests/Features/Flats/PatchFlatValidatorTests.cs`]

- [x] [Review][Patch] `onSavePlannedAnnualSpend` silently no-ops with no error feedback when `settings?.flatId` is falsy — the save button appears to do nothing rather than surfacing a visible error. [`client/src/features/settings/SettingsPage.tsx` — `TariffSettingsRoute`]

- [x] [Review][Patch] AC6 helper text never computes the auto-derived total — it interpolates the raw kWh/price/fee values into a formula-shaped string but never performs the `kwhNum * priceNum + feeNum * 12` arithmetic (`autoCalcSpend` from `OnboardingContract.tsx` was not actually reused), so the user has no computed number to compare against their manually-set target, defeating AC6's stated purpose. [`client/src/features/tariffs/components/TariffList.tsx` — helper text / budget section]

- [x] [Review][Patch] Lock-override confirmation `Dialog` has no feature-specific styling and relies on undefined theme tokens — `bg-background`/`text-foreground`/`text-muted-foreground`/`ring-ring` have no corresponding `--color-*` custom properties anywhere in `index.css`'s `@theme` block (confirmed by inspection), so the AC2-mandated confirmation dialog likely renders without any visible dark-glass background treatment consistent with the rest of the app. [`client/src/components/ui/dialog.tsx`; `client/src/features/tariffs/components/TariffForm.tsx:386-411`]

### Review Findings (Round 2 — post-fix verification pass)

A second adversarial review was run against the round-1 fixes to verify they were correct and catch any regressions the fix round itself introduced.

- [x] [Review][Patch] `PatchTariffFunction`'s new `JsonNode`-based body parsing dropped case-insensitive property matching — the removed `JsonSerializer.DeserializeAsync<T>` path used `PropertyNameCaseInsensitive = true` (a documented non-negotiable backend rule, and a pattern actively used in `CreateTariffFunction.cs`, `SubmitReadingFunction.cs`, `PatchReadingFunction.cs`, and others); the replacement manual `JsonNode.Parse(body)` had no equivalent option, so a differently-cased key would silently be treated as absent. Fixed by passing `new JsonNodeOptions { PropertyNameCaseInsensitive = true }` to `JsonNode.Parse`. [`api/Features/Tariffs/PatchTariffFunction.cs`]

- [x] [Review][Patch] `lockOverride` was the only field in the rewritten `PatchTariffFunction` with no wrong-type guard — every other field (`pricePerKwh`, `monthlyBaseFee`, `providerName`, `contractStartDate`, `contractDurationMonths`) returns 400 if present-but-wrong-type; `lockOverride` silently defaulted to `false` instead. Added the matching `else if` branch. [`api/Features/Tariffs/PatchTariffFunction.cs`]

- [x] [Review][Patch] `missingFlatIdError` state in `TariffSettingsRoute` could go stale — once set `true`, it was only cleared right before a subsequent save attempt, so if `flatId` later became available without the user retrying, the error banner would keep showing incorrectly. Fixed by ANDing the stale flag with the current `!settings?.flatId` check so it self-clears once `flatId` is available. [`client/src/features/settings/SettingsPage.tsx`]

- [x] [Review][Patch] `PlannedAnnualSpendSection`'s dirty-reset-on-settle effect had a race: if the user typed a new value while a prior save was still in flight, and that prior save then succeeded, `dirty` was cleared unconditionally — silently marking the user's newer unsaved edit as "saved". Fixed by tracking the actually-submitted value in a ref and only clearing `dirty` if the current parsed value still matches what was submitted. [`client/src/features/tariffs/components/TariffList.tsx`]

- [x] [Review][Patch] `TariffForm`'s edit-mode submit guard had a gap between the two sequential `patchMutateAsync` calls — `usePatchTariff`'s `isPending` genuinely drops to `false` between the first call resolving and the second starting, momentarily disengaging `useSubmitGuard` and `isSaveEnabled`, which could let a fast double-click slip a duplicate submit into the gap. Fixed by adding a local `isEditSequenceSubmitting` flag that spans the whole `onSubmitEdit` execution (set in a `try`/`finally`) and ORing it into `isPending`. [`client/src/features/tariffs/components/TariffForm.tsx`]

- [x] [Review][Patch] The lock-override confirmation dialog's generated close ("X") button still relied on undefined `ring-ring`/`ring-offset-background`/`bg-secondary` theme tokens even after the round-1 dialog styling fix — only the dialog surface and title/description text were overridden, not the close button's focus-ring/open-state classes. Added `[&>button]:ring-offset-transparent [&>button]:focus:ring-white/40 [&>button]:data-[state=open]:bg-white/10` to the same className override. [`client/src/features/tariffs/components/TariffForm.tsx`]

- [x] [Review][Dismiss] **Contract-date timezone interpretation (viewer-local vs. absolute-calendar)** — `TariffLockIndicator` and `toLocalDateInputValue` interpret `contractStartDate` using the *viewer's own local timezone*, while `TariffList.tsx`'s `toUtcDateString` (for `effectiveDate`/`isUpcoming`) deliberately uses UTC getters to stay viewer-independent. This means two viewers in different timezones can see a different "locked until" month for the same tariff. This predates this review round — it was already implemented and locked in by `TariffLockIndicator.test.tsx`'s existing test with explicit reasoning comments — so it was not silently changed. Raised to the user as a decision; **dismissed** as not a real-world concern for this product (single-flat/household usage, not a cross-timezone multi-viewer scenario).

Dismissed as noise or by-design after verification (from Blind Hunter / Edge Case Hunter round-2 findings): a claim that locked tariffs' contract-term fields aren't protected from mutation (by design — Task 7 explicitly requires `providerName`/`contractStartDate`/`contractDurationMonths` to stay "fully editable regardless of lock state"; only price fields are locked); a claimed frontend/backend mutual-exclusion category mismatch (pre-existing, and the frontend never actually bundles price with `providerName` in one request, so it doesn't manifest); a claim that price fields can't be null-cleared (by design — price is a required field, not a clearable one); full-body buffering and the fully-qualified `JsonException` catch (both match the existing `PatchFlatFunction.cs` precedent this pattern was copied from); `formatDate`'s reuse of `toLocalDateInputValue` (deliberate DRY reuse, not a bug); independently clearing only one of `contractStartDate`/`contractDurationMonths` via direct API call (theoretical hardening gap, not reachable through the actual app UI — deferred, not patched); `TariffLockIndicator` testing only one timezone direction (pre-existing test gap on code not touched by this review).

## Dev Notes

### Scope boundary — read this first

- This story is entirely about **editing** existing tariffs (lock indicator + override flow) and the **Planned Annual Spend** field. It does not touch `CreateTariffFunction`/`useCreateTariff`/the "Add Tariff" flow, which is unchanged from Story 4.2. The only backend change in this story is the `PlannedAnnualSpend` validator bound (Task 1) — `PatchTariffFunction`/`PatchTariffValidator`/`TariffLockPolicy` (Story 4.1) are consumed as-is, fully tested already.
- The sprint-change-proposal applied on 2026-07-02 (`_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02.md`) explicitly confirms Story 4.3's AC were unaffected by that change — it only touched Story 4.1's AC text (duplicate-`EffectiveDate` 409 handling, `TariffValidator` upper bounds), both already implemented and tested in the `done` Story 4.1.

### Known pre-existing gap — `TariffLockPolicy.IsLocked` never expires

- `api/Features/Tariffs/TariffModels.cs:29-33`: `IsLocked` returns true whenever `ContractStartDate < now && ContractDurationMonths != null` — it does **not** check whether `now` is still within the contract window (i.e. `ContractStartDate + ContractDurationMonths months > now`). A tariff with a contract that ended a year ago still reports `isLocked: true` forever. AC1's label text ("Locked — contract active **until** {month year}") reads as though the lock should expire at that date, but the backend this story consumes as-is does not implement that. **This is out of scope for Story 4.3** (AC3 explicitly says the backend lock is reused as-is from Story 4.1) — do not silently "fix" `TariffLockPolicy` as part of this story. Flagged here so the label text is understood as accurately describing the contract's stated end date, not a guarantee that the lock will actually release then; raise with the user/PM if this needs a follow-up story.

### Known pre-existing duplication — Planned Annual Spend already editable elsewhere

- `client/src/features/settings/components/FlatBaselineEdit.tsx` (Story 2.5, `/settings/flat`) already has a working "Planned annual spend" input wired to the same `usePatchFlat` mutation and the same `Flats.PlannedAnnualSpend` field. AC4 of this story adds a **second** edit surface for the same field, on the Tariff screen, per PRD FR-7 / UX decision D-23 ("In Settings it lives near the Tariff configuration, not in a separate budget section") — Story 2.5 predates that placement decision being fully honored. This story does not ask for `FlatBaselineEdit.tsx`'s field to be removed, and removing it is **not** part of this story's AC — leave it in place. Both write to the same backend field so there's no data-consistency risk, just two entry points; flag this to the user if they want the older one removed in a follow-up.

### Testing standards (frontend)

- Vitest, `globals: true`, co-located `.test.tsx`/`.test.ts`, `jsdom` environment.
- Mock `react-i18next` per-test; mock API modules (`vi.mock('@/features/tariffs/api/tariffApi')`), not `apiClient` directly.
- Query by role/label/text, not CSS class or `data-testid`.
- Do not test `components/ui/dialog.tsx` internals (shadcn-generated, same rule as `sheet.tsx`).

### Testing standards (backend)

- xUnit + Shouldly, `api.Tests/Features/Flats/PatchFlatValidatorTests.cs` — direct validator unit tests (no DB, no HTTP), matching the existing 3-test file's style exactly.

### Project Structure Notes

- New file: `client/src/components/ui/dialog.tsx` (generated shadcn primitive).
- New file: `client/src/features/tariffs/components/TariffLockIndicator.tsx` — the one file architecture.md named for this feature area that Story 4.2 explicitly deferred to this story.
- New file: `client/src/features/tariffs/hooks/usePatchTariff.ts`.
- Modified: `client/src/features/tariffs/api/tariffApi.ts`, `client/src/features/tariffs/components/TariffForm.tsx`, `client/src/features/tariffs/components/TariffList.tsx`, `client/src/features/settings/SettingsPage.tsx`, `api/Features/Flats/PatchFlatValidator.cs`.
- No new feature folders — everything fits within the existing `tariffs`/`settings` VSA slices.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-4-tariff-management.md#Story 4.3] — full AC text
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02.md] — confirms Story 4.3 AC unaffected by the 2026-07-02 change (Story 4.1 only)
- [Source: api/Features/Tariffs/PatchTariffFunction.cs] — authoritative PATCH behavior: mutual-exclusion of price vs. contract-term fields (lines 83-90), lock-override logic (lines 92-119), response shape
- [Source: api/Features/Tariffs/TariffModels.cs] — `PatchTariffRequest` (no `EffectiveDate` field), `TariffLockPolicy.IsLocked` (never-expires gap noted above)
- [Source: api/Features/Flats/PatchFlatValidator.cs, PatchFlatFunction.cs, FlatModels.cs] — existing `PlannedAnnualSpend` partial-PATCH semantics (`PlannedAnnualSpendProvided` flag) to preserve when adding the bound
- [Source: api.Tests/Features/Flats/PatchFlatValidatorTests.cs] — existing 3-test style to mirror for Task 2
- [Source: api.Tests/Features/Tariffs/PatchTariffFunctionTests.cs#L90-149] — existing lock/override backend test coverage (no new tests needed there)
- [Source: client/src/features/tariffs/components/TariffForm.tsx] — current create-only implementation to extend (Task 7); existing `isValidPrice`/`isValidFee`, `formatNumberForInput`/`parseLocaleNumber` usage
- [Source: client/src/features/tariffs/components/TariffList.tsx] — current list implementation; `isUpcoming`, `formatCurrency`, `formatPricePerKwh` helpers to reuse for Tasks 8 & 10
- [Source: client/src/features/readings/components/ReadingHistorySheet.tsx#L20-96] — tap-row-to-edit precedent (`editingReading` state, `<li><button onClick=...>`) copied for Task 8
- [Source: client/src/features/onboarding/components/OnboardingContract.tsx#L78-140] — `autoCalcSpend` formula (`kwhNum * priceNum + feeNum * 12`) reused verbatim for AC6's helper text
- [Source: client/src/features/settings/components/FlatBaselineEdit.tsx] — existing Planned Annual Spend editor (pre-existing duplication noted above); `usePatchFlat` usage pattern
- [Source: client/src/features/settings/hooks/usePatchFlat.ts, client/src/features/settings/api/settingsApi.ts] — reused as-is via the Task 9 bridge, not duplicated
- [Source: client/src/features/settings/SettingsPage.tsx#L11-15] — existing `TariffSettingsRoute` bridge to extend (Task 9); established `flatId`-as-prop VSA-isolation precedent
- [Source: client/src/components/ui/sheet.tsx] — structural pattern to copy for the new `dialog.tsx` (Task 3); confirms `@radix-ui/react-dialog` is already a dependency
- [Source: _bmad-output/implementation-artifacts/4-2-tariff-management-ui-list-and-add-form-in-settings.md#Review Findings] — the local-date-parts timezone fix (`todayIsoDate`/`isUpcoming`), `formatNumberForInput` pre-fill rule, `i18n` destructuring-not-import rule, generic-error-banner convention — all reapplied in this story's new code
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/DESIGN.md#L145,#L366] — `accent-tariff-locked` (#d97706) token; "Locked — contract active until Dec 2026" voice-and-tone precedent
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md#FR-7,#FR-37] — Planned Annual Spend Settings placement ("near the Tariff configuration") and immediate-effect-on-budget-alerts requirement
- [Source: _bmad-output/project-context.md#Critical Don't-Miss Rules] — no cross-feature hook imports (rule #5), `mode: 'onBlur'` (rule #7, unaffected — no new forms with a different mode), Problem Details error shape

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet test api.Tests` — 132/132 passed (8 new `PatchFlatValidatorTests` cases for the `PlannedAnnualSpend` bound)
- `npx vitest run` (client) — 155/155 passed across 27 test files
- `npx tsc -b` (client) — no errors
- `npm run lint` (client, oxlint) — no new warnings (pre-existing `router.tsx` fast-refresh warnings only)

### Completion Notes List

- Added the `PlannedAnnualSpend` bound (`>0 and <50000`) to `PatchFlatValidator`, guarded by `PlannedAnnualSpendProvided && PlannedAnnualSpend is not null` so the existing explicit-null "clear override" path stays valid — verified with a dedicated regression test.
- Built `client/src/components/ui/dialog.tsx` as a new shadcn-style generated primitive (centered modal, reusing `@radix-ui/react-dialog` like `sheet.tsx`) for the lock-override confirmation dialog.
- Extended `TariffForm` with an optional `tariff` prop for edit mode: read-only effective date, disabled+greyed price fields with `TariffLockIndicator` and an "Edit anyway" affordance when locked, and dirty-field-based submission that never sends price and contract-term fields in the same PATCH request (mirrors `PatchTariffFunction`'s mutual-exclusion rule). Verified with tests covering price-only, contract-only, and both-dirty submission paths.
- `TariffLockIndicator` computes "locked until" using local `Date` parts only (no `toISOString()`/`getUTCMonth()`), per the Story 4.2 review-fixed timezone pattern; covered by a regression test that stubs `TZ` to a UTC-negative offset near a month boundary.
- Wired tap-to-edit on `TariffList` rows (reusing the existing Add-Tariff `Sheet`) and added a Planned Annual Spend section below the tariff list, sourcing the auto-derived helper-text formula from the first non-upcoming (active) tariff and omitting it when none exists.
- Extended `TariffSettingsRoute` in `SettingsPage.tsx` to own the `usePatchFlat` mutation and pass `annualKwhBaseline`/`plannedAnnualSpend`/a save callback down as props, preserving the no-cross-feature-hook-import rule.
- Left `FlatBaselineEdit.tsx`'s pre-existing Planned Annual Spend editor untouched, as scoped by the story's Dev Notes (two entry points writing to the same field, no data-consistency risk).
- Self-review checklist (Task 14) passed: no combined price+contract-term PATCH bodies, explicit-null clear path still succeeds, no hardcoded strings in new JSX (found and fixed one — the Planned Annual Spend "€/yr" suffix, now `budget.spendSuffix`), no cross-feature hook imports, lock-indicator date math uses local getters only.

### File List

**New**
- `client/src/components/ui/dialog.tsx`
- `client/src/features/tariffs/components/TariffLockIndicator.tsx`
- `client/src/features/tariffs/components/TariffLockIndicator.test.tsx`
- `client/src/features/tariffs/hooks/usePatchTariff.ts`
- `client/src/features/tariffs/hooks/usePatchTariff.test.ts`
- `client/src/features/settings/SettingsPage.test.tsx`

**Modified**
- `api/Features/Flats/PatchFlatValidator.cs`
- `api.Tests/Features/Flats/PatchFlatValidatorTests.cs`
- `api/Features/Tariffs/TariffModels.cs`
- `api/Features/Tariffs/PatchTariffFunction.cs`
- `api.Tests/Features/Tariffs/PatchTariffFunctionTests.cs`
- `client/src/features/tariffs/api/tariffApi.ts`
- `client/src/features/tariffs/components/TariffForm.tsx`
- `client/src/features/tariffs/components/TariffForm.test.tsx`
- `client/src/features/tariffs/components/TariffList.tsx`
- `client/src/features/tariffs/components/TariffList.test.tsx`
- `client/src/features/settings/SettingsPage.tsx`
- `client/src/locales/en-US/tariffs.json`
- `client/src/locales/de-DE/tariffs.json`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-07-02: Implemented Story 4.3 — tariff lock indicator with override confirmation flow, `PlannedAnnualSpend` validator bound, and Planned Annual Spend editor on the Tariff settings screen. All 14 tasks complete; 8 new backend tests and 1 new + 6 extended frontend test files added.
- 2026-07-03: Code review pass — applied all 9 patch findings: added explicit-clear ("Provided" flag) semantics to `PatchTariffRequest`/`PatchTariffFunction` for `providerName`/`contractStartDate`/`contractDurationMonths` (mirroring the `Flats.PlannedAnnualSpend` pattern); guarded the tariff edit `Sheet` against dismissal while a PATCH is pending; fixed `PlannedAnnualSpendSection`'s stale-value resync and premature dirty-reset-before-success; deduplicated `onSubmitEdit`'s price/fee validation to reuse `isValidPrice`/`isValidFee`; fixed `TariffForm`'s edit-mode `formatDate` to use local-date parts (matching the Story 4.2 timezone fix); added off-boundary `PatchFlatValidatorTests` coverage; surfaced an error when `flatId` is missing on Planned Annual Spend save; computed the actual AC6 auto-derived total in the helper text; and added feature-specific dark-glass styling to the lock-override confirmation dialog. 3 new backend tests, 5 new/extended frontend tests — 137 backend and 160 frontend tests passing, clean typecheck and lint.
- 2026-07-03: Second code review pass (verification of round 1's fixes) — found and fixed 6 new issues introduced or left over by the round-1 fix round: restored case-insensitive JSON property matching lost in `PatchTariffFunction`'s rewritten body parsing; added a missing wrong-type guard for `lockOverride`; fixed a stale-error-banner edge case in `TariffSettingsRoute`; closed a race in `PlannedAnnualSpendSection`'s dirty-reset that could silently discard a newer unsaved edit; closed a submit-guard gap between the two sequential PATCH calls in edit mode; and completed the lock-override dialog's close-button styling. One pre-existing architectural question (contract-date timezone interpretation, predating this story) was raised with the user and dismissed as out of scope for this product. 2 new backend tests — 139 backend and 160 frontend tests passing, clean typecheck and lint.
