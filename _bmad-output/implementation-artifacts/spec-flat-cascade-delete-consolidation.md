---
title: 'Consolidate DeleteFlatFunction cascade-load calls into one extension method'
type: 'refactor'
created: '2026-07-05'
status: 'done'
context: []
baseline_commit: '47746bd09959773c731e636d47684748f98d3274'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `DeleteFlatFunction.RunAsync` inlines three separate `.LoadAsync()` calls (`MeterReadings`, `Tariffs`, `Rooms`) plus a call to the existing `LoadPowerPointsAndDevicesAsync` helper, all needed only so EF Core's `InMemory` test provider cascades the delete deterministically (real SQL Server cascades regardless via FK constraints). Epic 6 Story 6.1 adds three more Flat-scoped child tables (`ImportJobs`, `SmartPlugDailyData`, `SmartPlugIntervalData`) that will need the same treatment, and today there's no single place to add them — a future author has to find and edit the Function body directly.

**Approach:** Extend `api/Shared/AppDbContextExtensions.cs` with one new extension method, `LoadFlatCascadeChildrenAsync(this AppDbContext db, Guid flatId, CancellationToken ct)`, that performs all of `DeleteFlatFunction`'s current pre-delete loads (`MeterReadings`, `Tariffs`, `Rooms`, and calling the existing `LoadPowerPointsAndDevicesAsync`). Replace the four separate calls in `DeleteFlatFunction.RunAsync` with one call to this new method. Do not touch `UpdateFlatStructureFunction` — it has a narrower need (only Rooms/PowerPoints/Devices) and already uses the shared `LoadPowerPointsAndDevicesAsync` helper correctly; forcing it onto the new, wider method would load unrelated tables it has no reason to touch.

## Boundaries & Constraints

**Always:**
- Preserve exact existing cascade-delete behavior — every table currently loaded before `db.Flats.Remove(flat)` must still be loaded, in a call that happens before `db.Flats.Remove(flat)`.
- Keep the new method in `api/Shared/AppDbContextExtensions.cs`, following the existing `LoadPowerPointsAndDevicesAsync` method's shape (static extension method, `this AppDbContext db`, `Guid flatId`, `CancellationToken ct` parameters, `Task` return type) — call the existing `LoadPowerPointsAndDevicesAsync` from inside the new method rather than duplicating its two `.LoadAsync()` lines.
- All existing `DeleteFlatFunctionTests.cs` tests must keep passing unmodified — this is a pure internal refactor with no behavior change, and this test file is the project's hard-required proof of cascade completeness (Story 5.1 AC4).

**Ask First:** None — this is a same-behavior internal refactor with no design decisions requiring approval.

**Never:**
- Do not modify `UpdateFlatStructureFunction.cs` or its use of `LoadPowerPointsAndDevicesAsync`.
- Do not add the three Epic 6 tables (`ImportJobs`/`SmartPlugDailyData`/`SmartPlugIntervalData`) to the new method now — they don't exist yet; Story 6.1 will extend this method when it introduces them.
- Do not wrap any of this in an explicit `BeginTransactionAsync` — this project's established, verified-necessary pattern is a single `SaveChangesAsync` call for atomicity (explicit transactions throw under the `InMemory` test provider).

</frozen-after-approval>

## Code Map

- `api/Shared/AppDbContextExtensions.cs` -- existing shared extension-method file; add `LoadFlatCascadeChildrenAsync` here, alongside the existing `LoadPowerPointsAndDevicesAsync`.
- `api/Features/Flats/DeleteFlatFunction.cs` -- replace its four inline load calls (lines 36-39) with a single call to the new method.
- `api.Tests/Features/Flats/DeleteFlatFunctionTests.cs` -- existing hard-required cascade-completeness tests (AC4 from Story 5.1); must all keep passing unmodified as the regression proof for this refactor. No test changes needed since behavior is identical.

## Tasks & Acceptance

**Execution:**
- [x] `api/Shared/AppDbContextExtensions.cs` -- add `public static async Task LoadFlatCascadeChildrenAsync(this AppDbContext db, Guid flatId, CancellationToken ct)` containing `db.MeterReadings.Where(r => r.FlatId == flatId).LoadAsync(ct)`, `db.Tariffs.Where(t => t.FlatId == flatId).LoadAsync(ct)`, `db.Rooms.Where(r => r.FlatId == flatId).LoadAsync(ct)`, and a call to `db.LoadPowerPointsAndDevicesAsync(flatId, ct)` -- gives Story 6.1 one place to extend for its three new tables
- [x] `api/Features/Flats/DeleteFlatFunction.cs` -- replace the four separate load lines with `await db.LoadFlatCascadeChildrenAsync(flatGuid, ct);` -- removes the duplication risk and shrinks the Function body to its actual business logic

**Acceptance Criteria:**
- Given a Flat with `MeterReadings`, `Tariffs`, `Rooms`/`PowerPoints`/`Devices`, when `DeleteFlatFunction.RunAsync` executes, then all of those rows are still cascade-deleted with zero rows remaining for the deleted `flatId` (unchanged from current behavior).
- Given the full existing `DeleteFlatFunctionTests.cs` suite, when run after this refactor, then every test still passes unmodified.

## Design Notes

The new method's name (`LoadFlatCascadeChildrenAsync`) intentionally reads as "all direct-and-nested children of this Flat, for cascade-delete purposes" — distinct from `LoadPowerPointsAndDevicesAsync`'s narrower, structure-specific name. This keeps the two methods' purposes clear as Epic 6 adds callers: `UpdateFlatStructureFunction` keeps calling the narrow one; `DeleteFlatFunction` (and only it) calls the wide one.

## Verification

**Commands:**
- `dotnet build` (from `api/`) -- expected: 0 errors
- `dotnet test api.Tests --filter FullyQualifiedName~DeleteFlatFunctionTests` -- expected: all existing tests pass unmodified
- `dotnet test api.Tests` -- expected: full suite green, no regressions elsewhere

## Suggested Review Order

- New shared helper consolidating all Flat-scoped cascade-child loads; comment explains the InMemory-provider rationale (added during review).
  [`AppDbContextExtensions.cs:14`](../../api/Shared/AppDbContextExtensions.cs#L14)

- Call site: four inline loads replaced with one call, same position (before `Remove`/`SaveChangesAsync`).
  [`DeleteFlatFunction.cs:36`](../../api/Features/Flats/DeleteFlatFunction.cs#L36)
