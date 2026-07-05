---
baseline_commit: 10ecdd574b1e6966bccbc21d2070154e67e88dde
---

# Story 5.4: Flat Structure Editor Frontend

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to define and edit the rooms, power points, and devices in my flat using a structured editor pre-populated with a default room template,
so that I have the physical hierarchy ready before importing smart plug data.

## Acceptance Criteria

1. **Given** the Flat Structure editor opens for a Flat with no existing structure (`hasDefaultTemplate: true`), **when** rendered, **then** `FlatStructureEditor.tsx` pre-populates five default Room entries: living room, bedroom, kitchen, bathroom, hallway; a prompt reads "These rooms were pre-filled — edit names or add your own."; no database write occurs until the user saves.

2. **Given** the editor with rooms present, **when** rendered, **then** each Room shows its name (editable inline) and expands to reveal its PowerPoints; each PowerPoint shows its name, an optional Smart Plug / Smart Power Strip `plugId` assignment field, and its Devices; "Add Room", "Add Power Point", and "Add Device" controls are available at the appropriate hierarchy levels.

3. **Given** the user assigns a Smart Power Strip to a PowerPoint, **when** saved, **then** Strip Outlet rows (one per device slot) appear beneath the PowerPoint for Device assignment; the PowerPoint's `plugId` is set at the strip level (FR-21).

4. **Given** a Device row in the editor at this stage, **when** rendered, **then** `Name`, `Type`, `Manufacturer`, `Model` fields are editable; `ConsumptionApproach` defaults to `None`; an inline note reads "Configure consumption profile to include this device in Decomposition" — the EU label / self-measured entry UI is added in Epic 6 Story 6.5.

5. **Given** the user saves the structure, **when** `useUpdateFlatStructure` calls `PUT /api/v1/flats/{flatId}/structure`, **then** on success: TanStack Query key `['flat-structure', flatId]` is invalidated; a success confirmation is shown.

6. **Given** a `plugId` conflict (same plug assigned to two PowerPoints), **when** the user attempts to save, **then** an inline validation error appears: "This plug is already assigned to another power point"; Save is disabled until resolved.

## Tasks / Subtasks

### Frontend — API client & types

- [x] Task 1: `client/src/features/flat-structure/api/flatStructureApi.ts` (AC: 2, 3, 4, 5, 6)
  - [x] Mirror the backend's exact camelCase JSON shape from `api/Features/FlatStructure/FlatStructureModels.cs` (see Story 5.3, already `done`): `FlatStructureResponse { flatId, hasDefaultTemplate, rooms: RoomResponse[] }`; `RoomResponse { roomId, name, sortOrder, powerPoints: PowerPointResponse[] }`; `PowerPointResponse { powerPointId, name, plugId, devices: DeviceResponse[] }`; `DeviceResponse { deviceId, name, type, manufacturer, model, purchaseDate, consumptionApproach, euLabelClass, euAnnualKwh, selfMeasuredKwh, selfMeasuredPeriod }`.
  - [x] Request types mirror `RoomInput`/`PowerPointInput`/`DeviceInput`/`UpdateFlatStructureRequest` (no ID fields — full replace, per Story 5.3 Dev Notes: "the client never needs to correlate input rows to prior IDs").
  - [x] `ConsumptionApproach` type: `'None' | 'EuLabel' | 'SelfMeasured'`; `SelfMeasuredPeriod` type: `'Daily' | 'Weekly' | null`. These are **string literal unions, not numbers** — the backend's `JsonStringEnumConverter` (added in Story 5.3) serializes/deserializes enums as strings over the wire; sending an integer will fail to deserialize server-side.
  - [x] `getFlatStructure(flatId: string) => apiClient.get<FlatStructureResponse>(\`/flats/${flatId}/structure\`)`; `updateFlatStructure(flatId: string, body: UpdateFlatStructureRequest) => apiClient.put<FlatStructureResponse>(\`/flats/${flatId}/structure\`, body)` — follow `tariffApi.ts`'s exact shape (flat exported functions, no class).

### Frontend — hooks

- [x] Task 2: `client/src/features/flat-structure/hooks/useFlatStructure.ts` (AC: 1, 2)
  - [x] `useQuery({ queryKey: ['flat-structure', flatId], queryFn: () => getFlatStructure(flatId as string), enabled: !!flatId })` — mirrors `useTariffs.ts` exactly.

- [x] Task 3: `client/src/features/flat-structure/hooks/useUpdateFlatStructure.ts` (AC: 5)
  - [x] `useMutation({ mutationFn: (body) => { if (!flatId) throw new Error('flatId is required'); return updateFlatStructure(flatId, body) }, onSuccess: () => queryClient.invalidateQueries({ queryKey: ['flat-structure', flatId] }) })` — mirrors `useCreateTariff.ts`. Do **not** also invalidate `['dashboard', flatId]` — structure changes don't affect KPI/dashboard numbers (unlike a new Tariff), so there is no cross-invalidation here.

### Frontend — draft state model (the core design decision for this story)

- [x] Task 4: Draft types + mapping helpers, co-located in `FlatStructureEditor.tsx` (or a sibling `draftModel.ts` if it grows unwieldy) (AC: 1, 2, 3, 4, 5, 6)
  - [x] **This editor buffers ALL edits locally and persists them in a single `PUT` on explicit Save — it does not auto-persist per keystroke or per row, unlike every other feature in this codebase (Tariffs, Flat settings).** AC1 states this explicitly ("no database write occurs until the user saves"); AC5/AC6 confirm a single, explicit save action gated by validation. This is a deliberate deviation from `TariffForm`'s "each field submits independently" pattern — do not copy that pattern here.
  - [x] Define local-only draft types with a client-side `key: string` field (generated via `crypto.randomUUID()`) used purely for React list keys and stable identity while editing — **never sent to the server**:
    ```ts
    type DraftDevice = { key: string; name: string; type: string; manufacturer: string; model: string }
    type DraftPowerPoint = { key: string; name: string; plugId: string; devices: DraftDevice[] }
    type DraftRoom = { key: string; name: string; powerPoints: DraftPowerPoint[] }
    ```
  - [x] Mapping `FlatStructureResponse` → `DraftRoom[]`: assign a fresh `crypto.randomUUID()` per level; flatten `type`/`manufacturer`/`model` `null` → `''` for controlled inputs. Server `sortOrder` only determines initial array order (already sorted by the API) — do not carry it into the draft type; it is recomputed from array index on save.
  - [x] Mapping `DraftRoom[]` → `UpdateFlatStructureRequest` on Save: strip all `key` fields; `sortOrder: index` (0-based, from array position — no manual reordering UI in this story); for each Device: `consumptionApproach: 'None'`, `type`/`manufacturer`/`model`: empty string → `undefined` (so the JSON omits or nulls a truly-blank field rather than persisting `""`), `euLabelClass`/`euAnnualKwh`/`selfMeasuredKwh`/`selfMeasuredPeriod`/`purchaseDate`: always `undefined` — this story's Device form has no fields for them (AC4; deferred to Epic 6 Story 6.5). For each PowerPoint: `plugId`: trim; empty string → `undefined` (matches the backend's own normalization of `""` → "unassigned" from Story 5.3's Review Findings — do not send empty string as a real `plugId`).
  - [x] **Initialize the draft exactly once from server data (or the default template), never on every re-render/refetch.** Use a `useRef` init-guard (e.g. `hasInitializedRef`) set on first successful data arrival, mirroring `PlannedAnnualSpendSection`'s `dirty`-flag pattern in `TariffList.tsx` for "don't clobber local edits when the query refetches in the background." Background invalidation after Save (Task 3) must not silently discard in-progress edits on other rows.

### Frontend — components

- [x] Task 5: `client/src/features/flat-structure/components/FlatStructureEditor.tsx` (AC: 1, 2, 5, 6)
  - [x] Props: `{ flatId: string | undefined }` (passed in — **never** import `useUserSettings` from `@/features/settings/hooks/useUserSettings` here; `flat-structure` and `settings` are separate VSA slices and cross-slice hook imports are forbidden per `project-context.md`'s "VSA slice isolation" rule — see Task 9 for how `flatId` is threaded in).
  - [x] Fetch via `useFlatStructure(flatId)`; loading → skeleton rows (mirror `TariffList.tsx`'s `animate-pulse` pattern); error → inline error banner + retry button.
  - [x] Holds `draftRooms: DraftRoom[]` state (Task 4) and a `view` state machine: `{ type: 'list' } | { type: 'room'; roomKey: string } | { type: 'device'; roomKey: string; powerPointKey: string; deviceKey: string | null }` (`deviceKey: null` = adding a new Device). **This is in-component navigation state, not a react-router route** — see Dev Notes ("Drill-down navigation, not routing") for why.
  - [x] `view: 'list'` renders: header with back button (→ `navigate('/settings')`, only at this top level) + title + a **Save action in the header's action slot** (top-right, mirroring the `.header-action` slot the mock uses for the pencil icon on the Room view — the mock's Frame 1 doesn't render a button there, but AC1/AC5 require an explicit save action that isn't a per-row auto-persist; see Dev Notes). Below: subtitle showing room/plug counts, then the Room list (each row: inline-editable name input + "N power points" summary + chevron `›` that transitions `view` to `{ type: 'room', roomKey }`), "+ Add Room" pill (appends a `DraftRoom` with an i18n-sourced default name, e.g. `t('editor.newRoomName')`, and empty `powerPoints: []`), and the default-template footer note (AC1) shown only while `hasDefaultTemplate` was true on load.
  - [x] Save button: disabled when a plugId conflict exists (Task 6) or a mutation is in flight; on click, maps the draft (Task 4) and calls `useUpdateFlatStructure`'s `mutate`; on success shows a transient inline confirmation (no toast library in this codebase — add a simple dismissing/auto-clearing banner, consistent with `FlatBaselineEdit`'s inline error-banner pattern but styled as success); on error shows an inline error banner (do not silently swallow).
  - [x] plugId conflict detection (AC6): compute across **all** PowerPoints in **all** rooms of `draftRooms` on every render (a plain derived value, no memo needed at this data size): trim each `plugId`, filter out empty strings (an empty plugId is "unassigned," never a conflict — mirrors the backend's Story 5.3 Review Findings fix), then check for duplicates. If any duplicate exists, render the inline error text below the Save button and disable Save.

- [x] Task 6: `client/src/features/flat-structure/components/RoomEditor.tsx` (AC: 2, 3)
  - [x] Props: the single `DraftRoom` being viewed, plus `onChange(updated: DraftRoom)` and `onBack()`. Rendered when `view.type === 'room'`.
  - [x] Header: back button (→ `view: { type: 'list' }`, via `onBack`) + Room name as the title (read-only display here — the room name is edited inline on the list row, per AC2's "editable inline" language and the mock's Frame 1 list rows; Frame 2's header shows the name as a static title, not another edit field).
  - [x] Renders one `PowerPointEditor` per `DraftPowerPoint` in the room (Task 7), then an "+ Add Power Point" pill (appends a `DraftPowerPoint` with an i18n default name, empty `plugId`, empty `devices: []`).

- [x] Task 7: `client/src/features/flat-structure/components/PowerPointEditor.tsx` (AC: 2, 3)
  - [x] Props: the single `DraftPowerPoint`, plus `onChange(updated: DraftPowerPoint)`, and `onEditDevice(deviceKey: string | null)` (null = add new — transitions the parent's `view` to `{ type: 'device', ..., deviceKey }`).
  - [x] Renders: inline-editable Name input; a labeled `plugId` text input (free text — per `api/Data/Configurations/PowerPointConfiguration.cs`, `PlugId` is a plain nullable string the user assigns, never derived from file metadata; do not add a "provider name" or any other field — the API contract has exactly one field here). One row per existing Device (name + "Edit device ›" affordance calling `onEditDevice(device.key)`), then a "+ Add Device" pill calling `onEditDevice(null)`.
  - [x] **No separate "Smart Power Strip" mode or toggle.** Per Story 5.3 Dev Notes: "a Smart Power Strip is simply a PowerPoint with a PlugId set, and each 'outlet' is just a Device row attached to that same PowerPoint" — there is no backend field distinguishing a single smart plug from a strip. AC3's "Strip Outlet rows (one per device slot) appear beneath the PowerPoint" is **already satisfied** by this component's existing per-Device row list once more than one Device is added under a PowerPoint with a `plugId` set — do not build anything additional for AC3.

- [x] Task 8: `client/src/features/flat-structure/components/DeviceEditor.tsx` (AC: 4)
  - [x] Props: the `DraftDevice` being edited (or `undefined` when adding new), `onSave(device: DraftDevice)`, `onCancel()`. Rendered when `view.type === 'device'`.
  - [x] **This is intentionally simpler than the mock's Frame 3 ("Add Device — choose approach").** Frame 3's name input + "Smart plug connected / Estimated usage" choice cards + "Continue →" flow depicts **Epic 6 Story 6.5's** EU-label/self-measured entry UI (the mock file bundles UI for multiple stories/epics in one HTML doc — this is a preview of later work, not this story's scope). This story's AC4 only requires: `Name` (required, non-empty), `Type`, `Manufacturer`, `Model` (all optional free text), a static inline note (`t('device.consumptionNote')`, wired to i18n, reading "Configure consumption profile to include this device in Decomposition"), and Save/Cancel actions. `ConsumptionApproach` is not user-editable here — it is fixed to `'None'` in the draft mapping (Task 4). Do not build the choice-card UI or any EU label/self-measured fields — that is Story 6.5's job.
  - [x] Save disabled until `name.trim() !== ''` (mirrors the backend's `NotEmpty()` room/power-point/device name validation from `UpdateFlatStructureValidator.cs` in Story 5.3 — catching this client-side avoids a round-trip 400).

### Frontend — routing & entry points (VSA slice-boundary detail — read before wiring)

- [x] Task 9: `client/src/features/settings/SettingsPage.tsx` + `client/src/features/settings/components/FlatSettingsCard.tsx` (AC: 1–6, entry point for all)
  - [x] In `SettingsPage.tsx`, add a `FlatStructureSettingsRoute` wrapper function **in this file**, exactly mirroring the existing `TariffSettingsRoute` wrapper (both `flat-structure` and `tariffs` are slices separate from `settings`, so `settings` — which already legitimately owns `useUserSettings` — must fetch `flatId` and pass it down as a prop; the child component must not reach across the slice boundary itself):
    ```tsx
    const FlatStructureEditor = lazy(() =>
      import('@/features/flat-structure/components/FlatStructureEditor').then(m => ({ default: m.FlatStructureEditor }))
    )
    function FlatStructureSettingsRoute() {
      const { settings, isLoading, isError } = useUserSettings()
      if (isLoading || isError) return null
      return <FlatStructureEditor flatId={settings?.flatId} />
    }
    ```
    Add `<Route path="structure" element={<Suspense fallback={null}><FlatStructureSettingsRoute /></Suspense>} />` to the `<Routes>` tree, following the `flat`/`tariffs` routes' exact shape.
  - [x] `FlatStructureEditor` must use a **named export** (`export function FlatStructureEditor`), not default — matching `TariffList`'s export shape, since the lazy-import `.then(m => ({ default: m.FlatStructureEditor }))` pattern above requires it.
  - [x] In `FlatSettingsCard.tsx`, add a third pill next to the existing `kwhBaselineLink`/`tariffLink` pills, `onClick={() => navigate('/settings/structure')}`, labelled via a new `t('flat.structureLink')` key in `settings.json` (both locales) — follow the two existing pills' exact styling (same `style={{ background: 'rgba(255,255,255,0.10)', ... }}` object, same button classes).

### Frontend — i18n

- [x] Task 10: `client/src/locales/en-US/flat-structure.json` and `client/src/locales/de-DE/flat-structure.json` (currently both `{}` placeholders — this story is the first to populate them) (AC: 1–6)
  - [x] Required keys (structure, not exhaustive wording — write natural en-US/de-DE copy for each): `editor.title`, `editor.back`, `editor.subtitle` (interpolates room/plug counts), `editor.defaultTemplateNote` (AC1's exact prompt), `editor.addRoom`, `editor.newRoomName` (default name for a freshly-added room, distinct from the five template defaults below), `editor.save`, `editor.saving`, `editor.saveError`, `editor.saveSuccess`, `editor.plugIdConflict` (AC6's exact message), `editor.loadError`, `editor.retry`; `room.namePlaceholder`, `room.powerPointsSummary`, `room.addPowerPoint`; `powerPoint.namePlaceholder`, `powerPoint.plugIdLabel`, `powerPoint.plugIdPlaceholder`, `powerPoint.addDevice`, `powerPoint.editDevice`; `device.title`, `device.namePlaceholder`, `device.typePlaceholder`, `device.manufacturerPlaceholder`, `device.modelPlaceholder`, `device.consumptionNote` (AC4's exact note), `device.save`, `device.cancel`; `defaultRooms.livingRoom`, `defaultRooms.bedroom`, `defaultRooms.kitchen`, `defaultRooms.bathroom`, `defaultRooms.hallway` (the five FR-22 defaults — sourced via i18n, not hardcoded English, per this project's i18n rule; German copy: Wohnzimmer / Schlafzimmer / Küche / Badezimmer / Flur).
  - [x] Add `settings.json`'s new `flat.structureLink` key (Task 9) to both locale files alongside the existing `flat.kwhBaselineLink`/`flat.tariffLink` keys.
  - [x] `flat-structure` is already registered in `client/src/lib/i18n.ts`'s `ns: [...]` array — no changes needed there (confirmed present).

### Frontend — tests

- [x] Task 11: `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx` (AC: 1, 2, 5, 6)
  - [x] Mock `useFlatStructure`/`useUpdateFlatStructure` (module-level `vi.mock`, per project testing rules — not `apiClient` directly); wrap in `MemoryRouter` + fresh `QueryClientProvider` per test; mock `react-i18next` per the standard pattern.
  - [x] `hasDefaultTemplate: true` + empty `rooms` → renders exactly 5 rooms with the FR-22 default names and the AC1 footer prompt.
  - [x] `hasDefaultTemplate: false` with seeded rooms → renders those rooms, no footer prompt.
  - [x] Renaming a room inline updates state only — no `updateFlatStructure` call fires until Save is clicked.
  - [x] Clicking a room row transitions to the Room view (`RoomEditor`) showing its PowerPoints; back button returns to the list.
  - [x] "+ Add Room" appends a new room row; "+ Add Power Point" (inside a Room view) appends a PowerPoint; "+ Add Device" opens `DeviceEditor` with `deviceKey: null`.
  - [x] Two PowerPoints (in the same or different rooms) with the same non-empty `plugId` → Save button disabled, `editor.plugIdConflict` text visible; clearing one plugId re-enables Save.
  - [x] Two PowerPoints both with an **empty** `plugId` → no conflict, Save stays enabled (regression guard for the "empty-string-is-not-a-duplicate" rule).
  - [x] Clicking Save (no conflicts) calls the mutation with a correctly-shaped `UpdateFlatStructureRequest` (no `key` fields, `sortOrder` by index, `consumptionApproach: 'None'` on every device); on mutation success shows the success confirmation.

- [x] Task 12: `client/src/features/flat-structure/components/DeviceEditor.test.tsx` (AC: 4)
  - [x] Save disabled with empty `name`; enabled once a name is entered; `device.consumptionNote` text is always visible; no consumption-approach choice UI is rendered (regression guard against accidentally building Epic 6.5's scope).

- [x] Task 13: `client/src/features/flat-structure/hooks/useFlatStructure.test.ts`, `useUpdateFlatStructure.test.ts` (AC: 1, 5)
  - [x] Mirror `useTariffs.test.ts`/`useCreateTariff.test.ts`'s shape: mock the API module, assert the query key and `enabled: !!flatId` gating, and assert `onSuccess` invalidates exactly `['flat-structure', flatId]` (not `['dashboard', flatId]` — see Task 3).

### Cross-cutting

- [x] Task 14: Self-review pass before marking ready for review
  - [x] Grep `client/src/features/flat-structure/` for any import from `@/features/settings/` — none should exist (VSA slice isolation; `flatId` only arrives as a prop from `SettingsPage.tsx`'s wrapper).
  - [x] Grep for any `fetch(` or raw API call bypassing `apiClient`/the hooks.
  - [x] Confirm no `key` field (the local draft identity field) ever appears in the body passed to `updateFlatStructure`.
  - [x] Confirm `DeviceEditor.tsx` has no EU-label/self-measured UI (Epic 6.5 scope guard).
  - [x] `npx tsc -b`, `npx vitest run`, `npm run lint` (all from `client/`) all green. No backend changes in this story — no `dotnet build`/`dotnet test` needed.

### Review Findings

- [x] [Review][Decision] `consumptionApproach` hardcoded to `'None'` on every save with no field carrying forward existing values — `toDraftRooms`/`DraftDevice` never capture a device's current `consumptionApproach`, `euLabelClass`, `euAnnualKwh`, `selfMeasuredKwh`, or `selfMeasuredPeriod`, and `toUpdateRequest` always sends `consumptionApproach: 'None'`. Once Epic 6 Story 6.5 ships EU-label/self-measured entry, simply opening this editor and clicking Save (e.g. to rename a room) will silently reset any device's already-configured consumption profile back to `None`. **Resolved (guard now)**: `DraftDevice` now carries `consumptionApproach`/`purchaseDate`/`euLabelClass`/`euAnnualKwh`/`selfMeasuredKwh`/`selfMeasuredPeriod` through untouched for existing devices (`toDraftRooms`/`toUpdateRequest` in `draftModel.ts`); `DeviceEditor.tsx` only defaults a *new* device (no prior `device` prop) to `'None'`/undefined. Regression tests added in `DeviceEditor.test.tsx`.
- [x] [Review][Patch] `hasInitializedRef` never resets when `flatId` changes [client/src/features/flat-structure/components/FlatStructureEditor.tsx:29-38] — switching flats without unmounting the editor leaves the draft showing the previous flat's stale rooms. **Fixed**: replaced the boolean ref with `initializedFlatIdRef` tracking the last-initialized `flatId`; re-initializes (and resets `view`/`saveError`/`saveSuccess`) whenever `flatId` changes, while still guarding against background refetches for the same flat clobbering in-progress edits.
- [x] [Review][Patch] No non-empty validation for Room/PowerPoint names before Save [client/src/features/flat-structure/components/FlatStructureEditor.tsx:67-71] — `DeviceEditor` disables Save on blank `name`, but Room and PowerPoint name inputs have no equivalent guard, allowing blank names through to the backend's `NotEmpty()` validator (the exact round-trip 400 this story's Device guard was meant to avoid). **Fixed**: added `hasBlankName` helper in `draftModel.ts`; Save is now also disabled (with a new `editor.blankNameError` inline message, both locales) whenever any room or power point name is blank.
- [x] [Review][Patch] `saveSuccess` banner never clears after further edits post-save [client/src/features/flat-structure/components/FlatStructureEditor.tsx:57-59] — the stale "Structure saved." message stays visible while new unsaved edits accumulate. **Fixed**: `handleRenameRoom`/`handleAddRoom`/`handleUpdateRoom` now clear `saveSuccess` on every edit.
- [x] [Review][Patch] New button in `FlatSettingsCard.tsx` missing `type="button"` [client/src/features/settings/components/FlatSettingsCard.tsx:131] — every other new button in this diff explicitly sets it. **Fixed**.
- [x] [Review][Patch] `useFlatStructure.test.ts` doesn't assert the literal TanStack Query key array [client/src/features/flat-structure/hooks/useFlatStructure.test.ts] — `useUpdateFlatStructure.test.ts` explicitly asserts `queryKey: ['flat-structure', flatId]` on invalidation; the query-side test only checks the API call and `enabled` gating, not the cache key itself. **Fixed**: test now asserts `queryClient.getQueryData(['flat-structure', 'flat-1'])` resolves to the mocked response.

- [x] [Review][Defer] No delete affordance for rooms/power points/devices [client/src/features/flat-structure/components/RoomEditor.tsx, PowerPointEditor.tsx, FlatStructureEditor.tsx] — deferred, out of this story's AC scope (only Add controls specified); real gap once users start editing existing structures.
- [x] [Review][Defer] Plug-ID conflict banner doesn't identify which power points conflict [client/src/features/flat-structure/components/draftModel.ts:76-82] — deferred, single generic message satisfies AC6 as written; per-conflict detail is a UX nice-to-have.
- [x] [Review][Defer] Stale view-state dead-end when `view.type` references a missing room/powerPoint/device key [client/src/features/flat-structure/components/FlatStructureEditor.tsx:124-155] — deferred, currently unreachable with no delete path yet; will need a guard once delete (above) is implemented.
- [x] [Review][Defer] `mutate`'s `onError` discards the actual server error detail with no logging [client/src/features/flat-structure/components/FlatStructureEditor.tsx:60-66] — deferred, spec only requires an inline banner ("do not silently swallow"), which is met; richer error surfacing/logging is a follow-up.
- [x] [Review][Defer] No optimistic concurrency control (no version/ETag) for concurrent edits across sessions/tabs [client/src/features/flat-structure/api/flatStructureApi.ts] — deferred, architectural change spanning frontend and backend, out of scope for this frontend-only story.
- [x] [Review][Defer] `flatId` undefined (e.g. an account with zero flats) silently renders an empty "0 rooms" editor with no explanatory state [client/src/features/flat-structure/components/FlatStructureEditor.tsx:77-122] — deferred, plausible-but-rare edge case; reachability depends on whether the app ever allows a user with no flat to reach this screen.

## Dev Notes

### Draft-state-until-Save is a deliberate deviation from this codebase's usual "persist per field" pattern

Every other editable surface in this app (`TariffForm`, `FlatSettingsCard`, `PlannedAnnualSpendSection`) persists each change independently via its own mutation. AC1 ("no database write occurs until the user saves") and AC5/AC6 (a single save action, gated by validation) require the opposite here: the whole nested Room→PowerPoint→Device tree is edited as local state and flushed in one `PUT` (Task 4/5). Do not reach for `react-hook-form` + `useFieldArray` for this — no existing pattern in this codebase manages a tree this deep with RHF, and the "buffer everything, one explicit save" requirement doesn't map cleanly onto RHF's per-field submit model anyway. Plain `useState` with the `DraftRoom[]` shape (Task 4) is simpler and consistent with how this codebase already handles other local-before-save edits (e.g. `PlannedAnnualSpendSection`'s `dirty` flag).

### Drill-down navigation, not accordion expand — resolved ambiguity

AC2's prose ("expands to reveal its PowerPoints") reads like an accordion, but the authoritative UX artifact for this exact story — `_bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/mockups/flat-structure-editor.html` — shows three distinct phone frames with explicit back-navigation chrome (`‹ Flat Structure`, `‹ Living Room`), not an inline-expanding list. Given four hierarchy levels (Flat→Room→PowerPoint→Device) on a 375px mobile viewport, true accordion nesting would be unusably deep. This story implements "expand" as **in-component drill-down** (a `view` state machine inside `FlatStructureEditor.tsx`, not react-router routes — the whole tree is one continuous unsaved editing session, so there's no reason to put intermediate levels behind URL routes). Only the top-level list's back button leaves via `navigate('/settings')`; Room/Device views go back via local state.

### Mock's Frame 3 ("Add Device — choose approach") is Epic 6 Story 6.5, not this story

The mock file bundles preview UI for later stories in the same HTML document (a common pattern in this project's UX artifacts — see also Frame 4, which is Story 5.2's flat-switcher dropdown, already shipped, included here only for visual continuity). AC4's actual requirements for this story are narrower: `Name`/`Type`/`Manufacturer`/`Model` text fields, a static note, `ConsumptionApproach` hardcoded to `None`. Building Frame 3's "Smart plug connected / Estimated usage" choice cards now would be scope creep into Story 6.5's territory — resist the temptation, the mock shows it because it's a nice reference for later, not a Task in this list.

### No new "Smart Power Strip" concept in the frontend either

Confirmed by Story 5.3's Dev Notes (backend): there is no `StripOutlet` entity — a strip is just a `PowerPoint` with a `plugId` and multiple `Device` rows. `PowerPointEditor.tsx` needs no strip-specific mode, toggle, or extra field; AC3 falls out of the existing generic PowerPoint→Devices rendering for free once a user adds more than one Device under a plugId-bearing PowerPoint.

### VSA slice isolation — the one easy mistake in this story

`client/src/features/flat-structure/` is its own slice per `architecture.md`'s file tree and `project-context.md`'s "VSA slice isolation" rule ("cross-slice hook imports are forbidden"). `flatId` is not available inside this slice via any hook of its own — it must arrive as a prop, sourced by a wrapper in `SettingsPage.tsx` (which legitimately owns `useUserSettings`), exactly like the existing `TariffSettingsRoute` wrapper does for the `tariffs` slice (Task 9). Do not import `useUserSettings` from `@/features/settings/...` inside `flat-structure/`.

### Enum-as-string wire format (carried over from Story 5.3)

Story 5.3 established `JsonStringEnumConverter` on both the response-serialization path and the request-deserialization path in the backend — `ConsumptionApproach`/`SelfMeasuredPeriod` are strings over the wire (`"None"`, `"EuLabel"`, `"SelfMeasured"`, `"Daily"`, `"Weekly"`), not integers. Frontend types must be string literal unions matching those exact PascalCase values (case-sensitive on the backend's `JsonStringEnumConverter` default settings).

### plugId empty-string normalization must match the backend exactly

Story 5.3's Review Findings fixed a bug where `plugId: ""` was treated as a real value by the duplicate check. This story's frontend conflict-detection (Task 5) and payload-mapping (Task 4) must apply the same rule client-side: trim, treat empty as "unassigned," never flag two blank PowerPoints as conflicting.

### Project Structure Notes

- New files: `client/src/features/flat-structure/api/flatStructureApi.ts`; `hooks/useFlatStructure.ts`, `useUpdateFlatStructure.ts`; `components/FlatStructureEditor.tsx`, `RoomEditor.tsx`, `PowerPointEditor.tsx`, `DeviceEditor.tsx` — all inside the already-scaffolded (currently `.gitkeep`-only) `client/src/features/flat-structure/` directory, matching `architecture.md`'s file tree exactly. Co-located test files per component/hook (project convention — no separate test folder).
- Modified files: `client/src/features/settings/SettingsPage.tsx` (new `structure` route + `FlatStructureSettingsRoute` wrapper); `client/src/features/settings/components/FlatSettingsCard.tsx` (new pill link); `client/src/locales/en-US/flat-structure.json`, `client/src/locales/de-DE/flat-structure.json` (populated from empty placeholders); `client/src/locales/en-US/settings.json`, `client/src/locales/de-DE/settings.json` (new `flat.structureLink` key).
- No changes to `client/src/lib/i18n.ts` — `flat-structure` namespace is already registered in the `ns: [...]` array.
- No backend changes — Story 5.3 (done) already shipped both endpoints this story consumes.
- Follows the mandatory VSA feature-folder shape (`components/`, `hooks/`, `api/` all present, no `schemas/` needed here since this story doesn't use zod/react-hook-form — see Dev Notes on why RHF wasn't used).

### Testing standards

- Frontend only — no backend test changes.
- Vitest + `@testing-library/react`, `globals: true`; co-located `.test.tsx`/`.test.ts`.
- Mock `flatStructureApi.ts` at the module level for component tests (per project rule: mock API modules, not `apiClient`); mock the two hooks directly where a component test only cares about rendering behavior, not query wiring (mirrors how `TariffList.test.tsx`/similar tests are structured — check that file for the exact mocking shape if unsure, though it wasn't loaded for this story's context).
- Query selectors: by role/label/text — no `data-testid`.
- Highest-value tests: the default-template pre-population (AC1), the drill-down navigation, the plugId-conflict gating (AC6, including the empty-string non-conflict regression guard), and the payload-shape assertion on Save (no `key` fields, correct `sortOrder`, `consumptionApproach: 'None'`).

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-5-multi-flat-management-flat-structure.md#Story 5.4] — authoritative AC text (verbatim, reproduced above).
- [Source: _bmad-output/planning-artifacts/epics/requirements-inventory.md#FR-21, #FR-22] — FR-21 (hierarchy + plugId + Smart Power Strip semantics), FR-22 (five default room names, verbatim: living room, bedroom, kitchen, bathroom, hallway).
- [Source: _bmad-output/planning-artifacts/architecture.md#Structure Patterns, #Naming Conventions] — `flat-structure/` feature folder shape (`components/`, `hooks/`, `api/`); file list `FlatStructureEditor.tsx`, `RoomEditor.tsx`, `PowerPointEditor.tsx`, `DeviceEditor.tsx`, `useFlatStructure.ts`, `useUpdateFlatStructure.ts`, `flatStructureApi.ts`; locale file `flat-structure.json` per locale; TanStack Query key `['flat-structure', flatId]`; frontend naming conventions (PascalCase components, camelCase hooks/API files, `on{Event}`/`handle{Event}`).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/mockups/flat-structure-editor.html] — the four-frame mock (main Room list, Room detail, Add Device choose-approach [Epic 6.5 scope, not this story], flat-switcher dropdown [Story 5.2, already shipped]); exact visual tokens (glass card: `background: rgba(255,255,255,0.07)`, `border: 1px solid rgba(255,255,255,0.12)`, `border-radius: 16px`, `backdrop-filter: blur(20px) saturate(180%)`; blue accent `#60a5fa`; pill button shape) — reuse these tokens via inline `style` objects, matching `FlatSettingsCard.tsx`/`TariffForm.tsx`'s existing approach (no Tailwind config for custom tokens per this project's Tailwind v4 rules).
- [Source: _bmad-output/implementation-artifacts/5-3-flat-structure-backend-rooms-power-points-and-devices.md] — the two API endpoints this story consumes (`GET`/`PUT /api/v1/flats/{flatId}/structure`), exact response/request DTO shapes, the `hasDefaultTemplate`-is-a-top-level-field resolution, the "no `StripOutlet` entity" resolution, the enum-as-string wire convention, and the `plugId` empty-string normalization fix (Review Findings) — all directly consumed/mirrored by this story's frontend.
- [Source: _bmad-output/implementation-artifacts/5-2-flat-switcher-add-flat-and-deletion-ui.md#Dev Notes] — confirms `['flat-structure', flatId]` as the query-key family this story is responsible for wiring up.
- [Source: client/src/features/tariffs/components/TariffForm.tsx, TariffList.tsx; client/src/features/tariffs/hooks/useCreateTariff.ts, useTariffs.ts; client/src/features/tariffs/api/tariffApi.ts] — reference patterns for API module shape, hook shape, loading/error/skeleton states, and inline-style visual tokens (this story's closest sibling feature).
- [Source: client/src/features/settings/SettingsPage.tsx, components/FlatSettingsCard.tsx, components/FlatBaselineEdit.tsx] — `TariffSettingsRoute` wrapper pattern (the exact precedent this story's `FlatStructureSettingsRoute` follows); `FlatSettingsCard`'s existing pill-link pattern; `FlatBaselineEdit`'s full-page-with-back-button layout convention.
- [Source: client/src/lib/apiClient.ts] — base fetch wrapper (`/api/v1` prefix already applied — paths passed to `apiClient` must not repeat it).
- [Source: client/src/lib/i18n.ts] — confirms `flat-structure` namespace already registered; no changes needed there.
- [Source: _bmad-output/project-context.md#React / TanStack Query, #Forms, #i18n, #VSA slice isolation, #Critical Don't-Miss Rules] — query key tuple convention, `isPending` vs `isLoading`, invalidation-before-close convention, i18n namespace-per-feature rule, VSA cross-slice-import prohibition (the one real trap in this story).

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

None — implementation proceeded without needing debug logging; `npx tsc -b`, `npx vitest run` (248 tests, 41 files), and `npm run lint` (oxlint, pre-existing unrelated warnings in `router.tsx` only) all passed on the first full run after implementation.

### Completion Notes List

- Implemented the full `flat-structure` VSA slice: API client (`flatStructureApi.ts`), TanStack Query hooks (`useFlatStructure`, `useUpdateFlatStructure`), draft-state model (`draftModel.ts`), and four components (`FlatStructureEditor`, `RoomEditor`, `PowerPointEditor`, `DeviceEditor`) implementing in-component drill-down navigation (Flat→Room→Device), per Dev Notes.
- Draft-state-until-Save (AC1/AC5/AC6): all edits are buffered in local `useState<DraftRoom[]>`, initialized exactly once from server data via a `hasInitializedRef` guard in a `useEffect`; the whole tree is flushed in a single `PUT` on explicit Save. No `react-hook-form` used, per Dev Notes guidance.
- Default template population (AC1): when `hasDefaultTemplate === true` and `rooms` is empty, seeds five i18n-sourced default rooms (living room, bedroom, kitchen, bathroom, hallway) and shows the `editor.defaultTemplateNote` footer prompt; the prompt is suppressed once real data is present.
- Smart Power Strip (AC3) and no new backend concept: confirmed no dedicated "strip" UI is needed — `PowerPointEditor` already renders one row per Device under any PowerPoint (with or without a `plugId`), matching Story 5.3's "a strip is just a PowerPoint with a plugId and multiple Devices" resolution.
- Device editor (AC4) intentionally excludes the Epic 6.5 "choose approach" UI — only `Name`/`Type`/`Manufacturer`/`Model` fields, a static consumption note, and `ConsumptionApproach: 'None'` hardcoded in the payload mapping. A regression-guard test asserts no EU-label/self-measured choice UI renders.
- plugId conflict detection (AC6): computed on every render across all PowerPoints in all rooms, trimming and ignoring empty strings before checking for duplicates (mirrors the backend's Story 5.3 empty-string normalization fix); Save is disabled and an inline error shown while a conflict exists.
- Save payload mapping strips all client-only `key` fields, recomputes `sortOrder` from array index, and normalizes empty optional strings (`type`/`manufacturer`/`model`/`plugId`) to `undefined` rather than persisting `""`.
- Wired the entry point via a `FlatStructureSettingsRoute` wrapper in `SettingsPage.tsx` (mirrors the existing `TariffSettingsRoute` pattern) and a third pill (`flat.structureLink`) in `FlatSettingsCard.tsx`; `flatId` is passed as a prop — no cross-slice hook imports into `flat-structure/`.
- Populated both `flat-structure.json` locale files (previously empty placeholders) and added `flat.structureLink` to both `settings.json` locale files; `flat-structure` namespace was already registered in `i18n.ts`.
- Self-review pass (Task 14): confirmed no `@/features/settings` imports inside `flat-structure/`, no raw `fetch()` calls (all API access via `apiClient`/hooks), no `key` field ever reaches the request payload, and `DeviceEditor.tsx` has no EU-label/self-measured UI.
- Full verification: `npx tsc -b` clean; `npx vitest run` — 248/248 tests passing across 41 files (17 new tests added across `useFlatStructure.test.ts`, `useUpdateFlatStructure.test.ts`, `DeviceEditor.test.tsx`, `FlatStructureEditor.test.tsx`); `npm run lint` clean (pre-existing `router.tsx` warnings only, unrelated to this story). No backend changes — Story 5.3's endpoints were consumed as-is.

### File List

**New files:**
- `client/src/features/flat-structure/api/flatStructureApi.ts`
- `client/src/features/flat-structure/hooks/useFlatStructure.ts`
- `client/src/features/flat-structure/hooks/useFlatStructure.test.ts`
- `client/src/features/flat-structure/hooks/useUpdateFlatStructure.ts`
- `client/src/features/flat-structure/hooks/useUpdateFlatStructure.test.ts`
- `client/src/features/flat-structure/components/draftModel.ts`
- `client/src/features/flat-structure/components/FlatStructureEditor.tsx`
- `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx`
- `client/src/features/flat-structure/components/RoomEditor.tsx`
- `client/src/features/flat-structure/components/PowerPointEditor.tsx`
- `client/src/features/flat-structure/components/DeviceEditor.tsx`
- `client/src/features/flat-structure/components/DeviceEditor.test.tsx`

**Modified files:**
- `client/src/features/settings/SettingsPage.tsx` (new `structure` route + `FlatStructureSettingsRoute` wrapper)
- `client/src/features/settings/components/FlatSettingsCard.tsx` (new `structureLink` pill)
- `client/src/locales/en-US/flat-structure.json` (populated from empty placeholder)
- `client/src/locales/de-DE/flat-structure.json` (populated from empty placeholder)
- `client/src/locales/en-US/settings.json` (added `flat.structureLink`)
- `client/src/locales/de-DE/settings.json` (added `flat.structureLink`)
