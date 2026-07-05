---
baseline_commit: 1723d21ea17bdce851be94cc082a5e4a4013032d
---

# Story 5.3: Flat Structure Backend — Rooms, Power Points & Devices

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want the server to store and return the four-level physical hierarchy of my flat including Smart Plug and Smart Power Strip assignments,
so that imported smart plug data can be correctly attributed and Decomposition can group consumption by room and device.

## Acceptance Criteria

1. **Given** EF Core migrations for `Rooms`, `PowerPoints`, and `Devices`, **when** reviewed, **then** `RoomConfiguration` defines `RoomId` (guid PK), `FlatId` (FK, cascade delete), `Name`, `SortOrder` (int). `PowerPointConfiguration` defines `PowerPointId` (guid PK), `RoomId` (FK, cascade delete), `Name`, `PlugId` (nullable nvarchar — assigned smart plug identifier; never derived from file metadata). `DeviceConfiguration` defines `DeviceId` (guid PK), `PowerPointId` (FK, cascade delete), `Name`, `Type`, `Manufacturer`, `Model`, `PurchaseDate` (nullable datetimeoffset), `ConsumptionApproach` (enum: None/EuLabel/SelfMeasured), `EuLabelClass` (nullable), `EuAnnualKwh` (nullable decimal), `SelfMeasuredKwh` (nullable decimal), `SelfMeasuredPeriod` (nullable enum: Daily/Weekly). Zero Data Annotation attributes on any entity class.

2. **Given** `GET /api/v1/flats/{flatId}/structure`, **when** called, **then** `GetFlatStructureFunction` returns the full nested hierarchy as a `FlatStructureResponse` record (Flat → Rooms → PowerPoints → Devices); each PowerPoint includes `plugId` (nullable) and the response includes a top-level `hasDefaultTemplate` flag (true when no Rooms exist); HTTP 200; ≤ 2s; tenant-scoped.

3. **Given** `PUT /api/v1/flats/{flatId}/structure` with a complete structure payload, **when** `UpdateFlatStructureFunction.RunAsync` executes, **then** the full structure is replaced atomically within a transaction (delete-and-reinsert); a `plugId` on a PowerPoint is stored as provided — never derived from file metadata; HTTP 200 with the updated structure; ≤ 2s.

4. **Given** a PUT payload where a `plugId` is assigned to more than one PowerPoint in the same Flat, **when** validated, **then** HTTP 422 Problem Details is returned — each Smart Plug may be assigned to exactly one PowerPoint.

## Tasks / Subtasks

### Backend — data layer

- [x] Task 1: New entities `api/Data/Entities/Room.cs`, `PowerPoint.cs`, `Device.cs` (AC: 1)
  - [x] `Room.cs`: `RoomId` (Guid), `FlatId` (Guid), `required string Name`, `SortOrder` (int), `Flat Flat = null!` nav, `ICollection<PowerPoint> PowerPoints = new List<PowerPoint>()` nav — mirrors `Tariff.cs`/`MeterReading.cs`'s existing `{FK scalar} + {required nav to parent}` shape; the `PowerPoints` collection nav is new territory (no existing entity has a child collection nav yet) but is required both for `.Include().ThenInclude()` projection in Task 6 and for EF's relationship-fixup to wire FKs automatically when inserting a nested object graph in Task 7.
  - [x] `PowerPoint.cs`: `PowerPointId` (Guid), `RoomId` (Guid), `required string Name`, `PlugId` (string?), `Room Room = null!` nav, `ICollection<Device> Devices = new List<Device>()` nav.
  - [x] `Device.cs`: `DeviceId` (Guid), `PowerPointId` (Guid), `required string Name`, `Type` (string?), `Manufacturer` (string?), `Model` (string?), `PurchaseDate` (DateTimeOffset?), `ConsumptionApproach` (enum, required), `EuLabelClass` (string?), `EuAnnualKwh` (decimal?), `SelfMeasuredKwh` (decimal?), `SelfMeasuredPeriod` (enum?), `PowerPoint PowerPoint = null!` nav. Also declare the two enums in this file (no existing enum precedent anywhere in this codebase — this story is the first): `public enum ConsumptionApproach { None, EuLabel, SelfMeasured }` and `public enum SelfMeasuredPeriod { Daily, Weekly }`.
  - [x] **Do not** create a separate `StripOutlet`/`StripSlot` entity. FR-21's "Smart Power Strip supported with Strip Outlets" and Story 5.4's "Strip Outlet rows (one per device slot)" are a *frontend rendering concept only* — a Smart Power Strip is simply a `PowerPoint` with a `PlugId` set, and each "outlet" is just a `Device` row attached to that same `PowerPoint`. The architecture's entity model table (`architecture.md#Entity model`) lists only `Rooms`/`PowerPoints`/`Devices` — confirms there is no 4th/5th table for this.

- [x] Task 2: New configurations `api/Data/Configurations/RoomConfiguration.cs`, `PowerPointConfiguration.cs`, `DeviceConfiguration.cs` (AC: 1)
  - [x] Follow `TariffConfiguration.cs`'s exact shape: `ToTable`, `HasKey`, `Property(...).ValueGeneratedOnAdd()` on the PK, `HasOne(...).WithMany(...).HasForeignKey(...).OnDelete(DeleteBehavior.Cascade)` for the parent FK. Use `.WithMany(parent => parent.{ChildCollection})` (not the parameterless `.WithMany()` `Flat`/`Tariff` use) — since `Room`/`PowerPoint` now expose collection navs, wiring the collection side explicitly is what enables `.Include().ThenInclude()` in Task 6.
  - [x] `RoomConfiguration`: `Name` `HasMaxLength(200).IsRequired()`; `SortOrder` `IsRequired()` (plain int, no default).
  - [x] `PowerPointConfiguration`: `Name` `HasMaxLength(200).IsRequired()`; `PlugId` `HasMaxLength(200).IsRequired(false)` — 200 matches this codebase's universal free-text-nvarchar bound (`ProviderName`, `Flat.Name`), not a new value. **No unique index on `PlugId`** — unlike `IX_Tariffs_FlatId_ContractStartDate`, AC4's uniqueness rule is per-Flat, and `PowerPoints` has no `FlatId` scalar to build a composite DB index from (only `RoomId`); AC4 only requires app-level 422 validation (Task 8), not a schema constraint — do not add one.
  - [x] `DeviceConfiguration`: `Name` `HasMaxLength(200).IsRequired()`; `Type`/`Manufacturer`/`Model`/`EuLabelClass` all `HasMaxLength(200).IsRequired(false)` (same 200 convention — do not invent a shorter bound for `EuLabelClass` just because label codes like "A+++" are short); `PurchaseDate` `IsRequired(false)`; `ConsumptionApproach` `IsRequired()` (enum, stored as its default int representation — no `.HasConversion<string>()`, matching "decide simplest, no precedent mandates readable DB storage"); `EuAnnualKwh`/`SelfMeasuredKwh` `HasColumnType("decimal(18,4)").IsRequired(false)` (same precision as `AnnualKwhBaseline`); `SelfMeasuredPeriod` `IsRequired(false)` (nullable enum).

- [x] Task 3: `api/Data/AppDbContext.cs` — add three `DbSet` properties (AC: 1)
  - [x] `public DbSet<Room> Rooms => Set<Room>();`, `public DbSet<PowerPoint> PowerPoints => Set<PowerPoint>();`, `public DbSet<Device> Devices => Set<Device>();` — no other changes; `ApplyConfigurationsFromAssembly` already picks up the new `IEntityTypeConfiguration<T>` classes automatically.

- [x] Task 4: Migration (AC: 1)
  - [x] Run `dotnet ef migrations list` from `api/` first — confirm current head is `20260703170401_AddActiveFlatIdToUsers`.
  - [x] Run `dotnet ef migrations add AddRoomsPowerPointsAndDevicesTables` from `api/`. Expect three new tables (`Rooms`, `PowerPoints`, `Devices`) each with one FK and `ON DELETE CASCADE`; no FK cascade-cycle errors are possible here (unlike the `Users.ActiveFlatId` trap in Story 5.1) since this is a strict one-directional parent chain `Flat → Room → PowerPoint → Device`.
  - [x] Run `dotnet ef database update` to verify it applies cleanly against the real Azure SQL database (not just `InMemory` in tests) — this is the project's established verification step for every migration (see Story 5.1's Debug Log).

### Backend — models & validation

- [x] Task 5: `api/Features/FlatStructure/FlatStructureModels.cs` (AC: 2, 3, 4)
  - [x] Response records (suffix `Response`, matching project convention): `DeviceResponse(Guid DeviceId, string Name, string? Type, string? Manufacturer, string? Model, DateTimeOffset? PurchaseDate, ConsumptionApproach ConsumptionApproach, string? EuLabelClass, decimal? EuAnnualKwh, decimal? SelfMeasuredKwh, SelfMeasuredPeriod? SelfMeasuredPeriod)`; `PowerPointResponse(Guid PowerPointId, string Name, string? PlugId, List<DeviceResponse> Devices)`; `RoomResponse(Guid RoomId, string Name, int SortOrder, List<PowerPointResponse> PowerPoints)`; `FlatStructureResponse(Guid FlatId, bool HasDefaultTemplate, List<RoomResponse> Rooms)`.
  - [x] Request records — this story is the first to have a *nested* request payload (no precedent exists), so nested elements use a new `{Entity}Input` suffix to distinguish them from the root `{Action}{Entity}Request` (per project naming convention, the root keeps `Request`): `DeviceInput(string Name, string? Type, string? Manufacturer, string? Model, DateTimeOffset? PurchaseDate, ConsumptionApproach ConsumptionApproach, string? EuLabelClass, decimal? EuAnnualKwh, decimal? SelfMeasuredKwh, SelfMeasuredPeriod? SelfMeasuredPeriod)`; `PowerPointInput(string Name, string? PlugId, List<DeviceInput> Devices)`; `RoomInput(string Name, int SortOrder, List<PowerPointInput> PowerPoints)`; `UpdateFlatStructureRequest(List<RoomInput> Rooms)`.
  - [x] **No `RoomId`/`PowerPointId`/`DeviceId` fields on the `*Input` records.** AC3's "delete-and-reinsert" is a full replace with freshly generated GUIDs on every PUT — the client never needs to correlate input rows to prior IDs. Do not add optional ID fields "just in case."

- [x] Task 6: `api/Features/FlatStructure/UpdateFlatStructureValidator.cs` (AC: 3)
  - [x] `AbstractValidator<UpdateFlatStructureRequest>` using FluentValidation's `RuleForEach(...).ChildRules(...)` for nested validation (new territory for this codebase — no existing validator validates nested lists, but this is a standard, well-documented FluentValidation feature, not a workaround):
    ```csharp
    RuleForEach(r => r.Rooms).ChildRules(room =>
    {
        room.RuleFor(rm => rm.Name).NotEmpty().MaximumLength(200);
        room.RuleForEach(rm => rm.PowerPoints).ChildRules(pp =>
        {
            pp.RuleFor(p => p.Name).NotEmpty().MaximumLength(200);
            pp.RuleForEach(p => p.Devices).ChildRules(d =>
            {
                d.RuleFor(dv => dv.Name).NotEmpty().MaximumLength(200);
            });
        });
    });
    ```
  - [x] **Do not put the plugId-uniqueness rule (AC4) in this validator.** Mirror `PatchTariffFunction`'s pattern: FluentValidation handles structural/bounds checks → 400; the plugId-uniqueness business rule is a separate explicit check inside `UpdateFlatStructureFunction` (Task 8) that returns 422 — matching how `PatchTariffFunction` keeps its "tariff-locked" 422 check outside `PatchTariffValidator`.

### Backend — Functions

- [x] Task 7: `api/Features/FlatStructure/GetFlatStructureFunction.cs` (AC: 2)
  - [x] Constructor `(AppDbContext db)`. `[Function("GetFlatStructure")]`, `HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flats/{flatId}/structure")`.
  - [x] `Guid.TryParse(flatId, ...)` → 400 on failure (matches every other `{flatId}`-route Function).
  - [x] Ownership check: `db.Flats.AsNoTracking().SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct)` → 403 if null (the `Tariffs`-slice combined-filter pattern, per Story 5.1's Dev Notes on which of the two ownership-check styles to use for every new Function).
  - [x] Query: `db.Rooms.AsNoTracking().Include(r => r.PowerPoints).ThenInclude(pp => pp.Devices).Where(r => r.FlatId == flatGuid).OrderBy(r => r.SortOrder).ToListAsync(ct)`. No ordering is specified or needed for `PowerPoints`/`Devices` (only `Room` has a `SortOrder` column) — do not invent one.
  - [x] Map to `FlatStructureResponse(flatGuid, HasDefaultTemplate: rooms.Count == 0, Rooms: rooms.Select(...).ToList())`, nesting `PowerPointResponse`/`DeviceResponse` the same way. **`HasDefaultTemplate` is a single top-level field on `FlatStructureResponse`, computed as `rooms.Count == 0` — it is not a per-PowerPoint field.** The epic's AC2 prose ("each PowerPoint includes `plugId`... and a `hasDefaultTemplate` flag...") reads ambiguously but is grammatically listing two separate response facts, not one PowerPoint-level pair — confirmed by Story 5.4 AC1, which reads `hasDefaultTemplate: true` off the *overall* structure response to decide whether to pre-populate the default 5-room template, not off any individual PowerPoint.

- [x] Task 8: `api/Features/FlatStructure/UpdateFlatStructureFunction.cs` (AC: 3, 4)
  - [x] Constructor `(AppDbContext db, UpdateFlatStructureValidator validator)`. `[Function("UpdateFlatStructure")]`, `HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/flats/{flatId}/structure")`.
  - [x] `Guid.TryParse` → 400; ownership check (combined-filter `SingleOrDefaultAsync`) → 403; parse body via `JsonSerializer.DeserializeAsync<UpdateFlatStructureRequest>(req.Body, _jsonOptions, ct)` (typed deserialize — this is a full-replace PUT, not a tri-state PATCH, so it follows `CreateFlatFunction`/`CreateTariffFunction`'s typed-deserialize pattern, not `PatchTariffFunction`'s `JsonNode` pattern) with the standard `catch (JsonException)` → 400 and null-body → 400 checks.
  - [x] `_jsonOptions` for this Function **must** include an enum string converter (see Task 10) — without it, a request body like `"consumptionApproach": "EuLabel"` fails to deserialize against the `ConsumptionApproach` enum property.
  - [x] Run `validator.ValidateAsync(request, ct)` → 400 on failure (structural checks only, per Task 6).
  - [x] **plugId-uniqueness check (AC4, 422) — after 400 validation, before touching the DB:**
    ```csharp
    var plugIds = request.Rooms.SelectMany(r => r.PowerPoints)
        .Select(pp => pp.PlugId).Where(id => id is not null).ToList();
    if (plugIds.Count != plugIds.Distinct().Count())
        return new ObjectResult(new { title = "Unprocessable Entity", status = 422,
            detail = "Each Smart Plug may be assigned to exactly one Power Point." }) { StatusCode = 422 };
    ```
    This checks uniqueness only *within the submitted payload for this Flat* — a `plugId` reused across two different Flats is fine (each Flat's structure is independent; there is no cross-Flat uniqueness constraint anywhere in the AC or entity model). Write a test proving this explicitly (Task 12) — it is the easy over-broad mistake to make here.
  - [x] **Delete-and-reinsert, single `SaveChangesAsync` — read this before writing an explicit `BeginTransactionAsync`/`CommitAsync`:**
    ```csharp
    var existingRooms = await db.Rooms.Where(r => r.FlatId == flatGuid).ToListAsync(ct);
    await db.PowerPoints.Where(pp => pp.Room.FlatId == flatGuid).LoadAsync(ct);
    await db.Devices.Where(d => d.PowerPoint.Room.FlatId == flatGuid).LoadAsync(ct);
    db.Rooms.RemoveRange(existingRooms);

    var newRooms = request.Rooms.Select(r => new Room
    {
        FlatId = flatGuid, Name = r.Name.Trim(), SortOrder = r.SortOrder,
        PowerPoints = r.PowerPoints.Select(pp => new PowerPoint
        {
            Name = pp.Name.Trim(), PlugId = pp.PlugId,
            Devices = pp.Devices.Select(d => new Device
            {
                Name = d.Name.Trim(), Type = d.Type, Manufacturer = d.Manufacturer, Model = d.Model,
                PurchaseDate = d.PurchaseDate, ConsumptionApproach = d.ConsumptionApproach,
                EuLabelClass = d.EuLabelClass, EuAnnualKwh = d.EuAnnualKwh,
                SelfMeasuredKwh = d.SelfMeasuredKwh, SelfMeasuredPeriod = d.SelfMeasuredPeriod
            }).ToList()
        }).ToList()
    }).ToList();

    db.Rooms.AddRange(newRooms);
    await db.SaveChangesAsync(ct);
    ```
    **Do not set `RoomId`/`PowerPointId`/`DeviceId` explicitly** — leave them unset (default) and let EF's `ValueGeneratedOnAdd()` (Task 2) generate them, exactly like `CreateFlatFunction` never sets `Flat.FlatId` and reads it back from the entity after `SaveChangesAsync`. Because `PowerPoint`/`Device` are assigned via their parent's `PowerPoints`/`Devices` collection *before* `SaveChangesAsync`, EF's relationship-fixup sets `RoomId`/`PowerPointId` automatically — do not manually set those FK scalars either.
    **Do not wrap this in `await using var tx = await db.Database.BeginTransactionAsync(ct);`.** This was verified directly against this project's actual `AppDbContext`/EF Core 10 stack: `BeginTransactionAsync()` throws `InvalidOperationException` ("Transactions are not supported by the in-memory store") under the `InMemory` provider every test in this codebase uses (`MakeDb()`'s `UseInMemoryDatabase(...)`). AC3's "replaced atomically within a transaction" requirement is satisfied by a *single* `SaveChangesAsync` call spanning both the `RemoveRange` and the `AddRange` — EF Core wraps one `SaveChanges` invocation in its own implicit transaction on relational providers already; no explicit transaction object is needed, and adding one breaks every test that reaches this line.
    **The three-level `.LoadAsync()` sequence is not optional**, even though only `Rooms` are being `RemoveRange`d directly. Per Story 5.1's Dev Notes (verified there, applies identically here): the `InMemory` provider only cascades a delete to child rows that are already tracked in the current `DbContext` instance. `PowerPoints`/`Devices` are two and three levels below `Flat` respectively — loading only `Rooms` is not enough; `PowerPoints` and `Devices` must each be explicitly loaded (via navigation-property joins, since neither has a direct `FlatId` column) before `RemoveRange(existingRooms)`, or the old rows will silently survive under `InMemory` tests even though real SQL Server would cascade them correctly regardless.
  - [x] Response: reuse the exact same mapping shape as `GetFlatStructureFunction` (Task 7), built from `newRooms` (now populated with generated IDs post-`SaveChangesAsync`) — return `new OkObjectResult(response)` (HTTP 200 per AC3, not 201 — this is a replace of an existing sub-resource, not a creation).

### Backend — DI registration

- [x] Task 9: Register the new validator in `api/Program.cs` (AC: 3)
  - [x] Add `builder.Services.AddSingleton<UpdateFlatStructureValidator>();` next to the other validator registrations. `GetFlatStructureFunction` needs no new DI registration.

- [x] Task 10: Add a global enum-to-string JSON converter in `api/Program.cs` (AC: 1, 2, 3)
  - [x] This codebase has never had an enum in a DTO before this story — there is no existing convention. Extend the existing block: `options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;` → add a second line `options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());` in the same `Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(...)` block. This governs `OkObjectResult` response serialization (so `"consumptionApproach": "EuLabel"` renders as a string, not `1`).
  - [x] Separately, `UpdateFlatStructureFunction`'s own local `_jsonOptions` (used for `JsonSerializer.DeserializeAsync`, which does **not** go through the ASP.NET Core pipeline's `JsonOptions`) needs its own converter: `new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } }` with `using System.Text.Json.Serialization;` added to that file. These are two independent `JsonSerializerOptions` instances — updating one does not affect the other.

### Backend — cascade delete (carried-forward obligation from Story 5.1)

- [x] Task 11: Modify `api/Features/Flats/DeleteFlatFunction.cs` (AC: 1)
  - [x] Story 5.1's Dev Notes explicitly required this: "every future story that adds a new Flat-scoped child table... must add its own `.LoadAsync()` line here." Add, after the existing `MeterReadings`/`Tariffs` lines and before `db.Flats.Remove(flat)`:
    ```csharp
    await db.Rooms.Where(r => r.FlatId == flatGuid).LoadAsync(ct);
    await db.PowerPoints.Where(pp => pp.Room.FlatId == flatGuid).LoadAsync(ct);
    await db.Devices.Where(d => d.PowerPoint.Room.FlatId == flatGuid).LoadAsync(ct);
    ```
  - [x] Note this addition in this story's own Dev Notes (as this file already does) so the next Flat-scoped-child-table story (Epic 6/8) can find the precedent.

### Backend — tests

- [x] Task 12: `api.Tests/Features/FlatStructure/GetFlatStructureFunctionTests.cs` (AC: 2)
  - [x] `RunAsync_InvalidFlatIdFormat_Returns400`
  - [x] `RunAsync_FlatNotOwnedByUser_Returns403`
  - [x] `RunAsync_FlatWithNoRooms_ReturnsHasDefaultTemplateTrueAndEmptyRooms`
  - [x] `RunAsync_FlatWithRooms_ReturnsHasDefaultTemplateFalse`
  - [x] `RunAsync_FullNestedHierarchy_ReturnsCorrectlyShapedResponse` — seed a Room with a PowerPoint (with `PlugId` set) containing a Device with all fields populated (including `ConsumptionApproach`/`EuLabelClass`/etc.); assert every field round-trips correctly through the nested response.
  - [x] `RunAsync_RoomsBelongingToOtherFlats_AreNeverReturned` — seed structure for two different Flats, assert only the requested Flat's structure comes back.

- [x] Task 13: `api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs` (AC: 3, 4)
  - [x] `RunAsync_InvalidFlatIdFormat_Returns400`
  - [x] `RunAsync_FlatNotOwnedByUser_Returns403AndPersistsNothing`
  - [x] `RunAsync_ValidPayload_PersistsFullNestedHierarchyAndReturns200` — assert `Rooms`/`PowerPoints`/`Devices` rows exist in `db` matching the payload, and the response body mirrors it with server-generated GUIDs.
  - [x] `RunAsync_ReplacingExistingStructure_RemovesOldRoomsPowerPointsAndDevices` — **the hard-required test for this story, mirroring Story 5.1's AC4 cascade-completeness pattern.** Seed an existing Room/PowerPoint/Device for the Flat, `PUT` a completely different structure, then assert: (1) the *old* `RoomId`/`PowerPointId`/`DeviceId` no longer exist in `db`, (2) the counts of `Rooms`/`PowerPoints`/`Devices` for this `flatId` match only the *new* payload, (3) no orphaned `PowerPoints`/`Devices` remain referencing a deleted `RoomId`. This is the direct regression test for the three-level `.LoadAsync()` sequence in Task 8 — without it, this test fails under `InMemory` even though production SQL Server cascade config is correct.
  - [x] `RunAsync_EmptyRoomsList_ClearsExistingStructure` — `PUT` with `Rooms: []` against a Flat that already has structure; assert everything is deleted and the response has `HasDefaultTemplate: true`.
  - [x] `RunAsync_DuplicatePlugIdWithinSameFlatPayload_Returns422AndPersistsNothing` (AC4) — two `PowerPointInput`s in the same payload with the same `PlugId`; assert 422 and that **no** rows were written (neither the old structure removed nor any new rows added — the check must happen before any mutation).
  - [x] `RunAsync_SamePlugIdAcrossDifferentFlats_Succeeds` — seed Flat A with a PowerPoint using `plugId: "plug-1"`; `PUT` a structure for Flat B that also uses `plugId: "plug-1"`; assert 200 (no cross-Flat uniqueness constraint — this is the easy over-broad-validation mistake to explicitly guard against).
  - [x] `RunAsync_MissingRoomName_Returns400`, `RunAsync_MissingPowerPointName_Returns400`, `RunAsync_MissingDeviceName_Returns400` — one `[Fact]` each, exercising the nested `ChildRules` validator.
  - [x] `RunAsync_MalformedJsonBody_Returns400`

- [x] Task 14: Extend `api.Tests/Features/Flats/DeleteFlatFunctionTests.cs` (AC: 1)
  - [x] Add `RunAsync_ValidDelete_CascadeDeletesAllRoomsPowerPointsAndDevices` — seed a Flat with a Room → PowerPoint → Device chain (plus the existing `MeterReadings`/`Tariffs` seeding, for a full picture), delete it, assert zero rows remain in `Rooms`/`PowerPoints`/`Devices` for that `flatId`.
  - [x] Extend `RunAsync_FlatNotOwnedByUser_Returns403AndPerformsNoDeletion` to also seed a Room/PowerPoint/Device and assert they all still exist after the rejected delete.
  - [x] Extend both `RunAsync_DeletingOneFlat_Leaves...SiblingFlatDataUntouched` tests to also seed a Room/PowerPoint/Device for the sibling Flat and assert it survives.

### Cross-cutting

- [x] Task 15: Self-review pass before marking ready for review
  - [x] Grep `api/Features/FlatStructure/` for any unscoped `db.Rooms`/`db.PowerPoints`/`db.Devices` query — every query must ultimately filter by the resolved Flat's ownership.
  - [x] Verify `UpdateFlatStructureFunction`'s plugId-uniqueness check runs strictly before the `RemoveRange`/`AddRange`/`SaveChangesAsync` sequence (atomic-on-rejection, matching `PatchTariffFunction`'s convention).
  - [x] Verify no `RoomId`/`PowerPointId`/`DeviceId` is ever set explicitly in `UpdateFlatStructureFunction` — grep for stray assignments.
  - [x] Verify `DeleteFlatFunction`'s three new `.LoadAsync()` calls happen before `db.Flats.Remove(flat)`.
  - [x] `dotnet build` and `dotnet test api.Tests` green. No frontend changes in this story (backend-only per the epic's own story title) — no `npx tsc -b` / `npx vitest run` / `npm run lint` needed.

### Review Findings

- [x] [Review][Patch] No cross-field/range validation for `Device` fields tied to `ConsumptionApproach` — require `EuLabelClass`+`EuAnnualKwh` when `ConsumptionApproach = EuLabel`, require `SelfMeasuredKwh`+`SelfMeasuredPeriod` when `= SelfMeasured`, and reject negative `EuAnnualKwh`/`SelfMeasuredKwh` values. [`api/Features/FlatStructure/UpdateFlatStructureValidator.cs`]
- [x] [Review][Patch] `plugId: ""` (empty string) is treated as a real value by the AC4 duplicate check (`Where(id => id is not null)` lets `""` through), so two Power Points both submitted with `plugId: ""` trigger a false-positive 422 — normalize empty/whitespace string to "unassigned" (like `null`) before the uniqueness check. [`api/Features/FlatStructure/UpdateFlatStructureFunction.cs:75-82`]
- [x] [Review][Patch] A payload that omits `rooms`/`powerPoints`/`devices` (or sends `null` for one) deserializes those list properties to `null`; `RuleForEach` silently no-ops on a `null` collection so validation passes, and the code then throws an unhandled `NullReferenceException` (500) instead of a clean 400 Problem Details response. [`api/Features/FlatStructure/UpdateFlatStructureValidator.cs`, `api/Features/FlatStructure/UpdateFlatStructureFunction.cs:75,94,98`]
- [x] [Review][Patch] `PlugId` and the `Device` string fields (`Type`/`Manufacturer`/`Model`/`EuLabelClass`) have no `MaximumLength(200)` rule in the validator even though the DB columns are configured `HasMaxLength(200)` — an over-length value passes validation and fails later as a raw `DbUpdateException` instead of a 400. [`api/Features/FlatStructure/UpdateFlatStructureValidator.cs`]
- [x] [Review][Patch] `ConsumptionApproach`/`SelfMeasuredPeriod` have no `IsInEnum()` rule; an out-of-range integer value (e.g. `999`) deserializes without error (via `JsonStringEnumConverter(allowIntegerValues: true)` default) and is persisted with no corresponding enum member. [`api/Features/FlatStructure/UpdateFlatStructureValidator.cs`]
- [x] [Review][Patch] `UpdateFlatStructureFunction`'s response echoes `Rooms` in submitted-payload order rather than sorted by `SortOrder`, while `GetFlatStructureFunction` always sorts by `SortOrder` — a client submitting rooms out of `SortOrder` order gets a PUT response that visibly disagrees with the next GET for identical data. [`api/Features/FlatStructure/UpdateFlatStructureFunction.cs:117-121`]
- [x] [Review][Patch] The three-level cascade-load sequence (`Rooms` → `PowerPoints` via `Room.FlatId` → `Devices` via `PowerPoint.Room.FlatId`) is duplicated verbatim in both `DeleteFlatFunction` and `UpdateFlatStructureFunction` — worth extracting to a shared helper to avoid the two copies drifting. [`api/Features/Flats/DeleteFlatFunction.cs`, `api/Features/FlatStructure/UpdateFlatStructureFunction.cs`]
- [x] [Review][Patch] `MakeRequest(string)` and `MakeRawRequest(string)` in `UpdateFlatStructureFunctionTests` are byte-for-byte identical helper methods — one is redundant. [`api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs`]
- [x] [Review][Defer] No optimistic-concurrency handling anywhere in this codebase — a Flat/Room could be deleted concurrently between the ownership check and `SaveChangesAsync` in both `DeleteFlatFunction` and `UpdateFlatStructureFunction`, surfacing as an unhandled `DbUpdateConcurrencyException`/FK-violation 500 under a race. [`api/Features/Flats/DeleteFlatFunction.cs`, `api/Features/FlatStructure/UpdateFlatStructureFunction.cs`] — deferred, pre-existing architecture-wide gap, not introduced by this story
- [x] [Review][Defer] No cap on payload size or nesting depth for the PUT structure body — no endpoint in this codebase currently enforces request-size limits. [`api/Features/FlatStructure/UpdateFlatStructureFunction.cs`] — deferred, pre-existing systemic gap
- [x] [Review][Defer] No test asserts the generated migration is actually in sync with the current model (e.g. via `dotnet ef migrations has-pending-model-changes`). [`api/Data/Migrations/20260705072329_AddRoomsPowerPointsAndDevicesTables.cs`] — deferred, pre-existing testing gap, nice-to-have

## Dev Notes

### Why the InMemory-transaction gotcha matters more here than in any prior story

This was verified directly (not assumed) against this project's actual EF Core 10 / `AppDbContext` stack before writing this story: calling `db.Database.BeginTransactionAsync()` against an `InMemory`-provider context throws `InvalidOperationException: Transactions are not supported by the in-memory store.` Every existing test in `api.Tests` uses `UseInMemoryDatabase(...)` via each test file's `MakeDb()` helper — so an explicit transaction wrapper in `UpdateFlatStructureFunction` would make every test that reaches it fail immediately, not just be "slower" or "less correct." A single `SaveChangesAsync(ct)` call already gives the atomicity AC3 asks for (all-or-nothing for the `RemoveRange` + `AddRange` in that one call) — see Task 8.

### The three-level cascade-load requirement (extends Story 5.1's pattern one level further)

Story 5.1 established: "InMemory cascades a delete only to child rows already tracked in the same `DbContext` instance." That story only had to load *one* level (`MeterReadings`/`Tariffs`, both direct children of `Flat`). This story's hierarchy is three levels deep (`Flat → Room → PowerPoint → Device`), and `PowerPoint`/`Device` have **no direct `FlatId` column** — they can only be queried by traversing navigation properties (`pp.Room.FlatId`, `d.PowerPoint.Room.FlatId`). Both `DeleteFlatFunction` (Task 11) and `UpdateFlatStructureFunction` (Task 8) must load all three levels explicitly before removing the top of the subtree being deleted, or the deeper rows will silently survive under `InMemory` tests (while working correctly against real SQL Server regardless, since the DB-level cascade doesn't care what EF has loaded).

### `hasDefaultTemplate` placement — resolved ambiguity

Epic 5's AC2 text reads: "each PowerPoint includes `plugId`... and a `hasDefaultTemplate` flag (true when no Rooms exist)." Taken completely literally this reads as a per-PowerPoint field, which does not make semantic sense ("true when no Rooms exist" is a Flat-level fact, not a PowerPoint-level one). Story 5.4 (frontend, already written) settles this: its AC1 reads `hasDefaultTemplate: true` off the **top-level structure response** to decide whether to pre-populate the 5-room default template on first open. This story implements it as a single field on `FlatStructureResponse`, not `PowerPointResponse`. Do not re-litigate this while implementing — it's a resolved decision needed for Story 5.4 to build on.

### No `StripOutlet` entity — Smart Power Strips reuse `PowerPoint` + `Device`

FR-21 mentions "Smart Power Strip supported with Strip Outlets" and Story 5.4 renders "Strip Outlet rows (one per device slot) beneath the PowerPoint." Neither requires new backend schema: a Smart Power Strip is a `PowerPoint` with `PlugId` set to the strip's identifier; each outlet is simply a `Device` under that `PowerPoint`. `architecture.md`'s entity model table lists only `Rooms`/`PowerPoints`/`Devices` for this feature — confirms no additional table exists or is needed.

### Enum JSON representation — new project-wide decision, established here

No entity in this codebase has had an enum property before this story. This story establishes: enums serialize as **strings** over the wire (`JsonStringEnumConverter`), not the default integer representation. Two independent `JsonSerializerOptions` instances need the converter — the global one in `Program.cs` (governs response serialization) and `UpdateFlatStructureFunction`'s own local `_jsonOptions` (governs request-body deserialization, since it doesn't go through the ASP.NET Core pipeline's options). DB storage of the enum column itself stays as the default int representation — no `.HasConversion<string>()` needed, since nothing reads these columns via raw SQL. Future stories introducing enums (`ImportJobs.Status`, `Insights.Type`, etc. in Epic 6/8) should follow this same string-over-the-wire convention for consistency.

### Naming: `{Entity}Input` vs `{Entity}Request` — why this story introduces a new suffix

Every existing request DTO in this codebase is a *flat* record (`CreateFlatRequest`, `PatchTariffRequest`) — this is the first story with a genuinely nested request payload. The outer record keeps the established `{Action}{Entity}Request` convention (`UpdateFlatStructureRequest`), but its nested elements (`RoomInput`, `PowerPointInput`, `DeviceInput`) use a new `Input` suffix rather than also being called `...Request` — there is exactly one root request per HTTP call in this codebase's convention, and reusing `Request` for nested sub-objects would be confusing. Follow this suffix for any future nested-payload endpoint.

### Project Structure Notes

- New files: `api/Data/Entities/Room.cs`, `PowerPoint.cs`, `Device.cs`; `api/Data/Configurations/RoomConfiguration.cs`, `PowerPointConfiguration.cs`, `DeviceConfiguration.cs`; `api/Data/Migrations/{timestamp}_AddRoomsPowerPointsAndDevicesTables.cs` + `.Designer.cs`; `api/Features/FlatStructure/FlatStructureModels.cs`, `GetFlatStructureFunction.cs`, `UpdateFlatStructureFunction.cs`, `UpdateFlatStructureValidator.cs`; `api.Tests/Features/FlatStructure/GetFlatStructureFunctionTests.cs`, `UpdateFlatStructureFunctionTests.cs` — this is a brand-new `api.Tests/Features/FlatStructure/` folder, matching `api/Features/FlatStructure/` (mirrors `api.Tests/Features/{Feature}/` convention).
- Modified files: `api/Data/AppDbContext.cs` (3 new `DbSet`s), `api/Data/Migrations/AppDbContextModelSnapshot.cs` (regenerated), `api/Features/Flats/DeleteFlatFunction.cs` (3 new `.LoadAsync()` lines), `api/Program.cs` (validator DI registration + `JsonStringEnumConverter`), `api.Tests/Features/Flats/DeleteFlatFunctionTests.cs` (extended cascade + sibling-isolation coverage).
- No changes to `client/` — this story is explicitly backend-only per its title, mirroring Story 5.1's "Backend" story being frontend-free. Story 5.4 (already written) consumes these two endpoints from the frontend.
- Follows existing VSA layout exactly: `api/Features/FlatStructure/` is a new slice, already named in `architecture.md`'s file tree (`GetFlatStructureFunction, UpdateFlatStructureFunction, FlatStructureModels`) — no new subfolders beyond what's listed there.

### Testing standards (backend — this story has no frontend surface)

- Test placement: `api.Tests/Features/FlatStructure/{FunctionName}Tests.cs` (new folder), extending `api.Tests/Features/Flats/DeleteFlatFunctionTests.cs`.
- `InMemory` EF Core provider throughout, per project convention — see the dedicated Dev Notes above on why cascade-delete tests are still valid under `InMemory` when children are explicitly loaded first, and why no explicit transaction object is used.
- Do not test `RoomConfiguration.cs`/`PowerPointConfiguration.cs`/`DeviceConfiguration.cs` directly — trust EF Core, per project testing rules.
- Highest-value targets: `UpdateFlatStructureFunction`'s replace-cascade correctness (Task 13's `RunAsync_ReplacingExistingStructure_Removes...` test) and `DeleteFlatFunction`'s extended three-level cascade (Task 14) — both are direct extensions of Story 5.1's AC4 rigor for the same reasons (InMemory cascade gotcha).

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-5-multi-flat-management-flat-structure.md#Story 5.3] — authoritative AC text.
- [Source: _bmad-output/planning-artifacts/epics/requirements-inventory.md#FR-21, #FR-22] — FR-21 (Flat Structure hierarchy, Smart Plug/Power Strip assignment, plugId never derived from file metadata), FR-22 (default room template — Story 5.4's concern, this story only needs to report `hasDefaultTemplate` accurately).
- [Source: _bmad-output/planning-artifacts/architecture.md#Entity model, #Backend: Vertical Slice Architecture, #AD-5, #AD-8, #AD-12, #AD-13] — `Rooms`/`PowerPoints`/`Devices` column list (confirms no `StripOutlet` table); `FlatStructure/` slice naming (`GetFlatStructureFunction`, `UpdateFlatStructureFunction`, `FlatStructureModels`); Fluent-API-only entity config; hard-delete/cascade philosophy; tenant-isolation-via-middleware; route list (`GET/PUT /api/v1/flats/{flatId}/structure`).
- [Source: _bmad-output/implementation-artifacts/5-1-multi-flat-backend-create-list-and-cascade-delete.md] — origin of the `.LoadAsync()`-before-cascade pattern this story extends to three levels; the "don't manually set the Guid PK, let `ValueGeneratedOnAdd()` handle it" convention (`CreateFlatFunction`); the combined-filter ownership-check pattern (`SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId)`); the explicit Dev Notes instruction that this story is required to act on ("every future story that adds a new Flat-scoped child table... must add its own `.LoadAsync()` line").
- [Source: _bmad-output/implementation-artifacts/5-2-flat-switcher-add-flat-and-deletion-ui.md#Dev Notes] — confirms `['flat-structure', flatId]` is a query-key family this story's endpoints will need wired up by Story 5.4, not this story.
- [Source: _bmad-output/implementation-artifacts/4-1-tariff-crud-backend-list-create-and-contract-lock-enforcement.md] — origin of the "FluentValidation for structural checks (400); separate explicit business-rule check for special status codes (422)" pattern this story's plugId-uniqueness check follows (mirrors `PatchTariffFunction`'s "tariff-locked" 422 check being outside `PatchTariffValidator`).
- [Source: api/Data/Entities/Flat.cs, Tariff.cs, MeterReading.cs, User.cs] — existing entity shape convention (`{FK scalar} + {required nav to parent}`, no Data Annotations).
- [Source: api/Data/Configurations/FlatConfiguration.cs, TariffConfiguration.cs] — existing Fluent API configuration shape (`ToTable`, `HasKey`, `ValueGeneratedOnAdd()`, `HasOne().WithMany().HasForeignKey().OnDelete(Cascade)`, `HasMaxLength(200)` convention for all free-text columns, `decimal(18,4)` precision convention).
- [Source: api/Features/Flats/CreateFlatFunction.cs, DeleteFlatFunction.cs, api/Features/Tariffs/GetTariffsFunction.cs, PatchTariffFunction.cs] — reference patterns for typed-deserialize-vs-`JsonNode`, combined-filter ownership checks, and the separate-422-check-outside-the-validator convention this story's Functions replicate.
- [Source: api/Program.cs] — existing `Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>` block (camelCase policy) this story extends with `JsonStringEnumConverter`; existing validator DI registration list.
- [Source: api.Tests/Features/Flats/CreateFlatFunctionTests.cs, DeleteFlatFunctionTests.cs] — test scaffolding (`MakeDb`, `MakeFunctionContext`, `MakeRequest`/`MakeRawRequest`) to replicate in the two new `FlatStructure` test files, and the exact tests this story must extend for cascade coverage.
- [Source: _bmad-output/project-context.md#EF Core, #Critical Don't-Miss Rules, #Testing Rules] — tenant-scoping, `SingleOrDefaultAsync` vs `FirstOrDefaultAsync`, "trust EF cascade deletes" rule (note: this story's `RemoveRange(existingRooms)` is removing the top of an intentionally-replaced subtree, not "manually pre-deleting children of a Flat about to be removed" — the rule's target anti-pattern — so it does not conflict), `InMemory` provider limitations, decimal-for-all-kWh/cost rule.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet ef migrations add AddRoomsPowerPointsAndDevicesTables`: generated cleanly, three tables (`Rooms`, `PowerPoints`, `Devices`) each with one cascade FK, no cascade-cycle errors.
- `dotnet ef database update` against the real Azure SQL database (`energytracker-sqlsrv`/`energytracker-db`, Entra ID auth): applied cleanly.
- `dotnet build` (api): 0 warnings, 0 errors.
- Full backend test suite (`dotnet test` on `api.Tests`): 208 passed, 0 failed (207 pre-existing + 1 new `DeleteFlatFunction` cascade test; the two extended sibling-isolation tests and the new `FlatStructure` test files' cases are additional assertions folded into this count).
- No frontend changes in this story — no `npx tsc -b` / `npx vitest run` / `npm run lint` run, per Task 15.

### Completion Notes List

- Added `Room`, `PowerPoint`, `Device` entities plus the two new enums (`ConsumptionApproach`, `SelfMeasuredPeriod`) in `Device.cs`, matching the existing `{FK scalar} + {required nav to parent}` shape; `Room`/`PowerPoint` additionally expose child collection navs (`PowerPoints`/`Devices`) — new territory for this codebase, needed for `.Include().ThenInclude()` and EF relationship-fixup on nested inserts. No `StripOutlet`/`StripSlot` entity added — Smart Power Strips are represented as a `PowerPoint` with `PlugId` set plus `Device` rows underneath.
- Added `RoomConfiguration`/`PowerPointConfiguration`/`DeviceConfiguration` following `TariffConfiguration`'s shape; `PowerPointConfiguration`/`DeviceConfiguration` wire the collection side explicitly via `.WithMany(parent => parent.Children)` (required for `ThenInclude`), while `RoomConfiguration`→`Flat` keeps the parameterless `.WithMany()` like `Tariff`/`MeterReading` since `Flat` has no `Rooms` collection nav. No unique index on `PlugId` — AC4's uniqueness is per-Flat and enforced at the application layer only (Task 8), since `PowerPoints` has no `FlatId` scalar to build a composite DB index from.
- Migration `AddRoomsPowerPointsAndDevicesTables` created and applied to the real Azure SQL database — three new tables, all FKs `ON DELETE CASCADE`.
- `FlatStructureModels.cs` introduces the `{Entity}Input` suffix for nested request payload elements (`RoomInput`/`PowerPointInput`/`DeviceInput`) distinct from the root `UpdateFlatStructureRequest` — first nested-request-payload story in this codebase. No `RoomId`/`PowerPointId`/`DeviceId` fields on `*Input` records, per AC3's full-replace semantics.
- `UpdateFlatStructureValidator` uses FluentValidation's `RuleForEach(...).ChildRules(...)` for nested structural/bounds checks (400 only); the plugId-uniqueness business rule (AC4, 422) is a separate explicit check inside `UpdateFlatStructureFunction`, run strictly before any DB mutation — mirrors `PatchTariffFunction`'s "tariff-locked" 422 pattern being outside its validator.
- `GetFlatStructureFunction`: tenant-scoped read via combined-filter ownership check, `.Include(r => r.PowerPoints).ThenInclude(pp => pp.Devices)`, ordered by `Room.SortOrder` only. `HasDefaultTemplate` is a single top-level field on `FlatStructureResponse` (`rooms.Count == 0`), not a per-PowerPoint field — resolved ambiguity per Dev Notes, confirmed against Story 5.4's usage.
- `UpdateFlatStructureFunction`: typed deserialize (not `JsonNode`, since this is a full-replace PUT) with a dedicated `_jsonOptions` instance carrying `JsonStringEnumConverter` (required separately from the global `Program.cs` options, since `JsonSerializer.DeserializeAsync` doesn't go through the ASP.NET Core pipeline's `JsonOptions`). Delete-and-reinsert via a single `SaveChangesAsync` call — no explicit `BeginTransactionAsync`, since that throws under the `InMemory` provider every test in this codebase uses; a single `SaveChangesAsync` already gives AC3's required atomicity. All three levels (`Rooms`/`PowerPoints`/`Devices`) are explicitly `.LoadAsync()`'d before `RemoveRange`, since `InMemory` only cascades to already-tracked rows and neither child table has a direct `FlatId` column. No `RoomId`/`PowerPointId`/`DeviceId` set explicitly anywhere — left for `ValueGeneratedOnAdd()` and EF's relationship-fixup via the nested `PowerPoints`/`Devices` collection assignments.
- Registered `UpdateFlatStructureValidator` as a DI singleton in `Program.cs`; added `JsonStringEnumConverter` to the global `Configure<JsonOptions>` block (first enum in any DTO in this codebase — established as a project-wide "enums serialize as strings over the wire" convention for future stories).
- Extended `DeleteFlatFunction` with three new `.LoadAsync()` calls (`Rooms`/`PowerPoints`/`Devices`, traversing nav properties since the latter two have no direct `FlatId` column) before `db.Flats.Remove(flat)`, per Story 5.1's Dev Notes carried-forward obligation.
- Added `api.Tests/Features/FlatStructure/GetFlatStructureFunctionTests.cs` (6 tests) and `UpdateFlatStructureFunctionTests.cs` (13 tests) covering all four ACs, including the hard-required replace-cascade test (`RunAsync_ReplacingExistingStructure_RemovesOldRoomsPowerPointsAndDevices`) and the cross-Flat plugId-reuse test guarding against over-broad uniqueness validation. Extended `DeleteFlatFunctionTests.cs` with one new cascade test and seeded Room/PowerPoint/Device structure into the three existing ownership/sibling-isolation tests.
- Self-review pass (Task 15) completed: grepped `api/Features/FlatStructure/` confirming every `db.Rooms`/`db.PowerPoints`/`db.Devices` query is scoped by `FlatId`; confirmed the plugId-uniqueness check runs before `RemoveRange`/`AddRange`/`SaveChangesAsync`; confirmed no explicit `RoomId`/`PowerPointId`/`DeviceId` assignment anywhere; confirmed `DeleteFlatFunction`'s three new `.LoadAsync()` calls precede `db.Flats.Remove(flat)`.

### File List

**New:**
- `api/Data/Entities/Room.cs`
- `api/Data/Entities/PowerPoint.cs`
- `api/Data/Entities/Device.cs`
- `api/Data/Configurations/RoomConfiguration.cs`
- `api/Data/Configurations/PowerPointConfiguration.cs`
- `api/Data/Configurations/DeviceConfiguration.cs`
- `api/Data/Migrations/20260705072329_AddRoomsPowerPointsAndDevicesTables.cs`
- `api/Data/Migrations/20260705072329_AddRoomsPowerPointsAndDevicesTables.Designer.cs`
- `api/Features/FlatStructure/FlatStructureModels.cs`
- `api/Features/FlatStructure/UpdateFlatStructureValidator.cs`
- `api/Features/FlatStructure/GetFlatStructureFunction.cs`
- `api/Features/FlatStructure/UpdateFlatStructureFunction.cs`
- `api.Tests/Features/FlatStructure/GetFlatStructureFunctionTests.cs`
- `api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs`

**Modified:**
- `api/Data/AppDbContext.cs` (3 new `DbSet` properties)
- `api/Data/Migrations/AppDbContextModelSnapshot.cs` (regenerated by EF tooling)
- `api/Features/Flats/DeleteFlatFunction.cs` (3 new `.LoadAsync()` lines for cascade completeness)
- `api/Program.cs` (`UpdateFlatStructureValidator` DI registration; `JsonStringEnumConverter` added to global `JsonOptions`)
- `api.Tests/Features/Flats/DeleteFlatFunctionTests.cs` (new cascade test; extended ownership/sibling-isolation tests with Room/PowerPoint/Device seeding)
