---
baseline_commit: 4d85c79fa1deda3965539005d0ba75aa9b491b07
---

# Story 5.1: Multi-Flat Backend — Create, List & Cascade Delete

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to create additional flats, list all my flats, and permanently delete a flat along with all its data,
so that I can manage multiple dwellings independently with complete data isolation between them.

## Acceptance Criteria

1. **Given** `GET /api/v1/flats`, **when** called by an authenticated user, **then** `GetFlatsFunction` returns all Flats belonging to the resolved `UserId` as `FlatSummary` records (`FlatId`, `Name`, `AnnualKwhBaseline` decimal, `SpikeThreshold` decimal, `PlannedAnnualSpend` nullable decimal); HTTP 200; ≤ 2s; Flats belonging to other users are never returned.

2. **Given** `POST /api/v1/flats` with `{ name, annualKwhBaseline, plannedAnnualSpend }`, **when** `CreateFlatFunction.RunAsync` executes, **then** a new `Flat` record is created scoped to the resolved `UserId`; `SpikeThreshold` defaults to `2.0`; HTTP 201 with `Location: /api/v1/flats/{flatId}`; no Tariff entries are created — the caller must add one via the Tariff endpoint. This endpoint does **not** check whether the user has already completed onboarding (unlike `CompleteOnboardingFunction`, which 409s if any Flat already exists) — it is explicitly the "add another flat after onboarding" path.

3. **Given** `DELETE /api/v1/flats/{flatId}`, **when** `DeleteFlatFunction.RunAsync` executes, **then** it verifies `flatId` belongs to the resolved `UserId` (HTTP 403 otherwise, no deletion performed); the Flat and all of its currently-schema-present associated data are permanently deleted — as of this story that means `MeterReadings` and `Tariffs` (the only Flat-scoped child tables that exist today; `SmartPlugDailyData`, `SmartPlugIntervalData`, `ImportJobs`, `Rooms`/`PowerPoints`/`Devices`, `InsightRuns`, `Insights` don't exist in the schema yet — they arrive in Stories 5.3/6.x/8.x and must each add their own `OnDelete(DeleteBehavior.Cascade)` FK to `Flats` at that time, following this story's pattern); HTTP 204; no orphaned records remain for the deleted `flatId`.
**And** cascade delete is enforced at the database level via `OnDelete(DeleteBehavior.Cascade)` in Fluent API on all FK relationships from `Flats` (already true today for `MeterReadingConfiguration`/`TariffConfiguration` — no Fluent API change needed for those two, verify only) — not application-side loops.

4. **Given** `DeleteFlatFunctionTests` (HTTP-level, `api.Tests/Features/Flats/`), **this is a hard requirement for this story to reach `done` — not optional polish.** It must assert: (1) **cascade completeness** — zero rows remain in `MeterReadings` and `Tariffs` (the tables that exist today; see AC3) for the deleted `flatId`; (2) **wrong-owner rejection** — a `DELETE` for a `flatId` not owned by the resolved `UserId` returns HTTP 403 and performs no deletion; (3) **no-orphaned-records / sibling isolation** — deleting one Flat leaves all data belonging to any other Flat (same or different owner) untouched. [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-03-epic5-story5-1-ac.md — approved 2026-07-03, already applied verbatim to the epic file]

5. **Given** `GET /api/v1/user/settings` and `PUT /api/v1/user/settings`, **when** called, **then** the response includes `activeFlatId` (nullable guid); `PUT` accepts an optional `activeFlatId` field and persists it to `Users.ActiveFlatId` (new nullable column added via EF Core migration). `PUT` must not clobber an already-stored `ActiveFlatId` when the field is simply omitted from the body (the existing locale-only frontend call must keep working without silently clearing it) — an explicit JSON `null` clears it, an omitted key leaves it unchanged, and a value present must belong to the resolved `UserId` (403 otherwise, nothing persisted).

## Tasks / Subtasks

### Backend — data layer

- [x] Task 1: Add `Users.ActiveFlatId` column (AC: 5)
  - [x] `api/Data/Entities/User.cs` — add `public Guid? ActiveFlatId { get; set; }`. **Do not add a navigation property or a Fluent API `HasOne`/FK relationship to `Flat`.** Rationale (read this before implementing): `Flats.UserId` already has `OnDelete(DeleteBehavior.Cascade)` pointing *to* `Users`. Adding a second FK the other way (`Users.ActiveFlatId` → `Flats.FlatId`) creates a two-table cycle (`Users → Flats → Users`) that SQL Server rejects at `database update` time with "may cause cycles or multiple cascade paths" (error 1785), regardless of which `OnDelete` behavior the second FK uses. Treat `ActiveFlatId` as an unenforced soft reference instead — there is already a project precedent for this: `PowerPoints.PlugId` (Story 5.3, per `architecture.md`'s data model table) is a plain nullable column with no FK to any smart-plug entity. A dangling `ActiveFlatId` after its Flat is deleted is expected and handled by the frontend (Story 5.2 explicitly re-selects another Flat or redirects to onboarding on 204 from `DeleteFlatFunction` — not this story's concern).
  - [x] `api/Data/Configurations/UserConfiguration.cs` — add `builder.Property(u => u.ActiveFlatId).IsRequired(false);` (plain column, no `HasOne`).
  - [x] Run `dotnet ef migrations list` from `api/` first (project convention) — confirm current head is `20260703114416_ConsolidateTariffContractStartDate`.
  - [x] Run `dotnet ef migrations add AddActiveFlatIdToUsers` from `api/` after the entity/config change above. Expect a single `AddColumn<Guid>(name: "ActiveFlatId", table: "Users", nullable: true)` — no index, no FK constraint. If EF scaffolds anything FK-related, that means a `HasOne` was accidentally left in the configuration — remove it and regenerate.

### Backend — models & validation

- [x] Task 2: Add `FlatSummary` and `CreateFlatRequest` to `api/Features/Flats/FlatModels.cs` (AC: 1, 2)
  - [x] `public record FlatSummary(Guid FlatId, string Name, decimal AnnualKwhBaseline, decimal SpikeThreshold, decimal? PlannedAnnualSpend);` — field order matches AC1 exactly.
  - [x] `public record CreateFlatRequest(string? Name, decimal AnnualKwhBaseline, decimal? PlannedAnnualSpend);` — `Name` nullable at the record level (same pattern as `PatchFlatRequest.Name`) so a missing/null JSON value produces a validator error, not a silent default; `AnnualKwhBaseline` non-nullable decimal (same pattern as `CreateTariffRequest.PricePerKwh`/`MonthlyBaseFee`) — a missing JSON value deserializes to `0m`, which the validator's `GreaterThan(0)` rule rejects, giving the same "required" behavior without a separate null-check.
  - [x] Leave the existing `PatchFlatRequest`/`FlatResponse` records untouched — this story doesn't modify `PatchFlatFunction`.

- [x] Task 3: Add `api/Features/Flats/CreateFlatValidator.cs` (AC: 2)
  - [x] Mirror the exact bounds already established for the same fields elsewhere (`OnboardingValidator`, `PatchFlatValidator`) — do not invent new limits:
    ```csharp
    public class CreateFlatValidator : AbstractValidator<CreateFlatRequest>
    {
        public CreateFlatValidator()
        {
            RuleFor(r => r.Name).NotEmpty().WithMessage("name is required.").MaximumLength(200);
            RuleFor(r => r.AnnualKwhBaseline).GreaterThan(0).LessThan(20000)
                .WithMessage("annualKwhBaseline must be less than 20000.");
            RuleFor(r => r.PlannedAnnualSpend).GreaterThan(0m).LessThan(50000m)
                .WithMessage("plannedAnnualSpend must be greater than 0 and less than 50000.")
                .When(r => r.PlannedAnnualSpend is not null);
        }
    }
    ```

### Backend — Functions

- [x] Task 4: `api/Features/Flats/GetFlatsFunction.cs` (AC: 1)
  - [x] `[Function("GetFlats")]`, `HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flats")` — no `{flatId}` route segment, this lists across the whole user.
  - [x] `var userId = context.GetUserId();` then project directly in the LINQ query: `db.Flats.AsNoTracking().Where(f => f.UserId == userId).Select(f => new FlatSummary(f.FlatId, f.Name, f.AnnualKwhBaseline, f.SpikeThreshold, f.PlannedAnnualSpend)).ToListAsync(ct)`. Unlike `GetTariffsFunction` (which must materialize via `ToListAsync()` before mapping because `TariffLockPolicy.IsLocked` isn't EF-translatable), `FlatSummary`'s fields are all plain columns — EF Core can translate the `.Select()` projection directly to SQL, so project first, no separate mapping step needed.
  - [x] Return `new OkObjectResult(flats)` (a bare array response, per project convention for small collections).

- [x] Task 5: `api/Features/Flats/CreateFlatFunction.cs` (AC: 2)
  - [x] Constructor `(AppDbContext db, CreateFlatValidator validator)`. `[Function("CreateFlat")]`, `HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flats")`.
  - [x] Parse body via `JsonSerializer.DeserializeAsync<CreateFlatRequest>(req.Body, _jsonOptions, ct)` with the standard `private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };` field (matches `CreateTariffFunction`'s pattern exactly) — catch `JsonException` → 400; null result → 400.
  - [x] Run `validator.ValidateAsync(request, ct)` → 400 with joined errors on failure (matches `CreateTariffFunction`).
  - [x] Construct the entity: `new Flat { UserId = userId, Name = request.Name!.Trim(), AnnualKwhBaseline = request.AnnualKwhBaseline, SpikeThreshold = 2.0m, PlannedAnnualSpend = request.PlannedAnnualSpend }`. **Critical, easy to get wrong:** you must set `SpikeThreshold = 2.0m` explicitly in code — do not omit it and rely on `FlatConfiguration`'s `HasDefaultValue(2.0m).HasSentinel(-1m)`. That sentinel only fires the DB default when EF sees the sentinel value `-1m`; an unset C# `decimal` defaults to `0m`, which EF will happily insert as a real, explicit `0` — silently breaking spike detection for every newly created flat. `CompleteOnboardingFunction.cs:49` already demonstrates the correct pattern (`SpikeThreshold = 2.0m,`) — follow it exactly.
  - [x] **No check against `db.Flats.AnyAsync(f => f.UserId == userId)`** — that 409 "onboarding already completed" guard belongs only to `CompleteOnboardingFunction`; this endpoint must succeed for a user who already has one or more Flats (that's its entire purpose).
  - [x] `db.Flats.Add(flat); await db.SaveChangesAsync(ct);` then return `new CreatedResult($"/api/v1/flats/{flat.FlatId}", new FlatSummary(flat.FlatId, flat.Name, flat.AnnualKwhBaseline, flat.SpikeThreshold, flat.PlannedAnnualSpend))` — response body uses the same `FlatSummary` shape as the list endpoint, for frontend consistency.

- [x] Task 6: `api/Features/Flats/DeleteFlatFunction.cs` (AC: 3, 4)
  - [x] Constructor `(AppDbContext db)`. `[Function("DeleteFlat")]`, `HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/flats/{flatId}")`.
  - [x] `Guid.TryParse(flatId, ...)` → 400 on failure (matches every other `{flatId}`-route Function).
  - [x] Ownership check: `var flat = await db.Flats.SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct); if (flat is null) return 403;` — use this combined-filter pattern (the one established in `CreateTariffFunction`/`GetTariffsFunction`/`PatchTariffFunction`), **not** `PatchFlatFunction`'s older two-step "load then compare `UserId`" pattern. There is no separate reusable `TenantResolver.VerifyOwnership(...)` helper in this codebase (`TenantResolverMiddleware` only resolves `UserId` from the auth header) — the epic AC's phrase "`TenantResolver` verifies..." refers to this inline per-Function pattern, not a class to create.
  - [x] **The core of this task — why cascade delete needs a pre-load step, read before implementing:** before calling `db.Flats.Remove(flat)`, explicitly *load* (not remove) each currently-existing child collection into the same `AppDbContext` instance: `await db.MeterReadings.Where(r => r.FlatId == flatGuid).LoadAsync(ct); await db.Tariffs.Where(t => t.FlatId == flatGuid).LoadAsync(ct);`. This is necessary because EF Core's `InMemory` provider (used by every test in this project, per `api.Tests` convention) does **not** simulate real SQL Server `ON DELETE CASCADE` for rows that aren't already tracked in the current context — a fresh-context `Remove(flat)` + `SaveChangesAsync()` with unloaded children leaves them behind under `InMemory` (verified directly: a throwaway spike test reproduced this exact failure before this story was written). Loading the rows (via `.LoadAsync()`, never `.RemoveRange()`) lets EF's own change-tracker cascade — driven by `MeterReadingConfiguration`'s and `TariffConfiguration`'s existing `OnDelete(DeleteBehavior.Cascade)` — mark them for deletion automatically when the parent is removed. This is **not** the "manually pre-delete child entities" anti-pattern the project's `EF Core patterns` rule forbids (no `RemoveRange`/explicit delete call is made); it only makes already-configured cascade behavior deterministic under `InMemory` as well as real SQL Server. Every future story that adds a new Flat-scoped child table (5.3's `Rooms`, 6.x's `ImportJobs`/`SmartPlugDailyData`/`SmartPlugIntervalData`, 8.x's `InsightRuns`/`Insights`) must add its own `.LoadAsync()` line here alongside its own cascade FK — note this in that story's Dev Notes when it's created.
  - [x] `db.Flats.Remove(flat); await db.SaveChangesAsync(ct);` then `return new NoContentResult();` (this codebase's first 204 response — no existing precedent to copy, but it's the standard ASP.NET Core result type).

- [x] Task 7: Update the Settings slice for `activeFlatId` (AC: 5)
  - [x] `api/Features/Settings/SettingsModels.cs` — add `ActiveFlatId` as the **last** positional parameter of `UserSettingsResponse`: `public record UserSettingsResponse(string? Locale, bool HasFlat, Guid? FlatId, string? FlatName, decimal? AnnualKwhBaseline, decimal? PlannedAnnualSpend, Guid? ActiveFlatId);`. **Remove** `public record UpdateUserSettingsRequest(string Locale);` entirely — it becomes dead code once Task below switches body parsing away from `JsonSerializer.DeserializeAsync<UpdateUserSettingsRequest>`.
  - [x] `api/Features/Settings/GetUserSettingsFunction.cs` — purely additive: append `user.ActiveFlatId` as the final constructor argument when building `UserSettingsResponse`. No other change.
  - [x] `api/Features/Settings/UpdateUserSettingsFunction.cs` — rewrite body parsing from `JsonSerializer.DeserializeAsync<UpdateUserSettingsRequest>` to the `JsonNode`-based pattern, matching **`PatchTariffFunction`'s** style specifically (`JsonNode.Parse(body, new JsonNodeOptions { PropertyNameCaseInsensitive = true })` + explicit `obj.ContainsKey(...)`-driven "provided" checks) — not `PatchFlatFunction`'s older pattern, which omits the case-insensitivity option. This distinction matters because this function now needs true tri-state handling for `activeFlatId` (provided-with-value / provided-as-null / omitted), the same shape `PatchTariffFunction` already solves for its optional fields.
    - `locale` stays required exactly as today: read `obj["locale"]?.GetValue<string>()`, 400 if null or not in `AllowedLocales` — same validation, same error shape, just read via `JsonNode` instead of a typed record.
    - `activeFlatId`: `var activeFlatIdProvided = obj.ContainsKey("activeFlatId");` — if provided and the value is a non-null JSON string, `Guid.TryParse` it (400 on parse failure); if provided and JSON `null`, treat as "clear" (`activeFlatId = null` but still apply the write); if **not** provided, do not touch `user.ActiveFlatId` at all. **This omitted-means-unchanged behavior is the whole point of this rewrite** — the existing frontend call (`updateUserSettings(locale)`, `client/src/features/settings/api/settingsApi.ts`) never sends `activeFlatId` today, and a naive "always assign `body.ActiveFlatId`" implementation would silently null out a previously-set `ActiveFlatId` on every plain locale change. Write a regression test for this exact scenario (Task 12).
    - Ownership check: if `activeFlatIdProvided` and the parsed value is non-null, verify `await db.Flats.AnyAsync(f => f.FlatId == activeFlatId && f.UserId == userId, ct)` before touching the DB — 403 if it doesn't belong to the resolved user, and persist nothing at all (locale included) in that case, matching this codebase's atomic-on-rejection convention (`PatchTariffFunction`'s post-review fix: validate everything before any mutation or `SaveChangesAsync`).
    - Only assign `user.ActiveFlatId = activeFlatId` inside the `if (activeFlatIdProvided)` branches — both the new-user and existing-user code paths, and both the happy path and the `DbUpdateException` retry path (mirrors how `LocaleOverride` is currently set in all three places in this function).
    - `UserSettingsResponse` construction at the end — add `user.ActiveFlatId` as the final argument (both return points: happy path and the pre-existing `DbUpdateException` retry path).

### Backend — DI registration

- [x] Task 8: Register the new validator in `api/Program.cs` (AC: 2)
  - [x] Add `builder.Services.AddSingleton<CreateFlatValidator>();` next to the existing `builder.Services.AddSingleton<PatchFlatValidator>();` line. `GetFlatsFunction` and `DeleteFlatFunction` need no new DI registrations (only `AppDbContext`, already registered).

### Backend — tests

- [x] Task 9: `api.Tests/Features/Flats/GetFlatsFunctionTests.cs` (AC: 1)
  - [x] `RunAsync_UserWithNoFlats_ReturnsEmptyList`
  - [x] `RunAsync_UserWithMultipleFlats_ReturnsAllOfThem` — seed 2+ Flats for the same user, assert count and field values (including `SpikeThreshold` and `PlannedAnnualSpend`) match `FlatSummary`'s shape.
  - [x] `RunAsync_FlatsBelongingToOtherUsers_AreNeverReturned` — seed Flats for two different users, assert only the requesting user's Flats come back.

- [x] Task 10: `api.Tests/Features/Flats/CreateFlatFunctionTests.cs` (AC: 2)
  - [x] `RunAsync_ValidRequest_Returns201WithLocationAndSpikeThresholdDefaultsTo2` — assert the persisted entity's `SpikeThreshold` is exactly `2.0m` (this is the regression test for the sentinel/default-value gotcha in Task 5 — a bug here would silently persist `0m` instead).
  - [x] `RunAsync_ValidRequest_PersistsFlatScopedToResolvedUserId`
  - [x] `RunAsync_MissingOrEmptyName_Returns400`
  - [x] `RunAsync_AnnualKwhBaselineOutOfBounds_Returns400` — `[Theory]` with `0`, `-1`, `20000`, `20001` (mirrors `CreateTariffFunctionTests`' bounds-theory pattern).
  - [x] `RunAsync_PlannedAnnualSpendOmitted_CreatesFlatWithNullPlannedAnnualSpend`
  - [x] `RunAsync_PlannedAnnualSpendOutOfBounds_Returns400` — `[Theory]` with `0`, `-1`, `50000`, `50001`.
  - [x] `RunAsync_MalformedJsonBody_Returns400`
  - [x] `RunAsync_UserAlreadyHasAFlat_StillSucceeds` — explicitly proves this endpoint is **not** gated the way `CompleteOnboardingFunction` is; seed an existing Flat for the user first, then call `CreateFlatFunction` again and assert 201 (not 409).

- [x] Task 11: `api.Tests/Features/Flats/DeleteFlatFunctionTests.cs` — **the hard-required test file per AC4; this story cannot reach `done` without it.**
  - [x] `RunAsync_InvalidFlatIdFormat_Returns400`
  - [x] `RunAsync_FlatDoesNotExist_Returns403`
  - [x] `RunAsync_FlatNotOwnedByUser_Returns403AndPerformsNoDeletion` (AC4-2, wrong-owner rejection) — seed a Flat (with at least one `MeterReading` and one `Tariff`) owned by `"owner-user"`, call as `"attacker-user"`, assert 403 **and** assert the Flat and its child rows all still exist afterward.
  - [x] `RunAsync_ValidDelete_Returns204AndCascadeDeletesAllMeterReadingsAndTariffs` (AC4-1, cascade completeness) — seed a Flat with multiple `MeterReadings` and multiple `Tariffs`, call delete, assert 204, then assert `db.MeterReadings.CountAsync(r => r.FlatId == flatId)` and `db.Tariffs.CountAsync(t => t.FlatId == flatId)` are both `0`. Remember Task 6's `.LoadAsync()` requirement — without it this test fails under `InMemory` even though the production Fluent API cascade config is correct.
  - [x] `RunAsync_ValidDelete_RemovesFlatItself` — assert `db.Flats.CountAsync(f => f.FlatId == flatId)` is `0` after.
  - [x] `RunAsync_DeletingOneFlat_LeavesSiblingFlatDataUntouched` (AC4-3, no-orphaned-records / sibling isolation) — seed **two** Flats belonging to **different** users, each with its own `MeterReadings` and `Tariffs`; delete the first; assert the second Flat and all of its child rows are completely unaffected (both row counts and field values).

- [x] Task 12: Update `api.Tests/Features/Settings/GetUserSettingsFunctionTests.cs` and `UpdateUserSettingsFunctionTests.cs` (AC: 5)
  - [x] `GetUserSettingsFunctionTests`: add `RunAsync_UserWithActiveFlatIdSet_ReturnsIt` and `RunAsync_UserWithNoActiveFlatIdSet_ReturnsNull`.
  - [x] `UpdateUserSettingsFunctionTests`: add
    - `RunAsync_ActiveFlatIdOmitted_LeavesExistingStoredValueUnchanged` — **the critical regression test.** Seed a user with `ActiveFlatId` already set to some Flat they own, PUT with only `{"locale":"de-DE"}` (no `activeFlatId` key at all), assert the response's `ActiveFlatId` and the persisted `user.ActiveFlatId` both still equal the original value.
    - `RunAsync_ActiveFlatIdProvidedAndOwnedByUser_PersistsIt`
    - `RunAsync_ActiveFlatIdProvidedButNotOwnedByUser_Returns403AndPersistsNothing` — assert neither `ActiveFlatId` nor `LocaleOverride` changed (atomicity — a request with a valid `locale` but an unowned `activeFlatId` must not partially apply the locale change).
    - `RunAsync_ActiveFlatIdExplicitJsonNull_ClearsExistingValue`
    - `RunAsync_ActiveFlatIdMalformedGuidString_Returns400`
  - [x] All four existing tests in both files must keep passing unmodified (they don't reference `activeFlatId` at all) — if any needs a change, that's a signal something in Task 7 broke backward compatibility.

### Cross-cutting

- [x] Task 13: Self-review pass before marking ready for review
  - [x] Grep `api/Features/Flats/` and `api/Features/Settings/` for any remaining unscoped `db.Flats`/`db.Users` query (i.e., missing a `UserId`/`FlatId` filter) — every query touching Flat or User data must be tenant-scoped.
  - [x] Verify `CreateFlatFunction` never checks `db.Flats.AnyAsync(f => f.UserId == userId)` (that check belongs only to `CompleteOnboardingFunction`).
  - [x] Verify `DeleteFlatFunction`'s two `.LoadAsync()` calls happen **before** `db.Flats.Remove(flat)`, not after.
  - [x] Verify `UpdateUserSettingsFunction` only ever assigns `user.ActiveFlatId` inside an `if (activeFlatIdProvided)` guard — grep for any unconditional assignment.
  - [x] `dotnet build` and `dotnet test api.Tests` green. No frontend changes in this story (it's backend-only, per the epic's own story title) — no `npx tsc -b` / `npx vitest run` / `npm run lint` needed.

### Review Findings

- [x] [Review][Patch] Settings responses return an arbitrary flat, not the one matching `ActiveFlatId` (decision: patch now) — fixed: both functions now resolve `flat` via `user.ActiveFlatId` when set, falling back to the first owned flat when null or dangling. [api/Features/Settings/GetUserSettingsFunction.cs, api/Features/Settings/UpdateUserSettingsFunction.cs]
- [x] [Review][Patch] AC4 sibling-isolation test doesn't cover the same-owner, multiple-flats case — fixed: added `RunAsync_DeletingOneFlat_LeavesSameOwnerSiblingFlatDataUntouched`. [api.Tests/Features/Flats/DeleteFlatFunctionTests.cs]
- [x] [Review][Patch] Triplicated `if (activeFlatIdProvided) user.ActiveFlatId = activeFlatId;` assignment repeated across the new-user, existing-user, and `DbUpdateException`-retry paths — fixed: extracted into a single local `ApplySettings(User target)` function called from both write sites. [api/Features/Settings/UpdateUserSettingsFunction.cs]
- [x] [Review][Patch] Test name `RunAsync_MissingOrEmptyName_Returns400` only exercises the empty-string case, not a truly-absent `name` key — fixed: added `RunAsync_NameKeyAbsentFromBody_Returns400`. [api.Tests/Features/Flats/CreateFlatFunctionTests.cs]
- [x] [Review][Defer] No cap on flats-per-user and unpaginated `GetFlatsFunction` [api/Features/Flats/GetFlatsFunction.cs] — deferred, pre-existing (no endpoint in this codebase paginates)
- [x] [Review][Defer] Inconsistent Problem Details `type` field presence across Functions touched by this diff (`UpdateUserSettingsFunction` includes it, `CreateFlatFunction`/`DeleteFlatFunction` omit it) [api/Features/Settings/UpdateUserSettingsFunction.cs, api/Features/Flats/CreateFlatFunction.cs, api/Features/Flats/DeleteFlatFunction.cs] — deferred, pre-existing project-wide inconsistency (also present in `PatchFlatFunction`, `GetTariffsFunction`, etc.)
- [x] [Review][Defer] Range-validator messages only describe the upper bound (e.g. "must be less than 20000") even when the lower bound fails [api/Features/Flats/CreateFlatValidator.cs:11] — deferred, pre-existing pattern verbatim-copied from `OnboardingValidator`/`PatchFlatValidator` per this story's own spec instruction
- [x] [Review][Defer] Unbounded `StreamReader.ReadToEndAsync` with no request-size guard before JSON parsing [api/Features/Settings/UpdateUserSettingsFunction.cs:23-24] — deferred, pre-existing pattern identical to `PatchTariffFunction`, which the spec explicitly instructed this rewrite to mirror
- [x] [Review][Defer] `DbUpdateException` retry path has no handling for a second consecutive failure (would propagate as unhandled 500) [api/Features/Settings/UpdateUserSettingsFunction.cs:121-129, api/Features/Settings/GetUserSettingsFunction.cs:30-34] — deferred, pre-existing structure predating this diff
- [x] [Review][Defer] No decimal-precision validation (values with more than 4 decimal places are silently truncated by `decimal(18,4)` columns) [api/Features/Flats/CreateFlatValidator.cs] — deferred, pre-existing gap in every validator in the codebase (`OnboardingValidator`, `PatchFlatValidator`, `CreateTariffValidator`), not specific to this story

## Dev Notes

### Why this story is scoped backend-only

The epic explicitly titles this story "Multi-Flat **Backend**" — the flat switcher, "Add flat" UI, and type-to-confirm delete UI are Story 5.2's job, which consumes the four endpoints this story builds. Do not add any `client/` changes here; `useUserSettings`/`settingsApi.ts`/`UserSettings` (TS type) are untouched by this story, even though the backend response they wrap now carries an extra field — TanStack Query and the plain TS type in `client/src/features/settings/api/settingsApi.ts` have no runtime schema validation that would reject an unrecognized field, so this is safe to leave for Story 5.2 to pick up. [Verified via direct read: no zod schema exists for the settings response; every current frontend consumer (`OnboardingGate`, `OnboardingPage`, `SettingsRoot`, `FlatSettingsCard`, `TariffSettingsRoute`, `DashboardPage`) destructures only the fields it already knows about.]

### Epic 5 was blocked on Epic 4's Action Item #1 — now resolved

The Epic 4 retrospective (2026-07-03) explicitly said Epic 5 should not start until the `EffectiveDate`/`ContractStartDate` tariff data-model issue was resolved, since Epic 5 builds on `TariffResolver`. That work shipped as **Story 4.4** (`ConsolidateTariffContractStartDate` migration, already the current EF Core migration head) — Epic 4 is fully `done` in `sprint-status.yaml`. No further action needed here; this note exists only so you don't second-guess whether it's safe to start this story.

### The `Users.ActiveFlatId` FK-cascade-cycle trap

This is the single easiest architectural mistake to make in this story — see Task 1 for the full rationale. Short version: **do not** create a Fluent API `HasOne(u => u.ActiveFlat).WithMany().HasForeignKey(u => u.ActiveFlatId)` relationship. `Flats.UserId` already cascades to `Users`; a second FK the other direction creates a cascade cycle SQL Server refuses to create as a constraint. `ActiveFlatId` is a plain, unenforced nullable `Guid` column — matching the existing `PowerPoints.PlugId` soft-reference precedent in this codebase's data model.

### The `SpikeThreshold` sentinel-default trap

`FlatConfiguration.cs` configures `SpikeThreshold` with `HasDefaultValue(2.0m).HasSentinel(-1m)` — this DB-level default only activates when EF sees the *sentinel* value (`-1m`), not when the C# property is simply left unset (which defaults to `0m`, a real value EF will insert as-is). Every existing Flat-creation code path (`CompleteOnboardingFunction`) already works around this by setting `SpikeThreshold = 2.0m` explicitly — `CreateFlatFunction` (Task 5) must do the same, or every newly created flat via this endpoint will silently get `SpikeThreshold = 0`, breaking spike detection with no visible error anywhere.

### `InMemory` EF Core and cascade deletes — verified behavior, not assumption

This project's testing rules (`project-context.md`) say: "Do not write InMemory tests that rely on SQL-specific behaviour (cascade deletes...)." Taken at face value, this seems to conflict with AC4's hard requirement that `DeleteFlatFunctionTests` assert cascade completeness. The resolution (verified directly with a throwaway spike test before this story was written, not assumed): EF Core's `InMemory` provider **does** honor `OnDelete(DeleteBehavior.Cascade)` — but only for entities that are already tracked/loaded in the *same* `DbContext` instance performing the `Remove`+`SaveChangesAsync`. A fresh-context load-then-remove (simulating a real Function invocation that never touches `MeterReadings`/`Tariffs`) leaves child rows behind under `InMemory`, even though the exact same code against real SQL Server would correctly cascade via the server-side FK constraint regardless of what EF has loaded. Task 6's `.LoadAsync()` calls close this gap by making the child rows tracked before the parent is removed, so EF's own (already-configured) cascade logic handles them — this satisfies AC4's InMemory-testable requirement without violating the "don't manually pre-delete children" rule (no `RemoveRange`/explicit delete call exists anywhere in `DeleteFlatFunction`) and without needing a new SQLite/real-SQL integration harness (a known, pre-existing, documented gap in this project — do not attempt to introduce one for this story).

### Which ownership-check pattern to follow

Two exist in this codebase: `PatchFlatFunction`'s older two-step pattern (`FirstOrDefaultAsync(f => f.FlatId == flatGuid)` then compare `flat.UserId != userId`) and the newer, more recent combined-filter pattern used throughout `Tariffs/` (`SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId)`). Use the Tariffs-slice pattern for every new Function in this story (`GetFlatsFunction` doesn't need one — it has no `{flatId}` param; `CreateFlatFunction` doesn't need one either — it creates under the resolved `userId` directly; `DeleteFlatFunction` and the ownership check inside `UpdateUserSettingsFunction` both need it).

### Project Structure Notes

- New files: `api/Features/Flats/CreateFlatValidator.cs`, `api/Features/Flats/GetFlatsFunction.cs`, `api/Features/Flats/CreateFlatFunction.cs`, `api/Features/Flats/DeleteFlatFunction.cs`, plus their three test files under `api.Tests/Features/Flats/`, plus one new migration pair (`api/Data/Migrations/{timestamp}_AddActiveFlatIdToUsers.cs` + `.Designer.cs`) and a regenerated `AppDbContextModelSnapshot.cs`.
- Modified files: `api/Data/Entities/User.cs`, `api/Data/Configurations/UserConfiguration.cs`, `api/Features/Flats/FlatModels.cs`, `api/Features/Settings/SettingsModels.cs`, `api/Features/Settings/GetUserSettingsFunction.cs`, `api/Features/Settings/UpdateUserSettingsFunction.cs`, `api/Program.cs`, `api.Tests/Features/Settings/GetUserSettingsFunctionTests.cs`, `api.Tests/Features/Settings/UpdateUserSettingsFunctionTests.cs`.
- No changes to `client/` (see "Why this story is scoped backend-only" above) and no changes to `PatchFlatFunction.cs`/`PatchFlatValidator.cs` (untouched by this story).
- Follows existing VSA layout exactly: `api/Features/Flats/` gains three new Functions plus a validator, same folder as the existing `PatchFlatFunction`/`PatchFlatValidator`/`FlatModels.cs`. No new subfolders.

### Testing standards (backend — this story has no frontend surface)

- Test placement: `api.Tests/Features/Flats/{FunctionName}Tests.cs`, extending the existing `api.Tests/Features/Settings/*.cs` files. No new test project or folder needed.
- `InMemory` EF Core provider throughout, per project convention — see the dedicated Dev Notes section above on why cascade-delete tests are still valid under `InMemory` when children are explicitly loaded first.
- Do not test `UserConfiguration.cs`/`FlatConfiguration.cs` EF Core config classes directly — trust EF Core, per project testing rules.
- Highest-value target in this story is `DeleteFlatFunction` (AC4 hard requirement) — give it the most scenario coverage of the three new Functions.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-5-multi-flat-management-flat-structure.md#Story 5.1] — authoritative AC text (already includes the AC4 amendment from the sprint-change-proposal below).
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-03-epic5-story5-1-ac.md] — rationale for AC4's hard test requirement (mirrors the Epic 3→4 "soft ask slipped, re-committed as hard requirement" precedent).
- [Source: _bmad-output/implementation-artifacts/epic-4-retro-2026-07-03.md#Action Items #1, #4] — Epic 5 start-blocker (resolved via Story 4.4) and the origin of AC4's hard requirement.
- [Source: _bmad-output/planning-artifacts/architecture.md#Entity model, #AD-8, #AD-12, #AD-13] — `Flats`/`Tariffs`/`MeterReadings` data model, hard-delete/cascade philosophy, tenant-isolation-via-middleware pattern, route list (note: the route list at AD-13 predates this story and doesn't yet show `GET/POST /api/v1/flats` or `DELETE /api/v1/flats/{flatId}` — a pre-existing doc gap, not something this story is required to fix).
- [Source: api/Data/Entities/Flat.cs, api/Data/Configurations/FlatConfiguration.cs] — existing `SpikeThreshold` sentinel/default trap; existing entity shape (no changes needed to either file this story).
- [Source: api/Data/Entities/User.cs, api/Data/Configurations/UserConfiguration.cs] — files to modify for `ActiveFlatId` (Task 1).
- [Source: api/Data/Configurations/MeterReadingConfiguration.cs, api/Data/Configurations/TariffConfiguration.cs] — both already configure `OnDelete(DeleteBehavior.Cascade)` toward `Flats`; verify only, no change needed.
- [Source: api/Features/Onboarding/CompleteOnboardingFunction.cs] — reference pattern for Flat creation, including the `SpikeThreshold = 2.0m` explicit-set convention and the `AnyAsync`-based 409 onboarding-completion guard that `CreateFlatFunction` must deliberately *not* replicate.
- [Source: api/Features/Tariffs/CreateTariffFunction.cs, GetTariffsFunction.cs, PatchTariffFunction.cs] — reference patterns for JSON body parsing (`JsonSerializer.DeserializeAsync` for Create, `JsonNode` + `JsonNodeOptions { PropertyNameCaseInsensitive = true }` for tri-state Patch/Put fields), combined-filter ownership checks, and atomic-validate-before-mutate ordering.
- [Source: api/Features/Settings/GetUserSettingsFunction.cs, UpdateUserSettingsFunction.cs, SettingsModels.cs] — files to modify for `activeFlatId` (Task 7).
- [Source: api.Tests/Features/Flats/PatchFlatFunctionTests.cs, api.Tests/Features/Tariffs/CreateTariffFunctionTests.cs] — test scaffolding patterns (`MakeDb`, `MakeFunctionContext`, `MakeRequest`) to replicate in the three new test files.
- [Source: api.Tests/Features/Settings/GetUserSettingsFunctionTests.cs, UpdateUserSettingsFunctionTests.cs] — existing test files to extend, not replace.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md#Deferred from: code review of 2-1-i18n-infrastructure-and-locale-settings-api, #Deferred from: code review of 1-5-app-shell...] — pre-existing, out-of-scope-for-this-story items about `queryKey: ['settings']` not being tenant-scoped on the frontend; explicitly flagged there as "revisit when multi-flat... lands" — that revisit is Story 5.2's job (frontend), not this story's.
- [Source: _bmad-output/project-context.md#EF Core, #Critical Don't-Miss Rules, #Testing Rules] — tenant-scoping, `SingleOrDefaultAsync` vs `FirstOrDefaultAsync`, "trust EF cascade deletes" rule, `InMemory` provider limitations.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet ef migrations list` confirmed head `20260703114416_ConsolidateTariffContractStartDate` before adding the new migration.
- `dotnet ef migrations add AddActiveFlatIdToUsers` produced a single `AddColumn<Guid>(name: "ActiveFlatId", table: "Users", nullable: true)` — no index, no FK, matching the plain-soft-reference design in Task 1's rationale.
- `dotnet ef database update` applied the migration cleanly against the configured Azure SQL database — confirms no FK cascade-cycle error (1785) at the real-SQL level, not just InMemory.
- Full backend test suite: 186 passed, 0 failed (baseline 161 + 25 new tests across `GetFlatsFunctionTests`, `CreateFlatFunctionTests`, `DeleteFlatFunctionTests`, and additions to the two Settings test files).

### Completion Notes List

- Implemented `GET/POST/DELETE /api/v1/flats` (`GetFlatsFunction`, `CreateFlatFunction`, `DeleteFlatFunction`) plus `CreateFlatValidator`, following the Tariffs-slice combined-filter ownership-check pattern and the `CompleteOnboardingFunction` explicit-`SpikeThreshold = 2.0m` convention.
- `DeleteFlatFunction` pre-loads `MeterReadings`/`Tariffs` via `.LoadAsync()` before `db.Flats.Remove(flat)` so EF's already-configured cascade (`OnDelete(DeleteBehavior.Cascade)`) fires deterministically under the `InMemory` test provider, per the Dev Notes rationale; no `RemoveRange`/manual child deletion was added.
- Added `Users.ActiveFlatId` as a plain, unenforced nullable `Guid` column (no Fluent API `HasOne`) via migration `20260703170401_AddActiveFlatIdToUsers`, avoiding the `Users → Flats → Users` FK cascade cycle — verified against real Azure SQL, not just InMemory.
- Rewrote `UpdateUserSettingsFunction` from typed `JsonSerializer.DeserializeAsync<UpdateUserSettingsRequest>` to `JsonNode`-based tri-state parsing (matching `PatchTariffFunction`'s style): `activeFlatId` omitted leaves the stored value untouched, explicit JSON `null` clears it, a provided GUID is ownership-checked (403, no mutation, if not owned) before any `SaveChangesAsync`. Removed the now-dead `UpdateUserSettingsRequest` record.
- `GetUserSettingsFunction` and `UserSettingsResponse` extended additively with `ActiveFlatId` as the final field; no other behavior changed.
- Registered `CreateFlatValidator` as `AddSingleton` in `Program.cs` alongside `PatchFlatValidator`.
- Self-review pass (Task 13) confirmed: no unscoped `db.Flats`/`db.Users` query introduced; `CreateFlatFunction` has no onboarding-style `AnyAsync` 409 guard; `DeleteFlatFunction`'s two `.LoadAsync()` calls precede `Remove(flat)`; every `user.ActiveFlatId` assignment in `UpdateUserSettingsFunction` is inside an `if (activeFlatIdProvided)` guard (new-user path, existing-user path, and the `DbUpdateException` retry path).
- No `client/` changes — story is explicitly backend-only per its title and Dev Notes; no frontend type/schema updates needed since neither TanStack Query nor the plain TS `UserSettings` type validates against unknown response fields.

### File List

**New:**
- `api/Features/Flats/CreateFlatValidator.cs`
- `api/Features/Flats/GetFlatsFunction.cs`
- `api/Features/Flats/CreateFlatFunction.cs`
- `api/Features/Flats/DeleteFlatFunction.cs`
- `api/Data/Migrations/20260703170401_AddActiveFlatIdToUsers.cs`
- `api/Data/Migrations/20260703170401_AddActiveFlatIdToUsers.Designer.cs`
- `api.Tests/Features/Flats/GetFlatsFunctionTests.cs`
- `api.Tests/Features/Flats/CreateFlatFunctionTests.cs`
- `api.Tests/Features/Flats/DeleteFlatFunctionTests.cs`

**Modified:**
- `api/Data/Entities/User.cs`
- `api/Data/Configurations/UserConfiguration.cs`
- `api/Data/Migrations/AppDbContextModelSnapshot.cs`
- `api/Features/Flats/FlatModels.cs`
- `api/Features/Settings/SettingsModels.cs`
- `api/Features/Settings/GetUserSettingsFunction.cs`
- `api/Features/Settings/UpdateUserSettingsFunction.cs`
- `api/Program.cs`
- `api.Tests/Features/Settings/GetUserSettingsFunctionTests.cs`
- `api.Tests/Features/Settings/UpdateUserSettingsFunctionTests.cs`
