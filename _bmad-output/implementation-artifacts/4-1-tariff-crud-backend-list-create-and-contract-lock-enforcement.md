---
baseline_commit: 0155a15b3b0375fd362ea06cb80a9485d1e7e20e
---

# Story 4.1: Tariff CRUD Backend — List, Create & Contract Lock Enforcement

Status: done

## Story

As a user,
I want to add new tariff entries, list my tariff history, and have the app prevent me from editing price fields on active contracts,
so that my tariff history is accurate and locked rates cannot be accidentally changed.

## Acceptance Criteria

1. **Given** `GET /api/v1/flats/{flatId}/tariffs`, **when** called, **then** `GetTariffsFunction` returns all `Tariff` entries in descending effective-date order as `TariffResponse` records (`TariffId`, `EffectiveDate` datetimeoffset, `PricePerKwh` decimal, `MonthlyBaseFee` decimal, `ProviderName` nullable string, `ContractStartDate` nullable datetimeoffset, `ContractDurationMonths` nullable int, `IsLocked` bool derived from: `ContractStartDate` is not null AND in the past AND `ContractDurationMonths` is not null); HTTP 200; ≤ 2s response time.

2. **Given** `POST /api/v1/flats/{flatId}/tariffs` with a valid request body, **when** `CreateTariffFunction.RunAsync` executes, **then** a new `Tariff` record is created with all fields stored locale-neutrally (`EffectiveDate` as datetimeoffset, `PricePerKwh` and `MonthlyBaseFee` as `decimal`); HTTP 201 with `Location` header; ≤ 2s response time. A future `EffectiveDate` is accepted without affecting any past cost calculations (FR-12).

3. **Given** `POST /api/v1/flats/{flatId}/tariffs` with an `EffectiveDate` that already has a `Tariff` entry for this Flat, **when** `CreateTariffFunction.RunAsync` executes, **then** HTTP 409 Problem Details (`type: "https://tools.ietf.org/html/rfc9110#section-15.5.10"`, `title: "Conflict"`) is returned; no record is created. This relies on the `IX_Tariffs_FlatId_EffectiveDate` unique index (already added via migration `MakeTariffEffectiveDateUnique`, resolved in the Epic 3 retrospective, ahead of this epic) — the function must check for an existing entry **before** insert so the collision never surfaces as an unhandled DB-constraint 500.

4. **Given** `TariffValidator` (FluentValidation), **when** `PricePerKwh ≤ 0` or `≥ 10`, `MonthlyBaseFee < 0` or `≥ 1000`, or `EffectiveDate` is missing, **then** HTTP 400 Problem Details is returned; no record is created. The upper bounds must match the project-wide numeric-bound convention established in `OnboardingValidator` and `PatchFlatValidator` during the Epic 3 retrospective (2026-07-02): `PricePerKwh` `GreaterThan(0).LessThan(10)`, `MonthlyBaseFee` `GreaterThanOrEqualTo(0).LessThan(1000)`.

5. **Given** `PATCH /api/v1/flats/{flatId}/tariffs/{tariffId}` attempting to update `PricePerKwh` or `MonthlyBaseFee`, **when** the Tariff entry has `ContractStartDate` in the past AND `ContractDurationMonths` is not null, AND the request's optional `LockOverride` boolean is not `true` (missing or `false`), **then** HTTP 422 Problem Details with `type: "tariff-locked"` is returned; the price fields are not modified. Non-price fields (`ProviderName`, `ContractStartDate`, `ContractDurationMonths`) present in the **same** request are updated successfully regardless of lock status — the lock only blocks the two price fields, never the whole request.

6. **Given** the same locked-tariff `PATCH` request but with `LockOverride: true` in the request body, **when** `PatchTariffFunction` processes it, **then** the price fields are updated normally, subject to the same bound rules as `TariffValidator`; `LockOverride` is a request-only flag — it is not persisted as a column and has no effect when the tariff is not locked.

7. **Given** the `Tariffs` EF Core entity and `TariffConfiguration`, **when** reviewed, **then** all column mappings use Fluent API; `PricePerKwh` and `MonthlyBaseFee` are `decimal`; `EffectiveDate` and `ContractStartDate` are `datetimeoffset`; index `IX_Tariffs_FlatId_EffectiveDate` exists (unique); zero Data Annotation attributes on the entity class. **This is already true today — verify only, no entity/migration changes in this story.**

## Tasks / Subtasks

- [x] Task 1: Verify existing Tariff data layer (AC: 7) — no code changes expected
  - [x] Confirm `api/Data/Entities/Tariff.cs` has zero Data Annotations and all Fluent API config lives in `TariffConfiguration.cs`
  - [x] Confirm `IX_Tariffs_FlatId_EffectiveDate` is unique (migration `20260702121947_MakeTariffEffectiveDateUnique`)
  - [x] Confirm `PricePerKwh` is `decimal(18,6)` and `MonthlyBaseFee` is `decimal(18,4)` in `TariffConfiguration.cs`

- [x] Task 2: Create `api/Features/Tariffs/TariffModels.cs` (AC: 1, 2, 5, 6)
  - [x] `CreateTariffRequest(DateTimeOffset? EffectiveDate, decimal PricePerKwh, decimal MonthlyBaseFee, string? ProviderName, DateTimeOffset? ContractStartDate, int? ContractDurationMonths)` — record
  - [x] `PatchTariffRequest(decimal? PricePerKwh, decimal? MonthlyBaseFee, string? ProviderName, DateTimeOffset? ContractStartDate, int? ContractDurationMonths, bool LockOverride)` — record
  - [x] `TariffResponse(Guid TariffId, DateTimeOffset EffectiveDate, decimal PricePerKwh, decimal MonthlyBaseFee, string? ProviderName, DateTimeOffset? ContractStartDate, int? ContractDurationMonths, bool IsLocked)` — record, field order matches AC1 exactly
  - [x] `static class TariffLockPolicy` with `static bool IsLocked(DateTimeOffset? contractStartDate, int? contractDurationMonths) => contractStartDate.HasValue && contractStartDate.Value < DateTimeOffset.UtcNow && contractDurationMonths.HasValue;` — single source of truth for the lock rule, called from both `GetTariffsFunction` and `PatchTariffFunction`

- [x] Task 3: Create `api/Features/Tariffs/TariffValidator.cs` — FluentValidation, `AbstractValidator<CreateTariffRequest>` (AC: 4)
  - [x] `RuleFor(r => r.EffectiveDate).NotNull().WithMessage("effectiveDate is required.")`
  - [x] `RuleFor(r => r.PricePerKwh).GreaterThan(0m).LessThan(10m).WithMessage("pricePerKwh must be less than 10.")`
  - [x] `RuleFor(r => r.MonthlyBaseFee).GreaterThanOrEqualTo(0m).LessThan(1000m).WithMessage("monthlyBaseFee must be less than 1000.")`
  - [x] `RuleFor(r => r.ProviderName).MaximumLength(200).When(r => r.ProviderName != null)`
  - [x] `RuleFor(r => r.ContractDurationMonths).InclusiveBetween(1, 60).When(r => r.ContractDurationMonths.HasValue)`

- [x] Task 4: Create `api/Features/Tariffs/PatchTariffValidator.cs` — FluentValidation, `AbstractValidator<PatchTariffRequest>` (AC: 5, 6)
  - [x] Same bound rules as `TariffValidator` but each wrapped in `.When(r => <field> is not null)` since PATCH fields are optional (mirrors `PatchFlatValidator`'s conditional-rule style)
  - [x] No rule on `LockOverride` — it's a plain flag, not user data to bound-check

- [x] Task 5: Create `api/Features/Tariffs/GetTariffsFunction.cs` (AC: 1)
  - [x] `[Function("GetTariffs")]`, `HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flats/{flatId}/tariffs")`
  - [x] `context.GetUserId()` → validate `flatId` GUID → `SingleOrDefaultAsync` flat scoped to `userId` → 403 if not found/owned
  - [x] `db.Tariffs.AsNoTracking().Where(t => t.FlatId == flatGuid).OrderByDescending(t => t.EffectiveDate).ToListAsync(ct)`, then map to `TariffResponse` **in C# after materialization** (calling `TariffLockPolicy.IsLocked` inside an EF Core `Select()` will throw at runtime — it cannot be translated to SQL)
  - [x] Return `OkObjectResult(responseList)`

- [x] Task 6: Create `api/Features/Tariffs/CreateTariffFunction.cs` (AC: 2, 3, 4)
  - [x] `[Function("CreateTariff")]`, `HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flats/{flatId}/tariffs")`
  - [x] Same auth/flat-ownership/JSON-deserialize/validate skeleton as `SubmitReadingFunction.cs` (see Dev Notes)
  - [x] **Before** creating the entity: `await db.Tariffs.AnyAsync(t => t.FlatId == flatGuid && t.EffectiveDate == request.EffectiveDate!.Value, ct)` → if true, return 409 Conflict Problem Details with the exact `type` URI from AC3
  - [x] Create `Tariff`, `db.Tariffs.Add(...)`, `SaveChangesAsync(ct)`
  - [x] Return `CreatedResult($"/api/v1/flats/{flatId}/tariffs/{tariff.TariffId}", response)` with a `TariffResponse` body (mirrors `SubmitReadingFunction`'s 201 body convention)

- [x] Task 7: Create `api/Features/Tariffs/PatchTariffFunction.cs` (AC: 5, 6)
  - [x] `[Function("PatchTariff")]`, `HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/flats/{flatId}/tariffs/{tariffId}")`
  - [x] Auth + flat ownership (403) → look up tariff `SingleOrDefaultAsync(t => t.TariffId == tariffGuid && t.FlatId == flatGuid, ct)` → 404 if missing
  - [x] Deserialize `PatchTariffRequest`, validate via `PatchTariffValidator`
  - [x] Compute `isLocked = TariffLockPolicy.IsLocked(tariff.ContractStartDate, tariff.ContractDurationMonths)` **before** applying any field changes (this captures the lock state as of before this PATCH — see Dev Notes for why field-application order matters)
  - [x] `priceFieldsRequested = request.PricePerKwh is not null || request.MonthlyBaseFee is not null`
  - [x] `lockBlocksPriceUpdate = priceFieldsRequested && isLocked && !request.LockOverride`
  - [x] Apply non-price fields unconditionally (`ProviderName`, `ContractStartDate`, `ContractDurationMonths` — only if provided, i.e. not null)
  - [x] Apply price fields **only if** `!lockBlocksPriceUpdate`
  - [x] `SaveChangesAsync(ct)` — always save once, so a mixed request's non-price changes persist even when price changes are blocked
  - [x] If `lockBlocksPriceUpdate`: return 422 with `{ type = "tariff-locked", title = "Unprocessable Entity", status = 422, detail = "..." }`
  - [x] Else: return 200 `OkObjectResult(TariffResponse)` recomputing `IsLocked` from the (possibly just-updated) entity state

- [x] Task 8: Register DI in `api/Program.cs` (AC: 2, 4, 5, 6)
  - [x] `builder.Services.AddSingleton<TariffValidator>();`
  - [x] `builder.Services.AddSingleton<PatchTariffValidator>();`
  - [x] Add `using EnergyTracker.Api.Features.Tariffs;`
  - [x] `TariffResolver` is already registered (`AddScoped`) — do not touch it, this story does not use it

- [x] Task 9: Backend tests in `api.Tests/Features/Tariffs/` (AC: 1–6)
  - [x] `GetTariffsFunctionTests.cs`: empty list (zero tariffs) returns 200 with `[]`; descending order across 3+ tariffs; `IsLocked` true/false cases (past+duration vs. future vs. past-no-duration vs. no-start-date); 403 for wrong-owner flat; 400 for malformed `flatId`
  - [x] `CreateTariffFunctionTests.cs`: 201 + `Location` header + persisted decimal values; 409 on duplicate `EffectiveDate` (seed one tariff, POST same date again); 400 for each bound violation (price ≤0, price ≥10, fee <0, fee ≥1000, missing effectiveDate); future-dated tariff accepted (AC2); 403 wrong owner; 400 malformed `flatId`
  - [x] `PatchTariffFunctionTests.cs`: locked tariff + price-only patch + no override → 422, price unchanged, verify via re-fetch from `db`; locked tariff + non-price-only patch → 200, applied, no 422; locked tariff + **mixed** patch (price + `ProviderName`) + no override → 422 returned **but** `ProviderName` is persisted (this is the AC5 "non-price fields succeed regardless" case — the one most likely to be implemented wrong as an all-or-nothing patch); locked tariff + price patch + `LockOverride: true` → 200, price updated; non-locked tariff (no `ContractStartDate`, or `ContractStartDate` in the future, or no `ContractDurationMonths`) + price patch + no override → 200 (lock never applies); 404 tariff not found / wrong flat; 403 wrong owner; 400 bound violations on patch fields
  - [x] Follow the `MakeDb()` / `MakeFunctionContext()` / `SeedFlatAsync()` xUnit + Shouldly + Moq helper pattern from `api.Tests/Features/Readings/PatchReadingFunctionTests.cs` — add a local `SeedTariffAsync(db, flatId, ...)` helper

- [x] Task 10: Self-review checklist pass before marking ready for review
  - [x] Zero/empty-data state checked (empty tariff list) — explicit checklist line added per Epic 3 retro action item
  - [x] No silent `catch {}`, all `SaveChangesAsync` calls use `ct`, tenant scoping present on every query

### Review Findings

- [x] [Review][Patch] `PatchTariffFunction` gates the price-field lock check using pre-patch contract state, allowing a single request to simultaneously set contract-lock terms (`ContractStartDate`+`ContractDurationMonths`) and update price fields in one shot — because `isLocked` is computed at [api/Features/Tariffs/PatchTariffFunction.cs:83] before the non-price fields are mutated at [api/Features/Tariffs/PatchTariffFunction.cs:87-92], a tariff that was unlocked before the request stays "unlocked" for gating purposes even though the same request just gave it retroactive lock terms, bypassing the `lockOverride` requirement entirely. Decision (2026-07-02): reject combined requests — if a PATCH body contains both a price field (`PricePerKwh`/`MonthlyBaseFee`) and a contract-term field (`ContractStartDate`/`ContractDurationMonths`), return 400 Bad Request; require the client to split into two separate requests. Fixed + regression test added (`RunAsync_PriceFieldAndContractTermFieldInSameRequest_Returns400`). [api/Features/Tariffs/PatchTariffFunction.cs:83-90]
- [x] [Review][Patch] Duplicate-`EffectiveDate` check is TOCTOU with no backstop for the unique index — concurrent `POST`s can both pass the `AnyAsync` check at [api/Features/Tariffs/CreateTariffFunction.cs:77] before either commits; the second `SaveChangesAsync` at [api/Features/Tariffs/CreateTariffFunction.cs:97] will violate `IX_Tariffs_FlatId_EffectiveDate` and throw unhandled instead of returning 409, violating AC3's explicit requirement that the collision "never surfaces as an unhandled DB-constraint 500." Fixed — `SaveChangesAsync` wrapped in `try/catch (DbUpdateException)` returning the same 409 Problem Details as the pre-check, following the existing `DbUpdateException` catch pattern used in `api/Features/Settings/UpdateUserSettingsFunction.cs`. No regression test added: the `InMemory` EF Core provider does not enforce the unique index (per this story's own Dev Notes on AC3 testing), so the race cannot be reproduced under the current test setup — same documented limitation as the existing sequential-duplicate test. [api/Features/Tariffs/CreateTariffFunction.cs:96-107]
- [x] [Review][Patch] Inconsistent Guid-parse error message text for the same failure class — `"Invalid flatId format."` in Get/Create vs `"Invalid id format."` in Patch. Fixed — `PatchTariffFunction` now returns `"Invalid flatId or tariffId format."`. [api/Features/Tariffs/PatchTariffFunction.cs:31]
- [x] [Review][Defer] PATCH cannot explicitly clear `ProviderName`/`ContractStartDate`/`ContractDurationMonths` — JSON `null` and an omitted field are indistinguishable in the record-based `PatchTariffRequest`, so mutation is skip-if-null rather than clear-if-null. [api/Features/Tariffs/TariffModels.cs:11-17, api/Features/Tariffs/PatchTariffFunction.cs:87-92] — deferred, pre-existing pattern shared with other `Patch*` functions in this codebase, not required by any AC
- [x] [Review][Defer] No cross-field validation ensuring `ContractStartDate` and `ContractDurationMonths` are supplied together — a tariff can end up with only one set, making `TariffLockPolicy.IsLocked` permanently `false` regardless of intent. [api/Features/Tariffs/TariffValidator.cs, api/Features/Tariffs/PatchTariffValidator.cs] — deferred, not required by any AC, out of scope for this story
- [x] [Review][Defer] Decimal values that pass validator bounds can exceed the DB column's configured scale (`decimal(18,6)`/`decimal(18,4)`), so the immediate POST/PATCH response can diverge from the persisted/rounded value. [api/Features/Tariffs/TariffValidator.cs, api/Features/Tariffs/PatchTariffValidator.cs, api/Data/Configurations/TariffConfiguration.cs] — deferred, systemic gap likely present wherever decimal validators exist in this codebase; needs a broader precision-validation policy decision, not a point fix
- [x] [Review][Defer] No optimistic concurrency control on `Tariff` PATCH updates — two concurrent PATCHes silently last-write-wins with no conflict detection. [api/Features/Tariffs/PatchTariffFunction.cs:102] — deferred, consistent with rest of codebase (no `RowVersion`/ETag pattern anywhere)

## Dev Notes

### Architecture compliance & naming — read this first

- The architecture doc (`architecture.md`) names the PATCH function `UpdateTariffFunction.cs`. **The epic (later revised, more current) names it `PatchTariffFunction`.** Use `PatchTariffFunction` — the epic is the authoritative source, matches this project's actual PATCH-verb naming convention elsewhere (`PatchReadingFunction`, `PatchFlatFunction`), and both AC5 and AC6 reference `PatchTariffFunction` by name explicitly.
- Route verb is `patch` (lowercase) in the `HttpTrigger`, matching `PatchReadingFunction`/`PatchFlatFunction`.
- Do **not** put the contract-lock enforcement logic inside a FluentValidation validator. FluentValidation validators in this codebase are pure/stateless and registered as `AddSingleton` with no DB access (`OnboardingValidator`, `PatchFlatValidator`, `ReadingValidator`, `PatchReadingValidator` are all like this). The lock check needs the *existing* `Tariff` row's `ContractStartDate`/`ContractDurationMonths` from the DB — that belongs in `PatchTariffFunction.RunAsync` itself, after the entity is loaded, exactly as AC5/AC6 describe it ("when `PatchTariffFunction` processes it").

### Reuse — already built, do not recreate

- `api/Data/Entities/Tariff.cs` and `api/Data/Configurations/TariffConfiguration.cs` already exist with the exact shape AC7 requires, including the **unique** `IX_Tariffs_FlatId_EffectiveDate` index (added via migration `MakeTariffEffectiveDateUnique` during the Epic 3 retrospective specifically to unblock this story — the tie-break race that would otherwise exist between two tariffs sharing an `EffectiveDate` is already closed). **Do not add a new migration in this story.**
- `api/Shared/TariffResolver.cs` (`ResolveAsync(flatId, date, ct)`) already exists for period-accurate cost lookups — **not used by this story** (that's Dashboard/Decomposition's concern). Don't wire it in here; don't duplicate its logic either.
- The numeric bound convention (`PricePerKwh` `<10`, `MonthlyBaseFee` `<1000`) already exists in `OnboardingValidator.cs` (`api/Features/Onboarding/OnboardingValidator.cs`) — copy those exact bounds into `TariffValidator`/`PatchTariffValidator`, don't invent new ones.
- `CompleteOnboardingFunction.cs` (`api/Features/Onboarding/CompleteOnboardingFunction.cs`) already creates the very first `Tariff` row per flat during onboarding, with `EffectiveDate = DateTimeOffset.UtcNow` (insertion time, not `ContractStartDate`). This story's `CreateTariffFunction` is a second, independent write path into the same table — the uniqueness constraint is what keeps both paths safe from collision.

### Function skeleton to copy

Copy the auth → flatId-parse → flat-ownership (403) → JSON-deserialize → validate → mutate → respond skeleton verbatim from `api/Features/Readings/SubmitReadingFunction.cs` (for `CreateTariffFunction`) and `api/Features/Readings/PatchReadingFunction.cs` (for `PatchTariffFunction`), and `api/Features/Readings/GetReadingHistoryFunction.cs` (for `GetTariffsFunction`). Key details these three files establish that must carry over exactly:
- `context.GetUserId()` is the very first line inside `RunAsync` (tenant enforcement convention, non-negotiable per project rules).
- `SingleOrDefaultAsync` (not `FirstOrDefaultAsync`) for the flat lookup — it's a PK+unique-scoped lookup.
- 403 body shape: `new ObjectResult(new { title = "Forbidden", status = 403, detail = "Flat not found or access denied." }) { StatusCode = 403 }` — flat-not-found and flat-not-owned are indistinguishable by design (don't leak existence).
- `_jsonOptions` as a `private static readonly` field, `PropertyNameCaseInsensitive = true`, deserialize via `JsonSerializer.DeserializeAsync<T>(req.Body, _jsonOptions, ct)` — never `req.ReadFromJsonAsync<T>()`.
- Validation error shape: `new BadRequestObjectResult(new { title = "Validation Error", status = 400, detail = string.Join("; ", errors) })`.

**Deliberate deviation from the skeleton, required by the ACs:** the 409 (AC3) and 422 (AC5) responses must include a `type` field (Problem Details RFC 9457), unlike the plain `{ title, status, detail }` shape used elsewhere in `Readings`/`Flats` (a known, separately-tracked gap — see `deferred-work.md`, "Error responses missing `type` field"). Follow `CompleteOnboardingFunction.cs`'s 409 exactly for the `type` URI string: `"https://tools.ietf.org/html/rfc9110#section-15.5.10"`. For 422, AC5 gives the literal string `"tariff-locked"` as `type` — not a URL, use it verbatim.

### The AC5 mixed-request case — the one place a naive implementation breaks

Do not implement PATCH as all-or-nothing (validate whole request → if locked, reject everything). AC5's second sentence requires **partial success within a single request**: if the same PATCH body carries both a blocked price change and an allowed non-price change (e.g. `{ pricePerKwh: 0.35, providerName: "NewCo" }` on a locked tariff), the response is 422 (because the price change didn't happen) **but `ProviderName` is still persisted**. Apply non-price fields unconditionally, gate only the two price fields on `lockBlocksPriceUpdate`, then do exactly one `SaveChangesAsync(ct)` covering whatever was actually changed. This exact scenario is called out as a required test case in Task 9 because it's the most likely spot for a plausible-but-wrong all-or-nothing implementation to slip through review.

### `TariffLockPolicy.IsLocked` — one rule, two call sites, not EF-translatable

The lock rule (`ContractStartDate.HasValue && ContractStartDate.Value < DateTimeOffset.UtcNow && ContractDurationMonths.HasValue`) is a single domain invariant used by both `GetTariffsFunction` (to populate `IsLocked` in the list response) and `PatchTariffFunction` (to gate the price-field update). Put it in one static method (`TariffLockPolicy.IsLocked`, co-located in `TariffModels.cs`) so the two call sites can never drift apart. **Do not call it inside an EF Core LINQ `.Select()`** — EF Core cannot translate an arbitrary static method call to SQL and will throw at runtime. `GetTariffsFunction` must `ToListAsync()` first, then map to `TariffResponse` in plain C# afterward (same pattern `GetReadingHistoryFunction` uses for its projection, except that one's projection is simple enough to stay inside `.Select()` — this one is not, because of the method call).

### Testing standards (backend)

- Test placement: `api.Tests/Features/Tariffs/{FunctionName}Tests.cs`, mirroring `api/Features/Tariffs/`.
- `xUnit` + `Shouldly` + `Moq`, `InMemory` EF Core provider — copy the `MakeDb()` / `MakeFunctionContext(userId)` / `SeedFlatAsync(userId)` helpers verbatim from `api.Tests/Features/Readings/PatchReadingFunctionTests.cs`; add a `SeedTariffAsync(db, flatId, ...)` helper following the same shape as that file's `SeedReadingAsync`.
- `InMemory` provider does not enforce the unique index — the 409 test (AC3) must assert on `CreateTariffFunction`'s own `AnyAsync` pre-check returning 409, not rely on a DB-level constraint violation being caught (there is no DB-level enforcement to catch under `InMemory`).
- Do not test `TariffConfiguration.cs` (EF Core config — trust EF Core, per project testing rules).

### Project Structure Notes

- All new files live under `api/Features/Tariffs/`, which currently contains only a `.gitkeep` — this is the first code in that folder.
- No frontend work in this story — Stories 4.2/4.3 build `TariffList.tsx`, `TariffForm.tsx`, `useTariffs.ts`, `useCreateTariff.ts`, `tariffApi.ts` per `architecture.md`'s frontend tree. Nothing in `client/` should change here.
- No new EF Core migration — the schema this story needs already shipped ahead of the epic (see AC7, Task 1).

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-4-tariff-management.md#Story 4.1] — full AC text
- [Source: _bmad-output/planning-artifacts/architecture.md#L685-691] — `Tariffs/` feature folder shape (function naming superseded by epic, see Dev Notes)
- [Source: _bmad-output/planning-artifacts/architecture.md#L394-424] — HTTP status code table, Problem Details shape, JSON/decimal/datetime conventions
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md#FR-10, FR-11, FR-12] — tariff configuration, period-locked prices, future pre-entry requirements
- [Source: _bmad-output/implementation-artifacts/epic-3-retro-2026-07-02.md#Resolved Live During This Retrospective] — unique index + numeric bound precedents this story depends on
- [Source: _bmad-output/implementation-artifacts/deferred-work.md#Deferred from: epic-3-retro] — numeric bounds are team-judgment, not derived; known `type`-field gap in other Functions (not this one, per AC3/AC5)
- [Source: api/Features/Readings/SubmitReadingFunction.cs] — Function skeleton to copy for `CreateTariffFunction`
- [Source: api/Features/Readings/PatchReadingFunction.cs] — Function skeleton to copy for `PatchTariffFunction`
- [Source: api/Features/Flats/PatchFlatValidator.cs], [Source: api/Features/Onboarding/OnboardingValidator.cs] — numeric bound convention to replicate

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

None — implementation proceeded without needing a debug log; all tests passed on first full run after build succeeded.

### Completion Notes List

- Task 1: Verified `Tariff.cs`/`TariffConfiguration.cs` already met AC7 exactly (zero Data Annotations, Fluent API config, unique `IX_Tariffs_FlatId_EffectiveDate` index via migration `20260702121947_MakeTariffEffectiveDateUnique`, correct `decimal(18,6)`/`decimal(18,4)` precision). No entity or migration changes made.
- Tasks 2–8: Implemented `TariffModels.cs` (3 records + `TariffLockPolicy.IsLocked`), `TariffValidator.cs`, `PatchTariffValidator.cs`, `GetTariffsFunction.cs`, `CreateTariffFunction.cs`, `PatchTariffFunction.cs`, and registered both validators as singletons in `Program.cs`. Followed the `SubmitReadingFunction`/`PatchReadingFunction`/`GetReadingHistoryFunction` skeleton verbatim (auth → flatId parse → flat ownership 403 → JSON deserialize → validate → mutate → respond), with the AC3/AC5 deviation of including a Problem Details `type` field on the 409 and 422 responses.
- `GetTariffsFunction` materializes tariffs via `ToListAsync()` before mapping to `TariffResponse` in C#, since `TariffLockPolicy.IsLocked` cannot be translated to SQL inside an EF Core `.Select()`.
- `PatchTariffFunction` implements the AC5 partial-success case: non-price fields are applied unconditionally, price fields are gated on `lockBlocksPriceUpdate`, and exactly one `SaveChangesAsync(ct)` call persists whatever was allowed to change, with a single 422 response covering the blocked case.
- Removed the placeholder `.gitkeep` from `api/Features/Tariffs/` now that real code lives there.
- Task 9: Added `GetTariffsFunctionTests.cs` (8 tests), `CreateTariffFunctionTests.cs` (11 tests), `PatchTariffFunctionTests.cs` (12 tests, including the AC5 mixed-request 422-but-persisted-non-price-field case) — 39 new tests total (parameterized bound-violation tests expand this at execution time), following the `MakeDb()`/`MakeFunctionContext()`/`SeedFlatAsync()` helper pattern from `PatchReadingFunctionTests.cs` plus a local `SeedTariffAsync()` helper.
- Task 10: Self-review checklist passed — no silent `catch {}`, every `SaveChangesAsync` call threads `ct`, every query is tenant-scoped via flat ownership, empty-tariff-list case explicitly tested.
- Full regression suite: `dotnet test api.Tests` → 124 passed, 0 failed (85 pre-existing + 39 new).
- Post-review patches (2026-07-02): rejected combined price+contract-term PATCH requests (400), caught `DbUpdateException` on the duplicate-`EffectiveDate` race in `CreateTariffFunction` (409 backstop for AC3), and unified the Guid-parse error message in `PatchTariffFunction`. Full regression suite: `dotnet test api.Tests` → 127 passed, 0 failed (124 pre-existing + 3 new).

### File List

- `api/Features/Tariffs/TariffModels.cs` (new)
- `api/Features/Tariffs/TariffValidator.cs` (new)
- `api/Features/Tariffs/PatchTariffValidator.cs` (new)
- `api/Features/Tariffs/GetTariffsFunction.cs` (new)
- `api/Features/Tariffs/CreateTariffFunction.cs` (new)
- `api/Features/Tariffs/PatchTariffFunction.cs` (new)
- `api/Features/Tariffs/.gitkeep` (deleted)
- `api/Program.cs` (modified — DI registration)
- `api.Tests/Features/Tariffs/GetTariffsFunctionTests.cs` (new)
- `api.Tests/Features/Tariffs/CreateTariffFunctionTests.cs` (new)
- `api.Tests/Features/Tariffs/PatchTariffFunctionTests.cs` (new)

## Change Log

- 2026-07-02: Implemented Story 4.1 — Tariff CRUD backend (`GetTariffsFunction`, `CreateTariffFunction`, `PatchTariffFunction`), contract-lock enforcement via `TariffLockPolicy`, FluentValidation validators, and 39 new backend tests. All 10 tasks complete; full regression suite green (124/124).
