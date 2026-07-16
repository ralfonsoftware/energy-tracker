# Investigation: Device Editor — content does not scroll on mobile, energy consumption fields unreachable

## Hand-off Brief

1. **What happened.** On mobile, the Device Editor's `StickyActionBar` (Abbrechen/Speichern) visually covers the last form field (the kWh input) once the page is scrolled to its true bottom, because the content wrapper's bottom padding (`pb-10` = 40px) is smaller than the sticky bar's rendered height (~80px) — the page is not actually scroll-locked, the last field is just hidden underneath the bar with no further scroll room to reveal it.
2. **Where the case stands.** Root cause Confirmed via a real device screenshot: the app's persistent bottom tab bar is fully visible below the sticky Abbrechen/Speichern buttons, proving the page has reached its actual scroll end — yet the "KWH PRO WOCHE" input is almost entirely obscured by the sticky bar sitting on top of it.
3. **What's needed next.** Trivial, well-scoped fix (increase bottom padding on the scrollable content to clear the sticky bar's rendered height, plus a matching check on `RoomEditor` which shares the identical pattern) — hand off to `bmad-quick-dev`.

## Case Info

| Field            | Value                                                                      |
| ---------------- | -------------------------------------------------------------------------- |
| Ticket           | N/A                                                                        |
| Date opened      | 2026-07-16                                                                 |
| Status           | Active                                                                     |
| System           | Mobile web (iPhone Safari implied — consistent with prior mobile bugs in this area, e.g. commit `97d52ff`) |
| Evidence sources | Source code (`DeviceEditor.tsx`, `StickyActionBar.tsx`, `FlatStructureEditor.tsx`, `AppShell.tsx`), version control (commits `97d52ff`, `f8590fd`, `0bda6aa`), user-supplied screenshot (failed to transfer) |

## Problem Statement

"In edit device the content does not scroll and I can't edit the energy consumption values." (verbatim, Ralf, 2026-07-16). Treated as hypothesis pending independent confirmation.

## Evidence Inventory

| Source   | Status                          | Notes     |
| -------- | ------------------------------- | --------- |
| User screenshot | Available | Second attachment (image #4) is a genuine iPhone Safari capture of the Device Editor ("Waschmaschine" device, `ker.ralfonsoftware.de`), showing the exact defect. First attachment was broken (see Side Findings). |
| Source code (DeviceEditor.tsx, StickyActionBar.tsx, FlatStructureEditor.tsx, AppShell.tsx) | Available | Read in full |
| Git history (recent commits touching this area) | Available | `97d52ff` (room-list overflow fix), `f8590fd` (story 8.2 — sticky action bar + viewport fix), `0bda6aa` (story 8.1 — autosave) |
| Live mobile repro | Superseded by screenshot | Real-device screenshot provides equivalent evidence; live repro no longer required to confirm root cause |

## Investigation Backlog

| # | Path to Explore | Priority              | Status                                | Notes     |
| - | --------------- | --------------------- | ------------------------------------- | --------- |
| 1 | Get a real screenshot or live repro of the Device Editor on a narrow mobile viewport | High | Done | Second attachment (image #4) confirmed the defect directly |
| 2 | Confirm whether `AppShell`'s scroll container (`main.overflow-y-auto`) actually receives overflow when `DeviceEditor`'s content exceeds viewport height | Low | Done — moot | Screenshot shows scrolling works correctly and reaches true document end (persistent tab bar fully visible); overflow/scroll detection was never broken |
| 3 | Compare `DeviceEditor`'s sticky-bar wiring against `RoomEditor`'s (same commit, same `StickyActionBar`) to see if one works and the other doesn't | Medium | Open | `RoomEditor.tsx:57` has the identical `pb-10` vs. sticky-bar-height mismatch; not yet visually confirmed broken there but the same fix should be applied defensively |
| 4 | Fix: increase bottom padding (or otherwise reserve space) on `DeviceEditor`'s and `RoomEditor`'s scrollable content so it clears `StickyActionBar`'s rendered height | High | Open | Implementation, not investigation — hand off to `bmad-quick-dev` |

## Timeline of Events

| Time        | Event               | Source                | Confidence            |
| ----------- | ------------------- | --------------------- | --------------------- |
| 2026-07-16 08:51 | Story 8.2: `StickyActionBar` extracted; `DeviceEditor` and `RoomEditor` both switched from an in-flow bottom action row (`<div className="flex-1" />` spacer + inline buttons) to `position: sticky` bar | commit `f8590fd` | Confirmed |
| 2026-07-16 10:57 | Follow-up fix: room-list row overflow on iPhone Safari (unrelated screen — room list, not device editor) | commit `97d52ff` | Confirmed |
| 2026-07-16 (today) | User reports Device Editor content doesn't scroll, energy consumption fields unreachable | User report | Confirmed (report only; underlying claim not yet independently verified) |
| 2026-07-16 (today) | User provides real iPhone Safari screenshot of Device Editor for "Waschmaschine" (SelfMeasured approach, Weekly period expanded) showing kWh input hidden under sticky Abbrechen/Speichern bar, with app's persistent tab bar fully visible beneath — page is at true scroll end | User-supplied screenshot (image #4) | Confirmed |

## Confirmed Findings

### Finding 1: DeviceEditor's outer container has no explicit scroll boundary of its own

**Evidence:** `client/src/features/flat-structure/components/DeviceEditor.tsx:82` — `<div className="flex-1 flex flex-col" style={{ background: '#111827', minHeight: '100vh' }}>`

**Detail:** The form relies entirely on an ancestor for scrolling. `minHeight: '100vh'` sets a floor, not a ceiling — it does not create an overflow/scroll context by itself (no `overflow-y-auto`, no fixed `height`).

### Finding 2: The actual scroll container is `AppShell`'s `<main>`

**Evidence:** `client/src/components/AppShell.tsx:23-29` — `<main ref={mainRef} className="flex-1 overflow-y-auto pb-[calc(84px_+_env(safe-area-inset-bottom,0px))] md:pb-0">`

**Detail:** `main` is the only element in the render tree with `overflow-y-auto`. It sits inside `<div className="flex min-h-screen">` (`AppShell.tsx:19`), which is **not** height-constrained (`min-height`, not `height`) — meaning `main`'s flex-stretched height is driven by its content, not clamped to the viewport, so in a browser environment `main` would need the flex parent to actually cap its height for `overflow-y-auto` to activate a scrollbar rather than the element just growing taller than the viewport (document-level scroll takes over instead, which is normally fine in desktop Safari/Chrome but is the exact class of behavior that misbehaves on iOS Safari with dynamic toolbars).

### Finding 3: `StickyActionBar` uses `position: sticky` anchored with a `bottom` offset that hardcodes the mobile bottom-tab-bar height

**Evidence:** `client/src/features/flat-structure/components/StickyActionBar.tsx:8-14` — `className="sticky bottom-[calc(84px_+_env(safe-area-inset-bottom,0px))] md:bottom-0 ..."`

**Detail:** `position: sticky` requires a scrolling ancestor with defined bounds to stick within. If that ancestor's overflow never triggers (Finding 2), `sticky` degrades to normal in-flow behavior — it would not be a scroll blocker per se, but combined with the JSX ordering it needs checking (Finding 4).

### Finding 4: In DeviceEditor's JSX, `StickyActionBar` is a *sibling* of the scrollable content div, not a child of it

**Evidence:** `client/src/features/flat-structure/components/DeviceEditor.tsx:81-322` — structure is:
```
<div className="flex-1 flex flex-col" style={{ minHeight: '100vh' }}>   ← outer
  <div className="px-6 pt-4 flex-1 flex flex-col">                      ← header + form wrapper
    <h1>...</h1>
    <div className="flex flex-col gap-4 pb-10">...all form fields...</div>
  </div>
  <StickyActionBar>...</StickyActionBar>                                ← sibling of the wrapper above, child of outer
</div>
```
This mirrors `RoomEditor`'s structure (also fixed in the same commit) — not an obvious structural asymmetry between the two.

### Finding 5: Real device screenshot shows the page scrolled to its true end, with the last input hidden under the sticky bar

**Evidence:** User-supplied screenshot, image #4 (iPhone Safari, `ker.ralfonsoftware.de`, device "Waschmaschine", SelfMeasured/Weekly approach expanded).

**Detail:** Bottom-to-top in the screenshot: the app's persistent `BottomTabBar` (Übersicht/Erkenntnisse/Verbrauch/Einstellungen, `AppShell.tsx:31-33`) is fully visible with no cut-off — this only renders below the scroll container, so its full visibility proves `main` has reached `scrollHeight === scrollTop + clientHeight` (true scroll end). Directly above it: the `StickyActionBar`'s Abbrechen/Speichern buttons (`DeviceEditor.tsx:301-321`), rendered correctly, not overlapping the tab bar. Directly above that: only the top ~15% of the "KWH PRO WOCHE" input box is visible (`DeviceEditor.tsx:280-292`) before it's cut off by the sticky bar's top edge — the rest of the input, including where a user would tap to focus and type, is covered.

## Deduced Conclusions

### Deduction 1: The bug is a bottom-padding / sticky-bar-height mismatch, not a scroll-lock

**Based on:** Finding 1, Finding 2, Finding 3, Finding 5 (screenshot).

**Reasoning:** The screenshot shows the app's persistent `BottomTabBar` fully visible beneath the sticky Abbrechen/Speichern buttons — this bar only comes into view once the scroll container (`AppShell`'s `main`) has reached its actual `scrollHeight`. That rules out a true scroll-lock: the page scrolls correctly and completely. What remains visible above the sticky bar is only a sliver of the "KWH PRO WOCHE" input's top edge. `StickyActionBar`'s rendered height on mobile is approximately: `h-14` button (56px) + `py-3` vertical padding (24px) + 1px top border ≈ **81px**. `DeviceEditor`'s scrollable content wrapper reserves only `pb-10` = **40px** of bottom padding (`DeviceEditor.tsx:86`) before the sticky bar. Since the sticky bar's box is a sibling that occupies its own space in flow but renders pinned at the bottom of the scrollport, the last ~41px of whatever content sits just above it gets visually covered once the page is scrolled to the end — which in this form is exactly where the last field (kWh input, `h-[52px]`) lives.

**Conclusion:** Confirmed. This is a padding/height mismatch between `StickyActionBar`'s real rendered footprint and the bottom padding reserved on the scrollable content ahead of it, not a scroll-disabling bug. The user's "can't edit the energy consumption values" follows directly: the input is present and the page is scrolled correctly, but the field is unreachable because it's hidden under the action bar with no further scroll travel available to expose it.

## Hypothesized Paths

### Hypothesis 1: `100vh` / dynamic-toolbar mismatch causes DeviceEditor to under-report its content height, so no scroll is engaged

**Status:** Refuted

**Theory:** `minHeight: '100vh'` (`DeviceEditor.tsx:82`) combined with the non-height-constrained `AppShell` flex container means the browser may size the content area to exactly the layout viewport in some device-orientation/toolbar states, hiding overflow that should be scrollable.

**Supporting indicators:** This project has a documented history of iOS-Safari-specific viewport bugs in this exact feature area within the last few hours of work (`97d52ff` — room-list row overflow; `f8590fd` — "viewport fix" in its own commit title, for the same DeviceEditor/RoomEditor pair).

**Would confirm:** Live repro showing the energy-consumption inputs rendered below the visible fold with no scroll response to touch/swipe.

**Would refute:** Repro shows the page does scroll but the sticky bar overlaps/hides the inputs.

**Resolution:** Refuted by Finding 5 — the screenshot shows the app's persistent `BottomTabBar` fully visible beneath the sticky action bar, which is only possible once the scroll container has reached its true `scrollHeight`. Scroll/overflow detection works correctly; the defect is purely a bottom-clearance shortfall (see Hypothesis 2 and Deduction 1).

### Hypothesis 2: The scrollable content's bottom padding is smaller than `StickyActionBar`'s actual rendered height, so the sticky bar occludes the last field at full scroll

**Status:** Confirmed

**Theory:** `DeviceEditor.tsx:86` reserves `pb-10` (40px) below the form fields, but `StickyActionBar` (`StickyActionBar.tsx:8-14`) renders at ~81px tall on mobile (`h-14` button + `py-3` padding + border). At maximum scroll, the sticky bar's stuck position covers the ~41px shortfall, which lands exactly on the last field — the kWh input.

**Supporting indicators:** `RoomEditor.tsx:57` has the identical `pb-10` before its own `StickyActionBar`, meaning it carries the same latent risk (Backlog #3) even though it wasn't the one screenshotted.

**Would confirm:** Screenshot or repro showing the last form field partially hidden under the sticky bar while the page is provably at full scroll (persistent tab bar visible below the bar).

**Would refute:** Repro showing the sticky bar always leaves the last field fully visible and reachable, or shows a different obstruction (e.g., z-index stacking, focus-trap).

**Resolution:** Confirmed by Finding 5 and Deduction 1 — screenshot shows exactly this state: full scroll reached (tab bar visible), sticky bar rendered correctly, last field (kWh input) hidden underneath because of the padding shortfall.

## Missing Evidence

| Gap              | Impact                               | How to Obtain   |
| ---------------- | ------------------------------------ | --------------- |
| Whether `RoomEditor` exhibits the same occlusion in practice | `RoomEditor.tsx:57` has the identical `pb-10`-vs-sticky-bar-height shortfall but wasn't screenshotted; its last power-point card may happen to clear the bar by luck of content length | Quick manual check with a room that has several power points, or apply the fix defensively to both without further repro (low risk, same root cause) |

## Source Code Trace

| Element       | Detail                                      |
| ------------- | -------------------------------------------- |
| Error origin  | `client/src/features/flat-structure/components/DeviceEditor.tsx:86` — `pb-10` (40px) is less than `StickyActionBar`'s rendered height (~81px on mobile) |
| Trigger       | Scrolling the Device Editor form to its end on a mobile viewport where the sticky bar is in its "stuck" state (any time content height exceeds available viewport height) |
| Condition     | Sticky bar's rendered footprint (`StickyActionBar.tsx:8-14`: `h-14` button + `py-3` padding + border ≈ 81px) exceeds the bottom padding reserved ahead of it, so the last ~41px of content is covered at full scroll |
| Related files | `client/src/features/flat-structure/components/RoomEditor.tsx:57` (identical pattern, same latent defect), `client/src/features/flat-structure/components/StickyActionBar.tsx` (shared sticky-bar component), `client/src/components/AppShell.tsx` (outer scroll container — confirmed working correctly, not implicated) |

## Conclusion

**Confidence:** High

Root cause Confirmed via a real device screenshot: `DeviceEditor`'s scrollable content reserves only 40px of bottom padding (`DeviceEditor.tsx:86`, `pb-10`) ahead of the `StickyActionBar`, which actually renders at roughly 81px tall on mobile. At full scroll, the sticky bar's stuck position covers the last ~41px of content — which in this form is the last field, the kWh input — making it visible but untappable/unreadable. The page scrolls correctly and completely (proven by the app's persistent bottom tab bar being fully visible in the screenshot); this is not a scroll-lock, it's insufficient bottom clearance for a sticky footer of its actual size. `RoomEditor.tsx:57` shares the identical padding value ahead of the same `StickyActionBar` component and carries the same latent defect, just not directly evidenced by screenshot.

## Recommended Next Steps

### Fix direction

Increase the bottom padding on the scrollable content wrapper in both `DeviceEditor.tsx:86` and `RoomEditor.tsx:57` to reliably clear `StickyActionBar`'s rendered height (e.g. `pb-24`/`pb-28` instead of `pb-10`, or compute/reserve space matching the bar's actual height so it isn't a magic-number guess that drifts again if the bar's content changes, e.g. when `saveError`/`saveSuccess` banners add height in `RoomEditor`). One-line-per-file style fix, no structural change needed — good fit for `bmad-quick-dev`.

### Diagnostic

None required — root cause is Confirmed with direct visual evidence.

## Reproduction Plan

1. `swa start` (per `project-context.md` — required for auth simulation; `npm run dev` alone returns 403 on API calls).
2. Navigate to Settings → flat structure editor → a room → a power point → edit/add a device.
3. Set viewport to a narrow mobile width (e.g. DevTools iPhone 14 Pro, 393×852).
4. Select "Selbst gemessener Durchschnitt" (SelfMeasured) or "EU-Energielabel" to expand the consumption sub-form, pushing the kWh field to the bottom.
5. Scroll to the end — confirm the last field is hidden under the Abbrechen/Speichern bar, matching the reported screenshot.

## Side Findings

- The first image attachment in this conversation (`~/.claude/image-cache/.../3.png`) was a generic 1024×1024 "PNG file" placeholder icon rather than real screenshot content — an attachment-transfer failure, not a code defect. Worth being aware of if screenshots seem "off" in future bug reports; ask for a re-send rather than trusting the first attempt.
