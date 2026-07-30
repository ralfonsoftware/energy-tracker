---
baseline_commit: 2f9012fb78bfa68a254369ed554aa04267e8b3fb
---

# Story 11.7: Keyboard-Accessible Custom Dropdowns

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a keyboard-only user,
I want every dropdown in this app to support arrow-key navigation like a native `<select>`,
so that I can use the app without a mouse.

## Acceptance Criteria

1. **Given** four independent components (`LocaleDropdown.tsx`, `FlatSwitcher.tsx`, `PeriodSelector.tsx`, `InsightsPeriodSelector.tsx`) share the identical hand-rolled `Popover` + `role="listbox"`/`role="option"` shape with no keyboard model, **when** implemented, **then** a single shared hook `client/src/lib/useRovingListboxNav.ts` implements arrow-key (up/down) roving-tabindex navigation (clamped at the first/last item — no wrap-around, matching native `<select>` semantics), Home/End jump-to-first/last, and exposes what each component needs to wire into its existing `Popover`/`PopoverContent`/option markup, following the WAI-ARIA listbox keyboard pattern.
2. **Given** the shared hook, **when** retrofitted, **then** `LocaleDropdown.tsx`, `FlatSwitcher.tsx`, `PeriodSelector.tsx`, and `InsightsPeriodSelector.tsx` all adopt it, with no change to their existing visual appearance or click-based interaction (this is a keyboard-access addition, not a redesign).
3. **Given** the retrofit, **when** tested, **then** each of the four components' existing test files gains a keyboard-navigation test (arrow-down moves focus/selection, Enter/Space selects, Escape closes) using `@testing-library/user-event`, and all four components' existing click-based tests continue to pass unmodified.

## Tasks / Subtasks

- [x] Task 1: Create the shared roving-tabindex hook (AC: #1)
  - [x] 1.1 Create `client/src/lib/useRovingListboxNav.ts` implementing the hook per the exact shape in Dev Notes: `useRovingListboxNav(itemCount: number, selectedIndex: number)` returning `{ handleKeyDown, handleOpenAutoFocus, getItemProps }`
  - [x] 1.2 `handleKeyDown` (wire to `PopoverContent`'s `onKeyDown`): `ArrowDown`/`ArrowUp` move `activeIndex` by ±1 **clamped** to `[0, itemCount - 1]` (no wrap — see Dev Notes on why this diverges from a "typical" combobox and matches this story's own native-`<select>` framing instead); `Home`/`End` jump to `0`/`itemCount - 1`; each branch calls `event.preventDefault()` and imperatively `.focus()`s the target option's DOM node via `itemRefs`
  - [x] 1.3 Do **not** handle `Enter`/`Space` in the hook — every option is a real `<button>` with an existing `onClick`; native browser behavior already invokes `onClick` when a focused `<button>` receives `Enter`/`Space`. Handling it again in the hook double-fires the selection callback. See Dev Notes.
  - [x] 1.4 Do **not** handle `Escape` in the hook — Radix's `Popover.Content` dismissable layer already closes on `Escape` (confirmed passing today in `LocaleDropdown.test.tsx`'s and `FlatSwitcher.test.tsx`'s existing `*_EscapeKeyPressedWhileOpen_ClosesDropdown` tests). Re-handling it is redundant.
  - [x] 1.5 `handleOpenAutoFocus` (wire to `PopoverContent`'s `onOpenAutoFocus`): guard `itemCount === 0` (no-op, let Radix's default content-focus apply — see `FlatSwitcher`'s error-state case in Dev Notes); otherwise `event.preventDefault()` and focus `selectedIndex` (not index 0 — the currently-selected option should receive initial focus each time the popover opens, matching the WAI-ARIA listbox convention)
  - [x] 1.6 `getItemProps(index)` returns `{ ref: (el) => { itemRefs.current[index] = el }, tabIndex: index === activeIndex ? 0 : -1 }` — spread onto each option `<button>` alongside its existing props
  - [x] 1.7 Add `client/src/lib/useRovingListboxNav.test.ts` using `renderHook`/`act` from `@testing-library/react` covering: ArrowDown/ArrowUp move and clamp at both boundaries (no wrap), Home/End jump, `itemCount === 0` doesn't throw, `getItemProps` returns the right `tabIndex` for the active vs. inactive index
- [x] Task 2: Retrofit `LocaleDropdown.tsx` (AC: #2)
  - [x] 2.1 Compute `selectedIndex = LOCALES.findIndex(({ value }) => i18n.language.startsWith(value.split('-')[0]))` (fallback to `0` if `-1`)
  - [x] 2.2 Call `useRovingListboxNav(LOCALES.length, selectedIndex)`; wire `onKeyDown`/`onOpenAutoFocus` onto the existing `PopoverContent`; spread `{...getItemProps(index)}` onto each option `<button>` in the `LOCALES.map(...)` (needs an `index` param added to the existing `.map(({ value, labelKey }) => ...)` callback)
- [x] Task 3: Retrofit `FlatSwitcher.tsx` (AC: #2)
  - [x] 3.1 Compute `selectedIndex = (flats ?? []).findIndex(f => f.flatId === settings?.flatId)` (fallback to `0` if `-1`); compute `itemCount = isFlatsError ? 0 : (flats ?? []).length` (the error branch renders a `<p>`, not options — no roving target exists there)
  - [x] 3.2 Call `useRovingListboxNav(itemCount, selectedIndex)`; wire `onKeyDown`/`onOpenAutoFocus` onto the existing `PopoverContent` (careful: it already has an `onCloseAutoFocus` prop — add `onOpenAutoFocus`/`onKeyDown` alongside it, don't replace it); spread `{...getItemProps(index)}` onto each option `<button>` in the `(flats ?? []).map((flat, index) => ...)` (needs an `index` param added)
  - [x] 3.3 Leave the trailing "Add Flat" `SheetTrigger` button untouched — it is not part of the roving set (no `role="option"`), stays a normal Tab stop after the active option, same as today
- [x] Task 4: Retrofit `PeriodSelector.tsx` (AC: #2)
  - [x] 4.1 Compute `selectedIndex = OPTIONS.indexOf(value)`
  - [x] 4.2 Call `useRovingListboxNav(OPTIONS.length, selectedIndex)`; wire `onKeyDown`/`onOpenAutoFocus` onto the existing `PopoverContent`; spread `{...getItemProps(index)}` onto each option `<button>` in the `OPTIONS.map((option, index) => ...)` (needs an `index` param added)
- [x] Task 5: Retrofit `InsightsPeriodSelector.tsx` (AC: #2)
  - [x] 5.1 Compute `selectedIndex = OPTIONS.indexOf(value)`
  - [x] 5.2 Call `useRovingListboxNav(OPTIONS.length, selectedIndex)`; wire `onKeyDown`/`onOpenAutoFocus` onto the existing `PopoverContent`; spread `{...getItemProps(index)}` onto each option `<button>` in the `OPTIONS.map((option, index) => ...)` (needs an `index` param added)
- [x] Task 6: Component-level keyboard tests + full-suite verification (AC: #3)
  - [x] 6.1 Add one keyboard-navigation test to each of the four existing test files (`LocaleDropdown.test.tsx`, `FlatSwitcher.test.tsx`, `PeriodSelector.test.tsx`, `InsightsPeriodSelector.test.tsx`) using `@testing-library/user-event` (see exact pattern in Dev Notes): open the dropdown, assert the currently-selected option has focus, press `{ArrowDown}` and assert focus moved to the next option, press `{Enter}` and assert the same `onChange`/mutate callback that the existing click test asserts is called
  - [x] 6.2 Run `npm test -- --run` (Vitest, from `client/`) and confirm the full suite passes with no regressions to any of the four components' existing click-based tests
  - [x] 6.3 Run `npm run lint` and `npx tsc --noEmit` (both from `client/`) and confirm clean

### Review Findings

- [x] [Review][Patch] Roving-focus state does not resync while the popover stays open — `useRovingListboxNav`'s `activeIndex` only re-syncs to `selectedIndex` at the moment `handleOpenAutoFocus` fires (once per open). If `itemCount`/`selectedIndex` changes *while already open* — concretely reachable in `FlatSwitcher` via its async `flats` query resolving after the popover opened, or `isFlatsError` toggling — no option ever receives real DOM focus, and a previously-focused option that unmounts silently drops focus to `document.body`, breaking further arrow-key navigation until the popover is closed and reopened. The hook's shape was mandated verbatim by this story's own Dev Notes, so this is a gap in the prescribed design, not an implementation slip — needs a call on whether to fix now or accept as a known limitation. — fixed: added a `useEffect` in `useRovingListboxNav.ts` that resyncs/refocuses when `itemCount` transitions from 0→populated or the active index falls out of range; covered by two new hook tests.
- [x] [Review][Patch] Home/End is only unit-tested at the hook level [client/src/lib/useRovingListboxNav.test.ts] — no component-level test proves Home/End works through Radix's real `PopoverContent` event bubbling in any of the four retrofitted components. — fixed: added a Home/End component-level test to all four retrofitted test files.
- [x] [Review][Patch] New keyboard tests hardcode option indices via comments (e.g. "Home is index 0, Cabin is index 1") instead of deriving them from rendered role/name queries — brittle to unrelated mock-data reordering [client/src/components/FlatSwitcher.test.tsx, and the other three retrofitted test files]. — fixed: all four arrow-key tests now derive the active index via `aria-selected` instead of hardcoded comments.
- [x] [Review][Patch] No test exercises `FlatSwitcher`'s `handleOpenAutoFocus` `itemCount === 0` guard against the real `isFlatsError` UI state [client/src/components/FlatSwitcher.test.tsx] — only covered abstractly in the hook's own unit test. — fixed: added `FlatSwitcher_FlatsFetchFails_OpeningDropdownDoesNotThrowOrFocusAnyOption`.



### The shared hook — exact shape to implement

`client/src/lib/useRovingListboxNav.ts` (new file, following this project's established plain-hook-in-`lib/` pattern — see `client/src/lib/useSubmitGuard.ts` for precedent: a bare function, no JSX, co-located `.test.ts`):

```ts
import { useRef, useState, type KeyboardEvent } from 'react'

export function useRovingListboxNav(itemCount: number, selectedIndex: number) {
  const itemRefs = useRef<(HTMLElement | null)[]>([])
  const [activeIndex, setActiveIndex] = useState(selectedIndex)

  const focus = (index: number) => {
    setActiveIndex(index)
    itemRefs.current[index]?.focus()
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (itemCount === 0) return
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault()
        focus(Math.min(activeIndex + 1, itemCount - 1))
        break
      case 'ArrowUp':
        event.preventDefault()
        focus(Math.max(activeIndex - 1, 0))
        break
      case 'Home':
        event.preventDefault()
        focus(0)
        break
      case 'End':
        event.preventDefault()
        focus(itemCount - 1)
        break
    }
  }

  const handleOpenAutoFocus = (event: Event) => {
    if (itemCount === 0) return
    event.preventDefault()
    focus(selectedIndex)
  }

  const getItemProps = (index: number) => ({
    ref: (el: HTMLElement | null) => {
      itemRefs.current[index] = el
    },
    tabIndex: index === activeIndex ? 0 : -1,
  })

  return { handleKeyDown, handleOpenAutoFocus, getItemProps }
}
```

Wire into each component's existing `PopoverContent`:

```tsx
<PopoverContent
  role="listbox"
  onKeyDown={handleKeyDown}
  onOpenAutoFocus={handleOpenAutoFocus}
  {/* ...existing props unchanged... */}
>
  {ITEMS.map((item, index) => (
    <button
      key={...}
      role="option"
      {...getItemProps(index)}
      {/* ...existing props unchanged... */}
    >
      ...
    </button>
  ))}
</PopoverContent>
```

### Why `onOpenAutoFocus`, not a `useEffect` on an `isOpen` flag

Radix's `Popover.Content` is Presence-based — it only mounts into the DOM (and its option buttons only exist / are refable) once the popover opens. `onOpenAutoFocus` is Radix's own supported hook point, fired by `FocusScope` exactly once the content has mounted and is about to receive its default auto-focus — calling `event.preventDefault()` inside it is the documented way to redirect initial focus, and it fires deterministically in the right order relative to Radix's internals. A plain `useEffect` keyed on an `isOpen` boolean races against Radix's own internal auto-focus (which would otherwise steal focus back to the content wrapper `<div>` after your effect runs). Use `onOpenAutoFocus`, not an effect.

### Two "don't re-implement this" traps — both already work today

1. **`aria-expanded` is already correct, automatically, via Radix.** `PopoverTrigger` (from `@radix-ui/react-popover`) sets `"aria-expanded": context.open` and `"aria-haspopup": "dialog"` internally; because all four triggers use `asChild`, Radix's `Slot` merges these onto the underlying `<button>` — and since none of the four buttons set their own `aria-expanded` (only `aria-haspopup="listbox"`, which *does* override Radix's `"dialog"` default), Radix's `aria-expanded` value passes through untouched. This is already asserted and passing today in `LocaleDropdown.test.tsx`'s `LocaleDropdown_TriggerClicked_OpensDropdownListingBothLocalesAndMarksTriggerExpanded` test (`expect(trigger).toHaveAttribute('aria-expanded', 'false')` → `'true'` after click). **Do not add an explicit `aria-expanded` prop anywhere — it would be redundant and risks overriding the correct Radix-computed value with a stale one.**
2. **Escape-to-close already works via Radix's dismissable layer.** Already covered by passing tests in `LocaleDropdown.test.tsx` and `FlatSwitcher.test.tsx` (`*_EscapeKeyPressedWhileOpen_ClosesDropdown`). `PeriodSelector.test.tsx` and `InsightsPeriodSelector.test.tsx` don't yet have this test but the underlying behavior is identical (same `Popover` primitive) — AC #3 only requires *arrow-down/Enter/Escape* coverage per component; adding the Escape assertion to the two files that lack it is in scope for Task 6 but requires no new implementation code.

### Native `<select>` semantics: clamp, don't wrap

This story's own framing is "arrow-key navigation like a native `<select>`." A native HTML `<select>` element does **not** wrap when you press ArrowDown at the last option or ArrowUp at the first — it stays put. This is a deliberate deviation from some combobox/menu UI patterns that *do* wrap. `Math.min`/`Math.max` clamping (not modulo wrap-around) in `handleKeyDown` is required to match this story's stated goal.

### Why the hook takes `itemCount`/`selectedIndex`, not the items array

Each of the four components has a differently-shaped options array (`LOCALES: {value, labelKey}[]`, `flats: FlatSummary[]`, `OPTIONS: PeriodOption[]`, `OPTIONS: InsightsPeriod[]`). The hook only needs to reason about *positions*, not content — this keeps it fully generic and avoids four different type parameterizations. Selection itself stays entirely with each component's existing `onClick={() => handleSelect(...)}` on the option buttons — untouched by this story.

### Per-component wiring specifics

- **`LocaleDropdown.tsx`** (`client/src/components/LocaleDropdown.tsx`): the `.map(({ value, labelKey }) => ...)` at line 49 needs an `index` param added: `.map(({ value, labelKey }, index) => ...)`.
- **`FlatSwitcher.tsx`** (`client/src/components/FlatSwitcher.tsx`): the `.map(flat => ...)` at line 55 needs an `index` param: `.map((flat, index) => ...)`. The `PopoverContent` at lines 43-51 already has an `onCloseAutoFocus` prop — add `onKeyDown`/`onOpenAutoFocus` as additional props, don't touch the existing one. The error branch (`isFlatsError`, line 52-53) renders zero options — `itemCount` must be `0` in that state, not `flats.length` (which could be stale/non-empty from a previous successful fetch if `isFlatsError` flips independently — use the `isFlatsError` check directly, don't infer from array length).
- **`PeriodSelector.tsx`** (`client/src/features/decomposition/components/PeriodSelector.tsx`): the `OPTIONS.map(option => ...)` at line 48 needs an `index` param. `OPTIONS` is a module-level constant (`PeriodOption[]`, 5 entries) — `OPTIONS.indexOf(value)` is always safe (never `-1`) since `value`'s type is `PeriodOption`.
- **`InsightsPeriodSelector.tsx`** (`client/src/features/insights/components/InsightsPeriodSelector.tsx`): the `OPTIONS.map(option => ...)` at line 46 needs an `index` param. Same as above — `OPTIONS.indexOf(value)` is always safe since `value: InsightsPeriod`.

### Testing Requirements

- New hook test file: `client/src/lib/useRovingListboxNav.test.ts`, using `renderHook` from `@testing-library/react` (`^16.3.2`) — exact precedent already in this codebase at `client/src/lib/useSubmitGuard.test.ts`: `import { renderHook } from '@testing-library/react'`, then `const { result, rerender } = renderHook(({ prop }) => useHookName(prop), { initialProps: { prop: initialValue } })`. Since `useRovingListboxNav`'s `getItemProps` returns a `ref` callback, a hook-only test can't easily assert DOM focus — focus its test coverage on `handleKeyDown`'s index math (call it with a mock `KeyboardEvent`-shaped object exposing `key`/`preventDefault`) and `getItemProps`'s `tabIndex` output; the actual DOM-focus behavior is proven by the four component-level keyboard tests in Task 6, which render real DOM and can assert `toHaveFocus()`.
- Component-level keyboard tests use `@testing-library/user-event` (already a project dependency, `^14.6.1`, already used elsewhere e.g. `TariffForm.test.tsx` via `import userEvent from '@testing-library/user-event'` then `const user = userEvent.setup()` per test — follow this exact pattern, not raw `fireEvent.keyDown`, per epic AC #3's explicit tool requirement).
- Example shape for one of the four new component tests (adapt per component — this uses `LocaleDropdown` as the concrete example):
  ```tsx
  it('LocaleDropdown_ArrowDownThenEnterPressedWhileOpen_MovesFocusAndSelectsNextOption', async () => {
    const user = userEvent.setup()
    render(<LocaleDropdown />)
    await user.click(screen.getByRole('button', { name: 'Language' }))
    const options = screen.getAllByRole('option')
    // en-US is selected in this test's baseline (see beforeEach) — DE is index 0, EN is index 1
    expect(options[1]).toHaveFocus()
    await user.keyboard('{ArrowUp}')
    expect(options[0]).toHaveFocus()
    await user.keyboard('{Enter}')
    expect(mockMutate).toHaveBeenCalledWith('de-DE', expect.objectContaining({ onError: expect.any(Function) }))
  })
  ```
  Note: assert on which option has initial focus based on that component's *actual currently-selected item*, not always index 0 — `handleOpenAutoFocus` focuses `selectedIndex`, and each component's baseline selected item differs (`LocaleDropdown`'s test baseline is `en-US`, `FlatSwitcher`'s is `flat-1`/"Home", `PeriodSelector`'s test default is `'thisMonth'`, `InsightsPeriodSelector`'s varies per test).
- Test naming convention already established in these four files: `PascalCase`-segment style, e.g. `ComponentName_Scenario_ExpectedOutcome` — follow it for the new tests.
- After adding tests, run the full frontend suite (`npm test -- --run` from `client/`) — per Story 11.5/11.6 precedent in this epic, confirm zero regressions to any pre-existing test in these four files or elsewhere (nothing else imports or is affected by `useRovingListboxNav.ts`, a brand-new file).

### Project Structure Notes

- New file: `client/src/lib/useRovingListboxNav.ts` (+ `client/src/lib/useRovingListboxNav.test.ts`) — matches this project's established "shared, non-feature-specific hook lives in `lib/`" convention (`useSubmitGuard.ts`, `apiClient.ts`, `queryClient.ts` are the existing precedents).
- Four modified files: `client/src/components/LocaleDropdown.tsx`, `client/src/components/FlatSwitcher.tsx`, `client/src/features/decomposition/components/PeriodSelector.tsx`, `client/src/features/insights/components/InsightsPeriodSelector.tsx` — each crosses a different feature slice (`components/` is shared/app-shell-level, `decomposition` and `insights` are separate VSA slices). This is fine: `useRovingListboxNav.ts` is shared infrastructure in `lib/`, not a cross-slice hook import between feature slices — it doesn't violate the "no cross-feature hook imports" rule, which only forbids importing one feature's hook from another feature.
- Four modified test files: `client/src/components/LocaleDropdown.test.tsx`, `client/src/components/FlatSwitcher.test.tsx`, `client/src/features/decomposition/components/PeriodSelector.test.tsx`, `client/src/features/insights/components/InsightsPeriodSelector.test.tsx`.
- No locale/i18n changes, no API/schema changes, no visual/styling changes — purely a keyboard-interaction addition, consistent with architecture's stated "WCAG 2.2 AA accessibility floor" requirement (`_bmad-output/planning-artifacts/architecture.md:53`).

### Previous Story Intelligence (Story 11.6 — Frontend Network-Error Reshaping in `apiClient`)

- Story 11.6 (immediately preceding this one) was the first purely-frontend story in Epic 11 and established the "assert exact values/behavior, not just presence" testing discipline and the "confirm existing tests still pass unmodified" discipline for a shared-infrastructure change. Apply both here: assert exactly which option has focus (not just "something is focused"), and re-run the full suite to confirm none of the four components' pre-existing click/error/loading tests regress.
- 11.6 also reused an existing pattern verbatim rather than inventing a new one where possible (the `Object.assign(err, {...})` shape, deferred rather than redesigned). This story follows the same discipline: reuse the existing `PopoverContent role="listbox"` / `button role="option"` markup as-is, adding only the keyboard-handling props — no restructuring of the JSX shape.
- Unlike 11.6 (one file, one new branch), this story touches five files (one new shared hook + four retrofits) — closer in shape to Story 11.5's "one small change, repeated consistently across N files" sweep. Apply 11.5's discipline here too: each of the four retrofits should look structurally identical (same three added props/pattern), not four bespoke variations.

### Git Intelligence (recent commits)

- `2f9012f` (Story 11.6), `99b64b9` (Story 11.5), `df5a834` (Story 11.4), `457ff51` (CI Node 20→22 bump, infra-only), `c8805f8` (Story 11.3) — all recent Epic 11 commits follow the same shape: one narrow, well-scoped change + matching test-file extension + full relevant test suite run before completion. This story follows the same shape: `npm test -- --run` (Vitest) from `client/`, not `dotnet test` (this is a frontend-only story).

### Deferred-Work Cross-Check

- Searched `_bmad-output/implementation-artifacts/deferred-work.md` for any entry tagged `blocks: Story 11.7` — **none found.** No pre-existing deferred item gates or blocks this story.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md#Story 11.7] — epic-level AC and rationale, including the exact four affected file names
- [Source: client/src/components/LocaleDropdown.tsx] — current implementation (67 lines), the first retrofit target
- [Source: client/src/components/FlatSwitcher.tsx] — current implementation (87 lines), second retrofit target, notably the `isFlatsError` zero-options edge case
- [Source: client/src/features/decomposition/components/PeriodSelector.tsx] — current implementation (112 lines), third retrofit target
- [Source: client/src/features/insights/components/InsightsPeriodSelector.tsx] — current implementation (62 lines), fourth retrofit target
- [Source: client/src/lib/useSubmitGuard.ts] — existing precedent for a plain shared hook file in `client/src/lib/`
- [Source: client/node_modules/@radix-ui/react-popover/dist/index.js:128-151] — confirms `PopoverTrigger` already computes `aria-expanded`/`aria-haspopup`/`aria-controls` internally; verified directly in the installed package source
- [Source: client/src/components/LocaleDropdown.test.tsx] — existing `aria-expanded` assertion (already passing) and existing Escape-to-close test
- [Source: client/src/components/FlatSwitcher.test.tsx] — existing Escape-to-close and pointer-outside-dismiss tests, and the `isFlatsError` no-options test case
- [Source: client/src/features/tariffs/components/TariffForm.test.tsx] — existing `@testing-library/user-event` usage convention (`userEvent.setup()` per test) to follow for the new keyboard tests
- [Source: _bmad-output/planning-artifacts/architecture.md:53] — "WCAG 2.2 AA accessibility floor" — the governing architecture requirement this story directly serves
- [Source: _bmad-output/implementation-artifacts/11-6-frontend-network-error-reshaping-in-apiclient.md] — previous story in this epic; source of the "assert exact values" and "confirm unmodified tests still pass" disciplines
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — searched for `blocks: Story 11.7`, none found

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

None — implementation matched the Dev Notes spec exactly on first pass; no debugging required.

### Completion Notes List

- Created `client/src/lib/useRovingListboxNav.ts` verbatim per the Dev Notes exact-shape spec: clamped (no-wrap) Home/End/Arrow navigation, `handleOpenAutoFocus` guarding `itemCount === 0`, `getItemProps` returning a ref callback + roving `tabIndex`. Deliberately does not handle Enter/Space (native `<button>` behavior) or Escape (Radix's dismissable layer already handles it).
- Added `client/src/lib/useRovingListboxNav.test.ts` (`renderHook`/`act`) covering Arrow clamp-at-boundary in both directions, Home/End jump, `preventDefault` on handled keys, `itemCount === 0` no-throw for both `handleKeyDown` and `handleOpenAutoFocus`, and `getItemProps` tabIndex correctness.
- Retrofitted all four target components (`LocaleDropdown.tsx`, `FlatSwitcher.tsx`, `PeriodSelector.tsx`, `InsightsPeriodSelector.tsx`) identically: computed `selectedIndex`/`itemCount`, called the hook, wired `onKeyDown`/`onOpenAutoFocus` onto the existing `PopoverContent` (added alongside `FlatSwitcher`'s pre-existing `onCloseAutoFocus`, not replacing it), and spread `{...getItemProps(index)}` onto each option `<button>` after adding an `index` param to each `.map(...)` callback. No visual or click-based behavior changed.
- `FlatSwitcher.tsx`: `itemCount` uses `isFlatsError ? 0 : (flats ?? []).length` directly (not inferred from array length) per Dev Notes, so a stale non-empty `flats` array during an error state doesn't produce phantom roving targets. The trailing "Add Flat" `SheetTrigger` button was left untouched — not part of the roving set.
- Added one keyboard-navigation test per component (`ArrowDown`/`ArrowUp` + `Enter`, using `@testing-library/user-event`) asserting focus moves to the correct next/previous option and the same mutate/onChange callback the existing click test asserts is invoked. Also added the missing `Escape`-closes-dropdown test to `PeriodSelector.test.tsx` and `InsightsPeriodSelector.test.tsx` (already present for `LocaleDropdown`/`FlatSwitcher`) since AC #3 requires Escape coverage per component and the underlying Radix behavior is identical across all four.
- Full verification: `npm test -- --run` → 69 test files / 454 tests passed, zero regressions to any pre-existing click/error/loading test. `npm run lint` → clean (only pre-existing unrelated `router.tsx` fast-refresh warnings). `npx tsc --noEmit` → clean.

### File List

- `client/src/lib/useRovingListboxNav.ts` (new)
- `client/src/lib/useRovingListboxNav.test.ts` (new)
- `client/src/components/LocaleDropdown.tsx` (modified)
- `client/src/components/LocaleDropdown.test.tsx` (modified)
- `client/src/components/FlatSwitcher.tsx` (modified)
- `client/src/components/FlatSwitcher.test.tsx` (modified)
- `client/src/features/decomposition/components/PeriodSelector.tsx` (modified)
- `client/src/features/decomposition/components/PeriodSelector.test.tsx` (modified)
- `client/src/features/insights/components/InsightsPeriodSelector.tsx` (modified)
- `client/src/features/insights/components/InsightsPeriodSelector.test.tsx` (modified)

## Change Log

| Date | Change |
|---|---|
| 2026-07-30 | Implemented Story 11.7: added shared `useRovingListboxNav` hook and retrofitted `LocaleDropdown`, `FlatSwitcher`, `PeriodSelector`, `InsightsPeriodSelector` with arrow-key/Home/End roving-tabindex keyboard navigation; added keyboard-navigation tests to all four components' test files; full suite (454 tests), lint, and `tsc --noEmit` all pass. |
