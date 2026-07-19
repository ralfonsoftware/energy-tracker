---
baseline_commit: 8ca731390cb51b0543a3a7af9c6a769d23c1424c
---

# Story 9.6: Consistent Treatment for Unmeasured Devices — Standalone vs. Sub-Device

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want a standalone device with no consumption data configured to be visually flagged the same way an unconfigured Smart Power Strip sub-device already is,
so that I don't lose track of a device just because it isn't on a strip.

## Acceptance Criteria

1. **Given** standalone devices with `approach === 'None'` and no plug are silently excluded from the Room Card device list entirely (Story 7.3 AC2), while the identical "no measurement configured" state on a Smart Power Strip sub-device shows a dimmed row plus a "Go to settings" hint (AC7) — a real UX inconsistency flagged during Story 7.3's review (`deferred-work.md:309`), **when** implemented in `RoomCard.tsx` and `partitionAndSortDevices`, **then** `isNoneApproachNonStrip` devices are no longer filtered out of the grid entirely — they render as the approved ghost card (name dimmed at `opacity: 0.45`, hint text in the tertiary text token, "Configure consumption profile" pill button), sized and positioned as a normal sibling in the existing responsive device grid (Story 8.4).
2. **Given** the ghost card's "Configure consumption profile" button, **when** clicked, **then** it deep-links via Story 9.5's `?powerPointId=` mechanism directly to that device's edit screen — not just the room list.
3. **Given** a room where every device is a standalone `None`-approach device (the pre-existing "Direct consumption" fallback path), **when** rendered, **then** the existing `isDirectConsumptionOnly` fallback behavior is preserved unchanged — this story only affects rooms with at least one measured/estimated device alongside unmeasured standalone ones.
4. **Given** the decomposition API's `DeviceDecomposition` payload currently has no field carrying a standalone device's PowerPoint id (only `deviceId`, which for a standalone device is the *Device*'s id, not its PowerPoint's — see Dev Notes "The real gap this story must close" below), so AC2's deep link is structurally impossible without a backend change, **when** implemented, **then** `DeviceDecomposition` gains a `powerPointId` field, populated for every device (single-measured, standalone/estimated/none, and smart-strip), and the ghost card's deep link uses this field — not `deviceId`.

## Tasks / Subtasks

- [x] Task 1: Add `PowerPointId` to the backend `DeviceDecomposition` contract (AC: 4)
  - [x] In `api/Features/Decomposition/DecompositionModels.cs:12-15`, change the `DeviceDecomposition` record from `public record DeviceDecomposition(Guid DeviceId, string Name, decimal Kwh, decimal Cost, AttributionApproach Approach, bool IsSmartStrip, IReadOnlyList<SubDeviceDecomposition>? SubDevices);` to insert a new `Guid PowerPointId` parameter immediately after `DeviceId`: `public record DeviceDecomposition(Guid DeviceId, Guid PowerPointId, string Name, decimal Kwh, decimal Cost, AttributionApproach Approach, bool IsSmartStrip, IReadOnlyList<SubDeviceDecomposition>? SubDevices);`. Do **not** touch `SubDeviceDecomposition` — sub-devices are out of scope for this story (they already have their own "Go to settings" mechanism from Story 7.3/9.5).
  - [x] In `api/Features/Decomposition/DecompositionEngine.cs:92-94` (single measured device via plug, inside the `pp.PlugId is not null && pp.Devices.Count == 1` branch), update the constructor call to pass `pp.PowerPointId` as the second positional argument: `new DeviceDecomposition(device.DeviceId, pp.PowerPointId, device.Name, kwh, cost, AttributionApproach.Measured, IsSmartStrip: false, SubDevices: null)`.
  - [x] In `api/Features/Decomposition/DecompositionEngine.cs:113-114` (the standalone-devices loop, `else` branch where `pp.PlugId is null`) — **this is the critical site for AC2/AC4** — update the constructor call the same way: `new DeviceDecomposition(device.DeviceId, pp.PowerPointId, device.Name, kwh, cost, approach, IsSmartStrip: false, SubDevices: null)`. `pp` is already in scope here (the outer `foreach (var pp in room.PowerPoints)` loop) — no new query or lookup needed.
  - [x] In `api/Features/Decomposition/DecompositionEngine.cs:221-223` (`BuildSmartStripDecomposition`, end of method), update the constructor call: `new DeviceDecomposition(pp.PowerPointId, pp.PowerPointId, pp.Name, stripMeasuredTotal, subDeviceCostSum, AttributionApproach.Measured, IsSmartStrip: true, subDevices)`. Both `DeviceId` and the new `PowerPointId` are `pp.PowerPointId` here — this preserves the exact existing value/behavior Story 9.5 already depends on (`deviceId` for a strip already **is** the PowerPoint id); do not change what `SmartStripCard`'s consumer code receives as `deviceId`.

- [x] Task 2: Backend regression test for the new field (AC: 4)
  - [x] In `api.Tests/Features/Decomposition/DecompositionEngineTests.cs`, add a test (follow the existing `SeedRoomAsync`/`SeedPowerPointAsync`/`SeedDeviceAsync` helper pattern, e.g. mirror `ComputeAsync_MeasuredSingleDevicePowerPoint_SumsPlugKwhAndAppliesTariff` at `:84-103`) that seeds a standalone `ConsumptionApproach.None` device on a PowerPoint with no `plugId`, calls `ComputeAsync`, and asserts `result.Rooms.Single().Devices.Single().PowerPointId == pp.PowerPointId` (not the device's own id). This is the regression that pins the exact gap this story closes — without it, a future refactor could silently regress `PowerPointId` back to `DeviceId` for this branch and the frontend deep link would break silently (wrong id, no crash).
  - [x] Optionally (recommended, not required) add the same `PowerPointId` assertion to one existing measured-device test and one smart-strip test to confirm all three construction sites were updated consistently — e.g. extend the assertions in `ComputeAsync_MeasuredSingleDevicePowerPoint_SumsPlugKwhAndAppliesTariff` (`:98-103`) with `device.PowerPointId.ShouldBe(pp.PowerPointId)`.

- [x] Task 3: Add `powerPointId` to the frontend `DeviceDecomposition` type (AC: 4)
  - [x] In `client/src/features/decomposition/api/decompositionApi.ts:16-24`, add `powerPointId: string` to the `DeviceDecomposition` type, immediately after `deviceId: string`.

- [x] Task 4: Add the two new `decomposition` i18n keys (AC: 1)
  - [x] In `client/src/locales/en-US/decomposition.json`, add to the existing `"roomCard": { "directConsumption": "Direct consumption" }` block: `"unmeasuredHint": "Configure consumption profile to include this device in Decomposition"` and `"configureProfile": "Configure consumption profile"`. These are voice-matched verbatim to `client/src/locales/en-US/flat-structure.json:41-42`'s existing `device.consumptionNote`/`device.configureProfile` strings, per D-46's explicit instruction to add a **new key in the `decomposition` namespace**, not a cross-namespace reference (this project's per-feature-namespace i18n convention, `project-context.md`).
  - [x] In `client/src/locales/de-DE/decomposition.json`, add the same two keys to `roomCard`, copied verbatim from `client/src/locales/de-DE/flat-structure.json:41-42`: `"unmeasuredHint": "Verbrauchsprofil konfigurieren, um dieses Gerät in die Aufschlüsselung aufzunehmen"` and `"configureProfile": "Verbrauchsprofil konfigurieren"`.

- [x] Task 5: Create the `UnmeasuredDeviceCard` ghost-card component (AC: 1, 2)
  - [x] Create `client/src/features/decomposition/components/UnmeasuredDeviceCard.tsx`, following the existing sibling-component pattern (`DeviceCard.tsx`, `SmartStripCard.tsx` — one component per visual variant, `useTranslation('decomposition')`, no default export). Props: `type Props = { device: DeviceDecomposition; onConfigure: () => void }` — matches `SmartStripCard`'s prop shape exactly; the id is bound at the `RoomCard` call site via closure, the same pattern Story 9.5 established (do not add a `powerPointId` prop directly on this component).
  - [x] Sizing: base the markup on `DeviceCard.tsx`'s "compact/estimated" variant (`DeviceCard.tsx:44-57`, i.e. `rounded-card border border-glass-border bg-glass-surface px-3.5 py-2.5`) per D-46's explicit "sized like `DeviceCard`'s compact/estimated variant" instruction — do not use the larger "Measured" variant sizing (`DeviceCard.tsx:24-36`).
  - [x] Dimming: apply `opacity-[0.45]` to the card's outer container (the same class and the same mechanism `SmartStripCard.tsx:46` already uses for its unconfigured sub-device rows — D-46 explicitly cites this as "the same value already used"). Do not dim only the name text — matching the cited precedent means dimming the whole card, which satisfies AC1's "name dimmed" as a natural consequence.
  - [x] Content: device name (`text-body-sm text-white`, matching `DeviceCard.tsx:47`'s estimated-variant name styling); hint text using the new `roomCard.unmeasuredHint` key — **use the Tailwind class `text-text-tertiary`** (the actual utility class for this project's tertiary text token, `--color-text-tertiary` in `client/src/index.css:31`; the epic prose's "`text-tertiary`" is shorthand for this token, not a literal class name — do not write a nonexistent `text-tertiary` class, see e.g. `KpiTile.tsx:16` for correct usage); a `"Configure consumption profile"` pill button using the new `roomCard.configureProfile` key, styled identically to `SmartStripCard.tsx:54-60`'s `configureHint` button (`min-h-11 min-w-11 rounded-pill border border-white/[0.18] bg-white/10 px-2.5 py-0.5 text-caption text-white`, `type="button"`, `onClick={onConfigure}`).
  - [x] Do **not** render any kWh/cost figure — D-46 is explicit that this would be dishonest for a device with zero measurement or estimate (`kwh`/`cost` are always `0` for a `None`-approach device per `DecompositionEngine.cs`'s `EstimateDailyKwh` default case).

- [x] Task 6: Wire `UnmeasuredDeviceCard` into `RoomCard.tsx`'s grid (AC: 1, 2, 3)
  - [x] In `client/src/features/decomposition/components/RoomCard.tsx:21-30`, rewrite `partitionAndSortDevices` to stop excluding `isNoneApproachNonStrip` devices from the returned list — instead partition into three groups and concatenate: `measured` (unchanged: `approach === 'Measured' || isSmartStrip`, sorted by `kwh` descending), `estimated` (unchanged predicate but must now explicitly exclude `isNoneApproachNonStrip` devices too: `approach !== 'Measured' && !isSmartStrip && !isNoneApproachNonStrip(device)`, sorted by `kwh` descending), and a new `unmeasured` group (`isNoneApproachNonStrip(device)`, sorted by `name` — ascending alphabetical, since every device in this group has `kwh === 0`/`cost === 0` by construction, so sorting by value is meaningless; use `(a, b) => a.name.localeCompare(b.name)` for a deterministic, testable order). Return `[...measured, ...estimated, ...unmeasured]`.
  - [x] In the render block (`RoomCard.tsx:50-62`), add a third branch to the existing `device.isSmartStrip ? <SmartStripCard .../> : <DeviceCard .../>` ternary: render `<UnmeasuredDeviceCard key={device.deviceId} device={device} onConfigure={() => onConfigureDevice(device.powerPointId)} />` when `isNoneApproachNonStrip(device)` is true. Render it as a **plain grid sibling with no wrapper `<div className="md:col-span-full">`** — unlike the smart-strip branch, this card must size and position like a normal `DeviceCard`, per AC1.
  - [x] Do **not** change the `SmartStripCard` call site's existing `onConfigureDevice(device.deviceId)` (`RoomCard.tsx:55`) — that was correctly established by Story 9.5 (a smart strip's `deviceId` already **is** its PowerPoint id, confirmed by `DecompositionEngine.cs:222`/`:221-223`'s `new DeviceDecomposition(pp.PowerPointId, pp.PowerPointId, ...)`). Only the new `UnmeasuredDeviceCard` call site uses the new `device.powerPointId` field, because for a standalone device `deviceId` and `powerPointId` are genuinely different values (device's own id vs. its containing PowerPoint's id) — using `deviceId` there would silently deep-link to the wrong (nonexistent) PowerPoint.
  - [x] Do **not** change `isDirectConsumptionOnly` (`RoomCard.tsx:34-35`) — it already evaluates `room.devices.every(isNoneApproachNonStrip)` against the full, unfiltered `room.devices` array, independent of `partitionAndSortDevices`. This satisfies AC3 automatically; no code change is needed there.

- [x] Task 7: Frontend tests (AC: 1, 2, 3)
  - [x] Create `client/src/features/decomposition/components/UnmeasuredDeviceCard.test.tsx` following `DeviceCard.test.tsx`/`SmartStripCard.test.tsx`'s conventions (`vi.mock('react-i18next', ...)` returning the raw key, Vitest globals, query by role/text). Cover: renders the device name; renders the `roomCard.unmeasuredHint` text; renders the `roomCard.configureProfile` button; clicking the button calls `onConfigure`; does **not** render any kWh/cost text (assert e.g. `screen.queryByText(/kWh/)` is absent).
  - [x] In `client/src/features/decomposition/components/RoomCard.test.tsx`, add `powerPointId: 'pp-1'` as a new default field to the `makeDevice()` helper (`:10-21`) — use a value distinct from the `deviceId` default (`'device-1'`) so tests can't accidentally pass by coincidence.
  - [x] Replace `RoomCard_NoneApproachNonStripDevice_IsExcludedFromList` (`:85-97`) — its premise (exclusion) is now wrong — with a new test, e.g. `RoomCard_NoneApproachNonStripDevice_RendersGhostCardAndDeepLinksOnConfigureClick`: render a room with one `Measured` device and one `approach: 'None', isSmartStrip: false` device (set a distinct `powerPointId`, e.g. `'pp-ghost'`, on the ghost fixture); assert the ghost device's name **is** in the document (inverse of the old test); assert `roomCard.unmeasuredHint` text is present; click `roomCard.configureProfile` and assert `onConfigureDevice` was called with `'pp-ghost'` (the `powerPointId`, not the `deviceId`) — this is the test that actually pins AC2/AC4, not just AC1's visibility change.
  - [x] `RoomCard_AllDevicesNoneApproachNonStrip_RendersDirectConsumptionFallback` (`:107-119`) needs **no change** — it already asserts the fallback path renders and the device name is absent; that remains true because the fallback branch bypasses the grid (and therefore the ghost card) entirely, independent of this story's changes.
  - [x] Add a new ordering test, e.g. `RoomCard_MixedDevicesWithUnmeasured_RendersUnmeasuredGroupAfterMeasuredAndEstimated`, extending the pattern of `RoomCard_MixedDevices_RendersMeasuredGroupBeforeEstimatedGroup` (`:35-61`) with one additional `approach: 'None', isSmartStrip: false` device, asserting it appears after both the measured and estimated device names via `compareDocumentPosition`.
  - [x] Add a new test confirming the ghost card does not get the full-width wrapper, mirroring `RoomCard_RegularDevice_DoesNotGetFullWidthSpanClass` (`:140-149`): assert `screen.getByText(<ghost device name>).closest('.md\\:col-span-full')` is not in the document.

## Dev Notes

### The real gap this story must close — why AC2 was structurally impossible before Task 1

Story 9.5 built the `?powerPointId=` deep-link mechanism and wired it up for the Smart Power Strip "Go to settings" chip — but it worked *only* by a coincidence specific to smart strips: `DecompositionEngine.cs:221-223`'s `BuildSmartStripDecomposition` constructs a strip's `DeviceDecomposition` with `pp.PowerPointId` as its `DeviceId` (confirmed at `DecompositionEngine.cs:222`, and documented in Story 9.5's own Dev Notes). For a **standalone** device (this story's subject), that coincidence does not hold: `DecompositionEngine.cs:113-114`'s standalone-device branch constructs `new DeviceDecomposition(device.DeviceId, device.Name, ...)` — `DeviceId` here is the *Device* entity's own id (`Device.DeviceId`), which is a completely different value from its containing PowerPoint's id (`Device.PowerPointId`, confirmed as an existing FK column on the `Device` entity in `api/Data/Entities/Device.cs:19`). There is currently no field anywhere in the `DecompositionResponse` payload that carries a standalone device's PowerPoint id. Task 1/4 close this gap by adding a genuine `PowerPointId` field to `DeviceDecomposition`, populated correctly for all three construction sites (single-measured, standalone, smart-strip) — this is a small, mechanical, low-risk backend change (one field addition, three call-site updates), not a new feature.

### Why the smart-strip call site must NOT change

It is tempting, once a real `PowerPointId` field exists, to also switch `RoomCard.tsx:55`'s `onConfigureDevice(device.deviceId)` to `onConfigureDevice(device.powerPointId)` "for consistency." **Do not do this.** For a smart strip, both values are identical (`pp.PowerPointId`, per Task 1's third bullet) — switching would be a no-op behaviorally, but it would touch already-shipped, already-tested Story 9.5 code and its existing regression test (`RoomCard.test.tsx:172-201`, `RoomCard_SmartStripConfigureHintClicked_CallsOnConfigureDevice`, which asserts `toHaveBeenCalledWith('d1')`) for zero functional benefit, and would require simultaneously updating that test's fixture. This story's scope is the standalone-device ghost card only; leave the smart-strip path exactly as Story 9.5 left it.

### Existing code being modified — current state and what's preserved

- **`RoomCard.tsx`** (`:17-30`): `isNoneApproachNonStrip` (the predicate) is unchanged — reused as-is by both `isDirectConsumptionOnly` (unchanged) and the rewritten `partitionAndSortDevices` (changed: no longer filters this predicate out, instead groups it). `isDirectConsumptionOnly`'s "all devices are unmeasured standalone → show the direct-consumption single-line summary, not the grid" behavior (Story 7.3, AC3 of this story) is untouched — it evaluates against `room.devices` directly, never touching `partitionAndSortDevices`'s output.
- **`SmartStripCard.tsx`**: zero changes, same as Story 9.5 left it. Not touched by this story at all.
- **`DeviceCard.tsx`**: zero changes — read only, as a sizing reference for the new `UnmeasuredDeviceCard`.
- **`decompositionApi.ts`**: additive only (`powerPointId: string` field) — no existing field renamed or removed, so no other consumer of `DeviceDecomposition` (there are none outside the `decomposition` feature slice) is affected.

### Testing Standards Summary

- Frontend: Vitest + `@testing-library/react`, `globals: true` (no `describe`/`it`/`expect` imports). Query by role/text, not CSS class or `data-testid`, per `project-context.md`.
- Mock `react-i18next` returning the raw translation key (`t: (k: string) => k`) — the established pattern in every `decomposition` component test file (`RoomCard.test.tsx:6-8`, `SmartStripCard.test.tsx:9-11`).
- Backend: xUnit + EF Core `InMemory`, `Shouldly` assertions, following `DecompositionEngineTests.cs`'s existing `SeedRoomAsync`/`SeedPowerPointAsync`/`SeedDeviceAsync` helper pattern (`:21-57`) — do not write a new helper, reuse these.

### Project Structure Notes

- No new i18n namespace — `decomposition.json` already exists in both locales; this story only adds keys.
- New file: `client/src/features/decomposition/components/UnmeasuredDeviceCard.tsx` (+ its test file) — follows this feature's established one-component-per-visual-variant convention (`DeviceCard.tsx`, `SmartStripCard.tsx`).
- No VSA slice-isolation concerns — this story stays entirely within the `decomposition` feature slice on the frontend and the `Decomposition` feature folder on the backend. No cross-slice imports introduced.
- No database schema change — `Device.PowerPointId` already exists as a column (`api/Data/Entities/Device.cs:19`); this story only threads an already-persisted value through to a DTO that didn't previously carry it.

### Previous Story Intelligence (Story 9.5)

- Story 9.5 (immediately prior) built the `?powerPointId=` deep-link mechanism this story depends on (`FlatStructureEditor.tsx`'s `useSearchParams`-driven initial view resolution) and explicitly flagged in its own Dev Notes: *"Story 9.6 (`RoomCard.tsx`'s ghost-card treatment for standalone unmeasured devices) depends on this story's `?powerPointId=` mechanism ... this story's Task 2/3 plumbing must remain stable for 9.6 to build on."* Confirmed stable — no changes needed to `FlatStructureEditor.tsx`, `draftModel.ts`, or `DecompositionTab.tsx` for this story; they already correctly resolve any `?powerPointId=` value (from a ghost card or a smart-strip chip) to the right room/PowerPoint view.
- Story 9.5 also changed `RoomCard.tsx`'s `onConfigureDevice` prop type from `() => void` to `(powerPointId: string) => void` and changed the `SmartStripCard` call site to forward `device.deviceId`. This story adds a second call site (the new `UnmeasuredDeviceCard`) using the same prop, forwarding `device.powerPointId` instead — no prop-type change needed, `onConfigureDevice` already accepts a string id from either source.
- Story 9.5's test suite result: 385/385 passing, clean `tsc`/lint. Use the same full-suite verification bar for this story (`npx vitest run`, `npx tsc --noEmit`, `npm run lint` in `client/`; `dotnet test` for the backend project).

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-9-unified-save-affordance-fix-and-pre-epic-10-hardening.md#Story 9.6] — original epic AC text (verbatim source for AC1-3; AC4 added during story creation to close the discovered backend gap).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/.decision-log.md#D-46] — approved ghost-card design decision (opacity, sizing, copy voice-matching, i18n namespace rule).
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:309] — "Deferred from: party-mode review of story 7.3" entry this story resolves; should be marked closed once shipped.
- [Source: client/src/features/decomposition/components/RoomCard.tsx:17-30,50-62] — `isNoneApproachNonStrip`, `partitionAndSortDevices`, and the render ternary this story modifies.
- [Source: client/src/features/decomposition/components/DeviceCard.tsx:44-57] — "compact/estimated" sizing variant `UnmeasuredDeviceCard` should visually match.
- [Source: client/src/features/decomposition/components/SmartStripCard.tsx:46,54-60] — precedent for the `opacity-[0.45]` dimming mechanism and the `configureHint` pill button styling to replicate.
- [Source: client/src/features/decomposition/api/decompositionApi.ts:16-24] — `DeviceDecomposition` type this story extends with `powerPointId`.
- [Source: api/Features/Decomposition/DecompositionModels.cs:12-15] — `DeviceDecomposition` record this story extends.
- [Source: api/Features/Decomposition/DecompositionEngine.cs:92-94,113-114,221-223] — the three construction sites requiring the new `PowerPointId` argument; `:113-114` is the critical fix enabling AC2.
- [Source: api/Data/Entities/Device.cs:19] — confirms `Device.PowerPointId` already exists as a persisted FK, so no schema/migration change is needed.
- [Source: client/src/locales/en-US/flat-structure.json:41-42, de-DE/flat-structure.json:41-42] — source copy for the two new `decomposition` namespace keys (voice-matched verbatim per D-46).
- [Source: client/src/index.css:31] — `--color-text-tertiary` token; `text-text-tertiary` is the correct Tailwind class (not literal `text-tertiary`).
- [Source: _bmad-output/implementation-artifacts/9-5-flatstructureeditor-deep-link-addressability.md] — previous story; established the `?powerPointId=` mechanism and the `onConfigureDevice` prop signature this story reuses.
- [Source: api.Tests/Features/Decomposition/DecompositionEngineTests.cs:21-57] — seeding helper pattern to reuse for Task 2's new regression test.
- [Source: client/src/features/decomposition/components/RoomCard.test.tsx:10-21,85-97] — `makeDevice()` helper and the test whose premise this story inverts.
- [Source: _bmad-output/project-context.md#i18n] — per-feature-namespace i18n convention (new key in `decomposition`, not a cross-namespace reference to `flat-structure`).

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

None — one iteration required: the first draft of `RoomCard_UnmeasuredDevice_DoesNotGetFullWidthSpanClass` seeded a room with only the ghost device, which tripped `isDirectConsumptionOnly`'s single-line fallback instead of the grid; fixed by adding a real `Measured` device alongside it, matching every other single-device-assertion test in the file. All other tests passed on first run.

### Completion Notes List

- Closed the real structural gap found during story creation: `DeviceDecomposition` had no field carrying a standalone device's PowerPoint id (only `deviceId`, which for a standalone device is the *Device*'s own id). Added `PowerPointId` to the record (`DecompositionModels.cs`) and populated it at all three construction sites in `DecompositionEngine.cs` — single-measured-device, standalone (the critical site for this story), and smart-strip (where it equals `pp.PowerPointId`, same as the pre-existing `DeviceId` value, preserving Story 9.5's behavior unchanged).
- `RoomCard.tsx`'s `partitionAndSortDevices` no longer filters `isNoneApproachNonStrip` devices out — they're now a third group (`unmeasured`, sorted alphabetically since `kwh`/`cost` are always 0 for this group) appended after `measured`/`estimated`, rendered via the new `UnmeasuredDeviceCard` component as a normal grid sibling (no `md:col-span-full` wrapper).
- `UnmeasuredDeviceCard.tsx` created following the `DeviceCard`/`SmartStripCard` one-component-per-variant convention: `DeviceCard`'s compact/estimated sizing, `opacity-[0.45]` on the whole card (same mechanism as `SmartStripCard`'s unconfigured sub-device rows), no kWh/cost shown, and a "Configure consumption profile" pill styled identically to `SmartStripCard`'s `configureHint` button. Deep-links via `onConfigureDevice(device.powerPointId)` — deliberately not `device.deviceId`, since those two values genuinely differ for a standalone device.
- Left the `SmartStripCard` call site (`onConfigureDevice(device.deviceId)`) and `isDirectConsumptionOnly` completely untouched, exactly as scoped in Dev Notes — both already correct and out of this story's blast radius.
- Added two new `decomposition` i18n keys (`roomCard.unmeasuredHint`, `roomCard.configureProfile`) in both locales, copied verbatim from `flat-structure.json`'s existing `device.consumptionNote`/`device.configureProfile` strings per D-46.
- Updated four existing test fixtures (`DeviceCard.test.tsx`, `SmartStripCard.test.tsx`, `RoomCard.test.tsx`, `DecompositionTab.test.tsx`) to add the new required `powerPointId` field, replaced the now-inverted `RoomCard_NoneApproachNonStripDevice_IsExcludedFromList` test with one asserting the ghost card renders and deep-links correctly, and added ordering/wrapper regression tests plus a dedicated `UnmeasuredDeviceCard.test.tsx`.
- Full verification: `dotnet test api.Tests/api.Tests.csproj` — 362/362 passed; `npx vitest run` (client/) — 391/391 passed (60 test files); `npx tsc --noEmit` — clean; `npm run lint` — clean (only pre-existing, unrelated `router.tsx` fast-refresh warnings).

### File List

- `api/Features/Decomposition/DecompositionModels.cs` (modified)
- `api/Features/Decomposition/DecompositionEngine.cs` (modified)
- `api.Tests/Features/Decomposition/DecompositionEngineTests.cs` (modified)
- `client/src/features/decomposition/api/decompositionApi.ts` (modified)
- `client/src/features/decomposition/components/UnmeasuredDeviceCard.tsx` (new)
- `client/src/features/decomposition/components/UnmeasuredDeviceCard.test.tsx` (new)
- `client/src/features/decomposition/components/RoomCard.tsx` (modified)
- `client/src/features/decomposition/components/RoomCard.test.tsx` (modified)
- `client/src/features/decomposition/components/DeviceCard.test.tsx` (modified)
- `client/src/features/decomposition/components/SmartStripCard.test.tsx` (modified)
- `client/src/features/decomposition/components/DecompositionTab.test.tsx` (modified)
- `client/src/locales/en-US/decomposition.json` (modified)
- `client/src/locales/de-DE/decomposition.json` (modified)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified)

## Change Log

- 2026-07-18: Implemented ghost-card treatment for standalone `None`-approach devices in `RoomCard.tsx`, matching the existing Smart Power Strip unconfigured-sub-device pattern. Closed a discovered backend gap by adding `PowerPointId` to the decomposition API's `DeviceDecomposition` contract so the ghost card's "Configure consumption profile" button can deep-link via Story 9.5's `?powerPointId=` mechanism. Added/updated regression tests across backend and frontend. Status → review.

### Review Findings

- [x] [Review][Patch] `UnmeasuredDeviceCard_Rendered_DoesNotRenderKwhOrCostFigure` only asserts the literal text `/kWh/` is absent, so it would not catch a bare numeric kwh/cost value being wired in without units — weak regression guard [client/src/features/decomposition/components/UnmeasuredDeviceCard.test.tsx]
- [x] [Review][Patch] `UnmeasuredDeviceCard`'s configure button className includes an extra `shrink-0` not present in `SmartStripCard`'s otherwise-identical button, deviating from the spec's explicit "styled identically" / exact class-list instruction [client/src/features/decomposition/components/UnmeasuredDeviceCard.tsx]
- [x] [Review][Patch] No test asserts the ghost card's outer `opacity-[0.45]` dimming class is actually applied — AC1's dimming requirement is implemented correctly but unguarded by any regression test [client/src/features/decomposition/components/UnmeasuredDeviceCard.tsx]
- [x] [Review][Defer] Ghost card's whole outer container (including the CTA button) is dimmed to 0.45 opacity with no distinct hover/focus state, making the button look inert [client/src/features/decomposition/components/UnmeasuredDeviceCard.tsx] — deferred, pre-existing (mirrors `SmartStripCard`'s existing unconfigured-row treatment exactly, D-46-mandated)
- [x] [Review][Defer] en-US `unmeasuredHint` references "Decomposition" as a proper noun while de-DE uses a generic phrase with no equivalent proper-noun reference — the two locales don't say quite the same thing [client/src/locales/en-US/decomposition.json, client/src/locales/de-DE/decomposition.json] — deferred, pre-existing (copied verbatim from `flat-structure.json`'s existing strings per this story's explicit instruction)
