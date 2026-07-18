# Investigation: Power-points-view / device-edit Save bar scroll-visibility

## Hand-off Brief

1. **What happened.** `StickyActionBar`'s `position: sticky; bottom: 0` never gets a chance to "stick" early in `RoomEditor` because the trailing buffer after the last content item (`pb-32`, ~128px) is tiny compared to how much the power-point list typically exceeds the viewport — so for any room with a non-trivial number of power points, the Save bar is functionally a normal end-of-page element, invisible until the user scrolls ~93%+ of the way down. Live-repro-confirmed on the Wohnzimmer room (5 power points): bar absent at 58% scroll, present only at 100% scroll.
2. **Where the case stands.** Root cause Confirmed for the `RoomEditor` (power-points-view) case — Action Item #1's primary scope. `DeviceEditor` shares the identical architecture and is Hypothesized to fail the same way, exacerbated on tall/Safari viewports; not yet reproduced in Safari directly (Chrome-only tooling available this session).
3. **What's needed next.** This is not a `StickyActionBar` "regression" in the bug-introduced sense — it's a structural design flaw: a trailing sibling with `position: sticky` cannot function as an always-visible floating toolbar unless the scrollable buffer before it is deliberately sized ≥ viewport height (or the bar uses `position: fixed`/`sticky` pinned outside the scrolling content instead of after it). Recommend routing to Sally's unified save-affordance design pass (Action Item #2) with this mechanism flagged, then `bmad-quick-dev` for the fix.

## Case Info

| Field            | Value                                                                      |
| ---------------- | -------------------------------------------------------------------------- |
| Ticket           | Epic 8 Retro — Action Item #1                                              |
| Date opened      | 2026-07-18                                                                 |
| Status           | Active                                                                     |
| System           | Live deployed app, energytracker.ralfonsoftware.de/settings/structure; Chrome desktop (1728×941 viewport tested); Safari desktop reported by user, not directly tested this session |
| Evidence sources | Source code (`client/src/features/flat-structure/components/`), live DOM/CSS inspection via Chrome automation, user screenshots (Chrome + Safari, device-edit and room-list views) |

## Problem Statement

User-reported (Epic 8 retro, Challenges #1 and #2; Action Item #1): the room-list view's per-room Save button and the power-points-view save action "may not be visible until you scroll," and the device-edit Cancel/Save row "still sometimes needs a slight scroll on Safari." User provided 4 screenshots: device-edit on Chrome vs Safari (visually different bar visibility), and the Wohnzimmer room's power-points view showing no Save button at top of page, only appearing after scrolling to the very end. Action Item #1 explicitly asks to disambiguate: is this a `StickyActionBar` regression, or is it the room-list's per-room Save button (which has no sticky guarantee by design)?

## Evidence Inventory

| Source   | Status                          | Notes     |
| -------- | ------------------------------- | --------- |
| `StickyActionBar.tsx` source | Available | `position: sticky; bottom: 0` (desktop) / `bottom: calc(84px + safe-area)` (mobile) |
| `RoomEditor.tsx` source | Available | Renders power-points list + `StickyActionBar` as trailing sibling, `pb-32` buffer |
| `DeviceEditor.tsx` source | Available | Same pattern, `pb-32` buffer |
| `FlatStructureEditor.tsx` source | Available | Room-list (list view) uses a *different*, intentionally non-sticky, top-right "Save" button + per-room inline Save icons — separate mechanism from `StickyActionBar`, working as designed |
| `AppShell.tsx` source | Available | `<main>` declared `overflow-y-auto` but in practice does not clip (see Finding 2) — actual scroll happens on `<html>` |
| Live DOM/CSS (Chrome, Wohnzimmer room) | Available | Captured via `getComputedStyle`/`getBoundingClientRect`; live-scrolled repro performed |
| Live DOM/CSS (Chrome, device-edit) | Available | Captured; short-content case where `maxScroll` is small |
| Safari live DOM/CSS | Missing | No Safari automation available this session; only static screenshots from user |
| User screenshots | Available | 4 images: device-edit Chrome, device-edit Safari, Wohnzimmer top-of-page, Wohnzimmer scrolled-to-bottom |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --------------- | -------- | ------ | ----- |
| 1 | Reproduce device-edit Safari case directly (real Safari or BrowserStack) to confirm whether it's the same buffer-insufficiency mechanism or a distinct Safari `100vh`/viewport-unit issue | Medium | Open | Needed to fully close the DeviceEditor half of Challenges #1; not required to close Action Item #1's primary RoomEditor scope |
| 2 | Confirm `PowerPointEditor.tsx` doesn't have its own independent save affordance that could compound the picture | Low | Open | Not touched this session — out of scope for Action Item #1 as scoped in the retro |

## Timeline of Events

| Time        | Event               | Source                | Confidence            |
| ----------- | ------------------- | ---------------------- | --------------------- |
| Epic 8 (8.2) | `StickyActionBar` introduced for Power Point/Device editing, `pb-32` clearance added | commit history, `RoomEditor.tsx:57`, `DeviceEditor.tsx:86` comments | Confirmed |
| 2026-07-16 | Ralf's iPhone Safari manual pass on Story 8.2 found "dynamic toolbar behavior, viewport clipping" defects, fixed same day | Epic 8 retro, What Went Well #3 | Confirmed (per retro) |
| 2026-07-18 | Epic 8 retro flags scroll-visibility as unresolved residual gap; Action Item #1 opened | `epic-8-retro-2026-07-18.md:37-38,78` | Confirmed |
| 2026-07-18 | This investigation: live repro on Wohnzimmer room confirms sticky bar invisible until ~93% scroll | Chrome DOM inspection, this session | Confirmed |

## Confirmed Findings

### Finding 1: `StickyActionBar` is `position: sticky`, sibling-after-content, with only `pb-32` (~128px) of reserved buffer

**Evidence:** `client/src/features/flat-structure/components/StickyActionBar.tsx:9-14` — `sticky bottom-[calc(84px_+_env(safe-area-inset-bottom,0px))] md:bottom-0`; `client/src/features/flat-structure/components/RoomEditor.tsx:57-58,93` — comment states `pb-32` exists specifically "so the sticky bar doesn't cover the last power point at full scroll," and `<StickyActionBar>` is rendered as a sibling immediately after the `pb-32`-padded content div, not wrapping/overlaying it.

**Detail:** This is the standard "trailing sticky footer" pattern. Its correctness depends entirely on how much scrollable distance exists between the sticky element's natural resting position and the point where it needs to already be pinned — see Finding 3.

### Finding 2: The actual scrolling container is `<html>`, not `<main>`, despite `<main>` carrying `overflow-y-auto`

**Evidence:** Live inspection on `/settings/structure` (Wohnzimmer room open): `main.clientHeight === main.scrollHeight === 2055` (no internal overflow ever engages), while `document.scrollingElement` (`<html>`) reports `scrollHeight: 2054, clientHeight: 941, maxScroll: 1113`.

**Detail:** `<main>`'s box grows to fit all of its content rather than being height-capped, so its `overflow-y-auto` is inert in this layout; the page scrolls at the document level instead. This doesn't change the sticky math (sticky still resolves against whichever ancestor actually scrolls), but it means any future fix that assumes `<main>` is the scroll boundary (e.g. a `position: sticky` container based on `<main>`'s clientHeight) needs to account for document-level scrolling instead.

### Finding 3: Live repro — Save bar is invisible for the first ~93% of scroll on a 5-power-point room, then appears only at the very end

**Evidence:** Chrome, 1728×941 viewport, Wohnzimmer room (5 power points): `document.scrollingElement.scrollHeight = 2054`, `clientHeight = 941`, `maxScroll = 1113`. Sticky bar's unstuck flow position: `top: 1981.5, bottom: 2054.5` (73px tall). Screenshot at `scrollTop = 650` (58% of max scroll) shows no Save bar in viewport at all. Screenshot at `scrollTop = 1113` (100%, page bottom) shows the Save bar — matching the user's screenshots exactly (top-of-page: absent; scrolled-to-end: present, styled identically to a normal static element, not a floating bar).

**Detail:** The threshold at which `bottom: 0` stickiness engages is `scrollY ≈ (elementFlowTop − viewportHeight) = 1981.5 − 941 ≈ 1040.5`. Out of a `maxScroll` of 1113, that leaves only ~73px (≈6.5%) of the scroll range where the bar is actually "stuck" — i.e., for 93.5% of the scroll range the Save bar behaves exactly as if `position: sticky` weren't applied at all. This is not a regression bug in the CSS or a browser inconsistency; it is the mathematically inevitable consequence of pairing `position: sticky` on a trailing sibling with a buffer (`pb-32`) far smaller than the excess content height.

### Finding 4: The room-list (`FlatStructureEditor`) view's Save affordance is a structurally separate, intentionally non-sticky mechanism

**Evidence:** `client/src/features/flat-structure/components/FlatStructureEditor.tsx:268-276` (page-level "Save" button, always visible in the fixed header block, not in scrollable content) and `:356-373` (per-room inline Save icon button, in-flow, no sticky wrapper, no `StickyActionBar` import).

**Detail:** This confirms the retro's Action Item #1 disambiguation question has two different correct answers depending on which view is meant: the room-list (list-of-rooms) view's page-level Save button is *always* visible (it's in the non-scrolling header), and its per-room save icons were never meant to be sticky (matches retro's own framing: "no sticky guarantee ever specified there"). The view that actually exhibits the reported scroll-visibility bug is the **room detail / power-points view** (`RoomEditor`, reached by clicking into a room), which *does* use `StickyActionBar` and *is* failing to deliver on that sticky guarantee — see Finding 3.

## Deduced Conclusions

### Deduction 1: This is a `StickyActionBar` design/implementation defect, not the room-list's per-room Save button

**Based on:** Finding 3, Finding 4.

**Reasoning:** The screenshots showing "no Save button visible until scrolled to the end" (`Wohnzimmer`) are of the `RoomEditor` view, which wraps its Save button in `StickyActionBar` and explicitly documents (via code comment) an intent for it to remain visible without requiring a full scroll. Live reproduction shows it fails to do so for any room with enough power points to exceed the viewport by more than ~100px. The room-list view's separate Save buttons (page-level and per-room) are unaffected — they were never sticky by design and aren't what's shown in the user's room-detail screenshots.

**Conclusion:** Action Item #1's disambiguation resolves to: **`StickyActionBar` defect**, specifically in `RoomEditor`. The room-list's per-room Save button is a red herring for this specific complaint (it has its own, separate, "consistency" concern already captured in Action Item #2, but it is not scroll-broken).

### Deduction 2: `DeviceEditor` shares the identical failure mechanism, just with a much smaller failure window

**Based on:** Finding 1 (identical `StickyActionBar` + `pb-32` pattern reused verbatim in `DeviceEditor.tsx:86-87,302`), live measurement showing `DeviceEditor`'s `maxScroll` was only 60px in the tested Chrome viewport (vs. 1113px for the 5-power-point room).

**Reasoning:** Because `DeviceEditor`'s form content is short, the gap between "natural end of content" and "viewport height" is small in most windows, so the sticky bar's stuck threshold is reached after only a small scroll (or with none at all, if content fits within viewport) — this is why Chrome's screenshot shows the bar mostly/fully visible. On a taller viewport (as in the Safari screenshot, which shows a much taller window with substantial empty space below the form and no bar at all), the same content becomes shorter relative to viewport, and depending on how `minHeight: 100vh` interacts with `<main>`'s actual rendered height in that specific window (not confirmed directly in Safari this session), the bar can end up positioned beyond the visible area with no scrollable slack ever generated to bring it into the "stuck" zone.

**Conclusion:** Hypothesized (not yet Safari-confirmed) that the device-edit Safari screenshot is the same class of bug manifesting at the opposite extreme: instead of "requires near-total scroll," it may become effectively unreachable in very tall windows because no scrollbar/overflow is ever generated at all. Both symptoms trace to the same root design issue (Finding 1).

## Hypothesized Paths

### Hypothesis 1: DeviceEditor's Safari blank-space case is a `minHeight: 100vh` vs. actual scrollport-height mismatch, distinct from (but related to) the RoomEditor buffer-insufficiency issue

**Status:** Open

**Theory:** `DeviceEditor.tsx:82` sets `minHeight: '100vh'` on its own outer div, which is a descendant of `<main>` (not the literal viewport). If, in the Safari window captured, `<main>`'s actual content-box height ends up taller than the visible browser viewport (e.g., due to how Safari computes `100vh` including/excluding chrome, or extra height contributed by `Header`), the whole editor + sticky bar could be pushed down enough that the bar sits below the fold with no compensating scroll range — leaving it permanently unreachable in that window, rather than merely "hard to reach."

**Supporting indicators:** The Safari screenshot shows a large, otherwise-inexplicable block of empty dark background below the form fields, with no Save/Cancel row visible at all — different from "scroll a bit further and you'll see it" (Chrome's case) and more consistent with "it rendered off in space nothing currently scrolls to."

**Would confirm:** Reproducing in real Safari (or BrowserStack) and measuring `document.scrollingElement.scrollHeight` vs `clientHeight` at that window size — if `scrollHeight <= clientHeight` (no scrollbar) yet the bar's `getBoundingClientRect().top` is beyond `innerHeight`, that confirms the mismatch theory.

**Would refute:** If Safari's metrics turn out proportionally identical to Chrome's (i.e., a scrollbar exists and the bar becomes visible at some scroll position, just requiring more scroll than expected), this reduces to the same Finding 3 mechanism with no Safari-specific component.

**Resolution:** Not yet investigated — flagged in Investigation Backlog #1.

## Missing Evidence

| Gap              | Impact                               | How to Obtain   |
| ---------------- | ------------------------------------ | --------------- |
| Direct Safari DOM/CSS measurement of `DeviceEditor` | Cannot confirm/refute Hypothesis 1 (distinct Safari-only failure mode vs. same mechanism as RoomEditor) | Real Safari session or BrowserStack, repeat the `getComputedStyle`/`getBoundingClientRect` inspection performed in Chrome this session |

## Source Code Trace

| Element       | Detail                                      |
| ------------- | -------------------------------------------- |
| Error origin  | `client/src/features/flat-structure/components/StickyActionBar.tsx:9-14` (the `sticky` positioning itself is not "wrong" in isolation) combined with `RoomEditor.tsx:57-58,93-119` and `DeviceEditor.tsx:86-87,302-322` (insufficient buffer before the sticky element for it to engage early) |
| Trigger       | Any room/device-edit page where total content height exceeds viewport height by more than the sticky bar's own height + `pb-32` (128px) |
| Condition     | Long power-point lists (any room with several power points/devices) trigger this reliably; device-edit is viewport-height-dependent and only manifests on tall windows or short forms |
| Related files | `client/src/features/flat-structure/components/PowerPointEditor.tsx` (not yet reviewed — contributes to list length but not to the sticky mechanism itself); `client/src/components/AppShell.tsx` (establishes `<main>` as intended-but-inert scroll boundary; see Finding 2) |

## Conclusion

**Confidence:** High (RoomEditor/power-points-view root cause) / Medium (DeviceEditor Safari variant, same mechanism family but Safari-specific manifestation unconfirmed).

Action Item #1 is resolved: this is a **`StickyActionBar` design defect**, not the room-list's per-room Save button (which was never meant to be sticky and is unaffected). The defect is confirmed root-caused and live-reproduced: `position: sticky` on a trailing sibling only ever "sticks" for the last few percent of scroll when the reserved buffer (`pb-32`, ~128px) is small relative to how far the content exceeds the viewport — for the tested 5-power-point room, the Save bar was invisible for the first 93% of scrolling and only appeared once the user had scrolled essentially to the end, functionally indistinguishable from a plain static element. `DeviceEditor` shares the exact same code pattern and is expected to exhibit the same class of failure; the Safari screenshot's more severe "bar entirely absent, no scrollbar" symptom is a plausible but Safari-unconfirmed variant of the same underlying cause.

## Recommended Next Steps

### Fix direction

Structural, not a point patch: a trailing `position: sticky` sibling cannot deliver "always visible without scrolling" — that requires either (a) `position: fixed` for the action bar (removed from document flow entirely, with the scrollable content given `padding-bottom` sized to the bar's actual rendered height, recalculated for dynamic content like error/success messages), or (b) restructuring so the scrollable region is a fixed-height flex child *above* the action bar (bar stays in normal flow as the last, non-scrolling flex item, content area scrolls independently) — closer to a typical mobile app "content + fixed footer" layout. Either direction eliminates the buffer-math problem entirely rather than tuning `pb-32`. This aligns with Sally's already-recommended unified save-affordance design pass (Epic 8 retro Action Item #2) — recommend folding this root cause into that design pass rather than a standalone patch, since `DeviceEditor` needs the identical structural fix and any new pattern should apply to both.

### Diagnostic

For the DeviceEditor Safari hypothesis: reproduce in real Safari and run the same `getComputedStyle`/`getBoundingClientRect` inspection performed in this session to confirm whether it's the identical mechanism or the distinct `100vh`-mismatch theory (Hypothesis 1).

## Reproduction Plan

1. Open `energytracker.ralfonsoftware.de/settings/structure`.
2. Click into any room with 4+ power points (e.g. "Wohnzimmer").
3. Without scrolling, observe: no Save button visible anywhere in viewport.
4. Scroll down roughly halfway through the power-point list: Save button still not visible.
5. Continue scrolling to the very bottom of the page: Save button now appears, styled as a plain trailing element rather than a floating bar.
6. (Optional, via devtools console) `document.scrollingElement.scrollHeight - document.scrollingElement.clientHeight` gives `maxScroll`; the bar only becomes stuck in the final `(barHeight + pb-32-remainder)` px of that range.

## Side Findings

- `<main>` in `AppShell.tsx:25` carries `overflow-y-auto` but never actually clips content in the tested scenario (`main.clientHeight === main.scrollHeight`) — the real scroll happens at `<html>`. Not a bug per se (page still scrolls correctly), but worth knowing if a future fix assumes `<main>` is the scroll boundary.
- Confirms Epic 8 retro Challenge #5's assessment was correct: "`StickyActionBar` positioning cannot be verified by automated tests" — this defect is a layout/scroll-math issue invisible to `jsdom`-based tests and was only catchable by live viewport interaction, exactly as the retro anticipated.

## Follow-up: 2026-07-18

_(none yet — investigation opened and concluded same day)_
