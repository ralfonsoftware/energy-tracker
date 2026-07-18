---
baseline_commit: 9611ad72ff7956ae5533e6e749aec8ed6d749082
---

# Story 9.1: Unified Save-Affordance Design Decision

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want the Save action to look and behave the same way everywhere in Flat Structure settings — the room list, a room's Power Point list, and the Device edit screen,
so that I recognize and trust the save affordance regardless of which screen I'm on.

## Acceptance Criteria

1. **Given** the approved D-45 design (`_bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/.decision-log.md#D-45`, Sally proposed, Ralf approved 2026-07-18), **when** implemented for `RoomEditor.tsx` and `DeviceEditor.tsx` (Story 9.2), **then** the action bar uses `position: fixed; bottom: calc(84px + env(safe-area-inset-bottom, 0px))` on phone / `bottom: 0` on tablet+ (identical offsets to today's `StickyActionBar`, clearing `BottomTabBar`'s 72px + 12px gap), with the same visual styling already shipped — no other visual changes.
2. **Given** the confirmed root cause in `power-points-scroll-visibility-investigation.md` that `position: sticky` on a trailing sibling is mathematically incapable of remaining visible without scrolling once content exceeds the viewport by more than its buffer (live-reproduced: invisible for ~93% of scroll range on a 5-Power-Point room), **when** implemented, **then** `position: fixed` structurally guarantees visibility regardless of content length — verified by Story 9.2's own manual Chrome + Safari check against the investigation's repro case.
3. **Given** FR-45 ("every save/cancel action... remains within the visible viewport without requiring the user to scroll to find it, across all supported browsers, including Safari"), **when** implemented, **then** the fixed-position mechanism satisfies FR-45 as written, including the Safari clause.
4. **Given** the room list's per-row inline Save affordance and page-level "Speichern" button in `FlatStructureEditor.tsx` are functionally correct today and not scroll-broken (Finding 4 of the investigation), **when** D-45 is finalized, **then** the design decision explicitly confirms these stay inline/per-row (not collapsed into a global bar) and are only reskinned to match the fixed bar's border/background/accent/spinner treatment — this is Story 9.3's scope, not this story's, but must be unambiguous in the recorded decision so Story 9.3 has no open question.

## Tasks / Subtasks

- [x] Task 1: Confirm and record the D-45 design decision as final (AC: 1, 2, 3, 4)
  - [x] Verify D-45 is present and unambiguous in `.decision-log.md` (it is — see Dev Notes) and cross-check every concrete value it cites (offsets, breakpoint, `pb-32`) against the actual current code in `StickyActionBar.tsx`, `RoomEditor.tsx`, `DeviceEditor.tsx`, `BottomTabBar.tsx` (all read and confirmed matching during this story's creation — see Dev Notes "Verified against current code").
  - [x] Confirm no halt-and-ask is needed: this design decision was already proposed by Sally and approved by Ralf on 2026-07-18 (predates this story's creation) — Task 1 is a verification/formalization pass, not a fresh design negotiation. If any discrepancy is found between D-45's description and the current code, flag it to Ralf before Story 9.2 proceeds; otherwise no further design work occurs in this story.
  - [x] Confirm this story makes **no source code changes** — it is a design-decision/spec story only, consumed by Stories 9.2 (fixed-position mechanism) and 9.3 (room-list reskin).

- [x] Task 2: Regression pass (AC: n/a — no code changed)
  - [x] Since no production code is touched by this story, run no build/test commands beyond confirming `git status` shows no unintended source changes before marking this story done.

## Dev Notes

- This story is a **design-decision/spec gate**, following the same pattern established by Story 8.4 (see that story's "Why this story starts with a design gate, not code" Dev Note). Unlike 8.4, the design negotiation itself already happened during Epic 9 planning — D-45 is recorded and approved as of 2026-07-18, *before* this story file was created. This story's job is to formalize/verify that decision as the authoritative spec for Stories 9.2 and 9.3, not to run a fresh UX design pass.
- **Do not implement the fixed-position mechanism in this story.** AC1–AC3 read "when implemented... (Story 9.2)" verbatim from the epic — they describe what 9.2 must satisfy, using this story's confirmed decision as its spec. This story's own deliverable is the confirmed decision text plus the verification below, not code.

### The approved decision (D-45, verbatim summary)

- **Full-screen edit contexts** (`RoomEditor.tsx`'s Power Point save, `DeviceEditor.tsx`'s Cancel/Save): reuse `StickyActionBar`'s existing visual styling and offset math verbatim (`bottom: calc(84px + env(safe-area-inset-bottom, 0px))` phone / `bottom: 0` tablet+) — only the positioning mechanism changes, from `position: sticky` to `position: fixed`, the same mechanism `BottomTabBar.tsx` already uses (fixed, bottom-anchored, safe-area-hardened since Story 5.5). Scrollable content keeps its existing `pb-32` bottom padding so the fixed bar never covers the last field.
- **Room list** (`FlatStructureEditor.tsx`'s per-room saves + page-level "Speichern"): placement stays inline/per-row — collapsing many independent per-room save actions into one global bar would be an interaction regression (forces selecting a row before saving it). Reskin only: same border/background treatment, accent colors, and pending-spinner styling as the fixed bar (Story 9.3's scope).

### Verified against current code (this story's creation session, 2026-07-18)

- `StickyActionBar.tsx:9-14` — confirmed current classes/offsets exactly match D-45's cited values: `sticky bottom-[calc(84px_+_env(safe-area-inset-bottom,0px))] md:bottom-0`, solid `background: '#111827'`, `borderTop: '1px solid rgba(255,255,255,0.12)'`.
- **Important correction for Story 9.2's implementer:** D-45's phrase "glass-pill visual styling" is loose shorthand, not literal. `StickyActionBar`'s own container has **no** `backdrop-filter`/blur — it is a solid `#111827` bar with a plain top border (unlike `BottomTabBar.tsx`, which genuinely is glass: `rgba(10,15,25,0.75)` + `backdropFilter: blur(20px) saturate(180%)`). The "pill" part refers only to the `rounded-full` buttons rendered *inside* the bar (`RoomEditor.tsx:114`, `DeviceEditor.tsx:307,316`), styled `rgba(255,255,255,0.12)` background with a `rgba(255,255,255,0.40)` border. **Do not add a backdrop-filter/glass effect to the bar itself in Story 9.2** — D-45 and this story's AC1 both say "no other visual changes"; only the container's `className`/inline styles needed are `position: fixed` in place of `sticky` (offsets and background unchanged).
- `RoomEditor.tsx:57-58,93-119` and `DeviceEditor.tsx:86-87,302-322` — confirmed both still use `StickyActionBar` as a trailing sibling with `pb-32` on the preceding scrollable content, matching the investigation's Finding 1 and D-45's "keeps its existing `pb-32`" assumption. Both files also carry a code comment (`RoomEditor.tsx:57`, `DeviceEditor.tsx:86`) documenting the old sticky-buffer intent — Story 9.2 AC (epic Story 9.2, not this story) already calls for rewriting/removing these comments once the mechanism changes; flagging here so Story 9.2's dev agent doesn't miss it.
- `BottomTabBar.tsx:19-27` — confirmed `fixed bottom-0 left-0 right-0` with `height: calc(72px + env(safe-area-inset-bottom, 0px))` — this is the proven `position: fixed` + safe-area pattern D-45 directs Story 9.2 to replicate for the action bar (at its own, different, offset).
- `FlatStructureEditor.tsx:268-276` (page-level Save button, in the fixed, non-scrolling header block) and `:356-373` (per-room inline Save icon, `rounded-full`, `rgba(255,255,255,0.12)` bg / `rgba(255,255,255,0.40)` border — already visually closer to the target pill styling than a full reskin might suggest) — confirmed structurally separate from `StickyActionBar`, exactly as Finding 4 of the investigation states. No code changes in this story.

### Why `position: fixed` and not a taller `pb-*` buffer

Documented in the investigation's "Recommended Next Steps": a trailing `position: sticky` sibling can only ever "stick" for the fraction of scroll range between its natural flow position and the viewport bottom — tuning `pb-32` larger only shrinks (never eliminates) the invisible window, and would need to scale with content length (number of Power Points/devices), which is exactly the kind of per-page magic-number tuning `position: fixed` avoids entirely by removing the bar from document flow.

### Project Structure Notes

- No files are created or modified by this story. Story 9.2 will modify `StickyActionBar.tsx`, `RoomEditor.tsx`, `DeviceEditor.tsx`. Story 9.3 will modify `FlatStructureEditor.tsx`.
- No backend changes, no new dependencies, no new i18n keys.

### Testing Standards Summary

- No tests are added or run by this story (no code changes). Story 9.2 owns the manual Chrome+Safari verification called for in AC2; this class of layout/scroll-positioning bug is invisible to `jsdom`-based automated tests (confirmed by both the investigation and Epic 8 retro Challenge #5) — do not attempt to add a Vitest regression test for scroll visibility in either this story or 9.2.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-9-unified-save-affordance-fix-and-pre-epic-10-hardening.md#Story 9.1] — original epic AC text (verbatim source for this story's ACs).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/.decision-log.md#D-45] — the approved design decision this story formalizes.
- [Source: _bmad-output/implementation-artifacts/investigations/power-points-scroll-visibility-investigation.md] — root-cause investigation (Epic 8 retro Action Item #1) that D-45 and this story's AC2 are built on; Findings 1–4 and the "Fix direction" recommendation directly inform the approved mechanism.
- [Source: _bmad-output/implementation-artifacts/epic-8-retro-2026-07-18.md] — Challenges #1/#2 and Action Items #1/#2, the origin of this epic's Part 1 scope.
- [Source: client/src/features/flat-structure/components/StickyActionBar.tsx] — current component, fully read and verified during this story's creation (see "Verified against current code" above).
- [Source: client/src/features/flat-structure/components/RoomEditor.tsx] — current `StickyActionBar` usage and `pb-32` buffer, verified.
- [Source: client/src/features/flat-structure/components/DeviceEditor.tsx] — current `StickyActionBar` usage and `pb-32` buffer, verified.
- [Source: client/src/components/BottomTabBar.tsx] — reference precedent for the `position: fixed` + safe-area pattern D-45 directs Story 9.2 to reuse.
- [Source: client/src/features/flat-structure/components/FlatStructureEditor.tsx] — room-list Save button locations, confirmed structurally separate (Story 9.3 scope only).
- [Source: _bmad-output/planning-artifacts/epics/requirements-inventory.md#FR-45] — "every save/cancel action... remains within the visible viewport without requiring the user to scroll to find it, across all supported browsers, including Safari" — the functional requirement this story's decision satisfies.
- [Source: _bmad-output/implementation-artifacts/8-4-responsive-device-card-grid-room-card-layout-on-tablet-and-desktop.md] — precedent for this project's "design-gate story" format, referenced by the epic itself as the established pattern (`epic-9...md` Part 2 intro: "matching this project's established design-gate pattern (Stories 8.4, 9.1)").
- [Source: _bmad-output/project-context.md] — Tailwind v4 `@theme` conventions, VSA feature-folder rules, applied in the verification above.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

### Completion Notes List

- Task 1: Independently re-verified (dev-story session, distinct from story-creation session) every concrete value D-45 cites against current source: `.decision-log.md#D-45` (`bottom: calc(84px + safe-area-inset-bottom)` phone / `bottom: 0` tablet+, `pb-32`, glass-pill phrasing) matches `StickyActionBar.tsx:10,12` (`bottom-[calc(84px_+_env(safe-area-inset-bottom,0px))] md:bottom-0`, solid `background: '#111827'`, no backdrop-filter) and `BottomTabBar.tsx:19,23,25` (`fixed bottom-0`, `rgba(10,15,25,0.75)` + `backdropFilter: blur(20px) saturate(180%)` — genuinely glass, confirming the container-level "glass" distinction flagged in Dev Notes is real and not a documentation error). No discrepancy found; no halt-and-ask triggered. `git rev-parse HEAD` confirmed unchanged since `baseline_commit`, and `git status` showed no working-tree source changes prior to this session's edits — confirming the decision hasn't drifted since story creation.
- Task 2: No production code was touched by this story (by design — a design-decision/spec gate, not an implementation story). `git status` confirms the only changes are this story file and `sprint-status.yaml` (workflow bookkeeping). No test suite run — nothing to regress; this is documented and intentional per the story's own Testing Standards Summary.
- Net effect: D-45 is confirmed final and accurate. Story 9.2 can proceed directly against the "Verified against current code" section in Dev Notes without re-deriving these values, including the glass-pill correction (do not add backdrop-filter to `StickyActionBar`'s container).
- Code-review (`bmad-code-review`) skipped by explicit user decision (Ralf, 2026-07-18): diff against `baseline_commit` contains only `sprint-status.yaml` bookkeeping and this story file itself — no production source changed, nothing for an adversarial code review to examine. Story marked `done` directly.

### File List

- None — this story makes no source/production code changes (design-decision/spec gate only, per Task 1/Task 2 scope). Only this story file itself and `_bmad-output/implementation-artifacts/sprint-status.yaml` (status bookkeeping) changed.
