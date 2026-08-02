# Investigation: Reading History sheet hides its newest entries and close button off-screen

## Hand-off Brief

1. **What happened.** The Reading History bottom sheet (`ReadingHistorySheet` inside `TrendChart`'s `SheetContent`) has
   no height cap and no internal scroll container. Once its content (title + reading rows + "Mehr laden") taller than
   the viewport, the whole `position: fixed` box grows upward past `top: 0`, permanently pushing its title, newest
   rows, and the close (X) button above the visible viewport with no way to scroll to them.
2. **Where the case stands.** Root cause Confirmed via live reproduction against `energytracker.ralfonsoftware.de`:
   measured `getBoundingClientRect()` shows the sheet's computed `top: -210px` while `overflow-y: visible` and
   `max-height: none`.
3. **What's needed next.** Add `max-height` + `overflow-y-auto` (with the existing header/footer kept sticky, or at
   least the close button pinned) to `SheetContent`'s bottom variant, or to `ReadingHistorySheet`'s own list wrapper.
   Trivial-fix candidate — one Tailwind class change. Route to `bmad-quick-dev`.

## Case Info

| Field            | Value                                                                      |
| ---------------- | -------------------------------------------------------------------------- |
| Ticket           | N/A (user-reported UI bug)                                                 |
| Date opened      | 2026-08-02                                                                  |
| Status           | Concluded                                                                   |
| System           | Frontend, React 19 + Tailwind v4 + shadcn/ui Sheet (Radix Dialog), prod at `energytracker.ralfonsoftware.de`, reproduced in Chrome (desktop viewport 1728x846) |
| Evidence sources | Live browser reproduction (DOM, computed styles, accessibility tree), source code, git history, prior investigation `sheet-dialog-close-button-contrast-investigation.md` |

## Problem Statement

Ralf: "we have paging now in the meter history... What I'm missing: the newest values as well (newest is from today,
and I see it in database) and a close button." Screenshot showed the sheet's visible rows starting at 29.07.2026,
with no close button visible anywhere. Dashboard's "Letzte Ablesung" showed 02.08.2026, 12:50 as the true newest
reading — three days newer than the visible top row.

## Evidence Inventory

| Source                                                        | Status    | Notes                                                                 |
| -------------------------------------------------------------- | --------- | ---------------------------------------------------------------------- |
| Live app (fresh Chrome tab, own session)                       | Available | Reproduced independently of user's tab                                |
| `client/src/features/readings/components/ReadingHistorySheet.tsx` | Available | No scroll/height constraint in its own markup                        |
| `client/src/components/ui/sheet.tsx`                            | Available | `sheetVariants` bottom variant: `inset-x-0 bottom-0`, no top/height cap |
| `client/src/features/dashboard/components/TrendChart.tsx`       | Available | `SheetContent` usage site — no `max-h-*`/`overflow-y-*` override       |
| `client/src/features/readings/hooks/useReadingHistory.ts`, `api/readingApi.ts` | Available | Confirms data layer (skip/take, newest-first) is not implicated |
| `api/Features/Readings/GetReadingHistoryFunction.cs`            | Available | `OrderByDescending(ReadingDate)` — backend ordering correct, ruled out |
| git history of `ReadingHistorySheet.tsx`, `sheet.tsx`           | Available | Confirms overflow gap is pre-existing (since story 3.6/3.4), not a 12.5 regression |
| Prior case `sheet-dialog-close-button-contrast-investigation.md` | Available | Different defect (color contrast, fixed 2026-07-04) — ruled out as cause of *this* report |

## Timeline of Events

| Time                  | Event                                                                 | Source                          | Confidence |
| ---------------------- | ---------------------------------------------------------------------- | -------------------------------- | ---------- |
| 2026-07-04             | Close-button color-contrast bug fixed in `TrendChart.tsx` (and 3 other sites) | `sheet-dialog-close-button-contrast-investigation.md` | Confirmed |
| 2026-08-02 (commit 8f51b24) | Story 12.5 ships on-demand paging (`useInfiniteQuery`, 20/page) + cache-invalidation fix | git log, story file              | Confirmed |
| 2026-08-02, 12:50      | Newest `MeterReading` (7.511 kWh) submitted — visible on dashboard, absent from visible sheet viewport | Live app dashboard "Letzte Ablesung" | Confirmed |
| 2026-08-02             | Ralf opens Reading History sheet, reports missing newest values + missing close button | User report + screenshot         | Confirmed |

## Confirmed Findings

### Finding 1: The reading history data is correct and complete — the newest 3 readings ARE in the DOM

**Evidence:** Accessibility-tree read of the live sheet (`read_page` on `ref_8`, the dialog):
```
listitem → button → "02.08.2026, 12:50" / "7.511 kWh"
listitem → button → "01.08.2026, 14:44" / "7.506 kWh"
listitem → button → "29.07.2026, 17:39" / "7.491 kWh"
listitem → button → "28.07.2026, 20:57" / "7.485 kWh"   ← first row visible in the actual screenshot
...
button "Mehr laden"
button → "Close"
```
20 items total on the first page (matches `PAGE_SIZE = 20` in `useReadingHistory.ts:4`), newest-first, exactly as
the backend returns them. Nothing is missing from the fetch or the render — three rows plus the heading render
before the visually-first row a screenshot shows.

### Finding 2: The sheet's close button also exists in the DOM, unconditionally

**Evidence:** Same accessibility read — `button [ref_92] → generic "Close" [ref_93]` present at the end of the
dialog's children, matching `SheetPrimitive.Close` in `client/src/components/ui/sheet.tsx:66-69`. The prior
close-button contrast fix (`[&>button]:text-white/60 ...`) is still present in `TrendChart.tsx:84`, unchanged since
2026-07-04 through stories 10.4, 11.9, and 12.5 (`git log -p` shows no touch to that class list).

### Finding 3: Both "missing" elements are rendered off-screen above the viewport, with no way to scroll to them

**Evidence:** Live `getBoundingClientRect()` + computed style read on the sheet's content container and its close
button:
```json
"viewport": { "w": 1728, "h": 846, "scrollY": 0 },
"closeBtnRect":  { "top": -201, "bottom": -157, ... },
"contentRect":   { "top": -210, "bottom": 846, "height": 1056 },
"headingRect":   { "top": -177, "bottom": -153, ... },
"bodyOverflow": "hidden",
"contentComputed": { "position": "fixed", "top": "-210px", "bottom": "0px",
                     "height": "1056px", "maxHeight": "none", "overflowY": "visible" }
```
The sheet box is `position: fixed; bottom: 0` with **auto height** (`1056px`, driven purely by content) and
**no `max-height`, no `overflow-y` constraint** (`overflowY: visible`). Because the box is taller than the 846px
viewport, its top edge sits 210px *above* `y=0` — literally off-screen. `document.body` has `overflow: hidden`
(the Radix scroll-lock applied while the sheet is open), so there is no scrollable ancestor that could bring the
overflowing top portion into view. The title heading, the newest 3 rows, and the close button (all inside the top
~210px of the box) are unreachable by any scroll gesture.

### Finding 4: The overflow constraint has never existed on this component — this is not a 12.5 regression

**Evidence:** `git log -p --follow` on `ReadingHistorySheet.tsx` (stories 3.6, 9.10, 12.5) and `sheet.tsx` (only
touched once, story 3.4) shows no commit ever added or removed an `overflow-y`/`max-h-*` class on either file.
`sheetVariants`'s `bottom` variant (`client/src/components/ui/sheet.tsx:37-38`) has read `"inset-x-0 bottom-0
border-t ..."` — no top/height cap — since it was introduced.

## Deduced Conclusions

### Deduction 1: The bug was always latent; it became visible once total content height crossed the viewport height

**Based on:** Findings 1-4.

**Reasoning:** The Sheet/Dialog primitive never constrained height on the bottom variant (Finding 4). As long as the
rendered content (title + rows + footer button) stayed shorter than the viewport, the box's `top` stayed ≥ 0 and
everything was reachable — this was true when reading counts were low (early stories) or, effectively, in any session
where the accumulated reading count was small. Story 12.5 didn't introduce a regression in the strict sense (it
didn't remove a scroll fix that used to exist), but it did change what "the list" means: previously it was
"fetch everything" (per the story's Dev Notes), now it's a paginated first page of 20 items — a size incidentally
large enough, combined with the accumulated real reading history (readings from 10.07 through 02.08, i.e. ~3.5
weeks of sub-daily entries), to push total content height past 846px and expose the pre-existing overflow gap for
the first time in this session.

**Conclusion:** Root cause is a missing height constraint on the Sheet's bottom-sheet variant / its usage site, not
a data, caching, or pagination-ordering bug. The paging data itself (order, cache invalidation, skip/take) is
correct per Findings 1 and Finding 2 of the earlier cache/paging story.

## Hypothesized Paths

None remaining open — direct live reproduction fully explains both reported symptoms with one mechanism; no
hypothesis was needed to close the case.

## Missing Evidence

None blocking. (Not verified: whether this same unbounded-height pattern also affects `TariffList.tsx`,
`EnterReadingSheet.tsx`, or `AddFlatForm.tsx`'s two `SheetContent` usages if their content grows long enough — out
of scope for this report, flagged as a Side Finding below.)

## Source Code Trace

| Element       | Detail                                                                                       |
| ------------- | ---------------------------------------------------------------------------------------------- |
| Error origin  | `client/src/components/ui/sheet.tsx:31-48` (`sheetVariants`, `bottom` variant has no height cap); consumed by `client/src/features/dashboard/components/TrendChart.tsx:82-87` (`SheetContent` usage, no `max-h-*`/`overflow-y-*` override either) |
| Trigger       | Opening the Reading History sheet once accumulated `MeterReading` rows for the current page (up to 20, `PAGE_SIZE` in `useReadingHistory.ts:4`) render taller than the viewport |
| Condition     | `position: fixed` sheet with `height: auto` and `overflow-y: visible`, anchored to `bottom: 0` — grows upward past `top: 0` with `document.body { overflow: hidden }` blocking any compensating scroll |
| Related files | `client/src/features/readings/components/ReadingHistorySheet.tsx` (the list itself, also unconstrained), `client/src/components/ui/dialog.tsx` (same `sheetVariants`-style pattern, worth checking if `Dialog` has an equivalent bottom-anchored usage) |

## Conclusion

**Confidence:** High (Confirmed root cause via direct live-DOM measurement; mechanism fully explains both reported
symptoms without contradiction).

Both of Ralf's reports are the same bug: the Reading History bottom sheet has no `max-height`/`overflow-y-auto`
constraint, so once its content (title, up to 20 reading rows, "Mehr laden" button) is taller than the browser
viewport, the `position: fixed` box overflows *upward* past the top of the screen. The newest readings and the
close button are rendered correctly in the DOM (confirmed present, in the right order, with the correct data) —
they are simply unreachable because nothing above `y=0` can be scrolled into view. This is not a data/caching/paging
regression from story 12.5; the height constraint has never existed on this component, and the bug was only
exposed now because content height finally exceeded viewport height.

## Recommended Next Steps

### Fix direction

Add a height cap and internal scroll to the sheet's scrollable region — the two idiomatic options:
1. **Constrain `SheetContent`'s bottom variant** in `client/src/components/ui/sheet.tsx:37-38`, e.g. add
   `max-h-[85vh] overflow-y-auto` to the `bottom` variant string (affects all bottom sheets app-wide).
2. **Or constrain locally** in `TrendChart.tsx:84`'s `className`, adding `max-h-[85vh] overflow-y-auto` scoped to
   just the reading-history usage, keeping the fix local like the close-button color override already is.

Given the close-button color fix precedent was applied per-site (not in the generated `sheet.tsx`), option 2 keeps
convention; option 1 fixes it once for every current and future bottom sheet. Recommend option 1 since an unbounded
bottom sheet is a systemic risk (see Side Findings), with the close button's `absolute right-4 top-4` needing to stay
reachable — pinning it outside the scrollable region (or keeping it `fixed` relative to the outer box while only the
list scrolls) is the detail to get right in implementation.

### Diagnostic

None needed — mechanism is deterministic and fully reproduced.

## Reproduction Plan

1. Open `energytracker.ralfonsoftware.de`, ensure the active flat has ≥ 15-20 `MeterReading` rows (enough that title +
   rows + footer exceed viewport height — true for the "Zuhause" flat as of 2026-08-02).
2. Click the history (clock) icon on the Trend Chart card to open the Reading History sheet.
3. Observe: the sheet opens already "scrolled" — the visible viewport shows rows partway down the list (e.g.
   starting at 28.07.2026 instead of the true newest, 02.08.2026); no title, no close (X) button visible anywhere
   on screen.
4. Confirm via DevTools/`getBoundingClientRect()` on the dialog content: `top` is negative, `overflow-y: visible`,
   `max-height: none`, `document.body.overflow: hidden`.

## Side Findings

- The `sheetVariants` `bottom` variant has never had a height cap since its introduction (story 3.4) — any other
  bottom sheet whose content grows long enough (more rows, more form fields, wider content, larger font/zoom) is
  equally exposed. `EnterReadingSheet.tsx` and `AddFlatForm.tsx` also use bottom `SheetContent` per the earlier
  close-button-contrast case's Finding 4 — worth a quick check whether their content can realistically exceed
  viewport height, though neither is reported broken today.
- This is a second, independent defect on the exact same component (`TrendChart.tsx`'s `SheetContent` /
  `ReadingHistorySheet`) that previously had the close-button color-contrast bug fixed on 2026-07-04
  (`sheet-dialog-close-button-contrast-investigation.md`). That fix is still intact and unrelated to this bug —
  worth noting only because two different overlay defects have now surfaced on the same sheet within a month.
