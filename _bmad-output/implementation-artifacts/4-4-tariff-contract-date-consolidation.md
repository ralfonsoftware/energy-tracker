---
baseline_commit: 58e8b942eb11267e4b1e2bc8512ff5b594d9c255
---

# Story 4.4: Tariff Contract-Date Consolidation

Status: done

## Story

As a user,
I want my tariff's contract start date to be the one field that determines both its cost period and its price lock, without a separate hidden "effective date" I can silently get wrong,
so that entering a pre-existing real contract — including one I'm backdating — produces correct cost coverage from day one, with no unrecoverable mistake possible.

## Acceptance Criteria

1. **Given** the `Tariffs` table and its existing rows, **when** the migration for this story runs, **then** `ContractStartDate` becomes non-nullable; for any existing row where `ContractStartDate` was null, it is backfilled from that row's `EffectiveDate` before the column is dropped; the unique index moves from `IX_Tariffs_FlatId_EffectiveDate` to `IX_Tariffs_FlatId_ContractStartDate`; `EffectiveDate` is removed from the entity and schema entirely.

2. **Given** `TariffResolver.ResolveAsync(flatId, date, ct)` and `KpiCalculator.ResolveTariff` (the second, independently-implemented resolver flagged for logic duplication in the Epic 3 retrospective), **when** either resolves the tariff active on a given date, **then** both select the Tariff with the latest `ContractStartDate` at or before that date; their public signatures are unchanged, so no caller in Epics 6/7 requires modification.

3. **Given** `TariffLockPolicy.IsLocked`, **when** evaluated, **then** it returns `ContractStartDate <= DateTimeOffset.UtcNow` — `ContractDurationMonths` is no longer a factor.

4. **Given** `CreateTariffFunction` and `TariffValidator`, **when** a request is submitted, **then** `ContractStartDate` is required (`NotNull`); the duplicate-date 409 check and unique-index backstop operate on `ContractStartDate`; price/fee bounds are unchanged.

5. **Given** `PatchTariffFunction` and `PatchTariffRequest`, **when** implemented, **then** `ContractStartDate` is removed from the patchable fields entirely (immutable, same treatment `EffectiveDate` previously had); the mutual-exclusion 400 rule added in Story 4.1's review is removed, since the field it protected can no longer be mutated; a single request may combine price fields with `ProviderName`/`ContractDurationMonths` freely, gated only by the existing lock/`LockOverride` rule on the price fields.

6. **Given** `CompleteOnboardingFunction` (Onboarding, unaffected in user-facing behavior per FR-6), **when** the user leaves contract start date blank during Onboarding, **then** it continues to default the (renamed) field to `DateTimeOffset.UtcNow`, identical to its current default-to-insertion-time behavior — only the target field name changes.

7. **Given** `TariffForm.tsx` in edit mode, **when** rendered, **then** `contractStartDate` renders as a read-only label (reusing the pattern already built for the retired `effectiveDate` field); the two-sequential-PATCH-call branching introduced in Story 4.3 (the direct cause of that story's round-2 submit-guard-gap finding) is removed — edit-mode submission is a single `patchTariff` call.

8. **Given** `TariffForm.tsx` in create mode and `TariffList.tsx`, **when** rendered, **then** the field labelled "Gültig ab"/"Effective date" is relabelled to reflect `ContractStartDate` as the single required date field (pre-filled today, editable); `TariffList`'s "upcoming" label and ordering logic key off `ContractStartDate` instead of the retired `EffectiveDate`.

9. **Given** the one real affected tariff record (Ralf's, `ContractStartDate` = 2024-10-01, previously non-authoritative), **when** the migration and backfill complete, **then** the KPI Dashboard's tariff-coverage figures for that Flat show correct coverage for the historical reading period, verified manually post-deploy.

10. **Given** `deferred-work.md`, **when** this story completes, **then** item **W4** and the Story 4.1 cross-field-validation-gap note are marked resolved.

11. **Given** the existing test suites (`GetTariffsFunctionTests`, `CreateTariffFunctionTests`, `PatchTariffFunctionTests`, `TariffResolverTests`, any `KpiCalculator` tests exercising tariff resolution, `TariffForm.test.tsx`, `TariffList.test.tsx`, `TariffLockIndicator.test.tsx`), **when** updated for this story, **then** all references to `EffectiveDate` are replaced with `ContractStartDate`; tests for the removed PATCH mutual-exclusion rule are removed; a new test covers the migration backfill rule (`ContractStartDate = ContractStartDate ?? EffectiveDate` for pre-existing rows); full backend and frontend suites remain green.

## Tasks / Subtasks

### Backend — data layer

- [x] Task 1: Consolidate the `Tariff` entity and `TariffConfiguration` (AC: 1)
  - [x] `api/Data/Entities/Tariff.cs` — remove `public DateTimeOffset EffectiveDate { get; set; }`; change `public DateTimeOffset? ContractStartDate { get; set; }` to `public DateTimeOffset ContractStartDate { get; set; }` (non-nullable).
  - [x] `api/Data/Configurations/TariffConfiguration.cs` — remove the `builder.Property(t => t.EffectiveDate).IsRequired()` line; change `builder.Property(t => t.ContractStartDate).IsRequired(false)` to `.IsRequired()`; replace `builder.HasIndex(t => new { t.FlatId, t.EffectiveDate })...HasDatabaseName("IX_Tariffs_FlatId_EffectiveDate")` with `builder.HasIndex(t => new { t.FlatId, t.ContractStartDate }).IsUnique().HasDatabaseName("IX_Tariffs_FlatId_ContractStartDate")`.

- [x] Task 2: Generate and hand-fix the migration (AC: 1, 9)
  - [x] Run `dotnet ef migrations list` first (project convention) to confirm the current head is `20260702121947_MakeTariffEffectiveDateUnique`.
  - [x] Run `dotnet ef migrations add ConsolidateTariffContractStartDate` from `api/` after Task 1's entity/configuration changes. EF will generate, in some order: `DropIndex(IX_Tariffs_FlatId_EffectiveDate)`, `AlterColumn(ContractStartDate, nullable: false)`, `DropColumn(EffectiveDate)`, `CreateIndex(IX_Tariffs_FlatId_ContractStartDate, unique: true)`.
  - [x] **Critical hand-edit, non-negotiable:** insert `migrationBuilder.Sql("UPDATE Tariffs SET ContractStartDate = EffectiveDate WHERE ContractStartDate IS NULL");` as the **very first statement** in the generated `Up()` method, **before** the `AlterColumn` call that makes `ContractStartDate` non-nullable. SQL Server rejects a nullable→non-nullable `ALTER COLUMN` while NULL rows remain, and this is also the AC1/AC9 backfill rule itself (`ContractStartDate = ContractStartDate ?? EffectiveDate`) — order is both a correctness and a compile-time-safety requirement, not a style choice.
  - [x] If the scaffolded `AlterColumn` includes a `defaultValue:` argument (EF sometimes adds one defensively when narrowing nullability), remove it — the manual backfill above guarantees no NULLs remain, so no default is needed or wanted.
  - [x] `Down()`: mirror the reverse column/index operations (add nullable `EffectiveDate` back, drop the new index, revert `ContractStartDate` to nullable, recreate the old index) — do not attempt to restore historically-overwritten `EffectiveDate` values in `Down()`; this is a one-way data consolidation, consistent with this codebase having no rollback-with-data-recovery precedent anywhere.
  - [x] Verify `AppDbContextModelSnapshot.cs` was regenerated correctly (auto-generated by the `migrations add` command — do not hand-edit).
  - [x] After merge/deploy, manually verify AC9 for the one real affected Flat: dashboard tariff-coverage figures should read correctly once `dotnet ef database update` runs against the real DB.

### Backend — resolvers and lock policy

- [x] Task 3: Re-key `TariffResolver.ResolveAsync` (AC: 2)
  - [x] `api/Shared/TariffResolver.cs:10-12` — change `t.EffectiveDate <= date` → `t.ContractStartDate <= date` and `.OrderByDescending(t => t.EffectiveDate)` → `.OrderByDescending(t => t.ContractStartDate)`.

- [x] Task 4: Re-key `KpiCalculator.ResolveTariff` (AC: 2)
  - [x] `api/Features/Dashboard/KpiCalculator.cs:142-151` (private static `ResolveTariff`) — change `t.EffectiveDate <= date` → `t.ContractStartDate <= date` and `t.EffectiveDate > best.EffectiveDate` → `t.ContractStartDate > best.ContractStartDate`.
  - [x] **Not listed in any AC but required for the app to function end-to-end:** `api/Features/Dashboard/GetDashboardFunction.cs:45` orders the tariffs it hands to `KpiCalculator.Compute` via `.OrderBy(t => t.EffectiveDate)`. `EffectiveDate` won't exist after Task 1 — this line **must** become `.OrderBy(t => t.ContractStartDate)` or the Dashboard function fails to compile. This is the exact class of gap the workflow's Dev Notes are required to catch even when the epic text doesn't name the file explicitly.

- [x] Task 5: Simplify `TariffLockPolicy.IsLocked` (AC: 3)
  - [x] `api/Features/Tariffs/TariffModels.cs` — replace `public static bool IsLocked(DateTimeOffset? contractStartDate, int? contractDurationMonths) => contractStartDate.HasValue && contractStartDate.Value < DateTimeOffset.UtcNow && contractDurationMonths.HasValue;` with `public static bool IsLocked(DateTimeOffset contractStartDate) => contractStartDate <= DateTimeOffset.UtcNow;`
  - [x] **Boundary change, easy to miss:** the old rule used strict `<` (a tariff becoming active *this instant* was not yet locked); AC3 specifies `<=` (on-or-before today locks it). This matches Story 4.1's amended AC1 text ("`ContractStartDate` is on or before today"). Do not silently keep `<`.
  - [x] Update every call site to the new one-argument signature: `GetTariffsFunction.cs`, `CreateTariffFunction.cs`, `PatchTariffFunction.cs` (Tasks 6–8 below).

### Backend — request/response models

- [x] Task 6: Consolidate `TariffModels.cs` records (AC: 4, 5)
  - [x] `CreateTariffRequest` — remove the separate `EffectiveDate` and `ContractStartDate` parameters; the record becomes: `public record CreateTariffRequest(DateTimeOffset? ContractStartDate, decimal PricePerKwh, decimal MonthlyBaseFee, string? ProviderName, int? ContractDurationMonths);` (nullable at the record level so `TariffValidator`'s `NotNull` rule — not a compile-time non-null — produces the 400, matching every other required-field pattern in this codebase, e.g. `ReadingDate` in `SubmitReadingRequest`).
  - [x] `PatchTariffRequest` — remove `ContractStartDateProvided`/`ContractStartDate` entirely (no longer patchable). Becomes: `public record PatchTariffRequest(decimal? PricePerKwh, decimal? MonthlyBaseFee, bool ProviderNameProvided, string? ProviderName, bool ContractDurationMonthsProvided, int? ContractDurationMonths, bool LockOverride);`
  - [x] `TariffResponse` — collapse to one date field: `public record TariffResponse(Guid TariffId, DateTimeOffset ContractStartDate, decimal PricePerKwh, decimal MonthlyBaseFee, string? ProviderName, int? ContractDurationMonths, bool IsLocked);` — field order matches Story 4.1's amended AC1 exactly (`TariffId, ContractStartDate, PricePerKwh, MonthlyBaseFee, ProviderName, ContractDurationMonths, IsLocked`).

- [x] Task 7: Update `TariffValidator.cs` (AC: 4)
  - [x] `RuleFor(r => r.EffectiveDate).NotNull().WithMessage("effectiveDate is required.")` → `RuleFor(r => r.ContractStartDate).NotNull().WithMessage("contractStartDate is required.")`.
  - [x] Price/fee/provider-name/duration bounds are unchanged — do not touch them.
  - [x] `PatchTariffValidator.cs` needs **no changes** — it never had a rule referencing `ContractStartDate`; verify only.

### Backend — Functions

- [x] Task 8: Update `CreateTariffFunction.cs` (AC: 4)
  - [x] `request.EffectiveDate!.Value` → `request.ContractStartDate!.Value` (local var can stay named `effectiveDate` → rename to `contractStartDate` for clarity, not required).
  - [x] Duplicate-check query and `Tariff` construction: `t.EffectiveDate == effectiveDate` → `t.ContractStartDate == contractStartDate`; remove the old separate `ContractStartDate = request.ContractStartDate` assignment (now the same field).
  - [x] `TariffResponse` construction — update to the new 7-field shape (Task 6) and single-arg `TariffLockPolicy.IsLocked(tariff.ContractStartDate)`.
  - [x] The existing `DbUpdateException` catch-and-409 backstop (added in Story 4.1's review, for the TOCTOU race on the unique index) stays as-is — it's index-agnostic.

- [x] Task 9: Update `GetTariffsFunction.cs` (AC: 2, 4)
  - [x] `.OrderByDescending(t => t.EffectiveDate)` → `.OrderByDescending(t => t.ContractStartDate)`.
  - [x] `TariffResponse` projection — update to the new 7-field shape, single-arg `TariffLockPolicy.IsLocked(t.ContractStartDate)`. Keep materializing via `ToListAsync()` before mapping in C# (unchanged reason: `TariffLockPolicy.IsLocked` is still not EF-translatable).

- [x] Task 10: Simplify `PatchTariffFunction.cs` — the core of this story's backend work (AC: 3, 5)
  - [x] Remove the entire `contractStartDate` JSON-parsing block (`obj["contractStartDate"]` / `ContractStartDateProvided` — currently lines ~79–83 and ~102–103). If a stale client sends `contractStartDate` in a PATCH body, it is now silently ignored (not parsed into the request at all) — no AC requires rejecting unknown fields, and every other `Patch*Function` in this codebase already ignores unrecognized keys implicitly.
  - [x] Remove the mutual-exclusion check entirely (currently ~lines 119–126: `priceFieldsRequested && contractTermFieldsRequested` → 400 Bad Request). This is the direct simplification Story 4.4 exists to deliver — the field the rule protected (`ContractStartDate`) can no longer be mutated via PATCH at all, so the race it guarded against is structurally impossible.
  - [x] `isLocked = TariffLockPolicy.IsLocked(tariff.ContractStartDate, tariff.ContractDurationMonths)` → `TariffLockPolicy.IsLocked(tariff.ContractStartDate)` (single arg; `tariff.ContractStartDate` is non-nullable now, no `.Value`/null-check needed).
  - [x] Remove `if (request.ContractStartDateProvided) tariff.ContractStartDate = request.ContractStartDate;` — `ContractStartDate` is never mutated by this function anymore.
  - [x] The rest of the flow is unchanged: apply `ProviderName`/`ContractDurationMonths` unconditionally (still via their `*Provided` flags — this is the explicit-clear semantics Story 4.3's review added, keep it), gate price fields on `lockBlocksPriceUpdate`, one `SaveChangesAsync(ct)`, 422 if blocked else 200 with the new 7-field `TariffResponse`.
  - [x] Net effect for the frontend: a single PATCH request may now legally combine a price-field change with a `ProviderName`/`ContractDurationMonths` change — this is what unblocks Task 14's frontend simplification.

### Backend — Onboarding (behavior-preserving rename)

- [x] Task 11: Update `CompleteOnboardingFunction.cs` (AC: 6)
  - [x] `api/Features/Onboarding/CompleteOnboardingFunction.cs:54-63` currently constructs the first `Tariff` with `EffectiveDate = DateTimeOffset.UtcNow` (always insertion time) and a separate, independently-optional `ContractStartDate = body.ContractStartDate` (can be null). Consolidate to: `ContractStartDate = body.ContractStartDate ?? DateTimeOffset.UtcNow`.
  - [x] `OnboardingModels.cs`'s `CompleteOnboardingRequest.ContractStartDate` stays `DateTimeOffset?` (optional) — no validator change; `OnboardingValidator` has no rule on this field today and none is needed (AC6 explicitly requires blank-stays-legal, defaulting server-side).
  - [x] This preserves exact current behavior for a user who leaves the onboarding contract-start-date field blank (gets "now", same as always) while a user who fills it in now gets that value driving cost resolution from day one (previously it was silently ignored for that purpose — this was the bug class W4 flags).

### Backend — tests

- [x] Task 12: Update backend test suites (AC: 11)
  - [x] `api.Tests/Shared/TariffResolverTests.cs` — `SeedAsync`'s `Tariff` construction (`EffectiveDate = date`) → `ContractStartDate = date`; test names/assertions are already resolver-behavior-agnostic to the field name, no logic changes needed beyond the rename.
  - [x] `api.Tests/Features/Dashboard/KpiCalculatorTests.cs` — the `MakeTariff` test helper (`EffectiveDate = effectiveDate` at line 19) → `ContractStartDate = effectiveDate` (rename the local parameter too if convenient, not required).
  - [x] `api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs` — seeded tariffs' `EffectiveDate = ...` (lines ~115, ~162) → `ContractStartDate = ...`.
  - [x] `api.Tests/Features/Tariffs/GetTariffsFunctionTests.cs` — `SeedTariffAsync` helper's `effectiveDate` parameter and `EffectiveDate = effectiveDate` body field → rename to `contractStartDate`/`ContractStartDate = contractStartDate`; drop the helper's separate `contractStartDate: DateTimeOffset?` optional parameter (now the same parameter, required). Rename `RunAsync_MultipleTariffs_ReturnsDescendingByEffectiveDate` → `...ReturnsDescendingByContractStartDate`. Update the four existing `IsLocked` tests to the new `<=`-only rule: `RunAsync_PastContractStartWithDuration_IsLockedTrue` stays true; `RunAsync_FutureContractStart_IsLockedFalse` stays false; **`RunAsync_PastContractStartNoDuration_IsLockedFalse` must be renamed and its assertion flipped to `..._IsLockedTrue`** — this is the exact bug-fix case (past `ContractStartDate`, no `ContractDurationMonths`, previously wrongly unlocked); **`RunAsync_NoContractStartDate_IsLockedFalse` must be deleted** — a tariff with no `ContractStartDate` can no longer exist (the field is required), so this scenario is unreachable.
  - [x] `api.Tests/Features/Tariffs/CreateTariffFunctionTests.cs` — rename `EffectiveDate`-named tests/fields (`RunAsync_DuplicateEffectiveDate_Returns409AndDoesNotCreate`, `RunAsync_MissingEffectiveDate_Returns400`, `RunAsync_FutureEffectiveDate_Accepted`) to their `ContractStartDate` equivalents; update request bodies to send `contractStartDate` instead of `effectiveDate`.
  - [x] `api.Tests/Features/Tariffs/PatchTariffFunctionTests.cs` — **remove** `RunAsync_PriceFieldAndContractTermFieldInSameRequest_Returns400` and its `PriceAndContractTermCombinations` member-data source (lines ~168–199 — the mutual-exclusion test; the rule it tests no longer exists) and **remove** `RunAsync_ExplicitNullContractStartDateAndDuration_ClearsContractTerms` (tests clearing `ContractStartDate` via explicit null — no longer possible, the field isn't patchable; also drop its `persisted.ContractStartDate.ShouldBeNull()` assertions, which won't compile once the entity property is non-nullable). Update `SeedTariffAsync`'s `EffectiveDate = DateTimeOffset.UtcNow` → `ContractStartDate = ...`; its `contractStartDate` parameter can stay `DateTimeOffset? contractStartDate = null` for caller convenience, but must resolve a concrete value before assigning to the now-non-nullable entity property (e.g. `contractStartDate ?? DateTimeOffset.UtcNow`) — every existing call site that passes an explicit value is unaffected.
  - [x] **Concrete break to fix, not just a pattern to follow:** `RunAsync_NonLockedTariff_PricePatchNoOverride_Returns200`'s `NonLockedTariffCases()` member data (lines ~220–225) currently yields three "not locked" cases: `[null, null]`, `[DateTimeOffset.UtcNow.AddMonths(1), 12]`, and `[DateTimeOffset.UtcNow.AddMonths(-1), null]`. Under the new rule, `[null, null]` can no longer be constructed (no such tariff exists — `ContractStartDate` is required) and **must be deleted**; `[DateTimeOffset.UtcNow.AddMonths(-1), null]` (past start, no duration) is **no longer a non-locked case** — it flips to locked, which is precisely the bug this story fixes — move it to a "locked" test instead of leaving it under `NonLockedTariffCases`. Only `[DateTimeOffset.UtcNow.AddMonths(1), 12]` (and any other future-dated case you add) remains valid here. Add a new explicit test/case proving a past `ContractStartDate` with `ContractDurationMonths: null` is locked (the headline regression case for this story), rather than just relying on the moved case reading correctly.
  - [x] Add a new migration-focused test if the project's testing conventions allow it: since `InMemory` EF Core doesn't run migrations, the backfill rule itself (`ContractStartDate = ContractStartDate ?? EffectiveDate`) cannot be verified via `dotnet test` — instead, add a narrow unit test asserting `TariffLockPolicy.IsLocked`/`TariffResolver`/`KpiCalculator.ResolveTariff` all behave correctly for a `Tariff` row constructed exactly as the backfill would produce one (i.e., `ContractStartDate` populated, no separate `EffectiveDate` concept exists in the entity anymore to diverge from). Document in the PR/completion notes that the actual SQL backfill is verified manually against the one real affected Flat (AC9), not by an automated test — there is no integration-test harness against real SQL Server in this project (a documented, pre-existing gap, see `deferred-work.md`).

### Frontend — types and schema

- [x] Task 13: Consolidate `tariffApi.ts` types (AC: 7, 8)
  - [x] `TariffResponse` — rename `effectiveDate: string` → `contractStartDate: string` (still required, never null); remove the old separate `contractStartDate: string | null`.
  - [x] `CreateTariffRequest` — rename `effectiveDate: string` → `contractStartDate: string` (required); remove the old separate optional `contractStartDate?: string`.
  - [x] `PatchTariffRequest` — remove `contractStartDate?: string | null` entirely (immutable, matches backend Task 6).

- [x] Task 14: Update `tariffSchema.ts` (AC: 8)
  - [x] Remove `effectiveDate: z.string().min(1, 'Required')`.
  - [x] Change `contractStartDate: z.string().optional()` → `contractStartDate: z.string().min(1, 'Required')`.

### Frontend — components

- [x] Task 15: Rework `TariffForm.tsx` — create mode (AC: 8)
  - [x] Remove the "Effective date" field block (lines ~230–251) and the separate "Contract start date — optional" field block (lines ~332–346, including its `optional` tag). Replace with **one** required date field bound to `register('contractStartDate')`, pre-filled `todayIsoDate()` in create mode, labelled via `t('form.contractStartDate')` (repurposing the existing "Contract Start Date"/"Vertragsbeginn" translation — see Task 18), with the same validation-error display pattern the old `effectiveDate` block used (`touchedFields.contractStartDate && errors.contractStartDate`).
  - [x] `defaultValues` (create mode): replace `effectiveDate: todayIsoDate(), ..., contractStartDate: ''` with a single `contractStartDate: todayIsoDate()`.
  - [x] `onSubmitCreate`: replace `effectiveDate: \`${data.effectiveDate}T00:00:00Z\`` and the separate optional `contractStartDate: data.contractStartDate ? ... : undefined` with one required `contractStartDate: \`${data.contractStartDate}T00:00:00Z\``.

- [x] Task 16: Rework `TariffForm.tsx` — edit mode (AC: 7)
  - [x] `defaultValues` (edit mode): replace `effectiveDate: toLocalDateInputValue(tariff.effectiveDate), ..., contractStartDate: tariff.contractStartDate ? toLocalDateInputValue(tariff.contractStartDate) : ''` with `contractStartDate: toLocalDateInputValue(tariff.contractStartDate)` (always present, never empty-string fallback needed).
  - [x] Read-only label rendering: `t('form.effectiveDateReadonly', { date: formatDate(tariff.effectiveDate) })` → `t('form.contractStartDateReadonly', { date: formatDate(tariff.contractStartDate) })` (new key, see Task 18). `formatDate` itself is unchanged (already uses `toLocalDateInputValue` internally per Story 4.3's round-1 timezone fix — do not reintroduce `new Date(isoDate)` + default-timezone formatting).
  - [x] **Core simplification — single PATCH call.** Replaced `onSubmitEdit`'s two sequential `await patchMutateAsync(...)` calls with **one** call combining whatever is dirty, matching the story's reference snippet verbatim. This is legal now because `PatchTariffFunction` (Task 10) no longer rejects a request combining price and non-price fields.
  - [x] Remove `isEditSequenceSubmitting` state and its `try/finally` wrapper (it existed solely to bridge the gap *between* the two sequential calls Story 4.3's round-2 review found — with one call, `usePatchTariff`'s own `isPending` already covers the whole submission with no gap). Removed it from the `isPending`/`isSaveEnabled` OR-conditions accordingly.
  - [x] `contractFieldsDirty` (used for `isEditSaveEnabled`'s dirty check) drops `dirtyFields.contractStartDate` from its OR-list — that field is read-only in edit mode and can never become dirty; keeps `dirtyFields.providerName || dirtyFields.contractDurationMonths`.
  - [x] Lock indicator render condition: `isLockedAndNotOverridden && tariff?.contractStartDate && tariff.contractDurationMonths` (line ~297) → `isLockedAndNotOverridden && tariff?.contractStartDate` (drop the `contractDurationMonths` requirement — `contractStartDate` is always truthy now, and the indicator itself handles the duration-present-vs-absent branching, see Task 17). Passes `contractDurationMonths={tariff.contractDurationMonths}` through unchanged (now `number | null`, not required-truthy).

- [x] Task 17: Update `TariffLockIndicator.tsx` for the duration-optional label (AC: covers Story 4.3's amended AC1, implemented here per that story's amendment note)
  - [x] Change the `contractDurationMonths` prop type from `number` to `number | null`.
  - [x] When `contractDurationMonths` is provided: keep the existing "locked until" computation (`contractStartDate` + N months via local `Date` getters, unchanged timezone-safe logic) and render `t('form.lockedLabel', { date })`.
  - [x] When `contractDurationMonths` is `null`: skip the month-addition entirely and format `contractStartDate` itself (still via local `Date` getters, never `toISOString()`) as the "locked since" date, rendering a new key `t('form.lockedSinceLabel', { date })`.

### Frontend — `TariffList.tsx`

- [x] Task 18: Re-key `TariffList.tsx` from `effectiveDate` to `contractStartDate` (AC: 8)
  - [x] `activeTariff = (data ?? []).find(tariff => !isUpcoming(tariff.effectiveDate))` → `!isUpcoming(tariff.contractStartDate)`.
  - [x] `TariffRow`: `isUpcoming(tariff.effectiveDate)` → `isUpcoming(tariff.contractStartDate)`; `formatDate(tariff.effectiveDate)` → `formatDate(tariff.contractStartDate)` (both occurrences, upcoming-label and non-upcoming display).
  - [x] `toUtcDateString`/`isUpcoming`/`todayLocalDateString` helper functions are unchanged in implementation — only their call-site argument changes from `tariff.effectiveDate` to `tariff.contractStartDate`. Left their UTC-vs-local logic untouched.
  - [x] Backend already returns tariffs pre-sorted descending by `ContractStartDate` (Task 9) — `TariffList` does not re-sort, no change needed there.

### i18n

- [x] Task 19: Update `client/src/locales/{en-US,de-DE}/tariffs.json` (AC: 7, 8)
  - [x] Remove `form.effectiveDate` and `form.effectiveDateReadonly` keys.
  - [x] Add `form.contractStartDateReadonly`: en-US `"Contract start {{date}}"`, de-DE `"Vertragsbeginn {{date}}"` (mirrors the retired `effectiveDateReadonly` pattern exactly).
  - [x] Reuse the existing `form.contractStartDate` key ("Contract Start Date"/"Vertragsbeginn") as the label for the now-single required field in create mode — no text change needed, just a new consumer.
  - [x] Add `form.lockedSinceLabel`: en-US `"Locked — contract active since {{date}}"`, de-DE `"Gesperrt — Vertrag aktiv seit {{date}}"` (voice-and-tone matches the existing `lockedLabel` entry, just "since" instead of "until").
  - [x] No other key changes — `list.upcomingLabel`, `budget.*`, and all other existing keys are untouched.

### Frontend — tests

- [x] Task 20: Update frontend test suites (AC: 11)
  - [x] `client/src/features/tariffs/components/TariffForm.test.tsx` — replaced all `effectiveDate` fixture/assertion references with `contractStartDate`; **removed** the test asserting edit-mode submission sends **two sequential** `patchTariff` calls when both a price field and a contract-term field are dirty — replaced with `TariffForm_PriceAndContractTermBothDirty_SendsSingleCallWithBothCategories` asserting it sends **exactly one** `patchTariff` call containing both categories in the same body. Kept (adapted) the existing price-only-dirty and non-price-only-dirty single-call cases; fixed their assertions from `not.toHaveProperty` to `.toBeUndefined()` since the reference implementation now always includes all keys (some explicitly `undefined`).
  - [x] `client/src/features/tariffs/components/TariffList.test.tsx` — replaced `effectiveDate` fixtures with `contractStartDate`; upcoming/ordering/tap-to-edit assertions unchanged in behavior.
  - [x] `client/src/features/tariffs/components/TariffLockIndicator.test.tsx` — extended: existing test (duration provided → "locked until") stays; added `TariffLockIndicator_NoDuration_RendersLockedSinceLabelUsingContractStartDate` for `contractDurationMonths: null` asserting the `lockedSinceLabel` text renders using `contractStartDate` directly via the local-date-parts computation.
  - [x] `client/src/features/tariffs/hooks/useCreateTariff.test.ts` — updated `effectiveDate`-keyed request-body/response fixtures to `contractStartDate`; invalidation-key assertions unchanged.
  - [x] `client/src/features/tariffs/hooks/usePatchTariff.test.ts` — updated the `sampleResponse` fixture's `effectiveDate` → `contractStartDate`; no request-body fixtures needed updating (thin passthrough hook).
  - [x] `client/src/features/tariffs/hooks/useTariffs.test.ts` — not explicitly named in this task but found via self-review grep to still reference `effectiveDate`; updated its fixture to `contractStartDate` (would otherwise fail `tsc -b`).

### Cross-cutting — deferred work and self-review

- [x] Task 21: Update `deferred-work.md` (AC: 10)
  - [x] Under "Deferred from: code review of 2-4-onboarding-step-2-energy-contract-and-completion", marked **W4** resolved with a strikethrough + resolution note, pointing at this story and `CompleteOnboardingFunction.cs`'s consolidated default (Task 11).
  - [x] Under "Deferred from: code review of story-4.1", marked the "No cross-field validation ensuring `ContractStartDate` and `ContractDurationMonths` are supplied together" item resolved — `TariffLockPolicy.IsLocked` no longer depends on `ContractDurationMonths` at all (Task 5).
  - [x] Left the story-4.1 item about PATCH not being able to explicitly clear `ProviderName`/`ContractDurationMonths` untouched, as instructed.

- [x] Task 22: Self-review checklist pass before marking ready for review
  - [x] Grepped the full `api/` and `client/src/` trees for any remaining `EffectiveDate`/`effectiveDate` reference outside test/migration files — zero hits; `GetDashboardFunction.cs:45` verified updated to `.OrderBy(t => t.ContractStartDate)`.
  - [x] Verified `TariffLockPolicy.IsLocked` uses `<=`, not `<`.
  - [x] Verified the migration's backfill `Sql()` call executes before the `AlterColumn` narrowing `ContractStartDate` to non-nullable.
  - [x] Verified no PATCH request body constructed anywhere in `TariffForm.tsx` includes a `contractStartDate` key.
  - [x] `dotnet build` and `dotnet test api.Tests` are green (134/134 passing); `npx tsc -b`, `npx vitest run` (161/161 passing), `npm run lint` are clean in `client/`.

### Review Findings

- [x] [Review][Patch] Make PATCH atomic when price is blocked [api/Features/Tariffs/PatchTariffFunction.cs:116-138] — fixed: the `422` check now runs before any field mutation or `SaveChangesAsync`, so a blocked price update persists nothing (including `ProviderName`/`ContractDurationMonths`). Updated the pre-existing `RunAsync_LockedTariff_MixedPatchNoOverride_Returns422ButPersistsNonPriceField` test (renamed to `...Returns422AndPersistsNothing`) to assert the new atomic behavior.
- [x] [Review][Defer] Migration backfill doesn't deduplicate pre-existing non-null `ContractStartDate` values before creating the unique index [api/Data/Migrations/20260703114416_ConsolidateTariffContractStartDate.cs:17-40] — deferred, verify manually. `UPDATE Tariffs SET ContractStartDate = EffectiveDate WHERE ContractStartDate IS NULL` only fixes rows where `ContractStartDate` was `NULL`; pre-existing non-null duplicates (previously unconstrained) aren't deduplicated before `CreateIndex(..., unique: true)`. Reason for deferring: real dataset is tiny (Dev Notes cite only "the one real affected Flat"), so manual verification before running `dotnet ef database update` — already planned per Task 2/AC9 — is sufficient; no defensive migration code added.
- [x] [Review][Patch] Migration `Down()` recreates a UNIQUE index that the lossy rollback data can't satisfy [api/Data/Migrations/20260703114416_ConsolidateTariffContractStartDate.cs] — fixed: `Down()`'s recreated `IX_Tariffs_FlatId_EffectiveDate` index is no longer `unique`, since the single hardcoded `defaultValue` (`0001-01-01`) applied to every row on rollback structurally cannot satisfy a unique constraint for any flat with more than one tariff.
- [x] [Review][Patch] Stale "effective date" wording left in `CreateTariffFunction`'s 409 response [api/Features/Tariffs/CreateTariffFunction.cs:82,106] — fixed: both conflict `detail` messages now read "A tariff with this contract start date already exists for this flat."
- [x] [Review][Patch] Leftover `EffectiveDate`-named test method not renamed [api.Tests/Shared/TariffResolverTests.cs] — fixed: renamed `ResolveAsync_DateOnEffectiveDate_ReturnsTariff` to `ResolveAsync_DateOnContractStartDate_ReturnsTariff`.
- [x] [Review][Patch] No test coverage for explicitly clearing `ContractDurationMonths` to `null` via PATCH [api.Tests/Features/Tariffs/PatchTariffFunctionTests.cs] — fixed: added `RunAsync_ExplicitNullContractDurationMonths_ClearsContractDurationMonths`.
- [x] [Review][Patch] `NonLockedTariffCases` theory reduced to a single hardcoded case [api.Tests/Features/Tariffs/PatchTariffFunctionTests.cs] — fixed: simplified `RunAsync_NonLockedTariff_PricePatchNoOverride_Returns200` to a plain `[Fact]`, removed the now-pointless `[Theory]`/`[MemberData]` machinery.
- [x] [Review][Defer] Migration is a single unbatched schema rewrite [api/Data/Migrations/20260703114416_ConsolidateTariffContractStartDate.cs] — deferred, pre-existing — drops a column, narrows another to `NOT NULL`, and creates a unique index in one `Up()`, preceded by an unindexed raw `UPDATE`. Fine at this project's current tiny data scale; would need batching/online-index consideration at real scale. This follows the same unbatched pattern as every prior migration in this codebase (no migration here has ever considered batching) — systemic, not introduced by this story specifically.

## Dev Notes

### Why this story exists — read this first

This is a **bug fix disguised as a refactor**: the Tariff data model has always carried two separate temporal fields (`EffectiveDate`, driving cost resolution and defaulting silently to "today"; `ContractStartDate`, driving only the price lock, optional). A user backdating a real contract naturally fills in `ContractStartDate` — the field that reads as "when my contract started" — and leaves `EffectiveDate` at its create-time default, since nothing in the UI signals the two are different or that only one of them drives cost math. Once created, `EffectiveDate` was immutable (no PATCH field, no DELETE endpoint), so the mistake was permanent. This surfaced as a live defect during the Epic 4 retrospective (2026-07-03): a genuine 2024-10-01 contract showed "0 of 185 days covered." This story consolidates the two fields into the one real field (`ContractStartDate`) that already matched user intent, and — because the two-field split was also the direct cause of two separate code-review findings in Stories 4.1 and 4.3 (the PATCH mutual-exclusion rule, and the two-sequential-PATCH-call frontend branching) — removing it simplifies both.

Full root-cause narrative: [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-03.md#1. Issue Summary]

### Architecture compliance — already applied to the docs, code must catch up

`architecture.md` and the PRD (`prd-energy-tracker-2026-06-20/prd.md`, FR-10/11/12 + two Glossary entries) were already edited directly as part of the sprint-change-proposal approval — they now describe the target state this story must implement, not the current code. Do not re-edit them; just make the code match what they already say:
- [Source: _bmad-output/planning-artifacts/architecture.md#Data Model] — `Tariffs` row: `ContractStartDate` (datetimeoffset, required, unique per Flat); `ContractDurationMonths` annotated "informational only, no locking effect."
- [Source: _bmad-output/planning-artifacts/architecture.md] — naming convention example updated to `IX_Tariffs_FlatId_ContractStartDate`; `TariffResolver`'s doc comment now notes it resolves by `ContractStartDate`.
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md#FR-10,#FR-11,#FR-12] — target functional-requirement text this story's behavior must satisfy.

### Every current call site that must change — verified by direct file read, not by epic text alone

The epic's AC text names the main files, but two call sites only surface by actually reading the code (per this workflow's standing rule that a story must leave the system working end-to-end, not just satisfy its literally-stated ACs):
- `api/Features/Dashboard/GetDashboardFunction.cs:45` — `.OrderBy(t => t.EffectiveDate)` feeding `KpiCalculator.Compute`. Not named in any Story 4.4 AC, but the Dashboard function will not compile once `EffectiveDate` is removed from the entity (Task 1) if this line isn't updated (Task 4).
- `api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs` — seeds tariffs with `EffectiveDate = ...`; will not compile either.

Full list of every file referencing `EffectiveDate` today (grepped directly, use this as your checklist for Task 22's self-review, not as a substitute for grepping again after your own changes):
`api/Data/Entities/Tariff.cs`, `api/Data/Configurations/TariffConfiguration.cs`, `api/Shared/TariffResolver.cs`, `api/Features/Tariffs/TariffModels.cs`, `api/Features/Tariffs/TariffValidator.cs`, `api/Features/Tariffs/GetTariffsFunction.cs`, `api/Features/Tariffs/CreateTariffFunction.cs`, `api/Features/Tariffs/PatchTariffFunction.cs` (already has no `EffectiveDate` field in `PatchTariffRequest` today — only the request/response construction lines reference `tariff.EffectiveDate`), `api/Features/Dashboard/KpiCalculator.cs`, `api/Features/Dashboard/GetDashboardFunction.cs`, `api/Features/Onboarding/CompleteOnboardingFunction.cs`, plus the four test files named in Task 12 and `client/src/features/tariffs/components/TariffForm.test.tsx`. Migration files under `api/Data/Migrations/` referencing `EffectiveDate` (`20260629135534_AddTariffsTable.cs`, `20260702121947_MakeTariffEffectiveDateUnique.cs`, their `.Designer.cs` pairs, `AppDbContextModelSnapshot.cs`) are historical — **do not edit them**; your new migration (Task 2) is additive on top of migration history, exactly like `MakeTariffEffectiveDateUnique` was additive on top of `AddTariffsTable`.

### The lock-rule boundary change is easy to miss

Old: `contractStartDate.HasValue && contractStartDate.Value < DateTimeOffset.UtcNow && contractDurationMonths.HasValue` (strict `<`, and required `ContractDurationMonths`). New (AC3): `contractStartDate <= DateTimeOffset.UtcNow` (inclusive, no duration dependency). Two behavior changes bundled into one rule change:
1. A tariff whose `ContractStartDate` is exactly "now" is now locked (previously it wasn't, by one tick).
2. A tariff with a past `ContractStartDate` and **no** `ContractDurationMonths` is now locked (previously it was never locked without a duration — this exact gap, "no cross-field validation... makes `TariffLockPolicy.IsLocked` permanently false," is the Story 4.1 deferred-work item this story resolves per AC10/Task 21).

### `PatchTariffFunction`'s JSON-body parsing pattern — don't revert to `JsonSerializer.DeserializeAsync`

Story 4.3's second review round fixed a case-insensitivity regression when this function's body parsing was rewritten from `JsonSerializer.DeserializeAsync<T>` to manual `JsonNode.Parse` (needed for the explicit-null-vs-omitted-field "Provided" flag semantics `ProviderName`/`ContractDurationMonths` rely on). Keep the current `JsonNode.Parse(body, new JsonNodeOptions { PropertyNameCaseInsensitive = true })` pattern — you are deleting the `contractStartDate` parsing block from it (Task 10), not replacing the parsing approach itself.

### Frontend: what "single PATCH call" unlocks, and what it doesn't change

Task 16's simplification is possible **only** because Task 10 removes the backend's mutual-exclusion 400. Do not attempt the reverse order (simplify the frontend before the backend accepts combined requests) — that would break edit-mode submission entirely in an intermediate state. If you implement backend and frontend in the same PR (expected for this story), sequence backend Tasks 1–12 before frontend Tasks 13–20 so you can manually verify the combined-PATCH behavior via a quick backend test run before touching the frontend.

`lockOverride` semantics are unchanged: only meaningful on the price-field portion of the request; still safe to include unconditionally in the single combined body (backend ignores it when no price fields are present, per Task 10 — verify this behavior is preserved, since the mutual-exclusion removal touches the same code region).

### Testing standards (backend)

- Test placement: `api.Tests/Features/Tariffs/{FunctionName}Tests.cs`, `api.Tests/Shared/TariffResolverTests.cs`, `api.Tests/Features/Dashboard/KpiCalculatorTests.cs` / `GetDashboardFunctionTests.cs` — all existing files, no new ones needed for this story's backend scope.
- `InMemory` EF Core provider cannot execute or verify the migration itself (no migrations run against `InMemory`) — the backfill SQL (Task 2) is verified manually against the one real affected Flat (AC9), not by an automated test. This is consistent with this project's existing, documented gap (no SQLite/real-SQL integration tests) — do not attempt to spin up a real SQL Server instance for this story.
- Do not test `TariffConfiguration.cs` (EF Core config — trust EF Core, per project testing rules).

### Testing standards (frontend)

- Vitest, `globals: true`, co-located `.test.tsx`/`.test.ts`, `jsdom` environment, mock `react-i18next` per test, mock API modules not `apiClient`.
- Query by role/label/text, not CSS class or `data-testid`.

### Project Structure Notes

- No new files or folders — every change in this story modifies an existing file. This is a pure consolidation/simplification story: `api/Features/Tariffs/`, `api/Shared/`, `api/Features/Dashboard/`, `api/Features/Onboarding/`, `api/Data/Entities/`, `api/Data/Configurations/`, `client/src/features/tariffs/` all keep their current shape.
- One new file is expected: the EF Core migration pair (`api/Data/Migrations/{timestamp}_ConsolidateTariffContractStartDate.cs` + `.Designer.cs`), generated by `dotnet ef migrations add`, plus a regenerated `AppDbContextModelSnapshot.cs`.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-4-tariff-management.md#Story 4.4] — full AC text (this story's authoritative source)
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-03.md] — root cause, impact analysis, and approved change scope
- [Source: _bmad-output/planning-artifacts/architecture.md#Data Model, #Naming Conventions] — already-updated target schema/naming this story implements
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md#FR-10,#FR-11,#FR-12,#Glossary] — already-updated target functional requirements
- [Source: api/Data/Entities/Tariff.cs, api/Data/Configurations/TariffConfiguration.cs] — current two-field entity/config to consolidate
- [Source: api/Data/Migrations/20260702121947_MakeTariffEffectiveDateUnique.cs] — precedent migration shape (index-only) to follow structurally for the new migration
- [Source: api/Shared/TariffResolver.cs] — resolver to re-key (Task 3)
- [Source: api/Features/Dashboard/KpiCalculator.cs#L142-151,#L45 via GetDashboardFunction.cs] — second resolver + the not-explicitly-named `GetDashboardFunction.cs:45` call site (Task 4)
- [Source: api/Features/Tariffs/TariffModels.cs] — `TariffLockPolicy.IsLocked`, all three records to consolidate (Tasks 5, 6)
- [Source: api/Features/Tariffs/TariffValidator.cs, PatchTariffValidator.cs] — validator updates (Task 7)
- [Source: api/Features/Tariffs/CreateTariffFunction.cs, GetTariffsFunction.cs, PatchTariffFunction.cs] — Function updates (Tasks 8, 9, 10)
- [Source: api/Features/Onboarding/CompleteOnboardingFunction.cs, OnboardingModels.cs] — onboarding default-value consolidation (Task 11)
- [Source: api.Tests/Shared/TariffResolverTests.cs, api.Tests/Features/Dashboard/KpiCalculatorTests.cs, GetDashboardFunctionTests.cs, api.Tests/Features/Tariffs/*.cs] — existing tests to update/remove (Task 12)
- [Source: client/src/features/tariffs/api/tariffApi.ts, schemas/tariffSchema.ts] — type/schema consolidation (Tasks 13, 14)
- [Source: client/src/features/tariffs/components/TariffForm.tsx] — full create/edit rework, single-PATCH-call simplification (Tasks 15, 16)
- [Source: client/src/features/tariffs/components/TariffLockIndicator.tsx] — duration-optional label logic (Task 17)
- [Source: client/src/features/tariffs/components/TariffList.tsx] — field rename (Task 18)
- [Source: client/src/locales/en-US/tariffs.json, de-DE/tariffs.json] — i18n key updates (Task 19)
- [Source: client/src/features/tariffs/components/TariffForm.test.tsx, TariffList.test.tsx, TariffLockIndicator.test.tsx, hooks/useCreateTariff.test.ts, usePatchTariff.test.ts] — frontend test updates (Task 20)
- [Source: _bmad-output/implementation-artifacts/deferred-work.md#Deferred from: code review of 2-4-onboarding-step-2-energy-contract-and-completion (W4), #Deferred from: code review of story-4.1] — items to mark resolved (Task 21)
- [Source: _bmad-output/implementation-artifacts/4-1-tariff-crud-backend-list-create-and-contract-lock-enforcement.md] — original `TariffLockPolicy`/mutual-exclusion implementation and its review history
- [Source: _bmad-output/implementation-artifacts/4-3-tariff-lock-indicator-and-planned-annual-spend-settings.md] — `TariffLockIndicator`, two-sequential-PATCH-call implementation and its round-2 review (the submit-guard-gap finding this story's simplification structurally eliminates)
- [Source: _bmad-output/project-context.md#EF Core, #Critical Don't-Miss Rules] — `DateTimeOffset` invariant, `decimal` invariant, tenant-scoping, migration-ordering convention (`dotnet ef migrations list` before adding)

## Dev Agent Record

### Agent Model Used

Claude Opus 4.8

### Debug Log References

- `dotnet ef migrations list` (api/) confirmed head = `20260702121947_MakeTariffEffectiveDateUnique` before scaffolding.
- `dotnet ef migrations add ConsolidateTariffContractStartDate` scaffolded `DropIndex → DropColumn(EffectiveDate) → AlterColumn(non-null, with a defensive `defaultValue`) → CreateIndex`; hand-edited to prepend the backfill `Sql()` call before any of those (had to reorder before `DropColumn` too, not just before `AlterColumn`, since the backfill SQL needs `EffectiveDate` to still exist) and removed the scaffolded `defaultValue`.
- `dotnet build` (api/) green after Task 1 alone, confirming the entity/config change compiles standalone before the migration exists.
- `dotnet test api.Tests` — one failure surfaced after Task 12 (`PatchTariffFunctionTests.RunAsync_ProviderNameNotInBody_LeavesExistingProviderNameUnchanged`): its `SeedTariffAsync` default (`ContractStartDate` defaults to "now") is locked under the new inclusive `<=` rule, so the price-patch-adjacent assertion tripped a 422 instead of 200. Fixed by seeding that test with a future `ContractStartDate` (its intent — providerName survives an unrelated patch — is orthogonal to locking). 134/134 green after.
- `npx vitest run` on `TariffForm.test.tsx` surfaced a second boundary ripple: the story's own Task 16 reference snippet always includes all `PatchTariffRequest` keys (some explicitly `undefined` rather than omitted), so pre-existing `not.toHaveProperty('pricePerKwh')`-style assertions fail (`toHaveProperty` treats an explicit `undefined` value as "property present"). Verified via a throwaway probe test, then fixed the two affected assertions to `.toBeUndefined()` checks instead. 161/161 green after.

### Completion Notes List

- Backend: consolidated `Tariff.EffectiveDate`/`ContractStartDate` into a single non-nullable `ContractStartDate`; re-keyed `TariffResolver`, `KpiCalculator.ResolveTariff` (+ its caller `GetDashboardFunction.cs:45`, not named in any AC but required for compilation), `GetTariffsFunction`, `CreateTariffFunction`; simplified `TariffLockPolicy.IsLocked` to a single-argument, inclusive (`<=`) rule with no `ContractDurationMonths` dependency; removed the PATCH mutual-exclusion rule and `contractStartDate` patchability entirely from `PatchTariffFunction`; consolidated `CompleteOnboardingFunction`'s onboarding default to `ContractStartDate = body.ContractStartDate ?? DateTimeOffset.UtcNow`.
- Migration `ConsolidateTariffContractStartDate`: backfill `UPDATE Tariffs SET ContractStartDate = EffectiveDate WHERE ContractStartDate IS NULL` runs first (before `DropColumn`/`AlterColumn`), `defaultValue:` removed from the scaffolded `AlterColumn`, `Down()` left as scaffolded (adds `EffectiveDate` back nullable, no historical-value restoration, per Dev Notes). AC9 (the one real affected Flat's dashboard coverage) is a manual post-deploy verification, not covered by an automated test — no SQL Server integration harness exists in this project (pre-existing, documented gap).
- Frontend: `TariffForm.tsx` create mode now has one required `contractStartDate` field (was two: a required "effective date" and an optional "contract start date"); edit mode renders it read-only and submits via a single combined `patchTariff` call (was two sequential calls — the direct cause of Story 4.3's round-2 submit-guard-gap finding); `isEditSequenceSubmitting` state removed entirely. `TariffLockIndicator` now branches on `contractDurationMonths` being `null` vs a number, rendering a new "locked since" label when absent.
- Two boundary-change ripples not named in any task/AC were found and fixed via direct test-run failures (both logged above): a `PatchTariffFunctionTests` case whose seed defaulted to a now-locked tariff, and two `TariffForm.test.tsx` assertions incompatible with the reference snippet's explicit-`undefined` object-literal pattern.
- `deferred-work.md`: W4 (onboarding `EffectiveDate` insertion-time gap) and the Story 4.1 cross-field-validation gap both marked resolved with strikethrough + resolution notes; the unrelated story-4.1 PATCH-explicit-clear item left untouched per Dev Notes instruction.
- Full verification: `dotnet build` and `dotnet test api.Tests` (134/134) green; `npx tsc -b`, `npx vitest run` (161/161), and `npm run lint` (pre-existing unrelated `router.tsx` warnings only) all clean in `client/`.

### File List

**Backend — production code**
- `api/Data/Entities/Tariff.cs`
- `api/Data/Configurations/TariffConfiguration.cs`
- `api/Data/Migrations/20260703114416_ConsolidateTariffContractStartDate.cs` (new)
- `api/Data/Migrations/20260703114416_ConsolidateTariffContractStartDate.Designer.cs` (new)
- `api/Data/Migrations/AppDbContextModelSnapshot.cs`
- `api/Shared/TariffResolver.cs`
- `api/Features/Dashboard/KpiCalculator.cs`
- `api/Features/Dashboard/GetDashboardFunction.cs`
- `api/Features/Tariffs/TariffModels.cs`
- `api/Features/Tariffs/TariffValidator.cs`
- `api/Features/Tariffs/CreateTariffFunction.cs`
- `api/Features/Tariffs/GetTariffsFunction.cs`
- `api/Features/Tariffs/PatchTariffFunction.cs`
- `api/Features/Onboarding/CompleteOnboardingFunction.cs`

**Backend — tests**
- `api.Tests/Shared/TariffResolverTests.cs`
- `api.Tests/Features/Dashboard/KpiCalculatorTests.cs`
- `api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs`
- `api.Tests/Features/Tariffs/GetTariffsFunctionTests.cs`
- `api.Tests/Features/Tariffs/CreateTariffFunctionTests.cs`
- `api.Tests/Features/Tariffs/PatchTariffFunctionTests.cs`

**Frontend — production code**
- `client/src/features/tariffs/api/tariffApi.ts`
- `client/src/features/tariffs/schemas/tariffSchema.ts`
- `client/src/features/tariffs/components/TariffForm.tsx`
- `client/src/features/tariffs/components/TariffLockIndicator.tsx`
- `client/src/features/tariffs/components/TariffList.tsx`
- `client/src/locales/en-US/tariffs.json`
- `client/src/locales/de-DE/tariffs.json`

**Frontend — tests**
- `client/src/features/tariffs/components/TariffForm.test.tsx`
- `client/src/features/tariffs/components/TariffList.test.tsx`
- `client/src/features/tariffs/components/TariffLockIndicator.test.tsx`
- `client/src/features/tariffs/hooks/useCreateTariff.test.ts`
- `client/src/features/tariffs/hooks/usePatchTariff.test.ts`
- `client/src/features/tariffs/hooks/useTariffs.test.ts`

**Docs / process**
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- Consolidated `Tariff.EffectiveDate`/`ContractStartDate` into a single non-nullable `ContractStartDate`; migration backfills pre-existing rows before narrowing the column (Date: 2026-07-03)
- Re-keyed `TariffResolver`, `KpiCalculator.ResolveTariff`, `GetDashboardFunction`, `GetTariffsFunction`, `CreateTariffFunction` from `EffectiveDate` to `ContractStartDate` (Date: 2026-07-03)
- Simplified `TariffLockPolicy.IsLocked` to a single-argument, inclusive (`<=`) rule with no `ContractDurationMonths` dependency — fixes the bug where a past `ContractStartDate` with no duration was never locked (Date: 2026-07-03)
- Removed `PatchTariffFunction`'s mutual-exclusion rule and `contractStartDate` patchability entirely — `ContractStartDate` is now immutable after creation (Date: 2026-07-03)
- Consolidated `CompleteOnboardingFunction`'s tariff default to `ContractStartDate = body.ContractStartDate ?? DateTimeOffset.UtcNow` (Date: 2026-07-03)
- Reworked `TariffForm.tsx` to a single required contract-start-date field (create mode) / read-only label (edit mode), and a single combined PATCH call replacing the two-sequential-call pattern from Story 4.3 (Date: 2026-07-03)
- Extended `TariffLockIndicator.tsx` to render a "locked since" label when `contractDurationMonths` is absent (Date: 2026-07-03)
- Marked W4 (onboarding `EffectiveDate` gap) and the Story 4.1 cross-field-validation gap resolved in `deferred-work.md` (Date: 2026-07-03)
