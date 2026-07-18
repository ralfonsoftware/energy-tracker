---
baseline_commit: 8e4d6f07bd22cb69e78e983a1db47e5bb0a2ea85
---

# Story 9.5: FlatStructureEditor Deep-Link Addressability

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want the "Go to settings" chip on an unconfigured Smart Power Strip sub-device to take me directly to that device's settings,
so that I don't have to manually hunt through the room list to find what I was just looking at.

## Acceptance Criteria

1. **Given** `FlatStructureEditor.tsx`'s internal `view` state is keyed by client-generated `crypto.randomUUID()` draft keys that only exist after `useFlatStructure` data loads, with no existing mechanism to target a specific PowerPoint from outside the editor (confirmed a real structural blocker by architecture review, not a shortcut — `deferred-work.md:310`), **when** this story is implemented, **then** the editor accepts a stable, route-addressable identifier via a query parameter (`?powerPointId={id}`, using the server-side PowerPoint id) and, once `useFlatStructure` data resolves, automatically opens the room/PowerPoint view matching that id.
2. **Given** `SmartStripCard.tsx`'s "Go to settings" chip on unconfigured sub-devices (Story 7.3 AC7), **when** clicked, **then** it navigates to `/settings/structure?powerPointId={id}` instead of the bare room list, landing the user directly on the correct PowerPoint rather than requiring manual navigation.
3. **Given** a `powerPointId` that no longer exists (e.g. a stale link after deletion), **when** the editor loads, **then** it falls back gracefully to the room list view — no error, no crash, no dead-end blank state.

## Tasks / Subtasks

- [x] Task 1: Thread the server-side `powerPointId` through the draft model so it survives past load (AC: 1)
  - [x] In `client/src/features/flat-structure/components/draftModel.ts`, add `powerPointId?: string` to the `DraftPowerPoint` type (`:25-30`). Optional because power points added client-side via `RoomEditor.tsx`'s `handleAddPowerPoint` (`{ key: crypto.randomUUID(), name: '', plugId: '', devices: [] }`) never have one — same "absent = never persisted" convention this file already uses for `DraftRoom.originalName`.
  - [x] In `toDraftRooms` (`draftModel.ts:41-65`), populate it: `powerPointId: powerPoint.powerPointId` inside the `room.powerPoints.map(powerPoint => ({ ... }))` block (`:46-49`). `PowerPointResponse.powerPointId` (`flatStructureApi.ts:21`) is already returned by the API today — it is simply discarded here currently. This is a pure additive field; do not change any other mapping in this function.
  - [x] Do not add `powerPointId` to `RoomInput`/`PowerPointInput`/`toRoomInput`/`toWireRequest` — the save payload shape is unaffected by this story; the id is read-only, used for view-targeting only, never sent back to the server.

- [x] Task 2: Make `FlatStructureEditor` resolve `?powerPointId=` into the initial `view` state (AC: 1, 3)
  - [x] In `client/src/features/flat-structure/components/FlatStructureEditor.tsx`, import `useSearchParams` from `react-router-dom` (v7, already the project's router) alongside the existing `useNavigate` import (`:2`). Call `const [searchParams] = useSearchParams()` and read `const powerPointId = searchParams.get('powerPointId')`.
  - [x] Modify the existing data-seeding `useEffect` (`:52-68`, guarded by `initializedFlatIdRef.current === flatId`) — after building `seeded`/`draftRooms` from `data`, before `setView({ type: 'list' })`, search for a match: `const matchedRoom = powerPointId ? seeded.find(room => room.powerPoints.some(pp => pp.powerPointId === powerPointId)) : undefined`. Replace the hardcoded `setView({ type: 'list' })` with `setView(matchedRoom ? { type: 'room', roomKey: matchedRoom.key } : { type: 'list' })`.
  - [x] Important: run this lookup against `seeded` (the freshly computed `DraftRoom[]`), not the `draftRooms` state variable — state set via `setDraftRooms` in the same effect has not committed yet when this code runs, so reading `draftRooms` here would use the previous (likely empty) render's value.
  - [x] No match (stale/deleted `powerPointId`, or param absent) → `matchedRoom` is `undefined` → falls through to `{ type: 'list' }`, identical to today's default. This is the entire AC3 fallback — no additional error state, no crash path, no extra branching needed.
  - [x] Do not attempt to also open the specific `device` view or scroll/highlight the individual `PowerPointEditor` row within `RoomEditor.tsx` — out of scope. `RoomEditor.tsx` renders all of a room's power points as a flat always-visible list (no accordion/collapse), so landing on `{ type: 'room', roomKey }` already satisfies "lands the user directly on the correct PowerPoint" per AC2 without requiring new scroll-to/expand UI.

- [x] Task 3: Wire the "Go to settings" chip to pass the PowerPoint id through to the navigation call (AC: 2)
  - [x] In `client/src/features/decomposition/components/RoomCard.tsx`, change the `Props` type's `onConfigureDevice: () => void` (`:7`) to `onConfigureDevice: (powerPointId: string) => void`. Update the call site `<SmartStripCard device={device} onConfigure={onConfigureDevice} />` (`:55`) to `<SmartStripCard device={device} onConfigure={() => onConfigureDevice(device.deviceId)} />`.
  - [x] Critical existing-code fact: for a Smart Power Strip, `DeviceDecomposition.deviceId` (the `device` prop here) is **already the server-side PowerPoint id**, not a device id — confirmed at `api/Features/Decomposition/DecompositionEngine.cs:222`, `BuildSmartStripDecomposition` constructs `new DeviceDecomposition(pp.PowerPointId, pp.Name, ...)`. No backend change, no new field, no new API call is needed to get this id — it is already present on the object being passed around, just not yet forwarded to the navigation callback.
  - [x] `SmartStripCard.tsx` itself needs **no changes** — its `onConfigure: () => void` prop and the chip's `onClick={onConfigure}` (`:56`) stay exactly as-is; the id is bound at the `RoomCard` call site via closure, not passed as a new prop into `SmartStripCard`.
  - [x] In `client/src/features/decomposition/components/DecompositionTab.tsx`, change `onConfigureDevice={() => navigate('/settings/structure')}` (`:91`) to `onConfigureDevice={powerPointId => navigate(\`/settings/structure?powerPointId=${powerPointId}\`)}`.
  - [x] Do not touch `client/src/features/settings/components/FlatSettingsCard.tsx:133`'s bare `navigate('/settings/structure')` — that is the unrelated "manage structure" entry point from Settings, not a device-specific deep link, and must keep navigating to the plain room list.

- [x] Task 4: Update/add tests (AC: 1, 2, 3)
  - [x] `client/src/features/decomposition/components/RoomCard.test.tsx`: update `RoomCard_SmartStripConfigureHintClicked_CallsOnConfigureDevice` (`:172-201`) — it currently only asserts `expect(onConfigureDevice).toHaveBeenCalled()`. Change to `expect(onConfigureDevice).toHaveBeenCalledWith('d1')` (the strip device's `deviceId` used in that test's fixture), so the test actually pins the id-forwarding behavior this story adds, not just that the callback fires.
  - [x] `client/src/features/decomposition/components/DecompositionTab.test.tsx`: add a new test seeding a room with an `isSmartStrip: true` device (matching the shape used in `DecompositionTab_NormalResponse_RendersResidualCardBeforeAllRoomCardsInOrder`'s fixtures — check that test's mock data setup for the exact `RoomDecomposition`/`DeviceDecomposition` fixture shape already in this file and reuse it, do not invent a new one), click through to the strip's configure chip, and assert `mockNavigate` (already defined at `:14`, following the same pattern as the existing `/decomposition/import` assertion at `:87`) was called with `/settings/structure?powerPointId={that device's deviceId}`.
  - [x] `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx`: this file's `renderEditor()` helper (`:50-59`) wraps in a bare `<MemoryRouter>` with no `initialEntries`. Add a new test that renders with `<MemoryRouter initialEntries={['/settings/structure?powerPointId=pp-2']}>` (reuse `seededResponse()`'s existing `pp-1`/`pp-2` fixture, `:67-101`) and asserts the editor opens directly on the Garage room view (e.g. `expect(screen.getByDisplayValue('Charger Outlet')).toBeInTheDocument()` — the same room-view assertion pattern used by `FlatStructureEditor_ClickRoomRow_TransitionsToRoomViewAndBackReturnsToList`, `:185-198`) without any click needed to get there.
  - [x] Add a second new test with `initialEntries={['/settings/structure?powerPointId=does-not-exist']}` asserting the editor falls back to the room list view (e.g. both `screen.getByDisplayValue('Office')` and `screen.getByDisplayValue('Garage')` are present — the list view's per-room rows — and no room-detail-only content like `Desk Outlet` is directly visible) — this is AC3's stale-link fallback, and must not throw or render blank.
  - [x] Note: `renderEditor()`'s helper signature only takes `flatId` today; either add an optional second parameter for `initialEntries` (defaulting to `['/']`, preserving every existing call site unchanged) or inline a one-off `render(...)` call for the two new tests — either is acceptable, but do not change the default behavior for the ~40 existing tests in this file that call `renderEditor()` with no route.

## Dev Notes

### Why this was a real structural blocker, not a shortcut

`FlatStructureEditor.tsx`'s `view` state (`type View = { type: 'list' } | { type: 'room'; roomKey: string } | { type: 'device'; ... }`, `:28-31`) is keyed by `roomKey`/`powerPointKey`/`deviceKey` — all `crypto.randomUUID()` values generated fresh in `toDraftRooms`/`createDefaultDraftRooms` every time the component mounts (`draftModel.ts:43,46,51,74`). These keys do not exist until after `useFlatStructure`'s data resolves, and are never the same value twice across reloads. There was therefore no way to express "open PowerPoint X" as a URL before this story — not because no one built the plumbing, but because the only identifiers available were ephemeral client-generated ones. This story's fix is to route by the **server-side `powerPointId`** (stable, persisted, already returned by the API in `PowerPointResponse.powerPointId` — `flatStructureApi.ts:21`) and resolve it to the current render's `roomKey` only after data loads, rather than trying to route by the client-generated key directly.

### The PowerPoint id is already flowing through the Decomposition API — just not forwarded

This is the key discovery from reading `DecompositionEngine.cs` before writing this story: a Smart Power Strip's `DeviceDecomposition.deviceId` **is** `pp.PowerPointId` (`DecompositionEngine.cs:222`), not a device id — the decomposition API models a configured Smart Power Strip as a single "device" entry (the strip itself, addressed by its PowerPoint) with `subDevices` underneath for the individual plugged-in devices. This means `RoomCard.tsx`'s `device.deviceId` (the `device` prop passed to `SmartStripCard`) is already the exact identifier this story's `?powerPointId=` mechanism needs — no new backend field, no new API call, no schema change. The entire fix is: stop discarding it in `draftModel.ts`, and forward it through the two existing `onConfigureDevice`/`navigate` call sites (Task 3).

### `RoomEditor.tsx` has no accordion — landing on the room is landing on the PowerPoint

`RoomEditor.tsx` (`:59-77`) renders every power point in a room as an always-visible `PowerPointEditor` card in a flat list — there is no collapse/expand state, no per-power-point route, no scroll-to mechanism today. AC2's "landing the user directly on the correct PowerPoint" is fully satisfied by opening `{ type: 'room', roomKey }` for the room containing that `powerPointId` — the target `PowerPointEditor` is immediately visible on that screen, no scrolling/highlighting logic needs to be built. Do not add a new "highlight the matched power point" affordance — not required by any AC, and would be scope creep on a story that is purely a routing/addressability fix.

### What this story does NOT touch

- No backend change of any kind — `PowerPointResponse.powerPointId` already exists and is already returned; this is 100% a frontend plumbing fix.
- `SmartStripCard.tsx` — zero changes. Its prop signature (`onConfigure: () => void`) and click handler are untouched; the id is bound via closure one level up in `RoomCard.tsx`.
- `RoomEditor.tsx`, `PowerPointEditor.tsx`, `DeviceEditor.tsx` — no changes. This story only affects which `view` the editor opens to initially, not anything about how a room/power point/device is rendered or edited once you're there.
- `FlatSettingsCard.tsx`'s bare `/settings/structure` navigation (the "manage structure" settings entry point) — stays a bare link to the room list, unrelated to this device-specific deep link.
- The save/mutation payload shape (`toRoomInput`, `toWireRequest`, `UpdateFlatStructureRequest`) — `powerPointId` is read-only routing metadata, never sent back to the server.
- Story 9.6 (`RoomCard.tsx`'s ghost-card treatment for standalone unmeasured devices) depends on this story's `?powerPointId=` mechanism to deep-link its own "Configure consumption profile" button, per the epic's AC for that story — this story's Task 2/3 plumbing must remain stable for 9.6 to build on.

### Testing Standards Summary

- Frontend only — Vitest + `@testing-library/react`, `globals: true` (no `describe`/`it`/`expect` imports), per `project-context.md`.
- `FlatStructureEditor.test.tsx` already wraps in `MemoryRouter` + `QueryClientProvider` (`:50-59`) — the new deep-link tests only need `initialEntries` added, no new wrapper infrastructure. `react-router-dom` is mocked in this file only to override `useNavigate` (`:19-22`) via `vi.importActual` — `useSearchParams` is *not* mocked, so it will resolve real query params from `MemoryRouter`'s `initialEntries` correctly.
- `RoomCard.test.tsx` and `DecompositionTab.test.tsx` — no router wrapper needed for the `RoomCard` unit test (pure prop/callback assertion); `DecompositionTab.test.tsx` already has a `mockNavigate` pattern (`:10-14`) to reuse for the new assertion.
- Query by role/label/text, not CSS class or `data-testid`, per this project's convention.

### Project Structure Notes

- Files touched: `client/src/features/flat-structure/components/draftModel.ts`, `client/src/features/flat-structure/components/FlatStructureEditor.tsx`, `client/src/features/decomposition/components/RoomCard.tsx`, `client/src/features/decomposition/components/DecompositionTab.tsx`, plus their four corresponding test files.
- No new files, no new dependencies, no i18n changes (no new user-visible strings — this is pure routing logic), no backend/API/schema changes.
- VSA slice isolation: this story reads across two feature slices (`decomposition` calls into `settings/structure` via `navigate(...)`, a URL string) but does not import hooks/components across slices — the existing `navigate('/settings/structure')` pattern (already present pre-story) is a route string, not a cross-slice code import, consistent with this project's isolation rule.

### Previous Story Intelligence

- Story 9.4 (immediately prior) was a backend-only fix to `DecompositionEngine.cs`'s smart-strip share formula — no frontend patterns or learnings carry forward from it, and it did not touch any file this story touches.
- Story 9.3 (last frontend story to touch `FlatStructureEditor.tsx`) was a presentation-only restyle (Save button visual treatment) — explicitly no changes to `handleSave`/`handleSaveRoom`/`isRoomDirty`/view-state logic. This story is the first to modify `FlatStructureEditor.tsx`'s `view`-state initialization logic since it was introduced; no prior gotchas apply beyond what's captured above.
- Story 7.3 introduced the "Go to settings" chip on unconfigured Smart Power Strip sub-devices (`SmartStripCard.tsx` `configureHint`) that this story's Task 3 rewires — the chip and its `isUnconfigured` condition are unchanged, only where its click ultimately navigates to.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-9-unified-save-affordance-fix-and-pre-epic-10-hardening.md#Story 9.5] — original epic AC text (verbatim source for this story's ACs).
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:310] — "Deferred from: party-mode review of story 7.3" entry this story resolves and should be marked closed once shipped (architecture review by Winston confirmed this as a real structural blocker, not a shortcut).
- [Source: client/src/features/flat-structure/components/FlatStructureEditor.tsx:28-31,37-68] — `View` type and the data-seeding `useEffect` this story modifies.
- [Source: client/src/features/flat-structure/components/draftModel.ts:25-30,41-65] — `DraftPowerPoint` type and `toDraftRooms`, both modified by Task 1.
- [Source: client/src/features/flat-structure/api/flatStructureApi.ts:20-25] — `PowerPointResponse.powerPointId`, the already-existing server field this story starts propagating into the draft model.
- [Source: client/src/features/decomposition/components/RoomCard.tsx:7,55] — `onConfigureDevice` prop and `SmartStripCard` call site modified by Task 3.
- [Source: client/src/features/decomposition/components/SmartStripCard.tsx] — unchanged by this story; confirms `onConfigure: () => void` stays as-is.
- [Source: client/src/features/decomposition/components/DecompositionTab.tsx:91] — the `navigate('/settings/structure')` call site modified by Task 3.
- [Source: client/src/features/settings/components/FlatSettingsCard.tsx:133] — the unrelated bare `/settings/structure` navigation that must NOT be touched.
- [Source: client/src/features/settings/SettingsPage.tsx:53] — confirms `/settings/structure` route mounts `FlatStructureEditor` inside the router tree (required for `useSearchParams` to work).
- [Source: api/Features/Decomposition/DecompositionEngine.cs:222] — confirms `DeviceDecomposition.DeviceId` for a Smart Power Strip is `pp.PowerPointId`, the key discovery enabling Task 3 with zero backend changes.
- [Source: client/src/features/flat-structure/components/FlatStructureEditor.test.tsx:50-59,67-101] — `renderEditor()` helper and `seededResponse()` fixture (rooms `pp-1`/`pp-2`) to reuse for the new deep-link tests.
- [Source: client/src/features/decomposition/components/RoomCard.test.tsx:172-201] — existing test to update in Task 4.
- [Source: _bmad-output/project-context.md#react-router-dom v7] — router usage conventions (lazy imports only, hooks only work inside the router tree).

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

None — implementation went green on first test run for all four touched test files (61/61 in the touched suites, 385/385 full suite).

### Completion Notes List

- Added `powerPointId?: string` to `DraftPowerPoint` (`draftModel.ts`) and populated it from `PowerPointResponse.powerPointId` in `toDraftRooms` — previously discarded, now carried through so a stable server-side id survives into the client-generated draft model.
- `FlatStructureEditor.tsx` now reads `?powerPointId=` via `useSearchParams` and, in the existing data-seeding `useEffect`, matches it against the freshly-built `seeded` rooms (not the not-yet-committed `draftRooms` state) to open `{ type: 'room', roomKey }` directly; no match (absent or stale id) falls through to the pre-existing `{ type: 'list' }` default — satisfies AC3's graceful-fallback requirement with no new branching.
- `RoomCard.tsx`'s `onConfigureDevice` now takes a `powerPointId: string` and forwards `device.deviceId` (already the server-side PowerPoint id for a Smart Power Strip, confirmed via `DecompositionEngine.cs:222`) from the `SmartStripCard` call site. `SmartStripCard.tsx` itself required zero changes.
- `DecompositionTab.tsx`'s `onConfigureDevice` now navigates to `/settings/structure?powerPointId={id}` instead of the bare room list. `FlatSettingsCard.tsx`'s unrelated bare `/settings/structure` link was left untouched per story scope.
- Updated `RoomCard.test.tsx`'s existing configure-hint test to assert `onConfigureDevice` is called with the strip's id (`'d1'`), not just that it was called.
- Added a new `DecompositionTab.test.tsx` test asserting the configure-hint click navigates with the correct `powerPointId` query param.
- Added two new `FlatStructureEditor.test.tsx` tests: a matching `powerPointId` opens directly on that room's view, and a stale/unknown `powerPointId` falls back to the room list without error. Extended `renderEditor()` with an optional `initialEntries` parameter (default `['/']`) — no existing call sites changed behavior.
- Closed out the originating `deferred-work.md` entry ("Deferred from: party-mode review of story 7.3", the `powerPointId`/deep-link line) now that this story ships the fix; left the adjacent Story 9.6 entry in that same section untouched (still open, unrelated).
- Full verification: `npx vitest run` — 385/385 passed (59 test files); `npx tsc --noEmit` — clean; `npm run lint` — clean (only pre-existing, unrelated `router.tsx` fast-refresh warnings).

### File List

- `client/src/features/flat-structure/components/draftModel.ts` (modified)
- `client/src/features/flat-structure/components/FlatStructureEditor.tsx` (modified)
- `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx` (modified)
- `client/src/features/decomposition/components/RoomCard.tsx` (modified)
- `client/src/features/decomposition/components/RoomCard.test.tsx` (modified)
- `client/src/features/decomposition/components/DecompositionTab.tsx` (modified)
- `client/src/features/decomposition/components/DecompositionTab.test.tsx` (modified)
- `_bmad-output/implementation-artifacts/deferred-work.md` (modified)

## Change Log

- 2026-07-18: Implemented `?powerPointId=` deep-link addressability for `FlatStructureEditor`, wired the Smart Power Strip "Go to settings" chip through to pass the id, added/updated regression tests (deep-link match, stale-id fallback, id-forwarding), closed out the originating deferred-work.md entry. Status → review.
