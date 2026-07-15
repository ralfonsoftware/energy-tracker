# Epic 8: UI & Behavior Consistency Alignment

Save/cancel actions and dropdown/overlay behavior are consistent, always visible within the viewport, and located next to the fields they act on across the whole app. Consumption cards make efficient use of available width on tablet and desktop. This epic addresses UI/behavior debt surfaced by Ralf's own cross-app usage after Epic 7 — not a new user-facing capability, but a consistency and polish pass across Flat Structure, Settings, and Decomposition.

## Story 8.1: Flat Structure Editor — Autosave for Room Add/Delete

As a user,
I want adding or deleting a room in my Flat Structure to save immediately,
So that I don't need to find a separate Save button to keep a change I already made explicit.

**Acceptance Criteria:**

**Given** the Flat Structure editor's room list view (`FlatStructureEditor.tsx`), which currently only persists changes when the page-header "Speichern" button is clicked on the batched `draftRooms` state,
**When** the user clicks "+ Raum hinzufügen" (add room) or confirms a room deletion via the existing type-to-confirm-style delete row,
**Then** the change is persisted immediately via its own scoped save call, not the batched draft-save used by the rest of the editor; no separate "Speichern" click is required for the room to actually be added or removed from the account's data.

**Given** a room add or delete is in flight,
**When** the mutation is pending,
**Then** the affected row shows an inline pending/disabled state and the action cannot be double-submitted.

**Given** a room add or delete mutation fails,
**When** the failure is returned,
**Then** the room list reverts to its last known-good state and an inline error message appears, matching the existing `saveError` treatment already used in `FlatStructureEditor.tsx` — no silent data loss, no row that looks saved but isn't.

**Given** renaming a room (typing in the room name field) is a different interaction than add/delete,
**When** this story is implemented,
**Then** room rename stays on the existing batched draft/Save flow — this story's scope is add and delete only; renaming a room while mid-typing must not fire a network request per keystroke.

## Story 8.2: PowerPoint & Device Edit — Save-Action Placement & Viewport Fix

As a user,
I want the Save action for a new or edited Power Point to be visible right where I'm working, and the Cancel/Save buttons on the Device edit screen to always be visible without scrolling,
So that I don't have to navigate back to a distant part of the screen to find out whether my change was ever committed.

**Acceptance Criteria:**

**Given** the room-detail view (`RoomEditor.tsx` / `PowerPointEditor.tsx`), where adding a Power Point currently has no local save affordance at all — the only way to commit it is navigating back to the room list view and clicking its header "Speichern" button,
**When** the user adds a new Power Point or edits an existing one,
**Then** a save action is reachable from within the room-detail view itself — either a local save affordance on `RoomEditor.tsx`, or (if the shared-draft architecture from Story 8.1's sibling area is kept for Power Points/Devices) a persistent/sticky save affordance that stays visible while scrolling a room's Power Point list — so committing a change never requires navigating back to the room list first.

**Given** `DeviceEditor.tsx`'s Cancel/Save button row, currently placed via in-flow layout (a `<div className="flex-1" />` spacer inside a `minHeight: 100vh` column) rather than pinned to the viewport,
**When** the Device edit screen is opened in Safari (iOS or macOS), where the dynamic browser toolbar changes the effective visible viewport height,
**Then** the Cancel/Save row remains fully visible without requiring the user to scroll — implemented via a sticky/fixed-positioned button bar, not a `100vh`-in-flow assumption — verified by an actual manual check in Safari, not only Chrome/Firefox.

**Given** this story's viewport fix,
**When** implemented,
**Then** the sticky/fixed button-bar pattern used is documented as the one to reuse for any other full-height edit screen in this feature area, so the same Safari issue doesn't need re-solving screen by screen.

## Story 8.3: Overlay & Dropdown Visibility Audit

As a user,
I want every dropdown and overlay in the app — the language switcher, the flat switcher, the Decomposition period selector — to render as a clean, fully visible floating panel,
So that I can always see and select the option I need, on every screen.

**Acceptance Criteria:**

**Given** `LocaleDropdown.tsx`'s language-switcher overlay in Settings, which currently renders partially hidden/clipped behind adjacent page content instead of floating cleanly above it, despite already using `components/ui/popover.tsx`'s Radix `Portal`,
**When** the popover opens,
**Then** it renders as a fully visible, unclipped panel above all surrounding content regardless of scroll position or where on the page the trigger sits — root-caused and fixed at the shared level (`components/ui/popover.tsx`'s stacking/z-index behavior, or a stacking-context conflict introduced by an ancestor element) rather than patched per-instance, since `LocaleDropdown.tsx`, `FlatSwitcher.tsx`, and `PeriodSelector.tsx` all build on this same shared component.

**Given** every other Popover-based dropdown in the app (`FlatSwitcher.tsx`, `PeriodSelector.tsx`, and any others found during this story's audit),
**When** each is opened,
**Then** it is manually checked against the same clipping/stacking issue and confirmed fixed by the same shared-component-level fix — not re-diagnosed and separately patched per component.

**Given** this is a visibility regression rather than new functionality,
**When** the fix lands,
**Then** a regression test is added at the shared `popover.tsx` level (or the lowest shared level the bug is actually rooted in), so a future change can't silently reintroduce the clipping.

## Story 8.4: Responsive Device Card Grid — Room Card Layout on Tablet & Desktop

As a user,
I want the device cards inside a Room card to lay out in a multi-column grid on tablet and desktop instead of a single full-width column,
So that the Verbrauch/Decomposition view makes efficient use of the available screen space when I have many measured devices.

**Acceptance Criteria:**

**Given** `RoomCard.tsx`'s current device list (`className="flex flex-col gap-2"` — one full-width `DeviceCard`/`SmartStripCard` per row on every viewport),
**When** the UX design pass for this story is done,
**Then** Sally (UX Designer) proposes a responsive grid layout — column count and breakpoints consistent with `UX-DR12`'s existing phone/tablet/desktop breakpoint conventions — presented to Ralf for approval before implementation begins; this is a design decision to be made during this story, not a pre-made spec handed to the dev agent.

**Given** the approved grid design,
**When** implemented in `RoomCard.tsx`,
**Then** device cards reflow into the approved column count at each breakpoint without changing `DeviceCard.tsx`'s or `SmartStripCard.tsx`'s own internal content or sizing rules — this story is a container-layout change, not a card redesign.

**Given** the interleaved measured-then-estimated device ordering already established in Story 7.3 (`partitionAndSortDevices`),
**When** the grid layout is applied,
**Then** that ordering is preserved reading left-to-right, top-to-bottom within the grid — the grid must not silently re-order or re-group devices differently from the existing list order.

**Given** the "Direct consumption" compact-card fallback (Story 7.3 AC8) and the Smart Power Strip card (visually taller/denser than a regular `DeviceCard`),
**When** they appear inside a multi-column grid,
**Then** Sally's design explicitly specifies how these two visually-different card types behave in the grid (e.g. full-width span vs. fitting a single column) rather than leaving it to be improvised during implementation.
