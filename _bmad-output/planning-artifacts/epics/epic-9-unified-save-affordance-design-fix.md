# Epic 9: Unified Save-Affordance Design & Fix

Save/cancel actions across the app follow one consistent visual pattern and placement rule, and the power-point/device-edit save action is actually reachable without scrolling to the end of the page, on every supported browser. This epic closes the residual gap Epic 8 left open — sourced from Epic 8's own retrospective (Challenges #1–#2, Action Items #1–#2) and a subsequent investigation (`_bmad-output/implementation-artifacts/investigations/power-points-scroll-visibility-investigation.md`) that found Story 8.2's `StickyActionBar` does not actually satisfy FR-45 as written: it is structurally incapable of remaining visible without scrolling on any Power Point list long enough to exceed the viewport.

## Story 9.1: Unified Save-Affordance Design Decision

As a user,
I want the Save action to look and behave the same way everywhere in Flat Structure settings — the room list, a room's Power Point list, and the Device edit screen,
So that I recognize and trust the save affordance regardless of which screen I'm on.

**Acceptance Criteria:**

**Given** the three current save contexts (`FlatStructureEditor.tsx`'s page-level "Speichern" button and per-room inline Save icons, `RoomEditor.tsx`'s in-room Power Point save via `StickyActionBar`, `DeviceEditor.tsx`'s Cancel/Save via `StickyActionBar`), each currently a visually distinct, independently-shipped pattern (Epic 8 retro Challenge #1),
**When** the design pass for this story is done,
**Then** Sally (UX Designer) proposes one consistent visual pattern — styling, position, and placement rule — covering all three contexts, presented to Ralf for approval before any implementation begins in Stories 9.2/9.3; this is a design decision made during this story, not a pre-made spec handed to the dev agent (same gate-then-implement pattern as Story 8.4).

**Given** the confirmed root cause in `power-points-scroll-visibility-investigation.md` that `StickyActionBar`'s `position: sticky` trailing-sibling pattern is mathematically incapable of remaining visible without scrolling once content exceeds the viewport by more than its buffer (live-reproduced: invisible for ~93% of scroll range on a 5-Power-Point room),
**When** Sally's design specifies the save-bar mechanism for the Power Point/Device-edit contexts,
**Then** the approved pattern uses a mechanism that structurally guarantees visibility regardless of content length — e.g. `position: fixed` for the action bar, or a fixed-height-content-area-plus-non-scrolling-footer layout — not a restyled continuation of the existing trailing-sticky-sibling approach; a design that merely re-skins `StickyActionBar` without changing its positioning mechanism does not satisfy this AC.

**Given** FR-45 ("every save/cancel action... remains within the visible viewport without requiring the user to scroll to find it, across all supported browsers, including Safari"),
**When** the design is approved,
**Then** it is explicitly confirmed to satisfy FR-45 as written, including the Safari clause, before Stories 9.2/9.3 begin implementation.

## Story 9.2: Fix RoomEditor & DeviceEditor Save Bar (Structural)

As a user,
I want the Save action on a room's Power Point list and the Cancel/Save row on the Device edit screen to actually remain visible without scrolling, no matter how long the list is or which browser I'm using,
So that I never have to hunt for whether my change was committed.

**Acceptance Criteria:**

**Given** `RoomEditor.tsx`'s and `DeviceEditor.tsx`'s current `StickyActionBar` usage (`StickyActionBar.tsx:9-14`, rendered as a sibling after the scrollable content with only a `pb-32` buffer — confirmed structurally broken by live reproduction against the deployed app: the Save bar is invisible for the first ~93% of scroll range on a 5-Power-Point room, only appearing once scrolled essentially to the end),
**When** the fix is implemented per Story 9.1's approved design,
**Then** the save action remains visible in the viewport at every scroll position, for any number of Power Points/devices in the list — verified by reproducing the investigation's specific repro case (a room with enough Power Points to exceed viewport height) and confirming the Save button is visible without any scrolling.

**Given** both `RoomEditor.tsx` (Power Point save) and `DeviceEditor.tsx` (Cancel/Save) currently share the same `StickyActionBar` component and therefore share the same defect,
**When** the fix is implemented,
**Then** both components adopt the same replacement mechanism from Story 9.1 — not two independently-patched solutions — so future changes to the save-bar pattern only need to be made once.

**Given** this class of layout/scroll-positioning bug is invisible to `jsdom`-based automated tests (confirmed both by this epic's own investigation and by Epic 8 retro Challenge #5),
**When** the fix is complete,
**Then** it is manually verified in both Chrome and real Safari (not only Chromium-based browsers) against a content length equivalent to the investigation's repro case — this closes the open, Safari-unconfirmed Hypothesis 1 from `power-points-scroll-visibility-investigation.md` (the Device-edit Safari blank-space variant) by direct observation.

**Given** the existing `pb-32` spacer comments in `RoomEditor.tsx:57` and `DeviceEditor.tsx:86` that document the old buffer-based intent ("pb-32 clears StickyActionBar's rendered height... so the sticky bar doesn't cover the last field/power point at full scroll"),
**When** the new mechanism is implemented,
**Then** those comments are removed or rewritten to describe the actual mechanism in use — not left describing an approach that's no longer there.

## Story 9.3: Align Room-List Save Affordance

As a user,
I want the room-list view's Save actions — the page-level "Speichern" button and each room's inline Save icon — to look and feel consistent with the Power Point and Device edit save actions,
So that saving works the same way everywhere in Flat Structure settings.

**Acceptance Criteria:**

**Given** `FlatStructureEditor.tsx`'s page-level Save button (`:268-276`) and per-room inline Save icon buttons (`:356-373`), which are functionally correct today — not scroll-broken, confirmed by the investigation (Finding 4: this view's save buttons are structurally separate from `StickyActionBar` and were never meant to be sticky) — but visually distinct from the Power Point/Device-edit save pattern (Epic 8 retro Challenge #1),
**When** restyled per Story 9.1's approved pattern,
**Then** their visual treatment (styling, placement) matches the unified pattern without changing their existing working behavior — autosave-on-room-add/delete (Story 8.1), per-room save-on-dirty, and page-level batched save all continue to function exactly as before.

**Given** this story only changes presentation, not behavior,
**When** implemented,
**Then** no changes are made to `handleSave`, `handleSaveRoom`, `isRoomDirty`, or any other logic in `FlatStructureEditor.tsx` — this is a restyle, not a behavior change; existing tests for save/dirty-state logic continue to pass unmodified.
