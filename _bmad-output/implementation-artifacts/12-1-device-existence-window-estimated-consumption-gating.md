---
baseline_commit: 163516d
---

# Story 12.1: Device Existence Window — Estimated-Consumption Gating

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want a device I've just added to not be counted as consuming power before I actually installed it (and a device I've decommissioned to stop being counted afterward),
so that my Decomposition figures for past and future periods stay accurate as my device inventory changes.

## Acceptance Criteria

1. **Given** `Device.cs`/`DeviceConfiguration.cs` today have no existence-window columns, **when** implemented, **then** `Device` gains two nullable columns — `InUseSince` (`DateTimeOffset?`) and `DecommissionedDate` (`DateTimeOffset?`) — configured via Fluent API only in `DeviceConfiguration.cs`; the generated migration sets no default value for either (both remain `null` for every pre-existing row).

2. **Given** `DecompositionEngine.cs`'s standalone-device estimate path (`ComputeAsync`'s final `else` branch, currently computing `kwh = dailyEstimate * dayCount` and `cost = CostForDailySeries(_ => dailyEstimate)` uniformly across the whole `[startDate, endDate]` query window, with no per-day date check at all), **when** a Device has `ConsumptionApproach != None`, **then** its estimated daily kWh/cost is counted only for days within `[InUseSince, DecommissionedDate]` (either bound open-ended when unset) intersected with `[startDate, endDate]` — days outside that window contribute exactly `0` kWh/cost for that device, using the same per-day `ToLocalMidnight(date)` comparison basis `TariffResolution.Resolve` already uses for tariff-date comparisons in this same file (see Dev Notes).

3. **Given** a Device with both `InUseSince` and `DecommissionedDate` left unset, **when** Decomposition is computed, **then** behavior for that device is unchanged from today — full-period inclusion — for every existing row (all of which have both fields `null` after the migration), preserving backward compatibility with no migration-driven behavior change.

4. **Given** Smart Power Strip sub-devices (`BuildSmartStripDecomposition`'s pool-math branch), **when** a sub-device has `InUseSince`/`DecommissionedDate` set, **then** this story does **not** apply the day-window clamp there — sub-device shares continue using whole-period estimates exactly as today; date-sliced strip pooling is an explicit, out-of-scope follow-up, not a silently-unhandled case (see Dev Notes).

5. **Given** the Flat Structure editor's Device form (`DeviceEditor.tsx`), **when** adding or editing a Device, **then** two optional date fields are available — "In use since" (pre-filled with today's date as a suggested default only when *adding* a new Device; editable/clearable) and "Decommissioned" (no suggested default; editable/clearable) — see "Gap found during story creation" below for why both fields are in scope, not just the one the epic text names.

6. **Given** `DecompositionEngineTests.cs`, **when** run, **then** new tests cover: `InUseSince` mid-period (partial inclusion — device counts only from that date forward), `DecommissionedDate` mid-period (partial inclusion — device stops counting after that date), both set with the active window fully inside the query period, both set with the device's entire window falling *outside* the query period (zero contribution, no exception), neither set (full inclusion — regression guard), and a Smart Power Strip sub-device with both dates set (pool math byte-for-byte unaffected, per AC4's exclusion).

### Gap found during story creation

Two corrections to the epic's original text (`epics/epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md#Story 12.1`):

**1. The UI field scope is widened from "In use since" only to both fields.** The epic's own AC5 text names only an "In use since" field, but FR-52 (`prd.md:391-397`) explicitly requires *both* `InUseSince` **and** `DecommissionedDate` to be user-settable ("The user can specify when a Device started consuming power... and, optionally, when it was decommissioned"). This isn't a cosmetic omission: this codebase's architecture uses hard deletes throughout (AD-8) — if `DecommissionedDate` were only ever set some other way, the only way to "retire" a device would be to delete it outright, which would erase it from *all* historical Decomposition queries, not just future ones. That directly defeats this story's own stated purpose ("a device I've decommissioned... my Decomposition figures for past... periods stay accurate"). A user decommissioning a device on 2026-08-15 needs Decomposition queries for July 2026 to still show it as active. Both fields are therefore in scope for the UI (AC5), not just one.

**2. The Epic 11 retrospective (2026-08-01) flagged a decimal-rounding-policy gap in this exact code path** (`EstimateDailyKwh`'s `EuAnnualKwh / 365m` / `SelfMeasuredKwh / 7m` divisions, originally deferred from Story 7.1's review) **for folding into Epic 12.** It is **deliberately not folded into this story** — introducing rounding into `EstimateDailyKwh` would put it in direct tension with this story's own AC3 ("behavior... unchanged from today," a literal backward-compatibility guarantee for the ~100% of existing rows with no existence-window set). Bundling an unrelated precision change into a story whose whole point is proving *zero* behavior change for the untouched case adds regression-test ambiguity for no benefit. Deferred to Story 12.2 instead, which also touches `DecompositionEngine.cs`'s estimate math but for room-attribution, not for a backward-compatibility-sensitive existence-window feature — flag this explicitly when Story 12.2 is created so the fold-in isn't lost a second time.

## Tasks / Subtasks

- [ ] Task 1: Add `InUseSince`/`DecommissionedDate` to the `Device` entity and schema (AC: #1)
  - [ ] 1.1 In `api/Data/Entities/Device.cs`, add `public DateTimeOffset? InUseSince { get; set; }` and `public DateTimeOffset? DecommissionedDate { get; set; }` alongside the existing `PurchaseDate` property (same nullable-`DateTimeOffset` shape, no new using directives needed).
  - [ ] 1.2 In `api/Data/Configurations/DeviceConfiguration.cs`, add `builder.Property(d => d.InUseSince).IsRequired(false);` and `builder.Property(d => d.DecommissionedDate).IsRequired(false);` — mirror the existing `builder.Property(d => d.PurchaseDate).IsRequired(false);` line exactly (line 19), no `HasColumnType` needed (plain `datetimeoffset`, same as `PurchaseDate`).
  - [ ] 1.3 Generate the migration from `api/`: `dotnet ef migrations add AddDeviceExistenceWindow`. Confirm the generated migration only adds two nullable columns with no `defaultValue` — do not hand-edit; if EF Core's generated migration includes an unexpected default, that's a signal the entity/config edit was wrong, not something to patch in the migration file.
  - [ ] 1.4 Run `dotnet ef database update` locally to verify the migration applies cleanly. Per Story 11.3's precedent, do **not** assume a fresh schema — this project's dev DB has real data; two new nullable columns with no default and no index should apply without incident, but confirm before marking done.

- [ ] Task 2: Gate the standalone-device estimate path by existence window (AC: #2, #3, #4)
  - [ ] 2.1 In `api/Features/Decomposition/DecompositionEngine.cs`, add a private static helper `IsDeviceActiveOn(Device device, DateTimeOffset date)` returning `(device.InUseSince is null || device.InUseSince <= date) && (device.DecommissionedDate is null || date <= device.DecommissionedDate)`. Place it near the other private static helpers (`ToLocalMidnight`, `EstimateDailyKwh`).
  - [ ] 2.2 In the standalone-device `else` branch (currently lines 107-117), replace `var kwh = dailyEstimate * dayCount;` with a per-day active-day count: loop `startDate..endDate` via `ToLocalMidnight(date)` (the same conversion `CostForDailySeries` already uses for its own per-day loop) and sum `dailyEstimate` only for days where `IsDeviceActiveOn(device, ToLocalMidnight(date))` is true. Do not introduce a second date-iteration convention — reuse `ToLocalMidnight` for every comparison in this file, exactly as `TariffResolution.Resolve(tariffs, ToLocalMidnight(date))` already does two lines above in `CostForDailySeries`.
  - [ ] 2.3 Replace `var cost = approach == AttributionApproach.None ? 0m : CostForDailySeries(_ => dailyEstimate);` with `CostForDailySeries(date => approach != AttributionApproach.None && IsDeviceActiveOn(device, ToLocalMidnight(date)) ? dailyEstimate : 0m)` — this makes the existing `CostForDailySeries` lambda date-aware for the first time in this branch (today it ignores the `date` parameter entirely, which is exactly why this bug exists).
  - [ ] 2.4 Do **not** touch `BuildSmartStripDecomposition` (lines 169-225) — per AC4, sub-device shares keep using whole-period estimates. Do not add a "TODO" comment referencing this as unfinished; the epic explicitly scopes it out as a deliberate, tracked follow-up (`epic-12-...md#Story 12.1`, last AC), not an oversight in this story.

- [ ] Task 3: Add both date fields to `DeviceEditor.tsx` (AC: #5)
  - [ ] 3.1 In `client/src/features/flat-structure/components/DeviceEditor.tsx`, add two `useState` fields: `inUseSinceRaw` (defaults to `device?.inUseSince ? toLocalDateString(parseLocalDate(device.inUseSince)) : (device ? '' : toLocalDateString(new Date()))` — today's date only when adding, i.e. `device` is `undefined`) and `decommissionedDateRaw` (defaults to `device?.decommissionedDate ? toLocalDateString(parseLocalDate(device.decommissionedDate)) : ''` — no suggested default either way). Import `toLocalDateString`/`parseLocalDate`/`toLocalMidnightIsoString` from `@/lib/localDate` (already exists, hardened by Story 11.11 — reuse verbatim, do not reinvent local-midnight ISO conversion).
  - [ ] 3.2 Add two `<input type="date">` fields (matching this file's existing `inputClass`/`inputStyle`/`sectionLabelClass` conventions) below the existing Model field and above the consumption-approach section — one for "in use since," one for "decommissioned." Both optional, both clearable (an empty string clears to `undefined` on save).
  - [ ] 3.3 In `handleSave`, convert each non-empty raw value via `toLocalMidnightIsoString(raw)` before passing to `onSave(...)` (matching `TariffForm.tsx`'s post-Story-11.11 create-flow pattern exactly — local-midnight ISO string, never a hardcoded UTC-midnight suffix); pass `undefined` when the field is empty.
  - [ ] 3.4 Add the two new fields to `DraftDevice` (`draftModel.ts`) as `inUseSince?: string` / `decommissionedDate?: string`, thread them through `toDraftRooms` (read from `DeviceResponse`) and `toRoomInput` (write to `DeviceInput`) exactly like the existing `purchaseDate` field is threaded (lines 62 and 94 in `draftModel.ts`) — same pattern, two more fields.
  - [ ] 3.5 Add `inUseSince?: string` / `decommissionedDate?: string` to `DeviceResponse` and `DeviceInput` in `client/src/features/flat-structure/api/flatStructureApi.ts`, alongside the existing `purchaseDate` field in both types.

- [ ] Task 4: Thread the two fields through the backend DTOs and write path (AC: #1, #5)
  - [ ] 4.1 Add `DateTimeOffset? InUseSince, DateTimeOffset? DecommissionedDate` to `DeviceResponse` and `DeviceInput` in `api/Features/FlatStructure/FlatStructureModels.cs`, alongside the existing `PurchaseDate` parameter in both records (same position convention: after `PurchaseDate`, before `ConsumptionApproach`).
  - [ ] 4.2 In `api/Features/FlatStructure/UpdateFlatStructureFunction.cs`, add `InUseSince = d.InUseSince, DecommissionedDate = d.DecommissionedDate,` to the `new Device { ... }` object initializer (line 118 area, alongside the existing `PurchaseDate = d.PurchaseDate,`), and add `d.InUseSince, d.DecommissionedDate,` to the `new DeviceResponse(...)` positional-argument list (line 173 area, alongside `d.PurchaseDate,` — matching the record's new parameter order from Task 4.1).
  - [ ] 4.3 In `api/Features/FlatStructure/GetFlatStructureFunction.cs`, add `d.InUseSince, d.DecommissionedDate,` to its own `new DeviceResponse(...)` call (line 64 area, same position as Task 4.2).
  - [ ] 4.4 In `api/Features/FlatStructure/UpdateFlatStructureValidator.cs`, add a new rule inside the `Devices` `ChildRules` block: `d.RuleFor(dv => dv.DecommissionedDate).GreaterThanOrEqualTo(dv => dv.InUseSince).When(dv => dv.InUseSince.HasValue && dv.DecommissionedDate.HasValue).WithMessage("decommissionedDate must not be before inUseSince.");` — a device can't be decommissioned before it was ever in use.

- [ ] Task 5: Add i18n keys (AC: #5)
  - [ ] 5.1 In `client/src/locales/en-US/flat-structure.json`'s `device` block (after `modelPlaceholder`, before `consumptionNote`), add `"inUseSinceLabel": "In use since (optional)"` and `"decommissionedDateLabel": "Decommissioned (optional)"`.
  - [ ] 5.2 In `client/src/locales/de-DE/flat-structure.json`'s equivalent block, add the matching German keys: `"inUseSinceLabel": "In Gebrauch seit (optional)"` and `"decommissionedDateLabel": "Außer Betrieb seit (optional)"`.

- [ ] Task 6: Test coverage (AC: #3, #6)
  - [ ] 6.1 In `api.Tests/Features/Decomposition/DecompositionEngineTests.cs`, extend the `SeedDeviceAsync` helper (lines 37-57) with two new optional parameters: `DateTimeOffset? inUseSince = null, DateTimeOffset? decommissionedDate = null`, passed through to the constructed `Device`. All existing call sites keep compiling unchanged (defaults preserve today's behavior).
  - [ ] 6.2 Add new tests: a device with `inUseSince` set to a date inside the query period (assert `Kwh`/`Cost` reflect only the active sub-range, computed by hand against `dailyEstimate × activeDayCount`); a device with `decommissionedDate` set inside the query period (same, inverse direction); both dates set with the whole window inside the query period; both dates set with the window entirely *before* or entirely *after* the query period (assert `Kwh == 0m` and `Cost == 0m`, no exception); a Smart Power Strip sub-device with both dates set (assert its computed share is byte-for-byte identical to an otherwise-identical sub-device with neither date set, proving AC4's exclusion holds).
  - [ ] 6.3 Confirm every existing `DecompositionEngineTests.cs` test (all of which seed devices with neither date set) continues to pass unmodified — this is the AC3 regression guard; do not add rounding or other changes to `EstimateDailyKwh` that would perturb these values (see "Gap found during story creation" #2).
  - [ ] 6.4 Add a `DeviceEditor.test.tsx` case verifying: adding a new device pre-fills "in use since" with today's local date and leaves "decommissioned" empty; editing an existing device with both fields already set displays them correctly; clearing either field and saving passes `undefined` (not an empty string) to `onSave`.
  - [ ] 6.5 Run `dotnet test` (from `api.Tests/` — no root `.sln` in this repo, per Story 11.3/11.4/11.5's established precedent) and `npm test -- --run` (from `client/`) and confirm both full suites pass with zero regressions.
  - [ ] 6.6 Run `npx tsc --noEmit` and `npm run lint` (both from `client/`) — clean, no `as any`/`@ts-ignore`.

## Dev Notes

### Why this story exists

Epic 12 (`epics/epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md`) is sourced from an architecture review and a brainstorming session (`_bmad-output/brainstorming/brainstorming-session-2026-08-01-14-56.md`), not the original PRD or a retrospective action item — the gap it closes is that `Device.PurchaseDate` (FR-29) was captured but never consulted by anything, and a device's Decomposition inclusion has always been all-or-nothing regardless of when it was actually installed or retired. This is the first of Epic 12's four stories; the Epic 11 retrospective (2026-08-01) explicitly gated Epic 12's start on the retro completing, per `sprint-status.yaml`'s own comment — that gate is now satisfied.

### Current state of `DecompositionEngine.cs`'s standalone-device path (read in full during story creation)

The exact code this story changes, `ComputeAsync`'s final branch inside the `foreach (var pp in room.PowerPoints)` loop (lines 107-117):

```csharp
else
{
    foreach (var device in pp.Devices)
    {
        var (approach, dailyEstimate) = ResolveStandaloneApproach(device);
        var kwh = dailyEstimate * dayCount;
        var cost = approach == AttributionApproach.None ? 0m : CostForDailySeries(_ => dailyEstimate);
        deviceDecompositions.Add(new DeviceDecomposition(
            device.DeviceId, pp.PowerPointId, device.Name, kwh, cost, approach, IsSmartStrip: false, SubDevices: null));
    }
}
```

`dayCount` (line 61: `endDate.DayNumber - startDate.DayNumber + 1`) is the *entire* query period's day count, applied uniformly — no per-day check exists today. `CostForDailySeries(_ => dailyEstimate)` (the `_` explicitly discards the `date` parameter) means the cost computation is also date-blind: it applies `dailyEstimate` on every day in the period regardless of whether the device existed that day. Both of these must become date-aware per AC2.

### The `ToLocalMidnight`/`TariffResolution.Resolve` precedent to reuse, not reinvent

`CostForDailySeries` (lines 63-73) already establishes the per-day comparison idiom this story must match:

```csharp
decimal CostForDailySeries(Func<DateOnly, decimal> dailyKwh)
{
    decimal cost = 0m;
    for (var date = startDate; date <= endDate; date = date.AddDays(1))
    {
        var tariff = TariffResolution.Resolve(tariffs, ToLocalMidnight(date));
        if (tariff is not null)
            cost += dailyKwh(date) * tariff.PricePerKwh;
    }
    return cost;
}
```

`ToLocalMidnight(DateOnly date)` (line 244-245) converts a `DateOnly` to a `DateTimeOffset` at local midnight in `AppTimeZone` (Europe/Berlin, hardcoded — see the file's own comment at lines 10-11 explaining no shared timezone utility exists project-wide by design). `IsDeviceActiveOn`'s comparisons (Task 2.1) must use this exact same conversion for every date it checks — do not compare a raw `DateOnly` against `Device.InUseSince`/`DecommissionedDate` directly, and do not introduce a second timezone-conversion path. This mirrors exactly how `TariffResolution.Resolve` is already called with a `ToLocalMidnight(date)` argument two lines above.

### Why `InUseSince <= date` (inclusive), matching `TariffResolution`'s own boundary semantics

`TariffResolution.Resolve`'s documented contract (Story 11.1) treats `ContractStartDate <= date` as inclusive — a tariff becomes active *on* its start date, not the day after. This story's `InUseSince <= date` follows the identical convention for consistency across this codebase's two "does X apply on day Y" comparisons: a device becomes active *on* its `InUseSince` date, and stops being active *the day after* its `DecommissionedDate` (i.e., `date <= DecommissionedDate` is still active, `date` one day later is not) — so `DecommissionedDate` is the *last* day the device counted, not the first day it stopped, matching FR-52's wording ("stops contributing... for days *after* that date").

### FR-52 vs. Epic 12's Story 12.1 AC — the exact discrepancy driving the "Gap found" section above

`prd.md:391-397` (FR-52) explicitly frames both `InUseSince` and `DecommissionedDate` as user-specifiable ("The user can specify when a Device started consuming power (`InUseSince`) and, optionally, when it was decommissioned (`DecommissionedDate`)"). The epic file's own Story 12.1 AC text only mentions adding an "In use since" field to the Device form, silent on how `DecommissionedDate` ever gets set by a real user action. Given this codebase's hard-delete architecture (AD-8, `architecture.md:205-206`) — deleting a `Device` from the Flat Structure editor removes it and its cascade-children permanently, including from every past Decomposition query — the only way `DecommissionedDate` can do its job (keep a retired device's *historical* consumption intact while excluding it going forward) is if the UI lets a user set it directly, without deleting the device. This story's Task 3/5 add both fields to close that gap.

### The decimal-rounding-policy fold-in — deliberately NOT in this story

The Epic 11 retrospective (`_bmad-output/implementation-artifacts/epic-11-retro-2026-08-01.md`, Action Items #1/#2) recorded Ralf's decision to fold a `deferred-work.md` item (Story 7.1 review, 2026-07-13: "No rounding policy applied to decimal divisions... before they reach the JSON response") into Epic 12's Story 12.1 or 12.2. This story deliberately defers it to **Story 12.2**, not this one — see "Gap found during story creation" #2 above for the reasoning (direct tension with AC3's backward-compatibility guarantee). Whoever creates Story 12.2 should re-surface this from `deferred-work.md` explicitly; it is not carried in Story 12.2's own epic text today, only in the retro doc and here.

### What NOT to touch

- `BuildSmartStripDecomposition` (lines 169-225) — AC4 explicitly excludes strip pool math from this story's clamp.
- `EstimateDailyKwh` (lines 227-234) and `ResolveStandaloneApproach` (lines 236-242) — untouched; this story only changes how their *output* is applied across days, not how the daily estimate itself is computed. (This is also why the rounding fold-in doesn't belong here — it would touch `EstimateDailyKwh` directly.)
- `TryComputeMainMeterTotal`/`BuildMainMeterDailySeries` (lines 247-289) — main-meter reconciliation is unrelated to per-device existence windows.
- `RoomEditor.tsx`, `PowerPointEditor.tsx` — this story only touches `DeviceEditor.tsx`.
- Any measured-device path (`pp.PlugId is not null` branches, lines 87-106) — existence windows apply only to the estimate-based standalone path per AC2's explicit scope (`ConsumptionApproach != None` devices without a plug). A plugged, measured device's existence window is out of scope for this story (its consumption is already zero for any day with no `SmartPlugDailyData` row — a different, already-existing "no data" ambiguity flagged separately in `deferred-work.md`'s Story 7.1 entry, explicitly not folded into this story since it concerns the *measured* path, not the *estimate* path this story touches).

### Testing Rules (from project context)

- Backend: xUnit + Shouldly (`.ShouldBe(...)`), EF Core `InMemory` provider — matches every existing test in `DecompositionEngineTests.cs`. `InMemory` doesn't enforce column types/precision, which is irrelevant here (no new constraint, no decimal-scale concern in this story).
- Frontend: Vitest, `@testing-library/react` + `@testing-library/user-event` — this file imports `vi`/`describe`/`it`/`expect`/`beforeEach` explicitly rather than relying on the project's `globals: true` convention; follow this file's existing import style, not the global-convention default.
- `dotnet test` must be run from `api.Tests/` — this repo has no root-level `.sln` (established since Story 11.3).
- Do not add a SQLite-tier (Story 11.12) test for this story — two new nullable columns with no unique/FK constraint don't need constraint-enforcement verification; `InMemory` is sufficient.

### Project Structure Notes

- Backend files touched: `api/Data/Entities/Device.cs`, `api/Data/Configurations/DeviceConfiguration.cs`, one new migration pair under `api/Data/Migrations/`, `api/Features/Decomposition/DecompositionEngine.cs`, `api/Features/FlatStructure/FlatStructureModels.cs`, `api/Features/FlatStructure/UpdateFlatStructureFunction.cs`, `api/Features/FlatStructure/GetFlatStructureFunction.cs`, `api/Features/FlatStructure/UpdateFlatStructureValidator.cs`.
- Frontend files touched: `client/src/features/flat-structure/components/DeviceEditor.tsx`, `client/src/features/flat-structure/components/draftModel.ts`, `client/src/features/flat-structure/api/flatStructureApi.ts`, `client/src/locales/en-US/flat-structure.json`, `client/src/locales/de-DE/flat-structure.json`.
- Test files touched: `api.Tests/Features/Decomposition/DecompositionEngineTests.cs`, `client/src/features/flat-structure/components/DeviceEditor.test.tsx`.
- No new files besides the generated migration pair — this story extends existing entities/DTOs/components rather than introducing new ones, consistent with how `PurchaseDate` (an existing nullable-date field on the same entity) is already threaded through every one of these same files.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md#Story 12.1] — epic-level AC and rationale; corrected above per "Gap found during story creation"
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md#FR-52] — the FR this story implements; source of the DecommissionedDate-UI-field correction
- [Source: _bmad-output/planning-artifacts/architecture.md:211-212] — AD-8b, the architecture decision already documenting this story's target entity/engine shape (`DeviceAssignmentPeriods` is Story 12.2's table, not this story's — do not create it here)
- [Source: _bmad-output/planning-artifacts/architecture.md:227] — `Devices` table entity-model row already listing `InUseSince`/`DecommissionedDate` as the target schema
- [Source: api/Data/Entities/Device.cs, api/Data/Configurations/DeviceConfiguration.cs] — entity/config to modify; current state read in full during story creation (11 properties, no existence-window columns yet)
- [Source: api/Features/Decomposition/DecompositionEngine.cs] — full file read during story creation; exact line references above for the standalone-device branch, `ToLocalMidnight`, `CostForDailySeries`, `EstimateDailyKwh`, `BuildSmartStripDecomposition`
- [Source: api/Shared/TariffResolution.cs] — the `<=`-inclusive boundary + deterministic-comparison precedent this story's `IsDeviceActiveOn` follows (Story 11.1)
- [Source: client/src/features/flat-structure/components/DeviceEditor.tsx] — full file read during story creation (325 lines); current field set, `handleSave` shape, no date-input precedent in this file today
- [Source: client/src/features/flat-structure/components/draftModel.ts] — `DraftDevice`/`toDraftRooms`/`toRoomInput`'s existing `purchaseDate` threading (lines 18, 62, 94), the exact pattern to replicate twice
- [Source: client/src/features/flat-structure/api/flatStructureApi.ts] — `DeviceResponse`/`DeviceInput` DTOs, current `purchaseDate` field position
- [Source: api/Features/FlatStructure/FlatStructureModels.cs, UpdateFlatStructureFunction.cs, GetFlatStructureFunction.cs] — backend DTO/write-path/read-path files, current `PurchaseDate` threading read in full
- [Source: api/Features/FlatStructure/UpdateFlatStructureValidator.cs] — existing validator shape, the `.When(...)` convention the new cross-field rule follows
- [Source: client/src/lib/localDate.ts] — `toLocalDateString`/`parseLocalDate`/`toLocalMidnightIsoString`, hardened by Story 11.11; reused verbatim, not reinvented, for the two new date fields
- [Source: client/src/features/tariffs/components/TariffForm.tsx:126] — the post-11.11 local-midnight-ISO write pattern this story's `DeviceEditor.tsx` change mirrors
- [Source: _bmad-output/implementation-artifacts/deferred-work.md — "Deferred from: code review of story-7.1 (2026-07-13)"] — source of the decimal-rounding-policy item explicitly deferred to Story 12.2, not this story
- [Source: _bmad-output/implementation-artifacts/epic-11-retro-2026-08-01.md] — the retrospective whose Epic 12 preparation folds motivate (and, per this story's own analysis, partially redirect) this story's scope
- [Source: api.Tests/Features/Decomposition/DecompositionEngineTests.cs] — existing `SeedDeviceAsync` helper (lines 37-57) to extend, existing test conventions to match
- [Source: client/src/features/flat-structure/components/DeviceEditor.test.tsx] — existing test file, `ComponentName_Scenario_ExpectedOutcome` naming convention

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
