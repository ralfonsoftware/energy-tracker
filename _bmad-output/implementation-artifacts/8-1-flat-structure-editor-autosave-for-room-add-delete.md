---
baseline_commit: cbd304602e857d9a2f8ae52b2e6d7a639600654b
---

# Story 8.1: Flat Structure Editor — Autosave for Room Add/Delete

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want each room in my Flat Structure to have its own dedicated Save button that's ready exactly when there's something to save, and deleting a room to persist immediately,
so that I don't need to find a distant, page-level Save button to keep a change I already made explicit.

## Acceptance Criteria

1. **Given** a room row in the Flat Structure editor's room list view (`FlatStructureEditor.tsx`), **when** the row is rendered, **then** it has its own dedicated Save button (in addition to the existing rename input and delete icon) — not a single page-header "Speichern" shared across every room.

2. **Given** a newly-added room (created via "+ Raum hinzufügen", never yet persisted), **when** its row renders, **then** its Save button is enabled immediately — no edit is required first, since adding the room is itself the pending change.

3. **Given** an existing room (loaded from the server, or already saved once via its own Save button), **when** its row renders, **then** its Save button is enabled only while the room's current name differs from its last-saved name, and disabled when the name matches — a pure dirty-check against that one room's own baseline, not against any other room's state.

4. **Given** a room's Save button is clicked, **when** the save request is in flight, **then** that button (and, per AC7, every other room's Save button, every delete-confirm row, and the page-header "Speichern" button) is disabled until it resolves.

5. **Given** a room's Save request fails, **when** the failure is returned, **then** that room's name reverts to its last-saved value and an inline error appears (reuse the existing `editor.saveError` pattern) — no silent data loss, no button that looks "saved" but wasn't.

6. **Given** a room's Save request succeeds, **when** the response returns, **then** that room's "last-saved name" baseline updates to the value just saved (so its button becomes disabled again — no longer dirty), and if it was a brand-new room, it is now treated as an existing room for future dirty-checks.

7. **Given** deleting a room (via the existing delete-icon → confirm/cancel row), **when** the user confirms the deletion, **then** it persists immediately, exactly as in the original design — delete is not gated behind a Save button; confirming the deletion is itself the explicit, deliberate action. (FR-44's "no separate manual Save action" applies to delete as literally written; for add/rename it is satisfied by moving Save next to the row rather than removing the click entirely — see Dev Notes.)

8. **Given** renaming a Power Point or editing/adding a Device (Story 8.2's territory) are different interactions than room add/rename/delete, **when** this story is implemented, **then** they are explicitly out of scope and remain on the existing page-level batched draft/Speichern flow — this story only adds per-room Save buttons for room name changes and new rooms, and keeps delete's existing immediate-on-confirm behavior.

9. **Given a gap found during story creation** — the epic's Story 8.1 text (and this story's own earlier draft) described "its own scoped save call, not the batched draft-save," but a scoped/granular per-room backend endpoint does not exist and is **not needed**: `PUT /api/v1/flats/{flatId}/structure` (`UpdateFlatStructureFunction.cs`) is the only structure-mutation endpoint and always replaces the *entire* `rooms` array. The real risk is *what* gets sent in that payload when a single room's Save button is clicked. `draftRooms` (the live editor state) is **not safe to build that payload from directly**: `PowerPointEditor.tsx`'s name/plugId `<input>`s write straight into `draftRooms` on every keystroke with no local commit step (unlike `DeviceEditor.tsx`, which only writes into `draftRooms` when its own in-dialog Save is clicked — see `FlatStructureEditor.tsx:142-168`). Concretely: a user mid-typing a new Power Point's name in Room A (which starts as `''` per `RoomEditor.tsx:15-23`'s `handleAddPowerPoint`), who then navigates back to the list and clicks Room B's Save button (dirty because they renamed it), would — under a naive `draftRooms`-based payload — silently include that blank-named Power Point, which `UpdateFlatStructureValidator.cs`'s `RuleFor(p => p.Name).NotEmpty()` rejects with HTTP 400, and Room B's rename would fail for a reason that has nothing to do with Room B. **When** any per-room Save (or delete) is implemented, **then** its request payload must be built from a separately-tracked "last known saved" wire-shape snapshot (`UpdateFlatStructureRequest['rooms']`, i.e. the `RoomInput[]` shape from `flatStructureApi.ts` — no client `key`s) with only that one room's name updated (or appended, for a new room, or removed, for delete) — never from live `draftRooms` as a whole. See Dev Notes for the exact state/helper design.

10. **Given a gap found during story creation** — `UpdateFlatStructureFunction.cs` fully replaces the structure on every PUT (`db.Rooms.RemoveRange(existingRooms)` then `db.Rooms.AddRange(newRooms)`, lines 86/113–114), so two PUTs in flight at once race: whichever response resolves last silently wins and defines the final DB state. With N independent per-room Save buttons plus delete plus the page-level Speichern, this risk is more likely than in a single-button design. **When** any save (a room's Save, a delete, or the page-level Speichern) is in flight, **then** the app must not allow a second overlapping PUT — all save paths share the *same* `useUpdateFlatStructure` mutation instance and its single `isPending` flag, and every save-triggering control across every row (AC4) is disabled while it is true. Do not implement a separate `isPending`-like flag per row.

11. **Given a gap found during story creation** — because `UpdateFlatStructureFunction.cs` destroys and recreates every `Room`/`PowerPoint`/`Device` row on every PUT, **all** `RoomId`/`PowerPointId`/`DeviceId` values in the flat are regenerated on every save, not just the one room being saved. **When** this story is implemented, **then** this is treated as a known, pre-existing, out-of-scope characteristic of the endpoint (changing it to incremental upsert is a materially larger backend change unrelated to this story's UI-consistency scope). Confirm no client-side breakage results: `toDraftRooms()` (`draftModel.ts`) already mints fresh client-only `key`s for every room/power point/device on every load and never surfaces server IDs to the UI layer, so ID churn on the server is already invisible to the editor today — do not add any code that stores or compares a server `RoomId`/`PowerPointId`/`DeviceId` across saves.

12. **Given a gap found during story creation** — the client already refuses to persist an empty flat (`hasNoRooms` disables the page-level Speichern button and shows `editor.noRoomsError`), even though `UpdateFlatStructureValidator.cs`'s `RuleFor(r => r.Rooms).NotNull()` would technically accept an empty array. **When** deleting a room would bring the room count to zero, **then** the delete PUT is **not** issued (mirroring the existing client-only "at least one room" policy); the room is still removed from `draftRooms` locally so the UI reflects the deletion, and the existing `editor.noRoomsError` inline message is shown. The flat structure is recoverable once the user adds a new room and clicks its Save button.

13. **Given a gap found during story creation** — introducing per-room Save buttons raises the question of what the page-level "Speichern" button is still for. **When** this story is implemented, **then** the page-level Speichern button is **not removed**: it remains the save path for Power Point/Device edits (Story 8.2's unmodified territory) and continues to submit the full current `draftRooms`. It is unaffected by per-room saves other than sharing the same `isPending` gate (AC10) — after a room's dedicated Save succeeds, that room simply no longer contributes to `hasEmptyName`/dirty state, so clicking Speichern afterward (for an unrelated Power Point edit elsewhere) re-sends that room's already-correct name with no conflict.

## Tasks / Subtasks

- [x] Task 1: Track a per-room "last saved name" baseline and a whole-flat "last saved" wire-shape snapshot (AC: 2, 3, 6, 9)
  - [x] In `draftModel.ts`, add `originalName?: string` to `DraftRoom`. Absent/`undefined` means "new, never saved" (AC2's always-enabled case). Present means "existing" and drives the AC3 dirty-check (`room.name.trim() !== room.originalName`).
  - [x] `toDraftRooms(rooms: RoomResponse[])`: set `originalName: room.name` for every room (all are existing, loaded from the server).
  - [x] `createDefaultDraftRooms(t)`: leave `originalName` unset for all 5 default-template rooms — nothing has been persisted yet (matches the existing `hasDefaultTemplate` semantics), so they behave as "new" rooms per AC2 until each is individually saved.
  - [x] `handleAddRoom` (`FlatStructureEditor.tsx:61-67`): the new room object omits `originalName` (new room, AC2).
  - [x] In `FlatStructureEditor.tsx`, add `const [lastSaved, setLastSaved] = useState<UpdateFlatStructureRequest>({ rooms: [] })` — this is the whole-flat wire-shape baseline used to build safe per-room payloads (AC9), independent of `draftRooms`.
  - [x] In the existing data-load `useEffect` (`FlatStructureEditor.tsx:41-54`), after computing `draftRooms`, also set `lastSaved`: seeded case → `setLastSaved(toUpdateRequest(toDraftRooms(data.rooms)))`; default-template case → `setLastSaved({ rooms: [] })` (nothing persisted yet).

- [x] Task 2: Add positional room-list helpers to `draftModel.ts` (AC: 6, 9, 12)
  - [x] `export function withRoomAppended(base: UpdateFlatStructureRequest, name: string): UpdateFlatStructureRequest` — appends one `RoomInput` (`{ name, sortOrder: base.rooms.length, powerPoints: [] }`). (New-room Save.)
  - [x] `export function withRoomNameUpdatedAt(base: UpdateFlatStructureRequest, index: number, name: string): UpdateFlatStructureRequest` — returns a new request with the room at `index`'s `name` replaced; all other rooms and their power points/devices untouched. (Existing-room rename Save.)
  - [x] `export function withRoomRemovedAt(base: UpdateFlatStructureRequest, index: number): UpdateFlatStructureRequest` — removes the room at `index`, recomputing remaining `sortOrder`s from array position. (Delete — unchanged from the prior design.)
  - [x] All three are pure functions with no side effects, matching this file's existing style (`toDraftRooms`, `toUpdateRequest`, `findPlugIdConflict`, `hasBlankName`).
  - [x] The `index` passed to `withRoomNameUpdatedAt`/`withRoomRemovedAt` must be the room's position in `draftRooms` **at the moment its Save/delete is clicked** (`draftRooms.findIndex(r => r.key === roomKey)`), not a lookup by name — room names are not guaranteed unique. Per AC10, `lastSaved.rooms` and `draftRooms` stay positionally aligned only because saves are serialized through one shared `isPending`.

- [x] Task 3: Add a per-room Save button to the room row (AC: 1, 2, 3, 4, 5, 6, 9, 10)
  - [x] In `FlatStructureEditor.tsx`'s room list rendering (`FlatStructureEditor.tsx:246-305`), add a Save button per row, placed next to the existing rename input (before or alongside the existing "power points summary" / delete-icon controls — do not remove or relocate those).
  - [x] Compute per-row `isDirty = room.originalName === undefined || room.name.trim() !== room.originalName` — covers both AC2 (new room, `originalName` undefined → always dirty/enabled) and AC3 (existing room, dirty only when changed).
  - [x] Button `disabled={!isDirty || isPending || room.name.trim() === ''}` — reuse the existing blank-name guard philosophy (`hasBlankName`) at the row level so a room can't be saved with an empty name; do not silently trim-and-save an empty string.
  - [x] `onClick` handler: find `index = draftRooms.findIndex(r => r.key === room.key)`; build the payload via `room.originalName === undefined ? withRoomAppended(lastSaved, room.name.trim()) : withRoomNameUpdatedAt(lastSaved, index, room.name.trim())`; call the shared `useUpdateFlatStructure` mutation.
  - [x] `onSuccess`: `setLastSaved` to the payload just sent; update this room's `originalName` in `draftRooms` to the value just saved (`setDraftRooms(prev => prev.map(r => r.key === room.key ? { ...r, originalName: r.name.trim() } : r))`); show the existing `editor.saveSuccess` confirmation.
  - [x] `onError`: revert this room's `name` back to `room.originalName` (or, if it was a new room being saved for the first time and it fails, leave it in place — a new room has no "reverted" name to go back to, it just stays dirty/editable so the user can retry); show `editor.saveError`.

- [x] Task 4: Wire `handleDeleteRoom` to autosave, including the zero-rooms guard (AC: 7, 9, 10, 11, 12) — unchanged from the prior design
  - [x] Before mutating any state, compute `const index = draftRooms.findIndex(r => r.key === roomKey)`.
  - [x] Apply the existing optimistic local removal (`setDraftRooms(prev => prev.filter(room => room.key !== roomKey))`, existing `setConfirmDeleteRoomKey(null)`).
  - [x] If `draftRooms.length - 1 === 0` (deleting the last room): do **not** call the mutation; rely on the existing `hasNoRooms`-driven error path so `editor.noRoomsError` renders.
  - [x] Otherwise: call the shared mutation with `withRoomRemovedAt(lastSaved, index)`.
  - [x] `onSuccess`: `setLastSaved` to the payload sent; show `editor.saveSuccess`.
  - [x] `onError`: revert `draftRooms` by re-inserting the removed room at its original `index`; show `editor.saveError`.

- [x] Task 5: Disable all save-triggering controls while any save is pending (AC: 4, 10, 13)
  - [x] Every room row's new Save button (Task 3), plus its delete icon and confirm/cancel pair (`FlatStructureEditor.tsx:263-297`): add `disabled={isPending}` (combined with each control's own existing enablement condition, e.g. Task 3's `isDirty` check).
  - [x] "+ Raum hinzufügen" button (`FlatStructureEditor.tsx:308-319`): add `disabled={isPending}` — this still only creates a new local draft row (Task 1), it does not itself trigger a save; disabling it while a save is pending simply avoids the user queuing up multiple new rooms mid-save, which is a minor UX nicety, not a correctness requirement.
  - [x] The page-header "Speichern" button already has `disabled={hasPlugIdConflict || hasEmptyName || hasNoRooms || isPending}` (`FlatStructureEditor.tsx:204`) — no change needed, it already respects the shared `isPending`.
  - [x] Do **not** introduce a second `isPending`-like flag anywhere — every save path (per-room Save, delete, page-level Speichern) uses the same `useUpdateFlatStructure(flatId)` hook instance already declared at the top of the component.

- [x] Task 6: Tests (AC: all)
  - [x] Extend `FlatStructureEditor.test.tsx` following its existing mock pattern (`mockUseFlatStructure`, `mockUseUpdateFlatStructure`, `mockMutate`).
  - [x] `FlatStructureEditor_NewRoomAdded_SaveButtonEnabledImmediately` — click "+ Raum hinzufügen", assert the new row's Save button is enabled with no edits made (AC2).
  - [x] `FlatStructureEditor_ExistingRoomNameUnchanged_SaveButtonDisabled` — seed a room, assert its Save button is disabled on initial render (AC3).
  - [x] `FlatStructureEditor_ExistingRoomRenamed_SaveButtonBecomesEnabled` — seed a room, type a new name, assert its Save button becomes enabled (AC3).
  - [x] `FlatStructureEditor_ExistingRoomRenamedThenRevertedToOriginal_SaveButtonDisabledAgain` — type a new name then retype the original name, assert the button returns to disabled (proves the dirty-check compares live value to baseline on every render, not a one-shot "touched" flag).
  - [x] `FlatStructureEditor_ClickRoomSaveButton_CallsMutationWithoutPageLevelSpeichernClick` — rename a seeded room, click its Save button, assert `mockMutate` was called with a payload containing just that room's new name, and no page-level Speichern click occurred.
  - [x] `FlatStructureEditor_SaveRoomWhileUnrelatedPowerPointNameIsBlank_PayloadOmitsTheBlankPowerPoint` — seed two rooms, navigate into one room's detail view, click "+ Anschlussstelle hinzufügen" (leave its name blank), navigate back to the list, rename the *other* room and click its Save button; assert the `mockMutate` payload does **not** contain the blank-named Power Point (AC9 regression test — the exact scenario AC9's rationale describes, now scoped to per-room save instead of add).
  - [x] `FlatStructureEditor_SaveRoomSucceeds_ButtonDisabledAgainAndOriginalNameUpdated` — click a dirty room's Save, resolve `onSuccess`, assert the button is disabled again and a further identical rename attempt shows it disabled.
  - [x] `FlatStructureEditor_SaveRoomFails_RevertsNameAndShowsSaveError` — mock `mutate` to invoke `onError`, assert the room's name reverts to `originalName` and `editor.saveError` renders (AC5).
  - [x] `FlatStructureEditor_DeleteRoomConfirm_CallsMutationImmediatelyWithRoomRemoved` — arm delete on a room, click confirm, assert `mockMutate` called with that room removed (AC7 — unchanged from prior design).
  - [x] `FlatStructureEditor_DeleteLastRemainingRoom_DoesNotCallMutationShowsNoRoomsError` — seed a single room, delete it, assert `mockMutate` was **not** called and `editor.noRoomsError` renders (AC12).
  - [x] `FlatStructureEditor_AnySavePending_DisablesAllRoomSaveButtonsDeleteAndSpeichern` — mock `useUpdateFlatStructure` with `isPending: true`, assert every room's Save button, every delete control, "+ Raum hinzufügen", and the page Speichern button are all disabled (AC4/AC10).
  - [x] Run `npx vitest run` and `npm run lint` from `client/` — zero regressions in the existing `FlatStructureEditor.test.tsx` suite (plug-conflict and manual-Speichern tests must still pass unchanged, since that flow is untouched by this story per AC8/AC13).

### Review Findings

- [x] [Review][Patch] **(Decision resolved 2026-07-15: build from draftRooms)** Per-room Save drops that room's own unsaved PowerPoint/device edits — `withRoomNameUpdatedAt`/`withRoomAppended` build the save payload by patching only `name` onto the `lastSaved` snapshot of that room, never onto the room's current `draftRooms` powerPoints. Ralf's decision: the room-level Save payload must pull *that room's own* current `draftRooms` PowerPoints/devices into the payload it sends, while continuing to source every *other* room's data from `lastSaved`. **Fixed:** new `toRoomInput(room, name)` helper in `draftModel.ts` converts a single `DraftRoom`'s current powerPoints/devices to wire shape; `handleSaveRoom` now builds the saved room's entry from `toRoomInput(room, trimmedName)` instead of patching only `name` onto a stale snapshot. Regression test: `FlatStructureEditor_SaveRoomWithOwnNewPowerPoint_PayloadIncludesTheNewPowerPoint`. [`draftModel.ts` toRoomInput, `FlatStructureEditor.tsx` handleSaveRoom]
- [x] [Review][Patch] **(Decision resolved 2026-07-15: key-based correlation)** Positional index into `lastSaved.rooms` desyncs from `draftRooms` when new rooms are saved/deleted out of insertion order — can delete the wrong room on the server or silently no-op a rename. Ralf's decision: switch to key-based correlation instead of array position. **Fixed:** `lastSaved` is now `KeyedRoomInput[]` (`{ key: string; room: RoomInput }[]`, `draftModel.ts`) tracking each room's `DraftRoom.key` alongside its last-saved wire-shape data; `withRoomAppended`/`withRoomUpdated`/`withRoomRemoved` look up by `key` instead of numeric index; `toWireRequest` converts to the plain `UpdateFlatStructureRequest` (recomputing `sortOrder` from array position) only at the point `mutate()` is called. Regression test: `FlatStructureEditor_RenameAlreadySavedNewRoomWhileEarlierUnsavedRoomStillExists_PersistsTheRename`. [`draftModel.ts:101-150`, `FlatStructureEditor.tsx` handleSaveRoom/handleDeleteRoom]
- [x] [Review][Patch] onSuccess sets `originalName` from the live `draftRooms` value instead of the value actually sent to the server. **Fixed:** `handleSaveRoom`'s `onSuccess` now sets `originalName: trimmedName` (the captured value that was actually sent in the payload) instead of re-reading `r.name` from state at callback time. [`FlatStructureEditor.tsx` handleSaveRoom onSuccess]
- [x] [Review][Patch] `handleDeleteRoom` has no guard for deleting a never-saved (new) room — fires a wasted no-op PUT and reports false success. **Fixed:** added `if (room.originalName === undefined) return` guard mirroring `handleSaveRoom`'s new-room branch, skipping the mutation entirely for rooms that were never persisted. Regression test: `FlatStructureEditor_DeleteNeverSavedNewRoom_DoesNotCallMutation`. [`FlatStructureEditor.tsx:111` handleDeleteRoom]
- [x] [Review][Patch] Untrimmed room name used inconsistently with the trimmed dirty-check/persisted value (Save button aria-label; `toDraftRooms` seeding `originalName`). **Fixed:** aria-label now uses `room.name.trim()`; `toDraftRooms` now seeds `originalName: room.name.trim()`. [`FlatStructureEditor.tsx` aria-label, `draftModel.ts` toDraftRooms]
- [x] [Review][Defer] Duplicate room names produce identical per-row aria-labels, making individual Save/Delete buttons indistinguishable to assistive tech and exact-name test queries [FlatStructureEditor.tsx aria-label] — deferred, pre-existing (room-name uniqueness never validated anywhere in this app)
- [x] [Review][Defer] onSuccess/onError discard the mutation's response body entirely, no reconciliation with server-side name normalization [FlatStructureEditor.tsx handleSaveRoom] — deferred, pre-existing (matches the app's existing page-level Speichern pattern)
- [x] [Review][Defer] Rapid double-click before `isPending` flips to true is a theoretical double-submit race [FlatStructureEditor.tsx handleSaveRoom/handleDeleteRoom] — deferred, pre-existing (same risk existed with the single page-level Speichern button)

## Dev Notes

### Design pivot during story creation: per-room dedicated Save button, not silent zero-click autosave

This story was originally scoped around fully automatic persistence (room add/delete firing a save with no button at all). Ralf refined this during story creation: each room gets its **own dedicated Save button**, enabled immediately for a brand-new room and enabled-when-dirty (name changed) for an existing room — closer in spirit to "the Save action lives right next to what it saves" than to "no click ever required." Delete is unaffected by this pivot: its existing confirm/cancel row already is the deliberate action, so it still persists immediately on confirm (AC7). FR-44's "no separate manual Save action is required" is satisfied for delete literally, and for add/rename by making Save unmissable and immediately actionable at the row — not by removing the click.

### No backend changes — reuse `PUT /api/v1/flats/{flatId}/structure` exactly as-is

`UpdateFlatStructureFunction.cs`, `UpdateFlatStructureValidator.cs`, and `FlatStructureModels.cs` are unmodified by this story. There is no per-room backend endpoint, and this story does not create one — see AC9. All the work is client-side: what payload gets sent, and when.

### Why a separate `lastSaved` baseline instead of building payloads from `draftRooms` directly

This is the central design decision of the story (AC9). `draftRooms` is one shared, always-live draft object that every view in the editor (room list, room detail, device edit) writes into — but the views differ in whether they gate writes behind a local commit step:

- Room rename, Power Point name/plugId edits (`PowerPointEditor.tsx:55-88`): write into `draftRooms` on every keystroke, **no local commit gate**.
- Device add/edit (`DeviceEditor.tsx`): has its own in-screen Save/Cancel; only `onSave` writes into `draftRooms` (`FlatStructureEditor.tsx:154-166`) — `onCancel` discards cleanly.

Because Power Point edits have no local commit gate, `draftRooms` can legitimately contain a blank-named or half-typed Power Point at any moment, in a completely different room than the one whose Save button was clicked. Building a per-room save payload from `draftRooms` directly would intermittently 400 (blank Power Point name) for a reason unrelated to the room actually being saved. The fix: never build a save payload from `draftRooms` as a whole — only from `lastSaved` (a wire-shape snapshot of what the server has already accepted) with exactly the one room's change applied. `lastSaved` is by definition always valid, because it only ever advances via successful saves.

### Why saves must be serialized through one shared `isPending` (AC10)

`UpdateFlatStructureFunction.cs` fully replaces `Rooms`/`PowerPoints`/`Devices` on every call — there is no per-room upsert server-side, even though this story's UI now offers N independent per-room Save buttons. Two in-flight PUTs are a last-write-wins race that can silently discard one of the two changes. Reusing the single existing `useUpdateFlatStructure(flatId)` mutation instance for every save path (each room's Save, delete, the page-level Speichern), and disabling every save-triggering control while its `isPending` is true, prevents this by construction. This also keeps `lastSaved` and `draftRooms` positionally aligned for the index-based helpers in Task 2 — if overlapping saves were ever possible, an index captured at click time could no longer be trusted to mean the same thing in `lastSaved.rooms` by the time the request is built.

### ID churn on every save is pre-existing, not introduced by this story

`UpdateFlatStructureFunction.cs:86,113-114` deletes every existing `Room`/`PowerPoint`/`Device` row and recreates them fresh on every PUT — every server ID is regenerated on every save, today, with the existing page-level Speichern flow already. This story makes saves more frequent (potentially one per room per edit, instead of one per editing session), which means more frequent ID regeneration, but does not change the *fact* that IDs churn — see AC11. `draftModel.ts`'s `toDraftRooms()` already never threads server IDs into the UI (`key: crypto.randomUUID()` per entity, every load), so this has no client-visible effect. Do not attempt to "fix" the backend's destroy/recreate pattern in this story.

### The page-level "Speichern" button is not being removed (AC13)

It remains necessary for Power Point/Device edits (Story 8.2's still-unaddressed territory) and continues to submit the full `draftRooms`. Per-room Save buttons and the page-level Speichern share the same mutation and `isPending` gate (AC10) but are otherwise independent — a room saved via its own button simply stops contributing to `hasEmptyName`/dirty state for future Speichern clicks.

### Scope boundary with Story 8.2

Story 8.2 relocates/fixes the save affordance for Power Point add/edit (in `RoomEditor.tsx`) and the Device edit screen's viewport-clipped Cancel/Save row — it does not change their persistence model. It should be aware that the shared `isPending` flag this story introduces now also disables the page-level Speichern (and, per this story, every room's own Save button) while any save from this story is running.

### Testing conventions

Follow `FlatStructureEditor.test.tsx`'s existing conventions exactly: `vi.mock('@/features/flat-structure/hooks/useFlatStructure')` and `vi.mock('@/features/flat-structure/hooks/useUpdateFlatStructure')`, `mockMutate = vi.fn()`, `seededResponse()`/`seededResponseWithDevice()` fixture builders, `MemoryRouter` + fresh `QueryClientProvider` wrapper via `renderEditor()`. Test naming: `FlatStructureEditor_Scenario_ExpectedBehavior`, matching the existing 20 tests in this file.

### Project Structure Notes

- Modified files only:
  - `client/src/features/flat-structure/components/FlatStructureEditor.tsx` — `lastSaved` state, per-room Save button + dirty-check rendering, `handleDeleteRoom` autosave wiring, disabled states on all save-triggering controls.
  - `client/src/features/flat-structure/components/draftModel.ts` — `DraftRoom.originalName` field; new `withRoomAppended`/`withRoomNameUpdatedAt`/`withRoomRemovedAt` pure helpers.
  - `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx` — new tests per Task 6.
- No new files. No backend changes. No new i18n keys beyond one new label for the per-room Save button (reuse `editor.save`/`editor.saving` from `flat-structure.json` — already exist, no new key needed). No new dependencies.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-8-ui-behavior-consistency-alignment.md#Story 8.1] — original epic-level AC text; this story's ACs 1–8 refine it per Ralf's story-creation-time direction (per-room dedicated Save button, dirty-tracked) rather than reproducing it verbatim — see Change Log.
- [Source: _bmad-output/planning-artifacts/epics/requirements-inventory.md#FR-44] — the functional requirement this story satisfies (see Dev Notes for how the "no separate manual Save action" wording maps onto the per-room-button design).
- [Source: _bmad-output/implementation-artifacts/epic-7-retro-2026-07-14.md] — origin of Epic 8; Ralf's original screenshot/finding for this story ("saving places are inconsistent and confusing" for structure definition).
- [Source: client/src/features/flat-structure/components/FlatStructureEditor.tsx] — full current implementation; read in full during story creation (lines 1–327).
- [Source: client/src/features/flat-structure/components/RoomEditor.tsx, PowerPointEditor.tsx] — confirmed no local save gate exists for Power Point edits, the root cause behind AC9.
- [Source: client/src/features/flat-structure/components/DeviceEditor.tsx] — confirmed Device edits *do* have a local commit gate (`onSave`/`onCancel`), contrasted with Power Point edits in Dev Notes.
- [Source: client/src/features/flat-structure/components/draftModel.ts] — existing `toDraftRooms`/`toUpdateRequest`/`findPlugIdConflict`/`hasBlankName` helpers reused/extended by this story.
- [Source: client/src/features/flat-structure/hooks/useUpdateFlatStructure.ts] — the single shared mutation this story reuses for every save path (AC10).
- [Source: api/Features/FlatStructure/UpdateFlatStructureFunction.cs:86,113-114] — confirmed full destroy-and-recreate behavior underlying AC10/AC11; confirmed tenant-check pattern (`Guid.TryParse` → 400, `SingleOrDefaultAsync` → 403) is unaffected by this story.
- [Source: api/Features/FlatStructure/UpdateFlatStructureValidator.cs:10,13,17] — confirmed `Rooms` is only `NotNull()` (not `NotEmpty()`) at the backend, contrasted with the client's stricter `hasNoRooms` policy in AC12; confirmed `PowerPoint.Name`/`Room.Name` are `NotEmpty()`, the exact rule a naive `draftRooms`-based payload would intermittently violate (AC9).
- [Source: client/src/locales/en-US/flat-structure.json] — confirmed `editor.save`/`editor.saving`/`editor.saveError`/`editor.saveSuccess` keys already exist and are reused, not duplicated.
- [Source: client/src/features/flat-structure/components/FlatStructureEditor.test.tsx] — existing test conventions and fixtures (`seededResponse`, mock setup) mirrored by Task 6.
- [Source: _bmad-output/project-context.md] — VSA feature-folder conventions, TanStack Query mutation conventions, no-shared-formatter/no-premature-abstraction conventions honored by keeping the new `draftModel.ts` helpers local and pure.

## Dev Agent Record

### Agent Model Used

claude-sonnet-5 (Amelia, dev-story workflow)

### Debug Log References

None — implementation proceeded without needing a debug log; `npx tsc --noEmit`, `npx vitest run`, and `npm run lint` all passed clean on first full run after implementation.

### Completion Notes List

- Implemented `DraftRoom.originalName` (undefined = new/never-saved room, present = existing room's last-saved name) and a component-level `lastSaved: UpdateFlatStructureRequest` baseline seeded from the load effect, independent of the live `draftRooms` draft (AC9).
- Added three pure helpers to `draftModel.ts` — `withRoomAppended`, `withRoomNameUpdatedAt`, `withRoomRemovedAt` — that derive a full-flat save payload from `lastSaved` plus one room's change, never from `draftRooms` directly, so a blank Power Point being typed in another room can never leak into an unrelated room's save payload (AC9 regression covered by a dedicated test).
- Added a dedicated per-room Save button, enabled immediately for new rooms (`originalName === undefined`) and dirty-tracked for existing rooms (`name.trim() !== originalName`). `onSuccess` advances `lastSaved` and the room's `originalName`; `onError` reverts the room's name (existing rooms only — a still-unsaved new room just stays editable).
- Wired `handleDeleteRoom` to call the same shared `useUpdateFlatStructure` mutation immediately on confirm, with a guard that skips the mutation entirely (and relies on the existing `noRoomsError` UI) when the delete would empty the room list (AC12).
- Every save-triggering control (each room's Save button, delete icon, delete confirm/cancel pair, "+ Add Room", and the existing page-level Speichern button) now shares the single `isPending` flag from the one `useUpdateFlatStructure(flatId)` instance — no parallel pending flags were introduced (AC10).
- Per-room Save buttons reuse the existing `editor.save`/`editor.saving` translation strings (no new i18n key) but carry a distinguishing `aria-label` (`"<label>: <room name>"`) so they remain individually addressable in tests and assistive tech without colliding with the page-level Speichern button's accessible name.
- Extended `FlatStructureEditor.test.tsx` with 11 new tests covering ACs 2–7, 9, 10, 12; full suite (`npx vitest run`) is 362/362 passing (30/30 in this file) with zero regressions to the pre-existing plug-conflict/manual-Speichern tests. `npm run lint` and `npx tsc --noEmit` are clean (only pre-existing, unrelated `router.tsx` fast-refresh warnings).
- No backend changes — reused `PUT /api/v1/flats/{flatId}/structure` exactly as-is, per AC9/Dev Notes.

### File List

- `client/src/features/flat-structure/components/draftModel.ts` — modified
- `client/src/features/flat-structure/components/FlatStructureEditor.tsx` — modified
- `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx` — modified

## Change Log

- 2026-07-15: Story created via create-story workflow, scoped around fully automatic (zero-button) persistence for room add/delete.
- 2026-07-15: Revised during story creation per Ralf's direct clarification: replaced zero-click autosave-on-add with a per-room dedicated Save button (enabled immediately for a new room, enabled when the name differs from its last-saved value for an existing room). Delete's immediate-on-confirm behavior is unchanged. Reworked ACs 1–8 (previously 1–4) and added AC13 to address the page-level Speichern button's continued role; retained and adapted the technical "gap found during story creation" ACs (payload-baseline-not-live-draft, save serialization, ID churn, zero-rooms guard) from the original draft, since the underlying backend/frontend architecture risks they address are unchanged by this UX pivot.
- 2026-07-15: Implemented via dev-story workflow — all 6 tasks complete, 11 new tests added, zero regressions (362/362 tests, lint and typecheck clean). Status moved to review.
