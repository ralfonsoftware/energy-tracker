---
baseline_commit: 6fefacb
---

# Story 3.1: Meter Reading Submission — Backend

Status: done

## Story

As a user,
I want to submit a meter reading from my phone and have it persisted immediately,
so that I have a timestamped record of my consumption that the app can use for cost calculations.

## Acceptance Criteria

1. **POST creates a reading and returns 201** — Given `POST /api/v1/flats/{flatId}/readings` with a valid `kwhValue` (decimal > 0) and `readingDate` (datetimeoffset), when `SubmitReadingFunction.RunAsync` executes, then `TenantResolver` verifies `flatId` belongs to the resolved `UserId` (HTTP 403 otherwise); a `MeterReading` record is created with `ReadingId` (new guid), `FlatId`, `KwhValue` (decimal), `ReadingDate` (datetimeoffset), `IsCorrected = false`, `OriginalKwhValue = null`; HTTP 201 is returned with a `ReadingResponse` body and `Location` header pointing to `/api/v1/flats/{flatId}/readings/{readingId}`.

2. **Response time ≤ 2 seconds** — Given any valid reading submission, when the Function executes, then end-to-end server processing completes in ≤ 2 seconds (NFR-1 Tier 1). InMemory tests won't measure this but the implementation must not introduce unnecessary I/O.

3. **Retroactive readings stored with provided date** — Given a reading with a past `readingDate`, when submitted, then it is stored with the provided past date exactly as supplied (ISO 8601 with offset). No server-side date substitution. Future cost calculations will use `TariffResolver` to find the tariff active on the stored date — but `TariffResolver` is NOT part of this story; only storage matters here.

4. **Validation — kwhValue ≤ 0** — Given `ReadingValidator`, when `kwhValue ≤ 0`, then HTTP 400 Problem Details is returned; no `MeterReading` record is created.

5. **Validation — readingDate missing** — Given `ReadingValidator`, when `readingDate` is absent from the request body, then HTTP 400 Problem Details is returned; no record is created.

6. **EF Core entity compliance** — Given the `MeterReading` entity class and `MeterReadingConfiguration`, when reviewed, then: all column mappings use Fluent API; `KwhValue` and `OriginalKwhValue` are `decimal`; `ReadingDate` is `datetimeoffset`; zero Data Annotation attributes appear on the entity class.

## Tasks / Subtasks

- [x] Task 1: Create `MeterReading` entity and EF Core configuration (AC: 6)
  - [x] `api/Data/Entities/MeterReading.cs` — class with ReadingId (Guid), FlatId (Guid), KwhValue (decimal), ReadingDate (DateTimeOffset), IsCorrected (bool), OriginalKwhValue (decimal?), Flat navigation property
  - [x] `api/Data/Configurations/MeterReadingConfiguration.cs` — Fluent API config (see Dev Notes for complete spec)
  - [x] `api/Data/AppDbContext.cs` — add `public DbSet<MeterReading> MeterReadings => Set<MeterReading>();`

- [x] Task 2: Generate EF Core migration (AC: 6)
  - [x] Verify no pending migrations: `cd api && dotnet ef migrations list`
  - [x] Generate: `dotnet ef migrations add AddMeterReadingsTable`
  - [x] Verify the generated migration looks correct (Up creates MeterReadings table with FK to Flats + index)
  - [x] Apply locally: `dotnet ef database update`
  - [x] Do NOT hand-edit the generated migration file

- [x] Task 3: Create `ReadingModels.cs` — request and response DTOs (AC: 1)
  - [x] `api/Features/Readings/ReadingModels.cs` — two records: `SubmitReadingRequest(decimal KwhValue, DateTimeOffset? ReadingDate)` and `ReadingResponse(Guid ReadingId, decimal KwhValue, DateTimeOffset ReadingDate, bool IsCorrected, decimal? OriginalKwhValue)` (see Dev Notes)

- [x] Task 4: Create `ReadingValidator.cs` (AC: 4, 5)
  - [x] `api/Features/Readings/ReadingValidator.cs` — FluentValidation `AbstractValidator<SubmitReadingRequest>` (see Dev Notes for exact rules)
  - [x] `api/Program.cs` — register `builder.Services.AddSingleton<ReadingValidator>();` following the existing pattern

- [x] Task 5: Create `SubmitReadingFunction.cs` (AC: 1, 2, 3, 4, 5)
  - [x] `api/Features/Readings/SubmitReadingFunction.cs` — POST handler (see Dev Notes for complete implementation guide)
  - [x] Route: `Route = "v1/flats/{flatId}/readings"` (no `/api` prefix — SWA strips it)
  - [x] Validate flatId is a parsable GUID → 400 if not
  - [x] Verify flat belongs to resolved userId → 403 if not
  - [x] Deserialize body with `JsonSerializer.DeserializeAsync<SubmitReadingRequest>(req.Body, _jsonOptions, ct)`
  - [x] Run FluentValidation → 400 if invalid
  - [x] Persist `MeterReading` → `db.SaveChangesAsync(ct)`
  - [x] Return `CreatedResult($"/api/v1/flats/{flatId}/readings/{reading.ReadingId}", response)`

- [x] Task 6: Create `SubmitReadingTests.cs` (AC: 1, 3, 4, 5)
  - [x] `api.Tests/Features/Readings/SubmitReadingTests.cs` — minimum 8 tests (see Dev Notes for full list)

- [x] Task 7: Final verification
  - [x] `cd api && dotnet build` exits 0, no warnings
  - [x] `cd api && dotnet test` — all tests pass including pre-existing ones
  - [x] Update File List in this story

## Dev Notes

### What Already Exists — Read Before Writing Any Code

**Entities already in codebase (do NOT duplicate):**
- `api/Data/Entities/User.cs` — `UserId` (string, OIDC sub claim), `LocaleOverride` (nullable string)
- `api/Data/Entities/Flat.cs` — `FlatId` (Guid), `UserId` (string FK), `Name`, `AnnualKwhBaseline` (decimal), `SpikeThreshold` (decimal, default 2.0m), `PlannedAnnualSpend` (decimal?), nav `User`
- `api/Data/Entities/Tariff.cs` — `TariffId` (Guid), `FlatId` (Guid FK), `EffectiveDate` (DateTimeOffset), `PricePerKwh` (decimal), `MonthlyBaseFee` (decimal), `ProviderName` (string?), `ContractStartDate` (DateTimeOffset?), `ContractDurationMonths` (int?), nav `Flat`

**AppDbContext** (`api/Data/AppDbContext.cs`):
```csharp
public DbSet<User> Users => Set<User>();
public DbSet<Flat> Flats => Set<Flat>();
public DbSet<Tariff> Tariffs => Set<Tariff>();
// Task 1: add MeterReadings here
```

**Program.cs registrations already present:**
```csharp
builder.Services.AddSingleton<LocaleResolver>();
builder.Services.AddSingleton<OnboardingValidator>();
builder.Services.AddSingleton<PatchFlatValidator>();
// Task 4: add ReadingValidator here
```

**Migrations already applied (in order):**
1. `20260628072011_InitialCreate` (Users table)
2. `20260628194216_AddFlatsTable`
3. `20260629135534_AddTariffsTable`

New migration for Task 2 will be migration #4.

**TariffResolver does NOT exist yet** — it is mentioned in the AC context for how retroactive readings will eventually drive period-accurate cost calculations, but `TariffResolver` is NOT used in `SubmitReadingFunction` and should NOT be created in this story. It will be created in Story 3.2 (KPI Dashboard Backend Computation).

### Task 1: MeterReading Entity

```csharp
// api/Data/Entities/MeterReading.cs
namespace EnergyTracker.Api.Data.Entities;

public class MeterReading
{
    public Guid ReadingId { get; set; }
    public Guid FlatId { get; set; }
    public decimal KwhValue { get; set; }
    public DateTimeOffset ReadingDate { get; set; }
    public bool IsCorrected { get; set; }
    public decimal? OriginalKwhValue { get; set; }
    public Flat Flat { get; set; } = null!;
}
```

### Task 1: MeterReadingConfiguration

```csharp
// api/Data/Configurations/MeterReadingConfiguration.cs
using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        builder.ToTable("MeterReadings");
        builder.HasKey(r => r.ReadingId);
        builder.Property(r => r.ReadingId).ValueGeneratedOnAdd();
        builder.Property(r => r.FlatId).IsRequired();
        builder.Property(r => r.KwhValue).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(r => r.ReadingDate).IsRequired();
        builder.Property(r => r.IsCorrected).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.OriginalKwhValue).HasColumnType("decimal(18,4)").IsRequired(false);
        builder.HasOne(r => r.Flat)
            .WithMany()
            .HasForeignKey(r => r.FlatId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.FlatId, r.ReadingDate })
            .HasDatabaseName("IX_MeterReadings_FlatId_ReadingDate");
    }
}
```

Pattern mirrors `TariffConfiguration.cs` exactly — follow it for naming, cascade delete, and index format.

### Task 3: ReadingModels

```csharp
// api/Features/Readings/ReadingModels.cs
namespace EnergyTracker.Api.Features.Readings;

public record SubmitReadingRequest(decimal KwhValue, DateTimeOffset? ReadingDate);

public record ReadingResponse(
    Guid ReadingId,
    decimal KwhValue,
    DateTimeOffset ReadingDate,
    bool IsCorrected,
    decimal? OriginalKwhValue);
```

`ReadingDate` is nullable in the request (`DateTimeOffset?`) so the validator can detect "missing" vs a supplied value. It is non-nullable in the response because a persisted reading always has a date.

### Task 4: ReadingValidator

```csharp
// api/Features/Readings/ReadingValidator.cs
using FluentValidation;

namespace EnergyTracker.Api.Features.Readings;

public class ReadingValidator : AbstractValidator<SubmitReadingRequest>
{
    public ReadingValidator()
    {
        RuleFor(r => r.KwhValue).GreaterThan(0m)
            .WithMessage("kwhValue must be greater than 0.");
        RuleFor(r => r.ReadingDate).NotNull()
            .WithMessage("readingDate is required.");
    }
}
```

Register as `Singleton` in `Program.cs` — no DB access, same pattern as `OnboardingValidator` and `PatchFlatValidator`.

### Task 5: SubmitReadingFunction

Follow the exact pattern established by `PatchFlatFunction` (primary constructor DI, Route without `/api`, `AuthorizationLevel.Anonymous`, `RunAsync`, `_jsonOptions`):

```csharp
// api/Features/Readings/SubmitReadingFunction.cs
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EnergyTracker.Api.Features.Readings;

public class SubmitReadingFunction(AppDbContext db, ReadingValidator validator)
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [Function("SubmitReading")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flats/{flatId}/readings")]
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

        var flat = await db.Flats.FirstOrDefaultAsync(f => f.FlatId == flatGuid, ct);
        if (flat is null || flat.UserId != userId)
            return new ObjectResult(new
            {
                title = "Forbidden", status = 403,
                detail = "Flat not found or access denied."
            }) { StatusCode = 403 };

        SubmitReadingRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<SubmitReadingRequest>(
                req.Body, _jsonOptions, ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Invalid JSON in request body."
            });
        }

        if (request is null)
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Request body is required."
            });

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return new BadRequestObjectResult(new
            {
                title = "Validation Error", status = 400,
                detail = errors
            });
        }

        var reading = new MeterReading
        {
            ReadingId = Guid.NewGuid(),
            FlatId = flatGuid,
            KwhValue = request.KwhValue,
            ReadingDate = request.ReadingDate!.Value,
            IsCorrected = false,
            OriginalKwhValue = null
        };

        db.MeterReadings.Add(reading);
        await db.SaveChangesAsync(ct);

        var response = new ReadingResponse(
            reading.ReadingId,
            reading.KwhValue,
            reading.ReadingDate,
            reading.IsCorrected,
            reading.OriginalKwhValue);

        return new CreatedResult(
            $"/api/v1/flats/{flatId}/readings/{reading.ReadingId}",
            response);
    }
}
```

**Critical notes:**
- `request.ReadingDate!.Value` is safe here because the validator already confirmed it is non-null
- `CreatedResult(uri, value)` sets HTTP 201 + `Location` header automatically
- The `Location` URI uses `/api/v1/...` (with the `/api` prefix) because that is what the frontend sees through SWA — the route template strips `/api` only for the Function's routing, but the client-facing URL retains it
- Error response shape uses anonymous objects matching the existing pattern in `PatchFlatFunction` — no typed Problem Details class

### Task 6: SubmitReadingTests

Use the same test infrastructure as `GetUserSettingsFunctionTests.cs` — InMemory EF Core, Mock FunctionContext, Shouldly assertions, Moq for mocking:

```csharp
// api.Tests/Features/Readings/SubmitReadingTests.cs
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Readings;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using System.Text;
using System.Text.Json;

namespace api.Tests.Features.Readings;

public class SubmitReadingTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FunctionContext MakeFunctionContext(string userId = "user-test-123")
    {
        var mock = new Mock<FunctionContext>();
        var items = new Dictionary<object, object> { ["UserId"] = userId };
        mock.Setup(c => c.Items).Returns(items);
        return mock.Object;
    }

    private static async Task<(Flat flat, AppDbContext db)> SeedFlatAsync(string userId = "user-test-123")
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = userId });
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = userId,
            Name = "Test Flat",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m
        };
        db.Flats.Add(flat);
        await db.SaveChangesAsync();
        return (flat, db);
    }

    private static HttpRequest MakeRequest(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "application/json";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return ctx.Request;
    }
}
```

**Minimum 8 test methods to implement:**

1. `RunAsync_ValidReading_Returns201WithReadingResponse` — valid kwhValue + readingDate → 201, response has correct values
2. `RunAsync_ValidReading_PersistsToDatabase` — after 201, verify `db.MeterReadings` contains the reading with `IsCorrected = false` and `OriginalKwhValue = null`
3. `RunAsync_ValidReading_SetsLocationHeader` — `CreatedResult.Location` equals `/api/v1/flats/{flatId}/readings/{readingId}`
4. `RunAsync_KwhValueZero_Returns400` — `kwhValue: 0` → `BadRequestObjectResult`
5. `RunAsync_KwhValueNegative_Returns400` — `kwhValue: -1` → `BadRequestObjectResult`
6. `RunAsync_MissingReadingDate_Returns400` — body with only `{ kwhValue: 50.3 }`, no `readingDate` → `BadRequestObjectResult`
7. `RunAsync_FlatNotOwnedByUser_Returns403` — seed flat with `userId="owner"`, call with `userId="intruder"` → 403 status code
8. `RunAsync_InvalidFlatIdGuid_Returns400` — `flatId = "not-a-guid"` → `BadRequestObjectResult`
9. *(Bonus)* `RunAsync_RetroactiveReading_StoresProvidedDate` — past `readingDate` stored exactly, not overwritten with server time

For test 7, use `new ObjectResult(...) { StatusCode = 403 }` — check `(result as ObjectResult)?.StatusCode.ShouldBe(403)`.

For test 8, pass the invalid flatId string as the `flatId` parameter to `RunAsync` directly.

**How to call the function in tests:**
```csharp
var fn = new SubmitReadingFunction(db, new ReadingValidator());
var result = await fn.RunAsync(req, flatId, ctx, CancellationToken.None);
```

Note the `flatId` parameter is a `string` — pass it directly, the Function runtime extracts it from the route but in tests you supply it manually.

### Architecture Compliance Checklist

- [ ] Route: `"v1/flats/{flatId}/readings"` — no leading `/api`
- [ ] Method name: `RunAsync` (always)
- [ ] `AuthorizationLevel.Anonymous` (SWA Easy Auth is the gate)
- [ ] Primary constructor for DI: `public class SubmitReadingFunction(AppDbContext db, ReadingValidator validator)`
- [ ] `_jsonOptions` is `private static readonly` field
- [ ] `decimal` for `KwhValue` and `OriginalKwhValue` at every layer — entity, DTO, response
- [ ] `DateTimeOffset` for `ReadingDate` — never `DateTime`
- [ ] DTO is a `record`; entity is a `class`
- [ ] EF Core config: all via Fluent API, zero Data Annotations on entity class
- [ ] `CancellationToken ct` threaded through all async calls (`FirstOrDefaultAsync(ct)`, `ValidateAsync(request, ct)`, `DeserializeAsync(..., ct)`, `SaveChangesAsync(ct)`)
- [ ] Tenant check before any data access (flat ownership verified by `flat.UserId != userId`)
- [ ] `ReadingValidator` registered as `Singleton` in `Program.cs`
- [ ] Error responses: anonymous objects with `title`, `status`, `detail` — same pattern as `PatchFlatFunction`
- [ ] `CreatedResult` for 201 — never `OkObjectResult`

### Project Structure Notes

New files follow the same VSA slice layout as existing features:
```
api/Features/Readings/
├── SubmitReadingFunction.cs     ← POST handler (this story)
├── ReadingModels.cs             ← SubmitReadingRequest + ReadingResponse (this story)
└── ReadingValidator.cs          ← FluentValidation (this story)
                                 ← GetReadingHistoryFunction.cs will be added in Story 3.6
```

```
api.Tests/Features/Readings/
└── SubmitReadingTests.cs        ← (this story)
```

The `GetReadingHistoryFunction.cs` (Story 3.6) will be added to the same `api/Features/Readings/` folder. Do NOT create a stub for it now.

### Learnings from Epic 2 to Apply Here

- **`DateTimeOffset` not `DateTime`** — previous code reviews flagged `DateTime.UtcNow` usage; always use `DateTimeOffset` for all timestamps. `MeterReading.ReadingDate` is `DateTimeOffset`, stored locale-neutrally.
- **`StreamReader` must be `using`** — P8 from Story 2.5 review: `using var reader = new StreamReader(...)`. This story uses `JsonSerializer.DeserializeAsync` directly on `req.Body` (no StreamReader needed), but remember this for any future string-based reads.
- **Catch `JsonException` specifically** — P9 from Story 2.5 review: never use bare `catch {}`. This story's `try/catch` catches `JsonException` only.
- **Validators registered as Singleton** — `OnboardingValidator` was reviewed (W2 in deferred-work.md) as possibly better as `Transient`, but team pattern is `Singleton`. Follow existing pattern. `ReadingValidator` is stateless so no correctness risk.
- **`dotnet ef migrations list` before adding** — confirmed in project conventions. Run this first to confirm migration order is correct.
- **No upper-bound validation yet** — deferred pattern: W3 from Story 2.4 notes no upper-bound on `KwhValue`. Keep consistent — validate only `> 0`; extreme values are a future concern.

### References

- Architecture entity model: [`_bmad-output/planning-artifacts/architecture.md#Data Architecture`]
- API table (route format): [`_bmad-output/planning-artifacts/architecture.md#AD-13`]
- VSA slice structure: [`_bmad-output/planning-artifacts/architecture.md#Backend: Vertical Slice Architecture`]
- Function class pattern: [`project-context.md#Function class shape`]
- 10 non-negotiable backend rules: [`project-context.md#The 10 non-negotiable backend rules`]
- TariffConfiguration pattern to follow: [`api/Data/Configurations/TariffConfiguration.cs`]
- PatchFlatFunction pattern to follow: [`api/Features/Flats/PatchFlatFunction.cs`]
- Test infrastructure pattern: [`api.Tests/Features/Settings/GetUserSettingsFunctionTests.cs`]

### Review Findings

- [x] [Review][Decision] Error responses missing `type` field — Deferred; pattern without `type` matches established project convention (PatchFlatFunction). Address in a global error-shape hardening pass.
- [x] [Review][Patch] Make `(FlatId, ReadingDate)` index unique — Added `.IsUnique()` to `MeterReadingConfiguration`, migration, and model snapshot. [`api/Data/Configurations/MeterReadingConfiguration.cs`]
- [x] [Review][Patch] Flat lookup query not tenant-scoped + `SingleOrDefaultAsync` — Changed to `db.Flats.SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct)`; removed redundant app-level ownership check. [`api/Features/Readings/SubmitReadingFunction.cs:34`]
- [x] [Review][Defer] No upper bound on `KwhValue` — explicitly deferred in spec dev notes; pattern consistent with existing validators. [`api/Features/Readings/ReadingValidator.cs:9`] — deferred, pre-existing
- [x] [Review][Defer] No future-date rejection for `ReadingDate` — retroactive readings explicitly supported (AC3); no AC requires rejecting future dates in this story. [`api/Features/Readings/ReadingValidator.cs:11`] — deferred, pre-existing
- [x] [Review][Defer] `ReadingDate = DateTimeOffset.MinValue` passes `NotNull` validation — sentinel minimum-value date would be stored silently; broader date-range validation out of scope. [`api/Features/Readings/ReadingValidator.cs:11`] — deferred, pre-existing
- [x] [Review][Defer] `KwhValue` with >4 decimal places: response reflects pre-save value, DB stores rounded — EF/SQL Server truncates at `decimal(18,4)` without updating the in-memory entity; response may differ from stored value. Out of scope for this story. [`api/Features/Readings/SubmitReadingFunction.cs:80`] — deferred, pre-existing
- [x] [Review][Defer] Concurrent flat deletion between auth check and `SaveChangesAsync` → unhandled `DbUpdateException` → 500 — low-probability race condition; no cross-cutting exception handler in place yet. [`api/Features/Readings/SubmitReadingFunction.cs:34,86`] — deferred, pre-existing
- [x] [Review][Defer] `InMemoryDatabase` does not enforce FK constraints or `decimal(18,4)` precision — documented project-wide decision; previously tracked from Story 1.3 and 2.1 reviews. [`api.Tests/Features/Readings/SubmitReadingTests.cs`] — deferred, pre-existing
- [x] [Review][Defer] No `CreatedAt` server-side audit timestamp on `MeterReading` — retroactive submissions indistinguishable from on-time submissions without a submission time field. Out of scope for this story. [`api/Data/Entities/MeterReading.cs`] — deferred, pre-existing
- [x] [Review][Defer] No test for unauthenticated / null-`GetUserId()` path — not in the spec's required test list; auth boundary is covered by SWA Easy Auth + middleware (Story 1.4). [`api.Tests/Features/Readings/SubmitReadingTests.cs`] — deferred, pre-existing
- [x] [Review][Defer] `OperationCanceledException` not caught → noisy 500 telemetry on client disconnect — pre-existing cross-cutting concern across all Functions; not introduced by this story. [`api/Features/Readings/SubmitReadingFunction.cs`] — deferred, pre-existing
- [x] [Review][Defer] `ReadingDate` timezone offset not normalized to UTC before storage — AC3 explicitly requires storing the date exactly as supplied; same-instant submissions with different offsets will differ in the index. Accepted per spec; revisit if deduplication is added. [`api/Features/Readings/SubmitReadingFunction.cs:80`] — deferred, pre-existing

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Implemented MeterReading entity, MeterReadingConfiguration (Fluent API, decimal(18,4), DateTimeOffset), and AppDbContext DbSet.
- Generated migration `20260630155345_AddMeterReadingsTable` — creates MeterReadings table with PK, FK to Flats (cascade), composite index on (FlatId, ReadingDate).
- SubmitReadingFunction follows PatchFlatFunction pattern: primary constructor DI, route without /api, AuthorizationLevel.Anonymous, CancellationToken threaded through all async calls.
- ReadingValidator registered as Singleton in Program.cs, consistent with OnboardingValidator and PatchFlatValidator.
- 9 tests implemented (8 required + bonus retroactive reading test): all pass. Full suite: 40/40 passing, 0 regressions.

### File List

**Backend (new):**
- `api/Data/Entities/MeterReading.cs`
- `api/Data/Configurations/MeterReadingConfiguration.cs`
- `api/Data/Migrations/20260630155345_AddMeterReadingsTable.cs`
- `api/Data/Migrations/20260630155345_AddMeterReadingsTable.Designer.cs`
- `api/Features/Readings/ReadingModels.cs`
- `api/Features/Readings/ReadingValidator.cs`
- `api/Features/Readings/SubmitReadingFunction.cs`
- `api.Tests/Features/Readings/SubmitReadingTests.cs`

**Backend (modified):**
- `api/Data/AppDbContext.cs` — add `MeterReadings` DbSet
- `api/Data/Migrations/AppDbContextModelSnapshot.cs` (auto-updated by EF tooling)
- `api/Program.cs` — register `ReadingValidator`
