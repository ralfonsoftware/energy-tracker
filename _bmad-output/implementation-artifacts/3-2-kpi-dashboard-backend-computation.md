---
baseline_commit: 8b1c6c1
---

# Story 3.2: KPI Dashboard — Backend Computation

Status: done

## Story

As a user,
I want the server to compute my daily average, weekly average, and projected monthly cost from my meter readings,
so that the dashboard always reflects period-accurate figures in euros.

## Acceptance Criteria

1. **GET returns DashboardSummary** — Given `GET /api/v1/flats/{flatId}/dashboard`, when `GetDashboardFunction.RunAsync` executes, then `KpiCalculator.Compute()` returns a `DashboardSummary` record with: `DailyAvgKwh`, `WeeklyAvgKwh`, `DailyAvgCost`, `WeeklyAvgCost`, `ProjectedMonthlyCost` (all decimal), `LastReadingDate` (datetimeoffset nullable), `TodayKwh` (decimal), `DailyBudgetKwh` (decimal — `AnnualKwhBaseline ÷ 365`), `SpikeDays` (string[] — empty array, full algorithm in Story 3.5); HTTP 200.

2. **Response time ≤ 2 seconds** — Given any flat with up to 2 years of readings, server processing completes in ≤ 2 seconds (NFR-1 Tier 1). All data is loaded in two queries (readings + tariffs) before KpiCalculator is called — no per-reading DB calls.

3. **No readings → zeros** — Given a Flat with no meter readings, when called, then HTTP 200 with all numeric KPI values as `0m` and `LastReadingDate` as `null`.

4. **Period-accurate tariff costing** — Given a flat with readings spanning two different tariff periods, when the dashboard is computed, then each period's cost uses the tariff active on that period's start date (resolved by `TariffResolver`), not the current tariff. `ProjectedMonthlyCost` uses the tariff active NOW.

5. **JSON contract** — Given all responses, field names are camelCase; `LastReadingDate` is ISO 8601 with explicit offset; all decimal values are JSON numbers. `SpikeDays` serialises as `[]`.

6. **Tenant isolation** — Given a `flatId` that does not belong to the resolved `UserId`, then HTTP 403 Problem Details; no data is returned.

## Tasks / Subtasks

- [x] Task 1: Create `TariffResolver.cs` in `api/Shared/` (AC: 4)
  - [x] `api/Shared/TariffResolver.cs` — Scoped service; `ResolveAsync(Guid flatId, DateTimeOffset date, CancellationToken ct)` queries `Tariffs` where `FlatId == flatId && EffectiveDate <= date`, ordered by `EffectiveDate DESC`, returns `FirstOrDefaultAsync` (see Dev Notes for exact implementation)
  - [x] `api/Program.cs` — register `builder.Services.AddScoped<TariffResolver>()`; add `using EnergyTracker.Api.Shared;` if not already present

- [x] Task 2: Create `DashboardModels.cs` (AC: 1, 5)
  - [x] `api/Features/Dashboard/DashboardModels.cs` — single `DashboardSummary` record (see Dev Notes for field list and types)

- [x] Task 3: Create `KpiCalculator.cs` (AC: 1, 2, 3, 4)
  - [x] `api/Features/Dashboard/KpiCalculator.cs` — pure computation class; no DB dependency; `public DashboardSummary Compute(Flat flat, IReadOnlyList<MeterReading> readings, IReadOnlyList<Tariff> tariffs, DateTimeOffset now)` (see Dev Notes for complete algorithm)
  - [x] Internal `private static Tariff? ResolveTariff(IReadOnlyList<Tariff> tariffs, DateTimeOffset date)` — in-memory period-accurate lookup (no DB call)
  - [x] `api/Program.cs` — register `builder.Services.AddSingleton<KpiCalculator>()`; add `using EnergyTracker.Api.Features.Dashboard;`

- [x] Task 4: Create `GetDashboardFunction.cs` (AC: 1, 2, 3, 5, 6)
  - [x] `api/Features/Dashboard/GetDashboardFunction.cs` — GET handler; primary constructor `(AppDbContext db, KpiCalculator calculator)`; Route: `"v1/flats/{flatId}/dashboard"` (no `/api` prefix); tenant check via `SingleOrDefaultAsync`; fetch all readings (ascending) and all tariffs (ascending); call `calculator.Compute`; return `OkObjectResult` (see Dev Notes for complete implementation)

- [x] Task 5: Create `TariffResolverTests.cs` (AC: 4)
  - [x] `api.Tests/Shared/TariffResolverTests.cs` — minimum 5 tests using InMemory EF (see Dev Notes for full test list)

- [x] Task 6: Create `KpiCalculatorTests.cs` (AC: 1, 3, 4)
  - [x] `api.Tests/Features/Dashboard/KpiCalculatorTests.cs` — minimum 9 pure synchronous tests; NO mocking, NO InMemory DB (see Dev Notes for full test list)

- [x] Task 7: Create `GetDashboardFunctionTests.cs` (AC: 1, 3, 6)
  - [x] `api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs` — minimum 4 tests using InMemory EF + Moq FunctionContext (see Dev Notes for full test list)

- [x] Task 8: Final verification
  - [x] `cd api && dotnet build` exits 0, no warnings
  - [x] `cd api.Tests && dotnet test` — all 58 tests pass (40 pre-existing + 18 new)
  - [x] Update File List in this story

## Dev Notes

### Domain Invariant — Read First

**Meter readings are CUMULATIVE odometer values, not interval/delta values.**
- Reading at T1=100 kWh, Reading at T2=150 kWh → consumption for the period = 50 kWh over (T2-T1) days
- `DailyAvgKwh = (last.KwhValue - first.KwhValue) / totalDays`
- Readings pre-sorted ascending by `ReadingDate` from the DB query before being passed to `KpiCalculator`

### Task 1: TariffResolver

```csharp
// api/Shared/TariffResolver.cs
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Shared;

public class TariffResolver(AppDbContext db)
{
    public async Task<Tariff?> ResolveAsync(Guid flatId, DateTimeOffset date, CancellationToken ct)
        => await db.Tariffs
            .Where(t => t.FlatId == flatId && t.EffectiveDate <= date)
            .OrderByDescending(t => t.EffectiveDate)
            .FirstOrDefaultAsync(ct);
}
```

**Registration in Program.cs** (takes `AppDbContext` → must be Scoped):
```csharp
builder.Services.AddScoped<TariffResolver>();
```

`TariffResolver` is declared in the architecture as the **only correct path** for period-accurate tariff lookup. Do NOT inline equivalent logic in `KpiCalculator` or `GetDashboardFunction`.

Note: `TariffResolver` is registered as Scoped here but `GetDashboardFunction` does NOT inject it. Instead, `GetDashboardFunction` fetches all tariffs for the flat as a list and `KpiCalculator` does the in-memory resolution. `TariffResolver` is available for other features (Decomposition, Insights) that need per-date lookups.

### Task 2: DashboardModels

```csharp
// api/Features/Dashboard/DashboardModels.cs
namespace EnergyTracker.Api.Features.Dashboard;

public record DashboardSummary(
    decimal DailyAvgKwh,
    decimal WeeklyAvgKwh,
    decimal DailyAvgCost,
    decimal WeeklyAvgCost,
    decimal ProjectedMonthlyCost,
    DateTimeOffset? LastReadingDate,
    decimal TodayKwh,
    decimal DailyBudgetKwh,
    string[] SpikeDays
);
```

`SpikeDays` is `string[]` (not `List<string>`) — Story 3.5 populates it; Story 3.2 always returns `[]`.

### Task 3: KpiCalculator — Complete Implementation

```csharp
// api/Features/Dashboard/KpiCalculator.cs
using EnergyTracker.Api.Data.Entities;

namespace EnergyTracker.Api.Features.Dashboard;

public class KpiCalculator
{
    public DashboardSummary Compute(
        Flat flat,
        IReadOnlyList<MeterReading> readings,
        IReadOnlyList<Tariff> tariffs,
        DateTimeOffset now)
    {
        var dailyBudgetKwh = flat.AnnualKwhBaseline / 365m;

        if (readings.Count == 0)
            return new DashboardSummary(0m, 0m, 0m, 0m, 0m, null, 0m, dailyBudgetKwh, []);

        if (readings.Count == 1)
            return new DashboardSummary(0m, 0m, 0m, 0m, 0m, readings[0].ReadingDate, 0m, dailyBudgetKwh, []);

        // readings is pre-sorted ascending by ReadingDate
        var totalDays = (readings[^1].ReadingDate - readings[0].ReadingDate).TotalDays;
        if (totalDays <= 0)
            return new DashboardSummary(0m, 0m, 0m, 0m, 0m, readings[^1].ReadingDate, 0m, dailyBudgetKwh, []);

        var totalKwh = Math.Max(0m, readings[^1].KwhValue - readings[0].KwhValue);
        var dailyAvgKwh = totalKwh / (decimal)totalDays;
        var weeklyAvgKwh = dailyAvgKwh * 7m;

        // Period-accurate cost: each inter-reading interval uses tariff active at interval start
        decimal totalCost = 0m;
        for (var i = 0; i < readings.Count - 1; i++)
        {
            var periodKwh = Math.Max(0m, readings[i + 1].KwhValue - readings[i].KwhValue);
            var tariff = ResolveTariff(tariffs, readings[i].ReadingDate);
            if (tariff is not null)
                totalCost += periodKwh * tariff.PricePerKwh;
        }

        var dailyAvgCost = totalCost / (decimal)totalDays;
        var weeklyAvgCost = dailyAvgCost * 7m;

        // ProjectedMonthlyCost: daily avg kWh × current tariff price × 30 days
        var currentTariff = ResolveTariff(tariffs, now);
        var projectedMonthlyCost = currentTariff is not null
            ? dailyAvgKwh * currentTariff.PricePerKwh * 30m
            : 0m;

        // TodayKwh: last interval's daily rate (most recent consumption trend)
        var lastDays = (readings[^1].ReadingDate - readings[^2].ReadingDate).TotalDays;
        var lastKwh = Math.Max(0m, readings[^1].KwhValue - readings[^2].KwhValue);
        var todayKwh = lastDays > 0 ? lastKwh / (decimal)lastDays : dailyAvgKwh;

        return new DashboardSummary(
            DailyAvgKwh: dailyAvgKwh,
            WeeklyAvgKwh: weeklyAvgKwh,
            DailyAvgCost: dailyAvgCost,
            WeeklyAvgCost: weeklyAvgCost,
            ProjectedMonthlyCost: projectedMonthlyCost,
            LastReadingDate: readings[^1].ReadingDate,
            TodayKwh: todayKwh,
            DailyBudgetKwh: dailyBudgetKwh,
            SpikeDays: []
        );
    }

    private static Tariff? ResolveTariff(IReadOnlyList<Tariff> tariffs, DateTimeOffset date)
    {
        Tariff? best = null;
        foreach (var t in tariffs)
        {
            if (t.EffectiveDate <= date && (best is null || t.EffectiveDate > best.EffectiveDate))
                best = t;
        }
        return best;
    }
}
```

**Key decisions:**
- `Math.Max(0m, ...)` on kWh differences — defensive against corrected readings where `IsCorrected = true` and `KwhValue` was lowered
- `SpikeDays: []` — Story 3.5 adds the spike detection algorithm inside `KpiCalculator`
- `ProjectedMonthlyCost` uses current tariff × historical daily avg kWh (not historical weighted cost)
- `TodayKwh` = last interval's daily rate (most recent pattern); falls back to `dailyAvgKwh` if `lastDays <= 0`
- No DB calls here — all data received as parameters; enables pure unit testing with no mocking

**Registration in Program.cs** (no DB dependency → Singleton):
```csharp
builder.Services.AddSingleton<KpiCalculator>();
```

### Task 4: GetDashboardFunction — Complete Implementation

```csharp
// api/Features/Dashboard/GetDashboardFunction.cs
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Dashboard;

public class GetDashboardFunction(AppDbContext db, KpiCalculator calculator)
{
    [Function("GetDashboard")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flats/{flatId}/dashboard")]
        HttpRequest req,
        string flatId,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        if (!Guid.TryParse(flatId, out var flatGuid))
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Invalid flatId format."
            });

        var flat = await db.Flats.SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct);
        if (flat is null)
            return new ObjectResult(new
            {
                title = "Forbidden", status = 403,
                detail = "Flat not found or access denied."
            }) { StatusCode = 403 };

        var readings = await db.MeterReadings
            .Where(r => r.FlatId == flatGuid)
            .OrderBy(r => r.ReadingDate)
            .ToListAsync(ct);

        var tariffs = await db.Tariffs
            .Where(t => t.FlatId == flatGuid)
            .OrderBy(t => t.EffectiveDate)
            .ToListAsync(ct);

        var summary = calculator.Compute(flat, readings, tariffs, DateTimeOffset.UtcNow);
        return new OkObjectResult(summary);
    }
}
```

**Critical notes:**
- No request body — GET request; no `JsonSerializer`, no `_jsonOptions` needed
- Tariffs query is NOT additionally tenant-checked because the flat ownership is already verified; tariffs are FlatId-scoped only
- Two DB queries total (readings + tariffs), not per-reading/per-tariff calls — satisfies the ≤2s Tier 1 NFR
- Returns `OkObjectResult` (200), not `CreatedResult` — GET endpoint
- `OrderBy(r => r.ReadingDate)` ensures `KpiCalculator` receives ascending data as required

### Task 5: TariffResolverTests

```csharp
// api.Tests/Shared/TariffResolverTests.cs
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api.Tests.Shared;

public class TariffResolverTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(Guid flatId, AppDbContext db)> SeedAsync(params (DateTimeOffset date, decimal price)[] tariffs)
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        foreach (var (date, price) in tariffs)
        {
            db.Tariffs.Add(new Tariff
            {
                TariffId = Guid.NewGuid(),
                FlatId = flatId,
                EffectiveDate = date,
                PricePerKwh = price,
                MonthlyBaseFee = 10m
            });
        }
        await db.SaveChangesAsync();
        return (flatId, db);
    }
}
```

**Minimum 5 test methods:**

1. `ResolveAsync_NoTariffs_ReturnsNull` — empty tariff table → null
2. `ResolveAsync_DateBeforeAllTariffs_ReturnsNull` — date before first tariff's EffectiveDate → null
3. `ResolveAsync_DateOnEffectiveDate_ReturnsTariff` — date == EffectiveDate exactly → tariff returned
4. `ResolveAsync_DateBetweenTariffs_ReturnsEarlierOne` — date between T1 and T2 EffectiveDates → returns T1
5. `ResolveAsync_DateAfterAllTariffs_ReturnsMostRecent` — date after all tariffs → returns newest

### Task 6: KpiCalculatorTests

These are PURE unit tests — no InMemory DB, no mocking, no async. Instantiate `KpiCalculator` directly.

```csharp
// api.Tests/Features/Dashboard/KpiCalculatorTests.cs
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Dashboard;
using Shouldly;

namespace api.Tests.Features.Dashboard;

public class KpiCalculatorTests
{
    private readonly KpiCalculator _calculator = new();
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);

    private static Flat MakeFlat(decimal baseline = 3650m) =>
        new() { FlatId = Guid.NewGuid(), UserId = "u", Name = "F", AnnualKwhBaseline = baseline, SpikeThreshold = 2.0m };

    private static MeterReading MakeReading(DateTimeOffset date, decimal kwh) =>
        new() { ReadingId = Guid.NewGuid(), FlatId = Guid.NewGuid(), ReadingDate = date, KwhValue = kwh, IsCorrected = false };

    private static Tariff MakeTariff(DateTimeOffset effectiveDate, decimal pricePerKwh) =>
        new() { TariffId = Guid.NewGuid(), FlatId = Guid.NewGuid(), EffectiveDate = effectiveDate, PricePerKwh = pricePerKwh, MonthlyBaseFee = 10m };
}
```

**Minimum 9 test methods:**

1. `Compute_NoReadings_ReturnsAllZerosAndNullLastReadingDate`
2. `Compute_OneReading_ReturnsZeroKpisWithLastReadingDateSet`
3. `Compute_TwoReadings_ComputesDailyAvgKwhCorrectly`
   - 10 days apart, 100→150 kWh → `DailyAvgKwh = 5m`; `WeeklyAvgKwh = 35m`
4. `Compute_TwoReadings_WithTariff_ComputesDailyAvgCostCorrectly`
   - 10 days apart, 50 kWh consumed, tariff 0.30€/kWh → `totalCost = 15m`, `DailyAvgCost = 1.5m`
5. `Compute_TwoReadings_NoTariff_CostFieldsAreZero`
   - readings exist, no tariffs → `DailyAvgCost = 0m`, `ProjectedMonthlyCost = 0m`
6. `Compute_MultipleReadings_TariffChange_UsesPeriodAccurateCost`
   - Reading A→B (10 days, 50 kWh) at tariff 0.20€; Reading B→C (10 days, 50 kWh) at tariff 0.30€
   - `totalCost = 10m + 15m = 25m`; `DailyAvgCost = 25m / 20d = 1.25m`
7. `Compute_DailyBudgetKwh_IsAnnualBaselineDividedBy365`
   - `AnnualKwhBaseline = 3650m` → `DailyBudgetKwh = 10m`
8. `Compute_TodayKwh_IsLastIntervalDailyRate`
   - Three readings: A→B (10 days, 50 kWh), B→C (5 days, 30 kWh) → `TodayKwh = 6m`
9. `Compute_ProjectedMonthlyCost_UsesCurrentTariffPriceAndHistoricalKwhRate`
   - DailyAvgKwh = 5m, current tariff = 0.40€/kWh → `ProjectedMonthlyCost = 5m × 0.40m × 30m = 60m`

### Task 7: GetDashboardFunctionTests

Follow pattern from `SubmitReadingTests.cs`: InMemory DB + Moq FunctionContext.

```csharp
// api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs
// MakeDb(), MakeFunctionContext(), SeedFlatAsync() helpers — same as SubmitReadingTests pattern
// Call: var fn = new GetDashboardFunction(db, new KpiCalculator());
// var result = await fn.RunAsync(req, flatId, ctx, CancellationToken.None);
```

**Minimum 4 test methods:**

1. `RunAsync_ValidFlatNoReadings_Returns200WithZeroSummary`
   - seed flat, no readings, no tariffs → 200, `DailyAvgKwh == 0m`, `LastReadingDate == null`
2. `RunAsync_InvalidFlatIdGuid_Returns400`
   - `flatId = "not-a-guid"` → `BadRequestObjectResult`
3. `RunAsync_FlatNotOwnedByUser_Returns403`
   - flat owned by "owner", call with userId="intruder" → `(result as ObjectResult)?.StatusCode.ShouldBe(403)`
4. `RunAsync_ValidFlatWithReadings_Returns200WithComputedSummary`
   - seed flat + 2 readings + tariff → 200, `DailyAvgKwh > 0m`

### Architecture Compliance Checklist

- [ ] Route: `"v1/flats/{flatId}/dashboard"` — no leading `/api`
- [ ] Method name: `RunAsync` (always)
- [ ] `AuthorizationLevel.Anonymous` (SWA Easy Auth is the gate)
- [ ] Primary constructor for DI: `public class GetDashboardFunction(AppDbContext db, KpiCalculator calculator)`
- [ ] `decimal` for all KPI values — never `float` or `double`
- [ ] `DateTimeOffset?` for `LastReadingDate` — never `DateTime?`
- [ ] `DashboardSummary` is a `record` — never a `class`
- [ ] `CancellationToken ct` passed to all async EF calls: `SingleOrDefaultAsync(ct)`, `ToListAsync(ct)`
- [ ] Tenant check: `SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct)` — no separate flat lookup then user check
- [ ] `TariffResolver` registered as `Scoped` in `Program.cs` — uses `AppDbContext`
- [ ] `KpiCalculator` registered as `Singleton` in `Program.cs` — no DB dependency
- [ ] Error responses: anonymous objects with `title`, `status`, `detail` — same pattern as `SubmitReadingFunction`
- [ ] `OkObjectResult` for 200 GET — never `CreatedResult`
- [ ] No `_jsonOptions` in `GetDashboardFunction` — GET has no request body to deserialize

### Project Structure Notes

All new files:
```
api/Shared/
└── TariffResolver.cs          ← NEW (period-accurate tariff lookup shared service)

api/Features/Dashboard/        ← NEW folder
├── DashboardModels.cs         ← NEW (DashboardSummary record)
├── KpiCalculator.cs           ← NEW (pure computation class)
└── GetDashboardFunction.cs    ← NEW (GET handler)

api.Tests/Shared/
└── TariffResolverTests.cs     ← NEW
api.Tests/Features/Dashboard/  ← NEW folder
├── KpiCalculatorTests.cs      ← NEW
└── GetDashboardFunctionTests.cs ← NEW
```

Modified:
```
api/Program.cs                 ← ADD AddScoped<TariffResolver>(), AddSingleton<KpiCalculator>()
                                  ADD using EnergyTracker.Api.Features.Dashboard;
```

**No EF Core migration needed** — no new entities, no schema changes.

### What Already Exists — Do Not Recreate

- `api/Data/Entities/MeterReading.cs` — `ReadingId`, `FlatId`, `KwhValue` (decimal), `ReadingDate` (DateTimeOffset), `IsCorrected`, `OriginalKwhValue`
- `api/Data/Entities/Flat.cs` — `FlatId`, `UserId`, `Name`, `AnnualKwhBaseline` (decimal), `SpikeThreshold` (decimal), `PlannedAnnualSpend` (decimal?)
- `api/Data/Entities/Tariff.cs` — `TariffId`, `FlatId`, `EffectiveDate` (DateTimeOffset), `PricePerKwh` (decimal), `MonthlyBaseFee` (decimal), `ProviderName` (?), `ContractStartDate` (?), `ContractDurationMonths` (int?)
- `api/Data/AppDbContext.cs` — `MeterReadings` DbSet already added (Story 3.1); `Tariffs` DbSet already present
- `api/Shared/FunctionContextExtensions.cs` — `context.GetUserId()` extension method
- `api/Shared/LocaleResolver.cs` — unrelated to this story
- `api/Program.cs` — `ReadingValidator` registered as Singleton; follow the same pattern

Pattern files to study before writing:
- `api/Features/Readings/SubmitReadingFunction.cs` — tenant check, error shape, route pattern
- `api/Features/Settings/GetUserSettingsFunction.cs` — GET function pattern (no body, OkObjectResult)
- `api.Tests/Features/Readings/SubmitReadingTests.cs` — MakeDb/MakeFunctionContext/SeedFlatAsync helpers

### Learning from Story 3.1 to Apply Here

- **`SingleOrDefaultAsync` with combined predicate** — After Story 3.1's code review, the flat lookup is `db.Flats.SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct)` — one call that combines ID and tenant check. Do NOT do a two-step lookup.
- **Error shape**: anonymous objects `{ title, status, detail }` — no typed ProblemDetails class, no `type` field (deferred cross-cutting concern per review).
- **`decimal` everywhere** — Flagged repeatedly across reviews. `DailyAvgKwh`, `WeeklyAvgKwh`, etc. are ALL `decimal`. The `(decimal)totalDays` cast converts `double` from `TimeSpan.TotalDays`.
- **`DateTimeOffset.UtcNow`** — Use for the `now` parameter passed from `GetDashboardFunction`. Never `DateTime.UtcNow`.
- **Scoped services take AppDbContext** — `TariffResolver` takes `AppDbContext` → must be `AddScoped<TariffResolver>()`, never `AddSingleton`.

### References

- Architecture API table: [`_bmad-output/planning-artifacts/architecture.md#AD-13`]
- Architecture VSA structure: [`_bmad-output/planning-artifacts/architecture.md#Backend: Vertical Slice Architecture`]
- KpiCalculator as pure computation target: [`_bmad-output/project-context.md#Highest-value targets`]
- TariffResolver as central domain invariant: [`_bmad-output/project-context.md#Data integrity invariants`]
- 10 non-negotiable backend rules: [`_bmad-output/project-context.md#The 10 non-negotiable backend rules`]
- Function class shape: [`_bmad-output/project-context.md#Function class shape`]
- Pattern file — tenant check + GET: [`api/Features/Settings/GetUserSettingsFunction.cs`]
- Pattern file — function shape: [`api/Features/Readings/SubmitReadingFunction.cs`]
- Pattern file — tests: [`api.Tests/Features/Readings/SubmitReadingTests.cs`]

## Review Findings

- [x] [Review][Defer] dailyAvgCost silent underestimation when some reading intervals have no tariff — `totalCost` only accumulates for intervals that have a tariff, but `dailyAvgCost = totalCost / totalDays` divides by the full reading span. If early intervals pre-date any tariff, cost is understated with no signal to the caller. AC-4 is silent on what to do when no tariff is active for a period. See `KpiCalculator.cs:40`. — deferred, for team discussion after the code review
- [x] [Review][Defer] No test for null/empty userId (unauthenticated path) [`api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs`] — deferred, pre-existing gap across all function tests; SWA Easy Auth makes this path unreachable in production

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Implemented TariffResolver as a Scoped EF-backed service for period-accurate tariff lookup (DB query).
- Implemented KpiCalculator as a pure Singleton with no DB dependency — all computation uses pre-fetched lists; enables clean unit testing without mocking.
- GetDashboardFunction uses two-query pattern (readings + tariffs), passes both to KpiCalculator, returns OkObjectResult with DashboardSummary.
- Period-accurate costing uses in-memory ResolveTariff loop per interval; ProjectedMonthlyCost uses current tariff at DateTimeOffset.UtcNow.
- All 58 tests pass: 5 TariffResolverTests, 9 KpiCalculatorTests, 4 GetDashboardFunctionTests + 40 pre-existing.
- Build: 0 warnings, 0 errors.

### File List

**New files:**
- api/Shared/TariffResolver.cs
- api/Features/Dashboard/DashboardModels.cs
- api/Features/Dashboard/KpiCalculator.cs
- api/Features/Dashboard/GetDashboardFunction.cs
- api.Tests/Shared/TariffResolverTests.cs
- api.Tests/Features/Dashboard/KpiCalculatorTests.cs
- api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs

**Modified files:**
- api/Program.cs (added using EnergyTracker.Api.Features.Dashboard; + AddScoped<TariffResolver>() + AddSingleton<KpiCalculator>())
- _bmad-output/implementation-artifacts/sprint-status.yaml (status: review)
- _bmad-output/implementation-artifacts/3-2-kpi-dashboard-backend-computation.md (this file)
