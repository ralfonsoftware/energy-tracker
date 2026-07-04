---
baseline_commit: f1b124820b3c807fe0e8192fbe43c90d58a2151e
---

# Story 5.2: Flat Switcher, Add Flat & Deletion UI

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to switch between my flats from the app header, create additional flats, and delete a flat by typing its name to confirm,
so that I can move between dwellings quickly and remove a flat with friction appropriate to an irreversible action.

## Acceptance Criteria

1. **Given** the app header on any surface, **when** rendered, **then** the active Flat's name is displayed as a tappable element; tapping it opens the flat switcher dropdown listing all Flats plus an "Add flat" option at the bottom; the active Flat is visually distinguished. (FR-19)

2. **Given** a different Flat is selected from the dropdown, **when** the selection is made, **then** `PUT /api/v1/user/settings` is called with the new `activeFlatId`; all TanStack Query keys scoped to the previous `flatId` are invalidated; all surfaces reload data for the newly selected Flat; the header Flat name updates immediately.

3. **Given** the browser is closed and reopened, **when** the app loads, **then** `GET /api/v1/user/settings` returns the stored `activeFlatId`; the app initialises with that Flat active without requiring re-selection. (FR-20)

4. **Given** "Add flat" is tapped in the switcher, **when** the add flat form opens, **then** the user can enter a flat name (required) and Annual kWh Baseline using the same preset + custom pattern from onboarding; submitting calls `POST /api/v1/flats` and on success switches to the new Flat; a prompt guides the user to Settings → Tariff to add an initial tariff.

5. **Given** the user navigates to Settings → Account and taps "Delete Flat", **when** `FlatDeleteConfirm.tsx` opens, **then** a text input shows the prompt `Type "{flatName}" to delete`; the Delete button is disabled until the typed value matches the Flat name exactly (case-sensitive); tapping Delete calls `DELETE /api/v1/flats/{flatId}` and on 204: switches to another available Flat or redirects to onboarding if no Flats remain.

## Tasks / Subtasks

### Frontend — API & hooks (settings feature owns Flat CRUD; no new "flats" feature folder)

- [x] Task 1: Extend `client/src/features/settings/api/settingsApi.ts` (AC: 1, 2, 4, 5)
  - [x] Add `export type FlatSummary = { flatId: string; name: string; annualKwhBaseline: number; spikeThreshold: number; plannedAnnualSpend: number | null }` (camelCase JSON — mirrors backend `FlatSummary` record field order from Story 5.1).
  - [x] Add `export const getFlats = () => apiClient.get<FlatSummary[]>('/flats')`.
  - [x] Add `export type CreateFlatBody = { name: string; annualKwhBaseline: number; plannedAnnualSpend: number | null }` and `export const createFlat = (body: CreateFlatBody) => apiClient.post<FlatSummary>('/flats', body)`.
  - [x] Add `export const deleteFlat = (flatId: string) => apiClient.delete<void>(\`/flats/${flatId}\`)` (`apiClient.delete` already exists in `client/src/lib/apiClient.ts` — no change needed there).
  - [x] **Breaking-change-in-place, read before touching:** `updateUserSettings` currently has signature `(locale: string) => apiClient.put<UserSettings>('/user/settings', { locale })`. Change it to `updateUserSettings = (body: { locale: string; activeFlatId?: string | null }) => apiClient.put<UserSettings>('/user/settings', body)`. **Why this is mandatory, not optional:** the backend `UpdateUserSettingsFunction` (Story 5.1) treats `locale` as a required field on every PUT — a request missing the `locale` key entirely returns HTTP 400 (see `api/Features/Settings/UpdateUserSettingsFunction.cs:47-56`, `obj["locale"]?.GetValue<string>()` → 400 if null). Any new mutation that switches `activeFlatId` MUST also send the current `locale` in the same body, or every flat switch will 400.
  - [x] Update the one existing caller, `client/src/features/settings/hooks/useUpdateLocale.ts`, to match: `mutationFn: (locale: string) => updateUserSettings({ locale })`. Its own caller `LocaleDropdown.tsx` (`updateLocale(value, ...)`) needs no change — the hook's public signature (takes a bare `string`) is unchanged, only its internal `mutationFn` wrapping changes.
  - [x] Add `export type UserSettings` field: no change needed — `activeFlatId` already isn't in the type; do **not** add it, since no frontend consumer reads it directly (the header/switcher derive "active" purely from `settings.flatId`, which `GetUserSettingsFunction`/`UpdateUserSettingsFunction` already resolve server-side from `ActiveFlatId` with graceful fallback — see Dev Notes).

- [x] Task 2: `client/src/features/settings/hooks/useFlats.ts` (AC: 1) — new file
  - [x] `useQuery({ queryKey: ['flats'], queryFn: getFlats, staleTime: 60 * 1_000 })`. Not flat-scoped (it's the list of all the user's flats) — same pattern as `['settings']`, not `[resource, flatId]`.

- [x] Task 3: `client/src/features/settings/hooks/useSwitchActiveFlat.ts` (AC: 2) — new file
  - [x] `useMutation` wrapping `updateUserSettings`. Signature: `mutate({ flatId, locale, previousFlatId })` where the caller supplies the **current** `locale` (from `useUserSettings()`'s `settings.locale`, falling back to `i18n.language` if null — see Dev Notes on why this can't be read from inside the hook) and the `previousFlatId` (the flatId being switched away from, so this hook — not the caller — owns exactly which cache entries to purge).
  - [x] `mutationFn: ({ flatId, locale }) => updateUserSettings({ locale, activeFlatId: flatId })`.
  - [x] `onSuccess: (_data, { previousFlatId }) => { queryClient.invalidateQueries({ queryKey: ['settings'] }); if (previousFlatId) { queryClient.invalidateQueries({ queryKey: ['dashboard', previousFlatId] }); queryClient.invalidateQueries({ queryKey: ['readings', previousFlatId] }); queryClient.invalidateQueries({ queryKey: ['tariffs', previousFlatId] }) } }` — these three are the **complete** current set of `[resource, flatId]` families in the codebase (verified by grep; `flat-structure`/`decomposition`/`insights`/`import` query keys don't exist yet, they arrive in later Epic 5/6/7/8 stories and must add their own invalidation here when built).

- [x] Task 4: `client/src/features/settings/hooks/useCreateFlat.ts` (AC: 4) — new file
  - [x] `useMutation({ mutationFn: createFlat })`. On success this story's UI (Task 7) chains a `useSwitchActiveFlat` call — do **not** invalidate `['flats']`/`['settings']` inside this hook's `onSuccess`; let the switch-flat call (which always follows) own invalidation, avoiding a double-fetch race.

- [x] Task 5: `client/src/features/settings/hooks/useDeleteFlat.ts` (AC: 5) — new file
  - [x] `useMutation({ mutationFn: deleteFlat })`. `onSuccess: (_data, deletedFlatId) => { queryClient.invalidateQueries({ queryKey: ['flats'] }); queryClient.invalidateQueries({ queryKey: ['settings'] }); queryClient.invalidateQueries({ queryKey: ['dashboard', deletedFlatId] }); queryClient.invalidateQueries({ queryKey: ['readings', deletedFlatId] }); queryClient.invalidateQueries({ queryKey: ['tariffs', deletedFlatId] }) }`. **Do not** write any "pick another flat and call `useSwitchActiveFlat`" logic — it is unnecessary. See Dev Notes: `GetUserSettingsFunction` already falls back to "first owned flat" (or `hasFlat: false`) whenever `ActiveFlatId` is null or points to a flat that no longer exists. Invalidating `['settings']` is sufficient to trigger that fallback and, via `OnboardingGate`, an automatic redirect to `/onboarding` if zero flats remain.

### Frontend — Header & Flat Switcher (new cross-cutting components, not a "feature" slice)

- [x] Task 6: `client/src/components/FlatSwitcher.tsx` (AC: 1, 2, 3) — new file
  - [x] Hand-roll the dropdown exactly like `client/src/components/LocaleDropdown.tsx` (`useState` open flag + `useRef` + `mousedown`/`touchstart`/`Escape` document listeners to close) — **do not** reach for a shadcn `DropdownMenu`; that primitive is not installed in this codebase (`client/src/components/ui/` only has `dialog.tsx`, `popover.tsx`, `sheet.tsx` — verified by listing the directory) and this project's established pattern for exactly this kind of small header dropdown is the hand-rolled one.
  - [x] `useUserSettings()` for `settings.flatId`/`settings.flatName`/`settings.locale` (the currently-active Flat, already resolved server-side) and `useFlats()` for the full list to render.
  - [x] Trigger button: shows `settings.flatName` (fallback to a loading placeholder while `useUserSettings` is loading — do not render the trigger with an empty label).
  - [x] Dropdown body: one row per Flat from `useFlats()` (`role="option"`, `aria-selected={flat.flatId === settings.flatId}`); visually distinguish the active row (same selected-style convention as `LocaleDropdown`'s locale rows — border/background delta, not just an icon, so it also communicates via accessible attribute).
  - [x] Clicking a non-active Flat row: call `useSwitchActiveFlat().mutate({ flatId: flat.flatId, locale: settings.locale ?? i18n.language, previousFlatId: settings.flatId })`, then close the dropdown. Clicking the already-active row is a no-op (close dropdown only, no mutation — avoid a wasted PUT).
  - [x] "Add flat" row at the bottom of the dropdown list, visually separated (e.g. a divider) — wraps a shadcn `Sheet`/`SheetTrigger` (see Task 7) so tapping it opens `AddFlatForm` and closes the dropdown simultaneously (`onOpenChange` for the Sheet should also call the dropdown's own `setIsOpen(false)`).
  - [x] This component imports `useUserSettings`/`useFlats`/`useSwitchActiveFlat` from the `settings` feature — this is consistent with the existing precedent of `client/src/App.tsx`'s root-level `LocaleSync` already importing `useUserSettings` from the settings feature; `FlatSwitcher`/`Header` are app-shell furniture in `client/src/components/`, not a feature slice themselves, so the "no cross-feature hook imports" VSA rule (which governs feature-to-feature imports) does not block this.

- [x] Task 7: `client/src/features/settings/components/AddFlatForm.tsx` (AC: 4) — new file
  - [x] Structure: a shadcn `Sheet` content component following `client/src/features/readings/components/EnterReadingSheet.tsx`'s exact convention — receives `open`/`onOpenChange` props from its parent `Sheet`, even though the parent also manages the same boolean (this is the established, if slightly redundant, pattern in this codebase — replicate it, don't "simplify" it away).
  - [x] Two fields: flat name (required text input) and Annual kWh Baseline. For the baseline field, **replicate `client/src/features/settings/components/FlatBaselineEdit.tsx`'s preset-tile-grid + custom-value pattern verbatim** (same four presets: 1500/2500/3500/4250 kWh; same `parseLocaleNumber`/`i18n.language` parsing; same "clicking a preset fills the input, typing anything clears the selected preset" interaction) — this is the literal "same preset + custom pattern from onboarding" the AC calls for. Do not build a new preset UI from scratch.
  - [x] New zod schema in `client/src/features/settings/schemas/settingsSchema.ts` (co-located per project convention — do not import `onboardingSchema`'s `contractSchema` cross-feature): `addFlatSchema = z.object({ name: z.string().trim().min(1), annualKwhBaseline: z.string().min(1) })`. No `plannedAnnualSpend` field — AC4 only calls for name + baseline; `CreateFlatRequest`'s `plannedAnnualSpend` is optional/nullable server-side (Story 5.1), so submit `plannedAnnualSpend: null`.
  - [x] Call both mutation hooks (`const { mutate: createFlat, isPending: isCreating } = useCreateFlat()` and `const { mutate: switchFlat } = useSwitchActiveFlat()`) at the top of the component, alongside `const { settings } = useUserSettings()` — capture `settings.flatId` (the flat active *before* this new one is created) and `settings.locale` in local variables/closure at render time for use in the submit handler below.
  - [x] On submit: parse the baseline with `parseLocaleNumber(value, i18n.language)`, reject `NaN`/`<=0` inline (mirror `FlatBaselineEdit`'s validation, not a zod refinement — matches project convention of parsing-then-manual-checking for locale-formatted numeric strings). Call `createFlat({ name, annualKwhBaseline, plannedAnnualSpend: null }, { onSuccess: (newFlat) => { switchFlat({ flatId: newFlat.flatId, locale: settings?.locale ?? i18n.language, previousFlatId: settings?.flatId }); onOpenChange(false) }, onError: () => setSubmitError(...) })`.
  - [x] After the chained switch succeeds, show a one-line confirmation prompt inline on the parent surface ("Add a tariff in Settings → Tariff to start tracking costs" — new i18n key) rather than auto-navigating; the user might be mid-flow elsewhere in the header and an unsolicited route change would be jarring. Do not auto-navigate to `/settings/tariffs`.
  - [x] On create error (mutation `isError`): keep the sheet open, show inline error near the submit button (`mutation.error` message) — matches this codebase's universal "mutation errors keep the sheet open" rule (`project-context.md` §Mutations).

- [x] Task 8: `client/src/components/Header.tsx` (AC: 1) — new file
  - [x] Thin wrapper rendering `<FlatSwitcher />` in a header bar. Style: reuse the existing glass-card token set already used throughout this app (`background: rgba(255,255,255,0.07)`, `border: 1px solid rgba(255,255,255,0.12)`, `backdrop-filter: blur(20px)` — same values as `SettingsRoot.tsx`'s `cardStyle` and `SidebarNav.tsx`'s nav background) rather than inventing new tokens; no DESIGN.md visual spec exists for "flat switcher dropdown" (confirmed via `ux-designs/.../review-rubric.md` — it's explicitly called out as inheriting shadcn/layout-container styling, "not a defect"), so match existing app chrome instead of guessing a new visual language.
  - [x] Mount in `client/src/components/AppShell.tsx`: add `<Header />` directly above `<Outlet />` inside `<main>`, so it renders on **both** the mobile (`BottomTabBar`) and desktop (`SidebarNav`) layouts — AC1 says "any surface," and today `AppShell.tsx` has no header row at all (verified: only `EuroBurnGradient`, `SidebarNav`, `BottomTabBar`, `Outlet` exist). This is a genuinely new addition, not a modification of an existing header.

### Frontend — Flat Deletion (Settings → Account)

- [x] Task 9: `client/src/features/settings/components/FlatDeleteConfirm.tsx` (AC: 5) — new file (already named and slotted in `architecture.md`'s file tree — `client/src/features/settings/components/FlatDeleteConfirm.tsx # type-to-confirm (FR-23)`)
  - [x] Props: `flatId: string`, `flatName: string`, `onCancel: () => void`. Renders a text input, placeholder/prompt text interpolating the flat name: `t('account.deleteFlat.prompt', { flatName })` → `Type "{{flatName}}" to delete`.
  - [x] Delete button `disabled` until the typed value **exactly** (case-sensitive, no `.trim()`) equals `flatName` — do not add a case-insensitive or trimmed comparison; the AC is explicit ("matches the Flat name exactly (case-sensitive)").
  - [x] On confirmed tap: `useDeleteFlat().mutate(flatId, { onSuccess: onCancel, onError: () => setError(...) })` — on success just collapse back to the normal Account view (`onCancel`); no explicit navigation (see Task 5 — `OnboardingGate` + settings-invalidation handles both the "another flat remains" and "no flats remain" cases automatically).
  - [x] Style: mirror `AccountSettings.tsx`'s existing inline `showConfirm` sign-out pattern (same red/destructive button styling: `background: rgba(239,68,68,0.15)`, `border: 1px solid rgba(239,68,68,0.4)`, `color: #ef4444`) — this story adds a second destructive inline confirm flow, keep it visually consistent with the first.

- [x] Task 10: Modify `client/src/features/settings/components/AccountSettings.tsx` (AC: 5)
  - [x] Add local state `showDeleteFlatConfirm` alongside the existing `showConfirm` (sign-out) state — the two confirm flows are mutually exclusive in the UI (only one row expands at a time); reuse the existing conditional-render-in-place pattern, don't introduce a shadcn `Dialog`/`Sheet` for this (no precedent for it in this component, and the existing sign-out flow already establishes "inline expand within the card" as the pattern for destructive confirmations here).
  - [x] Add a new destructive row below sign-out: `"Delete Flat"` button (same red styling as sign-out's confirm button) that sets `showDeleteFlatConfirm = true`. Needs `settings.flatId`/`settings.flatName` — `AccountSettings` doesn't currently call `useUserSettings()`; add that call (it's a cheap, already-cached query — `staleTime: 5 * 60_000` — no extra network cost since `SettingsRoot` already fetches it and TanStack Query dedupes by key).
  - [x] When `showDeleteFlatConfirm` is true, render `<FlatDeleteConfirm flatId={settings.flatId} flatName={settings.flatName} onCancel={() => setShowDeleteFlatConfirm(false)} />` instead of the normal row — guard on `settings?.flatId && settings.flatName` being present (mirrors the existing `hasFlat && flatName` guard pattern from `SettingsRoot.tsx`).

### i18n

- [x] Task 11: Add translation keys (AC: 1, 4, 5) — both `client/src/locales/en-US/` and `client/src/locales/de-DE/` (this project requires both locales for every new string; no English-only stubs)
  - [x] `common.json`: add a `flatSwitcher` block (`addFlat`, `loading`, or similar) — `Header`/`FlatSwitcher` are cross-cutting components with no dedicated feature namespace, and `SidebarNav`/`BottomTabBar` already use `common` for identical cross-cutting nav strings; follow that precedent rather than inventing a new namespace or registering one in `client/src/lib/i18n.ts`'s `ns: [...]` array (that array already lists `common`, `settings`, `onboarding` — all namespaces this story needs already exist, no registration change required).
  - [x] `settings.json`: add `account.deleteFlat` block (`button`, `prompt` with `{{flatName}}` interpolation, `deleteButton`, `cancel`, `error`) and an `addFlat` block (`title`, `nameLabel`, `namePlaceholder`, `submit`, `error`, `tariffPrompt`) for `AddFlatForm`.

### Review Findings

_Code review — 2026-07-03. Blind Hunter + Edge Case Hunter + Acceptance Auditor (parallel adversarial review)._

- [x] [Review][Decision] Tariff-prompt sheet stays modally open instead of returning to "the parent surface" — Dev Notes for Task 7 say the confirmation should show "inline on the parent surface... the user might be mid-flow elsewhere in the header," implying the modal Sheet should close and the prompt should appear behind it. The actual implementation (`AddFlatForm.tsx`, `if (tariffPromptShown) return <SheetContent>...`) keeps the full Sheet overlay open and swaps its internal content instead. **Resolved: keep as-is** — verified safe (built-in Radix close button works regardless), simplest, no rewiring needed.
- [x] [Review][Decision] "Delete Flat" and "Sign out" rows are visually identical (`AccountSettings.tsx`) — same red color, same font weight, no distinguishing treatment despite one being a trivially-reversible action and the other an irreversible, cascading data-loss action gated only by type-to-confirm friction. **Resolved: keep as-is** — type-to-confirm friction is the safety gate; identical styling is consistent with the existing sign-out pattern in this component.
- [x] [Review][Patch] `['flats']` cache never invalidated after creating a flat — neither `useCreateFlat.ts` nor `useSwitchActiveFlat.ts` invalidates `queryKey: ['flats']` on success (unlike `useDeleteFlat.ts`, which does). A newly created flat won't appear in the `FlatSwitcher` dropdown for up to 60s (`useFlats`'s `staleTime`), even though the header's active-flat name updates immediately. [client/src/features/settings/hooks/useCreateFlat.ts:5-7, client/src/features/settings/hooks/useSwitchActiveFlat.ts] — Fixed: `useSwitchActiveFlat.ts` now invalidates `['flats']` in `onSuccess` (it always fires after a create, per Task 4's Dev Notes).
- [x] [Review][Patch] Tariff prompt shown even if the chained flat-switch fails — `AddFlatForm.tsx`'s `onSuccess` for `createFlat` calls `setTariffPromptShown(true)` immediately after firing `switchFlat(...)`, without waiting for or checking that mutation's own result. If the switch PUT fails, the user sees a false "success" confirmation while the active flat never actually changed. [client/src/features/settings/components/AddFlatForm.tsx] — Fixed: `setTariffPromptShown(true)` now only fires in `switchFlat`'s own `onSuccess`; its `onError` shows `addFlat.error` instead.
- [x] [Review][Patch] Non-finite baseline input silently serialized as `null` — the `annualKwhBaseline` input is `type="text"` (only `inputMode="decimal"` as a hint, not enforced), so typing "Infinity" passes the `isNaN(kwhParsed) || kwhParsed <= 0` guard (`isNaN(Infinity)` is `false`) and gets sent to the API as `null` via `JSON.stringify(Infinity)`. Add a `!Number.isFinite(kwhParsed)` check alongside the existing guard. [client/src/features/settings/components/AddFlatForm.tsx:65-69] — Fixed: guard is now `!Number.isFinite(kwhParsed) || kwhParsed <= 0` (subsumes the old `isNaN` check).
- [x] [Review][Patch] No accessible label on `FlatDeleteConfirm`'s text input — no `<label>`, `aria-label`, or `aria-labelledby`, unlike `AddFlatForm`'s inputs which correctly use `sr-only` labels. [client/src/features/settings/components/FlatDeleteConfirm.tsx] — Fixed: input now has `aria-labelledby` pointing at the prompt paragraph.
- [x] [Review][Patch] Misleading validation error message — when `parseLocaleNumber` yields `NaN`/`<=0`, `AddFlatForm.tsx` shows the generic `addFlat.error` ("Couldn't add flat — please try again") instead of the already-defined, more accurate `errors.validationNumber` ("Please enter a valid number"). [client/src/features/settings/components/AddFlatForm.tsx] — Fixed: the guard branch now shows `t('common:errors.validationNumber')`.
- [x] [Review][Patch] Locale mismatch between preset display and input parsing — preset buttons render `preset.kwh.toLocaleString()` with no locale argument (browser default), while submission parses via `parseLocaleNumber(value, i18n.language)` (app's active locale). A German app locale on an English-locale OS shows presets in one numeric convention while parsing assumes another. [client/src/features/settings/components/AddFlatForm.tsx] — Fixed: presets now render via `preset.kwh.toLocaleString(i18n.language)`.
- [x] [Review][Patch] Duplicated conditional in `AccountSettings.tsx` — the sign-out row's border style and the delete-flat row's render guard both separately hardcode `settings?.flatId && settings.flatName`; the two must be kept in sync manually across two locations. Extract to a single local variable. [client/src/features/settings/components/AccountSettings.tsx] — Fixed: extracted to a single `hasFlat` boolean reused by both sites.
- [x] [Review][Patch] No loading/error fallback in `FlatSwitcher` for `useFlats()` failure or `settings.flatName` being undefined once `isLoading` is false (e.g. right after the active flat was deleted) — trigger silently renders blank text plus "▾" with no indication of an error or no-flat state. [client/src/components/FlatSwitcher.tsx] — Fixed: trigger falls back to a new `flatSwitcher.error` string; dropdown shows the same string in place of the flat list when `useFlats()` errors.
- [x] [Review][Patch] No in-flight guard on dropdown flat-select — clicking two different flat rows in quick succession fires two concurrent `switchFlat` mutations with no cancellation; an out-of-order response could leave the active flat inconsistent with the user's last click. Low priority. [client/src/components/FlatSwitcher.tsx] — Fixed: `handleSelect` now no-ops while `useSwitchActiveFlat()`'s `isPending` is true.
- [x] [Review][Patch] `showDeleteFlatConfirm` not reset if `settings.flatId`/`flatName` become falsy while the confirm view is open (e.g. a background settings refetch after external change) — the guarded early-return silently falls through to the normal view without calling `onCancel`. Low priority/rare race. [client/src/features/settings/components/AccountSettings.tsx] — Fixed: a `useEffect` resets `showDeleteFlatConfirm` to `false` whenever `hasFlat` goes false while it's open.

_Dismissed as noise (7): "no way to dismiss tariff prompt" (verified false — `SheetContent` always renders a built-in Radix close button regardless of the unused `onOpenChange` prop); "conflicting Escape listeners" (closing both the dropdown and sheet together is acceptable, not a defect); "missing ARIA listbox keyboard navigation" (verified this exactly mirrors the pre-existing `LocaleDropdown.tsx` pattern the story explicitly instructed `FlatSwitcher` to replicate — not a regression); "cancel during in-flight delete" (the DELETE request already fired before Cancel could be clicked; no real abort is possible, consequence is at most a harmless no-op); "`updateUserSettings` signature break" (verified via grep — exactly two callers, both correctly updated); "no guard against deleting last flat" (verified intentional per Dev Notes — server-side fallback + `OnboardingGate` redirect handle it); test-coverage-gap findings (downstream of the confirmed bugs above, or reflect UI states already mutually exclusive by control flow, not independently actionable)._

## Dev Notes

### Why this story needs no new backend work at all

Story 5.1 (already `done`) shipped every endpoint this story consumes: `GET/POST/DELETE /api/v1/flats` and `GET/PUT /api/v1/user/settings` (with `activeFlatId` support, including the "omitted key leaves it unchanged" tri-state PUT semantics). This story is 100% frontend. Do not touch anything under `api/` or `api.Tests/`.

### The `locale`-is-required-on-every-PUT trap (read before writing `useSwitchActiveFlat`)

`UpdateUserSettingsFunction.RunAsync` (`api/Features/Settings/UpdateUserSettingsFunction.cs`) rejects any PUT body missing the `locale` key with HTTP 400 — this is true regardless of whether you're only trying to change `activeFlatId`. Every call site that switches the active flat (the switcher dropdown, the "just created a flat" auto-switch, and — if ever added — a bulk operation) **must** include the current locale in the same request body. The safest source for "current locale" is `useUserSettings()`'s own `settings.locale` at the call site (it's the value the server already resolved and returned), with `i18n.language` as a fallback only if `settings.locale` is unexpectedly null. Do not hardcode a locale, and do not try to read it from inside `useSwitchActiveFlat` itself via `queryClient.getQueryData` — pass it in explicitly from the calling component, which already has it from its own `useUserSettings()` call.

### Why flat deletion needs no "pick a replacement flat" logic

`GetUserSettingsFunction` (`api/Features/Settings/GetUserSettingsFunction.cs:29-31`) already implements the fallback: `flat = ...FirstOrDefaultAsync(f => f.FlatId == activeFlatId && ...); flat ??= ...FirstOrDefaultAsync(f => f.UserId == userId, ct);`. After a Flat is deleted, its `ActiveFlatId` reference becomes dangling (by design — `ActiveFlatId` is an unenforced soft reference, see Story 5.1 Dev Notes on the FK-cascade-cycle trap); the very next `GET /user/settings` call transparently resolves to "first owned flat" if one exists, or returns `hasFlat: false`/`flatId: undefined` if none remain. `OnboardingGate` (`client/src/features/onboarding/components/OnboardingGate.tsx`) already does `if (!settings?.hasFlat) return <Navigate to="/onboarding" replace />` on every route under `AppShell`. So: invalidating `['settings']` after a successful delete is **sufficient** — both AC5 outcomes ("switches to another available Flat" and "redirects to onboarding") happen for free through code that already exists. Writing explicit client-side "if flats.length > 0, switch to flats[0]" logic would be redundant, race-prone (a second PUT competing with the natural refetch), and is **not required**.

### Existing `[resource, flatId]` query-key families — the complete set (verified by grep, 2026-07-03)

Only three families exist today that need invalidation on flat switch/delete: `['dashboard', flatId]`, `['readings', flatId]`, `['tariffs', flatId]`. `['settings']` and `['flats']` are not flat-scoped. `['flat-structure', flatId]` (Story 5.3), `['decomposition', flatId, ...]` (Epic 7), `['insights', flatId]` (Epic 8), and import-related keys (Epic 6) don't exist in the codebase yet — each of those future stories must add its own line to `useSwitchActiveFlat`'s and `useDeleteFlat`'s `onSuccess` invalidation list when it introduces its query key, mirroring how Story 5.1's Dev Notes asked each future Flat-child-table story to add its own `.LoadAsync()` line to `DeleteFlatFunction`.

### No header component exists today — this is new, not a modification

`client/src/components/AppShell.tsx` currently renders only `EuroBurnGradient`, a conditional `SidebarNav`/`BottomTabBar`, and `<Outlet />` — there is no header row anywhere in the app today, despite `EXPERIENCE.md` describing one ("header present in R1 with single Flat name displayed"). No R1 story actually built it; there's no header mockup HTML in `ux-designs/.../mockups/` either (only `flat-structure-editor.html` exists for R2 so far), and no DESIGN.md visual spec for it (`review-rubric.md` confirms this gap is "not a defect" — it inherits from shadcn/layout-container conventions). Build `Header.tsx`/`FlatSwitcher.tsx` fresh, following this app's existing glass-surface visual language (see Task 8) — don't search for a pre-existing header to "wire up," there isn't one.

### `usePatchFlat`'s optimistic-update pattern is a reference, not something to copy verbatim here

`client/src/features/settings/hooks/usePatchFlat.ts` shows the established `onMutate`/`cancelQueries`/`setQueryData`/rollback-on-error pattern for the `['settings']` cache. This story's mutations (`useSwitchActiveFlat`, `useCreateFlat`, `useDeleteFlat`) do **not** need the same optimistic-update complexity — a flat switch/create/delete is a heavier, less frequent, less latency-sensitive operation than a name edit, and the UX for all three already tolerates a brief loading state (dropdown closes immediately; the switched-to data streams in via normal `invalidateQueries` + refetch). Keep these three mutations plain (`onSuccess`-only invalidation), don't add `onMutate` optimistic logic — simpler is correct here, not a regression from `usePatchFlat`'s pattern.

### Project Structure Notes

- New files: `client/src/components/Header.tsx`, `client/src/components/FlatSwitcher.tsx`, `client/src/features/settings/components/AddFlatForm.tsx`, `client/src/features/settings/components/FlatDeleteConfirm.tsx`, `client/src/features/settings/hooks/useFlats.ts`, `client/src/features/settings/hooks/useSwitchActiveFlat.ts`, `client/src/features/settings/hooks/useCreateFlat.ts`, `client/src/features/settings/hooks/useDeleteFlat.ts`, plus co-located `.test.tsx`/`.test.ts` for each.
- Modified files: `client/src/components/AppShell.tsx` (mount `<Header />`), `client/src/features/settings/api/settingsApi.ts` (add `FlatSummary`/`getFlats`/`createFlat`/`deleteFlat`; change `updateUserSettings` signature), `client/src/features/settings/hooks/useUpdateLocale.ts` (adapt to new `updateUserSettings` signature), `client/src/features/settings/components/AccountSettings.tsx` (add Delete Flat row), `client/src/features/settings/schemas/settingsSchema.ts` (add `addFlatSchema`), `client/src/locales/{de-DE,en-US}/{common,settings}.json`.
- No changes to `api/`, `api.Tests/`, or any Story 5.1 backend file — this story is frontend-only, mirroring how Story 5.1 was backend-only for the same feature pair.
- Follows existing VSA layout: Flat CRUD hooks/API live under `client/src/features/settings/` (matches `FlatSettingsCard`/`FlatBaselineEdit`/`usePatchFlat` already being there — there is no separate `flats` feature folder in the architecture, and none should be created for this story). `Header`/`FlatSwitcher` live in `client/src/components/` (app-shell-level, cross-cutting, alongside `AppShell`/`SidebarNav`/`BottomTabBar`/`LocaleDropdown` — all of which already follow this "shared component depends on a feature hook" shape).

### Testing standards (frontend-only story)

- Test placement: co-located `.test.tsx` next to each new component/hook, per project convention. No backend tests needed (see "no backend work" note above).
- Component tests: wrap in `MemoryRouter` (needed for `FlatDeleteConfirm`'s no-navigation-needed assertions and any `NavLink`-adjacent rendering); mock `react-i18next`'s `useTranslation` to `(k) => k` exactly as `FlatSettingsCard.test.tsx` does; mock the relevant hooks (`useUserSettings`, `useFlats`, `useSwitchActiveFlat`, `useCreateFlat`, `useDeleteFlat`) at module scope via `vi.mock(...)`, not real TanStack Query network calls — matches every existing test in this codebase (`FlatSettingsCard.test.tsx` mocks `usePatchFlat` the same way).
- Key scenarios to cover: (1) `FlatSwitcher` — dropdown lists all flats, active flat visually/`aria-selected` distinguished, selecting a different flat calls the switch mutation with the correct `flatId`/`previousFlatId`/`locale`, selecting the already-active flat is a no-op; (2) `AddFlatForm` — preset click fills baseline field, custom typing clears preset selection, submit calls create then switch in order, error keeps sheet open; (3) `FlatDeleteConfirm` — Delete button disabled until exact case-sensitive match, enabled on exact match, calls delete mutation on click; (4) `useSwitchActiveFlat`/`useDeleteFlat` — assert the exact set of `invalidateQueries` calls (dashboard/readings/tariffs + settings, and flats for delete) fire on success and do **not** fire on error.
- Do not add a real E2E/integration test hitting `/api/v1/flats` — this codebase has no frontend integration test harness (per `project-context.md`'s "Known gaps" — no `npm test` in CI either); unit-level component/hook tests with mocked API modules are the ceiling here, consistent with every other frontend story to date.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-5-multi-flat-management-flat-structure.md#Story 5.2] — authoritative AC text.
- [Source: _bmad-output/planning-artifacts/epics/requirements-inventory.md#FR-19, #FR-20, #FR-23] — FR-19 (header switcher across all surfaces), FR-20 (last-active-Flat persistence), FR-23 (cascade delete, satisfied by Story 5.1's backend).
- [Source: _bmad-output/implementation-artifacts/5-1-multi-flat-backend-create-list-and-cascade-delete.md] — the four endpoints this story consumes; the `ActiveFlatId` soft-reference/fallback design; the tri-state PUT semantics for `activeFlatId` (omitted/null/value).
- [Source: api/Features/Settings/GetUserSettingsFunction.cs, UpdateUserSettingsFunction.cs] — verified fallback-to-first-owned-flat behavior and the required-`locale` validation this story's frontend must satisfy.
- [Source: api/Features/Flats/FlatModels.cs, GetFlatsFunction.cs, CreateFlatFunction.cs, DeleteFlatFunction.cs] — exact response/request shapes for `FlatSummary`/`CreateFlatRequest` this story's `settingsApi.ts` additions must mirror.
- [Source: _bmad-output/planning-artifacts/architecture.md#Project Structure, #Frontend Architecture (AD-16, AD-17, AD-19)] — `client/src/features/settings/` file-tree placement (`FlatDeleteConfirm.tsx` explicitly named there already); TanStack Query cache-key convention (`[resource, flatId, ...]`); no-global-store rule; react-hook-form + zod per slice.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/EXPERIENCE.md#lines 50, 109, 150, 182] — flat switcher interaction description ("tap the active Flat name in the header... dropdown reveals all Flats plus Add flat"); flat deletion type-to-confirm description.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/review-rubric.md#lines 55-59] — confirms no DESIGN.md visual spec exists for "flat switcher dropdown" or "flat deletion confirm"; both inherit from shadcn/layout-container conventions, "not a defect."
- [Source: client/src/components/LocaleDropdown.tsx] — the hand-rolled dropdown pattern (`useState`/`useRef`/document-listener close-on-outside-click) `FlatSwitcher.tsx` must replicate; no shadcn `DropdownMenu` is installed in this codebase.
- [Source: client/src/features/settings/components/FlatBaselineEdit.tsx, client/src/features/onboarding/components/OnboardingContract.tsx] — the preset-tile-grid + custom-value baseline UI pattern `AddFlatForm.tsx` must replicate verbatim (same four presets: 1500/2500/3500/4250 kWh).
- [Source: client/src/features/readings/components/EnterReadingCta.tsx, EnterReadingSheet.tsx] — the `Sheet`/`SheetTrigger` + controlled `open`/`onOpenChange` prop-drilling convention `AddFlatForm.tsx` must follow.
- [Source: client/src/features/settings/components/AccountSettings.tsx] — existing inline sign-out confirm pattern that `FlatDeleteConfirm`'s mount point in this file must mirror (mutually-exclusive local `show*Confirm` state, same destructive red styling tokens).
- [Source: client/src/features/settings/hooks/usePatchFlat.ts, useCompleteOnboarding.ts, useUpdateLocale.ts] — existing `['settings']`-invalidating mutation hooks; `usePatchFlat`'s optimistic-update pattern noted as a reference, not to be copied for this story's simpler mutations (see Dev Notes).
- [Source: client/src/App.tsx] — precedent for a root-level, non-feature component (`LocaleSync`) importing a `settings`-feature hook (`useUserSettings`), justifying `Header`/`FlatSwitcher`'s equivalent import.
- [Source: client/src/lib/apiClient.ts] — `apiClient.delete<T>(path)` already exists; no client-library change needed for `deleteFlat`.
- [Source: client/src/lib/i18n.ts] — confirms `common`/`settings` namespaces are already registered in the `ns: [...]` array; no i18n bootstrap change needed for this story's new keys.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md#Deferred from: code review of 2-1-i18n-infrastructure-and-locale-settings-api] — pre-existing deferred item "`queryKey: ['settings']` not user-scoped... revisit if multi-flat support is added" — this story does **not** need to fix that (single-user-per-session app; the deferred item is about multi-*account* cache pollution, not multi-flat, and remains out of scope here).
- [Source: _bmad-output/project-context.md#TanStack Query v5, #Mutations, #Frontend, #i18n] — `invalidateQueries({ queryKey })` partial-match gotcha (informs Task 3/5's explicit per-family invalidation list); mutation error-keeps-sheet-open rule; `mode: 'onBlur'`/`onTouched` form conventions; every new namespace requiring both `de-DE` and `en-US` files.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- Full frontend test suite: 210 passed, 0 failed (36 test files) — includes 42 new tests across the 8 new/modified test files for this story.
- `npx tsc -b`: clean, no errors.
- `npm run lint` (oxlint): clean for all changed files (pre-existing unrelated warnings in `src/router.tsx` only).

### Completion Notes List

- Extended `settingsApi.ts` with `FlatSummary`/`getFlats`/`CreateFlatBody`/`createFlat`/`deleteFlat`, and changed `updateUserSettings` to accept `{ locale, activeFlatId? }` instead of a bare `locale` string, updating its one caller (`useUpdateLocale.ts`) to match; `UserSettings` type left unchanged per Dev Notes (active flat is derived from `settings.flatId`, not a raw `activeFlatId` field).
- Added four new TanStack Query hooks (`useFlats`, `useSwitchActiveFlat`, `useCreateFlat`, `useDeleteFlat`) following the codebase's plain `onSuccess`-only invalidation convention (no optimistic updates, per Dev Notes on `usePatchFlat` not being a template here). `useSwitchActiveFlat`/`useDeleteFlat` invalidate the exact three `[resource, flatId]` families that exist today (`dashboard`, `readings`, `tariffs`) plus `['settings']` (and `['flats']` for delete).
- Built `FlatSwitcher.tsx` as a hand-rolled dropdown mirroring `LocaleDropdown.tsx`'s open/close-on-outside-click pattern. One implementation subtlety: the `Sheet` wrapping `AddFlatForm` had to be moved *outside* the `isOpen`-conditional dropdown markup (with only the `SheetTrigger` staying inside the conditional listbox) — otherwise closing the dropdown on flat-select would unmount the `Sheet`/`AddFlatForm` before the user could interact with it.
- Built `AddFlatForm.tsx` replicating `FlatBaselineEdit.tsx`'s preset-tile-grid pattern verbatim. Diverged slightly from the story's literal task-7 code sample: instead of calling `onOpenChange(false)` immediately inside `createFlat`'s `onSuccess`, the sheet stays open and swaps to an inline confirmation view showing the tariff-prompt text (dismissed via the `Sheet`'s built-in close (X) affordance) — this satisfies the Dev Notes' explicit prose requirement ("show a one-line confirmation prompt... rather than auto-navigating") which conflicts with the literal code sample's immediate-close instruction; the prose was treated as authoritative since it carries the actual AC intent.
- Built `FlatDeleteConfirm.tsx` with exact case-sensitive, untrimmed name matching gating the Delete button, mirroring `AccountSettings.tsx`'s existing sign-out destructive-confirm styling.
- Modified `AccountSettings.tsx` to call `useUserSettings()` and render a new "Delete Flat" row (guarded on `settings?.flatId && settings.flatName`), mutually exclusive with the existing sign-out confirm via separate local state.
- Added `Header.tsx` (new file) mounted in `AppShell.tsx` directly above `<Outlet />`, rendering on both mobile and desktop layouts per AC1.
- Added all i18n keys (`common.flatSwitcher`, `settings.account.deleteFlat`, `settings.addFlat`) to both `en-US` and `de-DE`; no `ns` array change needed since `common`/`settings` were already registered.
- No backend changes — story is 100% frontend, consuming the four endpoints shipped in Story 5.1, per Dev Notes.

### File List

**New:**
- `client/src/components/FlatSwitcher.tsx`
- `client/src/components/FlatSwitcher.test.tsx`
- `client/src/components/Header.tsx`
- `client/src/components/Header.test.tsx`
- `client/src/features/settings/components/AddFlatForm.tsx`
- `client/src/features/settings/components/AddFlatForm.test.tsx`
- `client/src/features/settings/components/FlatDeleteConfirm.tsx`
- `client/src/features/settings/components/FlatDeleteConfirm.test.tsx`
- `client/src/features/settings/hooks/useFlats.ts`
- `client/src/features/settings/hooks/useFlats.test.ts`
- `client/src/features/settings/hooks/useSwitchActiveFlat.ts`
- `client/src/features/settings/hooks/useSwitchActiveFlat.test.ts`
- `client/src/features/settings/hooks/useCreateFlat.ts`
- `client/src/features/settings/hooks/useCreateFlat.test.ts`
- `client/src/features/settings/hooks/useDeleteFlat.ts`
- `client/src/features/settings/hooks/useDeleteFlat.test.ts`

**Modified:**
- `client/src/components/AppShell.tsx`
- `client/src/features/settings/api/settingsApi.ts`
- `client/src/features/settings/hooks/useUpdateLocale.ts`
- `client/src/features/settings/components/AccountSettings.tsx`
- `client/src/features/settings/components/AccountSettings.test.tsx`
- `client/src/features/settings/schemas/settingsSchema.ts`
- `client/src/locales/en-US/common.json`
- `client/src/locales/de-DE/common.json`
- `client/src/locales/en-US/settings.json`
- `client/src/locales/de-DE/settings.json`
