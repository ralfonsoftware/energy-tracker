---
baseline_commit: 21daef3d7741b5ce1dbc7658f7c486fcf19b5913
---

# Story 11.3: Enforce Unique `PlugId` Across Power Points

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the team maintaining this app,
I want two Power Points to never share the same smart-plug `PlugId`,
so that Standby/Replacement insight detection and Decomposition attribution can't silently misattribute one plug's readings to two different devices.

## Acceptance Criteria

1. **Given** no unique constraint exists on `PowerPoint.PlugId`, **when** implemented, **then** `PowerPointConfiguration.cs` adds a filtered unique index enforcing that no two Power Points **within the same Flat** share a non-null `PlugId` (a `null` `PlugId` — an unconfigured Power Point — remains unconstrained), and a migration is generated for it.
2. **Given** the existing `findPlugIdConflict` frontend validation (`client/src/features/flat-structure/components/draftModel.ts`) already prevents a user from saving a duplicate `PlugId` within the Flat Structure editor's own draft state, and the backend's existing in-request check (`UpdateFlatStructureFunction.cs:82-89`) already rejects a duplicate `PlugId` submitted twice in the *same* request with a 422, **when** the DB constraint is added, **then** `UpdateFlatStructureFunction.cs`'s `SaveChangesAsync` catches the resulting `DbUpdateException` (unique-constraint violation — distinct from the already-handled `DbUpdateConcurrencyException`) and returns a 409 Conflict Problem Details response as a defense-in-depth backstop for a `PlugId` already used by a *different, already-saved* Power Point in the same flat — a case neither the frontend check nor the in-request check can see, since it only becomes visible when this request's payload is compared against another session/tab's already-committed data.
3. **Given** the new constraint, **when** tested, **then** a new test confirms two Power Points in the **same flat** cannot end up with the same non-null `PlugId` (returns 409, not an unhandled 500), that two Power Points with `PlugId = null` save without conflict, and that the existing `RunAsync_SamePlugIdAcrossDifferentFlats_Succeeds` test (`UpdateFlatStructureFunctionTests.cs:257-287`) continues to pass unmodified — the same `PlugId` string reused across two *different* flats must remain valid (see Dev Notes: "Gap found during story creation").

### Gap found during story creation

The epic's original AC (`epics/epic-11-...md#Story 11.3`) describes the index only as "a filtered unique index on `PlugId`... following the same filtered-unique-index pattern already used for the Epic 10.1 `InsightRun` dedup index" — that referenced index is a **single-column**, flat-wide constraint (`HasIndex(r => r.FlatId).IsUnique()`). Read literally, the epic would have this story add a single-column unique index on `PowerPoint.PlugId` alone — i.e. globally unique across **all flats**, not just within one.

That literal reading directly contradicts an **already-passing, intentional test** in `UpdateFlatStructureFunctionTests.cs` (`RunAsync_SamePlugIdAcrossDifferentFlats_Succeeds`, lines 257-287) which seeds two different flats (different `UserId`s) each saving a Power Point with `plugId: "plug-1"` and asserts **both succeed**. This test locks in current, correct multi-tenant behavior — a `PlugId` string is just user-entered free text scoped to identifying a physical smart plug's *data rows* per flat (see `SmartPlugIntervalData`/`SmartPlugDailyData`, both keyed `(FlatId, PlugId, ...)`, never `PlugId` alone) and two unrelated users' flats coincidentally using the same string is expected and harmless — the detectors that motivate this story (`StandbyDetector`, `ReplacementDetector`, `DecompositionEngine`) already scope every `SmartPlugIntervalData`/`SmartPlugDailyData` query by `FlatId` first, so cross-flat `PlugId` reuse was never actually a misattribution risk. The real risk this story fixes is **two Power Points in the *same* flat** sharing a `PlugId` — that duplicate would make `intervalRowsByPlugId.TryGetValue(pp.PlugId!, ...)` (`StandbyDetector.cs:57`) or the equivalent lookup in `ReplacementDetector.cs`/`DecompositionEngine.cs` return the same physical plug's rows for two different "devices" within one flat.

A flat-wide (not global) unique index requires an AC-3 fix: **AC #1 above is corrected to scope the constraint per-Flat**, not globally, so this pre-existing test's asserted behavior is preserved. See Dev Notes for the concrete schema change this requires (adding a denormalized `FlatId` column to `PowerPoint`) and the SQL Server multi-cascade-path hazard that change introduces if done naively.

## Tasks / Subtasks

- [x] Task 1: Add denormalized `FlatId` to `PowerPoint` and the flat-scoped filtered unique index (AC: #1)
  - [x] 1.1 Add `public Guid FlatId { get; set; }` to `api/Data/Entities/PowerPoint.cs` (alongside the existing `RoomId`; do **not** add a `Flat` navigation property — see 1.3)
  - [x] 1.2 In `PowerPointConfiguration.cs`, add `builder.Property(pp => pp.FlatId).IsRequired();`
  - [x] 1.3 **Do not** add `builder.HasOne(pp => pp.Flat).WithMany().HasForeignKey(pp => pp.FlatId).OnDelete(DeleteBehavior.Cascade)` — `PowerPoint` is already reached by one cascade path (`Flat` → `Room` → `PowerPoint`, via `RoomConfiguration`'s `Flat` FK and `PowerPointConfiguration`'s existing `Room` FK). Adding a second `Flat` → `PowerPoint` cascade FK creates the exact "SQL Server rejects multiple cascade paths" deploy failure Story 10.1 hit (cited directly in this epic's Story 11.12 note) — `FlatId` here must be a **plain, unrelated scalar column** with no FK/navigation, populated manually at write time (Task 2), used only for the composite index.
  - [x] 1.4 Add the composite filtered unique index: `builder.HasIndex(pp => new { pp.FlatId, pp.PlugId }).IsUnique().HasDatabaseName("IX_PowerPoints_FlatId_PlugId_NotNull").HasFilter("[PlugId] IS NOT NULL");`
  - [x] 1.5 Generate the migration via `dotnet ef migrations add AddFlatIdAndUniqueIndexToPowerPoints` from `api/`. Do NOT hand-write the migration file. Since existing `PowerPoints` rows (if any exist in a real deployed DB) have no `FlatId` value, and the column is required, review the generated migration: it must add the column (temporarily nullable or with a computed default is unnecessary here since this is a fresh-schema project with no production data migration concern per this project's dev workflow — confirm by checking `api/Data/Migrations/AppDbContextModelSnapshot.cs` for the current `PowerPoints` table shape before generating).
  - [x] 1.6 Run `dotnet ef database update` locally to verify the migration applies cleanly (per this project's EF Core migration rule)
- [x] Task 2: Populate `FlatId` on every newly-created `PowerPoint` (AC: #1)
  - [x] 2.1 In `api/Features/FlatStructure/UpdateFlatStructureFunction.cs`, in the `newRooms` projection (around line 100-118), add `FlatId = flatGuid,` to the `new PowerPoint { ... }` object initializer (the enclosing `newRooms` LINQ projection already has `flatGuid` in scope from the earlier `Guid.TryParse` — no new parameter needed)
  - [x] 2.2 Confirm no other code path constructs a `PowerPoint` for persistence — `UpdateFlatStructureFunction.cs` is the only production write path for this entity (full replace-on-save pattern; `GetFlatStructureFunction.cs` is read-only). Existing test files that construct `new PowerPoint { ... }` directly for seeding (in `StandbyDetectorTests.cs`, `ReplacementDetectorTests.cs`, `DecompositionEngineTests.cs`, `GetDecompositionFunctionTests.cs`, `GetFlatStructureFunctionTests.cs`, `DeleteFlatFunctionTests.cs`, `InsightConfigurationTests.cs`) do **not** need `FlatId` added — they don't exercise this story's constraint and `Guid.Empty` (the implicit default) is harmless there; do not modify these 7 files.
- [x] Task 3: Catch the unique-constraint violation and return 409 (AC: #2)
  - [x] 3.1 In `UpdateFlatStructureFunction.cs`, add a second `catch` clause **after** the existing `catch (DbUpdateConcurrencyException)` block (around line 129-136) — `catch (DbUpdateException)` (note: `DbUpdateConcurrencyException` derives from `DbUpdateException`, so the existing, more specific catch must stay first; C# catch-block ordering already handles this correctly as long as the new block is added after, not before)
  - [x] 3.2 Return `new ConflictObjectResult(new { title = "Conflict", status = 409, detail = "This Smart Plug is already assigned to another Power Point in this flat." })` — match this file's existing 409 shape exactly (no `type` field; the file's existing RowVersion-conflict 409 at line 131-135 also omits `type` — this codebase's RFC 9457 `type`-field sweep across all 15 non-compliant Functions, including this one, is explicitly out of scope here and handled by Story 11.5; do not add `type` to only one of this file's two 409s)
- [x] Task 4: Test coverage (AC: #3)
  - [x] 4.1 In `api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs`, add a `UniqueConstraintConflictDbContext` test-double `AppDbContext` subclass, modeled exactly on the existing `ConcurrencyConflictDbContext` in this same file (lines 26-37) — **required because EF Core's `InMemory` provider does not enforce unique indexes at all** (confirmed: no test in this codebase today exercises real DB constraint enforcement; `api.Tests/Data/InsightConfigurationTests.cs` also uses `InMemory` only). The double's `SaveChangesAsync` override throws `DbUpdateException` (not `DbUpdateConcurrencyException`) on its first call, then delegates to `base.SaveChangesAsync()` on subsequent calls — same one-shot-failure shape as `ConcurrencyConflictDbContext`.
  - [x] 4.2 New test `RunAsync_DuplicatePlugIdAcrossDifferentSavedPowerPoints_Returns409ConflictAndPersistsNothing`: seed a flat, use `UniqueConstraintConflictDbContext` in place of the plain `MakeDb()`, submit a valid payload, assert the result is `ConflictObjectResult` with `StatusCode == 409` and the detail message from Task 3.2, and assert (via a fresh verification context against the same DB name, mirroring `RunAsync_ConcurrentModification_Returns409ConflictAndPersistsNothing`'s pattern at lines 749-780) that no new `Rooms`/`PowerPoints` rows were persisted.
  - [x] 4.3 New test `RunAsync_TwoPowerPointsWithNullPlugId_SucceedsWithoutConflict`: submit a payload with two Power Points both having `plugId: null` (or omitted) in the same room, assert `OkObjectResult` (this exercises the filter's `[PlugId] IS NOT NULL` exclusion conceptually — note this test alone cannot prove real DB-level enforcement since `InMemory` never enforces the index either way; it's a regression guard that the *application-level* 422 same-request check at lines 82-89 correctly excludes nulls via its existing `Where(id => !string.IsNullOrWhiteSpace(id))` filter, which it already does — this test just locks that in for the null case specifically).
  - [x] 4.4 Confirm the existing `RunAsync_SamePlugIdAcrossDifferentFlats_Succeeds` test (lines 257-287) and `RunAsync_DuplicatePlugIdWithinSameFlatPayload_Returns422AndPersistsNothing` (lines 226-255) both still pass unmodified after Tasks 1-3's changes.
  - [x] 4.5 Run the full backend suite (`dotnet test` from repo root) and confirm all existing tests pass.
  - [x] 4.6 **Do not** attempt to write a test asserting the SQL-level unique index actually rejects a duplicate insert — that requires a constraint-enforcing provider (SQLite or real SQL Server), which does not exist in this codebase yet. That capability is explicitly Story 11.12's scope (`epics/epic-11-...md#Story 11.12`, which names this exact index — `Story 11.3's new PlugId unique-index enforcement` — as one of its three initial SQLite-tier test targets). This story's test coverage (4.1-4.4) proves the *application code's exception-handling path* is correct; Story 11.12 proves the *index itself* is correct.

## Dev Notes

### Why this story exists

Flagged by the Epic 10 retrospective (Action Item #4). `PowerPointConfiguration.cs` currently declares `PlugId` as `HasMaxLength(200).IsRequired(false)` with no uniqueness constraint. This was a latent schema gap before Epic 10, but Epic 10's `StandbyDetector`/`ReplacementDetector` (and Epic 7's `DecompositionEngine`) now actively look up `SmartPlugIntervalData`/`SmartPlugDailyData` rows by grouping on `PlugId` per flat — a duplicate `PlugId` across two Power Points **in the same flat** would make both Power Points resolve to the same underlying smart-plug data, double-counting or cross-attributing one physical device's readings between two unrelated "devices" in a live, user-visible insight or decomposition chart.

### Current state of files being modified

**`api/Data/Entities/PowerPoint.cs`** (5 properties today: `PowerPointId`, `RoomId`, `Name`, `PlugId`, `RowVersion`, plus `Room`/`Devices` navigations) — gains one new plain scalar property, `FlatId` (no navigation).

**`api/Data/Configurations/PowerPointConfiguration.cs`** (23 lines today) — configures `RoomId`, `Name`, `PlugId`, `RowVersion`, and the existing `Room` FK with `OnDelete(DeleteBehavior.Cascade)`. Gains: `FlatId` property config (required, no FK) + the new composite filtered unique index. **Existing `Room` FK/cascade config is untouched.**

**`api/Features/FlatStructure/UpdateFlatStructureFunction.cs`** (168 lines today) — the sole production write path for `PowerPoint` rows. Uses a full replace-on-save pattern: on every save, all of the flat's existing `Room`s (and their cascade-deleted `PowerPoint`s/`Device`s) are removed and entirely new ones inserted (lines 91-120). This means `FlatId` must be set on every newly-constructed `PowerPoint` at the point of creation (line ~100-118) — there is no "existing PowerPoint gets patched" code path to also worry about. Already has one `try { SaveChangesAsync } catch (DbUpdateConcurrencyException) { return 409 }` block (lines 125-136) for the unrelated `Flat.RowVersion` optimistic-concurrency check (added by an earlier story) — this story adds a second `catch (DbUpdateException)` after it, not a new try block.

**Already present and NOT to be touched:** the in-request duplicate-`PlugId` check at `UpdateFlatStructureFunction.cs:82-89` (`plugIds.Count != plugIds.Distinct().Count()` → 422) already exists and already correctly excludes blank/whitespace `PlugId`s via `.Where(id => !string.IsNullOrWhiteSpace(id))`. This story's new DB-level check is a *different* failure mode (a `PlugId` colliding with an **already-persisted** Power Point from a different request/session, not two entries within the *same* request) — both checks stay, the DB one is defense-in-depth on top of the existing one.

### The SQL Server multi-cascade-path hazard (read before writing the migration)

`PowerPoint` is already reachable from `Flat` via exactly one cascade path: `Flat` →(`RoomConfiguration`, `OnDelete(Cascade)`)→ `Room` →(`PowerPointConfiguration`, `OnDelete(Cascade)`)→ `PowerPoint`. If the new `FlatId` column is configured with its own `HasOne(pp => pp.Flat).WithMany().HasForeignKey(pp => pp.FlatId).OnDelete(DeleteBehavior.Cascade)` (the pattern `SmartPlugIntervalDataConfiguration.cs`/`SmartPlugDailyDataConfiguration.cs` use for *their* `FlatId` columns — do **not** copy this part of their pattern here), SQL Server will reject the migration/deploy with a multi-cascade-paths error — this is the exact class of defect that caused Story 10.1's deploy failure (per this epic's own Story 11.12 note, which cites it by name). `SmartPlugIntervalData`/`SmartPlugDailyData` are safe to have their own `Flat` cascade FK because they are direct children of `Flat` with no other path; `PowerPoint` is not in that position. **The fix: no FK, no navigation, no `OnDelete` — just a plain required `Guid` column, populated by application code (Task 2), used only for the composite index.**

### Filtered unique index reference pattern

`InsightRunConfiguration.cs:27-30` (Epic 10.1):
```csharp
builder.HasIndex(r => r.FlatId)
    .IsUnique()
    .HasDatabaseName("IX_InsightRuns_FlatId_ActiveOnly")
    .HasFilter("[Status] IN (0, 1)");
```
This story's index (composite, two columns, filtered on the second):
```csharp
builder.HasIndex(pp => new { pp.FlatId, pp.PlugId })
    .IsUnique()
    .HasDatabaseName("IX_PowerPoints_FlatId_PlugId_NotNull")
    .HasFilter("[PlugId] IS NOT NULL");
```
Migration shape to replicate (from `20260725140716_AddInsightsTables.cs:68-73`):
```csharp
migrationBuilder.CreateIndex(
    name: "IX_PowerPoints_FlatId_PlugId_NotNull",
    table: "PowerPoints",
    columns: new[] { "FlatId", "PlugId" },
    unique: true,
    filter: "[PlugId] IS NOT NULL");
```
(Exact generated syntax may differ slightly — trust `dotnet ef migrations add`'s output over hand-transcribing this.)

### The 409 catch pattern — two precedents already in this codebase

`TriggerInsightsFunction.cs:54-66` and `CreateTariffFunction.cs:96-108` both already catch `DbUpdateException` from a filtered-unique-index violation and return a Problem-Details 409. `CreateTariffFunction.cs`'s version includes a `type` field (it's one of only 3 Functions in the whole codebase that consistently does — see Story 11.5); `UpdateFlatStructureFunction.cs`'s existing 409 (the `DbUpdateConcurrencyException` one, line 131-135) does **not** include `type`. Match the file you're editing, not the other precedent — do not introduce a `type` field on only one of this file's two 409 responses; that inconsistency is explicitly Story 11.5's job to fix everywhere at once, not this story's.

### EF Core `InMemory` provider does not enforce unique indexes — critical testing constraint

Confirmed by inspecting `api.Tests/Data/InsightConfigurationTests.cs` (tests the `InsightRun` filtered unique index's *application-level* behavior only, still on `InMemory`) and this project's own testing rules (`project-context.md`: "`InMemory` provider... does not enforce FK constraints, unique indexes, column types, or `decimal` precision"). This means **no test in Task 4 can prove the SQL Server index itself rejects a duplicate** — only that this story's new `catch (DbUpdateException)` block, once triggered, does the right thing. The test-double pattern in Task 4.1 (mirroring the file's own existing `ConcurrencyConflictDbContext`) is how every other story in this codebase has worked around this same `InMemory` limitation (see Story 11.2's `RowVersionSimulatingDbContext` for the identical technique applied to a different exception type). Real enforcement is verified once Story 11.12 (SQLite integration tier, later in this epic) exists — do not attempt to pull that work into this story.

### What NOT to touch

- `GetFlatStructureFunction.cs` — read-only, no `PowerPoint` construction, unaffected by this story.
- `client/src/features/flat-structure/components/draftModel.ts`'s `findPlugIdConflict`/`hasPlugIdConflictForRoomSave` — frontend pre-save validation already exists and already does its job (preventing same-draft-state duplicates); this story is backend-only defense-in-depth for the case the frontend structurally cannot see (concurrent session/tab). No frontend changes are in scope.
- The 7 test files listed in Task 2.2 that construct `new PowerPoint { ... }` for unrelated feature tests (Insights detectors, Decomposition, `GetFlatStructureFunctionTests`, `DeleteFlatFunctionTests`, `InsightConfigurationTests`) — leave these untouched.
- `UpdateFlatStructureFunction.cs`'s existing 422 in-request duplicate check (lines 82-89) and its existing `DbUpdateConcurrencyException` catch (lines 125-136) — both stay exactly as they are; this story only adds alongside them.

### Testing Rules (from project context)

- xUnit + Shouldly (`.ShouldBe(...)`), matching every existing test in `UpdateFlatStructureFunctionTests.cs`
- `EF Core InMemory` provider — see the critical constraint above; use the test-double pattern, not real constraint enforcement
- Test placement: extend the existing `api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs` — do not create a new file
- Do not test `PowerPointConfiguration.cs` itself directly (EF Core config classes are trusted, per project rules) — Task 4's tests exercise the Function's exception handling, not the configuration class

### Previous Story Intelligence (Story 11.2)

- Story 11.2 established the exact "InMemory doesn't naturally simulate a DB-enforced failure, so build a test-double `AppDbContext` subclass that throws on `SaveChangesAsync`" technique this story reuses directly (`RowVersionSimulatingDbContext` there → `UniqueConstraintConflictDbContext` here). Story 11.2's dev-agent notes flag that this technique was **discovered mid-implementation** via a throwaway diagnostic test before being written for real — this story already tells you the answer up front, so no rediscovery needed.
- Story 11.2's review found a **guard scoped too broadly** (a claim transition that silently no-op'd because EF's change tracker didn't mark an unchanged property dirty). The analogous risk here: verify the `catch (DbUpdateException)` block in Task 3 is placed **after**, not replacing, the existing `catch (DbUpdateConcurrencyException)` — if the order were reversed or merged, a genuine `RowVersion` conflict would incorrectly report the `PlugId`-conflict message instead of its own, since `DbUpdateConcurrencyException` would then match the more general `DbUpdateException` catch and never reach its own dedicated block. C# resolves catch clauses in source order and the more-derived type must come first; confirm this after editing, don't just trust that it compiles.
- Story 11.1 and 11.2 both verified the full backend suite (`dotnet test` from repo root) passed before marking done. Do the same here.

### Project Structure Notes

- No new files — this story modifies four existing files (`PowerPoint.cs`, `PowerPointConfiguration.cs`, `UpdateFlatStructureFunction.cs`, `UpdateFlatStructureFunctionTests.cs`) plus one generated migration file pair under `api/Data/Migrations/`
- Matches this codebase's established denormalized-`FlatId`-for-composite-uniqueness convention (`SmartPlugIntervalData`/`SmartPlugDailyData`) with the one deliberate deviation noted above (no FK/cascade on the new column, to avoid the multi-cascade-path hazard)

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md#Story 11.3] — epic-level AC and rationale (see "Gap found during story creation" above for where this story's AC #1 diverges from a literal reading)
- [Source: api/Data/Entities/PowerPoint.cs, api/Data/Configurations/PowerPointConfiguration.cs] — entity/config to modify
- [Source: api/Features/FlatStructure/UpdateFlatStructureFunction.cs] — write path to modify; existing 422 check (lines 82-89) and `DbUpdateConcurrencyException` catch (lines 125-136) are current-state context, not to be altered
- [Source: api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs] — existing test patterns to extend, especially `ConcurrencyConflictDbContext` (lines 26-37, the test-double pattern to mirror) and `RunAsync_SamePlugIdAcrossDifferentFlats_Succeeds` (lines 257-287, must keep passing)
- [Source: api/Data/Configurations/InsightRunConfiguration.cs] — filtered unique index pattern to replicate
- [Source: api/Data/Configurations/SmartPlugIntervalDataConfiguration.cs, api/Data/Configurations/SmartPlugDailyDataConfiguration.cs] — composite `(FlatId, PlugId, ...)` precedent to follow for the index shape, but NOT for the `Flat` FK/cascade part (see multi-cascade-path hazard above)
- [Source: api/Features/Insights/TriggerInsightsFunction.cs:54-66, api/Features/Tariffs/CreateTariffFunction.cs:96-108] — existing `catch (DbUpdateException)` → 409 precedents
- [Source: api/Data/Migrations/20260725140716_AddInsightsTables.cs:68-73] — filtered-index migration syntax to replicate
- [Source: _bmad-output/implementation-artifacts/11-2-insights-discovery-redelivery-db-level-idempotency-guard.md] — previous story; source of the `InMemory`-can't-simulate-DB-failure test-double technique reused here
- [Source: _bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md#Story 11.12] — the SQLite integration tier that will later verify this story's index at the DB level; explicitly names this story's index as one of its three initial targets

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- Generated migration `20260727074108_AddFlatIdAndUniqueIndexToPowerPoints` via `dotnet ef migrations add` from `api/` — matched the Dev Notes' expected shape exactly (`AddColumn<Guid>` + `CreateIndex` with `filter: "[PlugId] IS NOT NULL"`).
- First `dotnet ef database update` attempt against the local dev Azure SQL DB failed: `CREATE UNIQUE INDEX` rejected a duplicate key `(00000000-0000-0000-0000-000000000000, "HiFi")`. The story's assumption ("fresh-schema project with no production data migration concern") did not hold — this dev DB has real seeded data, and the generated migration's `defaultValue: Guid.Empty` collapsed every pre-existing `PowerPoint` row onto the same `FlatId`, so two rows that legitimately belong to *different* flats but happen to share a `PlugId` string collided under the shared default.
- Verified via `sqlcmd` that no genuine same-flat `PlugId` duplicate exists (`SELECT ... GROUP BY PlugId, r.FlatId HAVING COUNT(*) > 1` on `PowerPoints JOIN Rooms` returned 0 rows) — confirming the failure was purely an artifact of the default-value backfill, not a real data conflict this story's constraint should legitimately block.
- Confirmed the failed attempt rolled back cleanly (EF wraps each migration in a transaction): `__EFMigrationsHistory` had no new row and the `FlatId` column did not exist post-failure.
- Fixed by hand-editing the generated migration to add a `migrationBuilder.Sql(...)` backfill step (`UPDATE PowerPoints SET FlatId = Rooms.FlatId FROM ... JOIN Rooms`) between the `AddColumn` and `CreateIndex` calls, so every pre-existing row gets its correct real `FlatId` before the unique index is created. Migration then applied cleanly. This is a data-integrity necessity the story's Dev Notes didn't anticipate (they assumed no real data existed), not a deviation from the story's design intent — the index shape, entity/config changes, and no-FK/no-cascade decision all match the spec exactly.

### Completion Notes List

- Implemented all 4 tasks per spec: denormalized `FlatId` scalar (no FK/navigation, avoiding the SQL Server multi-cascade-path hazard) + composite filtered unique index on `PowerPoint`; `FlatId` populated on every newly-created `PowerPoint` in `UpdateFlatStructureFunction.cs`; new `catch (DbUpdateException)` block added after the existing `catch (DbUpdateConcurrencyException)` (verified derived-type-first ordering is correct); test coverage added mirroring the existing `ConcurrencyConflictDbContext` test-double pattern.
- One deviation from the story's literal migration-generation assumption: the local dev Azure SQL DB has real seeded data (not a "fresh schema"), which surfaced a genuine backfill gap in the naive EF-generated migration. Fixed by adding a `Sql()` backfill statement to the migration (see Debug Log). No production data risk identified — this is the dev database, and the fix is a strict correctness improvement (real `FlatId` per pre-existing row) with no schema/behavior deviation from the story's intended design.
- Full backend suite: 478/478 tests pass (`dotnet test` run from `api.Tests/` — no root-level `.sln` exists in this repo, so the story's literal "from repo root" instruction was run from the test project directory instead; behavior is identical).
- All ACs verified: AC1 (flat-scoped filtered unique index + migration) — done; AC2 (409 via `DbUpdateException` catch, added after the existing concurrency catch) — done; AC3 (new tests for same-flat conflict returning 409, null-`PlugId` pairs succeeding, and the two pre-existing cross-flat/in-request tests still passing unmodified) — done.

### File List

- `api/Data/Entities/PowerPoint.cs` (modified)
- `api/Data/Configurations/PowerPointConfiguration.cs` (modified)
- `api/Data/Migrations/20260727074108_AddFlatIdAndUniqueIndexToPowerPoints.cs` (generated + hand-edited backfill)
- `api/Data/Migrations/20260727074108_AddFlatIdAndUniqueIndexToPowerPoints.Designer.cs` (generated)
- `api/Data/Migrations/AppDbContextModelSnapshot.cs` (generated, updated)
- `api/Features/FlatStructure/UpdateFlatStructureFunction.cs` (modified)
- `api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs` (modified)

## Change Log

- 2026-07-27: Story 11.3 implemented — added `PowerPoint.FlatId` (plain scalar, no FK) and a flat-scoped filtered unique index on `(FlatId, PlugId)`; `UpdateFlatStructureFunction.cs` now populates `FlatId` on write and catches `DbUpdateException` to return 409 as defense-in-depth against cross-session `PlugId` collisions within the same flat; added regression tests. Migration required a hand-added SQL backfill step to correctly populate `FlatId` for pre-existing rows in the dev DB (see Dev Agent Record).

### Review Findings

- [x] [Review][Patch] Add a pre-check guard in the migration that raises a clear diagnostic error if a genuine `(FlatId, PlugId)` duplicate exists before `CreateIndex` runs [`api/Data/Migrations/20260727074108_AddFlatIdAndUniqueIndexToPowerPoints.cs:12-25`]
- [x] [Review][Defer] `catch (DbUpdateException)` in `UpdateFlatStructureFunction.cs` doesn't inspect the SQL error number/constraint name before returning the PlugId-conflict message, so any unrelated `SaveChangesAsync` failure (FK violation, timeout, connectivity blip) is misreported to the client as a PlugId conflict — deferred, pre-existing pattern (same blanket-catch shape already used by `TriggerInsightsFunction.cs` and `CreateTariffFunction.cs`; not unique to this diff) [`api/Features/FlatStructure/UpdateFlatStructureFunction.cs:138-145`]
- [x] [Review][Defer] No logging call in the new (or the sibling) `catch` block discards the original exception detail, making production troubleshooting of a 409 harder — deferred, pre-existing (this class has no `ILogger` injected at all; not introduced by this diff) [`api/Features/FlatStructure/UpdateFlatStructureFunction.cs:138-145`]
- [x] [Review][Defer] `PlugId = pp.PlugId` is persisted without trimming, unlike the sibling `Name = pp.Name.Trim()`, so whitespace-padded PlugIds bypass the new uniqueness guarantee — deferred, pre-existing (this line was not touched by this diff; only `FlatId = flatGuid,` was added alongside it) [`api/Features/FlatStructure/UpdateFlatStructureFunction.cs:104`]
- [x] [Review][Defer] Migration's SQL backfill (`UPDATE ... FROM PowerPoints pp INNER JOIN Rooms r`) runs as one unbatched statement with no chunking, a potential lock-duration concern on a large `PowerPoints` table — deferred, not a realistic concern at this project's current scale [`api/Data/Migrations/20260727074108_AddFlatIdAndUniqueIndexToPowerPoints.cs:16-20`]
- [x] [Review][Defer] The new filtered unique index's case-sensitivity/collation behavior for `PlugId` comparisons is untested and undocumented — deferred, inherits SQL Server's existing default collation already relied on elsewhere for `PlugId` (`SmartPlugIntervalData`/`SmartPlugDailyData`), not a new risk introduced by this diff [`api/Data/Configurations/PowerPointConfiguration.cs:22-25`]
