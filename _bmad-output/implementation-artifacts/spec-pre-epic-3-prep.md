---
title: 'Pre-Epic 3 Prep: LocaleDropdown extraction, parseLocaleNumber utility, flat name trim fix'
type: 'refactor'
created: '2026-06-30'
status: 'done'
baseline_commit: '4f580d67b8896b432f4adf78f4509f2c60af8032'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Three locale dropdown implementations are hand-rolled identically across onboarding screens, all missing `aria-haspopup`, keyboard navigation, and touch dismiss. `parseLocaleNumber` is copy-pasted in two files and will be needed again in Story 3.4. `CompleteOnboardingFunction` stores the flat name without trimming, unlike `PatchFlatFunction` which does trim.

**Approach:** Extract a shared `LocaleDropdown` pill component (used in 3 onboarding screens); update `LocaleSettings` in-place with the same a11y improvements; extract `parseLocaleNumber` to `client/src/lib/localeNumber.ts` with unit tests; add `.Trim()` to `body.FlatName` in `CompleteOnboardingFunction`.

## Boundaries & Constraints

**Always:**
- `LocaleDropdown` must preserve the rollback behaviour on locale-update failure: optimistic `i18n.changeLanguage(value)`, then `i18n.changeLanguage(prev)` in `onError`
- `parseLocaleNumber` signature stays `(value: string, locale: string): number` — callers in OnboardingContract and FlatBaselineEdit must update their import only, zero logic change
- Vitest globals:true — `describe`/`it`/`expect` are global, do NOT import from vitest
- Trim fix must only add `.Trim()` to `body.FlatName` — no other changes to `CompleteOnboardingFunction`

**Ask First:**
- If `LocaleDropdown` keyboard navigation (Arrow keys) requires focus management that conflicts with the surrounding `<form>` element

**Never:**
- Add a `variant` prop to `LocaleDropdown` — `LocaleSettings` keeps its row layout and is updated in-place
- Change the visual styling of any dropdown (no colour, spacing, or shadow changes)
- Convert `parseLocaleNumber` to an i18n-library-based implementation — keep the explicit DE/EN string-replace logic

</frozen-after-approval>

## Code Map

- `client/src/features/onboarding/components/OnboardingIntro.tsx` — has inline locale pill (no dimming); replace with `<LocaleDropdown />`
- `client/src/features/onboarding/components/OnboardingFlatName.tsx` — has inline locale pill (opacity 0.7); replace with `<LocaleDropdown dimmed />`
- `client/src/features/onboarding/components/OnboardingContract.tsx` — has inline locale pill (opacity 0.7) + `parseLocaleNumber` definition; replace both
- `client/src/features/settings/components/LocaleSettings.tsx` — has locale row item; update in-place to add `aria-haspopup`, keyboard nav, touchstart dismiss
- `client/src/features/settings/components/FlatBaselineEdit.tsx` — has `parseLocaleNumber` definition; remove and import from lib
- `client/src/components/LocaleDropdown.tsx` — new file; pill locale dropdown component
- `client/src/lib/localeNumber.ts` — new file; shared `parseLocaleNumber` utility
- `client/src/lib/localeNumber.test.ts` — new file; unit tests for `parseLocaleNumber`
- `api/Features/Onboarding/CompleteOnboardingFunction.cs:44–51` — flat entity creation; `Name = body.FlatName` → `Name = body.FlatName.Trim()`

## Tasks & Acceptance

**Execution:**
- [x] `client/src/lib/localeNumber.ts` -- CREATE: export `parseLocaleNumber(value: string, locale: string): number` with the exact DE/EN logic from OnboardingContract -- consolidates both copies into one canonical implementation
- [x] `client/src/lib/localeNumber.test.ts` -- CREATE: unit tests covering the I/O matrix below -- verifies edge cases before callers are updated
- [x] `client/src/features/onboarding/components/OnboardingContract.tsx` -- REMOVE the inline `parseLocaleNumber` function; import from `@/lib/localeNumber` -- first caller to use the shared utility
- [x] `client/src/features/settings/components/FlatBaselineEdit.tsx` -- REMOVE the inline `parseLocaleNumber` function; import from `@/lib/localeNumber` -- second caller
- [x] `client/src/components/LocaleDropdown.tsx` -- CREATE: pill locale dropdown component with `dimmed?: boolean` prop; includes `aria-haspopup="listbox"`, `role="listbox"` on popover, Escape key closes, touchstart dismiss alongside mousedown -- replaces three identical inline implementations
- [x] `client/src/features/onboarding/components/OnboardingIntro.tsx` -- REMOVE inline locale state/ref/effect/render; replace with `<LocaleDropdown />` at same DOM position (absolute top-4 right-4 z-20) -- first consumer of shared component
- [x] `client/src/features/onboarding/components/OnboardingFlatName.tsx` -- REMOVE inline locale state/ref/effect/render; replace with `<LocaleDropdown dimmed />` at same position (plus existing `style={{ opacity: 0.7 }}` wrapper) -- second consumer
- [x] `client/src/features/onboarding/components/OnboardingContract.tsx` -- REMOVE inline locale state/ref/effect/render; replace with `<LocaleDropdown dimmed />` at same position -- third consumer; also apply the parseLocaleNumber import from above task
- [x] `client/src/features/settings/components/LocaleSettings.tsx` -- UPDATE in-place: add `aria-haspopup="listbox"` to trigger button, `role="listbox"` on popover div, `touchstart` alongside `mousedown` in dismiss handler, Escape key closes via `keydown` listener on document -- keeps row layout unchanged
- [x] `api/Features/Onboarding/CompleteOnboardingFunction.cs` -- CHANGE line 47: `Name = body.FlatName` → `Name = body.FlatName.Trim()` -- matches PatchFlatFunction behaviour

**Acceptance Criteria:**
- Given `<LocaleDropdown />` rendered in OnboardingIntro, when Escape key is pressed while open, then dropdown closes
- Given `<LocaleDropdown />` rendered anywhere, when user taps outside on a touch device (touchstart outside), then dropdown closes
- Given the locale update API call fails, when `onError` fires, then `i18n.language` is reverted to previous value (rollback preserved)
- Given `parseLocaleNumber` imported from `client/src/lib/localeNumber.ts`, when called with same arguments as before, then all existing form parsing behaviour is identical
- Given `POST /api/v1/onboarding` with `flatName: "  My Flat  "`, when saved, then `Flat.Name` in DB is `"My Flat"` (trimmed)
- Given `OnboardingContract.tsx` and `FlatBaselineEdit.tsx`, when reviewed, then neither contains a local `parseLocaleNumber` definition
- Given OnboardingIntro, OnboardingFlatName, and OnboardingContract, when reviewed, then none contains inline locale dropdown state, ref, or useEffect

## Spec Change Log

## Design Notes

**LocaleDropdown component structure:**
```tsx
interface LocaleDropdownProps { dimmed?: boolean }

export function LocaleDropdown({ dimmed = false }: LocaleDropdownProps) {
  const [isOpen, setIsOpen] = useState(false)
  const dropdownRef = useRef<HTMLDivElement>(null)
  // mousedown + touchstart + Escape via single useEffect
  // locale options from t('locale.de') / t('locale.en')
  // uses useTranslation('onboarding') — same namespace as callers
}
```
The wrapper `div` gets `style={{ opacity: 0.7 }}` when `dimmed` is true. The `useTranslation` namespace must be `'onboarding'` because the locale key (`locale.de`, `locale.en`, `locale.label`) lives in the onboarding namespace.

**a11y additions — exact attributes:**
- Trigger button: add `aria-haspopup="listbox"` (already has `aria-expanded` and `aria-label`)
- Popover container: add `role="listbox"`
- Each option button: add `role="option"`, `aria-selected={isCurrentLocale}`

## Suggested Review Order

**Shared LocaleDropdown component**

- New pill component: self-contained state, rollback, a11y, keyboard+touch dismiss
  [`LocaleDropdown.tsx:15`](../../client/src/components/LocaleDropdown.tsx#L15)

- Rollback pattern: `prev` captured before optimistic change, restored in `onError`
  [`LocaleDropdown.tsx:40`](../../client/src/components/LocaleDropdown.tsx#L40)

- `aria-haspopup`, `aria-expanded`, `role="listbox"`, `role="option"`, `aria-selected` wiring
  [`LocaleDropdown.tsx:54`](../../client/src/components/LocaleDropdown.tsx#L54)

- Intro screen (non-dimmed): inline state → `<LocaleDropdown />`
  [`OnboardingIntro.tsx:16`](../../client/src/features/onboarding/components/OnboardingIntro.tsx#L16)

- FlatName screen (dimmed): inline state → `<LocaleDropdown dimmed />`
  [`OnboardingFlatName.tsx:39`](../../client/src/features/onboarding/components/OnboardingFlatName.tsx#L39)

- Contract screen (dimmed, also adopts shared parseLocaleNumber): `<LocaleDropdown dimmed />`
  [`OnboardingContract.tsx:180`](../../client/src/features/onboarding/components/OnboardingContract.tsx#L180)

**LocaleSettings a11y (in-place update)**

- touchstart + Escape keydown added alongside existing mousedown dismiss
  [`LocaleSettings.tsx:19`](../../client/src/features/settings/components/LocaleSettings.tsx#L19)

- `aria-haspopup`, `role="listbox"`, `role="option"`, `aria-selected` added to row variant
  [`LocaleSettings.tsx:41`](../../client/src/features/settings/components/LocaleSettings.tsx#L41)

**parseLocaleNumber extraction**

- Canonical implementation: DE (strip `.` thousands, `,`→`.`) and EN (strip `,` thousands)
  [`localeNumber.ts:1`](../../client/src/lib/localeNumber.ts#L1)

- Contract: inline function removed, import added; all 6 call sites unchanged
  [`OnboardingContract.tsx:6`](../../client/src/features/onboarding/components/OnboardingContract.tsx#L6)

- BaselineEdit: inline function removed, import added; 2 call sites unchanged
  [`FlatBaselineEdit.tsx:7`](../../client/src/features/settings/components/FlatBaselineEdit.tsx#L7)

**Backend flat name trim**

- One-line fix matching `PatchFlatFunction` behaviour; validator guards against null before this line
  [`CompleteOnboardingFunction.cs:47`](../../api/Features/Onboarding/CompleteOnboardingFunction.cs#L47)

**Tests**

- 9 edge cases: DE decimal, DE thousands, DE combined, EN decimal, EN thousands, EN combined, invalid, empty, zero
  [`localeNumber.test.ts:1`](../../client/src/lib/localeNumber.test.ts#L1)

## Verification

**Commands:**
- `cd client && npm test -- --run localeNumber` -- expected: all I/O matrix cases pass
- `cd client && npm test -- --run` -- expected: full suite green (no regressions in onboarding/settings tests)
- `cd api && dotnet test` -- expected: all backend tests pass

| Scenario | Input (`value`, `locale`) | Expected |
|----------|--------------------------|----------|
| DE decimal comma | `"1,27"`, `"de-DE"` | `1.27` |
| DE thousands dot | `"1.500"`, `"de-DE"` | `1500` |
| DE combined | `"1.500,27"`, `"de-DE"` | `1500.27` |
| EN decimal dot | `"1.27"`, `"en-US"` | `1.27` |
| EN thousands comma | `"1,500"`, `"en-US"` | `1500` |
| EN combined | `"1,500.27"`, `"en-US"` | `1500.27` |
| Invalid string | `"abc"`, `"en-US"` | `NaN` |
| Empty string | `""`, `"en-US"` | `NaN` |
| Zero | `"0"`, `"de-DE"` | `0` |
