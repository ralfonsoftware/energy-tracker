# Investigation: Room-list row overflow on iPhone Safari (flat-structure settings page)

## Hand-off Brief

1. **What happened.** The room-list row in `FlatStructureEditor.tsx` (name input + Save + "N Anschlüsse ›" + delete icon) is a single non-wrapping flex row with no `flex-wrap` and no `min-w-0` on the input — on iPhone-width viewports its four children's combined minimum width exceeds the screen, so the delete icon and part of the "N Anschlüsse" text run off the right edge (Confirmed).
2. **Where the case stands.** Root cause confirmed directly from source and git blame; the layout predates Story 8.1/8.2 (introduced in Story 6.0, 2026-07-06) and was never exercised on a real iPhone since — this is the same class of gap Story 8.2's still-outstanding AC6 manual Safari check was meant to catch. One detail remains Hypothesized: whether the clipped content is swipeable-but-hidden or truly unreachable.
3. **What's needed next.** Fix the row layout (wrap to a second row, or shrink/replace the text-heavy children with icons) — see Recommended Next Steps. The user's second request (removing the page-level "Speichern" button) is a separate scope question that conflicts with Story 8.2's own AC9 and needs a product decision, not a CSS fix.

## Case Info

| Field            | Value                                                                                                    |
| ---------------- | ---------------------------------------------------------------------------------------------------------|
| Ticket           | N/A — reported by Ralf via screenshot, framed as "story 8.2"                                              |
| Date opened      | 2026-07-16                                                                                                |
| Status           | Concluded                                                                                                   |
| System           | iPhone, Safari (mobile), flat-structure settings page (`/settings` → Wohnungsstruktur), room-list view    |
| Evidence sources | User screenshot, source code (`FlatStructureEditor.tsx`, `AppShell.tsx`), git blame/log                   |

## Problem Statement

User-reported (verbatim, lightly trimmed): "a visual bug for story 8.2 on iPhone and Safari: [screenshot]. The elements are overflowing. I would suggest that the power points ("Anschlussstellen") gets on second row and the action buttons save and delete together at the end of the first row. also save can be an icon. Additionally, the page top right save button can be removed, if we have only per-room save action."

Two distinct asks bundled together:
- **A.** A visual overflow bug in the room-list row on iPhone Safari, with a proposed layout fix (wrap "N Anschlüsse" to a second row; group Save+Delete at the end of row 1; Save as icon-only).
- **B.** A scope/design question: remove the page-level top-right "Speichern" button now that per-room Save exists.

## Evidence Inventory

| Source                                                    | Status    | Notes                                                                                                                    |
| ----------------------------------------------------------| --------- | -------------------------------------------------------------------------------------------------------------------------|
| Screenshot (`/Users/ralf/Downloads/client.png`)            | Available | iPhone Safari, Wohnungsstruktur room-list view. Rows show name input + "Speichern" pill fully visible, then "N Anschlus[…]" text clipped at the right edge; delete icon not visible at all. Top-right page-level "Speichern" also visible. |
| `FlatStructureEditor.tsx` room-list row markup              | Available | `client/src/features/flat-structure/components/FlatStructureEditor.tsx:322-386`                                          |
| `AppShell.tsx` scroll container                             | Available | `client/src/components/AppShell.tsx:23-29` — `<main>` sets `overflow-y-auto`, no explicit `overflow-x`                   |
| Git blame / log on the row markup                           | Available | `5fb6268` (2026-07-06, Story 6.0) introduced the row structure; `0bda6aa` (Story 8.1) and `f8590fd` (Story 8.2) only touched the Save button's `disabled`/`onClick`/`aria-label` internals |
| Story 8.2 spec (AC9)                                        | Available | `_bmad-output/implementation-artifacts/8-2-powerpoint-and-device-edit-save-action-placement-and-viewport-fix.md` — AC9 explicitly requires the page-level Speichern stay "functionally unchanged in trigger location and click behavior" |
| Live Safari DevTools / on-device measurement                | Missing   | No direct measurement of actual overflow amount or whether clipped content is swipe-reachable                            |

## Investigation Backlog

| # | Path to Explore                                                                                   | Priority | Status | Notes |
| - | --------------------------------------------------------------------------------------------------| -------- | ------ | ----- |
| 1 | Confirm whether clipped content (delete icon, rest of count text) is reachable via horizontal swipe or fully lost | Medium   | Open   | Affects severity framing, not the fix direction — row must not overflow either way |
| 2 | Check whether `RoomEditor.tsx`'s room-detail sticky Save bar has the same overflow risk on iPhone widths | Medium   | Open   | Not shown in the reported screenshot (screenshot is the room-*list* view); different layout (`StickyActionBar`, single Save button) so lower a priori risk, but unverified on-device |
| 3 | Resolve the page-level "Speichern" removal request (Ask B) against Story 8.2 AC9                   | High     | Open   | Needs a product decision — AC9 currently mandates keeping it; conflicts with the user's ask |

## Timeline of Events

| Time       | Event                                                                                          | Source                          | Confidence |
| ---------- | ------------------------------------------------------------------------------------------------| -------------------------------- | ---------- |
| 2026-07-05 | `f3605f3` (Story 5.4) creates the `<li>` room-card wrapper                                       | git log                          | Confirmed  |
| 2026-07-06 | `5fb6268` (Story 6.0, "Flat Structure Delete Affordance") adds the current room-list row: non-wrapping flex row, name input + Save/summary/delete | git blame                        | Confirmed  |
| 2026-07-15 | `0bda6aa` (Story 8.1) changes the Save button's `disabled` logic/label only, no layout change    | git blame                        | Confirmed  |
| 2026-07-16 | `f8590fd` (Story 8.2) changes the Save button's `onClick`/`disabled` only, no layout change; Story 8.2's own AC6 manual-Safari-check subtask was left unperformed per its Dev Agent Record | git blame + story file, code review findings | Confirmed  |
| 2026-07-16 | Ralf reports the overflow via real-device screenshot                                             | user report                      | Confirmed  |

## Confirmed Findings

### Finding 1: Room-list row is a non-wrapping flex row with 4 non-shrinking children

**Evidence:** `client/src/features/flat-structure/components/FlatStructureEditor.tsx:322` — `<div className="flex items-center gap-2">` (no `flex-wrap`), containing: `<input className="flex-1 h-10 px-3 ...">` (lines 323-332, no `min-w-0`), and — when not in delete-confirm mode — three `shrink-0` action items: Save button (354-363), "N Anschlüsse ›" button (364-375), delete icon button (376-382).

**Detail:** All three action buttons are explicitly `shrink-0`; the name input has no `min-w-0` override, so by the CSS flexbox default (`min-width: auto`) it also won't shrink below its own intrinsic content width. None of the four children can shrink to fit a narrow container.

### Finding 2: The overflowing layout predates Story 8.1/8.2

**Evidence:** `git blame -L 320,385 client/src/features/flat-structure/components/FlatStructureEditor.tsx` — the row's structural lines (flex container, input, all three action buttons' layout/className) are attributed to `5fb6268` (2026-07-06, Story 6.0). Only the Save button's `disabled` condition, `onClick` body, and `aria-label` text are attributed to `0bda6aa` (Story 8.1, 2026-07-15) and `f8590fd` (Story 8.2, 2026-07-16) — neither story touched `flex-wrap`, `shrink-0`, or the input's width classes.

## Deduced Conclusions

### Deduction 1: The row structurally cannot fit on iPhone-width viewports

**Based on:** Finding 1

**Reasoning:** With the `<li>`'s `p-4` (32px) and the outer container's `px-6` (48px) padding already consuming ~80px, plus a delete icon (~24px), the Save pill (~70-90px for German "Speichern"), the "N Anschlüsse ›" text (~110-150px for a 1-2 digit count in German), and 3×8px inter-item gaps, the four children's combined minimum width exceeds a 390pt-wide iPhone's available content width (390 − 48 ≈ 342px) even before accounting for the name input's own minimum width.

**Conclusion:** This is a deterministic layout defect that reproduces for every room row on iPhone-class viewports, not a data-dependent edge case — matches the screenshot exactly (Save pill fully visible, "Anschlüsse" text clipped mid-word, delete icon absent).

### Deduction 2: This is the same class of gap Story 8.2's un-performed AC6 Safari check was meant to catch

**Based on:** Finding 2, Story 8.2's own Dev Agent Record (code review of 2026-07-15 confirmed AC6's manual Safari verification was never performed)

**Reasoning:** The overflow bug sits on the same settings page, one view away from the sticky-bar viewport fix Story 8.2 actually shipped and never manually verified on real Safari. The story's Completion Notes explicitly flagged the missing manual check as an open item before merge.

**Conclusion:** This isn't a regression introduced by Story 8.2's code — it's a pre-existing (Story 6.0) defect that the story's own outstanding Safari-verification gap allowed to go unnoticed until now.

## Hypothesized Paths

### Hypothesis 1: Clipped content is swipeable-but-hidden, not fully unreachable

**Status:** Open

**Theory:** `AppShell.tsx:25`'s `<main>` sets `overflow-y-auto` with no explicit `overflow-x`. Per the CSS Overflow spec's axis-pairing rule, a `visible` value paired with a non-`visible` value on the other axis computes to `auto` — so `<main>` likely resolves to `overflow-x: auto`, meaning the delete icon and full count text exist in the DOM and are reachable by a horizontal swipe, just with no visual affordance signaling that.

**Supporting indicators:** No `overflow-x-hidden`, `overflow-clip`, or `truncate` found on the row, `<li>`, or `<main>` in the files read.

**Would confirm:** Live Safari test — swipe right within a room-list row on the actual device and check if the delete icon appears.

**Would refute:** An ancestor between the row and `<main>` (not yet checked exhaustively) setting `overflow-x: hidden`/`clip`.

**Resolution:** Open — doesn't change the required fix (row must not overflow regardless), only whether the current state is "content lost" or "content undiscoverable."

## Missing Evidence

| Gap                                                                        | Impact                                                                 | How to Obtain                                                                 |
| --------------------------------------------------------------------------- | ------------------------------------------------------------------------| --------------------------------------------------------------------------------|
| Live on-device measurement of row overflow amount / swipe reachability     | Confirms/refutes Hypothesis 1; not needed to confirm the root cause     | Safari Web Inspector on a connected iPhone, or resize any browser devtools to ~375-430px width against the local dev build |
| Screenshot/test of `RoomEditor.tsx`'s room-*detail* sticky Save bar on iPhone | Confirms whether the same class of issue reaches Story 8.2's own new UI (StickyActionBar) or is isolated to the pre-existing room-list rows | Manual check on the same device, one view deeper than the reported screenshot   |

## Source Code Trace

| Element       | Detail                                                                                                    |
| ------------- | ------------------------------------------------------------------------------------------------------------|
| Error origin  | `client/src/features/flat-structure/components/FlatStructureEditor.tsx:322` (room-list row container)      |
| Trigger       | Rendering the room-list view (`view.type === 'list'`, the default view) on a viewport narrower than the row's combined minimum content width (iPhone-class, ~375-430pt) |
| Condition     | Row's 4 children (name input, Save, PowerPoints-summary, delete) in a single non-wrapping flex row; input lacks `min-w-0`, all 3 buttons are `shrink-0` |
| Related files | `client/src/components/AppShell.tsx` (scroll-container overflow rules); `client/src/features/flat-structure/components/RoomEditor.tsx` (separate `StickyActionBar`-based save row, unverified but lower a priori risk — Backlog #2) |

## Conclusion

**Confidence:** High

Root cause is Confirmed directly from source (`FlatStructureEditor.tsx:322-386`) and corroborated pixel-for-pixel by the screenshot: a non-wrapping flex row with 4 unshrinkable-width children overflows the available width on iPhone-class viewports. This layout was introduced in Story 6.0 (2026-07-06) and left untouched by Stories 8.1/8.2, which only modified the Save button's behavior, not its container's layout. One secondary detail (whether overflow content is swipe-reachable) remains Hypothesized and doesn't affect the required fix. Ask B (removing the page-level Speichern button) is a separate, unresolved scope question that currently conflicts with Story 8.2's own AC9.

## Recommended Next Steps

### Fix direction

Layout mechanism — the row needs either:
- **Wrap-based:** Add `flex-wrap` to the row and let "N Anschlüsse ›" fall to a second line (user's suggestion), with Save + Delete regrouped to stay on row 1's trailing edge — requires re-ordering the JSX (Save/Delete after the input on row 1, PowerPoints-summary button on its own row 2) plus `min-w-0` on the input so it can shrink to accommodate on narrower widths.
- **Icon-based:** Replace the "Speichern" text pill with an icon-only Save button (user's suggestion) to reclaim horizontal space — smaller change, but needs an accessible label (`aria-label`, already present) and a visible focus/disabled state that survives losing the text.
- These two are complementary, not exclusive — the user proposed both together.

Ask B (top-right Speichern removal) is a scope change against Story 8.2 AC9, not a layout fix — route separately (see below), don't fold into the same patch.

### Diagnostic

If Hypothesis 1 needs resolving before deciding severity: swipe-test a room-list row on the real device, or reproduce at 390×844 in any desktop browser's responsive mode against the local dev build.

## Reproduction Plan

1. Run the app locally (`swa start` per project convention) or use the deployed environment.
2. Open `/settings` → Wohnungsstruktur (flat-structure editor), stay on the room-list view (don't enter a room).
3. View at an iPhone-class viewport width (~375-430px) — Safari on a real iPhone, or any browser's responsive-design mode at that width.
4. Observe any room row with a non-trivial name: the "N Anschlüsse ›" text clips and the delete icon is not visible, reproducing the screenshot.

## Side Findings

- Ask B (remove page-level Speichern) directly conflicts with Story 8.2 AC9's explicit requirement that the page-level button remain "not moved, relocated, or given new click behavior." Since Story 8.2 is still `in-progress` (AC6's Safari check outstanding, per the 2026-07-15 code review), this is a live scope conflict worth resolving via `bmad-correct-course` rather than silently reinterpreting the AC.
