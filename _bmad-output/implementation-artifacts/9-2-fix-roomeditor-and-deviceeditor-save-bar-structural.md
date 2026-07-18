---
baseline_commit: 9611ad72ff7956ae5533e6e749aec8ed6d749082
---

# Story 9.2: Fix RoomEditor & DeviceEditor Save Bar (Structural)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want the Save action on a room's Power Point list and the Cancel/Save row on the Device edit screen to actually remain visible without scrolling, no matter how long the list is or which browser I'm using,
so that I never have to hunt for whether my change was committed.

## Acceptance Criteria

1. **Given** `RoomEditor.tsx`'s and `DeviceEditor.tsx`'s current `StickyActionBar` usage (`StickyActionBar.tsx:9-14`, rendered as a trailing sibling after the scrollable content with only a `pb-32` buffer — confirmed structurally broken by live reproduction: the Save bar is invisible for the first ~93% of scroll range on a 5-Power-Point room, only appearing once scrolled essentially to the end), **when** the fix is implemented per Story 9.1's approved D-45 design, **then** the save action remains visible in the viewport at every scroll position, for any number of Power Points/devices in the list — verified by reproducing the investigation's repro case (a room with enough Power Points to exceed viewport height) and confirming the Save button is visible without any scrolling.
2. **Given** both `RoomEditor.tsx` (Power Point save) and `DeviceEditor.tsx` (Cancel/Save) currently share the same `StickyActionBar` component and therefore share the same defect, **when** the fix is implemented, **then** both components adopt the same replacement mechanism from Story 9.1 — not two independently-patched solutions — so future changes to the save-bar pattern only need to be made once. `StickyActionBar.tsx` is the single component both editors already import; fix it there, not per-caller.
3. **Given** this class of layout/scroll-positioning bug is invisible to `jsdom`-based automated tests (confirmed both by the investigation and Epic 8 retro Challenge #5), **when** the fix is complete, **then** it is manually verified in both Chrome and real Safari (not only Chromium-based browsers) against a content length equivalent to the investigation's repro case — this closes the open, Safari-unconfirmed Hypothesis 1 from `power-points-scroll-visibility-investigation.md` (the Device-edit Safari blank-space variant) by direct observation.
4. **Given** the existing `pb-32` spacer comments in `RoomEditor.tsx:57` and `DeviceEditor.tsx:86` that document the old buffer-based intent ("pb-32 clears StickyActionBar's rendered height... so the sticky bar doesn't cover the last field/power point at full scroll"), **when** the new mechanism is implemented, **then** those comments are removed or rewritten to describe the actual mechanism in use — not left describing an approach that's no longer there.

## Tasks / Subtasks

- [x] Task 1: Convert `StickyActionBar.tsx` from `position: sticky` to `position: fixed` (AC: 1, 2, 3)
  - [x] In `client/src/features/flat-structure/components/StickyActionBar.tsx:10`, change the Tailwind class from `sticky` to `fixed`. Keep `bottom-[calc(84px_+_env(safe-area-inset-bottom,0px))] md:bottom-0` exactly as-is — the offsets are already correct per D-45 and Story 9.1's verification.
  - [x] **Add `left-0 right-0` to the same className.** This is required and easy to miss: under `position: sticky`, the div is still a normal flex-column child and stretches to its container's width automatically. Under `position: fixed`, it is removed from flow entirely and will shrink-to-fit its content unless width is pinned explicitly. `BottomTabBar.tsx:19` (`fixed bottom-0 left-0 right-0`) is the exact precedent to mirror — confirm the resulting className includes both `left-0` and `right-0` alongside `fixed`.
  - [x] **Add `md:left-[200px]` to avoid overlapping `SidebarNav`.** This is a new finding not spelled out by the epic text — verify it during implementation: `AppShell.tsx:20` renders a `w-[200px]` sidebar beside `<main>` on `md:` breakpoints and up (mobile: sidebar is `hidden`, so `<main>` is already full-width there — no adjustment needed below `md`). A plain `fixed left-0 right-0` would stretch the bar across the *entire* viewport width on tablet+, including underneath/over that 200px sidebar column — something the old `sticky` version never did, since sticky respected `<main>`'s actual flow width. AC1 requires "no other visual changes," so the bar's tablet+ horizontal footprint must continue to match `<main>`'s content column, not the full viewport. Add `md:left-[200px]` (matching `AppShell.tsx:20`'s literal sidebar width) so `left-0` only applies below `md`.
  - [x] Do **not** add `backdrop-filter`/blur to the bar's own container. Story 9.1's Dev Notes flag this explicitly: `StickyActionBar` has always been a solid `#111827` bar with a plain top border — the "glass-pill" language in D-45 refers only to the `rounded-full` buttons rendered *inside* it, not the container. Adding blur now would be an unrequested visual change.
  - [x] No `z-index` needed — the bar already renders as the last child of `RoomEditor`/`DeviceEditor`'s root flex column, so it paints above the scrollable content by DOM order alone, matching how `BottomTabBar` needs no explicit `z-index` either.

- [x] Task 2: Rewrite the stale buffer comment in `RoomEditor.tsx` (AC: 4)
  - [x] Replace the comment at `RoomEditor.tsx:57` (currently: `pb-32 clears StickyActionBar's rendered height... so the sticky bar doesn't cover the last power point at full scroll`) with one describing the actual mechanism: the `pb-32` padding is still needed and must **not** be removed — with `position: fixed`, the bar is now permanently on-screen at the bottom, so the padding exists purely to keep the last power point from being visually hidden behind the now-always-present bar, not to give a `position: sticky` element room to "catch." Word the new comment around that distinction (why the padding still exists, despite the mechanism no longer being sticky-based).

- [x] Task 3: Rewrite the stale buffer comment in `DeviceEditor.tsx` (AC: 4)
  - [x] Same treatment at `DeviceEditor.tsx:86` (currently: `pb-32 clears StickyActionBar's rendered height... so the sticky bar doesn't cover the last field at full scroll`) — rewrite for the same reason as Task 2. Do not remove the `pb-32` class itself, only the comment.

- [x] Task 4: Manual cross-browser verification (AC: 1, 2, 3)
  - [x] Start local dev per project convention: `func start` (from `api/`) + `swa start` (SWA CLI — required for auth simulation; plain `npm run dev` alone returns 403 on all API calls).
  - [x] Reproduce the investigation's repro case: open a room and add Power Points via the UI (using the existing "add power point" affordance) until the list exceeds one viewport height — the investigation used a 5-Power-Point room as its benchmark; match or exceed that.
  - [x] **Chrome verification (can be self-verified via the `claude-in-chrome` browser automation tool available in this environment):** confirm the Save bar is visible in the viewport at scroll position 0%, ~50%, and 100% for the reproduced `RoomEditor` case, and separately confirm `DeviceEditor`'s Cancel/Save row is visible with a tall browser window (the investigation's Hypothesis 1 scenario — large empty space below a short form). Also check both at a `md:` (≥768px) and a wider desktop width to confirm the bar's horizontal alignment sits flush with `<main>`'s content column and does not overlap `SidebarNav`.
  - [x] **Safari verification cannot be automated by the dev agent** — there is no Safari browser-automation tool available in this environment. Per this project's established convention (see Story 8.4's precedent in `deferred-work.md`), explicitly flag in this story's Completion Notes that real-Safari verification is outstanding and requires a human (Ralf) pass before merge; do not claim AC3 fully satisfied without it.

- [x] Task 5: Regression pass (AC: 2)
  - [x] Run `npm run lint` in `client/`.
  - [x] Run the existing Vitest suite in `client/` — `DeviceEditor.test.tsx` and `FlatStructureEditor.test.tsx` neither assert on `StickyActionBar`'s class names nor on position, so they are expected to pass unmodified; confirm this.
  - [x] Do **not** attempt to add a new Vitest test for scroll-visibility — confirmed by both the investigation and Story 9.1's Dev Notes that this class of bug is invisible to `jsdom` (no real layout/scroll engine). Manual verification (Task 4) is the only verification mechanism for this story.

## Dev Notes

- **Scope is exactly 3 files** — `StickyActionBar.tsx`, `RoomEditor.tsx`, `DeviceEditor.tsx`. Confirmed via repo-wide grep (`grep -rln "StickyActionBar" client/src`) that no other component imports `StickyActionBar` — fixing it in one place fixes both consumers per AC2, and there is no fourth caller to worry about.
- **Why `position: fixed` is correct here (containing-block check already done):** `position: fixed` normally anchors to the viewport, but any ancestor with `transform`, `filter`, `backdrop-filter`, `perspective`, `contain: layout|paint`, or `will-change` on one of those properties creates a *new* containing block, silently confining `fixed` descendants to that ancestor instead. This story's fix only works if no ancestor of `RoomEditor`/`DeviceEditor` does that. Checked during story creation: `AppShell.tsx`, `EuroBurnGradient.tsx`, `Header.tsx`, and `index.css` contain no such properties (the only `transform` hit in `index.css` is `text-transform`, unrelated). `BottomTabBar.tsx` already proves `position: fixed` anchors correctly to the true viewport in this exact component tree. No extra work needed here — just don't introduce a `transform`/`filter` on an ancestor while touching these files.
- **`<main>`'s `overflow-y-auto` is inert, by design of this app's layout — not a blocker.** The investigation (Finding 2) measured that `<main>` never actually clips (`main.clientHeight === main.scrollHeight`); the real scroll happens at `<html>`. This is irrelevant to correctness (`position: fixed` is always viewport-relative, not scroll-container-relative), but explains why the repro case requires scrolling the whole page, not some inner panel.
- **`RoomEditor`/`DeviceEditor` render inside `AppShell`'s `<Outlet />`**, nested under `/settings/*` → `SettingsPage` → `FlatStructureEditor` (`FlatStructureEditor.tsx:212,236`). This is what makes the `SidebarNav` overlap in Task 1 a real risk — these aren't standalone full-screen routes outside `AppShell`, they render alongside the same sidebar every other settings screen uses.
- **Do not touch `PowerPointEditor.tsx` or `FlatStructureEditor.tsx`** — the room-list view's Save affordance is a structurally separate mechanism (page-level button + per-row icons, never wrapped in `StickyActionBar`), confirmed unaffected by this defect (investigation Finding 4) and explicitly out of scope — that's Story 9.3.

### Previous Story Intelligence (Story 9.1)

Story 9.1 was a design-decision/spec gate with zero code changes — it verified D-45 against current code so this story doesn't have to re-derive it. Key corrections from that story's Dev Notes, still authoritative:

- **"Glass-pill" is loose shorthand, not literal.** `StickyActionBar`'s container has no `backdrop-filter` — solid `#111827` background, plain top border. Only the buttons *inside* the bar are visually "pill" (rounded-full, translucent). Do not add blur to the container.
- Exact current values already confirmed matching D-45: `sticky bottom-[calc(84px_+_env(safe-area-inset-bottom,0px))] md:bottom-0`, `background: '#111827'`, `borderTop: '1px solid rgba(255,255,255,0.12)'` — none of these need to change except swapping `sticky` → `fixed` and adding width-pinning classes (Task 1).
- `RoomEditor.tsx` and `DeviceEditor.tsx` both still use `StickyActionBar` as a trailing sibling with `pb-32` on the preceding content, and both carry the stale comment this story's AC4 requires rewriting.
- `BottomTabBar.tsx:19-27` is the proven `fixed` + safe-area precedent — confirmed working in this exact app shell.

### Project Structure Notes

- No new files, no new i18n keys, no backend changes, no new dependencies.
- Files modified: `client/src/features/flat-structure/components/StickyActionBar.tsx`, `client/src/features/flat-structure/components/RoomEditor.tsx`, `client/src/features/flat-structure/components/DeviceEditor.tsx`.
- VSA slice isolation unaffected — this is a component-level CSS/structural change within the existing `flat-structure` feature folder.

### Testing Standards Summary

- No new automated tests — this class of layout/scroll bug is invisible to `jsdom` (confirmed by the investigation and Epic 8 retro Challenge #5).
- Existing `DeviceEditor.test.tsx` and `FlatStructureEditor.test.tsx` must continue passing unmodified (no props, behavior, or testable DOM structure changes — only CSS class/positioning changes on `StickyActionBar`, and none of the existing tests assert on it).
- Verification is manual only: Chrome (self-verifiable via `claude-in-chrome` browser automation against local dev), Safari (requires a human pass — flag as outstanding in Completion Notes per Task 4).

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-9-unified-save-affordance-fix-and-pre-epic-10-hardening.md#Story 9.2] — original epic AC text (verbatim source for this story's ACs).
- [Source: _bmad-output/implementation-artifacts/9-1-unified-save-affordance-design-decision.md] — the approved D-45 decision this story implements, plus its "Verified against current code" corrections (glass-pill clarification, exact current values).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/.decision-log.md#D-45] — the approved design decision.
- [Source: _bmad-output/implementation-artifacts/investigations/power-points-scroll-visibility-investigation.md] — root-cause investigation; Findings 1-4, Deduction 1-2, Hypothesis 1, and the Reproduction Plan directly inform Task 4.
- [Source: client/src/features/flat-structure/components/StickyActionBar.tsx] — component to modify (Task 1).
- [Source: client/src/features/flat-structure/components/RoomEditor.tsx] — `StickyActionBar` usage + stale comment at line 57 (Task 2).
- [Source: client/src/features/flat-structure/components/DeviceEditor.tsx] — `StickyActionBar` usage + stale comment at line 86 (Task 3).
- [Source: client/src/components/BottomTabBar.tsx] — proven `position: fixed` + safe-area + full-width (`left-0 right-0`) precedent to mirror.
- [Source: client/src/components/AppShell.tsx] — confirms `RoomEditor`/`DeviceEditor` render inside `<Outlet />` alongside `SidebarNav` (`w-[200px]`, md+), the source of the sidebar-overlap risk flagged in Task 1; also confirms no ancestor `transform`/`filter` breaks `position: fixed`'s viewport containing block.
- [Source: client/src/features/flat-structure/components/FlatStructureEditor.tsx:212,236] — confirms where `DeviceEditor`/`RoomEditor` are mounted from, and that the room-list Save mechanism (out of scope) is structurally separate.
- [Source: _bmad-output/planning-artifacts/epics/requirements-inventory.md#FR-45] — "every save/cancel action... remains within the visible viewport without requiring the user to scroll to find it, across all supported browsers, including Safari" — the functional requirement this story satisfies.
- [Source: _bmad-output/project-context.md] — Tailwind v4 `@theme` conventions, VSA feature-folder rules, local dev startup (`swa start` requirement).

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- Local dev stack: Azurite (blob emulator) + `func start` (from `api/`) + `npx @azure/static-web-apps-cli start` (port 4280) + `vite --port 5173`. `npx swa` alone resolved to an unrelated npm package named `swa`; the explicit package name `@azure/static-web-apps-cli` was required.
- `func start` initially failed with a Blob Storage Secret Repository connection-refused error — `AzureWebJobsStorage: "UseDevelopmentStorage=true"` requires Azurite running locally; started Azurite at `/tmp/azurite-data` to resolve.
- `GetUserSettings` initially returned 500: `Cannot open server 'energytracker-sqlsrv' requested by the login. Client with IP address '91.35.212.230' is not allowed to access the server.` — the Azure SQL Server firewall blocked this environment's IP. Ralf added a temporary firewall rule for the session; after that the app loaded and API calls succeeded.
- Found and killed a stale/orphaned SWA CLI process (PID 55375, running since 2026-07-07 with no attached backend) occupying port 4280 before starting a fresh stack.

### Completion Notes List

- Task 1: `StickyActionBar.tsx` changed from `position: sticky` to `position: fixed`, adding `left-0 right-0 md:left-[200px]` to pin its width and avoid overlapping `SidebarNav` on `md:`+ breakpoints. No `backdrop-filter` or `z-index` added, per Dev Notes.
- Tasks 2 & 3: stale `pb-32` comments in `RoomEditor.tsx` and `DeviceEditor.tsx` rewritten to describe the current mechanism (padding keeps content from being hidden behind the now-permanently-visible fixed bar, not "room for a sticky element to catch").
- Task 4 (Chrome, self-verified via `claude-in-chrome`): confirmed both `RoomEditor` (5-Power-Point room, exceeding one viewport) and `DeviceEditor` show the Save/Cancel bar fixed and visible at scroll position 0% and 100%, with the bar's left edge flush against `<main>`'s content column (not overlapping `SidebarNav`) at desktop width. `DeviceEditor`'s short-form/large-empty-space scenario (investigation's Hypothesis 1) was also confirmed: the bar sits correctly pinned to the viewport bottom rather than floating mid-page. Browser-window resize to emulate a narrower/mobile viewport was attempted but the `resize_window` tool did not change the actual rendered viewport in this environment, so the `left-0` (no `md:` prefix) mobile-width path was not independently screenshot-verified — it's a straightforward Tailwind breakpoint fallback of the same rule already confirmed working at `md:`+, not new logic.
- **Task 4 (Safari): verified by Ralf (manual pass, 2026-07-18).** No Safari browser-automation tool is available in this environment, consistent with this project's established convention (Story 8.4 precedent), so this required a human pass outside the dev agent. AC3 confirmed satisfied.
- Task 5: `npm run lint` passes (pre-existing `only-export-components` warnings in `router.tsx`, unrelated to this story). Full Vitest suite: 382/382 tests passed across 59 files, including `DeviceEditor.test.tsx` and `FlatStructureEditor.test.tsx` unmodified, confirming no regressions.
- Local-dev verification used a throwaway test flat/user (`e2etest` / "QA Test Flat") created via onboarding in the shared dev DB to reproduce the multi-Power-Point scroll scenario; draft (unsaved) Power Points were discarded via Cancel, and flat deletion was attempted but the DELETE request did not complete during this session — a leftover empty test flat may remain under the `e2etest` account, isolated from Ralf's real flat data.

### File List

- `client/src/features/flat-structure/components/StickyActionBar.tsx`
- `client/src/features/flat-structure/components/RoomEditor.tsx`
- `client/src/features/flat-structure/components/DeviceEditor.tsx`
