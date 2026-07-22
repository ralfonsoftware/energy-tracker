# Investigation: Flat switcher dropdown panel clipped by header's backdrop-filter box

> **Folded into `architecture.md`** (2026-07-22 doc consolidation, Epic 9 retro Action Item #2): the `backdrop-filter` compositing-layer clipping gotcha and the portal-based-overlay rule are now AD-19a in Frontend Architecture. This file remains as the historical record.

## Hand-off Brief

1. **What happened.** The flat switcher dropdown (opened via the "Zuhause ▾" button) is visually cut off at the
   bottom edge of the `<header>` element — only the first list row is visible; the "Wohnung hinzufügen" row and any
   additional flats are clipped away, with dashboard content showing through underneath.
2. **Where the case stands.** Root cause is Deduced with high confidence from code inspection and screenshot
   geometry: `<header>` (client/src/components/Header.tsx:8-12) sets `backdropFilter: 'blur(20px)'`, and the
   dropdown panel (client/src/components/FlatSwitcher.tsx:60-95) is a plain `position: absolute` `<div>` nested
   *inside* that header rather than portaled out — WebKit (this repro is Mobile Safari) clips absolutely-positioned
   overflow content to the bounding box of an ancestor that has `backdrop-filter` applied.
3. **What's needed next.** Fix direction is clear (see Recommended Next Steps) and low-risk; recommend
   `bmad-quick-dev` to implement, or `bmad-code-review` first if a broader portal-based refactor is preferred.

## Case Info

| Field            | Value                                                                                   |
| ---------------- | ---------------------------------------------------------------------------------------- |
| Ticket           | N/A                                                                                     |
| Date opened      | 2026-07-04                                                                              |
| Status           | Concluded                                                                               |
| System           | Mobile Safari (iOS), production deployment (`*.ralfonsoftware.de`, SWA)                |
| Evidence sources | User screenshot, source code (`Header.tsx`, `FlatSwitcher.tsx`, `AppShell.tsx`, sibling dropdown implementations), git history |

## Problem Statement

User report: "the new flat switcher overlay is partially hidden," with a screenshot showing the dropdown's first row
("Zuhause") visible directly under the trigger button, then abruptly cut off with dashboard content (CTA button, KPI
tiles) visible underneath through the translucent dropdown background.

## Evidence Inventory

| Source                                    | Status    | Notes                                                                 |
| ------------------------------------------ | --------- | ---------------------------------------------------------------------- |
| User screenshot                            | Available | Shows precise clip boundary at y≈305px, aligning with header's bottom edge |
| `client/src/components/Header.tsx`         | Available | `backdropFilter: 'blur(20px)'` on `<header>`, no explicit `overflow` |
| `client/src/components/FlatSwitcher.tsx`   | Available | Dropdown panel is a raw `absolute` `<div>`, child of `<header>`, not portal-based |
| `client/src/components/AppShell.tsx`       | Available | `<main className="overflow-y-auto">` wraps `<Header/>` + `<Outlet/>` — ruled out as clip source (geometry mismatch, see Deduction 2) |
| `client/src/components/LocaleDropdown.tsx` | Available | Same hand-rolled absolute-div pattern; not confirmed to reproduce (different ancestor, not yet tested) |
| `client/src/index.css`                     | Available | No global `overflow` rule targeting `header` — rules out a CSS reset explanation |
| Git history                                | Available | FlatSwitcher introduced in `4f63a1b feat: story 5.2 - flat switcher and other UI additions` |

## Timeline of Events

| Time       | Event                                                          | Source                | Confidence |
| ---------- | ---------------------------------------------------------------- | ---------------------- | ---------- |
| 2026-06-xx | `4f63a1b` introduces `FlatSwitcher.tsx` with hand-rolled dropdown | `git log`              | Confirmed  |
| 2026-07-04 | User observes clipped dropdown on production (Mobile Safari)     | User report + screenshot | Confirmed |

## Confirmed Findings

### Finding 1: Header applies `backdrop-filter` with no explicit overflow rule

**Evidence:** `client/src/components/Header.tsx:5-13`

```tsx
<header
  role="banner"
  className="flex items-center px-4 py-3"
  style={{
    background: 'rgba(255,255,255,0.07)',
    border: '1px solid rgba(255,255,255,0.12)',
    backdropFilter: 'blur(20px)',
  }}
>
```

### Finding 2: Dropdown panel is a plain absolutely-positioned DOM child, not a portal

**Evidence:** `client/src/components/FlatSwitcher.tsx:60-95`

The dropdown (`role="listbox"`, `absolute left-0 mt-1 ... z-10`) is rendered directly inside `<div ref={dropdownRef}
className="relative">`, which itself is a direct child of `<header>`. There is no `Portal` (e.g., Radix
`Portal`/`DropdownMenu`) taking it out of the header's DOM subtree.

### Finding 3: A sibling feature uses the portal-based pattern correctly

**Evidence:** `client/src/features/dashboard/components/CostGapBadge.tsx:18-24` uses shadcn/Radix `Popover` /
`PopoverContent`, which portals its content to `document.body` by default — escaping any ancestor's `backdrop-filter`
or `overflow` clipping. This is the pattern the codebase already uses elsewhere for exactly this class of problem.

## Deduced Conclusions

### Deduction 1: `backdrop-filter` on `<header>` clips the absolutely-positioned dropdown to the header's box

**Based on:** Finding 1, Finding 2, and screenshot geometry (clip line at y≈305px matches the header's bottom
border precisely).

**Reasoning:** WebKit/Safari renders elements with `backdrop-filter` in an isolated compositing layer bounded by the
element's own box, because the effect needs a well-defined region to sample "what's behind" it. This has the visual
side effect of clipping absolutely-positioned descendant content that extends past that box — even though nothing in
the component sets `overflow: hidden` explicitly. The dropdown's containing block (`.relative` div) is a descendant of
`<header>`, so its overflow is subject to this clip.

**Conclusion:** The dropdown is being cut off exactly at the header's box edge because it never escapes the header's
`backdrop-filter` compositing boundary.

### Deduction 2: `<main className="overflow-y-auto">` (AppShell.tsx:15) is not the clip source

**Based on:** Finding evidence + geometry — `main`'s own box spans from the top of the page to (at minimum) full
viewport height, and its scrollable content (header + full dashboard) is far taller. If `main`'s overflow were
clipping the dropdown, the clip boundary would occur at `main`'s own top/bottom edges, not ~305px down the page, mid
layout, aligned exactly with the header's border. The observed clip line rules this out as the mechanism (ruled out,
not Confirmed absent risk — see Missing Evidence).

## Hypothesized Paths

### Hypothesis 1 (user's implicit premise): dropdown has a z-index/stacking bug

**Status:** Refuted

**Theory:** The dashboard content is rendering on top of the dropdown due to a z-index conflict.

**Supporting indicators:** Dashboard content is visible "through" the dropdown in the screenshot, which superficially
looks like a stacking issue.

**Would confirm:** Dashboard cards having an explicit `z-index` ≥ the dropdown's `z-10`.

**Would refute:** No dashboard component sets `z-index`; the visible dashboard content below the clip line is simply
un-clipped page content, not content painted over the dropdown — the dropdown region itself is truncated, not
covered.

**Resolution:** Refuted — no dashboard component (`KpiTile.tsx`, etc.) sets a `z-index`. The dropdown isn't being
painted-over; its box is truncated at the header boundary, consistent with a clipping (not stacking) mechanism.

## Missing Evidence

| Gap                                                                 | Impact                                                                 | How to Obtain                                                                 |
| ---------------------------------------------------------------------- | ------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| Live DevTools inspection (computed style / layer borders) confirming WebKit's backdrop-filter clip | Would move Deduction 1 from Deduced to Confirmed | Open the production site in Safari, enable "Show compositing borders," inspect `<header>`'s layer bounds while the dropdown is open |
| Whether the same clip reproduces on `LocaleDropdown.tsx` (same pattern, different ancestor) | Would confirm this is a systemic pattern issue vs. one-off | Open the locale switcher (Settings page) on the same Mobile Safari device and check for identical clipping |
| Chromium/Firefox repro                                              | Confirms whether this is WebKit-specific or a general `backdrop-filter` behavior | Reproduce on desktop Chrome/Firefox dev tools mobile emulation |

## Source Code Trace

| Element       | Detail                                                                                          |
| ------------- | -------------------------------------------------------------------------------------------------- |
| Error origin  | `client/src/components/FlatSwitcher.tsx:60-95` (dropdown panel), clipped by `client/src/components/Header.tsx:8-12` (`backdropFilter`) |
| Trigger       | User taps "Zuhause ▾" to open the flat switcher dropdown                                        |
| Condition     | Rendering in a WebKit-based browser (Safari/iOS) where `backdrop-filter` on an ancestor clips overflowing absolutely-positioned descendants |
| Related files | `client/src/components/LocaleDropdown.tsx` (same hand-rolled pattern, likely same latent risk); `client/src/features/dashboard/components/CostGapBadge.tsx` (correct portal-based reference pattern already in the codebase) |

## Conclusion

**Confidence:** Medium-High (Deduced from Confirmed code evidence and precise screenshot geometry; not yet verified
live via DevTools — see Missing Evidence for the step that would elevate this to High/Confirmed).

The flat switcher dropdown is clipped because it is a plain `position: absolute` DOM child of `<header>`, and
`<header>`'s `backdropFilter: blur(20px)` causes WebKit to clip overflowing descendant content to the header's own
box. This is not a z-index/stacking bug (that hypothesis is refuted) — it's a clipping bug caused by nesting an
overflowing absolute element inside a `backdrop-filter` ancestor. The codebase already has the correct pattern
(`CostGapBadge.tsx`'s Radix `Popover`, which portals content to `document.body`), it just wasn't used for
`FlatSwitcher` (or `LocaleDropdown`, which shares the same latent risk).

## Recommended Next Steps

### Fix direction

Two viable mechanisms, in order of preference:

1. **Portal the dropdown panel out of `<header>`.** Replace the hand-rolled `absolute` `<div>` in `FlatSwitcher.tsx`
   with a Radix-based popover primitive (the codebase already has this dependency via shadcn — see
   `CostGapBadge.tsx`'s `Popover`/`PopoverContent` usage), so the panel renders in a portal to `document.body` and is
   no longer a descendant of the `backdrop-filter` header. This also fixes the same latent risk in
   `LocaleDropdown.tsx` if applied there too.
2. **(Narrower, not recommended as primary fix)** Move `backdropFilter` off `<header>` itself and onto an inner
   non-overflow-critical wrapper that doesn't contain the dropdown — riskier, since it changes the header's visual
   layering and doesn't address `LocaleDropdown.tsx`'s identical latent risk.

### Diagnostic

If higher certainty is wanted before implementing: open the production site in Safari on the affected device, open
the flat switcher, and use Safari Web Inspector's layer/compositing borders view to confirm the header forms a
clipped compositing layer — this would move Deduction 1 from Deduced to Confirmed.

## Reproduction Plan

1. Open the app in Mobile Safari (iOS) or Safari desktop responsive design mode.
2. Ensure the account has ≥2 flats (or at least the "add flat" row present) so the dropdown has more than one row.
3. Tap "Zuhause ▾" (or equivalent flat name) in the header to open the dropdown.
4. Observe: only the top portion of the dropdown is visible; the "Wohnung hinzufügen" row and any second/third flat
   row are clipped away at the header's bottom edge.
5. Expected (fixed) behavior: full dropdown panel visible, unclipped, painted above dashboard content.

## Side Findings

- `LocaleDropdown.tsx:60` uses an identical hand-rolled `absolute` pattern (`absolute right-0 mt-1 ... z-10`). It
  wasn't reported as broken, but shares the same architectural risk — worth checking whether its parent container
  also applies `backdrop-filter`/`filter`/`transform` (Hypothesized, not yet checked).
- The codebase already has a correct, portal-based reference implementation (`CostGapBadge.tsx`'s `Popover`) that the
  fix can mirror directly.
