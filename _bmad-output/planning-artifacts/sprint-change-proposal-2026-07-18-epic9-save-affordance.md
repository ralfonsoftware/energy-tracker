# Sprint Change Proposal — Insert Epic 9: Unified Save-Affordance Design & Fix (2026-07-18)

## 1. Issue Summary

Following the Epic 8 Retrospective (2026-07-18, `_bmad-output/implementation-artifacts/epic-8-retro-2026-07-18.md`), Ralf asked Amelia to investigate Action Item #1: whether the reported "save button may not be visible until you scroll" complaint was a `StickyActionBar` regression or the room-list's per-room Save button behaving as designed (no sticky guarantee was ever specified there). The investigation (`_bmad-output/implementation-artifacts/investigations/power-points-scroll-visibility-investigation.md`) confirmed, with a live-reproduced repro against the deployed app:

- **Root cause:** `StickyActionBar.tsx` (used by both `RoomEditor.tsx` and `DeviceEditor.tsx`) applies `position: sticky` to an element rendered as a trailing sibling after the scrollable content, with only a `pb-32` (~128px) buffer. This mechanism only "sticks" once the user has scrolled to within that buffer of the very end of the content. On the Wohnzimmer room (5 Power Points), the Save bar was measured to be completely absent from the viewport for the first 93% of scroll range (`document.scrollingElement`: `scrollHeight=2054`, `clientHeight=941`, `maxScroll=1113`; sticky bar only stuck for the final 73px).
- **This is not a tunable buffer bug.** It is a structural mismatch between the chosen CSS mechanism and the intended UX (an always-visible floating action bar). It affects both `RoomEditor.tsx` (Power Point save) and `DeviceEditor.tsx` (Cancel/Save) identically, since both share the same `StickyActionBar` component.
- **PRD conformance gap:** FR-45 ("every save/cancel action... remains within the visible viewport without requiring the user to scroll to find it, across all supported browsers, including Safari") was the acceptance target for Story 8.2, which shipped `StickyActionBar` and was marked `done`. The investigation shows FR-45 is not actually met for non-trivial content lengths — this is a conformance gap on an already-`done` story, not new scope.
- **Separately, the room-list view's own save affordance (page-level "Speichern" button + per-room inline Save icons in `FlatStructureEditor.tsx`) was confirmed NOT scroll-broken** — it uses a structurally different, always-in-header or in-flow mechanism. It is, however, visually inconsistent with the Power Point/Device-edit save pattern, which Epic 8's retro already flagged as an open item (Challenge #1) and scoped as Action Item #2: a unified save-affordance design pass across all three contexts, recommended to run before Epic 9 (Actionable Insights) begins.

## 2. Impact Analysis

- **PRD Impact:** No new FR needed. FR-45 already exists and already specifies the correct target behavior; the gap is non-conformance by the implementation, not a missing requirement. `requirements-inventory.md`'s FR coverage map is annotated to note the conformance fix lands in the new epic.
- **Architecture Impact:** None. `architecture.md` has no documented pattern for save-action positioning or `StickyActionBar` — confirmed via full-file grep, zero matches.
- **UX Impact:** No pre-written UX-DR. Following the Story 8.4 precedent (design decision made live within the story via a Sally-proposes/Ralf-approves gate, not handed down pre-made), the unified save-affordance pattern is designed as part of the new epic's first story, with a hard constraint that the chosen mechanism must structurally guarantee visibility (not a restyle of the broken `position: sticky` approach).
- **Epic Impact:**
  - **Epic 8** stays fully `done` (all 4 stories + retrospective complete) — this is new work, not reopened history, consistent with the Epic-5-stays-done / Item-4-as-new-story precedent from the 2026-07-05 pre-Epic-6 proposal.
  - **A new epic is inserted before the current Epic 9 (Actionable Insights)**, per Epic 8 retro's own Verdict: *"a new UI-consistency follow-up (save-affordance unification) is recommended to run before Epic 9, continuing the same theme Epic 8 was created for."*
  - **Numbering:** the new epic takes the number **Epic 9**; Actionable Insights is renumbered to **Epic 10**. This is a full renumber, not a fractional insertion (e.g. "Epic 8.5") — matching the exact precedent set when Epic 8 itself was inserted (Epic 7 retro Action Item #1: "Insert new epic as Epic 8, renumber Insights to Epic 9" — `epic-8-*.md` and `epic-9-*.md`, formerly `epic-8-actionable-insights.md`, were created together in commit `0bda6aa`). Confirmed with Ralf before proceeding.
  - **No other epic is affected.** The new epic touches only `client/src/features/flat-structure/components/` (`StickyActionBar.tsx`, `RoomEditor.tsx`, `DeviceEditor.tsx`, `FlatStructureEditor.tsx`); Epic 10 (Actionable Insights) is domain-disjoint (Insights detectors, `InsightsTab.tsx`) and unaffected in content, only in number.
- **Technical Impact:** Isolated to the `flat-structure` feature slice. The fix requires replacing `StickyActionBar`'s positioning mechanism (`position: fixed`, or a fixed-content-area-plus-non-scrolling-footer layout) rather than tuning existing buffer values.
- **Testing Impact:** This class of bug (scroll/layout positioning) is invisible to `jsdom`-based automated tests — confirmed independently by this investigation and already flagged in Epic 8 retro Challenge #5. The new epic's structural-fix story requires an explicit manual-verification AC in both Chrome and real Safari, continuing the practice already established by Epic 8 retro Action Item #4 (at least one live manual verification pass before closing an epic shipping new interactive UI).

## 3. Recommended Approach

**Direct Adjustment — insert a new epic.** Three options were evaluated:

1. **Direct Adjustment (selected):** Insert a new epic scoped to the unified save-affordance design pass and the `StickyActionBar` structural fix. Effort: Medium — one structural CSS/layout fix applied to two components, plus a genuine cross-context design decision, plus a visual-only restyle of the (unaffected) room-list save buttons. Risk: Low–Medium — root cause is fully understood and live-reproduced; blast radius is limited to one feature slice.
2. **Potential Rollback:** Not viable. Reverting Story 8.2 would not simplify the fix — the bottom-action-bar *concept* is sound and worth keeping; only its CSS positioning mechanism is broken. Rolling back gains nothing.
3. **PRD MVP Review:** Not applicable. This is Release 3 (UI & Behavior Consistency) scope, already an established, non-MVP-blocking category since Epic 8's insertion. No MVP scope change is needed.

## 4. Detailed Change Proposals

### New file: `epics/epic-9-unified-save-affordance-design-fix.md`

Full new epic file with 3 stories (Unified Save-Affordance Design Decision; Fix RoomEditor & DeviceEditor Save Bar — Structural; Align Room-List Save Affordance). See file for complete Given/When/Then acceptance criteria — summarized:

- **Story 9.1** — Sally proposes one unified visual pattern across all three save contexts; Ralf approves before implementation. Hard constraint: the mechanism must structurally guarantee visibility (not a `position: sticky` re-skin).
- **Story 9.2** — Replace `StickyActionBar`'s broken mechanism in both `RoomEditor.tsx` and `DeviceEditor.tsx`, per the approved design. Includes a manual Chrome + real-Safari verification AC, and removal/rewrite of the now-inaccurate `pb-32` buffer comments.
- **Story 9.3** — Restyle `FlatStructureEditor.tsx`'s page-level and per-room Save buttons to match the unified pattern, with an explicit AC that this is presentation-only — no changes to `handleSave`/`handleSaveRoom`/`isRoomDirty` logic.

### Renamed: `epics/epic-9-actionable-insights.md` → `epics/epic-10-actionable-insights.md`

Header renumbered `# Epic 9: Actionable Insights` → `# Epic 10: Actionable Insights`; all four story headers renumbered `Story 9.1`–`9.4` → `Story 10.1`–`10.4`. No content changes beyond numbering — verified via targeted grep before editing that only the 5 expected header lines contained `9.x`/`Epic 9` patterns.

### `epics/epic-list.md`

Inserted a new "Epic 9: Unified Save-Affordance Design & Fix" summary entry (with FR/UX coverage lines) immediately before the renumbered "Epic 10: Actionable Insights" entry.

### `epics/index.md`

Table of contents updated: new Epic 9 entry + 3 story links inserted; Epic 9 → Epic 10 renumbered for Actionable Insights' entry and all 4 of its story anchor links.

### `epics/requirements-inventory.md`

FR coverage map: `FR-35`, `FR-36`, `FR-37`, `FR-38`, `FR-39`, `FR-43` reassigned from "Epic 9" to "Epic 10". `FR-45`'s entry annotated: *"(conformance completed in Epic 9 — Story 8.2's StickyActionBar did not structurally satisfy this FR)"*.

### `planning-artifacts/index.md`

Epic count updated 9 → 10; new epic-9 file link added; epic-9-actionable-insights.md link updated to epic-10-actionable-insights.md.

### `implementation-artifacts/sprint-status.yaml`

`epic-9` block replaced with the new epic (3 stories, `backlog` status, retrospective `optional`); a new `epic-10` block added for the renumbered Actionable Insights (4 stories, `backlog`, retrospective `optional`). `last_updated` comment and field updated to record this change.

### Documents intentionally NOT edited (historical record)

`_bmad-output/implementation-artifacts/epic-7-retro-2026-07-14.md` and `_bmad-output/implementation-artifacts/epic-8-retro-2026-07-18.md` both reference "Epic 9" meaning Actionable Insights (as it was numbered at the time each was written). Per the project's own established precedent (the Story-6.0 naming decision explicitly avoided invalidating numbered references in "finalized historical documents"), these dated retrospectives are left unedited — they are point-in-time records, and their "Epic 9" now refers to what is renumbered here to Epic 10. This proposal is the forward-looking record of the renumbering, exactly as the Epic 7 retro's Action Item #1 was the forward-looking record of the prior Epic 8 insertion.

## 5. Implementation Handoff

**Scope classification: Moderate** — backlog reorganization (new epic inserted, one epic renumbered) plus a design-then-implement story sequence; no PRD/Architecture replan needed.

- **Product Owner / Developer (Amelia):** Implements Stories 9.2 and 9.3 once Story 9.1's design is approved. Story 9.1 itself is a design-gate story — Sally proposes, Ralf approves, before any of Amelia's implementation work in 9.2/9.3 begins.
- **UX Designer (Sally):** Owns Story 9.1 — the unified save-affordance pattern proposal, constrained to a structurally-visible mechanism per the investigation's findings.
- **Ralf:** Approves Story 9.1's design before implementation proceeds; confirmed the Epic 9/10 renumbering approach before this proposal was finalized.

**Success criteria:** Epic 9 reaches `done` when all 3 stories are `done` and a live manual verification pass (Chrome + real Safari) confirms the Save action remains visible without scrolling on both `RoomEditor.tsx` and `DeviceEditor.tsx`, for content lengths matching or exceeding the investigation's repro case.
