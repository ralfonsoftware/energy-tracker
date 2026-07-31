---
baseline_commit: c46629335b08486f0c1943c41c38efe9980b2096
---

# Story 11.8: Room-List Per-Row Save-State Consistency

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want saving one room's Power Points to only show a saving/disabled state on that room, not every room in the list,
so that the room list's save feedback tells me what's actually happening.

## Acceptance Criteria

1. **Given** the single shared `isPending` flag (from `useUpdateFlatStructure(flatId)`) currently drives all per-room and page-level Save button states together, **when** implemented, **then** each in-flight save (whether triggered by a single room's inline Save icon or the page-level batch Save button) tracks which specific room key(s) it is saving, and only those rooms' Save buttons show the disabled/spinner state — rooms not involved in the in-flight save remain fully interactive.
2. **Given** a blocked (disabled) per-room Save icon today gives no visible reason why it's blocked (just dims via `disabled:opacity-40`), unlike `RoomEditor.tsx`'s equivalent full-screen editor which always shows explicit blank-name/plug-ID-conflict text above its Save button, **when** implemented, **then** a blocked per-room Save button in the room list shows the same specific blocking reason (blank name or plug-ID conflict) inline near that row, matching `RoomEditor.tsx`'s established pattern rather than a global banner covering all rooms.
3. **Given** this changes real interaction behavior (not just presentation, unlike Story 9.3), **when** implemented, **then** existing `FlatStructureEditor.test.tsx` coverage of `isRoomDirty`/save/dirty-state logic is extended (not replaced) with cases for: saving Room A does not disable Room B's Save button, and Room B's dirty state is preserved and remains savable while Room A's save is in flight.

## Tasks / Subtasks

- [x] Task 1: Introduce per-room in-flight save tracking (AC: #1)
  - [x] 1.1 In `FlatStructureEditor.tsx`, add `const [savingRoomKeys, setSavingRoomKeys] = useState<Set<string>>(new Set())` alongside the existing `useState` declarations (near line 45-51).
  - [x] 1.2 In `handleSaveRoom(room)` (line 103): immediately before `mutate(...)`, call `setSavingRoomKeys(new Set([room.key]))`. In both the `onSuccess` and `onError` callbacks passed to that `mutate(...)` call, add `setSavingRoomKeys(new Set())` (clear it) alongside the existing state updates.
  - [x] 1.3 In `handleSave()` (the page-level batch save, line 165): immediately before `mutate(...)`, call `setSavingRoomKeys(new Set(draftRooms.map(r => r.key)))` — the batch save legitimately touches every room, so every room's key is marked in-flight (this intentionally preserves today's "all rooms show saving" behavior for the *batch* path — only the *single-room* path is being narrowed). In both `onSuccess` and `onError`, add `setSavingRoomKeys(new Set())`.
  - [x] 1.4 Do **not** add save-key tracking to `handleDeleteRoom` (line 134) — out of scope, see Dev Notes "Explicit scope boundary."
- [x] Task 2: Rewire room-list Save button states off the shared `isPending` (AC: #1)
  - [x] 2.1 In the room list `.map(room => ...)` block (line 338 area), replace the local `isPending`-based label computation `` `${isPending ? t('editor.saving') : t('editor.save')}: ${room.name.trim()}` `` (line 341) with a per-room `const isSaving = savingRoomKeys.has(room.key)` and use `isSaving` in its place.
  - [x] 2.2 Replace the Save icon button's `disabled={!isDirty || isPending || isSaveBlocked}` (line 384) with `disabled={!isDirty || isSaving || isSaveBlocked}`.
  - [x] 2.3 Replace the spinner ternary `{isPending ? (<div className="...animate-spin".../>) : (<Check .../>)}` (lines 390-397) with `{isSaving ? (...) : (...)}` — same markup, just swap the condition.
  - [x] 2.4 Leave the delete-related buttons (`disabled={isPending}` at line 402 — the trash icon that arms delete-confirmation — and at lines 365/373 — the Cancel/Delete buttons shown once armed) and the "Add Room" button (`disabled={isPending}` at line 438) **unchanged** — they continue reading the shared hook-level `isPending`. See Dev Notes for why this is correct, not an oversight.
- [x] Task 3: Rewire the page-level batch Save button (AC: #1)
  - [x] 3.1 Replace `disabled={hasPlugIdConflict || hasEmptyName || hasNoRooms || isPending}` (line 296) with `disabled={hasPlugIdConflict || hasEmptyName || hasNoRooms || savingRoomKeys.size > 0}`.
  - [x] 3.2 Replace the label ternary `{isPending ? t('editor.saving') : t('editor.save')}` (line 300) with `{savingRoomKeys.size > 0 ? t('editor.saving') : t('editor.save')}`.
  - [x] 3.3 Also update the top-of-function guard in `handleSave()` (line 166): `if (hasPlugIdConflict || hasEmptyName || hasNoRooms || isPending) return` → replace `isPending` with `savingRoomKeys.size > 0`.
- [x] Task 4: Rewire the room-detail view's Save button for the same per-room scoping (AC: #1)
  - [x] 4.1 In the `view.type === 'room'` branch (line 257-276), replace `isPending={isPending}` passed to `<RoomEditor>` (line 269) with `isPending={savingRoomKeys.has(room.key)}`. See Dev Notes for why this view needs the same fix — a user can navigate into a room's detail view while an unrelated room's save (or the batch save) is in flight, and that room's own Save button must not falsely appear to be saving/blocked in that case.
  - [x] 4.2 `RoomEditor.tsx` itself needs **no changes** — it already receives `isPending` as a prop and treats it as "is *this* room saving"; only the value passed in from `FlatStructureEditor` changes.
- [x] Task 5: Add inline blocking-reason text to each room-list row (AC: #2)
  - [x] 5.1 Inside the room `.map(room => ...)` block, alongside the existing `const isSaveBlocked = hasBlankNameInRoom(room) || hasPlugIdConflictForRoomSave(room, lastSaved)` (line 340), add `const blockedByBlankName = hasBlankNameInRoom(room)` (mirrors `RoomEditor.tsx` line 32 exactly — both helpers are already imported in this file).
  - [x] 5.2 Add a new conditional block inside the room's `<div className="flex flex-col gap-2">` wrapper (after the `{confirmDeleteRoomKey !== room.key && (<button>...powerPointsSummary...</button>)}` block, before that `</div>` closes — i.e. as a new sibling within the same wrapper, not inside the `<li>` root next to the `deletePrompt` span):
    ```tsx
    {isSaveBlocked && confirmDeleteRoomKey !== room.key && (
      <p role="alert" className="text-xs text-accent-error">
        {blockedByBlankName ? t('editor.blankNameError') : t('editor.plugIdConflict')}
      </p>
    )}
    ```
  - [x] 5.3 Do **not** remove the existing page-level banners (`hasPlugIdConflict`/`hasEmptyName` at lines 319-333) — those still explain why the *page-level batch* Save button is blocked and remain correct/needed. This task only *adds* the per-row reason; it does not replace the page-level one.
- [x] Task 6: Update the two pre-existing tests that assert the old (buggy) global-disable behavior (AC: #1, #3)
  - [x] 6.1 `FlatStructureEditor_AnySavePending_DisablesAllRoomSaveButtonsDeleteAndSpeichern` (`FlatStructureEditor.test.tsx` line 704) currently mocks `useUpdateFlatStructure` to return `isPending: true` on initial render (no `mutate` call actually happens) and asserts **both** room-level Save buttons show `editor.saving:` labels and are disabled **and** the page-level Save button/Delete/Add-Room buttons are disabled. After this story, `savingRoomKeys` is empty on initial render regardless of the mocked `isPending`, so the two room-level and one page-level assertions in this test will fail — this is expected, not a regression, since this test literally encodes the bug being fixed. Split it: keep a slimmed test (rename to e.g. `FlatStructureEditor_HookIsPendingTrueOnMount_DisablesDeleteAndAddRoomButtonsOnly`) asserting only the Delete (`room.delete` ×2) and `editor.addRoom` buttons are disabled under the mocked `isPending: true`; remove the room-Save-button and page-Save-button assertions from it.
  - [x] 6.2 `FlatStructureEditor_AnySavePendingWithRoomDetailViewActive_DisablesInRoomSaveButton` (line 936) has the same issue — mocking `isPending: true` statically no longer drives the room-detail Save button (which now reads `savingRoomKeys`). Delete this test; its replacement is covered by the new tests in 6.4 below (which exercise the real click path instead of a static mock).
  - [x] 6.3 Add new test: `FlatStructureEditor_SavingOneRoom_OtherDirtyRoomSaveButtonRemainsEnabledAndSavable` — render with `seededResponse()` (Office + Garage). Rename Office (making it dirty) and click its Save icon (`editor.save: Office Renamed`) — **do not** configure `mockMutate` to invoke callbacks, so the mutation stays "in flight" (mirrors the existing pattern at e.g. line 564-593 where `mockMutate` is a bare `vi.fn()`). Assert: Office's button now reads `editor.saving: Office Renamed` and is disabled. Then rename Garage (making it dirty too) and assert Garage's Save button (`editor.save: Garage Renamed`) is present and **enabled** (not disabled, not relabeled to `editor.saving:`) — proving Garage stayed fully interactive while Office's save was in flight. Also assert `mockMutate` was called exactly once (only Office's save fired; Garage's edit was local state only).
  - [x] 6.4 Add new test: `FlatStructureEditor_SavingOneRoomThenViewingUnrelatedRoomDetail_UnrelatedRoomSaveButtonNotDisabled` — same setup, click Office's Save icon (mutate left pending), then click Garage's `room.powerPointsSummary` row button to enter its detail view, and assert the sticky Save button there (`editor.save`, since Garage isn't dirty yet — or rename a power point in Garage first to make it dirty and assert `editor.save` is enabled) is not showing the `editor.saving` label and is not disabled purely because Office's save is in flight.
  - [x] 6.5 Add new test: `FlatStructureEditor_PageLevelBatchSaveInFlight_AllRoomSaveButtonsShowSavingAndDisabled` — render, click the page-level `editor.save` button (mutate left pending, no callback), and assert both room rows now show `editor.saving: Office` / `editor.saving: Garage` and are disabled, and the page-level button itself reads `editor.saving` and is disabled — this documents that the *batch* path intentionally still marks every room, distinguishing it from the *single-room* path fixed by 6.3/6.4.
  - [x] 6.6 Add new test for AC #2: `FlatStructureEditor_RoomBlockedByPlugIdConflict_ShowsInlineConflictReasonNearThatRow` (and a blank-name equivalent) — using the existing two-power-points-same-plug-id fixture (see line 235-259 for the shape), assert `screen.getAllByText('editor.plugIdConflict')` (or a scoped `within(row)` query) shows the reason inline near the affected room row, not only once as the page-level banner. Reuse the existing blank-name fixture pattern from the `InRoomSave...` tests (line 872-882) adapted to the list view (no navigation into room detail) to prove the message appears in the list row itself.
  - [x] 6.7 Run `npm test -- --run` (Vitest, from `client/`) and confirm the full suite passes with zero regressions to any test in `FlatStructureEditor.test.tsx` or `RoomEditor`'s own tests (unmodified — `RoomEditor.tsx` itself is not touched).
  - [x] 6.8 Run `npm run lint` and `npx tsc --noEmit` (both from `client/`) and confirm clean.

## Dev Notes

### Explicit scope boundary — read before touching anything beyond Save buttons

This story is about the **Save affordance** specifically (per its own title and AC #1's literal "Save buttons" wording), not every `isPending`-gated control in this file:

- **Delete-related buttons** (`disabled={isPending}` at `FlatStructureEditor.tsx:365,373` — Cancel/Delete once armed — and at line 402 — the trash icon that arms delete-confirmation) and the **Add Room button** (`disabled={isPending}` at line 438) stay wired to the raw hook-level `isPending` from `useUpdateFlatStructure`. This is correct, not an oversight: `isPending` still accurately reflects "some mutation via this hook is currently in flight" for *any* reason (room save, batch save, or a delete), because deletes and saves all go through the same single `mutate` function from the same hook instance. Keeping Delete/Add-Room gated on the raw flag continues to prevent a second mutation (delete or otherwise) from firing concurrently with an in-flight one — exactly as today. Narrowing these too would be scope creep beyond what AC #1 asks for.
- **`RoomEditor.tsx` itself is not modified.** It already accepts `isPending` as a prop meaning "is *this* room currently saving" — only the *value* `FlatStructureEditor` passes in changes (from the shared flag to `savingRoomKeys.has(room.key)`), per Task 4.

### Why the room-detail view also needs the fix (Task 4), not just the list

The epic's Note frames this as a room-*list* bug, but the same shared-`isPending` flag is also passed into `RoomEditor` for the full-screen detail view (`FlatStructureEditor.tsx:269`). A user can click a room's Save icon from the list (kicking off a per-room save, now correctly scoped to just that room), then — while it's still in flight — click a *different* room's `room.powerPointsSummary` button to enter its detail view (that navigation is local `view` state, not gated by `isPending` at all). Under the old code, that other room's detail-view Save button would show `saving`/disabled too, because it read the same shared flag — the identical bug this story fixes for the list, just reachable through a different path. Fixing only the list rows and leaving `RoomEditor`'s prop wired to the raw flag would leave this exact class of bug half-fixed. `savingRoomKeys.has(room.key)` is correct here for the same reason it's correct in the list: whether the current room got there via an individual save (its own key was set) or a batch save (every key was set), the value is accurate; if it's an *unrelated* room's individual save, this room's key was never added, so it correctly stays interactive.

### Why the batch-save path intentionally still marks every room (Task 1.3 / Task 6.5)

The page-level batch Save button sends **all** rooms in one request (`toUpdateRequest(draftRooms, ...)` — see `handleSave`, line 169). Every room's data is genuinely part of that in-flight write, so showing every room as "saving" during a batch save is correct, not the bug. Only the *single-room* path (`handleSaveRoom`, which sends one room's fresh data plus every other room's last-known-saved snapshot via `toWireRequest`/`withRoomUpdated`) was incorrectly appearing to affect every row. `savingRoomKeys = new Set(draftRooms.map(r => r.key))` for the batch path vs. `new Set([room.key])` for the single-room path is what encodes this distinction — do not simplify this into "always just the clicked room" or the batch path's UI will misrepresent what's actually being written.

### Concurrency note — no new race is introduced

Because the page-level Save button is disabled whenever `savingRoomKeys.size > 0` (Task 3.1) and each room's Save icon is disabled whenever *its own* key is in `savingRoomKeys` (Task 2.2) — and a batch save populates *every* key — the UI makes it structurally impossible to fire a second `mutate()` call while one is already in flight, regardless of which path triggered it first. This preserves the existing safety property (only one `mutate()` call in flight at a time through this single `useMutation` instance) while narrowing which rows visually reflect it.

### AC #2 — reuse `RoomEditor.tsx`'s existing pattern verbatim

`RoomEditor.tsx:32` already computes `const blockedByBlankName = hasBlankNameInRoom(room)` and, in its `StickyActionBar` (lines 94-104), renders `{blockedByBlankName ? t('editor.blankNameError') : t('editor.plugIdConflict')}` as a `role="alert"` paragraph next to its Save button. Both translation keys (`editor.blankNameError`, `editor.plugIdConflict`) and both helper functions (`hasBlankNameInRoom`, `hasPlugIdConflictForRoomSave`) are already imported in `FlatStructureEditor.tsx` (lines 17, 20) — no new imports, no new i18n keys, no new helper functions needed. This is a direct copy of an established, already-tested pattern into a new location, not new design.

### Previous Story Intelligence (Story 11.7 — Keyboard-Accessible Custom Dropdowns)

- 11.7 was a 5-file frontend-only change (1 new hook + 4 retrofits); this story is narrower (1 file with meaningful behavior change: `FlatStructureEditor.tsx`) but touches the same file that has the largest, most detail-sensitive test file in the flat-structure slice (`FlatStructureEditor.test.tsx`, ~970 lines, ~40 tests) — apply 11.7's discipline of running the *full* suite (`npm test -- --run`) before calling this done, not just the modified file's tests, since `RoomEditor.tsx` consumes the same `isPending`-derived prop this story changes the source of.
- 11.7 also established: when a pre-existing test's assertions directly contradict the story's own fix (there, none arose; here, two do — see Task 6.1/6.2), update them explicitly and call out *why* in Completion Notes rather than silently deleting/rewriting them — this preserves the audit trail for why coverage changed shape.
- This story is the first in Epic 11 to touch `FlatStructureEditor.tsx`/`draftModel.ts`/`RoomEditor.tsx` — no prior Epic 11 story modified this slice; the immediately-preceding UI precedent for "real interaction behavior change, not just presentation" is Story 9.3 (explicitly called out in this story's own epic AC #3 as the contrast — 9.3 was presentation-only, this one is not).

### Git Intelligence (recent commits)

- `c466293` (Story 11.7), `2f9012f` (Story 11.6), `99b64b9` (Story 11.5), `df5a834` (Story 11.4) — all recent Epic 11 commits are single narrow frontend/backend changes with matching test-file extensions and a full relevant-suite run before completion. This story follows the same shape: `npm test -- --run` (Vitest) from `client/`, no backend changes at all (purely `client/src/features/flat-structure/`).

### Deferred-Work Cross-Check

- Searched `_bmad-output/implementation-artifacts/deferred-work.md` for any entry tagged `blocks: Story 11.8` — **none found.** No pre-existing deferred item gates or blocks this story.

### Project Structure Notes

- Single feature slice touched: `client/src/features/flat-structure/`. Only `FlatStructureEditor.tsx` (component) and `FlatStructureEditor.test.tsx` (tests) are modified. `RoomEditor.tsx`, `draftModel.ts`, `useUpdateFlatStructure.ts`, and their respective test files are **not modified** — all reused as-is per VSA slice isolation convention (no cross-slice imports introduced; this stays entirely within one existing component's local state).
- No API/schema/backend changes — this is a pure frontend local-state-and-rendering fix; the server-side `PATCH`/`PUT` contract, `useUpdateFlatStructure` hook, and `draftModel.ts` pure functions are untouched.
- No new i18n keys — reuses `editor.blankNameError` / `editor.plugIdConflict`, already present in the `flat-structure` namespace and already used at the page-level banner and in `RoomEditor.tsx`.
- No new files — this story only edits two existing files.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md#Story 11.8] — epic-level AC and rationale, including the exact line numbers the epic scoping already identified (384, 390, 341)
- [Source: client/src/features/flat-structure/components/FlatStructureEditor.tsx] — the file being modified; current shared-`isPending` usage at lines 43, 166, 269, 296, 300, 341, 365, 373, 384, 390-397, 402, 438
- [Source: client/src/features/flat-structure/components/RoomEditor.tsx] — the established blocked-reason-inline pattern (`blockedByBlankName`, lines 32, 94-104) this story's AC #2 replicates into the list view
- [Source: client/src/features/flat-structure/components/draftModel.ts] — `hasBlankNameInRoom`, `hasPlugIdConflictForRoomSave`, `isRoomDirty` already exist and are reused unchanged; no new draft-model functions needed
- [Source: client/src/features/flat-structure/hooks/useUpdateFlatStructure.ts] — confirms a single shared `useMutation` instance backs every `mutate()` call (room save, batch save, delete) — the reason hook-level `isPending` remains valid for Delete/Add-Room gating and why no concurrent-mutation race is newly introduced
- [Source: client/src/features/flat-structure/components/FlatStructureEditor.test.tsx] — existing test conventions (`mockMutate` left un-configured to simulate "in flight", `mockUseUpdateFlatStructure.mockReturnValue({ isPending: true })` to simulate a static pending state) — Task 6 explains which existing tests must change and why
- [Source: _bmad-output/implementation-artifacts/11-7-keyboard-accessible-custom-dropdowns.md] — previous story in this epic; source of the "update contradicted pre-existing tests explicitly, with rationale in Completion Notes" discipline
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — searched for `blocks: Story 11.8`, none found

## Dev Agent Record

### Agent Model Used

claude-sonnet-5

### Debug Log References

None — no blocking issues encountered; implementation proceeded per Dev Notes as scoped.

### Completion Notes List

- Added `savingRoomKeys` state (`Set<string>`) to `FlatStructureEditor.tsx`; `handleSaveRoom` now sets it to `{room.key}` before `mutate` and clears it in both `onSuccess`/`onError`; `handleSave` (batch) sets it to all `draftRooms` keys before `mutate` and clears it the same way. Delete-related buttons and the Add Room button remain wired to the raw hook-level `isPending` per the story's explicit scope boundary — unchanged, not an oversight.
- Room-list rows, the page-level batch Save button, and the room-detail (`RoomEditor`) Save button now all derive their saving/disabled state from `savingRoomKeys` instead of the shared `isPending`, per Tasks 2-4.
- Added inline per-row blocking-reason text (`blockedByBlankName` + conditional `<p role="alert">`) reusing `RoomEditor.tsx`'s existing pattern verbatim — no new i18n keys or helper functions.
- Test file updates:
  - Split `FlatStructureEditor_AnySavePending_DisablesAllRoomSaveButtonsDeleteAndSpeichern` into `FlatStructureEditor_HookIsPendingTrueOnMount_DisablesDeleteAndAddRoomButtonsOnly`, dropping the room-Save-button and page-Save-button assertions (those directly encoded the bug this story fixes — `savingRoomKeys` is empty on mount regardless of a mocked `isPending: true`).
  - Deleted `FlatStructureEditor_AnySavePendingWithRoomDetailViewActive_DisablesInRoomSaveButton` (same static-mock issue); replaced in spirit by the new real-click-path tests below.
  - Added `FlatStructureEditor_SavingOneRoom_OtherDirtyRoomSaveButtonRemainsEnabledAndSavable`, `FlatStructureEditor_SavingOneRoomThenViewingUnrelatedRoomDetail_UnrelatedRoomSaveButtonNotDisabled`, `FlatStructureEditor_PageLevelBatchSaveInFlight_AllRoomSaveButtonsShowSavingAndDisabled`, `FlatStructureEditor_RoomBlockedByPlugIdConflict_ShowsInlineConflictReasonNearThatRow`, and `FlatStructureEditor_RoomBlockedByBlankName_ShowsInlineBlankNameReasonNearThatRow`.
  - Two further pre-existing tests (`FlatStructureEditor_TwoPowerPointsSameNonEmptyPlugId_SaveDisabledWithConflictText` and `FlatStructureEditor_ClearingOnePlugId_ReEnablesSave`, not called out in the story's own Task 6 list) broke as a direct, correct consequence of Task 5's new inline text: `hasPlugIdConflictForRoomSave` compares a room's own plug IDs against *other rooms' `lastSaved`* (server-confirmed) state, not their current draft — so after clearing one room's plug ID in-memory, the other room's row can still legitimately show an inline conflict reason (its own individual save would still collide with server truth). This is pre-existing, unmodified blocking logic — Task 5 only made a previously-invisible (but already-disabling) state visible as text. Updated both tests' assertions to match the new, correct visible state, with an inline comment explaining why.
- Full Vitest suite: 465/465 passing (69 files). `npm run lint`: clean (pre-existing unrelated `router.tsx` fast-refresh warnings only). `npx tsc --noEmit`: clean.

### File List

- `client/src/features/flat-structure/components/FlatStructureEditor.tsx` (modified)
- `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx` (modified)

## Change Log

| Date | Change |
|---|---|
| 2026-07-31 | Implemented Story 11.8: added per-room `savingRoomKeys` in-flight save tracking so an individual room's save only disables/spinners that room's Save button (list row, page-level batch button, and room-detail view all rewired off the shared `isPending`); added inline blank-name/plug-ID-conflict reason text to each room-list row matching `RoomEditor.tsx`'s established pattern; updated/added `FlatStructureEditor.test.tsx` coverage (2 pre-existing tests updated to reflect the fixed behavior, 1 split, 1 deleted and replaced, 5 new tests added, plus 2 further pre-existing tests fixed as a correct side effect of the new inline text); full suite (465 tests), lint, and `tsc --noEmit` all pass. |

### Review Findings

- [x] [Review][Patch] `savingRoomKeys` uses whole-set replace/clear instead of per-key add/remove, breaking concurrent per-room saves [client/src/features/flat-structure/components/FlatStructureEditor.tsx:114,123,133] — AC #3 requires Room B's Save button to stay enabled (and savable) while Room A's save is in flight, but `handleSaveRoom` replaces the whole `savingRoomKeys` set on start (`new Set([room.key])`, line 114) and clears it entirely on resolution (`new Set()`, lines 123/133) instead of adding/removing just that room's key. If Room B is actually clicked while Room A is still saving, Room A's tracking is silently dropped (its spinner/disabled state disappears mid-request), and whichever save resolves first wipes out the other's in-flight tracking too — enabling a duplicate `mutate()` call and a race on the shared `currentRowVersionRef`. This directly contradicts the story's own Dev Notes "Concurrency note" claim that a second `mutate()` call is "structurally impossible" while one is in flight. No test exercises this: `FlatStructureEditor_SavingOneRoom_OtherDirtyRoomSaveButtonRemainsEnabledAndSavable` asserts Room B's button stays enabled but never clicks it. Fix: use functional updates — `setSavingRoomKeys(prev => new Set(prev).add(room.key))` on start and `setSavingRoomKeys(prev => { const next = new Set(prev); next.delete(room.key); return next })` on resolve, for the single-room path (batch path can keep whole-set replace/clear, since it's disabled whenever `savingRoomKeys.size > 0`). (source: blind+edge)
- [x] [Review][Patch] Row-scoped inline-message tests don't verify row-level placement [client/src/features/flat-structure/components/FlatStructureEditor.test.tsx:987,1012] — `FlatStructureEditor_RoomBlockedByPlugIdConflict_ShowsInlineConflictReasonNearThatRow` and the blank-name equivalent assert only a document-wide `getAllByText(...).toHaveLength(n)` count, never scoping to the specific row with `within(row)`. The test names promise the message renders "near that row," but the assertions would pass even if the messages rendered on the wrong rows. (source: blind)
- [x] [Review][Patch] Weakened assertion inconsistent with this diff's own new-test style [client/src/features/flat-structure/components/FlatStructureEditor.test.tsx:257] — `expect(screen.getByText('editor.plugIdConflict')).toBeInTheDocument()` was loosened to `expect(screen.getAllByText('editor.plugIdConflict').length).toBeGreaterThan(0)`, which passes regardless of how many conflict messages render. The new tests added later in this same diff (e.g. line 1009, `toHaveLength(3)`) use exact counts instead — this assertion should be tightened to match the actual expected count for consistency. (source: blind)
- [x] [Review][Defer] Blank-name error hides a simultaneous plug-ID conflict [client/src/features/flat-structure/components/FlatStructureEditor.tsx:435-439] — deferred, pre-existing (the `blockedByBlankName ? blankNameError : plugIdConflict` ternary is a verbatim copy of `RoomEditor.tsx`'s established pattern per spec Task 5.2's explicit instruction; a room with both problems only ever reports the blank-name error)
- [x] [Review][Defer] `role="alert"` used for persistent, potentially multi-row-simultaneous blocking text [client/src/features/flat-structure/components/FlatStructureEditor.tsx:436] — deferred, pre-existing (pattern inherited verbatim from `RoomEditor.tsx`; the list view can now surface multiple simultaneous `role="alert"` elements across rows, which is new to this diff and worth a future accessibility pass, but the pattern itself was explicitly spec-mandated as a verbatim reuse)
- [x] [Review][Defer] No `aria-describedby` linking a blocked room's name/plug-ID input to its inline error text [client/src/features/flat-structure/components/FlatStructureEditor.tsx:435-439] — deferred, pre-existing (same gap already exists in `RoomEditor.tsx`'s source pattern, not introduced by this diff)
- [x] [Review][Defer] Plug-ID conflict compares against other rooms' last-saved (not draft) state, producing a confusing "still blocked after you fixed it" UX case [client/src/features/flat-structure/components/draftModel.ts (`hasPlugIdConflictForRoomSave`, unmodified)] — deferred, pre-existing (traced and confirmed by the Acceptance Auditor as existing, unmodified logic; this diff only makes the pre-existing staleness visible via the new inline text, documented in a test comment at `FlatStructureEditor.test.tsx:288-290` rather than fixed)


