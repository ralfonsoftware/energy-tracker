---
baseline_commit: f3605f3ead31dcecab375ae4cc5d3fe3e4122d68
---

# Story 5.5: UX Polish — Bottom Tab Bar Safe Area & Tariff Sheet Close Affordance

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want the bottom navigation to respect my device's safe area and the tariff edit sheet to have a clearly reachable close control,
so that the app is comfortable to use on notched/home-indicator iOS devices and I can reliably dismiss the tariff edit sheet.

## Acceptance Criteria

1. **Given** `BottomTabBar` on an iOS device with a home indicator (e.g. Safari/iOS), **when** rendered, **then** its bottom padding accounts for `env(safe-area-inset-bottom)` so the home indicator no longer overlaps the bar's content; the spec's exact 72px height is preserved as the content height with the safe-area inset added on top; the related scroll/layout quirk observed on tab-switch on Safari/iOS is also fixed.

2. **Given** the Tariff edit sheet (`TariffForm.tsx` in edit mode), **when** rendered, **then** the close ("✕") affordance is resized/repositioned so it is easy to notice and reach (adequate tap target, clear visual placement); no change to the sheet's submission behavior.

## Tasks / Subtasks

- [x] Task 1: Enable `env()` safe-area support at the document level (AC: 1)
  - [x] Add `viewport-fit=cover` to the `<meta name="viewport">` tag in `client/index.html`. **Without this, `env(safe-area-inset-bottom)` always resolves to `0` on iOS Safari** — the visual fix will silently do nothing if this step is skipped. Verify by checking the current tag has only `width=device-width, initial-scale=1.0`.

- [x] Task 2: `client/src/components/BottomTabBar.tsx` — safe-area-aware height (AC: 1)
  - [x] Change the `nav`'s fixed `h-[72px]` Tailwind class to an inline `style` that preserves 72px as the *content* height and adds the safe-area inset as extra height beneath it, so icons/labels stay vertically centered at the same visual position as before, with the translucent glass background extending into the safe area:
    ```ts
    style={{
      height: 'calc(72px + env(safe-area-inset-bottom, 0px))',
      paddingBottom: 'env(safe-area-inset-bottom, 0px)',
      background: 'rgba(10,15,25,0.75)',
      WebkitBackdropFilter: 'blur(20px) saturate(180%)',
      backdropFilter: 'blur(20px) saturate(180%)',
      borderTop: '1px solid rgba(255,255,255,0.10)',
    }}
    ```
    Keep `className="fixed bottom-0 left-0 right-0 flex items-center"` (drop `h-[72px]` from the class list since height now comes from the inline style). The always-present `flex items-center` on the now-taller box centers the tab row content within the full box, but because `paddingBottom` reserves exactly the safe-area amount at the bottom, the visual center of the *content* area still sits at the same 36px-from-top position as before — do not add `items-start` or manual offsets.
  - [x] Always include the `, 0px` fallback in every `env()` call — this is what makes the calc resolve to a valid `72px` (not `NaN`/invalid) on browsers/environments without safe-area support (all non-iOS, and jsdom in tests).

- [x] Task 3: `client/src/components/AppShell.tsx` — keep scroll clearance in sync with the taller bar (AC: 1)
  - [x] The `<main>` element's `pb-[84px] md:pb-0` clearance was sized for the bar's old fixed 72px height (+ 12px buffer). Now that the bar can grow by the safe-area inset on phone, update the mobile padding-bottom to grow by the same amount, or dashboard/settings content will be hidden behind the taller bar on notched devices: change to `pb-[calc(84px_+_env(safe-area-inset-bottom,0px))] md:pb-0`.
  - [x] **Tailwind arbitrary-value gotcha:** inside `[...]` bracket notation, literal spaces are not allowed — use `_` (underscore) wherever the raw CSS value would have a space (`84px_+_env(...)`, and `,` needs no escaping). Writing `pb-[calc(84px + env(safe-area-inset-bottom, 0px))]` with literal spaces will fail to generate the expected CSS class.
  - [x] Fix the Safari/iOS tab-switch scroll quirk: the `<main>` element scrolls internally (`overflow-y-auto`) and currently never resets scroll position when the route changes, so switching tabs while scrolled down on the previous page can land the user mid-scroll on the new page (the "related scroll/layout quirk on tab-switch" from the retro). Add a ref on `<main>` and a `useEffect` keyed on `useLocation().pathname` from `react-router-dom` that calls `mainRef.current?.scrollTo(0, 0)` on every path change.
  - [x] **jsdom gotcha for tests:** `HTMLElement.prototype.scrollTo` is not implemented in jsdom and calling it un-mocked logs a "Not implemented" error to the test console (and can fail assertions depending on setup) — any test exercising this effect must stub it first, e.g. `Element.prototype.scrollTo = vi.fn()` in a `beforeEach`, per this project's Vitest/jsdom setup (`client/src/test-setup.ts`).

- [x] Task 4: `client/src/features/tariffs/components/TariffList.tsx` — Tariff sheet close-affordance polish (AC: 2)
  - [x] The Sheet wrapping `TariffForm` (used for both Add and Edit — there's a single shared `SheetContent`/close button for this Sheet, so this fix naturally benefits both, satisfying the "edit mode" AC by construction) already sets a 44×44px tap target (`[&>button]:h-11 [&>button]:w-11`) positioned `right-2 top-2`, with color fixed by a prior bug fix (commit `1723d21`, "fix: invisible close button on Sheet overlays across the app"). **The remaining discoverability problem**: the generated `SheetContent` in `client/src/components/ui/sheet.tsx` (never hand-edit — see `project-context.md`) applies `opacity-70` at rest, only reaching `opacity-100` on `:hover` — but touch devices have no hover state, so on a phone the icon is rendered at a compounded ~42% visual intensity (70% opacity × 60% white text-color already applied) at all times. Add `[&>button]:opacity-100` to `SheetContent`'s `className` in `TariffList.tsx` to make it fully opaque at rest (dropping reliance on a hover state that mobile users never trigger).
  - [x] Also increase the icon's own size for clearer visual placement (currently the generated `<X className="h-4 w-4" />` — 16px — inside the 44px button, hardcoded in the generated `sheet.tsx` and not overridable via a plain className on `SheetContent`): add a descendant selector to `SheetContent`'s `className`: `[&>button_svg]:h-5 [&>button_svg]:w-5`.
  - [x] Reposition slightly further from the sheet's rounded top corner for a cleaner visual placement: change `[&>button]:right-2 [&>button]:top-2` to `[&>button]:right-3 [&>button]:top-3`.
  - [x] Do not touch `TariffForm.tsx`'s own submit/cancel logic, the override-confirm `Dialog` at the bottom of that file (it already has correct, unrelated close-button styling for a *different* dialog), or any other `Sheet`/`Dialog` usage elsewhere in the app — AC2 scopes this to the Tariff sheet only.

- [x] Task 5: Tests (AC: 1, 2)
  - [x] `client/src/components/BottomTabBar.test.tsx`: add an assertion that the `nav` element's inline `style` attribute contains `env(safe-area-inset-bottom` for both `height` and `paddingBottom`. Read the raw attribute via `nav.getAttribute('style')` rather than `nav.style.height`/`nav.style.paddingBottom` getters — jsdom's CSS parser (`cssstyle`) can be stricter about unknown CSS functions than a real browser and may not round-trip the value the same way through the style-object getters.
  - [x] Add a new `client/src/components/AppShell.test.tsx` (none exists today): render `AppShell` inside a `MemoryRouter` with a mocked `Outlet`/route tree (or a minimal set of routes), stub `Element.prototype.scrollTo = vi.fn()` in `beforeEach`, navigate between two routes, and assert `scrollTo` was called with `(0, 0)` on the navigation. Also assert the `<main>` element's `style`/`class` contains the updated `pb-[calc(84px_+_env(...))]` clearance (via `className` string match, not computed style).
  - [x] `client/src/features/tariffs/components/TariffList.test.tsx`: render the Sheet open (existing pattern in this file/`TariffForm.test.tsx` for opening the sheet), query the close button (`getByRole('button', { name: 'Close' })` — the generated component's `sr-only` label), and assert its class list includes `opacity-100` (or equivalent: assert it does *not* include a `opacity-70`-only state). Do not assert on hover-state classes, since jsdom doesn't simulate real `:hover` pseudo-class behavior reliably for this kind of assertion.

- [ ] Task 6: Self-review pass before marking ready for review
  - [ ] Manually verify (dev server, responsive device toolbar or a real iOS Safari session if available) that: the tab bar's icons remain visually at the same height as before on a normal viewport (no `env()` support), and grow only their bottom safe-area padding on a simulated notched viewport; dashboard/settings content is not clipped behind the bar on either; the tariff sheet's close button is visibly brighter/larger than before at rest (not just on hover). **Not performed this session** — no browser automation tool was connected, and a fully authenticated app view requires `swa start` with Functions/SQL running (per `project-context.md`, plain `npm run dev` yields 403s on all API calls), which was out of scope to stand up. **Caveat (2026-07-05):** story marked `done` per user decision after code review, accepting automated verification (unit tests, tsc, lint) plus the code-review layer's pass as sufficient closure; this manual check remains a non-blocking follow-up if a visual issue surfaces later.
  - [x] `npx tsc -b`, `npx vitest run`, `npm run lint` (all from `client/`) all green. No backend changes in this story — no `dotnet build`/`dotnet test` needed.

### Review Findings

- [x] [Review][Patch] `TariffList` close-button test asserted `opacity-100` on the close button's own `className`, but the fix lives on the `SheetContent`/`role="dialog"` wrapper's className (`[&>button]:opacity-100` is a Tailwind descendant selector, never copied onto the child). The assertion coincidentally passed regardless of the fix because the generated close button already carries an unrelated `hover:opacity-100` substring. Fixed by querying `getByRole('dialog')` and asserting on its className instead. [client/src/features/tariffs/components/TariffList.test.tsx:340-352]
- [x] [Review][Patch] `AppShell` route-change test asserted `scrollTo` was called with `(0, 0)` without isolating the navigation event — the effect also fires once on initial mount, so the assertion passed even if the navigation-triggered reset were broken. Fixed by clearing the mock after mount and asserting exactly one call after navigating. [client/src/components/AppShell.test.tsx:41-54]
- [x] [Review][Patch] Scroll-reset `useEffect` was keyed only on `pathname`, so a same-path navigation that only changes `search`/`hash` would not reset scroll. Widened the dependency array to `[pathname, search, hash]`. [client/src/components/AppShell.tsx:10,14]
- [x] [Review][Patch] `vi.spyOn` on the shared `CSSStyleProperties.prototype` setters in the `BottomTabBar` safe-area test was never restored, risking a lingering wrapped setter for the rest of the test file. Wrapped in `try/finally` with `mockRestore()`. [client/src/components/BottomTabBar.test.tsx:32-49]
- [x] [Review][Defer] `viewport-fit=cover` is a global opt-in but only `BottomTabBar`/`AppShell` got matching safe-area handling — other fixed/absolute-positioned UI (toasts, modals, headers) is unaudited for safe-area overlap. Out of scope for AC1/AC2. [client/index.html] — deferred, pre-existing/out of scope
- [x] [Review][Defer] Only `safe-area-inset-bottom` is handled; no `safe-area-inset-left`/`right` handling for landscape orientation on notched devices. AC1 only concerns the home-indicator (bottom) overlap. [client/src/components/BottomTabBar.tsx] — deferred, out of scope
- [x] [Review][Defer] The 72px bar / 84px main-clearance / safe-area-inset relationship is duplicated across two files with no shared constant. Pre-existing coupling (84px = 72px + 12px buffer existed before this story); this diff extends it symmetrically rather than introducing it. [client/src/components/BottomTabBar.tsx, AppShell.tsx] — deferred, pre-existing
- [x] [Review][Defer] The close-button opacity/size/position fix is applied only at the `TariffList` call site, not at the shared `Sheet` component level, so other `Sheet` consumers elsewhere in the app likely retain the same touch-discoverability gap. By design per this story's Dev Notes ("AC2 scopes this to the Tariff sheet only"). [client/src/components/ui/sheet.tsx] — deferred, intentionally out of scope, candidate for a follow-up story
- [x] [Review][Defer] `[&>button_svg]:h-5 [&>button_svg]:w-5` couples presentation CSS to the generated Sheet primitive's internal DOM structure (assumes a raw `<svg>` child). Inherent to the spec-mandated "never hand-edit `ui/sheet.tsx`" approach. [client/src/features/tariffs/components/TariffList.tsx] — deferred, accepted tradeoff
- [x] [Review][Defer] `getByRole('button', { name: 'Close' })` hardcodes the English "Close" string. The label originates from the generated, non-editable `sheet.tsx`, and this story explicitly makes no i18n changes. [client/src/features/tariffs/components/TariffList.test.tsx] — deferred, out of scope
- [x] [Review][Defer] The `BottomTabBar` safe-area test spies on `CSSStyleProperties.prototype` setters rather than reading the raw `style` attribute as the story's task text literally describes. Empirically verified necessary: in this project's jsdom/css-tree version, `getAttribute('style')` drops `padding-bottom: env(...)` entirely and mangles the `calc()`/`env()` argument order for `height` (documented in Debug Log References). [client/src/components/BottomTabBar.test.tsx] — deferred, verified deviation from literal spec wording
- [x] [Review][Defer] Exact-string CSS assertions (`toHaveBeenCalledWith('calc(72px + env(...))')`, `className.toContain('pb-[calc(...)]')`) are brittle to harmless reformatting. Accepted tradeoff — jsdom cannot render real CSS cascade/computed styles for Tailwind-driven behavior in this test environment. [client/src/components/BottomTabBar.test.tsx, AppShell.test.tsx] — deferred, accepted tradeoff

## Dev Notes

### This is a pure frontend CSS/JS polish story — no backend, no API, no schema changes

Both fixes are presentation-layer only. Nothing here touches `apiClient`, TanStack Query, or any DTO shape. There is no new library dependency.

### Why `viewport-fit=cover` is the easy-to-miss prerequisite

`env(safe-area-inset-bottom)` is a no-op (`0px`) unless the page's viewport meta tag opts into `viewport-fit=cover`. This project's `client/index.html` currently has no `viewport-fit` value at all, so Task 2/3's CSS would compile and look syntactically correct while doing literally nothing visually on a real device — this is the single most likely way to silently "complete" this story without the fix actually working. Task 1 must land before/with Tasks 2–3.

### 72px content height must be *preserved*, not replaced

AC1 explicitly says "the spec's exact 72px height is preserved as the content height with the safe-area inset added on top" — i.e. the tab icons/labels must not shift or shrink; only extra transparent-but-styled space appears below them for the home indicator to sit over. Implement this as `height: calc(72px + safe-area-inset)` + `paddingBottom: safe-area-inset` (Task 2), not by baking the inset directly into a single unconditional `72px` height (which would compress the tappable icon row) and not by only adding `padding-bottom` without growing `height` (which would make the bar visually taller than 72px+inset in a way that pushes the flex-centered content down into the inset area rather than keeping it above it).

### `AppShell`'s `pb-[84px]` and `BottomTabBar`'s height are coupled — a classic "fix one, break the other" trap

`AppShell.tsx`'s `<main>` clearance (84px = 72px bar + 12px buffer) exists purely to keep the last bit of scrollable content from being obscured by the fixed-position bar. If Task 2 makes the bar taller on notched devices without a matching Task 3 change, the last ~20–40px of dashboard/settings content becomes permanently hidden behind the bar on exactly the devices this story is meant to help — an easy way to "complete" AC1 for the home-indicator overlap while introducing a new, less obvious content-clipping regression. Both files must change together.

### The tab-switch scroll quirk: most likely root cause, not confirmed

The epic-4 retrospective flagged this as "likely a missing `env(safe-area-inset-bottom)` accommodation" but separately described it as "a related scroll/layout quirk...on tab-switch" — distinct wording from the overlap issue. There is currently no scroll-position reset on route change anywhere in this app (checked: no `ScrollRestoration`, no `useLocation`-keyed effect in `AppShell.tsx` or `App.tsx`). The most standard, low-risk fix for "new page appears mid-scroll after switching bottom tabs" in a React Router SPA is resetting the scrolling container's `scrollTop` on path change (Task 3) — this is the interpretation implemented here. If a real-device check in Task 6 shows a different/additional Safari-specific artifact (e.g. momentum-scroll rubber-banding revealing background), flag it in Completion Notes rather than silently declaring the AC met without having actually reproduced+fixed it.

### The Tariff sheet close button: the previous fix (commit `1723d21`) already solved a *different* problem

`1723d21` fixed the button being **invisible** (no text color at all, defaulting to black-on-black). That is a strictly worse failure mode than the "hard to notice/reach" wording in the retro (dated a day earlier, 2026-07-03) — but it does not mean the discoverability complaint is now moot. The generated `sheet.tsx`'s `opacity-70` (only reaching full opacity on `:hover`, which never fires on a touchscreen) still applies underneath the color fix, so the button is visible-but-faint at rest on the exact devices (phones) this app targets. Task 4 closes that gap. Do not remove or alter the `1723d21` color classes (`text-white/60 hover:text-white`) — only add to them.

### Never hand-edit `client/src/components/ui/*` — style via the `className` prop's descendant selectors instead

`sheet.tsx` and `dialog.tsx` under `components/ui/` are shadcn-generated (per `project-context.md`'s "shadcn/ui" rule). All of this story's close-button changes must go through `SheetContent`'s `className` prop in `TariffList.tsx` using Tailwind's `[&>button]:...` / `[&>button_svg]:...` arbitrary-selector syntax — exactly the pattern already used at that call site — not by editing `sheet.tsx` directly.

### Project Structure Notes

- Modified files only, no new files: `client/index.html`, `client/src/components/BottomTabBar.tsx`, `client/src/components/AppShell.tsx`, `client/src/features/tariffs/components/TariffList.tsx`.
- New test file: `client/src/components/AppShell.test.tsx` (none exists yet for this component).
- Modified test files: `client/src/components/BottomTabBar.test.tsx`, `client/src/features/tariffs/components/TariffList.test.tsx`.
- No i18n changes — no new user-visible strings are introduced (the close button's `sr-only` "Close" label is part of the generated component and already exists).
- No changes to `client/src/features/tariffs/components/TariffForm.tsx` itself — the close button being polished lives in the `SheetContent` wrapper in `TariffList.tsx`, one level up.

### Testing standards summary

- Frontend only (Vitest + `@testing-library/react`, `globals: true`, co-located `.test.tsx` files, `jsdom` environment) — no backend test changes.
- Query by role/label/text, never CSS class or `data-testid`, per project convention — except where a test's whole purpose *is* to assert a specific utility class landed on an element (the `opacity-100`/`env()`-string checks above), which is an accepted exception since there is no semantic role/text to query for those cases.
- Two jsdom-specific pitfalls apply directly to this story (both called out in Task 5): (1) `cssstyle`'s parser may not preserve unrecognized CSS functions like `env()`/`calc()` through `element.style.<prop>` getters — assert on the raw `style` attribute string instead; (2) `scrollTo` is unimplemented on jsdom elements and must be stubbed before any test exercises the new route-change effect.
- No new component logic beyond a single `useEffect`/`useRef` in `AppShell.tsx` — the bulk of "testing" here is visual/manual (Task 6), since neither `env()` resolution nor real Safari rendering quirks can be verified inside jsdom.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-5-multi-flat-management-flat-structure.md#Story 5.5] — authoritative AC text (verbatim, reproduced above), sourced from epic-4 retro action items #6/#7.
- [Source: _bmad-output/implementation-artifacts/epic-4-retro-2026-07-03.md#Action Items, #Went Well / Improve] — original retro items: "#2 [Minor] The tariff edit sheet's close ('✕') affordance is present but hard to notice/reach"; "#3 [Minor] Dashboard bottom tab bar is slightly overlaid by content on Safari/iOS; a related scroll/layout quirk appears on tab-switch"; action items #6 (bottom-bar/safe-area, owner Amelia) and #7 (tariff sheet close-affordance, owner Sally/Amelia).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/DESIGN.md#Tab bar height, #Scroll bottom clearance] — "Tab bar height: 72px fixed at viewport bottom"; "Scroll bottom clearance: 84px padding-bottom to clear the fixed tab bar on phone"; phone tab-bar spec block (`position: fixed, bottom:0, height:72px, background: rgba(10,15,25,0.75), backdrop-filter: blur(20px) saturate(180%), border-top: 1px solid rgba(255,255,255,0.10)`) — none of these tokens change; only extra safe-area space is added beneath them.
- [Source: client/src/components/BottomTabBar.tsx, AppShell.tsx] — current implementation (72px fixed height nav; `<main>`'s `pb-[84px] md:pb-0` clearance; no existing scroll-reset-on-route-change logic).
- [Source: client/index.html] — current viewport meta tag, missing `viewport-fit=cover`.
- [Source: client/src/components/ui/sheet.tsx, dialog.tsx] — generated Close button base classes (`absolute right-4 top-4 rounded-sm opacity-70 ... hover:opacity-100 ... data-[state=open]:bg-secondary`, `<X className="h-4 w-4" />`) — confirms the compounding-opacity root cause; never hand-edit these files.
- [Source: client/src/features/tariffs/components/TariffList.tsx] — current `SheetContent` override (`[&>button]:right-2 [&>button]:top-2 [&>button]:h-11 [&>button]:w-11 ... [&>button]:text-white/60 [&>button]:hover:text-white ... [&>button]:data-[state=open]:bg-white/10`), the exact className string Task 4 extends.
- [Source: git commit 1723d21 "fix: invisible close button on Sheet overlays across the app"] — prior, narrower fix (color/contrast only); this story's Task 4 builds on it, does not replace it.
- [Source: _bmad-output/project-context.md#shadcn/ui, #Tailwind v4, #Testing Rules — Frontend] — "never hand-edit `client/src/components/ui/`"; no `tailwind.config.js` (v4, `@theme` in `index.css`); Vitest `globals: true`/jsdom/co-located tests/query-by-role conventions.

## Change Log

- Implemented safe-area-aware `BottomTabBar` height/padding, `AppShell` scroll-clearance + scroll-reset-on-route-change, and `TariffList` Sheet close-button opacity/size/position polish; added/updated tests for all three; `npx tsc -b`, `npx vitest run`, `npm run lint` all green (Date: 2026-07-05)
- Code review (3-layer parallel: Blind Hunter, Edge Case Hunter, Acceptance Auditor): fixed 4 confirmed test-correctness issues (wrong-element assertion in `TariffList` test, unisolated navigation assertion in `AppShell` test, scroll-reset effect missing `search`/`hash` deps, unrestored `vi.spyOn` on shared prototype); 8 items deferred as pre-existing/out-of-scope (see Review Findings and `deferred-work.md`); status set to `done` per user decision, with Task 6's manual device check remaining a non-blocking follow-up (Date: 2026-07-05)

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- jsdom's `env()`/`calc()` CSS parsing (css-tree, jsdom 29.1.1) mangles or silently drops these values when read back via `getAttribute('style')` or `.style.<prop>` getters (e.g. `padding-bottom: env(safe-area-inset-bottom, 0px)` is dropped entirely; `calc(72px + env(...))` is reordered to `calc(72px + env(0px * , * safe-area-inset-bottom))`). Worked around in `BottomTabBar.test.tsx` by spying on the `CSSStyleProperties.prototype` `height`/`paddingBottom` setters to assert the exact raw value React assigns, rather than reading the serialized attribute back.

### Completion Notes List

- All 6 tasks implemented per spec: `viewport-fit=cover` added, `BottomTabBar` safe-area-aware height/padding, `AppShell` matching scroll clearance + scroll-reset-on-route-change, `TariffList`'s Sheet close button made fully opaque/larger/repositioned.
- Task 6's automated checks (tsc, full vitest suite, lint) are green. Task 6's **manual real-device/responsive-toolbar visual check was not performed this session** — no browser automation tool was connected, and a fully authenticated view of the app requires `swa start` with Functions + local SQL/Azurite running (plain `npm run dev` returns 403 on all API calls per this project's conventions), which was out of scope to stand up here. Per user decision, the story proceeds to "review" with this flagged as an outstanding manual verification for the reviewer/user to perform (dev server + `swa start`, or a real iOS Safari session) before considering the story fully done.
- No backend changes; no new dependencies; no i18n changes, matching Dev Notes' stated scope.

### File List

- `client/index.html` (modified)
- `client/src/components/BottomTabBar.tsx` (modified)
- `client/src/components/BottomTabBar.test.tsx` (modified)
- `client/src/components/AppShell.tsx` (modified)
- `client/src/components/AppShell.test.tsx` (new)
- `client/src/features/tariffs/components/TariffList.tsx` (modified)
- `client/src/features/tariffs/components/TariffList.test.tsx` (modified)
