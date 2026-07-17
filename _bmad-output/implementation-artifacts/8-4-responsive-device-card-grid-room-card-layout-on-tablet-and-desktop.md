---
baseline_commit: 2473fb9014df14612dde370f2855f8d631101520
---

# Story 8.4: Responsive Device Card Grid — Room Card Layout on Tablet & Desktop

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want the device cards inside a Room card to lay out in a multi-column grid on tablet and desktop instead of a single full-width column,
so that the Verbrauch/Decomposition view makes efficient use of the available screen space when I have many measured devices.

## Acceptance Criteria

1. **UX design gate — this story ships no grid CSS until Sally's design is approved.** `RoomCard.tsx`'s current device list (`client/src/features/decomposition/components/RoomCard.tsx:51`, `className="flex flex-col gap-2"`) renders one full-width `DeviceCard`/`SmartStripCard` per row on every viewport. Before any implementation, invoke the UX designer (Sally — `bmad-agent-ux-designer` / `bmad-ux` skill) to propose a responsive grid: column count per breakpoint, consistent with this codebase's existing phone(<768px)/tablet(768–1023px)/desktop(≥1024px) breakpoint convention (`UX-DR12`, `requirements-inventory.md:123`; the only breakpoints in active use are Tailwind's default `md:` (768px) and `lg:` (1024px) — confirmed via `client/src/index.css`, no custom `@theme` screens override). Present the proposal to Ralf for approval before writing implementation code. **Do not invent the column count or breakpoints yourself** — this is an explicit epic requirement (`epic-8-ui-behavior-consistency-alignment.md` Story 8.4 AC1: "a design decision to be made during this story, not a pre-made spec handed to the dev agent").

2. **Given the approved grid design, when implemented in `RoomCard.tsx`,** then device cards reflow into the approved column count at each breakpoint without changing `DeviceCard.tsx`'s or `SmartStripCard.tsx`'s own internal content, padding, or sizing rules (both currently size via their own internal `px-*`/`py-*` padding with no explicit width — they naturally fill whichever grid-cell width the container gives them). This story is a container-layout change on `RoomCard.tsx` only, not a card redesign.

3. **Given the interleaved measured-then-estimated device ordering already established by `partitionAndSortDevices` (`RoomCard.tsx:21-30`, added in Story 7.3),** when the grid layout is applied, then that function's output order is preserved reading left-to-right, top-to-bottom within the grid (CSS Grid's default document-order flow already does this — do not add `order` overrides or re-sort within the grid).

4. **Given the "Direct consumption" compact-card fallback (`RoomCard.tsx:45-49`, Story 7.3 AC8) and the `SmartStripCard` (visually taller/denser than a regular `DeviceCard` — it has its own header row plus a sub-device list, `SmartStripCard.tsx:28-68`),** when they appear inside a multi-column grid, then Sally's design explicitly specifies how these two card types behave in the grid (e.g., full-width span vs. fitting a single column like any other card) — this must be answered in the approved design from AC1, not improvised during coding.

5. **Given this is a layout-only change,** when implemented, then no changes are made to `DeviceCard.tsx`, `SmartStripCard.tsx`, `DecompositionTab.tsx`, or any decomposition API/hook file — only `RoomCard.tsx`'s device-list container `className` (and, if the approved design calls for it, a `SmartStripCard`/direct-consumption-specific wrapper `className` such as a grid-span utility) changes.

6. **Given the existing `RoomCard.test.tsx` suite asserts document-order via `compareDocumentPosition`,** when this story is implemented, then those existing tests continue to pass unchanged (grid reflow does not alter DOM order, only visual position — `compareDocumentPosition` is DOM-order-based and grid-agnostic) — no test rewrites, but the tests are re-run at the end to confirm.

## Tasks / Subtasks

- [x] Task 1: UX design pass — grid layout proposal and approval (AC: 1, 4)
  - [x] Invoke Sally (`bmad-agent-ux-designer` skill, or `bmad-ux` if that's the active planning skill) with the current `RoomCard.tsx`/`DeviceCard.tsx`/`SmartStripCard.tsx` state (read above), the existing breakpoint convention (`md:`768px / `lg:`1024px, no custom `@theme` screens), and the existing `DashboardGrid.tsx` responsive pattern (`grid grid-cols-2 gap-3 md:grid-cols-4`) as reference precedent for how this app already expresses tablet/desktop reflow.
  - [x] Get an explicit column-count-per-breakpoint proposal, plus explicit handling for the "Direct consumption" fallback row and `SmartStripCard` (full-width span vs. single grid cell).
  - [x] Present to Ralf; do not proceed to Task 2 until approved. Record the approved design in Dev Notes / Completion Notes before implementing.
  - [x] **Halt and ask Ralf if this gate is reached without a clear answer** — do not default to guessing a column count to keep moving. (N/A — clear approval received, no halt needed.)

- [x] Task 2: Implement the approved grid in `RoomCard.tsx` (AC: 2, 3, 5)
  - [x] Replace `className="flex flex-col gap-2 px-4 pb-3.5"` (line 51) with the approved `grid` classes (e.g. `grid grid-cols-1 gap-2 px-4 pb-3.5 md:grid-cols-{N} lg:grid-cols-{M}` — exact values from Task 1's approved design).
  - [x] If the approved design gives `SmartStripCard` (and/or the direct-consumption fallback) a full-width span at tablet/desktop, apply the appropriate grid-span utility (e.g. `md:col-span-full`) only to that element's wrapper — do not touch `SmartStripCard.tsx`'s internals.
  - [x] Do not modify `partitionAndSortDevices` — grid reflow must consume its output order as-is.
  - [x] Do not modify `DeviceCard.tsx` or `SmartStripCard.tsx`.

- [x] Task 3: Regression pass (AC: 6)
  - [x] Run `npx tsc --noEmit`, `npx vitest run`, and `npm run lint` from `client/` — zero regressions, `RoomCard.test.tsx` passes unchanged.
  - [x] Manually verify (or note if unable) at ~375px (phone), ~800px (tablet), ~1280px (desktop) viewport widths that the grid reflows as designed and no card overflows or gets clipped. **Unable to complete interactive in-browser verification in this session** — the Chrome browser extension was not connected in this background session, and Playwright is not an installed project dependency (installing it would add an undeclared dependency, out of scope for a layout-only story). Mitigations taken instead: (1) the grid classes (`grid grid-cols-1 gap-2 md:grid-cols-2 lg:grid-cols-3`, `md:col-span-full`) are standard Tailwind utilities following the exact same escalation pattern already shipped and working in `DashboardGrid.tsx`; (2) both cards (`DeviceCard.tsx`, `SmartStripCard.tsx`) size via padding only with no explicit width, so they are structurally guaranteed to fill whichever grid-cell width is given, per AC2's own precondition; (3) a temporary local preview harness (mock data, no backend) was built and a Vite dev server started successfully with no runtime errors, but could not be visually inspected without browser tooling — the harness was reverted before completing this story, `main.tsx` is confirmed back to its committed state via `git status`. Recommend a human (Ralf) do a quick visual pass at the three widths before/shortly after merge.

### Review Findings

- [x] [Review][Decision] Grid sparse auto-placement leaves gaps before a full-span SmartStripCard [`RoomCard.tsx:51`] — RESOLVED (Sally + Ralf, 2026-07-17): accepted as designed behavior. An occasional blank cell in the row preceding a full-span `SmartStripCard` is preferable to `grid-auto-flow: dense`, which would silently reorder devices visually to fill the gap and undermine `partitionAndSortDevices`' kWh-rank reading order (AC3 intent — biggest measured consumers read first, left-to-right/top-to-bottom). No code change; default sparse `grid-auto-flow` stays as shipped.
- [x] [Review][Patch] New grid tests don't actually cover a multi-device grid [RoomCard.test.tsx:121] — Fixed: `RoomCard_MultipleDevices_UsesResponsiveGridContainer` now renders 2 devices and asserts both land inside the grid container (`grid.children` length + `toContainElement`). Added new test `RoomCard_RegularDevice_DoesNotGetFullWidthSpanClass` covering the missing negative case.
- [x] [Review][Patch] Grid-container test selector is unscoped [RoomCard.test.tsx:125] — Fixed: switched to `querySelectorAll('.grid')` with an explicit `toHaveLength(1)` uniqueness assertion before indexing into it, so a future stray `.grid` class on a child would fail loudly instead of silently targeting the wrong node.
- [x] [Review][Defer] CSS grid reflow vs. DOM/tab reading-order [RoomCard.tsx:51] — deferred, pre-existing pattern (same property exists in `DashboardGrid.tsx`), not introduced uniquely by this diff.
- [x] [Review][Defer] Interactive viewport verification (375/800/1280px) not completed this session [RoomCard.tsx] — deferred, already disclosed in Task 3 completion notes with mitigations; human visual pass recommended before/at merge, and should also confirm/refute the grid-gap decision-needed finding above.

## Dev Notes

### Why this story starts with a design gate, not code

Unlike most stories in this project, the epic text explicitly withholds the grid spec: "Sally (UX Designer) proposes a responsive grid layout... presented to Ralf for approval before implementation begins; this is a design decision to be made during this story, not a pre-made spec handed to the dev agent" (`epic-8-ui-behavior-consistency-alignment.md`, Story 8.4 AC1). This story file therefore does not prescribe column counts — Task 1 is a hard gate. Skipping it and picking a plausible-looking `grid-cols-N` would satisfy the letter of AC2 but violate AC1 and the epic's explicit intent.

### Current `RoomCard.tsx` state (read in full during story creation)

- Device list container: `client/src/features/decomposition/components/RoomCard.tsx:51` — `<div className="flex flex-col gap-2 px-4 pb-3.5">`, single column on every viewport, no breakpoint classes at all today.
- `partitionAndSortDevices` (`RoomCard.tsx:21-30`, from Story 7.3): filters out `None`-approach non-strip devices, then orders `[...measured-sorted-by-kwh-desc, ...estimated-sorted-by-kwh-desc]`. Smart strips count as "measured" for this partition. This function's output order is what the grid must render left-to-right, top-to-bottom — CSS Grid's default `dense`-less auto-flow already respects DOM/document order, so no `order` property is needed as long as the `.map()` output order is unchanged (it is — Task 2 doesn't touch this function).
- "Direct consumption" fallback (`RoomCard.tsx:45-49`): renders instead of the device list entirely when `room.devices.length === 0` or every device is `None`-approach non-strip. This fallback is a `flex` row, not part of the device grid — it's a separate branch of the `isDirectConsumptionOnly` ternary and is unaffected by the grid class change unless Sally's design says otherwise.

### Card sizing — no internal width assumptions to break

- `DeviceCard.tsx` (both the `Measured` branch, lines 24-36, and the `EuLabel`/`SelfMeasured`/estimated branch, lines 44-58): no explicit `width`, sizes via `px-4 py-3.5` (Measured) or `px-3.5 py-2.5` (estimated) padding only. Will naturally fill a grid cell.
- `SmartStripCard.tsx`: taller/denser — has its own header row (name + kWh/cost + badge) plus a `flex flex-col gap-1.5` sub-device list (lines 41-66). No explicit width either, but its natural height is visually inconsistent with a single `DeviceCard` — this is exactly why AC4 requires Sally to explicitly decide its grid behavior (full-span vs. normal cell) rather than leaving ad-hoc grid-auto-flow to produce a lopsided row.

### Breakpoint convention already established elsewhere in this app

- `client/src/index.css` has a Tailwind v4 `@theme {}` block with no `screens` override — Tailwind's **default** breakpoints are in effect: `md:` = 768px, `lg:` = 1024px. This lines up exactly with `UX-DR12`'s phone(<768)/tablet(768–1023)/desktop(≥1024) convention (`requirements-inventory.md:123`) — no custom breakpoint values need to be introduced.
- Reference precedent: `DashboardGrid.tsx:97` — `<div className="grid grid-cols-2 gap-3 md:grid-cols-4">` (KPI tiles: 2×2 phone → 4-across tablet+, no separate desktop step). `AppShell.tsx:20` — sidebar nav appears at `md:flex` (768px+), reserving 200px, so tablet+ available width for `RoomCard.tsx`'s content is `viewport - 200px` minus page padding (`DecompositionPage.tsx:20`, `px-4`). There is **no page-level `max-width` constraint** anywhere between `AppShell.tsx`'s `main` and `RoomCard.tsx` — on a wide desktop window, `RoomCard.tsx` currently spans the full remaining width. This is useful context for Sally: unlike the KPI grid (fixed 4 tiles), the device grid's column count needs to make sense at arbitrarily wide desktop viewports too, not just at the 1024px breakpoint boundary.
- `requirements-inventory.md:123` (`UX-DR12`) already states the desktop-tier design intent as "multi-column decomposition" without specifying a count — confirming this has genuinely not been decided yet anywhere in the planning artifacts, consistent with AC1's design-gate framing.

### Previous story intelligence (Story 8.3)

Story 8.3's own experience is the clearest warning for this story: its epic text stated a root cause ("`popover.tsx` has a stacking bug") that turned out to be **stale** — the real bug was in a different, unaudited component. The lesson carried into this story: verify current file state yourself (done above) rather than trusting the epic text's framing verbatim, and — specific to 8.4 — treat the epic's explicit "not a pre-made spec" instruction as binding, not as boilerplate to route around by inventing a plausible grid yourself.

Stories 8.1/8.2 (autosave, sticky save-bar placement) are unrelated in surface area (`FlatStructureEditor.tsx`, `DeviceEditor.tsx`) — no shared code path with `RoomCard.tsx`. No cross-story pattern to reuse beyond the general "read current file state before touching it" discipline.

### Testing standards summary

- Frontend: Vitest + `@testing-library/react`, colocated `.test.tsx`, `globals: true` (no `describe`/`it`/`expect` imports). `RoomCard.test.tsx` (`client/src/features/decomposition/components/RoomCard.test.tsx`) already covers ordering via `compareDocumentPosition` — DOM-order-based assertions, unaffected by a CSS grid visual reflow. No new test file is required by this story's ACs; if the approved design introduces a genuinely new structural behavior (e.g., a wrapper element added around `SmartStripCard` for spanning), add/extend a `RoomCard.test.tsx` case for it — do not add a snapshot test for arbitrary Tailwind class strings, they add no regression value and churn on every design tweak.
- Query by role/label/text, not CSS class, per project convention — this applies to any new test added, not to the implementation's own `className` values (which are exactly what's changing here and don't need role-based assertions).

### Project Structure Notes

- Modify: `client/src/features/decomposition/components/RoomCard.tsx` only (device-list container `className`, plus a possible span-utility class on the `SmartStripCard`/direct-consumption wrapper per Sally's design).
- No changes to: `DeviceCard.tsx`, `SmartStripCard.tsx`, `DecompositionTab.tsx`, `DecompositionPage.tsx`, any `decomposition/api/` or `decomposition/hooks/` file, `client/src/index.css` (no new `@theme` tokens needed — default Tailwind breakpoints already suffice per the analysis above).
- No backend changes. No new i18n keys — this is a layout-only story.
- No new dependencies.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-8-ui-behavior-consistency-alignment.md#Story 8.4] — original epic AC text; AC1's "design decision during this story" framing is carried into this story's own AC1/Task 1 as a hard gate.
- [Source: _bmad-output/planning-artifacts/epics/requirements-inventory.md#FR-47] — "On tablet and desktop viewports, device cards within a Room Card lay out in a responsive multi-column grid instead of a single full-width column per device" — the functional requirement this story satisfies.
- [Source: _bmad-output/planning-artifacts/epics/requirements-inventory.md#UX-DR12] — breakpoint convention: phone <768px, tablet 768–1023px, desktop ≥1024px; desktop tier already named "multi-column decomposition" without a specified count.
- [Source: client/src/features/decomposition/components/RoomCard.tsx] — full current implementation read during story creation; line 51 is the only line this story's implementation task changes (plus possibly a span class on one child).
- [Source: client/src/features/decomposition/components/DeviceCard.tsx] — confirmed no explicit width, padding-only sizing on both render branches (Measured / estimated).
- [Source: client/src/features/decomposition/components/SmartStripCard.tsx] — confirmed taller/denser structure (header + sub-device list), no explicit width; this is why AC4 requires an explicit grid-behavior decision for this card type.
- [Source: client/src/features/decomposition/components/RoomCard.test.tsx] — existing test suite; DOM-order-based assertions unaffected by CSS grid reflow, confirmed to require no rewrites for this story's scope.
- [Source: client/src/features/decomposition/components/DecompositionTab.tsx] — confirms `RoomCard` is rendered in a `flex flex-col gap-3` list, one per room, sorted by kWh at the tab level (unrelated to and unaffected by this story's intra-card device grid).
- [Source: client/src/features/decomposition/DecompositionPage.tsx] — confirms `px-4 pt-4` page padding and no `max-width` constraint on the content column; relevant to Sally's desktop-width column-count decision.
- [Source: client/src/components/AppShell.tsx] — confirms sidebar nav (`w-[200px]`) appears at `md:` (768px+), reducing available content width on tablet/desktop versus phone.
- [Source: client/src/features/dashboard/components/DashboardGrid.tsx] — reference precedent for this app's existing responsive-grid idiom (`grid grid-cols-2 gap-3 md:grid-cols-4`), offered to Sally as prior art, not as this story's answer.
- [Source: client/src/index.css] — confirmed `@theme {}` block has no `screens` override; Tailwind default breakpoints (`md:`768px, `lg:`1024px) are what's actually in effect app-wide.
- [Source: _bmad-output/implementation-artifacts/8-3-overlay-and-dropdown-visibility-audit.md] — previous story; its "epic's stated root cause was stale, verify current code yourself" lesson informed this story's own from-scratch file reads above.
- [Source: _bmad-output/project-context.md] — VSA feature-folder conventions, Tailwind v4 `@theme` token rules (no `tailwind.config.js`), Vitest/testing-library conventions applied above.

### Approved grid design (Sally, 2026-07-17)

Approved as proposed, no changes from initial proposal:

| Breakpoint | Columns | Class |
|---|---|---|
| Phone (<768px) | 1 | (default, unprefixed) |
| Tablet (768–1023px) | 2 | `md:grid-cols-2` |
| Desktop (≥1024px) | 3 | `lg:grid-cols-3` |

- Device list container (`RoomCard.tsx:51`): `grid grid-cols-1 gap-2 px-4 pb-3.5 md:grid-cols-2 lg:grid-cols-3` (same `gap-2`/padding as today, `flex flex-col` → `grid`).
- `SmartStripCard`: full-width span at tablet/desktop via `md:col-span-full` on a thin wrapper `<div>` in `RoomCard.tsx` around the existing `<SmartStripCard .../>` call — no changes inside `SmartStripCard.tsx` (it has no `className` prop). Rationale: its variable-height header + sub-device list structure would create lopsided rows if squeezed into a single grid cell next to compact `DeviceCard`s.
- "Direct consumption" fallback (`RoomCard.tsx:45-49`): untouched — it's a separate branch of the `isDirectConsumptionOnly` ternary, mutually exclusive with the device grid (replaces the whole list, not a grid item), so no grid class applies to it.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

### Completion Notes List

- Task 1 (UX design gate): Sally (bmad-agent-ux-designer) proposed a 1→2→3 column grid (phone/tablet/desktop) with `SmartStripCard` spanning full width via a wrapper `md:col-span-full` and the "Direct consumption" fallback left untouched. Presented to Ralf and approved as proposed (no changes). See "Approved grid design" in Dev Notes above.
- Task 2 (implementation): Replaced `RoomCard.tsx`'s device-list `flex flex-col gap-2` container with `grid grid-cols-1 gap-2 md:grid-cols-2 lg:grid-cols-3`. Wrapped `<SmartStripCard>` in a `<div className="md:col-span-full">` (no changes to `SmartStripCard.tsx` itself — it has no `className` prop). `partitionAndSortDevices`, `DeviceCard.tsx`, and the "Direct consumption" fallback branch are untouched, per AC3/AC5. Followed red-green-refactor: added two new `RoomCard.test.tsx` cases (grid container classes, `SmartStripCard` span wrapper) that failed against the old markup, then implemented the change until they passed.
- Task 3 (regression): `npx tsc --noEmit` clean; `npx vitest run` — 380/380 passing across 59 files (all 8 `RoomCard.test.tsx` cases pass, including the pre-existing `compareDocumentPosition`-based ordering tests, unchanged as required by AC6); `npm run lint` clean (only pre-existing unrelated `router.tsx` fast-refresh warnings). Interactive in-browser viewport verification (375/800/1280px) could not be completed in this session — see the Task 3 checklist note for details and mitigations taken instead. Recommend a quick human visual pass before/at merge.

### File List

- `client/src/features/decomposition/components/RoomCard.tsx` (modified — device-list container changed from `flex flex-col` to responsive `grid`; `SmartStripCard` now wrapped in a `md:col-span-full` span wrapper)
- `client/src/features/decomposition/components/RoomCard.test.tsx` (modified — added `RoomCard_MultipleDevices_UsesResponsiveGridContainer` and `RoomCard_SmartStripDevice_WrapperSpansFullGridWidth` test cases)
- `client/src/features/decomposition/components/SmartStripCard.tsx` (modified — see Addendum below)
- `client/src/features/decomposition/components/SmartStripCard.test.tsx` (modified — see Addendum below)

## Addendum: Sub-Device Grid in `SmartStripCard` (2026-07-17)

Ralf asked to also improve the layout of the sub-device rows inside power-point (`SmartStripCard`) cards — e.g. "Verteiler HiFi" with 9 sub-devices was a single full-width column, wasting horizontal space and forcing a long vertical scroll. Handled as a follow-up within this same story rather than a new one, following the same UX-gate-then-implement pattern as the story's own AC1.

### Approved design (Sally, 2026-07-17)

Sally proposed 3 options (matching the outer 1/2/3-col grid; capped at 2 columns; adaptive `auto-fit`/`minmax`). Ralf approved **"Capped at 2 columns"**:

| Breakpoint | Columns | Class |
|---|---|---|
| Phone (<768px) | 1 | (default, unprefixed) |
| Tablet (768–1023px) | 2 | `md:grid-cols-2` |
| Desktop (≥1024px) | 2 | — no `lg:` step, stays at 2 |

Rationale: unlike a plain `DeviceCard`, a sub-device row carries more per-item content (name + kWh + cost + an optional "Zu den Einstellungen" configure button), and this app's sub-device names include long German compounds (`Universalfernbedienung`, `Switch 2 Docking`) that need more per-cell width than a 3-column split would give them.

- Unconfigured sub-device rows (dimmed, with the configure button) get **no special-case grid treatment** — they sit in a normal grid cell like any other row; the button is compact enough to wrap gracefully under a long name if needed. No `col-span-full` exception, unlike the outer grid's `SmartStripCard` treatment.
- Order: `sortSubDevices` (configured-by-kwh-desc, then unconfigured-by-kwh-desc) is untouched — grid consumes its output via default document-order flow, same principle as this story's own AC3.
- Scope: only `SmartStripCard.tsx`'s sub-device list container `className` changes. No changes to `RoomCard.tsx`, `DeviceCard.tsx`, or any API/hook file.

### Implementation

- `SmartStripCard.tsx`: sub-device container changed from `flex flex-col gap-1.5 px-4 pb-3.5` to `grid grid-cols-1 gap-1.5 px-4 pb-3.5 md:grid-cols-2`.
- TDD: added `SmartStripCard_MultipleSubDevices_UsesTwoColumnGridContainer` to `SmartStripCard.test.tsx` (asserts a single `.grid` container, `grid-cols-1 md:grid-cols-2` classes present, `lg:grid-cols-3` explicitly absent, and both sub-devices land inside it) — confirmed red against the old `flex` markup, then green after the change. All 5 pre-existing `SmartStripCard.test.tsx` cases (including the `compareDocumentPosition`-based ordering test) pass unchanged.
- Regression: `npx tsc --noEmit` clean; `npx vitest run` — 382/382 passing across 59 files; `npm run lint` clean (only pre-existing unrelated `router.tsx` warnings). Interactive in-browser viewport verification not performed in this session (same tooling constraint as the base story) — recommend a human visual pass alongside the base story's own pending visual check.
