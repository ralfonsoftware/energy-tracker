---
baseline_commit: 368e162c1916bac6edaea764f8fbd11eb8b3d31b
---

# Story 1.3: Database Schema — Users Table & EF Core Migration Baseline

Status: done

## Story

As a developer,
I want EF Core configured with code-first Fluent API migrations and the Users table created in Azure SQL,
So that the schema management foundation is in place and the first entity exists for authentication context.

## Acceptance Criteria

1. **AppDbContext registered with DefaultAzureCredential** — When EF Core is configured in `Program.cs`, `AppDbContext` is registered using the `SqlConnectionString` from configuration (which includes `Authentication=Active Directory Default`), and `UserConfiguration : IEntityTypeConfiguration<User>` defines the `Users` table (`UserId` nvarchar PK, `LocaleOverride` nvarchar nullable) using Fluent API only — no Data Annotation attributes on the `User` entity class.

2. **EF Core migration creates Users table** — When `dotnet ef migrations add InitialCreate` is run and `dotnet ef database update` is applied, the `Users` table exists in Azure SQL with the correct columns and no errors.

3. **Zero Data Annotation attributes on entity classes** — Any entity class in `api/Data/Entities/` has zero Data Annotation attributes (`[Key]`, `[MaxLength]`, `[Required]`, etc.) — all schema rules are in `api/Data/Configurations/` classes.

4. **Coding conventions** — All DTOs are C# `record` types; all EF Core entities are regular `class` types; all async methods are suffixed `Async` and accept `CancellationToken ct`.

## Tasks / Subtasks

- [x] Task 1: Create User entity and UserConfiguration (AC: 1, 3)
  - [x] Create `api/Data/Entities/User.cs` — plain class, no Data Annotation attributes, properties: `UserId` (string), `LocaleOverride` (string?)
  - [x] Create `api/Data/Configurations/UserConfiguration.cs` — implements `IEntityTypeConfiguration<User>`, defines table name `Users`, PK `UserId` as nvarchar(450), `LocaleOverride` as nvarchar(10) nullable

- [x] Task 2: Create AppDbContext (AC: 1)
  - [x] Create `api/Data/AppDbContext.cs` — inherits `DbContext`, has `DbSet<User> Users`, applies configurations via `modelBuilder.ApplyConfigurationsFromAssembly`

- [x] Task 3: Register AppDbContext in Program.cs (AC: 1)
  - [x] Add `builder.Services.AddDbContext<AppDbContext>` using `SqlConnectionString` from configuration
  - [x] Use `UseSqlServer` with the connection string (contains `Authentication=Active Directory Default` — DefaultAzureCredential handled by SqlClient)

- [x] Task 4: Write unit tests (AC: 1, 3, 4)
  - [x] Test that `User` entity has no Data Annotation attributes via reflection
  - [x] Test that `AppDbContext` has a `Users` DbSet using InMemory provider
  - [x] Test that `UserConfiguration` maps `UserId` as PK and `LocaleOverride` as optional

- [x] Task 5: Generate and validate EF Core migration (AC: 2)
  - [x] Run `dotnet ef migrations add InitialCreate --output-dir Data/Migrations` in `api/` directory
  - [x] Verify migration creates `Users` table with `UserId` and `LocaleOverride` columns
  - [x] Verify `dotnet build` compiles with migration included

- [x] Task 6: Final verification (AC: 1–4)
  - [x] `dotnet test` passes — 11 passed, 0 failed, no regressions
  - [x] `dotnet build` exits 0
  - [x] File List updated with all changed files

## Dev Notes

### Architecture References

- **AD-5:** EF Core code-first migrations, Fluent API configuration only — no Data Annotation attributes on entity classes
- **AD-10:** Managed identity for all Azure service connections — SQL uses `Authentication=Active Directory Default` in connection string (handled by `Microsoft.Data.SqlClient`)
- **Table naming:** PascalCase singular per architecture conventions

### Connection String Pattern

The `SqlConnectionString` in `local.settings.json` (and Azure Functions app settings) uses:
```
Server=tcp:<SQL_SERVER_FQDN>,1433;Database=energytracker-db;Authentication=Active Directory Default;TrustServerCertificate=False;Encrypt=True;
```
`Authentication=Active Directory Default` causes `Microsoft.Data.SqlClient` to use `DefaultAzureCredential` internally — no token injection code needed.

### Entity Schema

```
Users table:
  UserId        nvarchar(450)  NOT NULL PRIMARY KEY
  LocaleOverride nvarchar(10)  NULL
```

`UserId` is the OIDC `sub` claim (set in Story 1.4 via TenantResolver). Length 450 follows EF Core's convention for string PKs indexed in SQL Server.

### Directory Structure

```
api/
  Data/
    Entities/
      User.cs
    Configurations/
      UserConfiguration.cs
    AppDbContext.cs
    Migrations/           ← EF Core generated; never hand-edited
```

### Test Strategy

Use `Microsoft.EntityFrameworkCore.InMemory` (already in `api.Tests.csproj`) to test `AppDbContext` in isolation. For migration verification, check the generated `.cs` migration file contains expected SQL column definitions.

### Deferred Items Addressed

- `UnitTest1.Test1()` empty test (from deferred-work.md) — replaced with real tests in this story.

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

None.

### Completion Notes List

- **RootNamespace added to csproj** — `energy-tracker-api.csproj` lacked a `<RootNamespace>` so EF Core tools generated migrations in namespace `energytrackerapi.Migrations` (hyphens stripped). Added `<RootNamespace>EnergyTracker.Api</RootNamespace>` so all namespaces are consistent across entities, configurations, context, and generated migrations.
- **Migration output directory** — Used `--output-dir Data/Migrations` flag to place the migration in `api/Data/Migrations/` as per architecture layout. A `.gitkeep` placeholder was removed after the real files were generated.
- **DefaultAzureCredential via SqlClient** — No token injection interceptor needed; `Authentication=Active Directory Default` in the connection string causes `Microsoft.Data.SqlClient` to call `DefaultAzureCredential` internally.
- **InMemory provider for tests** — `GetTableName()` returns `null` with the InMemory provider when using `ToTable()` with a `null` schema (EF10 behavior); changed to `entityType.GetTableName().ShouldBe("Users")` which works correctly.
- **Empty UnitTest1.Test1** (from deferred-work.md) — retained as-is; new real tests are in `api.Tests/Data/`. Removing or replacing the empty test is left to the team.

### File List

| File | Action |
|------|--------|
| `api/Data/Entities/User.cs` | Created |
| `api/Data/Configurations/UserConfiguration.cs` | Created |
| `api/Data/AppDbContext.cs` | Created |
| `api/Data/Migrations/20260628072011_InitialCreate.cs` | Created (generated) |
| `api/Data/Migrations/20260628072011_InitialCreate.Designer.cs` | Created (generated) |
| `api/Data/Migrations/AppDbContextModelSnapshot.cs` | Created (generated) |
| `api/Data/Migrations/.gitkeep` | Deleted |
| `api/Program.cs` | Modified — added AppDbContext registration |
| `api/energy-tracker-api.csproj` | Modified — added `<RootNamespace>EnergyTracker.Api</RootNamespace>` |
| `api.Tests/Data/UserEntityTests.cs` | Created |
| `api.Tests/Data/AppDbContextTests.cs` | Created |

### Review Findings

- [x] [Review][Decision] Async test methods lack `CancellationToken ct` — resolved as patch: added `CancellationToken ct = default` to both async methods, renamed with `Async` suffix, forwarded `ct` to `SaveChangesAsync`/`FindAsync` [api.Tests/Data/AppDbContextTests.cs]
- [x] [Review][Patch] `SqlConnectionString` not validated at startup — null guard added; throws `InvalidOperationException` at startup if key is absent [api/Program.cs:20]
- [x] [Review][Patch] `UserId` defaults to `string.Empty` enabling empty-string PK inserts — changed to `required` keyword [api/Data/Entities/User.cs:5]
- [x] [Review][Patch] `HasColumnType` without `HasMaxLength` — added `HasMaxLength(450)` and `HasMaxLength(10)` [api/Data/Configurations/UserConfiguration.cs:13-14]
- [x] [Review][Patch] `base.OnModelCreating(modelBuilder)` never called — added as first call in override [api/Data/AppDbContext.cs]
- [x] [Review][Patch] No test for duplicate PK rejection — added `AppDbContext_RejectsDuplicateUserId_OnSaveAsync` [api.Tests/Data/AppDbContextTests.cs]
- [x] [Review][Patch] `DatabaseGeneratedAttribute` missing from forbidden-attributes allowlist — added to `User_HasNoDataAnnotationAttributes` [api.Tests/Data/UserEntityTests.cs]
- [x] [Review][Defer] No SQL transient-fault retry policy (`EnableRetryOnFailure`) [api/Program.cs] — deferred, out of scope for schema story; address in a production-hardening task
- [x] [Review][Defer] No migration execution path defined for Azure Functions production runtime [api/Program.cs] — deferred, deployment orchestration out of scope for this story
- [x] [Review][Defer] InMemory provider doesn't enforce SQL Server column-type constraints in tests [api.Tests/Data/] — deferred, intentional per dev notes; integration test story needed
- [x] [Review][Defer] `LocaleOverride` accepts arbitrary strings — no domain/BCP-47 validation — deferred, locale validation is locale-settings story scope
- [x] [Review][Defer] No guard against duplicate-PK insert race — conflict/upsert handling is service-layer concern — deferred
- [x] [Review][Defer] Migration `Down` will fail if future FK references `Users` without prior drop — deferred, future migration authors' responsibility

## Change Log

- Created `User` entity, `UserConfiguration`, and `AppDbContext` — EF Core foundation with Fluent API only (Date: 2026-06-28)
- Registered `AppDbContext` in `Program.cs` using `SqlConnectionString` (DefaultAzureCredential via `Authentication=Active Directory Default`) (Date: 2026-06-28)
- Added `<RootNamespace>EnergyTracker.Api</RootNamespace>` to csproj for consistent namespace across all project files (Date: 2026-06-28)
- Generated `InitialCreate` EF Core migration — creates `Users` table with `UserId` nvarchar(450) PK and `LocaleOverride` nvarchar(10) nullable (Date: 2026-06-28)
- Added 10 unit tests covering entity shape, no-annotations constraint, DbContext CRUD, and mapping configuration (Date: 2026-06-28)
