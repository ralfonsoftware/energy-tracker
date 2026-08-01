---
baseline_commit: 63cc052f36a57cbceac772255d3ca3b7a311199a
---

# Story 11.12: SQLite Integration Test Tier for Schema-Constraint Scenarios

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the team maintaining this app,
I want a lightweight integration test tier that actually enforces database constraints,
so that cascade-delete paths, unique indexes, and decimal-precision truncation can be verified by an automated test instead of only by manual `dotnet ef database update` runs and production incidents.

## Acceptance Criteria

1. **Given** no integration test project exists that runs against a real constraint-enforcing database engine, **when** implemented, **then** a new test collection (`api.Tests/Integration/`) uses EF Core's SQLite provider (`Microsoft.EntityFrameworkCore.Sqlite`, an in-process, no-external-dependency engine) against a real (non-InMemory) `:memory:` SQLite connection, applying **actual EF Core migrations** (via `Database.Migrate()`) rather than `EnsureCreated()`.

2. **Given** the new tier, **when** populated, **then** it initially covers: (a) the full cascade-delete chain from `DeleteFlatFunction`'s dependency graph — confirming every descendant row (`MeterReadings`, `Tariffs`, `Rooms`, `PowerPoints`, `Devices`, `ImportJobs`, `SmartPlugDailyData`, `SmartPlugIntervalData`, `InsightRuns`, `Insights`) is actually removed when a `Flat` is deleted, with no unhandled FK-violation exception; (b) Story 11.3's `IX_PowerPoints_FlatId_PlugId_NotNull` filtered unique index rejecting a same-flat duplicate `PlugId` while two different flats (or two `PlugId = null` rows) succeed; (c) at least one `decimal(18,4)`/`decimal(18,6)` column-scale truncation case. **See "Gap found during story creation" below — this AC's coverage is corrected relative to the epic's original wording, which described (a) as "confirming no multi-cascade-path rejection," a SQL-Server-only DDL restriction SQLite cannot reproduce.**

3. **Given** SQLite's known type-affinity differences from SQL Server (it does not enforce `decimal` precision/scale natively, and does not support database-generated concurrency tokens), **when** a test relies on decimal truncation or the schema includes a `RowVersion` column, **then** the test tier documents and works around each gap explicitly via an EF Core value converter (decimal rounding to match production's precision/scale) and a provider-specific concurrency-token downgrade (see Dev Notes) — never silently asserting behavior SQLite wouldn't actually produce.

### Gap found during story creation

The epic's original AC2 text names this tier's first target scenario as "confirming no multi-cascade-path rejection — the exact class of defect that caused Story 10.1's deploy failure." Investigation during story creation (`_bmad-output/implementation-artifacts/investigations/story-10-1-deploy-failure-investigation.md`) confirms that defect was **SQL Server error 1785** ("may cause cycles or multiple cascade paths"), a schema-DDL-time restriction specific to SQL Server. **SQLite has no equivalent restriction** — it will happily create a schema with two referential-action paths reaching the same table, so a SQLite-based test asserting "the migration applies without a multi-cascade-path error" would trivially always pass, proving nothing, and would give **false confidence**: it could not catch a regression of the exact Story 10.1 defect class, because that defect only manifests against SQL Server.

This story's SQLite tier is corrected to verify what it *can* actually prove: that the **cascade delete's data-level effect is complete and correct** — every descendant row across all ten Flat-scoped tables is actually removed, with no `DbUpdateException`/FK violation, when a `Flat` is deleted through a real relational engine with FK enforcement on (SQLite enables `PRAGMA foreign_keys` by default via `SQLitePCLRaw.bundle_e_sqlite3`, EF Core's default native bundle — no explicit pragma needed). This is still a valuable regression guard (today's InMemory-based `DeleteFlatFunctionTests` only pass because `LoadFlatCascadeChildrenAsync` manually pre-loads every child table into the change tracker — see Dev Notes — so InMemory can't independently prove FK-driven cascade correctness either). It is a genuinely different, narrower guarantee than "SQL Server will accept this schema," which remains something only a real SQL Server deploy (or `dotnet ef migrations script` review) can confirm. Flag this distinction to Ralf if a stronger SQL-Server-specific regression guard is wanted later — out of scope here.

## Tasks / Subtasks

- [x] Task 1: Add the SQLite provider package (AC: 1)
  - [x] 1.1 Add `<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.9" />` to `api.Tests/api.Tests.csproj` (matching the `10.0.9` version already pinned for `Microsoft.EntityFrameworkCore.InMemory`/`Microsoft.EntityFrameworkCore.SqlServer`/`Microsoft.EntityFrameworkCore.Design` elsewhere in this solution — EF Core providers must match major.minor exactly across a solution). If `10.0.9` isn't the exact patch available on NuGet at implementation time, use the newest `10.0.x` patch published and note the discrepancy in the Dev Agent Record — don't silently drift to a different minor line.

- [x] Task 2: Create a SQLite-targeted `DbContext` subclass with provider-specific model adjustments (AC: 1, 3)
  - [x] 2.1 Create `api/Data/SqliteAppDbContext.cs`: `public class SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : DbContext(options)` exposing the **same `DbSet<T>` properties as `AppDbContext`** (copy the list from `api/Data/AppDbContext.cs:8-19`) so test code can query it identically. Do not make it inherit from `AppDbContext` directly — `AppDbContext`'s constructor is typed to `DbContextOptions<AppDbContext>`, a different generic type; a sibling class sharing the same `OnModelCreating` base logic is simpler than fighting that mismatch.
  - [x] 2.2 In `SqliteAppDbContext.OnModelCreating`, call `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)` first (reuses every existing `IEntityTypeConfiguration<T>` class — cascade behaviors, indexes, `decimal(18,4)`/`decimal(18,6)` column types, filtered unique indexes all apply identically), then apply two SQLite-only corrections documented below.
  - [x] 2.3 **RowVersion downgrade** (required — see Dev Notes "Why `IsRowVersion()` cannot be used as-is on SQLite"): after the base configuration runs, iterate `modelBuilder.Model.GetEntityTypes()` and for each entity type's `RowVersion` property (present on `Flat`, `Tariff`, `MeterReading`, `Room`, `PowerPoint`, `Device`, `ImportJob`, `InsightRun` — confirmed via `grep -rn "IsRowVersion" api/Data/Configurations/`), reconfigure it away from a database-generated concurrency token: set `ValueGenerated = ValueGenerated.Never` and keep `IsConcurrencyToken = true` (a manually-managed token — fine, since none of this story's 3 target scenarios exercise optimistic-concurrency conflict detection; they only need the column to exist and accept writes without EF Core's model-validation exception). Use the `IMutableProperty` API directly (`entityType.FindProperty("RowVersion")`), not the fluent `PropertyBuilder`, since you're overriding configuration already applied by the shared `IEntityTypeConfiguration<T>` classes.
  - [x] 2.4 **Decimal truncation converters** (required for AC3): for the specific column(s) your Task 6 test targets (at minimum one `decimal(18,4)` and, if convenient, `Tariff.PricePerKwh`'s `decimal(18,6)`), add a `HasConversion` value converter that rounds to the matching scale on write (e.g. `v => Math.Round(v, 4)` both directions) scoped to `SqliteAppDbContext` only — do **not** touch the shared `IEntityTypeConfiguration<T>` classes (that would change production SQL Server behavior). Apply this via the same post-`ApplyConfigurationsFromAssembly` loop, or via a second `modelBuilder.Entity<T>().Property(...).HasConversion(...)` call after the shared apply — either is fine, but it must only affect `SqliteAppDbContext`'s model, never `AppDbContext`'s.
  - [x] 2.5 Create `api/Data/SqliteAppDbContextFactory.cs` implementing `IDesignTimeDbContextFactory<SqliteAppDbContext>` (mirror `api/Data/AppDbContextFactory.cs`'s shape, but simpler — a hardcoded `"Data Source=:memory:"` connection string is sufficient since this factory is only ever invoked by `dotnet ef migrations add`, never `database update`, against a real target). This is required for `dotnet ef` tooling to construct `SqliteAppDbContext` at design time (it isn't registered in `Program.cs`'s DI, and the Function App must never register a second production `DbContext`).

- [x] Task 3: Generate a parallel SQLite migration history (AC: 1)
  - [x] 3.1 From `api/`, run `dotnet ef migrations add InitialSqliteSchema --context SqliteAppDbContext --output-dir Data/Migrations/Sqlite`. This creates a **separate** migrations history rooted at `Data/Migrations/Sqlite/`, independent of the existing SQL-Server-authored `Data/Migrations/` history — per EF Core's official "Migrations with Multiple Providers" pattern (one context type per provider, each with its own migrations folder; see References). Do **not** attempt to reuse or "convert" the existing SQL Server migrations — `type: "rowversion", rowVersion: true` calls baked into them (e.g. `api/Data/Migrations/20260719122743_AddOptimisticConcurrencyRowVersions.cs`) are SQL-Server-specific and are exactly what Task 2.3's downgrade avoids needing to touch.
  - [x] 3.2 Inspect the generated migration to confirm it includes every table/index/FK from the current model (all 12 entity tables, all filtered/composite unique indexes including `IX_PowerPoints_FlatId_PlugId_NotNull`). If EF Core's SQLite migrations generator errors or produces something clearly wrong for a specific construct (e.g. a filtered index's `HasFilter("[PlugId] IS NOT NULL")` SQL-Server-bracket syntax may need translating to SQLite's `"PlugId" IS NOT NULL` quoting — trust the generated SQL over hand-guessing), fix by adjusting `SqliteAppDbContext`'s model config in Task 2, not by hand-editing the generated migration file (this project's standing rule: "Migrations are generated — never hand-edit" applies equally to this new history).
  - [x] 3.3 Going forward, whenever a future story changes the shared entity model (new column/index/table), that story must also run `dotnet ef migrations add <Name> --context SqliteAppDbContext --output-dir Data/Migrations/Sqlite` to keep both histories in sync — note this in this story's Dev Notes for future stories to discover (there is no automated check for this; it is a manual discipline this story introduces).

- [x] Task 4: Build the integration test collection's shared setup (AC: 1)
  - [x] 4.1 Create `api.Tests/Integration/` directory.
  - [x] 4.2 Create a shared test base or fixture (e.g. `api.Tests/Integration/SqliteIntegrationTestBase.cs`) that: opens a `Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:")` and calls `.Open()` **before** constructing the `DbContextOptionsBuilder<SqliteAppDbContext>().UseSqlite(connection)` — required because SQLite's `:memory:` database is deleted the moment its one-and-only connection closes, and EF Core would otherwise open/close a connection per query (see Dev Notes reference); calls `context.Database.Migrate()` once to apply Task 3's real migration history; disposes the connection when the test/fixture completes. Follow xUnit conventions already used in this codebase (constructor + `IDisposable`, matching the style of other test classes' setup, not a new pattern).

- [x] Task 5: Cascade-delete integration test (AC: 2a)
  - [x] 5.1 New test file `api.Tests/Integration/FlatCascadeDeleteTests.cs`. Seed one `Flat` plus at least one row in each of the ten Flat-scoped tables (`MeterReadings`, `Tariffs`, `Rooms`→`PowerPoints`→`Devices`, `ImportJobs`, `SmartPlugDailyData`, `SmartPlugIntervalData`, `InsightRuns`, `Insights`) directly via `SqliteAppDbContext` (no need to invoke `DeleteFlatFunction.RunAsync` itself — this test exercises the DB's own FK cascade behavior, which is provider-level, not Function-level; `DeleteFlatFunctionTests.cs`'s existing InMemory tests already cover the Function's HTTP/auth/concurrency behavior).
  - [x] 5.2 Remember `Insight.Device`/`Insight.Run` use `ClientSetNull` (EF-managed, `NO ACTION` at the DB level — see `InsightConfiguration.cs:26-45`), not DB-cascade — this means EF Core nulls those FKs in the change tracker only if the `Insight` rows are already loaded/tracked when the parent is deleted. Since this test constructs and saves everything through the **same** `SqliteAppDbContext` instance (no separate load step), all rows are already tracked, so this should behave correctly — but if you instead re-fetch into a fresh context before deleting, you must replicate `AppDbContextExtensions.LoadFlatCascadeChildrenAsync`'s pre-load pattern first, exactly as `DeleteFlatFunction.cs:61` already does, or the `ClientSetNull` edges won't fire and a live FK constraint violation will occur on `SaveChangesAsync`.
  - [x] 5.3 `db.Flats.Remove(flat); await db.SaveChangesAsync();` — assert (via a fresh `SqliteAppDbContext` sharing the same open connection) that every seeded child row across all ten tables is gone, and that `SaveChangesAsync` did not throw.
  - [x] 5.4 Name the test to reflect what it actually proves (per the "Gap found during story creation" correction) — e.g. `FlatDelete_CascadesAcrossAllTenDependentTables_NoOrphansOrFkViolations`, not a name implying it guards against SQL Server's multi-cascade-path DDL error.

- [x] Task 6: `PlugId` unique-index integration test (AC: 2b)
  - [x] 6.1 New test file `api.Tests/Integration/PowerPointPlugIdUniqueIndexTests.cs`. Seed a `Flat` → `Room` → two `PowerPoint`s in the same flat sharing the same non-null `PlugId` string (remember `PowerPoint.FlatId` is a plain denormalized scalar column, added by Story 11.3 with no FK/navigation — set it manually to match the seeded `Flat.FlatId`, same as `Room.FlatId`). Assert `SaveChangesAsync()` throws (catch the specific exception type EF Core's SQLite provider raises for a unique-constraint violation — likely `DbUpdateException` wrapping a `Microsoft.Data.Sqlite.SqliteException`; confirm empirically rather than assuming the exact wrapped-exception shape).
  - [x] 6.2 Second test: two `PowerPoint`s in the same flat both with `PlugId = null` — assert `SaveChangesAsync()` succeeds (proves the filtered index's `WHERE PlugId IS NOT NULL` exclusion is correctly translated to SQLite).
  - [x] 6.3 Optional third test: same `PlugId` across two *different* flats succeeds (mirrors the already-covered `UpdateFlatStructureFunctionTests.RunAsync_SamePlugIdAcrossDifferentFlats_Succeeds` InMemory test, now proven at the real constraint level too) — nice-to-have, not required by AC2b's literal wording.

- [x] Task 7: Decimal-precision truncation test (AC: 2c, 3)
  - [x] 7.1 New test file `api.Tests/Integration/DecimalPrecisionTruncationTests.cs`. Using a column with Task 2.4's rounding converter applied (e.g. `MeterReading.KwhValue`, `decimal(18,4)`), save a value with more decimal places than the column scale allows (e.g. `123.456789m` against a `decimal(18,4)` column) and assert the value read back from a **fresh** `SqliteAppDbContext` (forcing a real round-trip, not just reading the in-memory tracked value) is truncated/rounded to 4 decimal places (`123.4568m` if rounding, or `123.4567m` if truncating — pick one behavior and document which; match whatever direction your Task 2.4 converter implements, and note in the test's own comment/name that this converter exists **only** to make the test meaningful — SQLite itself does not enforce this natively, per Dev Notes/AC3).
  - [x] 7.2 Confirm this test would **fail** (i.e. return the untruncated value) if Task 2.4's converter were removed — a quick manual sanity check during development, not a permanent test — proving the test is actually exercising the converter and not silently passing regardless.

- [x] Task 8: Full regression pass (all ACs)
  - [x] 8.1 Run `dotnet test` from `api.Tests/` (this repo has no root-level `.sln`, per Story 11.3's precedent) and confirm all existing tests still pass alongside the new `Integration/` tests.
  - [x] 8.2 Confirm `dotnet ef database update` still applies cleanly against a real local SQL Server dev DB for the **existing** (`Data/Migrations/`, non-Sqlite) history — Task 2/3's additions must not have altered `AppDbContext`'s own model or its existing migrations in any way.

### Review Findings

**Patch (applied 2026-08-01):**
- [x] [Review][Patch] Relocated the SQLite-only `DbContext`/factory/migrations out of the deployed Function App project — moved `SqliteAppDbContext.cs`, `SqliteAppDbContextFactory.cs`, and `Data/Migrations/Sqlite/` from `api/` to `api.Tests/Data/`; removed `Microsoft.EntityFrameworkCore.Sqlite`/`SQLitePCLRaw.bundle_e_sqlite3` from `api/energy-tracker-api.csproj` (they were bundling unused native SQLite binaries into the production deployment artifact). Originated as a decision-needed finding — Ralf chose relocation over leaving it in place. [api.Tests/Data/SqliteAppDbContext.cs]
- [x] [Review][Patch] `FlatCascadeDeleteTests` now deletes through a fresh context that calls the actual production `AppDbContextExtensions.LoadFlatCascadeChildrenAsync` helper (the one `DeleteFlatFunction` depends on), instead of relying on entities already tracked from creation — a regression in that helper (e.g. a forgotten table on a future Flat-scoped entity) would previously not have been caught by this test. Required generalizing `AppDbContextExtensions`'s two loader methods from `this AppDbContext` to `this DbContext` (using `Set<T>()` instead of the named `DbSet` properties) so `SqliteAppDbContext` can share the same production logic. [api/Shared/AppDbContextExtensions.cs, api.Tests/Integration/FlatCascadeDeleteTests.cs]
- [x] [Review][Patch] `FlatCascadeDeleteTests` now seeds an untouched second `Flat` + child row and asserts both survive the cascade, proving it doesn't over-delete unrelated data. [api.Tests/Integration/FlatCascadeDeleteTests.cs]
- [x] [Review][Patch] `PowerPointPlugIdUniqueIndexTests`'s two "succeeds" cases now re-query via a fresh context to confirm the rows were actually persisted, not just that `SaveChangesAsync` didn't throw. [api.Tests/Integration/PowerPointPlugIdUniqueIndexTests.cs]
- [x] [Review][Patch] Fixed a stale `last_updated` field that still said "ready-for-dev" while this story's `development_status` was already `review`. [_bmad-output/implementation-artifacts/sprint-status.yaml:38]

**Defer (pre-existing / out of current AC scope, logged to `deferred-work.md`):**
- [x] [Review][Defer] Decimal-rounding converter covers only 2 of ~9 `decimal(18,4)`/`decimal(18,6)` columns — deferred, pre-existing (AC3 is scoped to tested columns, satisfied; latent trap for future test authors on other columns)
- [x] [Review][Defer] The one decimal-precision test value rounds identically under banker's/away-from-zero rounding — deferred, pre-existing (doesn't prove rounding-mode equivalence at a true midpoint)
- [x] [Review][Defer] Rounding converter clamps scale only, not total precision — deferred, pre-existing (integer-digit overflow unguarded)
- [x] [Review][Defer] No try/catch around `connection.Open()`/`Migrate()` in `SqliteIntegrationTestBase`'s constructor — deferred, pre-existing (leaks the connection object on migration failure; low impact for `:memory:`)
- [x] [Review][Defer] `PlugId` uniqueness tested only under SQLite's default case-sensitive collation, not SQL Server's typically case-insensitive default — deferred, pre-existing (out of AC2b's literal scope)
- [x] [Review][Defer] Filtered-index migration carries raw `[Status] IN (0, 1)`-style bracket-quoted filter strings inherited from shared entity configuration — deferred, pre-existing (works on SQLite only incidentally, not introduced by this diff)

## Dev Notes

### Why this story exists now

This codebase's own testing rules (`_bmad-output/project-context.md`, "Backend > EF Core in tests") already name SQLite/real-SQL-Server as the intended future direction beyond `InMemory`, but this was never previously scoped as a story until the Epic 10 retrospective's audit. Two concrete gaps motivate it: (1) Story 10.1's SQL Server deploy failure (a schema-DDL defect InMemory could never catch — see the corrected scope above), and (2) Story 11.3's `PlugId` unique index, whose Dev Notes explicitly deferred real constraint-level proof to this story (`_bmad-output/implementation-artifacts/11-3-enforce-unique-plugid-across-power-points.md`, Task 4.6: *"Do not attempt to write a test asserting the SQL-level unique index actually rejects a duplicate insert... That capability is explicitly Story 11.12's scope."*).

### Why `IsRowVersion()` cannot be used as-is on SQLite

Confirmed via Microsoft Learn (`https://learn.microsoft.com/ef/core/providers/sqlite/limitations`, "Modeling limitations"): SQLite does not support **database-generated concurrency tokens** — the exact mechanism `IsRowVersion()` configures (`ValueGeneratedOnAddOrUpdate()` + `IsConcurrencyToken()`). Every entity's `RowVersion` property in this codebase is configured via `.IsRowVersion()` inside its `IEntityTypeConfiguration<T>` class (8 entities — confirmed via `grep -rn "IsRowVersion" api/Data/Configurations/`: `Flat`, `Tariff`, `MeterReading`, `Room`, `PowerPoint`, `Device`, `ImportJob`, `InsightRun`), and the existing SQL-Server migrations hardcode `type: "rowversion", rowVersion: true` for each (e.g. `api/Data/Migrations/20260719122743_AddOptimisticConcurrencyRowVersions.cs:13-19`) — a SQL Server-native type with no SQLite equivalent. Attempting to build/use `SqliteAppDbContext`'s model with these configurations applied unmodified will fail (either at model-validation time or at migration-generation time, depending on exactly which EF Core version-specific error surfaces — confirm empirically rather than assuming the precise message). Task 2.3's `IMutableProperty`-level downgrade (keep the column, keep it as a *manually-set* concurrency token, drop the "must be DB-generated" requirement) is the standard workaround; none of this story's three target scenarios (cascade delete, unique index, decimal truncation) exercise optimistic-concurrency conflict detection, so losing that specific semantic for the SQLite-only test model has no bearing on what's being verified.

### SQLite provider references consulted during story creation

- `https://learn.microsoft.com/ef/core/managing-schemas/migrations/providers` — "Migrations with Multiple Providers": the officially documented pattern this story follows (one `DbContext` subclass per provider, each with its own `--output-dir`).
- `https://learn.microsoft.com/ef/core/providers/sqlite/limitations` — confirms both gaps this story must work around: no database-generated concurrency tokens, and `decimal`/`DateTimeOffset`/`TimeSpan`/`ulong` are supported for storage/equality but not native precision/ordering semantics; recommends a value converter to preserve `decimal` usage (exactly Task 2.4's approach) rather than switching the model type.
- `https://learn.microsoft.com/ef/core/testing/testing-without-the-database` — "SQLite in-memory": documents the "open the connection yourself before handing it to EF Core, close it only when the test completes" requirement Task 4.2 implements; a `:memory:` SQLite database is deleted the instant its connection closes, and EF Core would otherwise cycle connections per-query.
- `https://learn.microsoft.com/dotnet/api/microsoft.data.sqlite.sqliteconnectionstringbuilder.foreignkeys` and the EF Core 3.0 breaking-changes notes — confirm FK enforcement is **on by default** via the `SQLitePCLRaw.bundle_e_sqlite3` native bundle EF Core's SQLite provider depends on by default; no explicit `PRAGMA foreign_keys=1` or `Foreign Keys=True` connection-string flag is needed for this story's cascade test to see real FK enforcement.

### The existing InMemory cascade-delete test already has a documented limitation this story addresses

`api/Shared/AppDbContextExtensions.cs:14-17`'s own comment: *"Loads every Flat-scoped child row into the change tracker before the Flat is removed, so EF Core's configured `OnDelete(Cascade)` fires deterministically under the InMemory test provider (which, unlike real SQL Server, only cascades to rows already tracked in the current DbContext)."* This story's Task 5 test is the first in this codebase to prove cascade-delete correctness against a provider that enforces FK cascade **without** needing that manual pre-load workaround — though Task 5.2 notes the `ClientSetNull` edges (`Insight.Device`/`Insight.Run`) still depend on tracked state regardless of provider, since `ClientSetNull` is an EF-managed (not DB-enforced) behavior by design (that's the whole point of why it was chosen over `SetNull` — see `InsightConfiguration.cs:26-45`'s own comments and the Story 10.1 investigation).

### `PowerPoint.FlatId` — denormalized scalar, no FK (Story 11.3 context)

`PowerPoint.cs` has a `FlatId` property with **no** `Flat` navigation and **no** FK/cascade configuration in `PowerPointConfiguration.cs` — deliberately, to avoid creating a second SQL-Server cascade path (`Flat`→`PowerPoint` directly, alongside the existing `Flat`→`Room`→`PowerPoint` path) that would reproduce the Story 10.1 defect class. When seeding test data for Task 6, you must set `PowerPoint.FlatId` manually to match the `Room.FlatId` it belongs to — there is no relationship EF Core will populate this from automatically.

### Testing Rules (from project context)

- xUnit + Shouldly (`.ShouldBe(...)`), matching every existing test in this codebase.
- Do not modify or extend `DeleteFlatFunctionTests.cs`, `UpdateFlatStructureFunctionTests.cs`, or any other existing `InMemory`-based test file as part of this story — this story adds a new, separate `Integration/` tier; it does not replace or duplicate existing Function-level test coverage (HTTP/auth/concurrency-conflict behavior stays InMemory-tested, exactly as today).
- Do not attempt to make `SqliteAppDbContext` the "real" test context going forward for existing feature tests — this story's scope is the three named schema-constraint scenarios only, not a migration of the whole test suite off `InMemory`.

### Previous Story Intelligence (Story 11.11)

Story 11.11 was frontend-only (`localDate.ts` hardening) with zero shared surface area with this backend-infrastructure story. `deferred-work.md` was checked for a `blocks: Story 11.12` tag per this project's standing process — none found.

### Project Structure Notes

- New files: `api/Data/SqliteAppDbContext.cs`, `api/Data/SqliteAppDbContextFactory.cs`, `api/Data/Migrations/Sqlite/*` (generated), `api.Tests/Integration/SqliteIntegrationTestBase.cs`, `api.Tests/Integration/FlatCascadeDeleteTests.cs`, `api.Tests/Integration/PowerPointPlugIdUniqueIndexTests.cs`, `api.Tests/Integration/DecimalPrecisionTruncationTests.cs`.
- Modified files: `api.Tests/api.Tests.csproj` (new PackageReference).
- No changes to `AppDbContext.cs`, any existing `IEntityTypeConfiguration<T>` class, or the existing `Data/Migrations/` (SQL Server) history — this story is purely additive.
- `api/Data/SqliteAppDbContext.cs` and `SqliteAppDbContextFactory.cs` live in the production `api` project (not `api.Tests`) despite being test-only in practice, because `dotnet ef migrations add` design-time tooling needs `Microsoft.EntityFrameworkCore.Design` (already referenced by `api/`, not by `api.Tests/`) — this mirrors the existing precedent of `api/Data/AppDbContextFactory.cs`, itself a design-time-only class never invoked by the running Function App.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md#Story 11.12] — original epic AC text; corrected above per "Gap found during story creation."
- [Source: _bmad-output/implementation-artifacts/investigations/story-10-1-deploy-failure-investigation.md] — root-cause detail for the SQL Server multi-cascade-path defect this story's AC originally cited; establishes why SQLite cannot reproduce it.
- [Source: _bmad-output/implementation-artifacts/11-3-enforce-unique-plugid-across-power-points.md] — the story whose Task 4.6 explicitly deferred real constraint-level proof to this story; source of the `PowerPoint.FlatId` denormalization context.
- [Source: api/Data/AppDbContext.cs, api/Data/AppDbContextFactory.cs] — base context and design-time-factory precedent to mirror.
- [Source: api/Data/Configurations/*.cs] — all `IEntityTypeConfiguration<T>` classes reused unmodified via `ApplyConfigurationsFromAssembly`; `RowVersion` locations confirmed via grep (8 entities), `decimal` column locations confirmed via grep (7 columns across `Flat`, `Device`, `MeterReading`, `Tariff`, `SmartPlugIntervalData`, `SmartPlugDailyData`).
- [Source: api/Data/Migrations/20260719122743_AddOptimisticConcurrencyRowVersions.cs, api/Data/Migrations/20260727074108_AddFlatIdAndUniqueIndexToPowerPoints.cs] — existing SQL-Server-specific migration shapes that must NOT be reused/converted for the new Sqlite history.
- [Source: api/Shared/AppDbContextExtensions.cs, api/Features/Flats/DeleteFlatFunction.cs] — existing cascade-delete production code and its documented InMemory-specific workaround.
- [Source: api.Tests/Features/Flats/DeleteFlatFunctionTests.cs, api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs] — existing InMemory test coverage this story's tier is additive to, not a replacement for.
- [Microsoft Learn: Migrations with Multiple Providers](https://learn.microsoft.com/ef/core/managing-schemas/migrations/providers) — the one-DbContext-subclass-per-provider pattern this story implements.
- [Microsoft Learn: SQLite EF Core Database Provider Limitations](https://learn.microsoft.com/ef/core/providers/sqlite/limitations) — database-generated concurrency tokens and decimal-precision gaps.
- [Microsoft Learn: Testing without your production database system — SQLite in-memory](https://learn.microsoft.com/ef/core/testing/testing-without-the-database) — the open-connection-before-passing-to-EF-Core requirement for `:memory:`.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet ef migrations add InitialSqliteSchema --context SqliteAppDbContext --output-dir Data/Migrations/Sqlite` succeeded on first attempt; generated migration includes all 12 entity tables and all indexes (including the filtered `IX_PowerPoints_FlatId_PlugId_NotNull` using SQL-Server-bracket `[PlugId] IS NOT NULL` filter syntax, which SQLite accepts natively via its bracket-quoting compatibility mode — no translation needed).
- `dotnet test --filter FullyQualifiedName~PowerPointPlugIdUniqueIndexTests` confirmed empirically that a `PlugId` unique-constraint violation on SQLite surfaces as `DbUpdateException` (wrapping a `Microsoft.Data.Sqlite.SqliteException`), matching the task's expected-but-unconfirmed exception shape.
- Manual sanity check per Task 7.2: temporarily replaced the `MeterReading.KwhValue` rounding converter with a no-op converter, reran `DecimalPrecisionTruncationTests` — test failed as expected (`123.456789m` read back unrounded), then reverted and reconfirmed the real converter passes.
- `dotnet restore` initially reported NU1903 (high-severity CVE-2025-6965 in transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, pulled in by `Microsoft.EntityFrameworkCore.Sqlite` 10.0.9). Fixed by adding a direct `SQLitePCLRaw.bundle_e_sqlite3` 2.1.12 package reference (first version outside the advisory's `<= 2.1.11` vulnerable range) to both `api.Tests.csproj` and `api/energy-tracker-api.csproj` — not part of the story's explicit task list, but a minimal fix for a vulnerability introduced directly by this story's new dependency.
- Task 8.2 (confirm `dotnet ef database update` applies cleanly against a real SQL Server dev DB) could not be executed in this environment: `local.settings.json`'s `SqlConnectionString` points to a live Azure SQL Database (`energytracker-sqlsrv.database.windows.net`), and per this project's standing convention dev agents do not connect to or modify live Azure infrastructure. Verified instead via `git diff`/`git status` that `api/Data/AppDbContext.cs`, every `api/Data/Configurations/*.cs` file, and the existing (non-Sqlite) `api/Data/Migrations/` history are byte-for-byte unchanged by this story — confirming there is no new SQL Server migration to apply and the existing history is untouched. Flagging this gap to Ralf; a live `dotnet ef database update` run remains outstanding if he wants it confirmed directly.

### Completion Notes List

- Added `Microsoft.EntityFrameworkCore.Sqlite` 10.0.9 to both `api.Tests` (for test execution) and `api` (SqliteAppDbContext/Factory live in the production project per the story's design-time-tooling rationale, mirroring `AppDbContextFactory.cs`'s existing precedent).
- `SqliteAppDbContext` reuses every existing `IEntityTypeConfiguration<T>` via `ApplyConfigurationsFromAssembly`, then applies two SQLite-only corrections: (1) downgrades all 8 `RowVersion` properties from database-generated to manually-managed concurrency tokens (`ValueGenerated.Never`, `IsConcurrencyToken = true`), and (2) adds rounding value converters on `MeterReading.KwhValue` (round to 4 decimals) and `Tariff.PricePerKwh` (round to 6 decimals) to simulate SQL Server's column-scale enforcement, which SQLite lacks natively.
- Generated a new, independent SQLite migration history at `api/Data/Migrations/Sqlite/` via `dotnet ef migrations add InitialSqliteSchema --context SqliteAppDbContext`; the existing SQL-Server `Data/Migrations/` history and `AppDbContext` model were not touched.
- Three integration test files added under `api.Tests/Integration/`, all built on a shared `SqliteIntegrationTestBase` that opens one real SQLite `:memory:` connection per test, runs `Database.Migrate()` (not `EnsureCreated()`), and disposes the connection at test teardown:
  - `FlatCascadeDeleteTests`: seeds one row in each of the ten Flat-scoped tables and confirms a `Flat` delete cascades through all of them with no FK violation.
  - `PowerPointPlugIdUniqueIndexTests`: three tests confirming the filtered unique index rejects same-flat duplicate `PlugId`s, allows two `null` `PlugId`s in the same flat, and allows the same `PlugId` across two different flats.
  - `DecimalPrecisionTruncationTests`: confirms `MeterReading.KwhValue` written with 6 decimal places round-trips as rounded to 4, exercising the Task 2.4 converter (verified this test fails without the converter — see Debug Log).
- Full regression suite: 485/485 tests pass (up from the pre-story count, all new tests included), zero failures, zero build warnings.
- Fixed a NU1903 high-severity transitive vulnerability (CVE-2025-6965, SQLitePCLRaw pre-2.1.12) introduced by adding the SQLite provider — pinned `SQLitePCLRaw.bundle_e_sqlite3` to 2.1.12 in both csproj files.

### File List

**New files:**
- `api/Data/SqliteAppDbContext.cs`
- `api/Data/SqliteAppDbContextFactory.cs`
- `api/Data/Migrations/Sqlite/20260731163232_InitialSqliteSchema.cs`
- `api/Data/Migrations/Sqlite/20260731163232_InitialSqliteSchema.Designer.cs`
- `api/Data/Migrations/Sqlite/SqliteAppDbContextModelSnapshot.cs`
- `api.Tests/Integration/SqliteIntegrationTestBase.cs`
- `api.Tests/Integration/FlatCascadeDeleteTests.cs`
- `api.Tests/Integration/PowerPointPlugIdUniqueIndexTests.cs`
- `api.Tests/Integration/DecimalPrecisionTruncationTests.cs`

**Modified files:**
- `api.Tests/api.Tests.csproj` (added `Microsoft.EntityFrameworkCore.Sqlite` and `SQLitePCLRaw.bundle_e_sqlite3` package references)
- `api/energy-tracker-api.csproj` (added `Microsoft.EntityFrameworkCore.Sqlite` and `SQLitePCLRaw.bundle_e_sqlite3` package references)

## Change Log

| Date | Change |
|---|---|
| 2026-07-31 | Implemented SQLite integration test tier: `SqliteAppDbContext`/`SqliteAppDbContextFactory`, parallel `Data/Migrations/Sqlite/` history, and three integration test files (cascade-delete, PlugId unique index, decimal-precision truncation). All ACs satisfied; full regression suite passes (485/485). |
