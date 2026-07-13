---
baseline_commit: 1dbf4b225217db1d9d90864da0c8a182fb95f584
---

# Story 7.1: Decomposition Backend — Engine, API & Cost Attribution

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want the app to compute my consumption breakdown by room and device for any date range I choose, with attributed costs using the tariff that was active during each day,
so that I get accurate per-room and per-device kWh and cost figures that are consistent with my billing history.

## Acceptance Criteria

1. **Given** `DecompositionModels.cs` in `api/Features/Decomposition/`, **when** reviewed, **then** it defines: `DecompositionResponse` (record: `Period` [`StartDate`, `EndDate` as `DateOnly`], `TotalKwh` decimal, `TotalCost` decimal, `IsUnavailable` bool, `HasInterpolatedData` bool, `Residual` `ResidualItem`, `Rooms` `IReadOnlyList<RoomDecomposition>`); `ResidualItem` (record: `Kwh`, `Cost`, always present); `RoomDecomposition` (record: `RoomId`, `RoomName`, `Kwh`, `Cost`, `Devices` `IReadOnlyList<DeviceDecomposition>`); `DeviceDecomposition` (record: `DeviceId`, `Name`, `Kwh`, `Cost`, `Approach`, `IsSmartStrip` bool, `SubDevices` `IReadOnlyList<SubDeviceDecomposition>?`); `SubDeviceDecomposition` (record: `DeviceId`, `Name`, `Kwh`, `Cost`, `IsConfigured` bool, `IsUnconfigured` bool). No Data Annotation attributes on any class — Fluent API / plain records only, per this codebase's convention.

2. **Given** `DecompositionEngine.cs` processes a period with `SmartPlugDailyData` present, **when** `ComputeAsync(flatId, startDate, endDate, ct)` is called, **then**: for each `Device` whose `PowerPoint.PlugId` is set and is the *sole* Device on that `PowerPoint` (i.e. not a Smart Power Strip — see AC12), `Kwh` = sum of `SmartPlugDailyData.KwhValue` for that `PlugId` in the period, `Approach = Measured`; for a `Device` with `ConsumptionApproach = EuLabel`, daily estimate = `EuAnnualKwh ÷ 365`, projected across the period's day count, `Approach = EuLabel`; for a `Device` with `ConsumptionApproach = SelfMeasured`, per-day kWh is derived from `SelfMeasuredKwh` (`÷ 7` if `SelfMeasuredPeriod = Weekly`, unchanged if `Daily`), projected across the period's day count, `Approach = SelfMeasured`; a `Device` with `ConsumptionApproach = None` and no plug contributes `Kwh = 0`, `Approach = None`; devices are grouped into their `Room` via `PowerPoint.Room`.

3. **Given** a Smart Power Strip (a `PowerPoint` with `PlugId` set and more than one `Device` attached — see AC12 for the exact derivation rule, since no dedicated "strip" entity or flag exists in the data model), **when** its sub-devices are attributed, **then**: the strip's measured total kWh (sum of `SmartPlugDailyData.KwhValue` for the strip `PowerPoint`'s `PlugId` in the period) is split among its `Device` rows using the exact formula from `.decision-log.md` D-44 — `device_share = (device_estimated_kWh ÷ sum_of_all_configured_estimates) × strip_measured_total` for each configured device (`ConsumptionApproach != None`), where `device_estimated_kWh` is that device's own EU-label/self-measured daily estimate (from AC2's formulas) projected across the period, **not** a smart-plug measurement (sub-devices never have their own `PlugId` — only the strip `PowerPoint` does); unconfigured devices (`ConsumptionApproach = None`) share the remainder equally: `(strip_measured_total − sum_of_configured_shares) ÷ unconfigured_device_count`; `IsConfigured`/`IsUnconfigured` are set accordingly on each `SubDeviceDecomposition`; the strip's total exactly equals the sum of all sub-device shares within ±0.01 kWh rounding tolerance.

4. **Given** cost attribution for a period, **when** computing costs, **then** each day's kWh (per device, per room, and the flat total) is multiplied by the import tariff active on that calendar date (FR-13 period-accurate costing) and summed into device/room/total cost fields; all cost fields are `decimal` — no float or double. See AC13 for the exact resolution mechanism to use (the epic's literal "`TariffResolver` is called" wording does not match this codebase's actual established pattern — clarified there, do not call `TariffResolver.ResolveAsync` in a per-day loop).

5. **Given** `Residual` computation, **when** included in the response, **then** `Residual.Kwh = TotalKwh − sum(all device kWh)` (summed across every `RoomDecomposition.Devices` entry, including strip sub-devices); `Residual.Cost` uses the same period-accurate tariff resolution as AC4; the invariant `Residual.Kwh + attributed kWh = TotalKwh` holds within ±0.1 kWh for periods where `HasInterpolatedData = false`, and within ±1.0 kWh for periods where `HasInterpolatedData = true` (FR-27); `Residual.Kwh` may be zero but is always included in the response (FR-33).

6. **Given** a period with no `SmartPlugDailyData` for any plug in the flat, **when** `ComputeAsync` is called, **then** `DecompositionResponse.IsUnavailable = true`; `Rooms` is empty; `Residual` is still present with `Kwh = 0`, `Cost = 0`; `TotalKwh = 0`, `TotalCost = 0` (do not populate a real Main Meter total here — a non-zero `TotalKwh` alongside an empty `Rooms`/zeroed `Residual` would itself violate this story's own AC5 invariant and FR-34's "no partial figures for unavailable periods"); HTTP 200 is returned (not 404).

7. **Given** any `SmartPlugDailyData` row in the period has `IsInterpolated = true`, **when** building the response, **then** `HasInterpolatedData = true` on the response object.

8. **Given** `GET /api/v1/flats/{flatId}/decomposition?startDate={date}&endDate={date}`, **when** `GetDecompositionFunction.RunAsync` executes, **then**: `startDate`/`endDate` are required query params in `yyyy-MM-dd` format (`DateOnly.TryParseExact`); missing or unparsable dates return HTTP 400 Problem Details (`{ title: "Bad Request", status: 400, detail: "..." }`, matching this codebase's established anonymous-object error shape); `endDate < startDate` returns HTTP 400; the standard tenant-check pattern (`Guid.TryParse(flatId)` → 400 if invalid; `db.Flats.SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId)` → 403 if not found, exactly mirroring `GetDashboardFunction.cs`) enforces `flatId` belongs to the authenticated `userId`; a valid request returns HTTP 200 with `DecompositionResponse`.

9. **Given** `DecompositionEngineTests.cs` in `api.Tests/Features/Decomposition/`, **when** run, **then** tests cover: measured single-device attribution; EU label daily estimate; SelfMeasured daily estimate (both Daily and Weekly periods); Smart Power Strip proportional split (configured devices weighted by their own estimate, unconfigured equal-share of remainder); a Smart Power Strip where all sub-devices are unconfigured (equal split of the full strip total, no divide-by-zero); Residual = TotalKwh − attributed within ±0.1 kWh for a clean (non-interpolated) period; Residual = TotalKwh − attributed within ±1.0 kWh for a period containing interpolated data; a period with no `SmartPlugDailyData` sets `IsUnavailable = true`; `HasInterpolatedData = true` when any row is interpolated; `Residual.Kwh` is 0 but present when all kWh is attributed; the AC10 same-local-day duplicate-reading regression test; the AC12 `Measured` vs `EuLabel`/`SelfMeasured` `Approach` derivation test.

10. **Given a gap found during story creation** — `deferred-work.md`'s Story 6.4 review entry flags that `ReconciliationEngine.BuildMainMeterDailySeries`'s per-day allocation algorithm (the only established way this codebase computes a "Main Meter total for a period" from raw `MeterReading` rows) can theoretically double-count if two `MeterReading` rows land on the same local calendar day, because the unique index on `MeterReadings` (`IX_MeterReadings_FlatId_ReadingDate`, `MeterReadingConfiguration.cs:23-25`) is on the exact `DateTimeOffset` instant, not the calendar date — and this same computation is what this story's `TotalKwh` (epic AC2's "sum of all MainMeter daily readings for period") depends on, feeding directly into the FR-27 Residual invariant this story must satisfy — **when** `DecompositionEngine.cs` computes `TotalKwh`, **then**: it MUST reuse **both** the `BuildMainMeterDailySeries` day-allocation algorithm **and** the `TryComputeMainMeterTotal` coverage guard already established in `ReconciliationEngine.cs:64-103` (both duplicated verbatim into `DecompositionEngine.cs` as private static methods, per this codebase's own established convention of self-contained per-engine duplication — see `ReconciliationEngine.cs:13-15`'s comment and Story 6.4's Dev Notes mandating the same for `KpiCalculator`/`ReconciliationEngine`) — it must **not** be implemented as a naive `readings.Sum(r => r.KwhValue)` (physically meaningless, since `KwhValue` is a cumulative meter counter, not a per-period delta), and it must **not** skip the coverage guard (which would silently default any day outside actual reading coverage to `0`, under-counting `TotalKwh` and producing a falsely-passing Residual check); when the guard reports insufficient coverage (fewer than 2 readings, or the requested period isn't fully bounded by the first/last reading) for a period that otherwise has `SmartPlugDailyData` (so `IsUnavailable = false`), set `TotalKwh` = the sum of all attributed device `Kwh` for that period (i.e. `Residual.Kwh = 0` in this specific edge case) rather than asserting an unverifiable non-zero residual — this is a story-creation decision, not specified by any epic/PRD text, made because no Main Meter ground truth exists to compute a real residual against; a new regression test (`DecompositionEngineTests.TotalKwh_MultipleReadingsOnSameLocalCalendarDay_TelescopesCorrectlyWithoutDoubleCounting`) must seed 3+ `MeterReading` rows where two fall on the same local calendar day with monotonically increasing `KwhValue`, and assert `TotalKwh` equals exactly `last.KwhValue − first.KwhValue` for the covering period — proving (not just asserting) that the day-allocation algorithm's telescoping-sum property makes same-local-day duplicate readings safe for `TotalKwh` in practice, closing out the deferred-work.md item's "confirm/document... unreachable in practice" option with executable proof rather than leaving it an open question; a second new test (`DecompositionEngineTests.TotalKwh_InsufficientMeterReadingCoverage_FallsBackToZeroResidualNotFalseNonZero`) must cover the insufficient-coverage fallback.

11. **Given a gap found during story creation** — the epic's AC4 text says "`TariffResolver` is called for cost attribution," but `api/Shared/TariffResolver.cs`'s `ResolveAsync` has **zero current callers** anywhere in the codebase (confirmed by full-repo grep) — every existing per-day cost computation (`KpiCalculator.Compute`) instead preloads the flat's `Tariff` list once (`GetDashboardFunction.cs`'s `db.Tariffs.Where(t => t.FlatId == flatGuid).OrderBy(t => t.ContractStartDate).ToListAsync(ct)`) and resolves in-memory per interval via a private static helper (`KpiCalculator.cs:155-164`'s `ResolveTariff(tariffs, date)`) — calling the DB-backed `TariffResolver.ResolveAsync` inside a per-day loop would issue one SQL query per day of the period, an N+1 pattern with no precedent in this codebase — **when** `DecompositionEngine.cs` attributes cost, **then** it loads `Tariff` rows for the flat once per `ComputeAsync` call (ordered by `ContractStartDate`) and resolves each day's tariff via a duplicated private static helper matching `KpiCalculator.ResolveTariff`'s exact logic (latest `Tariff` with `ContractStartDate <= date`), consistent with the established in-memory pattern; `TariffResolver.ResolveAsync` is not called anywhere in this story's new code. **This is a deliberate, acknowledged deviation from `project-context.md:423`**, which states `TariffResolver.ResolveAsync(flatId, date, ct)` is "the only correct path" for period-accurate costing — that line predates `KpiCalculator`'s in-memory precedent becoming the codebase's actual only *live* pattern (verified via full-repo grep, not assumption) and was never updated to reflect it. Flag `project-context.md:423` for a human correction alongside this story rather than silently following a written rule that no real code in the repo actually follows.

12. **Given a gap found during story creation** — the epic's `DeviceDecomposition.IsSmartStrip`/`SubDevices` fields and AC3's "Smart Power Strip" concept assume a resolvable strip identity, but `api/Data/Entities/Device.cs`/`PowerPoint.cs` have **no** `IsSmartStrip`/`DeviceKind` flag, no parent/child self-reference on `Device`, and no separate strip entity — `PlugId` lives only on `PowerPoint` (`PowerPoint.cs:8`), and a `PowerPoint` already natively holds a collection of `Device`s (`Devices` navigation) — **when** `DecompositionEngine.cs` builds `DeviceDecomposition` entries, **then**: a `PowerPoint` is treated as a Smart Power Strip if and only if `PlugId != null && Devices.Count > 1` (per D-43's "Power Point → Smart Power Strip → multiple Strip Outlets" model); in that case, one `DeviceDecomposition` entry per room-level strip is synthesized with `DeviceId = PowerPoint.PowerPointId` (there is no single `Device` row representing the whole strip) and `Name = PowerPoint.Name`, `IsSmartStrip = true`, `Approach` = a **new** `AttributionApproach` enum value (`Measured`/`EuLabel`/`SelfMeasured`/`None` — defined fresh in `DecompositionModels.cs`, **not** a reuse of the stored `Device.ConsumptionApproach` enum, since `Measured` has no equivalent stored value there) set to `Measured`, and `SubDevices` populated per AC3; the strip's constituent `Device` rows do **not** also appear as separate top-level `DeviceDecomposition` entries in the room's `Devices` list — only nested inside `SubDevices`. A `PowerPoint` with `PlugId != null && Devices.Count == 1` is the simple case (D-43's "Smart Plug → one Device"): that single `Device` gets `IsSmartStrip = false`, `Approach = Measured`, `Kwh` = 100% of the `PowerPoint`'s measured total (per AC2). For every other `Device` (no plug on its `PowerPoint`, or its `PowerPoint` has 0 devices — out of scope for a `DeviceDecomposition` entry, see Dev Notes), `Approach` is derived from `ConsumptionApproach` directly (`EuLabel`/`SelfMeasured`/`None`) — never from a raw cast of the storage enum, since the response enum's `Measured` member has no storage-enum counterpart.

## Tasks / Subtasks

- [x] Task 1: `DecompositionModels.cs` (AC: 1, 12)
  - [x] Create `api/Features/Decomposition/DecompositionModels.cs` with the records exactly as specified in AC1, plus a new `AttributionApproach` enum (`Measured`, `EuLabel`, `SelfMeasured`, `None`) — this is intentionally distinct from `EnergyTracker.Api.Data.Entities.ConsumptionApproach` (which has no `Measured` member).
    ```csharp
    namespace EnergyTracker.Api.Features.Decomposition;

    public enum AttributionApproach { Measured, EuLabel, SelfMeasured, None }

    public record PeriodRange(DateOnly StartDate, DateOnly EndDate);
    public record ResidualItem(decimal Kwh, decimal Cost);
    public record SubDeviceDecomposition(Guid DeviceId, string Name, decimal Kwh, decimal Cost, bool IsConfigured, bool IsUnconfigured);
    public record DeviceDecomposition(
        Guid DeviceId, string Name, decimal Kwh, decimal Cost,
        AttributionApproach Approach, bool IsSmartStrip,
        IReadOnlyList<SubDeviceDecomposition>? SubDevices);
    public record RoomDecomposition(Guid RoomId, string RoomName, decimal Kwh, decimal Cost, IReadOnlyList<DeviceDecomposition> Devices);
    public record DecompositionResponse(
        PeriodRange Period, decimal TotalKwh, decimal TotalCost,
        bool IsUnavailable, bool HasInterpolatedData,
        ResidualItem Residual, IReadOnlyList<RoomDecomposition> Rooms);
    ```
  - [x] No Data Annotation attributes anywhere in this file — plain records only, matching every other `*Models.cs` file in this codebase.

- [x] Task 2: `DecompositionEngine.cs` skeleton, DI registration, and `TotalKwh` via the reused day-allocation algorithm (AC: 2, 8, 10)
  - [x] Create `api/Features/Decomposition/DecompositionEngine.cs` as `public class DecompositionEngine(AppDbContext db)` (primary constructor, matching `ReconciliationEngine`'s shape) with `public async Task<DecompositionResponse> ComputeAsync(Guid flatId, DateOnly startDate, DateOnly endDate, CancellationToken ct)`.
  - [x] Register in `api/Program.cs` alongside the other DbContext-backed engines: `builder.Services.AddScoped<DecompositionEngine>();` (Scoped, not Singleton — it takes `AppDbContext`, per this codebase's non-negotiable DI-lifetime rule).
  - [x] Duplicate `ReconciliationEngine.cs:16-28`'s `AppTimeZone`/`ResolveAppTimeZone()` verbatim into `DecompositionEngine.cs` (private static field) — this codebase's established convention is self-contained per-engine duplication, not a shared timezone utility (see AC10's rationale).
  - [x] Duplicate both `ReconciliationEngine.cs:64-82`'s `TryComputeMainMeterTotal(readings, periodStart, periodEnd)` (coverage guard — returns `null` when `readings.Count < 2` or the period isn't fully bounded by the first/last local reading date) **and** `ReconciliationEngine.cs:84-103`'s `BuildMainMeterDailySeries(List<MeterReading> readings)` verbatim (both private static methods) and use them together to compute `TotalKwh` for `[startDate, endDate]`: load all `MeterReading` rows for the flat ordered by `ReadingDate`, call the guard first. Do **not** implement `TotalKwh` as a raw sum of `MeterReading.KwhValue`, and do **not** skip the coverage guard (see AC10 for the insufficient-coverage fallback: `TotalKwh` = sum of attributed device kWh, `Residual.Kwh = 0`, when the guard returns `null` for a period that otherwise has `SmartPlugDailyData`).

- [x] Task 3: Per-device attribution — Measured / EU label / SelfMeasured (AC: 2)
  - [x] Load the flat's full structure in one query: `Rooms` → `PowerPoints` → `Devices` (via `db.Rooms.Include(r => r.PowerPoints).ThenInclude(pp => pp.Devices).Where(r => r.FlatId == flatId).ToListAsync(ct)`, `AsNoTracking()`).
  - [x] Load all `SmartPlugDailyData` for the flat in `[startDate, endDate]` grouped by `PlugId` (`db.SmartPlugDailyData.Where(d => d.FlatId == flatId && d.Date >= startDate && d.Date <= endDate).ToListAsync(ct)`), then `.GroupBy(d => d.PlugId)` in memory for per-plug sums and per-plug `IsInterpolated` checks.
  - [x] For each `PowerPoint` with `PlugId != null` and exactly 1 `Device` (the simple Measured case): `Kwh` = sum of that plug's `SmartPlugDailyData.KwhValue` in range; `Approach = Measured`.
  - [x] For each standalone `Device` (no plug on its `PowerPoint`, or `Devices.Count > 1` handled separately per Task 4) with `ConsumptionApproach = EuLabel`: `dailyEstimate = EuAnnualKwh / 365m`; `Kwh = dailyEstimate * (decimal)(endDate.DayNumber - startDate.DayNumber + 1)`.
  - [x] With `ConsumptionApproach = SelfMeasured`: `dailyEstimate = SelfMeasuredPeriod == SelfMeasuredPeriod.Weekly ? SelfMeasuredKwh!.Value / 7m : SelfMeasuredKwh!.Value`; project across the same day count as above.
  - [x] With `ConsumptionApproach = None` and no plug: `Kwh = 0`, `Approach = AttributionApproach.None`.
  - [x] Group resulting `DeviceDecomposition`s by `Room`, summing `Kwh`/`Cost` per room into `RoomDecomposition.Kwh`/`Cost`.

- [x] Task 4: Smart Power Strip sub-device split (AC: 3, 12)
  - [x] For each `PowerPoint` with `PlugId != null` and `Devices.Count > 1`: compute `stripMeasuredTotal` = sum of that plug's `SmartPlugDailyData.KwhValue` in range.
  - [x] For each `Device` on the strip, compute its own `device_estimated_kWh` using the exact same EU-label/self-measured daily-estimate formulas from Task 3 (never a smart-plug measurement — sub-devices have no `PlugId` of their own).
  - [x] `configuredDevices` = devices with `ConsumptionApproach != None`; `sumConfiguredEstimates` = sum of their `device_estimated_kWh`.
  - [x] If `sumConfiguredEstimates > 0`: each configured device's share = `(device_estimated_kWh / sumConfiguredEstimates) * stripMeasuredTotal`; `unconfiguredDevices` share the remainder `(stripMeasuredTotal - sum_of_configured_shares) / unconfiguredDevices.Count` equally (guard `unconfiguredDevices.Count == 0` → skip, no remainder to distribute).
  - [x] If `sumConfiguredEstimates == 0` (all sub-devices unconfigured, or all configured devices have a zero estimate) — **defensive fallback to avoid a divide-by-zero**: split `stripMeasuredTotal` equally across **all** sub-devices (configured and unconfigured alike).
  - [x] Build the synthesized strip `DeviceDecomposition`: `DeviceId = PowerPoint.PowerPointId`, `Name = PowerPoint.Name`, `Kwh = stripMeasuredTotal`, `IsSmartStrip = true`, `Approach = AttributionApproach.Measured`, `SubDevices` = one `SubDeviceDecomposition` per `Device` with `IsConfigured`/`IsUnconfigured` set from `ConsumptionApproach`.
  - [x] This strip `DeviceDecomposition` is the **only** entry these devices contribute to the room's `Devices` list — do not also add each sub-device as its own top-level entry.

- [x] Task 5: Cost attribution via in-memory tariff resolution (AC: 4, 11)
  - [x] Load `Tariff` rows once per `ComputeAsync` call: `db.Tariffs.Where(t => t.FlatId == flatId).OrderBy(t => t.ContractStartDate).ToListAsync(ct)`.
  - [x] Duplicate `KpiCalculator.cs:155-164`'s `ResolveTariff(IReadOnlyList<Tariff> tariffs, DateTimeOffset date)` verbatim as a private static helper in `DecompositionEngine.cs`.
  - [x] For each `DateOnly date` in `[startDate, endDate]`, convert to a `DateTimeOffset` at that date's local midnight in `AppTimeZone` **before** calling `ResolveTariff` (this helper takes `DateTimeOffset`, and `Tariff.ContractStartDate` is `DateTimeOffset` — do not compare `DateOnly` directly): `var localMidnight = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, AppTimeZone.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue)));` (using `GetUtcOffset` rather than a fixed offset handles DST-boundary dates correctly, consistent with how `AppTimeZone` is used elsewhere in this story). Resolve the day's tariff via `ResolveTariff(tariffs, localMidnight)` and multiply into that day's device/room/total kWh to build cost fields (`decimal` throughout — no float/double). If no tariff resolves for a day, that day contributes 0 cost (no tariff configured is a valid state; do not throw).
  - [x] `DecompositionEngine.cs` must not reference `TariffResolver` — confirm via `grep -n "TariffResolver" api/Features/Decomposition/DecompositionEngine.cs` returning no matches before marking this task done.

- [x] Task 6: Residual computation (AC: 5)
  - [x] `Residual.Kwh = TotalKwh - sum(all DeviceDecomposition.Kwh across every RoomDecomposition, including strip sub-device shares)`.
  - [x] `Residual.Cost`: apply the same per-day tariff resolution (Task 5) to the residual's implied daily kWh, or equivalently `TotalCost - sum(all device Cost)` — either is acceptable as long as it stays period-accurate; pick whichever composes more naturally with the Task 2/5 loop structure and document the choice in Dev Agent Record.
  - [x] `Residual` is always included in `DecompositionResponse`, even when `Residual.Kwh == 0`.

- [x] Task 7: `IsUnavailable` and `HasInterpolatedData` (AC: 6, 7)
  - [x] If the flat has zero `SmartPlugDailyData` rows for any `PlugId` in `[startDate, endDate]`: return early with `IsUnavailable = true`, `Rooms = []`, `Residual = new(0m, 0m)`, `TotalKwh = 0m`, `TotalCost = 0m` — do **not** compute a real `TotalKwh` from `MeterReading`s in this branch (see AC6). HTTP 200 either way (enforced at the Function level, not by throwing).
  - [x] `HasInterpolatedData = true` if any loaded `SmartPlugDailyData` row in range has `IsInterpolated = true`.

- [x] Task 8: `GetDecompositionFunction.cs` (AC: 8, 13)
  - [x] Create `api/Features/Decomposition/GetDecompositionFunction.cs` as `public class GetDecompositionFunction(AppDbContext db, DecompositionEngine engine)`, mirroring `GetDashboardFunction.cs`'s tenant-check structure exactly:
    ```csharp
    [Function("GetDecomposition")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flats/{flatId}/decomposition")]
        HttpRequest req,
        string flatId,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        if (!Guid.TryParse(flatId, out var flatGuid))
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "Invalid flatId format." });

        var flat = await db.Flats.AsNoTracking()
            .SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct);
        if (flat is null)
            return new ObjectResult(new { title = "Forbidden", status = 403, detail = "Flat not found or access denied." }) { StatusCode = 403 };

        if (!DateOnly.TryParseExact(req.Query["startDate"], "yyyy-MM-dd", out var startDate))
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "Invalid or missing startDate." });

        if (!DateOnly.TryParseExact(req.Query["endDate"], "yyyy-MM-dd", out var endDate))
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "Invalid or missing endDate." });

        if (endDate < startDate)
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "endDate must not precede startDate." });

        var response = await engine.ComputeAsync(flatGuid, startDate, endDate, ct);
        return new OkObjectResult(response);
    }
    ```
    Note: `req.Query["startDate"]` returns `StringValues`, which implicitly converts to `string?` — `DateOnly.TryParseExact` needs the `string?` overload; confirm this compiles against .NET 10 (`StringValues` → `string?` implicit operator has been stable since ASP.NET Core's inception, so this should compile as written, but verify during implementation since this codebase has **no existing precedent** for query-string parsing to copy from).
  - [x] This is the first Function in the codebase to parse `req.Query` — after implementing, note in Dev Agent Record whether the pattern above needed adjustment, since future Functions needing query params will likely copy this one.

- [x] Task 9: `DecompositionEngineTests.cs` (AC: 9, 10, 12)
  - [x] Create `api.Tests/Features/Decomposition/DecompositionEngineTests.cs` using the `ReconciliationEngineTests.cs` pattern (`UseInMemoryDatabase(Guid.NewGuid().ToString())`, no mocked logger needed since `DecompositionEngine` takes only `AppDbContext`), with seed helpers for `Room`/`PowerPoint`/`Device`/`SmartPlugDailyData`/`MeterReading`/`Tariff` following `ReconciliationEngineTests`'s `SeedReadingAsync`/`SeedDailyRowAsync` style.
  - [x] Cover every scenario listed in AC9, including both AC10 tests (`TotalKwh_MultipleReadingsOnSameLocalCalendarDay_TelescopesCorrectlyWithoutDoubleCounting` and `TotalKwh_InsufficientMeterReadingCoverage_FallsBackToZeroResidualNotFalseNonZero`) and an AC12 test asserting a single-device measured `PowerPoint` gets `Approach = Measured` while a same-shaped `EuLabel` device on an unplugged `PowerPoint` gets `Approach = EuLabel` (proving the response enum is computed, not a pass-through cast of `ConsumptionApproach`).
  - [x] Use `Shouldly` assertions (`result.TotalKwh.ShouldBe(5m)`, etc.), matching this codebase's existing test-assertion library.

- [x] Task 10: Full verification pass before marking ready for review (AC: all)
  - [x] `dotnet test api.Tests/` — all green, zero regressions.
  - [x] `dotnet ef migrations has-pending-model-changes` from `api/` — expect **no pending migration** (this story adds no new entities/columns, only a new read-only computation over existing tables); if this reports a pending change, something was added to an entity that shouldn't have been — investigate before proceeding.
  - [x] `npm run lint` is not applicable (backend-only story, no frontend files touched).
  - [x] No `./infra/deploy.sh` run, no push to live Azure — this story has no infra changes.

### Review Findings

- [x] [Review][Defer] Smart Power Strip "unconfigured remainder" formula is mathematically dead in mixed strips — AC3/D-44's proportional-split formula (`device_share = (device_estimated_kWh / sumConfiguredEstimates) * stripMeasuredTotal`) causes `sum_of_configured_shares` to always equal `strip_measured_total` exactly, so `remainder = strip_measured_total - sum_of_configured_shares` is always ≈0. Unconfigured devices in a strip with ≥1 configured sibling never receive a meaningful share, contradicting AC3's stated intent that they "share the remainder equally." The code correctly implements the literal formula as written. [api/Features/Decomposition/DecompositionEngine.cs:170-189] — deferred: needs a follow-up story to redesign the split formula and handling of unconfigured devices
- [x] [Review][Patch] Plugged `PowerPoint` with zero `Devices` interacts badly with AC10's insufficient-coverage fallback — **Fixed**: tracks an `orphanedPlugSeries` for zero-device plugged PowerPoints and folds its kWh/cost into `TotalKwh`/`TotalCost` in the AC10 fallback branch, keeping `Residual.Kwh = 0` accurate. New test `ComputeAsync_PluggedPowerPointWithZeroDevices_ContributesToTotalKwhInFallbackBranch`. [api/Features/Decomposition/DecompositionEngine.cs:76-100]
- [x] [Review][Patch] AC10's regression test doesn't actually prove what the Change Log claims — **Fixed**: added `TotalKwh_PartialRangeStartingOnSameLocalDayDuplicateReadings_DoesNotInflateResult`, a partial-range test with a hand-derived expected value (62.5 kWh) that actually stresses the duplicate-day boundary. Passed on first run — confirms `BuildMainMeterDailySeries` does not double-count; no engine fix was needed. [api.Tests/Features/Decomposition/DecompositionEngineTests.cs]
- [x] [Review][Defer] Per-device "no data available" isn't distinguished from "verified zero consumption" — a plugged device with zero `SmartPlugDailyData` rows in range, or only partial-period coverage (offline mid-period, not flagged `IsInterpolated`), reports `Kwh = 0` or a partial sum indistinguishable from genuine zero/full consumption. No AC addresses this. [api/Features/Decomposition/DecompositionEngine.cs] — deferred: same UX ambiguity as the zero-device PowerPoint case in Story 7.3
- [x] [Review][Patch] Duplicate `(PlugId, Date)` rows in `SmartPlugDailyData` crash the engine — **Fixed**: `plugDailySeries` now groups by `(PlugId, Date)` with last-wins dedup instead of a bare `ToDictionary`. [api/Features/Decomposition/DecompositionEngine.cs:43-46]
- [x] [Review][Patch] `GetDecompositionFunction` has zero test coverage (400/403/routing) — **Fixed**: added `GetDecompositionFunctionTests.cs` (7 tests: invalid flatId, wrong tenant, missing/unparsable dates, endDate<startDate, unavailable 200, computed 200). [api.Tests/Features/Decomposition/GetDecompositionFunctionTests.cs]
- [x] [Review][Patch] `DateOnly.TryParseExact` omits `CultureInfo.InvariantCulture` for `startDate`/`endDate` — **Fixed**: both calls now pass `CultureInfo.InvariantCulture, DateTimeStyles.None`. [api/Features/Decomposition/GetDecompositionFunction.cs:30,33]
- [x] [Review][Patch] `deferred-work.md`'s `blocks: Story 7.1` entry was never marked resolved despite the Change Log claiming it's closed out — **Fixed**: struck through and annotated resolved, pointing at the new partial-range test. [_bmad-output/implementation-artifacts/deferred-work.md]
- [x] [Review][Patch] AC11's required "flag `project-context.md:423` for a human correction" was never turned into an actionable artifact — **Fixed**: `project-context.md:423`'s Data Integrity Invariants entry now carries an explicit 2026-07-13 correction explaining `TariffResolver.ResolveAsync` is dead code and `KpiCalculator.ResolveTariff`'s in-memory pattern is the real live convention. [_bmad-output/project-context.md:423]
- [x] [Review][Patch] AC9's "Residual.Kwh is 0 but present when all kWh is attributed" test duplicates the AC10 fallback test rather than exercising a genuine full-attribution scenario — **Fixed**: renamed the original to `ComputeAsync_InsufficientCoverageFallback_AllKwhAttributed_ResidualKwhIsZeroButPresent` (clarifying it's the fallback case) and added a new `ComputeAsync_RealReconciliationFullyAccountsForAttributedKwh_ResidualKwhIsExactlyZero` exercising the real main-meter reconciliation path. [api.Tests/Features/Decomposition/DecompositionEngineTests.cs]
- [x] [Review][Defer] `TryComputeMainMeterTotal`'s `periodStart <= firstLocalDate` boundary rejects a "since tracking began" query [api/Features/SmartPlugImport/ReconciliationEngine.cs:70] — deferred, pre-existing (duplicated verbatim per AC10)
- [x] [Review][Defer] `BuildMainMeterDailySeries` dumps a full inter-reading kWh delta into one day when two `MeterReading` rows share the exact same timestamp instant [api/Features/SmartPlugImport/ReconciliationEngine.cs:84-103] — deferred, pre-existing (duplicated verbatim per AC10)
- [x] [Review][Defer] No upper bound on requested date range — unfiltered `MeterReading` loads, no memoization across per-day loops [api/Features/Decomposition/DecompositionEngine.cs, GetDecompositionFunction.cs] — deferred, not required by any AC
- [x] [Review][Defer] `ResolveTariff`'s tie-break on identical `ContractStartDate` is non-deterministic [api/Features/Decomposition/DecompositionEngine.cs] — deferred, pre-existing (duplicated verbatim per AC11)
- [x] [Review][Defer] No rounding policy on decimal divisions before serialization [api/Features/Decomposition/DecompositionEngine.cs] — deferred, not required by any AC

## Dev Notes

### This is a brand-new feature slice — no partial implementation exists

`api/Features/Decomposition/` currently contains only a `.gitkeep` placeholder. Nothing to reconcile against; this story creates `DecompositionModels.cs`, `DecompositionEngine.cs`, and `GetDecompositionFunction.cs` from scratch, per `architecture.md:701-704`'s planned file layout.

### The three gap-clarification ACs (10, 11, 12) exist because the epic's prose doesn't match this codebase's real, established patterns

This story's epic text was written before the underlying engines (`ReconciliationEngine`, `KpiCalculator`, `TariffResolver`) existed in their current form, and before the Flat Structure data model (`Device`/`PowerPoint`/`Room`) was finalized in Story 5.3. Three places where epic prose and actual code diverge, each confirmed by direct code inspection during this story's creation:
- **AC10** — "sum of all MainMeter daily readings" is ambiguous phrasing for what `ReconciliationEngine.BuildMainMeterDailySeries` actually does (delta-based day-allocation between consecutive cumulative meter readings, not a raw sum of reading values) — and that same algorithm is the one `deferred-work.md` (`_bmad-output/implementation-artifacts/deferred-work.md:14`, tagged `blocks: Story 7.1`) flags as needing an explicit decision before this story relies on it for `TotalKwh`.
- **AC11** — `TariffResolver.ResolveAsync` reads as the obvious per-day cost-resolution call, but it has zero real callers today; `KpiCalculator`'s actual in-memory `ResolveTariff` is the only live precedent for per-day tariff resolution in this codebase.
- **AC12** — the epic's `IsSmartStrip`/`SubDevices` response fields imply a resolved "strip" concept that has no matching entity/flag in `Device.cs`/`PowerPoint.cs` — it must be derived structurally from `PowerPoint.PlugId` + `Devices.Count`.

### `project-context.md:423` is stale and this story deliberately does not follow it

`project-context.md`'s Data Integrity Invariants section states `TariffResolver.ResolveAsync(...)` is "the only correct path" for period-accurate costing, and the file's own Usage Guidelines say it wins over general best practice when rules conflict. AC11 deliberately does not follow this — a full-repo grep confirms `TariffResolver.ResolveAsync` has zero real callers today, and `KpiCalculator`'s in-memory `ResolveTariff` is the only pattern any live code in this repo actually uses. Treat AC11 as authoritative for this story (it reflects real, verified code), and raise `project-context.md:423` for a human correction so the next story doesn't hit the same contradiction.

### Open question flagged for Story 7.3's creation (not this story's concern)

Epic 7.3's AC ("a room with zero devices having any approach or plug configured... appears... with a non-zero kWh... renders one compact card reading 'Direct consumption'") describes a case this story's data model doesn't naturally produce: a `PowerPoint` with `PlugId` set but **zero** attached `Device` rows has its measured kWh flow entirely into the flat-level `Residual` (there's no `Device` to attribute it to), not into any `RoomDecomposition.Kwh` — so a room in that state would show `Kwh = 0` from this story's response, not the non-zero figure 7.3's AC describes. This is a plausible real scenario (a smart plug wired up before its device is registered) but resolving it may require a new response field (e.g. a room-level "unattributed" kWh) not specified anywhere in the PRD/epic/decision-log today. Flag this for whoever creates Story 7.3 — it may need a follow-up decision or a small addendum to this story's response shape at that time. Not blocking for 7.1: none of this story's own ACs require it.

### Testing conventions

Backend-only story — `xUnit` + `EF Core InMemory` + `Shouldly`, mirroring `ReconciliationEngineTests.cs` (`api.Tests/Features/SmartPlugImport/ReconciliationEngineTests.cs`) exactly: `MakeDb()` via `UseInMemoryDatabase(Guid.NewGuid().ToString())`, per-test seed helpers, `NoonUtc(year, month, day)` for unambiguous local-date seeding regardless of DST (`ReconciliationEngineTests.cs`'s own comment: "Noon UTC keeps the local (Europe/Berlin) calendar date unambiguous regardless of DST").

### Project Structure Notes

- New files only:
  - `api/Features/Decomposition/DecompositionModels.cs`
  - `api/Features/Decomposition/DecompositionEngine.cs`
  - `api/Features/Decomposition/GetDecompositionFunction.cs`
  - `api.Tests/Features/Decomposition/DecompositionEngineTests.cs`
- Modified: `api/Program.cs` (one new `AddScoped<DecompositionEngine>()` line).
- No changes to any entity, EF configuration, or migration — this story is a pure read-side computation over existing tables (`Room`, `PowerPoint`, `Device`, `MeterReading`, `SmartPlugDailyData`, `Tariff`). Confirm with `dotnet ef migrations has-pending-model-changes` (Task 10).
- No frontend changes — Story 7.2 consumes this endpoint.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-7-consumption-decomposition.md#Story 7.1] — authoritative AC text (verbatim, reproduced above as ACs 1–9; ACs 10–12 added during story creation per the gaps described in Dev Notes above).
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md#FR-13, FR-27, FR-29–34] — functional requirements: period-accurate costing (FR-13), reconciliation/Residual invariant and tolerances (FR-27), device registry approaches (FR-29–31), decomposition view/Residual-always-shown/unavailable-state (FR-32–34).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/.decision-log.md#D-43, D-44] — D-43: Smart Power Strip physical model (`Power Point → Smart Power Strip → multiple Strip Outlets`, no dedicated entity); D-44: the exact proportional-split formula this story's AC3/Task 4 implement verbatim.
- [Source: _bmad-output/planning-artifacts/architecture.md:151, 217-218, 701-704, 782-783, 872] — planned file layout (`Decomposition/GetDecompositionFunction.cs`, `DecompositionEngine.cs`, `DecompositionModels.cs`), `Devices`/`PowerPoints`/`SmartPlugDailyData` table column lists, traceability row for FR-32–34.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:13-14] — the `blocks: Story 7.1` deferred item resolved by AC10/Task 2/Task 9's telescoping regression test.
- [Source: api/Features/SmartPlugImport/ReconciliationEngine.cs] — the day-allocation algorithm (`BuildMainMeterDailySeries`, `TryComputeMainMeterTotal`, `AppTimeZone`) duplicated verbatim into `DecompositionEngine.cs` per AC10/Task 2.
- [Source: api/Features/Dashboard/KpiCalculator.cs:155-164] — `ResolveTariff`'s in-memory tariff-resolution helper duplicated verbatim into `DecompositionEngine.cs` per AC11/Task 5.
- [Source: api/Shared/TariffResolver.cs, api/Program.cs:55] — confirmed dead code (zero callers) as of this story's baseline commit; not to be used per AC11.
- [Source: api/Data/Entities/Device.cs, PowerPoint.cs, Room.cs; api/Data/Configurations/DeviceConfiguration.cs, MeterReadingConfiguration.cs:23-25, SmartPlugDailyDataConfiguration.cs] — exact current entity shapes and indexes underpinning AC1, AC10, AC12.
- [Source: api/Features/Dashboard/GetDashboardFunction.cs, api/Shared/FunctionContextExtensions.cs] — the tenant-check/400/403 Function pattern mirrored in Task 8; confirmed identical in `api/Features/Readings/GetReadingHistoryFunction.cs:22-36`.
- [Source: api.Tests/Features/SmartPlugImport/ReconciliationEngineTests.cs, api.Tests/Features/Dashboard/KpiCalculatorTests.cs] — test-setup patterns (`MakeDb`, seed helpers, `NoonUtc`, Shouldly) mirrored in Task 9.

## Change Log

- 2026-07-12: Story created via create-story workflow. Three gap-clarification ACs (10, 11, 12) added after cross-referencing the epic's literal text against `ReconciliationEngine.cs`, `KpiCalculator.cs`, `TariffResolver.cs`, and the actual `Device`/`PowerPoint`/`Room` entity shapes — resolving the `deferred-work.md` `blocks: Story 7.1` item (AC10) and two additional undocumented divergences found during research (AC11, AC12).
- 2026-07-12: Fixed two review-found defects before marking ready-for-dev: (1) AC6/Task 7's `IsUnavailable` branch was computing a real `TotalKwh` from `MeterReading`s while forcing `Residual.Kwh = 0`, which could violate this story's own AC5 invariant — now fully zeroes `TotalKwh`/`TotalCost` in that branch; (2) AC10/Task 2 only reused `BuildMainMeterDailySeries`, not `ReconciliationEngine`'s `TryComputeMainMeterTotal` coverage guard — now reuses both, with an explicit fallback (`Residual.Kwh = 0`) for periods with insufficient meter-reading coverage. Also specified the missing `DateOnly`→`DateTimeOffset` conversion for Task 5's per-day tariff resolution, and added an explicit Dev Notes acknowledgment that AC11 deliberately overrides a stale rule in `project-context.md:423`.
- 2026-07-13: Implemented all 10 tasks. `DecompositionEngine.ComputeAsync` builds a per-day kWh series for every attribution unit (measured plug reading, EU-label/self-measured constant daily estimate, or smart-strip proportional share) and prices each day against that day's in-memory-resolved tariff, so device/room/total cost figures stay period-accurate without any per-day DB round-trip. `Residual.Cost` is computed as `TotalCost − Σ(device costs)` (the "equivalently acceptable" option named in Task 6), which is trivially consistent with `Residual.Kwh = 0` in the AC10 insufficient-coverage fallback branch since `TotalCost` collapses to `Σ(device costs)` there too. 15 new tests added covering every AC9-listed scenario; 347/347 backend tests pass (0 regressions); `dotnet ef migrations has-pending-model-changes` confirms no pending migration; `grep -n "TariffResolver" api/Features/Decomposition/DecompositionEngine.cs` returns no matches (Task 5's explicit gate). Story moved to `review`.
- 2026-07-13: Code review (Blind Hunter + Edge Case Hunter + Acceptance Auditor). 4 decision-needed, 6 patch, 5 defer, 3 dismissed. Decisions: smart-strip unconfigured-remainder formula (AC3/D-44) is mathematically dead in mixed strips — deferred, needs a follow-up story to redesign the split formula; zero-device plugged-PowerPoint kWh loss in the AC10 fallback branch — fixed now; AC10's regression test didn't actually prove the telescoping property (full-range sums are trivially correct regardless of internal allocation) — strengthened with a partial-range test, which confirmed no double-counting bug exists; per-device "no data" vs "verified zero" signal — deferred alongside the existing Story 7.3 open question. All 8 patches applied: orphaned-plug kWh now folds into the AC10 fallback `TotalKwh`/`TotalCost`; a proper partial-range double-counting regression test was added (`deferred-work.md`'s `blocks: Story 7.1` item is now genuinely resolved, not just claimed); duplicate `(PlugId, Date)` rows no longer crash `ComputeAsync` (last-wins dedup); `GetDecompositionFunctionTests.cs` added (7 tests: 400/403/200 paths); `DateOnly.TryParseExact` now pins `CultureInfo.InvariantCulture`; `project-context.md:423`'s stale `TariffResolver.ResolveAsync` claim corrected per AC11; AC9's residual-zero test split into a genuinely distinct fallback-path test and a real-reconciliation-path test. 357/357 backend tests pass (0 regressions); `dotnet ef migrations has-pending-model-changes` confirms no pending migration.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

None — no failing test runs or build errors encountered during implementation; all new tests passed on first execution.

### Completion Notes List

- All 12 ACs implemented and verified via `DecompositionEngineTests.cs` (15 new tests) plus the full existing 332-test regression suite — 347/347 total, 0 failures.
- Cost attribution (AC4/AC11): built a `CostForDailySeries(Func<DateOnly, decimal>)` helper inside `ComputeAsync` that resolves each calendar day's tariff via the duplicated `ResolveTariff` (matching `KpiCalculator.cs:155-164` verbatim) and multiplies by that day's kWh — reused for device costs, smart-strip sub-device costs (via a constant per-day split ratio applied to the strip's daily plug series), and the flat-level `TotalCost` (via the reused `BuildMainMeterDailySeries` day series). `TariffResolver.ResolveAsync` is not referenced anywhere in the new code (confirmed via grep, Task 5's explicit gate).
- Residual.Cost (AC5/Task 6): implemented as `TotalCost − Σ(device costs)`, the simpler of the two "equally acceptable" options Task 6 names, since it composes directly with the loop structure already built for Task 2/5 without needing a second residual-specific day-series pass.
- AC10 (`TotalKwh`): both `TryComputeMainMeterTotal` (coverage guard) and `BuildMainMeterDailySeries` (day-allocation) were duplicated verbatim from `ReconciliationEngine.cs:64-103` into `DecompositionEngine.cs` as private static methods, per this codebase's established per-engine duplication convention. The telescoping-sum regression test (`TotalKwh_MultipleReadingsOnSameLocalCalendarDay_TelescopesCorrectlyWithoutDoubleCounting`) seeds 4 `MeterReading` rows (2 landing on the same local calendar day) and asserts `TotalKwh` equals exactly `last.KwhValue − first.KwhValue`, closing out the `deferred-work.md` `blocks: Story 7.1` item with executable proof.
- AC12 (Smart Power Strip identity): a `PowerPoint` is a strip iff `PlugId != null && Devices.Count > 1`; the synthesized `DeviceDecomposition` uses `PowerPoint.PowerPointId`/`Name` (no single `Device` row represents a whole strip) and a fresh `AttributionApproach` enum distinct from the stored `ConsumptionApproach` enum. A dedicated test (`ComputeAsync_ApproachIsComputedNotCastFromStorageEnum`) proves the response `Approach` is computed, not a pass-through cast, by seeding two identically-configured (`EuLabel`) devices — one behind a solo smart plug (→ `Measured`), one unplugged (→ `EuLabel`).
- Task 8 note (per the story's own prompt): `req.Query["startDate"]`'s implicit `StringValues` → `string?` conversion compiled without any adjustment against .NET 10 / ASP.NET Core — no changes were needed to the pattern given in the story text. This is the first Function in the codebase to parse `req.Query`; future query-param Functions can copy this pattern as-is.
- `.gitkeep` placeholders removed from `api/Features/Decomposition/` and `api.Tests/Features/Decomposition/` now that real files exist, matching the convention in every other feature folder.
- No entity/EF configuration/migration changes — `dotnet ef migrations has-pending-model-changes` confirms zero pending model changes, as expected for a pure read-side computation story.
- No frontend changes; no infra changes; no `deploy.sh` run.

### File List

- `api/Features/Decomposition/DecompositionModels.cs` (new)
- `api/Features/Decomposition/DecompositionEngine.cs` (new)
- `api/Features/Decomposition/GetDecompositionFunction.cs` (new)
- `api.Tests/Features/Decomposition/DecompositionEngineTests.cs` (new)
- `api/Program.cs` (modified — registered `DecompositionEngine` as Scoped)
- `api/Features/Decomposition/.gitkeep` (deleted)
- `api.Tests/Features/Decomposition/.gitkeep` (deleted)
- `api.Tests/Features/Decomposition/GetDecompositionFunctionTests.cs` (new — code review patch)
- `_bmad-output/project-context.md` (modified — code review patch, corrected stale `TariffResolver` invariant at line 423)
- `_bmad-output/implementation-artifacts/deferred-work.md` (modified — code review, resolved `blocks: Story 7.1` item and added new deferred items)
