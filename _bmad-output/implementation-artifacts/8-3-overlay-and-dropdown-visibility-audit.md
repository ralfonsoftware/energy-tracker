---
baseline_commit: 0ff5793c62c36f6887f8ed574daf247ec4cd5f7e
---

# Story 8.3: Overlay & Dropdown Visibility Audit

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want every dropdown and overlay in the app — the language switcher, the flat switcher, the Decomposition period selector — to render as a clean, fully visible floating panel,
so that I can always see and select the option I need, on every screen.

## Acceptance Criteria

1. **Given a gap found during story creation — the epic's stated root cause does not match current code.** The epic text (and its AC1) assumes `LocaleDropdown.tsx`/`popover.tsx` currently render "partially hidden/clipped." This is stale: `LocaleDropdown.tsx` was already migrated onto the shared `Popover`/`PopoverTrigger`/`PopoverContent` (`client/src/components/ui/popover.tsx`) in commit `cd742e4` — proactively, before ever reproducing the clipping bug — specifically because `FlatSwitcher.tsx`'s identical hand-rolled `position: absolute` pattern was found clipped by an ancestor `backdrop-filter` in commit `127731b` (see `_bmad-output/implementation-artifacts/investigations/flat-switcher-overlay-clipped-investigation.md` and `deferred-work.md:278-280`). **When** this story is implemented, **then** no changes are made to `client/src/components/ui/popover.tsx` itself — it already wraps `PopoverContent` in Radix `Portal` with no `container` override (confirmed via full-repo grep for `container=`), so it already portals to `document.body` and escapes any ancestor `backdrop-filter`/`filter`/`transform` clipping. There is no shared-component bug to fix here; do not "fix" `popover.tsx` speculatively.

2. **Given a gap found during story creation — the actual unfixed clipping bug lives in a different component the prior fix pass never touched.** `client/src/features/settings/components/LocaleSettings.tsx` (the Settings page's own language row — a **different component** from `client/src/components/LocaleDropdown.tsx`, which is only used in onboarding) is still a hand-rolled `position: absolute` dropdown (`LocaleSettings.tsx:52-53`, `absolute right-4 top-full mt-1 ... z-10`), nested inside `SettingsRoot.tsx`'s `cardStyle` wrapper (`SettingsRoot.tsx:11-16`, `backdropFilter: 'blur(20px)'`) at `SettingsRoot.tsx:58`. This reproduces the exact same clipping mechanism documented for `FlatSwitcher.tsx`'s original bug: WebKit clips absolutely-positioned descendant content to the bounding box of an ancestor with `backdrop-filter`. **When** this story is implemented, **then** `LocaleSettings.tsx` is migrated onto the same `Popover`/`PopoverTrigger`/`PopoverContent` pattern already used by `LocaleDropdown.tsx`/`FlatSwitcher.tsx`/`CostGapBadge.tsx`, so its dropdown panel portals to `document.body` and is no longer a DOM descendant of `SettingsRoot.tsx`'s `backdrop-filter` card — fixing the actual, currently-reproducible clipping bug in this app.

3. **Given every other Popover-based dropdown in the app**, **when** each is audited during this story: `FlatSwitcher.tsx`, `PeriodSelector.tsx` (`features/decomposition/components/`), and `CostGapBadge.tsx` (`features/dashboard/components/`) — **then** each is confirmed (by code inspection: each wraps its dropdown content in the shared `PopoverContent`, which portals via `PopoverPrimitive.Portal` with no `container` override) to already render correctly, unclipped, regardless of scroll position or trigger placement — no code changes needed to any of them. A full-repo grep (`grep -rln "top-full\|position: 'absolute'\|position: \"absolute\""`) confirms `LocaleSettings.tsx` is the **only** remaining hand-rolled, non-portaled dropdown in the codebase — this audit's scope is complete once it is migrated; do not go looking for further instances beyond it.

4. **Given this is a visibility regression rather than new functionality**, **when** the fix lands, **then** a regression test is added — not at `popover.tsx` (nothing is broken there per AC1), but at `LocaleSettings.tsx` (the actual level the bug is rooted in, mirroring AC3's own "lowest shared level" principle applied to where the real defect lives): a new `LocaleSettings.test.tsx` asserts that when the dropdown is open, its `role="listbox"` panel is **not** a DOM descendant of the `SettingsRoot`-supplied `backdrop-filter` card wrapper — proving it escaped via Portal — so a future regression back to a hand-rolled `absolute` div (which would reintroduce the clipping bug) fails this test immediately.

5. **Given `LocaleSettings.tsx` currently manages its own open/close state via a `useRef` + manual `mousedown`/`touchstart`/`keydown` document-listener `useEffect`** (`LocaleSettings.tsx:15,19-34`), **when** it is migrated to `Popover`, **then** this manual outside-click/Escape-dismiss logic is deleted entirely and replaced by Radix Popover's built-in `open`/`onOpenChange` controlled state (the same pattern `LocaleDropdown.tsx` already uses) — no dual dismiss-handling logic, no leftover dead code.

6. **Given the currently-visible row content and behavior** (`t('locale.title')` label, current-language display, `›` chevron, the two-option `de-DE`/`en-US` list, `updateLocale` mutation call with `onError` i18n-rollback), **when** this story is implemented, **then** none of that user-visible text, i18n keys, or selection/mutation behavior changes — this story is a rendering-mechanism fix only (hand-rolled `absolute` div → `Popover`), not a redesign or behavior change. No new i18n keys are introduced.

## Tasks / Subtasks

- [x] Task 1: Migrate `LocaleSettings.tsx` to the shared `Popover` pattern (AC: 2, 5, 6)
  - [x] Import `Popover`, `PopoverTrigger`, `PopoverContent` from `@/components/ui/popover` (already the import path used by `LocaleDropdown.tsx`/`FlatSwitcher.tsx`).
  - [x] Replace the outer `<div className="relative" ref={dropdownRef}>` with `<Popover open={isOpen} onOpenChange={setIsOpen}>`; wrap the existing trigger `<button>` in `<PopoverTrigger asChild>` (the button's current content/classes/`aria-haspopup="listbox"` stay unchanged; remove the manually-set `aria-expanded={isOpen}` — Radix's `Trigger` injects this itself via `asChild` cloning, matching how `LocaleDropdown.tsx`/`FlatSwitcher.tsx` already leave it to Radix rather than setting it manually).
  - [x] Replace the `{isOpen && (<div className="absolute right-4 top-full mt-1 ...">...)}` block with `<PopoverContent role="listbox" align="end" sideOffset={4} className="w-auto min-w-[120px] p-0 rounded-xl overflow-hidden" style={{ background: 'rgba(30,30,50,0.95)', backdropFilter: 'blur(20px)', border: '1px solid rgba(255,255,255,0.15)' }}>` — keep the exact same visual styling values (background color, blur, border, `min-w-[120px]`, `rounded-xl`) currently on the hand-rolled div, just moved onto `PopoverContent`; `align="end"` matches the previous `right-4` anchoring.
  - [x] Keep the `LOCALES.map(...)` option-button rendering exactly as-is inside `PopoverContent` (same `role="option"`, `aria-selected`, `onClick` handler calling `i18n.changeLanguage`/`updateLocale`/`setIsOpen(false)`).
  - [x] Delete the `useRef`, the `dropdownRef`, and the entire outside-click/Escape `useEffect` block (`LocaleSettings.tsx:15,19-34`) — Radix Popover's own outside-dismiss/Escape handling (already relied upon by `LocaleDropdown.tsx`) replaces it.
  - [x] Remove the now-unused `useRef` import if nothing else in the file needs it.

- [x] Task 2: Confirm the other Popover consumers need no changes (AC: 1, 3)
  - [x] Re-read `client/src/components/ui/popover.tsx`, `client/src/components/FlatSwitcher.tsx`, `client/src/features/decomposition/components/PeriodSelector.tsx`, `client/src/features/dashboard/components/CostGapBadge.tsx` — confirm each still uses `PopoverContent` (Portal-based) with no `container` override, no ancestor rendering change since this story's investigation. Do not modify these files.

- [x] Task 3: Add the regression test proving Portal escapes the `backdrop-filter` ancestor (AC: 4)
  - [x] New file `client/src/features/settings/components/LocaleSettings.test.tsx`, following `LocaleDropdown.test.tsx`'s conventions (`vi.mock('../hooks/useUpdateLocale')`, `mockMutate = vi.fn()`, `i18n.changeLanguage('en-US')` in `beforeEach`/`afterEach`).
  - [x] `LocaleSettings_Rendered_ShowsCurrentLanguageOnRow` — render wrapped in a parent div with the same `backdropFilter: 'blur(20px)'` style as `SettingsRoot.tsx`'s `cardStyle` (assign it a `data-testid="settings-card"` or use a `ref`), assert the row shows the current language.
  - [x] `LocaleSettings_TriggerClicked_OpensListboxWithBothLocales` — click the trigger row, assert `getAllByRole('option')` has length 2.
  - [x] `LocaleSettings_DropdownOpen_ListboxIsNotDescendantOfBackdropFilterCard` — with the dropdown open, assert `cardElement.contains(screen.getByRole('listbox'))` is `false` — this is the core regression test: it fails if `LocaleSettings.tsx` ever reverts to a DOM-nested `absolute` div instead of a portaled `Popover`.
  - [x] `LocaleSettings_LocaleSelected_CallsUpdateLocaleMutationAndClosesDropdown` — click a locale option, assert `mockMutate` called with the expected locale + `onError` callback, and the listbox closes.
  - [x] `LocaleSettings_EscapeKeyPressedWhileOpen_ClosesDropdown` — mirrors `LocaleDropdown.test.tsx`'s equivalent test, confirms Radix's built-in Escape handling now covers what the deleted manual `keydown` listener used to do.

- [x] Task 4: Full regression pass (AC: all)
  - [x] Run `npx tsc --noEmit`, `npx vitest run`, and `npm run lint` from `client/` — zero regressions across the full suite, not just files touched by this story. Pay particular attention to `SettingsPage.test.tsx` (renders `SettingsRoot` → `LocaleSettings`) — confirm it still passes unchanged.

### Review Findings

- [x] [Review][Patch] Outside-click dismissal is untested — add `LocaleSettings.test.tsx` coverage mirroring `LocaleDropdown.test.tsx`'s `PointerDownOutsideDropdown_ClosesDropdown` test [client/src/features/settings/components/LocaleSettings.test.tsx]
- [x] [Review][Patch] `aria-expanded` toggle on the trigger is unverified after removing the manual attribute — add the same `toHaveAttribute('aria-expanded', ...)` assertions `LocaleDropdown.test.tsx` uses [client/src/features/settings/components/LocaleSettings.test.tsx]
- [x] [Review][Defer] Rapid double-locale-selection race can revert to the wrong language if a stale mutation's `onError` fires after a later successful selection [client/src/features/settings/components/LocaleSettings.tsx] — deferred, pre-existing (unchanged onClick/onError logic, out of scope for this rendering-mechanism-only story)

## Dev Notes

### The epic's premise about where the bug lives is wrong — read this before touching `popover.tsx`

The epic text for this story (`_bmad-output/planning-artifacts/epics/epic-8-ui-behavior-consistency-alignment.md#Story 8.3`) describes `LocaleDropdown.tsx`'s overlay as "currently render[ing] partially hidden/clipped" and directs a fix "at the shared level (`popover.tsx`'s stacking/z-index behavior)." This was true of `FlatSwitcher.tsx` **before** it was fixed on 2026-07-04, and the epic's author appears to have generalized from that historical bug description without re-checking current code. By the time this story was created, both `FlatSwitcher.tsx` and `LocaleDropdown.tsx` were **already** migrated onto the portal-based `Popover` pattern (see `deferred-work.md:280`: `LocaleDropdown.tsx` was migrated proactively on 2026-07-04, in the same pass as the `FlatSwitcher` fix). `popover.tsx` itself has no bug. **The only actual, currently-reproducible instance of this clipping bug in the app today is `LocaleSettings.tsx`** — a same-purpose, differently-named component that the 2026-07-04 fix pass didn't touch because it lives in a different file (Settings page vs. onboarding). Do not spend time trying to root-cause a z-index/stacking issue in `popover.tsx` — there isn't one to find.

### Why `LocaleSettings.tsx` and `LocaleDropdown.tsx` are two different components

Easy to conflate given both are "the language switcher": `LocaleDropdown.tsx` (`client/src/components/`) is used in onboarding (compact pill-button trigger, white/10 translucent style). `LocaleSettings.tsx` (`client/src/features/settings/components/`) is a full-width settings row (used only in `SettingsRoot.tsx`) with its own independent hand-rolled dropdown implementation — it does not import or reuse `LocaleDropdown.tsx` at all. They happen to both call `useUpdateLocale`/`i18n.changeLanguage` with the same logic, just via separately-written components. This story only touches `LocaleSettings.tsx`.

### The clipping mechanism (for context, already root-caused in a prior investigation)

Full write-up: `_bmad-output/implementation-artifacts/investigations/flat-switcher-overlay-clipped-investigation.md`. Summary: WebKit/Safari renders an element with `backdrop-filter` in its own compositing layer bounded by that element's box — any absolutely-positioned **descendant** content that visually extends past that box gets clipped to it, even with no explicit `overflow: hidden` anywhere. `SettingsRoot.tsx`'s `cardStyle` (`backdropFilter: 'blur(20px)'`, applied at `SettingsRoot.tsx:58` wrapping `<LocaleSettings />`) is exactly this kind of ancestor. Radix `Portal` escapes it because the portaled content becomes a DOM child of `document.body`, a sibling of `#root`, not a descendant of the `backdrop-filter` div at all — that's why the fix is "use `Popover`," not "add `overflow: visible`" or "raise z-index" (this was Hypothesis 1 in the investigation and was refuted: it's a clipping bug, not a stacking bug).

### Migration is mechanical — `LocaleDropdown.tsx` is the exact reference pattern

`LocaleDropdown.tsx` (already correct) and `LocaleSettings.tsx` (to be fixed) both: hold `isOpen` in `useState`, call the same `useUpdateLocale` hook, iterate a two-item locale list, call `i18n.changeLanguage` + `updateLocale` + `setIsOpen(false)` on select. The only structural difference is trigger visual style (settings full-width row vs. onboarding pill) and dropdown position (`align="end"` on both is actually already correct for this migration, since both anchor toward the row's right edge). Copy `LocaleDropdown.tsx`'s `Popover`/`PopoverTrigger`/`PopoverContent` wiring directly; only the trigger's JSX content and the `PopoverContent`'s visual styling differ (keep `LocaleSettings.tsx`'s existing distinct look — dark `rgba(30,30,50,0.95)` background — do not make it look like `LocaleDropdown.tsx`'s white/10 style).

### Testing note: jsdom can verify the Portal escape, not the actual visual clip

Vitest/jsdom does not render real CSS (`backdrop-filter`, compositing layers), so no automated test can reproduce "would this actually be visually clipped in WebKit." What jsdom **can** verify, and what Task 3's core regression test does, is the structural precondition that makes the visual bug possible or impossible: whether the listbox panel is a DOM descendant of the card wrapper. `Popover`'s `Portal` guarantees it's a sibling of `#root` under `document.body`; a hand-rolled `absolute` div would be a literal DOM descendant. This is a meaningful, deterministic regression test even without real CSS rendering — it directly encodes "did this file revert to the hand-rolled pattern."

### Project Structure Notes

- Modified: `client/src/features/settings/components/LocaleSettings.tsx` (migrated to `Popover`; manual dismiss-effect deleted).
- New: `client/src/features/settings/components/LocaleSettings.test.tsx`.
- No changes to: `client/src/components/ui/popover.tsx`, `client/src/components/LocaleDropdown.tsx`, `client/src/components/FlatSwitcher.tsx`, `client/src/features/decomposition/components/PeriodSelector.tsx`, `client/src/features/dashboard/components/CostGapBadge.tsx`, `client/src/features/settings/components/SettingsRoot.tsx` (its `cardStyle`/`backdropFilter` stays — the fix is escaping it via Portal, not removing it).
- No backend changes. No new i18n keys — reuses `settings.json`'s existing `locale.title`/`locale.de`/`locale.en` keys in both locales, unchanged.
- No new dependencies — `@radix-ui/react-popover` is already a project dependency via `components/ui/popover.tsx`.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-8-ui-behavior-consistency-alignment.md#Story 8.3] — original epic-level AC text; its premise about the bug's location is corrected by AC1/AC2 above.
- [Source: _bmad-output/planning-artifacts/epics/requirements-inventory.md#FR-46] — "Every dropdown/overlay/popover in the app renders fully visible and unclipped within the viewport, regardless of its trigger's position on the page" — the functional requirement this story satisfies.
- [Source: _bmad-output/implementation-artifacts/investigations/flat-switcher-overlay-clipped-investigation.md] — full root-cause investigation for the `backdrop-filter`-clips-absolute-descendant mechanism; confirms the fix pattern (`Popover`/Portal) and flags `LocaleDropdown.tsx` as sharing the "same latent risk" (since resolved) — this story finds and fixes the one instance that investigation didn't (`LocaleSettings.tsx`, a component it never examined).
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:278-280] — confirms `LocaleDropdown.tsx` was already migrated to `Popover` on 2026-07-04, proactively; this is why it is out of scope for further fixing in this story.
- [Source: client/src/components/ui/popover.tsx] — confirmed current shape: `PopoverPrimitive.Portal` wrapping `PopoverPrimitive.Content`, `z-50`, no `container` prop override anywhere in the codebase (grepped).
- [Source: client/src/components/LocaleDropdown.tsx] — confirmed current shape (already correct); the direct reference pattern for this story's migration of `LocaleSettings.tsx`.
- [Source: client/src/components/FlatSwitcher.tsx] — confirmed current shape (already correct, portal-based since `127731b`).
- [Source: client/src/features/decomposition/components/PeriodSelector.tsx] — confirmed current shape (already correct, portal-based).
- [Source: client/src/features/dashboard/components/CostGapBadge.tsx] — confirmed current shape (already correct, portal-based); the original reference pattern the investigation cited.
- [Source: client/src/features/settings/components/LocaleSettings.tsx] — full current implementation read during story creation; confirmed hand-rolled `absolute` div (lines 52-53), manual outside-click/Escape `useEffect` (lines 19-34), `useRef` (line 15) — all to be replaced/deleted per Task 1.
- [Source: client/src/features/settings/components/SettingsRoot.tsx] — confirmed `cardStyle` (`backdropFilter: 'blur(20px)'`, lines 11-16) wrapping `<LocaleSettings />` at line 58-60 — the clipping ancestor.
- [Source: client/src/components/LocaleDropdown.test.tsx] — existing test conventions (mock setup, `i18n.changeLanguage` reset in `beforeEach`/`afterEach`, Escape/outside-dismiss test patterns) this story's new `LocaleSettings.test.tsx` follows.
- [Source: _bmad-output/project-context.md] — VSA feature-folder conventions (test file co-located with the component it tests), React/Radix version gotchas (no `container` override needed for standard Portal behavior).

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `npx tsc --noEmit` (client/) — clean, no errors.
- `npx vitest run` (client/) — 59 files / 377 tests passed, including `SettingsPage.test.tsx` and the new `LocaleSettings.test.tsx` (5 tests).
- `npm run lint` (client/) — 0 errors; 6 pre-existing `react(only-export-components)` warnings in `src/router.tsx`, unrelated to this story.
- Confirmed RED before migration: ran the new `LocaleSettings.test.tsx` against the pre-migration hand-rolled `LocaleSettings.tsx` — `LocaleSettings_DropdownOpen_ListboxIsNotDescendantOfBackdropFilterCard` failed (`expected true to be false`), the other 4 tests passed. Confirmed GREEN after migration — all 5 passed.

### Completion Notes List

- Migrated `LocaleSettings.tsx` from a hand-rolled `absolute`-positioned dropdown with manual `useRef` + `mousedown`/`touchstart`/`keydown` document-listener dismiss logic onto the shared `Popover`/`PopoverTrigger`/`PopoverContent` pattern (Radix, Portal-based), mirroring `LocaleDropdown.tsx`'s existing reference implementation. Visual styling (dark `rgba(30,30,50,0.95)` background, blur, border, `min-w-[120px]`, `rounded-xl`) preserved exactly on `PopoverContent`. No i18n keys, user-visible text, or mutation/selection behavior changed.
- Verified (code inspection only, no changes) that `FlatSwitcher.tsx`, `PeriodSelector.tsx`, and `CostGapBadge.tsx` already use the Portal-based `PopoverContent` with no `container` override — confirming the epic's audit scope is satisfied by this one migration.
- Added `LocaleSettings.test.tsx` with 5 tests, including the core regression test asserting the open listbox is not a DOM descendant of a `backdrop-filter` ancestor card — proven RED against the pre-migration implementation, GREEN after.
- Full regression pass: `tsc --noEmit`, `vitest run` (full suite), `npm run lint` — all clean, zero regressions.

### File List

- Modified: `client/src/features/settings/components/LocaleSettings.tsx`
- New: `client/src/features/settings/components/LocaleSettings.test.tsx`

## Change Log

- 2026-07-16: Story created via create-story workflow. Investigation during story creation found the epic's stated root cause (`LocaleDropdown.tsx`/`popover.tsx`) was stale — both were already fixed on 2026-07-04 (`deferred-work.md:278-280`). Re-scoped the story around the actual remaining defect: `LocaleSettings.tsx` (Settings page's own locale picker, a distinct component from `LocaleDropdown.tsx`), still hand-rolled and still clipped by `SettingsRoot.tsx`'s `backdrop-filter` card. Confirmed via full-repo grep that it is the only remaining non-portaled dropdown. Regression test relocated from "shared `popover.tsx` level" (per epic text) to `LocaleSettings.tsx` level, since that's the level the actual bug lives at.
- 2026-07-16: Implemented — migrated `LocaleSettings.tsx` to the shared `Popover` pattern, deleted manual dismiss-effect logic, added `LocaleSettings.test.tsx` regression suite (5 tests). Full regression pass clean (`tsc`, `vitest`, `lint`). No changes to any other Popover consumer.
