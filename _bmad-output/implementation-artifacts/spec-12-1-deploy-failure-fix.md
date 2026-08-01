---
title: 'Fix Story 12.1 deploy failure — stale DeviceResponse test fixtures'
type: 'bugfix'
created: '2026-08-01'
status: 'done'
route: 'one-shot'
---

## Intent

**Problem:** Story 12.1's deploy (GitHub Actions run 30709243022) failed at the `deploy` job's "Build frontend" step (`tsc -b`) because two pre-existing `DeviceResponse` mock literals in `FlatStructureEditor.test.tsx` — a file the story never touched — were missing the two newly-required fields (`inUseSince`, `decommissionedDate`). This went undetected pre-merge because Vitest doesn't type-check and the documented verification command, `npx tsc --noEmit`, is a silent no-op in this repo.

**Approach:** Add the two missing fields to both mock literals; correct the documented verification command in Story 12.1's file from `npx tsc --noEmit` to `npx tsc -b`; record the incident in the story's File List and Change Log; defer the systemic fix (a shared `DeviceResponse` fixture builder, and revisiting AD-22a's "no pre-merge gate" tradeoff) to `deferred-work.md` since it's a second occurrence of an already-documented architectural risk.

## Suggested Review Order

**The actual break — missing required fields**

- Entry point: the field gap `tsc -b` caught, now closed.
  [`FlatStructureEditor.test.tsx:125`](../../client/src/features/flat-structure/components/FlatStructureEditor.test.tsx#L125)

- Identical fix, second mock literal in the same file.
  [`FlatStructureEditor.test.tsx:345`](../../client/src/features/flat-structure/components/FlatStructureEditor.test.tsx#L345)

**Verification-command correction**

- Task 6.6 corrected from the no-op `npx tsc --noEmit` to the real `npx tsc -b`.
  [`12-1-...md:76`](12-1-device-existence-window-estimated-consumption-gating.md#L76)

- Dev Agent Record completion note corrected to match; original false-negative claim preserved and annotated, not silently overwritten.
  [`12-1-...md:206`](12-1-device-existence-window-estimated-consumption-gating.md#L206)

**Incident record**

- File List updated to include the fixed test file.
  [`12-1-...md:230`](12-1-device-existence-window-estimated-consumption-gating.md#L230)

- New Change Log entry recording the deploy failure and fix, full counts re-verified.
  [`12-1-...md:235`](12-1-device-existence-window-estimated-consumption-gating.md#L235)

- Deferred: no shared `DeviceResponse` fixture builder exists; this is the second occurrence of AD-22a's documented "no pre-merge type-check gate" risk (first was Story 9.10).
  [`deferred-work.md:5`](deferred-work.md#L5)
